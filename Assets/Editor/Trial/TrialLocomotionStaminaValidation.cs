using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TomorinTrial.Editor
{
    public static class TrialLocomotionStaminaValidation
    {
        static readonly List<string> results=new List<string>();
        static void Check(bool passed,string message)
        {
            if(!passed)throw new Exception(message);
            results.Add("PASS: "+message);
        }
        static void Set(TrialPlayer player,string property,object value)=>typeof(TrialFighter).GetProperty(property).SetValue(player,value);
        static void Tick(TrialPlayer player,byte pressed=0,byte held=0,float dt=0)
        {
            var inputObject=new GameObject("Stamina validation controls");
            try
            {
                var input=inputObject.AddComponent<TrialInput>();
                input.ApplyNetwork(new TrialControls{pressed=pressed,held=held});
                player.Tick(dt,input);
            }
            finally{UnityEngine.Object.DestroyImmediate(inputObject);}
        }

        [MenuItem("Tools/Tomorin/Verify fish footwork (Edit Mode)")]
        public static void RunPoses()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            results.Clear();
            var scene=EditorSceneManager.NewPreviewScene();
            var actor=new GameObject("Fish footwork validation");SceneManager.MoveGameObjectToScene(actor,scene);
            try
            {
                var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TrialBossBuilder.ModelPath),actor.transform);
                foreach(var animator in model.GetComponentsInChildren<Animator>())animator.enabled=false;
                var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                model.transform.localScale*=2.85f/bounds.size.y;
                bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                model.transform.position+=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
                var bones=model.GetComponentsInChildren<Transform>();
                var soles=bones.Where(t=>t.name=="toes.L" || t.name=="toes.R" || t.name=="foot.L" || t.name=="foot.R").ToArray();
                var leg=bones.First(t=>t.name=="upper_leg.L");
                float ground=soles.Min(t=>t.position.y);
                var rig=actor.AddComponent<TrialBossRig>();rig.Initialize(null,null,model.transform);
                var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();
                Check(skins.Length==6 && skins.All(s=>s.bones.Length>0 && s.sharedMesh.bindposes.Length==s.bones.Length),"All six visible meshes have real skin bindings");
                var body=skins.First(s=>s.name=="body");var baked=new Mesh();
                try
                {
                    foreach(var skill in new[]{DeepFishSkill.None,DeepFishSkill.TidalCharge,DeepFishSkill.RiptideRush})
                    {
                        rig.Pose(skill,.35f,1,.2f,false);body.BakeMesh(baked);var before=baked.vertices;
                        rig.Pose(skill,.35f,1,.6f,false);body.BakeMesh(baked);var after=baked.vertices;
                        int changed=0;
                        for(int i=0;i<before.Length;i++)if((body.transform.TransformVector(after[i]-before[i])).magnitude>.02f)changed++;
                        Check(changed>100,$"{skill} deforms the rendered body, not just bones ({changed} vertices move over 2 cm)");
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(baked);}
                rig.Pose(DeepFishSkill.None,0,1,.2f,false);
                foreach(var side in new[]{"L","R"})
                {
                    var shoulder=bones.First(t=>t.name=="upper_arm."+side);var hand=bones.First(t=>t.name=="hand."+side);
                    Check(shoulder.position.y-hand.position.y>.2f,"Walking arm "+side+" hangs below the shoulder instead of a T pose");
                }
                Check(skins.First(s=>s.name=="hair").sharedMesh.GetBlendShapeIndex("TiePen")>=0,"Repaired model retains TiePen blend shape");
                foreach(float yaw in new[]{0f,90f,180f})
                {
                    actor.transform.rotation=Quaternion.Euler(0,yaw,0);
                    foreach(var skill in new[]{DeepFishSkill.None,DeepFishSkill.TidalCharge,DeepFishSkill.RiptideRush,DeepFishSkill.TailSweep,DeepFishSkill.ReverseTail,DeepFishSkill.BubbleBarrage,DeepFishSkill.Maelstrom,DeepFishSkill.Enrage})
                    {
                        float worst=0;
                        for(int i=0;i<=60;i++)
                        {
                            float t=i/60f;rig.Pose(skill,t,1,t,false);
                            worst=Mathf.Max(worst,Mathf.Abs(soles.Min(s=>s.position.y)-ground));
                        }
                        Check(worst<.002f,$"{skill} support foot stays grounded at yaw {yaw} (error {worst:F4} m)");
                    }
                }
                foreach(var skill in new[]{DeepFishSkill.None,DeepFishSkill.TidalCharge,DeepFishSkill.RiptideRush})
                {
                    rig.Pose(skill,.35f,1,.2f,false);var a=leg.localRotation;
                    rig.Pose(skill,.35f,1,.6f,false);
                    Check(Quaternion.Angle(a,leg.localRotation)>60,$"{skill} visibly alternates leg strides");
                }
                rig.Pose(DeepFishSkill.BellySlam,.39f,0,0,false);
                Check(soles.Min(s=>s.position.y)>ground+3,"Belly slam retains its deliberate jump");
                rig.Pose(DeepFishSkill.None,0,0,.2f,false);var idle=leg.localRotation;
                rig.Pose(DeepFishSkill.None,0,0,.6f,false);
                Check(Quaternion.Angle(idle,leg.localRotation)<.001f,"Stationary idle does not walk");
                Write("FishFootworkValidation");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }

        [MenuItem("Tools/Tomorin/Verify stamina rewards (Play Mode)")]
        public static void RunStamina()
        {
            var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
            if(!EditorApplication.isPlaying || !game || !game.Ready || TrialNetworkManager.SessionActive)
                throw new InvalidOperationException("Start AshenCourt in offline Play Mode first.");
            results.Clear();var player=game.Player;var point=player.transform.position;
            player.ResetRunBoon();
            try
            {
                player.Restore(point);
                Check(player.MaxHealth==200 && player.MaxStamina==420,"Player starts with 200 health and 420 stamina");
                Set(player,"Stamina",100f);Tick(player,1);
                Check(player.Stamina==85,"Light attack costs 15 stamina");
                player.Restore(point);Set(player,"Stamina",100f);Tick(player,2);
                Check(player.Stamina==72,"Heavy attack costs 28 stamina");
                player.Restore(point);Set(player,"Stamina",100f);Tick(player,4);
                Check(player.Stamina==80,"Dodge costs 20 stamina");
                player.Restore(point);Set(player,"Stamina",0f);Tick(player,dt:.5f);
                Check(player.Stamina==63,"Standing still recovers 126 stamina per second, including from empty");
                player.Restore(point);Set(player,"Stamina",50f);Tick(player,held:1);Tick(player,held:1,dt:.5f);
                Check(player.Stamina==65,"Holding guard recovers 30 stamina per second");
                player.Restore(point);Set(player,"Stamina",100f);Tick(player,1);Set(player,"Action",FighterAction.Free);
                Tick(player,dt:.2f);Check(player.Stamina==85,"Spending stamina waits briefly before natural recovery");
                Tick(player,dt:.15f);Check(player.Stamina>85,"Natural recovery resumes after the shortened delay");
                player.Restore(point);Set(player,"Stamina",20f);
                Tick(player,8);
                Check(player.Action==FighterAction.Heal,"Full-health player can drink to recover stamina");
                Check(player.Flasks==3 && player.Stamina==20,"Starting a drink grants no premature reward");
                Set(player,"ActionTime",.8f);Tick(player);
                Check(player.Flasks==2 && player.Stamina==player.MaxStamina,"Completed drink fills stamina and consumes one flask");
                Tick(player);
                Check(player.Flasks==2,"Drink completion cannot consume a second flask");
                player.Restore(point);Set(player,"Health",30f);Set(player,"Stamina",15f);
                Tick(player,8);Set(player,"ActionTime",.8f);Tick(player);
                Check(player.Health==115 && player.Stamina==player.MaxStamina,"Drink restores both health and stamina");
                player.Restore(point);Set(player,"Stamina",20f);Tick(player,8);
                Set(player,"Action",FighterAction.Stagger);Tick(player);
                Check(player.Flasks==3 && player.Stamina==20,"Interrupted drink gives no stamina and spends no flask");
                player.Restore(point);Set(player,"Stamina",100f);
                Tick(player,4);Set(player,"ActionTime",.1f);
                float spent=player.Stamina;player.ReceiveHit(null,20,0,true);
                Check(player.Stamina==spent+TrialPlayer.PerfectDodgeStamina && player.Health==player.MaxHealth,"Perfect dodge restores 40 stamina and avoids damage");
                player.ReceiveHit(null,20,0,true);
                Check(player.Stamina==spent+TrialPlayer.PerfectDodgeStamina,"Multiple contacts in the same dodge grant only one reward");
                player.Restore(point);Set(player,"Stamina",100f);Tick(player,4);Set(player,"ActionTime",.28f);
                spent=player.Stamina;player.ReceiveHit(null,20,0,true);
                Check(player.Stamina==spent,"Late invulnerable dodge receives no perfect-dodge reward");
                player.Restore(point);Set(player,"Stamina",100f);Tick(player,0,1);
                player.ReceiveHit(null,20,0);
                Check(player.Stamina==122,"Successful parry refunds its cost plus 22 stamina");
                player.Restore(point);Tick(player,0,1);player.ReceiveHit(null,20,0);
                Check(player.Stamina==player.MaxStamina,"Parry reward is capped at maximum stamina");
                player.Restore(point);Tick(player,8);
                Check(player.Action==FighterAction.Free && player.Flasks==3,"Full health and full stamina cannot waste a flask");
                Write("StaminaRewardsValidation");
            }
            finally{player.Restore(point);}
        }

        static void Write(string name)
        {
            Directory.CreateDirectory("Logs/Trial");
            File.WriteAllText("Logs/Trial/"+name+".txt",DateTime.UtcNow.ToString("O")+"\n"+string.Join("\n",results));
            Debug.Log($"[{name}] {results.Count} checks passed");
        }

        [MenuItem("Tools/Tomorin/Verify displacement driven fish gait (Play Mode)")]
        public static void RunMotion()
        {
            var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
            if(!EditorApplication.isPlaying || !game || !game.Ready || Time.deltaTime<=0 || TrialNetworkManager.SessionActive)
                throw new InvalidOperationException("Run offline AshenCourt with time advancing.");
            results.Clear();var boss=game.Enemies.Last();var rig=boss.GetComponent<TrialBossRig>();
            var leg=boss.Visual.GetComponentsInChildren<Transform>().First(t=>t.name=="upper_leg.L");
            var tick=typeof(TrialBossRig).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
            var phase=typeof(TrialBossRig).GetField("gaitDistance",BindingFlags.Instance|BindingFlags.NonPublic);
            var position=boss.transform.position;var original=boss.Boss.CaptureNetwork();
            try
            {
                foreach(var skill in new[]{DeepFishSkill.None,DeepFishSkill.TidalCharge,DeepFishSkill.RiptideRush})
                {
                    var state=original;state.skill=(byte)skill;state.age=1;state.duration=3.6f;boss.Boss.ApplyNetwork(state);
                    tick.Invoke(rig,null);phase.SetValue(rig,0f);
                    boss.transform.position+=Vector3.right*.3125f;tick.Invoke(rig,null);var pose=leg.localRotation;
                    boss.transform.position+=Vector3.right*.625f;tick.Invoke(rig,null);
                    float angle=Quaternion.Angle(pose,leg.localRotation);
                    Check(angle>5,$"Runtime {skill} displacement animates leg ({angle:F1} degrees), independent of MoveBlend={boss.MoveBlend:F1}");
                }
                Write("FishDisplacementValidation");
            }
            finally{boss.Teleport(position);boss.Boss.ApplyNetwork(original);tick.Invoke(rig,null);}
        }
    }
}

