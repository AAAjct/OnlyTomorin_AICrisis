using UnityEngine;

namespace TomorinTrial
{
    public sealed class TrialEnemy : TrialFighter
    {
        public bool IsBoss { get; private set; }
        public float BodyRadius => IsBoss?.75f:.38f;
        public bool Awakened { get; private set; }
        public bool PhaseTwo => IsBoss && Health < MaxHealth * .55f;
        public string DisplayName => IsBoss?"大肥鱼 · DeepSeek":"蚀影剑卫";
        bool unblockable;
        public bool Unblockable { get => Boss?Boss.Unblockable:unblockable; private set=>unblockable=value; }
        public bool WindingUp => Boss?Boss.WindingUp:(Action==FighterAction.Light || Action==FighterAction.Heavy) && Progress<.33f;
        public TrialBoss Boss { get; private set; }
        public bool Vulnerable => Action==FighterAction.Stagger;
        public Vector3 Spawn { get; private set; }
        Transform torso,head,rightShoulder,leftShoulder,arm,forearm,leftArm,thighL,thighR;
        Quaternion armBind,forearmBind;
        float cooldown,orbitAge,gait;
        bool attackLanded,phaseAnnounced;
        int attackIndex,chain;
        Material warningMaterial;
        Renderer eye;

        public void Initialize(TrialGame owner,Vector3 position,bool boss,GameObject bossModel=null)
        {
            IsBoss=boss;Spawn=position;transform.position=position;
            Configure(owner,boss?960:105,100,boss?.72f:.34f,boss?2.7f:1.95f);
            name=DisplayName;
            if(boss)
            {
                if(!bossModel)throw new System.InvalidOperationException("DeepSeek model is required for the boss encounter.");
                Boss=gameObject.AddComponent<TrialBoss>();Boss.Initialize(this,owner,bossModel);
                Visual=Boss.Visual;renderers=Visual.GetComponentsInChildren<Renderer>();
                transform.rotation=Quaternion.Euler(0,180,0);return;
            }
            float scale=boss?1.3f:1;
            Visual=new GameObject("Armored skeleton").transform;Visual.SetParent(transform,false);Visual.localScale=Vector3.one*scale;
            var world=owner.World;
            torso=TrialWorld.Part("Cuirass",PrimitiveType.Cube,Visual,new Vector3(0,1.16f,0),new Vector3(.65f,.72f,.35f),world.Armor);
            TrialWorld.Part("Chest sigil",PrimitiveType.Cube,torso,new Vector3(0,.1f,-.52f),new Vector3(.28f,.45f,.05f),world.Crimson);
            head=TrialWorld.Part("Faceless helm",PrimitiveType.Sphere,Visual,new Vector3(0,1.78f,0),new Vector3(.43f,.49f,.39f),world.Armor);
            TrialWorld.Part("Crown",PrimitiveType.Cube,Visual,new Vector3(0,1.98f,0),new Vector3(.60f,.09f,.28f),world.Gold);
            var visor=TrialWorld.Part("Ember visor",PrimitiveType.Cube,Visual,new Vector3(0,1.82f,.195f),new Vector3(.29f,.035f,.03f),world.RedGlow);eye=visor.GetComponent<Renderer>();
            for(int i=0;i<5;i++)
            {
                var skirt=TrialWorld.Part("Lamellar",PrimitiveType.Cube,Visual,new Vector3((i-2)*.135f,.72f,0),new Vector3(.125f,.40f,.41f),i%2==0?world.Armor:world.DarkWood);
                skirt.localRotation=Quaternion.Euler(0,0,(i-2)*-6);
            }
            thighL=Joint("Hip.L",Visual,new Vector3(-.19f,.76f,0));thighR=Joint("Hip.R",Visual,new Vector3(.19f,.76f,0));
            foreach(var leg in new[]{thighL,thighR})
            {
                TrialWorld.Part("Greave",PrimitiveType.Cube,leg,new Vector3(0,-.31f,0),new Vector3(.22f,.62f,.23f),world.Armor);
                TrialWorld.Part("Boot",PrimitiveType.Cube,leg,new Vector3(0,-.65f,.08f),new Vector3(.25f,.14f,.40f),world.DarkWood);
                TrialWorld.Part("Gold edge",PrimitiveType.Cube,leg,new Vector3(0,-.25f,.125f),new Vector3(.07f,.40f,.025f),world.Gold);
            }
            rightShoulder=Joint("Shoulder.R",Visual,new Vector3(.42f,1.48f,0));leftShoulder=Joint("Shoulder.L",Visual,new Vector3(-.42f,1.48f,0));
            TrialWorld.Part("Pauldron",PrimitiveType.Cube,rightShoulder,Vector3.zero,new Vector3(.37f,.20f,.48f),world.Gold);
            TrialWorld.Part("Pauldron",PrimitiveType.Cube,leftShoulder,Vector3.zero,new Vector3(.37f,.20f,.48f),world.Gold);
            arm=Joint("Sword arm",rightShoulder,Vector3.zero);forearm=Joint("Sword wrist",arm,new Vector3(0,-.35f,0));
            TrialWorld.Part("Upper arm",PrimitiveType.Cube,arm,new Vector3(0,-.18f,0),new Vector3(.19f,.36f,.22f),world.Armor);
            TrialWorld.Part("Forearm",PrimitiveType.Cube,forearm,new Vector3(0,-.18f,0),new Vector3(.18f,.36f,.21f),world.Armor);
            leftArm=Joint("Offhand",leftShoulder,Vector3.zero);
            TrialWorld.Part("Off arm",PrimitiveType.Cube,leftArm,new Vector3(0,-.32f,.06f),new Vector3(.19f,.66f,.23f),world.Armor);
            Weapon=TrialWorld.Sword(forearm,world.Steel,world.Gold,world.RedGlow,true);
            Weapon.localPosition=new Vector3(0,-.36f,0);Weapon.localRotation=Quaternion.Euler(80,0,0);Weapon.localScale=Vector3.one*(boss?1.35f:1.05f);
            trail=Weapon.GetComponentInChildren<TrailRenderer>();
            armBind=Quaternion.Euler(-30,0,-10);forearmBind=Quaternion.Euler(-50,0,0);
            renderers=Visual.GetComponentsInChildren<Renderer>();
            cooldown=1.1f;transform.rotation=Quaternion.Euler(0,180,0);
        }

        static Transform Joint(string label,Transform parent,Vector3 position)
        {var t=new GameObject(label).transform;t.SetParent(parent,false);t.localPosition=position;return t;}

        public void Tick(float dt)
        {
            if(Boss)
            {
                if(!Alive){ActionTime+=dt;Boss.Tick(dt);return;}
                TickBase(dt);
                if(!Awakened && game.GateOpen && Vector3.Distance(game.CombatTarget.transform.position,transform.position)<15)
                {Awakened=true;}
                Boss.Tick(dt);return;
            }
            if(!Alive) {ActionTime+=dt;Animate(dt);return;}
            TickBase(dt);cooldown-=dt;
            var target=game.CombatTarget;
            if(!target.Alive) {MoveBlend=0;Animate(dt);return;}
            var delta=target.transform.position-transform.position;delta.y=0;
            float distance=delta.magnitude;
            if(!Awakened && distance<(IsBoss?10:8.5f) && (!IsBoss || game.GateOpen))
            {Awakened=true;}
            if(!Awakened) {Animate(dt);return;}
            if(PhaseTwo && !phaseAnnounced)
            {phaseAnnounced=true;game.Hud.Notify("二阶段",1.2f);game.Effects.Burst(transform.position+Vector3.up,Color.red,60);}
            if(Action==FighterAction.Stagger)
            {if(ActionTime>ActionDuration)Begin(FighterAction.Free,0);Animate(dt);return;}
            if(Action==FighterAction.Light || Action==FighterAction.Heavy)
            {
                if(Progress<.25f)Turn(delta,IsBoss?100:155,dt);
                if(Progress>.26f && Progress<.53f)Move(transform.forward*(IsBoss?4.5f:3),dt);
                if(AttackActive && !attackLanded && distance<(IsBoss?3.5f:2.5f) && Vector3.Angle(transform.forward,delta)<(Unblockable?115:65))
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
                Animate(dt);return;
            }
            Turn(delta,180,dt);
            float desired=IsBoss?3.1f:2.15f;
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
            Animate(dt);
        }

        void Animate(float dt)
        {
            gait+=dt*7*MoveBlend;
            thighL.localRotation=Quaternion.Euler(Mathf.Sin(gait)*25*MoveBlend,0,0);
            thighR.localRotation=Quaternion.Euler(-Mathf.Sin(gait)*25*MoveBlend,0,0);
            arm.localRotation=armBind;forearm.localRotation=forearmBind;
            torso.localRotation=Quaternion.identity;leftArm.localRotation=Quaternion.Euler(-12,0,12);
            Visual.localPosition=Vector3.up*(Mathf.Abs(Mathf.Sin(gait))*.025f*MoveBlend);
            if(Action==FighterAction.Light || Action==FighterAction.Heavy)
            {
                float swing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.24f,.57f,Progress));
                arm.localRotation=Quaternion.Euler(Mathf.Lerp(-150,40,swing),Mathf.Lerp(-35,40,swing)*(Combo==0?1:-1),-18);
                forearm.localRotation=Quaternion.Euler(Mathf.Lerp(-65,-20,swing),0,0);
                torso.localRotation=Quaternion.Euler(0,Mathf.Lerp(-28,32,swing),0);
            }
            if(Action==FighterAction.Stagger)torso.localRotation=Quaternion.Euler(-24*Mathf.Sin(Progress*Mathf.PI),0,8);
            if(Action==FighterAction.Dead)Visual.localRotation=Quaternion.Euler(Mathf.SmoothStep(0,87,Progress),0,-12*Progress);
            else Visual.localRotation=Quaternion.identity;
            if(eye) eye.sharedMaterial=WindingUp?(Unblockable?game.World.RedGlow:game.World.Glow):game.World.RedGlow;
        }

        public void ApplyReplica(TrialActorState state,float dt)
        {ApplyNetwork(state,dt);Awakened=state.awake;if(!Boss)Animate(dt);}

        public override bool ReceiveHit(TrialFighter attacker,float damage,float postureDamage,bool unblockable=false)
        {
            if(!Alive)return false;
            bool exposed=Action==FighterAction.Stagger;
            Impact(attacker,damage*(exposed?1.65f:1));
            if(!Alive) {game.OnEnemyDied(this);return true;}
            Awakened=true;Posture+=postureDamage;
            if(Posture>=100 || (!IsBoss && Action==FighterAction.Free))
            {Posture=0;Begin(FighterAction.Stagger,IsBoss?2.0f:.55f);cooldown=.6f;if(Boss)Boss.CancelAttack();}
            return true;
        }
        public void Parried()
        {Posture=0;Begin(FighterAction.Stagger,IsBoss?2.5f:2.0f);cooldown=.6f;if(Boss)Boss.CancelAttack();}
        internal void BossBegin(FighterAction next,float duration)=>Begin(next,duration);
        internal void BossMove(Vector3 velocity,float dt)=>Move(velocity,dt);
        internal void BossFace(Vector3 direction,float speed,float dt)=>Turn(direction,speed,dt);
        internal void BossMotion(float movement){MoveBlend=Mathf.MoveTowards(MoveBlend,movement,Time.deltaTime*7);}
        public void ResetFighter()
        {
            Health=MaxHealth;Stamina=MaxStamina;Posture=0;Awakened=false;phaseAnnounced=false;
            hitFlash=0;foreach(var r in renderers)if(r)r.SetPropertyBlock(null);if(trail){trail.Clear();trail.emitting=false;}
            attackIndex=chain=0;cooldown=1.1f;Unblockable=false;motor.enabled=true;Teleport(Spawn);
            transform.rotation=Quaternion.Euler(0,180,0);Visual.localRotation=Quaternion.identity;Begin(FighterAction.Free,0);
            MoveBlend=0;if(Boss)Boss.ResetBoss();
        }
    }
}
