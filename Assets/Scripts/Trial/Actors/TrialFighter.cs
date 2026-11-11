using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    public enum FighterAction { Free, Light, Heavy, Dodge, Heal, Stagger, Dead, Charge }

    public abstract class TrialFighter : MonoBehaviour
    {
        public float Health { get; protected set; }
        public float MaxHealth { get; protected set; }
        public float Stamina { get; protected set; }
        public float MaxStamina { get; protected set; }
        public float Posture { get; protected set; }
        public float PostureLimit { get; protected set; } = 100;
        public FighterAction Action { get; protected set; }
        public float ActionTime { get; protected set; }
        public float ActionDuration { get; protected set; }
        public int Combo { get; protected set; }
        public float MoveBlend { get; protected set; }
        public bool Guarding { get; protected set; }
        float combatFreezeUntil;
        public bool CombatFrozen=>Time.unscaledTime<combatFreezeUntil;
        public void HoldCombat(float seconds){combatFreezeUntil=Mathf.Max(combatFreezeUntil,Time.unscaledTime+seconds);}
        protected void ClearCombatHold(){combatFreezeUntil=0;}
        public bool Alive => Health > 0;
        public float Progress => ActionDuration > 0 ? Mathf.Clamp01(ActionTime / ActionDuration) : 0;
        public bool AttackActive => (!(this is TrialPlayer) || TrialPlayer.HasSwordContact(Combo)) && (Action == FighterAction.Light || Action == FighterAction.Heavy || (this is TrialPlayer && Action==FighterAction.Dodge && Combo==7)) && Progress >= (this is TrialPlayer && Action==FighterAction.Light?.22f:.33f) && Progress <= (this is TrialPlayer && Action==FighterAction.Light?.54f:.63f);
        public bool Invulnerable
        {
            get
            {
                if(this is TrialEnemy enemy && (enemy.BreakoutInvulnerable || enemy.Champion &&
                    (enemy.Champion.Evading || enemy.Champion.Introducing || enemy.Champion.TerraceChangingPhase)))return true;
                var player=this as TrialPlayer;
                if(Action==FighterAction.Dodge)
                    return ActionTime>=(player?player.DodgeSafeStart:.025f) && ActionTime<=(player?player.DodgeSafeEnd:TrialPlayer.DodgeInvulnerabilityEnd);
                return player && (player.RollThrustEvading || Action==FighterAction.Heavy && Combo==TrialPlayer.SwordGuardReturn && Progress<.68f ||
                    Action==FighterAction.Light && (Combo==9 || Combo==TrialPlayer.BehindThrust) && Progress<.32f ||
                    Action==FighterAction.Heavy && Combo==10 && Progress<.48f);
            }
        }
        public Transform Visual { get; protected set; }
        public Transform Weapon { get; protected set; }
        protected TrialGame game;
        protected CharacterController motor;
        protected TrailRenderer trail;
        protected float recoveryDelay;
        protected float verticalSpeed;
        protected float hitFlash;
        float spellControlRemaining;
        protected bool SpellControlled => spellControlRemaining>0;
        internal void ApplySpellControl(float duration)
        {
            if(!Alive || !motor.enabled)return;
            if(this is TrialEnemy enemy)enemy.InterruptForSpell();
            else if(this is TrialPlayer player)player.InterruptForSpell();
            spellControlRemaining=Mathf.Max(spellControlRemaining,duration);
            Begin(FighterAction.Stagger,spellControlRemaining);
            StopLocomotion();
        }
        protected bool TickSpellControl(float dt)
        {
            if(!Alive || !SpellControlled)return false;
            TickBase(dt);
            if(!Alive)return true;
            spellControlRemaining-=dt;
            if(spellControlRemaining<=0 && Alive)Begin(FighterAction.Free,0);
            return true;
        }
        protected void ClearSpellControl()=>spellControlRemaining=0;
        protected Renderer[] renderers;
        protected MaterialPropertyBlock flashProperties;
        Color[] authoredEmission;
        protected virtual float StaminaRecoveryRate => Guarding?0:29;
        protected virtual float StaminaRecoveryDelay => .7f;
        protected virtual bool CanRecoverStamina => Action==FighterAction.Free;
        protected virtual bool CanRecoverPosture => true;

        public virtual TrialActorState CaptureNetwork(int id) => new TrialActorState
        {
            id=id,position=transform.position,yaw=transform.eulerAngles.y,health=Health,maxHealth=MaxHealth,stamina=Stamina,maxStamina=MaxStamina,posture=Posture,postureLimit=PostureLimit,
            action=(byte)Action,actionTime=ActionTime,duration=ActionDuration,combo=(byte)Combo,movement=MoveBlend,guard=Guarding
        };
        public virtual void ApplyNetwork(TrialActorState state,float dt)
        {
            float previousHealth=Health;var previousAction=Action;int previousCombo=Combo;
            if(motor)motor.enabled=false;
            // The owning peer should converge promptly; the old 45 ms smoothing
            // time constant added visible delay on top of the round trip.
            bool local=game && game.Player==this;
            bool swordBlink=this is TrialPlayer && state.combo==TrialPlayer.SwordGuardReturn && (Combo!=TrialPlayer.SwordGuardReturn || Action!=FighterAction.Heavy || state.actionTime<ActionTime);
            float t=swordBlink || (transform.position-state.position).sqrMagnitude>16?1:1-Mathf.Exp(-(local?60:30)*dt);
            transform.position=Vector3.Lerp(transform.position,state.position,t);
            transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.Euler(0,state.yaw,0),t);
            Health=state.health;if(state.maxHealth>0)MaxHealth=state.maxHealth;if(state.postureLimit>0)PostureLimit=state.postureLimit;Stamina=state.stamina;Posture=state.posture;
            if(state.maxStamina>0)MaxStamina=state.maxStamina;
            Action=(FighterAction)state.action;ActionTime=state.actionTime;ActionDuration=state.duration;
            Combo=state.combo;MoveBlend=Health>0?state.movement:0;Guarding=Health>0 && state.guard;
            if(trail)trail.emitting=AttackActive;
            if(game && game.Effects)
            {
                if(Health<previousHealth)HoldCombat(Alive?.04f:.09f);
                if(Health<previousHealth && !state.spellExecuted){game.Effects.Burst(transform.position+Vector3.up,new Color(1,.65f,.23f),18);game.Effects.Sound(Alive?"hit":"death",transform.position);}
                if((Action==FighterAction.Light||Action==FighterAction.Heavy)&&(Action!=previousAction||Combo!=previousCombo))
                {
                    if(this is TrialPlayer)game.Effects.MoveSound(Combo,Action==FighterAction.Heavy,transform.position);
                    else game.Effects.Sound("swing",transform.position);
                }
            }
        }

        protected void Configure(TrialGame owner, float health, float stamina, float radius, float height)
        {
            game = owner;
            flashProperties = new MaterialPropertyBlock();
            MaxHealth = Health = health;
            MaxStamina = Stamina = stamina;
            gameObject.layer = 2; // Actors are excluded from the camera's world-only collision mask.
            motor = gameObject.AddComponent<CharacterController>();
            motor.center = Vector3.up * (height * .5f);
            motor.height = height;
            motor.radius = radius;
            motor.stepOffset = .28f;
            motor.skinWidth = .035f;
            motor.slopeLimit = 48;
        }

        internal static void DisableShadows(Transform visual)
        {
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;
            }
        }

        protected void Begin(FighterAction next, float duration)
        {
            Action = next; ActionTime = 0; ActionDuration = duration;
            Guarding = false;
        }

        internal void AdvanceResultPose(float dt)
        {
            if(CombatFrozen || ActionTime>=ActionDuration || (Action!=FighterAction.Light && Action!=FighterAction.Heavy))return;
            ActionTime=Mathf.Min(ActionTime+dt,ActionDuration);
            if(trail)trail.emitting=AttackActive;
        }

        protected void TickBase(float dt)
        {
            ActionTime += dt;
            recoveryDelay -= dt;
            if (recoveryDelay <= 0 && CanRecoverStamina)
                Stamina = Mathf.Min(MaxStamina, Stamina + StaminaRecoveryRate * Mathf.Min(dt,-recoveryDelay));
            if (CanRecoverPosture)
                Posture = Mathf.Max(0, Posture - 7 * dt);
            TickGravity(dt);
            if (trail != null) trail.emitting = AttackActive;
            if (hitFlash > 0)
            {
                hitFlash = Mathf.Max(0,hitFlash-dt);
                SetHitEmission(new Color(2,.58f,.17f) * (hitFlash / .16f));
            }
            if (transform.position.y < -8 && Alive) ReceiveHit(null, 9999, 0, true);
        }

        protected virtual void TickGravity(float dt)
        {
            verticalSpeed = motor.isGrounded ? -2 : verticalSpeed - 24 * dt;
            motor.Move(Vector3.up * (verticalSpeed * dt));
        }

        protected void SetHitEmission(Color pulse)
        {
            if(authoredEmission==null || authoredEmission.Length!=renderers.Length)
            {
                authoredEmission=new Color[renderers.Length];
                for(int i=0;i<renderers.Length;i++)
                {
                    var material=renderers[i]?renderers[i].sharedMaterial:null;
                    authoredEmission[i]=material && material.HasProperty("_EmissionColor")?material.GetColor("_EmissionColor"):Color.black;
                }
            }
            for(int i=0;i<renderers.Length;i++)if(renderers[i])
            {
                renderers[i].GetPropertyBlock(flashProperties);
                flashProperties.SetColor("_EmissionColor",authoredEmission[i]+pulse);
                renderers[i].SetPropertyBlock(flashProperties);
            }
        }

        protected bool Spend(float amount)
        {
            if (Stamina < amount) return false;
            Stamina -= amount; recoveryDelay = StaminaRecoveryDelay; return true;
        }

        internal void StopLocomotion(){MoveBlend=0;}
        protected void Move(Vector3 velocity, float dt) { motor.Move(velocity * dt); }
        public void Teleport(Vector3 point)
        {
            motor.enabled = false; transform.position = point; motor.enabled = true; verticalSpeed = 0;
        }
        protected void Turn(Vector3 direction, float speed, float dt)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), speed * dt);
        }

        // Ordinary grounded swings should pass below the player's 1.25 m leap.
        internal const float GroundMeleeHeight=1.10f;

        // Match vertical contact before consuming a hazard's per-target hit slot.
        public bool OverlapsAttackHeight(float bottom,float top)
        {
            float feet=transform.position.y+motor.center.y-motor.height*.5f;
            return feet<=top+motor.skinWidth && feet+motor.height>=bottom-motor.skinWidth;
        }

        public abstract bool ReceiveHit(TrialFighter attacker, float damage, float postureDamage, bool unblockable = false);
        protected void Impact(TrialFighter attacker, float damage,bool spellExecution=false)
        {
            float previousHealth=Health;
            Health = Mathf.Max(0,Health-damage);
            if (Health <= 0) { MoveBlend=0;Begin(FighterAction.Dead, 1.4f); motor.enabled = false; }
            if(spellExecution)return;
            hitFlash = .16f;
            game.Effects.Burst(transform.position + Vector3.up * 1.05f, new Color(1,.65f,.23f), 18);
            game.Effects.Sound(Health <= 0 ? "death" : "hit", transform.position);
            if(!TrialNetworkManager.SessionActive || this==game.Player || attacker==game.Player)game.CameraRig.Kick(.13f);
            if(this is TrialEnemy runEnemy && attacker is TrialPlayer runPlayer)runPlayer.OnRunHit(runEnemy,previousHealth-Health);
        }
    }
}
