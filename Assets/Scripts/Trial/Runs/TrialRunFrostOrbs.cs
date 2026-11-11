using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    // The run owns in-flight orbs so a cast already released survives the caster's death.
    internal sealed class TrialRunFrostOrbs
    {
        const float Speed=11.5f, Lifetime=1.5f, Radius=.25f, PlayerRadius=.32f;
        readonly TrialGame game;
        readonly List<Orb> pool=new List<Orb>();
        int nextId;
        Material ice,trailMaterial;

        sealed class Orb
        {
            public int id;
            public float remaining;
            public Vector3 position,direction;
            public TrialEnemy source;
            public Transform visual;
            public TrailRenderer trail;
            public bool active,received;
        }

        public TrialRunFrostOrbs(TrialGame game){this.game=game;}

        public void Launch(TrialEnemy source,Vector3 position,Vector3 direction)
        {
            var orb=Acquire(++nextId,position);
            orb.source=source;orb.position=position;orb.direction=direction;orb.remaining=Lifetime;
            Emit(TrialCombatCue.RunFrostOrbCast,position);
        }

        Orb Acquire(int id,Vector3 point)
        {
            Orb orb=null;
            foreach(var candidate in pool)
                if(!candidate.active){orb=candidate;break;}
            if(orb==null)
            {
                if(!ice)ice=Resources.Load<Material>("Trial/VFX/FrostIce");
                if(!trailMaterial)trailMaterial=Resources.Load<Material>("Trial/VFX/TideRibbon");
                var obj=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.name="Snow Lotus frost orb";
                obj.layer=2;
                obj.transform.SetParent(game.transform,false);
                var collider=obj.GetComponent<Collider>();collider.enabled=false;
                Object.Destroy(collider);
                var renderer=obj.GetComponent<MeshRenderer>();
                renderer.sharedMaterial=ice;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows=false;
                var trail=obj.AddComponent<TrailRenderer>();
                trail.sharedMaterial=trailMaterial;trail.time=.23f;trail.minVertexDistance=.08f;
                trail.startWidth=.31f;trail.endWidth=.015f;
                trail.startColor=new Color(.55f,.9f,1,.9f);trail.endColor=new Color(.25f,.65f,1,0);
                trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows=false;
                obj.transform.localScale=Vector3.one*(Radius*2);
                orb=new Orb{visual=obj.transform,trail=trail};
                pool.Add(orb);
            }
            orb.id=id;orb.active=true;orb.visual.position=point;
            orb.visual.gameObject.SetActive(true);orb.trail.Clear();
            return orb;
        }

        public void Tick(float dt)
        {
            if(dt<=0)return;
            foreach(var orb in pool)
            {
                if(!orb.active)continue;
                if(!orb.source){Release(orb);continue;}
                float distance=Speed*Mathf.Min(dt,orb.remaining);
                bool blocked=Physics.SphereCast(orb.position,Radius,orb.direction,out var wall,distance,1,QueryTriggerInteraction.Ignore);
                if(blocked)distance=wall.distance;

                TrialPlayer victim=null;
                if(Mirror.NetworkServer.active)
                {
                    foreach(var player in TrialNetworkManager.Current.Players)
                        FindContact(orb,player,ref victim,ref distance);
                }
                else FindContact(orb,game.Player,ref victim,ref distance);

                orb.position+=orb.direction*distance;
                if(victim || blocked)
                {
                    if(victim && victim.ReceiveHazardHit(orb.source,21,19,false,-orb.direction))
                        victim.ApplyRunFrostSlow();
                    Emit(TrialCombatCue.RunFrostOrbImpact,orb.position);
                    Release(orb);
                    if(game.HasResult)return;
                    continue;
                }
                orb.remaining-=dt;
                if(orb.remaining<=0)Release(orb);
                else orb.visual.position=orb.position;
            }
        }

        static void FindContact(Orb orb,TrialPlayer player,ref TrialPlayer victim,ref float distance)
        {
            if(!player || !player.Alive || player.ChoosingBoon ||
                !player.OverlapsAttackHeight(orb.position.y-Radius,orb.position.y+Radius))return;
            var offset=player.transform.position-orb.position;offset.y=0;
            float along=Vector3.Dot(offset,orb.direction);
            float contactRadius=Radius+PlayerRadius;
            float chord=contactRadius*contactRadius-(offset-orb.direction*along).sqrMagnitude;
            if(chord<0)return;
            float halfChord=Mathf.Sqrt(chord);
            if(along+halfChord<0)return;
            float entry=Mathf.Max(0,along-halfChord);
            if(entry>distance || victim && entry>=distance)return;
            victim=player;distance=entry;
        }

        void Emit(TrialCombatCue cue,Vector3 point)
        {
            var value=new TrialCombatEvent{player=-1,cue=cue,position=point};
            if(Mirror.NetworkServer.active)TrialNetworkManager.Current.BroadcastEnemyCombat(value);
            else game.Effects.PlayCombat(value,false);
        }

        public TrialFrostOrbState[] CaptureNetwork()
        {
            int count=0;
            foreach(var orb in pool)if(orb.active)count++;
            if(count==0)return System.Array.Empty<TrialFrostOrbState>();
            var states=new TrialFrostOrbState[count];
            int index=0;
            foreach(var orb in pool)if(orb.active)
                states[index++]=new TrialFrostOrbState{id=orb.id,position=orb.position,direction=orb.direction};
            return states;
        }

        public void ApplyNetwork(TrialFrostOrbState[] states,float snapshotAge)
        {
            foreach(var orb in pool)orb.received=false;
            if(states!=null)foreach(var state in states)
            {
                Orb match=null;
                foreach(var orb in pool)if(orb.active && orb.id==state.id){match=orb;break;}
                var point=state.position+state.direction*(Speed*snapshotAge);
                if(match==null)match=Acquire(state.id,point);
                match.received=true;match.visual.position=point;
            }
            foreach(var orb in pool)if(orb.active && !orb.received)Release(orb);
        }

        public void Clear(){foreach(var orb in pool)if(orb.active)Release(orb);}
        static void Release(Orb orb)
        {
            orb.active=false;orb.source=null;orb.visual.gameObject.SetActive(false);
        }
    }
}
