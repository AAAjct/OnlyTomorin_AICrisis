using UnityEngine;

namespace TomorinTrial
{
    public sealed class TrialEnemy : TrialFighter
    {
        public bool IsBoss { get; private set; }
        public float BodyRadius => IsBoss?.75f:.38f;
        public bool Awakened { get; private set; }
        public bool PhaseTwo => IsBoss && Health < MaxHealth * .55f;
        public string DisplayName => Champion?TrialChampionCatalog.Name(Champion.Kind):IsBoss?"大肥鱼 · DeepSeek":"奶蛙 · 拳卫";
        bool unblockable;
        public bool Unblockable { get => Champion?Champion.Unblockable:Boss?Boss.Unblockable:unblockable; private set=>unblockable=value; }
        public bool WindingUp => Champion?Champion.WindingUp:Boss?Boss.WindingUp:(Action==FighterAction.Light || Action==FighterAction.Heavy) && Progress<.33f;
        public TrialBoss Boss { get; private set; }
        public TrialChampion Champion {get;private set;}
        public bool Vulnerable => Action==FighterAction.Stagger;
        public Vector3 Spawn { get; private set; }
        float cooldown,orbitAge;
        bool attackLanded,phaseAnnounced;
        int attackIndex,chain;
        TrialNaiWaRig frogRig;
        public Vector3 Recoil {get;private set;}
        public float FlinchRemaining {get;private set;}
        public bool Flinching=>FlinchRemaining>0;
        float flinchCooldown;
        void HitFlinch(TrialFighter attacker)
        {
            if(!IsBoss || !Alive || Vulnerable || ResolveActive || flinchCooldown>0)return;
            FlinchRemaining=attacker && attacker.Action==FighterAction.Heavy?.22f:.13f;
            flinchCooldown=.38f;MoveBlend=0;
        }
        Vector3 recoilVelocity;
        float pressureClock,lastPressureHit=-10,pressureStarted,resolveTime,resolveCooldown;
        int pressureHits;
        bool resolvePending;
        bool resolveGlow;
        public bool ResolveActive=>IsBoss && resolveTime>0;
        public bool BreakoutInvulnerable=>ResolveActive && resolveTime>1.8f;
        void LateUpdate()
        {
            if(!ResolveActive && !resolveGlow)return;
            resolveGlow=ResolveActive;
            SetHitEmission(ResolveActive?new Color(.15f,.65f,1.8f):Color.black);
        }

        void TickResolve(float dt)
        {
            pressureClock+=dt;resolveTime=Mathf.Max(0,resolveTime-dt);resolveCooldown=Mathf.Max(0,resolveCooldown-dt);
            if(!Alive || !IsBoss)return;
            // A posture break always keeps its full execution window, even when escape is queued.
            if(resolvePending && (!Vulnerable || ActionTime>=ActionDuration))
            {
                resolvePending=false;pressureHits=0;resolveTime=2.5f;resolveCooldown=7;
                ClearCombatHold();Posture=0;Begin(FighterAction.Free,0);
                if(Champion)Champion.BreakPressure();
                else if(Boss){Boss.CancelAttack();Boss.StartSkill(DeepFishSkill.ReverseTail);}
                game.Effects.Burst(transform.position+Vector3.up,new Color(.3f,.85f,1),40);
                if(game.Player.LockTarget==this)game.Hud.Notify("破围反击 · 蓝光护身，准备闪避或弹反",1.5f);
            }
        }
        void RecordPressure(TrialFighter attacker)
        {
            if(!IsBoss || !(attacker is TrialPlayer) || resolveCooldown>0 || !Alive)return;
            if(pressureClock-lastPressureHit>1.1f){pressureHits=0;pressureStarted=pressureClock;}
            lastPressureHit=pressureClock;pressureHits++;
            if(pressureHits>=4 && pressureClock-pressureStarted>=1.2f)resolvePending=true;
        }

        public void InitializeChampion(TrialGame owner,Vector3 point,ChampionKind kind,GameObject model)
        {
            IsBoss=true;Spawn=point;transform.position=point;
            Configure(owner,TrialChampionCatalog.Health(kind),100,.48f,1.9f);PostureLimit=kind==ChampionKind.Remnant?340:kind==ChampionKind.Anon?240:180;
            Champion=gameObject.AddComponent<TrialChampion>();Champion.Initialize(this,owner,kind,model);
            Visual=Champion.Visual;renderers=Visual.GetComponentsInChildren<Renderer>();name=DisplayName;transform.rotation=Quaternion.Euler(0,180,0);
        }

        public void Initialize(TrialGame owner,Vector3 position,bool boss,GameObject bossModel=null)
        {
            IsBoss=boss;Spawn=position;transform.position=position;
            Configure(owner,boss?1400:105,100,boss?.72f:.34f,boss?2.7f:1.95f);
            name=DisplayName;
            if(boss)
            {
                if(!bossModel)throw new System.InvalidOperationException("DeepSeek model is required for the boss encounter.");
                Boss=gameObject.AddComponent<TrialBoss>();Boss.Initialize(this,owner,bossModel);
                Visual=Boss.Visual;renderers=Visual.GetComponentsInChildren<Renderer>();
                transform.rotation=Quaternion.Euler(0,180,0);return;
            }
            if(!owner.NaiWaModel)throw new System.InvalidOperationException("NaiWa model is required for guards.");
            Visual=Instantiate(owner.NaiWaModel,transform).transform;Visual.name="NaiWa · 奶蛙";
            foreach(var animator in Visual.GetComponentsInChildren<Animator>())animator.enabled=false;
            foreach(var collider in Visual.GetComponentsInChildren<Collider>())Destroy(collider);
            renderers=Visual.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            Visual.localScale*=1.72f/Mathf.Max(.01f,bounds.size.y);
            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            Visual.position+=new Vector3(transform.position.x-bounds.center.x,transform.position.y-bounds.min.y,transform.position.z-bounds.center.z);
            frogRig=gameObject.AddComponent<TrialNaiWaRig>();frogRig.Initialize(this,Visual);
            cooldown=1.1f;transform.rotation=Quaternion.Euler(0,180,0);
        }

        public void Tick(float dt)
        {
            TickResolve(dt);
            if(CombatFrozen)return;
            flinchCooldown=Mathf.Max(0,flinchCooldown-dt);
            if(!Alive)FlinchRemaining=0;
            if(Flinching)
            {
                FlinchRemaining=Mathf.Max(0,FlinchRemaining-dt);TickGravity(dt);
                if(Champion)Champion.TickDuringFlinch(dt);
                if(Boss)Boss.TickDuringFlinch(dt);
                return;
            }
            if(IsBoss)Recoil=Vector3.MoveTowards(Recoil,Vector3.zero,dt*7);
            if(Champion)
            {
                if(!Alive){ActionTime+=dt;Champion.Tick(dt);return;}
                TickBase(dt);
                if(!Awakened && game.Mode==TrialMode.Playing && game.GateOpen){Awakened=true;game.SealArena();}
                Champion.Tick(dt);return;
            }
            if(Boss)
            {
                if(!Alive){ActionTime+=dt;Boss.Tick(dt);return;}
                TickBase(dt);
                if(!Awakened && game.GateOpen && Vector3.Distance(game.CombatTarget.transform.position-new Vector3(0,0,25),Vector3.zero)<8.5f)
                {Awakened=true;game.SealArena();}
                Boss.Tick(dt);return;
            }
            if(!Alive) {MoveBlend=0;ActionTime+=dt;return;}
            TickBase(dt);cooldown-=dt;
            if(recoilVelocity.sqrMagnitude>.01f){Move(recoilVelocity,dt);recoilVelocity=Vector3.MoveTowards(recoilVelocity,Vector3.zero,dt*20);}
            if(Action!=FighterAction.Free)MoveBlend=0;
            var target=game.CombatTarget;
            if(!target.Alive) {MoveBlend=0;return;}
            var delta=target.transform.position-transform.position;delta.y=0;
            float distance=delta.magnitude;
            if(!Awakened && distance<(IsBoss?10:8.5f) && (!IsBoss || game.GateOpen))
            {Awakened=true;}
            if(!Awakened) {return;}
            if(PhaseTwo && !phaseAnnounced)
            {phaseAnnounced=true;game.Hud.Notify("二阶段",1.2f);game.Effects.Burst(transform.position+Vector3.up,Color.red,60);}
            if(Action==FighterAction.Stagger)
            {if(ActionTime>ActionDuration)Begin(FighterAction.Free,0);return;}
            if(Action==FighterAction.Light || Action==FighterAction.Heavy)
            {
                if(Progress<.25f)Turn(delta,IsBoss?100:155,dt);
                if(Progress>.26f && Progress<.53f)Move(transform.forward*(IsBoss?4.5f:3),dt);
                if(AttackActive && !attackLanded && distance<(IsBoss?3.5f:1.95f) && Vector3.Angle(transform.forward,delta)<(Unblockable?115:65))
                {
                    if(!Physics.Linecast(transform.position+Vector3.up,target.transform.position+Vector3.up,1,QueryTriggerInteraction.Ignore))
                    {attackLanded=true;target.ReceiveHit(this,IsBoss?(Unblockable?45:32):23,30,Unblockable);}
                }
                if(ActionTime>=ActionDuration)
                {
                    Begin(FighterAction.Free,0);Unblockable=false;
                    cooldown=IsBoss?(PhaseTwo?.5f:.82f):1.3f;
                    if(IsBoss && PhaseTwo && chain==0 && distance<4) {chain=1;cooldown=.20f;}else chain=0;
                }
                return;
            }
            Turn(delta,180,dt);
            float desired=IsBoss?3.1f:1.65f;
            MoveBlend=0;
            if(distance>desired)
            {
                var direction=delta.normalized;
                if(Physics.SphereCast(transform.position+Vector3.up,.35f,direction,out var hit,1.4f,1,QueryTriggerInteraction.Ignore))
                    direction=Vector3.Cross(Vector3.up,hit.normal).normalized;
                foreach(var other in game.Enemies)
                    if(other!=this && other.Alive && (other.transform.position-transform.position).sqrMagnitude<1.8f)
                        direction+=Vector3.ProjectOnPlane(transform.position-other.transform.position,Vector3.up).normalized*.7f;
                Move(direction.normalized*(IsBoss?2.4f:1.8f),dt);MoveBlend=1;
            }
            else if(cooldown<=0)
            {
                attackIndex++;
                Unblockable=IsBoss && PhaseTwo && attackIndex%3==0;
                Combo=attackIndex%2;attackLanded=false;
                Begin(Unblockable?FighterAction.Heavy:FighterAction.Light,IsBoss?(Unblockable?1.65f:PhaseTwo?1.05f:1.3f):1.36f);
                game.Effects.Sound("warning",transform.position,Unblockable?.65f:1);
            }
            else if(IsBoss)
            {orbitAge+=dt;Move(transform.right*(Mathf.Sin(orbitAge)*.65f),dt);}

        }

        public void ApplyReplica(TrialActorState state,float dt)
        {ApplyNetwork(state,dt);Awakened=state.awake;Recoil=state.recoil;resolveTime=state.bossResolve;FlinchRemaining=state.bossFlinch;}
        public override TrialActorState CaptureNetwork(int id){var state=base.CaptureNetwork(id);state.recoil=Recoil;state.bossResolve=resolveTime;state.bossFlinch=FlinchRemaining;return state;}

        public override bool ReceiveHit(TrialFighter attacker,float damage,float postureDamage,bool unblockable=false)
        {
            if(!Alive || (IsBoss && !game.GateOpen))return false;
            if(attacker is TrialPlayer && Invulnerable)return false;
            bool exposed=Action==FighterAction.Stagger;
            if(Champion)
            {
                bool counterStance=Champion.CounterReady;
                bool countered=attacker is TrialPlayer && Champion.TryCounter(attacker.Action==FighterAction.Heavy);
                bool swordGuard=!counterStance && !exposed && Champion.TryGuardHit(attacker,unblockable);
                Impact(attacker,damage*(exposed?1.65f:countered?.15f:swordGuard?.18f:1));
                if(attacker && !ResolveActive)Recoil=Vector3.ProjectOnPlane(transform.position-attacker.transform.position,Vector3.up).normalized*(attacker.Action==FighterAction.Heavy?1.0f:.65f);
                if(!Alive){Champion.CancelAttack(true);game.OnEnemyDied(this);return true;}
                Awakened=true;if(!ResolveActive)AddChampionPosture(postureDamage*(swordGuard?1.2f:.75f));
                if(!countered && !swordGuard)HitFlinch(attacker);
                RecordPressure(attacker);return true;
            }
            if(!IsBoss && attacker){Recoil=Vector3.ProjectOnPlane(transform.position-attacker.transform.position,Vector3.up).normalized*Mathf.Clamp(damage*.045f,1,4);recoilVelocity=Recoil;}
            Impact(attacker,damage*(exposed?1.65f:1)*(Boss && Boss.Boon==TrialBossBoon.IronScales?.92f:1));
            if(!Alive) {game.OnEnemyDied(this);return true;}
            if(IsBoss && !Awakened)game.SealArena();
            Awakened=true;if(!ResolveActive)Posture+=postureDamage;
            if(!exposed && (Posture>=PostureLimit || (!IsBoss && Action==FighterAction.Free)))
            {Posture=0;Begin(FighterAction.Stagger,IsBoss?1.35f:.55f);cooldown=.6f;if(Boss)Boss.CancelAttack();}
            RecordPressure(attacker);
            HitFlinch(attacker);
            return true;
        }
        public void Parried()
        {
            resolveTime=0;resolvePending=false;pressureHits=0;
            if(Champion){Champion.Deflected();return;}
            if(IsBoss){Posture+=55;if(Posture<PostureLimit){Begin(FighterAction.Stagger,.55f);Boss.CancelAttack();return;}}
            Posture=0;Begin(FighterAction.Stagger,IsBoss?1.8f:2.0f);cooldown=.6f;if(Boss)Boss.CancelAttack();
        }
        public void ScaleForParty(int players)
        {
            int extra=Mathf.Max(1,players)-1;
            float maximum=Champion?TrialChampionCatalog.Health(Champion.Kind)*(1f+extra):IsBoss?1400*(1f+extra):105*(1+Mathf.Min(3,extra)*.55f);
            maximum*=TrialDifficultySettings.HealthScale(game.Difficulty)*(Champion?Champion.KillHealthScale:1);
            if(Mathf.Approximately(MaxHealth,maximum))return;
            float ratio=MaxHealth>0?Health/MaxHealth:1;
            MaxHealth=maximum;Health=maximum*ratio;
            float limit=(Champion?(Champion.IsRemnant?340:Champion.Kind==ChampionKind.Anon?240:180):100)*(1+extra*(IsBoss?.55f:.2f))*(Champion?Champion.KillPostureScale:1);
            Posture=Posture/PostureLimit*limit;PostureLimit=limit;
        }
        internal void BossBegin(FighterAction next,float duration)=>Begin(next,duration);
        internal void BossGuard(bool value){Guarding=value;}
        internal void ApplyKillGrowth()
        {float previousMax=MaxHealth,previousHealth=Health;ScaleForParty(game.PartySize);Health=Mathf.Min(MaxHealth,previousHealth+MaxHealth-previousMax+previousMax*.08f);}
        internal void BossMove(Vector3 velocity,float dt)=>Move(velocity,dt);
        internal void BossFace(Vector3 direction,float speed,float dt)=>Turn(direction,speed,dt);
        internal void BossMotion(float movement){MoveBlend=Mathf.MoveTowards(MoveBlend,movement,Time.deltaTime*7);}
        internal void AddChampionPosture(float amount)
        {
            Posture+=amount;
            if(Posture<PostureLimit || Vulnerable)return;
            Posture=0;Begin(FighterAction.Stagger,2.6f);Champion.CancelAttack();
            game.Effects.Burst(transform.position+Vector3.up,new Color(1,.8f,.25f),40);
            if(game.Player.LockTarget==this){game.Hud.Notify("架势崩溃 · 重击可发动处决",2);game.CameraRig.CombatFrame(this,.65f,.75f);}
        }
        public void ResetFighter()
        {
            FlinchRemaining=flinchCooldown=0;
            if(Champion){Champion.ResetEncounter();ScaleForParty(game.PartySize);}
            ClearCombatHold();
            pressureClock=pressureStarted=resolveTime=resolveCooldown=0;lastPressureHit=-10;pressureHits=0;resolvePending=resolveGlow=false;
            Health=MaxHealth;Stamina=MaxStamina;Posture=0;Awakened=false;phaseAnnounced=false;
            Recoil=recoilVelocity=Vector3.zero;
            hitFlash=0;foreach(var r in renderers)if(r)r.SetPropertyBlock(null);if(trail){trail.Clear();trail.emitting=false;}
            attackIndex=chain=0;cooldown=1.1f;Unblockable=false;motor.enabled=true;Teleport(Spawn);
            transform.rotation=Quaternion.Euler(0,180,0);Visual.localRotation=Quaternion.identity;Begin(FighterAction.Free,0);
            MoveBlend=0;if(frogRig)frogRig.ResetPresentation();if(Boss)Boss.ResetBoss();if(Champion)Champion.ResetEncounter();
        }
    }
}
