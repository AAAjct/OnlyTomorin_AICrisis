using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    public sealed class TrialPlayer : TrialFighter
    {
        public int Flasks { get; private set; } = 3;
        public TrialEnemy Target { get; private set; }
        public bool Locked => Target != null && Target.Alive;
        public float GuardAge { get; private set; }
        public int SuccessfulDodges { get; private set; }
        public int SuccessfulParries { get; private set; }
        public Vector3 PlanarVelocity { get; private set; }
        public Vector2 LocalMotion { get; private set; }
        public bool Sprinting { get; private set; }
        public float TurnLean { get; private set; }
        readonly HashSet<TrialEnemy> struck = new HashSet<TrialEnemy>();
        float bufferedLight = -10, bufferedDodge = -10, bufferedHeavy = -10;
        const float InputBuffer = .28f;
        bool healingApplied;
        bool sprintExhausted;
        float hurtUntil;
        Vector3 dodgeDirection;

        public void Initialize(TrialGame owner, GameObject model)
        {
            Configure(owner,160,120,.28f,1.72f);
            name = "Tomorin · 剑行者";
            Visual = Instantiate(model,transform).transform;
            Visual.name = "Tomorin";
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
            animation.Pose(FighterAction.Free,0,0,0,false,0);
            trail = Weapon.GetComponentInChildren<TrailRenderer>();
        }

        public void Tick(float dt, TrialInput input)
        {
            if (!Alive) return;
            TickBase(dt);
            if (input.Lock) ToggleLock();
            if (Target && (!Target.Alive || Vector3.Distance(Target.transform.position,transform.position)>21)) Target=null;
            if (input.Attack) bufferedLight=Time.time;
            if (input.Dodge) bufferedDodge=Time.time;
            if (input.Heavy) bufferedHeavy=Time.time;
            if (Action != FighterAction.Free)
            {
                Sprinting=false;
                PlanarVelocity=Vector3.zero;
                if (Action == FighterAction.Dodge) Move(dodgeDirection * Mathf.Lerp(11.8f,3.2f,Progress),dt);
                if (Action == FighterAction.Light || Action == FighterAction.Heavy)
                {
                    if (Progress < .28f) Turn(Locked?Target.transform.position-transform.position:MoveDirection(input),900,dt);
                    if (Progress > .22f && Progress < .55f) Move(transform.forward * (Action==FighterAction.Heavy?3.4f:2.6f),dt);
                    if (AttackActive) HitEnemies();
                    // Dodge cancels recovery, while the attack's wind-up and contact remain committed.
                    if (Progress>(Action==FighterAction.Heavy?.66f:.58f) && Time.time-bufferedDodge<InputBuffer && TryDodge(input)) return;
                    if (Action==FighterAction.Light && Combo<2 && Progress>.76f && Time.time-bufferedLight<.36f && StartAttack(false,Combo+1)) return;
                }
                if (Action == FighterAction.Heal && !healingApplied && Progress>.63f)
                {
                    healingApplied=true; Flasks--; Health=Mathf.Min(MaxHealth,Health+85);
                    game.Effects.Burst(transform.position+Vector3.up,new Color(.35f,1,.74f),30);
                    game.Effects.Sound("heal",transform.position);
                }
                if (ActionTime >= ActionDuration || (Action==FighterAction.Dodge && Progress>.84f && input.Move.sqrMagnitude>.01f))
                {
                    bool combo = Action == FighterAction.Light && Combo<2 && Time.time-bufferedLight<.35f;
                    Begin(FighterAction.Free,0);
                    if (combo) { StartAttack(false,Combo+1); bufferedLight=-10; }
                }
                MoveBlend=Mathf.MoveTowards(MoveBlend,0,dt*5);
                LocalMotion=Vector2.Lerp(LocalMotion,Vector2.zero,1-Mathf.Exp(-18*dt));
                return;
            }
            bool wasGuarding=Guarding;
            Guarding=input.Guard && Stamina>1;
            GuardAge=Guarding ? (wasGuarding?GuardAge+dt:0) : 100;
            if(Time.time-bufferedDodge<InputBuffer && TryDodge(input)) return;
            if(Time.time-bufferedHeavy<InputBuffer && StartAttack(true,0)) return;
            if(Time.time-bufferedLight<InputBuffer && StartAttack(false,0)) return;
            if(input.Heal && Flasks>0 && Health<MaxHealth && !Guarding)
            { healingApplied=false; Begin(FighterAction.Heal,1.25f);return; }
            var direction=MoveDirection(input);
            if(!input.Sprint || Stamina>25)sprintExhausted=false;
            if(Stamina<=1)sprintExhausted=true;
            bool running=input.Sprint && !sprintExhausted && !Guarding && direction.sqrMagnitude>.05f && Stamina>1;
            if(running) {Stamina=Mathf.Max(0,Stamina-8*dt);recoveryDelay=.45f;}
            float speed=Guarding?2.5f:running?7.2f:4.5f;
            var desired=direction*speed;
            float acceleration=direction.sqrMagnitude<.01f?58:Vector3.Dot(PlanarVelocity,direction)<0?72:46;
            PlanarVelocity=Vector3.MoveTowards(PlanarVelocity,desired,acceleration*dt);
            float oldYaw=transform.eulerAngles.y;
            if(Locked) Turn(Target.transform.position-transform.position,960,dt);
            else Turn(direction,1080,dt);
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
            if(!Spend(heavy?35:19)) return false;
            bufferedLight=bufferedHeavy=-10;PlanarVelocity=Vector3.zero;
            Combo=combo;struck.Clear();
            if(!Locked)
            {
                var nearest=game.NearestEnemy(transform.position,4.5f);
                if(nearest && Vector3.Angle(transform.forward,nearest.transform.position-transform.position)<65)
                    transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(nearest.transform.position-transform.position,Vector3.up));
            }
            Begin(heavy?FighterAction.Heavy:FighterAction.Light,heavy?.88f:combo==2?.64f:.52f);
            game.Effects.Sound("swing",transform.position,heavy?.72f:1+combo*.12f);
            return true;
        }

        bool TryDodge(TrialInput input)
        {
            if(!Spend(25)) return false;
            Vector3 forward=input.NetworkDirected?Quaternion.Euler(0,input.NetworkYaw,0)*Vector3.forward:Vector3.ProjectOnPlane(game.CameraRig.transform.forward,Vector3.up).normalized;
            dodgeDirection=(forward*input.Move.y+Vector3.Cross(Vector3.up,forward)*input.Move.x).normalized;
            if(dodgeDirection.sqrMagnitude<.1f) dodgeDirection=-transform.forward;
            else if(!Locked) Turn(dodgeDirection,36000,.02f);
            bufferedDodge=-10;PlanarVelocity=Vector3.zero;Begin(FighterAction.Dodge,.46f);
            game.Effects.Sound("dodge",transform.position);
            return true;
        }

        void HitEnemies()
        {
            float reach=Action==FighterAction.Heavy?3.0f:2.65f;
            foreach(var enemy in game.Enemies)
            {
                if(!enemy.Alive || struck.Contains(enemy)) continue;
                var delta=enemy.transform.position-transform.position;delta.y=0;
                if(delta.magnitude>reach+enemy.BodyRadius || Vector3.Angle(transform.forward,delta)>78) continue;
                if(Physics.Linecast(transform.position+Vector3.up,enemy.transform.position+Vector3.up,1,QueryTriggerInteraction.Ignore)) continue;
                struck.Add(enemy);
                float multiplier=Action==FighterAction.Heavy?1.9f:Combo==2?1.4f:1;
                enemy.ReceiveHit(this,28*multiplier,Action==FighterAction.Heavy?48:21);
                game.CameraRig.Kick(.22f);
                game.Effects.HitStop(.045f);
            }
        }

        public void ToggleLock()
        {
            Target=Locked?null:game.NearestEnemy(transform.position,18);
        }

        public override bool ReceiveHit(TrialFighter attacker,float damage,float postureDamage,bool unblockable=false)
        {
            if(!Alive) return false;
            if(Invulnerable) {SuccessfulDodges++;return false;}
            if(Time.time<hurtUntil)return false;
            bool frontal=attacker==null || Vector3.Angle(transform.forward,attacker.transform.position-transform.position)<85;
            if(Guarding && frontal && !unblockable)
            {
                if(GuardAge<.20f && Spend(8))
                {
                    SuccessfulParries++;
                    if(attacker is TrialEnemy enemy) enemy.Parried();
                    game.Effects.Burst(transform.position+transform.forward+Vector3.up,new Color(1,.86f,.38f),45);
                    game.Effects.Sound("parry",transform.position);game.CameraRig.Kick(.45f);game.Effects.HitStop(.095f);
                    game.Hud.Notify("弹反",.6f);return false;
                }
                if(Spend(30))
                {
                    Health=Mathf.Max(1,Health-damage*.12f);game.Effects.Sound("parry",transform.position,.65f);
                    game.Effects.Burst(transform.position+Vector3.up,new Color(1,.8f,.5f),12);return false;
                }
                Stamina=0;game.Hud.Notify("架势崩溃",1.2f);
            }
            Impact(attacker,damage);
            hurtUntil=Time.time+.42f;
            PlanarVelocity=Vector3.zero;
            if(Alive) {Begin(FighterAction.Stagger,.42f);recoveryDelay=1;}
            else game.OnPlayerDied();
            return true;
        }

        public void Restore(Vector3 checkpoint)
        {
            motor.enabled=true;Teleport(checkpoint);Health=MaxHealth;Stamina=MaxStamina;Flasks=3;
            hitFlash=0;foreach(var r in renderers)if(r)r.SetPropertyBlock(null);if(trail){trail.Clear();trail.emitting=false;}
            Posture=0;Target=null;bufferedLight=bufferedDodge=bufferedHeavy=-10;GuardAge=100;
            PlanarVelocity=Vector3.zero;LocalMotion=Vector2.zero;MoveBlend=0;Sprinting=false;sprintExhausted=false;TurnLean=0;hurtUntil=0;Begin(FighterAction.Free,0);
            transform.rotation=Quaternion.identity;
        }

        public override TrialActorState CaptureNetwork(int id)
        {var state=base.CaptureNetwork(id);state.flasks=(byte)Flasks;return state;}
        public override void ApplyNetwork(TrialActorState state,float dt)
        {
            base.ApplyNetwork(state,dt);Flasks=state.flasks;Sprinting=state.movement>1.1f;
            if(this==game.Player && game.Input.Lock)ToggleLock();
        }
    }
}
