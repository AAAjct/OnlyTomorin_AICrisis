using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialPlayer
    {
        public byte SwordStyle=>Combo switch
        {
            ParryMirror or SwordRecallCut or SwordRecallRise=>3,ParryAscend=>0,
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
        const float GuardComboWindup=.14f;
        float weaveUntil,weaveAge;
        bool attackGuardLatch,weavePending,weaveHeavy,startingGuardCombo;
        int weaveFrom;
        // G is a complete command. Different sword positions select distinct
        // physical follow-ups; no extra attack press is required to activate one.
        public static int GuardComboMove(bool heavy,int from,bool air)
        {
            if(air)return from==AirFan || from==AerialCascade || IsAirBlade(from)?AirPierce:WallRebound;
            if(!heavy && (from==1 || from==2) || heavy && from==0 || from==RollThrust || from==RollFlash || from==CrossDraw || from==DoubleFang || from==OrbitCut || from==FlowingSweep)return SpiralSword;
            if(from==SpiralSword)return ThreadPierce;
            if(from==WheelFinish || from==19 || from==FallingDraw)return LowVault;
            if(from==17)return SakuraFollowup;
            if(heavy && from==18)return 17;
            if(from==HeelTurn)return LoftingDraw;
            if(from==5 || from==ThreadPierce || from==MoonChase)return MoonChase;
            if(IsBodyStrike(from))return ElbowBreak;
            if(from==OrbitCut || from==SheathTurn)return FlowingSweep;
            return CrossDraw;
        }
        static bool GuardComboHeavy(int move)=>move==SpiralSword || move==LoftingDraw || move==SakuraFollowup || move==17;
        public int GuardFollowupMove=>GuardComboMove(weaveHeavy,weaveFrom,IsAerialMove(weaveFrom) && transform.position.y>launchFloor+.35f);
        void BeginWeaveGuard()
        {
            // Reuse the roll-slash source ID so the existing replicated weave
            // state and the client's guide describe dodge -> guard correctly.
            bool fromDodge=Action==FighterAction.Dodge || Action==FighterAction.Free && dodgeLinkRemaining>0;
            weaveFrom=fromDodge?RollFlash:Combo;weaveHeavy=!fromDodge && (Action==FighterAction.Heavy || Action==FighterAction.Free && chainHeavy);
            // Recognize rapidly buffered L,L,G / L,L,L,G as the same gesture as
            // spaced taps, while letting G defend immediately instead of waiting.
            if(!weaveHeavy && weaveFrom<=2)
                foreach(var press in attackInputs)
                {if(press.heavy || weaveFrom>=2)break;weaveFrom++;}
            // A throw already guards during its own animation. Enter it directly
            // instead of inserting a stationary guard pose before the cast.
            if(GuardFollowupMove==SpiralSword && !ParryEmpowered)
            {
                startingGuardCombo=true;
                try
                {
                    if(StartAttack(true,SpiralSword))
                    {ClearAttackInputs(false);attackGuardLatch=true;return;}
                }
                finally{startingGuardCombo=false;}
            }
            ClearDodgeLink();ClearAttackInputs(false);chainUntil=0;weaveUntil=Time.time+.85f;weaveAge=0;weavePending=true;
            attackGuardLatch=true;PlanarVelocity=Vector3.zero;
            Begin(FighterAction.Free,0);Guarding=true;GuardAge=0;
            EmitSwordCue(TrialCombatCue.WeaveSigil,transform.position+Vector3.up,transform.forward,transform.position,5);
        }
        bool TickGuardCombo(float dt,TrialInput input)
        {
            if(!weavePending)return false;
            // A real perfect guard always wins over the ordinary scheduled art.
            if(ParryEmpowered || ParryDodgeFollowup || input.Heal)
            {weavePending=false;weaveUntil=0;return false;}
            weaveAge+=dt;
            if(weaveAge<GuardComboWindup)return true;
            int move=GuardFollowupMove;
            weavePending=false;weaveUntil=0;startingGuardCombo=true;
            try{StartAttack(GuardComboHeavy(move),move);}
            finally{startingGuardCombo=false;}
            return true;
        }
        void TrackSwordTarget(float dt)
        {
            bool attacking=Action==FighterAction.Light || Action==FighterAction.Heavy;
            if(!attacking || SwordGuardReturning || Combo==FireDragon || Combo==SpiralSword || Combo==RollThrust || IsDive(Combo))return;
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
