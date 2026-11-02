using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TomorinTrial.Editor
{
    public static class TrialBossBuilder
    {
        const string ModelPath="Assets/Art/Models/DeepSeek/DeepSeek.fbx";
        const string Folder="Assets/Anims/DeepSeek";

        [MenuItem("Tools/Tomorin/Update movement and DeepSeek boss assets")]
        public static void UpdateAssets()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Leave Play Mode before updating assets.");
            var game=Object.FindFirstObjectByType<TrialGame>();
            if(!game || game.gameObject.scene.path!=TrialBuilder.ScenePath)
                throw new System.InvalidOperationException("Open AshenCourt before updating the boss reference.");
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if(!model)throw new FileNotFoundException("DeepSeek model missing",ModelPath);
            Undo.RecordObject(game,"Assign DeepSeek boss");
            var serialized=new SerializedObject(game);serialized.FindProperty("deepSeekModel").objectReferenceValue=model;serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            TrialBuilder.Bake();Bake();AssetDatabase.SaveAssets();
            Debug.Log("[Tomorin] Movement clips refreshed; DeepSeek scene reference, 10 clips and preview prefab ready.");
        }

        [MenuItem("Tools/Tomorin/Bake DeepSeek boss animations")]
        public static void Bake()
        {
            Directory.CreateDirectory(Folder);Directory.CreateDirectory("Assets/Prefabs/Trial");AssetDatabase.Refresh();
            var actor=new GameObject("DeepSeek_BossRig");
            try
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                model.transform.SetParent(actor.transform,false);model.name="DeepSeek";
                foreach(var a in model.GetComponentsInChildren<Animator>())a.enabled=false;
                var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var r in renderers)bounds.Encapsulate(r.bounds);model.transform.localScale*=2.85f/bounds.size.y;
                bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                model.transform.position+=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
                var rig=actor.AddComponent<TrialBossRig>();rig.Initialize(null,null,model.transform);
                var targets=new[]{model.transform}.Concat(rig.DrivenBones).ToArray();
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/DeepSeekBoss.controller");
                if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(Folder+"/DeepSeekBoss.controller");
                foreach(var state in controller.layers[0].stateMachine.states.ToArray())controller.layers[0].stateMachine.RemoveState(state.state);
                Clip(actor,rig,targets,controller,"Idle",DeepFishSkill.None,2.86f,0,true);
                Clip(actor,rig,targets,controller,"Swim",DeepFishSkill.None,.827f,1,true);
                Clip(actor,rig,targets,controller,"TailSweep",DeepFishSkill.TailSweep,1.75f,0,false);
                Clip(actor,rig,targets,controller,"TidalCharge",DeepFishSkill.TidalCharge,2.1f,0,false);
                Clip(actor,rig,targets,controller,"BellySlam",DeepFishSkill.BellySlam,2.65f,0,false);
                Clip(actor,rig,targets,controller,"BubbleBarrage",DeepFishSkill.BubbleBarrage,2.7f,0,false);
                Clip(actor,rig,targets,controller,"Maelstrom",DeepFishSkill.Maelstrom,3.08f,0,false);
                Clip(actor,rig,targets,controller,"Enrage",DeepFishSkill.Enrage,2.1f,0,false);
                Clip(actor,rig,targets,controller,"Stagger",DeepFishSkill.None,2,0,false,false,true);
                Clip(actor,rig,targets,controller,"Death",DeepFishSkill.None,1.4f,0,false,true);
                rig.Pose(DeepFishSkill.None,0,0,0,false);
                actor.AddComponent<Animator>().runtimeAnimatorController=controller;
                PrefabUtility.SaveAsPrefabAsset(actor,"Assets/Prefabs/Trial/DeepSeek_BossRig.prefab");
            }
            finally{Object.DestroyImmediate(actor);}
        }

        static void Clip(GameObject actor,TrialBossRig rig,Transform[] targets,AnimatorController controller,string label,DeepFishSkill skill,float duration,float movement,bool loop,bool dead=false,bool stagger=false)
        {
            const int rate=30;int frames=Mathf.CeilToInt(duration*rate);
            var curves=new AnimationCurve[targets.Length,7];var previous=new Quaternion[targets.Length];
            for(int i=0;i<targets.Length;i++)for(int j=0;j<7;j++)curves[i,j]=new AnimationCurve();
            for(int frame=0;frame<=frames;frame++)
            {
                float p=(float)frame/frames,t=p*duration;
                rig.Pose(skill,p,movement,t,skill==DeepFishSkill.Maelstrom || skill==DeepFishSkill.Enrage,dead,p,stagger);
                for(int i=0;i<targets.Length;i++)
                {
                    var q=targets[i].localRotation;if(frame>0 && Quaternion.Dot(previous[i],q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous[i]=q;
                    var v=targets[i].localPosition;float[] values={q.x,q.y,q.z,q.w,v.x,v.y,v.z};
                    for(int j=0;j<7;j++)curves[i,j].AddKey(t,values[j]);
                }
            }
            string path=Folder+"/"+label+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}else clip.ClearCurves();
            clip.name=label;clip.frameRate=rate;
            string[] names={"m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z"};
            for(int i=0;i<targets.Length;i++)for(int j=0;j<7;j++)
            {
                var curve=curves[i,j];var keys=curve.keys;bool constant=true;
                for(int k=1;k<keys.Length;k++)if(Mathf.Abs(keys[k].value-keys[0].value)>1e-7f){constant=false;break;}
                if(constant)curve=new AnimationCurve(new Keyframe(0,keys[0].value),new Keyframe(duration,keys[0].value));
                else curve=Simplify(curve,j<4?.00025f:.000015f);
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(targets[i],actor.transform),typeof(Transform),names[j]),curve);
            }
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);
            controller.AddMotion(clip).name=label;
        }

        // Reduce sampled curves with a bounded interpolation error, retaining endpoints.
        static AnimationCurve Simplify(AnimationCurve source,float tolerance)
        {
            var keys=source.keys;var keep=new System.Collections.Generic.SortedSet<int>{0,keys.Length-1};
            Reduce(keys,0,keys.Length-1,tolerance,keep);
            var curve=new AnimationCurve(keep.Select(i=>new Keyframe(keys[i].time,keys[i].value)).ToArray());
            for(int i=0;i<curve.length;i++)
            {AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}
            return curve;
        }
        static void Reduce(Keyframe[] keys,int first,int last,float tolerance,System.Collections.Generic.SortedSet<int> keep)
        {
            float error=tolerance;int index=-1;
            for(int i=first+1;i<last;i++)
            {
                float expected=Mathf.Lerp(keys[first].value,keys[last].value,Mathf.InverseLerp(keys[first].time,keys[last].time,keys[i].time));
                float delta=Mathf.Abs(keys[i].value-expected);if(delta>error){error=delta;index=i;}
            }
            if(index<0)return;keep.Add(index);Reduce(keys,first,index,tolerance,keep);Reduce(keys,index,last,tolerance,keep);
        }
    }
}
