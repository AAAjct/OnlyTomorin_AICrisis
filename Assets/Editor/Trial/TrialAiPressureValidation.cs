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
