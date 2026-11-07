using UnityEngine;

namespace TomorinTrial
{
    // Observe committed actions, never input commands. Memory is local to one opponent.
    public sealed class TrialDuelRead
    {
        public FighterAction Action {get;private set;}
        public float ActionAge {get;private set;}
        public float GuardAge {get;private set;}
        public float DodgeHabit {get;private set;}
        public bool Ready=>ActionAge>=.18f;
        public bool Healing=>Ready && Action==FighterAction.Heal;
        public bool Charging=>Ready && (Action==FighterAction.Charge || Action==FighterAction.Heavy);
        public bool LightPressure=>Ready && Action==FighterAction.Light;
        public bool HoldingGuard=>GuardAge>=.45f;
        public bool EarlyDodge=>DodgeHabit>=1.65f;
        TrialPlayer observed;

        public void Reset()
        {observed=null;Action=FighterAction.Free;ActionAge=GuardAge=DodgeHabit=0;}

        public void Tick(TrialPlayer target,float dt)
        {
            if(!target || !target.Alive){Reset();return;}
            if(observed!=target){Reset();observed=target;Action=target.Action;}
            dt=Mathf.Max(0,dt);
            DodgeHabit=Mathf.Max(0,DodgeHabit-dt*.22f);
            if(Action!=target.Action)
            {
                Action=target.Action;ActionAge=0;
                if(Action==FighterAction.Dodge)DodgeHabit=Mathf.Min(3,DodgeHabit+1);
            }
            else ActionAge+=dt;
            GuardAge=target.Guarding?GuardAge+dt:0;
        }

        public float Weight(ChampionMove move)
        {
            float weight=1;
            if(HoldingGuard && move.Unblockable)weight*=2.6f;
            if(LightPressure && move.shape==ChampionShape.Counter)weight*=3;
            if(EarlyDodge)weight*=move.windup>=.65f?2.4f:.7f;
            if((Healing || Charging) && (move.shape==ChampionShape.Thrust || move.shape==ChampionShape.Snipe || move.shape==ChampionShape.DashCut))weight*=2.5f;
            return weight;
        }
    }
}
