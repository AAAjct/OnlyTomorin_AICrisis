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
        float currentDistance=4.6f;
        public void Initialize(TrialPlayer target)
        {
            player=target;view=GetComponent<Camera>();view.fieldOfView=58;view.nearClipPlane=.12f;view.farClipPlane=180;
            view.clearFlags=CameraClearFlags.Skybox;view.backgroundColor=new Color(.06f,.10f,.13f);
            var data=view.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var lightObject=new GameObject("Character rim light");lightObject.transform.SetParent(transform,false);lightObject.transform.localPosition=new Vector3(-1,1,1);
            var light=lightObject.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(.73f,.84f,1);light.intensity=2.8f;light.range=8;
            focus=player.transform.position+Vector3.up*1.0f;Snap();
        }
        public Camera View => view;
        public void Look(Vector2 input)
        {
            if(!player.Locked){yaw+=input.x;pitch=Mathf.Clamp(pitch-input.y,-12,60);}
        }
        public void Snap()
        {if(!player)return;yaw=player.transform.eulerAngles.y;focus=player.transform.position+Vector3.up;Place(1);}
        public void Kick(float strength){shake=Mathf.Max(shake,strength);}
        void LateUpdate()
        {
            if(!player)return;
            float dt=Time.deltaTime;
            if(dt<=0)return;
            focus=Vector3.Lerp(focus,player.transform.position+Vector3.up*1.05f,1-Mathf.Exp(-12*dt));
            if(player.Locked)
            {
                Vector3 delta=player.Target.transform.position-player.transform.position;
                yaw=Mathf.LerpAngle(yaw,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,1-Mathf.Exp(-7*dt));
                pitch=Mathf.Lerp(pitch,17,dt*3);
                focus=Vector3.Lerp(focus,(player.transform.position+player.Target.transform.position)*.5f+Vector3.up,Mathf.Clamp01(1-delta.magnitude/15)*.22f);
            }
            Place(dt);
        }
        void Place(float dt)
        {
            Quaternion rotation=Quaternion.Euler(pitch,yaw,0);
            Vector3 boom=rotation*Vector3.back;
            float targetDistance=player.Locked?(player.Target.IsBoss?7.4f:5.2f):player.Sprinting?5.25f:distance;
            currentDistance=Mathf.Lerp(currentDistance,targetDistance,1-Mathf.Exp(-5*dt));
            view.fieldOfView=Mathf.Lerp(view.fieldOfView,player.Sprinting?66:player.Locked && player.Target.IsBoss?64:60,1-Mathf.Exp(-5*dt));
            float desired=currentDistance;
            if(Physics.SphereCast(focus,.18f,boom,out var hit,desired,1,QueryTriggerInteraction.Ignore))desired=Mathf.Max(.5f,hit.distance-.08f);
            Vector3 point=focus+boom*desired+rotation*Vector3.right*.30f;
            shake=Mathf.MoveTowards(shake,0,dt*1.7f);
            transform.position=point+new Vector3(Mathf.Sin(Time.unscaledTime*89),Mathf.Sin(Time.unscaledTime*103),0)*shake*.11f;
            transform.rotation=Quaternion.LookRotation(focus-transform.position);
        }
    }
}
