using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TomorinTrial.Editor
{
    public static class TrialBuilder
    {
        public const string ScenePath="Assets/Scenes/AshenCourt.unity";
        const string ModelPath="Assets/Art/Models/Tomorin/Tomorin.fbx";
        const string AnimFolder="Assets/Anims/Trial";

        [MenuItem("Tools/Tomorin/Build playable trial")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Leave Play Mode before building the trial scene.");
            Directory.CreateDirectory(AnimFolder);Directory.CreateDirectory("Assets/Prefabs/Trial");
            AssetDatabase.Refresh();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if(!model)throw new FileNotFoundException("Tomorin model missing",ModelPath);
            BakeAnimations(model);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            scene.name="AshenCourt";
            var root=new GameObject("残火 · 雾庭 / Playable Trial");
            var game=root.AddComponent<TrialGame>();
            var serialized=new SerializedObject(game);
            serialized.FindProperty("tomorinModel").objectReferenceValue=model;
            serialized.FindProperty("deepSeekModel").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/DeepSeek/DeepSeek.fbx");
            serialized.FindProperty("naiWaModel").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/NaiWa/NaiWa.fbx");
            serialized.FindProperty("uiFont").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/InkUI.ttc");
            serialized.FindProperty("inputActions").objectReferenceValue=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
            serialized.FindProperty("surfaceShader").objectReferenceValue=Shader.Find("Universal Render Pipeline/Lit");
            serialized.FindProperty("skyShader").objectReferenceValue=Shader.Find("Tomorin/Ink Night Sky");
            serialized.FindProperty("particleShader").objectReferenceValue=Shader.Find("Universal Render Pipeline/Particles/Unlit");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var camera=new GameObject("Main Camera");camera.tag="MainCamera";var view=camera.AddComponent<Camera>();camera.AddComponent<AudioListener>();
            camera.transform.position=new Vector3(0,2.8f,-25);camera.transform.rotation=Quaternion.Euler(16,0,0);view.fieldOfView=58;
            EditorSceneManager.SaveScene(scene,ScenePath);
            var scenes=new List<EditorBuildSettingsScene>{new EditorBuildSettingsScene(ScenePath,true)};
            scenes.AddRange(EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath));EditorBuildSettings.scenes=scenes.ToArray();
            AssetDatabase.SaveAssets();Selection.activeObject=game;
            Debug.Log("[Tomorin] AshenCourt scene ready. Press Play, then Enter. 13 original skeletal animation clips baked.");
        }

        [MenuItem("Tools/Tomorin/Bake original sword animations")]
        public static void Bake()
        {BakeAnimations(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));}

        static void BakeAnimations(GameObject modelAsset)
        {
            var actor=new GameObject("Tomorin_SwordRig");
            try
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(modelAsset);model.transform.SetParent(actor.transform,false);model.name="Tomorin";
                foreach(var animator in model.GetComponentsInChildren<Animator>())animator.enabled=false;
                var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                model.transform.localScale*=1.68f/bounds.size.y;
                bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                model.transform.position+=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
                var rig=actor.AddComponent<TrialRig>();rig.Initialize(null,model.transform);
                var targets=actor.GetComponentsInChildren<Transform>();
                string controllerPath=AnimFolder+"/TomorinSword.controller";
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine=controller.layers[0].stateMachine;
                foreach(var state in machine.states.ToArray())machine.RemoveState(state.state);
                BakeClip(actor,rig,targets,controller,"Idle",FighterAction.Free,2,0,0,false,true);
                BakeClip(actor,rig,targets,controller,"Run",FighterAction.Free,.70f,0,1,false,true);
                BakeClip(actor,rig,targets,controller,"Sprint",FighterAction.Free,.48f,0,1.5f,false,true);
                BakeClip(actor,rig,targets,controller,"Slash01",FighterAction.Light,.38f,0,0,false,false);
                BakeClip(actor,rig,targets,controller,"Slash02",FighterAction.Light,.38f,1,0,false,false);
                BakeClip(actor,rig,targets,controller,"Slash03",FighterAction.Light,.38f,2,0,false,false);
                BakeClip(actor,rig,targets,controller,"Slash04",FighterAction.Light,.82f,3,0,false,false);
                BakeClip(actor,rig,targets,controller,"AirCrimson01",FighterAction.Light,.56f,TrialPlayer.AirBladeFirst,0,false,false);
                BakeClip(actor,rig,targets,controller,"AirCrimson02",FighterAction.Light,.56f,TrialPlayer.AirBladeSecond,0,false,false);
                BakeClip(actor,rig,targets,controller,"AirCrimson03",FighterAction.Light,.56f,TrialPlayer.AirBladeThird,0,false,false);
                BakeClip(actor,rig,targets,controller,"SandDive",FighterAction.Light,.78f,TrialPlayer.AirSandDive,0,false,false);
                BakeClip(actor,rig,targets,controller,"SwordPlunge",FighterAction.Heavy,.92f,20,0,false,false);
                BakeClip(actor,rig,targets,controller,"SakuraLunge",FighterAction.Light,.64f,TrialPlayer.SakuraFollowup,0,false,false);
                BakeClip(actor,rig,targets,controller,"RisingSlash",FighterAction.Heavy,.76f,4,0,false,false);
                BakeClip(actor,rig,targets,controller,"ChainThrust",FighterAction.Heavy,.76f,5,0,false,false);
                BakeClip(actor,rig,targets,controller,"ChainFinisher",FighterAction.Heavy,1.02f,6,0,false,false);
                BakeClip(actor,rig,targets,controller,"WindDodgeSlash",FighterAction.Dodge,.44f,7,0,false,false);
                BakeClip(actor,rig,targets,controller,"DodgePursuit",FighterAction.Light,.48f,8,0,false,false);
                BakeClip(actor,rig,targets,controller,"GuardThrust",FighterAction.Light,.58f,11,0,false,false);
                BakeClip(actor,rig,targets,controller,"RunningDraw",FighterAction.Light,.68f,12,0,false,false);
                BakeClip(actor,rig,targets,controller,"AerialCleave",FighterAction.Heavy,.90f,13,0,false,false);
                BakeClip(actor,rig,targets,controller,"PostureExecution",FighterAction.Heavy,1.12f,14,0,false,false);
                BakeClip(actor,rig,targets,controller,"InterceptionCut",FighterAction.Heavy,.66f,15,0,false,false);
                BakeClip(actor,rig,targets,controller,"ReverseDraw",FighterAction.Light,.46f,16,0,false,false);
                BakeClip(actor,rig,targets,controller,"ChargedCleave",FighterAction.Heavy,1f,17,0,false,false);
                BakeClip(actor,rig,targets,controller,"HeavyReverse",FighterAction.Heavy,.78f,18,0,false,false);
                BakeClip(actor,rig,targets,controller,"HeavyFinish",FighterAction.Heavy,.94f,19,0,false,false);
                BakeClip(actor,rig,targets,controller,"Charge",FighterAction.Charge,1.25f,0,0,false,false);
                BakeClip(actor,rig,targets,controller,"Heavy",FighterAction.Heavy,.84f,0,0,false,false);
                BakeClip(actor,rig,targets,controller,"Dodge",FighterAction.Dodge,.44f,0,0,false,false);
                BakeClip(actor,rig,targets,controller,"Guard",FighterAction.Free,1,0,0,true,true);
                BakeClip(actor,rig,targets,controller,"Drink",FighterAction.Heal,1.25f,0,0,false,false);
                BakeClip(actor,rig,targets,controller,"Hit",FighterAction.Stagger,.42f,0,0,false,false);
                BakeClip(actor,rig,targets,controller,"Death",FighterAction.Dead,1.4f,0,0,false,false);
                BakeClip(actor,rig,targets,controller,"Walk",FighterAction.Free,.95f,0,.6f,false,true);
                rig.Pose(FighterAction.Free,0,0,0,false,0);
                var animatorRoot=actor.AddComponent<Animator>();animatorRoot.runtimeAnimatorController=controller;
                PrefabUtility.SaveAsPrefabAsset(actor,"Assets/Prefabs/Trial/Tomorin_SwordRig.prefab");
            }
            finally{Object.DestroyImmediate(actor);}
        }

        static void BakeClip(GameObject actor,TrialRig rig,Transform[] targets,AnimatorController controller,string label,FighterAction action,float duration,int combo,float movement,bool guard,bool loop)
        {
            const int rate=30;int frames=Mathf.CeilToInt(duration*rate);
            var curves=new AnimationCurve[targets.Length,7];for(int i=0;i<targets.Length;i++)for(int j=0;j<7;j++)curves[i,j]=new AnimationCurve();
            Quaternion[] previous=new Quaternion[targets.Length];
            for(int frame=0;frame<=frames;frame++)
            {
                float p=(float)frame/frames,time=p*duration;
                rig.Pose(action,p,combo,movement,guard,p*Mathf.PI*2);
                for(int i=1;i<targets.Length;i++)
                {
                    var q=targets[i].localRotation;if(frame>0 && Quaternion.Dot(previous[i],q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous[i]=q;
                    curves[i,0].AddKey(time,q.x);curves[i,1].AddKey(time,q.y);curves[i,2].AddKey(time,q.z);curves[i,3].AddKey(time,q.w);
                    var v=targets[i].localPosition;curves[i,4].AddKey(time,v.x);curves[i,5].AddKey(time,v.y);curves[i,6].AddKey(time,v.z);
                }
            }
            string path=AnimFolder+"/"+label+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}else clip.ClearCurves();
            clip.name=label;clip.frameRate=rate;
            string[] properties={"m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z"};
            for(int i=1;i<targets.Length;i++)
            {
                string binding=AnimationUtility.CalculateTransformPath(targets[i],actor.transform);
                for(int j=0;j<7;j++)
                {
                    var curve=curves[i,j];var keys=curve.keys;bool constant=true;
                    for(int k=1;k<keys.Length;k++)if(Mathf.Abs(keys[k].value-keys[0].value)>1e-8f){constant=false;break;}
                    if(constant)curve=new AnimationCurve(new Keyframe(0,keys[0].value),new Keyframe(duration,keys[0].value));
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(binding,typeof(Transform),properties[j]),curve);
                }
            }
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);
            var state=controller.AddMotion(clip);state.name=label;
        }

        [MenuItem("Tools/Tomorin/Build Windows player")]
        public static void BuildPlayer()
        {
            Directory.CreateDirectory("Builds/AshenCourt");
            var enabledScenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToList();
            if(!enabledScenes.Contains(ScenePath))enabledScenes.Add(ScenePath);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=enabledScenes.ToArray(),locationPathName="Builds/AshenCourt/Tomorin_AshenCourt.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            Directory.CreateDirectory("Logs/Trial");
            File.WriteAllText("Logs/Trial/BuildSummary.txt",$"{System.DateTime.UtcNow:O}\nResult: {result.summary.result}\nErrors: {result.summary.totalErrors}\nWarnings: {result.summary.totalWarnings}\nSize: {result.summary.totalSize} bytes\nTime: {result.summary.totalTime}\nScenes:\n{string.Join("\n",enabledScenes)}");
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new System.Exception("Windows build failed: "+result.summary.result);
            Debug.Log("[Tomorin] Windows player built: Builds/AshenCourt/Tomorin_AshenCourt.exe");
        }
    }
}
