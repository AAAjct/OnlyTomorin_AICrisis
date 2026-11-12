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
    public static class TrialTacticsValidation
    {
        public static string Status {get;private set;}="Idle";
        static IEnumerator routine;
        static double resumeAt;
        static readonly List<string> checks=new List<string>();
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [MenuItem("Tools/Tomorin/Validate tactical boss encounters (Play Mode)")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying || routine!=null)throw new InvalidOperationException("Requires Play Mode");
            checks.Clear();Status="Running";resumeAt=0;routine=Validate();EditorApplication.update+=Step;
        }
        static void Check(bool pass,string message){if(!pass)throw new Exception(message);checks.Add("PASS "+message);}
        static void Set(object target,string name,object value)=>target.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.Instance).SetValue(target,value);
        static void Field(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
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
            Directory.CreateDirectory("Logs/Trial");File.WriteAllText("Logs/Trial/TacticsValidation.txt",Status+"\n"+string.Join("\n",checks));
            if(ex==null)Debug.Log(Status);else Debug.LogException(ex);
        }
        static IEnumerator Validate()
        {
            foreach(var level in new[]{TrialLevel.Twins,TrialLevel.Anon,TrialLevel.Taffy,TrialLevel.Penguin,TrialLevel.YaYa,TrialLevel.Remnant})
            {
                SceneManager.LoadScene(TrialChampionCatalog.Scene(level));yield return .8f;
                var game=UnityEngine.Object.FindAnyObjectByType<TrialGame>();Check(game && game.Ready,level+" scene starts");
                if(level==TrialLevel.Anon)
                {
                    Check(TrialTerraceValidation.Run().StartsWith("PASS"),"Crimson Sword Terrace passes its dedicated combat checks");
                    continue;
                }
                game.StartTrial();game.Player.ChooseBoon(game.Player.OfferA);game.enabled=false;game.Input.enabled=false;
                var enemy=game.Enemies[0];var brain=enemy.Champion;
                if(brain.Kind==ChampionKind.Anon)Field(brain,"introPlayed",true);
                game.Player.Restore(new Vector3(0,0,8));enemy.Tick(.01f);brain.BeginMove(0);
                brain.Tick(brain.Move.Duration+.01f);
                Check(brain.Regrouping && !brain.Attacking,level+" finishes a string with a deliberate respite");
                int attacks=brain.AttacksStarted;
                Set(game.Player,"Action",FighterAction.Heal);Set(game.Player,"ActionDuration",2f);
                for(int i=0;i<12;i++)brain.Tick(.05f);
                Check(brain.AttacksStarted==attacks && !brain.Attacking,level+" healing cannot cancel the quiet interval");
                Check(brain.RespiteRemaining>0,level+" respite retains a meaningful minimum duration");
                enemy.ResetFighter();game.Player.Restore(new Vector3(0,0,28));enemy.Teleport(new Vector3(0,0,32));enemy.Tick(.001f);
                if(level==TrialLevel.Twins)
                {
                    brain.BeginMove(0);brain.Tick(brain.Move.Duration+.01f);var partner=game.Enemies[1].Champion;partner.BeginMove(0);
                    float rest=brain.RespiteRemaining;brain.Tick(.5f);
                    Check(brain.RespiteRemaining<rest && !brain.Attacking,"Twins recovery clock progresses during partner attack");partner.CancelAttack(true);
                }
                if(level!=TrialLevel.Anon && level!=TrialLevel.Remnant){game.enabled=true;game.Input.enabled=true;continue;}
                Set(game.Player,"Action",FighterAction.Light);Set(game.Player,"ActionTime",0f);Set(game.Player,"ActionDuration",.5f);
                brain.Tick(.01f);Check(enemy.Guarding,level+" reads a light startup and visibly raises guard");
                float health=enemy.Health;enemy.ReceiveHit(game.Player,100,0);
                Check(Mathf.Abs(health-enemy.Health-18)<.01f,level+" frontal light guard mitigates 82% damage");
                Set(game.Player,"Action",FighterAction.Heavy);health=enemy.Health;enemy.ReceiveHit(game.Player,100,0);
                Check(Mathf.Abs(health-enemy.Health-100)<.01f && !enemy.Guarding,level+" heavy attack breaks the sword guard");
                enemy.ResetFighter();game.Player.Restore(new Vector3(0,0,28));enemy.Teleport(new Vector3(0,0,32));enemy.Tick(.001f);
                Field(brain,"pressureAge",1f);Field(brain,"evasionCooldown",0f);Field(brain,"mobilityCooldown",0f);Field(brain,"decisionClock",0f);
                Set(game.Player,"Action",FighterAction.Heavy);Set(game.Player,"ActionTime",0f);Set(game.Player,"ActionDuration",1f);
                brain.Tick(.01f);Check(brain.Mobility==ChampionMobility.SideSlip,level+" heavy startup triggers a side slip");
                brain.Tick(.08f);health=enemy.Health;
                Check(brain.Evading && !enemy.ReceiveHit(game.Player,100,20) && enemy.Health==health,level+" moving evade has actual invulnerability");
                brain.Tick(.3f);Check(!brain.Evading,level+" evade invulnerability ends with its window");
                enemy.ResetFighter();game.Player.Restore(new Vector3(0,0,2));enemy.Teleport(new Vector3(0,0,32));enemy.Tick(.001f);Field(brain,"mobilityCooldown",100f);
                if(brain.Kind==ChampionKind.Anon)Field(brain,"introPlayed",true);
                Field(brain,"pursuitAge",3f);brain.Tick(.05f);
                Check(!brain.Regrouping,level+" sustained pursuit does not grant another recovery window");
                if(level==TrialLevel.Remnant)
                {
                    Check(enemy.MaxHealth==7200 && enemy.PostureLimit==340,"Remnant has the strongest base health and posture");
                    Check(enemy.GetComponentsInChildren<SkinnedMeshRenderer>().Length>=3,"Remnant includes Tomorin skins and bone-bound custom regalia");
                    for(int i=0;i<16;i++)Check(TrialChampionCatalog.Move(ChampionKind.Remnant,i).damage>=57,"Remnant move "+i+" has extreme damage");
                    for(int i=0;i<16;i++)
                    {
                        enemy.ResetFighter();enemy.Teleport(new Vector3(0,0,32));game.Player.Restore(new Vector3(0,0,25));
                        Set(game.Player,"Health",100000f);enemy.Tick(.001f);brain.BeginMove(i);
                        float duration=brain.Move.Duration;
                        for(float t=0;t<duration+.04f;t+=.033f)brain.Tick(.033f);
                        Check(!brain.Attacking && brain.Regrouping,"Remnant full move "+i+" runs to completion and regroups");
                    }
                    game.Player.Restore(new Vector3(0,0,25));game.Player.ReceiveHit(enemy,10000,0,true);
                    Check(!game.Player.Alive,"Remnant bypasses the player last-stand safety net");
                    Check(brain.KillStacks==0,"Solo kills do not apply cooperative growth");
                }
                game.enabled=true;game.Input.enabled=true;
            }
            SceneManager.LoadScene(TrialChampionCatalog.Scene(TrialLevel.BigFish));yield return .8f;
            var fishGame=UnityEngine.Object.FindAnyObjectByType<TrialGame>();fishGame.StartBossChallenge();fishGame.Player.ChooseBoon(fishGame.Player.OfferA);fishGame.enabled=false;
            var fish=fishGame.Enemies[fishGame.Enemies.Count-1];fish.Tick(.01f);fish.Boss.StartSkill(DeepFishSkill.TailSweep);fish.Boss.Tick(2.1f);
            Check(fish.Boss.Regrouping && fish.Boss.Skill==DeepFishSkill.None,"Big fish also regroups after its attack");fishGame.enabled=true;
            SceneManager.LoadScene(TrialChampionCatalog.Scene(TrialLevel.Remnant));yield return .8f;
        }
    }
}
