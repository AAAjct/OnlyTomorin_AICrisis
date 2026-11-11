using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TomorinTrial.Editor
{
    public static class TrialDuelValidation
    {
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static void Set(object target,string name,object value)=>target.GetType().GetProperty(name,Flags).SetValue(target,value);
        static void Check(bool pass,string message,List<string> results)
        {if(!pass)throw new Exception(message);results.Add("PASS "+message);}

        [MenuItem("Tools/Tomorin/Validate duel decisions")]
        public static string Run()
        {
            var results=new List<string>();
            var go=new GameObject("Temporary duel validation"){hideFlags=HideFlags.HideAndDontSave};
            var other=new GameObject("Temporary second opponent"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var player=go.AddComponent<TrialPlayer>();var second=other.AddComponent<TrialPlayer>();
                Set(player,"Health",100f);Set(second,"Health",100f);
                var read=new TrialDuelRead();read.Tick(player,0);
                Set(player,"Action",FighterAction.Heal);read.Tick(player,.01f);
                Check(!read.Healing,"heal cannot be punished on its first observed frame",results);
                read.Tick(player,.19f);Check(read.Healing,"visible healing becomes punishable after reaction delay",results);
                Set(player,"Action",FighterAction.Free);read.Tick(player,.01f);
                Check(!read.Healing,"cancelled heal does not remain punishable",results);
                Set(player,"Guarding",true);read.Tick(player,.2f);
                Check(!read.HoldingGuard,"brief guard does not trigger guard punishment",results);
                read.Tick(player,.26f);Check(read.HoldingGuard,"held guard triggers alternate defense",results);
                var red=TrialChampionCatalog.Move(ChampionKind.Anon,1);
                var gold=TrialChampionCatalog.Move(ChampionKind.Anon,0);
                Check(read.Weight(red)>read.Weight(gold),"held guard favors unblockable over ordinary cuts",results);
                Set(player,"Guarding",false);read.Tick(player,.01f);
                for(int i=0;i<2;i++)
                {Set(player,"Action",FighterAction.Dodge);read.Tick(player,.01f);Set(player,"Action",FighterAction.Free);read.Tick(player,.1f);}
                Check(read.EarlyDodge,"repeated dodges create a short-lived habit",results);
                read.Tick(player,10);Check(!read.EarlyDodge,"changed behavior erases dodge habit",results);
                Set(player,"Action",FighterAction.Light);read.Tick(player,0);read.Tick(player,.2f);
                Check(read.Weight(TrialChampionCatalog.Move(ChampionKind.Anon,8))>read.Weight(gold),"light pressure invites a baitable counter stance",results);
                Set(player,"Combo",1);read.Tick(player,.2f);
                Check(read.RepeatingLight && !read.Ready,"consecutive basic lights are counted even when the action enum does not change",results);
                Set(player,"Combo",TrialPlayer.BehindThrust);read.Tick(player,.25f);read.Tick(player,.2f);
                Check(!read.LightPressure,"a specialized light followup is distinct from basic light pressure",results);
                var parry=typeof(TrialDuelRead).GetMethod("ObserveParry",Flags);
                parry.Invoke(read,new object[]{second});parry.Invoke(read,new object[]{second});
                Check(!read.RepeatingParry,"another player's deflects do not train the current matchup",results);
                parry.Invoke(read,new object[]{player});read.Tick(player,2);parry.Invoke(read,new object[]{player});read.Tick(player,2);
                Check(read.RepeatingParry && read.Weight(red)>read.Weight(gold),"repeated parries influence the next turn even after recovery",results);
                read.Tick(player,12);
                Check(!read.RepeatingParry && !read.RepeatingLight,"old offense and defense patterns decay",results);
                read.Tick(second,0);Check(!read.LightPressure && read.DodgeHabit==0,"switching multiplayer target clears opponent memory",results);
                foreach(ChampionKind kind in Enum.GetValues(typeof(ChampionKind)))
                {
                    int answers=0;
                    for(int i=0;i<TrialChampionCatalog.SkillCount;i++)
                    {var move=TrialChampionCatalog.Move(kind,i);if(move.Unblockable && move.windup>=.45f && move.beats<=4)answers++;}
                    Check(answers>0,kind+" has a readable guard-punish branch",results);
                }
                read.Reset();Check(!read.Ready && !read.HoldingGuard && !read.EarlyDodge,"retry clears observation and habits",results);
                string report=string.Join("\n",results);System.IO.Directory.CreateDirectory("Logs/Trial");
                System.IO.File.WriteAllText("Logs/Trial/DuelValidation.txt",report);return report;
            }
            finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(other);}
        }

        // Run against an initialized Play Mode encounter, without advancing or damaging the player.
        public static string ValidateLive(TrialChampion champion)
        {
            if(!EditorApplication.isPlaying || !champion)throw new InvalidOperationException("Requires an initialized champion in Play Mode.");
            var results=new List<string>();var type=typeof(TrialChampion);
            var read=(TrialDuelRead)type.GetField("duel",Flags).GetValue(champion);
            var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();var player=game.CombatTarget;
            champion.CancelAttack(true);Set(champion,"Phase",2);
            type.GetField("phaseAnnounced",Flags).SetValue(champion,2);
            Set(player,"Guarding",true);read.Tick(player,0);read.Tick(player,.5f);
            champion.BeginMove(0);
            type.GetField("beat",Flags).SetValue(champion,1);Set(champion,"Age",champion.Move.windup+.19f);
            var branch=type.GetMethod("TryDuelBranch",Flags);
            Check((bool)branch.Invoke(champion,null),"held guard changes first-beat followup",results);
            Check(champion.Age==0 && champion.WindingUp && champion.Move.windup>=.45f,"branch restarts full warning",results);
            Check((int)type.GetField("chainDepth",Flags).GetValue(champion)==2,"branch spends entire extension budget",results);
            Check(!(bool)branch.Invoke(champion,null),"cannot recursively branch",results);
            champion.CancelAttack(true);champion.BeginMove(0);
            var enemy=champion.GetComponent<TrialEnemy>();
            float posture=enemy.Posture;
            enemy.Parried(player,true);
            Check(champion.Attacking && enemy.Posture==posture,"deflecting a projectile does not interrupt the source's current move",results);
            enemy.Parried(player);
            Check(champion.Attacking && enemy.Posture>posture,"a direct deflect builds posture and preserves committed beats",results);
            Check(!(bool)type.GetMethod("TryChainPressure",Flags).Invoke(champion,new object[]{champion.Move,0}),"successful deflect denies followup",results);
            int answer=(int)type.GetMethod("ChooseDuelResponse",Flags).Invoke(champion,new object[]{3f,false});
            Check(answer>=0 && TrialChampionCatalog.Move(champion.Kind,answer).Unblockable,"the next attack answers repeated deflects with a peril",results);
            champion.CancelAttack(true);read.Reset();Set(player,"Guarding",false);
            Check((int)type.GetMethod("ChooseDuelFollowup",Flags).Invoke(champion,new object[]{3f})==-1,"disciplined neutral play earns end of string",results);
            champion.ResetEncounter();return string.Join("\n",results);
        }
    }
}
