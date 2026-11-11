using UnityEngine;

namespace TomorinTrial
{
    // Track committed actions and defense outcomes on the authoritative simulation.
    public sealed class TrialDuelRead
    {
        const float ReactionTime=.18f;
        const float HabitThreshold=1.65f;
        public FighterAction Action {get;private set;}
        public float ActionAge {get;private set;}
        public float GuardAge {get;private set;}
        public float DodgeHabit {get;private set;}
        public bool Ready=>ActionAge>=ReactionTime;
        public bool Healing=>Ready && Action==FighterAction.Heal;
        public bool Charging=>Ready && (Action==FighterAction.Charge || Action==FighterAction.Heavy);
        public bool LightPressure=>Ready && basicLight;
        public bool RepeatingLight=>lightHabit>=HabitThreshold;
        public bool HoldingGuard=>GuardAge>=.45f;
        public bool RepeatingParry=>parryHabit>=HabitThreshold || guardHabit>=2.45f;
        public bool EarlyDodge=>DodgeHabit>=HabitThreshold;
        TrialPlayer observed;
        float lightHabit,parryHabit,guardHabit,lastActionTime;
        int combo;
        bool basicLight,wasGuarding;

        internal static bool IsBasicLight(TrialFighter actor)
            =>actor is TrialPlayer player && player.Action==FighterAction.Light && player.Combo>=0 && player.Combo<=2;

        public void Reset()
        {
            observed=null;Action=FighterAction.Free;ActionAge=GuardAge=DodgeHabit=0;
            lightHabit=parryHabit=guardHabit=lastActionTime=0;combo=0;basicLight=wasGuarding=false;
        }

        public void Tick(TrialPlayer target,float dt)
        {
            if(!target || !target.Alive){Reset();return;}
            bool newTarget=observed!=target;
            if(newTarget){Reset();observed=target;}
            dt=Mathf.Max(0,dt);
            DodgeHabit=Mathf.Max(0,DodgeHabit-dt*.22f);
            lightHabit=Mathf.Max(0,lightHabit-dt*.48f);
            guardHabit=Mathf.Max(0,guardHabit-dt*.34f);
            parryHabit=Mathf.Max(0,parryHabit-dt*.13f);
            basicLight=IsBasicLight(target);
            if(newTarget || Action!=target.Action || combo!=target.Combo || target.ActionTime<lastActionTime)
            {
                Action=target.Action;ActionAge=0;
                if(Action==FighterAction.Dodge)DodgeHabit=Mathf.Min(3,DodgeHabit+1);
                if(basicLight)lightHabit=Mathf.Min(3,lightHabit+1);
            }
            else ActionAge+=dt;
            combo=target.Combo;lastActionTime=target.ActionTime;
            if(target.Guarding && !wasGuarding)guardHabit=Mathf.Min(3,guardHabit+1);
            wasGuarding=target.Guarding;
            GuardAge=target.Guarding?GuardAge+dt:0;
        }

        internal void ObserveParry(TrialPlayer defender)
        {
            if(observed==defender && defender)parryHabit=Mathf.Min(3,parryHabit+1.25f);
        }

        internal bool BracesAgainst(TrialFighter attacker)
            =>observed==attacker && RepeatingLight && IsBasicLight(attacker);

        internal float ResponseScore(ChampionMove move,float distance)
        {
            float score=0;
            if((LightPressure || RepeatingLight && basicLight) && move.shape==ChampionShape.Counter)
                score=RepeatingLight?5:3;
            if(RepeatingLight && basicLight && distance<=move.range &&
               (move.shape==ChampionShape.Stomp || move.shape==ChampionShape.Grab ||
                move.shape==ChampionShape.Vacuum || move.shape==ChampionShape.OrbitWhip))
                score=Mathf.Max(score,3.9f-move.windup*.5f);
            if((HoldingGuard || RepeatingParry) && move.Unblockable)
                score=Mathf.Max(score,3.4f+(move.shape==ChampionShape.Grab?.5f:0));
            if(EarlyDodge && move.windup>=.95f && move.shape!=ChampionShape.Counter)
                score=Mathf.Max(score,2+Mathf.Min(move.windup,1.5f)+(move.beats>1?.25f:0));
            bool chase=move.shape==ChampionShape.Thrust || move.shape==ChampionShape.DashCut ||
                move.shape==ChampionShape.Snipe || move.shape==ChampionShape.Bullets || move.shape==ChampionShape.Lash ||
                move.shape==ChampionShape.BladeWave || move.shape==ChampionShape.Rocket;
            if((Healing || Charging) && chase && distance<=move.range)
                score=Mathf.Max(score,(Healing?5.8f:4.4f)-move.windup);
            return score;
        }

        public float Weight(ChampionMove move)=>1+ResponseScore(move,0);
    }
}
