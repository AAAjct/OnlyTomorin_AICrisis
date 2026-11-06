using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialPlayer
    {
        const float DodgeFollowupGrace=.12f;
        const float DodgeCarryDuration=.075f;
        float DodgeCarryWindow=>Combo==SpiralSword?.18f:DodgeCarryDuration;
        float dodgeLinkRemaining,dodgeCarryRemaining;
        Vector3 dodgeCarryVelocity;
        bool dodgeGuardQueued;
        bool guardWasHeld,guardPressedThisTick;
        Vector3 DodgeVelocity=>dodgeDirection*Mathf.Lerp(11.8f,3.2f,Progress)*(Combo==ParryDodge?2:1);
        bool CanLinkDodgeAttack=>Action==FighterAction.Dodge || Action==FighterAction.Free && dodgeLinkRemaining>0 && !Guarding && !weavePending && !airborneCombo;
        void TickDodgeLink(float dt){dodgeLinkRemaining=Mathf.Max(0,dodgeLinkRemaining-dt);}
        void CarryDodgeIntoAttack(Vector3 velocity)
        {
            // Grounded roll derivatives and landing pursuit inherit momentum.
            // Teleport and aerial follow-ups retain their authored displacement.
            if(Combo!=RollThrust && Combo!=RollFlash && Combo!=SpiralSword && Combo!=SakuraFollowup)return;
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
        void ClearDodgeLink(){dodgeLinkRemaining=dodgeCarryRemaining=0;dodgeCarryVelocity=Vector3.zero;dodgeGuardQueued=false;}
    }
}
