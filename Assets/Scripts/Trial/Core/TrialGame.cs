using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TomorinTrial
{
    public enum TrialMode { Title, Playing, Paused, Dead, Victory }

    // Read touch buttons after EventSystem.Update has dispatched this frame's UI input.
    // Keyboard/gamepad state is already available; touch should reach the same combat tick.
    [DefaultExecutionOrder(100)]
    public sealed partial class TrialGame : MonoBehaviour
    {
        [SerializeField] GameObject tomorinModel;
        [SerializeField] GameObject deepSeekModel;
        [SerializeField] GameObject naiWaModel;
        [SerializeField] TrialLevel initialLevel;
        [SerializeField] GameObject[] championModels;
        [SerializeField] GameObject championGun;
        public GameObject ChampionGun=>championGun;
        public bool IsChampionStage=>TrialChampionCatalog.IsChampion(Level);
        [SerializeField] Font uiFont;
        public Font UiFont => uiFont;
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] Shader surfaceShader;
        [SerializeField] Shader skyShader;
        [SerializeField] Shader particleShader;
        public GameObject NaiWaModel => naiWaModel;
        public Shader ParticleShader => particleShader;
        public TrialWorld World { get; private set; }
        public TrialPlayer Player { get; private set; }
        TrialPlayer combatTarget;
        public TrialPlayer CombatTarget => combatTarget?combatTarget:Player;
        public void SetCombatTarget(TrialPlayer target){combatTarget=target;}
        public TrialCamera CameraRig { get; private set; }
        public TrialEffects Effects { get; private set; }
        public TrialHud Hud { get; private set; }
        public TrialInput Input { get; private set; }
        public IReadOnlyList<TrialEnemy> Enemies => enemies;
        public TrialMode Mode { get; private set; }
        internal const float ResultSlowScale=.5f;
        internal const float ResultInkDelay=.45f;
        const float ResultSlowDuration=3f;
        internal const float RespawnDelay=5f;
        float resultStartedAt=float.NegativeInfinity;
        internal bool HasResult=>Mode==TrialMode.Dead || Mode==TrialMode.Victory;
        internal float ResultAge=>Time.unscaledTime-resultStartedAt;
        internal bool ResultInProgress=>HasResult && ResultAge<ResultSlowDuration;
        public bool GateOpen { get; private set; }
        public int DefeatedGuards { get; private set; }
        public int Deaths { get; private set; }
        public bool Ready { get; private set; }
        public float PlaySeconds { get; private set; }
        public TrialLevel Level { get; private set; }
        public TrialDifficulty Difficulty { get; private set; }
        public bool IsArena => Level==TrialLevel.Arena;
        public int PartySize { get; private set; } = 1;
        public float EnemyDamageScale => TrialDifficultySettings.DamageScale(Difficulty)*(1+Mathf.Min(.25f,.06f*(PartySize-1)));
        public Vector3 Checkpoint { get; private set; } = new Vector3(0,0,-21);
        readonly List<TrialEnemy> enemies=new List<TrialEnemy>();
        float restCooldown;
        bool previousBackground;
        bool networkDriven;

        void Awake(){Ready=false;enemies.Clear();}

        void Start()
        {
            if(!tomorinModel || !deepSeekModel || !naiWaModel){Debug.LogError("Trial scene requires Tomorin, DeepSeek and NaiWa FBX references.",this);enabled=false;return;}
            AudioListener.volume=PlayerPrefs.GetFloat("Ink.Volume",.7f);
            InkMainMenu.SetReducedMotion(PlayerPrefs.GetInt("Ink.ReducedMotion",0)==1);
            previousBackground=Application.runInBackground;Application.runInBackground=true;
            Level=initialLevel;
            Difficulty=TrialNetworkManager.SessionActive && TrialNetworkManager.Current?TrialNetworkManager.Current.SelectedDifficulty:TrialDifficultySettings.Solo;
            if(IsChampionStage || IsRunStage)Checkpoint=Level==TrialLevel.Anon?TrialWorld.TerraceCheckpoint:new Vector3(0,0,8);
            World=new TrialWorld();World.Build(transform,surfaceShader,skyShader,Level);
            Input=gameObject.AddComponent<TrialInput>();Input.Initialize(inputActions);
            Player=new GameObject("Tomorin").AddComponent<TrialPlayer>();Player.transform.SetParent(transform,false);Player.transform.position=Checkpoint;Player.Initialize(this,tomorinModel,TrialAppearance.Saved);
            var camera=Camera.main;if(!camera){var obj=new GameObject("Main Camera");obj.tag="MainCamera";camera=obj.AddComponent<Camera>();}
            if(!camera.GetComponent<AudioListener>())camera.gameObject.AddComponent<AudioListener>();
            CameraRig=camera.gameObject.AddComponent<TrialCamera>();CameraRig.Initialize(Player);
            Effects=gameObject.AddComponent<TrialEffects>();Effects.Initialize(this);
            if(IsRunStage){Run=new TrialEndlessRun(this);GateOpen=true;World.Gate.SetActive(false);World.ArenaEntrance.SetActive(true);Player.Restore(Checkpoint);}
            else if(IsChampionStage)
            {
                if(Level==TrialLevel.Twins){SpawnChampion(ChampionKind.FeiBi,new Vector3(-4,0,30));SpawnChampion(ChampionKind.NuoNuo,new Vector3(4,0,30));}
                else SpawnChampion(Level==TrialLevel.Remnant?ChampionKind.Remnant:Level==TrialLevel.Anon?ChampionKind.Anon:Level==TrialLevel.Taffy?ChampionKind.Taffy:Level==TrialLevel.Penguin?ChampionKind.Penguin:ChampionKind.YaYa,Level==TrialLevel.Anon?TrialWorld.TerraceBossSpawn:new Vector3(0,0,32));
                GateOpen=true;
            }
            else {Spawn(new Vector3(-3.7f,0,-5),false);Spawn(new Vector3(4.4f,0,3.0f),false);Spawn(new Vector3(-2.5f,0,11),false);Spawn(new Vector3(0,0,27),true);}
            SetPartySize(PartySize);
            Hud=gameObject.AddComponent<TrialHud>();Hud.Initialize(this);
            Mode=TrialMode.Title;SetCursor(false);Ready=true;
            StartCoroutine(ReleaseUnusedLevelAssets());
            if(InkMainMenu.AutoStartTrial){InkMainMenu.AutoStartTrial=false;StartTrial();}
        }

        System.Collections.IEnumerator ReleaseUnusedLevelAssets()
        {
            // Run encounters spawn later waves from these authored references.
            if(IsRunStage)yield break;
            // Runtime actors retain their own meshes/materials. Companions still need Tomorin.
            if(championModels!=null)System.Array.Clear(championModels,0,championModels.Length);
            deepSeekModel=null;naiWaModel=null;championGun=null;
            yield return null;
            yield return Resources.UnloadUnusedAssets();
        }
        void Spawn(Vector3 point,bool boss)
        {var enemy=new GameObject().AddComponent<TrialEnemy>();enemy.transform.SetParent(transform,false);enemy.Initialize(this,point,boss,deepSeekModel);enemies.Add(enemy);}
        void SpawnChampion(ChampionKind kind,Vector3 point)
        {var enemy=new GameObject().AddComponent<TrialEnemy>();enemy.transform.SetParent(transform,false);enemy.InitializeChampion(this,point,kind,kind==ChampionKind.Remnant?Resources.Load<GameObject>("Trial/Models/Remnant_Champion"):championModels[(int)kind]);enemies.Add(enemy);}

        public TrialPlayer CreateCompanion(int id,TrialOutfit outfit=TrialOutfit.Default)
        {
            var actor=new GameObject("同伴 "+(id+1)).AddComponent<TrialPlayer>();actor.transform.SetParent(transform,false);
            actor.transform.position=CoopSpawn(id);actor.Initialize(this,tomorinModel,outfit);
            if(!IsArena && Mirror.NetworkServer.active)actor.RollRunBoons();return actor;
        }
        public void AdvanceNetworkTime(float dt){PlaySeconds+=dt;}
        public void SetPartySize(int count)
        {
            PartySize=Mathf.Max(1,count);
            if(!IsArena)foreach(var enemy in enemies)enemy.ScaleForParty(PartySize);
        }
        public void ConfigureSession(TrialLevel level)
        {
            Level=level;RestartTrial();
            if(IsArena)
            {
                foreach(var enemy in enemies)enemy.gameObject.SetActive(false);
                GateOpen=true;World.Gate.SetActive(false);World.ArenaEntrance.SetActive(true);
                Checkpoint=new Vector3(0,0,25);Player.Restore(ArenaSpawn(0));Player.GrantSpawnProtection();
                Hud.Notify("竞技场 · 自由决斗 · 击杀计分 · 3 秒重生",4);
            }
            else
            {
                foreach(var enemy in enemies)enemy.gameObject.SetActive(true);
                Hud.Notify(IsRunStage?TrialChampionCatalog.LevelName(Level):IsChampionStage?TrialChampionCatalog.Lesson(Level):"庭院 · 先击败守卫，再挑战大肥鱼",5);
            }
            CameraRig.Snap();
        }
        public void SetDifficulty(TrialDifficulty difficulty)
        {
            Difficulty=difficulty<=TrialDifficulty.Hard?difficulty:TrialDifficulty.Normal;
            SetPartySize(PartySize);
        }
        public Vector3 ArenaSpawn(int slot)
        {
            float angle=slot*2.399963f;
            return new Vector3(Mathf.Sin(angle)*6.8f,0,25+Mathf.Cos(angle)*6.8f);
        }
        public Vector3 CoopSpawn(int id)=>World.ArenaEntrance.activeSelf?ArenaSpawn(id):Checkpoint+new Vector3(Mathf.Sin(id*2.4f)*2.8f,0,Mathf.Cos(id*2.4f)*1.8f);
        public IEnumerable<TrialFighter> Opponents(TrialPlayer attacker)
        {
            if(IsArena)
            {
                if(TrialNetworkManager.Current)foreach(var actor in TrialNetworkManager.Current.Players)
                    if(actor && actor!=attacker)yield return actor;
            }
            else foreach(var enemy in enemies)yield return enemy;
        }
        public TrialFighter NearestOpponent(TrialPlayer attacker,float range,float? bossRange=null)
        {
            TrialFighter found=null;float closest=float.PositiveInfinity;
            foreach(var target in Opponents(attacker))
            {
                if(!target || !target.isActiveAndEnabled || !target.Alive)continue;
                bool boss=target is TrialEnemy enemy && enemy.IsBoss;
                if(boss && !GateOpen)continue;
                float limit=boss?bossRange??range:range;
                float d=(target.transform.position-attacker.transform.position).sqrMagnitude;
                if(d<closest && d<limit*limit){closest=d;found=target;}
            }
            return found;
        }
        int nextRunNetworkId;

        internal int RunEnemyId(TrialEnemy enemy)
        {
            if (enemy.RunNetworkId == 0) enemy.RunNetworkId = ++nextRunNetworkId;
            return enemy.RunNetworkId;
        }

        internal int SpellTargetId(TrialFighter fighter)
        {
            if(fighter is TrialPlayer rival)return TrialNetworkManager.Current.IdOf(rival);
            return IsRunStage && fighter is TrialEnemy enemy ? RunEnemyId(enemy) : enemies.IndexOf(fighter as TrialEnemy);
        }
        internal TrialFighter SpellTarget(int id)
        {
            if(IsArena)return TrialNetworkManager.Current?TrialNetworkManager.Current.PlayerById(id):null;
            if(!IsRunStage)return id>=0 && id<enemies.Count?enemies[id]:null;
            if(Mirror.NetworkClient.active && !Mirror.NetworkServer.active)
                return runReplicas.TryGetValue(id,out var replica)?replica:null;
            foreach(var enemy in enemies)if(enemy && enemy.RunNetworkId==id)return enemy;
            return null;
        }
        public void SealArena()
        {
            if(World.ArenaEntrance.activeSelf)return;
            if(IsChampionStage || IsRunStage){World.ArenaEntrance.SetActive(true);return;}
            if(TrialNetworkManager.SessionActive)
            {
                int index=0;
                foreach(var actor in TrialNetworkManager.Current.Players)
                    if(actor && actor.Alive && Vector3.ProjectOnPlane(actor.transform.position-new Vector3(0,0,25),Vector3.up).magnitude>8.7f)
                        actor.Teleport(ArenaSpawn(index++));
            }
            World.ArenaEntrance.SetActive(true);Physics.SyncTransforms();
        }
        public void ApplyNetworkWorld(TrialWorldState state)
        {
            PartySize=Mathf.Max(1,state.partySize);
            GateOpen=state.gate;DefeatedGuards=state.guards;PlaySeconds=state.seconds;World.Gate.SetActive(!GateOpen);World.ArenaEntrance.SetActive(state.arena);
            if(state.victory && Mode!=TrialMode.Victory)EnterVictory();
            else if(state.defeat && Mode!=TrialMode.Dead)EnterDefeat();
            else if(!state.victory && !state.defeat && (Mode==TrialMode.Victory || Mode==TrialMode.Dead))StartTrial();
        }

        void Update()
        {
            if(!Ready)return;
            Input.Read();Effects.TickTime();
            if(HasResult)TickResult();
            if(TrialNetworkManager.SessionActive){networkDriven=true;TrialNetworkManager.Current.TickGame(this);return;}
            // StopClient clears network flags before the menu scene finishes loading.
            // Replicas must not run the standalone simulation during that gap.
            if(networkDriven)return;
            if(Mode==TrialMode.Title)
            {if(Input.Confirm)StartTrial();return;}
            if(Mode==TrialMode.Paused)
            {if(Input.Pause)HandlePauseInput();return;}
            // Result screens handle confirmation through their selected UI button.
            if(HasResult)return;
            if(Input.Pause){Pause();return;}
            if(Input.Upgrade && Player.OpenRunUpgrade())return;
            if(Player.ChoosingBoon){SetCursor(false);return;}
            float dt=Time.deltaTime;PlaySeconds+=dt;restCooldown-=dt;
            Player.Tick(dt,Input);
            if(!Player.Alive || Player.ChoosingBoon)return;
            foreach(var enemy in enemies){enemy.Tick(dt);if(Player.ChoosingBoon || Mode!=TrialMode.Playing)break;}
            if(Run!=null && !Player.ChoosingBoon && Mode==TrialMode.Playing)Run.Tick(dt);
            CameraRig.Look(Input.Look);
            if(!IsRunStage && Input.Interact && Vector3.Distance(Player.transform.position,World.ShrinePosition)<2.6f && restCooldown<=0)
            {
                bool danger=false;foreach(var enemy in enemies)if(enemy.Alive && enemy.Awakened && Vector3.Distance(enemy.transform.position,Player.transform.position)<9)danger=true;
                if(danger || Level==TrialLevel.Anon && World.ArenaEntrance.activeSelf){Hud.Notify("战斗中无法休息",1.4f);return;}
                Checkpoint=World.ShrinePosition+new Vector3(1.5f,0,0);
                Player.Restore(Checkpoint);ResetEnemies();restCooldown=2;
                Effects.Burst(World.ShrinePosition+Vector3.up,new Color(1,.65f,.20f),50);Effects.Sound("heal",World.ShrinePosition);
                Hud.Notify("已恢复",1.2f);
            }
        }

        public void StartTrial()
        {
            Mode=TrialMode.Playing;CameraRig.Frozen=false;Input.ResetSprint();Time.timeScale=1;
            Hud.ResetDamageFeedback();
            if(!IsArena && (!TrialNetworkManager.SessionActive || Mirror.NetworkServer.active))
            {Player.RollRunBoons();var fish=enemies.Count>0?enemies[enemies.Count-1].Boss:null;if(fish && fish.Boon==TrialBossBoon.None)fish.RollBoon();}
            SetCursor(!Player.ChoosingBoon);
        }
        public void FinishBoonChoice(){Input.ResetSprint();Input.SuppressCombat();SetCursor(Mode==TrialMode.Playing);Hud.Notify("获得增益 · "+TrialRunBoons.Name(Player.Boon),2);}
        public void SelectRunBoon(TrialBoon boon)
        {if(TrialNetworkManager.SessionActive)TrialNetworkManager.Current.ChooseBoon(boon);else Player.ChooseBoon(boon);}
        public void StartBossChallenge()
        {
            RestartTrial();
            if(IsRunStage)return;
            if(IsChampionStage){Hud.Notify(TrialChampionCatalog.Lesson(Level),5);return;}
            for(int i=0;i<3;i++)enemies[i].ReceiveHit(null,10000,100,true);
            Player.Restore(new Vector3(0,0,19));CameraRig.Snap();
            Hud.Notify("",0);
        }
        public void HandlePauseInput(){if(Hud.SettingsOpen)Hud.CloseSettings();else if(Mode==TrialMode.Paused)Resume();else Pause();}
        public void Pause(){Input.ResetSprint();Mode=TrialMode.Paused;if(!TrialNetworkManager.SessionActive)Time.timeScale=0;SetCursor(false);}
        public void Resume(){Hud.CloseSettings();Input.ResetSprint();Time.timeScale=1;Mode=TrialMode.Playing;SetCursor(true);}
        public void ReturnToTitle()
        {
            Time.timeScale=1;
            if(TrialNetworkManager.SessionActive)TrialNetworkManager.Current.Leave();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(TrialNetworkManager.MenuScene);
        }
        public void QuitGame()
        {
            if(TrialNetworkManager.SessionActive)TrialNetworkManager.Current.Leave();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        public void OnPlayerDied()
        {
            Deaths++;if(TrialNetworkManager.SessionActive){Hud.Notify(IsArena?"3 秒后重生":"5 秒后重生",3);return;}
            foreach(var enemy in enemies)if(enemy.Champion)enemy.Champion.CancelAttack(true);
            EnterDefeat();
        }
        public void OnEnemyDied(TrialEnemy enemy,TrialPlayer killer=null,bool awardSpellEnergy=true)
        {
            if(killer && awardSpellEnergy)killer.AwardSpellEnergyForKill();
            if(IsRunStage){Run.EnemyDied(enemy,killer);(killer?killer:Player).OnRunKill(enemy);return;}
            if(enemy.IsBoss)
            {
                if(IsChampionStage){foreach(var other in enemies)if(other!=enemy && other.Alive){Hud.Notify(enemy.DisplayName+" 倒下 · 另一位进入决意阶段",3);return;}foreach(var other in enemies)other.Champion.CancelAttack(true);}
                World.ArenaEntrance.SetActive(false);EnterVictory();Effects.Sound("heal",Player.transform.position,.7f);return;
            }
            DefeatedGuards++;
            if(DefeatedGuards>=3)
            {GateOpen=true;World.Gate.SetActive(false);Hud.Notify("庭门已开",1.8f);Effects.Sound("heal",Player.transform.position,.8f);}
        }
        void EnterVictory()
        {
            EnterResult(TrialMode.Victory);
            if(Level!=TrialLevel.Anon || enemies.Count==0)return;
            var anon=enemies[0];
            Vector3 contact=anon.transform.position+Vector3.up*.95f;
            Effects.SlashEffect(contact,anon.transform.forward,new Color(.91f,.95f,1),1.25f,width:.12f,duration:.42f);
            Effects.Burst(contact,new Color(1,.74f,.58f),38);
            if((Player.transform.position-anon.transform.position).sqrMagnitude<25)
                CameraRig.CombatFrame(anon,.8f,.85f,true);
        }
        public void EnterDefeat()
        {
            EnterResult(TrialMode.Dead);
            Hud.Notify("",0);
        }
        void EnterResult(TrialMode mode)
        {
            if(Mode==mode)return;
            Mode=mode;
            resultStartedAt=Time.unscaledTime;
            foreach(var enemy in enemies)enemy.StopLocomotion();
            Input.ResetSprint();
            CameraRig.Frozen=false;
            Time.timeScale=ResultSlowScale;
            SetCursor(false);
        }
        void TickResult()
        {
            CameraRig.Frozen=!ResultInProgress;
            if(CameraRig.Frozen || (networkDriven && !Mirror.NetworkServer.active))return;

            // Advance presentation while combat, input and the run timer are stopped.
            float dt=Time.deltaTime;
            foreach(var enemy in enemies)if(!enemy.Alive)enemy.Tick(dt);
            if(!TrialNetworkManager.SessionActive)Player.AdvanceResultPose(dt);
        }
        public void RetryChallenge()
        {
            if(TrialNetworkManager.SessionActive)TrialNetworkManager.Current.RetryChallenge();
            else if(Mode!=TrialMode.Dead)RestartTrial();
            else if(!networkDriven && !ResultInProgress)Respawn();
        }
        public void Respawn(){if(IsRunStage){RestartTrial();return;}Player.Restore(Checkpoint);if(Level==TrialLevel.Anon)Player.transform.rotation=Quaternion.identity;ResetEnemies();CameraRig.Snap(Level==TrialLevel.Anon);StartTrial();Hud.Notify("",0);}
        public void RestartTrial(){Player.ResetSpellProgress();if(IsRunStage){Effects.ClearSpellVisuals();RestartRun();return;}Checkpoint=Level==TrialLevel.Anon?TrialWorld.TerraceCheckpoint:IsChampionStage?new Vector3(0,0,8):new Vector3(0,0,-21);Deaths=0;PlaySeconds=0;Player.ResetRunBoon();var fish=enemies.Count>0?enemies[enemies.Count-1].Boss:null;if(fish)fish.ClearBoon();Respawn();}
        void ResetEnemies(){Effects.ClearSpellVisuals();World.ArenaEntrance.SetActive(false);foreach(var enemy in enemies)enemy.ResetFighter();DefeatedGuards=0;GateOpen=IsChampionStage;World.Gate.SetActive(!IsChampionStage);}
        public TrialEnemy NearestEnemy(Vector3 point,float range)
        {
            TrialEnemy found=null;float closest=range*range;
            foreach(var enemy in enemies)
            {
                if(!enemy.Alive || (enemy.IsBoss && !GateOpen))continue;
                float d=(enemy.transform.position-point).sqrMagnitude;
                if(d<closest){closest=d;found=enemy;}
            }
            return found;
        }
        void SetCursor(bool captured){Cursor.lockState=captured?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!captured;}
        void OnDestroy()
        {Ready=false;if(Run!=null)Run.Dispose();if(World!=null)World.Dispose();Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;Application.runInBackground=previousBackground;}
    }
}
