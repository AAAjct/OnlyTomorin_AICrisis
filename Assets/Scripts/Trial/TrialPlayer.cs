using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialPlayer : TrialFighter
    {
        public const float BaseHealth=200;
        public const float BaseStamina=280;
        public const float PerfectDodgeStamina=40;
        public const float ParryStamina=30;
        public const float HitStamina=6;
        public const float KillStamina=45;
        protected override float StaminaRecoveryRate => Guarding?20:PlanarVelocity.sqrMagnitude<.25f?84:62;
        protected override float StaminaRecoveryDelay => .32f;
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
        public float TurnLean { get; private set; }
        readonly HashSet<TrialFighter> struck = new HashSet<TrialFighter>();
        float bufferedLight = -10, bufferedDodge = -10, bufferedHeavy = -10, bufferedHeal=-10;
        const float InputBuffer = .36f;
        int chainCount;
        float chainUntil;
        float chargeTime;
        bool chargeReady,attackAccent;
        public float ChargeAmount => Action==FighterAction.Charge?Mathf.Clamp01(ActionTime/.85f):0;
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
            if(!Alive)return;Begin(FighterAction.Stagger,.72f);
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

        public void Initialize(TrialGame owner, GameObject model)
        {
            Configure(owner,BaseHealth,BaseStamina,.28f,1.72f);
            name = "Tomorin · 剑行者";
            Visual = Instantiate(model,transform).transform;
            Visual.name = "Tomorin";
            GhostSkins=Visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var a in Visual.GetComponentsInChildren<Animator>()) a.enabled = false;
            foreach (var c in Visual.GetComponentsInChildren<Collider>()) Destroy(c);
            var meshes = Visual.GetComponentsInChildren<Renderer>();
            var bounds = meshes[0].bounds;
            foreach (var r in meshes) bounds.Encapsulate(r.bounds);
            float scale = 1.68f / bounds.size.y;
            Visual.localScale *= scale;
            bounds = meshes[0].bounds;
            foreach (var r in meshes) bounds.Encapsulate(r.bounds);
            Visual.position += new Vector3(transform.position.x-bounds.center.x,transform.position.y-bounds.min.y,transform.position.z-bounds.center.z);
            renderers = meshes;
            var animation = gameObject.AddComponent<TrialRig>();
            animation.Initialize(this,Visual);
            Weapon = TrialWorld.Sword(animation.RightHand != null ? animation.RightHand : Visual,owner.World.Steel,owner.World.Gold,owner.World.Glow,false);
            animation.SetWeapon(Weapon);
            var flask=new GameObject("灵露 · 手持药瓶").transform;flask.SetParent(transform,false);
            TrialWorld.Part("Jade bottle",PrimitiveType.Sphere,flask,Vector3.zero,new Vector3(.12f,.17f,.12f),owner.World.Water);
            TrialWorld.Part("Brass neck",PrimitiveType.Cylinder,flask,new Vector3(0,.10f,0),new Vector3(.06f,.035f,.06f),owner.World.Gold);
            TrialWorld.Part("Spirit dew",PrimitiveType.Sphere,flask,new Vector3(0,-.025f,.01f),new Vector3(.10f,.10f,.10f),owner.World.Glow);
            animation.SetFlask(flask);flask.gameObject.SetActive(false);
            animation.Pose(FighterAction.Free,0,0,0,false,0);
            trail = Weapon.GetComponentInChildren<TrailRenderer>();
        }

        public void Tick(float dt, TrialInput input)
        {
            if (!Alive || ChoosingBoon || CombatFrozen) return;
            TickBase(dt);
            if(combatImpulse.sqrMagnitude>.01f){if(!Invulnerable)Move(combatImpulse,dt);combatImpulse=Vector3.MoveTowards(combatImpulse,Vector3.zero,dt*38);}
            if (input.Lock) ToggleLock();
            if (Target && (!Target.Alive || Vector3.Distance(Target.transform.position,transform.position)>21)) Target=null;
            if (DuelTarget && (!DuelTarget.Alive || Vector3.Distance(DuelTarget.transform.position,transform.position)>21)) DuelTarget=null;
            if (input.Attack) bufferedLight=Time.time;
            if (input.Dodge) bufferedDodge=Time.time;
            if (input.Heavy) bufferedHeavy=Time.time;
            if (input.Heal) bufferedHeal=Time.time;
            if(Action==FighterAction.Charge)
            {
                PlanarVelocity=Vector3.MoveTowards(PlanarVelocity,MoveDirection(input)*2.2f,40*dt);Move(PlanarVelocity,dt);
                Turn(Locked?LockTarget.transform.position-transform.position:MoveDirection(input),540,dt);
                chargeTime=ActionTime;
                if(!chargeReady && chargeTime>=.65f){chargeReady=true;EmitCombat(TrialCombatCue.ChargeReady,transform.position+Vector3.up);}
                if(Time.time-bufferedDodge<InputBuffer && TryDodge(input)){chargeTime=0;return;}
                if(input.Guard){chargeTime=0;Begin(FighterAction.Free,0);Guarding=true;GuardAge=0;return;}
                if(!input.HeavyHeld || chargeTime>=1.25f)
                {int branch=chargeTime>=.65f?17:0;chargeTime=0;if(!StartAttack(true,branch))Begin(FighterAction.Free,0);}
                return;
            }
            if (Action != FighterAction.Free)
            {
                Sprinting=false;
                if(Action==FighterAction.Heal)TickLocomotion(dt,input,true);
                else if(Action!=FighterAction.Light || Progress<=.54f)PlanarVelocity=Vector3.zero;
                if (Action == FighterAction.Dodge)
                {
                    Move(dodgeDirection * Mathf.Lerp(11.8f,3.2f,Progress),dt);
                    if(Progress<.8f && Time.unscaledTime>=nextGhostAt){game.Effects.Afterimage(this);nextGhostAt=Time.unscaledTime+.10f;}
                }
                if (Action == FighterAction.Light || Action == FighterAction.Heavy)
                {
                    bool light=Action==FighterAction.Light;
                    if(Progress<(light?.30f:.28f) || Progress>.54f)
                        Turn(Locked?LockTarget.transform.position-transform.position:MoveDirection(input),light?1440:1080,dt);
                    if(light && Progress>.54f)
                    {
                        PlanarVelocity=Vector3.MoveTowards(PlanarVelocity,MoveDirection(input)*3.8f,52*dt);
                        Move(PlanarVelocity,dt);
                    }
                    if(Combo==9 && Progress>.10f && Progress<.33f)
                    {float step=18*dt;if(blinkTarget)step=Mathf.Min(step,Mathf.Max(0,Vector3.ProjectOnPlane(blinkTarget.transform.position-transform.position,Vector3.up).magnitude-1.1f));Move(blinkDirection*(step/Mathf.Max(.001f,dt)),dt);}
                    else if(Combo==10 && reversalTarget && Progress>.12f && Progress<.36f && (reversalTarget.transform.position-transform.position).sqrMagnitude>3.2f)Move(transform.forward*11,dt);
                    else if (Progress > .22f && Progress < .55f) Move(transform.forward * (Action==FighterAction.Heavy?3.4f:2.6f),dt);
                    if((Combo==9 || Combo==10) && Progress<.6f && Time.unscaledTime>=nextGhostAt){game.Effects.Afterimage(this);nextGhostAt=Time.unscaledTime+(Application.isMobilePlatform?.12f:.045f);}
                    if(Combo==17 && Progress>=.43f && !attackAccent)
                    {attackAccent=true;EmitCombat(TrialCombatCue.HeavyLand,transform.position+transform.forward*.7f);}
                    if (AttackActive) HitEnemies();
                    // Preserve contact, then accept defensive cancels and buffered branches.
                    if (Progress>(Action==FighterAction.Heavy?.56f:.40f) && Time.time-bufferedDodge<InputBuffer && TryDodge(input)) return;
                    if ((Progress>(light?.40f:.56f) || light && Progress<.12f) && input.Guard && Stamina>1)
                    { Begin(FighterAction.Free,0); Guarding=true; GuardAge=0; return; }
                    if (Action==FighterAction.Light && Progress>.55f)
                    {
                        if(Time.time-bufferedHeavy<InputBuffer && StartAttack(true,Combo+1))return;
                        if(Time.time-bufferedLight<InputBuffer && StartAttack(false,Combo<3?Combo+1:0))return;
                    }
                    if(Progress>(light?.62f:.76f) && TryHeal(dt,input))return;
                }
                if(Action==FighterAction.Heavy && Progress>.72f)
                {
                    if(Time.time-bufferedLight<InputBuffer && StartAttack(false,0))return;
                    if(Time.time-bufferedHeavy<InputBuffer && StartAttack(true,Combo==0?18:Combo==18?19:0))return;
                }
                if(Action==FighterAction.Dodge)
                {
                    if(Combo==7 && Progress>.32f && Progress<.64f)HitEnemies();
                    if(Progress>.68f)
                    {
                        if(Time.time-bufferedLight<InputBuffer && StartAttack(false,8))return;
                        if(Time.time-bufferedHeavy<InputBuffer && StartAttack(true,13))return;
                        if(input.Guard && Stamina>1){Begin(FighterAction.Free,0);Guarding=true;GuardAge=0;return;}
                    }
                }
                if (Action == FighterAction.Heal && !healingApplied && Progress>.63f)
                {
                    healingApplied=true; Flasks--; Health=Mathf.Min(MaxHealth,Health+85+(Boon==TrialBoon.SpiritDew?12:0));
                    RestoreCombatStamina(MaxStamina);
                    game.Effects.Burst(transform.position+Vector3.up,new Color(.35f,1,.74f),30);
                    EmitCombat(TrialCombatCue.Heal,transform.position);
                }
                if (ActionTime >= ActionDuration || (Action==FighterAction.Light && Progress>.72f && input.Move.sqrMagnitude>.01f) || (Action==FighterAction.Dodge && Progress>.84f && input.Move.sqrMagnitude>.01f))
                {
                    if(Action==FighterAction.Light){chainCount=Combo<3?Combo+1:0;chainUntil=Time.time+.30f;}
                    Begin(FighterAction.Free,0);
                }
                if(Action!=FighterAction.Heal)
                {
                    MoveBlend=Mathf.MoveTowards(MoveBlend,0,dt*5);
                    LocalMotion=Vector2.Lerp(LocalMotion,Vector2.zero,1-Mathf.Exp(-18*dt));
                }
                return;
            }
            bool wasGuarding=Guarding;
            Guarding=input.Guard && Stamina>1;
            GuardAge=Guarding ? (wasGuarding?GuardAge+dt:0) : 100;
            if(Time.time-bufferedDodge<InputBuffer && TryDodge(input)) return;
            if(Time.time-bufferedHeavy<InputBuffer)
            {
                bufferedHeavy=-10;
                if(input.HeavyHeld && !Guarding && Spend(8))
                {protectedUntil=0;chargeTime=0;chargeReady=false;Combo=0;Begin(FighterAction.Charge,1.25f);return;}
                if(StartAttack(true,Time.time<chainUntil?chainCount:0))return;
            }
            if(Time.time-bufferedLight<InputBuffer && StartAttack(false,Time.time<chainUntil?chainCount:0)) return;
            if(TryHeal(dt,input))return;
            TickLocomotion(dt,input,false);
        }

        bool TryHeal(float dt,TrialInput input)
        {
            if(Time.time-bufferedHeal>=InputBuffer || Flasks<=0 || Health>=MaxHealth && Stamina>=MaxStamina || Guarding)return false;
            bufferedHeal=bufferedLight=bufferedHeavy=-10;healingApplied=false;
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
            var desired=direction*speed;
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
            var executionTarget=Locked?LockTarget as TrialEnemy:game.NearestEnemy(transform.position,3.5f);
            if(heavy && combo==0 && executionTarget && executionTarget.Champion && executionTarget.Vulnerable)combo=14;
            else if(combo==0 && Guarding)combo=11;
            else if(combo==0 && Sprinting)combo=12;
            if(!Spend(combo==14?40:heavy?28:15)) return false;
            protectedUntil=0;
            bufferedLight=bufferedHeavy=bufferedHeal=-10;PlanarVelocity=Vector3.zero;
            Combo=heavy && Action==FighterAction.Light && Combo<=3?15:
                !heavy && Action==FighterAction.Heavy?16:heavy && combo>0 && combo<4?combo+3:combo;
            if(!heavy && Time.time<blinkReadyUntil){Combo=9;blinkReadyUntil=0;}
            chainCount=0;chainUntil=0;struck.Clear();attackAccent=false;
            if(!Locked)
            {
                var nearest=game.NearestOpponent(this,4.5f);
                if(nearest && Vector3.Angle(transform.forward,nearest.transform.position-transform.position)<65)
                    transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(nearest.transform.position-transform.position,Vector3.up));
            }
            if(Combo==9)
            {
                var target=Locked?LockTarget:game.NearestOpponent(this,8);
                blinkTarget=target;
                blinkDirection=target?Vector3.ProjectOnPlane(target.transform.position-transform.position,Vector3.up).normalized:transform.forward;
                Turn(blinkDirection,36000,.02f);game.Effects.Afterimage(this);EmitCombat(TrialCombatCue.BlinkSlash,transform.position+Vector3.up);
            }
            Begin(heavy?FighterAction.Heavy:FighterAction.Light,Combo==19?.94f:Combo==18?.78f:Combo==17?1.0f:Combo==15?.66f:Combo==16?.46f:Combo==14?1.12f:Combo==13?.90f:Combo==12?.68f:Combo==11?.58f:Combo==9?.64f:heavy?(Combo>=4?.76f:.84f):combo==3?.48f:combo==8?.36f:.38f);
            if(Combo==14 && executionTarget && this==game.Player)game.CameraRig.CombatFrame(executionTarget,.8f,1);
            game.Effects.Sound("swing",transform.position,heavy?.72f:1+Mathf.Clamp(combo,0,3)*.06f);
            return true;
        }

        bool TryDodge(TrialInput input)
        {
            if(!Spend(DodgeCost)) return false;
            Vector3 forward=input.NetworkDirected?Quaternion.Euler(0,input.NetworkYaw,0)*Vector3.forward:Vector3.ProjectOnPlane(game.CameraRig.transform.forward,Vector3.up).normalized;
            dodgeDirection=(forward*input.Move.y+Vector3.Cross(Vector3.up,forward)*input.Move.x).normalized;
            if(dodgeDirection.sqrMagnitude<.1f) dodgeDirection=-forward.normalized;
            else if(!Locked) Turn(dodgeDirection,36000,.02f);
            bool derivative=Action==FighterAction.Light && Combo>=1 && Combo<=3;
            chainCount=0;chainUntil=0;struck.Clear();Combo=derivative?7:0;
            perfectDodgeAwarded=false;
            bufferedDodge=bufferedLight=bufferedHeavy=bufferedHeal=-10;PlanarVelocity=Vector3.zero;Begin(FighterAction.Dodge,.44f);
            if(derivative && this==game.Player)game.Hud.Notify("回风斩 · 闪避衍生",.8f);
            EmitCombat(TrialCombatCue.Dodge,transform.position);
            return true;
        }

        void HitEnemies()
        {
            float reach=Combo==11?3.9f:Combo==12?3.6f:Combo==5?3.7f:Action==FighterAction.Heavy?3.2f:2.75f;
            if(game.IsChampionStage)for(int i=0;i<game.Enemies.Count;i++)game.Enemies[i].Champion.CutHazards(transform.position,transform.forward,reach,Action==FighterAction.Heavy);
            foreach(var enemy in game.Opponents(this))
            {
                if(!enemy.Alive || struck.Contains(enemy)) continue;
                var delta=enemy.transform.position-transform.position;
                if(enemy is TrialEnemy airborne && airborne.Champion && Mathf.Abs(delta.y)>2.25f)continue;
                delta.y=0;
                float radius=enemy is TrialEnemy foe?foe.BodyRadius:.28f;
                if(delta.magnitude>reach+radius || Vector3.Angle(transform.forward,delta)>(Combo==7?150:Combo==5?42:85)) continue;
                if(Physics.Linecast(transform.position+Vector3.up,enemy.transform.position+Vector3.up,1,QueryTriggerInteraction.Ignore)) continue;
                struck.Add(enemy);
                float multiplier=Combo==19?2.8f:Combo==18?2.3f:Combo==17?3.25f:Combo==15?2.0f:Combo==16?1.65f:Combo==14 && enemy is TrialEnemy broken && broken.Vulnerable?5.5f:Combo==13?2.65f:Combo==12?1.8f:Combo==11?1.4f:Combo==9?1.5f:Combo==10?2.1f:Action==FighterAction.Heavy?(Combo>=4?2.25f:1.9f):Combo==7?1.15f:Combo==3?1.65f:Combo==2?1.3f:1;
                if(enemy.ReceiveHit(this,34*multiplier*(Boon==TrialBoon.Edge?1.12f:1),(Action==FighterAction.Heavy?56:26)*(Boon==TrialBoon.Breaker?1.15f:1)))
                {
                    RestoreCombatStamina(enemy.Alive?HitStamina:KillStamina);
                    bool heavy=Action==FighterAction.Heavy || Combo==9;
                    HoldCombat(heavy?.075f:.045f);if(!(enemy is TrialEnemy boss && boss.ResolveActive))enemy.HoldCombat(!enemy.Alive?.09f:heavy?.085f:.055f);
                    EmitCombat(!enemy.Alive?TrialCombatCue.Kill:heavy?TrialCombatCue.HeavyHit:TrialCombatCue.Hit,enemy.transform.position+Vector3.up);
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
            if(attacker is TrialEnemy enemySource)damage*=game.EnemyDamageScale*(enemySource.Boss && enemySource.Boss.Boon==TrialBossBoon.RagingTide?1.08f:1);
            if(Invulnerable)
            {
                SuccessfulDodges++;
                if(Action==FighterAction.Dodge && !perfectDodgeAwarded && ActionTime<=.24f)
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
                    SuccessfulParries++;
                    RestoreCombatStamina(ParryStamina);
                    if(attacker is TrialEnemy enemy) enemy.Parried();
                    else if(attacker is TrialPlayer rival)rival.ReceiveParry();
                    HoldCombat(.085f);attacker?.HoldCombat(.10f);CombatFeedback(TrialCombatCue.Parry);
                    if(attacker is TrialEnemy cameraTarget && cameraTarget.Champion && this==game.Player)game.CameraRig.CombatFrame(cameraTarget,.28f,.45f);
                    if(deflectable && attacker is TrialEnemy fishTarget && fishTarget.Boss && Time.time>=nextReversalAt)BeginReversal(fishTarget);
                    return false;
                }
                if(Spend(Mathf.Clamp(damage*.42f,10,24)))
                {
                    Health=Mathf.Max(1,Health-damage*.04f);HoldCombat(.03f);CombatFeedback(TrialCombatCue.Block);return false;
                }
                Stamina=0;CombatFeedback(TrialCombatCue.GuardBreak);
            }
            if(!game.IsArena && attacker is TrialEnemy)
            {
                float healthRatio=Health/MaxHealth;
                damage*=Mathf.Lerp(.68f,1,Mathf.Clamp01(healthRatio/.30f));
                if(!lastStandSpent && Health>MaxHealth*.12f && damage>=Health)
                {damage=Health-1;lastStandSpent=true;hurtUntil=Time.time+.65f;}
            }
            Impact(attacker,damage);
            if(Mirror.NetworkServer.active)TrialNetworkManager.Current.BroadcastCombat(this,TrialCombatCue.Hurt,null,sourceDirection.normalized);
            else game.Effects.PlayCombat(new TrialCombatEvent{cue=TrialCombatCue.Hurt,position=transform.position,forward=sourceDirection.normalized},this==game.Player);
            HoldCombat(.045f);
            hurtUntil=Mathf.Max(hurtUntil,Time.time+.42f);
            PlanarVelocity=Vector3.zero;
            if(Alive) {Begin(FighterAction.Stagger,.32f);recoveryDelay=.5f;}
            else
            {
                Deaths++;if(game.IsArena && attacker is TrialPlayer killer && killer!=this)killer.Kills++;
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

        void ReceiveParry(){Begin(FighterAction.Stagger,.8f);PlanarVelocity=Vector3.zero;recoveryDelay=1;}
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
            ClearCombatHold();
            motor.enabled=true;Teleport(checkpoint);Health=MaxHealth;Stamina=MaxStamina;Flasks=3;
            hitFlash=0;foreach(var r in renderers)if(r)r.SetPropertyBlock(null);if(trail){trail.Clear();trail.emitting=false;}
            Posture=0;Target=null;DuelTarget=null;protectedUntil=0;chainCount=0;chainUntil=0;bufferedLight=bufferedDodge=bufferedHeavy=bufferedHeal=-10;GuardAge=100;
            blinkReadyUntil=0;reversalTarget=null;nextReversalAt=0;
            PlanarVelocity=Vector3.zero;LocalMotion=Vector2.zero;MoveBlend=0;Sprinting=false;sprintExhausted=false;TurnLean=0;hurtUntil=0;Begin(FighterAction.Free,0);
            combatImpulse=Vector3.zero;
            recoveryDelay=0;perfectDodgeAwarded=false;lastStandSpent=false;chargeTime=0;
            transform.rotation=Quaternion.identity;
        }

        public override TrialActorState CaptureNetwork(int id)
        {var state=base.CaptureNetwork(id);state.dodgeDirection=dodgeDirection;state.flasks=(byte)Flasks;state.kills=Kills;state.deaths=Deaths;state.protection=Mathf.Max(0,protectedUntil-Time.time);state.boon=Boon;state.offerA=OfferA;state.offerB=OfferB;state.offerC=OfferC;return state;}
        public override void ApplyNetwork(TrialActorState state,float dt)
        {
            base.ApplyNetwork(state,dt);Flasks=state.flasks;Sprinting=state.movement>1.1f;Kills=state.kills;Deaths=state.deaths;protectedUntil=Time.time+state.protection;
            dodgeDirection=state.dodgeDirection;
            bool wasChoosing=ChoosingBoon;Boon=state.boon;OfferA=state.offerA;OfferB=state.offerB;OfferC=state.offerC;
            if(wasChoosing && !ChoosingBoon && this==game.Player)game.FinishBoonChoice();
            if((Action==FighterAction.Dodge && Progress<.8f || (Combo==9 || Combo==10) && (Action==FighterAction.Light || Action==FighterAction.Heavy) && Progress<.6f) && Time.unscaledTime>=nextGhostAt){game.Effects.Afterimage(this);nextGhostAt=Time.unscaledTime+(Application.isMobilePlatform?.14f:.10f);}
            if(this==game.Player && game.Input.Lock)ToggleLock();
        }
    }
}
