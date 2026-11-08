using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace TomorinTrial.Editor
{
    public static partial class TrialValidation
    {
        [MenuItem("Tools/Tomorin/Verify death menu (Play Mode)")]
        public static void RunDeathMenu()
        {
            if(!EditorApplication.isPlaying || routine!=null)throw new InvalidOperationException("Enter Play Mode with the trial running.");
            passed.Clear();Status="Running death menu checks";
            previousBackgroundBehavior=InputSystem.settings.backgroundBehavior;previousEditorBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>("Death menu keyboard");routine=DeathMenuChecks();resumeAt=0;EditorApplication.update+=Step;
        }
        static IEnumerator DeathMenuChecks()
        {
            var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();Check(game && game.Ready,"Trial is ready");
            var pad=InputSystem.AddDevice<Gamepad>("Death menu gamepad");
            try
            {
                game.Input.SetDeviceFilter(new InputDevice[]{keyboard,pad});game.RestartTrial();
                if(game.Player.ChoosingBoon)game.SelectRunBoon(game.Player.OfferA);
                yield return .15f;
                game.Player.ReceiveHit(null,99999,100,true);
                Check(game.Mode==TrialMode.Dead && Time.timeScale==.5f && !game.CameraRig.Frozen,"Player death starts slow motion");
                yield return 3.2f;
                var buttons=game.Hud.GetComponentsInChildren<Button>();var retry=buttons.First(b=>b.name=="Begin");var exit=buttons.First(b=>b.name=="Boss challenge or title");
                Check(game.Mode==TrialMode.Dead && Time.timeScale==0 && game.CameraRig.Frozen && retry.gameObject.activeInHierarchy && exit.gameObject.activeInHierarchy,"Death screen pauses and displays both retry and exit after slow motion");
                Check(exit.GetComponentInChildren<Text>().text=="退出到主菜单","Exit destination is explicitly labeled");
                Check(retry.navigation.selectOnDown==exit && exit.navigation.selectOnUp==retry,"Death buttons have bidirectional explicit navigation");
                Check(!EventSystem.current.currentSelectedGameObject,"Death opens without a selected button");
                Keys(Key.DownArrow);yield return .12f;Keys();yield return .12f;
                Check(EventSystem.current.currentSelectedGameObject==retry.gameObject,"Keyboard navigation focuses retry");
                Keys(Key.Enter);yield return .12f;Keys();yield return .12f;
                Check(game.Mode==TrialMode.Playing && game.Player.Alive,"Keyboard confirmation retries immediately");
                game.Player.ReceiveHit(null,99999,100,true);yield return 3.2f;
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadDown));yield return .12f;
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return .12f;
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadDown));yield return .12f;
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return .12f;
                Check(EventSystem.current.currentSelectedGameObject==exit.gameObject && exit.GetComponent<Image>().color.r>.9f,"Gamepad selects and highlights exit");
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadUp));yield return .12f;
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return .12f;
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return .12f;
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return .12f;
                Check(game.Mode==TrialMode.Playing && game.Player.Alive,"Gamepad confirmation retries immediately");
                game.Player.ReceiveHit(null,99999,100,true);yield return 3.2f;
                Keys(Key.DownArrow);yield return .12f;Keys();yield return .12f;
                Keys(Key.DownArrow);yield return .12f;Keys();yield return .12f;
                Check(EventSystem.current.currentSelectedGameObject==exit.gameObject,"Keyboard selects exit after death delay");
                Keys(Key.Enter);yield return .15f;Keys();yield return .4f;
                Check(UnityEngine.Object.FindFirstObjectByType<InkMainMenu>() && !UnityEngine.Object.FindFirstObjectByType<TrialGame>(),"Confirming exit returns to main menu without respawning");
                var menu=UnityEngine.Object.FindFirstObjectByType<InkMainMenu>();
                menu.GetComponentsInChildren<Button>().First(b=>b.name=="独行入庭").onClick.Invoke();
                menu.GetComponentsInChildren<Button>().First(b=>b.name==TrialChampionCatalog.LevelName(TrialLevel.BigFish)).onClick.Invoke();
                double deadline=EditorApplication.timeSinceStartup+15;
                do{yield return .15f;game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();}while((!game || !game.Ready) && EditorApplication.timeSinceStartup<deadline);
                Check(game && game.Ready,"Main menu can start a fresh trial after death exit");
                if(game.Player.ChoosingBoon)game.SelectRunBoon(game.Player.OfferA);
                game.Player.ReceiveHit(null,99999,100,true);yield return 3.2f;
                exit=game.Hud.GetComponentsInChildren<Button>().First(b=>b.name=="Boss challenge or title");
                ExecuteEvents.Execute(exit.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);yield return .4f;
                Check(UnityEngine.Object.FindFirstObjectByType<InkMainMenu>() && !UnityEngine.Object.FindFirstObjectByType<TrialGame>(),"Mouse click on death exit returns to main menu");
            }
            finally{InputSystem.RemoveDevice(pad);}
        }
    }
}
