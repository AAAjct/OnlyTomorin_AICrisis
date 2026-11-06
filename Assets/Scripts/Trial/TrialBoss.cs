using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    public enum DeepFishSkill { None, TailSweep, TidalCharge, BellySlam, BubbleBarrage, Maelstrom, Enrage, ReverseTail, RiptideRush, DelayedSlam, AbyssRain }

    // Fixed-size hazards and reused telegraphs keep the encounter bounded and allocation-free.
    public sealed class TrialBoss : MonoBehaviour
    {
        public DeepFishSkill Skill { get; private set; }
        public float SkillProgress => duration>0?Mathf.Clamp01(age/duration):0;
        public float WarningProgress
        {
            get
            {
                float p=SkillProgress;
                if(Skill==DeepFishSkill.ReverseTail)return p<.43f?Mathf.Clamp01(p/.30f):Mathf.InverseLerp(.43f,.66f,p);
                if(Skill==DeepFishSkill.RiptideRush)return p<.46f?Mathf.Clamp01(p/.20f):Mathf.InverseLerp(.46f,.67f,p);
                if(Skill==DeepFishSkill.AbyssRain)return p<.5f?Mathf.Clamp01(p/.40f):Mathf.InverseLerp(.5f,.82f,p);
                return Mathf.Clamp01(p/ImpactPoint);
            }
        }
        public bool Enraged { get; private set; }
        public bool Frenzied { get; private set; }
        public TrialBossBoon Boon {get;private set;}
        public void RollBoon(){Boon=(TrialBossBoon)Random.Range(1,4);}
        public void ClearBoon(){Boon=TrialBossBoon.None;}
        public bool WindingUp => Skill!=DeepFishSkill.None && (Skill==DeepFishSkill.RiptideRush?(SkillProgress<.20f || SkillProgress>=.46f && SkillProgress<.67f):Skill==DeepFishSkill.ReverseTail?(SkillProgress<.30f || SkillProgress>=.43f && SkillProgress<.66f):Skill==DeepFishSkill.AbyssRain?(SkillProgress<.40f || SkillProgress>=.5f && SkillProgress<.82f):SkillProgress<ImpactPoint);
        public bool Unblockable => Skill==DeepFishSkill.TidalCharge || Skill==DeepFishSkill.BellySlam || Skill==DeepFishSkill.Maelstrom || Skill==DeepFishSkill.Enrage || Skill==DeepFishSkill.RiptideRush || Skill==DeepFishSkill.DelayedSlam || Skill==DeepFishSkill.AbyssRain;
        public bool CanBeDeflected=>Skill==DeepFishSkill.TailSweep || Skill==DeepFishSkill.ReverseTail || Skill==DeepFishSkill.TidalCharge || Skill==DeepFishSkill.RiptideRush;
        public Transform Visual { get; private set; }
        public string SkillName => NameOf(Skill);
        public int AttacksStarted { get; private set; }
        public int ActiveHazards { get {int n=0;foreach(var h in hazards)if(h.active)n++;return n;} }
        float ImpactPoint => Skill==DeepFishSkill.DelayedSlam?.72f:Skill==DeepFishSkill.BellySlam?.54f:Skill==DeepFishSkill.Maelstrom?.42f:Skill==DeepFishSkill.BubbleBarrage?.30f:Skill==DeepFishSkill.Enrage?.55f:.36f;
        TrialEnemy fighter;
        TrialGame game;
        float age,duration,cooldown=1.2f;
        int waves,variant,comboDepth,markCount;
        float reactiveCooldown;
        float regroupTime,pursuitTime;
        int orbitSide=1;
        DeepFishSkill previousChoice;
        static readonly DeepFishSkill[] attackChoices={DeepFishSkill.TailSweep,DeepFishSkill.ReverseTail,DeepFishSkill.TidalCharge,DeepFishSkill.RiptideRush,DeepFishSkill.BubbleBarrage,DeepFishSkill.AbyssRain,DeepFishSkill.Maelstrom};
        readonly float[] attackWeights=new float[7];
        public bool Regrouping=>regroupTime>0;
        Vector3[] marks=new Vector3[4];
        LineRenderer[] markWarnings=new LineRenderer[4];
        bool landed;
        Vector3 aim,origin,heading;
        LineRenderer warning,fill,sweep;
        Material water,bubble;
        readonly Vector3[] ringPoints=new Vector3[65];
        readonly Hazard[] hazards=new Hazard[64];
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
            water=new Material(Resources.Load<Material>("Trial/VFX/TideRibbon"));
            bubble=new Material(Resources.Load<Material>("Trial/VFX/TideShell"));
            warning=Line("Skill boundary",.10f);fill=Line("Skill charge",.045f);sweep=Line("Tail arc",.20f);
            for(int i=0;i<markWarnings.Length;i++)markWarnings[i]=Line("Abyss rain target "+i,.12f);
            warning.gameObject.AddComponent<TrialSkillSurface>().Initialize(warning);
            foreach(var mark in markWarnings)mark.gameObject.AddComponent<TrialSkillSurface>().Initialize(mark);
            for(int i=0;i<hazards.Length;i++)
            {
                var obj=GameObject.CreatePrimitive(PrimitiveType.Sphere);obj.name="Pooled tide bubble "+i;obj.layer=2;
                obj.transform.SetParent(transform,false);Destroy(obj.GetComponent<Collider>());
                obj.GetComponent<MeshFilter>().sharedMesh=Resources.Load<Mesh>("Trial/VFX/TidePearl");
                var tail=obj.AddComponent<TrailRenderer>();tail.time=.22f;tail.minVertexDistance=.08f;tail.startWidth=.28f;tail.endWidth=0;tail.sharedMaterial=water;
                tail.startColor=new Color(.08f,.65f,1,.65f);tail.endColor=new Color(.02f,.2f,.5f,0);tail.shadowCastingMode=ShadowCastingMode.Off;
                var renderer=obj.GetComponent<MeshRenderer>();renderer.sharedMaterial=bubble;renderer.shadowCastingMode=ShadowCastingMode.Off;
                var h=new Hazard{visual=obj.transform,ring=Line("Pooled shockwave "+i,.16f)};
                h.ring.gameObject.AddComponent<TrialWaveCrest>().Initialize(h.ring,water);
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
                case DeepFishSkill.ReverseTail:return "回身断潮 · 二连摆尾";
                case DeepFishSkill.RiptideRush:return "折返猎杀 · 二连冲撞";
                case DeepFishSkill.DelayedSlam:return "虚晃鱼跃 · 延迟重砸";
                case DeepFishSkill.AbyssRain:return "深渊点名 · 双轮落潮";
                default:return "";
            }
        }

        public TrialBossState CaptureNetwork()
        {
            int active=ActiveHazards;
            var state=new TrialBossState{skill=(byte)Skill,age=age,duration=duration,aim=aim,origin=origin,heading=heading,enraged=Enraged,frenzied=Frenzied,variant=variant,boon=Boon,marks=markCount==0?System.Array.Empty<Vector3>():new Vector3[markCount],hazards=active==0?System.Array.Empty<TrialHazardState>():new TrialHazardState[active]};
            for(int i=0;i<markCount;i++)state.marks[i]=marks[i];
            int index=0;
            foreach(var h in hazards)if(h.active)state.hazards[index++]=new TrialHazardState{active=true,wave=h.shockwave,position=h.position,radius=h.radius};
            return state;
        }
        public void ApplyNetwork(TrialBossState state)
        {
            bool newSkill=state.skill!=0 && ((byte)Skill!=state.skill || state.age+.1f<age);
            Skill=(DeepFishSkill)state.skill;age=state.age;duration=state.duration;aim=state.aim;origin=state.origin;heading=state.heading;Enraged=state.enraged;Frenzied=state.frenzied;variant=state.variant;
            Boon=state.boon;
            if(newSkill)game.Effects.Sound(CanBeDeflected && Unblockable?"tideCharge":"warning",transform.position,Unblockable?.85f:1.1f);
            EnsureMarks(state.marks==null?0:state.marks.Length);markCount=state.marks==null?0:state.marks.Length;for(int i=0;i<markCount;i++)marks[i]=state.marks[i];
            Color tint=Unblockable?new Color(1,.19f,.12f,.9f):new Color(1,.77f,.25f,.9f);
            HideWarnings();DrawTelegraphs(tint);
            DrawActiveSweep();
            int count=state.hazards==null?0:Mathf.Min(hazards.Length,state.hazards.Length);
            for(int i=0;i<hazards.Length;i++)
            {
                var h=hazards[i];var remote=i<count?state.hazards[i]:default;h.active=remote.active;h.shockwave=remote.wave;
                h.visual.gameObject.SetActive(remote.active&&!remote.wave);h.ring.enabled=remote.active&&remote.wave;
                if(remote.active){h.visual.position=remote.position;h.visual.localScale=Vector3.one*(remote.radius*2);if(remote.wave)Circle(h.ring,remote.position,remote.radius,new Color(.18f,.85f,1,Mathf.Clamp01(1-remote.radius/15)));}
            }
        }

        internal void TickDuringFlinch(float dt)=>TickHazards(dt);
        public void Tick(float dt)
        {
            if(!fighter.Alive){CancelAttack();return;}
            if(fighter.CombatFrozen)return;
            if(!game.CombatTarget.Alive){CancelAttack();fighter.BossMotion(0);return;}
            TickHazards(dt);
            reactiveCooldown=Mathf.Max(0,reactiveCooldown-dt);
            if(!fighter.Awakened){fighter.BossMotion(0);return;}
            if(game.CombatTarget.transform.position.z<15 || Vector3.Distance(game.CombatTarget.transform.position,transform.position)>27)
            {fighter.ResetFighter();return;}
            if(fighter.Vulnerable)
            {if(Skill!=DeepFishSkill.None)CancelAttack();if(fighter.ActionTime>=fighter.ActionDuration)fighter.BossBegin(FighterAction.Free,0);return;}
            if(!Enraged && fighter.PhaseTwo){Enraged=true;StartSkill(DeepFishSkill.Enrage);}
            if(!Frenzied && fighter.Health<fighter.MaxHealth*.25f)
            {Frenzied=true;game.Hud.Notify("深渊狂潮 · 小心连续变招",2);}
            if(Skill!=DeepFishSkill.None){TickSkill(dt);return;}
            cooldown-=dt;
            var delta=game.CombatTarget.transform.position-transform.position;delta.y=0;
            float distance=delta.magnitude;
            fighter.BossFace(delta,190,dt);
            if(regroupTime>0)
            {
                regroupTime=Mathf.Max(0,regroupTime-dt);
                cooldown=Mathf.Max(cooldown,regroupTime+.1f);
                Vector3 radial=delta.normalized,tangent=Vector3.Cross(Vector3.up,radial)*orbitSide;
                Vector3 step=(distance<4?-radial+tangent*.4f:distance<7?tangent:radial+tangent*.25f).normalized;
                Vector3 next=transform.position+step*1.8f*dt;
                if(next.x>-10 && next.x<10 && next.z>18 && next.z<33 && !Physics.SphereCast(transform.position+Vector3.up,.7f,step,out _,1,1,QueryTriggerInteraction.Ignore))
                    fighter.BossMove(step*1.8f,dt);
                else{orbitSide=-orbitSide;step=Vector3.zero;}
                fighter.BossMotion(step.sqrMagnitude>.01f?1:0);return;
            }
            pursuitTime+=dt;
            if(pursuitTime>3 && distance>5){pursuitTime=0;cooldown=Mathf.Min(cooldown,.15f);orbitSide=-orbitSide;}
            // Set the gait target once: easing toward zero and then one each frame
            // previously cancelled out, leaving the walking animation at zero.
            fighter.BossMotion(distance>3.8f?1:0);
            if(cooldown<=0 && distance<19)
            {
                // React to visible commitments, then choose a reachable attack.
                DeepFishSkill choice;
                if(reactiveCooldown<=0 && distance<5.5f && Vector3.Angle(transform.forward,delta)>100)
                {choice=DeepFishSkill.ReverseTail;reactiveCooldown=5;}
                else if(reactiveCooldown<=0 && game.CombatTarget.Action==FighterAction.Heal)
                {choice=DeepFishSkill.AbyssRain;reactiveCooldown=5;}
                else choice=ChooseAttack(distance);
                comboDepth=0;
                StartSkill(choice);return;
            }
            if(distance>3.8f)
            {
                Vector3 direction=delta.normalized;
                if(Physics.SphereCast(transform.position+Vector3.up,.65f,direction,out var hit,1.6f,1,QueryTriggerInteraction.Ignore))
                    direction=Vector3.ProjectOnPlane(direction,hit.normal).normalized;
                fighter.BossMove(direction*(Enraged?4.2f:3.5f),dt);
            }
        }

        DeepFishSkill ChooseAttack(float distance)
        {
            float total=0;
            for(int i=0;i<attackChoices.Length;i++)
            {
                var skill=attackChoices[i];float weight;
                switch(skill)
                {
                    case DeepFishSkill.TailSweep:weight=distance<4.8f?3:0;break;
                    case DeepFishSkill.ReverseTail:weight=distance<5.3f?2.5f:0;break;
                    case DeepFishSkill.TidalCharge:weight=distance>5?2:0;break;
                    case DeepFishSkill.RiptideRush:weight=distance>6?3:distance>3?1:0;break;
                    case DeepFishSkill.BubbleBarrage:weight=distance>5?2: .5f;break;
                    case DeepFishSkill.Maelstrom:weight=Enraged && distance<11?2:0;break;
                    default:weight=distance>5?2:1;break;
                }
                if(skill==previousChoice)weight*=.15f;
                if(game.CombatTarget.transform.position.y>transform.position.y+1.2f)
                    weight*=skill==DeepFishSkill.BubbleBarrage?6:skill==DeepFishSkill.AbyssRain?2:.3f;
                if(game.CombatTarget.Action==FighterAction.Dodge)
                    weight*=skill==DeepFishSkill.ReverseTail || skill==DeepFishSkill.RiptideRush?2:1;
                if(game.CombatTarget.Guarding && (skill==DeepFishSkill.AbyssRain || skill==DeepFishSkill.Maelstrom))weight*=1.8f;
                attackWeights[i]=weight;total+=weight;
            }
            float roll=Random.value*total;
            for(int i=0;i<attackChoices.Length;i++){roll-=attackWeights[i];if(attackWeights[i]>0 && roll<=0)return attackChoices[i];}
            return DeepFishSkill.AbyssRain;
        }

        public void StartSkill(DeepFishSkill next)
        {
            if(!fighter.Alive || next==DeepFishSkill.None)return;
            // Retain the enum ID for serialized/network data, but retire the tracking feint slam.
            if(next==DeepFishSkill.DelayedSlam)return;
            if(next==DeepFishSkill.BellySlam)next=DeepFishSkill.TailSweep;
            HideWarnings();Skill=next;previousChoice=next;regroupTime=0;age=0;waves=0;markCount=0;variant=AttacksStarted%2;landed=false;contacted.Clear();AttacksStarted++;
            origin=transform.position;aim=game.CombatTarget.transform.position;
            aim.x=Mathf.Clamp(aim.x,-10.5f,10.5f);aim.z=Mathf.Clamp(aim.z,18,32);aim.y=0;
            // The ordinary leap lands in place; its telegraph, damage and wave share this point.
            if(next==DeepFishSkill.BellySlam)aim=new Vector3(origin.x,0,origin.z);
            heading=Vector3.ProjectOnPlane(aim-origin,Vector3.up).normalized;
            if(heading.sqrMagnitude<.01f)heading=transform.forward;
            fighter.BossFace(heading,36000,.02f);
            duration=next==DeepFishSkill.ReverseTail?3.3f:next==DeepFishSkill.RiptideRush?3.6f:next==DeepFishSkill.DelayedSlam?3.15f:next==DeepFishSkill.AbyssRain?4.2f:next==DeepFishSkill.TailSweep?1.9f:next==DeepFishSkill.TidalCharge?2f:next==DeepFishSkill.BellySlam?2.5f:next==DeepFishSkill.BubbleBarrage?2.7f:next==DeepFishSkill.Maelstrom?3.5f:2.1f;
            if(next!=DeepFishSkill.Enrage)duration*=Mathf.Max(.80f,1-.06f*(game.PartySize-1))*(Frenzied?.78f:Enraged?.86f:1);
            if(next==DeepFishSkill.AbyssRain && game.PartySize>=2)duration=Mathf.Max(duration,3.6f);
            if(Boon==TrialBossBoon.SwiftCurrent)duration/=1.06f;
            if(next==DeepFishSkill.AbyssRain)CaptureMarks();
            fighter.BossBegin(Unblockable?FighterAction.Heavy:FighterAction.Light,duration);
            game.Effects.Sound(CanBeDeflected && Unblockable?"tideCharge":"warning",transform.position,Unblockable?.85f:1.1f);
        }

        void TickSkill(float dt)
        {
            age+=dt;float p=SkillProgress;
            fighter.BossMotion(0);
            Color tint=Unblockable?new Color(1,.19f,.12f,.9f):new Color(1,.77f,.25f,.9f);
            warning.enabled=fill.enabled=false;
            DrawTelegraphs(tint);
            if(Skill==DeepFishSkill.ReverseTail)
            {
                if(p>=.30f && p<.43f)SweepContact(4.3f,-110,110,.30f,.43f,30,dt);
                if(p>=.43f && waves==0){waves=1;contacted.Clear();sweep.enabled=false;}
                if(p>=.66f && p<.80f)SweepContact(4.3f,70,290,.66f,.80f,34,dt);
                if(p>=.80f)sweep.enabled=false;
            }
            if(Skill==DeepFishSkill.RiptideRush)
            {
                if(p>=.46f && waves==0)
                {waves=1;contacted.Clear();origin=transform.position;aim=game.CombatTarget.transform.position;heading=Vector3.ProjectOnPlane(aim-origin,Vector3.up).normalized;fighter.BossFace(heading,36000,.02f);}
                if(p>=.20f && p<.42f || p>=.67f && p<.89f)
                {fighter.BossMove(heading*(Enraged?19:17),dt);Contact(1.9f,120,40,true);}
            }
            if(Skill==DeepFishSkill.DelayedSlam)
            {
                // Track only during the feint; the final landing point has a clear escape window.
                if(p<.38f){aim=ClampArena(game.CombatTarget.transform.position);heading=Vector3.ProjectOnPlane(aim-transform.position,Vector3.up).normalized;fighter.BossFace(heading,180,dt);}
                if(p>.42f && p<.72f){var destination=Vector3.Lerp(origin,aim,Mathf.SmoothStep(0,1,(p-.42f)/.30f));fighter.BossMove((destination-transform.position)/Mathf.Max(dt,.001f),dt);}
                if(p>=.72f && !landed){landed=true;Area(aim,4.7f,51);Impact(aim);SpawnWave(aim,8);}
            }
            if(Skill==DeepFishSkill.AbyssRain)
            {
                bool repeat=game.PartySize<2;
                if(p<.16f || repeat && p>=.5f && p<.57f)CaptureMarks();
                if(waves==0 && p>=.4f){DetonateMarks();waves=1;markCount=0;}
                if(repeat && waves==1 && p>=.5f){CaptureMarks();waves=2;}
                if(repeat && waves==2 && p>=.82f){DetonateMarks();waves=3;markCount=0;}
                if(!repeat && waves>0)markCount=0;
            }
            if(Skill==DeepFishSkill.TailSweep)
            {
                if(p>=.36f && p<=.64f)
                {
                    SweepContact(4.1f,-110,110,.36f,.64f,30,dt);
                }
                else sweep.enabled=false;
            }
            if(Skill==DeepFishSkill.TidalCharge && p>=.36f && p<=.69f)
            {fighter.BossMove(heading*(Enraged?18:15),dt);Contact(1.9f,100,38,true);}
            if(Skill==DeepFishSkill.BellySlam)
            {
                if(p>=.54f && !landed)
                {landed=true;Area(aim,4.2f,45);Impact(aim);SpawnWave(aim,Enraged?9:7);}
            }
            if(Skill==DeepFishSkill.BubbleBarrage && waves<(Frenzied?4:3) && p>=.30f+waves*.14f)
            {ShootFan(waves++);game.Effects.Sound("bubbleCast",transform.position,.95f);}
            if(Skill==DeepFishSkill.Maelstrom && waves<(Frenzied?4:3) && p>=.42f+waves*.13f)
            {SpawnWave(transform.position,7.0f+waves*.65f);waves++;game.Effects.Sound("warning",transform.position,.75f);}
            if(Skill==DeepFishSkill.Enrage && !landed && p>=.55f)
            {landed=true;game.Effects.Burst(transform.position+Vector3.up,Color.cyan,65);game.CameraRig.Kick(.35f);}
            // A parry can cancel the skill inside Contact; preserve the resulting stagger.
            if(Skill!=DeepFishSkill.None && age>=duration)
            {
                var previous=Skill;Skill=DeepFishSkill.None;fighter.BossBegin(FighterAction.Free,0);HideWarnings();
                if(Enraged && comboDepth<(Frenzied?2:1) && previous!=DeepFishSkill.Enrage && previous!=DeepFishSkill.AbyssRain && ActiveHazards<12)
                {comboDepth++;StartSkill(ChooseAttack(Vector3.ProjectOnPlane(game.CombatTarget.transform.position-transform.position,Vector3.up).magnitude));return;}
                comboDepth=0;regroupTime=Frenzied?.65f:Enraged?.8f:1.05f;cooldown=regroupTime;pursuitTime=0;orbitSide=-orbitSide;
            }
        }

        static Vector3 ClampArena(Vector3 point){point.x=Mathf.Clamp(point.x,-10.5f,10.5f);point.z=Mathf.Clamp(point.z,18,32);point.y=0;return point;}
        void EnsureMarks(int capacity)
        {
            if(capacity<=marks.Length)return;
            int previous=marks.Length;System.Array.Resize(ref marks,capacity);System.Array.Resize(ref markWarnings,capacity);
            for(int i=previous;i<capacity;i++)
            {
                markWarnings[i]=Line("Abyss rain target "+i,.12f);
                markWarnings[i].gameObject.AddComponent<TrialSkillSurface>().Initialize(markWarnings[i]);
            }
        }
        void CaptureMarks(){EnsureMarks(game.PartySize);markCount=0;foreach(var target in Targets)if(target && target.Alive && !target.ChoosingBoon && markCount<marks.Length)marks[markCount++]=ClampArena(target.transform.position);}
        void DetonateMarks()
        {
            // Overlapping circles damage each player once; stacking cannot multiply an unavoidable hit.
            contacted.Clear();
            for(int i=0;i<markCount;i++)
            {
                Impact(marks[i]);SpawnWave(marks[i],4.6f);
                foreach(var target in Targets)
                    if(target && target.Alive && !contacted.Contains(target) && target.OverlapsAttackHeight(marks[i].y-.1f,marks[i].y+.3f) && Vector3.ProjectOnPlane(target.transform.position-marks[i],Vector3.up).sqrMagnitude<5.76f && !Blocked(marks[i],target.transform.position))
                    {contacted.Add(target);target.ReceiveHit(fighter,36,30,true);}
            }
        }
        void DrawTelegraphs(Color tint)
        {
            foreach(var line in markWarnings)line.enabled=false;
            if(!WindingUp)return;
            if(Skill==DeepFishSkill.AbyssRain)
            {for(int i=0;i<markCount;i++)Circle(markWarnings[i],marks[i],2.6f,tint);return;}
            if(Skill==DeepFishSkill.TidalCharge || Skill==DeepFishSkill.RiptideRush)DrawCharge(tint);
            else if(Skill==DeepFishSkill.BellySlam || Skill==DeepFishSkill.DelayedSlam)
            {float radius=Skill==DeepFishSkill.DelayedSlam?4.7f:4.2f;Circle(warning,aim,radius,tint);Circle(fill,aim,radius*Mathf.Clamp01(SkillProgress/ImpactPoint),tint);}
            else if(Skill==DeepFishSkill.ReverseTail)Arc(warning,transform.position,4.3f,SkillProgress<.43f?-110:70,SkillProgress<.43f?110:290,tint);
            else if(Skill==DeepFishSkill.TailSweep){Arc(warning,transform.position,4.1f,-110,110,tint);Arc(fill,transform.position,4.1f*Mathf.Clamp01(SkillProgress/.36f),-110,110,tint);}
            else Circle(warning,transform.position,Skill==DeepFishSkill.Maelstrom?9:2.3f,tint);
        }
        void DrawActiveSweep()
        {
            float p=SkillProgress;
            if(Skill==DeepFishSkill.TailSweep && p>=.36f && p<=.64f)
            {float angle=Mathf.Lerp(-110,110,Mathf.InverseLerp(.36f,.64f,p));Arc(sweep,transform.position+Vector3.up*.8f,4.1f,angle-7,angle+7,Color.cyan);}
            if(Skill==DeepFishSkill.ReverseTail && (p>=.30f && p<.43f || p>=.66f && p<.80f))
            {float angle=p<.43f?Mathf.Lerp(-110,110,Mathf.InverseLerp(.30f,.43f,p)):Mathf.Lerp(70,290,Mathf.InverseLerp(.66f,.80f,p));Arc(sweep,transform.position+Vector3.up*.8f,4.3f,angle-7,angle+7,Color.cyan);}
        }
        void SweepContact(float radius,float from,float to,float begin,float end,float damage,float dt)
        {
            float current=Mathf.Lerp(from,to,Mathf.InverseLerp(begin,end,SkillProgress));
            float previous=Mathf.Lerp(from,to,Mathf.InverseLerp(begin,end,(age-dt)/duration));
            Arc(sweep,transform.position+Vector3.up*.8f,radius,current-7,current+7,new Color(.25f,.9f,1,.95f));
            foreach(var target in Targets)
            {
                if(!target || !target.Alive || contacted.Contains(target) || !target.OverlapsAttackHeight(transform.position.y,transform.position.y+1.6f))continue;
                var delta=Vector3.ProjectOnPlane(target.transform.position-transform.position,Vector3.up);
                if(delta.magnitude>radius-.12f || Blocked(transform.position,target.transform.position))continue;
                float angle=Vector3.SignedAngle(transform.forward,delta,Vector3.up);
                if(from>=0)angle=Mathf.Repeat(angle,360);
                float margin=Mathf.Asin(Mathf.Clamp01(.45f/Mathf.Max(.45f,delta.magnitude)))*Mathf.Rad2Deg;
                if(angle<previous-margin || angle>current+margin)continue;
                contacted.Add(target);target.ReceiveHit(fighter,damage,30);
                if(Skill==DeepFishSkill.None)break;
            }
        }

        void Contact(float range,float angle,float damage,bool unblockable)
        {
            foreach(var target in Targets)
            {
                if(!target||!target.Alive||contacted.Contains(target)||!target.OverlapsAttackHeight(transform.position.y,transform.position.y+1.6f))continue;
                var delta=target.transform.position-transform.position;delta.y=0;
                if(delta.magnitude>range-.12f||Vector3.Angle(transform.forward,delta)>angle-4||Blocked(transform.position,target.transform.position))continue;
                contacted.Add(target);landed=true;target.ReceiveHit(fighter,damage,30,unblockable);
                if(Skill==DeepFishSkill.None)break;
            }
        }
        void Area(Vector3 point,float range,float damage)
        {foreach(var target in Targets)if(target&&target.Alive&&target.OverlapsAttackHeight(point.y-.1f,point.y+.3f)&&Vector3.ProjectOnPlane(target.transform.position-point,Vector3.up).magnitude<range-.18f&&!Blocked(point,target.transform.position))target.ReceiveHit(fighter,damage,35,true);}
        bool Blocked(Vector3 from,Vector3 to)=>Physics.Linecast(from+Vector3.up*.8f,to+Vector3.up*.8f,1,QueryTriggerInteraction.Ignore);
        void Impact(Vector3 point)
        {game.Effects.Burst(point+Vector3.up*.2f,new Color(.2f,.8f,1),25);game.CameraRig.Kick(.65f);game.Effects.SkillImpact(point,new Color(.12f,.7f,1));}

        void ShootFan(int wave)
        {
            var delta=(game.CombatTarget.transform.position-transform.position).normalized;
            int count=(Enraged?9:7)+Mathf.Min(4,game.PartySize-1);
            for(int i=0;i<count;i++)
            {
                var h=Available();if(h==null)break;
                var direction=Quaternion.AngleAxis((i-(count-1)*.5f)*12+(variant==0?(wave%2==1?6:0):(wave-1)*9),Vector3.up)*delta;
                h.position=transform.position+Vector3.up*.85f+direction*.9f;h.velocity=direction*(Enraged?7.5f:6.2f);
                h.radius=.27f;h.age=0;h.hit=false;h.hitPlayers.Clear();h.active=true;h.shockwave=false;h.visual.gameObject.SetActive(true);
                h.visual.position=h.position;h.visual.GetComponent<TrailRenderer>().Clear();h.visual.localScale=Vector3.one*.54f;h.ring.enabled=false;
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
                        if(!target||!target.Alive||h.hitPlayers.Contains(target)||!target.OverlapsAttackHeight(h.position.y-.1f,h.position.y+.3f))continue;
                        float distance=Vector3.ProjectOnPlane(target.transform.position-h.position,Vector3.up).magnitude;
                        if(distance>=previous-.26f&&distance<=h.radius+.26f&&!Blocked(h.position,target.transform.position))
                        {h.hitPlayers.Add(target);target.ReceiveHit(fighter,Enraged?22:20,20,true);}
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
                        if((to-step*fraction).sqrMagnitude<.45f*.45f){target.ReceiveHit(fighter,16,12);touched=true;break;}
                    }
                    if(touched){game.Effects.Burst(h.position,new Color(.15f,.8f,1),12);game.Effects.Sound("bubblePop",h.position);Release(h);continue;}
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
            bool sector=!world && to-from<359;
            for(int i=0;i<ringPoints.Length;i++)
            {float fraction=sector?(float)Mathf.Clamp(i-1,0,62)/62:(float)i/(ringPoints.Length-1);float a=(yaw+Mathf.Lerp(from,to,fraction))*Mathf.Deg2Rad;ringPoints[i]=sector && (i==0 || i==64)?center+Vector3.up*.10f:center+new Vector3(Mathf.Sin(a)*radius,.10f,Mathf.Cos(a)*radius);}
            line.SetPositions(ringPoints);line.startColor=line.endColor=tint;line.enabled=true;
        }
        void DrawCharge(Color tint)
        {
            float length=Skill==DeepFishSkill.RiptideRush?duration*.22f*(Enraged?19:17)+1.9f:duration*.33f*(Enraged?18:15)+1.9f;
            Vector3 a=origin+Vector3.up*.10f,b=a+heading*length,side=Vector3.Cross(Vector3.up,heading)*1.9f;
            // Four corners are interpolated into the existing buffer; no per-frame allocation.
            for(int i=0;i<65;i++)
            {float p=i/16f;int edge=Mathf.Min(3,(int)p);float t=p-edge;Vector3 start=edge==0?a-side:edge==1?b-side:edge==2?b+side:a+side;Vector3 end=edge==0?b-side:edge==1?b+side:edge==2?a+side:a-side;ringPoints[i]=Vector3.Lerp(start,end,t);}
            warning.SetPositions(ringPoints);warning.startColor=warning.endColor=tint;warning.enabled=true;
        }
        void HideWarnings(){if(warning)warning.enabled=false;if(fill)fill.enabled=false;if(sweep)sweep.enabled=false;foreach(var line in markWarnings)if(line)line.enabled=false;}
        public void CancelAttack()
        {Skill=DeepFishSkill.None;age=duration=0;markCount=comboDepth=0;cooldown=.9f;HideWarnings();foreach(var h in hazards)if(h!=null)Release(h);}
        public void ResetBoss(){CancelAttack();Enraged=Frenzied=false;previousChoice=DeepFishSkill.None;reactiveCooldown=regroupTime=pursuitTime=0;waves=AttacksStarted=0;orbitSide=1;}
        void OnDestroy(){if(water)Destroy(water);if(bubble)Destroy(bubble);}
    }
}
