using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialPlayer
    {
        const float DodgeFollowupGrace=.12f;
        const float DodgeCarryDuration=.075f;
        public const float DodgeLightLinkTime=.11f;
        const float DodgeLightChordWindow=.08f;
        const float RollThrustDriveEnd=.28f;
        internal const float DodgeInvulnerabilityEnd=.32f;
        float rollThrustEvasionTime;
        internal bool RollThrustEvading=>Action==FighterAction.Light && Combo==RollThrust && ActionTime<rollThrustEvasionTime;
        float DodgeCarryWindow=>Combo==SpiralSword?.18f:DodgeCarryDuration;
        float dodgeLinkRemaining,dodgeCarryRemaining;
        Vector3 dodgeCarryVelocity;
        Vector3 bufferedDodgeDirection;
        bool bufferedDodgeTurns;
        bool dodgeGuardQueued;
        bool guardWasHeld,guardPressedThisTick;
        Vector3 DodgeVelocity=>dodgeDirection*Mathf.Lerp(11.8f,3.2f,Progress)*(Combo==ParryDodge?2:1);
        float perfectDodgeActionTime,perfectGhostUntil;
        int perfectGhostCount;
        Vector3 perfectGhostPosition;
        float PerfectDodgeSpeed=>perfectDodgeAwarded?1+1.2f*(1-Mathf.Clamp01((ActionTime-perfectDodgeActionTime)/.115f)):1;
        float DodgeLightLinkAge=>perfectDodgeAwarded && Time.time<blinkReadyUntil
            ?Mathf.Max(.07f,perfectDodgeActionTime+.025f):DodgeLightLinkTime;

        internal void ShowPerfectDodge(Vector3 contact)
        {
            perfectGhostPosition=contact;
            perfectGhostCount=0;
            perfectGhostUntil=Time.unscaledTime+.55f;
        }

        internal void SamplePerfectDodgeTrail()
        {
            if(Time.unscaledTime>=perfectGhostUntil)return;
            if(!Alive || game.HasResult){ClearPerfectDodgeTrail();return;}
            bool driving=Action==FighterAction.Light && (Combo==RollThrust && Progress<=RollThrustDriveEnd
                || Combo==BlinkHorizontalSlash && Progress<=BlinkSlashDriveEnd);
            if(Action!=FighterAction.Dodge && !driving)
            {
                // The combat cue can reach a peer just before its dodge snapshot.
                if(perfectGhostCount>0)ClearPerfectDodgeTrail();
                return;
            }

            var position=transform.position;
            if((position-perfectGhostPosition).sqrMagnitude>16f){ClearPerfectDodgeTrail();return;}
            bool limited=Application.isMobilePlatform || TrialGraphicsBudget.Performance;
            if(perfectGhostCount==0)
            {
                // Keep the first pose at the contact point even if the next frame has moved on.
                game.Effects.PerfectDodgeAfterimage(this,perfectGhostPosition-position,0);
                perfectGhostCount=1;
            }
            if(perfectGhostCount>=(limited?3:5))return;
            float spacing=limited?.82f:.54f;
            if((position-perfectGhostPosition).sqrMagnitude<spacing*spacing)return;
            game.Effects.PerfectDodgeAfterimage(this,Vector3.zero,perfectGhostCount);
            game.Effects.PerfectDodgeWake(perfectGhostPosition,position,perfectGhostCount);
            perfectGhostPosition=position;
            perfectGhostCount++;
        }

        void ClearPerfectDodgeTrail(){perfectGhostUntil=0;perfectGhostCount=0;}

        bool CanLinkDodgeAttack=>Action==FighterAction.Dodge || Action==FighterAction.Free && dodgeLinkRemaining>0 && !Guarding && !weavePending && !airborneCombo;
        void BufferDodge(TrialInput input)
        {
            bufferedDodge=Time.time;
            bufferedDodgeOrder=++combatInputOrder;
            Vector3 forward=input.NetworkDirected?Quaternion.Euler(0,input.NetworkYaw,0)*Vector3.forward:Vector3.ProjectOnPlane(game.CameraRig.transform.forward,Vector3.up).normalized;
            var direction=forward*input.Move.y+Vector3.Cross(Vector3.up,forward)*input.Move.x;
            bufferedDodgeTurns=direction.sqrMagnitude>.000001f;
            bufferedDodgeDirection=bufferedDodgeTurns?direction.normalized:-forward;
        }
        void TickDodgeLink(float dt){dodgeLinkRemaining=Mathf.Max(0,dodgeLinkRemaining-dt);}
        void CarryDodgeIntoAttack(Vector3 velocity)
        {
            // Grounded roll derivatives and landing pursuit inherit momentum.
            // Teleport and aerial follow-ups retain their authored displacement.
            if(Combo!=RollFlash && Combo!=SpiralSword && Combo!=SakuraFollowup)return;
            dodgeCarryVelocity=Vector3.ClampMagnitude(velocity,10);
            dodgeCarryRemaining=DodgeCarryWindow;
        }
        void TickDodgeCarry(float dt)
        {
            if(dodgeCarryRemaining<=0)return;
            float before=dodgeCarryRemaining;
            dodgeCarryRemaining=Mathf.Max(0,before-dt);
            // Integrate a linearly fading velocity, independent of the frame rate.
            float distanceTime=(before*before-dodgeCarryRemaining*dodgeCarryRemaining)/(2*DodgeCarryWindow);
            motor.Move(dodgeCarryVelocity*distanceTime);
        }
        void ClearDodgeLink(){dodgeLinkRemaining=dodgeCarryRemaining=rollThrustEvasionTime=0;dodgeCarryVelocity=Vector3.zero;dodgeGuardQueued=false;}
    }
}
