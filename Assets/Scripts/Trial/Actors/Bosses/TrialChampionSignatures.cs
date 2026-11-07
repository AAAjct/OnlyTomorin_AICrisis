using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialChampion
    {
        bool introPlayed,anonBlinkQueued;
        float introAge=-1;
        Transform introSword;
        TrialSignatureProp introProp;
        Material signatureCore;
        Vector3 introCenter;
        public bool Introducing=>Kind==ChampionKind.Anon && !introPlayed;
        public float IntroProgress=>Mathf.Clamp01(introAge/3.5f);
        public int SwordRainCount {get {int n=0;foreach(var h in hazards)if(h.active && h.kind==ChampionHazardKind.SwordRain)n++;return n;}}

        void InitializeSignature()
        {
            signatureCore=new Material(game.World.Glow){name=Kind+" signature core"};
            var tint=TrialChampionCatalog.Color(Kind);signatureCore.SetColor("_BaseColor",tint);
            if(signatureCore.HasProperty("_EmissionColor"))signatureCore.SetColor("_EmissionColor",tint*2);
            if(Kind!=ChampionKind.Anon)return;
            introProp=TrialSignatureProp.Create(transform);introSword=introProp.transform;
            introCenter=new Vector3(0,0,25);
        }
        void ResetSignature()
        {
            introPlayed=Kind!=ChampionKind.Anon;introAge=-1;anonBlinkQueued=false;anonDashRemaining=0;
            DrawSignatureIntro();
        }
        bool TickSignatureIntro(float dt)
        {
            if(!Introducing)return false;
            float before=introAge;introAge=Mathf.Max(0,introAge)+dt;
            fighter.BossMotion(0);fighter.BossGuard(false);
            float landing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1.6f,introAge));
            Vector3 position=introCenter+Vector3.back*1.05f+Vector3.up*(7*(1-landing));
            fighter.Teleport(position);
            fighter.BossFace(game.CombatTarget.transform.position-position,400,dt);
            if(before<1.6f && introAge>=1.6f)
            {game.Effects.SignatureGlyph(introCenter,Kind,3,0);game.Effects.Burst(introCenter,TrialChampionCatalog.Color(Kind),36);game.Effects.Sound("landing",introCenter,.8f);}
            if(before<2.65f && introAge>=2.65f)
            {game.Effects.SignatureSlash(introCenter+Vector3.up,transform.forward,Kind,2.8f,0);game.Effects.Sound("swing",introCenter,1.3f);}
            DrawSignatureIntro();
            if(introAge>=3.5f){introPlayed=true;introProp.Hide();cooldown=.28f;fighter.BossBegin(FighterAction.Free,0);}
            return true;
        }
        void DrawSignatureIntro()
        {
            if(!introProp)return;
            if(introPlayed){introProp.Hide();return;}
            float fall=1-Mathf.Clamp01(introAge/.5f);
            float draw=Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.1f,2.65f,introAge));
            introProp.Show("SignatureSword",introCenter+Vector3.up*(1.82f+fall*8+draw*1.4f),Quaternion.Euler(180,0,-draw*65),Vector3.one*1.35f,game.World.Steel,signatureCore);
        }
        internal void TickDuringFlinch(float dt)
        {
            // A hit delays the body, not swords / shells that have already left it.
            TickHazards(dt);
        }
        bool TryBlinkBehind()
        {
            var target=game.CombatTarget;if(!target || !target.Alive)return false;
            Vector3 point=ClampArena(Planar(target.transform.position-target.transform.forward*2.65f),2);
            Vector3 delta=point-Planar(transform.position);
            if(Physics.CheckSphere(point+Vector3.up,.48f,1,QueryTriggerInteraction.Ignore) || Physics.SphereCast(transform.position+Vector3.up,.45f,delta.normalized,out _,delta.magnitude,1,QueryTriggerInteraction.Ignore))return false;
            game.Effects.DodgeWake(transform.position,delta.normalized,TrialChampionCatalog.Color(Kind));
            fighter.Teleport(point);fighter.BossFace(target.transform.position-point,10000,.1f);
            game.Effects.SignatureGlyph(point,Kind,1.2f,2);game.Effects.Sound("blink",point);return true;
        }
        void OnSignatureBegin()
        {
            if(Kind==ChampionKind.Anon && (MoveIndex==1 || MoveIndex==4))
            {
                Vector3 delta=Planar(aim-transform.position);float travel=Mathf.Min(6,Mathf.Max(0,delta.magnitude-3.5f));
                if(travel>0 && !Physics.SphereCast(transform.position+Vector3.up,.5f,delta.normalized,out _,travel,1,QueryTriggerInteraction.Ignore))
                {anonDashRemaining=travel;anonDashDirection=delta.normalized;game.Effects.DodgeWake(transform.position,anonDashDirection,TrialChampionCatalog.Color(Kind));}
            }
            game.Effects.SignatureGlyph(transform.position,Kind,1.05f,MoveIndex);
        }
        float anonDashRemaining;
        Vector3 anonDashDirection;
        void TickSignatureAttack(float dt)
        {
            if(anonDashRemaining>0 && Kind==ChampionKind.Anon)
            {
                float step=Mathf.Min(anonDashRemaining,18*dt);anonDashRemaining-=step;
                fighter.BossMove(anonDashDirection*(step/Mathf.Max(.001f,dt)),dt);
            }
            if(Kind==ChampionKind.NuoNuo && WindingUp)
                SafeTacticalStep(Vector3.Cross(Vector3.up,heading)*orbitSide,1.6f,dt);
            // The relay blade position is locked for the next strike, which retains its own windup.
            if(Kind==ChampionKind.Anon && MoveIndex==9 && beat>0 && WindingUp && Age-(Move.windup+(beat-1)*Move.interval)<dt*1.2f)
            {
                foreach(var h in hazards)if(h.active && h.kind==ChampionHazardKind.Blade)
                {
                    Vector3 point=ClampArena(Planar(h.position)-heading*1.5f,2);
                    Vector3 travel=point-transform.position;
                    if(!Physics.SphereCast(transform.position+Vector3.up,.45f,travel.normalized,out _,travel.magnitude,1,QueryTriggerInteraction.Ignore))
                    {game.Effects.DodgeWake(transform.position,travel.normalized,TrialChampionCatalog.Color(Kind));fighter.Teleport(point);LockAim();}
                    break;
                }
            }
        }
        void SignatureContactArt()
        {
            game.Effects.SignatureSlash(transform.position+Vector3.up*1.05f,heading,Kind,Mathf.Min(6,Move.range),MoveIndex+beat);
        }
        bool ContactSignature()
        {
            if(Kind==ChampionKind.Anon)return ContactAnon();
            if(Kind==ChampionKind.FeiBi)
            {
                if(Move.shape==ChampionShape.Counter && !counterTriggered)return true;
                // Hot compressed fists travel rather than reusing another boss's floor circles.
                int count=MoveIndex==15?7:MoveIndex==9?6:MoveIndex==14?2:1;
                for(int i=0;i<count;i++)
                {
                    float angle=MoveIndex==9?i*60:count==1?Mathf.Sin(beat*1.8f)*12:Mathf.Lerp(-65,65,i/(float)(count-1));
                    Vector3 direction=Quaternion.AngleAxis(angle,Vector3.up)*heading;
                    var h=SpawnSpecial(ChampionHazardKind.SolarFist,transform.position+Vector3.up*.9f+direction*.9f,direction*(MoveIndex==1?17:10+MoveIndex%4),.42f,1.5f,Move.damage*.72f);
                    if(h!=null){h.aux=.28f;h.unblockable=Move.Unblockable;}
                }
                if(MoveIndex==9 || MoveIndex==15)return true;
                return false;
            }
            if(Kind==ChampionKind.NuoNuo)
            {
                if(Move.shape==ChampionShape.Counter && !counterTriggered)return true;
                int count=MoonCount[MoveIndex];
                Vector3 side=Vector3.Cross(Vector3.up,heading);
                for(int i=0;i<count;i++)
                {
                    Vector3 position=ClampArena(MoonFormation(i,count,side),2);
                    var echo=SpawnSpecial(ChampionHazardKind.MoonEcho,position,heading,MoveIndex==9?2.6f:1.25f,1.9f,Move.damage);
                    if(echo!=null){echo.aux=.75f+i*.12f;echo.unblockable=true;}
                }
                game.Effects.Sound("warning",aim,.85f);return true;
            }
            if(Kind==ChampionKind.Taffy)
            {
                // Air slots become distinct gun tactics, never the same replacement slide volley.
                if(MoveIndex==4 || MoveIndex==15)
                {
                    int count=MoveIndex==4?2:5;
                    for(int i=0;i<count;i++)
                    {
                        Vector3 p=ClampArena(aim+Vector3.Cross(Vector3.up,heading)*(i-(count-1)*.5f)*2.7f,2);
                        var h=SpawnSpecial(MoveIndex==4?ChampionHazardKind.Turret:ChampionHazardKind.Mine,p+Vector3.up*(MoveIndex==4?.6f:.08f),Vector3.zero,MoveIndex==4?.6f:1.7f,MoveIndex==4?4.8f:5.0f,Move.damage*.8f);
                        if(h!=null){h.endpoint=aim+Vector3.up;h.aux=MoveIndex==4?1:0;}
                    }
                    return true;
                }
            }
            if(Kind==ChampionKind.Penguin)
            {
                if(MoveIndex==1 || MoveIndex==4 || MoveIndex==7 || MoveIndex==11)
                {
                    int count=MoveIndex==11?4:MoveIndex==7?3:1;
                    for(int i=0;i<count;i++)
                    {
                        Vector3 direction=Quaternion.AngleAxis(MoveIndex==11?i*90:(i-(count-1)*.5f)*23,Vector3.up)*heading;
                        Vector3 p=MoveIndex==7?aim+direction*2.4f:transform.position;
                        var rail=SpawnSpecial(ChampionHazardKind.FrostRail,Planar(p)+Vector3.up*.07f,direction,MoveIndex==4?1.1f:.7f,2.8f,Move.damage);
                        if(rail!=null){rail.limit=MoveIndex==4?9:16;rail.aux=.7f;rail.unblockable=true;}
                    }
                    return true;
                }
            }
            if(Kind==ChampionKind.YaYa)
            {
                if(MoveIndex==2 || MoveIndex==4 || MoveIndex==5 || MoveIndex==13)
                {
                    int count=MoveIndex==4?3:MoveIndex==13?4:2;
                    for(int i=0;i<count;i++)
                    {
                        float a=i*Mathf.PI*2/count+beat*.7f;
                        Vector3 p=aim+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*(MoveIndex==2?2:4);
                        var knot=SpawnSpecial(ChampionHazardKind.SilkKnot,ClampArena(p,2),Vector3.zero,.25f,3.2f,Move.damage);
                        if(knot!=null){knot.endpoint=aim;knot.aux=.9f;}
                    }
                    return true;
                }
            }
            if(IsRemnant && (MoveIndex==4 || MoveIndex==7 || MoveIndex==10 || MoveIndex==12))
            {
                int count=MoveIndex==7?1:MoveIndex==4?3:5;
                for(int i=0;i<count;i++)
                {
                    Vector3 dir=Quaternion.AngleAxis(i*180f/count+beat*37,Vector3.up)*heading;
                    var feather=SpawnSpecial(ChampionHazardKind.FeatherCut,aim-dir*4,dir,.35f,1.6f,Move.damage);
                    if(feather!=null){feather.endpoint=aim+dir*4;feather.aux=.65f;feather.unblockable=true;}
                }
                return true;
            }
            return false;
        }
        static readonly int[] MoonCount={2,2,3,3,2,4,3,3,1,5,2,3,2,8,6,8};
        Vector3 MoonFormation(int i,int count,Vector3 side)
        {
            float middle=i-(count-1)*.5f;
            Vector3 radial=Quaternion.AngleAxis(i*360f/count,Vector3.up)*heading;
            switch(MoveIndex)
            {
                case 0:return aim+side*middle*2;
                case 1:return aim+heading*middle*4;
                case 2:return aim+side*middle*2.4f-heading*Mathf.Abs(middle);
                case 3:return aim+radial*2.6f;
                case 4:return Vector3.Lerp(aim,transform.position,i==0?.25f:.75f)+side*(i==0?-2:2);
                case 5:return aim+radial*3.2f;
                case 6:return aim+heading*(2-i)*2.3f;
                case 7:return aim+heading*i*1.8f+side*(i%2==0?-1.6f:1.6f);
                case 8:return aim-heading*1.2f;
                case 9:return transform.position+radial*4.8f;
                case 10:return aim+side*(i==0?-2.6f:2.6f)-heading;
                case 11:return aim+side*middle*2+heading*Mathf.Abs(middle)*2;
                case 12:return Vector3.Lerp(transform.position,aim,.5f)+side*(i==0?-3:3);
                case 13:return aim+radial*5.2f;
                case 14:return aim+side*(i%3-1)*3+heading*(i/3==0?-1.8f:1.8f);
                default:return aim+Quaternion.AngleAxis(i*65,Vector3.up)*heading*(1.3f+i*.43f);
            }
        }
        bool ContactAnon()
        {
            if(MoveIndex==4 || MoveIndex==11)
            {
                Vector3 center=ClampArena(aim,7);float radius=MoveIndex==4?5.6f:6.8f;
                for(int i=0;i<5;i++)
                {
                    Vector3 a=StarVertex(center,radius,i),b=StarVertex(center,radius,(i+2)%5);
                    var edge=SpawnSpecial(ChampionHazardKind.StarEdge,a,Vector3.zero,.20f,3.1f,Move.damage);
                    if(edge!=null){edge.endpoint=b;edge.aux=MoveIndex==4?1.05f:1.4f;edge.unblockable=true;}
                    for(int j=0;j<(MoveIndex==4?3:2);j++)
                        RainSword(Vector3.Lerp(a,b,(j+.5f)/(MoveIndex==4?3:2)),.95f+(i%3)*.12f+j*.16f,.72f,Move.damage);
                }
                game.Effects.SignatureGlyph(center,Kind,radius,4);game.Effects.Sound("charge",center,.7f);return true;
            }
            if(MoveIndex==12)
            {
                foreach(var target in Targets)if(target && target.Alive)
                {
                    Vector3 p=ClampArena(Planar(target.transform.position),3);
                    for(int i=0;i<5;i++)RainSword(p+Quaternion.AngleAxis(i*72,Vector3.up)*Vector3.forward*1.8f,.82f+i*.08f,.7f,Move.damage);
                    RainSword(p,1.1f,.85f,Move.damage);
                }
                return true;
            }
            if(MoveIndex==14)
            {for(int i=0;i<3;i++)RainSword(transform.position+heading*(4+i*3.5f),.72f+i*.18f,1.05f,Move.damage);return true;}
            if(MoveIndex==5 || MoveIndex==9)
            {
                if(MoveIndex==9 && beat>0)return false;
                int count=MoveIndex==5?7:1;
                for(int i=0;i<count;i++)
                {
                    Vector3 dir=Quaternion.AngleAxis(count==1?0:Mathf.Lerp(-55,55,i/(float)(count-1))+(beat%2)*7,Vector3.up)*heading;
                    SpawnSpecial(ChampionHazardKind.Blade,transform.position+Vector3.up*1.1f,dir*(MoveIndex==9?11:16),.55f,2.4f,Move.damage);
                }
                return true;
            }
            if(MoveIndex==6)
            {
                Vector3 side=Vector3.Cross(Vector3.up,heading);
                var edge=SpawnSpecial(ChampionHazardKind.StarEdge,aim-side*3-heading*2,Vector3.zero,.18f,1.6f,Move.damage*.7f);
                if(edge!=null){edge.endpoint=aim+side*3+heading*2;edge.aux=.7f;edge.unblockable=true;}
            }
            if(MoveIndex==13)SpawnSpecial(ChampionHazardKind.Blade,transform.position+Vector3.up,Quaternion.AngleAxis(beat*90,Vector3.up)*firstHeading*15,.6f,2.2f,Move.damage*.7f);
            if(MoveIndex==15)
            {
                for(int i=0;i<2;i++)SpawnSpecial(ChampionHazardKind.Boomerang,transform.position+Vector3.up,Quaternion.AngleAxis(i==0?-23:23,Vector3.up)*heading*19,.5f,2.5f,Move.damage);
                return true;
            }
            return false;
        }
        static Vector3 StarVertex(Vector3 center,float radius,int index)
        {float a=(index*72+18)*Mathf.Deg2Rad;return Planar(center)+new Vector3(Mathf.Sin(a)*radius,.075f,Mathf.Cos(a)*radius);}
        void RainSword(Vector3 position,float delay,float radius,float damage)
        {
            var h=SpawnSpecial(ChampionHazardKind.SwordRain,Planar(position)+Vector3.up*.06f,Vector3.zero,radius,delay+.65f,damage);
            if(h!=null){h.aux=delay;h.unblockable=true;}
        }
        bool TickSignatureHazard(Hazard h,float dt)
        {
            if(h.kind<ChampionHazardKind.SwordRain)return false;
            Vector3 before=h.position;
            if(h.kind==ChampionHazardKind.SolarFist)
            {
                if(h.age>h.aux)
                {
                    h.position+=h.velocity*dt;
                    if(Physics.Linecast(before,h.position,1,QueryTriggerInteraction.Ignore)){Deactivate(h);return true;}
                    foreach(var target in Targets)if(target && target.Alive && !h.hit.Contains(target) && SegmentDistance(target.transform.position+Vector3.up*.9f,before,h.position)<h.radius+.2f)
                    {h.hit.Add(target);target.ReceiveHazardHit(fighter,h.damage,25,h.unblockable,-h.velocity);}
                }
            }
            else if(h.age>=h.aux)
            {
                if(h.age-dt<h.aux)
                {
                    game.Effects.SignatureGlyph(h.position,Kind,h.kind==ChampionHazardKind.FrostRail?1.4f:h.radius*1.5f,MoveIndex);
                    game.Effects.Burst(h.position+Vector3.up*.2f,TrialChampionCatalog.Color(Kind),h.kind==ChampionHazardKind.SwordRain?14:8);
                    if(h.kind==ChampionHazardKind.SwordRain)game.Effects.Sound("swing",h.position,1.6f);
                }
                foreach(var target in Targets)
                {
                    if(!target || !target.Alive || h.hit.Contains(target))continue;
                    if(h.kind!=ChampionHazardKind.SwordRain && !target.OverlapsAttackHeight(h.position.y-.1f,h.position.y+.3f))continue;
                    if(h.kind==ChampionHazardKind.SwordRain && !target.OverlapsAttackHeight(h.position.y,h.position.y+4.5f))continue;
                    Vector3 p=Planar(target.transform.position);
                    bool hit=false;
                    if(h.kind==ChampionHazardKind.SwordRain || h.kind==ChampionHazardKind.MoonEcho)hit=Planar(p-h.position).sqrMagnitude<(h.radius+.12f)*(h.radius+.12f) && h.age<h.aux+.22f;
                    else if(h.kind==ChampionHazardKind.StarEdge || h.kind==ChampionHazardKind.FeatherCut)hit=SegmentDistance(p+Vector3.up*.075f,h.position,h.endpoint)<h.radius+.22f && h.age<h.aux+.3f;
                    else if(h.kind==ChampionHazardKind.SilkKnot)
                    {
                        Vector3 end=Vector3.Lerp(h.position,h.endpoint,Mathf.Clamp01((h.age-h.aux)/1.4f));
                        hit=SegmentDistance(p+Vector3.up*.075f,h.position,end)<.42f;
                    }
                    else if(h.kind==ChampionHazardKind.FrostRail)
                    {
                        float length=Mathf.Min(h.limit,(h.age-h.aux)*13);
                        Vector3 end=h.position+h.velocity*length;
                        hit=SegmentDistance(p+Vector3.up*.07f,h.position,end)<h.radius+.2f;
                    }
                    if(!hit || Physics.Linecast(h.position+Vector3.up*.6f,target.transform.position+Vector3.up*.6f,1,QueryTriggerInteraction.Ignore))continue;
                    h.hit.Add(target);target.ReceiveHazardHit(fighter,h.damage,30,true,h.position-target.transform.position);
                    if(h.kind==ChampionHazardKind.SilkKnot)target.ApplyCombatImpulse(Planar(h.endpoint-p).normalized*5);
                }
            }
            if(h.active)DrawHazard(h);return true;
        }
        bool DrawSignatureWarnings(Vector3 center,float amount)
        {
            if(Kind!=ChampionKind.Anon || MoveIndex!=4 && MoveIndex!=11)return false;
            Vector3 c=ClampArena(aim,7);float r=Move.range;
            warning.positionCount=6;
            for(int i=0;i<6;i++)warning.SetPosition(i,StarVertex(c,r,i*2%5));
            Circle(charge,c,r*amount,0,360);return true;
        }
        bool DrawSignatureHazard(Hazard h)
        {
            if(h.kind<ChampionHazardKind.SwordRain)return false;
            bool active=h.age>=h.aux;
            Color tint=TrialChampionCatalog.Color(Kind);
            h.line.startColor=active?Color.Lerp(tint,Color.white,.65f):tint;
            h.line.endColor=h.line.startColor;h.line.widthMultiplier=active?.20f:.06f;
            if(h.kind==ChampionHazardKind.SwordRain || h.kind==ChampionHazardKind.MoonEcho)
            {
                h.line.positionCount=65;Circle(h.line,h.position,h.radius,0,360);
                h.line.widthMultiplier=active?.10f:.065f;
                if(h.kind==ChampionHazardKind.MoonEcho)
                {
                    // An actual crescent silhouette, not a reskinned full circle.
                    h.line.positionCount=33;
                    for(int i=0;i<33;i++)
                    {float a=i/32f*Mathf.PI*2;float x=Mathf.Sin(a),z=Mathf.Cos(a);if(i>16)x*=.48f;h.line.SetPosition(i,h.position+new Vector3(x,0,z)*h.radius);}
                }
            }
            else if(h.kind==ChampionHazardKind.SolarFist)
            {
                h.line.positionCount=25;h.line.widthMultiplier=.15f;
                Quaternion frame=Quaternion.LookRotation(h.velocity.normalized);
                for(int i=0;i<25;i++){float a=i/24f*Mathf.PI*2;h.line.SetPosition(i,h.position+frame*new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*h.radius);}
            }
            else
            {
                h.line.positionCount=2;h.line.SetPosition(0,h.position);
                Vector3 end=h.kind==ChampionHazardKind.FrostRail?h.position+h.velocity*(active?Mathf.Min(h.limit,(h.age-h.aux)*13):h.limit):h.kind==ChampionHazardKind.SilkKnot?Vector3.Lerp(h.position,h.endpoint,active?Mathf.Clamp01((h.age-h.aux)/1.4f):1):h.endpoint;
                h.line.SetPosition(1,end);
                if(h.kind==ChampionHazardKind.FrostRail)h.line.widthMultiplier=active?h.radius*1.8f:.1f;
            }
            return true;
        }
        void DrawSignatureProp(Hazard h)
        {
            string resource=h.kind==ChampionHazardKind.SwordRain || Kind==ChampionKind.Anon && h.kind==ChampionHazardKind.Blade?"SignatureSword":
                h.kind==ChampionHazardKind.Pillar || h.kind==ChampionHazardKind.FrostRail || h.kind==ChampionHazardKind.Boulder?"SignatureCrystal":
                h.kind==ChampionHazardKind.FeatherCut?"SignatureFeather":null;
            if(resource==null){if(h.art)h.art.Hide();return;}
            if(!h.art)h.art=TrialSignatureProp.Create(transform);
            Vector3 point=h.position;Quaternion rotation=Quaternion.identity;Vector3 scale=Vector3.one;
            if(h.kind==ChampionHazardKind.SwordRain)
            {
                float drop=Mathf.InverseLerp(h.aux-.22f,h.aux,h.age);
                point+=Vector3.up*(1.1f+(1-drop)*7);rotation=Quaternion.Euler(180,h.age*20,0);scale=Vector3.one*(h.radius*1.25f);
            }
            else if(h.kind==ChampionHazardKind.Blade)
            {rotation=Quaternion.LookRotation(h.velocity)*Quaternion.Euler(90,0,h.age*540);scale=Vector3.one*.8f;}
            else if(h.kind==ChampionHazardKind.FeatherCut)
            {point=Vector3.Lerp(h.position,h.endpoint,Mathf.Clamp01(h.age/h.lifetime))+Vector3.up*.8f;rotation=Quaternion.Euler(65,h.age*250,25);scale=Vector3.one*.85f;}
            else
            {
                float rise=Mathf.Clamp01((h.age-.55f)*6);
                if(h.kind==ChampionHazardKind.FrostRail){rise=Mathf.Clamp01((h.age-h.aux)*6);point+=h.velocity*Mathf.Min(h.limit,Mathf.Max(0,h.age-h.aux)*13);}
                rotation=Quaternion.Euler(0,h.position.x*28,h.kind==ChampionHazardKind.Boulder?45:12);
                scale=new Vector3(h.radius,h.kind==ChampionHazardKind.Boulder?.7f:rise,h.radius);
            }
            h.art.Show(resource,point,rotation,scale,Kind==ChampionKind.Penguin?signatureCore:game.World.Steel,signatureCore);
            if(h.kind==ChampionHazardKind.SwordRain)
                h.art.Beam(h.position,point,projectileMaterial,TrialChampionCatalog.Color(Kind),h.age<h.aux?.38f:.72f);
        }
    }
}
