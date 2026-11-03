using UnityEngine;

namespace TomorinTrial
{
    public enum ChampionHazardKind : byte { Bullet, Ring, Blade, Boomerang, Ricochet, Rocket, Mine, Ray, Turret, Silk, Pillar, Boulder, WhipOrbit }
    public sealed partial class TrialChampion
    {
        static bool IsAerialStrike(ChampionShape shape)=>shape==ChampionShape.Leap || shape==ChampionShape.Dive || shape==ChampionShape.TwinPincer;
        public bool TryCounter(bool heavy)
        {
            if(!CounterReady)return false;
            if(heavy){fighter.AddChampionPosture(100);CancelAttack();fighter.BossBegin(FighterAction.Stagger,.85f);return false;}
            counterTriggered=true;Age=Mathf.Max(Age,Move.windup-.24f);game.Effects.Sound("parry",transform.position);game.Effects.Burst(transform.position+Vector3.up,TrialChampionCatalog.Color(Kind),14);return true;
        }
        void TickSpecialMotion(float dt,float until)
        {
            var shape=Move.shape;
            if(IsAerialStrike(shape))
            {
                float start=beat==0?0:Move.windup+(beat-1)*Move.interval;
                float span=beat==0?Move.windup:Move.interval;
                float t=Mathf.Clamp01((Age-start)/Mathf.Max(.01f,span));
                Vector3 destination=Vector3.Lerp(origin,Planar(aim),Mathf.SmoothStep(0,1,t));
                if(shape==ChampionShape.Dive)destination+=Vector3.Cross(Vector3.up,heading)*Mathf.Sin(t*Mathf.PI)*2.3f*orbitSide;
                if(shape==ChampionShape.TwinPincer)destination+=Vector3.Cross(Vector3.up,heading)*Mathf.Sin(t*Mathf.PI)*3.0f*orbitSide;
                destination.y=origin.y+Mathf.Sin(t*Mathf.PI)*(shape==ChampionShape.Dive?3.7f:shape==ChampionShape.TwinPincer?2.1f:2.8f);
                fighter.BossMove((destination-transform.position)/Mathf.Max(.001f,dt),dt);
            }
            else if(shape==ChampionShape.Flight)
            {
                float t=Mathf.Clamp01(Age/Move.Duration);Vector3 desired=origin+Vector3.Cross(Vector3.up,firstHeading)*Mathf.Sin(t*Mathf.PI)*4*orbitSide;
                desired.y=origin.y+Mathf.Sin(t*Mathf.PI)*2.8f;fighter.BossMove((desired-transform.position)/Mathf.Max(.001f,dt),dt);
            }
            else if(shape==ChampionShape.Spiral)
            {
                heading=Quaternion.AngleAxis(Age*145*orbitSide,Vector3.up)*firstHeading;
                fighter.BossFace(heading,900,dt);
                Vector3 tangent=Vector3.Cross(Vector3.up,Planar(game.CombatTarget.transform.position-transform.position).normalized)*orbitSide;
                fighter.BossMove(tangent*(Kind==ChampionKind.Penguin?3.2f:5.8f),dt);
            }
            else if(shape==ChampionShape.SlideVolley)
            {
                fighter.BossMove(Vector3.Cross(Vector3.up,firstHeading)*orbitSide*6,dt);
            }
            else if(shape==ChampionShape.DashCut && until>.03f && until<.22f)
            {
                float distance=Planar(game.CombatTarget.transform.position-transform.position).magnitude;
                if(distance>1.4f)fighter.BossMove(heading*Mathf.Min(15,(distance-1.3f)/Mathf.Max(.01f,dt)),dt);
            }
            else if(shape==ChampionShape.Vacuum && WindingUp)
            {
                foreach(var target in Targets)if(target && target.Alive)
                {Vector3 delta=Planar(transform.position-target.transform.position);if(delta.sqrMagnitude<(Move.range+4)*(Move.range+4))target.ApplyCombatImpulse(delta.normalized*1.7f);}
            }
        }
        bool ContactSpecial()
        {
            var shape=Move.shape;Vector3 muzzle=transform.position+Vector3.up*1.1f;
            switch(shape)
            {
                case ChampionShape.Counter:return !counterTriggered;
                case ChampionShape.BladeWave:
                    SpawnSpecial(ChampionHazardKind.Blade,muzzle,heading*24,.6f,3);game.Effects.Sound("swing",muzzle);return true;
                case ChampionShape.Boomerang:
                    SpawnSpecial(ChampionHazardKind.Boomerang,muzzle,heading*(Kind==ChampionKind.Penguin?16:22),Kind==ChampionKind.Penguin?.8f:.5f,2.8f);return true;
                case ChampionShape.Ricochet:
                case ChampionShape.Pellets:
                case ChampionShape.SlideVolley:
                    int count=shape==ChampionShape.Pellets?13:shape==ChampionShape.Ricochet?5:3;
                    for(int i=0;i<count;i++)
                    {
                        float yaw=Mathf.Lerp(-Move.angle*.5f,Move.angle*.5f,(float)i/(count-1));
                        var h=SpawnSpecial(shape==ChampionShape.Ricochet?ChampionHazardKind.Ricochet:ChampionHazardKind.Bullet,muzzle,Quaternion.AngleAxis(yaw,Vector3.up)*heading*(shape==ChampionShape.Pellets?25:22),.18f,shape==ChampionShape.Pellets?.8f:3);
                        if(h!=null)h.damage=Move.damage*(shape==ChampionShape.Pellets?.68f:1);
                    }
                    game.Effects.Sound("hit",muzzle,.8f);return true;
                case ChampionShape.Rocket:SpawnSpecial(ChampionHazardKind.Rocket,muzzle,heading*13,.35f,3.8f);return true;
                case ChampionShape.Mine:case ChampionShape.Snare:
                    SpawnSpecial(ChampionHazardKind.Mine,aim,Vector3.zero,Move.range,7.5f);return true;
                case ChampionShape.SweepRay:case ChampionShape.OrbitWhip:
                    var ray=SpawnSpecial(shape==ChampionShape.SweepRay?ChampionHazardKind.Ray:ChampionHazardKind.WhipOrbit,Planar(transform.position)+Vector3.up*1.05f,firstHeading,Move.inner,2.5f);
                    if(ray!=null){ray.limit=Move.range;ray.aux=shape==ChampionShape.OrbitWhip?300:110;}return true;
                case ChampionShape.Turret:
                    Vector3 turret=ClampArena(Planar(transform.position)+Quaternion.AngleAxis(beat*120,Vector3.up)*heading*5.5f,3)+Vector3.up*.8f;
                    var sentry=SpawnSpecial(ChampionHazardKind.Turret,turret,Vector3.zero,.7f,6);if(sentry!=null){sentry.endpoint=game.CombatTarget.transform.position+Vector3.up*1.05f;sentry.aux=.85f;}return true;
                case ChampionShape.Fissure:
                    Vector3 impact=origin+heading*(Move.range*(beat+1)/Move.beats);impact.y=.06f;
                    var crack=SpawnSpecial(ChampionHazardKind.Pillar,impact,Vector3.zero,1.9f,1.3f);if(crack!=null)crack.aux=-1;return true;
                case ChampionShape.Pillars:
                    for(int i=0;i<3;i++)
                    {Vector3 pos=aim+Quaternion.AngleAxis(i*120+beat*35,Vector3.up)*Vector3.forward*.85f;SpawnSpecial(ChampionHazardKind.Pillar,pos,Vector3.zero,1.35f,3.8f);}return true;
                case ChampionShape.Avalanche:
                    for(int i=0;i<3;i++){float a=Mathf.Lerp(-Move.angle*.5f,Move.angle*.5f,i*.5f);Vector3 dir=Quaternion.AngleAxis(a+beat%2*8,Vector3.up)*heading;SpawnSpecial(ChampionHazardKind.Boulder,transform.position+Vector3.up*.7f+dir*1.2f,dir*11,.9f,4);}return true;
                case ChampionShape.SilkLine:
                    Vector3 side=Vector3.Cross(Vector3.up,heading)*Move.range*.5f;
                    var silk=SpawnSpecial(ChampionHazardKind.Silk,aim-side,Vector3.zero,.35f,4.5f);if(silk!=null)silk.endpoint=aim+side;return true;
                case ChampionShape.TwinPincer:
                    if(beat==0)
                    {
                        var partner=Partner();if(partner && partner.Alive && !partner.Champion.Attacking)
                        {Vector3 flank=Planar(game.CombatTarget.transform.position-partner.transform.position)+Vector3.Cross(Vector3.up,heading)*orbitSide*5;partner.Champion.StartMobility(ChampionMobility.Vault,flank.normalized,.4f,14,-1);}
                    }
                    return false;
                default:return false;
            }
        }
        Hazard SpawnSpecial(ChampionHazardKind kind,Vector3 point,Vector3 velocity,float radius,float lifetime,float damage=-1)
        {
            foreach(var h in hazards)if(!h.active)
            {
                h.active=true;h.kind=kind;h.ring=false;h.position=point;h.velocity=velocity;h.endpoint=point;h.radius=radius;h.lifetime=lifetime;h.limit=Attacking?Move.range:30;h.age=0;h.aux=0;h.bounced=false;
                h.damage=damage>=0?damage:Move.damage;h.unblockable=Attacking && Move.Unblockable;h.hit.Clear();h.line.positionCount=2;h.line.enabled=true;DrawHazard(h);return h;
            }
            return null;
        }
        void TickSpecialHazard(Hazard h,float dt)
        {
            if(h.age>=h.lifetime){Deactivate(h);return;}
            if(h.kind==ChampionHazardKind.Mine)
            {
                if(h.age>.7f && h.aux==0)foreach(var target in Targets)
                    if(target && target.Alive && Planar(target.transform.position-h.position).sqrMagnitude<(h.radius+.4f)*(h.radius+.4f)){h.aux=h.age+.65f;game.Effects.Sound("warning",h.position,1.4f);break;}
                if(h.aux>0 && h.age>=h.aux){Explode(h,h.radius);return;}
            }
            else if(h.kind==ChampionHazardKind.Turret)
            {
                if(h.age>=h.aux)
                {
                    SpawnSpecial(ChampionHazardKind.Bullet,h.position,(h.endpoint-h.position).normalized*18,.18f,3,h.damage);
                    h.aux=h.age+.95f;h.endpoint=game.CombatTarget.transform.position+Vector3.up*1.05f;
                    game.Effects.Sound("hit",h.position,.6f);
                }
            }
            else if(h.kind==ChampionHazardKind.Silk || h.kind==ChampionHazardKind.Pillar)
            {
                float delay=h.kind==ChampionHazardKind.Silk?.55f:h.aux<0?0:.65f;
                if(h.age>delay)foreach(var target in Targets)
                {
                    if(!target || !target.Alive || h.hit.Contains(target))continue;
                    bool contact=h.kind==ChampionHazardKind.Silk?SegmentDistance(Planar(target.transform.position)+Vector3.up*.045f,h.position,h.endpoint)<.42f:Planar(target.transform.position-h.position).sqrMagnitude<h.radius*h.radius;
                    if(contact){h.hit.Add(target);target.ReceiveHazardHit(fighter,h.damage,35,true,h.position-target.transform.position);}
                }
            }
            else if(h.kind==ChampionHazardKind.Ray || h.kind==ChampionHazardKind.WhipOrbit)
            {
                float turn=h.kind==ChampionHazardKind.WhipOrbit?Mathf.Clamp01((h.age-.6f)/1.8f)*h.aux:Mathf.Lerp(-55,55,Mathf.Clamp01((h.age-.6f)/1.8f));
                Vector3 dir=Quaternion.AngleAxis(turn,Vector3.up)*h.velocity;
                Vector3 start=h.position+dir*h.radius,end=h.position+dir*h.limit;
                if(Physics.Linecast(start,end,out var wall,1,QueryTriggerInteraction.Ignore))end=wall.point;
                h.endpoint=end;
                if(h.age>.6f)foreach(var target in Targets)
                {
                    if(!target || !target.Alive || h.hit.Contains(target))continue;
                    float previousAge=Mathf.Max(.6f,h.age-dt);
                    float previousTurn=h.kind==ChampionHazardKind.WhipOrbit?Mathf.Clamp01((previousAge-.6f)/1.8f)*h.aux:Mathf.Lerp(-55,55,Mathf.Clamp01((previousAge-.6f)/1.8f));
                    Vector3 delta=Planar(target.transform.position-h.position);float distance=delta.magnitude;
                    float angle=Vector3.SignedAngle(h.velocity,delta,Vector3.up),swept=Mathf.DeltaAngle(previousTurn,turn),relative=Mathf.DeltaAngle(previousTurn,angle),padding=Mathf.Atan2(.38f,Mathf.Max(.1f,distance))*Mathf.Rad2Deg;
                    bool crossed=swept>=0?relative>=-padding && relative<=swept+padding:relative<=padding && relative>=swept-padding;
                    if(crossed && distance>=h.radius-.28f && distance<=h.limit+.28f && !Physics.Linecast(h.position,target.transform.position+Vector3.up*1.05f,1,QueryTriggerInteraction.Ignore))
                    {h.hit.Add(target);target.ReceiveHazardHit(fighter,h.damage,40,true,h.position-target.transform.position);}
                }
            }
            else
            {
                Vector3 before=h.position;
                if(h.kind==ChampionHazardKind.Boomerang && (h.age>.75f || h.bounced))
                {h.bounced=true;h.velocity=(transform.position+Vector3.up*1.1f-h.position).normalized*20;if((h.position-transform.position-Vector3.up*1.1f).sqrMagnitude<.6f){Deactivate(h);return;}}
                Vector3 next=h.position+h.velocity*dt;
                if(Physics.Linecast(before,next,out var wall,1,QueryTriggerInteraction.Ignore))
                {
                    h.position=wall.point+wall.normal*.1f;
                    if(h.kind==ChampionHazardKind.Ricochet && !h.bounced){h.velocity=Vector3.Reflect(h.velocity,wall.normal);h.bounced=true;game.Effects.Burst(h.position,TrialChampionCatalog.Color(Kind),6);}
                    else if(h.kind==ChampionHazardKind.Boomerang)h.bounced=true;
                    else if(h.kind==ChampionHazardKind.Rocket){Explode(h,2.8f);return;}
                    else{Deactivate(h);return;}
                }
                else h.position=next;
                foreach(var target in Targets)
                {
                    if(!target || !target.Alive || h.hit.Contains(target))continue;
                    if(SegmentDistance(target.transform.position+Vector3.up*1.05f,before,h.position)>h.radius+.28f)continue;
                    if(h.kind==ChampionHazardKind.Rocket){Explode(h,2.8f);return;}
                    h.hit.Add(target);target.ReceiveHazardHit(fighter,h.damage,35,h.unblockable,-h.velocity);
                    if(h.kind!=ChampionHazardKind.Boomerang){Deactivate(h);return;}
                }
            }
            if(h.active)DrawHazard(h);
        }
        void Explode(Hazard h,float radius)
        {
            game.Effects.Burst(h.position,TrialChampionCatalog.Color(Kind),24);game.Effects.Sound("heavy",h.position,.7f);
            foreach(var target in Targets)
            {
                if(!target || !target.Alive || Planar(target.transform.position-h.position).sqrMagnitude>radius*radius)continue;
                if(Physics.Linecast(h.position+Vector3.up*.25f,target.transform.position+Vector3.up,1,QueryTriggerInteraction.Ignore))continue;
                bool hit=target.ReceiveHazardHit(fighter,h.damage,40,true,h.position-target.transform.position);
                if(hit && Kind==ChampionKind.YaYa)target.ApplyCombatImpulse(Planar(h.position-target.transform.position).normalized*8);
            }
            Deactivate(h);
        }
        static void Deactivate(Hazard h){h.active=false;h.line.enabled=false;if(h.prop)h.prop.gameObject.SetActive(false);}
        public void CutHazards(Vector3 point,Vector3 forward,float reach,bool heavy)
        {
            foreach(var h in hazards)
            {
                if(!h.active || h.kind!=ChampionHazardKind.Mine && h.kind!=ChampionHazardKind.Turret && h.kind!=ChampionHazardKind.Silk && !(heavy && (h.kind==ChampionHazardKind.Rocket || h.kind==ChampionHazardKind.Pillar)))continue;
                Vector3 nearest=h.position;if(h.kind==ChampionHazardKind.Silk){Vector3 ab=h.endpoint-h.position;nearest=h.position+ab*Mathf.Clamp01(Vector3.Dot(point-h.position,ab)/Mathf.Max(.01f,ab.sqrMagnitude));}
                Vector3 delta=Planar(nearest-point);if(delta.sqrMagnitude>reach*reach || Vector3.Angle(forward,delta)>90 || Mathf.Abs(nearest.y-point.y)>2.2f)continue;
                game.Effects.Burst(nearest,TrialChampionCatalog.Color(Kind),8);Deactivate(h);
            }
        }
        bool DrawSpecialWarnings(Vector3 center,float amount)
        {
            if(Move.shape==ChampionShape.CrossLine)
            {
                innerWarning.enabled=true;Vector3 right=Vector3.Cross(Vector3.up,heading);warning.positionCount=innerWarning.positionCount=2;warning.widthMultiplier=innerWarning.widthMultiplier=1.45f;
                innerWarning.startColor=innerWarning.endColor=warning.startColor;
                warning.SetPosition(0,center-heading*Move.range);warning.SetPosition(1,center+heading*Move.range);innerWarning.SetPosition(0,center-right*Move.range);innerWarning.SetPosition(1,center+right*Move.range);Circle(charge,center,amount*1.5f,0,360);return true;
            }
            if(Move.shape==ChampionShape.SilkLine)
            {Vector3 side=Vector3.Cross(Vector3.up,heading)*Move.range*.5f;warning.positionCount=2;warning.widthMultiplier=.15f;warning.SetPosition(0,aim-side);warning.SetPosition(1,aim+side);Circle(charge,aim,amount,0,360);return true;}
            if(Move.shape==ChampionShape.Fissure)
            {Vector3 mark=origin+heading*Move.range*(beat+1)/Move.beats;mark.y=.06f;Circle(warning,mark,1.9f,0,360);Circle(charge,mark,amount*1.9f,0,360);return true;}
            if(Move.shape==ChampionShape.OrbitWhip)
            {innerWarning.enabled=true;Circle(warning,center,Move.range,0,360);Circle(innerWarning,center,Move.inner,0,360);Circle(charge,center,Mathf.Lerp(Move.range,Move.inner,amount),0,360);return true;}
            return false;
        }
        void DrawSpecialHazard(Hazard h)
        {
            var color=TrialChampionCatalog.Color(Kind);h.line.startColor=h.line.endColor=color;h.line.widthMultiplier=.1f;
            if(h.kind==ChampionHazardKind.Mine)
            {
                h.line.positionCount=65;h.line.startColor=h.line.endColor=h.aux>0?new Color(1,.15f,.08f):color;
                Circle(h.line,Planar(h.position)+Vector3.up*.06f,h.aux>0?h.radius*(.5f+.5f*Mathf.Clamp01(1-(h.aux-h.age)/.65f)):h.radius*.45f,0,360);
            }
            else if(h.kind==ChampionHazardKind.Pillar)
            {
                if(h.age<.65f && h.aux>=0){h.line.positionCount=65;Circle(h.line,h.position,h.radius,0,360);}
                else{h.line.positionCount=5;h.line.widthMultiplier=.45f;h.line.SetPosition(0,h.position-Vector3.right*h.radius*.6f);h.line.SetPosition(1,h.position+Vector3.up*2.3f);h.line.SetPosition(2,h.position+Vector3.right*h.radius*.6f);h.line.SetPosition(3,h.position+Vector3.forward*h.radius*.6f);h.line.SetPosition(4,h.position+Vector3.up*2.3f);}
            }
            else if(h.kind==ChampionHazardKind.Turret)
            {
                h.line.positionCount=9;h.line.widthMultiplier=.20f;
                for(int i=0;i<5;i++){float a=i*Mathf.PI*.5f;h.line.SetPosition(i,h.position+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*.55f);}
                h.line.SetPosition(5,h.position+Vector3.up*.6f);h.line.SetPosition(6,h.position);h.line.SetPosition(7,h.position);h.line.SetPosition(8,Vector3.Lerp(h.position,h.endpoint,.18f));
            }
            else if(h.kind==ChampionHazardKind.Ray || h.kind==ChampionHazardKind.WhipOrbit)
            {
                float turn=h.kind==ChampionHazardKind.WhipOrbit?Mathf.Clamp01((h.age-.6f)/1.8f)*h.aux:Mathf.Lerp(-55,55,Mathf.Clamp01((h.age-.6f)/1.8f));
                Vector3 dir=Quaternion.AngleAxis(turn,Vector3.up)*h.velocity;h.line.positionCount=2;h.line.widthMultiplier=h.age<.6f?.06f:.20f;
                h.line.SetPosition(0,h.position+dir*h.radius);h.line.SetPosition(1,h.endpoint.sqrMagnitude>0 && (h.endpoint-h.position).sqrMagnitude>.01f?h.endpoint:h.position+dir*h.limit);
            }
            else if(h.kind==ChampionHazardKind.Silk)
            {h.line.positionCount=2;h.line.widthMultiplier=h.age<.55f?.035f:.15f;h.line.SetPosition(0,h.position);h.line.SetPosition(1,h.endpoint);}
            else if(h.kind==ChampionHazardKind.Boomerang || h.kind==ChampionHazardKind.Boulder)
            {
                if(h.prop && h.kind==ChampionHazardKind.Boomerang)
                {h.line.enabled=false;h.prop.position=h.position;h.prop.rotation=Quaternion.LookRotation(h.velocity.sqrMagnitude>.01f?h.velocity:Vector3.forward)*Quaternion.Euler(h.age*800,0,0);return;}
                h.line.positionCount=5;h.line.widthMultiplier=h.kind==ChampionHazardKind.Boulder?.35f:.18f;
                for(int i=0;i<5;i++){float a=i*Mathf.PI*.5f+h.age*12;h.line.SetPosition(i,h.position+(Vector3.right*Mathf.Sin(a)+Vector3.up*Mathf.Cos(a))*h.radius);}
            }
            else if(h.kind==ChampionHazardKind.Blade)
            {h.line.positionCount=2;h.line.widthMultiplier=.20f;Vector3 side=Vector3.Cross(Vector3.up,h.velocity.normalized);h.line.SetPosition(0,h.position-side*.75f);h.line.SetPosition(1,h.position+side*.75f);}
            else
            {h.line.positionCount=2;h.line.widthMultiplier=h.kind==ChampionHazardKind.Rocket?.3f:.11f;h.line.SetPosition(0,h.position);h.line.SetPosition(1,h.position-h.velocity.normalized*(h.kind==ChampionHazardKind.Rocket?1.2f:.85f));}
        }
    }
}
