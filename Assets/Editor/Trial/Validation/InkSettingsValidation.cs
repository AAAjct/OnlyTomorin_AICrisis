using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TomorinTrial.Editor
{
    public static class InkSettingsValidation
    {
        public static string Status { get; private set; }="Not run";
        static IEnumerator routine;
        static double next;
        static Keyboard keyboard;
        static Gamepad pad;
        static Mouse mouse;
        static InputSystemUIInputModule module;
        static UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
        static InputSettings.BackgroundBehavior background;
        static InputSettings.EditorInputBehaviorInPlayMode editorInput;
        static TrialSettingsPanel panel;
        static float volume,timeScale;
        static bool volumeSaved;
        static readonly List<string> results=new List<string>();

        [MenuItem("Tools/Tomorin/Verify settings focus and ink title (Play Mode)")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying || routine!=null)throw new InvalidOperationException("Enter Play Mode first.");
            panel=UnityEngine.Object.FindObjectsByType<TrialSettingsPanel>(FindObjectsInactive.Include).First();
            module=EventSystem.current.GetComponent<InputSystemUIInputModule>();devices=module.actionsAsset.devices;
            background=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>("Settings focus keyboard");pad=InputSystem.AddDevice<Gamepad>("Settings focus gamepad");
            mouse=InputSystem.AddDevice<Mouse>("Settings focus mouse");
            module.actionsAsset.devices=new InputDevice[]{keyboard,pad,mouse};
            volume=AudioListener.volume;volumeSaved=PlayerPrefs.HasKey("Ink.Volume");timeScale=Time.timeScale;
            results.Clear();Status="Running";routine=Checks();next=0;EditorApplication.update+=Step;
        }
        static void Check(bool ok,string text){if(!ok)throw new Exception(text);results.Add(text);}
        static bool Focused(Selectable control)=>control.GetComponent<Outline>().enabled && control.GetComponent<Image>().color.a>.99f;
        static void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        static IEnumerator Checks()
        {
            int frame=Time.frameCount;
            var menuAtStart=UnityEngine.Object.FindAnyObjectByType<InkMainMenu>();
            var menuButtons=menuAtStart.GetComponentsInChildren<Button>();
            var solo=menuButtons.First(b=>b.name=="独行入庭" || b.name=="单人游玩");
            var multiplayer=menuButtons.First(b=>b.name=="局域网联机");
            solo.Select();Keys(Key.S);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==multiplayer.gameObject && InkMainMenu.NavigationActive,"WASD moves and visibly highlights the main-menu selection");
            Keys(Key.W);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==solo.gameObject,"W returns to the previous main-menu button");
            multiplayer.onClick.Invoke();yield return .12f;
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            T Field<T>(string name)=>(T)typeof(InkMainMenu).GetField(name,flags).GetValue(menuAtStart);
            var browse=Field<Button>("browseRooms");var create=Field<Button>("createRoomTab");var joinTab=Field<Button>("joinRoomTab");
            browse.Select();Keys(Key.S);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==create.gameObject,"Keyboard moves exactly one sidebar tab");
            Keys(Key.DownArrow);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==joinTab.gameObject,"Arrow reaches the merged Join room tab");
            Keys(Key.Enter);yield return .12f;Keys();yield return .12f;
            var address=Field<InputField>("address");var port=Field<InputField>("port");
            Check(address.IsActive() && !port.IsActive() && menuAtStart.GetComponentsInChildren<InputField>().Length==2,"Join room has one target input plus the nickname, with no transport switch");
            Check(Field<InputField>("nickname").transform.Find("Field border") && address.transform.Find("Field border"),"Nickname and join target have persistent outlines");
            Keys(Key.RightArrow);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==address.gameObject && !address.isFocused,"Right enters the unified Join room input without starting editing");
            Keys(Key.S);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==Field<Button>("join").gameObject,"Keyboard reaches the Join button");
            Keys(Key.W);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==address.gameObject,"W returns to the unified input");
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadLeft));yield return .12f;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==joinTab.gameObject,"Gamepad dpad returns to the active sidebar tab");
            InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.right});yield return .12f;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==address.gameObject && !address.isFocused,"Gamepad stick reaches the unified input");
            var focusColor=address.GetComponent<Outline>().effectColor;
            Check(focusColor.b>focusColor.r+.3f && joinTab.GetComponent<Image>().color.r>joinTab.GetComponent<Image>().color.b+.3f,
                "Blue input focus differs from the red active tab");
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return .12f;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return .12f;
            Check(address.isFocused,"Gamepad confirm enters endpoint editing");
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.East));yield return .12f;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return .12f;
            Check(!address.isFocused && address.IsActive(),"Gamepad cancel exits editing and retains Join room");
            string oldAddress=address.text;
            address.Select();address.ActivateInputField();yield return .12f;
            address.text="";
            string clipboard=GUIUtility.systemCopyBuffer;
            try{GUIUtility.systemCopyBuffer="frp-six.com:52134";address.ProcessEvent(Event.KeyboardEvent("^v"));}
            finally{GUIUtility.systemCopyBuffer=clipboard;}
            yield return .12f;
            Check(address.text=="frp-six.com:52134","Pasting a domain and port keeps the complete endpoint in one input");
            address.text="";
            try{GUIUtility.systemCopyBuffer="192.168.1.10:7777";address.ProcessEvent(Event.KeyboardEvent("^v"));}
            finally{GUIUtility.systemCopyBuffer=clipboard;}
            yield return .12f;
            Check(address.text=="192.168.1.10:7777","Pasting IPv4 and port keeps the complete endpoint in one input");
            address.text=oldAddress;address.DeactivateInputField();
            yield return .12f;
            create.onClick.Invoke();
            Check(Field<InputField>("roomName").IsActive(),"Creation exposes the room name field");
            Field<InputField>("roomName").Select();Keys(Key.Tab);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==Field<Button>("changeLevel").gameObject,"Tab moves from room name to the level control");
            Keys(Key.Enter);yield return .12f;Keys();yield return .12f;
            var difficulties=Field<Button[]>("networkDifficulty");
            Keys(Key.RightArrow);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==difficulties[1].gameObject,"Keyboard navigation works inside the level modal");
            Keys(Key.Escape);yield return .12f;Keys();yield return .12f;
            Check(!Field<RectTransform>("networkLevels").gameObject.activeSelf,"Escape closes only the level modal");
            menuAtStart.GetComponentsInChildren<Button>().First(b=>b.name=="返回").onClick.Invoke();yield return .1f;
            panel.Show();yield return .2f;
            Check(Time.frameCount>frame+2,"Live player loop advances");
            var controls=panel.GetComponentsInChildren<Selectable>();var slider=controls.OfType<Slider>().Single(s=>s.name=="Master volume");
            var sprint=controls.First(c=>c.name=="Sprint mode");var motion=controls.First(c=>c.name=="Motion");
            Check(EventSystem.current.currentSelectedGameObject==sprint.gameObject && Focused(sprint),"Opening settings highlights the selected sprint control");
            Keys(Key.DownArrow);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==motion.gameObject && Focused(motion) && !Focused(sprint),"Keyboard down moves the visible focus exactly once");
            Keys(Key.W);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==sprint.gameObject && Focused(sprint),"W visibly navigates settings upward");
            Keys(Key.S);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==motion.gameObject && Focused(motion),"S visibly navigates settings downward");
            var moveRight=controls.Single(c=>c.name=="Bind Right 0");
            var rightArrow=controls.Single(c=>c.name=="Bind Right 1");
            var moveLeft=controls.Single(c=>c.name=="Bind Left 0");
            var volumeNeighbor=controls.Single(c=>c.name=="Bind Down 0");
            slider.Select();slider.value=.5f;
            Keys(Key.RightArrow);yield return .12f;Keys();yield return .12f;
            Check(Focused(volumeNeighbor) && Mathf.Approximately(slider.value,.5f),"Keyboard right from volume enters the nearest binding row");
            moveRight.Select();Keys(Key.RightArrow);yield return .12f;Keys();yield return .12f;
            Check(Focused(rightArrow),"Keyboard right reaches the alternate move-right key");
            moveRight.Select();Keys(Key.UpArrow);yield return .12f;Keys();yield return .12f;
            Check(Focused(moveLeft),"Keyboard up from the move-right binding reaches the move-left binding");
            var moveUp=controls.Single(c=>c.name=="Bind Up 0");
            var lastBinding=controls.Last(c=>c.name.StartsWith("Bind ") && c.name.EndsWith(" 0"));
            moveUp.Select();Keys(Key.UpArrow);yield return .12f;Keys();yield return .12f;
            Check(Focused(controls.Single(c=>c.name=="Restore bindings")),"Keyboard up wraps from the top to the bottom of the binding column");
            lastBinding.Select();Keys(Key.DownArrow);yield return .12f;Keys();yield return .12f;
            Check(Focused(controls.Single(c=>c.name=="Restore bindings")),"Keyboard down reaches Restore bindings below the final binding");
            var upArrow=controls.Single(c=>c.name=="Bind Up 1");
            upArrow.Select();Keys(Key.RightArrow);yield return .12f;Keys();yield return .12f;
            Check(Focused(moveUp),"Keyboard right wraps from the rightmost key");
            Keys(Key.LeftArrow);yield return .12f;Keys();yield return .12f;
            Check(Focused(upArrow),"Keyboard left wraps back to the rightmost key");
            slider.Select();Keys(Key.Enter);yield return .12f;Keys();yield return .12f;
            Keys(Key.RightArrow);yield return .12f;Keys();yield return .12f;
            Check(slider.value>.5f && Focused(slider) && Mathf.Approximately(AudioListener.volume,slider.value),"Confirm enters volume editing and right raises volume");
            Keys(Key.Enter);yield return .12f;Keys();yield return .12f;
            controls.Single(c=>c.name=="Boss 技能与预警").Select();
            var point=RectTransformUtility.WorldToScreenPoint(null,motion.transform.TransformPoint(((RectTransform)motion.transform).rect.center));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point,delta=Vector2.one});yield return .12f;
            Check(Focused(motion) && !Focused(slider) && !Focused(sprint),"Mouse hover hides retained navigation focus and highlights only the hovered button");
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(10,10),delta=Vector2.one});yield return .12f;
            Check(!Focused(motion) && !Focused(slider) && !Focused(sprint),"Moving the mouse to empty space clears button highlights");
            Keys(Key.DownArrow);yield return .12f;Keys();yield return .12f;
            Check(Focused(sprint) && EventSystem.current.currentSelectedGameObject==sprint.gameObject,"Keyboard navigation resumes from retained selection after mouse use");
            Keys(Key.UpArrow);yield return .12f;Keys();yield return .12f;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point,delta=Vector2.one});yield return .12f;
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadDown));yield return .12f;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==sprint.gameObject && Focused(sprint) && !Focused(motion),"Gamepad navigation suppresses stationary mouse hover and focuses sprint");
            moveRight.Select();
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadUp));yield return .12f;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return .12f;
            Check(Focused(moveLeft),"Gamepad up follows the binding column");
            moveRight.Select();
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadRight));yield return .12f;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return .12f;
            Check(Focused(rightArrow),"Gamepad right follows the binding row");
            slider.Select();
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadRight));yield return .12f;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return .12f;
            Check(Focused(volumeNeighbor),"Gamepad right from volume enters the nearest binding row");
            foreach(var button in controls.OfType<Button>())
            {
                button.Select();yield return .12f;
                Check(Focused(button),"Visible focus: "+button.name);
            }
            var binding=controls.OfType<Button>().First(b=>b.name.StartsWith("Bind "));
            binding.Select();yield return .05f;binding.onClick.Invoke();yield return .12f;
            Check(panel.IsRebinding && !binding.interactable && !Focused(binding),"Rebinding disables controls and removes misleading active focus");
            Keys(Key.Escape);yield return .12f;Keys();yield return .12f;
            Check(!panel.IsRebinding && binding.interactable && Focused(binding),"Canceling rebind restores the same binding focus");
            for(int i=0;i<3;i++){panel.Hide();yield return .04f;panel.Show();yield return .06f;Check(Focused(sprint),"Reopening restores focus, cycle "+(i+1));}
            Time.timeScale=0;slider.Select();yield return .12f;
            Check(Focused(slider),"Paused time scale preserves volume focus rendering");Time.timeScale=timeScale;
            Directory.CreateDirectory("Logs/Trial");
            InkStartBuilder.Capture("Logs/Trial/SettingsFocus.png");
            controls.OfType<Button>().First(b=>b.name=="Back").onClick.Invoke();yield return .1f;
            var menu=UnityEngine.Object.FindAnyObjectByType<InkMainMenu>();
            var title=menu.GetComponentsInChildren<RawImage>(true).First(i=>i.name=="Ink calligraphy title");
            Check(title.gameObject.activeInHierarchy && title.texture && title.texture.width>1000 && !title.raycastTarget,"High-resolution calligraphy texture is visible without intercepting menu input");
            InkStartBuilder.Capture("Logs/Trial/InkTitle.png");
        }
        static void Step()
        {
            if(EditorApplication.timeSinceStartup<next)return;
            try{if(!routine.MoveNext()){Finish(null);return;}next=EditorApplication.timeSinceStartup+(routine.Current is float t?t:0);}
            catch(Exception e){Finish(e);}
        }
        static void Finish(Exception error)
        {
            EditorApplication.update-=Step;routine=null;
            module.actionsAsset.devices=devices;InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(pad);InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            AudioListener.volume=volume;Time.timeScale=timeScale;
            if(volumeSaved)PlayerPrefs.SetFloat("Ink.Volume",volume);else PlayerPrefs.DeleteKey("Ink.Volume");
            if(panel)panel.Hide();
            Status=error==null?"Passed "+results.Count+" checks":"FAILED: "+error.Message;
            Directory.CreateDirectory("Logs/Trial");File.WriteAllText("Logs/Trial/InkSettingsValidation.txt",Status+"\n"+string.Join("\n",results)+(error==null?"":"\n"+error));
            if(error==null)Debug.Log(Status);else Debug.LogError(Status);
        }
    }
}
