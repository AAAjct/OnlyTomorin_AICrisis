using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TomorinTrial.Editor
{
    public static class TrialCombatUpgradeValidation
    {
        public static string Status {get;private set;}="Not run";
        static IEnumerator routine;
        static double resumeAt;
        static readonly List<string> results=new List<string>();
        static int errors;
        [MenuItem("Tools/Tomorin/Verify combat upgrade")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying || routine!=null)throw new InvalidOperationException("Enter Play Mode first.");
            results.Clear();errors=0;Status="Running";Application.logMessageReceived+=OnLog;
            routine=Checks();resumeAt=0;EditorApplication.update+=Step;
        }
        static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception)errors++;}
        static void Check(bool condition,string text){if(!condition)throw new Exception(text);results.Add(text);}
        static void Step()
        {
            if(EditorApplication.timeSinceStartup<resumeAt)return;
            try {if(!routine.MoveNext()){Finish(null);return;}resumeAt=EditorApplication.timeSinceStartup+(routine.Current is float seconds?seconds:0);}
            catch(Exception error){Finish(error);}
        }
        static void Finish(Exception error)
        {
            (routine as IDisposable)?.Dispose();routine=null;EditorApplication.update-=Step;Application.logMessageReceived-=OnLog;
            Status=error==null?"PASS "+results.Count:"FAIL "+error;
            Directory.CreateDirectory("Logs/Trial");File.WriteAllText("Logs/Trial/CombatUpgradeValidation.txt",Status+"\n"+string.Join("\n",results));
            if(error==null)Debug.Log(Status);else Debug.LogException(error);
        }
        static IEnumerator Checks()
        {
            SceneManager.LoadScene("AshenCourt");yield return .8f;
            var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();Check(game && game.Ready,"Courtyard initializes");
            game.StartTrial();game.Player.ChooseBoon(game.Player.OfferA);game.enabled=false;
            var input=game.gameObject.AddComponent<TrialInput>();var p=game.Player;var foe=game.Enemies[0];
            foreach(var enemy in game.Enemies)enemy.Teleport(new Vector3(12,0,36));
            Action reset=()=>{p.ResetRunBoon();p.Restore(new Vector3(0,0,-10));};
            Action<TrialControls,float> tick=(c,dt)=>{input.ApplyNetwork(c);p.Tick(dt,input);};
            try
            {
                reset();p.ReceiveHit(null,60,0);yield return .1f;tick(default,.4f);
                tick(new TrialControls{move=Vector2.up,held=2,pressed=8},.1f);
                tick(new TrialControls{move=Vector2.up,held=2},.1f);
                Check(p.Action==FighterAction.Heal && p.Sprinting,"Sprint can enter a drinking action");
                tick(new TrialControls{move=Vector2.up},.12f);tick(new TrialControls{move=Vector2.up,held=2},.12f);
                Check(p.Sprinting && p.Action==FighterAction.Heal,"Drinking can transition into sprinting");
                tick(new TrialControls{move=Vector2.up,held=2},.6f);yield return .05f;
                Check(p.Flasks==2 && p.Health>180,"Running heal consumes exactly one flask and restores health");
                Check(game.Hud.GetComponentsInChildren<Image>(true).Any(x=>x.name=="Spirit dew · green fade" && !x.raycastTarget),"Green healing overlay does not capture input");
                reset();tick(new TrialControls{pressed=2,held=4},.01f);
                Check(p.Action==FighterAction.Charge,"Holding heavy enters charge");
                tick(new TrialControls{held=4},.7f);tick(default,.01f);
                Check(p.Action==FighterAction.Heavy && p.Combo==17,"Releasing a full charge produces jumping cleave");
                reset();tick(new TrialControls{pressed=2,held=4},.01f);tick(default,.08f);
                Check(p.Action==FighterAction.Heavy && p.Combo!=17,"Short heavy tap remains an ordinary heavy attack");
                reset();tick(new TrialControls{pressed=2},.01f);tick(new TrialControls{pressed=2},.63f);
                Check(p.Action==FighterAction.Heavy && p.Combo==18,"Second heavy follows the first with a reverse rise");
                tick(new TrialControls{pressed=2},.58f);
                Check(p.Action==FighterAction.Heavy && p.Combo==19,"Third heavy resolves into the finishing cleave");
                reset();tick(new TrialControls{pressed=1},.01f);tick(new TrialControls{pressed=2},.23f);
                Check(p.Action==FighterAction.Heavy && p.Combo==15,"Light-to-heavy selects interception cut");
                foe.Teleport(p.transform.position+Vector3.forward*2);tick(default,.1f);float hp=p.Health;
                int parries=p.SuccessfulParries;p.ReceiveHit(foe,30,20);
                Check(p.Health==hp && p.SuccessfulParries==parries+1,"Interception window counters a frontal readable attack");
                yield return .16f;foe.Teleport(new Vector3(12,0,36));
                reset();tick(new TrialControls{pressed=2},.01f);tick(new TrialControls{pressed=1},.64f);
                Check(p.Action==FighterAction.Light && p.Combo==16,"Heavy-to-light selects reverse draw");
                reset();p.ReceiveHit(null,160,0);yield return .12f;float before=p.Health;p.ReceiveHit(foe,20,0);
                // hurt grace must elapse in game time, independently of the hit freeze.
                yield return .55f;before=p.Health;p.ReceiveHit(foe,20,0);
                Check(before-p.Health<20 && before-p.Health>0,"Low-health PvE hit receives bounded damage relief");
                reset();yield return .12f;p.ReceiveHit(foe,500,0);
                Check(p.Health==1,"A fresh PvE life can survive one lethal enemy blow");
                float graceEnds=Time.time+.8f;while(Time.time<graceEnds)yield return .02f;p.ReceiveHit(foe,500,0);Check(!p.Alive,"Lethal protection cannot repeat indefinitely");
                reset();tick(new TrialControls{pressed=4,move=Vector2.right},.01f);tick(default,.12f);
                Check(game.Effects.GetComponentsInChildren<MeshFilter>().Any(x=>x.sharedMesh && x.sharedMesh.name=="Baked blink silhouette"),"Ordinary rolling generates pooled silhouette geometry");
                Check(Application.targetFrameRate>0 && QualitySettings.vSyncCount==0,"Cross-platform frame budget is applied");
                game.Pause();game.Hud.OpenSettings();yield return .1f;
                Check(game.Hud.GetComponentsInChildren<Text>(true).Any(x=>x.text.StartsWith("帧率限制：")),"Settings use a platform-neutral frame label");
                Check(game.Hud.GetComponentsInChildren<Slider>(true).Count()==5,"Settings expose master plus four sound-effect category sliders");
                game.Hud.CloseSettings();game.Resume();
                Check(Resources.Load<AudioClip>("Trial/Audio/charge_0") && Resources.Load<AudioClip>("Trial/Audio/counter_0") && Resources.Load<AudioClip>("Trial/Audio/landing_0"),"New original charge, counter and landing sounds import successfully");
                Check(UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length<700,"Static batching releases redundant render components");
                Check(errors==0,"Gameplay and settings checks produce no runtime exceptions");
            }
            finally {if(game){game.enabled=true;game.RestartTrial();}if(input)UnityEngine.Object.Destroy(input);}
        }
    }
}
