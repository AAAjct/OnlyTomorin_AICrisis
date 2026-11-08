using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialEnemy
    {
        float runHealthScale=1;
        public float RunDamageScale {get;private set;}=1;
        float runSlow,runSlowUntil,burnUntil,burnClock,burnDamage;
        TrialPlayer burnOwner;
        public void ConfigureRunStrength(float healthScale,float damageScale)
        {
            runHealthScale=healthScale;ScaleForParty(game.PartySize);Health=MaxHealth;RunDamageScale=damageScale;
            runSlow=runSlowUntil=burnUntil=burnClock=burnDamage=0;burnOwner=null;
        }
        public void WakeForRun(){Awakened=true;}
        public void PullForRun(Vector3 displacement){if(Alive && !IsBoss && !CoffinSealed && motor.enabled)motor.Move(displacement);}
        public void ApplyRunSlow(float amount){if(!IsBoss){runSlow=Mathf.Min(.54f,amount);runSlowUntil=Time.time+2;}}
        public void ApplyRunBurn(TrialPlayer owner,float damage){burnOwner=owner;burnDamage=damage;burnUntil=Time.time+4;}
        void TickRunStatuses(float dt)
        {
            if(!Alive || !game.IsEndless)return;
            burnClock+=dt;
            if(Time.time<burnUntil && burnClock>=1 && burnOwner)
            {burnClock=0;burnOwner.DealRunEffectDamage(this,burnDamage);}
        }
    }
}
