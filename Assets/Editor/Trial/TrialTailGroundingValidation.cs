using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TomorinTrial.Editor
{
    public static class TrialTailGroundingValidation
    {
        public static string Status {get;private set;}="Not run";

        [MenuItem("Tools/Tomorin/Verify tail sweep grounding (Edit Mode)")]
        public static void Run()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before checking the imported model.");
            var scene=EditorSceneManager.NewPreviewScene();
            var actor=new GameObject("Tail grounding validation");SceneManager.MoveGameObjectToScene(actor,scene);
            var baked=new List<Mesh>();var vertices=new List<Vector3>();
            float worst=0,worstAt=0;DeepFishSkill worstSkill=DeepFishSkill.None;int samples=0;
            try
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/DeepSeek/DeepSeek.fbx");
                var model=UnityEngine.Object.Instantiate(asset,actor.transform);
                foreach(var animator in model.GetComponentsInChildren<Animator>())animator.enabled=false;
                var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                model.transform.localScale*=2.85f/bounds.size.y;
                bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                model.transform.position+=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
                var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();foreach(var skin in skins)baked.Add(new Mesh());
                var rig=actor.AddComponent<TrialBossRig>();rig.Initialize(null,null,model.transform);
                foreach(bool enraged in new[]{false,true})foreach(var skill in new[]{DeepFishSkill.TailSweep,DeepFishSkill.ReverseTail})
                    for(int i=0;i<=100;i++)
                    {
                        float progress=i/100f;rig.Pose(skill,progress,0,0,enraged);samples++;
                        for(int mesh=0;mesh<skins.Length;mesh++)
                        {
                            skins[mesh].BakeMesh(baked[mesh]);baked[mesh].GetVertices(vertices);
                            foreach(var vertex in vertices)
                            {
                                float height=skins[mesh].transform.TransformPoint(vertex).y;
                                if(height<worst){worst=height;worstAt=progress;worstSkill=skill;}
                            }
                        }
                    }
                Status=$"{samples} poses; minimum mesh height {worst:F3} m ({worstSkill} at {worstAt:F2})";
                if(worst<-.10f)throw new Exception("Tail sweep sinks through the floor: "+Status);
                Status="Passed: "+Status;Debug.Log("[Tail grounding] "+Status);
            }
            catch(Exception error){Status="FAILED: "+error.Message;throw;}
            finally
            {
                foreach(var mesh in baked)UnityEngine.Object.DestroyImmediate(mesh);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
