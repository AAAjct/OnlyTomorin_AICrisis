using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    public enum DeepFishSkill { None, TailSweep, TidalCharge, BellySlam, BubbleBarrage, Maelstrom, Enrage }

    // Fixed-size hazards and reused telegraphs keep the encounter bounded and allocation-free.
    public sealed class TrialBoss : MonoBehaviour
    {
        public DeepFishSkill Skill { get; private set; }
        public float SkillProgress => duration>0?Mathf.Clamp01(age/duration):0;
        public bool Enraged { get; private set; }
        public bool WindingUp => Skill!=DeepFishSkill.None && SkillProgress<ImpactPoint;
        public bool Unblockable => Skill==DeepFishSkill.TidalCharge || Skill==DeepFishSkill.BellySlam || Skill==DeepFishSkill.Maelstrom || Skill==DeepFishSkill.Enrage;
        public Transform Visual { get; private set; }
        public string SkillName => NameOf(Skill);
        public int AttacksStarted { get; private set; }
        public int ActiveHazards { get {int n=0;foreach(var h in hazards)if(h.active)n++;return n;} }
        float ImpactPoint => Skill==DeepFishSkill.BellySlam?.54f:Skill==DeepFishSkill.Maelstrom?.48f:.36f;
        TrialEnemy fighter;
        TrialGame game;
        float age,duration,cooldown=1.2f;
        int sequence,waves;
        bool landed;
        Vector3 aim,origin,heading;
        LineRenderer warning,fill,sweep;
        Material water,bubble;
        readonly Vector3[] ringPoints=new Vector3[65];
        readonly Hazard[] hazards=new Hazard[32];
        readonly TrialPlayer[] soloTarget=new TrialPlayer[1];
        readonly HashSet<TrialPlayer> contacted=new HashSet<TrialPlayer>();
        IEnumerable<TrialPlayer> Targets=>Mirror.NetworkServer.active?TrialNetworkManager.Current.Players:soloTarget;
        sealed class Hazard
        {
            public Transform visual;
            public LineRenderer ring;
            public Vector3 position,velocity;
            public float age,radius;
            public bool active,shockwave,hit;
            public readonly HashSet<TrialPlayer> hitPlayers=new HashSet<TrialPlayer>();
        }

        public void Initialize(TrialEnemy owner,TrialGame trial,GameObject model)
        {
            fighter=owner;game=trial;soloTarget[0]=trial.Player;
            Visual=Instantiate(model,transform).transform;Visual.name="DeepSeek · 大肥鱼";
            foreach(var animator in Visual.GetComponentsInChildren<Animator>())animator.enabled=false;
            foreach(var collider in Visual.GetComponentsInChildren<Collider>())Destroy(collider);
            var meshes=Visual.GetComponentsInChildren<Renderer>();var bounds=meshes[0].bounds;
            foreach(var r in meshes)bounds.Encapsulate(r.bounds);
            Visual.localScale*=2.85f/bounds.size.y;
            bounds=meshes[0].bounds;foreach(var r in meshes)bounds.Encapsulate(r.bounds);
            Visual.position+=new Vector3(transform.position.x-bounds.center.x,transform.position.y-bounds.min.y,transform.position.z-bounds.center.z);
            foreach(var r in meshes)if(r is SkinnedMeshRenderer skin)
            {skin.updateWhenOffscreen=false;var b=skin.localBounds;b.Expand(b.size*.65f);skin.localBounds=b;}
            var rig=gameObject.AddComponent<TrialBossRig>();rig.Initialize(owner,this,Visual);
            water=new Material(trial.ParticleShader){name="DeepSeek · tidal energy"};
            water.SetFloat("_Surface",1);water.SetOverrideTag("RenderType","Transparent");water.renderQueue=3000;
            water.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);water.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);water.SetInt("_ZWrite",0);water.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            bubble=new Material(water){name="DeepSeek · cyan bubbles"};bubble.SetColor("_BaseColor",new Color(.13f,.8f,1,.78f));
            warning=Line("Skill boundary",.10f);fill=Line("Skill charge",.045f);sweep=Line("Tail arc",.20f);
            for(int i=0;i<hazards.Length;i++)
            {
                var obj=GameObject.CreatePrimitive(PrimitiveType.Sphere);obj.name="Pooled tide bubble "+i;obj.layer=2;
                obj.transform.SetParent(transform,false);Destroy(obj.GetComponent<Collider>());
                var renderer=obj.GetComponent<Renderer>();renderer.sharedMaterial=bubble;renderer.shadowCastingMode=ShadowCastingMode.Off;
                var h=new Hazard{visual=obj.transform,ring=Line("Pooled shockwave "+i,.16f)};
                obj.SetActive(false);hazards[i]=h;
            }
        }

        LineRenderer Line(string label,float width)
        {
            var obj=new GameObject(label);obj.transform.SetParent(transform,false);
            var line=obj.AddComponent<LineRenderer>();line.sharedMaterial=water;line.positionCount=65;line.widthMultiplier=width;
            line.useWorldSpace=true;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;return line;
        }

        public static string NameOf(DeepFishSkill skill)
        {
            switch(skill)
            {
                case DeepFishSkill.TailSweep:return "摆尾断浪";
                case DeepFishSkill.TidalCharge:return "大肥鱼冲撞";
                case DeepFishSkill.BellySlam:return "鱼跃压顶";
                case DeepFishSkill.BubbleBarrage:return "深海泡泡阵";
                case DeepFishSkill.Maelstrom:return "涡流三重奏";
                case DeepFishSkill.Enrage:return "深海觉醒";
                default:return "";
            }
        }

        public TrialBossState CaptureNetwork()
        {
            var state=new TrialBossState{skill=(byte)Skill,age=age,duration=duration,aim=aim,origin=origin,heading=heading,enraged=Enraged,hazards=new TrialHazardState[hazards.Length]};
            for(int i=0;i<hazards.Length;i++){var h=hazards[i];state.hazards[i]=new TrialHazardState{active=h.active,wave=h.shockwave,position=h.position,radius=h.radius};}
            return state;
        }
        public void ApplyNetwork(TrialBossState state)
        {
            Skill=(DeepFishSkill)state.skill;age=state.age;duration=state.duration;aim=state.aim;origin=state.origin;heading=state.heading;Enraged=state.enraged;
            Color tint=Unblockable?new Color(1,.19f,.12f,.9f):new Color(1,.77f,.25f,.9f);
            warning.enabled=fill.enabled=sweep.enabled=false;
            if(WindingUp)
            {
                if(Skill==DeepFishSkill.TidalCharge)DrawCharge(tint);
                else if(Skill==DeepFishSkill.BellySlam){Circle(warning,aim,4.2f,tint);Circle(fill,aim,4.2f*Mathf.Clamp01(SkillProgress/.54f),tint);}
                else if(Skill==DeepFishSkill.TailSweep)Arc(warning,transform.position,4.1f,-115,115,tint);
                else Circle(warning,transform.position,Skill==DeepFishSkill.Maelstrom?9:2.3f,tint);
            }
            if(state.hazards==null)return;
            for(int i=0;i<Mathf.Min(hazards.Length,state.hazards.Length);i++)
            {
                var h=hazards[i];var remote=state.hazards[i];h.active=remote.active;h.shockwave=remote.wave;
                h.visual.gameObject.SetActive(remote.active&&!remote.wave);h.ring.enabled=remote.active&&remote.wave;
                if(remote.active){h.visual.position=remote.position;h.visual.localScale=Vector3.one*(remote.radius*2);if(remote.wave)Circle(h.ring,remote.position,remote.radius,new Color(.18f,.85f,1,Mathf.Clamp01(1-remote.radius/15)));}
            }
        }

        public void Tick(float dt)
        {
            if(!fighter.Alive){CancelAttack();return;}
            if(!game.CombatTarget.Alive){CancelAttack();fighter.BossMotion(0);return;}
            TickHazards(dt);
            if(!fighter.Awakened){fighter.BossMotion(0);return;}
            if(game.CombatTarget.transform.position.z<15 || Vector3.Distance(game.CombatTarget.transform.position,transform.position)>27)
            {fighter.ResetFighter();return;}
            if(fighter.Vulnerable)
            {if(Skill!=DeepFishSkill.None)CancelAttack();if(fighter.ActionTime>=fighter.ActionDuration)fighter.BossBegin(FighterAction.Free,0);return;}
            if(!Enraged && fighter.PhaseTwo){Enraged=true;StartSkill(DeepFishSkill.Enrage);}
            if(Skill!=DeepFishSkill.None){TickSkill(dt);return;}
            cooldown-=dt;
            var delta=game.CombatTarget.transform.position-transform.position;delta.y=0;
            float distance=delta.magnitude;
            fighter.BossFace(delta,190,dt);fighter.BossMotion(0);
            if(cooldown<=0 && distance<19)
            {
                // A deterministic rotation prevents repeated rolls and guarantees every move is used.
                DeepFishSkill choice;
                switch(sequence++%5)
                {case 0:choice=distance<5?DeepFishSkill.TailSweep:DeepFishSkill.TidalCharge;break;
                 case 1:choice=DeepFishSkill.BubbleBarrage;break;case 2:choice=DeepFishSkill.BellySlam;break;
                 case 3:choice=DeepFishSkill.TailSweep;break;default:choice=Enraged?DeepFishSkill.Maelstrom:DeepFishSkill.TidalCharge;break;}
                StartSkill(choice);return;
            }
            if(distance>3.8f)
            {
                Vector3 direction=delta.normalized;
                if(Physics.SphereCast(transform.position+Vector3.up,.65f,direction,out var hit,1.6f,1,QueryTriggerInteraction.Ignore))
                    direction=Vector3.ProjectOnPlane(direction,hit.normal).normalized;
                fighter.BossMove(direction*(Enraged?3.3f:2.6f),dt);fighter.BossMotion(1);
            }
        }

        public void StartSkill(DeepFishSkill next)
        {
            if(!fighter.Alive || next==DeepFishSkill.None)return;
            HideWarnings();Skill=next;age=0;waves=0;landed=false;contacted.Clear();AttacksStarted++;
            origin=transform.position;aim=game.CombatTarget.transform.position;
            aim.x=Mathf.Clamp(aim.x,-10.5f,10.5f);aim.z=Mathf.Clamp(aim.z,18,32);aim.y=0;
            heading=Vector3.ProjectOnPlane(aim-origin,Vector3.up).normalized;
            if(heading.sqrMagnitude<.01f)heading=transform.forward;
            fighter.BossFace(heading,36000,.02f);
            duration=next==DeepFishSkill.TailSweep?1.75f:next==DeepFishSkill.TidalCharge?2.1f:next==DeepFishSkill.BellySlam?2.65f:next==DeepFishSkill.BubbleBarrage?2.7f:next==DeepFishSkill.Maelstrom?3.5f:2.1f;
            if(Enraged && next!=DeepFishSkill.Enrage)duration*=.88f;
            fighter.BossBegin(Unblockable?FighterAction.Heavy:FighterAction.Light,duration);
            game.Effects.Sound("warning",transform.position,Unblockable?.65f:1.1f);
        }

        void TickSkill(float dt)
        {
            age+=dt;float p=SkillProgress;
            fighter.BossMotion(0);
            Color tint=Unblockable?new Color(1,.19f,.12f,.9f):new Color(1,.77f,.25f,.9f);
            if(WindingUp)
            {
                if(Skill==DeepFishSkill.TidalCharge)DrawCharge(tint);
                else if(Skill==DeepFishSkill.BellySlam){Circle(warning,aim,4.2f,tint);Circle(fill,aim,4.2f*Mathf.Clamp01(p/.54f),tint);}
                else if(Skill==DeepFishSkill.TailSweep){Arc(warning,transform.position,4.1f,-115,115,tint);Arc(fill,transform.position,4.1f*Mathf.Clamp01(p/.36f),-115,115,tint);}
                else Circle(warning,transform.position,Skill==DeepFishSkill.Maelstrom?9:2.3f,tint);
            }
            else {warning.enabled=fill.enabled=false;}
            if(Skill==DeepFishSkill.TailSweep)
            {
                if(p>=.36f && p<=.64f)
                {
                    Arc(sweep,transform.position+Vector3.up*.8f,3.6f,Mathf.Lerp(-115,90,(p-.36f)/.28f),Mathf.Lerp(-85,120,(p-.36f)/.28f),new Color(.25f,.9f,1,.9f));
                    Contact(4.1f,120,30,false);
                }
                else sweep.enabled=false;
            }
            if(Skill==DeepFishSkill.TidalCharge && p>=.36f && p<=.69f)
            {fighter.BossMove(heading*(Enraged?18:15),dt);Contact(1.9f,100,38,true);}
            if(Skill==DeepFishSkill.BellySlam)
            {
                if(p>.24f && p<.54f)
                {var destination=Vector3.Lerp(origin,aim,Mathf.SmoothStep(0,1,(p-.24f)/.30f));fighter.BossMove((destination-transform.position)/Mathf.Max(dt,.001f),dt);}
                if(p>=.54f && !landed)
                {landed=true;Area(aim,4.2f,45);Impact(aim);SpawnWave(aim,Enraged?9:7);}
            }
            if(Skill==DeepFishSkill.BubbleBarrage && waves<3 && p>=.36f+waves*.14f)
            {ShootFan(waves++);game.Effects.Sound("swing",transform.position,.65f);}
            if(Skill==DeepFishSkill.Maelstrom && waves<3 && p>=.48f+waves*.12f)
            {SpawnWave(transform.position,7.8f+waves*.8f);waves++;game.Effects.Sound("warning",transform.position,.75f);}
            if(Skill==DeepFishSkill.Enrage && !landed && p>=.55f)
            {landed=true;game.Effects.Burst(transform.position+Vector3.up,Color.cyan,65);game.CameraRig.Kick(.35f);}
            // A parry can cancel the skill inside Contact; preserve the resulting stagger.
            if(Skill!=DeepFishSkill.None && age>=duration)
            {Skill=DeepFishSkill.None;fighter.BossBegin(FighterAction.Free,0);cooldown=Enraged?.85f:1.15f;HideWarnings();}
        }

        void Contact(float range,float angle,float damage,bool unblockable)
        {
            foreach(var target in Targets)
            {
                if(!target||!target.Alive||contacted.Contains(target))continue;
                var delta=target.transform.position-transform.position;delta.y=0;
                if(delta.magnitude>range+.28f||Vector3.Angle(transform.forward,delta)>angle||Blocked(transform.position,target.transform.position))continue;
                contacted.Add(target);landed=true;target.ReceiveHit(fighter,damage,30,unblockable);
            }
        }
        void Area(Vector3 point,float range,float damage)
        {foreach(var target in Targets)if(target&&target.Alive&&Vector3.ProjectOnPlane(target.transform.position-point,Vector3.up).magnitude<range+.28f&&!Blocked(point,target.transform.position))target.ReceiveHit(fighter,damage,35,true);}
        bool Blocked(Vector3 from,Vector3 to)=>Physics.Linecast(from+Vector3.up*.8f,to+Vector3.up*.8f,1,QueryTriggerInteraction.Ignore);
        void Impact(Vector3 point)
        {game.Effects.Burst(point+Vector3.up*.2f,new Color(.2f,.8f,1),65);game.CameraRig.Kick(.65f);game.Effects.Sound("hit",point,.55f);}

        void ShootFan(int wave)
        {
            var delta=Vector3.ProjectOnPlane(game.CombatTarget.transform.position-transform.position,Vector3.up).normalized;
            int count=Enraged?9:7;
            for(int i=0;i<count;i++)
            {
                var h=Available();if(h==null)break;
                var direction=Quaternion.AngleAxis((i-(count-1)*.5f)*12+(wave==1?6:0),Vector3.up)*delta;
                h.position=transform.position+Vector3.up*.85f+direction*.9f;h.velocity=direction*(Enraged?7.5f:6.2f);
                h.radius=.27f;h.age=0;h.hit=false;h.hitPlayers.Clear();h.active=true;h.shockwave=false;h.visual.gameObject.SetActive(true);
                h.visual.position=h.position;h.visual.localScale=Vector3.one*.54f;h.ring.enabled=false;
            }
        }
        void SpawnWave(Vector3 point,float speed)
        {
            var h=Available();if(h==null)return;
            h.active=true;h.shockwave=true;h.hit=false;h.hitPlayers.Clear();h.age=0;h.radius=.2f;h.position=point;h.velocity=Vector3.forward*speed;
            h.visual.gameObject.SetActive(false);h.ring.enabled=true;
        }
        Hazard Available(){foreach(var h in hazards)if(!h.active)return h;return null;}

        void TickHazards(float dt)
        {
            foreach(var h in hazards)
            {
                if(!h.active)continue;
                h.age+=dt;
                if(h.shockwave)
                {
                    float previous=h.radius;h.radius+=h.velocity.z*dt;
                    Circle(h.ring,h.position,h.radius,new Color(.18f,.85f,1,Mathf.Clamp01(1-h.radius/15)));
                    foreach(var target in Targets)
                    {
                        if(!target||!target.Alive||h.hitPlayers.Contains(target))continue;
                        float distance=Vector3.ProjectOnPlane(target.transform.position-h.position,Vector3.up).magnitude;
                        if(distance>=previous-.45f&&distance<=h.radius+.45f&&!Blocked(h.position,target.transform.position))
                        {h.hitPlayers.Add(target);target.ReceiveHit(fighter,Enraged?25:20,20,true);}
                    }
                    if(h.radius>15)Release(h);
                }
                else
                {
                    var step=h.velocity*dt;
                    if(Physics.SphereCast(h.position,h.radius,step.normalized,out _,step.magnitude,1,QueryTriggerInteraction.Ignore)){Release(h);continue;}
                    bool touched=false;
                    foreach(var target in Targets)
                    {
                        if(!target||!target.Alive)continue;
                        var to=target.transform.position+Vector3.up*.85f-h.position;
                        float fraction=step.sqrMagnitude>.00001f?Mathf.Clamp01(Vector3.Dot(to,step)/step.sqrMagnitude):0;
                        if((to-step*fraction).sqrMagnitude<.58f*.58f){target.ReceiveHit(fighter,16,12);touched=true;break;}
                    }
                    if(touched){Release(h);continue;}
                    h.position+=step;h.visual.position=h.position;
                    if(h.age>4.5f)Release(h);
                }
            }
        }

        void Release(Hazard h){h.active=false;h.visual.gameObject.SetActive(false);h.ring.enabled=false;}
        void Circle(LineRenderer line,Vector3 center,float radius,Color tint)=>Arc(line,center,radius,0,360,tint,true);
        void Arc(LineRenderer line,Vector3 center,float radius,float from,float to,Color tint,bool world=false)
        {
            float yaw=world?0:transform.eulerAngles.y;
            for(int i=0;i<ringPoints.Length;i++)
            {float a=(yaw+Mathf.Lerp(from,to,(float)i/(ringPoints.Length-1)))*Mathf.Deg2Rad;ringPoints[i]=center+new Vector3(Mathf.Sin(a)*radius,.10f,Mathf.Cos(a)*radius);}
            line.SetPositions(ringPoints);line.startColor=line.endColor=tint;line.enabled=true;
        }
        void DrawCharge(Color tint)
        {
            Vector3 a=origin+Vector3.up*.10f,b=a+heading*11,side=Vector3.Cross(Vector3.up,heading)*1.25f;
            // Four corners are interpolated into the existing buffer; no per-frame allocation.
            for(int i=0;i<65;i++)
            {float p=i/16f;int edge=Mathf.Min(3,(int)p);float t=p-edge;Vector3 start=edge==0?a-side:edge==1?b-side:edge==2?b+side:a+side;Vector3 end=edge==0?b-side:edge==1?b+side:edge==2?a+side:a-side;ringPoints[i]=Vector3.Lerp(start,end,t);}
            warning.SetPositions(ringPoints);warning.startColor=warning.endColor=tint;warning.enabled=true;
        }
        void HideWarnings(){if(warning)warning.enabled=false;if(fill)fill.enabled=false;if(sweep)sweep.enabled=false;}
        public void CancelAttack()
        {Skill=DeepFishSkill.None;age=duration=0;cooldown=1.1f;HideWarnings();foreach(var h in hazards)if(h!=null)Release(h);}
        public void ResetBoss(){CancelAttack();Enraged=false;sequence=waves=AttacksStarted=0;}
        void OnDestroy(){if(water)Destroy(water);if(bubble)Destroy(bubble);}
    }
}
