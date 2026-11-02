using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    // Uses DeepSeek's own skeleton; the controller owns movement and hit timing.
    public sealed class TrialBossRig : MonoBehaviour
    {
        TrialEnemy fighter;
        TrialBoss brain;
        Transform model,spine,chest,head,leftArm,rightArm,leftForearm,rightForearm,leftLeg,rightLeg,leftKnee,rightKnee;
        Transform[] joints,tails,hair;
        Quaternion[] bind;
        Vector3 origin;
        Quaternion rotation;
        float clock;
        SkinnedMeshRenderer[] shapes;
        float potWeight;
        public Transform[] DrivenBones => joints;

        public void Initialize(TrialEnemy owner,TrialBoss controller,Transform visual)
        {
            fighter=owner;brain=controller;model=visual;origin=model.localPosition;rotation=model.localRotation;
            shapes=visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            var all=model.GetComponentsInChildren<Transform>();
            spine=Find(all,"spine");chest=Find(all,"chest");head=Find(all,"head");
            leftArm=Find(all,"upper_arm.L");rightArm=Find(all,"upper_arm.R");
            leftForearm=Find(all,"lower_arm.L");rightForearm=Find(all,"lower_arm.R");
            leftLeg=Find(all,"upper_leg.L");rightLeg=Find(all,"upper_leg.R");
            leftKnee=Find(all,"lower_leg.L");rightKnee=Find(all,"lower_leg.R");
            var tailList=new List<Transform>();var hairList=new List<Transform>();
            foreach(var t in all)
            {
                if(t.name.StartsWith("WeiBa") && !t.name.EndsWith("_end"))tailList.Add(t);
                if((t.name.StartsWith("ErDuo") || t.name.StartsWith("DaiMao") || t.name.StartsWith("Hou_hair")) && !t.name.EndsWith("_end"))hairList.Add(t);
            }
            tails=tailList.ToArray();hair=hairList.ToArray();
            var driven=new List<Transform>{spine,chest,head,leftArm,rightArm,leftForearm,rightForearm,leftLeg,rightLeg,leftKnee,rightKnee};
            driven.AddRange(tails);driven.AddRange(hair);driven.RemoveAll(t=>!t);
            joints=driven.ToArray();bind=new Quaternion[joints.Length];
            for(int i=0;i<joints.Length;i++)bind[i]=joints[i].localRotation;
            Pose(DeepFishSkill.None,0,0,0,false);
        }

        static Transform Find(Transform[] all,string label)
        {foreach(var t in all)if(t.name==label)return t;return null;}

        void LateUpdate()
        {
            if(!fighter || fighter.CombatFrozen || Time.deltaTime<=0)return;
            clock+=Time.deltaTime;
            Pose(brain.Skill,brain.SkillProgress,fighter.MoveBlend,clock,brain.Enraged,!fighter.Alive,fighter.Progress,fighter.Vulnerable);
        }

        public void Pose(DeepFishSkill skill,float p,float movement,float time,bool enraged,bool dead=false,float death=0,bool stagger=false)
        {
            potWeight=Mathf.MoveTowards(potWeight,enraged?100:0,Time.unscaledDeltaTime*160);
            foreach(var skin in shapes)for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)
            {
                string shape=skin.sharedMesh.GetBlendShapeName(i);
                if(shape=="TiePen")skin.SetBlendShapeWeight(i,potWeight);
                else if(shape=="angry")skin.SetBlendShapeWeight(i,enraged?65:skill!=DeepFishSkill.None?25:0);
                else if(shape=="blink")skin.SetBlendShapeWeight(i,Mathf.Pow(Mathf.Max(0,Mathf.Sin(time*1.55f)),24)*100);
                else if(shape=="oh")skin.SetBlendShapeWeight(i,skill==DeepFishSkill.Enrage?70:0);
            }
            float turn=0;
            if(skill==DeepFishSkill.ReverseTail)
            {
                turn=p<.43f?0:p<.66f?Mathf.SmoothStep(0,180,Mathf.InverseLerp(.43f,.63f,p)):p<.84f?180:Mathf.SmoothStep(180,0,Mathf.InverseLerp(.84f,1,p));
                skill=DeepFishSkill.TailSweep;
                p=p<.30f?p/.30f*.34f:p<.43f?.34f+(p-.30f)/.13f*.29f:p<.50f?.63f+(p-.43f)/.07f*.37f:p<.66f?(p-.50f)/.16f*.34f:p<.80f?.34f+(p-.66f)/.14f*.29f:.63f+(p-.80f)/.20f*.37f;
            }
            else if(skill==DeepFishSkill.RiptideRush){skill=DeepFishSkill.TidalCharge;p=p<.46f?p/.46f:(p-.46f)/.54f;}
            else if(skill==DeepFishSkill.DelayedSlam){skill=DeepFishSkill.BellySlam;p=p<.42f?p/.42f*.24f:p<.72f?.24f+(p-.42f):.54f+(p-.72f)/.28f*.46f;}
            else if(skill==DeepFishSkill.AbyssRain)skill=DeepFishSkill.Maelstrom;
            for(int i=0;i<joints.Length;i++)joints[i].localRotation=bind[i];
            // FBX bind rotation tilts its local Y axis; yaw in actor space keeps the turn upright.
            model.localPosition=origin;model.localRotation=Quaternion.AngleAxis(turn,Vector3.up)*rotation;
            float gait=Mathf.Sin(time*7.6f),sway=Mathf.Sin(time*2.2f);
            float lean=5,twist=sway*3,arms=68,lift=.035f+.025f*sway,tail=0;
            Rotate(leftLeg,transform.right,gait*24*movement);Rotate(rightLeg,transform.right,-gait*24*movement);
            Rotate(leftKnee,transform.right,Mathf.Max(0,-gait)*34*movement);Rotate(rightKnee,transform.right,Mathf.Max(0,gait)*34*movement);
            if(skill==DeepFishSkill.TailSweep)
            {
                float wind=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.34f,p));
                float strike=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.34f,.63f,p));
                float recover=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.68f,1,p));
                twist=(-48*wind+150*strike)*recover;tail=(-30*wind+95*strike)*recover;arms=46;lean=12;
            }
            if(skill==DeepFishSkill.TidalCharge)
            {float thrust=Mathf.Sin(Mathf.InverseLerp(.27f,.79f,p)*Mathf.PI);lean=15+thrust*35;arms=26;lift-=.08f;tail=22;}
            if(skill==DeepFishSkill.BellySlam)
            {
                float jump=Mathf.Sin(Mathf.InverseLerp(.24f,.54f,p)*Mathf.PI);
                float impact=Mathf.Sin(Mathf.InverseLerp(.54f,.78f,p)*Mathf.PI);
                lift+=jump*3.3f-impact*.10f;lean=-jump*28+impact*42;arms=68-jump*83;
                Rotate(leftLeg,transform.right,-jump*48);Rotate(rightLeg,transform.right,-jump*48);
                Rotate(leftKnee,transform.right,jump*78);Rotate(rightKnee,transform.right,jump*78);
                tail=-jump*28;
            }
            if(skill==DeepFishSkill.BubbleBarrage)
            {arms=15;lean=-12;twist=Mathf.Sin(p*Mathf.PI*6)*7;Rotate(leftForearm,transform.right,-65);Rotate(rightForearm,transform.right,-65);}
            if(skill==DeepFishSkill.Maelstrom || skill==DeepFishSkill.Enrage)
            {arms=-35;lift+=Mathf.Sin(p*Mathf.PI)*.65f;lean=-12;twist=Mathf.Sin(p*Mathf.PI*2)*22;tail=Mathf.Sin(p*Mathf.PI*6)*25;}
            if(stagger){lean=-28;arms=80;lift=-.06f;twist=8;}
            Rotate(leftArm,transform.forward,arms);Rotate(rightArm,transform.forward,-arms);
            Rotate(leftArm,transform.right,-12-gait*movement*10);Rotate(rightArm,transform.right,-12+gait*movement*10);
            Rotate(leftForearm,transform.right,-22);Rotate(rightForearm,transform.right,-22);
            Rotate(spine,transform.right,lean*.65f);Rotate(chest,transform.right,lean*.35f);
            Rotate(chest,transform.up,twist);Rotate(head,transform.right,-lean*.4f);
            for(int i=0;i<tails.Length;i++)
                Rotate(tails[i],transform.up,Mathf.Sin(time*(enraged?5:3.5f)-i*.55f)*(enraged?13:8)+tail/(1+i*.22f));
            for(int i=0;i<hair.Length;i++)Rotate(hair[i],transform.right,Mathf.Sin(time*3.7f-i*.6f)*3.2f);
            model.localPosition+=Vector3.up*lift;
            if(dead)
            {
                float fall=Mathf.SmoothStep(0,1,death);
                model.localRotation=rotation*Quaternion.Euler(-78*fall,0,12*fall);
                model.localPosition+=Vector3.down*(.15f*fall);
            }
        }

        static void Rotate(Transform joint,Vector3 axis,float degrees)
        {if(joint)joint.rotation=Quaternion.AngleAxis(degrees,axis)*joint.rotation;}
    }
}
