using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    public struct TrialChampionHazard
    {public Vector3 position,velocity,endpoint;public float age,radius,limit,lifetime,aux;public byte kind;public bool ring,active,unblockable;}
    public struct TrialChampionState
    {public int move,beat,phase,kills;public byte mobility;public float age,cooldown,mobilityAge,mobilityDuration,respite,guardTime,introAge;public bool introPlayed;public Vector3 aim,heading,origin,mobilityDirection;public TrialChampionHazard[] hazards;}
    public enum ChampionMobility : byte { Grounded, BurstStep, SideSlip, BackLeap, Vault }

    // Host-owned attack clocks; telegraphs and contacts use the very same geometry.
    public sealed partial class TrialChampion : MonoBehaviour
    {
        public ChampionKind Kind {get;private set;}
        public int MoveIndex {get;private set;}=-1;
        public int Phase {get;private set;}=1;
        public ChampionMove Move=>TrialChampionCatalog.Move(Kind,MoveIndex);
        public float Age {get;private set;}
        public float WarningProgress=>Attacking?Mathf.Clamp01(1-(NextImpact-Age)/Mathf.Max(.01f,Mathf.Min(Move.windup,beat==0?Move.windup:Move.interval))):0;
        public float BeatAge=>Age-(beat==0?0:Move.windup+(beat-1)*Move.interval);
        public bool Attacking=>MoveIndex>=0;
        public bool WindingUp=>Attacking && beat<Move.beats && Age<NextImpact;
        public bool Unblockable=>Attacking && Move.Unblockable;
        public bool CanDeflect=>Attacking && !Move.Unblockable;
        public bool Recovering=>Attacking && beat>=Move.beats;
        public string SkillName=>Attacking?Move.name:fighter.Guarding?"架剑 · 识破轻击":Regrouping?"迂回 · 重新观察":cooldown>1 && Kind==ChampionKind.Taffy?"换弹 · 贴身压制":"";
        public string Counter=>Attacking?Move.counter:"";
        public int AttacksStarted {get;private set;}
        public int ActiveHazards {get {int count=0;foreach(var h in hazards)if(h.active)count++;return count;}}
        public bool WeaponAway {get {if(Kind!=ChampionKind.Anon && Kind!=ChampionKind.Penguin && !IsRemnant)return false;foreach(var h in hazards)if(h.active && h.kind==ChampionHazardKind.Boomerang)return true;return false;}}
        public Transform Visual {get;private set;}
        public TrialChampionRig Rig {get;private set;}
        public Vector3 Aim=>aim;
        public Vector3 Heading=>heading;
        public bool CounterReady=>Attacking && Move.shape==ChampionShape.Counter && Age<Move.windup && !counterTriggered;
        public ChampionMobility Mobility {get;private set;}
        public Vector3 MobilityDirection=>mobilityDirection;
        public float MobilityProgress=>mobilityDuration>0?Mathf.Clamp01(mobilityAge/mobilityDuration):0;
        public float AimCommitTime=>Move.Unblockable?Mathf.Min(.28f,Move.windup*.42f):Mathf.Min(.16f,Move.interval*.45f);
        public float AnimationBeat=>beat>0 && Age-(Move.windup+(beat-1)*Move.interval)<.105f?.38f+Mathf.Clamp01((Age-(Move.windup+(beat-1)*Move.interval))/.105f)*.24f:WindingUp?Mathf.Clamp01(1-(NextImpact-Age)/Mathf.Min(Move.windup,beat==0?Move.windup:Move.interval))*.38f:Recovering?.65f+Mathf.Clamp01((Age-(Move.windup+Move.interval*(Move.beats-1)))/(Move.recovery+.2f))*.35f:.5f;
        float NextImpact=>Move.windup+beat*Move.interval;
        TrialEnemy fighter;
        TrialGame game;
        int beat,sequence,previous=-1,phaseAnnounced=1;
        float cooldown=1.0f,phaseAge,aimLockedAt,mobilityAge,mobilityDuration,mobilityCooldown,evasionCooldown,decisionClock,pressureAge;
        Vector3 mobilityDirection,steering;
        float mobilitySpeed;
        float mobilityGroundY;
        int queuedMove=-1,orbitSide=1;
        readonly float[] moveWeights=new float[TrialChampionCatalog.SkillCount];
        uint usedMoves;
        bool counterTriggered;
        Vector3 firstHeading;
        Vector3 aim,heading,origin;
        Material lineMaterial;
        Material projectileMaterial;
        Texture2D projectileTexture;
        LineRenderer warning,innerWarning,charge,trace;
        readonly Vector3[] points=new Vector3[65];
        readonly HashSet<TrialPlayer> contacted=new HashSet<TrialPlayer>();
        readonly List<TrialPlayer> targets=new List<TrialPlayer>(4);
        readonly Hazard[] hazards=new Hazard[48];
        List<TrialPlayer> Targets=>targets;
        sealed class Hazard
        {
            public LineRenderer line;public Transform prop;public TrialSignatureProp art;public Vector3 position,velocity,endpoint;public float age,radius,damage,limit=23,lifetime=3,aux;public bool active,ring,unblockable,bounced;public ChampionHazardKind kind;
            public readonly HashSet<TrialPlayer> hit=new HashSet<TrialPlayer>();
        }

        public void Initialize(TrialEnemy owner,TrialGame trial,ChampionKind kind,GameObject prefab)
        {
            fighter=owner;game=trial;Kind=kind;targets.Add(trial.Player);
            Visual=Instantiate(prefab,transform).transform;Visual.name=TrialChampionCatalog.Name(kind);
            Rig=Visual.GetComponent<TrialChampionRig>();Rig.Initialize(owner,this);
            lineMaterial=new Material(Resources.Load<Shader>("Trial/VFX/SignatureEnergy")){name=Kind+" pooled skill ink"};
            lineMaterial.SetColor("_BaseColor",Color.white);lineMaterial.SetFloat("_Surface",1);lineMaterial.SetInt("_ZWrite",0);
            lineMaterial.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);lineMaterial.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);lineMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");lineMaterial.renderQueue=3000;
            lineMaterial.SetFloat("_Cull",(float)CullMode.Off);
            // A braided core and soft edges give flying lashes / blades a surface,
            // while preserving the exact collision and warning geometry.
            projectileTexture=new Texture2D(64,16,TextureFormat.RGBA32,false){name=Kind+" braided skill texture",wrapMode=TextureWrapMode.Repeat};
            var pixels=new Color[64*16];
            for(int y=0;y<16;y++)for(int x=0;x<64;x++)
            {float edge=Mathf.Sin((y+.5f)/16*Mathf.PI);float braid=.68f+.32f*Mathf.Cos(x*Mathf.PI/8+y*Mathf.PI/4);pixels[y*64+x]=new Color(braid,braid,braid,edge);}
            projectileTexture.SetPixels(pixels);projectileTexture.Apply(false,true);
            projectileMaterial=new Material(lineMaterial){name=Kind+" textured weapon energy"};projectileMaterial.SetTexture("_BaseMap",projectileTexture);
            warning=Line("Attack boundary",.10f,65);innerWarning=Line("Safe inner boundary",.08f,65);charge=Line("Windup progress",.065f,65);trace=Line("Weapon contact",.16f,2);
            for(int i=0;i<hazards.Length;i++){var line=Line("Pooled projectile "+i,.075f,2);line.sharedMaterial=projectileMaterial;line.textureMode=LineTextureMode.Tile;hazards[i]=new Hazard{line=line};}
            Rig.CreateWeapon(trial.World,trial.ChampionGun,projectileMaterial);
            InitializeSignature();
            if(Kind==ChampionKind.Anon || Kind==ChampionKind.Penguin || IsRemnant)for(int i=0;i<2;i++)hazards[i].prop=Rig.CloneProjectileWeapon(transform);
            warning.gameObject.AddComponent<TrialSkillSurface>().Initialize(warning);
            ResetEncounter();
        }
        LineRenderer Line(string label,float width,int count)
        {
            var go=new GameObject(label);go.layer=2;go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=lineMaterial;line.useWorldSpace=true;line.positionCount=count;line.widthMultiplier=width;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;line.numCornerVertices=0;line.numCapVertices=0;return line;
        }
        public void ResetEncounter()
        {CancelAttack(true);ResetTactics();ResetSignature();Phase=phaseAnnounced=1;sequence=0;previous=-1;usedMoves=0;phaseAge=0;cooldown=Kind==ChampionKind.NuoNuo?1.1f:.65f;mobilityCooldown=.2f;evasionCooldown=1;pressureAge=decisionClock=0;Mobility=ChampionMobility.Grounded;mobilityAge=mobilityDuration=0;queuedMove=-1;orbitSide=Kind==ChampionKind.NuoNuo?-1:1;AttacksStarted=0;Rig.ResetPose();}
        public void CancelAttack(bool clearHazards=false)
        {
            guardTime=0;guardCounter=false;fighter.BossGuard(false);
            MoveIndex=-1;Age=0;beat=0;cooldown=Mathf.Max(cooldown,.75f);HideWarnings();
            if(clearHazards){Mobility=ChampionMobility.Grounded;queuedMove=-1;foreach(var h in hazards){h.active=false;h.hit.Clear();h.line.enabled=false;if(h.art)h.art.Hide();if(h.prop)h.prop.gameObject.SetActive(false);}}
        }
        internal void BreakPressure()
        {
            CancelAttack(true);phaseAge=0;phaseAnnounced=Phase;
            bool ranged=Kind==ChampionKind.Taffy || Kind==ChampionKind.YaYa;
            if(ranged)
            {
                Vector3 away=Planar(transform.position-game.CombatTarget.transform.position).normalized;
                if(away.sqrMagnitude<.1f)away=-transform.forward;
                StartMobility(ChampionMobility.BackLeap,away,.24f,13,0);
            }
            else BeginMove(Kind==ChampionKind.Penguin?3:Kind==ChampionKind.Anon?0:2);
        }
        public void Tick(float dt)
        {
            targets.Clear();if(Mirror.NetworkServer.active){foreach(var actor in TrialNetworkManager.Current.Players)if(actor)targets.Add(actor);}else targets.Add(game.Player);
            if(IsRemnant && !Attacking && !Regrouping)SelectRemnantTarget();
            if(!fighter.Alive){CancelAttack(true);return;}
            if(!game.CombatTarget || !game.CombatTarget.Alive){CancelAttack(true);return;}
            if(!fighter.Awakened){HideWarnings();return;}
            if(TickSignatureIntro(dt))return;
            TickHazards(dt);
            mobilityCooldown-=dt;evasionCooldown-=dt;decisionClock-=dt;pressureAge+=dt;
            if(fighter.Vulnerable){Mobility=ChampionMobility.Grounded;queuedMove=-1;CancelAttack();fighter.BossMotion(0);if(fighter.ActionTime>=fighter.ActionDuration)fighter.BossBegin(FighterAction.Free,0);return;}
            Phase=fighter.Health<fighter.MaxHealth*.3f?3:fighter.Health<fighter.MaxHealth*.65f?2:1;
            var partner=Partner();if(partner && !partner.Alive)Phase=3;
            if(Phase>phaseAnnounced && !Attacking && !fighter.ResolveActive)
            {
                phaseAge+=dt;fighter.BossMotion(0);
                if(phaseAge<.95f){if(phaseAge<=dt){game.Hud.Notify(TrialChampionCatalog.Name(Kind)+" · "+(Phase==3?"决意终式":"二阶段解放"),2);game.Effects.Burst(transform.position+Vector3.up,TrialChampionCatalog.Color(Kind),40);if(fighter==game.Player.Target)game.CameraRig.CombatFrame(fighter,.7f,.5f);}return;}
                phaseAnnounced=Phase;phaseAge=0;cooldown=.35f;
            }
            if(Attacking){TickAttack(dt);return;}
            if(Mobility!=ChampionMobility.Grounded){TickMobility(dt);return;}
            cooldown-=dt;
            var delta=Planar(game.CombatTarget.transform.position-transform.position);float distance=delta.magnitude;float range=Mathf.Max(.2f,Move.range-.18f);
            fighter.BossFace(delta,640,dt);
            bool gun=Kind==ChampionKind.Taffy;float desired=gun?10:Kind==ChampionKind.YaYa?5.4f:Kind==ChampionKind.Penguin?3.9f:2.8f;
            bool partnerActive=partner && partner.Alive && partner.Champion.Attacking && !partner.Champion.Recovering;
            var target=game.CombatTarget;
            if(TickRespite(dt,delta))return;
            if(TickSwordDefense(dt,target,distance))return;
            pursuitAge+=dt;
            if(pursuitAge>2.6f && distance>desired+2){BeginRespite();TickRespite(dt,delta);return;}
            // Commands come from the same authoritative player simulation for local and remote peers.
            if(mobilityCooldown<=0 && decisionClock<=0)
            {
                if((target.Action==FighterAction.Heal || IsRemnant && target.CommandHeal) && distance>4 && distance<17 && !partnerActive)
                {if(gun){BeginMove(1);return;}StartMobility(ChampionMobility.BurstStep,delta.normalized,.26f,Kind==ChampionKind.Penguin?11:17,1);return;}
                if(distance<5.3f && (target.CommandHeavy || target.Action==FighterAction.Heavy && target.Progress<.28f || target.Action==FighterAction.Charge) && evasionCooldown<=0 && pressureAge>.35f && Kind!=ChampionKind.Penguin)
                {orbitSide=-orbitSide;evasionCooldown=IsRemnant?1.65f:3.5f;StartMobility(gun||Kind==ChampionKind.NuoNuo?ChampionMobility.BackLeap:Kind==ChampionKind.YaYa?ChampionMobility.Vault:ChampionMobility.SideSlip,gun?-delta.normalized:Vector3.Cross(Vector3.up,delta).normalized*orbitSide,.28f,12,-1);return;}
                if((distance>desired+4 || gun && distance<5.5f) && !partnerActive)
                {StartMobility(gun && distance<5.5f?ChampionMobility.BackLeap:ChampionMobility.BurstStep,delta.normalized*(gun && distance<5.5f?-1:1),.30f,Kind==ChampionKind.Penguin?12:16,-1);return;}
            }
            if(decisionClock<=0)
            {
                decisionClock=.12f;
                Vector3 radial=delta.normalized;Vector3 tangent=Vector3.Cross(Vector3.up,radial)*orbitSide;
                float close=Mathf.Clamp((distance-desired)*.8f,-1,1);
                steering=radial*close+tangent*(distance<desired+3?.65f:.25f);
                if(partner && partner.Alive){var away=Planar(transform.position-partner.transform.position);if(away.sqrMagnitude<49)steering+=away.normalized*.85f;}
                if(Physics.SphereCast(transform.position+Vector3.up,.4f,steering.normalized,out var hit,1.4f,1,QueryTriggerInteraction.Ignore)){orbitSide=-orbitSide;steering=Vector3.Cross(Vector3.up,hit.normal)*orbitSide;}
            }
            float speed=distance>desired+2?Kind==ChampionKind.Penguin?5.5f:7.2f:4.2f;
            fighter.BossMove(steering.normalized*speed,dt);fighter.BossMotion(steering.sqrMagnitude>.05f?1:0);
            if(cooldown>0 || distance>(gun?32:Kind==ChampionKind.YaYa?14:Kind==ChampionKind.Penguin?13:8))return;
            // Dual pressure is sequenced. Only a specifically telegraphed cross-combo overlaps.
            if(partnerActive){cooldown=.12f;return;}
            int index=ChooseMove(distance);BeginMove(index);
        }
        TrialEnemy Partner()
        {
            if(Kind!=ChampionKind.FeiBi && Kind!=ChampionKind.NuoNuo)return null;
            for(int i=0;i<game.Enemies.Count;i++){var e=game.Enemies[i];if(e!=fighter && e.Champion)return e;}return null;
        }
        int ChooseMove(float distance)
        {
            int max=TrialChampionCatalog.SkillCount;
            uint all=(1u<<max)-1;if((usedMoves&all)==all)usedMoves=previous>=0?1u<<previous:0;
            var player=game.CombatTarget;float total=0;
            for(int i=0;i<max;i++)
            {
                var m=TrialChampionCatalog.Move(Kind,i);float score=(usedMoves&(1u<<i))!=0?0:1;
                if((m.shape==ChampionShape.Cut || m.shape==ChampionShape.Cross) && distance>m.range+.8f)score*=.08f;
                if(distance>6 && (m.shape==ChampionShape.Thrust || m.shape==ChampionShape.Leap || m.shape==ChampionShape.Lash))score*=3;
                if(player.Guarding && player.GuardAge>.3f && m.Unblockable)score*=2.5f;
                if(player.Action==FighterAction.Heal && (m.shape==ChampionShape.Thrust || m.shape==ChampionShape.Snipe || m.shape==ChampionShape.Mark))score*=3;
                if(Kind==ChampionKind.Taffy && distance<4.5f)score*=i==3?8:.2f;
                if(Kind==ChampionKind.YaYa && distance<3.5f)score*=i==2?4:i==1?.2f:1;
                if(i>=8)score*=1.4f;
                moveWeights[i]=score;total+=score;
            }
            if(IsRemnant || Kind==ChampionKind.Anon)
            {
                int best=0;float highest=-1;
                for(int i=0;i<max;i++)
                {
                    var m=TrialChampionCatalog.Move(Kind,i);float score=moveWeights[i];
                    if(Kind==ChampionKind.Anon){if(distance>5 && i==4)score*=5;if(player.CommandHeavy && i==10)score*=4;if(ActiveHazards>4 && i==4)score=0;}
                    if(player.CommandDodge || player.Action==FighterAction.Dodge)score*=m.windup>.65f?3.5f:.3f;
                    if(player.CommandHeavy || player.Action==FighterAction.Charge)score*=m.shape==ChampionShape.Thrust || m.shape==ChampionShape.BladeWave?3:1;
                    if(player.CommandLight)score*=m.shape==ChampionShape.Counter?4:1;
                    if(score>highest){highest=score;best=i;}
                }
                return best;
            }
            float roll=Random.value*total;for(int i=0;i<max;i++){roll-=moveWeights[i];if(roll<=0)return i;}return max-1;
        }
        void StartMobility(ChampionMobility move,Vector3 direction,float duration,float speed,int followup)
        {
            evasionOrigin=transform.position;
            if(Kind==ChampionKind.Anon && move==ChampionMobility.SideSlip)anonBlinkQueued=true;
            game.Effects.DodgeWake(transform.position,direction,TrialChampionCatalog.Color(Kind));
            Mobility=move;mobilityDirection=direction.normalized;mobilityAge=0;mobilityDuration=duration;mobilitySpeed=speed;queuedMove=followup;mobilityCooldown=Kind==ChampionKind.Penguin?1.5f:.85f;steering=Vector3.zero;
            mobilityGroundY=transform.position.y;
            fighter.BossBegin(FighterAction.Free,0);game.Effects.Sound("dodge",transform.position,.85f);
        }
        void TickMobility(float dt)
        {
            mobilityAge+=dt;float ease=Mathf.Sin(MobilityProgress*Mathf.PI);
            var next=transform.position+mobilityDirection*mobilitySpeed*dt;
            if(next.x<-22 || next.x>22 || next.z<3 || next.z>47 || Physics.SphereCast(transform.position+Vector3.up,.45f,mobilityDirection,out _,.65f,1,QueryTriggerInteraction.Ignore))mobilityAge=mobilityDuration;
            else
            {
                float height=Mobility==ChampionMobility.Vault?1.35f:Mobility==ChampionMobility.BackLeap?.8f:.14f;
                Vector3 velocity=mobilityDirection*mobilitySpeed*Mathf.Lerp(.6f,1,ease);velocity.y=(mobilityGroundY+ease*height-transform.position.y)/Mathf.Max(.001f,dt);fighter.BossMove(velocity,dt);
            }
            fighter.BossFace(game.CombatTarget.transform.position-transform.position,840,dt);fighter.BossMotion(1.5f);
            if(mobilityAge<mobilityDuration)return;
            fighter.BossMove(Vector3.up*((mobilityGroundY-transform.position.y)/Mathf.Max(.001f,dt)),dt);
            Mobility=ChampionMobility.Grounded;cooldown=Mathf.Min(cooldown,.12f);decisionClock=0;
            int nextMove=queuedMove;queuedMove=-1;
            if(anonBlinkQueued){anonBlinkQueued=false;if(TryBlinkBehind())nextMove=2;}
            if(nextMove>=0)BeginMove(nextMove);
        }
        public void BeginMove(int index)
        {
            if(!fighter.Alive)return;
            guardTime=0;fighter.BossGuard(false);
            MoveIndex=Mathf.Clamp(index,0,TrialChampionCatalog.SkillCount-1);previous=MoveIndex;usedMoves|=1u<<MoveIndex;sequence++;beat=0;Age=0;AttacksStarted++;contacted.Clear();counterTriggered=false;
            Mobility=ChampionMobility.Grounded;pressureAge=0;steering=Vector3.zero;
            origin=transform.position;origin.y=0;LockAim();firstHeading=heading;aimLockedAt=-1;
            fighter.BossBegin(FighterAction.Heavy,Move.Duration);fighter.BossMotion(0);
            game.Effects.Sound("warning",transform.position,Move.Unblockable?.7f:1.15f);
            OnSignatureBegin();DrawWarnings();
        }
        void LockAim()
        {
            aim=game.CombatTarget.transform.position;aim.y=.045f;
            if(IsRemnant && Move.shape!=ChampionShape.Counter)aim+=Vector3.ClampMagnitude(Planar(game.CombatTarget.PlanarVelocity)*.14f,1.8f);
            if(Move.shape==ChampionShape.Annulus || Move.shape==ChampionShape.Ring || Move.shape==ChampionShape.Stomp || Move.shape==ChampionShape.Vacuum || Move.shape==ChampionShape.OrbitWhip || Move.shape==ChampionShape.Mark && Move.defense==ChampionDefense.Retreat)aim=Planar(transform.position)+Vector3.up*.045f;
            if(IsAerialStrike(Move.shape))aim=ClampArena(aim,3);
            heading=Planar(aim-transform.position).normalized;if(heading.sqrMagnitude<.1f)heading=transform.forward;
        }
        void TickAttack(float dt)
        {
            Age+=dt;
            TickSignatureAttack(dt);
            if(Recovering && Move.shape==ChampionShape.Flight)TickSpecialMotion(dt,0);
            if(beat<Move.beats)
            {
                float until=NextImpact-Age;
                // Fast parry strings commit earlier than contact; area perils keep a wider dodge cue.
                if(until>AimCommitTime && !(Move.shape==ChampionShape.Fissure && beat>0) && Move.shape!=ChampionShape.Spiral)
                {
                    LockAim();fighter.BossFace(heading,620,dt);
                    if((Move.shape==ChampionShape.Cut || Move.shape==ChampionShape.Cross) && Planar(game.CombatTarget.transform.position-transform.position).magnitude>Move.range*.68f)
                        fighter.BossMove(heading*(IsRemnant?5.5f:Kind==ChampionKind.Penguin?3.5f:4.5f),dt);
                }
                else if(aimLockedAt<0){aimLockedAt=Age;}
                TickSpecialMotion(dt,until);
                if(Age>=NextImpact)
                {
                    Contact();if(!Attacking || fighter.Vulnerable || !fighter.Alive)return;
                    beat++;aimLockedAt=-1;contacted.Clear();
                    if(beat<Move.beats){if(Move.shape!=ChampionShape.Fissure){origin=transform.position;origin.y=0;LockAim();}game.Effects.Sound("warning",transform.position,1.25f);}
                }
            }
            if(Age>=Move.Duration)
            {
                bool keepPressure=Kind==ChampionKind.Anon && MoveIndex==4 && ActiveHazards>0;
                CancelAttack();fighter.BossBegin(FighterAction.Free,0);
                if(keepPressure){BeginMove(10);return;}
                BeginRespite();
            }
            DrawWarnings();
        }
        void Contact()
        {
            var shape=Move.shape;
            SignatureContactArt();
            if(ContactSignature())return;
            if(ContactSpecial())return;
            if(shape==ChampionShape.Bullets || shape==ChampionShape.Snipe || shape==ChampionShape.Flight)
            {
                int count=shape==ChampionShape.Snipe?1:Move.angle>60?9:Move.angle>30?5:1;
                float spread=count==1?0:Move.angle;
                for(int i=0;i<count;i++)
                {
                    float angle=count==1?0:Mathf.Lerp(-spread*.5f,spread*.5f,(float)i/(count-1));
                    if(beat%2==1 && count>1)angle+=spread/(count-1)*.35f;
                    Vector3 direction=shape==ChampionShape.Flight?(aim+Vector3.up*1.05f-(transform.position+Vector3.up*1.15f)).normalized:heading;
                    SpawnHazard(transform.position+Vector3.up*1.15f,Quaternion.AngleAxis(angle,Vector3.up)*direction*(shape==ChampionShape.Snipe?38:20+Phase*2),false);
                }
                game.Effects.Burst(transform.position+heading*.7f+Vector3.up*1.2f,TrialChampionCatalog.Color(Kind),8);game.Effects.Sound("hit",transform.position,.7f);return;
            }
            if(shape==ChampionShape.Ring){game.Effects.SkillImpact(transform.position,TrialChampionCatalog.Color(Kind));SpawnHazard(transform.position+Vector3.up*.08f,Vector3.zero,true);game.Effects.Sound("heavy",transform.position);return;}
            trace.enabled=true;
            trace.startColor=trace.endColor=TrialChampionCatalog.Color(Kind);
            trace.SetPosition(0,transform.position+Vector3.up);trace.SetPosition(1,(shape==ChampionShape.Leap||shape==ChampionShape.Mark?aim:transform.position+heading*Move.range)+Vector3.up*.9f);
            game.Effects.Sound("swing",transform.position,Kind==ChampionKind.Penguin?.6f:1.1f);
            foreach(var target in Targets)
            {
                if(!target || !target.Alive || !Contains(target.transform.position) || contacted.Contains(target))continue;
                if(Physics.Linecast(transform.position+Vector3.up,target.transform.position+Vector3.up,1,QueryTriggerInteraction.Ignore))continue;
                contacted.Add(target);
                bool hit=target.ReceiveHit(fighter,Move.damage,40,Move.Unblockable);
                if(hit && Move.shape==ChampionShape.Tether)target.ApplyCombatImpulse(Planar(transform.position-target.transform.position).normalized*10);
                if(hit && Move.shape==ChampionShape.Grab)target.ThrowFromGrapple(fighter);
                if(!Attacking || fighter.Vulnerable)break;
            }
            if(IsAerialStrike(shape) || shape==ChampionShape.Mark || shape==ChampionShape.Stomp || shape==ChampionShape.Vacuum)
            {game.Effects.Burst(aim+Vector3.up*.15f,TrialChampionCatalog.Color(Kind),30);if((game.Player.transform.position-aim).sqrMagnitude<70)game.CameraRig.Kick(.22f);}
        }
        public bool Contains(Vector3 point)
        {
            Vector3 delta=Planar(point-transform.position);float distance=delta.magnitude;float range=Mathf.Max(.2f,Move.range-.18f);
            switch(Move.shape)
            {
                case ChampionShape.Leap:case ChampionShape.Dive:case ChampionShape.TwinPincer:case ChampionShape.Mark:case ChampionShape.Mine:case ChampionShape.Snare:case ChampionShape.Pillars:return Planar(point-aim).sqrMagnitude<range*range;
                case ChampionShape.Annulus:return distance>=Move.inner+.10f && distance<=range;
                case ChampionShape.Stomp:case ChampionShape.Vacuum:return distance<=range;
                case ChampionShape.Fissure:return Planar(point-(origin+heading*Move.range*(beat+1)/Move.beats)).sqrMagnitude<1.72f*1.72f;
                case ChampionShape.CrossLine:return distance<=range && (Mathf.Abs(Vector3.Dot(delta,heading))<.70f || Mathf.Abs(Vector3.Dot(delta,Vector3.Cross(Vector3.up,heading)))<.70f);
                case ChampionShape.Thrust:case ChampionShape.Lash:case ChampionShape.Snipe:case ChampionShape.Tether:
                    float along=Vector3.Dot(delta,heading);return along>=-.3f && along<=range && Mathf.Abs(Vector3.Dot(delta,Vector3.Cross(Vector3.up,heading)))<=Mathf.Max(.36f,along*Mathf.Tan(Mathf.Max(1,Move.angle*.5f-3)*Mathf.Deg2Rad));
                default:return distance>=Move.inner+.10f && distance<=range && Vector3.Angle(heading,delta)<=Mathf.Max(1,Move.angle-4);
            }
        }
        void SpawnHazard(Vector3 point,Vector3 velocity,bool ring)
        {
            foreach(var h in hazards)if(!h.active)
            {h.active=true;h.position=point;h.velocity=velocity;h.age=0;h.radius=ring?.6f:.16f;h.ring=ring;h.kind=ring?ChampionHazardKind.Ring:ChampionHazardKind.Bullet;h.damage=Move.damage;h.limit=Move.range;h.lifetime=3;h.unblockable=Move.Unblockable;h.hit.Clear();h.line.positionCount=ring?65:2;h.line.widthMultiplier=ring?.18f:.10f;h.line.enabled=true;return;}
        }
        void TickHazards(float dt)
        {
            foreach(var h in hazards)
            {
                if(!h.active)continue;h.age+=dt;
                if(h.kind>ChampionHazardKind.Ring){TickSpecialHazard(h,dt);continue;}
                Vector3 before=h.position;
                if(h.ring)h.radius+=8*dt;else h.position+=h.velocity*dt;
                if(h.age>h.lifetime || h.ring && h.radius>h.limit || !h.ring && Physics.Linecast(before,h.position,1,QueryTriggerInteraction.Ignore))
                {h.active=false;h.line.enabled=false;continue;}
                foreach(var target in Targets)
                {
                    if(!target || !target.Alive || h.hit.Contains(target))continue;
                    bool contact=h.ring?Mathf.Abs(Planar(target.transform.position-h.position).magnitude-h.radius)<.24f+8*dt:SegmentDistance(target.transform.position+Vector3.up*1.05f,before,h.position)<.34f;
                    if(!contact)continue;h.hit.Add(target);
                    target.ReceiveHazardHit(fighter,h.damage,30,h.unblockable,h.ring?h.position-target.transform.position:-h.velocity);
                    if(!h.ring){h.active=false;h.line.enabled=false;break;}
                }
                if(h.active)DrawHazard(h);
            }
        }
        static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b)
        {Vector3 ab=b-a;return Vector3.Distance(p,a+ab*Mathf.Clamp01(Vector3.Dot(p-a,ab)/Mathf.Max(.0001f,ab.sqrMagnitude)));}
        void DrawHazard(Hazard h)
        {
            DrawSignatureProp(h);
            if(h.prop)h.prop.gameObject.SetActive(h.active && h.kind==ChampionHazardKind.Boomerang);
            if(h.kind>ChampionHazardKind.Ring){DrawSpecialHazard(h);return;}
            Color color=TrialChampionCatalog.Color(Kind);h.line.startColor=h.line.endColor=color;
            if(h.ring)Circle(h.line,h.position,h.radius,0,360);
            else{h.line.SetPosition(0,h.position);h.line.SetPosition(1,h.position-h.velocity.normalized*.85f);}
        }
        void DrawWarnings()
        {
            bool show=WindingUp;warning.enabled=charge.enabled=show;innerWarning.enabled=show && Move.shape==ChampionShape.Annulus;
            trace.enabled=Attacking && beat>0 && Age-(Move.windup+(beat-1)*Move.interval)<.10f;
            if(!show)return;
            warning.positionCount=innerWarning.positionCount=charge.positionCount=65;warning.widthMultiplier=.1f;innerWarning.widthMultiplier=.08f;
            Color color=Move.Unblockable?Move.defense==ChampionDefense.Sidestep?new Color(.25f,.7f,1):new Color(1,.22f,.16f):new Color(1,.78f,.22f);
            warning.startColor=warning.endColor=color;innerWarning.startColor=innerWarning.endColor=new Color(.35f,1,.6f);
            charge.startColor=charge.endColor=Color.Lerp(color,Color.white,.35f);
            float remaining=NextImpact-Age;float amount=Mathf.Clamp01(1-remaining/Mathf.Min(Move.windup,beat==0?Move.windup:Move.interval));
            Vector3 center=Planar(transform.position)+Vector3.up*.055f;
            if(DrawSignatureWarnings(center,amount))return;
            if(DrawSpecialWarnings(center,amount))return;
            switch(Move.shape)
            {
                case ChampionShape.Leap:case ChampionShape.Dive:case ChampionShape.TwinPincer:case ChampionShape.Mark:case ChampionShape.Mine:case ChampionShape.Snare:case ChampionShape.Pillars:Circle(warning,aim,Move.range,0,360);Circle(charge,aim,Move.range*amount,0,360);break;
                case ChampionShape.Annulus:Circle(warning,center,Move.range,0,360);Circle(innerWarning,center,Move.inner,0,360);Circle(charge,center,Mathf.Lerp(Move.range,Move.inner,amount),0,360);break;
                case ChampionShape.Ring:Circle(warning,center,2.4f,0,360);Circle(charge,center,2.4f*amount,0,360);break;
                case ChampionShape.Stomp:case ChampionShape.Vacuum:Circle(warning,center,Move.range,0,360);Circle(charge,center,Move.range*amount,0,360);break;
                case ChampionShape.Cut:case ChampionShape.Cross:case ChampionShape.Counter:case ChampionShape.Grab:case ChampionShape.DashCut:case ChampionShape.Spiral:
                    Arc(warning,center,Move.range,Move.angle);Arc(charge,center,Move.range*amount,Move.angle);break;
                default:
                    float range=Move.range;float width=Mathf.Clamp(range*Mathf.Tan(Mathf.Max(1,Move.angle*.5f-3)*Mathf.Deg2Rad),.45f,14);
                    Vector3 right=Vector3.Cross(Vector3.up,heading);
                    for(int i=0;i<65;i++)
                    {float t=i/64f*3;points[i]=t<1?Vector3.Lerp(center,center+heading*range-right*width,t):t<2?Vector3.Lerp(center+heading*range-right*width,center+heading*range+right*width,t-1):Vector3.Lerp(center+heading*range+right*width,center,t-2);}
                    warning.SetPositions(points);Circle(charge,center,1.15f*amount,0,360);break;
            }
        }
        void Arc(LineRenderer line,Vector3 center,float radius,float arc)
        {
            float yaw=Mathf.Atan2(heading.x,heading.z)*Mathf.Rad2Deg;
            points[0]=center;points[64]=center;
            for(int i=1;i<64;i++){float a=(yaw+Mathf.Lerp(-arc,arc,(i-1)/62f))*Mathf.Deg2Rad;points[i]=center+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*radius;}
            line.SetPositions(points);
        }
        void Circle(LineRenderer line,Vector3 center,float radius,float start,float end)
        {for(int i=0;i<65;i++){float a=Mathf.Lerp(start,end,i/64f)*Mathf.Deg2Rad;points[i]=center+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*radius;}line.SetPositions(points);}
        void HideWarnings(){if(warning)warning.enabled=false;if(innerWarning)innerWarning.enabled=false;if(charge)charge.enabled=false;if(trace)trace.enabled=false;}
        public void Deflected()
        {
            fighter.AddChampionPosture(Kind==ChampionKind.Anon?44:62);
            if(fighter.Vulnerable){CancelAttack();return;}
            if(Kind!=ChampionKind.Anon){CancelAttack();fighter.BossBegin(FighterAction.Stagger,.65f);}
            // The sword saint retains her remaining beats: each successful deflect invites the next.
        }
        public TrialChampionState CaptureNetwork()
        {
            int count=ActiveHazards;var snapshot=new TrialChampionState{move=MoveIndex,phase=Phase,beat=beat,kills=KillStacks,introAge=introAge,introPlayed=introPlayed,respite=respite,guardTime=guardTime,age=Age,cooldown=cooldown,aim=aim,heading=heading,origin=origin,mobility=(byte)Mobility,mobilityAge=mobilityAge,mobilityDuration=mobilityDuration,mobilityDirection=mobilityDirection,hazards=count==0?System.Array.Empty<TrialChampionHazard>():new TrialChampionHazard[count]};int i=0;
            foreach(var h in hazards)if(h.active)snapshot.hazards[i++]=new TrialChampionHazard{position=h.position,velocity=h.velocity,endpoint=h.endpoint,age=h.age,radius=h.radius,limit=h.limit,lifetime=h.lifetime,aux=h.aux,kind=(byte)h.kind,ring=h.ring,unblockable=h.unblockable,active=true};return snapshot;
        }
        public void ApplyNetwork(TrialChampionState state,float advance)
        {
            bool contactBeat=state.move>=0 && state.beat>0 && (state.move!=MoveIndex || state.beat!=beat || state.age+.1f<Age);
            bool newEvade=state.mobility!=0 && ((byte)Mobility!=state.mobility || state.mobilityAge+.05f<mobilityAge);
            bool newMove=state.move>=0 && (state.move!=MoveIndex || state.age+.1f<Age || state.beat!=beat);
            MoveIndex=state.move;Phase=state.phase;beat=state.beat;Age=state.age+advance;cooldown=state.cooldown;aim=state.aim;heading=state.heading;origin=state.origin;
            introAge=state.introAge;introPlayed=state.introPlayed;DrawSignatureIntro();
            KillStacks=state.kills;respite=state.respite;guardTime=state.guardTime;
            if(contactBeat && Attacking)SignatureContactArt();
            if(newEvade)game.Effects.DodgeWake(transform.position,state.mobilityDirection,TrialChampionCatalog.Color(Kind));
            if(newMove && Attacking)game.Effects.Sound("warning",transform.position,Move.Unblockable?.7f:1.15f);
            Mobility=(ChampionMobility)state.mobility;mobilityAge=state.mobilityAge+advance;mobilityDuration=state.mobilityDuration;mobilityDirection=state.mobilityDirection;
            for(int i=0;i<hazards.Length;i++)
            {var h=hazards[i];bool active=state.hazards!=null && i<state.hazards.Length;h.active=active;h.line.enabled=active;if(!active){if(h.art)h.art.Hide();if(h.prop)h.prop.gameObject.SetActive(false);continue;}var r=state.hazards[i];h.position=r.position;h.velocity=r.velocity;h.endpoint=r.endpoint;h.age=r.age;h.radius=r.radius;h.ring=r.ring;h.limit=r.limit;h.lifetime=r.lifetime;h.aux=r.aux;h.kind=(ChampionHazardKind)r.kind;h.line.positionCount=h.ring?65:2;DrawHazard(h);}
            DrawWarnings();
        }
        public static Vector3 Planar(Vector3 value){value.y=0;return value;}
        static Vector3 ClampArena(Vector3 value,float inset){value.x=Mathf.Clamp(value.x,-23+inset,23-inset);value.z=Mathf.Clamp(value.z,2+inset,48-inset);return value;}
        void OnDestroy(){if(signatureCore)Destroy(signatureCore);if(lineMaterial)Destroy(lineMaterial);if(projectileMaterial)Destroy(projectileMaterial);if(projectileTexture)Destroy(projectileTexture);}
    }
}
