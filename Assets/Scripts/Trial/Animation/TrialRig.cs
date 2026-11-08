using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    // Authored motion on Tomorin's actual generic skeleton. IK makes the sword hand independent
    // of the FBX's unusual centimetre scale and bone roll; damage never relies on animation events.
    public sealed partial class TrialRig : MonoBehaviour
    {
        public Transform RightHand { get; private set; }
        TrialFighter fighter;
        Transform flask;
        internal const float FlaskLipHeight=.135f;
        Transform faceMask;
        Vector3 mouthOffset,maskedMouthOffset;
        Transform model, spine, head, leftLeg, rightLeg, leftFoot, rightFoot;
        Transform rightArm,rightElbow,leftArm,leftElbow,leftHand,weapon;
        Transform[] bones;
        Quaternion[] bindRotations;
        Quaternion[] previousRotations;
        Quaternion[] transitionRotations;
        TrialPlayer player;
        Vector3 modelOrigin;
        Quaternion modelRotation;
        float phase;
        float deathPoseTime;
        FighterAction renderedAction;
        int renderedCombo;
        float renderedTime,transitionAge;
        bool hasRenderedPose,renderingTransition;
        Vector3 transitionHand,transitionOffhand,transitionRoot;
        Quaternion transitionSword,transitionRotation;
        const float TransitionDuration=.11f;
        float activeTransitionDuration=TransitionDuration;
        bool responsiveTransition;
        Vector3 posedHand,posedOffhand;
        Vector3 posedRightHint,posedLeftHint;
        Transform hat;
        Quaternion hatRest,hatAxes;
        Vector3 lastBladeTip;
        Vector2 hatTilt,hatVelocity;
        bool hasBladeSample;

        public void Initialize(TrialFighter owner,Transform visual)
        {
            fighter=owner;player=owner as TrialPlayer;model=visual;modelOrigin=model.localPosition;modelRotation=model.localRotation;
            var list=new List<Transform>();
            foreach(var t in visual.GetComponentsInChildren<Transform>(true)) if(t!=visual && t.name!="RovingHat") list.Add(t);
            bones=list.ToArray();bindRotations=new Quaternion[bones.Length];previousRotations=new Quaternion[bones.Length];transitionRotations=new Quaternion[bones.Length];
            for(int i=0;i<bones.Length;i++) bindRotations[i]=bones[i].localRotation;
            spine=Find("spine_01_x");head=Find("head_x");
            hat=head?head.Find("RovingHat"):null;
            if(head)
            {
                faceMask=head.Find("RovingMask");
                // Tomorin's lips in the normalized bind pose, with clearance for the mask.
                mouthOffset=head.InverseTransformPoint(transform.TransformPoint(new Vector3(0,1.09f,.26f)));
                maskedMouthOffset=head.InverseTransformPoint(transform.TransformPoint(new Vector3(0,1.09f,.30f)));
            }
            if(hat)
            {
                hatRest=hat.localRotation;
                hatAxes=Quaternion.Inverse(hat.rotation)*model.rotation;
            }
            leftLeg=Find("leftthigh");rightLeg=Find("rightthigh");leftFoot=Find("leftfoot");rightFoot=Find("rightfoot");
            rightArm=Find("arm_stretch_r");rightElbow=Find("forearm_stretch_r");RightHand=Find("hand_r");
            leftArm=Find("arm_stretch_l");leftElbow=Find("forearm_stretch_l");leftHand=Find("hand_l");
            if(hat)CacheRelaxedFlippers();
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
            if(Time.deltaTime<=0)return;
            float gaitSpeed=scabbard?Mathf.Lerp(6.8f,13.3f,Mathf.InverseLerp(.55f,1.6f,fighter.MoveBlend))
                :Mathf.Lerp(8.8f,12.8f,Mathf.InverseLerp(.5f,1.6f,fighter.MoveBlend));
            phase=Mathf.Repeat(phase+Time.deltaTime*gaitSpeed*Mathf.Clamp01(fighter.MoveBlend*2),2*Mathf.PI);
            if(player)idlePhase=Mathf.Repeat(idlePhase+Time.deltaTime*(2*Mathf.PI/IdleCycleDuration),2*Mathf.PI);
            if(scabbard)TickScabbard(Time.deltaTime);
            deathPoseTime=fighter.Action==FighterAction.Dead?deathPoseTime+Time.deltaTime:0;
            bool changed=renderedAction!=fighter.Action || renderedCombo!=fighter.Combo || fighter.ActionTime<renderedTime;
            if(hasRenderedPose && changed)
            {
                bool attacking=fighter.Action==FighterAction.Light || fighter.Action==FighterAction.Heavy;
                bool landingLink=TrialPlayer.IsDive(fighter.Combo) && TrialPlayer.IsAirVolleyFinisher(renderedCombo)
                    || fighter.Combo==TrialPlayer.SakuraFollowup && (TrialPlayer.IsDive(renderedCombo) || TrialPlayer.IsAirVolleyFinisher(renderedCombo));
                bool groundChain=player && attacking && (renderedAction==FighterAction.Light || renderedAction==FighterAction.Heavy)
                    && !TrialPlayer.IsAerialMove(renderedCombo) && !TrialPlayer.IsAerialMove(fighter.Combo);
                responsiveTransition=attacking && (groundChain || landingLink || renderedAction==FighterAction.Dodge || fighter.Combo==TrialPlayer.RollThrust || fighter.Combo==TrialPlayer.RollFlash || fighter.Combo==TrialPlayer.SpiralSword || fighter.Combo==TrialPlayer.SwordGuardReturn);
                activeTransitionDuration=groundChain?.06f:responsiveTransition?.075f:TransitionDuration;
                transitionAge=responsiveTransition?Mathf.Min(Time.deltaTime,.025f):0;
                transitionHand=transform.InverseTransformPoint(RightHand?RightHand.position:weapon?weapon.position:transform.position);
                transitionOffhand=transform.InverseTransformPoint(leftHand?leftHand.position:transform.position);
                transitionSword=Quaternion.Inverse(transform.rotation)*(weapon?weapon.rotation:transform.rotation);
                transitionRoot=model.localPosition;transitionRotation=model.localRotation;
                for(int i=0;i<bones.Length;i++)transitionRotations[i]=bones[i].localRotation;
            }
            else transitionAge+=Time.deltaTime;
            renderingTransition=hasRenderedPose && transitionAge<activeTransitionDuration && fighter.Action!=FighterAction.Dodge && fighter.Action!=FighterAction.Dead;
            renderedAction=fighter.Action;renderedCombo=fighter.Combo;renderedTime=fighter.ActionTime;
            for(int i=0;i<bones.Length;i++)previousRotations[i]=bones[i].localRotation;
            if(fighter is TrialEnemy enemy && enemy.Champion && enemy.Champion.IsRemnant && enemy.Alive && !enemy.Vulnerable)
            {
                var brain=enemy.Champion;var shape=brain.Move.shape;
                int cut=shape==ChampionShape.Thrust?11:shape==ChampionShape.BladeWave?12:shape==ChampionShape.Stomp?4:shape==ChampionShape.Spiral?2:brain.MoveIndex%2;
                Pose(enemy.Flinching?FighterAction.Stagger:brain.Attacking && !brain.CounterReady?FighterAction.Light:FighterAction.Free,brain.AnimationBeat,cut,fighter.MoveBlend,fighter.Guarding || brain.CounterReady,phase);
            }
            else if(player && player.AirVolleyDescending && fighter.Action==FighterAction.Free)
                Pose(FighterAction.Light,.90f,fighter.Combo,0,false,phase);
            else Pose(fighter.Action,fighter.Action==FighterAction.Dead?Mathf.Clamp01(deathPoseTime/1.3f):fighter.Progress,fighter.Combo,fighter.MoveBlend,fighter.Guarding,phase);
            Quaternion posedSwordRotation=weapon?weapon.rotation:Quaternion.identity;
            float blend=1-Mathf.Exp(-((fighter.Action==FighterAction.Light || fighter.Action==FighterAction.Heavy) && fighter.Progress<.55f?48:28)*Time.deltaTime);
            if(fighter.Action!=FighterAction.Dodge && fighter.Action!=FighterAction.Dead)
                for(int i=0;i<bones.Length;i++)bones[i].localRotation=renderingTransition
                    ?Quaternion.Slerp(transitionRotations[i],bones[i].localRotation,Mathf.SmoothStep(0,1,transitionAge/activeTransitionDuration))
                    :Quaternion.Slerp(previousRotations[i],bones[i].localRotation,blend);
            // Body blending changes shoulder positions. Solve the grips again
            // against the already blended hand targets, rather than blending IK twice.
            if(drivingScabbardGrip || renderingTransition && responsiveTransition)
            {
                SolveArm(rightArm,rightElbow,RightHand,transform.TransformPoint(posedHand),posedRightHint);
                SolveArm(leftArm,leftElbow,leftHand,transform.TransformPoint(posedOffhand),posedLeftHint);
            }
            // Bone smoothing changes the final hand position; keep the sword in its grip.
            if(weapon)
            {
                weapon.rotation=posedSwordRotation;
                if(swordGuidedByScabbard)weapon.position=guidedSwordPosition;
                else if(RightHand)weapon.position=RightHand.position;
            }
            if(fighter.Action==FighterAction.Heal && head && leftHand)
                PoseDrink(fighter.Progress,transform.TransformPoint(posedOffhand),posedLeftHint);
            TickHat(Time.deltaTime);
            if(player)player.SamplePerfectDodgeTrail();
            hasRenderedPose=true;renderingTransition=false;
        }

        void TickHat(float dt)
        {
            if(!hat || !weapon || dt<=0)return;
            Vector3 tip=transform.InverseTransformPoint(weapon.TransformPoint(Vector3.up*.92f));
            Vector3 velocity=hasBladeSample?(tip-lastBladeTip)/dt:Vector3.zero;
            lastBladeTip=tip;
            hasBladeSample=true;
            bool swinging=fighter.Action==FighterAction.Light || fighter.Action==FighterAction.Heavy;
            Vector2 target=swinging
                ?Vector2.ClampMagnitude(new Vector2(-velocity.z-.25f*velocity.y,velocity.x)*.52f,3.2f)
                :Vector2.zero;

            // Analytic damped spring. The brim lags a cut, overshoots slightly and settles.
            const float damping=15f,frequency=24f;
            float fade=Mathf.Exp(-damping*dt);
            float sin=Mathf.Sin(frequency*dt),cos=Mathf.Cos(frequency*dt);
            Vector2 offset=hatTilt-target;
            Vector2 carry=(hatVelocity+damping*offset)/frequency;
            Vector2 wave=offset*cos+carry*sin;
            hatVelocity=(frequency*(carry*cos-offset*sin)-damping*wave)*fade;
            hatTilt=Vector2.ClampMagnitude(target+wave*fade,3.5f);
            hat.localRotation=hatRest*hatAxes*Quaternion.Euler(hatTilt.x,0,hatTilt.y)*Quaternion.Inverse(hatAxes);
        }

        public void Pose(FighterAction action,float p,int combo,float movement,bool guard,float walkPhase)
        {
            swordGuidedByScabbard=drivingScabbardGrip=false;
            for(int i=0;i<bones.Length;i++) bones[i].localRotation=bindRotations[i];
            model.localPosition=modelOrigin;model.localRotation=modelRotation;
            float gait=Mathf.Sin(walkPhase)*Mathf.Min(1,movement);
            float sprint=Mathf.InverseLerp(.9f,1.6f,movement);
            float breathe=Mathf.Sin(Time.time*2.5f)*.008f;
            float idleWeight=action==FighterAction.Free && !guard && (player || !fighter)
                ?1-Ease(.02f,.26f,movement):0;
            Vector3 hand=new Vector3(.30f,.76f,.26f), offhand=new Vector3(-.25f,.77f,.18f);
            Vector3 swordAngles=new Vector3(8,0,-52);
            Quaternion? swordRotation=null;
            Quaternion rootTurn=Quaternion.identity;
            Vector3? rootPivot=null;
            float twist=0,lean=movement*6,bank=player? -player.TurnLean*9:0;
            float run=0;
            if(scabbard && (action==FighterAction.Free || action==FighterAction.Heal) && !guard)
            {
                run=Ease(.96f,1.55f,movement);
                SwordLocomotion(movement,walkPhase,run,ref hand,ref offhand,ref swordAngles,ref lean,ref twist,ref bank);
            }
            else if(action==FighterAction.Free || action==FighterAction.Heal)
            {
                float stride=Mathf.Lerp(38,61,sprint);
                model.localPosition+=Vector3.up*(Mathf.Abs(Mathf.Cos(walkPhase))*.046f*Mathf.Min(1,movement)+breathe*(1-idleWeight));
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
                if(combo==TrialPlayer.DoubleFang){float beat=Mathf.Clamp01((p-(p<.48f?.08f:.48f))/.27f);swing=Mathf.SmoothStep(0,1,beat);recover=p>.85f?(p-.85f)/.15f:0;}
                if(combo==TrialPlayer.ParryMirror){float beat=Mathf.Clamp01((p-(p<.42f?.08f:.42f))/.27f);swing=Mathf.SmoothStep(0,1,beat);recover=p>.82f?(p-.82f)/.18f:0;}
                if(combo==TrialPlayer.RollFlash){swing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.40f,p));recover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.62f,1,p));}
                if(combo==TrialPlayer.RollThrust)swing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.30f,p));
                if(combo==TrialPlayer.BlinkHorizontalSlash)swing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.45f,p));
                if(combo==TrialPlayer.SakuraFollowup && player && player.LandingSakuraFlow)
                    swing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.42f,p));
                if(combo==TrialPlayer.SwordGuardReturn){swing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.02f,.50f,p));recover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.68f,1,p));}
                if(combo==TrialPlayer.SpiralSword){swing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.025f,.18f,p));recover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.62f,1,p));}
                if(combo==TrialPlayer.AirFan || combo==TrialPlayer.AerialCascade){swing=Mathf.Sin(Mathf.Clamp01(Mathf.Repeat(Mathf.Max(0,p-.08f),.22f)/.22f)*Mathf.PI);recover=0;}
                if(combo==TrialPlayer.DodgeDragon){swing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.64f,.82f,p));recover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.86f,1,p));}
                Vector3 windup,finish;
                Vector3 startAngles,endAngles;
                switch(combo)
                {
                    case TrialPlayer.SwordGuardReturn:
                        windup=new Vector3(-.45f,.80f,.10f);finish=new Vector3(.52f,1.10f,.68f);
                        startAngles=new Vector3(95,-100,0);endAngles=new Vector3(85,105,-10);break;
                    case TrialPlayer.SwordRecallCut:
                        windup=new Vector3(.28f,1.18f,.92f);finish=new Vector3(-.48f,.65f,.65f);
                        startAngles=new Vector3(75,80,-25);endAngles=new Vector3(135,-95,25);break;
                    case TrialPlayer.ParryMirror:
                        windup=p<.42f?new Vector3(-.54f,.52f,.16f):new Vector3(.54f,1.48f,.38f);
                        finish=p<.42f?new Vector3(.54f,1.48f,.65f):new Vector3(-.50f,.48f,.68f);
                        startAngles=p<.42f?new Vector3(145,-80,35):new Vector3(20,85,-35);
                        endAngles=p<.42f?new Vector3(20,85,-35):new Vector3(145,-80,35);break;
                    case TrialPlayer.SwordRecallRise:
                    case TrialPlayer.ParryAscend:
                        windup=new Vector3(-.48f,.38f,.16f);finish=new Vector3(.38f,1.70f,.58f);
                        startAngles=new Vector3(155,-100,25);endAngles=new Vector3(-30,80,-30);break;
                    case TrialPlayer.HiltStrike:
                        windup=new Vector3(.18f,1.13f,.42f);finish=new Vector3(.04f,1.20f,.88f);
                        startAngles=new Vector3(-15,0,15);endAngles=new Vector3(-25,0,5);break;
                    case TrialPlayer.ElbowBreak:
                        windup=new Vector3(.45f,.92f,.12f);finish=new Vector3(.26f,1.08f,.48f);
                        startAngles=new Vector3(15,80,-30);endAngles=new Vector3(25,30,-60);break;
                    case TrialPlayer.HeelTurn:
                        windup=new Vector3(.18f,.72f,.20f);finish=new Vector3(-.32f,1.02f,.18f);
                        startAngles=new Vector3(-10,30,-30);endAngles=new Vector3(20,-60,-20);break;
                    case TrialPlayer.LowVault:
                        windup=new Vector3(-.30f,.90f,.12f);finish=new Vector3(.40f,1.38f,.04f);
                        startAngles=new Vector3(20,-45,30);endAngles=new Vector3(-50,60,-35);break;
                    case TrialPlayer.VaultSweep:
                        windup=new Vector3(.50f,1.30f,-.12f);finish=new Vector3(-.54f,.86f,.62f);
                        startAngles=new Vector3(60,120,-35);endAngles=new Vector3(115,-95,25);break;
                    case TrialPlayer.OrbitCut:
                        windup=new Vector3(-.52f,.80f,-.08f);finish=new Vector3(.52f,.98f,.62f);
                        startAngles=new Vector3(85,-140,25);endAngles=new Vector3(100,110,-30);break;
                    case TrialPlayer.FallingDraw:
                        windup=new Vector3(.48f,1.48f,-.08f);finish=new Vector3(-.42f,.44f,.68f);
                        startAngles=new Vector3(-35,75,-15);endAngles=new Vector3(145,-75,20);break;
                    case TrialPlayer.ScissorKick:
                        windup=new Vector3(.26f,1.30f,.04f);finish=new Vector3(-.30f,1.15f,.06f);
                        startAngles=new Vector3(-30,60,20);endAngles=new Vector3(-40,-55,-20);break;
                    case TrialPlayer.SheathTurn:
                        windup=new Vector3(-.32f,.72f,-.15f);finish=new Vector3(.56f,.90f,.70f);
                        startAngles=new Vector3(90,-110,-30);endAngles=new Vector3(90,100,-15);break;
                    case TrialPlayer.LoftingDraw:
                    case TrialPlayer.CloudRake:
                        windup=new Vector3(.22f,1.60f,.15f);finish=new Vector3(-.42f,1.18f,.54f);
                        startAngles=new Vector3(-40,80,-40);endAngles=new Vector3(90,-110,20);break;
                    case TrialPlayer.AirStep:
                    case TrialPlayer.WallRebound:
                        windup=new Vector3(.28f,.72f,.22f);finish=new Vector3(-.30f,1.28f,.22f);
                        startAngles=new Vector3(35,45,-20);endAngles=new Vector3(-25,-50,25);break;
                    case TrialPlayer.AirCross:
                        windup=new Vector3(-.46f,.60f,.28f);finish=new Vector3(.50f,1.28f,.60f);
                        startAngles=new Vector3(140,-70,30);endAngles=new Vector3(15,80,-40);break;
                    case TrialPlayer.AirWheel:
                        windup=new Vector3(.50f,1.28f,.60f);finish=new Vector3(-.52f,.82f,.56f);
                        startAngles=new Vector3(70,110,-20);endAngles=new Vector3(100,-110,20);break;
                    case TrialPlayer.AerialCascade:
                    case TrialPlayer.AirFan:
                        windup=new Vector3(-.38f,1.28f,.15f);finish=new Vector3(.42f,.90f,.68f);
                        startAngles=new Vector3(45,-70,20);endAngles=new Vector3(115,75,-30);break;
                    case TrialPlayer.GatheredPierce:
                    case TrialPlayer.GuardNeedle:
                    case TrialPlayer.AirPierce:
                    case TrialPlayer.RollThrust:
                    case TrialPlayer.ThreadPierce:
                        windup=new Vector3(.26f,.88f,-.20f);finish=new Vector3(.05f,1.02f,1.02f);
                        startAngles=endAngles=new Vector3(90,0,0);break;
                    case TrialPlayer.RollFlash:
                        windup=new Vector3(.50f,.38f,-.46f);finish=new Vector3(-.24f,1.02f,.90f);
                        startAngles=Quaternion.FromToRotation(Vector3.up,new Vector3(.8f,-.4f,-.7f)).eulerAngles;
                        endAngles=Quaternion.FromToRotation(Vector3.up,new Vector3(-1,0,.4f)).eulerAngles;break;
                    case TrialPlayer.CrossDraw:
                        windup=new Vector3(-.44f,.94f,.28f);finish=new Vector3(.48f,1.32f,.62f);
                        startAngles=new Vector3(115,-85,25);endAngles=new Vector3(30,80,-35);break;
                    case TrialPlayer.LowReap:
                        windup=new Vector3(.48f,.50f,.28f);finish=new Vector3(-.52f,.44f,.62f);
                        startAngles=new Vector3(100,95,15);endAngles=new Vector3(100,-100,-15);break;
                    case TrialPlayer.DoubleFang:
                        windup=p<.48f?new Vector3(-.50f,.52f,.28f):new Vector3(.48f,1.32f,.62f);
                        finish=p<.48f?new Vector3(.48f,1.32f,.62f):new Vector3(-.42f,.72f,.65f);
                        startAngles=p<.48f?new Vector3(130,-70,20):new Vector3(25,75,-25);
                        endAngles=p<.48f?new Vector3(25,75,-25):new Vector3(125,-80,25);break;
                    case TrialPlayer.StormWheel:
                    case TrialPlayer.GuardBloom:
                    case TrialPlayer.WheelFinish:
                        windup=new Vector3(-.48f,1.22f,.28f);finish=new Vector3(.52f,.85f,.64f);
                        startAngles=new Vector3(70,-120,10);endAngles=new Vector3(100,115,-10);break;
                    case TrialPlayer.RetreatDraw:
                        windup=new Vector3(.52f,.85f,.64f);finish=new Vector3(-.36f,1.20f,.20f);
                        startAngles=new Vector3(100,80,-10);endAngles=new Vector3(45,-85,30);break;
                    case TrialPlayer.FlowingSweep: // Fold the extended thrust into a reverse sweep.
                        windup=new Vector3(.08f,1.04f,.78f);finish=new Vector3(-.48f,1.04f,.42f);
                        startAngles=new Vector3(90,0,0);endAngles=new Vector3(90,-100,0);break;
                    case TrialPlayer.RisingBridge: // Carry a low recovery upward into the aerial route.
                        windup=new Vector3(-.42f,.58f,.38f);finish=new Vector3(.32f,1.40f,.48f);
                        startAngles=new Vector3(130,-65,25);endAngles=new Vector3(-35,45,-20);break;
                    case TrialPlayer.MoonChase: // Uncoil a ranged cast into a forward pursuit thrust.
                        windup=new Vector3(.42f,.96f,.22f);finish=new Vector3(.06f,1.04f,.94f);
                        startAngles=new Vector3(90,65,0);endAngles=new Vector3(90,0,0);break;
                    case 0 when !heavy: // Front-facing rising cut: low left to high right.
                        windup=new Vector3(-.43f,.64f,.40f);finish=new Vector3(.46f,1.24f,.56f);
                        startAngles=Quaternion.FromToRotation(Vector3.up,new Vector3(-.65f,-.3f,1)).eulerAngles;
                        endAngles=Quaternion.FromToRotation(Vector3.up,new Vector3(.7f,.6f,1)).eulerAngles;break;
                    case TrialPlayer.ParryTriple:
                    case TrialPlayer.BehindThrust:
                        windup=new Vector3(.20f,1.0f,.15f);finish=new Vector3(.05f,1.08f,.92f);
                        startAngles=endAngles=new Vector3(90,0,0);break;
                    case TrialPlayer.ThunderDragon:
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
                    case TrialPlayer.BlinkHorizontalSlash:
                        windup=new Vector3(-.38f,.75f,-.12f);finish=new Vector3(.56f,.95f,.56f);
                        startAngles=new Vector3(90,-85,8);endAngles=new Vector3(90,85,-8);break;
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
                float wind=combo==TrialPlayer.SakuraFollowup && player && player.LandingSakuraFlow?1:Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,heavy?.20f:.10f,p));
                bool thrust=combo==TrialPlayer.GatheredPierce || combo==TrialPlayer.GuardNeedle || combo==TrialPlayer.AirPierce || combo==TrialPlayer.RollThrust || combo==TrialPlayer.ThreadPierce || combo==2 || combo==5 || combo==8 || combo==11 || combo==25 || combo==TrialPlayer.MoonChase || combo==TrialPlayer.SakuraFollowup || combo==TrialPlayer.ParryTriple || combo==TrialPlayer.BehindThrust;
                Vector3 arc=(windup+finish)*.5f+Vector3.forward*(thrust?0:heavy?.20f:.16f);
                Vector3 cut=Vector3.Lerp(Vector3.Lerp(windup,arc,swing),Vector3.Lerp(arc,finish,swing),swing);
                hand=Vector3.Lerp(Vector3.Lerp(ready,cut,wind),ready,recover);
                bool groundWheel=combo==TrialPlayer.WheelFinish || combo==TrialPlayer.StormWheel || combo==TrialPlayer.GuardBloom;
                Vector3 supportHand=groundWheel?new Vector3(-.38f,.90f,.12f):heavy?hand+new Vector3(-.12f,-.10f,-.08f):new Vector3(-.35f,1.02f,-.10f);
                offhand=Vector3.Lerp(supportHand,offhand,recover);
                if(combo==TrialPlayer.SpiralSword && guard)
                {
                    float block=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.30f,p));
                    hand=Vector3.Lerp(hand,new Vector3(.14f,1.04f,.32f),block);
                    offhand=Vector3.Lerp(offhand,new Vector3(-.10f,.99f,.35f),block);
                }
                float side=combo==1 || combo==2 || combo==4 || combo==10 || combo==16 || combo==TrialPlayer.AirBladeSecond?-1:1;
                twist=Mathf.Lerp(-42*side,58*side,swing)*(1-recover);
                lean=Mathf.Lerp(-12,heavy?25:14,swing)*(1-recover);
                if(thrust){twist=Mathf.Lerp(-22,8,swing)*(1-recover);lean=Mathf.Lerp(-6,22,swing)*(1-recover);}
                if(combo==2){lean=3+5*swing;twist=Mathf.Lerp(-18,42,swing)*(1-recover);}
                if(groundWheel || combo==TrialPlayer.AirWheel)
                {
                    // Rotate the whole visual through the cut. The spine only follows through.
                    float turn=Mathf.SmoothStep(0,1,Mathf.InverseLerp(heavy?.17f:.08f,heavy?.76f:.65f,p));
                    float direction=combo==TrialPlayer.AirWheel?-1:1;
                    rootTurn=Quaternion.AngleAxis(direction*360*turn,Vector3.up);
                    rootPivot=Vector3.up*.65f;
                    twist=direction*Mathf.Lerp(-24,28,swing)*wind*(1-recover);
                }
                if(combo==TrialPlayer.LowReap){lean=26;model.localPosition-=Vector3.up*(Mathf.Sin(p*Mathf.PI)*.22f);}
                if(combo==TrialPlayer.RollFlash){twist=Mathf.Lerp(65,-50,swing)*(1-recover);lean=Mathf.Lerp(22,4,swing);}
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
                if(TrialPlayer.IsLowAirMove(combo))
                {
                    Leg(leftLeg,leftFoot,combo==TrialPlayer.WallRebound?Mathf.Lerp(-65,35,swing):-48);
                    Leg(rightLeg,rightFoot,combo==TrialPlayer.WallRebound?Mathf.Lerp(50,-42,swing):28);
                    lean=combo==TrialPlayer.AirStep?-16:combo==TrialPlayer.WallRebound?-22:8;
                    bank=combo==TrialPlayer.AirWheel?Mathf.Sin(p*Mathf.PI)*18:0;
                }
                if(TrialPlayer.IsMartialMove(combo))
                {
                    float beat=Mathf.Sin(Mathf.Clamp01((p-.08f)/.68f)*Mathf.PI);
                    if(combo==TrialPlayer.SwordRecallCut){twist=Mathf.Lerp(45,-48,swing)*(1-recover);lean=16*beat;bank=-12*beat;}
                    if(combo==TrialPlayer.ParryMirror){twist=Mathf.Lerp(p<.42f?-34:34,p<.42f?38:-40,swing)*(1-recover);lean=18*beat;bank=Mathf.Sin(p*Mathf.PI*2)*-20;}
                    if(combo==TrialPlayer.SwordRecallRise || combo==TrialPlayer.ParryAscend){twist=Mathf.Lerp(-26,30,swing)*(1-recover);lean=-14;bank=-28*beat;Leg(leftLeg,leftFoot,-72);Leg(rightLeg,rightFoot,42);}
                    if(combo==TrialPlayer.HiltStrike){lean=14*beat;twist=-18*beat;offhand=hand+new Vector3(-.10f,-.08f,-.1f);}
                    if(combo==TrialPlayer.ElbowBreak){lean=18*beat;twist=-55*beat;offhand=new Vector3(-.12f,1.18f,.82f);}
                    if(combo==TrialPlayer.HeelTurn)
                    {
                        lean=18;twist=Mathf.Lerp(18,-32,swing)*(1-recover);
                        Leg(leftLeg,leftFoot,-10);Leg(rightLeg,rightFoot,Mathf.Lerp(-85,70,swing));
                        if(rightLeg)rightLeg.rotation=Quaternion.AngleAxis(65*beat,transform.forward)*rightLeg.rotation;
                        model.localPosition-=Vector3.up*.24f*beat;
                    }
                    if(combo==TrialPlayer.LowVault){lean=-22;bank=-24*beat;Leg(leftLeg,leftFoot,-65);Leg(rightLeg,rightFoot,38);}
                    if(combo==TrialPlayer.VaultSweep){twist=Mathf.Lerp(28,-32,swing)*(1-recover);lean=12;bank=32*beat;}
                    if(combo==TrialPlayer.OrbitCut){twist=Mathf.Lerp(-25,30,swing)*(1-recover);bank=-18*beat;}
                    if(combo==TrialPlayer.FallingDraw){lean=34*beat;twist=Mathf.Lerp(65,-60,swing);Leg(leftLeg,leftFoot,-48*beat);Leg(rightLeg,rightFoot,40*beat);}
                    if(combo==TrialPlayer.ScissorKick)
                    {
                        float kick=Mathf.Sin(Mathf.Clamp01((p-.10f)/.70f)*Mathf.PI*2);
                        lean=-24;bank=18*beat;twist=55*beat;
                        Leg(leftLeg,leftFoot,-kick*85);Leg(rightLeg,rightFoot,kick*85);offhand=new Vector3(-.45f,1.18f,-.1f);
                    }
                    if(combo==TrialPlayer.SheathTurn){lean=10;twist=Mathf.Lerp(-42,48,swing)*(1-recover);offhand=new Vector3(-.24f,.70f,-.10f);}
                    if(combo==TrialPlayer.CloudRake || combo==TrialPlayer.LoftingDraw){twist=28*swing*(1-recover);bank=-24*beat;Leg(leftLeg,leftFoot,-62);Leg(rightLeg,rightFoot,45);}
                    // The compact character needs whole-body silhouette changes as well as limb IK.
                    Vector3 rootAngles=Vector3.zero;
                    if(combo==TrialPlayer.ParryMirror)rootAngles=new Vector3(14*beat,Mathf.Sin(p*Mathf.PI*2)*65,-22*Mathf.Sin(p*Mathf.PI*2));
                    if(combo==TrialPlayer.SwordRecallRise || combo==TrialPlayer.ParryAscend)rootAngles=new Vector3(-22*beat,360*swing,-28*beat);
                    if(combo==TrialPlayer.LowVault)rootAngles=new Vector3(-360*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.88f,p)),0,-16*beat);
                    if(combo==TrialPlayer.VaultSweep)rootAngles=new Vector3(16*beat,-360*swing,38*beat);
                    if(combo==TrialPlayer.OrbitCut)rootAngles.y=360*swing;
                    if(combo==TrialPlayer.HeelTurn)rootAngles=new Vector3(22*beat,-155*swing*(1-recover),15*beat);
                    if(combo==TrialPlayer.ScissorKick)rootAngles=new Vector3(-32*beat,70*beat,-48*beat);
                    if(combo==TrialPlayer.CloudRake || combo==TrialPlayer.LoftingDraw)rootAngles=new Vector3(-26*beat,360*swing,-25*beat);
                    if(combo==TrialPlayer.HiltStrike)rootAngles=new Vector3(15*beat,0,0);
                    if(combo==TrialPlayer.ElbowBreak)rootAngles=new Vector3(18*beat,-40*beat,0);
                    if(combo==TrialPlayer.FallingDraw)rootAngles=new Vector3(32*beat,-55*swing*(1-recover),0);
                    rootTurn=Quaternion.Euler(rootAngles);
                    rootPivot=Vector3.up*.65f;
                }
                if(TrialPlayer.IsDive(combo))
                {
                    float landed=player?player.SwordLandingBlend:0;
                    lean=Mathf.Lerp(combo==TrialPlayer.AirSandDive?48:14,12,landed);twist=0;
                    hand=Vector3.Lerp(hand,new Vector3(.20f,.94f,.12f),landed);
                    swordRotation=Quaternion.Slerp(swordRotation??Quaternion.Euler(180,0,0),Quaternion.Euler(90,0,0),landed);
                    offhand=hand+new Vector3(-.12f,-.1f,0);
                    Leg(leftLeg,leftFoot,Mathf.Lerp(-22,-32,landed));Leg(rightLeg,rightFoot,Mathf.Lerp(22,14,landed));
                    model.localPosition-=Vector3.up*(Mathf.Sin(landed*Mathf.PI)*.12f);
                }
                if(TrialPlayer.IsAirVolleyFinisher(combo))
                {
                    float release=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.64f,.90f,p));
                    lean=Mathf.Lerp(lean,24,release);twist*=1-release;
                    hand=Vector3.Lerp(hand,new Vector3(.18f,.88f,.34f),release);
                    swordRotation=Quaternion.Slerp(swordRotation??Quaternion.identity,Quaternion.Euler(135,0,0),release);
                }
                if(combo==TrialPlayer.ThunderDragon && p<.44f)
                {
                    float roll=Mathf.SmoothStep(0,1,p/.44f);
                    rootTurn=Quaternion.Euler(-360*roll,0,-25*Mathf.Sin(roll*Mathf.PI));
                    rootPivot=Vector3.up*.75f;
                    model.localPosition=modelOrigin+Vector3.up*(Mathf.Sin(roll*Mathf.PI)*.4f);
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
                offhand=new Vector3(-.28f,.65f,.12f);
                hand=new Vector3(.32f,.67f,.14f);swordAngles=new Vector3(15,15,-55);lean=-6*FlaskLift(p);
            }
            if(action==FighterAction.Stagger) {lean=-24*Mathf.Sin(p*Mathf.PI);twist=12;hand.z=.13f;}
            if(action==FighterAction.Dodge)
            {
                var rollAxis=player && player.DodgeDirection.sqrMagnitude>.01f?transform.InverseTransformDirection(Vector3.Cross(Vector3.up,player.DodgeDirection)).normalized:Vector3.right;
                rootTurn=combo==7?Quaternion.Euler(0,p*360,0):Quaternion.AngleAxis(p*360,rollAxis);
                rootPivot=Vector3.up*.65f;
                model.localPosition=modelOrigin+Vector3.up*(Mathf.Sin(p*Mathf.PI)*.12f);
                hand=new Vector3(.18f,.65f,.22f);offhand=new Vector3(-.18f,.65f,.22f);
                swordAngles=combo==7?new Vector3(95,0,-10):new Vector3(10,0,-25);
                if(combo==7)hand=new Vector3(.38f,.90f,.42f);
            }
            if(action==FighterAction.Dead)
            {
                rootTurn=Quaternion.Euler(-Mathf.SmoothStep(0,87,p),0,8*p);
                model.localPosition=modelOrigin+new Vector3(0,.12f*p,0);
                hand=new Vector3(.38f,.48f,.05f);offhand=new Vector3(-.38f,.48f,.05f);
            }
            if(scabbard)BlendScabbardPose(action,ref lean,ref twist,ref bank);
            Vector3 idleHead=idleWeight>0
                ?PlayerIdle(idleWeight*(1-sheathFocus),ref hand,ref offhand,ref swordAngles,ref lean,ref twist,ref bank)
                :Vector3.zero;
            model.localRotation=rootTurn*modelRotation;
            if(rootPivot is Vector3 pivot)model.localPosition+=pivot-rootTurn*pivot;
            float transition=renderingTransition?Mathf.SmoothStep(0,1,transitionAge/activeTransitionDuration):1;
            if(renderingTransition)
            {
                model.localPosition=Vector3.Lerp(transitionRoot,model.localPosition,transition);
                model.localRotation=Quaternion.Slerp(transitionRotation,model.localRotation,transition);
            }
            // Pose and solve the arms in the same frame as the turned visual.
            Quaternion orientation=transform.rotation*model.localRotation*Quaternion.Inverse(modelRotation);
            if(spine) spine.rotation=orientation*Quaternion.Euler(lean,twist,bank)*Quaternion.Inverse(orientation)*spine.rotation;
            if(head && action==FighterAction.Free)
            {
                idleHead.x+=-lean*.5f+sheathFocus*12;
                head.rotation=orientation*Quaternion.Euler(idleHead)*Quaternion.Inverse(orientation)*head.rotation;
            }
            Vector3 basis=transform.position;
            if(action==FighterAction.Heavy && (combo==13 || combo==17))basis+=Vector3.up*(Mathf.Sin(Mathf.Clamp01(p/.72f)*Mathf.PI)*.75f);
            if(rootPivot.HasValue || action==FighterAction.Dead)
                basis=transform.position+transform.rotation*(model.localPosition-modelOrigin);
            Vector3 handTarget=Vector3.Lerp(transform.TransformPoint(transitionHand),basis+orientation*hand,drivingScabbardGrip?1:transition);
            Vector3 offhandTarget=Vector3.Lerp(transform.TransformPoint(transitionOffhand),basis+orientation*offhand,drivingScabbardGrip?1:transition);
            if(scabbard)
            {
                PlaceScabbard(orientation,out guidedSwordPosition,out Vector3 sheathGrip);
                if(drivingScabbardGrip)
                {
                    Vector3 liftGrip=basis+orientation*new Vector3(.40f,1.08f,.35f);
                    handTarget=sheathProgress<Aligned
                        ?Vector3.Lerp(Vector3.Lerp(handTarget,liftGrip,Ease(0,.17f,sheathProgress)),guidedSwordPosition,Ease(.12f,Aligned,sheathProgress))
                        :Vector3.Lerp(guidedSwordPosition,handTarget,Ease(Seated,1,sheathProgress));
                    if(action!=FighterAction.Heal)
                        offhandTarget=Vector3.Lerp(offhandTarget,sheathGrip,sheathFocus);
                }
            }
            posedHand=transform.InverseTransformPoint(handTarget);posedOffhand=transform.InverseTransformPoint(offhandTarget);
            posedRightHint=basis+orientation*new Vector3(.75f,.86f,-.1f);
            posedLeftHint=basis+orientation*new Vector3(-.75f,.80f,-.1f);
            float locomotionArmWeight=scabbard && action==FighterAction.Free && !guard
                ?Ease(.03f,.30f,movement)*(1-sheathFocus):0;
            if(locomotionArmWeight>0 && rightArm && leftArm)
            {
                var lateral=orientation*Vector3.right;
                posedRightHint=Vector3.Lerp(posedRightHint,LocomotionElbowHint(rightArm,handTarget,lateral),locomotionArmWeight);
                posedLeftHint=Vector3.Lerp(posedLeftHint,LocomotionElbowHint(leftArm,offhandTarget,lateral),locomotionArmWeight);
            }
            SolveArm(rightArm,rightElbow,RightHand,handTarget,posedRightHint);
            bool drinking=action==FighterAction.Heal && head && leftHand;
            if(drinking)PoseDrink(p,offhandTarget,posedLeftHint);
            else SolveArm(leftArm,leftElbow,leftHand,offhandTarget,posedLeftHint);
            if(scabbard && action==FighterAction.Free && !guard)
                RelaxFlippers(Ease(Seated,1,sheathProgress)*(1-Ease(.03f,.30f,movement)));
            if(flask)flask.gameObject.SetActive(drinking && p>.06f && p<.94f);
            if(weapon)
            {
                weapon.position=swordGuidedByScabbard?guidedSwordPosition:RightHand?RightHand.position:handTarget;
                weapon.rotation=swordGuidedByScabbard?scabbard.rotation
                    :Quaternion.Slerp(transform.rotation*transitionSword,orientation*(swordRotation??Quaternion.Euler(swordAngles)),drivingScabbardGrip?1:transition);
                if(drivingScabbardGrip && !swordGuidedByScabbard)
                {
                    Quaternion liftBlade=orientation*Quaternion.Euler(-15,-32,-33);
                    weapon.rotation=Quaternion.Slerp(weapon.rotation,liftBlade,Ease(0,.16f,sheathProgress));
                    weapon.rotation=Quaternion.Slerp(weapon.rotation,scabbard.rotation,Ease(.12f,Aligned,sheathProgress));
                }
            }
        }

        static float FlaskLift(float progress)=>Ease(.12f,.42f,progress)*(1-Ease(.76f,.94f,progress));

        void PoseDrink(float progress,Vector3 loweredHand,Vector3 elbowHint)
        {
            float lift=FlaskLift(progress);
            float tilt=Ease(.24f,.59f,progress)*(1-Ease(.74f,.93f,progress));
            Quaternion rotation=transform.rotation*Quaternion.Euler(-105*tilt,0,-34*lift);
            Vector3 mouth=head.TransformPoint(faceMask && faceMask.gameObject.activeInHierarchy?maskedMouthOffset:mouthOffset);
            Vector3 grip=mouth-rotation*Vector3.up*FlaskLipHeight;
            SolveArm(leftArm,leftElbow,leftHand,Vector3.Lerp(loweredHand,grip,lift),elbowHint);
            if(flask)flask.SetPositionAndRotation(leftHand.position,rotation);
        }

        void Leg(Transform thigh,Transform foot,float angle)
        {
            if(!thigh) return;
            Vector3 axis=GaitAxis();
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
