using UnityEngine;

namespace TomorinTrial
{
    // Six authored styles use each imported character's own deform skeleton.
    // Slots: hips,spine,chest,head,L arm,elbow,hand,R arm,elbow,hand,L thigh,knee,foot,R thigh,knee,foot.
    public sealed class TrialChampionRig : MonoBehaviour
    {
        [SerializeField] ChampionKind kind;
        [SerializeField] Transform model;
        [SerializeField] Transform[] joints;
        [SerializeField] Quaternion[] bind;
        [SerializeField] Vector3 modelOrigin;
        [SerializeField] Quaternion modelRotation;
        [SerializeField] Vector3 leftFootRest,rightFootRest;
        [SerializeField] Quaternion leftFootRotation,rightFootRotation;
        [SerializeField] float strideCycle=2.6f;
        [SerializeField] Material weaponMaterial;
        [SerializeField] TrialSkirtRig skirt;
        public void SetSkirt(TrialSkirtRig value){skirt=value;}
        TrialEnemy fighter;
        TrialChampion brain;
        TrialRig remnantRig;
        Transform actor,weapon,offhand;
        TrailRenderer trail;
        LineRenderer whip;
        readonly Vector3[] whipPoints=new Vector3[25];
        float clock,gait,locomotion;
        [SerializeField] float sole;
        Vector3 lastPosition;
        Vector2 localMotion;
        public Transform[] DrivenBones=>joints;
        public Transform Model=>model;
        public ChampionKind Kind=>kind;
        public Transform WeaponRoot=>weapon;
        public Transform LeftWeapon=>offhand;
        public Transform RightHand=>joints!=null && joints.Length>9?joints[9]:null;
        public void ConfigureRemnant(Transform visual){kind=ChampionKind.Remnant;model=visual;}
        public void Configure(ChampionKind style,Transform visual,Transform[] bones)
        {
            kind=style;model=visual;joints=bones;bind=new Quaternion[joints.Length];
            for(int i=0;i<joints.Length;i++)bind[i]=joints[i]?joints[i].localRotation:Quaternion.identity;
            modelOrigin=model.localPosition;modelRotation=model.localRotation;actor=transform;sole=SupportHeight();
            leftFootRest=transform.InverseTransformPoint(joints[12].position);rightFootRest=transform.InverseTransformPoint(joints[15].position);
            leftFootRotation=Quaternion.Inverse(transform.rotation)*joints[12].rotation;rightFootRotation=Quaternion.Inverse(transform.rotation)*joints[15].rotation;
            strideCycle=Mathf.Clamp(Vector3.Distance(joints[10].position,joints[11].position)*5.8f,1.5f,2.9f);
        }
        public void Initialize(TrialEnemy owner,TrialChampion controller)
        {
            fighter=owner;brain=controller;actor=owner.transform;lastPosition=actor.position;
            foreach(var a in GetComponentsInChildren<Animator>())a.enabled=false;
            foreach(var c in GetComponentsInChildren<Collider>())Destroy(c);
            if(kind==ChampionKind.Remnant)
            {remnantRig=gameObject.AddComponent<TrialRig>();remnantRig.Initialize(owner,model);return;}
            sole=SupportHeight();ResetPose();
        }
        public void ResetPose(){clock=gait=locomotion=0;localMotion=Vector2.zero;if(actor)lastPosition=actor.position;if(remnantRig)remnantRig.Pose(FighterAction.Free,0,0,0,false,0);else Pose(-1,0,0,false,0);if(trail){trail.Clear();trail.emitting=false;}}
        public void CreateWeapon(TrialWorld world,GameObject gun,Material lashMaterial)
        {
            if(kind==ChampionKind.Anon || kind==ChampionKind.Remnant)weapon=TrialWorld.Sword(transform,world.Steel,world.Crimson,world.Glow,true);
            else if(kind==ChampionKind.Taffy)
            {
                weapon=Instantiate(gun,transform).transform;weapon.name="塔菲 · M4 imported weapon";
                foreach(var c in weapon.GetComponentsInChildren<Collider>())Destroy(c);
                var rs=weapon.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
                if(weaponMaterial)foreach(var r in rs){var materials=r.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=weaponMaterial;r.sharedMaterials=materials;}
                weapon.localScale*=.95f/Mathf.Max(b.size.x,b.size.z);
            }
            else if(kind==ChampionKind.Penguin)
            {
                weapon=Instantiate(Resources.Load<GameObject>("Trial/Models/FrostRelic"),transform).transform;
                weapon.name="冰炉遗锤 · forged frost relic";
                foreach(var renderer in weapon.GetComponentsInChildren<Renderer>())
                {
                    var materials=renderer.sharedMaterials;
                    for(int i=0;i<materials.Length;i++)
                    {
                        string role=materials[i]?materials[i].name:renderer.name;
                        materials[i]=role.Contains("Ice")?Resources.Load<Material>("Trial/VFX/FrostIce"):role.Contains("Brass")?world.Gold:role.Contains("Leather")?world.DarkWood:role.Contains("Ember")?world.RedGlow:role.Contains("Steel")?world.Steel:world.Armor;
                    }
                    renderer.sharedMaterials=materials;
                }
            }
            else if(kind==ChampionKind.YaYa)
            {
                weapon=new GameObject("月丝鞭柄").transform;weapon.SetParent(transform,false);
                TrialWorld.Part("Whip grip",PrimitiveType.Cylinder,weapon,Vector3.zero,new Vector3(.045f,.16f,.045f),world.Gold);
                whip=new GameObject("Continuous articulated lash").AddComponent<LineRenderer>();whip.transform.SetParent(transform,false);whip.sharedMaterial=lashMaterial;whip.positionCount=25;
                whip.startColor=new Color(1,.75f,.4f);whip.endColor=new Color(.75f,.5f,1);whip.textureMode=LineTextureMode.Tile;
                whip.useWorldSpace=true;whip.startWidth=.035f;whip.endWidth=.009f;whip.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;whip.receiveShadows=false;
            }
            if(kind==ChampionKind.Anon && !brain.TerraceDuel)
            {offhand=TrialWorld.Sword(transform,world.Steel,world.Crimson,world.Glow,true);offhand.name="爱音 · 左手绯刃";offhand.localScale*=.85f;}
            if(kind==ChampionKind.Anon){PolishSword(weapon,world);PolishSword(offhand,world);}
            if(weapon)trail=weapon.GetComponentInChildren<TrailRenderer>();
            if(trail && brain.TerraceDuel)
            {
                trail.sharedMaterial=lashMaterial;trail.time=.13f;trail.startWidth=.26f;
                trail.startColor=new Color(.88f,.97f,1,.82f);trail.endColor=new Color(.67f,.79f,.85f,0);
            }
            if(remnantRig)remnantRig.SetWeapon(weapon);
        }
        static void PolishSword(Transform root,TrialWorld world)
        {
            var prefab=Resources.Load<GameObject>("Trial/VFX/SignatureSword");if(!root || !prefab)return;
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())renderer.enabled=false;
            var model=Instantiate(prefab,root).transform;model.name="Blender forged petal sword";model.localScale=Vector3.one*.70f;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=renderer.name.Contains("fuller") || renderer.name.Contains("jewel")?world.Crimson:world.Steel;
        }
        public Transform CloneProjectileWeapon(Transform parent)
        {
            if(!weapon)return null;var clone=Instantiate(weapon.gameObject,parent).transform;clone.name="Pooled flying "+kind+" weapon";
            foreach(var t in clone.GetComponentsInChildren<TrailRenderer>()){t.emitting=false;t.enabled=false;}
            clone.gameObject.SetActive(false);return clone;
        }
        void LateUpdate()
        {
            if(kind==ChampionKind.Remnant){if(weapon)weapon.gameObject.SetActive(!brain.WeaponAway);if(trail)trail.emitting=brain.Attacking && !brain.WindingUp && !brain.Recovering;return;}
            if(!fighter || fighter.CombatFrozen || Time.deltaTime<=0)return;
            float dt=Time.deltaTime;clock+=dt;
            Vector3 displacement=TrialChampion.Planar(actor.position-lastPosition);float distance=displacement.magnitude;lastPosition=actor.position;
            if(distance>2){distance=0;displacement=Vector3.zero;}
            gait+=distance*Mathf.PI*2/strideCycle;locomotion=Mathf.MoveTowards(locomotion,Mathf.Clamp01(distance/Mathf.Max(.001f,dt)/5),dt*12);
            Vector3 local=actor.InverseTransformDirection(displacement/Mathf.Max(.001f,dt));
            localMotion=Vector2.Lerp(localMotion,new Vector2(local.x,local.z).normalized,1-Mathf.Exp(-16*dt));
            int move=!fighter.Alive?-7:brain.TerraceChangingPhase?-9:fighter.Vulnerable || fighter.Flinching?-6:brain.Attacking?brain.MoveIndex:fighter.Guarding?-5:-1;
            Pose(move,!fighter.Alive || move==-6?fighter.Progress:move==-9?brain.TerracePhaseProgress:brain.Attacking?brain.AnimationBeat:0,locomotion,fighter.Vulnerable || fighter.Flinching,clock);
            if(trail)trail.emitting=brain.TerraceDuel?brain.TerraceActive:brain.Attacking && !brain.WindingUp && !brain.Recovering;
        }
        public void Pose(int move,float p,float movement,bool stagger,float time)
        {
            if(!model || joints==null)return;if(!actor)actor=transform;
            for(int i=0;i<joints.Length;i++)if(joints[i])joints[i].localRotation=bind[i];
            model.localPosition=modelOrigin;model.localRotation=modelRotation;
            bool dead=move==-7;bool active=move>=0;var attack=brain && brain.TerraceDuel?brain.Move:TrialChampionCatalog.Move(kind,Mathf.Max(0,move));
            float walkCycle=fighter?gait:time*(move==-1 && movement>.8f?14:8.6f);
            float stride=Mathf.Sin(walkCycle)*movement;
            float lean=movement*9,twist=Mathf.Sin(time*2.2f)*2,lift=0;
            Vector3 right=new Vector3(.34f,1.0f,.28f),left=new Vector3(-.34f,1.0f,.24f),angles=new Vector3(15,0,-45);
            if(kind==ChampionKind.FeiBi){right=new Vector3(.30f,.82f,.38f);left=new Vector3(-.30f,.85f,.35f);lean=8;}
            if(kind==ChampionKind.NuoNuo){right=new Vector3(.32f,.92f,.30f);left=new Vector3(-.32f,.89f,.40f);twist=12;}
            if(kind==ChampionKind.Anon){right=new Vector3(.26f,1.03f,.36f);left=right+new Vector3(-.12f,-.08f,-.08f);angles=new Vector3(-20,0,15);}
            if(kind==ChampionKind.Taffy){right=new Vector3(.27f,1.28f,.36f);left=new Vector3(-.13f,1.23f,.58f);angles=Vector3.zero;lean=5;}
            if(kind==ChampionKind.Penguin){right=new Vector3(.38f,.97f,.30f);left=new Vector3(-.28f,1.0f,.35f);angles=new Vector3(15,0,-35);lean=8;}
            if(kind==ChampionKind.YaYa){right=new Vector3(.43f,1.13f,.14f);left=new Vector3(-.34f,1.30f,.1f);angles=new Vector3(35,0,-60);twist=-12;}
            right.z+=stride*.10f;left.z-=stride*.12f;
            bool stepping=!active && !stagger && !dead && move==-1;
            if(stepping)
            {
                Vector3 direction=fighter?new Vector3(localMotion.x,0,localMotion.y):Vector3.forward;if(direction.sqrMagnitude<.01f)direction=Vector3.forward;
                model.localPosition-=Vector3.up*(.045f*movement);
                StepLeg(10,11,12,leftFootRest,leftFootRotation,walkCycle,direction,movement);
                StepLeg(13,14,15,rightFootRest,rightFootRotation,walkCycle+Mathf.PI,direction,movement);
                lean=movement*(kind==ChampionKind.Anon?13:9);twist=-stride*7;
            }
            if(active)
            {
                float wind=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.36f,p));float hit=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.36f,.58f,p));float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.64f,1,p));
                float swing=(-1*wind+2*hit)*fade;
                if(kind==ChampionKind.FeiBi || kind==ChampionKind.NuoNuo)
                {
                    bool kick=move==2 || move==6;right=Vector3.Lerp(new Vector3(.4f,1.3f,.12f),new Vector3(-.10f,1.25f,.93f),hit);left=new Vector3(-.3f,1.3f,.32f);
                    twist=swing*(kind==ChampionKind.FeiBi?48:-55);lean=hit*16;
                    if(kick){Rotate(13,actor.right,-hit*92*fade);Rotate(14,actor.right,hit*24);twist+=swing*45;}
                    if(kind==ChampionKind.NuoNuo){var exchange=right;right=left;left=new Vector3(-exchange.x,exchange.y,exchange.z);}
                }
                else if(brain && brain.TerraceDuel)
                    PoseTerraceCut((TrialChampion.TerraceCut)move,p,wind,hit,fade,movement,walkCycle,ref right,ref left,ref angles,ref lean,ref twist);
                else if(kind==ChampionKind.Anon)
                {
                    bool thrust=attack.shape==ChampionShape.Thrust;
                    right=thrust?Vector3.Lerp(new Vector3(.2f,1.1f,.02f),new Vector3(.1f,1.12f,.9f),hit):Vector3.Lerp(new Vector3(.42f,1.68f,-.12f),new Vector3(-.45f,.70f,.65f),hit);
                    left=right+new Vector3(-.10f,-.08f,-.09f);angles=thrust?new Vector3(90,0,0):Vector3.Lerp(new Vector3(-60,60,-40),new Vector3(140,-85,50),hit);
                    twist=swing*57;lean=swing*15;
                }
                else if(kind==ChampionKind.Taffy)
                {float recoil=Mathf.Sin(hit*Mathf.PI)*.12f;right.z-=recoil;left.z-=recoil;lean=-recoil*45;twist=move==3?swing*48:4;}
                else if(kind==ChampionKind.Penguin)
                {right=Vector3.Lerp(new Vector3(.22f,1.8f,-.1f),new Vector3(.25f,.50f,.75f),hit);left=right+new Vector3(-.18f,-.18f,-.1f);angles=Vector3.Lerp(new Vector3(-35,0,-12),new Vector3(135,0,12),hit);lean=swing*29;twist=move==3?swing*90:swing*12;}
                else
                {right=Vector3.Lerp(new Vector3(.48f,1.6f,-.18f),new Vector3(-.2f,1.05f,.95f),hit);left=new Vector3(-.42f,1.4f,.08f);angles=new Vector3(80,swing*80,10);twist=swing*65;lean=hit*12;}
                if(attack.shape==ChampionShape.Leap || attack.shape==ChampionShape.Flight || attack.shape==ChampionShape.Dive || attack.shape==ChampionShape.TwinPincer)
                {
                    float jump=Mathf.Sin(Mathf.InverseLerp(0,.52f,p)*Mathf.PI);lift=fighter?0:jump*(kind==ChampionKind.YaYa?3.2f:kind==ChampionKind.Penguin?2.7f:2.2f);
                    Rotate(10,actor.right,-jump*55);Rotate(13,actor.right,-jump*65);Rotate(11,actor.right,jump*85);Rotate(14,actor.right,jump*90);lean-=jump*20;
                }
                switch(attack.shape)
                {
                    case ChampionShape.Grab:case ChampionShape.Tether:
                        right=Vector3.Lerp(new Vector3(.42f,1.25f,.12f),new Vector3(.18f,1.22f,.85f),hit);left=Vector3.Lerp(new Vector3(-.42f,1.25f,.12f),new Vector3(-.18f,1.22f,.85f),hit);lean=hit*24;twist=0;break;
                    case ChampionShape.Counter:
                        if(p<.38f){right=new Vector3(.12f,1.32f,.48f);left=new Vector3(-.13f,1.30f,.45f);angles=new Vector3(10,0,42);lean=-3;twist=-12;}break;
                    case ChampionShape.DashCut:
                        right.y-=.15f;lean+=18;Rotate(10,actor.right,-25);Rotate(14,actor.right,50);break;
                    case ChampionShape.Stomp:case ChampionShape.Vacuum:
                        lean=wind*20-hit*30;right=new Vector3(.50f,1.20f+wind*.45f,.05f);left=new Vector3(-.50f,1.20f+wind*.45f,.05f);Rotate(10,actor.right,-wind*65);Rotate(11,actor.right,wind*80);break;
                    case ChampionShape.Boomerang:case ChampionShape.BladeWave:
                        right=Vector3.Lerp(new Vector3(.35f,1.55f,-.18f),new Vector3(.10f,1.25f,.9f),hit);angles=Vector3.Lerp(new Vector3(80,100,-20),new Vector3(80,-80,20),hit);twist=swing*80;break;
                    case ChampionShape.Mine:case ChampionShape.Snare:case ChampionShape.Pillars:
                        right=new Vector3(.25f,.65f,.55f);left=new Vector3(-.25f,.70f,.38f);lean=28;Rotate(11,actor.right,35);Rotate(14,actor.right,35);break;
                    case ChampionShape.SilkLine:case ChampionShape.CrossLine:
                        right=new Vector3(.55f,1.30f,.35f);left=new Vector3(-.55f,1.30f,.35f);twist=swing*48;angles=new Vector3(70,0,-50);break;
                    case ChampionShape.Spiral:
                        right=new Vector3(.60f,1.16f,.50f);left=new Vector3(-.48f,1.20f,.18f);twist=55;angles=new Vector3(85,65,0);
                        StepLeg(10,11,12,leftFootRest,leftFootRotation,walkCycle,new Vector3(1,0,0),movement);StepLeg(13,14,15,rightFootRest,rightFootRotation,walkCycle+Mathf.PI,new Vector3(1,0,0),movement);break;
                    case ChampionShape.SlideVolley:
                        lean=-12;Rotate(10,actor.right,-55);Rotate(11,actor.right,100);Rotate(14,actor.right,65);break;
                    case ChampionShape.OrbitWhip:
                        right=new Vector3(.6f,1.7f,.05f);left=new Vector3(-.45f,1.3f,.1f);twist=time*90%360;break;
                }
            }
            if(move==-3 || move==-4){lift=Mathf.Sin(p*Mathf.PI)*(move==-4?3.2f:1.8f);Rotate(11,actor.right,45);Rotate(14,actor.right,55);right.y=left.y=1.5f;}
            if(move==-5){right=new Vector3(.13f,1.4f,.5f);left=right+new Vector3(-.12f,-.08f,0);angles=new Vector3(15,0,45);}
            if(stagger || move==-6){lean=-28;right.y=.75f;left.y=.7f;twist=10;}
            if(brain && brain.TerraceDuel && move==-6)
            {
                if(fighter.Vulnerable)
                {
                    float buckle=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1,p));
                    lean=33*buckle;twist=-22*buckle;
                    right=Vector3.Lerp(right,new Vector3(.44f,.40f,.40f),buckle);
                    left=Vector3.Lerp(left,new Vector3(-.34f,.38f,.46f),buckle);
                    angles=Vector3.Lerp(angles,new Vector3(168,0,8),buckle);
                    Rotate(10,actor.right,-24*buckle);Rotate(13,actor.right,-32*buckle);
                    Rotate(11,actor.right,42*buckle);Rotate(14,actor.right,54*buckle);
                }
                else
                {
                    float recoil=Mathf.Clamp01(fighter.FlinchRemaining/.16f);
                    lean=-28*recoil;twist=-17*recoil;
                    right=new Vector3(.38f,.94f,.30f-.29f*recoil);
                    left=new Vector3(-.36f,1.03f,.18f+.24f*recoil);
                    Rotate(13,actor.right,22*recoil);
                    Rotate(14,actor.right,24*recoil);
                }
            }
            if(move==-9)
            {
                float liftBlade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.34f,1,p));
                float tap=Mathf.Sin(Mathf.Clamp01(p/.46f)*Mathf.PI*4)*.10f;
                right=Vector3.Lerp(new Vector3(.35f,.51f,.45f),new Vector3(.29f,1.40f,.50f),liftBlade)+Vector3.up*tap;
                left=new Vector3(-.13f,1.12f,.34f);
                angles=Vector3.Lerp(new Vector3(170,10,0),new Vector3(-20,0,15),liftBlade);
                lean=18*(1-liftBlade);
            }
            if(move==-8){right=new Vector3(.65f,1.65f,.1f);left=new Vector3(-.65f,1.65f,.1f);lift=Mathf.Sin(p*Mathf.PI)*.4f;}
            if(brain && brain.Mobility!=ChampionMobility.Grounded)
            {
                float q=brain.MobilityProgress,jump=Mathf.Sin(q*Mathf.PI);
                lift=0; // The authoritative actor follows the jump arc, including its hurtbox.
                lean=brain.Mobility==ChampionMobility.BackLeap?-jump*24:jump*22;
                Rotate(10,actor.right,-jump*48);Rotate(13,actor.right,jump*34);Rotate(11,actor.right,jump*72);Rotate(14,actor.right,jump*55);
            }
            // Anon's waist seam bridges two lower-body bones in the source skin.
            // Keep the bend above the waistband so it follows the pelvis during a lunge.
            float waistBend=kind==ChampionKind.Anon?.08f:.55f;
            Rotate(1,actor.right,lean*waistBend);
            Rotate(2,actor.right,lean*(1-waistBend));
            Rotate(2,Vector3.up,twist);Rotate(3,actor.right,-lean*.4f);
            if(fighter && fighter.Alive && fighter.Recoil.sqrMagnitude>.001f)
            {
                var recoil=actor.InverseTransformDirection(fighter.Recoil);
                Rotate(2,actor.right,-recoil.z*12);Rotate(2,actor.forward,recoil.x*10);
            }
            if(!dead)model.localPosition+=Vector3.up*(sole-SupportHeight()+lift);
            else{float fall=Mathf.SmoothStep(0,1,p);model.localRotation=Quaternion.AngleAxis(-83*fall,Vector3.right)*modelRotation;model.localPosition+=Vector3.down*.12f*fall;}
            if(kind==ChampionKind.Anon && active && (!brain || !brain.TerraceDuel))left=new Vector3(-right.x,right.y-.08f,.3f+(1-p)*.5f);
            Vector3 center=actor.position;Quaternion basis=actor.rotation;
            right+=Vector3.up*lift;left+=Vector3.up*lift;
            TrialRig.SolveArm(J(7),J(8),J(9),center+basis*right,center+basis*new Vector3(.8f,1.0f,-.22f));
            TrialRig.SolveArm(J(4),J(5),J(6),center+basis*left,center+basis*new Vector3(-.8f,1.0f,-.22f));
            if(skirt)skirt.Pose(J(12),J(15));
            if(weapon)
            {
                weapon.gameObject.SetActive(!brain || !brain.WeaponAway && !brain.Introducing);
                weapon.position=J(9)?J(9).position:center+basis*right;weapon.rotation=basis*Quaternion.Euler(angles);
                // The imported M4's muzzle is -Z; combat projectiles travel along +Z.
                if(kind==ChampionKind.Taffy){weapon.rotation*=Quaternion.Euler(0,180,0);weapon.position+=basis*new Vector3(0,-.015f,.12f);}
            }
            if(offhand)
            {
                offhand.gameObject.SetActive(!brain || !brain.Introducing);
                offhand.position=J(6)?J(6).position:center+basis*left;
                offhand.rotation=basis*Quaternion.Euler(angles.x,-angles.y,-angles.z-25);
            }
            if(brain && brain.Introducing)
            {
                float draw=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,.78f,brain.IntroProgress));
                TrialRig.SolveArm(J(7),J(8),J(9),center+basis*new Vector3(.1f,.5f+draw*.8f,.9f),center+basis*new Vector3(.7f,.6f,.4f));
            }
            if(whip)
            {
                Vector3 from=weapon.position;float length=active?(attack.shape==ChampionShape.Annulus?attack.range:Mathf.Lerp(1.5f,attack.range,Mathf.Sin(Mathf.Clamp01(p/.65f)*Mathf.PI))):1.7f;
                for(int i=0;i<25;i++){float t=i/24f;float yaw=active && attack.shape==ChampionShape.Annulus?p*360+t*280:Mathf.Sin(time*3+t*4)*18;Vector3 dir=Quaternion.AngleAxis(yaw,Vector3.up)*actor.forward;whipPoints[i]=from+dir*length*t+actor.right*Mathf.Sin(t*9-time*12)*.20f*t+Vector3.down*Mathf.Sin(t*Mathf.PI)*.32f;}
                whip.SetPositions(whipPoints);
            }
        }
        void PoseTerraceCut(TrialChampion.TerraceCut cut,float p,float wind,float hit,float fade,float movement,float cycle,
            ref Vector3 right,ref Vector3 left,ref Vector3 angles,ref float lean,ref float twist)
        {
            Vector3 ready=new(.26f,1.03f,.36f);
            Vector3 draw=new(.45f,1.23f,.03f), contact=new(-.47f,1.16f,.74f);
            Vector3 bladeDraw=new(-20,72,-32), bladeContact=new(72,-84,30);
            float bodyTurn=54, crouch=10;
            left=new Vector3(-.35f,1.06f,.25f);

            switch(cut)
            {
                case TrialChampion.TerraceCut.Dash:
                    draw=new Vector3(.49f,.62f,-.13f);contact=new Vector3(-.27f,1.63f,.71f);
                    bladeDraw=new Vector3(159,30,-18);bladeContact=new Vector3(-36,-62,18);
                    crouch=24;
                    StepLeg(10,11,12,leftFootRest,leftFootRotation,cycle,Vector3.forward,movement);
                    StepLeg(13,14,15,rightFootRest,rightFootRotation,cycle+Mathf.PI,Vector3.forward,movement);
                    break;
                case TrialChampion.TerraceCut.Return:
                    draw=new Vector3(-.15f,1.27f,.42f);contact=new Vector3(.56f,1.11f,.70f);
                    bladeDraw=new Vector3(35,-82,38);bladeContact=new Vector3(85,78,-29);bodyTurn=-47;
                    break;
                case TrialChampion.TerraceCut.Delay:
                    draw=new Vector3(.23f,1.72f,-.14f);contact=new Vector3(-.29f,.73f,.78f);
                    bladeDraw=new Vector3(-70,35,-23);bladeContact=new Vector3(135,-18,25);crouch=19;
                    break;
                case TrialChampion.TerraceCut.Side:
                    float side=brain.TerraceSideDirection;
                    draw=new Vector3(side*.44f,.88f,.06f);contact=new Vector3(-side*.44f,1.05f,.72f);
                    bladeDraw=new Vector3(72,side*84,0);bladeContact=new Vector3(84,-side*75,0);bodyTurn=side*59;
                    StepLeg(10,11,12,leftFootRest,leftFootRotation,cycle,new Vector3(side,0,.15f),movement);
                    StepLeg(13,14,15,rightFootRest,rightFootRotation,cycle+Mathf.PI,new Vector3(side,0,.15f),movement);
                    break;
                case TrialChampion.TerraceCut.Brace:
                    wind=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.085f,p));
                    draw=contact=new Vector3(.12f,1.34f,.47f);
                    bladeDraw=bladeContact=new Vector3(15,0,68);bodyTurn=-8;crouch=2;
                    break;
                case TrialChampion.TerraceCut.Chase:
                case TrialChampion.TerraceCut.FinalThrust:
                    hit=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.31f,.415f,p));
                    draw=new Vector3(.34f,.94f,-.16f);contact=new Vector3(.09f,1.06f,.95f);
                    bladeDraw=new Vector3(103,10,-10);bladeContact=new Vector3(90,0,0);
                    left=Vector3.Lerp(new Vector3(-.20f,1.20f,.42f),new Vector3(-.30f,.95f,-.18f),hit);
                    bodyTurn=35;crouch=28+13*hit;
                    float run=movement*(1-hit);
                    StepLeg(10,11,12,leftFootRest,leftFootRotation,cycle,Vector3.forward,run);
                    StepLeg(13,14,15,rightFootRest,rightFootRotation,cycle+Mathf.PI,Vector3.forward,run);
                    Rotate(10,actor.right,-28*hit*fade);Rotate(11,actor.right,47*hit*fade);
                    Rotate(13,actor.right,24*hit*fade);Rotate(14,actor.right,17*hit*fade);
                    break;
                case TrialChampion.TerraceCut.Wheel:
                    float turnSide=brain.TerraceSideDirection;
                    draw=new Vector3(turnSide*.44f,.72f,-.26f);contact=new Vector3(-turnSide*.52f,.83f,.62f);
                    bladeDraw=new Vector3(83,turnSide*126,-18);
                    bladeContact=new Vector3(90,-turnSide*119,16);
                    left=new Vector3(-turnSide*.39f,.78f,-.08f);
                    bodyTurn=turnSide*96;crouch=42;
                    Vector3 crossing=new Vector3(turnSide,0,.65f).normalized;
                    StepLeg(10,11,12,leftFootRest,leftFootRotation,cycle,crossing,movement);
                    StepLeg(13,14,15,rightFootRest,rightFootRotation,cycle+Mathf.PI,crossing,movement);
                    Rotate(11,actor.right,25*wind*fade);Rotate(14,actor.right,32*wind*fade);
                    break;
                case TrialChampion.TerraceCut.Sweep:
                    draw=new Vector3(.45f,.40f,.04f);contact=new Vector3(-.45f,.41f,.70f);
                    bladeDraw=new Vector3(92,83,24);bladeContact=new Vector3(83,-100,18);crouch=44;
                    Rotate(10,actor.right,-22*wind*fade);Rotate(11,actor.right,46*wind*fade);
                    Rotate(13,actor.right,-30*wind*fade);Rotate(14,actor.right,52*wind*fade);
                    break;
                case TrialChampion.TerraceCut.Charge:
                    draw=new Vector3(.31f,1.42f,.20f);contact=new Vector3(-.48f,.90f,.77f);
                    bladeDraw=new Vector3(-35,48,-32);bladeContact=new Vector3(102,-80,26);
                    left=Vector3.Lerp(left,new Vector3(.20f,1.52f,.26f),wind*(1-hit)*fade);crouch=29;
                    break;
                case TrialChampion.TerraceCut.Downbeat:
                    draw=new Vector3(.12f,1.79f,.28f);contact=new Vector3(.08f,.75f,.82f);
                    bladeDraw=new Vector3(-14,0,0);bladeContact=new Vector3(145,0,0);bodyTurn=8;crouch=18;
                    Rotate(13,actor.right,-17*Mathf.Sin(Mathf.Clamp01(p/.32f)*2*Mathf.PI)*fade);
                    break;
            }

            if(cut is TrialChampion.TerraceCut.Pair or TrialChampion.TerraceCut.Return or
                TrialChampion.TerraceCut.Brace or TrialChampion.TerraceCut.Riposte)
            {
                Vector3 stepDirection=new Vector3(localMotion.x,0,localMotion.y);
                StepLeg(10,11,12,leftFootRest,leftFootRotation,cycle,stepDirection,movement);
                StepLeg(13,14,15,rightFootRest,rightFootRotation,cycle+Mathf.PI,stepDirection,movement);
            }
            right=Vector3.Lerp(ready,Vector3.Lerp(draw,contact,hit),wind*fade);
            if(cut==TrialChampion.TerraceCut.Wheel || cut==TrialChampion.TerraceCut.Sweep)
                right.z+=.38f*Mathf.Sin(hit*Mathf.PI)*fade;
            angles=Vector3.Lerp(new Vector3(24,0,17),Vector3.Lerp(bladeDraw,bladeContact,hit),wind*fade);
            lean=crouch*wind*fade;twist=bodyTurn*(-wind+2*hit)*fade;
        }


        Transform J(int i)=>joints[i];
        void StepLeg(int thigh,int knee,int foot,Vector3 rest,Quaternion rotation,float phase,Vector3 direction,float movement)
        {
            float cycle=Mathf.Repeat(phase,Mathf.PI*2);bool swing=cycle>Mathf.PI;float t=swing?(cycle-Mathf.PI)/Mathf.PI:cycle/Mathf.PI;
            float travel=(swing?Mathf.SmoothStep(-1,1,t):Mathf.Lerp(1,-1,t))*strideCycle*.17f*movement;
            float height=swing?Mathf.Sin(t*Mathf.PI)*.16f*movement:0;
            Vector3 target=actor.position+actor.rotation*(rest+direction*travel+Vector3.up*height);
            Vector3 hint=actor.position+actor.rotation*(rest+Vector3.forward*.8f+direction*.12f+Vector3.up*.7f);
            TrialRig.SolveArm(J(thigh),J(knee),J(foot),target,hint);
            J(foot).rotation=actor.rotation*rotation*Quaternion.Euler(swing?-Mathf.Sin(t*Mathf.PI)*12:0,0,0);
        }
        void Rotate(int slot,Vector3 axis,float angle){var bone=J(slot);if(bone)bone.rotation=Quaternion.AngleAxis(angle,axis)*bone.rotation;}
        float SupportHeight(){if(joints==null || !J(12) || !J(15))return 0;return Mathf.Min(transform.InverseTransformPoint(J(12).position).y,transform.InverseTransformPoint(J(15).position).y);}
    }
}
