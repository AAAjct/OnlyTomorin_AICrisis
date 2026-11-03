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
            panel=UnityEngine.Object.FindObjectsByType<TrialSettingsPanel>(FindObjectsInactive.Include,FindObjectsSortMode.None).First();
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
            var menuAtStart=UnityEngine.Object.FindFirstObjectByType<InkMainMenu>();
            var menuButtons=menuAtStart.GetComponentsInChildren<Button>();
            var solo=menuButtons.First(b=>b.name=="独行入庭" || b.name=="单人游玩");
            var multiplayer=menuButtons.First(b=>b.name=="局域网联机");
            solo.Select();Keys(Key.S);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==multiplayer.gameObject && InkMainMenu.NavigationActive,"WASD moves and visibly highlights the main-menu selection");
            Keys(Key.W);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==solo.gameObject,"W returns to the previous main-menu button");
            multiplayer.onClick.Invoke();yield return .12f;
            var fields=menuAtStart.GetComponentsInChildren<InputField>();
            var address=fields.First(f=>f.name=="房主 IP / 穿透域名");var port=fields.First(f=>f.name=="TCP 端口");
            string oldAddress=address.text,oldPort=port.text;
            address.Select();address.ActivateInputField();yield return .12f;
            address.text="";
            string clipboard=GUIUtility.systemCopyBuffer;
            try{GUIUtility.systemCopyBuffer="frp-six.com:52134";address.ProcessEvent(Event.KeyboardEvent("^v"));}
            finally{GUIUtility.systemCopyBuffer=clipboard;}
            yield return .12f;
            Check(address.text=="frp-six.com" && port.text=="52134","Pasting a domain and port populates both actual input fields");
            address.text="";
            try{GUIUtility.systemCopyBuffer="192.168.1.10:7777";address.ProcessEvent(Event.KeyboardEvent("^v"));}
            finally{GUIUtility.systemCopyBuffer=clipboard;}
            yield return .12f;
            Check(address.text=="192.168.1.10" && port.text=="7777","Pasting IPv4 and port populates both actual input fields");
            address.text=oldAddress;port.text=oldPort;
            menuAtStart.GetComponentsInChildren<Button>().First(b=>b.name=="返回").onClick.Invoke();yield return .1f;
            panel.Show();yield return .2f;
            Check(Time.frameCount>frame+2,"Live player loop advances");
            var controls=panel.GetComponentsInChildren<Selectable>();var slider=controls.OfType<Slider>().Single();
            var sprint=controls.First(c=>c.name=="Sprint mode");var motion=controls.First(c=>c.name=="Motion");
            Check(EventSystem.current.currentSelectedGameObject==sprint.gameObject && Focused(sprint),"Opening settings highlights the selected sprint control");
            Keys(Key.DownArrow);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==motion.gameObject && Focused(motion) && !Focused(sprint),"Keyboard down moves the visible focus exactly once");
            Keys(Key.W);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==sprint.gameObject && Focused(sprint),"W visibly navigates settings upward");
            Keys(Key.S);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==motion.gameObject && Focused(motion),"S visibly navigates settings downward");
            Keys(Key.UpArrow);yield return .12f;Keys();yield return .12f;
            Keys(Key.UpArrow);yield return .12f;Keys();yield return .12f;
            Check(EventSystem.current.currentSelectedGameObject==slider.gameObject && Focused(slider),"Keyboard up highlights the entire volume track");
            slider.value=.5f;Keys(Key.RightArrow);yield return .12f;Keys();yield return .12f;
            Check(slider.value>.5f && Focused(slider) && Mathf.Approximately(AudioListener.volume,slider.value),"Keyboard right changes volume while preserving focus");
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
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadUp));yield return .12f;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return .12f;
            slider.value=.5f;InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadRight));yield return .12f;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return .12f;
            Check(slider.value>.5f && Focused(slider),"Gamepad right changes volume while preserving track focus");
            foreach(var button in controls.OfType<Button>())
            {
                button.Select();yield return .03f;
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
            var menu=UnityEngine.Object.FindFirstObjectByType<InkMainMenu>();
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
