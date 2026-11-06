using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TomorinTrial.Editor
{
    public static class TrialResponsivenessValidation
    {
        public static string Status {get;private set;}="Not run";
        static IEnumerator routine;
        static double resumeAt;
        static readonly List<string> checks=new List<string>();
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static int errors;
        [MenuItem("Tools/Tomorin/Verify responsive combat and LAN controller navigation")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying || routine!=null || !UnityEngine.Object.FindFirstObjectByType<InkMainMenu>())throw new InvalidOperationException("Enter InkStart Play Mode first.");
            checks.Clear();errors=0;Status="Running";Application.logMessageReceived+=OnLog;
            routine=Checks();resumeAt=0;EditorApplication.update+=Step;
        }
        static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors++;}
        static void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
        static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Private).GetValue(owner);
        static void Step()
        {
            if(EditorApplication.timeSinceStartup<resumeAt)return;
            try
            {
                if(!routine.MoveNext()){Finish(null);return;}
                resumeAt=EditorApplication.timeSinceStartup+(routine.Current is float seconds?seconds:0);
            }
            catch(Exception error){Finish(error);}
        }
        static void Finish(Exception error)
        {
            (routine as IDisposable)?.Dispose();routine=null;EditorApplication.update-=Step;Application.logMessageReceived-=OnLog;
            Status=error==null?"PASS "+checks.Count:"FAIL "+error;
            Directory.CreateDirectory("Logs/Trial");File.WriteAllText("Logs/Trial/ResponsivenessValidation.txt",Status+"\n"+string.Join("\n",checks));
            if(error==null)Debug.Log(Status);else Debug.LogException(error);
        }
        static IEnumerator Checks()
        {
            Gamepad pad=null;TrialInput input=null;TrialGame game=null;
            var background=InputSystem.settings.backgroundBehavior;var editor=InputSystem.settings.editorInputBehaviorInPlayMode;
            try
            {
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                pad=InputSystem.AddDevice<Gamepad>("LAN regression controller");
                var menu=UnityEngine.Object.FindFirstObjectByType<InkMainMenu>();
                Field<Button>(menu,"multiplayer").onClick.Invoke();yield return .15f;
                Check(EventSystem.current.currentSelectedGameObject==Field<Button>(menu,"host").gameObject,"LAN entry selects Create Room instead of starting text editing");
                var expected=new Selectable[]{Field<InputField>(menu,"port"),Field<InputField>(menu,"address"),Field<InputField>(menu,"nickname"),Field<Button>(menu,"back")};
                foreach(var target in expected)
                {
                    InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadUp));yield return .18f;
                    InputSystem.QueueStateEvent(pad,new GamepadState());yield return .09f;
                    Check(EventSystem.current.currentSelectedGameObject==target.gameObject,"Gamepad exits text editing and reaches "+target.name);
                }
                var nickname=Field<InputField>(menu,"nickname");nickname.Select();
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return .08f;
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return .06f;
                Check(nickname.isFocused,"A deliberately enters nickname editing");
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadDown));yield return .12f;
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return .06f;
                Check(!nickname.isFocused && EventSystem.current.currentSelectedGameObject==Field<InputField>(menu,"address").gameObject,"Controller navigation also escapes deliberate text editing without skipping fields");
                Field<Button>(menu,"back").onClick.Invoke();
                SceneManager.LoadScene("AshenCourt");yield return .65f;
                game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();Check(game && game.Ready,"Courtyard and authored VFX resources initialize successfully");
                game.StartTrial();game.enabled=false;input=game.gameObject.AddComponent<TrialInput>();
                var player=game.Player;var foe=game.Enemies[0];
                foreach(var enemy in game.Enemies)enemy.Teleport(new Vector3(10,0,32));
                Action<TrialControls,float> tick=(controls,dt)=>{input.ApplyNetwork(controls);player.Tick(dt,input);};
                Action reset=()=>{player.ResetRunBoon();player.Restore(new Vector3(0,0,-10));};
                reset();tick(new TrialControls{pressed=1},.02f);
                Check(player.Action==FighterAction.Light && Mathf.Abs(player.ActionDuration-.38f)<.001f,"Basic light attack completes in 380 ms");
                tick(default,.10f);Check(player.AttackActive,"Light contact opens by 100 ms");
                tick(new TrialControls{held=1},.08f);Check(player.Guarding && player.Action==FighterAction.Free,"Light attack links directly into guard after 180 ms");
                reset();tick(new TrialControls{pressed=1},.02f);tick(new TrialControls{pressed=4,move=Vector2.right},.17f);
                Check(player.Action==FighterAction.Dodge,"Buffered dodge cancels light recovery");
                reset();tick(new TrialControls{pressed=1},.02f);tick(new TrialControls{pressed=2},.22f);
                Check(player.Action==FighterAction.Heavy,"Light attack chains into a buffered heavy attack");
                reset();tick(new TrialControls{pressed=1},.02f);tick(new TrialControls{pressed=1},.22f);
                Check(player.Action==FighterAction.Light && player.Combo==1 && player.ActionTime==0,"Light combo consumes the buffered input once at the chain window");
                reset();tick(new TrialControls{pressed=1},.02f);tick(new TrialControls{pressed=8},.25f);
                Check(player.Action==FighterAction.Heal && player.Flasks==3,"Buffered healing starts from light recovery without consuming a flask early");
                reset();tick(new TrialControls{pressed=1},.02f);tick(new TrialControls{move=Vector2.right},.29f);
                Check(player.Action==FighterAction.Free && player.transform.position.x>0,"Movement cancels light recovery and resumes steering");
                reset();tick(new TrialControls{held=1,move=Vector2.right},.05f);
                Check(player.Guarding && Vector3.Angle(player.transform.forward,Vector3.right)<1,"Guard can turn 90 degrees within 50 ms");
                for(int i=0;i<15;i++)tick(new TrialControls{held=1},.02f);
                foe.Teleport(player.transform.position+player.transform.forward*2);
                float hp=player.Health,stamina=player.Stamina;
                player.ReceiveHit(foe,30,20);
                Check(player.Health>=hp-1.21f && player.Stamina>=stamina-13,"Held guard reduces chip damage and stamina cost without refreshing parry age");
                yield return .1f;
                reset();foe.ResetFighter();foe.Teleport(player.transform.position+Vector3.forward*2);
                float foeHp=foe.Health;tick(new TrialControls{pressed=1},.02f);tick(default,.09f);
                Check(foe.Health<foeHp,"Shortened contact window still connects with a real opponent");
                float after=foe.Health;yield return .06f;for(int i=0;i<5;i++)tick(default,.02f);
                Check(foe.Health==after,"One light swing cannot repeatedly damage the same opponent");
                reset();player.ReceiveHit(foe,50,10,true);yield return .08f;
                var veil=Field<Image>(game.Hud,"damageVeil");var caption=Field<Text>(game.Hud,"damageCaption");
                Check(veil.color.a>.1f && caption.text.Contains("50"),"Actual health loss displays a red screen-edge cue and damage amount");
                Check(!veil.raycastTarget && !caption.raycastTarget,"Damage feedback does not intercept gameplay or touch UI");
                var shell=Resources.Load<Material>("Trial/VFX/TideShell");var sigil=Resources.Load<Material>("Trial/VFX/TideSigil");
                Check(shell && sigil && !UnityEditor.ShaderUtil.ShaderHasError(shell.shader) && !UnityEditor.ShaderUtil.ShaderHasError(sigil.shader),"Both authored VFX shaders compile without errors");
                Check(Resources.Load<Mesh>("Trial/VFX/TideCrown").vertexCount>200 && Resources.Load<Mesh>("Trial/VFX/TidePearl").vertexCount>900,"Authored tidal crown and pearl meshes are available in Resources");
                Check(Directory.GetFiles("Assets/Audio/Resources/Trial/Audio","*.wav").Length>=54,"Original sound library remains available alongside new combo variations");
                game.StartBossChallenge();player.ResetRunBoon();player.Restore(new Vector3(7,0,19));var boss=game.Enemies.Last();game.SetCombatTarget(player);boss.ReceiveHit(player,1,0);boss.Boss.StartSkill(DeepFishSkill.Maelstrom);
                boss.Boss.Tick(.2f);yield return .08f;
                Check(boss.Boss.GetComponentsInChildren<TrialSkillSurface>().Any(s=>s.GetComponentInChildren<MeshRenderer>().enabled),"Boss windup renders layered warning geometry");
                var hazardState=new TrialBossState{hazards=new[]{new TrialHazardState{active=true,position=new Vector3(0,1,24),radius=.27f}}};
                boss.Boss.ApplyNetwork(hazardState);yield return .05f;
                Check(boss.Boss.GetComponentsInChildren<TrailRenderer>().Any(t=>t.gameObject.activeInHierarchy),"Replicated boss projectiles use the authored shell and trail");
                game.Effects.SkillImpact(new Vector3(0,0,24),Color.cyan);yield return .06f;
                Check(game.Effects.GetComponentsInChildren<MeshRenderer>().Any(r=>r.name=="Breaking water crown" && r.gameObject.activeInHierarchy),"Skill impact activates the pooled water crown");
                Check(errors==0,"Navigation, combat, damage UI, audio and VFX produce no runtime errors");
            }
            finally
            {
                if(game){game.enabled=true;game.RestartTrial();}if(input)UnityEngine.Object.Destroy(input);
                if(pad!=null)InputSystem.RemoveDevice(pad);
                InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editor;
                Time.timeScale=1;
            }
        }
    }
}
