using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    // Authored motion on Tomorin's actual generic skeleton. IK makes the sword hand independent
    // of the FBX's unusual centimetre scale and bone roll; damage never relies on animation events.
    public sealed class TrialRig : MonoBehaviour
    {
        public Transform RightHand { get; private set; }
        TrialFighter fighter;
        Transform model, spine, head, leftLeg, rightLeg, leftFoot, rightFoot;
        Transform rightArm,rightElbow,leftArm,leftElbow,leftHand,weapon;
        Transform[] bones;
        Quaternion[] bindRotations;
        Quaternion[] previousRotations;
        TrialPlayer player;
        Vector3 modelOrigin;
        Quaternion modelRotation;
        float phase;
        float deathPoseTime;

        public void Initialize(TrialFighter owner,Transform visual)
        {
            fighter=owner;player=owner as TrialPlayer;model=visual;modelOrigin=model.localPosition;modelRotation=model.localRotation;
            var list=new List<Transform>();
            foreach(var t in visual.GetComponentsInChildren<Transform>()) if(t!=visual) list.Add(t);
            bones=list.ToArray();bindRotations=new Quaternion[bones.Length];previousRotations=new Quaternion[bones.Length];
            for(int i=0;i<bones.Length;i++) bindRotations[i]=bones[i].localRotation;
            spine=Find("spine_01_x");head=Find("head_x");
            leftLeg=Find("leftthigh");rightLeg=Find("rightthigh");leftFoot=Find("leftfoot");rightFoot=Find("rightfoot");
            rightArm=Find("arm_stretch_r");rightElbow=Find("forearm_stretch_r");RightHand=Find("hand_r");
            leftArm=Find("arm_stretch_l");leftElbow=Find("forearm_stretch_l");leftHand=Find("hand_l");
        }
        Transform Find(string boneName)
        {
            foreach(var t in bones) if(t.name==boneName) return t;
            return null;
        }
        public void SetWeapon(Transform sword) {weapon=sword;}
        void LateUpdate()
        {
            if(fighter==null) return;
            if(Time.deltaTime<=0 && fighter.Action!=FighterAction.Dead)return;
            phase+=Time.deltaTime*Mathf.Lerp(8.8f,12.8f,Mathf.InverseLerp(.5f,1.6f,fighter.MoveBlend))*Mathf.Clamp01(fighter.MoveBlend*2);
            deathPoseTime=fighter.Action==FighterAction.Dead?deathPoseTime+Time.unscaledDeltaTime:0;
            for(int i=0;i<bones.Length;i++)previousRotations[i]=bones[i].localRotation;
            Pose(fighter.Action,fighter.Action==FighterAction.Dead?Mathf.Clamp01(deathPoseTime/1.3f):fighter.Progress,fighter.Combo,fighter.MoveBlend,fighter.Guarding,phase);
            float blend=1-Mathf.Exp(-28*Time.deltaTime);
            if(fighter.Action!=FighterAction.Dodge && fighter.Action!=FighterAction.Dead)
                for(int i=0;i<bones.Length;i++)bones[i].localRotation=Quaternion.Slerp(previousRotations[i],bones[i].localRotation,blend);
        }

        public void Pose(FighterAction action,float p,int combo,float movement,bool guard,float walkPhase)
        {
            for(int i=0;i<bones.Length;i++) bones[i].localRotation=bindRotations[i];
            model.localPosition=modelOrigin;model.localRotation=modelRotation;
            float gait=Mathf.Sin(walkPhase)*Mathf.Min(1,movement);
            float sprint=Mathf.InverseLerp(.9f,1.6f,movement);
            float breathe=Mathf.Sin(Time.time*2.5f)*.008f;
            Vector3 hand=new Vector3(.30f,.76f,.26f), offhand=new Vector3(-.25f,.77f,.18f);
            Vector3 swordAngles=new Vector3(8,0,-52);
            float twist=0,lean=movement*6,bank=player? -player.TurnLean*9:0;
            if(action==FighterAction.Free)
            {
                float stride=Mathf.Lerp(38,61,sprint);
                model.localPosition+=Vector3.up*(Mathf.Abs(Mathf.Cos(walkPhase))*.046f*Mathf.Min(1,movement)+breathe);
                Leg(leftLeg,leftFoot,gait*stride);Leg(rightLeg,rightFoot,-gait*stride);
                offhand=new Vector3(-.24f,.80f+Mathf.Max(0,-gait)*.08f,.20f-gait*.22f);
                hand=new Vector3(.29f,.78f+Mathf.Max(0,gait)*.06f,.23f+gait*.13f);
                twist=-gait*(5+sprint*5);lean=5*movement+sprint*8;
                swordAngles=new Vector3(8+sprint*14+gait*8,0,-43);
                if(guard) {hand=new Vector3(.16f,.96f,.34f);offhand=new Vector3(-.08f,.91f,.32f);swordAngles=new Vector3(12,0,32);lean=3;}
            }
            if(action==FighterAction.Light || action==FighterAction.Heavy)
            {
                bool heavy=action==FighterAction.Heavy;
                float swing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.23f,.60f,p));
                float recover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.67f,1,p));
                float side=combo==1?-1:1;
                Vector3 windup=heavy?new Vector3(.15f,1.22f,.02f):new Vector3(.40f*side,1.02f,.12f);
                Vector3 finish=heavy?new Vector3(.12f,.58f,.43f):new Vector3(-.32f*side,.74f,.36f);
                hand=Vector3.Lerp(Vector3.Lerp(windup,finish,swing),hand,recover);
                offhand=Vector3.Lerp(new Vector3(-.18f,.89f,.28f),offhand,recover);
                twist=Mathf.Lerp(-36*side,48*side,swing)*(1-recover);
                lean=heavy?Mathf.Lerp(-18,26,swing)*(1-recover):9;
                swordAngles=heavy?new Vector3(Mathf.Lerp(-48,155,swing),0,-8):new Vector3(Mathf.Lerp(15,113,swing),Mathf.Lerp(65*side,-75*side,swing),Mathf.Lerp(-55*side,50*side,swing));
                Leg(leftLeg,leftFoot,-12);Leg(rightLeg,rightFoot,19);
            }
            if(action==FighterAction.Heal)
            {hand=new Vector3(.14f,1.10f,.20f);offhand=new Vector3(-.22f,.75f,.12f);swordAngles=new Vector3(105,20,-30);lean=-7;}
            if(action==FighterAction.Stagger) {lean=-24*Mathf.Sin(p*Mathf.PI);twist=12;hand.z=.13f;}
            if(action==FighterAction.Dodge)
            {
                var roll=Quaternion.Euler(p*360,0,0);
                model.localRotation=modelRotation*roll;
                var pivot=Vector3.up*.65f;
                model.localPosition=modelOrigin+pivot-roll*pivot+Vector3.up*(Mathf.Sin(p*Mathf.PI)*.12f);
                hand=new Vector3(.18f,.65f,.22f);offhand=new Vector3(-.18f,.65f,.22f);
                swordAngles=new Vector3(10,0,-25);
            }
            if(action==FighterAction.Dead)
            {
                model.localRotation=modelRotation*Quaternion.Euler(-Mathf.SmoothStep(0,87,p),0,8*p);
                model.localPosition=modelOrigin+new Vector3(0,.12f*p,0);
                hand=new Vector3(.38f,.48f,.05f);offhand=new Vector3(-.38f,.48f,.05f);
            }
            if(spine) spine.rotation=transform.rotation*Quaternion.Euler(lean,twist,bank)*Quaternion.Inverse(transform.rotation)*spine.rotation;
            if(head && action==FighterAction.Free) head.rotation=Quaternion.AngleAxis(-lean*.5f,transform.right)*head.rotation;
            // The targets follow the rolling visual root, so arms remain tucked during evasion.
            Vector3 basis=transform.position;
            Quaternion orientation=transform.rotation;
            if(action==FighterAction.Dodge || action==FighterAction.Dead)
            {
                orientation=transform.rotation*Quaternion.Inverse(modelRotation)*model.localRotation;
                basis=transform.position+transform.rotation*(model.localPosition-modelOrigin);
            }
            SolveArm(rightArm,rightElbow,RightHand,basis+orientation*hand,basis+orientation*new Vector3(.75f,.86f,-.1f));
            SolveArm(leftArm,leftElbow,leftHand,basis+orientation*offhand,basis+orientation*new Vector3(-.75f,.80f,-.1f));
            if(weapon)
            {
                weapon.position=RightHand?RightHand.position:basis+orientation*hand;
                weapon.rotation=orientation*Quaternion.Euler(swordAngles);
            }
        }

        void Leg(Transform thigh,Transform foot,float angle)
        {
            if(!thigh) return;
            Vector3 axis=transform.right;
            if(player && player.Locked && player.LocalMotion.sqrMagnitude>.04f)
                axis=(transform.right*player.LocalMotion.y-transform.forward*player.LocalMotion.x).normalized;
            thigh.rotation=Quaternion.AngleAxis(angle,axis)*thigh.rotation;
            if(foot) foot.rotation=Quaternion.AngleAxis(-angle*.45f,axis)*foot.rotation;
        }

        static void SolveArm(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 hint)
        {
            if(!upper || !lower || !hand) return;
            float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,hand.position);
            Vector3 delta=target-upper.position;
            float length=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);
            Vector3 forward=delta.normalized;
            Vector3 bend=Vector3.ProjectOnPlane(hint-upper.position,forward).normalized;
            float x=(a*a-b*b+length*length)/(2*length);
            float h=Mathf.Sqrt(Mathf.Max(0,a*a-x*x));
            Vector3 elbow=upper.position+forward*x+bend*h;
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
        }
    }
}
