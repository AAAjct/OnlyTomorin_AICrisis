using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mirror;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TomorinTrial.Editor
{
    public static class TrialSignatureValidation
    {
        public static string Status {get;private set;}="Idle";
        static IEnumerator routine;
        static double resumeAt;
        static readonly List<string> checks=new List<string>();
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool pass,string message){if(!pass)throw new Exception(message);checks.Add("PASS "+message);}
        static void Set(object target,string name,object value)=>target.GetType().GetProperty(name).SetValue(target,value);
        static void Field(object target,string name,object value)=>target.GetType().GetField(name,Hidden).SetValue(target,value);
        [MenuItem("Tools/Tomorin/Validate original boss signatures (Play Mode)")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying || routine!=null)throw new InvalidOperationException("Requires Play Mode");
            checks.Clear();Status="Running";resumeAt=0;routine=Validate();EditorApplication.update+=Step;
        }
        static void Step()
        {
            if(EditorApplication.timeSinceStartup<resumeAt)return;
            try
            {
                if(!EditorApplication.isPlaying)throw new Exception("Play Mode ended");
                if(routine.MoveNext()){resumeAt=EditorApplication.timeSinceStartup+(routine.Current is float wait?wait:0);return;}
                Finish(null);
            }
            catch(Exception ex){Finish(ex);}
        }
        static void Finish(Exception ex)
        {
            (routine as IDisposable)?.Dispose();routine=null;EditorApplication.update-=Step;Time.timeScale=1;
            Status=ex==null?"PASS "+checks.Count+" checks":"FAIL "+ex;
            Directory.CreateDirectory("Logs/Trial");File.WriteAllText("Logs/Trial/SignatureValidation.txt",Status+"\n"+string.Join("\n",checks));
            if(ex==null)Debug.Log(Status);else Debug.LogException(ex);
        }
        static void Prepare(TrialGame game,TrialEnemy boss,Vector3 player)
        {
            boss.ResetFighter();if(boss.Champion)Field(boss.Champion,"introPlayed",true);
            game.Player.Restore(player);Set(game.Player,"Health",100000f);
            boss.Teleport(new Vector3(0,0,29));boss.Tick(.001f);boss.Champion?.CancelAttack(true);
        }
        static void ValidateDeath(TrialGame game,TrialEnemy boss)
        {
            Prepare(game,boss,new Vector3(0,0,25));boss.ReceiveHit(game.Player,1,0);boss.ReceiveHit(game.Player,1000000,0);boss.Tick(.02f);
            Check(!boss.Alive && !boss.Flinching && boss.Champion.ActiveHazards==0,boss.Champion.Kind+" death immediately clears flinch and persistent hazards");
        }
        static IEnumerator Validate()
        {
            Check(TrialPlayer.BaseStamina==630,"Base stamina supports extended sword chains (630)");
            var names=new HashSet<string>();
            for(int kind=0;kind<7;kind++)for(int i=0;i<16;i++)
                Check(names.Add(TrialChampionCatalog.Move((ChampionKind)kind,i).name),"Unique move name "+(ChampionKind)kind+" / "+i);
            foreach(var asset in new[]{"SignatureSword","SignatureCrystal","SignatureFeather"})
                Check(Resources.Load<GameObject>("Trial/VFX/"+asset),"Blender model imports: "+asset);
            foreach(var level in new[]{TrialLevel.Twins,TrialLevel.Anon,TrialLevel.Taffy,TrialLevel.Penguin,TrialLevel.YaYa,TrialLevel.Remnant})
            {
                SceneManager.LoadScene(TrialChampionCatalog.Scene(level));yield return .85f;
                var game=UnityEngine.Object.FindAnyObjectByType<TrialGame>();Check(game && game.Ready,level+" initializes");
                if(level==TrialLevel.Anon)
                {
                    Check(TrialTerraceValidation.Run().StartsWith("PASS"),"Crimson Sword Terrace passes its dedicated combat checks");
                    continue;
                }
                game.StartTrial();game.Player.ChooseBoon(game.Player.OfferA);game.enabled=false;game.Input.enabled=false;
                foreach(var boss in game.Enemies)
                {
                    var brain=boss.Champion;
                    if(brain.Kind==ChampionKind.Anon)
                    {
                        Set(boss,"Awakened",true);Check(brain.Introducing && boss.Invulnerable,"Anon intro protects the landing and draw sequence");
                        brain.Tick(1.2f);Check(boss.transform.position.y>0,"Anon really descends from above");
                        brain.Tick(.6f);Check(boss.transform.position.y<.1f && brain.Introducing,"Anon lands before drawing the center sword");
                        brain.Tick(1.8f);Check(!brain.Introducing && !boss.Invulnerable,"Draw sequence releases combat after 3.5 seconds");
                        Check(brain.Rig.LeftWeapon && brain.Rig.WeaponRoot,"Anon has two independent physical swords");
                    }
                    for(int i=0;i<16;i++)
                    {
                        Prepare(game,boss,new Vector3(0,0,24));brain.BeginMove(i);
                        float duration=brain.Move.Duration;int started=brain.AttacksStarted;
                        for(float t=0;t<duration+.1f;t+=.025f)brain.Tick(.025f);
                        Check(!brain.Attacking || brain.Kind==ChampionKind.Anon && i==4 && brain.MoveIndex==10,brain.Kind+" move "+i+" completes its authored beats");
                        if(i==4 && brain.Kind==ChampionKind.Anon)Check(brain.Attacking && brain.ActiveHazards>0,"Anon pressures with close combat while the independent sword garden persists");
                        var snapshot=brain.CaptureNetwork();
                        using(var writer=NetworkWriterPool.Get())
                        {
                            writer.Write(snapshot);
                            using(var reader=NetworkReaderPool.Get(writer.ToArraySegment()))
                            {
                                var copy=reader.Read<TrialChampionState>();
                                Check(copy.move==snapshot.move && copy.hazards.Length==snapshot.hazards.Length && copy.introPlayed==snapshot.introPlayed,brain.Kind+" move "+i+" roundtrips through Mirror serialization");
                            }
                        }
                        yield return .01f;
                    }
                    Prepare(game,boss,new Vector3(0,0,25));brain.BeginMove(0);
                    Set(game.Player,"Action",FighterAction.Light);
                    Check(boss.ReceiveHit(game.Player,1,0),brain.Kind+" ordinary hit lands");float age=brain.Age;
                    Check(boss.Flinching && !boss.Vulnerable,brain.Kind+" short flinch is separate from execution stagger");
                    boss.Tick(.06f);Check(brain.Age==age,brain.Kind+" hit flinch actually pauses the attack clock");
                    boss.Tick(.3f);boss.Tick(.025f);Check(!boss.Flinching && brain.Age>age,brain.Kind+" attack resumes after a bounded flinch");
                    using(var writer=NetworkWriterPool.Get())
                    {
                        var actor=boss.CaptureNetwork(1);actor.bossFlinch=.17f;writer.Write(actor);
                        using(var reader=NetworkReaderPool.Get(writer.ToArraySegment()))Check(reader.Read<TrialActorState>().bossFlinch==.17f,brain.Kind+" remote actor receives flinch state");
                    }
                    Prepare(game,boss,new Vector3(0,0,25));boss.ReceiveHit(game.Player,1,10000);
                    Check(boss.Vulnerable && boss.ActionDuration>=2.5f,brain.Kind+" posture break retains the full execution window");
                    if(brain.Kind!=ChampionKind.Anon){ValidateDeath(game,boss);continue;}
                    Prepare(game,boss,new Vector3(0,0,25));Set(game.Player,"Action",FighterAction.Light);Set(game.Player,"ActionTime",0f);Set(game.Player,"ActionDuration",1f);
                    brain.Tick(.01f);Check(boss.Guarding,"Anon reads light startup and guards");
                    var before=game.Player.transform.position;float hp=boss.Health;boss.ReceiveHit(game.Player,100,0);
                    Check(Mathf.Abs(hp-boss.Health-18)<.1f && !boss.Flinching,"Sword guard mitigates damage without a false hit flinch");
                    game.Input.ApplyNetwork(default);game.Player.Tick(.1f,game.Input);
                    Check(Vector3.Distance(before,game.Player.transform.position)>.65f,"Anon guard physically repels the player");
                    Prepare(game,boss,new Vector3(0,0,25));Set(game.Player,"Action",FighterAction.Heavy);Set(game.Player,"ActionDuration",1f);
                    Field(brain,"evasionCooldown",0f);Field(brain,"mobilityCooldown",0f);Field(brain,"decisionClock",0f);Field(brain,"pressureAge",1f);
                    brain.Tick(.01f);Check(brain.Mobility==ChampionMobility.SideSlip,"Anon reads heavy startup and evades");
                    brain.Tick(.1f);Check(brain.Evading && !boss.ReceiveHit(game.Player,100,0),"Anon side slip has actual invulnerability");
                    brain.Tick(.2f);Check(brain.MoveIndex==2 && Vector3.Dot(boss.transform.position-game.Player.transform.position,game.Player.transform.forward)<-1,"Evade chains into a readable slash behind the player");
                    Prepare(game,boss,new Vector3(0,0,24));brain.BeginMove(4);brain.Tick(.92f);
                    Check(brain.SwordRainCount==15,"Pentagram creates fifteen physical falling swords and five dangerous edges");
                    var state=brain.CaptureNetwork();var rain=Array.Find(state.hazards,h=>h.kind==(byte)ChampionHazardKind.SwordRain);
                    game.Player.Teleport(rain.position);float health=game.Player.Health;
                    brain.Tick(.1f);Check(game.Player.Health==health,"Sword rain cannot deal damage during its visible warning");
                    for(int t=0;t<24;t++)brain.Tick(.05f);
                    Check(game.Player.Health<health,"Falling sword warning becomes real damage at impact");
                    brain.CancelAttack(true);Check(brain.ActiveHazards==0,"Cancelling an encounter clears every persistent sword and edge");
                    ValidateDeath(game,boss);
                }
                game.enabled=true;game.Input.enabled=true;
            }
            SceneManager.LoadScene(TrialChampionCatalog.Scene(TrialLevel.BigFish));yield return .85f;
            var fishGame=UnityEngine.Object.FindAnyObjectByType<TrialGame>();fishGame.StartBossChallenge();fishGame.Player.ChooseBoon(fishGame.Player.OfferA);fishGame.enabled=false;
            var fish=fishGame.Enemies[fishGame.Enemies.Count-1];fish.Tick(.01f);fish.Boss.StartSkill(DeepFishSkill.TailSweep);fish.ReceiveHit(fishGame.Player,1,0);
            float progress=fish.Boss.SkillProgress;fish.Tick(.06f);
            Check(fish.Flinching && fish.Boss.SkillProgress==progress,"Big fish also pauses its body during ordinary hit flinch");
            fishGame.enabled=true;
            SceneManager.LoadScene(TrialChampionCatalog.Scene(TrialLevel.Anon));yield return .85f;
        }
    }
}
