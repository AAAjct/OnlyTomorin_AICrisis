using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mirror;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TomorinTrial.Editor
{
    public static class TrialBossPressureValidation
    {
        static IEnumerator routine;
        static readonly List<string> results=new List<string>();
        public static string Status {get;private set;}="Idle";
        [MenuItem("Tools/Tomorin/Validate boss pressure and lock camera (Play Mode)")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying || routine!=null)throw new InvalidOperationException("Requires Play Mode and no running validation.");
            results.Clear();Status="Running";routine=Validate();EditorApplication.update+=Step;
        }
        static void Check(bool pass,string message)
        {if(!pass)throw new Exception(message);results.Add("PASS "+message);}
        static void Step()
        {
            try {if(!EditorApplication.isPlaying)throw new Exception("Play Mode ended");if(routine.MoveNext())return;Status="PASS "+results.Count+" checks";Finish();}
            catch(Exception ex){Status="FAIL "+ex;results.Add(Status);Finish();Debug.LogError(Status);}
        }
        static void Finish()
        {EditorApplication.update-=Step;routine=null;Directory.CreateDirectory("Logs/Trial");File.WriteAllLines("Logs/Trial/BossPressureValidation.txt",results);}
        static void Prepare(TrialGame game,TrialEnemy enemy)
        {
            game.Player.Restore(new Vector3(0,0,28));enemy.ResetFighter();enemy.Teleport(new Vector3(0,0,25));Physics.SyncTransforms();enemy.Tick(.001f);
        }
        static void Advance(TrialEnemy enemy,float seconds)
        {for(float t=0;t<seconds-.001f;t+=.05f)enemy.Tick(Mathf.Min(.05f,seconds-t));}
        static IEnumerator Validate()
        {
            var levels=new[]{TrialLevel.BigFish,TrialLevel.Twins,TrialLevel.Anon,TrialLevel.Taffy,TrialLevel.Penguin,TrialLevel.YaYa};
            foreach(var level in levels)
            {
                Status="Testing "+level;
                var load=SceneManager.LoadSceneAsync(TrialChampionCatalog.Scene(level));while(!load.isDone)yield return null;
                var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();while(!game || !game.Ready){yield return null;game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();}
                game.StartTrial();if(level==TrialLevel.BigFish)game.StartBossChallenge();
                game.enabled=false;game.Input.enabled=false;
                game.Player.transform.rotation=Quaternion.Euler(0,90,0);
                game.Player.ToggleLock();
                Check(game.Player.Locked && game.Player.LockTarget is TrialEnemy lockedBoss && lockedBoss.IsBoss,
                    level+" can lock a boss from the encounter spawn");
                Vector3 bearing=Vector3.ProjectOnPlane(game.Player.LockTarget.transform.position-game.Player.transform.position,Vector3.up);
                Check(Vector3.Angle(game.Player.transform.forward,bearing)<1,level+" lock turns the player toward the boss without movement input");
                game.Player.ToggleLock();
                if(level==TrialLevel.Anon)
                {
                    foreach(string result in TrialTerraceValidation.Run().Split('\n'))results.Add(result);
                    game.enabled=true;game.Input.enabled=true;
                    continue;
                }
                foreach(var enemy in game.Enemies)
                {
                    if(!enemy.IsBoss)continue;
                    Prepare(game,enemy);
                    enemy.ReceiveHit(game.Player,1,0);Advance(enemy,.7f);enemy.ReceiveHit(game.Player,1,0);enemy.Tick(0);
                    Check(!enemy.ResolveActive,enemy.DisplayName+" isolated pair does not trigger escape");
                    Prepare(game,enemy);
                    for(int i=0;i<5;i++){if(i>0)Advance(enemy,.35f);enemy.ReceiveHit(game.Player,1,0);}enemy.Tick(0);
                    Check(enemy.ResolveActive && enemy.BreakoutInvulnerable,enemy.DisplayName+" sustained pressure triggers protected retaliation");
                    Check(enemy.Boss?enemy.Boss.Skill!=DeepFishSkill.None:enemy.Champion.Attacking || enemy.Champion.Mobility!=ChampionMobility.Grounded,enemy.DisplayName+" escape starts an actual skill or evasive follow-up");
                    float health=enemy.Health;
                    Check(!enemy.ReceiveHit(game.Player,20,999) && enemy.Health==health,enemy.DisplayName+" initial escape rejects damage and posture");
                    var state=enemy.CaptureNetwork(7);
                    Check(state.bossResolve>0,enemy.DisplayName+" snapshot includes protection clock");
                    using(var writer=NetworkWriterPool.Get())
                    {
                        writer.Write(state);
                        using(var reader=NetworkReaderPool.Get(writer.ToArraySegment()))
                            Check(Mathf.Approximately(reader.Read<TrialActorState>().bossResolve,state.bossResolve),enemy.DisplayName+" Mirror serialization retains protection");
                    }
                    Advance(enemy,.8f);health=enemy.Health;enemy.ReceiveHit(game.Player,1,999);
                    Check(enemy.Health<health && !enemy.Vulnerable,enemy.DisplayName+" armor takes damage without being staggered");
                    enemy.Parried();Check(!enemy.ResolveActive,enemy.DisplayName+" successful parry cancels protection");
                    Prepare(game,enemy);enemy.ReceiveHit(game.Player,1,999);
                    float window=enemy.ActionDuration;
                    for(int i=0;i<5;i++){Advance(enemy,.25f);enemy.ReceiveHit(game.Player,1,0);}enemy.Tick(0);
                    Check(enemy.Vulnerable && !enemy.ResolveActive && enemy.ActionTime>=1.24f && enemy.ActionDuration==window,enemy.DisplayName+" full posture-break window remains available");
                    Advance(enemy,window-1.25f+.1f);
                    Check(enemy.ResolveActive,enemy.DisplayName+" queued retaliation starts after execution window");
                    enemy.ResetFighter();Check(!enemy.ResolveActive && !enemy.BreakoutInvulnerable,enemy.DisplayName+" retry clears protection");
                }
                if(level==TrialLevel.Penguin)
                {
                    var move=TrialChampionCatalog.Move(ChampionKind.Penguin,0);
                    Check(move.windup<.5f && move.interval<.52f && move.recovery<.7f && move.damage>48,"Penguin faster double hammer and stronger damage");
                    var enemy=game.Enemies[0];Prepare(game,enemy);if(game.Player.Locked)game.Player.ToggleLock();game.Player.ToggleLock();
                    game.CameraRig.Snap();game.CameraRig.Frozen=false;
                    for(int f=0;f<120;f++)yield return null;
                    float baseHeight=game.CameraRig.transform.position.y,peakHeight=0,peakYaw=0;
                    for(int f=0;f<180;f++)
                    {
                        float yaw=game.CameraRig.transform.eulerAngles.y;
                        // Alternate an airborne target, fast crossovers, and near-overlap on both sides.
                        Vector3 offset=f<60?new Vector3(0,f%2==0?4:0,-3):f<120?new Vector3(f%2==0?8:-8,0,-3):new Vector3(f%2==0?.1f:-.1f,0,0);
                        enemy.Teleport(game.Player.transform.position+offset);
                        yield return null;
                        peakHeight=Mathf.Max(peakHeight,Mathf.Abs(game.CameraRig.transform.position.y-baseHeight));
                        peakYaw=Mathf.Max(peakYaw,Mathf.Abs(Mathf.DeltaAngle(yaw,game.CameraRig.transform.eulerAngles.y))/Mathf.Max(.0001f,Time.deltaTime));
                    }
                    Check(peakHeight<.25f,"Aerial and crossing targets keep camera height stable: "+peakHeight.ToString("F3")+"m");
                    Check(peakYaw<112,"Fast targets and overlaps bound camera yaw: "+peakYaw.ToString("F2")+" deg/s");
                    enemy.ResetFighter();
                }
                game.enabled=true;game.Input.enabled=true;
            }
        }
    }
}
