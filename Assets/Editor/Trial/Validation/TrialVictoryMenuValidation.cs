using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TomorinTrial.Editor
{
    public static partial class TrialValidation
    {
        [MenuItem("Tools/Tomorin/Verify victory menu (Play Mode)")]
        public static void RunVictoryMenu()
        {
            if(!EditorApplication.isPlaying || routine!=null)throw new InvalidOperationException("Enter Play Mode with the trial running.");
            passed.Clear();Status="Running victory menu checks";
            previousBackgroundBehavior=InputSystem.settings.backgroundBehavior;previousEditorBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>("Victory menu keyboard");routine=VictoryMenuChecks();resumeAt=0;EditorApplication.update+=Step;
        }

        static IEnumerator VictoryMenuChecks()
        {
            var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
            Check(game && game.Ready,"Trial is ready");
            game.Input.SetDeviceFilter(new InputDevice[]{keyboard});game.StartBossChallenge();yield return .15f;
            var boss=game.Enemies[game.Enemies.Count-1];
            boss.ReceiveHit(game.Player,99999,100,true);
            Check(game.Mode==TrialMode.Victory && Time.timeScale==.5f && !game.CameraRig.Frozen,"Boss death immediately starts the slow-motion result");
            yield return .75f;
            Check(boss.Progress>0 && boss.Progress<1 && Time.timeScale==.5f,"Boss fall advances during slow motion");
            Check(!game.Hud.GetComponentsInChildren<Button>().Any(b=>b.name=="Begin"),"Victory menu stays hidden while the boss falls");
            yield return 2.45f;
            Check(boss.Progress>.99f && Time.timeScale==0 && game.CameraRig.Frozen,"Victory pauses once the boss has fallen");
            var buttons=game.Hud.GetComponentsInChildren<Button>();
            var retry=buttons.First(b=>b.name=="Begin");var title=buttons.First(b=>b.name=="Boss challenge or title");
            Check(buttons.Count(b=>b.gameObject.activeInHierarchy)==2,"Victory shows exactly two buttons");
            Check(retry.GetComponentInChildren<Text>().text=="重新挑战" && title.GetComponentInChildren<Text>().text=="返回标题","Victory buttons use requested labels");
            Check(!EventSystem.current.currentSelectedGameObject,"Victory opens without a selected button");
            Check(Cursor.lockState==CursorLockMode.None && Cursor.visible,"Victory releases cursor for mouse interaction");
            var position=game.Player.transform.position;var cameraPosition=game.CameraRig.transform.position;var cameraRotation=game.CameraRig.transform.rotation;
            var bossBones=boss.GetComponent<TrialBossRig>().DrivenBones;var rotations=bossBones.Select(b=>b.localRotation).ToArray();
            float seconds=game.PlaySeconds;
            game.CameraRig.Look(new Vector2(120,50));Keys(UnityEngine.InputSystem.Key.W,UnityEngine.InputSystem.Key.J,UnityEngine.InputSystem.Key.Escape);yield return .4f;Keys();yield return .15f;
            Check(game.Mode==TrialMode.Victory && Time.timeScale==0 && game.PlaySeconds==seconds,"Gameplay keys and Escape cannot resume victory or advance timer");
            Check(Vector3.Distance(position,game.Player.transform.position)<.0001f,"Player cannot move after victory");
            Check(Vector3.Distance(cameraPosition,game.CameraRig.transform.position)<.0001f && Quaternion.Angle(cameraRotation,game.CameraRig.transform.rotation)<.001f,"Camera position and rotation remain frozen despite look input");
            Check(bossBones.Select((b,i)=>Quaternion.Angle(b.localRotation,rotations[i])<.001f).All(v=>v),"Boss presentation remains frozen while paused");
            ExecuteEvents.Execute(retry.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);yield return .2f;
            if(game.Player.ChoosingBoon)game.SelectRunBoon(game.Player.OfferA);
            yield return .1f;
            Check(game.Mode==TrialMode.Playing && Time.timeScale>0 && !game.CameraRig.Frozen && boss.Alive && game.Player.Alive,"Mouse retry restores gameplay, camera and combatants");
            Check(game.PlaySeconds<1 && game.Deaths==0 && game.DefeatedGuards==0,"Retry resets challenge progress");
            position=game.Player.transform.position;Keys(UnityEngine.InputSystem.Key.W);yield return .25f;Keys();yield return .1f;
            Check(Vector3.Distance(position,game.Player.transform.position)>.2f,"Movement works after retry");
            game.StartBossChallenge();boss.ReceiveHit(game.Player,99999,100,true);yield return 3.2f;
            Keys(UnityEngine.InputSystem.Key.DownArrow);yield return .12f;Keys();yield return .12f;
            Keys(UnityEngine.InputSystem.Key.DownArrow);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==title.gameObject,"Keyboard can select return to title");
            Keys(UnityEngine.InputSystem.Key.Enter);yield return .12f;Keys();yield return .5f;
            Check(UnityEngine.Object.FindFirstObjectByType<InkMainMenu>() && !UnityEngine.Object.FindFirstObjectByType<TrialGame>(),"Confirming return loads the title scene");
            Check(Time.timeScale==1,"Return to title restores time scale");
        }
    }
}
