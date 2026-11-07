using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialPlayer : TrialFighter
    {
        public const float BaseHealth=200;
        public const float BaseStamina=630;
        public const float PerfectDodgeStamina=40;
        public const float ParryStamina=30;
        public const float HitStamina=10;
        public const float KillStamina=45;
        protected override bool CanRecoverStamina => base.CanRecoverStamina || Action==FighterAction.Light || Action==FighterAction.Heavy;
        protected override float StaminaRecoveryRate => (1+.12f*AffixStacks(TrialAffix.Recovery))*(Action==FighterAction.Light || Action==FighterAction.Heavy?18:Guarding?40:PlanarVelocity.sqrMagnitude<.25f?165:120);
        protected override float StaminaRecoveryDelay => .18f;
        public int Flasks { get; private set; } = 3;
        public TrialEnemy Target { get; private set; }
        public TrialPlayer DuelTarget { get; private set; }
        public TrialFighter LockTarget => game.IsArena?(TrialFighter)DuelTarget:Target;
        public bool Locked => LockTarget && LockTarget.Alive;
        public int Kills { get; private set; }
        public int Deaths { get; private set; }
        float protectedUntil;
        public bool SpawnProtected => Time.time<protectedUntil;
        public void GrantSpawnProtection(){protectedUntil=Time.time+2;}
        public void ResetScore(){Kills=Deaths=0;}
        public float GuardAge { get; private set; }
        public int SuccessfulDodges { get; private set; }
        public int SuccessfulParries { get; private set; }
        public Vector3 PlanarVelocity { get; private set; }
        public Vector2 LocalMotion { get; private set; }
        public bool Sprinting { get; private set; }
        float commandLightAt=-10,commandHeavyAt=-10,commandDodgeAt=-10,commandHealAt=-10;
        public bool CommandLight=>Time.time-commandLightAt<.18f;
        public bool CommandHeavy=>Time.time-commandHeavyAt<.18f;
        public bool CommandDodge=>Time.time-commandDodgeAt<.18f;
        public bool CommandHeal=>Time.time-commandHealAt<.18f;
        public float TurnLean { get; private set; }
        readonly HashSet<TrialFighter> struck = new HashSet<TrialFighter>();
        float bufferedLight = -10, bufferedDodge = -10, bufferedHeavy = -10, bufferedHeal=-10, bufferedJump=-10;
        const float InputBuffer = .36f;
        int chainCount;
        float chainUntil;
        float chargeTime;
        bool chargeReady,attackAccent;
        public float ChargeAmount => Action==FighterAction.Charge?Mathf.Clamp01(ActionTime/(Combo==FireDragon?DragonChargeLimit:.85f)):0;
        bool healingApplied;
        bool sprintExhausted;
        float hurtUntil;
        bool lastStandSpent;
        Vector3 dodgeDirection;
        Vector3 combatImpulse;
        bool receivingHazard;
        Vector3 hazardSourceDirection;
        public void ApplyCombatImpulse(Vector3 velocity){combatImpulse=Vector3.ClampMagnitude(velocity,10);}
        public void ThrowFromGrapple(TrialEnemy source)
        {
            if(!Alive)return;ClearAttackInputs();chainUntil=0;Begin(FighterAction.Stagger,.72f);
            ApplyCombatImpulse(Vector3.ProjectOnPlane(transform.position-source.transform.position,Vector3.up).normalized*10+Vector3.up*2);
            if(this==game.Player)game.CameraRig.CombatFrame(source,.5f,.7f);
        }
        public bool ReceiveHazardHit(TrialFighter source,float damage,float posture,bool unblockable,Vector3 sourceDirection)
        {
            receivingHazard=true;hazardSourceDirection=sourceDirection;
            try{return ReceiveHit(source,damage,posture,unblockable);}finally{receivingHazard=false;}
        }
        public Vector3 DodgeDirection=>dodgeDirection;
        float blinkReadyUntil,nextGhostAt,nextReversalAt;
        bool perfectDodgeAwarded;
        Vector3 blinkDirection;
        TrialFighter blinkTarget;
        TrialEnemy reversalTarget;
        public SkinnedMeshRenderer[] GhostSkins {get;private set;}
        TrialRig animationRig;
        bool groundedAfterGravity;
        internal bool CanAutoSheathe => !airborneCombo && !AirVolleyDescending && !AutoSwordThrowGuard &&
            (!motor.enabled || groundedAfterGravity) && Time.time >= chainUntil && Time.time >= weaveUntil &&
            !weavePending && !AttackQueued(false) && !AttackQueued(true);

        public void Initialize(TrialGame owner, GameObject model, TrialOutfit outfit)
        {
            Configure(owner,BaseHealth,BaseStamina,.28f,1.72f);
            name = "Tomorin · 剑行者";
            Visual = Instantiate(model,transform).transform;
            Visual.name = "Tomorin";
            foreach (var a in Visual.GetComponentsInChildren<Animator>()) a.enabled = false;
            foreach (var c in Visual.GetComponentsInChildren<Collider>()) Destroy(c);
            var skins = Visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            TrialAppearance.FitModel(Visual,skins.Length>0?skins:Visual.GetComponentsInChildren<Renderer>());
            TrialAppearance.ApplyAccessories(Visual,outfit);
            GhostSkins = Visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            renderers = Visual.GetComponentsInChildren<Renderer>();
            var animation = animationRig = gameObject.AddComponent<TrialRig>();
            animation.Initialize(this,Visual);
            var sword = outfit & TrialOutfit.Swords;
            Weapon = TrialWorld.Sword(animation.RightHand != null ? animation.RightHand : Visual,owner.World.Steel,owner.World.Gold,owner.World.Glow,false,sword);
            DisableShadows(Visual);
            animation.SetWeapon(Weapon);
            if (sword == TrialOutfit.MingShuang)
                animation.SetScabbard(TrialWorld.SwordScabbard(transform));
            var flask=new GameObject("灵露 · 手持药瓶").transform;flask.SetParent(transform,false);
            TrialWorld.Part("Jade bottle",PrimitiveType.Sphere,flask,Vector3.zero,new Vector3(.12f,.17f,.12f),owner.World.Water);
            const float neckHalfHeight=.035f;
            TrialWorld.Part("Brass neck",PrimitiveType.Cylinder,flask,new Vector3(0,TrialRig.FlaskLipHeight-neckHalfHeight,0),new Vector3(.06f,neckHalfHeight,.06f),owner.World.Gold);
            TrialWorld.Part("Spirit dew",PrimitiveType.Sphere,flask,new Vector3(0,-.025f,.01f),new Vector3(.10f,.10f,.10f),owner.World.Glow);
            DisableShadows(flask);
            animation.SetFlask(flask);flask.gameObject.SetActive(false);
            animation.Pose(FighterAction.Free,0,0,0,false,0);
            trail = Weapon.GetComponentInChildren<TrailRenderer>();
        }

        public void Tick(float dt, TrialInput input)
        {
            if (!Alive || ChoosingBoon) return;
            TickRoguelike(dt);
            guardPressedThisTick=input.Guard && !guardWasHeld;guardWasHeld=input.Guard;
            if(input.Attack)commandLightAt=Time.time;
            if(input.Heavy || input.HeavyHeld)commandHeavyAt=Time.time;
            if(input.Dodge)commandDodgeAt=Time.time;
            if(input.Heal)commandHealAt=Time.time;
            TickSwordProjectiles(dt);TickParryFlights(dt);
            // Contact pauses the pose, not input collection. Preserve taps made
            // during hit-stop so heavier impact never makes a chain unresponsive.
            if (input.Attack) bufferedLight=Time.time;
            if (input.Dodge)BufferDodge(input);
            if (input.Heavy) bufferedHeavy=Time.time;
            if (input.Heal) bufferedHeal=Time.time;
            if (input.Jump) bufferedJump=Time.time;
            CollectAttackInputs(dt,input);
            if(guardPressedThisTick && (Action==FighterAction.Dodge || Action==FighterAction.Light && Combo==RollThrust && !attackGuardLatch))
                dodgeGuardQueued=true;
            if(!input.Guard)attackGuardLatch=false;
            if(CombatFrozen)return;
            TickDodgeLink(dt);
            TickBase(dt);
            // The following horizontal Move can clear CharacterController.isGrounded.
            groundedAfterGravity=motor.isGrounded;
            TickSwordThrowGuard(dt);
            if(combatImpulse.sqrMagnitude>.01f){if(!Invulnerable)Move(combatImpulse,dt);combatImpulse=Vector3.MoveTowards(combatImpulse,Vector3.zero,dt*38);}
            if (input.Lock) ToggleLock();
            if (Target && (!Target.Alive || Vector3.Distance(Target.transform.position,transform.position)>21)) Target=null;
            if (DuelTarget && (!DuelTarget.Alive || Vector3.Distance(DuelTarget.transform.position,transform.position)>21)) DuelTarget=null;
            if(Time.time-bufferedJump<InputBuffer && TryJump(input))return;
            if(Action==FighterAction.Charge)
            {
                PlanarVelocity=Vector3.MoveTowards(PlanarVelocity,MoveDirection(input)*2.2f,40*dt);Move(PlanarVelocity,dt);
                Turn(Locked?LockTarget.transform.position-transform.position:MoveDirection(input),540,dt);
                chargeTime=ActionTime;
                if(!chargeReady && chargeTime>=.65f){chargeReady=true;EmitCombat(TrialCombatCue.ChargeReady,transform.position+Vector3.up);}
                if(Time.time-bufferedDodge<InputBuffer && TryDodge()){chargeTime=0;return;}
                if(input.Guard){ClearAttackInputs();chargeTime=0;Begin(FighterAction.Free,0);Guarding=true;GuardAge=0;return;}
                if(!input.HeavyHeld || chargeTime>=(Combo==FireDragon?DragonChargeLimit:1.25f))
                {int branch=Combo==FireDragon?FireDragon:chargeTime>=.65f?17:0;if(branch==FireDragon)dragonPower=Mathf.Clamp01(chargeTime/DragonChargeLimit);chargeTime=0;if(!StartAttack(true,branch))Begin(FighterAction.Free,0);}
                return;
            }
            if (Action != FighterAction.Free)
            {
                Sprinting=false;
                if(Action==FighterAction.Heal)TickLocomotion(dt,input,true);
                else if(Action!=FighterAction.Light || Progress<=.54f)PlanarVelocity=Vector3.zero;
                if (Action == FighterAction.Dodge)
                {
                    Move(DodgeVelocity,dt);
                    TickDodgeGhosts();
                }
                if (Action == FighterAction.Light || Action == FighterAction.Heavy)
                {
                    bool light=Action==FighterAction.Light;
                    if(!IsMartialMove(Combo) && !IsLowAirMove(Combo) && !IsExtendedMove(Combo) && Combo!=2 && Combo!=5 && Combo!=6 && Combo!=SakuraFollowup && Combo!=MoonChase && Combo!=FireDragon && Combo!=SpiralSword && !IsDive(Combo) && (Progress<(light?.30f:.28f) || Progress>.54f))
                        Turn(Locked?LockTarget.transform.position-transform.position:MoveDirection(input),light?1440:1080,dt);
                    if(light && Progress>.54f && !IsAerialMove(Combo))
                    {
                        PlanarVelocity=Vector3.MoveTowards(PlanarVelocity,MoveDirection(input)*3.8f,52*dt);
                        Move(PlanarVelocity,dt);
                    }
                    TrackSwordTarget(dt);
                    TickDodgeCarry(dt);
                    if(TickSwordMotion(dt)){}
                    else if(Combo==9 && Progress>.10f && Progress<.33f)
                    {float step=18*dt;if(blinkTarget)step=Mathf.Min(step,Mathf.Max(0,Vector3.ProjectOnPlane(blinkTarget.transform.position-transform.position,Vector3.up).magnitude-1.1f));Move(blinkDirection*(step/Mathf.Max(.001f,dt)),dt);}
                    else if(Combo==10 && reversalTarget && Progress>.12f && Progress<.36f && (reversalTarget.transform.position-transform.position).sqrMagnitude>3.2f)Move(transform.forward*11,dt);
                    else TickAttackChase(dt,input);
                    TickSwordArts();
                    if((Combo==9 || Combo==10) && Progress<.6f && Time.unscaledTime>=nextGhostAt){game.Effects.Afterimage(this);nextGhostAt=Time.unscaledTime+(Application.isMobilePlatform?.12f:.045f);}
                    if(Combo==17 && Progress>=.43f && !attackAccent)
                    {attackAccent=true;EmitCombat(TrialCombatCue.HeavyLand,transform.position+transform.forward*.7f);}
                    if (AttackActive) HitEnemies();
                    // Preserve contact, then accept defensive cancels and buffered branches.
                    bool canCancel=!IsDive(Combo) || swordLanded;
                    bool lungeCommitted=Combo!=RollThrust || Progress>=RollThrustDriveEnd;
                    if (lungeCommitted && ActionTime>(light?.055f:.075f) && Time.time-bufferedDodge<InputBuffer && TryDodge()) return;
                    if (lungeCommitted && ActionTime>.045f && Combo!=SpiralSword && (input.Guard || Combo==RollThrust && dodgeGuardQueued) && !attackGuardLatch && Stamina>1)
                    { BeginWeaveGuard(); return; }
                    // Contact confirms the landing. A short compression beat replaces
                    // the old full-duration recovery and intervening idle frame.
                    if(IsSwordLandingSource(Combo) && swordLanded && AttackQueued(false))
                    {
                        if(swordLandingAge<.025f)return;
                        if(StartAttack(false,SakuraFollowup))return;
                    }
                    if(attackInputs.Count>0 && RecallFlight!=null && ActionTime>.075f)
                    {
                        if(AttackQueued(false) && StartAttack(false,SwordRecallCut))return;
                        if(AttackQueued(true) && (BeginDragonCharge(input) || StartAttack(true,SwordRecallRise)))return;
                    }
                    if (canCancel && Action==FighterAction.Light && Progress>LightLinkProgress(Combo))
                    {
                        if(AttackQueued(true) && (BeginDragonCharge(input) || StartAttack(true,LightHeavyBranch(Combo))))return;
                        if(AttackQueued(false) && StartAttack(false,NextLight(Combo)))return;
                    }
                    // The grounded light finisher has a small recovery commitment,
                    // then a fresh light opener can flow without an intervening idle pose.
                    if(light && Combo==WheelFinish && Progress>=.82f && AttackQueued(false) && StartAttack(false,0))return;
                    if(canCancel && !airborneCombo && Progress>(light?.62f:.76f) && TryHeal(dt,input))return;
                }
                if(Action==FighterAction.Heavy && Progress>HeavyLinkProgress(Combo) && !AwaitingDragonLanding && (!IsDive(Combo) || swordLanded))
                {
                    if(AttackQueued(false) && StartAttack(false,HeavyLightBranch(Combo)))return;
                    if(AttackQueued(true) && (BeginDragonCharge(input) || StartAttack(true,HeavyHeavyBranch(Combo))))return;
                }
                if(Action==FighterAction.Dodge)
                {
                    if(ActionTime>=.10f && dodgeGuardQueued && Stamina>1){BeginWeaveGuard();return;}
                    if(Combo==7 && Progress>.32f && Progress<.64f)HitEnemies();
                    if(ActionTime>=DodgeLightLinkTime && AttackQueued(false) && StartAttack(false,RollThrust))return;
                    if(ActionTime>=.10f)
                    {
                        if(AttackQueued(true) && (BeginDragonCharge(input) || StartAttack(true,RollFlash)))return;
                    }
                }
                if (Action == FighterAction.Heal && !healingApplied && Progress>.63f)
                {
                    healingApplied=true; Flasks--; Health=Mathf.Min(MaxHealth,Health+85+(Boon==TrialBoon.SpiritDew?12:0)+18*AffixStacks(TrialAffix.FlaskPower));
                    RestoreCombatStamina(MaxStamina);
                    game.Effects.Burst(transform.position+Vector3.up,new Color(.35f,1,.74f),30);
                    EmitCombat(TrialCombatCue.Heal,transform.position);
                }
                if (!(Action==FighterAction.Heavy && AwaitingDragonLanding) && (!(Action==FighterAction.Light || Action==FighterAction.Heavy) || !IsDive(Combo) || swordLanded) && (ActionTime >= ActionDuration || IsSwordLandingSource(Combo) && swordLanded && swordLandingAge>=.16f || (Action==FighterAction.Light && Combo!=RollThrust && !IsComboEnd(false,Combo) && !IsAerialMove(Combo) && Progress>.72f && input.Move.sqrMagnitude>.01f) || (Action==FighterAction.Dodge && Progress>.84f && input.Move.sqrMagnitude>.01f)))
                {
                    if(Action==FighterAction.Light || Action==FighterAction.Heavy)
                    {
                        chainCount=Combo;chainHeavy=Action==FighterAction.Heavy;
                        chainUntil=IsSwordLandingSource(Combo) && swordLanded?Time.time+LandingSakuraGrace:IsComboEnd(chainHeavy,Combo)?0:Time.time+ChainGrace;
                        attackInputAge=0;
                    }
                    Begin(FighterAction.Free,0);
                    TickSwordThrowGuard(0);
                }
                if(Action!=FighterAction.Heal)
                {
                    MoveBlend=Mathf.MoveTowards(MoveBlend,0,dt*5);
                    LocalMotion=Vector2.Lerp(LocalMotion,Vector2.zero,1-Mathf.Exp(-18*dt));
                }
                return;
            }
            bool wasGuarding=Guarding;
            Guarding=(input.Guard || AutoSwordThrowGuard) && Stamina>1;
            GuardAge=AutoSwordThrowGuard?swordThrowGuardAge:Guarding ? (wasGuarding?GuardAge+dt:0) : 100;
            if(Time.time-bufferedDodge<InputBuffer && TryDodge()) return;
            if(!AutoSwordThrowGuard && input.Guard && !attackGuardLatch && (Time.time<chainUntil || dodgeLinkRemaining>0)){BeginWeaveGuard();return;}
            if(TickGuardCombo(dt,input))return;
            if(AttackQueued(true))
            {
                if(BeginDragonCharge(input))return;
                if(StartAttack(true,Mathf.Max(0,Time.time<chainUntil?(chainHeavy?HeavyHeavyBranch(chainCount):LightHeavyBranch(chainCount)):0)))return;
            }
            if(AttackQueued(false) && StartAttack(false,LandingSakuraReady?SakuraFollowup:Mathf.Max(0,Time.time<chainUntil?(chainHeavy?HeavyLightBranch(chainCount):NextLight(chainCount)):0))) return;
            if(TryHeal(dt,input))return;
            TickLocomotion(dt,input,false);
        }

        bool TryHeal(float dt,TrialInput input)
        {
            if(Time.time-bufferedHeal>=InputBuffer || Flasks<=0 || Health>=MaxHealth && Stamina>=MaxStamina || Guarding)return false;
            ClearAttackInputs();bufferedHeal=-10;healingApplied=false;
            Begin(FighterAction.Heal,1.25f);TickLocomotion(dt,input,true);return true;
        }

        void TickLocomotion(float dt,TrialInput input,bool drinking)
        {
            var direction=MoveDirection(input);
            if(!input.Sprint || Stamina>25)sprintExhausted=false;
            if(Stamina<=1)sprintExhausted=true;
            bool running=input.Sprint && !sprintExhausted && !Guarding && direction.sqrMagnitude>.05f && Stamina>1;
            if(running) {Stamina=Mathf.Max(0,Stamina-5*dt);recoveryDelay=.32f;}
            float speed=Guarding?3.8f:running?8f:5f;
            var desired=direction*speed*(1+.06f*AffixStacks(TrialAffix.Haste));
            float acceleration=direction.sqrMagnitude<.01f?58:Vector3.Dot(PlanarVelocity,direction)<0?72:46;
            PlanarVelocity=Vector3.MoveTowards(PlanarVelocity,desired,acceleration*dt);
            float oldYaw=transform.eulerAngles.y;
            if(Locked) Turn(LockTarget.transform.position-transform.position,960,dt);
            else Turn(direction,Guarding?1800:1260,dt);
            TurnLean=Mathf.Lerp(TurnLean,Mathf.Clamp(Mathf.DeltaAngle(oldYaw,transform.eulerAngles.y)/Mathf.Max(dt,.001f)/360,-1,1),1-Mathf.Exp(-12*dt));
            var before=transform.position;
            Move(PlanarVelocity,dt);
            var actual=Vector3.ProjectOnPlane((transform.position-before)/Mathf.Max(dt,.001f),Vector3.up);
            Sprinting=running && actual.magnitude>4.7f;
            var local=transform.InverseTransformDirection(actual)/4.5f;
            LocalMotion=Vector2.Lerp(LocalMotion,new Vector2(local.x,local.z),1-Mathf.Exp(-18*dt));
            MoveBlend=Mathf.MoveTowards(MoveBlend,Mathf.Min(1.6f,actual.magnitude/4.5f),dt*12);
        }

        Vector3 MoveDirection(TrialInput input)
        {
            var forward=input.NetworkDirected?Quaternion.Euler(0,input.NetworkYaw,0)*Vector3.forward:Vector3.ProjectOnPlane(game.CameraRig.transform.forward,Vector3.up).normalized;
            return Vector3.ClampMagnitude(forward*input.Move.y+Vector3.Cross(Vector3.up,forward)*input.Move.x,1);
        }

        bool StartAttack(bool heavy,int combo)
        {
            bool landingPursuit=combo==SakuraFollowup && LandingSakuraReady;
            Vector3 landingVelocity=landingPursuit?LandingCarryVelocity:Vector3.zero;
            bool dodgeFollowup=CanLinkDodgeAttack;
            Vector3 incomingDodge=Action==FighterAction.Dodge?DodgeVelocity:Vector3.zero;
            if(dodgeFollowup && combo==0)combo=heavy?RollFlash:RollThrust;
            if(!startingGuardCombo && RecallFlight!=null)combo=heavy?SwordRecallRise:SwordRecallCut;
            if(combo<0 || (combo==SwordRecallCut || combo==SwordRecallRise) && RecallFlight==null)return false;
            var executionTarget=Locked?LockTarget as TrialEnemy:game.NearestEnemy(transform.position,3.5f);
            bool empowered=ParryEmpowered || ParryDodgeFollowup;
            if(!empowered && heavy && combo==0 && executionTarget && executionTarget.Champion && executionTarget.Vulnerable)combo=14;
            else if(combo==0 && Guarding)combo=11;
            else if(combo==0 && Sprinting)combo=12;
            int pricedMove=ParryDodgeFollowup?(heavy?SpiralSword:BehindThrust):ParryEmpowered?(heavy?FireDragon:ParryTriple):heavy && Time.time<blinkReadyUntil?DodgeDragon:combo;
            if(!Spend(SwordStaminaCost(heavy,pricedMove))) return false;
            attackGuardLatch=attackGuardLatch || Guarding;
            weaveUntil=0;
            combo=EmpoweredBranch(heavy,combo);
            protectedUntil=0;
            if(startingGuardCombo)attackInputAge=0;else ConsumeAttackInput();bufferedLight=bufferedHeavy=bufferedHeal=-10;PlanarVelocity=Vector3.zero;
            Combo=combo;
            if(!heavy && Time.time<blinkReadyUntil){Combo=9;blinkReadyUntil=0;}
            chainCount=0;chainUntil=0;struck.Clear();attackAccent=false;
            chaseTarget=FindChaseTarget();chaseRemaining=heavy?1.65f:Combo==1?2.1f:1.45f;
            bool rollCancel=Action==FighterAction.Dodge;
            float remainingEvasion=rollCancel?Mathf.Max(0,DodgeInvulnerabilityEnd-ActionTime):0;
            if(Combo==RollThrust && chaseTarget)
            {
                Vector3 aim=chaseTarget.transform.position-transform.position;
                if(Mathf.Abs(aim.y)<1.2f && (chaseTarget==LockTarget || Vector3.Angle(transform.forward,Vector3.ProjectOnPlane(aim,Vector3.up))<=24))
                    Turn(aim,36000,.02f);
            }
            else if(!Locked && !rollCancel && Combo!=SpiralSword)
            {
                if(chaseTarget)Turn(chaseTarget.transform.position-transform.position,1440,.06f);
            }
            BeginSwordArt();
            landingSakuraFlow=landingPursuit;
            if(landingPursuit)CarryDodgeIntoAttack(landingVelocity);
            if(dodgeFollowup)CarryDodgeIntoAttack(incomingDodge);
            if(IsMartialMove(Combo))BeginMartialArt();
            PrepareEmpoweredArt();
            if(Combo==9)
            {
                var target=Locked?LockTarget:game.NearestOpponent(this,8);
                blinkTarget=target;
                blinkDirection=target?Vector3.ProjectOnPlane(target.transform.position-transform.position,Vector3.up).normalized:transform.forward;
                Turn(blinkDirection,36000,.02f);game.Effects.Afterimage(this);EmitCombat(TrialCombatCue.BlinkSlash,transform.position+Vector3.up);
            }
            Begin(heavy?FighterAction.Heavy:FighterAction.Light,IsMartialMove(Combo)?MartialDuration(Combo):Combo==AerialCascade?.94f:Combo==StormWheel?.84f:Combo==GuardBloom?.72f:Combo==GatheredPierce?.62f:Combo==GuardNeedle?.48f:IsLowAirMove(Combo)?Combo==AirStep?.56f:Combo==AirFan?.92f:Combo==WallRebound?.64f:.72f:IsExtendedMove(Combo)?Combo==DoubleFang?.76f:Combo==WheelFinish?.78f:Combo==RollThrust?.46f:Combo==RollFlash?.56f:.54f:Combo==FlowingSweep?.60f:Combo==RisingBridge?.62f:Combo==MoonChase?.60f:Combo==DodgeDragon?1.25f:Combo==ParryTriple?.78f:Combo==FireDragon?1.05f:Combo==SpiralSword?.85f:Combo==BehindThrust?.52f:IsAirBlade(Combo)?.56f:Combo==AirSandDive?.78f:Combo==SakuraFollowup?.64f:Combo==6?1.02f:Combo==3?.82f:Combo==20?.92f:Combo==21?.78f:Combo==22?.44f:Combo==23?.52f:Combo==24?.86f:Combo==25?.68f:Combo==19?.94f:Combo==18?.78f:Combo==17?1.0f:Combo==15?.66f:Combo==16?.46f:Combo==14?1.12f:Combo==13?.90f:Combo==12?.68f:Combo==11?.58f:Combo==9?.64f:heavy?(Combo>=4?.76f:.84f):combo==2?.48f:combo==1?.42f:combo==8?.36f:.44f);
            if(Combo==SpiralSword)BeginSwordThrowGuard();
            if(rollCancel && Combo!=DodgeDragon){airborneCombo=false;verticalSpeed=Mathf.Min(verticalSpeed,0);game.Effects.Afterimage(this,false);}
            if(Combo==RollThrust)
            {
                rollThrustEvasionTime=remainingEvasion;
                MoveRollThrust();
            }
            if(Combo==14 && executionTarget && this==game.Player)game.CameraRig.CombatFrame(executionTarget,.8f,1);
            game.Effects.MoveSound(Combo,heavy,transform.position);
            return true;
        }

        bool TryDodge()
        {
            if(!Spend(DodgeCost)) return false;
            dodgeDirection=bufferedDodgeDirection;
            if(bufferedDodgeTurns && !Locked)Turn(dodgeDirection,36000,.02f);
            weaveUntil=0;
            bool enhanced=ParryEmpowered;
            if(enhanced){parryReadyUntil=0;parryDodgeUntil=Time.time+3.5f;blinkReadyUntil=0;}
            bool derivative=!enhanced && Action==FighterAction.Light && Combo>=1 && Combo<=3;
            // Adjacent input frames can report the light first. Recover only
            // a fresh opener, never stale light presses from an existing chain.
            bool lightChord=Action==FighterAction.Light && Combo==0 && ActionTime<=DodgeLightChordWindow
                && Time.time-commandLightAt<=DodgeLightChordWindow;
            chainCount=0;chainUntil=0;struck.Clear();Combo=enhanced?ParryDodge:derivative?7:0;
            perfectDodgeAwarded=false;perfectGhostUntil=0;nextGhostAt=0;
            PreserveDodgeFollowupInputs(lightChord);bufferedDodge=bufferedHeal=-10;PlanarVelocity=Vector3.zero;Begin(FighterAction.Dodge,.44f);
            ClearDodgeLink();dodgeLinkRemaining=ActionDuration+DodgeFollowupGrace;
            dodgeGuardQueued=guardPressedThisTick;
            if(derivative && this==game.Player)game.Hud.Notify("回风斩 · 闪避衍生",.8f);
            RunDodge();
            EmitCombat(TrialCombatCue.Dodge,transform.position);
            if(derivative)EmitSwordCue(TrialCombatCue.SwordTurn,transform.position+Vector3.up*.95f,transform.forward,transform.position,1);
            if(enhanced)EmitSwordCue(TrialCombatCue.EmpoweredStep,transform.position+Vector3.up,dodgeDirection,transform.position+dodgeDirection*6+Vector3.up,.5f);
            return true;
        }

        void TickDodgeGhosts()
        {
            if(Progress>.76f || Time.unscaledTime<nextGhostAt)return;
            bool perfect=Time.unscaledTime<perfectGhostUntil;
            game.Effects.Afterimage(this,perfect);
            nextGhostAt=Time.unscaledTime+(perfect?.075f:.145f);
        }

        void HitEnemies()
        {
            if(IsMartialMove(Combo) || !HasSwordContact(Combo))return;
            float reach=Combo==StormWheel || Combo==GuardBloom?4.1f:Combo==GatheredPierce || Combo==GuardNeedle || Combo==AirPierce?4.3f:Combo==RollThrust || Combo==ThreadPierce?3.9f:Combo==RollFlash || Combo==WheelFinish?3.7f:Combo==LowReap?3.1f:Combo==MoonChase?4.2f:Combo==FlowingSweep?3.5f:Combo==RisingBridge?3.4f:Combo==BehindThrust?4.4f:Combo==SakuraFollowup?3.9f:Combo==4?4.0f:Combo==25?4.1f:Combo==24?3.6f:Combo==22?2.4f:Combo==11?3.9f:Combo==12?3.6f:Combo==2 || Combo==5?3.7f:Action==FighterAction.Heavy?3.2f:2.75f;
            reach*=1+Mathf.Min(.4f,.08f*AffixStacks(TrialAffix.Reach));
            for(int i=0;i<game.Enemies.Count;i++)if(game.Enemies[i].Champion)game.Enemies[i].Champion.CutHazards(transform.position,transform.forward,reach,Action==FighterAction.Heavy);
            foreach(var enemy in game.Opponents(this))
            {
                if(!enemy.Alive || struck.Contains(enemy)) continue;
                var delta=enemy.transform.position-transform.position;
                if(enemy is TrialEnemy airborne && airborne.Champion && Mathf.Abs(delta.y)>2.25f)continue;
                delta.y=0;
                float radius=enemy is TrialEnemy foe?foe.BodyRadius:.28f;
                if(delta.magnitude>reach+radius || Vector3.Angle(transform.forward,delta)>(Combo==StormWheel || Combo==GuardBloom || Combo==WheelFinish?165:Combo==RollFlash || Combo==LowReap || Combo==7 || Combo==24 || Combo==FlowingSweep?150:Combo==RollThrust || Combo==ThreadPierce || Combo==2 || Combo==5 || Combo==25 || Combo==MoonChase?36:85)) continue;
                if(Physics.Linecast(transform.position+Vector3.up,enemy.transform.position+Vector3.up,1,QueryTriggerInteraction.Ignore)) continue;
                struck.Add(enemy);
                float multiplier=Combo==StormWheel?2.4f:Combo==GuardBloom?1.7f:Combo==GatheredPierce || Combo==GuardNeedle || Combo==AirPierce?1.6f:Combo==WheelFinish?2.1f:Combo==RollFlash?1.9f:Combo==RollThrust || Combo==ThreadPierce?1.35f:Combo==LowReap?1.1f:Combo==AirCross || Combo==AirWheel?1.2f:Combo==MoonChase?1.85f:Combo==FlowingSweep?1.7f:Combo==RisingBridge?1.8f:Combo==BehindThrust?1.9f:Combo==20?3.1f:Combo==21?2.5f:Combo==22?1.35f:Combo==23?1.5f:Combo==24?2.6f:Combo==25?2.4f:Combo==19?2.8f:Combo==18?2.3f:Combo==17?3.25f:Combo==15?2.0f:Combo==16?1.65f:Combo==14 && enemy is TrialEnemy broken && broken.Vulnerable?5.5f:Combo==13?2.65f:Combo==12?1.8f:Combo==11?1.4f:Combo==9?1.5f:Combo==10?2.1f:Action==FighterAction.Heavy?(Combo>=4?2.25f:1.9f):Combo==7?1.15f:Combo==3?1.65f:Combo==2?1.3f:1;
                if(enemy.ReceiveHit(this,34*multiplier*(Boon==TrialBoon.Edge?1.12f:1),(Action==FighterAction.Heavy?56:26)*(Boon==TrialBoon.Breaker?1.15f:1)))
                {
                    RestoreCombatStamina(enemy.Alive?HitStamina:KillStamina);
                    bool heavy=Action==FighterAction.Heavy || Combo==9;
                    bool finish=IsFinishingCut;
                    HoldCombat(heavy?(finish?.12f:.095f):finish?.075f:Combo==3?.055f:.035f);if(!(enemy is TrialEnemy boss && boss.ResolveActive))enemy.HoldCombat(!enemy.Alive || finish?.13f:heavy?.10f:.075f);
                    EmitCombat(!enemy.Alive?TrialCombatCue.Kill:finish?TrialCombatCue.FinisherHit:heavy?TrialCombatCue.HeavyHit:TrialCombatCue.Hit,enemy.transform.position+Vector3.up);
                }
            }
        }

        public void ToggleLock()
        {
            if(game.IsArena)DuelTarget=Locked?null:game.NearestOpponent(this,18) as TrialPlayer;
            else Target=Locked?null:game.NearestEnemy(transform.position,18);
        }

        public override bool ReceiveHit(TrialFighter attacker,float damage,float postureDamage,bool unblockable=false)
        {
            if(!Alive) return false;
            if(ChoosingBoon)return false;
            if(attacker is TrialPlayer player && (!game.IsArena || player==this))return false;
            if(game.IsArena && SpawnProtected && attacker!=null)return false;
            if(attacker is TrialEnemy enemySource)damage*=enemySource.RunDamageScale*game.EnemyDamageScale*(enemySource.Champion?enemySource.Champion.KillDamageScale:enemySource.Boss && enemySource.Boss.Boon==TrialBossBoon.RagingTide?1.08f:1);
            if(Invulnerable)
            {
                SuccessfulDodges++;
                float dodgeAge=RollThrustEvading?DodgeInvulnerabilityEnd-rollThrustEvasionTime+ActionTime:ActionTime;
                if((Action==FighterAction.Dodge || RollThrustEvading) && !perfectDodgeAwarded && dodgeAge<=.24f)
                {perfectDodgeAwarded=true;RestoreCombatStamina(PerfectDodgeStamina);blinkReadyUntil=Time.time+3;EmitCombat(TrialCombatCue.PerfectDodge,transform.position+Vector3.up);}
                return false;
            }
            if(Time.time<hurtUntil)return false;
            Vector3 sourceDirection=receivingHazard?hazardSourceDirection:attacker?attacker.transform.position-transform.position:transform.forward;
            bool frontal=Vector3.Angle(transform.forward,Vector3.ProjectOnPlane(sourceDirection,Vector3.up))<100;
            bool deflectable=!receivingHazard && attacker is TrialEnemy bossSource && (bossSource.Boss && bossSource.Boss.CanBeDeflected || bossSource.Champion && bossSource.Champion.CanDeflect);
            // A committed light-to-heavy cut can intercept a readable frontal strike.
            if(Action==FighterAction.Heavy && Combo==15 && Progress>=.08f && Progress<=.30f && frontal &&
               !receivingHazard && attacker is TrialEnemy intercepted && (!unblockable || deflectable) && Time.time>=nextReversalAt && Spend(12))
            {
                nextReversalAt=Time.time+2;SuccessfulParries++;intercepted.Parried();
                HoldCombat(.09f);intercepted.HoldCombat(.12f);RestoreCombatStamina(ParryStamina);
                EmitCombat(TrialCombatCue.CutThrough,transform.position+Vector3.up);return false;
            }
            if(Guarding && frontal && (!unblockable || deflectable && GuardAge<.20f))
            {
                if(GuardAge<.20f && Spend(8))
                {
                    SuccessfulParries++;RunParry();
                    RestoreCombatStamina(ParryStamina);
                    if(attacker is TrialEnemy enemy) enemy.Parried();
                    else if(attacker is TrialPlayer rival)rival.ReceiveParry();
                    if(TrySwordGuardReturn())return false;
                    EmpowerParry();HoldCombat(.115f);attacker?.HoldCombat(.14f);CombatFeedback(TrialCombatCue.Parry);
                    if(attacker is TrialEnemy cameraTarget && cameraTarget.Champion && this==game.Player)game.CameraRig.CombatFrame(cameraTarget,.28f,.45f);
                    // The player chooses the empowered follow-up after the deflect.
                    return false;
                }
                if(Spend(Mathf.Clamp(damage*.42f,10,24)))
                {
                    Health=Mathf.Max(1,Health-damage*.04f);HoldCombat(.03f);CombatFeedback(TrialCombatCue.Block);return false;
                }
                Stamina=0;CombatFeedback(TrialCombatCue.GuardBreak);
            }
            if(!game.IsArena && attacker is TrialEnemy mercySource && !(mercySource.Champion && mercySource.Champion.IsRemnant))
            {
                float healthRatio=Health/MaxHealth;
                damage*=Mathf.Lerp(.68f,1,Mathf.Clamp01(healthRatio/.30f));
                if(!lastStandSpent && Health>MaxHealth*.12f && damage>=Health)
                {damage=Health-1;lastStandSpent=true;hurtUntil=Time.time+.65f;}
            }
            damage=RunDefense(damage);
            ClearDodgeLink();Impact(attacker,damage);RunRetaliate(attacker);
            if(Mirror.NetworkServer.active)TrialNetworkManager.Current.BroadcastCombat(this,TrialCombatCue.Hurt,null,sourceDirection.normalized);
            else game.Effects.PlayCombat(new TrialCombatEvent{cue=TrialCombatCue.Hurt,position=transform.position,forward=sourceDirection.normalized},this==game.Player);
            HoldCombat(.045f);
            hurtUntil=Mathf.Max(hurtUntil,Time.time+.42f);
            PlanarVelocity=Vector3.zero;
            ClearAttackInputs();chainUntil=0;weaveUntil=0;
            if(Alive) {Begin(FighterAction.Stagger,.32f);recoveryDelay=.5f;}
            else
            {
                Deaths++;if(game.IsArena && attacker is TrialPlayer killer && killer!=this)killer.Kills++;
                if(attacker is TrialEnemy killerBoss && killerBoss.Champion)killerBoss.Champion.AwardPlayerKill();
                CombatFeedback(TrialCombatCue.Death);
            }
            return true;
        }

        void RestoreCombatStamina(float amount)
        {
            Stamina=Mathf.Min(MaxStamina,Stamina+amount);
            recoveryDelay=0;
            sprintExhausted=false;
        }

        void ReceiveParry(){ClearAttackInputs();chainUntil=0;Begin(FighterAction.Stagger,.8f);PlanarVelocity=Vector3.zero;recoveryDelay=1;}
        void BeginReversal(TrialEnemy target)
        {
            reversalTarget=target;nextReversalAt=Time.time+3;Combo=10;struck.Clear();Guarding=false;chainCount=0;
            bufferedLight=bufferedHeavy=bufferedDodge=bufferedHeal=-10;Turn(target.transform.position-transform.position,36000,.02f);
            Begin(FighterAction.Heavy,.82f);EmitCombat(TrialCombatCue.Reversal,transform.position+Vector3.up);
        }
        void EmitCombat(TrialCombatCue cue,Vector3 point)
        {
            if(Mirror.NetworkServer.active)TrialNetworkManager.Current.BroadcastCombat(this,cue,point);
            else game.Effects.PlayCombat(new TrialCombatEvent{cue=cue,position=point,forward=transform.forward,player=0},this==game.Player);
        }

        void CombatFeedback(TrialCombatCue cue)
        {
            if(Mirror.NetworkServer.active)TrialNetworkManager.Current.BroadcastCombat(this,cue);
            else game.Effects.PlayCombat(new TrialCombatEvent{cue=cue,position=transform.position,forward=transform.forward},this==game.Player);
        }

        public void Restore(Vector3 checkpoint)
        {
            ClearDodgeLink();
            combatInputOrder=bufferedDodgeOrder=0;
            guardWasHeld=guardPressedThisTick=false;
            ClearAttackInputs();weaveUntil=0;attackGuardLatch=false;
            commandLightAt=commandHeavyAt=commandDodgeAt=commandHealAt=-10;
            ClearCombatHold();ResetSwordArts();ResetParryArts();
            motor.enabled=true;Teleport(checkpoint);Health=MaxHealth;Stamina=MaxStamina;Flasks=3;
            hitFlash=0;foreach(var r in renderers)if(r)r.SetPropertyBlock(null);if(trail){trail.Clear();trail.emitting=false;}
            Posture=0;Target=null;DuelTarget=null;protectedUntil=0;chainCount=0;chainUntil=0;bufferedLight=bufferedDodge=bufferedHeavy=bufferedHeal=bufferedJump=-10;GuardAge=100;
            blinkReadyUntil=0;reversalTarget=null;nextReversalAt=0;chaseTarget=null;chaseRemaining=0;perfectGhostUntil=0;
            PlanarVelocity=Vector3.zero;LocalMotion=Vector2.zero;MoveBlend=0;Sprinting=false;sprintExhausted=false;TurnLean=0;hurtUntil=0;Begin(FighterAction.Free,0);
            combatImpulse=Vector3.zero;
            recoveryDelay=0;perfectDodgeAwarded=false;lastStandSpent=false;chargeTime=0;
            if(this==game.Player && game.Hud)game.Hud.ResetDamageFeedback();
            transform.rotation=Quaternion.identity;
            animationRig.ResetSwordPresentation();
        }

        public override TrialActorState CaptureNetwork(int id)
        {var state=base.CaptureNetwork(id);state.comboGrace=Mathf.Max(0,chainUntil-Time.time);state.comboHeavy=chainHeavy;state.weaveGrace=Mathf.Max(0,weaveUntil-Time.time);state.weaveFrom=(byte)weaveFrom;state.weaveHeavy=weaveHeavy;state.guardComboDelay=weavePending?Mathf.Max(.001f,GuardComboWindup-weaveAge):0;state.orbitSwordTime=RecallFlight!=null?TrialParryFlight.Duration(TrialCombatCue.SpiralSword,1)-RecallFlight.age:0;state.orbitSwordPosition=OrbitSwordPosition;state.swordReturnOrigin=swordReturnOrigin;state.swordLanding=swordLanded?swordLandingAge+.001f:0;state.landingSakuraFlow=landingSakuraFlow;state.airVolleyDescending=AirVolleyDescending;state.swordCharge=SwordCharge;state.dragonLanded=dodgeDragonLanded;state.parryReady=Mathf.Max(0,parryReadyUntil-Time.time);state.dodgeFollowup=Mathf.Max(0,parryDodgeUntil-Time.time);state.dodgeDirection=dodgeDirection;CaptureRoguelike(ref state);state.flasks=(byte)Flasks;state.kills=Kills;state.deaths=Deaths;state.protection=Mathf.Max(0,protectedUntil-Time.time);state.boon=Boon;state.offerA=OfferA;state.offerB=OfferB;state.offerC=OfferC;return state;}
        public override void ApplyNetwork(TrialActorState state,float dt)
        {
            bool respawned=!Alive && state.health>0;
            base.ApplyNetwork(state,dt);Flasks=state.flasks;Sprinting=state.movement>1.1f;Kills=state.kills;Deaths=state.deaths;protectedUntil=Time.time+state.protection;
            dodgeDirection=state.dodgeDirection;swordLanded=state.swordLanding>0;swordLandingAge=Mathf.Max(0,state.swordLanding-.001f);landingSakuraFlow=state.landingSakuraFlow;replicaVolleyDescending=state.airVolleyDescending;
            chainUntil=state.comboGrace>0?Time.time+state.comboGrace:0;chainHeavy=state.comboHeavy;chainCount=state.combo;
            weaveUntil=state.weaveGrace>0?Time.time+state.weaveGrace:0;weaveFrom=state.weaveFrom;weaveHeavy=state.weaveHeavy;weavePending=state.guardComboDelay>0;weaveAge=GuardComboWindup-state.guardComboDelay;
            replicaSwordUntil=state.orbitSwordTime>0?Time.time+state.orbitSwordTime:0;replicaSwordPosition=state.orbitSwordPosition;swordReturnOrigin=state.swordReturnOrigin;
            dragonPower=state.swordCharge;dodgeDragonLanded=state.dragonLanded;parryReadyUntil=Time.time+state.parryReady;parryDodgeUntil=Time.time+state.dodgeFollowup;
            ApplyRoguelike(state);
            if(respawned)animationRig.ResetSwordPresentation();
            bool wasChoosing=ChoosingBoon;Boon=state.boon;OfferA=state.offerA;OfferB=state.offerB;OfferC=state.offerC;
            if(wasChoosing && !ChoosingBoon && this==game.Player)game.FinishBoonChoice();
            if(Action==FighterAction.Dodge)TickDodgeGhosts();
            else if((Combo==9 || Combo==10) && (Action==FighterAction.Light || Action==FighterAction.Heavy) && Progress<.6f && Time.unscaledTime>=nextGhostAt){game.Effects.Afterimage(this);nextGhostAt=Time.unscaledTime+(Application.isMobilePlatform?.14f:.10f);}
            if(this==game.Player && game.Input.Lock)ToggleLock();
        }
    }
}
