using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialRig
    {
        public const float SheatheDuration = .82f;
        public const float IdleCycleDuration = 4f;
        const float DrawDuration = .11f;
        const float CombatReadyTime = .55f;
        const float MouthOnBlade = .143f;
        const float TipOnBlade = 1.17f;
        const float Aligned = .36f;
        const float Seated = .79f;

        Transform scabbard;
        float sheathProgress = 1, combatReadyRemaining, idlePhase, sheathFocus;
        bool swordGuidedByScabbard, drivingScabbardGrip;
        Vector3 guidedSwordPosition;

        public void SetScabbard(Transform value) => scabbard = value;

        internal void ResetSwordPresentation()
        {
            sheathProgress = 1;
            combatReadyRemaining = phase = idlePhase = 0;
            hasRenderedPose = renderingTransition = false;
            hasBladeSample=false;
            hatTilt=hatVelocity=Vector2.zero;
            if(hat)hat.localRotation=hatRest;
            Pose(FighterAction.Free,0,0,0,false,0);
        }

        void TickScabbard(float dt)
        {
            var action = fighter.Action;
            bool needsBlade = fighter.Guarding || player.AirVolleyDescending ||
                action == FighterAction.Light || action == FighterAction.Heavy || action == FighterAction.Charge ||
                action == FighterAction.Dodge && fighter.Combo == 7;
            if(needsBlade)
            {
                combatReadyRemaining = CombatReadyTime;
                sheathProgress = Mathf.MoveTowards(sheathProgress,0,dt/DrawDuration);
            }
            else if(action == FighterAction.Free)
            {
                combatReadyRemaining = Mathf.Max(0,combatReadyRemaining-dt);
                if(combatReadyRemaining == 0 && player.CanAutoSheathe && weapon && weapon.gameObject.activeSelf)
                    sheathProgress = Mathf.MoveTowards(sheathProgress,1,dt/SheatheDuration);
            }
            else if(sheathProgress > 0 && sheathProgress < 1)
            {
                // An interrupted return brings the blade back into the hand.
                if(action == FighterAction.Dead)
                    sheathProgress = sheathProgress >= Seated ? 1 : 0;
                else
                    sheathProgress = Mathf.MoveTowards(sheathProgress,0,dt/DrawDuration);
            }
        }

        void SwordLocomotion(float movement,float walkPhase,float run,ref Vector3 hand,ref Vector3 offhand,
            ref Vector3 swordAngles,ref float lean,ref float twist,ref float bank)
        {
            float travel = Mathf.Clamp01(movement/1.08f);
            float step = Mathf.Sin(walkPhase), lift = Mathf.Cos(walkPhase);
            float relaxed = Ease(Seated,1,sheathProgress);
            float stride = Mathf.Lerp(34,56,run) * travel;
            GaitLeg(leftLeg,leftFoot,-step,lift,stride,travel,run);
            GaitLeg(rightLeg,rightFoot,step,-lift,stride,travel,run);

            float stanceDrop = (1-Mathf.Cos(step*stride*Mathf.Deg2Rad))*.27f;
            model.localPosition += new Vector3(step*Mathf.Lerp(.010f,.006f,run)*travel,
                -stanceDrop + lift*lift*.026f*run,0);
            lean = Mathf.Lerp(4,1,relaxed) + travel*Mathf.Lerp(3,17,run);
            twist = Mathf.Lerp(-9,-2,relaxed)*(1-travel) - step*travel*Mathf.Lerp(4,5,run);
            bank += lift*travel*Mathf.Lerp(2,1,run);

            // Keep the arms at the sides and lead with the arm opposite the forward foot.
            float drive = step*travel;
            float rightLift = Mathf.Max(0,drive), leftLift = Mathf.Max(0,-drive);
            float movingArms = Ease(.03f,.30f,movement);
            float side = Mathf.Lerp(.37f,.39f,run);
            float height = Mathf.Lerp(.58f,.65f,run);
            float liftAmount = Mathf.Lerp(.045f,.15f,run);
            float swing = Mathf.Lerp(.125f,.25f,run);
            var carrySword = new Vector3(Mathf.Lerp(.45f,.49f,run),
                Mathf.Lerp(.70f,.65f,run)+rightLift*Mathf.Lerp(.020f,.035f,run),
                Mathf.Lerp(.15f,-.05f,run)+drive*Mathf.Lerp(.035f,.09f,run));
            var swingRight = new Vector3(side,height+rightLift*liftAmount,.09f+drive*swing);
            var swingLeft = new Vector3(-side,height+leftLift*liftAmount,.09f-drive*swing);
            var idleRight = Vector3.Lerp(new Vector3(.44f,.73f,.24f),new Vector3(.37f,.54f,.06f),relaxed);
            var idleLeft = Vector3.Lerp(new Vector3(-.32f,.73f,.14f),new Vector3(-.37f,.55f,.07f),relaxed);
            hand = Vector3.Lerp(idleRight,Vector3.Lerp(carrySword,swingRight,relaxed),movingArms);
            offhand = Vector3.Lerp(idleLeft,swingLeft,movingArms);
            swordAngles = new Vector3(-110-run*5+drive*2,0,-24-run*4);
        }

        Vector3 PlayerIdle(float weight,ref Vector3 hand,ref Vector3 offhand,ref Vector3 swordAngles,
            ref float lean,ref float twist,ref float bank)
        {
            float relaxed = scabbard ? Ease(Seated,1,sheathProgress) : 0;
            float breath = (1-Mathf.Cos(idlePhase))*.5f;
            float shift = Mathf.Sin(idlePhase);
            float cycle = idlePhase/(2*Mathf.PI);
            float glance = Ease(.22f,.43f,cycle)*(1-Ease(.57f,.86f,cycle))*relaxed;

            // The spine moves above the planted legs. The head leads the turn and settles back.
            lean -= breath*Mathf.Lerp(1.4f,2.4f,relaxed)*weight;
            twist += (shift*.85f + glance*2.2f)*weight;
            bank += shift*Mathf.Lerp(1.1f,3.6f,relaxed)*weight;

            float ready = (1-relaxed)*weight;
            hand += new Vector3(shift*.012f,breath*.022f,-breath*.014f)*ready;
            offhand += new Vector3(shift*.009f,breath*.013f,breath*.018f)*ready;
            swordAngles += new Vector3(breath*3,0,-shift*2)*ready;

            return new Vector3(-glance*3.3f,glance*8f-shift*.9f,-shift*Mathf.Lerp(.5f,2.0f,relaxed))*weight;
        }

        static Vector3 LocomotionElbowHint(Transform shoulder,Vector3 wrist,Vector3 lateral)
        {
            Vector3 bend = Vector3.Cross(lateral,wrist-shoulder.position).normalized;
            return shoulder.position+bend*.45f;
        }

        Transform[] relaxedFlippers;
        Quaternion[] relaxedFlipperRotations;

        void CacheRelaxedFlippers()
        {
            relaxedFlippers=new[]{Find("shoulder_r"),rightArm,rightElbow,RightHand,
                Find("shoulder_l"),leftArm,leftElbow,leftHand};
            relaxedFlipperRotations=new Quaternion[relaxedFlippers.Length];
            float[] bend={18,41,7,3};
            for(int i=0;i<relaxedFlippers.Length;i++)
            {
                var bone=relaxedFlippers[i];
                if(!bone)continue;
                int joint=i%bend.Length;
                float side=Mathf.Sign(model.InverseTransformPoint(bone.position).x);
                Quaternion frame=Quaternion.Inverse(bone.rotation)*model.rotation;
                var turn=Quaternion.AngleAxis(joint==0?side*4:0,Vector3.up)
                    *Quaternion.AngleAxis(-side*bend[joint],Vector3.forward);
                relaxedFlipperRotations[i]=bone.localRotation*frame*turn*Quaternion.Inverse(frame);
            }
        }

        void RelaxFlippers(float amount)
        {
            if(relaxedFlippers==null || amount<=0)return;
            for(int i=0;i<relaxedFlippers.Length;i++)
            {
                var bone=relaxedFlippers[i];
                if(bone)bone.localRotation=Quaternion.Slerp(bone.localRotation,relaxedFlipperRotations[i],amount);
            }
        }

        void GaitLeg(Transform thigh,Transform foot,float step,float lift,float stride,float travel,float run)
        {
            if(!thigh || !foot) return;
            Vector3 axis = GaitAxis();
            // Tomorin's chibi rig has a thigh and an ankle, with no knee joint.
            float swing = Mathf.Max(0,lift)*travel;
            float angle = step*stride-swing*Mathf.Lerp(9,18,run);
            thigh.rotation = Quaternion.AngleAxis(angle,axis)*thigh.rotation;
            foot.rotation = Quaternion.AngleAxis(-angle+swing*27-Mathf.Max(0,-lift)*travel*8,axis)*foot.rotation;
        }

        Vector3 GaitAxis()
        {
            if(player && player.Locked && player.LocalMotion.sqrMagnitude>.04f)
                return (transform.right*player.LocalMotion.y-transform.forward*player.LocalMotion.x).normalized;
            return transform.right;
        }

        static float Ease(float from,float to,float value) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,value));

        void BlendScabbardPose(FighterAction action,ref float lean,ref float twist,ref float bank)
        {
            sheathFocus = Ease(0,Aligned,sheathProgress)*(1-Ease(Seated,1,sheathProgress));
            drivingScabbardGrip = sheathProgress > 0 && sheathProgress < 1;
            lean = Mathf.Lerp(lean,7,sheathFocus);
            twist = Mathf.Lerp(twist,Mathf.Lerp(-44,-54,Ease(Aligned,Seated,sheathProgress)),sheathFocus);
            if(action == FighterAction.Free)bank = Mathf.Lerp(bank,-4,sheathFocus);
        }

        void PlaceScabbard(Quaternion orientation,out Vector3 swordGrip,out Vector3 sheathGrip)
        {
            float prepare = Ease(0,Aligned,sheathProgress);
            float slide = Ease(Aligned,Seated,sheathProgress);
            float settle = Ease(Seated,1,sheathProgress);
            Vector3 restMouth = new Vector3(-.32f,.78f,.13f);
            Vector3 restAxis = new Vector3(-.13f,-.36f,-.92f);
            Vector3 mouth = Vector3.Lerp(restMouth,new Vector3(-.55f,.86f,-.49f),prepare);
            Vector3 axis = Vector3.Lerp(restAxis,new Vector3(-.45f,-.12f,-.885f),prepare);
            mouth = Vector3.Lerp(mouth,new Vector3(-.28f,.84f,.18f),slide);
            axis = Vector3.Lerp(axis,new Vector3(-.20f,-.25f,-.95f),slide);
            mouth = Vector3.Lerp(mouth,restMouth,settle);
            axis = Vector3.Lerp(axis,restAxis,settle).normalized;

            Vector3 root = transform.position + transform.rotation*(model.localPosition-modelOrigin);
            scabbard.rotation = orientation*Quaternion.LookRotation(Vector3.Cross(Vector3.right,axis),axis);
            scabbard.position = root + orientation*(mouth-axis*MouthOnBlade);
            swordGrip = scabbard.position - scabbard.up*((TipOnBlade-MouthOnBlade)*(1-slide));
            sheathGrip = scabbard.position + scabbard.up*(MouthOnBlade+.055f) - orientation*Vector3.right*.025f;
            swordGuidedByScabbard = sheathProgress >= Aligned;
        }

#if UNITY_EDITOR
        public Transform[] PreviewLocomotionTargets() => new[]
        {
            transform, model, spine, head, leftLeg, leftFoot, rightLeg, rightFoot,
            rightArm, rightElbow, RightHand, leftArm, leftElbow, leftHand, weapon, scabbard
        };
#endif

        public void PreviewSwordLocomotion(float movement,float cycle,float stow)
        {
            idlePhase = cycle*2*Mathf.PI;
            sheathProgress = stow;
            Pose(FighterAction.Free,0,0,movement,false,cycle*2*Mathf.PI);
        }
    }
}
