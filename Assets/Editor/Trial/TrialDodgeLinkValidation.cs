using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TomorinTrial.Editor
{
    public static partial class TrialValidation
    {
        [MenuItem("Tools/Tomorin/Verify rendered dodge follow-ups (Play Mode)")]
        public static void RunDodgeLinks()
        {
            if(!EditorApplication.isPlaying || routine!=null)throw new InvalidOperationException("Open an offline trial in Play Mode first.");
            previousBackgroundBehavior=InputSystem.settings.backgroundBehavior;previousEditorBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            passed.Clear();Status="Running rendered dodge follow-ups";routine=DodgeLinkFrames();resumeAt=0;EditorApplication.update+=Step;
        }
        static IEnumerator DodgeLinkFrames()
        {
            var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
            Check(game && game.Ready && !TrialNetworkManager.SessionActive,"Offline player initialized");
            var player=game.Player;bool enabled=game.enabled;game.StartTrial();if(player.ChoosingBoon)player.ChooseBoon(player.OfferA);
            game.enabled=false;Time.timeScale=1;
            var input=game.gameObject.AddComponent<TrialInput>();
            var budget=UnityEngine.Object.FindFirstObjectByType<TrialPowerBudget>();bool budgetEnabled=budget && budget.enabled;
            if(budget)budget.enabled=false;
            int cap=Application.targetFrameRate,sync=QualitySettings.vSyncCount;QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
            try
            {
                foreach(var enemy in game.Enemies)enemy.Teleport(new Vector3(35,0,40));
                for(int route=0;route<3;route++)
                {
                    bool throwing=route>0,guardThrow=route==2;
                    player.Restore(new Vector3(0,0,20));game.CameraRig.Snap();Physics.SyncTransforms();
                    yield return .08f;
                    if(throwing && !guardThrow)typeof(TrialPlayer).GetField("parryReadyUntil",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player,Time.time+5);
                    var previous=player.Visual.localRotation;float largestAngle=0;int observed=0;
                    bool sawFollowup=false;float age=0;int frame=-1;
                    while(age<.46f)
                    {
                        while(frame==Time.frameCount)yield return null;frame=Time.frameCount;
                        float dt=Mathf.Clamp(Time.unscaledDeltaTime,.001f,.05f);
                        input.ApplyNetwork(new TrialControls{pressed=age==0?(byte)(4|(guardThrow?0:throwing?2:1)):(byte)0,held=age==0 && guardThrow?(byte)1:(byte)0,move=Vector2.up,yaw=0});
                        player.Tick(dt,input);age+=dt;
                        int before=Time.frameCount;while(before==Time.frameCount)yield return null;
                        var pose=player.Visual.localRotation;
                        if(player.Action!=FighterAction.Dodge)
                        {largestAngle=Mathf.Max(largestAngle,Quaternion.Angle(previous,pose));observed++;}
                        previous=pose;
                        if(player.Combo==(throwing?TrialPlayer.SpiralSword:TrialPlayer.RollThrust) && player.Action!=FighterAction.Dodge)sawFollowup=true;
                        Check(!float.IsNaN(player.Weapon.position.x) && Vector3.Distance(player.Weapon.position,player.GetComponent<TrialRig>().RightHand.position)<.01f,"Rendered sword stays in the hand before flight");
                    }
                    Check(sawFollowup && observed>=3,(throwing?"Thrown sword":"Roll thrust")+" runs through real rendered frames");
                    Check(largestAngle<85,(throwing?"Thrown sword":"Roll thrust")+" has no one-frame upright snap (max "+largestAngle.ToString("0.0")+" degrees)");
                    if(throwing)Check((int)typeof(TrialPlayer).GetField("swordEmissions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(player)==1,"Real rendered throw releases exactly one projectile");
                    if(guardThrow)Check(sawFollowup,"Dodge/guard directly chains the rendered throw");
                    yield return .12f;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(input);player.Restore(new Vector3(0,0,20));
                game.enabled=enabled;if(budget)budget.enabled=budgetEnabled;Application.targetFrameRate=cap;QualitySettings.vSyncCount=sync;game.Pause();
            }
        }
    }
}
