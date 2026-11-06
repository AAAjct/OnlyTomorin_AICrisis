using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TomorinTrial.Editor
{
    public static class TrialAiPressureValidation
    {
        public static string Status {get;private set;}="Idle";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static IEnumerator routine;
        static readonly List<string> checks=new List<string>();
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private).Invoke(o,args);
        static void Field(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
        static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add("PASS "+text);}

        [MenuItem("Tools/Tomorin/Validate adaptive boss pressure (Play Mode)")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying || routine!=null)throw new InvalidOperationException("Requires Play Mode");
            checks.Clear();Status="Running";routine=Validate();EditorApplication.update+=Step;
        }
        static void Step()
        {
            try
            {
                if(!EditorApplication.isPlaying)throw new Exception("Play Mode ended");
                if(routine.MoveNext())return;
                Finish(null);
            }
            catch(Exception ex){Finish(ex);}
        }
        static void Finish(Exception ex)
        {
            (routine as IDisposable)?.Dispose();routine=null;EditorApplication.update-=Step;
            Status=ex==null?"PASS "+checks.Count+" checks":"FAIL "+ex;
            Directory.CreateDirectory("Logs/Trial");File.WriteAllText("Logs/Trial/AiPressureValidation.txt",Status+"\n"+string.Join("\n",checks));
            if(ex!=null)Debug.LogException(ex);
        }
        static void Prepare(TrialGame game,TrialEnemy enemy)
        {
            game.Player.Restore(new Vector3(0,0,28));enemy.ResetFighter();enemy.Teleport(new Vector3(0,0,24));
            if(enemy.Champion)Field(enemy.Champion,"introPlayed",true);
            Physics.SyncTransforms();enemy.Tick(.001f);
        }
        static IEnumerator Validate()
        {
            var randomState=UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(107);
                ValidateMeshes();
                foreach(var level in new[]{TrialLevel.Twins,TrialLevel.Anon,TrialLevel.Taffy,TrialLevel.Penguin,TrialLevel.YaYa,TrialLevel.Remnant,TrialLevel.BigFish})
                {
                    Status="Testing "+level;
                    var load=SceneManager.LoadSceneAsync(TrialChampionCatalog.Scene(level));while(!load.isDone)yield return null;
                    var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
                    while(!game || !game.Ready){yield return null;game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();}
                    if(level==TrialLevel.Twins)ValidateDisplaySettings(game.GetComponentsInChildren<TrialSettingsPanel>(true)[0]);
                    game.StartTrial();if(level==TrialLevel.BigFish)game.StartBossChallenge();
                    if(game.Player.ChoosingBoon)game.Player.ChooseBoon(game.Player.OfferA);
                    game.enabled=false;game.Input.enabled=false;
                    try
                    {
                        foreach(var enemy in game.Enemies)
                        {
                            if(!enemy.IsBoss)continue;Prepare(game,enemy);var name=enemy.DisplayName;
                            if(enemy.Champion)
                            {
                                var brain=enemy.Champion;var selected=new HashSet<int>();bool reachable=true;
                                for(int i=0;i<100;i++)
                                {
                                    int index=(int)Call(brain,"ChooseMove",9f);selected.Add(index);
                                    if(index<0)continue;
                                    var move=TrialChampionCatalog.Move(brain.Kind,index);
                                    reachable&=!((move.shape==ChampionShape.Cut || move.shape==ChampionShape.Cross || move.shape==ChampionShape.Counter || move.shape==ChampionShape.Grab) && move.range+.6f<9);
                                    Field(brain,"usedMoves",(uint)(1u<<index));
                                }
                                Check(reachable,name+" 100 ranged decisions avoid unreachable melee");
                                selected.Clear();Field(brain,"usedMoves",0u);
                                for(int i=0;i<100;i++){int index=(int)Call(brain,"ChooseMove",3.8f);selected.Add(index);if(index>=0)Field(brain,"usedMoves",1u<<index);}
                                Check(selected.Count>1,name+" varies reachable responses");
                                brain.BeginMove(0);
                                Call(brain,"SpawnHazard",new Vector3(0,.08f,24),Vector3.zero,true);
                                brain.CancelAttack();Call(brain,"BeginRespite");
                                float rest=brain.RespiteRemaining;int attacks=brain.AttacksStarted;
                                brain.Tick(.2f);
                                Check(brain.ActiveHazards>0 && Mathf.Abs(brain.RespiteRemaining-(rest-.2f))<.01f,name+" live hazards do not freeze respite");
                                Check(brain.AttacksStarted==attacks && brain.RespiteRemaining>0,name+" keeps a real punish window");
                                if(level==TrialLevel.Twins)
                                {
                                    var partner=game.Enemies[enemy==game.Enemies[0]?1:0];Prepare(game,partner);partner.Champion.BeginMove(0);
                                    rest=brain.RespiteRemaining;brain.Tick(.2f);
                                    Check(Mathf.Abs(brain.RespiteRemaining-(rest-.2f))<.01f,name+" partner cannot freeze recovery clock");
                                    for(int i=0;i<30;i++)brain.Tick(.05f);
                                    Check(!brain.Regrouping && brain.AttacksStarted==attacks,name+" waits for partner attack without restarting respite");
                                    partner.Champion.CancelAttack(true);
                                }
                                Prepare(game,enemy);game.Player.Restore(new Vector3(0,0,8));
                                Field(brain,"pursuitAge",3f);Field(brain,"mobilityCooldown",100f);Field(brain,"cooldown",1f);
                                brain.Tick(.02f);
                                Check(!brain.Regrouping,name+" fleeing player does not trigger free respite");
                                Check(brain.Mobility!=ChampionMobility.Grounded || brain.Attacking || enemy.MoveBlend>0,name+" pursuit chooses movement or attack");
                                Prepare(game,enemy);brain.BeginMove(0);var moveBefore=brain.Move;
                                enemy.Parried();Check(!enemy.ResolveActive,name+" parry still removes pressure armor");
                                Check(moveBefore.windup==TrialChampionCatalog.Move(brain.Kind,0).windup,name+" authored windup remains unchanged");
                            }
                            else
                            {
                                var brain=enemy.Boss;var selected=new HashSet<DeepFishSkill>();bool reachable=true;
                                for(int i=0;i<100;i++)
                                {
                                    var skill=(DeepFishSkill)Call(brain,"ChooseAttack",12f);selected.Add(skill);
                                    reachable&=skill!=DeepFishSkill.TailSweep && skill!=DeepFishSkill.ReverseTail && skill!=DeepFishSkill.Maelstrom;
                                }
                                Check(reachable,name+" 100 ranged decisions avoid unreachable sweeps");
                                Check(selected.Count>=3,name+" varies ranged responses");
                                brain.StartSkill(DeepFishSkill.TailSweep);brain.Tick(2f);
                                Call(brain,"SpawnWave",enemy.transform.position,4f);Field(brain,"regroupTime",.8f);
                                brain.Tick(.2f);var remaining=(float)brain.GetType().GetField("regroupTime",Private).GetValue(brain);
                                Check(brain.ActiveHazards>0 && Mathf.Abs(remaining-.6f)<.01f,name+" wave cannot freeze recovery clock");
                                Prepare(game,enemy);game.Player.Restore(new Vector3(0,0,33));Field(brain,"pursuitTime",3.1f);Field(brain,"cooldown",1f);
                                brain.Tick(.02f);Check(!brain.Regrouping,name+" distant player does not trigger free respite");
                                brain.StartSkill(DeepFishSkill.ReverseTail);enemy.Parried();
                                Check(brain.Skill==DeepFishSkill.None && enemy.Vulnerable,name+" deflection still cancels into stagger");
                            }
                            ValidateEscalation(game,enemy);
                        }
                    }
                    finally{if(game){game.enabled=true;game.Input.enabled=true;}}
                }
                var menu=SceneManager.LoadSceneAsync("Assets/Scenes/InkStart.unity");while(!menu.isDone)yield return null;
                yield return null;
                ValidateDisplaySettings(UnityEngine.Object.FindFirstObjectByType<TrialSettingsPanel>(FindObjectsInactive.Include));
            }
            finally{UnityEngine.Random.state=randomState;}
        }
        static void Advance(TrialEnemy enemy,float seconds)
        {for(float t=0;t<seconds;t+=.02f)enemy.Tick(Mathf.Min(.02f,seconds-t));}

        static void ValidateEscalation(TrialGame game,TrialEnemy enemy)
        {
            string label=enemy.DisplayName;
            Prepare(game,enemy);
            for(int i=0;i<8;i++)
            {
                if(i>0)Advance(enemy,.08f);
                enemy.ReceiveHit(game.Player,.01f,0);
                if(i<7)Check(!enemy.ResolveActive,label+" rapid string keeps opening hit "+(i+1));
            }
            enemy.Tick(0);
            Check(enemy.ResolveActive && enemy.BreakoutInvulnerable,label+" eight fast contacts trigger protected breakout");
            float health=enemy.Health;
            Check(!enemy.ReceiveHit(game.Player,10,999) && enemy.Health==health,label+" breakout rejects opening damage");
            Advance(enemy,.8f);health=enemy.Health;enemy.ReceiveHit(game.Player,1,999);
            Check(enemy.Health<health && !enemy.Vulnerable,label+" breakout armor still takes damage after opening");
            enemy.Parried();Check(!enemy.ResolveActive,label+" parry ends rapid-pressure armor");

            Prepare(game,enemy);enemy.ReceiveHit(game.Player,.01f,999);
            float execution=enemy.ActionDuration;
            for(int i=0;i<8;i++){if(i>0)Advance(enemy,.08f);enemy.ReceiveHit(game.Player,.01f,0);}
            enemy.Tick(0);
            Check(enemy.Vulnerable && !enemy.ResolveActive && Mathf.Approximately(enemy.ActionDuration,execution),label+" rapid hits preserve full posture-break execution window");
            Advance(enemy,execution-.56f+.06f);
            Check(enemy.ResolveActive,label+" rapid-pressure breakout waits for execution window to end");
            Prepare(game,enemy);

            if(enemy.Champion)
            {
                var brain=enemy.Champion;
                Field(brain,"<Phase>k__BackingField",3);Field(brain,"phaseAnnounced",3);
                brain.BeginMove(brain.IsRemnant?2:0);var completed=brain.Move;int attacks=brain.AttacksStarted;
                Field(brain,"beat",completed.beats);Field(brain,"<Age>k__BackingField",completed.Duration-.11f);
                Call(brain,"TickAttack",.05f);
                Check(brain.AttacksStarted==attacks,label+" follow-up waits for authored recovery");
                Call(brain,"TickAttack",.07f);
                bool twin=brain.Kind==ChampionKind.FeiBi || brain.Kind==ChampionKind.NuoNuo;
                Check(twin?brain.Regrouping:brain.AttacksStarted==attacks+1 && brain.Age==0,label+(twin?" living twins pass initiative":" phase three chains with a fresh full windup"));
                brain.CancelAttack(true);
                Check(!(bool)Call(brain,"TryChainPressure",completed,2),label+" third move always ends normal string");
                Field(brain,"<Phase>k__BackingField",2);Field(brain,"phaseAnnounced",2);
                Check(!(bool)Call(brain,"TryChainPressure",completed,1),label+" phase two stops after two moves");
                Field(brain,"<Phase>k__BackingField",1);Field(brain,"phaseAnnounced",1);
                Check(!(bool)Call(brain,"TryChainPressure",completed,0),label+" phase one retains single-move rhythm");
                Field(brain,"<Phase>k__BackingField",3);Field(brain,"phaseAnnounced",3);
                var finisher=new ChampionMove("test","",ChampionShape.Cut,ChampionDefense.Deflect,.8f,.5f,1,4,0,90,1,5);
                Check(!(bool)Call(brain,"TryChainPressure",finisher,0),label+" long finisher retains extra punish window");
                Field(brain,"phaseAnnounced",2);
                Check(!(bool)Call(brain,"TryChainPressure",completed,0),label+" pending phase transition interrupts chaining");
                if(brain.Kind==ChampionKind.Anon)
                {
                    Field(brain,"phaseAnnounced",3);brain.BeginMove(4);Field(brain,"chainDepth",2);
                    Call(brain,"SpawnHazard",enemy.transform.position,Vector3.zero,true);
                    Field(brain,"beat",brain.Move.beats);Field(brain,"<Age>k__BackingField",brain.Move.Duration-.01f);
                    Call(brain,"TickAttack",.02f);
                    Check(brain.Regrouping && !brain.Attacking,label+" sword garden cannot bypass three-move limit");
                }
                Prepare(game,enemy);
                if(brain.Kind==ChampionKind.Taffy)
                {
                    game.Player.Teleport(new Vector3(0,3,33));brain.BeginMove(1);
                    Call(brain,"LockAim");
                    // Move after aim commits: the shot must retain its locked direction.
                    game.Player.Teleport(new Vector3(8,0,33));Call(brain,"Contact");
                    var snapshot=brain.CaptureNetwork();bool upward=snapshot.hazards.Length>0;
                    foreach(var hazard in snapshot.hazards)upward&=hazard.velocity.y>0 && Mathf.Abs(hazard.velocity.x)<.01f;
                    Check(upward,label+" anti-air shot retains locked elevation and heading");
                }
            }
            else
            {
                var brain=enemy.Boss;
                Field(brain,"<Enraged>k__BackingField",true);Field(brain,"<Frenzied>k__BackingField",true);
                for(int depth=0;depth<3;depth++)
                {
                    brain.StartSkill(DeepFishSkill.TailSweep);Field(brain,"comboDepth",depth);
                    var snapshot=brain.CaptureNetwork();Field(brain,"age",snapshot.duration-.01f);
                    Call(brain,"TickSkill",.02f);
                    Check(depth<2?brain.Skill!=DeepFishSkill.None:brain.Skill==DeepFishSkill.None && brain.Regrouping,label+" frenzy string boundary "+(depth+1));
                }
                brain.CancelAttack();game.Player.Teleport(new Vector3(0,3,33));brain.StartSkill(DeepFishSkill.BubbleBarrage);Call(brain,"ShootFan",0);
                var hazards=brain.GetType().GetField("hazards",Private).GetValue(brain) as Array;
                bool upward=false;
                foreach(var hazard in hazards)
                {
                    var type=hazard.GetType();
                    if((bool)type.GetField("active").GetValue(hazard) && !(bool)type.GetField("shockwave").GetValue(hazard))
                        upward|=((Vector3)type.GetField("velocity").GetValue(hazard)).y>0;
                }
                Check(upward,label+" bubble fan can reach airborne player");
            }
            Prepare(game,enemy);
            Check(!enemy.ResolveActive && !enemy.Flinching,label+" retry clears escalation state");
        }
        static void ValidateMeshes()
        {
            var root=new GameObject("Warning mesh transform validation");
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(7,2,9),Quaternion.Euler(0,37,0));root.transform.localScale=new Vector3(2,1,3);
                var line=root.AddComponent<LineRenderer>();line.positionCount=65;line.useWorldSpace=true;
                var points=new Vector3[65];for(int i=0;i<65;i++)points[i]=new Vector3(Mathf.Cos(i*Mathf.PI/32)*4,.1f,Mathf.Sin(i*Mathf.PI/32)*4);
                line.SetPositions(points);
                var surface=root.AddComponent<TrialSkillSurface>();surface.Initialize(line);Call(surface,"LateUpdate");
                var filter=surface.GetComponentInChildren<MeshFilter>();bool matches=true;
                var mesh=filter.sharedMesh;var vertices=mesh.vertices;
                for(int i=0;i<65;i++)matches&=(filter.transform.TransformPoint(vertices[i])-(points[i]-Vector3.up*.016f)).sqrMagnitude<.000001f;
                Check(matches,"Warning surface keeps world footprint under translated, rotated, scaled parent");
                Check(mesh.bounds.size.sqrMagnitude>1,"Warning surface keeps nonzero automatic bounds");
                line.positionCount=2;line.SetPosition(0,Vector3.zero);line.SetPosition(1,Vector3.forward*3);Call(surface,"LateUpdate");
                Check(mesh.vertexCount==4 && mesh.triangles.Length==6,"Warning surface safely shrinks from ring to ribbon");
                line.positionCount=65;line.SetPositions(points);Call(surface,"LateUpdate");
                Check(mesh.vertexCount==65 && mesh.triangles.Length==189,"Warning surface safely grows back to ring");
                var wave=root.AddComponent<TrialWaveCrest>();wave.Initialize(line,Resources.Load<Material>("Trial/VFX/TideRibbon"));Call(wave,"LateUpdate");
                var sheet=root.transform.Find("Moving tidal crest");var waveMesh=sheet.GetComponent<MeshFilter>().sharedMesh;
                Check(waveMesh.bounds.size.sqrMagnitude>1,"Wave crest keeps nonzero automatic bounds");
                line.enabled=false;Call(wave,"LateUpdate");Call(surface,"LateUpdate");
                Check(!sheet.GetComponent<Renderer>().enabled && !filter.GetComponent<Renderer>().enabled,"Released warning hides both mesh layers");
            }
            finally{UnityEngine.Object.Destroy(root);}
        }
        static void ValidateDisplaySettings(TrialSettingsPanel panel)
        {
            Check(panel,"Shared display settings exist in "+SceneManager.GetActiveScene().name);
            bool hadFrame=PlayerPrefs.HasKey("Ink.MobileFrameRate"),hadSync=PlayerPrefs.HasKey("Ink.VSync");
            int frame=PlayerPrefs.GetInt("Ink.MobileFrameRate",60),sync=PlayerPrefs.GetInt("Ink.VSync",0);
            try
            {
                panel.Show();UnityEngine.UI.Button frameButton=null,syncButton=null;
                foreach(var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>())
                {if(button.name=="Frame rate limit")frameButton=button;if(button.name=="VSync")syncButton=button;}
                Check(frameButton && syncButton,"Frame cap and VSync buttons are visible");
                TrialPreferences.VSyncEnabled=false;PlayerPrefs.SetInt("Ink.MobileFrameRate",0);
                foreach(int expected in new[]{30,60,120,144,0,30})
                {
                    frameButton.onClick.Invoke();Check(TrialPreferences.MobileFrameRate==expected,"Frame cap cycles to "+expected);
                    Check(QualitySettings.vSyncCount==0 && Application.targetFrameRate==(expected==0?-1:expected),"Frame cap applies immediately: "+expected);
                }
                syncButton.onClick.Invoke();Check(TrialPreferences.VSyncEnabled && QualitySettings.vSyncCount==1 && Application.targetFrameRate==-1,"VSync enables immediately and takes precedence");
                syncButton.onClick.Invoke();Check(!TrialPreferences.VSyncEnabled && QualitySettings.vSyncCount==0 && Application.targetFrameRate==30,"Disabling VSync restores selected cap");
                Check(PlayerPrefs.GetInt("Ink.VSync")==0 && PlayerPrefs.GetInt("Ink.MobileFrameRate")==30,"Display choices persist in PlayerPrefs");
                Check(TrialPowerBudget.Budget(0,true,true,false)==-1 && TrialPowerBudget.Budget(0,true,true,true)==-1,"Unlimited survives combat and LAN frame budgeting");
                Check(TrialPowerBudget.Budget(30,true,true,true)==30,"LAN honors explicit 30 FPS choice");
                Check(TrialPowerBudget.Budget(0,false,true,false)==30 && TrialPowerBudget.Budget(0,true,false,false)==15,"Unlimited retains solo idle and background power saving");
                Canvas.ForceUpdateCanvases();var hint=panel.GetComponentsInChildren<UnityEngine.UI.Text>();bool hintVisible=false;
                foreach(var label in hint)if(label.text.Contains("所选帧率限制"))hintVisible=label.rectTransform.rect.height>=label.preferredHeight;
                Check(hintVisible,"Display settings explanation fits the panel");
            }
            finally
            {
                panel.Hide();if(hadFrame)PlayerPrefs.SetInt("Ink.MobileFrameRate",frame);else PlayerPrefs.DeleteKey("Ink.MobileFrameRate");
                if(hadSync)PlayerPrefs.SetInt("Ink.VSync",sync);else PlayerPrefs.DeleteKey("Ink.VSync");
                PlayerPrefs.Save();TrialPreferences.ApplyMobileFrameRate();
            }
        }
    }
}
