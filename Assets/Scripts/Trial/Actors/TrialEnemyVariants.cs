using UnityEngine;

namespace TomorinTrial
{
    // These values share enemyKind with the existing guard (0), boss (1) and champions (2+).
    public enum TrialRunGuardKind : byte { NaiWa=0, DoroXueLian=32, Takosaki=33 }

    public sealed partial class TrialEnemy
    {
        const float FrostCastRange=13f, SlamRadius=3.1f;
        static readonly Color FrostTint=new Color(.42f,.87f,1);
        static readonly Color SlamTint=new Color(.85f,.48f,1);

        public TrialRunGuardKind RunGuardKind {get;private set;}
        bool HasRunSkill=>RunGuardKind!=TrialRunGuardKind.NaiWa;
        bool IsFrostGuard=>RunGuardKind==TrialRunGuardKind.DoroXueLian;
        bool RunSkillActive=>HasRunSkill && Action==FighterAction.Heavy;
        float GuardBaseHealth=>IsFrostGuard?85:HasRunSkill?130:105;
        float RunWindup=>IsFrostGuard?.70f:.96f;
        internal float RunWarningProgress=>RunSkillActive?Mathf.Clamp01(ActionTime/RunWindup):0;
        Vector3 runSkillOrigin,runSkillForward;
        LineRenderer runWarning;
        Vector3 runVisualBase,runVisualScale;
        float runGait;
        (Transform bone, Quaternion bind)[] frostBones;

        internal void InitializeRunVariant(TrialGame owner,Vector3 point,TrialRunGuardKind kind,GameObject model)
        {
            if(!model)throw new System.InvalidOperationException("Missing Endless guard prefab for "+kind);
            RunGuardKind=kind;
            Spawn=point;transform.position=point;
            float height=IsFrostGuard?1.55f:1.45f;
            Configure(owner,GuardBaseHealth,100,BodyRadius,height+.1f);
            name=DisplayName;
            Visual=Instantiate(model,transform).transform;
            foreach(var animator in Visual.GetComponentsInChildren<Animator>())animator.enabled=false;
            renderers=Visual.GetComponentsInChildren<Renderer>();
            var bounds=RunVisualBounds();
            Visual.localScale*=height/Mathf.Max(.01f,bounds.size.y);
            bounds=RunVisualBounds();
            Visual.position+=point-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            runVisualBase=Visual.localPosition;runVisualScale=Visual.localScale;
            if(IsFrostGuard)
            {
                var bones=Visual.GetComponentsInChildren<Transform>();
                frostBones=System.Array.ConvertAll(new[]{"UpperArm.L","UpperArm.R","UpperLeg.L","UpperLeg.R"},label=>
                {
                    var bone=System.Array.Find(bones,t=>t.name==label);
                    return (bone,bone?bone.localRotation:Quaternion.identity);
                });
            }
            var warning=new GameObject("Run skill footprint");
            warning.transform.SetParent(transform,false);
            runWarning=warning.AddComponent<LineRenderer>();
            runWarning.sharedMaterial=Resources.Load<Material>("Trial/VFX/TideRibbon");
            runWarning.useWorldSpace=true;runWarning.loop=!IsFrostGuard;
            runWarning.positionCount=IsFrostGuard?2:40;
            runWarning.widthMultiplier=.065f;
            runWarning.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            runWarning.receiveShadows=false;
            runWarning.enabled=false;
            if(!IsFrostGuard)warning.AddComponent<TrialSkillSurface>().Initialize(runWarning);
            cooldown=1.1f;
            transform.rotation=Quaternion.Euler(0,180,0);
            PresentRunVariant();
        }

        Bounds RunVisualBounds()
        {
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        void TickRunVariant(Vector3 delta,float distance,float dt)
        {
            if(RunSkillActive)
            {
                if(!attackLanded && ActionTime>=RunWindup)
                {
                    attackLanded=true;
                    ReleaseRunSkill();
                }
                if(RunSkillActive && ActionTime>=ActionDuration)Begin(FighterAction.Free,0);
                return;
            }

            Turn(delta,220,dt);
            if(distance>(IsFrostGuard?FrostCastRange:2.35f) ||
                Physics.Linecast(transform.position+Vector3.up,game.CombatTarget.transform.position+Vector3.up,1,QueryTriggerInteraction.Ignore))
            {
                ApproachGuard(delta,IsFrostGuard?1.95f:2.4f,dt);
                return;
            }
            if(cooldown>0)return;

            if(delta.sqrMagnitude>.001f)transform.rotation=Quaternion.LookRotation(delta);
            runSkillOrigin=transform.position;runSkillForward=transform.forward;
            attackLanded=false;recoilVelocity=Vector3.zero;
            float duration=RunWindup+(IsFrostGuard?.65f:.74f);
            cooldown=duration+(IsFrostGuard?1.55f:1.30f);
            Begin(FighterAction.Heavy,duration);
            game.Effects.Sound("warning",transform.position,IsFrostGuard?1.24f:.79f);
        }

        void ReleaseRunSkill()
        {
            if(IsFrostGuard)
            {
                var muzzle=runSkillOrigin+Vector3.up*.9f+runSkillForward*.36f;
                game.Run.FrostOrbs.Launch(this,muzzle,runSkillForward);
                return;
            }

            game.Effects.SkillImpact(runSkillOrigin,SlamTint);
            if(!Mirror.NetworkServer.active){HitWithRunSlam(game.CombatTarget);return;}
            foreach(var player in TrialNetworkManager.Current.Players)HitWithRunSlam(player);
        }

        void HitWithRunSlam(TrialPlayer target)
        {
            if(!target || !target.Alive)return;
            Vector3 delta=target.transform.position-runSkillOrigin;delta.y=0;
            if(delta.sqrMagnitude<=SlamRadius*SlamRadius &&
                target.OverlapsAttackHeight(runSkillOrigin.y-.1f,runSkillOrigin.y+.52f) &&
                !Physics.Linecast(runSkillOrigin+Vector3.up*.8f,target.transform.position+Vector3.up*.8f,1,QueryTriggerInteraction.Ignore) &&
                target.ReceiveHazardHit(this,30,34,false,runSkillOrigin-target.transform.position))
                target.ApplyRunSlamStun(1.1f);
        }

        void PresentRunVariant()
        {
            runWarning.enabled=RunSkillActive && ActionTime<RunWindup+(IsFrostGuard?0:.15f);
            if(runWarning.enabled)DrawRunWarning();

            float windup=RunWarningProgress;
            float gait=Alive && Action==FighterAction.Free?MoveBlend:0;
            runGait+=Time.deltaTime*(IsFrostGuard?10:9)*gait;
            float step=Mathf.Sin(runGait)*gait;
            float death=Alive?0:Mathf.SmoothStep(0,1,Progress);
            if(IsFrostGuard)
            {
                float cast=RunSkillActive?(windup<1?windup:1-Mathf.InverseLerp(RunWindup+.15f,ActionDuration,ActionTime)):0;
                Visual.localPosition=runVisualBase+Vector3.up*(Mathf.Abs(step)*.035f);
                Visual.localRotation=Quaternion.Euler(-12*cast+84*death,0,0);
                PoseFrostBone(0,step*15,cast*74,62*(1-cast));
                PoseFrostBone(1,-step*15,-cast*74,-62*(1-cast));
                PoseFrostBone(2,-step*22,0,0);
                PoseFrostBone(3,step*22,0,0);
            }
            else
            {
                float hop=RunSkillActive?Mathf.Sin(Mathf.PI*Mathf.InverseLerp(.26f,1,windup)):0;
                float landing=RunSkillActive && windup>=1?1-Mathf.InverseLerp(RunWindup,RunWindup+.32f,ActionTime):0;
                float squash=(RunSkillActive && windup<.26f?.23f*windup/.26f:0)+landing*.32f+death*.74f;
                Visual.localPosition=Vector3.Scale(runVisualBase,new Vector3(1,1-squash,1))+Vector3.up*(hop*.85f+Mathf.Abs(step)*.12f);
                Visual.localScale=Vector3.Scale(runVisualScale,new Vector3(1+squash*.20f,1-squash,1+squash*.20f));
                Visual.localRotation=Quaternion.Euler(0,0,step*6);
            }
        }

        void PoseFrostBone(int index,float pitch,float yaw,float roll)
        {
            var (bone,bind)=frostBones[index];
            if(!bone)return;
            bone.localRotation=bind;
            bone.rotation=transform.rotation*Quaternion.Euler(pitch,yaw,roll)*Quaternion.Inverse(transform.rotation)*bone.rotation;
        }

        void DrawRunWarning()
        {
            float progress=RunWarningProgress;
            var tint=Color.Lerp(IsFrostGuard?FrostTint:SlamTint,Color.white,progress*progress*.65f);
            tint.a=Mathf.Lerp(.45f,1,progress);
            runWarning.startColor=runWarning.endColor=tint;
            var origin=runSkillOrigin+Vector3.up*.045f;
            if(IsFrostGuard)
            {
                runWarning.SetPosition(0,origin+runSkillForward*.36f);
                runWarning.SetPosition(1,origin+runSkillForward*FrostCastRange);
            }
            else
            {
                for(int i=0;i<runWarning.positionCount;i++)
                {
                    float angle=i*2*Mathf.PI/runWarning.positionCount;
                    runWarning.SetPosition(i,origin+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*SlamRadius);
                }
            }
        }
    }
}
