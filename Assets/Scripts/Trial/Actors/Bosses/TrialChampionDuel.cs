using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialChampion
    {
        readonly TrialDuelRead duel=new TrialDuelRead();
        bool duelDeflected;

        int ChooseDuelFollowup(float distance)
        {
            // A quiet opponent earns the end of the string. No automatic random extension.
            if(!duel.HoldingGuard && !duel.EarlyDodge && !duel.Healing && !duel.Charging && !duel.LightPressure)return -1;
            int best=-1;float highest=1.01f;
            for(int i=0;i<TrialChampionCatalog.SkillCount;i++)
            {
                if(i==previous)continue;
                var move=TrialChampionCatalog.Move(Kind,i);
                if(move.beats>4 || move.windup<.45f || MoveSuitability(move,distance)<=0)continue;
                float score=duel.Weight(move);
                if((usedMoves&(1u<<i))!=0)score*=.7f;
                if(score>highest){highest=score;best=i;}
            }
            return best;
        }

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
