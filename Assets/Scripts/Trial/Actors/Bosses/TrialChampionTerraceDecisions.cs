using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialChampion
    {
        readonly float[] terraceCutStartedAt=new float[terraceAttacks.Length];
        TrialPlayer terraceObserved;
        float terraceNextDecision, terraceGuardRead, terraceRetreatRead, terraceEarlyRollRead;
        float terraceBackstepUntil;
        int terraceOrbitSide;

        void ResetTerraceDecisions()
        {
            System.Array.Fill(terraceCutStartedAt,-30f);
            terraceObserved=null;
            terraceNextDecision=0;
            terraceOrbitSide=1;
            ClearTerraceReads();
        }

        void ClearTerraceReads()
        {
            duel.Reset();
            terraceGuardRead=terraceRetreatRead=terraceEarlyRollRead=0;
            terraceBackstepUntil=terraceRetreatUntil=0;
            terraceLights=0;
            terraceLastLight=-10;
        }

        void ObserveTerrace(TrialPlayer target,Vector3 delta,float dt)
        {
            if(terraceObserved!=target)
            {
                ClearTerraceReads();
                terraceObserved=target;
            }
            duel.Tick(target,dt);
            terraceGuardRead=Mathf.Max(0,terraceGuardRead-.42f*dt);
            terraceRetreatRead=Mathf.Max(0,terraceRetreatRead-.52f*dt);
            terraceEarlyRollRead=Mathf.Max(0,terraceEarlyRollRead-.30f*dt);

            Vector3 direction=delta.normalized;
            if(duel.Action==FighterAction.Dodge && duel.ActionAge==0)
            {
                if(Vector3.Dot(target.DodgeDirection,direction)>.60f)
                {
                    terraceRetreatRead=Mathf.Min(3,terraceRetreatRead+1.2f);
                    terraceBackstepUntil=terraceClock+.68f;
                }
                if(WindingUp && Cut!=TerraceCut.Brace && Age<terraceWindup-.18f && delta.sqrMagnitude<25)
                    terraceEarlyRollRead=Mathf.Min(3,terraceEarlyRollRead+1.25f);
            }
            if(duel.Action==FighterAction.Free && Vector3.Dot(target.PlanarVelocity,direction)>3.25f)
            {
                terraceRetreatRead=Mathf.Min(3,terraceRetreatRead+2.05f*dt);
                terraceBackstepUntil=terraceClock+.25f;
            }
        }

        TerraceCut? ChooseTerraceOpener(TrialPlayer target,Vector3 delta,float distance)
        {
            if(Vector3.Angle(transform.forward,delta)>60 || Mathf.Abs(target.transform.position.y-transform.position.y)>1.6f)
                return null;

            bool pinned=TerracePlayerPinned(target);
            bool hasSideRoom=DuelWallDistance(transform.position)>1.5f;
            bool lightPressure=TerraceLightPressure || duel.RepeatingLight && duel.LightPressure;
            bool exposed=duel.Healing || duel.Charging && target.Progress<.55f;
            bool retreating=terraceRetreatRead>=1.5f;
            float guardPressure=Mathf.Max(terraceGuardRead,duel.HoldingGuard?2.6f:duel.RepeatingParry?1.6f:0);
            TerraceCut? best=null;
            float bestScore=0;

            void Consider(TerraceCut cut,float min,float max,float score)
            {
                if(distance<min || distance>max || !TerraceReady(cut))return;
                float recency=Mathf.Clamp01(1-(terraceClock-terraceCutStartedAt[(int)cut])/6f);
                score-=recency*1.1f;
                if(terraceLastOpener==(int)cut)score-=1.45f;
                if(score<=bestScore)return;
                bestScore=score;
                best=cut;
            }

            Consider(TerraceCut.Pair,0,2.75f,4.35f+(exposed?1:0));
            Consider(TerraceCut.Delay,1.35f,3.0f,3.35f+terraceEarlyRollRead*1.85f);
            Consider(TerraceCut.Charge,2.25f,3.45f,3.15f+guardPressure*.55f-(lightPressure || exposed?2:0));
            Consider(TerraceCut.Dash,4.25f,8.5f,4.8f);
            if(lightPressure)Consider(TerraceCut.Brace,0,2.8f,8f);
            if(!pinned)
            {
                if(exposed || retreating)Consider(TerraceCut.Chase,3.0f,7.45f,exposed?9:7);
                if(guardPressure>=1.35f)Consider(TerraceCut.Sweep,1.2f,3.0f,5.2f+guardPressure);
                if(hasSideRoom)
                {
                    Consider(TerraceCut.Side,1.25f,2.95f,3.45f+(duel.EarlyDodge?.8f:0));
                    if(Phase==2)Consider(TerraceCut.Wheel,2.05f,4.05f,4.65f+(retreating?.65f:0));
                }
            }
            return best;
        }

        void MoveTerraceNeutral(TrialPlayer target,Vector3 delta,float distance,float dt)
        {
            bool bait=duel.Charging && target.Progress<.55f && !TerracePlayerPinned(target);
            bool pursue=duel.Healing || terraceRetreatRead>=1.5f;
            float spacing=bait?2.9f:Phase==2 || pursue?1.9f:2.25f;
            Vector3 direction=distance>.02f?delta/distance:transform.forward;
            terraceOrbitSide=TerraceOpenSide(direction);
            Vector3 tangent=Vector3.Cross(Vector3.up,direction)*terraceOrbitSide;
            float approach=Mathf.Clamp((distance-spacing)*3.8f,-2.6f,pursue?8.3f:7.1f);
            float orbit=(bait?3.3f:2.55f)*Mathf.Clamp01((4.8f-distance)/2.6f);
            Vector3 motion=direction*approach+tangent*orbit;
            if(DuelWallDistance(transform.position)<1.6f)
                motion+=Planar(TrialWorld.TerraceCenter-transform.position).normalized*2.2f;
            StepTerraceBody(motion*dt,dt);
            fighter.BossMotion(Mathf.Clamp01(motion.magnitude/4.8f));
        }

        int TerraceOpenSide(Vector3 direction)
        {
            Vector3 side=Vector3.Cross(Vector3.up,direction)*1.8f;
            float right=DuelWallDistance(transform.position+side);
            float left=DuelWallDistance(transform.position-side);
            return Mathf.Abs(right-left)<.55f?terraceOrbitSide:right>left?1:-1;
        }

        bool TerraceReady(TerraceCut cut)
        {
            float delay=cut switch
            {
                TerraceCut.Delay=>2.65f,
                TerraceCut.Side=>4.1f,
                TerraceCut.Brace=>5.8f,
                TerraceCut.Chase=>7.8f,
                TerraceCut.Sweep=>10.5f,
                TerraceCut.Charge=>8f,
                TerraceCut.Downbeat=>10f,
                TerraceCut.Wheel=>5.5f,
                _=>0
            };
            return terraceClock-terraceCutStartedAt[(int)cut]>=delay;
        }

        void RememberTerraceCut(TerraceCut cut)
        {
            terraceCutStartedAt[(int)cut]=terraceClock;
            if(cut==TerraceCut.FinalThrust)terraceCutStartedAt[(int)TerraceCut.Downbeat]=terraceClock;
            if(cut==TerraceCut.Sweep)terraceGuardRead=0;
        }

        bool TryTerraceFollowup()
        {
            if(terraceGuardBroken || !terraceTarget || !terraceTarget.Alive || terraceHits>=4 || TerracePlayerPinned(terraceTarget))
                return false;
            if(Phase==1 && fighter.Health<=fighter.MaxHealth*.5f)return false;

            Vector3 delta=Planar(terraceTarget.transform.position-transform.position);
            float distance=delta.magnitude, angle=Vector3.Angle(heading,delta);
            bool contact=terraceOutcome>=TerraceOutcome.Hit;
            bool retreating=terraceClock<terraceBackstepUntil;
            if(Cut==TerraceCut.Dash)
                return angle<=35 && distance<=3.15f && (contact || distance<=2.35f) && TerraceFollow(TerraceCut.Pair);
            if(Cut==TerraceCut.Downbeat)
                return angle<=28 && !retreating && (contact?distance<=3.1f:distance<=2.45f) && TerraceFollow(TerraceCut.FinalThrust);

            bool center=DuelWallDistance(transform.position)>3 &&
                DuelWallDistance(terraceTarget.transform.position)>3;
            if(Phase==2 && (Cut==TerraceCut.Pair || Cut==TerraceCut.Wheel) && contact && !retreating &&
                center && angle<=35 && distance<=2.7f && TerraceFollow(TerraceCut.Downbeat))return true;

            if(Cut==TerraceCut.Riposte)
                return angle<=38 && distance>=1.35f && distance<=2.8f && terraceEarlyRollRead>=1.3f && TerraceFollow(TerraceCut.Delay);
            if(Cut!=TerraceCut.Pair)return false;
            if(!contact && angle>=35 && angle<=75 && distance>=1.6f && distance<=3.1f && TerraceFollow(TerraceCut.Side))
                return true;
            if(retreating && terraceRound>1 && angle<=29 && distance>=3.2f && distance<=6f && TerraceFollow(TerraceCut.Chase))
                return true;
            if(angle>46 || distance>3.0f || !contact && (retreating || distance>2.45f))return false;

            bool delay=terraceRound>1 && (terraceOutcome==TerraceOutcome.Blocked || duel.HoldingGuard ||
                terraceEarlyRollRead>=1.25f || terraceClock-terraceCutStartedAt[(int)TerraceCut.Return]<4f);
            return delay && TerraceFollow(TerraceCut.Delay) || TerraceFollow(TerraceCut.Return);
        }

        bool TerraceFollow(TerraceCut cut)
        {
            var next=terraceAttacks[(int)cut];
            if(!TerraceReady(cut) || terraceClock-terraceRoundStart+next.Move.windup+next.Active>2.65f)return false;
            BeginTerraceCut(cut,true);
            return true;
        }
    }
}
