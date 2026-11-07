using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TomorinTrial.Editor
{
    public static class TrialRunSetup
    {
        [MenuItem("Tools/Tomorin/Build endless and Boss rush stages")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var previous=SceneManager.GetActiveScene();
            var source=EditorSceneManager.OpenScene("Assets/Scenes/TwinLotusCourt.unity",OpenSceneMode.Additive);
            try
            {
                var original=source.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<TrialGame>()).Single();
                foreach(var level in new[]{TrialLevel.Endless,TrialLevel.BossRush})
                {
                    var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                    try
                    {
                        var root=new GameObject(TrialChampionCatalog.LevelName(level));SceneManager.MoveGameObjectToScene(root,scene);
                        var game=root.AddComponent<TrialGame>();EditorUtility.CopySerialized(original,game);
                        var so=new SerializedObject(game);so.FindProperty("initialLevel").enumValueIndex=(int)level;so.ApplyModifiedPropertiesWithoutUndo();
                        var camera=new GameObject("Main Camera");SceneManager.MoveGameObjectToScene(camera,scene);camera.tag="MainCamera";camera.AddComponent<Camera>();camera.AddComponent<AudioListener>();
                        EditorSceneManager.SaveScene(scene,TrialChampionCatalog.Scene(level));
                    }
                    finally{EditorSceneManager.CloseScene(scene,true);}
                }
            }
            finally{EditorSceneManager.CloseScene(source,true);SceneManager.SetActiveScene(previous);}
            var scenes=EditorBuildSettings.scenes.ToList();
            foreach(var level in new[]{TrialLevel.Endless,TrialLevel.BossRush})if(!scenes.Any(s=>s.path==TrialChampionCatalog.Scene(level)))scenes.Add(new EditorBuildSettingsScene(TrialChampionCatalog.Scene(level),true));
            EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Tomorin/Apply balanced rendering defaults")]
        public static void OptimizeRendering()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            pipeline.shadowCascadeCount=2;pipeline.shadowDistance=35;pipeline.supportsCameraOpaqueTexture=false;
            var settings=new SerializedObject(pipeline);settings.FindProperty("m_SoftShadowQuality").intValue=2;settings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            foreach(var feature in renderer.rendererFeatures)
            {
                if(feature.GetType().Name!="ScreenSpaceAmbientOcclusion")continue;
                var serialized=new SerializedObject(feature);serialized.FindProperty("m_Settings.Downsample").boolValue=true;serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(feature);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
