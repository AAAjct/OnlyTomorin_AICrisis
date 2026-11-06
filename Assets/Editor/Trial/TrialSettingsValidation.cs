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

namespace TomorinTrial.Editor
{
    public static partial class TrialValidation
    {
        [MenuItem("Tools/Tomorin/Verify shared settings and rebinding (Play Mode)")]
        public static void RunSettings()
        {
            if(!EditorApplication.isPlaying || routine!=null)throw new InvalidOperationException("Start InkStart in Play Mode with no validation running.");
            passed.Clear();Status="Running settings checks";
            previousBackgroundBehavior=InputSystem.settings.backgroundBehavior;previousEditorBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>("Settings validation keyboard");routine=SettingsChecks();resumeAt=0;EditorApplication.update+=Step;
        }
        static UnityEngine.UI.Button SettingsButton(TrialSettingsPanel panel,string name)=>panel.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name==name);
        static string SettingsLayout(TrialSettingsPanel panel)=>string.Join("|",panel.GetComponentsInChildren<RectTransform>(true).Select(r=>r.name+":"+r.anchoredPosition+":"+r.sizeDelta));
        static IEnumerator SettingsChecks()
        {
            var snapshot=new Dictionary<string,string>();
            foreach(var entry in TrialBindings.Entries)for(int slot=0;slot<3;slot++)
            {string key="Ink.Binding."+entry.Id+"."+slot;snapshot[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetString(key):null;}
            bool sprint=TrialPreferences.ToggleSprint,motion=InkMainMenu.ReducedMotion;float volume=AudioListener.volume;
            var pad=InputSystem.AddDevice<Gamepad>("Settings validation gamepad");
            TrialGame game=null;
            try
            {
                TrialBindings.ResetDefaults();Keys();yield return .2f;
                var menu=UnityEngine.Object.FindFirstObjectByType<InkMainMenu>();Check(menu,"Main menu initialized");
                menu.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.GetComponentInChildren<UnityEngine.UI.Text>().text=="设置").onClick.Invoke();yield return .2f;
                var panel=menu.GetComponentInChildren<TrialSettingsPanel>();Check(panel && panel.IsOpen,"Main menu opens shared settings");
                string layout=SettingsLayout(panel);Check(panel.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="VSync"),"Settings exposes the vertical sync control");
                bool hadFrame=PlayerPrefs.HasKey("Ink.MobileFrameRate");int frame=TrialPreferences.MobileFrameRate;
                try
                {
                    PlayerPrefs.SetInt("Ink.MobileFrameRate",60);
                    foreach(int expected in new[]{120,144,0,30,60})
                    {
                        SettingsButton(panel,"Frame rate limit").onClick.Invoke();
                        Check(TrialPreferences.MobileFrameRate==expected,"Mobile frame preference cycles and saves "+expected);
                    }
                }
                finally
                {
                    if(hadFrame)PlayerPrefs.SetInt("Ink.MobileFrameRate",frame);else PlayerPrefs.DeleteKey("Ink.MobileFrameRate");
                    PlayerPrefs.Save();TrialPreferences.ApplyMobileFrameRate();
                }
                CaptureSettings(panel,"Temp/settings-main.png");
                SettingsButton(panel,"Sprint mode").onClick.Invoke();Check(TrialPreferences.ToggleSprint!=sprint,"Shared sprint setting updates");SettingsButton(panel,"Sprint mode").onClick.Invoke();
                SettingsButton(panel,"Bind Attack 0").onClick.Invoke();yield return .3f;
                Check(panel.IsRebinding,"Rebind enters capture mode");Keys(Key.F);yield return .2f;Keys();yield return .12f;
                var attack=TrialBindings.Entries.First(e=>e.Id=="Attack");
                Check(!panel.IsRebinding && TrialBindings.Path(attack,0)=="<Keyboard>/f","Keyboard capture saves replacement key");
                Check(SettingsButton(panel,"Bind Attack 0").GetComponentInChildren<UnityEngine.UI.Text>().text==TrialBindings.Display("Attack"),"Rebind refreshes visible key label");
                SettingsButton(panel,"Bind Attack 0").onClick.Invoke();yield return .3f;Keys(Key.Q);yield return .2f;Keys();yield return .12f;
                Check(TrialBindings.Path(attack,0)=="<Keyboard>/f","Duplicate key is rejected without losing previous binding");
                Check(panel.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text.Contains("已用于")),"Conflicting action is explained in the UI");
                SettingsButton(panel,"Bind Attack 0").onClick.Invoke();yield return .3f;Keys(Key.Escape);yield return .2f;Keys();yield return .12f;
                Check(!panel.IsRebinding && panel.IsOpen && TrialBindings.Path(attack,0)=="<Keyboard>/f","Escape cancels capture and keeps settings open");
                SettingsButton(panel,"Bind Heavy 2").onClick.Invoke();yield return .3f;
                var padState=new GamepadState();padState.rightTrigger=1;InputSystem.QueueStateEvent(pad,padState);yield return .2f;InputSystem.QueueStateEvent(pad,new GamepadState());yield return .12f;
                Check(TrialBindings.Path(TrialBindings.Entries.First(e=>e.Id=="Heavy"),2)=="<Gamepad>/rightTrigger","Gamepad button capture saves replacement");
                SettingsButton(panel,"Restore bindings").onClick.Invoke();Check(TrialBindings.Path(attack,0)=="<Mouse>/leftButton","Restore defaults resets keyboard binding");
                Check(TrialBindings.Path(TrialBindings.Entries.First(e=>e.Id=="Heavy"),2)=="<Gamepad>/rightShoulder","Restore defaults resets gamepad binding");
                string error;Check(TrialBindings.TrySet(attack,0,"<Keyboard>/f",out error),"Replacement reapplies after reset");
                Check(TrialBindings.TrySet(attack,1,"<Keyboard>/h",out error),"Alternate keyboard binding is configurable");
                Check(TrialBindings.TrySet(TrialBindings.Entries[0],0,"<Keyboard>/v",out error),"Movement composite part is configurable");
                SettingsButton(panel,"Back").onClick.Invoke();yield return .12f;Check(!panel.IsOpen,"Back closes main settings");
                InkMainMenu.AutoStartTrial=true;UnityEngine.SceneManagement.SceneManager.LoadScene(TrialNetworkManager.GameScene);yield return .8f;
                game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();Check(game && game.Ready,"Gameplay loads after rebinding");game.Input.SetDeviceFilter(new InputDevice[]{keyboard,pad});
                if(game.Player.ChoosingBoon)game.Player.ChooseBoon(game.Player.OfferA);
                yield return .12f;
                Check(PlayerPrefs.GetString("Ink.Binding.Attack.0")=="<Keyboard>/f","Key preference survives scene transition");
                game.Player.Restore(game.Checkpoint);Keys(Key.F);yield return .06f;Keys();Check(game.Player.Action==FighterAction.Light,"Rebound primary key triggers gameplay attack");
                Check(game.Hud.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.name=="Key" && t.text=="F"),"HUD key hint follows the rebound action");yield return .5f;
                game.Player.Restore(game.Checkpoint);Keys(Key.J);yield return .06f;Keys();Check(game.Player.Action==FighterAction.Free,"Old alternate key no longer attacks");yield return .12f;
                Keys(Key.H);yield return .06f;Keys();Check(game.Player.Action==FighterAction.Light,"Rebound alternate key triggers gameplay attack");yield return .5f;
                game.Player.Restore(game.Checkpoint);Vector3 origin=game.Player.transform.position;Keys(Key.W);yield return .2f;Keys();Check(Vector3.Distance(origin,game.Player.transform.position)<.05f,"Old movement key no longer moves");
                Keys(Key.V);yield return .25f;Keys();Check(Vector3.Distance(origin,game.Player.transform.position)>.4f,"Rebound movement composite moves the player");
                game.Player.Restore(game.Checkpoint);yield return .12f;game.Pause();yield return .12f;game.Hud.OpenSettings();yield return .12f;
                panel=game.Hud.GetComponentInChildren<TrialSettingsPanel>();Check(panel && game.Hud.SettingsOpen,"Pause opens shared settings component");
                // Reset labels before comparing the identical layout in both scenes.
                SettingsButton(panel,"Restore bindings").onClick.Invoke();yield return .12f;
                Check(SettingsLayout(panel)==layout,"Main and pause settings use identical hierarchy and layout");
                Check(Time.timeScale==0 && game.Mode==TrialMode.Paused,"Settings preserves single player pause");CaptureSettings(panel,"Temp/settings-pause.png");
                var slider=panel.GetComponentInChildren<UnityEngine.UI.Slider>();slider.value=.32f;Check(Mathf.Abs(AudioListener.volume-.32f)<.01f,"Shared volume slider works while paused");
                SettingsButton(panel,"Motion").onClick.Invoke();Check(InkMainMenu.ReducedMotion!=motion,"Shared motion control works while paused");
                SettingsButton(panel,"Bind Guard 1").onClick.Invoke();yield return .3f;Keys(Key.G);yield return .2f;Keys();yield return .12f;
                Check(game.Hud.SettingsOpen && game.Mode==TrialMode.Paused,"Capturing a gameplay key keeps the game paused");
                Keys(Key.Escape);yield return .1f;Keys();yield return .15f;Check(!game.Hud.SettingsOpen && game.Mode==TrialMode.Paused,"Escape closes settings without resuming gameplay");
                Keys(Key.Escape);yield return .1f;Keys();yield return .15f;Check(game.Mode==TrialMode.Playing && Time.timeScale==1,"Next Escape resumes gameplay");
                Keys(Key.G);yield return .1f;Check(game.Player.Guarding,"Paused rebind becomes effective on resume");Keys();yield return .12f;
                game.Pause();yield return .12f;game.Hud.OpenSettings();yield return .12f;panel=game.Hud.GetComponentInChildren<TrialSettingsPanel>();
                SettingsButton(panel,"Bind Attack 0").onClick.Invoke();yield return .3f;game.Hud.CloseSettings();yield return .12f;
                Check(!panel.IsRebinding && !game.Hud.SettingsOpen,"Closing settings disposes a pending capture");
                game.Hud.OpenSettings();yield return .12f;Check(panel.IsOpen && !panel.IsRebinding,"Settings can reopen after cancelled capture");
                SettingsButton(panel,"Restore bindings").onClick.Invoke();game.Hud.CloseSettings();game.Resume();yield return .12f;
                game.Player.Restore(game.Checkpoint);Keys(Key.J);yield return .06f;Keys();Check(game.Player.Action==FighterAction.Light,"Restore defaults reinstates original gameplay binding");
            }
            finally
            {
                Keys();InputSystem.RemoveDevice(pad);TrialBindings.ResetDefaults();
                foreach(var pair in snapshot)if(pair.Value!=null)PlayerPrefs.SetString(pair.Key,pair.Value);
                TrialPreferences.ToggleSprint=sprint;InkMainMenu.SetReducedMotion(motion);AudioListener.volume=volume;PlayerPrefs.SetFloat("Ink.Volume",volume);PlayerPrefs.Save();
                if(game && game.Ready){game.Hud.CloseSettings();game.Resume();game.Input.SetDeviceFilter(null);}
            }
        }
        static void CaptureSettings(TrialSettingsPanel panel,string path)
        {
            var canvas=panel.GetComponentInParent<Canvas>();var previous=canvas.renderMode;var previousCamera=canvas.worldCamera;float distance=canvas.planeDistance;
            var obj=new GameObject("Settings screenshot camera");var camera=obj.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.1f,.12f,.12f);camera.cullingMask=1<<5;
            var layers=canvas.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.gameObject.layer);foreach(var item in layers)item.Key.gameObject.layer=5;
            var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);var rt=new RenderTexture(1600,900,24);var previousActive=RenderTexture.active;
            try{canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally{foreach(var item in layers)item.Key.gameObject.layer=item.Value;canvas.renderMode=previous;canvas.worldCamera=previousCamera;canvas.planeDistance=distance;RenderTexture.active=previousActive;UnityEngine.Object.DestroyImmediate(obj);UnityEngine.Object.DestroyImmediate(texture);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
    }
}
