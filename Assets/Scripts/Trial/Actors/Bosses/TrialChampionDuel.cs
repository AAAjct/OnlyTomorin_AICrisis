using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialChampion
    {
        readonly TrialDuelRead duel=new TrialDuelRead();
        bool duelDeflected;

        int ChooseDuelFollowup(float distance)=>ChooseDuelResponse(distance,true);

        int ChooseDuelResponse(float distance,bool followup)
        {
            int best=-1;float highest=0;
            var target=game.CombatTarget;
            for(int i=0;i<TrialChampionCatalog.SkillCount;i++)
            {
                if(followup && i==previous)continue;
                var move=TrialChampionCatalog.Move(Kind,i);
                if((followup && (move.beats>4 || move.windup<.45f)) || MoveSuitability(move,distance)<=0)continue;
                float score=duel.ResponseScore(move,distance)*HeightWeight(move,target);
                if(i==previous)score*=.82f;
                else if((usedMoves&(1u<<i))!=0)score*=.94f;
                if(score>highest){highest=score;best=i;}
            }
            return best;
        }

        internal bool BracesAgainst(TrialFighter attacker)=>Attacking && !Recovering && duel.BracesAgainst(attacker);
        internal void ObserveParry(TrialPlayer defender)=>duel.ObserveParry(defender);

        bool TryDuelBranch()
        {
            // Only the first beat can branch, before the second aim commitment.
            // The new move starts at age zero with its FULL, distinct warning.
            if(Phase<2 || chainDepth!=0 || duelDeflected || beat!=1 || Move.beats<2 || Move.beats>4 ||
               Age-Move.windup<.18f || NextImpact-Age<=AimCommitTime || fighter.ResolveActive || ActiveHazards>0)return false;
            var partner=Partner();if(partner && partner.Alive)return false;
            float distance=Planar(game.CombatTarget.transform.position-transform.position).magnitude;
            int next=ChooseDuelFollowup(distance);
            if(next<0)return false;
            chainDepth=2; // Consumes the entire chain budget, including phase three.
            BeginMove(next);
            return true;
        }
    }
}
