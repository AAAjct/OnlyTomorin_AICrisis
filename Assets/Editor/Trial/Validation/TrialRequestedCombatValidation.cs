using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TomorinTrial.Editor
{
    public static class TrialRequestedCombatValidation
    {
        public static string Status {get;private set;}="Not run";
        static IEnumerator routine;
        static double resumeAt;
        static readonly List<string> checks=new List<string>();
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static int errors;
        [MenuItem("Tools/Tomorin/Verify requested combat polish")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying || routine!=null)throw new InvalidOperationException("Enter Play Mode first.");
            checks.Clear();errors=0;Status="Running";Application.logMessageReceived+=OnLog;
            routine=Checks();resumeAt=0;EditorApplication.update+=Step;
        }
        static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception)errors++;}
        static void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
        static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Private).GetValue(owner);
        static void Call(object owner,string method)=>owner.GetType().GetMethod(method,Private).Invoke(owner,null);
        static void Step()
        {
            if(EditorApplication.timeSinceStartup<resumeAt)return;
            try{if(!routine.MoveNext()){Finish(null);return;}resumeAt=EditorApplication.timeSinceStartup+(routine.Current is float seconds?seconds:0);}
            catch(Exception e){Finish(e);}
        }
        static void Finish(Exception e)
        {
            (routine as IDisposable)?.Dispose();routine=null;EditorApplication.update-=Step;Application.logMessageReceived-=OnLog;
            Status=e==null?"PASS "+checks.Count:"FAIL "+e;
            Directory.CreateDirectory("Logs/Trial");File.WriteAllText("Logs/Trial/RequestedCombatValidation.txt",Status+"\n"+string.Join("\n",checks));
            if(e==null)Debug.Log(Status);else Debug.LogException(e);
        }
        static Rect Bounds(RectTransform t)
        {var p=new Vector3[4];t.GetWorldCorners(p);return new Rect(p[0],p[2]-p[0]);}
        static IEnumerator Checks()
        {
            TrialGame game=null;GameObject wall=null;
            try
            {
                SceneManager.LoadScene("InkStart");yield return .65f;
                var menu=UnityEngine.Object.FindFirstObjectByType<InkMainMenu>();Field<Button>(menu,"multiplayer").onClick.Invoke();Canvas.ForceUpdateCanvases();
                var labels=menu.GetComponentsInChildren<Text>(true);
                var name=labels.First(t=>t.text=="你的名字");var hint=labels.First(t=>t.text.Contains("A / Enter 编辑"));
                Check(!Bounds(name.rectTransform).Overlaps(Bounds(hint.rectTransform)),"LAN name label and navigation hint have disjoint layout bounds");
                Check(TrialPowerBudget.Budget(120,false,true,false)==30 && TrialPowerBudget.Budget(120,true,false,false)==15 && TrialPowerBudget.Budget(60,true,true,false)==60,"Idle and background rendering budgets preserve active combat FPS");
                Check(TrialPowerBudget.Budget(30,false,false,true)==30,"LAN honors the selected cap without additional background throttling");
                foreach(ChampionKind kind in Enum.GetValues(typeof(ChampionKind)))for(int i=0;i<TrialChampionCatalog.SkillCount;i++)
                {var m=TrialChampionCatalog.Move(kind,i);Check(m.shape!=ChampionShape.Leap && m.shape!=ChampionShape.Dive && m.shape!=ChampionShape.TwinPincer,kind+" slot "+i+" has no aerial slam");}
                SceneManager.LoadScene("CrimsonSwordTerrace");yield return .8f;
                double readyDeadline=EditorApplication.timeSinceStartup+25;
                do{game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();if(game && game.Ready)break;yield return .1f;}while(EditorApplication.timeSinceStartup<readyDeadline);
                Check(game && game.Ready,"Anon arena initializes");
                game.StartTrial();game.Player.ResetRunBoon();game.enabled=false;
                int frame=Time.frameCount;yield return .12f;Check(Time.frameCount>frame,"Play Mode player loop advances");
                var p=game.Player;var boss=game.Enemies[0];var input=game.gameObject.AddComponent<TrialInput>();
                Action<TrialControls,float> tick=(c,dt)=>{input.ApplyNetwork(c);p.Tick(dt,input);};
                Action reset=()=>{p.ResetRunBoon();p.Restore(new Vector3(0,0,20));boss.ResetFighter();typeof(TrialEnemy).GetProperty("Awakened").SetValue(boss,true);boss.Teleport(new Vector3(9,0,30));Time.timeScale=1;};
                reset();tick(new TrialControls{pressed=1},.01f);
                for(int i=0;i<4;i++)
                {
                    Check(p.Action==FighterAction.Light && p.Combo==i,"Light sequence reaches stage "+i);
                    if(i<3)tick(new TrialControls{pressed=1},p.ActionDuration*.57f);
                }
                for(int stage=0;stage<3;stage++)
                {
                    reset();tick(new TrialControls{pressed=1},.01f);
                    for(int i=0;i<stage;i++)tick(new TrialControls{pressed=1},p.ActionDuration*.57f);
                    tick(new TrialControls{pressed=2},p.ActionDuration*.57f);
                    Check(p.Action==FighterAction.Heavy && p.Combo==4+stage,"Light stage "+stage+" branches to distinct heavy "+p.Combo);
                }
                // Feed actual buffered controls through the action machine, rather than invoking branch helpers.
                Action<int> lights=n=>{tick(new TrialControls{pressed=1},.01f);for(int j=1;j<n;j++)tick(new TrialControls{pressed=1},p.ActionDuration*.57f);};
                reset();lights(4);tick(new TrialControls{pressed=2},p.ActionDuration*.57f);Check(p.Combo==20 && p.IsFinishingCut,"Four lights branch into the cleave finisher");
                reset();lights(1);tick(new TrialControls{pressed=2},p.ActionDuration*.57f);tick(new TrialControls{pressed=2},p.ActionDuration*.74f);Check(p.Combo==21,"Rising heavy branches into a reverse sweep");
                reset();lights(2);tick(new TrialControls{pressed=2},p.ActionDuration*.57f);tick(new TrialControls{pressed=1},p.ActionDuration*.74f);Check(p.Combo==22 && p.Action==FighterAction.Light,"Thrust heavy retracts into a close draw cut");
                reset();lights(3);tick(new TrialControls{pressed=2},p.ActionDuration*.57f);tick(new TrialControls{pressed=1},p.ActionDuration*.74f);Check(p.Combo==TrialPlayer.MoonChase,"Three-light heavy branches into moon pursuit thrust");
                float retreatStart=p.transform.position.z;tick(default,p.ActionDuration*.48f);Check(p.transform.position.z>retreatStart+4,"Moon pursuit closes the spacing created by the ranged retreat");
                reset();lights(2);tick(new TrialControls{pressed=4,move=Vector2.right},p.ActionDuration*.43f);tick(new TrialControls{pressed=2},p.ActionDuration*.70f);Check(p.Combo==TrialPlayer.RollFlash && p.Action==FighterAction.Heavy,"Dodge derivative cancels into the new flashing cut");
                reset();tick(new TrialControls{pressed=4},.01f);tick(new TrialControls{pressed=1},p.ActionDuration*.70f);tick(new TrialControls{pressed=2},p.ActionDuration*.57f);Check(p.Combo==25,"Rolling pursuit light branches into driving thrust");
                reset();tick(new TrialControls{pressed=2},.01f);tick(new TrialControls{pressed=1},p.ActionDuration*.74f);tick(new TrialControls{pressed=2},p.ActionDuration*.57f);Check(p.Combo==15,"Heavy-light-heavy activates cut-through branch");
                reset();lights(1);tick(default,.12f);Check(!p.CombatFrozen,"Whiffed attacks never trigger pose hit-stop");
                reset();boss.Teleport(new Vector3(0,0,22));boss.Champion.BeginMove(1);lights(1);tick(default,.12f);
                float firstContactHealth=boss.Health;Check(p.CombatFrozen,"Actual light contact triggers pose hit-stop");
                tick(new TrialControls{pressed=2},0);
                yield return .09f;tick(default,.03f);Check(Mathf.Abs(boss.Health-firstContactHealth)<.01f,"A single sword swing damages each target only once across contact frames");
                tick(default,.12f);Check(p.Action==FighterAction.Heavy && p.Combo==4,"Input tapped during hit-stop survives into the next combo branch ("+p.Action+" / "+p.Combo+" / "+p.Progress+" / input age "+(Time.time-Field<float>(p,"bufferedHeavy"))+")");
                reset();boss.Teleport(new Vector3(0,0,25));tick(new TrialControls{pressed=1},.01f);tick(default,.09f);
                Check(p.transform.position.z>20.3f,"Basic attack pursues a target five metres ahead");
                reset();boss.Teleport(new Vector3(0,0,25));wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,1,22);wall.transform.localScale=new Vector3(4,3,.3f);Physics.SyncTransforms();
                tick(new TrialControls{pressed=1},.01f);Check(Field<TrialFighter>(p,"chaseTarget")==null,"Attack chase rejects an occluded target");
                UnityEngine.Object.DestroyImmediate(wall);wall=null;
                reset();boss.Teleport(new Vector3(0,0,25));tick(new TrialControls{pressed=1},.01f);tick(new TrialControls{move=Vector2.down,yaw=0},.09f);
                Check(Mathf.Abs(p.transform.position.z-20)<.05f,"Backward input cancels pursuit");
                reset();tick(new TrialControls{pressed=4,move=Vector2.right},.01f);tick(default,.08f);
                Check(!game.Effects.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.transform.parent.name.StartsWith("Blink afterimage")),"Ordinary dodge never bakes blue silhouettes");
                Check(!game.Effects.GetComponentsInChildren<LineRenderer>(true).Any(r=>r.name.StartsWith("Impact slash") && r.enabled && r.startColor.b>r.startColor.r+.15f),"Ordinary dodge emits no blue ring");
                p.ReceiveHit(boss,30,20);Check(p.SuccessfulDodges>0,"Timed incoming contact is evaded");
                Check(game.Effects.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.transform.parent.name.StartsWith("Blink afterimage") && r.gameObject.activeInHierarchy),"Perfect dodge emits a blue silhouette immediately");
                reset();p.ReceiveHit(null,60,0);yield return .12f;p.Restore(new Vector3(0,0,20));Call(game.Hud,"UpdateDamageFeedback");
                Check(Field<Image>(game.Hud,"healingVeil").color.a==0,"Restore to full health has no green fade");
                game.Hud.HealFlash();Call(game.Hud,"UpdateDamageFeedback");Check(Field<Image>(game.Hud,"healingVeil").color.a>0,"Explicit healing event still displays healing feedback");
                p.Restore(new Vector3(0,0,20));
                var rig=boss.Champion.Rig;var skirt=rig.GetComponent<TrialSkirtRig>();Check(skirt,"Anon runtime prefab includes skirt correction");
                var panels=Field<Transform[]>(skirt,"panels");Check(panels.Length==12,"Skirt and outline share twelve deformation panels");
                rig.Pose(-1,0,1,false,0);var baseline=panels.Select(t=>t.rotation).ToArray();
                typeof(TrialChampionRig).GetField("gait",Private).SetValue(rig,Mathf.PI*.5f);
                rig.Pose(-1,0,1,false,.11f);Check(panels.Where((t,i)=>Quaternion.Angle(t.rotation,baseline[i])>1).Any(),"Skirt follows the actual animated thigh excursion");
                for(int i=0;i<12;i++)
                {boss.Champion.BeginMove(i);for(int j=0;j<12;j++)boss.Champion.Tick(.08f);Check(Mathf.Abs(boss.transform.position.y)<.5f,"Anon move "+i+" remains grounded");boss.Champion.CancelAttack(true);}
                reset();boss.Teleport(new Vector3(0,0,22));boss.Champion.BeginMove(1);tick(new TrialControls{pressed=1},.01f);tick(default,.12f);
                Check(p.CombatFrozen && boss.CombatFrozen && boss.Recoil.sqrMagnitude>.1f,"Blade contact holds both poses and produces boss body recoil");
                yield return .2f;reset();boss.ReceiveHit(p,100000,0,true);yield return .6f;Call(game.Hud,"UpdateInkResult");
                Check(Field<Text>(game.Hud,"resultCharacter").text=="斩" && Field<CanvasGroup>(game.Hud,"resultInk").gameObject.activeSelf,"Boss victory displays the ink slash character during slow motion");
                game.Respawn();game.Player.ResetRunBoon();game.enabled=false;p.ReceiveHit(null,10000,0,true);yield return .6f;Call(game.Hud,"UpdateInkResult");
                Check(Field<Text>(game.Hud,"resultCharacter").text=="菜","Player death displays the ink defeat character");
                Check(!Field<CanvasGroup>(game.Hud,"resultInk").blocksRaycasts,"Ink verdict never blocks retry or exit controls");
                Check(errors==0,"No runtime exceptions during the regression suite");
            }
            finally
            {if(wall)UnityEngine.Object.DestroyImmediate(wall);if(game){game.enabled=true;game.ReturnToTitle();}Time.timeScale=1;}
        }
    }
}
