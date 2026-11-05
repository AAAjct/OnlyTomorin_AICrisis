using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialPlayer
    {
        bool chainHeavy,airborneCombo,swordLanded;
        float launchFloor;
        int swordEmissions;
        Vector3 swordStart;
        Vector3 swordHeading;
        float swordMotionProgress;
        readonly List<SwordFlight> swordFlights=new List<SwordFlight>();
        sealed class SwordFlight
        {
            public Vector3 point,direction;
            public float remaining,damage,posture;
            public TrialCombatCue cue;
            public readonly HashSet<TrialFighter> hits=new HashSet<TrialFighter>();
        }

        public static bool IsAerialMove(int move)=>move==3 || IsAirBlade(move) || IsDive(move);
        static float LightLinkProgress(int move)=>move==3?.50f:IsAirBlade(move)?.60f:move==AirSandDive?.75f:.55f;
        void BeginSwordArt()
        {
            swordStart=transform.position;swordHeading=transform.forward;swordMotionProgress=0;swordEmissions=0;swordLanded=false;
            if(Combo==3){launchFloor=transform.position.y;airborneCombo=true;}
        }
        void ResetSwordArts()
        {airborneCombo=false;swordLanded=false;swordEmissions=0;swordFlights.Clear();}

        protected override void TickGravity(float dt)
        {
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
            if(airborneCombo && attacking && IsDive(Combo))
            {
                if(Progress>.12f)
                {
                    // The ranged-light finisher dives diagonally; the sword-down heavy stays planted.
                    verticalSpeed=Combo==AirSandDive?-13:-26;
                    motor.Move((Vector3.up*verticalSpeed+(Combo==AirSandDive?swordHeading*23:Vector3.zero))*dt);
                }
                return;
            }
            bool hold=airborneCombo && (attacking && (Combo==3 || IsAirBlade(Combo)) || Action==FighterAction.Free && Time.time<chainUntil);
            if(hold)
            {
                float rise=Combo==3 && attacking?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.50f,Progress)):1;
                float targetY=launchFloor+2.15f*rise;
                motor.Move(Vector3.up*(targetY-transform.position.y));verticalSpeed=0;return;
            }
            airborneCombo=false;base.TickGravity(dt);
        }

        // All displacement goes through CharacterController; walls still stop lunges and retreat.
        bool TickSwordMotion(float dt)
        {
            if(TickEmpoweredMotion(dt))return true;
            if(IsAerialMove(Combo))return true;
            if(Combo==6)
            {MoveSwordDistance(-swordHeading,4.2f,.04f,.30f);return true;}
            if(Combo==5 || Combo==SakuraFollowup)
            {MoveSwordDistance(swordHeading,Combo==5?6.0f:6.8f,.12f,.48f);return true;}
            if(Combo==2)
            {MoveSwordDistance(swordHeading,1.8f,.13f,.43f);return true;}
            return false;
        }
        void MoveSwordDistance(Vector3 heading,float distance,float start,float end)
        {
            float progress=Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,Progress));
            float step=Mathf.Max(0,progress-swordMotionProgress)*distance;swordMotionProgress=progress;
            if(step>0)motor.Move(heading*step);
        }
        void TickSwordArts()
        {
            TickEmpoweredArts();
            if((Combo==0 || Combo==1 || Combo==2) && Action==FighterAction.Light && Progress>=.22f && swordEmissions==0)
            {
                swordEmissions++;
                EmitSwordCue(Combo==0?TrialCombatCue.RisingCut:Combo==1?TrialCombatCue.CrescentCut:TrialCombatCue.ThrustTrace,transform.position+Vector3.up,swordHeading,transform.position+swordHeading*2.6f+Vector3.up,1);
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
            if((Combo==5 || Combo==SakuraFollowup) && Progress>=.48f && swordEmissions==0)
            {
                swordEmissions++;
                Vector3 from=swordStart+Vector3.up,to=transform.position+Vector3.up;
                EmitSwordCue(TrialCombatCue.SakuraScatter,from,swordHeading,to,1);
                SwordSegment(from,to,1.35f,24,20,new HashSet<TrialFighter>());
            }
            if(IsDive(Combo) && !swordLanded && Progress>=.34f && motor.isGrounded)
            {
                swordLanded=true;airborneCombo=false;verticalSpeed=-2;
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

        void SwordSegment(Vector3 from,Vector3 to,float radius,float damage,float posture,HashSet<TrialFighter> hits)
        {
            Vector3 segment=to-from;
            foreach(var enemy in game.Opponents(this))
            {
                if(!enemy || !enemy.Alive || hits.Contains(enemy))continue;
                Vector3 torso=enemy.transform.position+Vector3.up;
                float t=segment.sqrMagnitude>.0001f?Mathf.Clamp01(Vector3.Dot(torso-from,segment)/segment.sqrMagnitude):0;
                float body=enemy is TrialEnemy foe?foe.BodyRadius:.28f;
                if(Vector3.Distance(torso,from+segment*t)>radius+body+.24f || Physics.Linecast(from,torso,1,QueryTriggerInteraction.Ignore))continue;
                hits.Add(enemy);SwordDamage(enemy,damage,posture,false);
            }
        }

        void LaunchSword(TrialCombatCue cue,Vector3 point,Vector3 direction,float damage,float posture)
        {
            if(swordFlights.Count>=48)return;
            swordFlights.Add(new SwordFlight{point=point,direction=direction,remaining=TrialSwordFlight.Lifetime(cue),cue=cue,damage=damage,posture=posture});
            if(Mirror.NetworkServer.active)TrialNetworkManager.Current.BroadcastCombat(this,cue,point,direction);
            else game.Effects.PlayCombat(new TrialCombatEvent{cue=cue,position=point,forward=direction},this==game.Player);
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
                    flight.hits.Add(enemy);SwordDamage(enemy,flight.damage,flight.posture,false);
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
        void SwordDamage(TrialFighter enemy,float damage,float posture,bool finish)
        {
            if(!enemy.ReceiveHit(this,damage*(Boon==TrialBoon.Edge?1.12f:1),posture*(Boon==TrialBoon.Breaker?1.15f:1)))return;
            RestoreCombatStamina(enemy.Alive?HitStamina:KillStamina);
            HoldCombat(finish?.12f:.065f);
            if(!(enemy is TrialEnemy boss && boss.ResolveActive))enemy.HoldCombat(finish?.13f:.075f);
            EmitCombat(!enemy.Alive?TrialCombatCue.Kill:finish?TrialCombatCue.FinisherHit:TrialCombatCue.Hit,enemy.transform.position+Vector3.up);
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
