using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TomorinTrial.Editor
{
    // Recover the authored weights without discarding the newer face / TiePen blend shapes.
    public static class TrialBossSkinRepair
    {
        const string ImportedModel="Assets/Art/Models/DeepSeek/DeepSeek.fbx";
        const string MeshAsset="Assets/Art/Models/DeepSeek/DeepSeekAnimatedMeshes.asset";
        public const string PrefabPath="Assets/Prefabs/Trial/DeepSeek_Animated.prefab";

        public static string Repair(string weightedSource)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            if(AssetDatabase.LoadMainAssetAtPath(MeshAsset))throw new InvalidOperationException("Animated meshes already exist; inspect them before replacing asset references.");
            var sourceAsset=AssetDatabase.LoadAssetAtPath<GameObject>(weightedSource);
            if(!sourceAsset)throw new FileNotFoundException("Import the original weighted FBX first.",weightedSource);
            var source=UnityEngine.Object.Instantiate(sourceAsset);
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ImportedModel));
            model.name="DeepSeek_Animated";
            var report=new List<string>();
            try
            {
                var bones=model.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);
                var originals=source.GetComponentsInChildren<SkinnedMeshRenderer>().ToDictionary(s=>s.name);
                bool first=true;
                foreach(var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    var original=originals[renderer.name];
                    if(original.bones.Length==0)throw new InvalidOperationException("Source has no weights: "+original.name);
                    var imported=renderer is SkinnedMeshRenderer skin?skin.sharedMesh:renderer.GetComponent<MeshFilter>().sharedMesh;
                    var mesh=UnityEngine.Object.Instantiate(imported);mesh.name=imported.name+"_Animated";
                    var sourceVertices=original.sharedMesh.vertices;
                    var targetVertices=mesh.vertices;
                    var weights=original.sharedMesh.boneWeights;var mapped=new BoneWeight[targetVertices.Length];
                    // Both imports are instantiated at the origin. Compare Unity world units:
                    // the older import has an extra identity container above the FBX root.
                    for(int i=0;i<sourceVertices.Length;i++)sourceVertices[i]=original.transform.TransformPoint(sourceVertices[i]);
                    float worst=0,total=0;
                    for(int i=0;i<targetVertices.Length;i++)
                    {
                        Vector3 point=renderer.transform.TransformPoint(targetVertices[i]);
                        int nearest=0;float distance=float.PositiveInfinity;
                        for(int j=0;j<sourceVertices.Length;j++)
                        {
                            float next=(point-sourceVertices[j]).sqrMagnitude;
                            if(next<distance){distance=next;nearest=j;}
                        }
                        mapped[i]=weights[nearest];worst=Mathf.Max(worst,Mathf.Sqrt(distance));total+=Mathf.Sqrt(distance);
                    }
                    if(worst>.001f)throw new InvalidOperationException("Source geometry does not match "+renderer.name+"; refusing an approximate skin transfer.");
                    var mappedBones=original.bones.Select(b=>bones[b.name]).ToArray();
                    mesh.boneWeights=mapped;
                    mesh.bindposes=mappedBones.Select(b=>b.worldToLocalMatrix*renderer.transform.localToWorldMatrix).ToArray();
                    if(first){AssetDatabase.CreateAsset(mesh,MeshAsset);first=false;}
                    else AssetDatabase.AddObjectToAsset(mesh,MeshAsset);
                    var obj=renderer.gameObject;var materials=renderer.sharedMaterials;
                    var animated=renderer as SkinnedMeshRenderer;
                    if(!animated)
                    {
                        UnityEngine.Object.DestroyImmediate(obj.GetComponent<MeshFilter>());
                        UnityEngine.Object.DestroyImmediate(renderer);
                        animated=obj.AddComponent<SkinnedMeshRenderer>();
                    }
                    animated.sharedMesh=mesh;animated.bones=mappedBones;animated.rootBone=bones[original.rootBone.name];
                    animated.sharedMaterials=materials;animated.localBounds=mesh.bounds;
                    report.Add($"{obj.name}: {mapped.Length} weighted vertices, {mappedBones.Length} bones, {mesh.blendShapeCount} blend shapes; nearest distance mean={total/mapped.Length:F5}, max={worst:F5}");
                }
                PrefabUtility.SaveAsPrefabAsset(model,PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{UnityEngine.Object.DestroyImmediate(model);UnityEngine.Object.DestroyImmediate(source);}
            Directory.CreateDirectory("Logs/Trial");
            File.WriteAllText("Logs/Trial/FishSkinRepair.txt",string.Join("\n",report));
            return string.Join("\n",report);
        }

        public static void AssignToCourt()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/AshenCourt.unity",OpenSceneMode.Additive);
            try
            {
                var game=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TrialGame>()).Single();
                var serialized=new SerializedObject(game);
                serialized.FindProperty("deepSeekModel").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                serialized.ApplyModifiedProperties();EditorSceneManager.SaveScene(scene);
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
