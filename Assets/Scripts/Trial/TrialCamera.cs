using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TomorinTrial
{
    public sealed class TrialCamera : MonoBehaviour
    {
        TrialPlayer player;
        float yaw,pitch=17,distance=4.6f,shake;
        Vector3 focus;
        TrialFighter trackedLock;
        Vector3 lockOffset,lockVelocity;
        float yawVelocity;
        bool closeLock;
        Camera view;
        float currentDistance=4.6f, collisionDistance=4.6f;
        float pulse;
        Renderer[] localRenderers;
        TrialFighter framedOpponent;
        float frameUntil,frameStrength;
        public void CombatFrame(TrialFighter opponent,float seconds,float strength)
        {if(InkMainMenu.ReducedMotion || !opponent)return;framedOpponent=opponent;frameUntil=Time.unscaledTime+Mathf.Min(seconds,.9f);frameStrength=Mathf.Clamp01(strength);}
        public void Pulse(float amount){if(InkMainMenu.ReducedMotion)return;pulse=Mathf.Max(pulse,amount);}
        public bool Frozen { get; set; }
        public void Initialize(TrialPlayer target)
        {
            player=target;view=GetComponent<Camera>();view.fieldOfView=58;view.nearClipPlane=.12f;view.farClipPlane=180;
            localRenderers=target.GetComponentsInChildren<Renderer>();
            view.clearFlags=CameraClearFlags.Skybox;view.backgroundColor=new Color(.06f,.10f,.13f);
            var data=view.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var lightObject=new GameObject("Character rim light");lightObject.transform.SetParent(transform,false);lightObject.transform.localPosition=new Vector3(-1,1,1);
            var light=lightObject.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(.73f,.84f,1);light.intensity=2.8f;light.range=8;
            focus=player.transform.position+Vector3.up*1.0f;Snap();
        }
        public Camera View => view;
        public void Look(Vector2 input)
        {
            if(!Frozen && !player.Locked){yaw+=input.x;pitch=Mathf.Clamp(pitch-input.y,-12,60);}
        }
        public void Snap()
        {if(!player)return;trackedLock=null;lockVelocity=Vector3.zero;yawVelocity=0;closeLock=false;yaw=player.transform.eulerAngles.y;focus=player.transform.position+Vector3.up;Place(1);}
        public void Kick(float strength){if(InkMainMenu.ReducedMotion)return;shake=Mathf.Max(shake,strength);}
        void LateUpdate()
        {
            if(!player || Frozen)return;
            float dt=Time.deltaTime;
            if(dt<=0)return;
            Vector3 desiredFocus=player.transform.position+Vector3.up*1.05f;
            if(framedOpponent && Time.unscaledTime<frameUntil)
            {
                // Brief framing preserves heading and camera collision; it never takes input away.
                float cue=Mathf.Sin(Mathf.Clamp01((frameUntil-Time.unscaledTime)/.9f)*Mathf.PI)*frameStrength;
                Vector3 framedOffset=framedOpponent.transform.position-player.transform.position;
                framedOffset.y=Mathf.Clamp(framedOffset.y,-.4f,.6f);
                desiredFocus=Vector3.Lerp(desiredFocus,player.transform.position+framedOffset*.5f+Vector3.up*1.2f,.3f*cue);
                pulse=Mathf.Max(pulse,cue*4);
            }
            if(player.Locked)
            {
                Vector3 rawOffset=player.LockTarget.transform.position-player.transform.position;
                // Follow the fight on the ground; an aerial animation must not lift the camera boom.
                rawOffset.y=Mathf.Clamp(rawOffset.y,-.4f,.6f);
                if(trackedLock!=player.LockTarget)
                {trackedLock=player.LockTarget;lockOffset=rawOffset;lockVelocity=Vector3.zero;yawVelocity=0;closeLock=false;}
                lockOffset=Vector3.SmoothDamp(lockOffset,rawOffset,ref lockVelocity,.16f,45,dt);
                Vector3 delta=Vector3.ProjectOnPlane(lockOffset,Vector3.up);
                float rawDistance=Vector3.ProjectOnPlane(rawOffset,Vector3.up).magnitude;
                // Hysteresis avoids alternating bearings when a dash passes through the player.
                closeLock=rawDistance<(closeLock?2.6f:1.8f);
                if(!closeLock && delta.sqrMagnitude>3.24f)
                {
                    float bearing=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
                    if(Mathf.Abs(Mathf.DeltaAngle(yaw,bearing))>.8f)
                    {
                        float next=Mathf.SmoothDampAngle(yaw,bearing,ref yawVelocity,.24f,110,dt);
                        yaw=Mathf.MoveTowardsAngle(yaw,next,110*dt);
                    }
                    else yawVelocity=0;
                }
                else yawVelocity=0;
                pitch=Mathf.Lerp(pitch,17,1-Mathf.Exp(-3*dt));
                desiredFocus+=lockOffset*.11f*Mathf.Clamp01(1-delta.magnitude/15);
            }
            else {trackedLock=null;lockVelocity=Vector3.zero;yawVelocity=0;closeLock=false;}
            focus=Vector3.Lerp(focus,desiredFocus,1-Mathf.Exp(-12*dt));
            Place(dt);
        }
        void Place(float dt)
        {
            Quaternion rotation=Quaternion.Euler(pitch,yaw,0);
            Vector3 boom=rotation*Vector3.back;
            bool bossLocked=player.LockTarget is TrialEnemy enemy && enemy.IsBoss;
            float targetDistance=player.Locked?(bossLocked?7.4f:5.2f):player.Sprinting?5.25f:distance;
            currentDistance=Mathf.Lerp(currentDistance,targetDistance,1-Mathf.Exp(-5*dt));
            pulse=Mathf.MoveTowards(pulse,0,Time.unscaledDeltaTime*24);
            view.fieldOfView=Mathf.Lerp(view.fieldOfView,(player.Sprinting?66:player.Locked && bossLocked?64:60)+pulse,1-Mathf.Exp(-12*Time.unscaledDeltaTime));
            float desired=currentDistance;
            if(Physics.SphereCast(focus,.18f,boom,out var hit,desired,1,QueryTriggerInteraction.Ignore))desired=Mathf.Max(.5f,hit.distance-.08f);
            collisionDistance=desired<collisionDistance?desired:Mathf.Lerp(collisionDistance,desired,1-Mathf.Exp(-7*dt));
            Vector3 point=focus+boom*collisionDistance+rotation*Vector3.right*.30f;
            var sight=point-focus;
            if(Physics.SphereCast(focus,.23f,sight.normalized,out var shoulderHit,sight.magnitude,1,QueryTriggerInteraction.Ignore))point=focus+sight.normalized*Mathf.Max(.15f,shoulderHit.distance-.07f);
            shake=Mathf.MoveTowards(shake,0,dt*1.7f);
            transform.position=point+new Vector3(Mathf.Sin(Time.unscaledTime*89),Mathf.Sin(Time.unscaledTime*103),0)*shake*.11f;
            transform.rotation=Quaternion.LookRotation(focus-transform.position);
            bool hidden=(point-(player.transform.position+Vector3.up*1.1f)).sqrMagnitude<.81f;
            if(localRenderers!=null)foreach(var renderer in localRenderers)if(renderer)renderer.forceRenderingOff=hidden;
        }
        void OnDestroy(){if(localRenderers!=null)foreach(var renderer in localRenderers)if(renderer)renderer.forceRenderingOff=false;}
    }
}
