using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TomorinTrial
{
    public enum TrialMode { Title, Playing, Paused, Dead, Victory }

    public sealed class TrialGame : MonoBehaviour
    {
        [SerializeField] GameObject tomorinModel;
        [SerializeField] GameObject deepSeekModel;
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] Shader surfaceShader;
        [SerializeField] Shader skyShader;
        [SerializeField] Shader particleShader;
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
        public bool GateOpen { get; private set; }
        public int DefeatedGuards { get; private set; }
        public int Deaths { get; private set; }
        public bool Ready { get; private set; }
        public float PlaySeconds { get; private set; }
        public Vector3 Checkpoint { get; private set; } = new Vector3(0,0,-21);
        readonly List<TrialEnemy> enemies=new List<TrialEnemy>();
        float deathAt,restCooldown;
        bool checkpointLit;
        bool previousBackground;

        void Awake(){Ready=false;enemies.Clear();}

        void Start()
        {
            if(!tomorinModel || !deepSeekModel){Debug.LogError("Trial scene requires Tomorin and DeepSeek FBX references.",this);enabled=false;return;}
            previousBackground=Application.runInBackground;Application.runInBackground=true;
            World=new TrialWorld();World.Build(transform,surfaceShader,skyShader);
            Input=gameObject.AddComponent<TrialInput>();Input.Initialize(inputActions);
            Player=new GameObject("Tomorin").AddComponent<TrialPlayer>();Player.transform.SetParent(transform,false);Player.transform.position=Checkpoint;Player.Initialize(this,tomorinModel);
            var camera=Camera.main;if(!camera){var obj=new GameObject("Main Camera");obj.tag="MainCamera";camera=obj.AddComponent<Camera>();}
            if(!camera.GetComponent<AudioListener>())camera.gameObject.AddComponent<AudioListener>();
            CameraRig=camera.gameObject.AddComponent<TrialCamera>();CameraRig.Initialize(Player);
            Effects=gameObject.AddComponent<TrialEffects>();Effects.Initialize(this);
            Spawn(new Vector3(-3.7f,0,-5),false);Spawn(new Vector3(4.4f,0,3.0f),false);Spawn(new Vector3(-2.5f,0,11),false);Spawn(new Vector3(0,0,27),true);
            Hud=gameObject.AddComponent<TrialHud>();Hud.Initialize(this);
            Mode=TrialMode.Title;SetCursor(false);Ready=true;
            if(InkMainMenu.AutoStartTrial){InkMainMenu.AutoStartTrial=false;StartTrial();}
        }

        void Spawn(Vector3 point,bool boss)
        {var enemy=new GameObject().AddComponent<TrialEnemy>();enemy.transform.SetParent(transform,false);enemy.Initialize(this,point,boss,deepSeekModel);enemies.Add(enemy);}

        public TrialPlayer CreateCompanion(int id)
        {
            var actor=new GameObject("同伴 "+(id+1)).AddComponent<TrialPlayer>();actor.transform.SetParent(transform,false);
            actor.transform.position=Checkpoint+Vector3.right*(1.8f*(id%4));actor.Initialize(this,tomorinModel);return actor;
        }
        public void AdvanceNetworkTime(float dt){PlaySeconds+=dt;}
        public void ApplyNetworkWorld(TrialWorldState state)
        {
            GateOpen=state.gate;DefeatedGuards=state.guards;PlaySeconds=state.seconds;World.Gate.SetActive(!GateOpen);
            if(state.victory && Mode!=TrialMode.Victory){Mode=TrialMode.Victory;SetCursor(false);}
        }

        void Update()
        {
            if(!Ready)return;
            Input.Read();Effects.TickTime();
            if(TrialNetworkManager.SessionActive){TrialNetworkManager.Current.TickGame(this);return;}
            if(Mode==TrialMode.Title)
            {if(Input.Confirm)StartTrial();return;}
            if(Mode==TrialMode.Paused)
            {if(Input.Pause)Resume();return;}
            if(Mode==TrialMode.Dead || Mode==TrialMode.Victory)
            {
                foreach(var enemy in enemies)if(!enemy.Alive)enemy.Tick(Time.unscaledDeltaTime);
                if(Mode==TrialMode.Dead)
                {
                    // Continue the fall animation while the gameplay loop is stopped.
                    if(Input.Restart && Time.unscaledTime-deathAt>1.3f)Respawn();
                }
                else if(Input.Restart)RestartTrial();
                return;
            }
            if(Input.Pause){Pause();return;}
            float dt=Time.deltaTime;PlaySeconds+=dt;restCooldown-=dt;
            Player.Tick(dt,Input);
            if(!Player.Alive)return;
            foreach(var enemy in enemies)enemy.Tick(dt);
            CameraRig.Look(Input.Look);
            if(Input.Interact && Vector3.Distance(Player.transform.position,World.ShrinePosition)<2.6f && restCooldown<=0)
            {
                bool danger=false;foreach(var enemy in enemies)if(enemy.Alive && enemy.Awakened && Vector3.Distance(enemy.transform.position,Player.transform.position)<9)danger=true;
                if(danger){Hud.Notify("战斗中无法休息",1.4f);return;}
                checkpointLit=true;Checkpoint=World.ShrinePosition+new Vector3(1.5f,0,0);
                Player.Restore(Checkpoint);ResetEnemies();restCooldown=2;
                Effects.Burst(World.ShrinePosition+Vector3.up,new Color(1,.65f,.20f),50);Effects.Sound("heal",World.ShrinePosition);
                Hud.Notify("已恢复",1.2f);
            }
        }

        public void StartTrial(){Mode=TrialMode.Playing;SetCursor(true);}
        public void StartBossChallenge()
        {
            RestartTrial();
            for(int i=0;i<3;i++)enemies[i].ReceiveHit(Player,10000,100,true);
            Player.Restore(new Vector3(0,0,19));CameraRig.Snap();
            Hud.Notify("",0);
        }
        public void Pause(){Mode=TrialMode.Paused;if(!TrialNetworkManager.SessionActive)Time.timeScale=0;SetCursor(false);}
        public void Resume(){Mode=TrialMode.Playing;SetCursor(true);}
        public void ReturnToTitle()
        {
            if(TrialNetworkManager.SessionActive)TrialNetworkManager.Current.Leave();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(TrialNetworkManager.MenuScene);
        }
        public void OnPlayerDied()
        {
            Deaths++;if(TrialNetworkManager.SessionActive){Hud.Notify("5 秒后重生",3);return;}
            Mode=TrialMode.Dead;deathAt=Time.unscaledTime;SetCursor(false);
        }
        public void OnEnemyDied(TrialEnemy enemy)
        {
            if(enemy.IsBoss){Mode=TrialMode.Victory;Effects.Sound("heal",Player.transform.position,.7f);SetCursor(false);return;}
            DefeatedGuards++;
            if(DefeatedGuards>=3)
            {GateOpen=true;World.Gate.SetActive(false);Hud.Notify("庭门已开",1.8f);Effects.Sound("heal",Player.transform.position,.8f);}
        }
        public void Respawn(){Player.Restore(Checkpoint);ResetEnemies();CameraRig.Snap();Mode=TrialMode.Playing;SetCursor(true);Hud.Notify("",0);}
        public void RestartTrial(){Checkpoint=new Vector3(0,0,-21);checkpointLit=false;Deaths=0;PlaySeconds=0;Respawn();}
        void ResetEnemies(){foreach(var enemy in enemies)enemy.ResetFighter();DefeatedGuards=0;GateOpen=false;World.Gate.SetActive(true);}
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
        {Ready=false;if(World!=null)World.Dispose();Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;Application.runInBackground=previousBackground;}
    }
}
