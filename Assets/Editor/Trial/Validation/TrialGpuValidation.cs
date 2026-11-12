using System;
using System.Collections.Generic;
using UnityEditor;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TomorinTrial.Editor
{
    // Compare rendering settings at an identical frozen camera and fixed 60 FPS cap.
    public static class TrialGpuValidation
    {
        [Serializable] public sealed class Result
        {public string scene,gpu,resolution;public int samples;public double oldGpuMs,balancedGpuMs,oldTriangles,balancedTriangles,oldBatches,balancedBatches;}
        static UniversalRenderPipelineAsset pipeline;
        static ScriptableRendererFeature ao;
        static string pipelineJson,aoJson;
        static TrialPowerBudget budget;
        static bool previousBudget;
        static int cap,sync,phase,frames,lastFrame;
        static ulong lastTiming;
        static readonly FrameTiming[] timings=new FrameTiming[1];
        static readonly List<double> gpuTimes=new List<double>();
        static double triangles,batches;
        static ProfilerRecorder batchCounter;
        static Result result;
        static Canvas hudCanvas;
        static bool canvasEnabled;
        public static string Status {get;private set;}="Idle";
        public static void Start()
        {
            if(Status=="Running")throw new InvalidOperationException("Benchmark already active.");
            var game=UnityEngine.Object.FindAnyObjectByType<TrialGame>();if(!game || !game.Ready)throw new InvalidOperationException("Start a trial scene first.");
            game.StartTrial();if(game.Player.ChoosingBoon)game.Player.ChooseBoon(game.Player.OfferA);game.Pause();
            hudCanvas=game.Hud.GetComponentInChildren<Canvas>();canvasEnabled=hudCanvas && hudCanvas.enabled;if(hudCanvas)hudCanvas.enabled=false;
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;pipelineJson=EditorJsonUtility.ToJson(pipeline);
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            foreach(var feature in renderer.rendererFeatures)if(feature.GetType().Name=="ScreenSpaceAmbientOcclusion")ao=feature;
            if(ao)aoJson=EditorJsonUtility.ToJson(ao);
            budget=UnityEngine.Object.FindAnyObjectByType<TrialPowerBudget>();if(budget){previousBudget=budget.enabled;budget.enabled=false;}
            cap=Application.targetFrameRate;sync=QualitySettings.vSyncCount;QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
            result=new Result{scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,gpu=SystemInfo.graphicsDeviceName,resolution=Screen.width+"x"+Screen.height};
            phase=frames=0;lastFrame=-1;lastTiming=0;gpuTimes.Clear();triangles=batches=0;Status="Running";
            batchCounter = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            Apply(true);RenderPipelineManager.endContextRendering+=Sample;EditorApplication.playModeStateChanged+=PlayChanged;
        }
        static void Apply(bool old)
        {
            pipeline.renderScale=1;pipeline.shadowCascadeCount=old?4:2;pipeline.shadowDistance=old?50:35;pipeline.supportsCameraOpaqueTexture=old;
            var serialized=new SerializedObject(pipeline);serialized.FindProperty("m_SoftShadowQuality").intValue=old?3:2;serialized.ApplyModifiedPropertiesWithoutUndo();
            if(ao){var settings=new SerializedObject(ao);settings.FindProperty("m_Settings.Downsample").boolValue=!old;settings.ApplyModifiedPropertiesWithoutUndo();}
        }
        static void Sample(ScriptableRenderContext context,List<Camera> cameras)
        {
            bool gameCamera=false;foreach(var camera in cameras)if(camera.CompareTag("MainCamera") && camera.cameraType==CameraType.Game)gameCamera=true;
            if(!gameCamera || Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;frames++;FrameTimingManager.CaptureFrameTimings();
            if(frames<=45)return;
            if(FrameTimingManager.GetLatestTimings(1,timings)>0 && timings[0].gpuFrameTime>0 && timings[0].frameStartTimestamp!=lastTiming)
            {lastTiming=timings[0].frameStartTimestamp;gpuTimes.Add(timings[0].gpuFrameTime);}
            triangles+=UnityStats.triangles;
            if (batchCounter.Valid) batches += batchCounter.LastValue;
            if(frames<165)return;
            gpuTimes.Sort();double median=gpuTimes.Count>0?gpuTimes[gpuTimes.Count/2]:0;
            if(phase==0)
            {result.oldGpuMs=median;result.oldTriangles=triangles/120;result.oldBatches=batches/120;phase=1;frames=0;gpuTimes.Clear();triangles=batches=0;Apply(false);return;}
            result.samples=gpuTimes.Count;result.balancedGpuMs=median;result.balancedTriangles=triangles/120;result.balancedBatches=batches/120;
            System.IO.Directory.CreateDirectory("output");System.IO.File.WriteAllText("output/gpu-rendering-comparison.json",JsonUtility.ToJson(result,true));
            Status="Complete";Stop();
        }
        static void PlayChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode){Status="Cancelled";Stop();}}
        static void Stop()
        {
            if (batchCounter.Valid) batchCounter.Dispose();
            RenderPipelineManager.endContextRendering-=Sample;EditorApplication.playModeStateChanged-=PlayChanged;
            if(pipeline && pipelineJson!=null)EditorJsonUtility.FromJsonOverwrite(pipelineJson,pipeline);
            if(ao && aoJson!=null)EditorJsonUtility.FromJsonOverwrite(aoJson,ao);
            if(budget)budget.enabled=previousBudget;Application.targetFrameRate=cap;QualitySettings.vSyncCount=sync;
            if(hudCanvas)hudCanvas.enabled=canvasEnabled;
        }
    }
}
