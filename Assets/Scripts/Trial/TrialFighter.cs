using UnityEngine;

namespace TomorinTrial
{
    public enum FighterAction { Free, Light, Heavy, Dodge, Heal, Stagger, Dead }

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
        public bool AttackActive => (Action == FighterAction.Light || Action == FighterAction.Heavy || (this is TrialPlayer && Action==FighterAction.Dodge && Combo==7)) && Progress >= .33f && Progress <= .63f;
        public bool Invulnerable => Action == FighterAction.Dodge && ActionTime >= .025f && ActionTime <= .32f || this is TrialPlayer && (Action==FighterAction.Light && Combo==9 && Progress<.32f || Action==FighterAction.Heavy && Combo==10 && Progress<.48f);
        public Transform Visual { get; protected set; }
        public Transform Weapon { get; protected set; }
        protected TrialGame game;
        protected CharacterController motor;
        protected TrailRenderer trail;
        protected float recoveryDelay;
        protected float verticalSpeed;
        protected float hitFlash;
        protected Renderer[] renderers;
        protected MaterialPropertyBlock flashProperties;

        public virtual TrialActorState CaptureNetwork(int id) => new TrialActorState
        {
            id=id,position=transform.position,yaw=transform.eulerAngles.y,health=Health,maxHealth=MaxHealth,stamina=Stamina,maxStamina=MaxStamina,posture=Posture,postureLimit=PostureLimit,
            action=(byte)Action,actionTime=ActionTime,duration=ActionDuration,combo=(byte)Combo,movement=MoveBlend,guard=Guarding
        };
        public virtual void ApplyNetwork(TrialActorState state,float dt)
        {
            float previousHealth=Health;var previousAction=Action;int previousCombo=Combo;
            if(motor)motor.enabled=false;
            float t=(transform.position-state.position).sqrMagnitude>16?1:1-Mathf.Exp(-22*dt);
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
                if(Health<previousHealth){game.Effects.Burst(transform.position+Vector3.up,new Color(1,.65f,.23f),18);game.Effects.Sound(Alive?"hit":"death",transform.position);}
                if((Action==FighterAction.Light||Action==FighterAction.Heavy)&&(Action!=previousAction||Combo!=previousCombo))game.Effects.Sound("swing",transform.position);
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

        protected void Begin(FighterAction next, float duration)
        {
            Action = next; ActionTime = 0; ActionDuration = duration;
            Guarding = false;
        }

        protected void TickBase(float dt)
        {
            ActionTime += dt;
            recoveryDelay -= dt;
            if (recoveryDelay <= 0 && Action == FighterAction.Free && !Guarding)
                Stamina = Mathf.Min(MaxStamina, Stamina + 29 * dt);
            Posture = Mathf.Max(0, Posture - 7 * dt);
            verticalSpeed = motor.isGrounded ? -2 : verticalSpeed - 24 * dt;
            motor.Move(Vector3.up * (verticalSpeed * dt));
            if (trail != null) trail.emitting = AttackActive;
            if (hitFlash > 0)
            {
                hitFlash = Mathf.Max(0,hitFlash-dt);
                flashProperties.SetColor("_EmissionColor", new Color(2,.58f,.17f) * (hitFlash / .16f));
                foreach (var r in renderers) if(r) r.SetPropertyBlock(flashProperties);
            }
            if (transform.position.y < -8 && Alive) ReceiveHit(null, 9999, 0, true);
        }

        protected bool Spend(float amount)
        {
            if (Stamina < amount) return false;
            Stamina -= amount; recoveryDelay = .7f; return true;
        }

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

        public abstract bool ReceiveHit(TrialFighter attacker, float damage, float postureDamage, bool unblockable = false);
        protected void Impact(TrialFighter attacker, float damage)
        {
            Health = Mathf.Max(0,Health-damage);
            hitFlash = .16f;
            game.Effects.Burst(transform.position + Vector3.up * 1.05f, new Color(1,.65f,.23f), 18);
            game.Effects.Sound(Health <= 0 ? "death" : "hit", transform.position);
            if(!TrialNetworkManager.SessionActive || this==game.Player || attacker==game.Player)game.CameraRig.Kick(.13f);
            if (Health <= 0) { MoveBlend=0;Begin(FighterAction.Dead, 1.4f); motor.enabled = false; }
        }
    }
}
