using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialPlayer
    {
        bool chainHeavy,airborneCombo,swordLanded;
        bool swordGroundContact,landingSakuraFlow,replicaVolleyDescending;
        float swordLandingAge,landingForwardSpeed;
        public static bool IsAirVolleyFinisher(int move)=>move==AirFan || move==AerialCascade || move==AirBladeThird;
        static bool IsSwordLandingSource(int move)=>IsDive(move) || IsAirVolleyFinisher(move);
        bool AirVolleyComplete=>IsAirVolleyFinisher(Combo) && (Combo==AirBladeThird?swordEmissions>0:comboBladeEmissions>=3);
        public bool AirVolleyDescending=>airborneCombo && AirVolleyComplete && (Action==FighterAction.Free || Progress>=.66f)
            || replicaVolleyDescending && TrialNetworkManager.SessionActive && !Mirror.NetworkServer.active;
        public float SwordLandingBlend=>swordLanded?Mathf.SmoothStep(0,1,swordLandingAge/.14f):0;
        public bool LandingSakuraFlow=>landingSakuraFlow;
        float launchFloor,launchHeight=2.15f;
        Vector3 reboundDirection;
        int swordEmissions;
        Vector3 swordStart;
        Vector3 swordHeading;
        float swordMotionProgress;
        byte swordMotionCues;
        byte comboBladeEmissions;
        int swordMoveId;
        bool lightImpactPlayed;
        bool guardFinishPaused;
        const float BlinkSlashDriveEnd=.34f;
        bool IsSakuraThrust=>Combo==5 || Combo==SakuraFollowup;
        int CurrentLightMove=>Action==FighterAction.Light?swordMoveId:0;
        readonly List<SwordFlight> swordFlights=new List<SwordFlight>();
        sealed class SwordFlight
        {
            public Vector3 point,direction;
            public float remaining,damage,posture;
            public TrialCombatCue cue;
            public int lightMove;
            public readonly HashSet<TrialFighter> hits=new HashSet<TrialFighter>();
        }

        public static bool IsAerialMove(int move)=>IsLowAirMove(move) || move==3 || IsAirBlade(move) || IsDive(move);
        static float LightLinkProgress(int move)=>move==ParryMirror?.76f:move==AerialCascade?.80f:move==FallingDraw?.72f:move==ScissorKick || move==CloudRake?.66f:move==AirFan?.78f:move==DoubleFang?.74f:move==WheelFinish?.66f:move==ParryTriple?.70f:move==BlinkHorizontalSlash?.54f:move==3 || move==AirStep?.50f:IsAirBlade(move)?.60f:move==AirSandDive?.75f:.55f;
        bool TryJump(TrialInput input)
        {
            // A direct entry to the existing aerial tree, with the same cost and rig.
            // Preserve queued light/heavy presses so Ctrl + attack can link in the air.
            if(input.Guard || Time.time-bufferedDodge<InputBuffer || airborneCombo || !motor.isGrounded)return false;
            bool attacking=Action==FighterAction.Light || Action==FighterAction.Heavy;
            bool canCancel=attacking && ActionTime>(Action==FighterAction.Light?.055f:.075f) && (!IsDive(Combo) || swordLanded) && !AwaitingDragonLanding;
            if(Action!=FighterAction.Free && !(Action==FighterAction.Dodge && ActionTime>=.10f) && !canCancel)return false;
            if(!Spend(SwordStaminaCost(false,AirStep)))return false;
            var direction=MoveDirection(input);
            if(Locked)direction=LockTarget.transform.position-transform.position;
            if(direction.sqrMagnitude>.01f)Turn(direction,36000,.02f);
            bufferedJump=bufferedHeal=-10;protectedUntil=0;weaveUntil=0;weavePending=false;chainUntil=0;chainCount=0;
            PlanarVelocity=Vector3.zero;Sprinting=false;struck.Clear();attackAccent=false;
            chaseTarget=null;chaseRemaining=0;Combo=AirStep;
            BeginSwordArt();Begin(FighterAction.Light,.56f);
            return true;
        }
        void BeginSwordArt()
        {
            ClearDodgeLink();
            swordMoveId++;
            lightImpactPlayed=false;
            guardFinishPaused=false;
            swordStart=transform.position;swordHeading=transform.forward;swordMotionProgress=0;swordEmissions=0;swordLanded=false;swordMotionCues=0;comboBladeEmissions=0;
            swordGroundContact=false;swordLandingAge=0;landingForwardSpeed=0;landingSakuraFlow=false;
            if(Combo==3 || Combo==AirStep){launchFloor=transform.position.y;launchHeight=Combo==AirStep?1.25f:2.15f;airborneCombo=true;}
            if(Combo==AerialCascade && !airborneCombo){launchFloor=transform.position.y;launchHeight=1.25f;airborneCombo=true;}
            if(Combo==AirPierce && transform.position.y>launchFloor+.35f)airborneCombo=true;
            if(Combo==WallRebound)
            {
                reboundDirection=-swordHeading;
                // Kick off nearby solid walls; the same input is an air backstep in open space.
                if(Physics.SphereCast(transform.position+Vector3.up*.8f,.22f,swordHeading,out var wall,1.7f,1,QueryTriggerInteraction.Ignore))
                    reboundDirection=Vector3.ProjectOnPlane(wall.normal,Vector3.up).normalized;
            }
        }
        void ResetSwordArts()
        {airborneCombo=false;swordLanded=false;swordGroundContact=false;swordLandingAge=0;landingForwardSpeed=0;landingSakuraFlow=false;replicaVolleyDescending=false;swordEmissions=0;swordFlights.Clear();}

        protected override void TickGravity(float dt)
        {
            if(swordLanded)swordLandingAge+=dt;
            if(TickMartialGravity())return;
            if(Action==FighterAction.Heavy && Combo==DodgeDragon && !dodgeDragonLanded)
            {
                verticalSpeed-=24*dt;
                var contact=motor.Move(Vector3.up*(verticalSpeed*dt));
                if((contact&CollisionFlags.Above)!=0)verticalSpeed=Mathf.Min(0,verticalSpeed);
                if((contact&CollisionFlags.Below)!=0 && verticalSpeed<0)
                {dodgeDragonLanded=true;verticalSpeed=-2;ActionTime=Mathf.Min(ActionTime,.80f);}
                return;
            }
            bool attacking=Action==FighterAction.Light || Action==FighterAction.Heavy;
            if(airborneCombo && (attacking && IsDive(Combo) || AirVolleyDescending))
            {
                // Start lowering during the last cast's follow-through; a queued dive
                // inherits that descent instead of inserting another suspended windup.
                float target=IsDive(Combo)?Combo==AirSandDive?-13:-26:-10;
                verticalSpeed=Mathf.MoveTowards(verticalSpeed,target,(Combo==20?130:80)*dt);
                landingForwardSpeed=Combo==AirSandDive?23*Mathf.Clamp01(-verticalSpeed/13):0;
                var contact=motor.Move((Vector3.up*verticalSpeed+swordHeading*landingForwardSpeed)*dt);
                if((contact&CollisionFlags.Above)!=0)verticalSpeed=Mathf.Min(0,verticalSpeed);
                swordGroundContact=(contact&CollisionFlags.Below)!=0;
                if(swordGroundContact && !IsDive(Combo))
                {
                    ConfirmSwordLanding();
                    EmitSwordCue(TrialCombatCue.SwordLanding,transform.position,swordHeading,transform.position,1);
                    if(Action==FighterAction.Free){chainCount=Combo;chainHeavy=false;chainUntil=Time.time+LandingSakuraGrace;attackInputAge=0;}
                }
                return;
            }
            bool hold=airborneCombo && (attacking && (Combo==3 || IsAirBlade(Combo) || IsLowAirMove(Combo)) || Action==FighterAction.Free && Time.time<chainUntil);
            if(hold)
            {
                float rise=(Combo==SwordRecallRise || Combo==ParryAscend || Combo==LoftingDraw || Combo==LowVault || Combo==CloudRake || Combo==3 || Combo==AirStep) && attacking?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.50f,Progress)):1;
                float targetY=launchFloor+launchHeight*rise;
                if(Combo==VaultSweep || Combo==ScissorKick)targetY+=Mathf.Sin(Progress*Mathf.PI)*.22f;
                if(Combo==WallRebound && attacking)targetY+=Mathf.Sin(Progress*Mathf.PI)*.48f;
                motor.Move(Vector3.up*(targetY-transform.position.y));verticalSpeed=0;return;
            }
            airborneCombo=false;base.TickGravity(dt);
        }
        void ConfirmSwordLanding()
        {swordLanded=true;airborneCombo=false;verticalSpeed=-2;swordLandingAge=0;}
        Vector3 LandingCarryVelocity=>swordHeading*landingForwardSpeed*Mathf.Exp(-swordLandingAge*20);

        // All displacement goes through CharacterController; walls still stop lunges and retreat.
        bool TickSwordMotion(float dt)
        {
            if(TickSwordGuardReturn())return true;
            if(TickMartialMotion())return true;
            if(TickEmpoweredMotion(dt))return true;
            if(Combo==BlinkHorizontalSlash)
            {
                if(Progress<=.54f)Turn(swordHeading,1800,dt);
                MoveSwordDistance(swordHeading,2.65f,0,BlinkSlashDriveEnd);
                return true;
            }
            if(IsSwordLandingSource(Combo) && swordLanded)
            {
                // Integrate the decaying landing momentum at any frame rate.
                float before=Mathf.Max(0,swordLandingAge-dt);
                float distance=landingForwardSpeed*(Mathf.Exp(-before*20)-Mathf.Exp(-swordLandingAge*20))/20;
                if(distance>0)motor.Move(swordHeading*distance);
                return true;
            }
            if(IsLowAirMove(Combo))
            {
                MoveSwordDistance(Combo==WallRebound?reboundDirection:swordHeading,Combo==AirPierce?3.4f:Combo==WallRebound?3.2f:Combo==AirStep?1.6f:Combo==AirFan?.6f:1.2f,.08f,.50f);return true;
            }
            if(Combo==RollThrust)
            {
                MoveRollThrust();return true;
            }
            if(IsExtendedMove(Combo))
            {
                float distance=Combo==GatheredPierce || Combo==GuardNeedle?3.6f:Combo==ThreadPierce?3.6f:Combo==RetreatDraw?2.6f:Combo==RollFlash?1.5f:Combo==WheelFinish || Combo==StormWheel?1.6f:.9f;
                MoveSwordDistance(Combo==RetreatDraw?-swordHeading:swordHeading,distance,.08f,.48f);return true;
            }
            if(IsAerialMove(Combo))return true;
            if(Combo==MoonChase)
            {MoveSwordDistance(swordHeading,4.4f,.10f,.46f);return true;}
            if(Combo==FlowingSweep || Combo==RisingBridge)
            {MoveSwordDistance(swordHeading,Combo==FlowingSweep?1.4f:1.8f,.10f,.48f);return true;}
            if(Combo==6)
            {MoveSwordDistance(-swordHeading,4.2f,.04f,.30f);return true;}
            if(IsSakuraThrust)
            {
                var excluded=motor.excludeLayers;
                try
                {
                    // Fighter bodies share a layer. Keep world collision during the thrust.
                    motor.excludeLayers=excluded.value|(1<<gameObject.layer);
                    MoveSwordDistance(swordHeading,Combo==5?6.0f:6.8f,landingSakuraFlow?0:.12f,landingSakuraFlow?.42f:.48f);
                }
                finally{motor.excludeLayers=excluded;}
                return true;
            }
            if(Combo==2)
            {MoveSwordDistance(swordHeading,1.8f,.13f,.43f);return true;}
            return false;
        }
        void MoveRollThrust()
        {
            const float distance=4.8f;
            const float launch=.36f;
            float progress=Mathf.Lerp(launch/distance,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,RollThrustDriveEnd,Progress)));
            float step=(progress-swordMotionProgress)*distance;
            swordMotionProgress=progress;
            if(step<=0)return;

            // Only a target whose body intersects the lunge path can shorten it.
            if(chaseTarget && chaseTarget.Alive)
            {
                Vector3 offset=chaseTarget.transform.position-transform.position;
                if(Mathf.Abs(offset.y)<1.2f)
                {
                    offset.y=0;
                    float ahead=Vector3.Dot(offset,swordHeading);
                    float sideSquared=(offset-swordHeading*ahead).sqrMagnitude;
                    float radius=motor.radius+(chaseTarget is TrialEnemy foe?foe.BodyRadius:.28f);
                    if(ahead>0 && sideSquared<radius*radius)
                        step=Mathf.Min(step,Mathf.Max(0,ahead-Mathf.Sqrt(radius*radius-sideSquared)));
                }
            }
            if(step>0)motor.Move(swordHeading*step);
        }

        void MoveSwordDistance(Vector3 heading,float distance,float start,float end)
        {
            float progress=Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,Progress));
            float step=Mathf.Max(0,progress-swordMotionProgress)*distance;swordMotionProgress=progress;
            if(!IsSakuraThrust && chaseTarget && chaseTarget.Alive && Vector3.Dot(heading,swordHeading)>.8f)
            {
                float body=chaseTarget is TrialEnemy foe?foe.BodyRadius:.28f;
                float gap=Vector3.ProjectOnPlane(chaseTarget.transform.position-transform.position,Vector3.up).magnitude-body-1.1f;
                step=Mathf.Min(step,Mathf.Max(0,gap));
            }
            if(step>0)motor.Move(heading*step);
        }

        bool SakuraCrossed(Vector3 target,float bodyRadius)
        {
            Vector3 travel=transform.position-swordStart;
            Vector3 horizontal=Vector3.ProjectOnPlane(travel,Vector3.up);
            if(horizontal.sqrMagnitude<.0001f)return false;

            float along=Vector3.Dot(target-swordStart,horizontal)/horizontal.sqrMagnitude;
            if(along<0 || along>1)return false;
            Vector3 offset=target-(swordStart+travel*along);
            float width=.65f+bodyRadius;
            return Mathf.Abs(offset.y)<=GroundMeleeHeight && Vector3.ProjectOnPlane(offset,Vector3.up).sqrMagnitude<=width*width;
        }

        void TickSwordArts()
        {
            if(IsMartialMove(Combo)){TickMartialArts();return;}
            TickEmpoweredArts();
            TickSwordMotionCues();
            TickComboBlades();
            if(Combo==DoubleFang)
            {
                // Two separate contact beats; defensive cancellation preserves only completed cuts.
                while(swordEmissions<2 && Progress>=.24f+swordEmissions*.35f)
                {
                    swordEmissions++;
                    EmitSwordCue(swordEmissions==1?TrialCombatCue.RisingCut:TrialCombatCue.CrescentCut,transform.position+Vector3.up,swordHeading,transform.position+swordHeading*3+Vector3.up,1);
                    SwordSegment(transform.position+Vector3.up,transform.position+swordHeading*3.1f+Vector3.up,.85f,27,22,new HashSet<TrialFighter>());
                }
            }
            if(IsExtendedMove(Combo) && Combo!=DoubleFang && Progress>=(Action==FighterAction.Heavy?.30f:.22f) && swordEmissions==0)
            {
                swordEmissions++;
                bool thrust=Combo==GatheredPierce || Combo==GuardNeedle || Combo==RollThrust || Combo==ThreadPierce;
                Vector3 origin=transform.position+Vector3.up*(Combo==LowReap?.45f:1);
                EmitSwordCue(Combo==RollFlash?TrialCombatCue.RollFlash:thrust?TrialCombatCue.ThrustTrace:Combo==CrossDraw?TrialCombatCue.RisingCut:TrialCombatCue.CrescentCut,origin,swordHeading,origin+swordHeading*3,Combo==WheelFinish?1.25f:1);
                if(Combo==StormWheel || Combo==GuardBloom)EmitSwordCue(TrialCombatCue.WeaveSigil,origin,swordHeading,origin,Combo==StormWheel?2:3);
            }
            if((Combo==FlowingSweep || Combo==RisingBridge || Combo==MoonChase) && Progress>=.24f && swordEmissions==0)
            {
                swordEmissions++;
                EmitSwordCue(Combo==MoonChase?TrialCombatCue.ThrustTrace:Combo==FlowingSweep?TrialCombatCue.CrescentCut:TrialCombatCue.RisingCut,transform.position+Vector3.up,swordHeading,transform.position+swordHeading*3+Vector3.up,1);
            }
            if((Combo==0 || Combo==1 || Combo==2) && Action==FighterAction.Light && Progress>=.22f && swordEmissions==0)
            {
                swordEmissions++;
                EmitSwordCue(Combo==0?TrialCombatCue.RisingCut:Combo==1?TrialCombatCue.CrescentCut:TrialCombatCue.ThrustTrace,transform.position+Vector3.up,swordHeading,transform.position+swordHeading*2.6f+Vector3.up,1);
            }
            if(swordEmissions==0 && HasSwordContact(Combo) && Combo!=5 && Combo!=SakuraFollowup && Progress>=(Action==FighterAction.Heavy?.30f:.22f))
            {
                swordEmissions++;
                bool thrust=Combo==8 || Combo==11 || Combo==25 || Combo==BehindThrust;
                bool rising=Combo==10 || Combo==15 || Combo==16 || Combo==18 || Combo==23;
                bool sweep=Combo==AirWheel || Combo==StormWheel || Combo==GuardBloom || Combo==12 || Combo==21 || Combo==22 || Combo==24;
                EmitSwordCue(thrust?TrialCombatCue.ThrustTrace:sweep?TrialCombatCue.CrescentCut:TrialCombatCue.RisingCut,transform.position+Vector3.up,transform.forward,transform.position+transform.forward*3+Vector3.up,rising || sweep || thrust?1:-1);
            }
            if(IsAirBlade(Combo) && Progress>=.30f && swordEmissions==0)
            {
                swordEmissions++;
                Vector3 origin=transform.position+Vector3.up*.95f+transform.forward*.6f;
                // Aim toward the target's torso from the actual airborne launch point.
                var target=Locked?LockTarget:game.NearestOpponent(this,18);
                Vector3 aim=target?target.transform.position+Vector3.up-origin:transform.forward-Vector3.up*.22f;
                if(Vector3.Angle(transform.forward,Vector3.ProjectOnPlane(aim,Vector3.up))>65)aim=transform.forward-Vector3.up*.22f;
                LaunchSword(TrialCombatCue.CrimsonBlade,origin,aim.normalized,35,20);
            }
            if(Combo==6)
            {
                while(swordEmissions<3 && Progress>=.36f+swordEmissions*.14f)
                {
                    float spread=(swordEmissions-1)*9;
                    swordEmissions++;
                    LaunchSword(TrialCombatCue.MoonBlade,transform.position+Vector3.up+transform.forward*.6f,Quaternion.AngleAxis(spread,Vector3.up)*transform.forward,28,25);
                }
            }
            if(IsSakuraThrust && Progress>=.48f && swordEmissions==0)
            {
                swordEmissions++;
                Vector3 from=swordStart+Vector3.up,to=transform.position+Vector3.up;
                SwordSegment(from,to,1.35f,24,20,new HashSet<TrialFighter>(),finisherMoveId:swordMoveId);
                EmitSwordCue(TrialCombatCue.SakuraScatter,from,swordHeading,to,1);
            }
            if(IsDive(Combo) && !swordLanded && (swordGroundContact || motor.isGrounded) && verticalSpeed<=0)
            {
                ConfirmSwordLanding();
                Vector3 centre=transform.position+Vector3.up*.12f;
                EmitCombat(TrialCombatCue.SandImpact,centre);
                SwordArea(centre,3.1f,Combo==20?72:60,60,true);
                for(int i=0;i<12;i++)
                {
                    Vector3 direction=Quaternion.AngleAxis(i*30,Vector3.up)*transform.forward;
                    LaunchSword(TrialCombatCue.BlueSand,centre+Vector3.up*.4f,direction,12,10);
                }
            }
        }

        void TickComboBlades()
        {
            // Ground routes weave ranged cuts between sword contacts. These are independent
            // projectiles, so leaving the move never repeats or recalls an emitted blade.
            // Dedicated cast finishers retain flying blades. Most physical cuts are now close-range only.
            bool woven=Combo==AerialCascade || Combo==GuardBloom || Combo==AirFan || Combo==WheelFinish || Combo==RollFlash || Combo==MoonChase;
            if(!woven)return;
            int count=Combo==AerialCascade || Combo==GuardBloom || Combo==StormWheel || Combo==AirFan?3:Combo==AirPierce || Combo==AirCross || Combo==AirWheel || Combo==DoubleFang || Combo==WheelFinish?2:1;
            while(comboBladeEmissions<count && Progress>=(count==3?.20f:.32f)+comboBladeEmissions*(count==3?.22f:.30f))
            {
                int index=comboBladeEmissions++;
                float yaw=Combo==GuardBloom || Combo==StormWheel?(index-1)*16:Combo==WheelFinish || Combo==AirWheel?(index==0?-12:12):0;
                Vector3 direction=Quaternion.AngleAxis(yaw,Vector3.up)*swordHeading;
                Vector3 origin=transform.position+Vector3.up+direction*.65f;
                var target=Locked?LockTarget:game.NearestOpponent(this,18);
                if(target && Combo!=WheelFinish && Combo!=AirWheel && Combo!=GuardBloom && Combo!=StormWheel)
                {
                    Vector3 aim=target.transform.position+Vector3.up-origin;
                    if(Vector3.Angle(direction,Vector3.ProjectOnPlane(aim,Vector3.up))<60)direction=aim.normalized;
                }
                else if(IsLowAirMove(Combo))direction=(direction-Vector3.up*.23f).normalized;
                LaunchSword(Combo==RollFlash || Combo==WheelFinish || Combo==AirWheel?TrialCombatCue.MoonBlade:TrialCombatCue.CrimsonBlade,origin,direction,Combo==AerialCascade || Combo==DoubleFang || Combo==AirFan?14:18,12);
            }
        }

        void TickSwordMotionCues()
        {
            if((Combo==AirFan || Combo==AerialCascade) && Progress>=.12f && (swordMotionCues&16)==0)
            {swordMotionCues|=16;EmitSwordCue(TrialCombatCue.WeaveSigil,transform.position+Vector3.up,swordHeading,transform.position,2);}
            bool leap=Combo==AirStep || Combo==WallRebound || Combo==3 || Combo==13 || Combo==17 || Combo==DodgeDragon;
            if(leap && Progress>=.06f && (swordMotionCues&1)==0)
            {
                swordMotionCues|=1;
                EmitSwordCue(TrialCombatCue.SwordLeap,swordStart+Vector3.up*.06f,swordHeading,transform.position,1);
            }
            bool turning=Combo==2 || Combo==6 || Combo==9 || Combo==10 || Combo==21 || Combo==24 || Combo==FlowingSweep || Combo==ThunderDragon;
            float turnAt=Combo==2?.44f:Combo==6?.32f:Combo==ThunderDragon?.08f:.18f;
            if(turning && Progress>=turnAt && (swordMotionCues&2)==0)
            {
                swordMotionCues|=2;
                EmitSwordCue(TrialCombatCue.SwordTurn,transform.position+Vector3.up*.95f,swordHeading,transform.position,Combo==24?1.2f:1);
            }
            bool descending=IsDive(Combo) && Progress>=.10f || (Combo==13 || Combo==17) && Progress>=.48f || Combo==DodgeDragon && !dodgeDragonLanded && verticalSpeed<0;
            if(descending && (swordMotionCues&4)==0)
            {
                swordMotionCues|=4;
                Vector3 floor=transform.position;
                if(Physics.Raycast(transform.position+Vector3.up*.1f,Vector3.down,out var hit,12,1,QueryTriggerInteraction.Ignore))floor=hit.point;
                EmitSwordCue(TrialCombatCue.SwordDescent,transform.position+Vector3.up*1.1f,swordHeading,floor,Combo==AirSandDive?1.15f:1);
            }
            bool landed=(Combo==13 || Combo==17) && Progress>=.72f || Combo==DodgeDragon && dodgeDragonLanded;
            if(landed && (swordMotionCues&8)==0)
            {
                swordMotionCues|=8;
                EmitSwordCue(TrialCombatCue.SwordLanding,transform.position+Vector3.up*.06f,swordHeading,transform.position,1);
            }
        }

        void SwordSegment(Vector3 from,Vector3 to,float radius,float damage,float posture,HashSet<TrialFighter> hits,bool flowing=false,int? lightMove=null,int? finisherMoveId=null,float thunderPower=-1)
        {
            Vector3 segment=to-from;
            foreach(var enemy in game.Opponents(this))
            {
                if(!enemy || !enemy.Alive || hits.Contains(enemy))continue;
                Vector3 torso=enemy.transform.position+Vector3.up;
                float t=segment.sqrMagnitude>.0001f?Mathf.Clamp01(Vector3.Dot(torso-from,segment)/segment.sqrMagnitude):0;
                float body=enemy is TrialEnemy foe?foe.BodyRadius:.28f;
                if(Vector3.Distance(torso,from+segment*t)>radius+body+.24f || Physics.Linecast(from,torso,1,QueryTriggerInteraction.Ignore))continue;
                hits.Add(enemy);
                if(SwordDamage(enemy,damage,posture,false,flowing,lightMove,finisherMoveId,unblockable:thunderPower>=0) && thunderPower>=0)
                    ConductThunder(enemy,hits,thunderPower);
            }
        }

        void LaunchSword(TrialCombatCue cue,Vector3 point,Vector3 direction,float damage,float posture)
        {
            if(swordFlights.Count>=48)return;
            swordFlights.Add(new SwordFlight{point=point,direction=direction,remaining=TrialSwordFlight.Lifetime(cue),cue=cue,damage=damage,posture=posture,lightMove=CurrentLightMove});
            if(Mirror.NetworkServer.active)TrialNetworkManager.Current.BroadcastSwordCombat(this,cue,point,direction,Vector3.zero,0,SwordStyle);
            else game.Effects.PlayCombat(new TrialCombatEvent{cue=cue,position=point,forward=direction,style=SwordStyle},this==game.Player);
        }
        void TickSwordProjectiles(float dt)
        {
            if(dt<=0 || swordFlights.Count==0)return;
            for(int i=swordFlights.Count-1;i>=0;i--)
            {
                var flight=swordFlights[i];
                Vector3 from=flight.point;
                float radius=TrialSwordFlight.Radius(flight.cue);
                float travel=TrialSwordFlight.Speed(flight.cue)*Mathf.Min(dt,flight.remaining);
                bool wall=Physics.SphereCast(from,radius,flight.direction,out var contact,travel,1,QueryTriggerInteraction.Ignore);
                Vector3 to=from+flight.direction*(wall?contact.distance:travel);
                foreach(var enemy in game.Opponents(this))
                {
                    if(!enemy || !enemy.Alive || flight.hits.Contains(enemy))continue;
                    Vector3 torso=enemy.transform.position+Vector3.up;
                    Vector3 segment=to-from;
                    float t=segment.sqrMagnitude>.0001f?Mathf.Clamp01(Vector3.Dot(torso-from,segment)/segment.sqrMagnitude):0;
                    float body=enemy is TrialEnemy foe?foe.BodyRadius:.28f;
                    if(Vector3.Distance(torso,from+segment*t)>radius+body+.24f)continue;
                    if(Physics.Linecast(from,torso,1,QueryTriggerInteraction.Ignore))continue;
                    flight.hits.Add(enemy);SwordDamage(enemy,flight.damage,flight.posture,false,lightMove:flight.lightMove);
                }
                flight.point=to;flight.remaining-=dt;
                if(wall || flight.remaining<=0)swordFlights.RemoveAt(i);
            }
        }
        void SwordArea(Vector3 point,float radius,float damage,float posture,bool finish)
        {
            foreach(var enemy in game.Opponents(this))
            {
                if(!enemy || !enemy.Alive)continue;
                Vector3 torso=enemy.transform.position+Vector3.up;
                if(Vector3.Distance(point,torso)>radius || Physics.Linecast(point,torso,1,QueryTriggerInteraction.Ignore))continue;
                SwordDamage(enemy,damage,posture,finish);
            }
        }
        bool IsSecondaryLightContact(int lightMove)
        {
            // A late projectile keeps its damage, but cannot pause another move.
            if(lightMove==0)return false;
            if(lightMove!=CurrentLightMove || lightImpactPlayed)return true;
            lightImpactPlayed=true;
            return false;
        }
        bool TryFinishGuard(TrialFighter target,int finisherMoveId)
        {
            if(target is not TrialEnemy guard || guard.IsBoss || guard.Alive)return false;
            guard.SeverHead();
            bool currentMove=finisherMoveId==swordMoveId;
            guard.HoldCombat(.12f);
            // A lingering throw can sever another guard without stopping the next move.
            bool contactOnly=!currentMove || guardFinishPaused;
            if(!contactOnly)
            {
                guardFinishPaused=true;lightImpactPlayed=true;
                HoldCombat(.11f);
            }
            EmitCombat(TrialCombatCue.Kill,guard.transform.position+Vector3.up,contactOnly);
            return true;
        }
        bool SwordDamage(TrialFighter enemy,float damage,float posture,bool finish,bool flowing=false,int? lightMove=null,int? finisherMoveId=null,bool unblockable=false)
        {
            if(!enemy.ReceiveHit(this,damage*(Boon==TrialBoon.Edge?1.12f:1),posture*(Boon==TrialBoon.Breaker?1.15f:1),unblockable))return false;
            RestoreCombatStamina(enemy.Alive?HitStamina:KillStamina);
            if(finisherMoveId.HasValue && TryFinishGuard(enemy,finisherMoveId.Value))return true;
            flowing|=SwordGuardReturning;
            bool contactOnly=!flowing && IsSecondaryLightContact(lightMove??CurrentLightMove);
            if(!flowing && !contactOnly)HoldCombat(finish?.12f:.065f);
            if(!(enemy is TrialEnemy boss && boss.ResolveActive))enemy.HoldCombat(finish?.13f:.075f);
            EmitCombat(flowing?TrialCombatCue.SwordFlowHit:!enemy.Alive?TrialCombatCue.Kill:finish?TrialCombatCue.FinisherHit:TrialCombatCue.Hit,enemy.transform.position+Vector3.up,contactOnly);
            return true;
        }
    }

    // Presentation and host collision share flight dimensions and lifetime.
    public static class TrialSwordFlight
    {
        public static float Speed(TrialCombatCue cue)=>cue==TrialCombatCue.BlueSand?11:cue==TrialCombatCue.MoonBlade?17:22;
        public static float Radius(TrialCombatCue cue)=>cue==TrialCombatCue.BlueSand?.18f:cue==TrialCombatCue.MoonBlade?.72f:.65f;
        public static float Lifetime(TrialCombatCue cue)=>cue==TrialCombatCue.BlueSand?.65f:1.1f;
    }
}
