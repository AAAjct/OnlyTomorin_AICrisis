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
        Transform flask;
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
        public void SetFlask(Transform value){flask=value;}
        void LateUpdate()
        {
            if(fighter==null || fighter.CombatFrozen) return;
            if(Time.deltaTime<=0 && fighter.Action!=FighterAction.Dead)return;
            phase+=Time.deltaTime*Mathf.Lerp(8.8f,12.8f,Mathf.InverseLerp(.5f,1.6f,fighter.MoveBlend))*Mathf.Clamp01(fighter.MoveBlend*2);
            deathPoseTime=fighter.Action==FighterAction.Dead?deathPoseTime+Time.unscaledDeltaTime:0;
            for(int i=0;i<bones.Length;i++)previousRotations[i]=bones[i].localRotation;
            if(fighter is TrialEnemy enemy && enemy.Champion && enemy.Champion.IsRemnant && enemy.Alive && !enemy.Vulnerable)
            {
                var brain=enemy.Champion;var shape=brain.Move.shape;
                int cut=shape==ChampionShape.Thrust?11:shape==ChampionShape.BladeWave?12:shape==ChampionShape.Stomp?4:shape==ChampionShape.Spiral?2:brain.MoveIndex%2;
                Pose(enemy.Flinching?FighterAction.Stagger:brain.Attacking && !brain.CounterReady?FighterAction.Light:FighterAction.Free,brain.AnimationBeat,cut,fighter.MoveBlend,fighter.Guarding || brain.CounterReady,phase);
            }
            else Pose(fighter.Action,fighter.Action==FighterAction.Dead?Mathf.Clamp01(deathPoseTime/1.3f):fighter.Progress,fighter.Combo,fighter.MoveBlend,fighter.Guarding,phase);
            float blend=1-Mathf.Exp(-((fighter.Action==FighterAction.Light || fighter.Action==FighterAction.Heavy) && fighter.Progress<.55f?48:28)*Time.deltaTime);
            if(fighter.Action!=FighterAction.Dodge && fighter.Action!=FighterAction.Dead)
                for(int i=0;i<bones.Length;i++)bones[i].localRotation=Quaternion.Slerp(previousRotations[i],bones[i].localRotation,blend);
            if(flask && leftHand)flask.position=leftHand.position;
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
            Quaternion? swordRotation=null;
            float twist=0,lean=movement*6,bank=player? -player.TurnLean*9:0;
            if(action==FighterAction.Free || action==FighterAction.Heal)
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
                float swing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(heavy?.20f:.10f,heavy?.58f:.51f,p));
                float recover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(heavy?.69f:.55f,1,p));
                if(combo==TrialPlayer.ParryTriple){float beat=Mathf.Repeat(Mathf.Max(0,p-.08f),.22f)/.22f;swing=Mathf.Sin(Mathf.Clamp01(beat)*Mathf.PI);recover=0;}
                if(combo==TrialPlayer.DodgeDragon){swing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.64f,.82f,p));recover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.86f,1,p));}
                Vector3 windup,finish;
                Vector3 startAngles,endAngles;
                switch(combo)
                {
                    case 0 when !heavy: // Front-facing rising cut: low left to high right.
                        windup=new Vector3(-.43f,.64f,.40f);finish=new Vector3(.46f,1.24f,.56f);
                        startAngles=Quaternion.FromToRotation(Vector3.up,new Vector3(-.65f,-.3f,1)).eulerAngles;
                        endAngles=Quaternion.FromToRotation(Vector3.up,new Vector3(.7f,.6f,1)).eulerAngles;break;
                    case TrialPlayer.ParryTriple:
                    case TrialPlayer.BehindThrust:
                        windup=new Vector3(.20f,1.0f,.15f);finish=new Vector3(.05f,1.08f,.92f);
                        startAngles=endAngles=new Vector3(90,0,0);break;
                    case TrialPlayer.FireDragon:
                        windup=new Vector3(-.38f,.85f,.26f);finish=new Vector3(.42f,1.12f,.60f);
                        startAngles=new Vector3(80,-95,12);endAngles=new Vector3(90,80,-15);break;
                    case TrialPlayer.DodgeDragon:
                        windup=new Vector3(.10f,1.48f,-.04f);finish=new Vector3(.12f,.48f,.70f);
                        startAngles=new Vector3(-65,0,0);endAngles=new Vector3(150,0,0);break;
                    case TrialPlayer.SpiralSword:
                        windup=new Vector3(.38f,1.32f,.12f);finish=new Vector3(.10f,1.05f,.95f);
                        startAngles=new Vector3(-30,25,10);endAngles=new Vector3(90,0,0);break;
                    case 20: // Sword points straight down throughout the planted aerial plunge.
                    case TrialPlayer.AirSandDive:
                        windup=new Vector3(.08f,1.24f,.28f);finish=new Vector3(.08f,.42f,.30f);
                        startAngles=endAngles=new Vector3(180,0,0);break;
                    case TrialPlayer.AirBladeFirst:
                    case TrialPlayer.AirBladeThird:
                        windup=new Vector3(.48f,1.35f,.08f);finish=new Vector3(-.45f,.78f,.58f);
                        startAngles=new Vector3(10,70,-55);endAngles=new Vector3(120,-75,45);break;
                    case TrialPlayer.AirBladeSecond:
                        windup=new Vector3(-.45f,.78f,.58f);finish=new Vector3(.48f,1.35f,.30f);
                        startAngles=new Vector3(120,-75,45);endAngles=new Vector3(10,70,-55);break;
                    case TrialPlayer.SakuraFollowup:
                        windup=new Vector3(.20f,.94f,.04f);finish=new Vector3(.06f,1.04f,.96f);
                        startAngles=new Vector3(85,0,0);endAngles=new Vector3(90,0,0);break;
                    case 21: // Rising heavy into a reverse waist-height sweep.
                        windup=new Vector3(-.42f,1.32f,.15f);finish=new Vector3(.58f,.82f,.58f);
                        startAngles=new Vector3(75,-110,25);endAngles=new Vector3(95,100,-30);break;
                    case 22: // Retract a thrust and draw the edge across at close range.
                        windup=new Vector3(.08f,1.08f,.78f);finish=new Vector3(-.44f,.94f,.28f);
                        startAngles=new Vector3(90,0,0);endAngles=new Vector3(85,-95,30);break;
                    case 23: // Recover a deep cleave with a stepping-back rising draw.
                        windup=new Vector3(.32f,.48f,.66f);finish=new Vector3(-.36f,1.22f,.36f);
                        startAngles=new Vector3(150,55,-20);endAngles=new Vector3(-35,-60,35);break;
                    case 24: // Spinning dodge cut into a broad planted reverse cleave.
                        windup=new Vector3(-.50f,1.18f,-.06f);finish=new Vector3(.57f,.65f,.56f);
                        startAngles=new Vector3(65,-120,20);endAngles=new Vector3(125,100,-35);break;
                    case 25: // Rolling pursuit thrust into a two-handed driving thrust.
                        windup=new Vector3(.18f,.98f,.10f);finish=new Vector3(.04f,1.08f,.96f);
                        startAngles=new Vector3(85,0,0);endAngles=new Vector3(90,0,0);break;
                    case 18: // Heavy chain two: reverse rising cut.
                        windup=new Vector3(.12f,.48f,.58f);finish=new Vector3(-.28f,1.40f,.48f);
                        startAngles=new Vector3(145,35,-15);endAngles=new Vector3(-55,-55,20);break;
                    case 19: // Heavy chain three: gathered two-handed diagonal finish.
                        windup=new Vector3(-.28f,1.40f,.48f);finish=new Vector3(.35f,.50f,.66f);
                        startAngles=new Vector3(-55,-55,20);endAngles=new Vector3(145,65,-25);break;
                    case 15: // Cut through: tight rising interception into forward cleave.
                        windup=new Vector3(-.28f,.78f,.16f);finish=new Vector3(.30f,1.25f,.64f);
                        startAngles=new Vector3(120,-45,30);endAngles=new Vector3(-35,40,-20);break;
                    case 16: // Heavy-to-light: reverse draw across the torso.
                        windup=new Vector3(.12f,.48f,.58f);finish=new Vector3(-.40f,1.12f,.45f);
                        startAngles=new Vector3(145,30,-20);endAngles=new Vector3(25,-65,45);break;
                    case 17: // Charged airborne cleave.
                        windup=new Vector3(.08f,1.63f,-.08f);finish=new Vector3(.10f,.46f,.68f);
                        startAngles=new Vector3(-65,0,0);endAngles=new Vector3(155,0,0);break;
                    case 11: // Guarded straight counterthrust.
                        windup=new Vector3(.12f,1.20f,.16f);finish=new Vector3(.08f,1.08f,.82f);
                        startAngles=new Vector3(45,0,30);endAngles=new Vector3(90,0,0);break;
                    case 12: // Running draw with a broad hip turn.
                        windup=new Vector3(-.42f,.66f,-.1f);finish=new Vector3(.58f,1.06f,.65f);
                        startAngles=new Vector3(80,-105,0);endAngles=new Vector3(90,110,-10);break;
                    case 13: // Roll into a jumping overhead cleave.
                        windup=new Vector3(.10f,1.65f,-.16f);finish=new Vector3(.15f,.44f,.68f);
                        startAngles=new Vector3(-65,0,0);endAngles=new Vector3(160,0,0);break;
                    case 14: // Deliberate posture-break execution.
                        windup=new Vector3(-.36f,1.38f,-.10f);finish=new Vector3(.32f,.55f,.72f);
                        startAngles=new Vector3(-50,-70,10);endAngles=new Vector3(145,70,-30);break;
                    case 1: // Waist-high, forward half-moon sweep.
                        windup=new Vector3(.48f,1.0f,.28f);finish=new Vector3(-.45f,1.0f,.48f);
                        startAngles=new Vector3(90,85,0);endAngles=new Vector3(90,-85,0);break;
                    case 2: // Upright torso and a horizontal blade, then thrust and turn out.
                        windup=new Vector3(.24f,1.03f,.20f);finish=new Vector3(.06f,1.04f,.92f);
                        startAngles=endAngles=new Vector3(90,0,0);break;
                    case 4: // Reverse the rising first cut into a broad descending diagonal.
                        windup=new Vector3(.46f,1.24f,.56f);finish=new Vector3(-.54f,.52f,.62f);
                        startAngles=new Vector3(55,40,-25);endAngles=new Vector3(125,-50,30);break;
                    case 5: // Lunging thrust after two light attacks.
                    case 8:
                        windup=new Vector3(.26f,.94f,-.08f);finish=new Vector3(.05f,1.02f,.78f);
                        startAngles=new Vector3(76,0,0);endAngles=new Vector3(90,0,0);break;
                    case 3: // Empty leap: draw the sword close and tuck the knees.
                        windup=new Vector3(-.28f,1.22f,.25f);finish=new Vector3(.30f,1.30f,.18f);
                        startAngles=new Vector3(-25,-50,20);endAngles=new Vector3(-30,55,-30);break;
                    case 6: // Retreat, gather, then cast three round crescent cuts.
                        windup=new Vector3(-.42f,1.18f,.12f);finish=new Vector3(.50f,.92f,.58f);
                        startAngles=new Vector3(75,-95,15);endAngles=new Vector3(100,95,-20);break;
                    case 9: // Blink step: low draw into a level, full-body horizontal cut.
                        windup=new Vector3(-.38f,.75f,-.12f);finish=new Vector3(.56f,.95f,.56f);
                        startAngles=new Vector3(90,-105,8);endAngles=new Vector3(90,110,-8);break;
                    case 10: // Countercut travels from the deflect into a rising reverse cut.
                        windup=new Vector3(-.45f,.45f,.08f);finish=new Vector3(.28f,1.44f,.48f);
                        startAngles=new Vector3(140,-85,35);endAngles=new Vector3(-65,65,-35);break;
                    default:
                        windup=heavy?new Vector3(.15f,1.38f,.02f):new Vector3(.50f,1.20f,.06f);
                        finish=heavy?new Vector3(.12f,.48f,.55f):new Vector3(-.42f,.68f,.48f);
                        startAngles=heavy?new Vector3(-55,0,-8):new Vector3(0,75,-65);
                        endAngles=heavy?new Vector3(155,0,8):new Vector3(125,-85,55);break;
                }
                // Windup, fast contact arc, then controlled recovery. Adjacent cuts use
                // the previous finish as their starting guard, so a chain keeps its flow.
                Vector3 ready=hand;
                float wind=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,heavy?.20f:.10f,p));
                bool thrust=combo==2 || combo==5 || combo==8 || combo==11 || combo==25 || combo==TrialPlayer.SakuraFollowup || combo==TrialPlayer.ParryTriple || combo==TrialPlayer.BehindThrust;
                Vector3 arc=(windup+finish)*.5f+Vector3.forward*(thrust?0:heavy?.20f:.16f);
                Vector3 cut=Vector3.Lerp(Vector3.Lerp(windup,arc,swing),Vector3.Lerp(arc,finish,swing),swing);
                hand=Vector3.Lerp(Vector3.Lerp(ready,cut,wind),ready,recover);
                offhand=Vector3.Lerp(heavy?hand+new Vector3(-.12f,-.10f,-.08f):new Vector3(-.35f,1.02f,-.10f),offhand,recover);
                float side=combo==1 || combo==2 || combo==4 || combo==10 || combo==16 || combo==TrialPlayer.AirBladeSecond?-1:1;
                twist=Mathf.Lerp(-42*side,58*side,swing)*(1-recover);
                lean=Mathf.Lerp(-12,heavy?25:14,swing)*(1-recover);
                if(thrust){twist=Mathf.Lerp(-22,8,swing)*(1-recover);lean=Mathf.Lerp(-6,22,swing)*(1-recover);}
                if(combo==2){lean=3+5*swing;twist=Mathf.Lerp(0,150,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.46f,.85f,p)))*(1-recover*.7f);}
                if(combo==TrialPlayer.DodgeDragon && p<.64f){lean=-10;twist=0;Leg(leftLeg,leftFoot,-40);Leg(rightLeg,rightFoot,32);}
                var cutRotation=Quaternion.Slerp(Quaternion.Euler(startAngles),Quaternion.Euler(endAngles),swing);
                swordRotation=Quaternion.Slerp(Quaternion.Slerp(Quaternion.Euler(swordAngles),cutRotation,wind),Quaternion.Euler(swordAngles),recover);
                model.localPosition+=transform.InverseTransformDirection(transform.forward)*Mathf.Sin(p*Mathf.PI)*.10f;
                Leg(leftLeg,leftFoot,Mathf.Lerp(-24,15,swing));Leg(rightLeg,rightFoot,Mathf.Lerp(22,-16,swing));
                if(combo==13 || combo==17)model.localPosition+=Vector3.up*(Mathf.Sin(Mathf.Clamp01(p/.72f)*Mathf.PI)*.75f);
                if(combo==3 || TrialPlayer.IsAirBlade(combo))
                {
                    float tuck=combo==3?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.4f,p)):1;
                    Leg(leftLeg,leftFoot,-48*tuck);Leg(rightLeg,rightFoot,30*tuck);lean=-8;bank=0;
                }
                if(TrialPlayer.IsDive(combo))
                {lean=combo==TrialPlayer.AirSandDive?48:14;twist=0;offhand=hand+new Vector3(-.12f,-.1f,0);Leg(leftLeg,leftFoot,-22);Leg(rightLeg,rightFoot,22);}
                if(combo==TrialPlayer.FireDragon && p<.44f)
                {
                    float roll=Mathf.SmoothStep(0,1,p/.44f);var rotation=Quaternion.Euler(-360*roll,0,-25*Mathf.Sin(roll*Mathf.PI));
                    model.localRotation=modelRotation*rotation;var pivot=Vector3.up*.75f;
                    model.localPosition=modelOrigin+pivot-rotation*pivot+Vector3.up*(Mathf.Sin(roll*Mathf.PI)*.4f);
                    Leg(leftLeg,leftFoot,-45);Leg(rightLeg,rightFoot,40);
                }
            }
            if(action==FighterAction.Charge)
            {
                float charge=Mathf.SmoothStep(0,1,Mathf.Clamp01(p/.55f));
                hand=Vector3.Lerp(hand,new Vector3(.12f,1.48f,-.05f),charge);
                offhand=hand+new Vector3(-.12f,-.10f,-.06f);swordAngles=new Vector3(-55,0,-5);
                lean=-10*charge;twist=-12*charge;Leg(leftLeg,leftFoot,-18);Leg(rightLeg,rightFoot,20);
            }
            if(action==FighterAction.Heal)
            {
                float drink=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.42f,p))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,1,p)));
                offhand=Vector3.Lerp(new Vector3(-.28f,.65f,.12f),new Vector3(-.10f,1.40f,.28f),drink);
                hand=new Vector3(.32f,.67f,.14f);swordAngles=new Vector3(15,15,-55);lean=-6*drink;
            }
            if(action==FighterAction.Stagger) {lean=-24*Mathf.Sin(p*Mathf.PI);twist=12;hand.z=.13f;}
            if(action==FighterAction.Dodge)
            {
                var rollAxis=player && player.DodgeDirection.sqrMagnitude>.01f?transform.InverseTransformDirection(Vector3.Cross(Vector3.up,player.DodgeDirection)).normalized:Vector3.right;
                var roll=combo==7?Quaternion.Euler(0,p*360,0):Quaternion.AngleAxis(p*360,rollAxis);
                model.localRotation=modelRotation*roll;
                var pivot=Vector3.up*.65f;
                model.localPosition=modelOrigin+pivot-roll*pivot+Vector3.up*(Mathf.Sin(p*Mathf.PI)*.12f);
                hand=new Vector3(.18f,.65f,.22f);offhand=new Vector3(-.18f,.65f,.22f);
                swordAngles=combo==7?new Vector3(95,0,-10):new Vector3(10,0,-25);
                if(combo==7)hand=new Vector3(.38f,.90f,.42f);
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
            if(action==FighterAction.Heavy && (combo==13 || combo==17))basis+=Vector3.up*(Mathf.Sin(Mathf.Clamp01(p/.72f)*Mathf.PI)*.75f);
            if(action==FighterAction.Dodge || action==FighterAction.Dead || action==FighterAction.Heavy && combo==TrialPlayer.FireDragon && p<.44f)
            {
                orientation=transform.rotation*Quaternion.Inverse(modelRotation)*model.localRotation;
                basis=transform.position+transform.rotation*(model.localPosition-modelOrigin);
            }
            SolveArm(rightArm,rightElbow,RightHand,basis+orientation*hand,basis+orientation*new Vector3(.75f,.86f,-.1f));
            SolveArm(leftArm,leftElbow,leftHand,basis+orientation*offhand,basis+orientation*new Vector3(-.75f,.80f,-.1f));
            if(flask)
            {
                flask.gameObject.SetActive(action==FighterAction.Heal && p>.06f && p<.94f);
                flask.position=leftHand?leftHand.position:basis+orientation*offhand;
                flask.rotation=orientation*Quaternion.Euler(0,0,Mathf.Lerp(0,115,Mathf.InverseLerp(.28f,.60f,p)));
            }
            if(weapon)
            {
                weapon.position=RightHand?RightHand.position:basis+orientation*hand;
                weapon.rotation=orientation*(swordRotation??Quaternion.Euler(swordAngles));
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

        internal static void SolveArm(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 hint)
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
