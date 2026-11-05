using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialPlayer
    {
        public const float DragonChargeLimit=1.4f;
        float parryReadyUntil,parryDodgeUntil,dragonPower;
        bool dodgeDragonLanded;
        public bool DodgeDragonLanded=>dodgeDragonLanded;
        bool AwaitingDragonLanding=>Combo==DodgeDragon && !dodgeDragonLanded;
        public bool ParryEmpowered=>Time.time<parryReadyUntil;
        public bool ParryDodgeFollowup=>Time.time<parryDodgeUntil;
        public float SwordCharge=>Action==FighterAction.Charge && Combo==FireDragon?Mathf.Clamp01(ActionTime/DragonChargeLimit):dragonPower;
        readonly List<ParryFlight> parryFlights=new List<ParryFlight>();
        sealed class ParryFlight
        {
            public TrialCombatCue cue;
            public Vector3 start,end,point,direction;
            public float age,power;
            public readonly HashSet<TrialFighter> hits=new HashSet<TrialFighter>();
        }

        void EmpowerParry()
        {
            parryReadyUntil=Time.time+5;parryDodgeUntil=0;dragonPower=0;
            if(this==game.Player)game.Hud.Notify("完美格挡 · 轻：三连刺 / 重：蓄力火龙 / 闪：远闪追击",2.2f);
        }
        int EmpoweredBranch(bool heavy,int requested)
        {
            if(ParryDodgeFollowup){parryDodgeUntil=0;blinkReadyUntil=0;return heavy?SpiralSword:BehindThrust;}
            if(!ParryEmpowered)
            {
                if(heavy && Time.time<blinkReadyUntil){blinkReadyUntil=0;return DodgeDragon;}
                return requested;
            }
            parryReadyUntil=0;blinkReadyUntil=0;return heavy?FireDragon:ParryTriple;
        }
        bool BeginDragonCharge(TrialInput input)
        {
            if(!ParryEmpowered || !input.HeavyHeld)return false;
            if(!Spend(8))return false;
            parryReadyUntil=0;blinkReadyUntil=0;ClearAttackInputs();
            protectedUntil=0;dragonPower=0;chargeReady=false;chargeTime=0;Combo=FireDragon;
            Begin(FighterAction.Charge,DragonChargeLimit);return true;
        }
        void PrepareEmpoweredArt()
        {
            if(Combo!=FireDragon)dragonPower=Combo==DodgeDragon?.5f:0;
            if(Combo==DodgeDragon){dodgeDragonLanded=false;verticalSpeed=9.6f;airborneCombo=false;}
            if(Combo==BehindThrust)
            {
                var target=Locked?LockTarget:game.NearestOpponent(this,16);
                if(target)
                {
                    float radius=target is TrialEnemy enemy?enemy.BodyRadius:.28f;
                    Vector3 behind=target.transform.position-target.transform.forward*(radius+1.1f);
                    Vector3 offset=Vector3.ProjectOnPlane(behind-transform.position,Vector3.up);
                    // Blink through actors, but sweep the full body against world geometry first.
                    game.Effects.Afterimage(this);float distance=Mathf.Min(16,offset.magnitude);
                    Vector3 direction=offset.normalized;
                    if(distance>.001f && Physics.CapsuleCast(transform.position+Vector3.up*(motor.radius+.05f),transform.position+Vector3.up*(motor.height-motor.radius),motor.radius,direction,out var wall,distance,1,QueryTriggerInteraction.Ignore))distance=Mathf.Max(0,wall.distance-.05f);
                    Teleport(transform.position+direction*distance);
                    Turn(target.transform.position-transform.position,36000,.02f);
                }
                EmitSwordCue(TrialCombatCue.EmpoweredStep,swordStart+Vector3.up,transform.forward,transform.position+Vector3.up,1);
            }
        }
        bool TickEmpoweredMotion(float dt)
        {
            if(Combo==DodgeDragon){MoveSwordDistance(-swordHeading,4.2f,.03f,.60f);return true;}
            if(Combo==FireDragon)
            {
                float t=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.04f,.43f,Progress));
                float old=swordMotionProgress;swordMotionProgress=t;
                // A half-circle, starting backward and curving around the flank before release.
                Vector3 right=Vector3.Cross(Vector3.up,swordHeading);
                Vector3 before=right*(1-Mathf.Cos(old*Mathf.PI))*2.3f-swordHeading*Mathf.Sin(old*Mathf.PI)*3.7f;
                Vector3 after=right*(1-Mathf.Cos(t*Mathf.PI))*2.3f-swordHeading*Mathf.Sin(t*Mathf.PI)*3.7f;
                motor.Move(after-before);
                if(Locked && Progress<.56f)Turn(LockTarget.transform.position-transform.position,1080,dt);
                return true;
            }
            if(Combo==ParryTriple || Combo==BehindThrust || Combo==SpiralSword)return true;
            return false;
        }
        void TickEmpoweredArts()
        {
            if(Combo==ParryTriple)
            {
                while(swordEmissions<3 && Progress>=.18f+swordEmissions*.22f)
                {
                    swordEmissions++;
                    Vector3 from=transform.position+Vector3.up,to=from+transform.forward*4.6f;
                    EmitSwordCue(TrialCombatCue.ThrustTrace,from,transform.forward,to,1.25f);
                    SwordSegment(from,to,.48f,32,24,new HashSet<TrialFighter>());
                    motor.Move(transform.forward*.5f);
                }
            }
            if(Combo==FireDragon && Progress>=.54f && swordEmissions==0)
            {
                swordEmissions++;
                LaunchParryFlight(TrialCombatCue.FireDragon,dragonPower);
            }
            if(Combo==SpiralSword && Progress>=.23f && swordEmissions==0)
            {
                swordEmissions++;
                LaunchParryFlight(TrialCombatCue.SpiralSword,1);
            }
            if(Combo==DodgeDragon && dodgeDragonLanded && Progress>=.74f && swordEmissions==0)
            {swordEmissions++;LaunchParryFlight(TrialCombatCue.FireDragon,dragonPower);}
        }
        void LaunchParryFlight(TrialCombatCue cue,float power)
        {
            if(parryFlights.Count>=8)return;
            Vector3 start=transform.position+Vector3.up;
            var target=Locked?LockTarget:game.NearestOpponent(this,22);
            Vector3 direction=target?Vector3.ProjectOnPlane(target.transform.position-transform.position,Vector3.up).normalized:transform.forward;
            if(direction.sqrMagnitude<.01f)direction=transform.forward;
            Vector3 end=cue==TrialCombatCue.SpiralSword && target?target.transform.position+Vector3.up:start+direction*(cue==TrialCombatCue.SpiralSword?12:Mathf.Lerp(9,24,power));
            parryFlights.Add(new ParryFlight{cue=cue,start=start,end=end,point=start,direction=direction,power=power});
            EmitSwordCue(cue,start,direction,end,power);
        }
        void TickParryFlights(float dt)
        {
            if(!Alive){parryFlights.Clear();return;}
            while(dt>.00001f && parryFlights.Count>0){float step=Mathf.Min(.016f,dt);TickParryFlightStep(step);dt-=step;}
        }
        void TickParryFlightStep(float dt)
        {
            for(int i=parryFlights.Count-1;i>=0;i--)
            {
                var flight=parryFlights[i];flight.age+=dt;
                float duration=TrialParryFlight.Duration(flight.cue,flight.power);
                Vector3 to=TrialParryFlight.Point(flight.cue,flight.start,flight.end,transform.position+Vector3.up,flight.age/duration,flight.power);
                Vector3 step=to-flight.point;
                float radius=flight.cue==TrialCombatCue.FireDragon?Mathf.Lerp(.8f,1.3f,flight.power):.52f;
                bool wall=step.sqrMagnitude>.00001f && Physics.SphereCast(flight.point,radius,step.normalized,out _,step.magnitude,1,QueryTriggerInteraction.Ignore);
                if(!wall)SwordSegment(flight.point,to,radius,flight.cue==TrialCombatCue.FireDragon?Mathf.Lerp(65,155,flight.power):85,65,flight.hits);
                flight.point=to;
                if(wall || flight.age>=duration)parryFlights.RemoveAt(i);
            }
        }
        void EmitSwordCue(TrialCombatCue cue,Vector3 from,Vector3 direction,Vector3 end,float amount)
        {
            if(Mirror.NetworkServer.active)TrialNetworkManager.Current.BroadcastSwordCombat(this,cue,from,direction,end,amount,SwordStyle);
            else game.Effects.PlayCombat(new TrialCombatEvent{cue=cue,position=from,forward=direction,end=end,amount=amount,style=SwordStyle},this==game.Player);
        }
        void ResetParryArts(){parryReadyUntil=parryDodgeUntil=dragonPower=0;dodgeDragonLanded=false;parryFlights.Clear();if(game && game.Effects)game.Effects.ClearSwordArts(this);}
    }

    public static class TrialParryFlight
    {
        public static float Duration(TrialCombatCue cue,float power)=>cue==TrialCombatCue.SpiralSword?1.15f:Mathf.Lerp(.65f,1.35f,Mathf.Clamp01(power));
        public static Vector3 Point(TrialCombatCue cue,Vector3 start,Vector3 end,Vector3 hand,float t,float power)
        {
            t=Mathf.Clamp01(t);
            Vector3 forward=(end-start).normalized,right=Vector3.Cross(Vector3.up,forward).normalized;
            if(cue==TrialCombatCue.SpiralSword)
            {
                float u=t<.57f?t/.57f:(t-.57f)/.43f;
                Vector3 centre=t<.57f?Vector3.Lerp(start,end,u):Vector3.Lerp(end,hand,u);
                float envelope=Mathf.Sin(u*Mathf.PI)*.38f;
                return centre+(right*Mathf.Sin(t*Mathf.PI*12)+Vector3.up*Mathf.Cos(t*Mathf.PI*12))*envelope;
            }
            return Vector3.Lerp(start,end,t)+right*(Mathf.Sin(t*Mathf.PI*5)*Mathf.Sin(t*Mathf.PI)*.55f);
        }
    }
}
