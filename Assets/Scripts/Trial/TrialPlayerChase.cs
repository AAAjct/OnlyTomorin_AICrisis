using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialPlayer
    {
        TrialFighter chaseTarget;
        float chaseRemaining,perfectGhostUntil;
        public void ShowPerfectDodge()
        {perfectGhostUntil=Time.unscaledTime+.35f;game.Effects.Afterimage(this);}

        TrialFighter FindChaseTarget()
        {
            TrialFighter best=null;float score=float.MaxValue;
            foreach(var candidate in game.Opponents(this))
            {
                if(!candidate || !candidate.Alive)continue;
                var offset=candidate.transform.position-transform.position;
                if(Mathf.Abs(offset.y)>3.5f)continue;
                offset.y=0;float distance=offset.magnitude;
                float angle=Vector3.Angle(transform.forward,offset);
                if(distance>(candidate==LockTarget?24:10) || angle>(candidate==LockTarget?180:80))continue;
                if(Physics.Linecast(transform.position+Vector3.up,candidate.transform.position+Vector3.up,1,QueryTriggerInteraction.Ignore))continue;
                float rank=distance+angle*.035f-(candidate==LockTarget?2:0);
                if(rank<score){score=rank;best=candidate;}
            }
            return best;
        }

        void TickAttackChase(float dt,TrialInput input)
        {
            if(Combo==23){if(Progress>.40f && Progress<.70f)Move(-transform.forward*3.2f,dt);return;}
            if(Progress<.08f || Progress>.50f || chaseRemaining<=0 || dt<=0)return;
            Vector3 direction=transform.forward;float step;
            if(chaseTarget && chaseTarget.Alive)
            {
                var offset=Vector3.ProjectOnPlane(chaseTarget.transform.position-transform.position,Vector3.up);
                // Backward input deliberately cancels assistance; never reacquire mid-swing.
                if(Vector3.Dot(MoveDirection(input),offset.normalized)<-.35f)return;
                if(Vector3.Angle(transform.forward,offset)>100 || Mathf.Abs(chaseTarget.transform.position.y-transform.position.y)>1.8f)return;
                float radius=chaseTarget is TrialEnemy enemy?enemy.BodyRadius:.28f;
                float gap=Mathf.Max(0,offset.magnitude-(1.45f+radius));
                if(gap<=.01f)return;
                if(Physics.Linecast(transform.position+Vector3.up,chaseTarget.transform.position+Vector3.up,1,QueryTriggerInteraction.Ignore))return;
                if(Progress<.30f)Turn(offset,600,dt);
                direction=transform.forward;
                step=Mathf.Min(gap,chaseRemaining,(Combo==1 || Combo==5?10:8)*dt);
            }
            else step=Mathf.Min(chaseRemaining,2.2f*dt);
            Move(direction*(step/dt),dt);chaseRemaining-=step;
        }
    }
}
