using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TomorinTrial
{
    public enum TrialGraphicsPreset { Performance, Balanced, Quality }
    // A runtime clone preserves the authored pipeline asset and restores it on exit.
    public sealed class TrialGraphicsBudget : MonoBehaviour
    {
        UniversalRenderPipelineAsset original,runtime;
        RenderPipelineAsset previousQualityPipeline;
        static int cachedPreset=-1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache(){cachedPreset=-1;}
        public static TrialGraphicsPreset Preset
        {get{if(cachedPreset<0)cachedPreset=Mathf.Clamp(PlayerPrefs.GetInt("Ink.GraphicsPreset",Application.isMobilePlatform?0:1),0,2);return (TrialGraphicsPreset)cachedPreset;}}
        public static bool Performance=>Preset==TrialGraphicsPreset.Performance;
        public static float EffectDensity=>Performance?.55f:Preset==TrialGraphicsPreset.Balanced?.8f:1;
        public static string Caption=>"画质："+(Preset==TrialGraphicsPreset.Performance?"流畅":Preset==TrialGraphicsPreset.Quality?"精细":"均衡");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install(){var go=new GameObject("Graphics budget");DontDestroyOnLoad(go);go.AddComponent<TrialGraphicsBudget>();}
        void Awake()
        {
            original=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;if(!original)return;
            previousQualityPipeline=QualitySettings.renderPipeline;
            runtime=Instantiate(original);runtime.name=original.name+" (runtime budget)";QualitySettings.renderPipeline=runtime;Apply();
        }
        void OnEnable(){SceneManager.sceneLoaded+=SceneLoaded;}
        void OnDisable(){SceneManager.sceneLoaded-=SceneLoaded;}
        void SceneLoaded(Scene scene,LoadSceneMode mode){ApplySceneEffects();}
        public static void Cycle()
        {
            cachedPreset=((int)Preset+1)%3;PlayerPrefs.SetInt("Ink.GraphicsPreset",cachedPreset);PlayerPrefs.Save();
            var budget=FindAnyObjectByType<TrialGraphicsBudget>();if(budget)budget.Apply();
        }
        void Apply()
        {
            if(!runtime)return;
            var preset=Preset;
            runtime.renderScale=preset==TrialGraphicsPreset.Performance?.8f:1;
            runtime.shadowDistance=preset==TrialGraphicsPreset.Performance?25:preset==TrialGraphicsPreset.Balanced?35:50;
            runtime.shadowCascadeCount=preset==TrialGraphicsPreset.Quality?4:preset==TrialGraphicsPreset.Performance?1:2;
            runtime.mainLightShadowmapResolution=preset==TrialGraphicsPreset.Performance?1024:original.mainLightShadowmapResolution;
            // No project shader samples scene colour; skip the opaque colour copy.
            runtime.supportsCameraOpaqueTexture=false;
            ApplySceneEffects();
        }
        void ApplySceneEffects()
        {
            foreach(var camera in Camera.allCameras)ApplyCamera(camera);
            foreach(var volume in FindObjectsByType<Volume>())
                if(volume.sharedProfile && volume.name=="Night atmosphere" && volume.sharedProfile.TryGet<Bloom>(out var bloom))bloom.active=!Application.isMobilePlatform && !Performance;
        }
        public static void ApplyCamera(Camera camera)
        {
            var data=camera.GetUniversalAdditionalCameraData();
            data.antialiasing=Performance?AntialiasingMode.FastApproximateAntialiasing:AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }
        void OnDestroy(){if(runtime){if(QualitySettings.renderPipeline==runtime)QualitySettings.renderPipeline=previousQualityPipeline;Destroy(runtime);}}
    }
}
