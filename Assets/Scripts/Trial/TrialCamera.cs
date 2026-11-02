using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TomorinTrial
{
    public sealed class TrialCamera : MonoBehaviour
    {
        TrialPlayer player;
        float yaw,pitch=17,distance=4.6f,shake;
        Vector3 focus;
        Camera view;
        float currentDistance=4.6f, collisionDistance=4.6f;
        float pulse;
        Renderer[] localRenderers;
        public void Pulse(float amount){pulse=Mathf.Max(pulse,amount);}
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
        {if(!player)return;yaw=player.transform.eulerAngles.y;focus=player.transform.position+Vector3.up;Place(1);}
        public void Kick(float strength){shake=Mathf.Max(shake,strength);}
        void LateUpdate()
        {
            if(!player || Frozen)return;
            float dt=Time.deltaTime;
            if(dt<=0)return;
            Vector3 desiredFocus=player.transform.position+Vector3.up*1.05f;
            if(player.Locked)
            {
                Vector3 delta=player.LockTarget.transform.position-player.transform.position;
                delta.y=0;
                // Hold the orbit heading when overlapping; tiny position errors have no stable bearing.
                if(delta.sqrMagnitude>1.44f)yaw=Mathf.LerpAngle(yaw,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,1-Mathf.Exp(-6*dt));
                pitch=Mathf.Lerp(pitch,17,dt*3);
                desiredFocus=Vector3.Lerp(desiredFocus,(player.transform.position+player.LockTarget.transform.position)*.5f+Vector3.up,Mathf.Clamp01(1-delta.magnitude/15)*.22f);
            }
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
