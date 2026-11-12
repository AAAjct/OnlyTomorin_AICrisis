using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TomorinTrial.Editor
{
    public static partial class TrialValidation
    {
        [MenuItem("Tools/Tomorin/Verify NaiWa presentation and corpse lifecycle (Play Mode)")]
        public static void RunPresentation()
        {
            if(!EditorApplication.isPlaying || routine!=null)throw new InvalidOperationException("Enter Play Mode with no other validation running.");
            passed.Clear();Status="Running presentation checks";
            previousBackgroundBehavior=InputSystem.settings.backgroundBehavior;previousEditorBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>("Presentation keyboard");routine=PresentationChecks();resumeAt=0;EditorApplication.update+=Step;
        }
        static Transform Bone(TrialEnemy enemy,string name)=>enemy.Visual.GetComponentsInChildren<Transform>().First(t=>t.name==name);
        static IEnumerator PresentationChecks()
        {
            double deadline;
            var game=UnityEngine.Object.FindAnyObjectByType<TrialGame>();Check(game && game.Ready,"Combat bootstrap is ready");
            game.Input.SetDeviceFilter(new InputDevice[]{keyboard});game.RestartTrial();Keys();yield return .25f;
            var enemy=game.Enemies[0];var left=Bone(enemy,"hand.L");var right=Bone(enemy,"hand.R");
            Vector3 lp=enemy.transform.InverseTransformPoint(left.position),rp=enemy.transform.InverseTransformPoint(right.position);
            Check(lp.y<.90f && rp.y<.90f && Mathf.Abs(lp.x)<.49f && Mathf.Abs(rp.x)<.49f,"Idle NaiWa lowers both weighted hands from the T-pose");
            enemy.Teleport(game.Player.transform.position+Vector3.forward*1.65f);enemy.transform.rotation=Quaternion.Euler(0,180,0);
            float health=game.Player.Health;deadline=EditorApplication.timeSinceStartup+3;
            while(enemy.Action!=FighterAction.Light && EditorApplication.timeSinceStartup<deadline)yield return .04f;
            Check(enemy.Action==FighterAction.Light,"NaiWa AI enters its hand attack at melee range");
            var hand=enemy.Combo==1?left:right;Vector3 first=enemy.transform.InverseTransformPoint(hand.position);float travel=0;
            for(int i=0;i<13;i++){yield return .075f;travel=Mathf.Max(travel,Vector3.Distance(first,enemy.transform.InverseTransformPoint(hand.position)));}
            Check(travel>.18f,"Weighted attacking hand swings through a visible arc (travel="+travel.ToString("F2")+")");
            Check(game.Player.Health<health,"Hand attack contact deals damage to the player");
            game.Player.Restore(new Vector3(0,0,-21));enemy.ResetFighter();enemy.Teleport(new Vector3(0,0,-17));yield return .45f;
            Check(enemy.MoveBlend>.5f,"NaiWa is walking before the lethal hit");
            enemy.ReceiveHit(game.Player,9999,100,true);Check(!enemy.Alive && enemy.MoveBlend==0,"Death immediately clears locomotion weight");
            yield return 1.35f;var thigh=Bone(enemy,"thigh.L");Quaternion settled=thigh.localRotation;yield return .4f;
            Check(Quaternion.Angle(settled,thigh.localRotation)<.1f,"Settled corpse no longer cycles the walking legs");
            var rig=enemy.GetComponent<TrialNaiWaRig>();Check(rig.CorpseOpacity==1,"Corpse remains visible briefly after falling");
            game.Pause();float age=enemy.ActionTime;yield return .2f;Check(Mathf.Approximately(age,enemy.ActionTime),"Pause freezes the corpse lifecycle");game.Resume();
            yield return 1.45f;Check(rig.CorpseOpacity>0 && rig.CorpseOpacity<.85f,"Corpse gradually fades between the hold and disappearance");
            yield return 1.5f;Check(enemy.Visual.GetComponentsInChildren<Renderer>().All(r=>!r.enabled),"Finished corpse fade disables its renderers");
            game.Respawn();yield return .15f;
            Check(enemy.Alive && rig.CorpseOpacity==1 && enemy.Visual.GetComponentsInChildren<Renderer>().All(r=>r.enabled),"Respawn restores visible materials and the living pose");
            var state=enemy.CaptureNetwork(0);state.health=0;state.action=(byte)FighterAction.Dead;state.actionTime=3.3f;state.movement=1;
            enemy.ApplyReplica(state,.05f);yield return .1f;
            Check(enemy.MoveBlend==0 && rig.CorpseOpacity<1,"Replicated death clears gait and resumes the synchronized corpse fade");
            game.Respawn();yield return .1f;
            Check(enemy.Visual.GetComponentsInChildren<Renderer>().All(r=>r.enabled),"Repeated death and restoration leave no hidden mesh");
        }
    }
}
