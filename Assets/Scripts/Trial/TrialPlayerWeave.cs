using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialPlayer
    {
        public byte SwordStyle=>Combo switch
        {
            HiltStrike or ElbowBreak or SheathTurn=>3,
            LoftingDraw or HeelTurn or LowVault or VaultSweep=>2,
            AerialCascade or ScissorKick or CloudRake=>1,OrbitCut=>4,FallingDraw=>5,
            6 or RollFlash or FireDragon=>0,
            AirStep or AirCross or AirFan or AirPierce or WallRebound or MoonChase or 2=>1,
            CrossDraw or LowReap or RisingBridge or 0=>2,
            GatheredPierce or GuardNeedle or WheelFinish or 17 or 19=>3,
            AirWheel or GuardBloom or SpiralSword or FlowingSweep=>4,
            StormWheel or RetreatDraw or ThreadPierce or DoubleFang=>5,
            _=>1
        };
        float lightHeldAge,heavyHeldAge,weaveUntil;
        bool lightHoldSpent,heavyHoldSpent,attackGuardLatch;
        int weaveFrom;
        void CollectHeldAttack(float dt,TrialInput input)
        {
            if(!input.AttackHeld){lightHeldAge=0;lightHoldSpent=false;}
            if(!input.HeavyHeld){heavyHeldAge=0;heavyHoldSpent=false;}
            if(CombatFrozen || Action!=FighterAction.Light && Action!=FighterAction.Heavy)return;
            if(input.AttackHeld)lightHeldAge+=dt;
            if(input.HeavyHeld)heavyHeldAge+=dt;
            // Each physical hold creates one alternate follow-up, never an automatic loop.
            if(lightHeldAge>=.26f && !lightHoldSpent && attackInputs.Count<2){QueueAttack(false,true);lightHoldSpent=true;}
            if(heavyHeldAge>=.30f && !heavyHoldSpent && attackInputs.Count<2){QueueAttack(true,true);heavyHoldSpent=true;}
        }
        void BeginWeaveGuard()
        {
            weaveFrom=Combo;weaveUntil=Time.time+.85f;
            ClearAttackInputs();chainUntil=0;Begin(FighterAction.Free,0);Guarding=true;GuardAge=0;
            EmitSwordCue(TrialCombatCue.WeaveSigil,transform.position+Vector3.up,transform.forward,transform.position,5);
        }
        int ResolveWeaveAttack(bool heavy,int requested)
        {
            if(ParryEmpowered || ParryDodgeFollowup || Time.time<blinkReadyUntil)return requested;
            bool air=(airborneCombo || Time.time<weaveUntil && IsAerialMove(weaveFrom)) && transform.position.y>launchFloor+.35f;
            if(attackInputs.Count>0 && attackInputs.Peek().held)return air?(heavy?AirFan:AirPierce):heavy?StormWheel:GatheredPierce;
            if(Time.time<weaveUntil)
            {
                bool wasAir=IsAerialMove(weaveFrom);
                return air && wasAir?(heavy?AirWheel:AirPierce):heavy?GuardBloom:GuardNeedle;
            }
            // Up close, the ordinary air route turns into physical choreography;
            // the original three-blade cast remains available against distant opponents.
            if(air && requested==AirBladeFirst)
            {
                var nearby=Locked?LockTarget:game.NearestOpponent(this,6);
                if(nearby && Vector3.ProjectOnPlane(nearby.transform.position-transform.position,Vector3.up).magnitude<5.5f)
                    return VaultSweep;
            }
            return requested;
        }
        void TrackSwordTarget(float dt)
        {
            bool attacking=Action==FighterAction.Light || Action==FighterAction.Heavy;
            if(!attacking || Combo==FireDragon || IsDive(Combo))return;
            if(Locked && LockTarget.Alive)chaseTarget=LockTarget;
            else if(!chaseTarget || !chaseTarget.Alive)chaseTarget=FindChaseTarget();
            if(!chaseTarget)return;
            Vector3 offset=Vector3.ProjectOnPlane(chaseTarget.transform.position-transform.position,Vector3.up);
            if(offset.sqrMagnitude<.16f || Physics.Linecast(transform.position+Vector3.up,chaseTarget.transform.position+Vector3.up,1,QueryTriggerInteraction.Ignore))return;
            // Follow through the contact phase, then commit to recovery; don't snap across targets.
            if(Progress<.62f)
            {
                Turn(offset,Locked?1260:780,dt);
                swordHeading=Vector3.RotateTowards(swordHeading,offset.normalized,Mathf.Deg2Rad*(Locked?1080:660)*dt,0).normalized;
            }
        }
    }
}
