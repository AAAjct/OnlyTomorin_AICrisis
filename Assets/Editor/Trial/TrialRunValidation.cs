using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TomorinTrial.Editor
{
    public static class TrialRunValidation
    {
        static readonly List<string> passed=new List<string>();
        static void Check(bool condition,string text){if(!condition)throw new InvalidOperationException(text);passed.Add(text);}
        static void Set(object target,Type owner,string name,object value)=>owner.GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
        static void ChooseAll(TrialPlayer player){player.OpenRunUpgrade();int count=0;while(player.ChoosingRunAffix && count++<100)player.ChooseRunAffix(0);}
        static void DefeatBoss(TrialGame game,TrialEnemy enemy)
        {if(enemy.Champion)enemy.Champion.CancelAttack(true);if(enemy.Boss)enemy.Boss.CancelAttack();Set(enemy,typeof(TrialFighter),"Health",0f);game.OnEnemyDied(enemy);}
        [MenuItem("Tools/Tomorin/Verify endless run (Play Mode)")]
        public static void VerifyEndless(){Debug.Log(Endless());}
        public static string Endless()
        {
            passed.Clear();var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
            Check(game && game.Ready && game.IsEndless,"Endless scene initialized");game.RestartTrial();
            var player=game.Player;Check(!player.ChoosingBoon,"Endless starts without the one-time entrance boon");
            player.AwardRunExperience(500);Check(player.PendingRunChoices>1,"Experience queues every earned level");
            Check(!player.ChoosingRunAffix && Time.timeScale==1,"Earning levels does not interrupt combat");
            Check(player.OpenRunUpgrade(),"Player manually opens pending upgrades");
            Check(Time.timeScale==0,"Upgrade pauses scaled time");
            while(player.ChoosingRunAffix)
            {
                Check(player.RunOffers[0]!=player.RunOffers[1] && player.RunOffers[1]!=player.RunOffers[2] && player.RunOffers[0]!=player.RunOffers[2],"Three unique affix offers");
                Check(!player.ChooseRunAffix(-1),"Invalid selection rejected");player.ChooseRunAffix(0);
            }
            Check(Time.timeScale==1,"Final selection resumes time");
            game.RestartTrial();Check(player.RunLevel==1 && player.RunExperience==0 && player.PendingRunChoices==0,"Restart clears experience and pending selections");
            int[] affixes=(int[])typeof(TrialPlayer).GetField("affixes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(player);
            for(int i=0;i<affixes.Length;i++)Check(affixes[i]==0,"Restart clears affix "+i);
            // Exercise actual hit hooks, including nested area kills and delayed burn.
            var first=game.CreateRunGuard(new Vector3(0,0,15));var second=game.CreateRunGuard(new Vector3(4,0,15));
            first.ConfigureRunStrength(5,1);second.ConfigureRunStrength(5,1);
            affixes[(int)TrialAffix.Vacuum]=1;affixes[(int)TrialAffix.Shockwave]=1;affixes[(int)TrialAffix.ChainLightning]=1;affixes[(int)TrialAffix.Burn]=1;
            Set(player,typeof(TrialFighter),"Action",FighterAction.Light);Set(player,typeof(TrialFighter),"Combo",0);
            Vector3 before=second.transform.position;float health=second.Health;
            first.ReceiveHit(player,10,0);
            Check(second.Health<health,"Shockwave / lightning damage nearby enemies");
            Check((second.transform.position-first.transform.position).sqrMagnitude<(before-first.transform.position).sqrMagnitude,"Light attack vacuum moves guards inward");
            float burned=first.Health;first.Tick(1.1f);Check(first.Health<burned,"Burn deals delayed damage through the real hit path");
            affixes[(int)TrialAffix.KillBlast]=5;first.ReceiveHit(player,100000,0);ChooseAll(player);
            Check(!first.Alive && game.Mode==TrialMode.Playing,"Area effects do not recursively end the endless run");
            game.RestartTrial();
            int bosses=0,maxAlive=0;var bossIds=new HashSet<int>();
            for(int step=0;step<600 && game.Run.Wave<11;step++)
            {
                game.Run.Tick(.7f);int alive=0;
                var snapshot=new List<TrialEnemy>(game.Enemies);
                foreach(var enemy in snapshot)
                {
                    if(!enemy.Alive)continue;alive++;
                    if(enemy.IsBoss){if(bossIds.Add(enemy.GetInstanceID()))bosses++;DefeatBoss(game,enemy);}
                    else enemy.ReceiveHit(player,1000000,0,true);
                    ChooseAll(player);
                }
                maxAlive=Mathf.Max(maxAlive,alive);
            }
            Check(game.Run.Wave>=11 && bosses==2,"Ten waves advance with bosses on waves five and ten");
            Check(maxAlive<=12,"Active enemy count stays bounded");
            Check(game.Mode==TrialMode.Playing && game.Player.RunLevel>1,"Endless continues with accumulated levels");
            Check(player.PendingRunChoices==0 && !player.ChoosingRunAffix,"All manually opened upgrade queues drain cleanly");
            game.RestartTrial();
            for(int step=0;step<120;step++)game.Run.Tick(.7f);
            int live=0;foreach(var e in game.Enemies)if(e.Alive)live++;
            Check(live<=12,"Surviving enemies stop further spawns at the active cap");
            game.RestartTrial();game.Pause();return Finish("endless");
        }
        [MenuItem("Tools/Tomorin/Verify Boss rush (Play Mode)")]
        public static void VerifyBossRush(){Debug.Log(BossRush());}
        public static string BossRush()
        {
            passed.Clear();var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
            Check(game && game.Ready && game.IsBossRush,"Boss rush scene initialized");game.RestartTrial();var player=game.Player;
            Check(player.Flasks==3,"Full run starts with three flasks");if(player.ChoosingBoon)player.ChooseBoon(player.OfferA);
            Physics.SyncTransforms();
            // Consume a flask through the existing input and healing action.
            Set(player,typeof(TrialFighter),"Health",50f);
            game.Input.ApplyNetwork(new TrialControls{pressed=8});player.Tick(.01f,game.Input);
            Check(player.Action==FighterAction.Heal,"Flask is usable during Boss rush");
            game.Input.ApplyNetwork(new TrialControls());for(int step=0;step<60;step++)player.Tick(1f/60,game.Input);
            Check(player.Flasks==2 && player.Health>50,"Healing consumes one flask");
            player.Tick(1,game.Input);
            var seen=new HashSet<string>();
            for(int round=0;round<8;round++)
            {
                game.Run.Tick(4);TrialEnemy boss=null;int count=0;
                foreach(var enemy in game.Enemies)if(enemy.Alive){boss=enemy;count++;}
                Check(count==1 && boss.IsBoss,"Exactly one live boss in round "+(round+1));seen.Add(boss.DisplayName);
                Set(player,typeof(TrialFighter),"Health",30f);Set(player,typeof(TrialFighter),"Stamina",20f);
                DefeatBoss(game,boss);
                Check(player.Health==player.MaxHealth && player.Stamina==player.MaxStamina,"Victory restores health and stamina in round "+(round+1));
                Check(player.Flasks==2,"Victory preserves remaining flasks in round "+(round+1));
            }
            game.Run.Tick(.1f);Check(game.Mode==TrialMode.Victory && seen.Count==8,"All eight distinct bosses complete the run");
            game.RestartTrial();Check(player.Flasks==3 && game.Run.BossesDefeated==0,"Restart resets full run and restores initial three flasks");
            game.Pause();return Finish("boss-rush");
        }
        static string Finish(string name)
        {string text=string.Join("\n",passed);System.IO.Directory.CreateDirectory("output");System.IO.File.WriteAllText("output/"+name+"-validation.txt",text);Debug.Log(name+" validation: "+passed.Count+" checks passed");return text;}
    }
}
