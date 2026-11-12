using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TomorinTrial.Editor
{
    public static partial class TrialValidation
    {
        [MenuItem("Tools/Tomorin/Verify combat and interface polish (Play Mode)")]
        public static void RunPolish()
        {
            if(!EditorApplication.isPlaying || routine!=null)throw new InvalidOperationException("Enter Play Mode with no validation running.");
            passed.Clear();Status="Running polish checks";
            previousBackgroundBehavior=InputSystem.settings.backgroundBehavior;previousEditorBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>("Polish keyboard");routine=PolishChecks();resumeAt=0;EditorApplication.update+=Step;
        }
        static IEnumerator PolishChecks()
        {
            var regression=Checks(false);while(regression.MoveNext())yield return regression.Current;
            var game=UnityEngine.Object.FindAnyObjectByType<TrialGame>();var player=game.Player;
            game.RestartTrial();Keys();yield return .15f;
            Check(game.Enemies[0].Visual.name.Contains("NaiWa") && game.Enemies[0].Weapon==null,"Guards use NaiWa and bare hands");
            Check(InkMainMenu.TrySplitEndpoint("frp-six.com:24456",out var address,out var port) && address=="frp-six.com" && port=="24456","Pasted hostname and TCP port split correctly");
            Check(!InkMainMenu.TrySplitEndpoint("host:99999",out _,out _) && !InkMainMenu.TrySplitEndpoint("2001:db8::1",out _,out _),"Invalid ports and bare IPv6 are not split");
            Keys(Key.W,Key.LeftShift);yield return .15f;Keys(Key.W);yield return .25f;
            Check(player.Sprinting,"Sprint remains enabled after Shift release");
            Keys(Key.W,Key.LeftShift);yield return .1f;Keys(Key.W);yield return .15f;
            Check(!player.Sprinting,"Second Shift press disables sprint");Keys();
            foreach(float side in new[]{-9f,9f})
            {
                player.Restore(new Vector3(side,0,14));game.CameraRig.Snap();Keys(Key.W);yield return .7f;Keys();
                Check(player.transform.position.z<15.8f,"Side fence blocks bypass at x="+side);
            }
            game.RestartTrial();yield return .15f;
            Keys(Key.J);yield return .07f;Keys();yield return .17f;Keys(Key.Q);yield return .07f;Keys();yield return .10f;
            Check(player.Action==FighterAction.Heavy && player.Combo==4,"Light then heavy branches into rising slash");
            yield return .85f;player.Restore(new Vector3(0,0,-21));
            Keys(Key.J);yield return .07f;Keys();yield return .17f;Keys(Key.J);yield return .07f;Keys();yield return .10f;
            Check(player.Action==FighterAction.Light && player.Combo==1,"Second light attack chains through buffered input");
            yield return .13f;Keys(Key.Space);yield return .07f;Keys();yield return .10f;
            Check(player.Action==FighterAction.Dodge && player.Combo==7,"Two lights then dodge trigger wind dodge slash");
            yield return .06f;Keys(Key.J);yield return .07f;Keys();yield return .10f;
            Check(player.Action==FighterAction.Light && player.Combo==8,"Light during dodge recovery becomes pursuit thrust");
            yield return .6f;player.Restore(new Vector3(0,0,-21));
            Keys(Key.J);yield return .07f;Keys();yield return .19f;Keys(Key.K);yield return .12f;
            Check(player.Guarding && player.Action==FighterAction.Free,"Guard cancels light recovery");Keys();
            player.Restore(new Vector3(0,0,-21));
            Keys(Key.J);yield return .07f;Keys();yield return .17f;Keys(Key.J);yield return .07f;Keys();yield return .23f;
            Keys(Key.Q);yield return .07f;Keys();yield return .12f;
            Check(player.Action==FighterAction.Heavy && player.Combo==5,"Two lights then heavy trigger long thrust");
            yield return .9f;player.Restore(new Vector3(0,0,-21));
            Keys(Key.J);yield return .07f;Keys();yield return .17f;Keys(Key.J);yield return .07f;Keys();yield return .23f;
            Keys(Key.J);yield return .07f;Keys();yield return .27f;Keys(Key.Q);yield return .07f;Keys();yield return .15f;
            Check(player.Action==FighterAction.Heavy && player.Combo==6,"Three lights then heavy trigger overhead finisher");
            yield return .9f;player.Restore(new Vector3(0,0,-21));
            Keys(Key.J);yield return .07f;Keys();yield return .17f;Keys(Key.J);yield return .07f;Keys();yield return .23f;
            Keys(Key.J);yield return .07f;Keys();yield return .27f;Keys(Key.J);yield return .07f;Keys();yield return .15f;
            Check(player.Action==FighterAction.Light && player.Combo==3,"Four light attacks reach the fourth distinct cut");
            yield return .8f;player.Restore(new Vector3(0,0,-21));
            player.ReceiveHit(null,60,0,true);yield return .5f;Keys(Key.R);yield return .07f;Keys();yield return .4f;
            Transform flask=player.transform.Find("灵露 · 手持药瓶");
            Check(flask && flask.gameObject.activeInHierarchy && flask.GetComponentsInChildren<Renderer>().Length==3,"Healing visibly equips the three-part potion bottle");
            yield return 1f;Check(!flask.gameObject.activeSelf,"Potion bottle hides after the drink");
            Check(game.Hud.GetComponentInChildren<TrialTouchControl>(true)==null,"Desktop HUD contains no mobile buttons");
            var frog=game.Enemies[0];frog.Teleport(player.transform.position+Vector3.forward*.15f);frog.Parried();player.ToggleLock();
            game.CameraRig.Snap();yield return .3f;float cameraYaw=game.CameraRig.transform.eulerAngles.y;
            for(int i=0;i<6;i++){frog.Teleport(player.transform.position+new Vector3(i%2==0?.05f:-.05f,0,.05f));yield return .05f;}
            Check(Mathf.Abs(Mathf.DeltaAngle(cameraYaw,game.CameraRig.transform.eulerAngles.y))<1,"Near-overlap lock target does not flip or jitter camera orbit");
            player.ToggleLock();
            game.StartBossChallenge();player.Teleport(new Vector3(0,0,20));yield return .15f;
            Check(game.World.ArenaEntrance.activeSelf,"Entering the boss arena seals its entrance");
            game.Enemies[3].Parried();player.Teleport(new Vector3(0,0,17));player.transform.rotation=Quaternion.identity;game.CameraRig.Snap();
            Keys(Key.S);yield return .8f;Keys();Check(player.transform.position.z>15.4f,"Arena entrance prevents walking out during combat");
            game.Respawn();yield return .1f;Check(!game.World.ArenaEntrance.activeSelf,"Respawn resets arena closure");
            var pad=InputSystem.AddDevice<Gamepad>("Polish gamepad");
            try
            {
                game.Input.SetDeviceFilter(new InputDevice[]{keyboard,pad});
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.Start));yield return .12f;
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return .12f;
                Check(game.Mode==TrialMode.Paused,"Gamepad Start opens pause");
                Check(!EventSystem.current.currentSelectedGameObject,"Pause opens without a selected button");
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadDown));yield return .12f;
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return .1f;
                var selected=EventSystem.current.currentSelectedGameObject;
                Check(selected && selected.name=="Begin","First D-pad move focuses Continue");
                Check(selected.GetComponent<UnityEngine.UI.Image>().color.r>.9f,"Focused pause button has a bright gold background");
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadDown));yield return .12f;
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return .1f;
                Check(EventSystem.current.currentSelectedGameObject.name=="Settings","D-pad down selects Settings");
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadUp));yield return .12f;
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return .1f;
                Check(EventSystem.current.currentSelectedGameObject.name=="Begin","D-pad up selects the upper pause item");
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return .12f;
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return .1f;
                Check(game.Mode==TrialMode.Playing,"Gamepad A activates Continue");
            }
            finally{InputSystem.RemoveDevice(pad);game.Input.SetDeviceFilter(new InputDevice[]{keyboard});}
            typeof(TrialHud).GetMethod("InitializeTouchControls",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game.Hud,new object[]{true});
            yield return .1f;
            TrialTouchControl stick=null,attack=null,guardTouch=null;
            foreach(var control in game.Hud.GetComponentsInChildren<TrialTouchControl>()){if(control.Stick)stick=control;if(control.Guard)guardTouch=control;if(control.Action==1)attack=control;}
            Check(stick && attack,"Mobile controls include joystick and attack button");
            var pointer=new PointerEventData(EventSystem.current){pointerId=1};
            var stickRect=(RectTransform)stick.transform;
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,stickRect.TransformPoint(stickRect.rect.center+Vector2.right*60));
            stick.OnPointerDown(pointer);Check(game.Input.TouchMove.x>.5f,"Touch joystick maps drag displacement to movement");stick.OnPointerUp(pointer);
            Check(game.Input.TouchMove==Vector2.zero,"Releasing joystick clears movement");
            guardTouch.OnPointerDown(pointer);yield return .1f;Check(player.Guarding,"Holding touch guard enters guard state");guardTouch.OnPointerUp(pointer);yield return .1f;
            attack.OnPointerDown(pointer);yield return .1f;attack.OnPointerUp(pointer);
            Check(player.Action==FighterAction.Light,"Touch attack enters the shared combat input pipeline");
            game.Pause();yield return .1f;Check(!attack.gameObject.activeInHierarchy && game.Input.TouchMove==Vector2.zero,"Pause hides mobile gameplay controls and releases held touch");game.Resume();
            game.RestartTrial();Keys();
        }
    }
}
