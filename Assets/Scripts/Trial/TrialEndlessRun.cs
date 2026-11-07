using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    // Owns encounter progression. Death callbacks only enqueue transitions; spawning
    // and recycling happen after the combat iteration has finished.
    public sealed class TrialEndlessRun
    {
        readonly TrialGame game;
        readonly List<TrialEnemy> guardPool=new List<TrialEnemy>();
        readonly Dictionary<TrialEnemy,float> corpses=new Dictionary<TrialEnemy,float>();
        readonly List<TrialEnemy> expired=new List<TrialEnemy>();
        int remaining,spawned;
        float spawnClock,intermission;
        public int Wave { get; private set; }
        public int Kills { get; private set; }
        public int BossesDefeated { get; private set; }
        public const int BossCount=8;
        public bool Complete=>game.IsBossRush && BossesDefeated>=BossCount;
        string status;
        int statusWave=-1,statusKills=-1,statusBosses=-1;
        public string Status
        {
            get
            {
                if(statusWave!=Wave || statusKills!=Kills || statusBosses!=BossesDefeated)
                {statusWave=Wave;statusKills=Kills;statusBosses=BossesDefeated;status=game.IsBossRush?$"Boss 连战  {Mathf.Min(BossCount,BossesDefeated+1)} / {BossCount} · 药瓶仅有初始 3 瓶":$"无尽雾潮 · 第 {Wave} 波{(Wave>0 && Wave%5==0?" · 精英来袭":"")}  击杀 {Kills}";}
                return status;
            }
        }
        public TrialEndlessRun(TrialGame owner){game=owner;intermission=1;}
        public void ApplyNetwork(int wave,int kills,int bosses){Wave=wave;Kills=kills;BossesDefeated=bosses;}
        public void Reset()
        {
            game.ClearRunEnemies();guardPool.Clear();corpses.Clear();expired.Clear();
            Wave=Kills=BossesDefeated=remaining=spawned=0;intermission=1;spawnClock=0;
        }
        public void EnemyDied(TrialEnemy enemy,TrialPlayer killer=null)
        {
            if(corpses.ContainsKey(enemy))return;
            corpses.Add(enemy,2.2f);Kills++;
            if(game.IsEndless)
            {
                int experience=enemy.IsBoss?150+Wave*12:24+Wave*2;
                (killer?killer:game.Player).AwardRunExperience(experience);
            }
            else if(enemy.IsBoss)
            {
                BossesDefeated++;
                if(TrialNetworkManager.SessionActive)TrialNetworkManager.Current.RestorePartyForBossRound();
                else game.Player.RestoreForBossRound(game.Checkpoint);
                game.Hud.Notify(Complete?"八位 Boss 全部击破":"此战告捷 · 气血与气力已回满",3);
            }
        }
        public void Tick(float dt)
        {
            expired.Clear();
            foreach(var pair in corpses){if(pair.Value-dt<=0)expired.Add(pair.Key);}
            // Avoid changing dictionary values during enumeration on older Mono runtimes.
            var keys=corpseKeys;keys.Clear();foreach(var pair in corpses)keys.Add(pair.Key);
            foreach(var enemy in keys)corpses[enemy]-=dt;
            foreach(var enemy in expired)
            {
                corpses.Remove(enemy);game.RemoveRunEnemy(enemy);
                if(enemy.IsBoss)Object.Destroy(enemy.gameObject);else enemy.gameObject.SetActive(false);
            }
            if(Complete){game.CompleteRun();return;}
            if(remaining>0)
            {
                spawnClock-=dt;
                if(spawnClock<=0 && AliveCount()<12)
                {SpawnGuard();remaining--;spawned++;spawnClock=Mathf.Max(.35f,1.25f-Wave*.025f);}
                return;
            }
            if(AliveCount()>0)return;
            intermission-=dt;if(intermission>0)return;
            Wave++;spawned=0;intermission=3;
            game.Hud.Notify(game.IsBossRush?$"下一战 · {BossesDefeated+1} / {BossCount}":$"第 {Wave} 波{(Wave%5==0?" · 精英 Boss":" · 雾潮涌来")}",2.5f);
            if(game.IsBossRush)game.SpawnRunBoss(BossesDefeated,1);
            else
            {
                remaining=Mathf.Min(40,4+Wave*2);spawnClock=0;
                if(Wave%5==0)game.SpawnRunBoss(((Wave/5)-1)%BossCount,.45f+Wave*.035f);
            }
        }
        readonly List<TrialEnemy> corpseKeys=new List<TrialEnemy>();
        int AliveCount(){int count=0;foreach(var e in game.Enemies)if(e && e.Alive)count++;return count;}
        void SpawnGuard()
        {
            TrialEnemy enemy=null;
            foreach(var candidate in guardPool)if(!candidate.gameObject.activeSelf){enemy=candidate;break;}
            float angle=spawned*2.399963f+Wave*.71f;
            var point=new Vector3(Mathf.Sin(angle)*17,0,25+Mathf.Cos(angle)*17);
            if(!enemy){enemy=game.CreateRunGuard(point);guardPool.Add(enemy);}
            else {enemy.gameObject.SetActive(true);enemy.ResetFighter();enemy.Teleport(point);game.AddRunEnemy(enemy);}
            enemy.ConfigureRunStrength(1+Wave*.085f,1+Mathf.Min(2,Wave*.045f));
            enemy.WakeForRun();
        }
        public void Dispose(){foreach(var e in guardPool)if(e)Object.Destroy(e.gameObject);guardPool.Clear();}
    }
    public sealed partial class TrialGame
    {
        public bool IsEndless=>Level==TrialLevel.Endless;
        public bool IsBossRush=>Level==TrialLevel.BossRush;
        public bool IsRunStage=>IsEndless || IsBossRush;
        public TrialEndlessRun Run { get; private set; }
        readonly Dictionary<int,TrialEnemy> runReplicas=new Dictionary<int,TrialEnemy>();
        public void SyncRunReplicas(TrialActorState[] states)
        {
            var stale=new List<int>();
            foreach(var pair in runReplicas)
            {bool present=false;foreach(var state in states)if(state.id==pair.Key){present=true;break;}if(!present){Destroy(pair.Value.gameObject);stale.Add(pair.Key);}}
            foreach(int id in stale)runReplicas.Remove(id);
            enemies.Clear();
            foreach(var state in states)
            {
                if(!runReplicas.TryGetValue(state.id,out var enemy) || !enemy)
                {
                    if(state.enemyKind>=2)SpawnChampion((ChampionKind)(state.enemyKind-2),state.position);else Spawn(state.position,state.enemyKind==1);
                    enemy=enemies[enemies.Count-1];enemies.RemoveAt(enemies.Count-1);runReplicas[state.id]=enemy;
                }
                enemies.Add(enemy);
            }
        }
        public void ClearRunEnemies()
        {foreach(var e in enemies)if(e){e.gameObject.SetActive(false);Destroy(e.gameObject);}enemies.Clear();runReplicas.Clear();}
        public void RemoveRunEnemy(TrialEnemy enemy){enemies.Remove(enemy);}
        public void AddRunEnemy(TrialEnemy enemy){enemies.Add(enemy);}
        public TrialEnemy CreateRunGuard(Vector3 point)
        {Spawn(point,false);return enemies[enemies.Count-1];}
        public void SpawnRunBoss(int index,float healthScale)
        {
            if(index==0)Spawn(new Vector3(0,0,30),true);
            else SpawnChampion((ChampionKind)(index-1),new Vector3(0,0,32));
            var enemy=enemies[enemies.Count-1];enemy.ScaleForParty(PartySize);
            enemy.ConfigureRunStrength(healthScale,IsEndless?1+Run.Wave*.035f:1);enemy.WakeForRun();
        }
        void RestartRun()
        {Time.timeScale=1;Run.Dispose();Run.Reset();Deaths=0;PlaySeconds=0;Player.ResetRunBoon();Player.ResetRoguelike();Player.Restore(Checkpoint);GateOpen=true;World.Gate.SetActive(false);World.ArenaEntrance.SetActive(true);CameraRig.Snap();StartTrial();}
        public void CompleteRun(){World.ArenaEntrance.SetActive(false);EnterVictory();}
    }
}
