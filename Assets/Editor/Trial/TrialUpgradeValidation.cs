using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TomorinTrial.Editor
{
    public static partial class TrialValidation
    {
        [MenuItem("Tools/Tomorin/Verify movement and DeepSeek boss (Play Mode)")]
        public static void RunUpgrade()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Start AshenCourt in Play Mode first.");
            if(routine!=null)throw new InvalidOperationException("Validation is already running.");
            passed.Clear();Status="Running movement and DeepSeek checks";
            previousBackgroundBehavior=InputSystem.settings.backgroundBehavior;previousEditorBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>("Movement validation keyboard");
            routine=UpgradeChecks();resumeAt=0;EditorApplication.update+=Step;
        }

        static IEnumerator UpgradeChecks()
        {
            var regression=Checks(false);while(regression.MoveNext())yield return regression.Current;
            var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
            game.RestartTrial();Keys();yield return .15f;
            var player=game.Player;var start=player.transform.position;
            Transform thigh=null;foreach(var t in player.Visual.GetComponentsInChildren<Transform>())if(t.name=="leftthigh")thigh=t;
            Check(thigh!=null,"Tomorin's weighted thigh is resolved for locomotion");
            Keys(Key.W,Key.LeftShift);yield return .55f;
            var pose=thigh.localRotation;float legMotion=0;
            for(int i=0;i<6;i++){yield return .06f;legMotion=Mathf.Max(legMotion,Quaternion.Angle(pose,thigh.localRotation));}
            Check(legMotion>8,"Sprinting visibly animates the actual weighted leg bones");
            Check(player.Sprinting && player.MoveBlend>1.3f && player.transform.position.z-start.z>3,"Sprint reaches full speed and drives the sprint pose");
            Keys();var stop=player.transform.position;yield return .30f;
            Check(Vector3.Distance(player.transform.position,stop)<.65f && player.PlanarVelocity.sqrMagnitude<.01f,"Releasing movement brakes promptly without sliding");
            Keys(Key.W);yield return .3f;Keys(Key.S);yield return .25f;
            Check(player.PlanarVelocity.z< -3 && Vector3.Dot(player.transform.forward,Vector3.back)>.95f,"Reversing input changes velocity and facing responsively");
            Keys();yield return .2f;
            player.Restore(new Vector3(0,0,-21));game.CameraRig.Snap();
            var guard=game.Enemies[0];guard.Teleport(new Vector3(0,0,-17));guard.Parried();player.ToggleLock();
            Keys(Key.D);yield return .35f;
            Check(player.Locked && Vector3.Dot(player.transform.forward,(guard.transform.position-player.transform.position).normalized)>.9f && Mathf.Abs(player.LocalMotion.x)>.5f,"Locked movement strafes while keeping the enemy in front");
            Keys();guard.ResetFighter();player.Restore(new Vector3(0,0,-21));game.CameraRig.Snap();yield return .2f;
            Keys(Key.J);yield return .06f;Keys();yield return .20f;Keys(Key.J);yield return .06f;Keys();yield return .23f;
            Check(player.Action==FighterAction.Light && player.Combo==1,"Buffered input chains into the second sword attack");
            yield return .17f;Keys(Key.Space);yield return .08f;Keys();
            Check(player.Action==FighterAction.Dodge,"Dodge cancels the recovery of a combo attack");
            yield return .65f;

            game.StartBossChallenge();Keys();yield return .2f;
            var boss=game.Enemies[3];var brain=boss.Boss;
            Check(brain && boss.Visual.name.Contains("DeepSeek") && boss.Visual.GetComponentsInChildren<SkinnedMeshRenderer>().Length==6,"Boss uses the imported DeepSeek model and its six skinned meshes");
            Check(game.GateOpen && player.transform.position.z>18,"Direct Boss challenge opens the arena and restores the player");
            boss.Parried();yield return .1f;
            boss.Teleport(new Vector3(0,0,25));player.Restore(new Vector3(0,0,22));
            brain.StartSkill(DeepFishSkill.TailSweep);float hp=player.Health;
            yield return .25f;
            Check(brain.WindingUp && !brain.Unblockable && player.Health==hp,"Tail sweep has a visible, blockable anticipation before contact");
            yield return .55f;
            Check(player.Health<hp,"Tail sweep deals damage during its active window");
            boss.Parried();player.Restore(new Vector3(0,0,22));
            brain.StartSkill(DeepFishSkill.TailSweep);yield return .55f;
            hp=player.Health;int parries=player.SuccessfulParries;Keys(Key.K);yield return .22f;Keys();
            Check(player.SuccessfulParries>parries && player.Health==hp && boss.Vulnerable && brain.Skill==DeepFishSkill.None,"A real timed guard parries the Boss and cancels its skill");
            player.Restore(new Vector3(6,0,22));brain.StartSkill(DeepFishSkill.TidalCharge);
            var origin=boss.transform.position;yield return 1.2f;
            Check(Vector3.Distance(boss.transform.position,origin)>3 && brain.Unblockable,"Charge travels along its committed direction and is unblockable");
            boss.Parried();boss.Teleport(new Vector3(0,0,25));player.Restore(new Vector3(0,0,20));
            int attacks=brain.AttacksStarted;
            brain.StartSkill(DeepFishSkill.DelayedSlam);
            Check(brain.Skill==DeepFishSkill.None && brain.AttacksStarted==attacks && brain.ActiveHazards==0,"Retired feint slam cannot start or spawn hazards");
            brain.StartSkill(DeepFishSkill.BellySlam);
            var slamOrigin=boss.transform.position;yield return .95f;
            Check(brain.Skill==DeepFishSkill.BellySlam && boss.Visual.localPosition.y>1,"Ordinary slam still lifts the model off the ground");
            Check(Vector3.ProjectOnPlane(boss.transform.position-slamOrigin,Vector3.up).sqrMagnitude<.0001f && Vector3.ProjectOnPlane(brain.CaptureNetwork().aim-slamOrigin,Vector3.up).sqrMagnitude<.0001f,"Airborne slam stays over its fixed replicated landing point");
            player.Teleport(new Vector3(-7,0,20));hp=player.Health;yield return .65f;
            Check(player.Health==hp && brain.ActiveHazards>0,"Leaving the stationary slam avoids impact while its shockwave spawns");
            Check(Vector3.ProjectOnPlane(boss.transform.position-slamOrigin,Vector3.up).sqrMagnitude<.0001f,"Slam lands without horizontal travel");
            boss.Parried();boss.Teleport(new Vector3(0,0,25));player.Restore(new Vector3(0,0,19));
            brain.StartSkill(DeepFishSkill.BubbleBarrage);yield return 1.12f;
            Check(brain.ActiveHazards>=7 && brain.ActiveHazards<=32,"Bubble barrage emits a bounded pool of moving projectiles");
            game.Pause();float progress=brain.SkillProgress;yield return .2f;
            Check(Mathf.Approximately(progress,brain.SkillProgress),"Pausing freezes Boss skill progression and hazards");game.Resume();
            boss.Parried();Check(brain.ActiveHazards==0,"Parrying clears all active hazards");
            boss.ResetFighter();boss.ReceiveHit(player,boss.MaxHealth*.5f,0);yield return .12f;
            Check(boss.PhaseTwo && brain.Enraged && brain.Skill==DeepFishSkill.Enrage,"Half health enters the authored deep-sea awakening");
            brain.CancelAttack();brain.StartSkill(DeepFishSkill.Maelstrom);yield return 1.6f;
            Check(brain.ActiveHazards>0 && brain.Unblockable,"Second phase creates expanding unblockable tide rings");
            game.Respawn();yield return .1f;
            Check(brain.ActiveHazards==0 && !brain.Enraged && !boss.Awakened && boss.Health==boss.MaxHealth,"Respawn resets Boss health, awakening, skill state and hazard pool");
            game.StartBossChallenge();brain.StartSkill(DeepFishSkill.BubbleBarrage);yield return 1.1f;
            boss.ReceiveHit(player,10000,100,true);yield return .12f;
            Check(game.Mode==TrialMode.Victory && brain.ActiveHazards==0,"Boss death completes the trial and removes remaining projectiles");
            game.ReturnToTitle();
        }
    }
}

