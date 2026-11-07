using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    // All hand targets use actor metres and the actual weighted NaiWa arm chain.
    public sealed class TrialNaiWaRig : MonoBehaviour
    {
        TrialEnemy owner;
        Transform visual,spine,leftArm,rightArm,leftForearm,rightForearm,leftHand,rightHand,leftThigh,rightThigh,leftShin,rightShin;
        Transform[] bones;Quaternion[] bind;
        Renderer[] meshes;Material[][] originals;
        readonly List<Material> fadingMaterials=new List<Material>();
        Vector3 origin,leftTarget,rightTarget;Quaternion rotation;
        float gait,lastEmber;bool wasDead,hidden;
        public float CorpseOpacity { get; private set; }=1;
        public void Initialize(TrialEnemy fighter,Transform model)
        {
            owner=fighter;visual=model;origin=model.localPosition;rotation=model.localRotation;
            bones=model.GetComponentsInChildren<Transform>();bind=new Quaternion[bones.Length];
            for(int i=0;i<bones.Length;i++)bind[i]=bones[i].localRotation;
            spine=Find("spine.002");leftArm=Find("upper_arm.L");rightArm=Find("upper_arm.R");
            leftHand=Find("hand.L");rightHand=Find("hand.R");leftForearm=Find("forearm.L");rightForearm=Find("forearm.R");
            leftThigh=Find("thigh.L");rightThigh=Find("thigh.R");leftShin=Find("shin.L");rightShin=Find("shin.R");
            meshes=model.GetComponentsInChildren<Renderer>();originals=new Material[meshes.Length][];
            for(int i=0;i<meshes.Length;i++)originals[i]=meshes[i].sharedMaterials;
            leftTarget=new Vector3(-.34f,.73f,.04f);rightTarget=new Vector3(.34f,.73f,.04f);
        }
        Transform Find(string label){foreach(var bone in bones)if(bone.name==label)return bone;return null;}
        void Rotate(Transform bone,float x,float y,float z){if(bone)bone.rotation=Quaternion.AngleAxis(x,transform.right)*Quaternion.AngleAxis(y,transform.up)*Quaternion.AngleAxis(z,transform.forward)*bone.rotation;}
        public void ResetPresentation()
        {
            for(int i=0;i<meshes.Length;i++){meshes[i].sharedMaterials=originals[i];meshes[i].enabled=true;meshes[i].shadowCastingMode=ShadowCastingMode.On;meshes[i].SetPropertyBlock(null);}
            foreach(var m in fadingMaterials)if(m)Destroy(m);fadingMaterials.Clear();
            wasDead=hidden=false;CorpseOpacity=1;gait=lastEmber=0;visual.localPosition=origin;visual.localRotation=rotation;
            leftTarget=new Vector3(-.34f,.73f,.04f);rightTarget=new Vector3(.34f,.73f,.04f);
        }
        void LateUpdate()
        {
            if(!owner || owner.CombatFrozen || Time.deltaTime<=0)return;
            if(owner.Alive && wasDead)ResetPresentation();
            if(hidden)return;
            for(int i=0;i<bones.Length;i++)bones[i].localRotation=bind[i];
            visual.localPosition=origin;visual.localRotation=rotation;
            if(!owner.Alive){PoseCorpse();return;}
            float movement=owner.Action==FighterAction.Free?owner.MoveBlend:0;
            gait+=Time.deltaTime*8*movement;
            visual.localPosition+=Vector3.up*(Mathf.Abs(Mathf.Sin(gait))*.025f*movement);
            Rotate(leftThigh,Mathf.Sin(gait)*25*movement,0,0);Rotate(rightThigh,-Mathf.Sin(gait)*25*movement,0,0);
            Rotate(leftShin,Mathf.Max(0,-Mathf.Sin(gait))*28*movement,0,0);Rotate(rightShin,Mathf.Max(0,Mathf.Sin(gait))*28*movement,0,0);
            Vector3 left=new Vector3(-.34f,.73f,.04f-Mathf.Sin(gait)*.12f*movement);
            Vector3 right=new Vector3(.34f,.73f,.04f+Mathf.Sin(gait)*.12f*movement);
            bool attacking=owner.Action==FighterAction.Light || owner.Action==FighterAction.Heavy;
            if(owner.Awakened && movement<.1f){left=new Vector3(-.28f,.90f,.15f);right=new Vector3(.28f,.88f,.17f);}
            if(attacking)
            {
                bool useLeft=owner.Combo==1;float side=useLeft?-1:1,p=owner.Progress;
                Vector3 ready=new Vector3(side*.28f,.91f,.17f),windup=new Vector3(side*.43f,.98f,-.10f),contact=new Vector3(side*.08f,1.01f,.34f);
                Vector3 swing=p<.25f?Vector3.Lerp(ready,windup,Mathf.SmoothStep(0,1,p/.25f)):
                    p<.52f?Vector3.Lerp(windup,contact,Mathf.SmoothStep(0,1,(p-.25f)/.27f)):
                    Vector3.Lerp(contact,ready,Mathf.SmoothStep(0,1,(p-.60f)/.40f));
                left=new Vector3(-.28f,.94f,.16f);right=new Vector3(.28f,.94f,.16f);
                if(useLeft)left=swing;else right=swing;
                float twist=Mathf.Lerp(-22,30,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.22f,.57f,p)))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,p)));
                Rotate(spine,5,side*twist,0);
            }
            if(owner.Action==FighterAction.Stagger)Rotate(spine,-22*Mathf.Sin(owner.Progress*Mathf.PI),0,6);
            float blend=1-Mathf.Exp(-22*Time.deltaTime);leftTarget=Vector3.Lerp(leftTarget,left,blend);rightTarget=Vector3.Lerp(rightTarget,right,blend);
            TrialRig.SolveArm(leftArm,leftForearm,leftHand,transform.TransformPoint(leftTarget),transform.TransformPoint(new Vector3(-.57f,.76f,-.13f)));
            TrialRig.SolveArm(rightArm,rightForearm,rightHand,transform.TransformPoint(rightTarget),transform.TransformPoint(new Vector3(.57f,.76f,-.13f)));
        }
        void PoseCorpse()
        {
            wasDead=true;float age=owner.ActionTime;
            float fall=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.06f,.72f,age));
            // Death has its own fixed limbs: gait never participates in this pose.
            Rotate(leftThigh,-14*fall,0,-8);Rotate(rightThigh,18*fall,0,6);
            Rotate(leftShin,26*fall,0,0);Rotate(rightShin,10*fall,0,0);
            TrialRig.SolveArm(leftArm,leftForearm,leftHand,transform.TransformPoint(new Vector3(-.38f,.61f,.10f)),transform.TransformPoint(new Vector3(-.6f,.7f,-.1f)));
            TrialRig.SolveArm(rightArm,rightForearm,rightHand,transform.TransformPoint(new Vector3(.38f,.64f,.12f)),transform.TransformPoint(new Vector3(.6f,.7f,-.1f)));
            visual.localRotation=rotation*Quaternion.Euler(-87*fall,0,12*fall);
            float fade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.2f,4.4f,age));CorpseOpacity=1-fade;
            visual.localPosition=origin+transform.InverseTransformDirection(owner.Recoil)*(.18f*fall)+new Vector3(0,.12f*Mathf.Sin(fall*Mathf.PI)-fade*.20f,0);
            if(fade>0 && fadingMaterials.Count==0)BeginFade();
            foreach(var m in fadingMaterials){var color=m.GetColor("_BaseColor");color.a=CorpseOpacity;m.SetColor("_BaseColor",color);}
            if(fade>0 && age-lastEmber>.15f && age<4.2f)
            {lastEmber=age;var game=owner.GetComponentInParent<TrialGame>();game.Effects.Burst(transform.position+transform.forward*-.6f+Vector3.up*.2f,new Color(.52f,.85f,.64f,CorpseOpacity),2);}
            if(fade>=1){foreach(var mesh in meshes)mesh.enabled=false;hidden=true;}
        }
        void BeginFade()
        {
            foreach(var mesh in meshes)
            {
                var materials=mesh.materials;
                foreach(var m in materials)
                {
                    m.SetFloat("_Surface",1);m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;
                    m.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);m.SetInt("_ZWrite",0);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.DisableKeyword("_ALPHAPREMULTIPLY_ON");m.SetShaderPassEnabled("ShadowCaster",false);fadingMaterials.Add(m);
                }
                mesh.shadowCastingMode=ShadowCastingMode.Off;mesh.SetPropertyBlock(null);
            }
        }
        void OnDestroy(){foreach(var m in fadingMaterials)if(m)Destroy(m);}
    }
}
