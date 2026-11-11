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
        static void CheckRunAnon(TrialGame game)
        {
            game.RestartTrial();
            var player=game.Player;
            if(player.ChoosingBoon)player.ChooseBoon(player.OfferA);
            // A preceding guard catches assumptions that the boss is always enemy zero.
            game.CreateRunGuard(new Vector3(-16,0,42));
            game.SpawnRunBoss(3,1);
            var boss=game.Enemies[game.Enemies.Count-1];
            var brain=boss.Champion;
            Check(brain.Kind==ChampionKind.Anon && brain.TerraceDuel,"Run Anon uses the standalone sword-duel brain");
            Check(Mathf.Approximately(boss.MaxHealth,1800*TrialDifficultySettings.HealthScale(game.Difficulty)) && boss.PostureLimit==100,
                "Run Anon shares standalone base health and posture");
            var opponent=typeof(TrialPlayer).GetProperty("TerraceOpponent",BindingFlags.Instance|BindingFlags.NonPublic);
            var rules=typeof(TrialPlayer).GetProperty("TerraceRules",BindingFlags.Instance|BindingFlags.NonPublic);
            Check(opponent.GetValue(player)==boss && (bool)rules.GetValue(player),"Player finds run Anon after guards and enables sword-duel defenses");
            boss.Teleport(new Vector3(18,0,42));player.Restore(new Vector3(18,0,37));Physics.SyncTransforms();
            brain.BeginMove(0);
            Check(brain.Move.name=="踏线挑斩" && Mathf.Approximately(brain.Move.windup,.68f),"Run Anon opens with the authored terrace slash and timing");
            for(int move=0;move<13;move++)
            {
                brain.BeginMove(move);
                var state=brain.CaptureNetwork();
                Check(state.move==move && state.terraceWindup>0 && brain.Move.beats==1,"Run sword cut replicates terrace timing for move "+move);
            }
            brain.CancelAttack(true);
            Vector3 before=boss.transform.position;
            for(int step=0;step<30;step++)boss.Tick(.01f);
            Check(Vector3.ProjectOnPlane(boss.transform.position-before,Vector3.up).sqrMagnitude>.01f,
                "Run Anon moves freely beyond the standalone circular arena");
            brain.CancelAttack(true);
            Set(boss,typeof(TrialFighter),"Health",boss.MaxHealth*.45f);
            for(int step=0;step<400 && brain.Phase==1;step++)boss.Tick(.01f);
            Check(brain.Phase==2,"Run Anon enters the same second sword phase at half health");
            DefeatBoss(game,boss);
            Check(opponent.GetValue(player)==null && !(bool)rules.GetValue(player),"Run sword-duel rules end when Anon is defeated");
            game.RestartTrial();
        }
        [MenuItem("Tools/Tomorin/Verify endless run (Play Mode)")]
        public static void VerifyEndless(){Debug.Log(Endless());}
        public static string Endless()
        {
            passed.Clear();var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
            Check(game && game.Ready && game.IsEndless,"Endless scene initialized");game.RestartTrial();
            var player=game.Player;Check(!player.ChoosingBoon,"Endless starts without the one-time entrance boon");
            player.AwardRunExperience(500);Check(player.PendingRunChoices>1,"Experience queues every earned level");
            Check(!player.ChoosingRunAffix && Time.timeScale==1,"Earning levels does not interrupt combat");
            var movement=new TrialControls{move=Vector2.up,held=2};
            game.Input.ApplyNetwork(movement);
            for(int i=0;i<12;i++)player.Tick(1f/60,game.Input);
            Check(player.CaptureNetwork(0).movement>0 && player.Sprinting,"Moving player publishes a sprint gait before upgrading");
            Check(player.OpenRunUpgrade(),"Player manually opens pending upgrades");
            var upgradeState=player.CaptureNetwork(0);
            Check(upgradeState.runChoiceOpen && upgradeState.movement==0 && player.PlanarVelocity==Vector3.zero && !player.Sprinting,
                "Opening an upgrade while moving publishes an idle gait");
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
            CheckRunAnon(game);game.Pause();return Finish("endless");
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
                player.Teleport(new Vector3(20,0,5));boss.Teleport(new Vector3(0,0,38));Physics.SyncTransforms();
                Set(boss,typeof(TrialFighter),"Health",boss.MaxHealth*.8f);
                float bossHealth=boss.Health;
                float distance=Vector3.ProjectOnPlane(boss.transform.position-player.transform.position,Vector3.up).magnitude;
                for(int step=0;step<50;step++)boss.Tick(.1f);
                Check(boss.Awakened && boss.Health==bossHealth,"Distant boss stays awake without resetting health in round "+(round+1));
                Check(Vector3.ProjectOnPlane(boss.transform.position-player.transform.position,Vector3.up).magnitude<distance-.1f,"Boss pursues player across the arena in round "+(round+1));
                player.transform.rotation=Quaternion.Euler(0,117,0);
                var victoryPosition=player.transform.position;var victoryRotation=player.transform.rotation;
                Set(player,typeof(TrialFighter),"Health",30f);Set(player,typeof(TrialFighter),"Stamina",20f);
                DefeatBoss(game,boss);
                Check(player.Health==player.MaxHealth && player.Stamina==player.MaxStamina,"Victory restores health and stamina in round "+(round+1));
                Check(player.Flasks==2,"Victory preserves remaining flasks in round "+(round+1));
                Check(player.transform.position==victoryPosition && Quaternion.Angle(player.transform.rotation,victoryRotation)<.01f,"Victory preserves player position and facing in round "+(round+1));
                if(round<7)
                {
                    game.Run.Tick(.5f);
                    Check(game.Mode==TrialMode.Playing && Time.timeScale==1 && game.Run.Wave==round+1,"Intermission keeps play active and delays the next boss in round "+(round+1));
                    game.Input.ApplyNetwork(new TrialControls{move=Vector2.up,yaw=0});
                    for(int step=0;step<10;step++)player.Tick(.05f,game.Input);
                    game.Input.ApplyNetwork(new TrialControls());
                    Check(Vector3.ProjectOnPlane(player.transform.position-victoryPosition,Vector3.up).sqrMagnitude>.1f,"Player can move while waiting for the next boss in round "+(round+1));
                }
            }
            game.Run.Tick(.1f);Check(game.Mode==TrialMode.Victory && seen.Count==8,"All eight distinct bosses complete the run");
            game.RestartTrial();Check(player.Flasks==3 && game.Run.BossesDefeated==0,"Restart resets full run and restores initial three flasks");
            CheckRunAnon(game);game.Pause();return Finish("boss-rush");
        }
        static string Finish(string name)
        {string text=string.Join("\n",passed);System.IO.Directory.CreateDirectory("output");System.IO.File.WriteAllText("output/"+name+"-validation.txt",text);Debug.Log(name+" validation: "+passed.Count+" checks passed");return text;}
    }
}
