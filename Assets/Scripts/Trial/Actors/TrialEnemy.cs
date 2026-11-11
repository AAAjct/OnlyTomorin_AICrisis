using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialEnemy : TrialFighter
    {
        const float TerraceHealth=1800;
        public bool IsBoss { get; private set; }
        public float BodyRadius => IsBoss?.75f:RunGuardKind==TrialRunGuardKind.Takosaki?.62f:.38f;
        public bool Awakened { get; private set; }
        public bool PhaseTwo => IsBoss && Health < MaxHealth * .55f;
        public string DisplayName => Champion?TrialChampionCatalog.Name(Champion.Kind):IsBoss?"大肥鱼 · DeepSeek":RunGuardKind switch
        {
            TrialRunGuardKind.DoroXueLian => "雪莲 · 冰咒师",
            TrialRunGuardKind.Takosaki => "章鱼小祥 · 潮击",
            _ => "奶蛙 · 拳卫"
        };
        bool unblockable;
        public bool Unblockable { get => Champion?Champion.Unblockable:Boss?Boss.Unblockable:unblockable; private set=>unblockable=value; }
        public bool WindingUp => Champion?Champion.WindingUp:Boss?Boss.WindingUp:HasRunSkill?RunWarningProgress<1 && RunSkillActive:(Action==FighterAction.Light || Action==FighterAction.Heavy) && Progress<.33f;
        public TrialBoss Boss { get; private set; }
        public TrialChampion Champion {get;private set;}
        public bool Vulnerable => Action==FighterAction.Stagger;
        public Vector3 Spawn { get; private set; }
        float cooldown,orbitAge;
        bool attackLanded,phaseAnnounced;
        int attackIndex,chain;
        TrialNaiWaRig frogRig;
        TrialPlayer coffinCaster;
        float coffinExecutionAt,coffinReplicaHideUntil;
        bool CoffinSealed=>coffinExecutionAt>0;
        public Vector3 Recoil {get;private set;}
        public float FlinchRemaining {get;private set;}
        public bool Flinching=>FlinchRemaining>0;
        float flinchCooldown;
        internal bool MaintainsInitiativeAgainst(TrialFighter attacker)
            =>TrialDuelRead.IsBasicLight(attacker) &&
                (Champion && Champion.Attacking && !Champion.Recovering || Boss && Boss.Skill!=DeepFishSkill.None && !Boss.Recovering);

        void HitFlinch(TrialFighter attacker)
        {
            if(!IsBoss || !Alive || Vulnerable || ResolveActive || flinchCooldown>0)return;
            if(Champion && Champion.TerraceDuel)
            {
                if(!attacker || !Champion.TerraceCanFlinch)return;
                FlinchRemaining=attacker.Action==FighterAction.Heavy?.26f:.16f;
                flinchCooldown=.42f;MoveBlend=0;
                return;
            }
            if(MaintainsInitiativeAgainst(attacker))return;
            FlinchRemaining=attacker && attacker.Action==FighterAction.Heavy?.22f:.13f;
            // Later phases can finish their telegraphed strings under light pressure.
            if(Champion && Champion.Attacking)FlinchRemaining*=Champion.Phase==3?.65f:Champion.Phase==2?.85f:1;
            else if(Boss && Boss.Skill!=DeepFishSkill.None)FlinchRemaining*=Boss.Frenzied?.65f:Boss.Enraged?.85f:1;
            flinchCooldown=.38f;MoveBlend=0;
        }
        Vector3 recoilVelocity;
        float pressureClock,lastPressureHit=-10,pressureStarted,resolveTime,resolveCooldown;
        int pressureHits;
        bool resolvePending;
        bool resolveGlow;
        public bool ResolveActive=>IsBoss && resolveTime>0;
        protected override bool CanRecoverPosture => !(Champion && (Champion.TerraceDuel || Champion.IsRemnant && Champion.Attacking));
        public bool BreakoutInvulnerable=>ResolveActive && resolveTime>1.8f;
        void LateUpdate()
        {
            if(HasRunSkill)PresentRunVariant();
            if(!ResolveActive && !resolveGlow)return;
            resolveGlow=ResolveActive;
            SetHitEmission(ResolveActive?new Color(.15f,.65f,1.8f):Color.black);
        }

        void TickResolve(float dt)
        {
            pressureClock+=dt;resolveTime=Mathf.Max(0,resolveTime-dt);resolveCooldown=Mathf.Max(0,resolveCooldown-dt);
            if(!Alive || !IsBoss || Champion && Champion.TerraceDuel)return;
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
            float sustained=pressureClock-pressureStarted;
            if(pressureHits>=4 && sustained>=1.2f || pressureHits>=8 && sustained>=.45f)resolvePending=true;
        }

        public void InitializeChampion(TrialGame owner,Vector3 point,ChampionKind kind,GameObject model)
        {
            IsBoss=true;Spawn=point;transform.position=point;
            bool terrace=kind==ChampionKind.Anon;
            Configure(owner,terrace?TerraceHealth:TrialChampionCatalog.Health(kind),100,.48f,1.9f);PostureLimit=terrace?100:kind==ChampionKind.Remnant?340:180;
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
                DisableShadows(Visual);
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
            if(CoffinSealed)
            {
                if(Time.time>=coffinExecutionAt)AdvanceCoffinExecution();
                return;
            }
            TickRunStatuses(dt);
            TickResolve(dt);
            if(CombatFrozen)return;
            if(TickSpellControl(dt))return;
            flinchCooldown=Mathf.Max(0,flinchCooldown-dt);
            if(!Alive)FlinchRemaining=0;
            if(Flinching)
            {
                FlinchRemaining=Mathf.Max(0,FlinchRemaining-dt);TickGravity(dt);
                if(Champion && Champion.TerraceDuel)Champion.TickTerraceRecoil(dt);
                else if(Champion)Champion.TickDuringFlinch(dt);
                if(Boss)Boss.TickDuringFlinch(dt);
                return;
            }
            if(IsBoss)Recoil=Vector3.MoveTowards(Recoil,Vector3.zero,dt*7);
            if(Champion)
            {
                if(!Alive){ActionTime+=dt;Champion.Tick(dt);return;}
                TickBase(dt);
                if(!Awakened && game.Mode==TrialMode.Playing && game.GateOpen && (!Champion.TerraceDuel || game.CombatTarget.transform.position.z>=TrialWorld.TerraceEntryZ)){Awakened=true;game.SealArena();}
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
            TickBase(dt);
            if(!Alive)return;
            cooldown-=dt;
            if(!RunSkillActive && recoilVelocity.sqrMagnitude>.01f){Move(recoilVelocity,dt);recoilVelocity=Vector3.MoveTowards(recoilVelocity,Vector3.zero,dt*20);}
            if(Action!=FighterAction.Free)MoveBlend=0;
            var target=game.CombatTarget;
            if(!target.Alive && !RunSkillActive) {MoveBlend=0;return;}
            var delta=target.transform.position-transform.position;delta.y=0;
            float distance=delta.magnitude;
            if(!Awakened && distance<(IsBoss?10:8.5f) && (!IsBoss || game.GateOpen))
            {Awakened=true;}
            if(!Awakened) {return;}
            if(PhaseTwo && !phaseAnnounced)
            {phaseAnnounced=true;game.Hud.Notify("二阶段",1.2f);game.Effects.Burst(transform.position+Vector3.up,Color.red,60);}
            if(Action==FighterAction.Stagger)
            {if(ActionTime>ActionDuration)Begin(FighterAction.Free,0);return;}
            if(HasRunSkill){TickRunVariant(delta,distance,dt);return;}
            if(Action==FighterAction.Light || Action==FighterAction.Heavy)
            {
                if(Progress<.25f)Turn(delta,IsBoss?100:155,dt);
                if(Progress>.26f && Progress<.53f)Move(transform.forward*(IsBoss?4.5f:3),dt);
                if(AttackActive && !attackLanded && target.OverlapsAttackHeight(transform.position.y,transform.position.y+GroundMeleeHeight) && distance<(IsBoss?3.5f:1.95f) && Vector3.Angle(transform.forward,delta)<(Unblockable?115:65))
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
                ApproachGuard(delta,IsBoss?2.4f:1.8f,dt);
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

        void ApproachGuard(Vector3 delta,float speed,float dt)
        {
            var direction=delta.normalized;
            if(Physics.SphereCast(transform.position+Vector3.up,.35f,direction,out var hit,1.4f,1,QueryTriggerInteraction.Ignore))
                direction=Vector3.Cross(Vector3.up,hit.normal).normalized;
            foreach(var other in game.Enemies)
                if(other!=this && other.Alive && (other.transform.position-transform.position).sqrMagnitude<1.8f)
                    direction+=Vector3.ProjectOnPlane(transform.position-other.transform.position,Vector3.up).normalized*.7f;
            Move(direction.normalized*speed*(Time.time<runSlowUntil?1-runSlow:1),dt);MoveBlend=1;
        }

        internal void SeverHead(){if(frogRig && !Alive)frogRig.SeverHead();}
        public void ApplyReplica(TrialActorState state,float dt)
        {
            ApplyNetwork(state,dt);Awakened=state.awake;Recoil=state.recoil;resolveTime=state.bossResolve;FlinchRemaining=state.bossFlinch;
            if(RunSkillActive)
            {
                runSkillOrigin=state.position;
                runSkillForward=Quaternion.Euler(0,state.yaw,0)*Vector3.forward;
            }
            if(state.spellExecuted)coffinReplicaHideUntil=0;
            bool spellHidden=state.spellExecuted || Time.unscaledTime<coffinReplicaHideUntil;
            if(Visual.gameObject.activeSelf==spellHidden)Visual.gameObject.SetActive(!spellHidden);
            if(state.headSevered)SeverHead();
            else if(frogRig && frogRig.HeadSevered)frogRig.ResetPresentation();
        }

        internal void PresentCoffinExecution()
        {
            // A reliable effect cue can arrive before the snapshot carrying the execution.
            coffinReplicaHideUntil=Time.unscaledTime+.5f;
            Visual.gameObject.SetActive(false);
        }

        public override TrialActorState CaptureNetwork(int id){var state=base.CaptureNetwork(id);state.enemyKind=(byte)(Champion?2+(int)Champion.Kind:IsBoss?1:(int)RunGuardKind);state.recoil=Recoil;state.bossResolve=resolveTime;state.bossFlinch=FlinchRemaining;state.headSevered=frogRig && frogRig.HeadSevered;state.spellExecuted=!Visual.gameObject.activeSelf;return state;}

        public override bool ReceiveHit(TrialFighter attacker,float damage,float postureDamage,bool unblockable=false)
            =>ReceiveEnemyHit(attacker,damage,postureDamage,unblockable,awardSpellEnergy:true);

        bool ReceiveEnemyHit(TrialFighter attacker,float damage,float postureDamage,bool unblockable,bool awardSpellEnergy)
        {
            if(!Alive || (!IsBoss && CoffinSealed) || (IsBoss && !game.GateOpen))return false;
            if(Champion && Champion.TerraceDuel && (!Awakened || Champion.TerraceChangingPhase))return false;
            if(attacker is TrialPlayer && Invulnerable)return false;
            if(attacker is TrialPlayer runPlayer)damage=runPlayer.ModifyRunDamage(this,damage,ref postureDamage);
            bool exposed=Vulnerable;
            if(Champion && Champion.TerraceDuel)
            {
                if(Champion.TerraceReceiveStrike(attacker,postureDamage))return false;
                Impact(attacker,damage);
                if(attacker)
                {
                    Vector3 away=TrialChampion.Planar(transform.position-attacker.transform.position).normalized;
                    bool heavy=attacker.Action==FighterAction.Heavy;
                    Recoil=away*(heavy?.8f:.35f);
                    if(Alive)Champion.TerraceStrikeRecoil(away,heavy);
                }
                if(!Alive){Champion.CancelAttack(true);game.OnEnemyDied(this,attacker as TrialPlayer,awardSpellEnergy);}
                else if(postureDamage>0)HitFlinch(attacker);
                return true;
            }
            bool braced=!exposed && !unblockable && postureDamage>0 &&
                (Champion && Champion.BracesAgainst(attacker) || Boss && Boss.BracesAgainst(attacker)) &&
                Vector3.Dot(transform.forward,Vector3.ProjectOnPlane(attacker.transform.position-transform.position,Vector3.up))>0;
            if(Champion)
            {
                bool counterStance=Champion.CounterReady;
                bool countered=!unblockable && postureDamage>0 && attacker is TrialPlayer &&
                    (attacker.Action==FighterAction.Heavy || TrialDuelRead.IsBasicLight(attacker)) &&
                    Vector3.Angle(transform.forward,attacker.transform.position-transform.position)<=85 &&
                    Champion.TryCounter(attacker.Action==FighterAction.Heavy);
                bool swordGuard=!counterStance && !exposed && Champion.TryGuardHit(attacker,unblockable);
                Impact(attacker,damage*(exposed?1.65f:countered?.15f:swordGuard?.18f:braced?.65f:1));
                if(attacker && !ResolveActive)Recoil=Vector3.ProjectOnPlane(transform.position-attacker.transform.position,Vector3.up).normalized*(attacker.Action==FighterAction.Heavy?1.0f:.65f);
                if(!Alive){Champion.CancelAttack(true);game.OnEnemyDied(this,attacker as TrialPlayer,awardSpellEnergy);return true;}
                Awakened=true;if(!ResolveActive)AddChampionPosture(postureDamage*(swordGuard?1.2f:countered||braced?.35f:.75f));
                if(!countered && !swordGuard)HitFlinch(attacker);
                RecordPressure(attacker);return true;
            }
            if(!IsBoss && attacker){Recoil=Vector3.ProjectOnPlane(transform.position-attacker.transform.position,Vector3.up).normalized*Mathf.Clamp(damage*.045f,1,4);recoilVelocity=Recoil;}
            Impact(attacker,damage*(exposed?1.65f:braced?.65f:1)*(Boss && Boss.Boon==TrialBossBoon.IronScales?.92f:1));
            if(!Alive) {game.OnEnemyDied(this,attacker as TrialPlayer,awardSpellEnergy);return true;}
            if(IsBoss && !Awakened)game.SealArena();
            Awakened=true;if(!ResolveActive)Posture+=postureDamage*(braced?.45f:1);
            if(!exposed && (Posture>=PostureLimit || (!IsBoss && Action==FighterAction.Free)))
            {Posture=0;Begin(FighterAction.Stagger,IsBoss?1.35f:.55f);cooldown=.6f;if(Boss)Boss.CancelAttack();}
            RecordPressure(attacker);
            HitFlinch(attacker);
            return true;
        }
        internal void InterruptForSpell()
        {
            if(Champion)Champion.CancelAttack(true);
            if(Boss)Boss.CancelAttack();
            Recoil=recoilVelocity=Vector3.zero;
            FlinchRemaining=0;
        }
        internal bool SealInCoffin(TrialPlayer caster,float stagger)
        {
            if(!CanBeCoffined)return false;
            coffinCaster=caster;
            float sealDuration=TrialPlayer.CoffinSealDuration+stagger;
            coffinExecutionAt=Time.time+sealDuration;
            InterruptForSpell();
            StopLocomotion();
            if(!IsBoss)Begin(FighterAction.Stagger,sealDuration);
            else if(!Vulnerable)Begin(FighterAction.Free,0);
            return true;
        }

        void AdvanceCoffinExecution()
        {
            var caster=coffinCaster;
            var presenter=caster?caster:game.Player;
            coffinExecutionAt=0;coffinCaster=null;
            if(IsBoss)
            {
                presenter.PlayCoffinCue(this,TrialCoffinStage.BreakShell);
                if(DamageWithSpell(caster?caster:null,TrialPlayer.CoffinBossDamage,55,awardSpellEnergy:false))
                    presenter.PlayCoffinCue(this,TrialCoffinStage.DamageBoss);
                return;
            }
            if(!ExecuteWithSpell())return;
            presenter.PlayCoffinCue(this,TrialCoffinStage.ShatterEnemy);
            game.OnEnemyDied(this,caster,awardSpellEnergy:false);
        }

        internal bool CanBeSpellExecuted=>Alive && isActiveAndEnabled && !IsBoss && !CoffinSealed;
        internal bool CanBeSpellDamaged=>IsBoss && Alive && isActiveAndEnabled && game.GateOpen && !Invulnerable && !CoffinSealed;
        internal bool CanBeCoffined=>CanBeSpellExecuted || CanBeSpellDamaged;

        internal bool DamageWithSpell(TrialPlayer caster,float healthFraction,float postureDamage,bool awardSpellEnergy)
            =>CanBeSpellDamaged && ReceiveEnemyHit(caster,MaxHealth*healthFraction,postureDamage,unblockable:true,awardSpellEnergy:awardSpellEnergy);

        internal bool ExecuteWithSpell()
        {
            if(!CanBeSpellExecuted)return false;
            Impact(null,Health,true);
            Visual.gameObject.SetActive(false);
            return true;
        }
        public void Parried(TrialPlayer defender=null,bool hazard=false)
        {
            if(Champion && Champion.TerraceDuel){if(!hazard)Champion.TerraceDeflect(defender);return;}
            if(Champion)Champion.ObserveParry(defender);
            if(Boss)Boss.ObserveParry(defender);
            // Deflecting a projectile deals with that projectile, even if its owner has started another move.
            if(hazard)return;
            resolveTime=0;resolvePending=false;pressureHits=0;
            if(Champion){Champion.Deflected();return;}
            if(Boss)
            {
                Posture+=55;
                if(Posture<PostureLimit)
                {
                    if(defender && Boss.RetainNextBeat())return;
                    Begin(FighterAction.Stagger,.55f);Boss.CancelAttack();return;
                }
            }
            Posture=0;Begin(FighterAction.Stagger,IsBoss?1.8f:2.0f);cooldown=.6f;if(Boss)Boss.CancelAttack();
        }
        public void ScaleForParty(int players)
        {
            int extra=Mathf.Max(1,players)-1;
            float maximum=Champion?(Champion.TerraceDuel?TerraceHealth:TrialChampionCatalog.Health(Champion.Kind))*(1f+extra):IsBoss?1400*(1f+extra):GuardBaseHealth*(1+Mathf.Min(3,extra)*.55f);
            maximum*=runHealthScale*TrialDifficultySettings.HealthScale(game.Difficulty)*(Champion?Champion.KillHealthScale:1);
            if(Mathf.Approximately(MaxHealth,maximum))return;
            float ratio=MaxHealth>0?Health/MaxHealth:1;
            MaxHealth=maximum;Health=maximum*ratio;
            float limit=(Champion && !Champion.TerraceDuel?(Champion.IsRemnant?340:180):100)*(1+extra*(IsBoss?.55f:.2f))*(Champion?Champion.KillPostureScale:1);
            Posture=Posture/PostureLimit*limit;PostureLimit=limit;
        }
        internal void BossBegin(FighterAction next,float duration)=>Begin(next,duration);
        internal void BossGuard(bool value){Guarding=value;}
        internal void ApplyKillGrowth()
        {float previousMax=MaxHealth,previousHealth=Health;ScaleForParty(game.PartySize);Health=Mathf.Min(MaxHealth,previousHealth+MaxHealth-previousMax+previousMax*.08f);}
        internal void BossMove(Vector3 velocity,float dt)=>Move(velocity,dt);
        internal void BossFace(Vector3 direction,float speed,float dt)=>Turn(direction,speed,dt);
        internal void BossMotion(float movement){MoveBlend=Mathf.MoveTowards(MoveBlend,movement,Time.deltaTime*7);}
        internal bool AddTerracePosture(float amount,bool force)
        {
            if(Vulnerable)return false;
            Posture+=amount;
            if(!force && Posture<PostureLimit)return false;
            Posture=0;
            return true;
        }
        internal void DecayTerracePosture(float amount){Posture=Mathf.Max(0,Posture-amount);}
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
            coffinExecutionAt=0;coffinCaster=null;
            coffinReplicaHideUntil=0;
            ClearSpellControl();
            Visual.gameObject.SetActive(true);
            FlinchRemaining=flinchCooldown=0;
            if(Champion){Champion.ResetEncounter();ScaleForParty(game.PartySize);}
            ClearCombatHold();
            pressureClock=pressureStarted=resolveTime=resolveCooldown=0;lastPressureHit=-10;pressureHits=0;resolvePending=resolveGlow=false;
            Health=MaxHealth;Stamina=MaxStamina;Posture=0;Awakened=false;phaseAnnounced=false;
            Recoil=recoilVelocity=Vector3.zero;
            hitFlash=0;foreach(var r in renderers)if(r)r.SetPropertyBlock(null);if(trail){trail.Clear();trail.emitting=false;}
            attackIndex=chain=0;attackLanded=false;cooldown=1.1f;Unblockable=false;motor.enabled=true;Teleport(Spawn);
            transform.rotation=Quaternion.Euler(0,180,0);Visual.localRotation=Quaternion.identity;Begin(FighterAction.Free,0);
            MoveBlend=0;
            if(HasRunSkill){runGait=0;PresentRunVariant();}
            if(frogRig)frogRig.ResetPresentation();if(Boss)Boss.ResetBoss();if(Champion)Champion.ResetEncounter();
        }
    }
}
