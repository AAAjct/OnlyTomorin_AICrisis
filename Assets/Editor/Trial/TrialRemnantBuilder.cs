using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TomorinTrial.Editor
{
    public static class TrialRemnantBuilder
    {
        public const string PrefabPath="Assets/Resources/Trial/Models/Remnant_Champion.prefab";
        [MenuItem("Tools/Tomorin/Build Penguin Remnant boss")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play Mode before building assets.");
            const string folder="Assets/Art/Champions/Remnant";
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Remnant/Remnant.fbx");
            if(!source)throw new FileNotFoundException("Blender Remnant.fbx export missing");
            var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Tomorin/Tomorin.fbx");
            var originals=original.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().ToArray();
            var root=new GameObject("凑企鹅残躯 · 断羽无赦");
            try
            {
                var model=UnityEngine.Object.Instantiate(source,root.transform);model.name="Tomorin_Remnant";
                foreach(var a in model.GetComponentsInChildren<Animator>())UnityEngine.Object.DestroyImmediate(a);
                foreach(var c in model.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
                var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                model.transform.localScale*=1.98f/bounds.size.y;
                bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                model.transform.position+=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
                foreach(var r in renderers)
                {
                    var materials=r.sharedMaterials;
                    for(int i=0;i<materials.Length;i++)
                    {
                        string role=materials[i]?materials[i].name:"Remnant_Obsidian";
                        string path=folder+"/"+role.Replace('/','_')+".mat";
                        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(!mat)
                        {
                            var template=originals.FirstOrDefault(m=>role.StartsWith(m.name,StringComparison.Ordinal));
                            mat=template?new Material(template):new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name=role;
                            if(role.Contains("Void"))
                            {mat.SetColor("_BaseColor",new Color(.65f,.04f,.95f));mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",new Color(1.6f,.04f,3));}
                            else if(role.Contains("Obsidian"))
                            {mat.SetColor("_BaseColor",new Color(.045f,.025f,.075f));mat.SetFloat("_Metallic",.7f);}
                            else if(role.Contains("Iron"))
                            {mat.SetColor("_BaseColor",new Color(.26f,.19f,.34f));mat.SetFloat("_Metallic",.8f);}
                            else if(role.Contains("MI_Private"))mat.SetColor("_BaseColor",new Color(.42f,.32f,.53f));
                            else if(role.Contains("hair"))mat.SetColor("_BaseColor",new Color(.33f,.22f,.43f));
                            AssetDatabase.CreateAsset(mat,path);
                        }
                        materials[i]=mat;
                    }
                    r.sharedMaterials=materials;
                    if(r is SkinnedMeshRenderer skin){skin.updateWhenOffscreen=false;var b=skin.localBounds;b.Expand(.5f);skin.localBounds=b;}
                }
                root.AddComponent<TrialChampionRig>().ConfigureRemnant(model.transform);
                root.AddComponent<TrialRemnantAura>();
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            var sourceScene=EditorSceneManager.OpenScene(TrialChampionCatalog.Scene(TrialLevel.Anon),OpenSceneMode.Additive);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                var originalGame=sourceScene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<TrialGame>()).Single();
                var go=new GameObject(TrialChampionCatalog.LevelName(TrialLevel.Remnant));SceneManager.MoveGameObjectToScene(go,scene);
                var game=go.AddComponent<TrialGame>();EditorUtility.CopySerialized(originalGame,game);
                var so=new SerializedObject(game);so.FindProperty("initialLevel").enumValueIndex=(int)TrialLevel.Remnant;so.ApplyModifiedPropertiesWithoutUndo();
                var camera=new GameObject("Main Camera");SceneManager.MoveGameObjectToScene(camera,scene);camera.tag="MainCamera";camera.AddComponent<Camera>();camera.AddComponent<AudioListener>();
                EditorSceneManager.SaveScene(scene,TrialChampionCatalog.Scene(TrialLevel.Remnant));
            }
            finally{EditorSceneManager.CloseScene(scene,true);EditorSceneManager.CloseScene(sourceScene,true);}
            var buildScenes=EditorBuildSettings.scenes.ToList();string scenePath=TrialChampionCatalog.Scene(TrialLevel.Remnant);
            if(!buildScenes.Any(s=>s.path==scenePath))buildScenes.Add(new EditorBuildSettingsScene(scenePath,true));
            EditorBuildSettings.scenes=buildScenes.ToArray();AssetDatabase.SaveAssets();
            Debug.Log("[Remnant] Rigged Tomorin variant, 16 moves, solo/LAN stage and build entry ready.");
        }
    }
}
