using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialPlayer
    {
        // Stable move IDs also drive the replicated rig; no animation events or local input shortcuts.
        public const int BlinkHorizontalSlash=9;
        public const int AirBladeFirst=26,AirBladeSecond=27,AirBladeThird=28,AirSandDive=29,SakuraFollowup=30;
        public const int ParryTriple=31,ThunderDragon=32,ParryDodge=33,BehindThrust=34,SpiralSword=35,DodgeDragon=36;
        public const int FlowingSweep=37,RisingBridge=38,MoonChase=39;
        public const int RollThrust=40,RollFlash=41,CrossDraw=42,LowReap=43,DoubleFang=44,WheelFinish=45,RetreatDraw=46,ThreadPierce=47;
        public const int AirStep=48,AirCross=49,AirWheel=50,AirFan=51,WallRebound=52;
        public const int GatheredPierce=53,StormWheel=54,GuardNeedle=55,GuardBloom=56,AirPierce=57;
        public const int HiltStrike=58,HeelTurn=59,LowVault=60,VaultSweep=61,OrbitCut=62,FallingDraw=63,ElbowBreak=64,ScissorKick=65,SheathTurn=66,CloudRake=67;
        public const int LoftingDraw=68,AerialCascade=69;
        public const int ParryMirror=70,ParryAscend=71,SwordRecallCut=72,SwordRecallRise=73;
        public const int SwordGuardReturn=74;
        public static bool IsMartialMove(int move)=>move>=HiltStrike && move<=LoftingDraw || move==ParryMirror || move==ParryAscend || move==SwordRecallCut || move==SwordRecallRise;
        public static bool IsBodyStrike(int move)=>move==HiltStrike || move==HeelTurn || move==ElbowBreak || move==ScissorKick;
        public static bool IsMartialAir(int move)=>move==SwordRecallRise || move==ParryAscend || move==LoftingDraw || move==LowVault || move==VaultSweep || move==ScissorKick || move==CloudRake;
        public static bool IsLowAirMove(int move)=>move==AerialCascade || IsMartialAir(move) || move==AirPierce || move>=AirStep && move<=WallRebound;
        public static bool IsExtendedMove(int move)=>move>=RollThrust && move<=ThreadPierce || move>=GatheredPierce && move<=GuardBloom;
        const float ChainGrace=.55f;
        const float LandingSakuraGrace=.75f;
        bool LandingSakuraReady=>swordLanded && IsSwordLandingSource(Combo) && (Action==FighterAction.Light || Action==FighterAction.Heavy)
            || Action==FighterAction.Free && Time.time<chainUntil && IsSwordLandingSource(chainCount) && (IsDive(chainCount) || swordLanded);
        // Two future presses are enough for L,L,H. Consume only the oldest press;
        // contact holds never age the queue, and a committed move renews the next lease.
        struct AttackPress {public bool heavy;public long order;}
        long combatInputOrder,bufferedDodgeOrder;
        readonly Queue<AttackPress> attackInputs=new Queue<AttackPress>(2);
        float attackInputAge;
        bool repeatLight,repeatHeavy,blockHeldLight,blockHeldHeavy,preferHeldHeavy;
        void ReadHeldAttacks(TrialInput input)
        {
            repeatLight=input.AttackHeld;repeatHeavy=input.HeavyHeld;
            if(!repeatLight || input.Attack)blockHeldLight=false;
            if(!repeatHeavy || input.Heavy)blockHeldHeavy=false;
            if(input.Heavy)preferHeldHeavy=true;
            else if(input.Attack)preferHeldHeavy=false;
        }
        bool HeldAttack(bool heavy)
        {
            bool light=repeatLight && !blockHeldLight, strong=repeatHeavy && !blockHeldHeavy;
            return heavy?strong && (!light || preferHeldHeavy):light && (!strong || !preferHeldHeavy);
        }
        void CollectAttackInputs(float dt,TrialInput input)
        {
            ReadHeldAttacks(input);
            // A press accepted for this roll must survive until its follow-up window,
            // including the first frame that crosses the threshold at low frame rates.
            if(!CombatFrozen && Action!=FighterAction.Dodge && attackInputs.Count>0)
            {
                attackInputAge+=dt;
                bool attacking=Action==FighterAction.Light || Action==FighterAction.Heavy;
                bool heavy=Action==FighterAction.Heavy,nextHeavy=attackInputs.Peek().heavy;
                int followup=heavy?(nextHeavy?HeavyHeavyBranch(Combo):HeavyLightBranch(Combo)):(nextHeavy?LightHeavyBranch(Combo):NextLight(Combo));
                // A heavy-only extension must not expire a queued light that waits for recovery.
                float link=followup<0?1:heavy?HeavyLinkProgress(Combo):LightLinkProgress(Combo);
                float lease=attacking?Mathf.Max(InputBuffer,ActionDuration*link+.15f):InputBuffer;
                if(attackInputAge>lease)ClearAttackInputs();
            }
            if(input.Attack)QueueAttack(false);
            if(input.Heavy)QueueAttack(true);
        }
        void QueueAttack(bool heavy)
        {
            long order=++combatInputOrder;
            // A pending dodge supersedes older queued cuts. Make room for a
            // genuinely new press after that dodge, even if the old queue is full.
            while(attackInputs.Count>=2 && Time.time-bufferedDodge<InputBuffer && attackInputs.Peek().order<bufferedDodgeOrder)attackInputs.Dequeue();
            if(attackInputs.Count>=2)
            {
                // Keep the immediate buffered cut, but let a deliberate change of
                // attack type replace the last future press instead of losing it.
                var first=attackInputs.Dequeue();var last=attackInputs.Dequeue();
                attackInputs.Enqueue(first);
                attackInputs.Enqueue(last.heavy==heavy?last:new AttackPress{heavy=heavy,order=order});
                return;
            }
            if(attackInputs.Count==0)attackInputAge=0;
            attackInputs.Enqueue(new AttackPress{heavy=heavy,order=order});
        }
        void PreserveDodgeFollowupInputs(bool lightChord)
        {
            AttackPress? first=null,second=null;
            foreach(var press in attackInputs)if(press.order>bufferedDodgeOrder)
            {if(!first.HasValue)first=press;else second=press;}
            ClearAttackInputs();
            if(first.HasValue){attackInputs.Enqueue(first.Value);if(first.Value.heavy)blockHeldHeavy=false;else blockHeldLight=false;}
            else if(lightChord)attackInputs.Enqueue(new AttackPress{order=++combatInputOrder});
            if(second.HasValue){attackInputs.Enqueue(second.Value);if(second.Value.heavy)blockHeldHeavy=false;else blockHeldLight=false;}
        }
        bool AttackQueued(bool heavy)=>attackInputs.Count>0?attackInputs.Peek().heavy==heavy:HeldAttack(heavy);
        void ConsumeAttackInput()
        {if(attackInputs.Count>0)attackInputs.Dequeue();attackInputAge=0;}
        void ClearAttackInputs(bool suppressHeld=true)
        {
            attackInputs.Clear();attackInputAge=0;bufferedLight=bufferedHeavy=-10;
            if(suppressHeld)
            {
                blockHeldLight=repeatLight;blockHeldHeavy=repeatHeavy;
                weavePending=false;weaveUntil=0;
            }
        }
        // Routes have one purpose and a recovery boundary. -1 means finish the
        // current move; a queued press can start a fresh route after recovery.
        static int NextLight(int move)=>move switch
        {
            0 or 12 or BlinkHorizontalSlash=>1,1=>2,2=>WheelFinish,
            RollThrust or 22 or GuardNeedle or GatheredPierce=>CrossDraw,
            17=>RisingBridge,SakuraFollowup=>CrossDraw,
            CrossDraw=>HiltStrike,HiltStrike=>HeelTurn,
            ElbowBreak=>OrbitCut,OrbitCut=>SheathTurn,
            GuardBloom=>ThreadPierce,StormWheel=>LowVault,
            ParryTriple or BehindThrust or MoonChase=>ParryMirror,
            ThunderDragon or SpiralSword or DodgeDragon=>MoonChase,
            SwordRecallCut or SwordGuardReturn=>CrossDraw,SwordRecallRise=>VaultSweep,ParryMirror=>ParryAscend,ParryAscend=>VaultSweep,
            FlowingSweep=>RisingBridge,RisingBridge=>WheelFinish,
            RetreatDraw=>ThreadPierce,ThreadPierce=>WheelFinish,
            LowReap=>DoubleFang,DoubleFang=>WheelFinish,
            3=>AirBladeFirst,AirBladeFirst=>AirBladeSecond,AirBladeSecond=>AirBladeThird,AirBladeThird=>AirSandDive,
            AirStep=>AirCross,AirCross=>AirWheel,AirWheel=>AirFan,
            AirFan or AerialCascade=>AirSandDive,WallRebound=>AirFan,
            LoftingDraw or LowVault or AirPierce=>VaultSweep,
            VaultSweep=>ScissorKick,ScissorKick or CloudRake=>FallingDraw,
            _=>-1
        };
        static int LightHeavyBranch(int move)=>move switch
        {
            0=>4,1=>5,2=>6,
            // Grounded finishers can commit to sakura pursuit with one more heavy.
            // Keep their light/recovery exits and the original L,L,H entry intact.
            4=>5,
            RollFlash or WheelFinish or 19=>SakuraFollowup,
            RollThrust=>RollFlash,
            CrossDraw or HeelTurn or GatheredPierce or ThreadPierce=>LoftingDraw,
            HiltStrike=>ElbowBreak,ElbowBreak=>SheathTurn,OrbitCut=>FallingDraw,
            GuardNeedle=>GuardBloom,GuardBloom=>LoftingDraw,
            SwordRecallCut or SwordGuardReturn=>LoftingDraw,SwordRecallRise=>AerialCascade,StormWheel=>RetreatDraw,17=>SakuraFollowup,SakuraFollowup=>LoftingDraw,
            ParryTriple=>SpiralSword,BehindThrust or SpiralSword or MoonChase=>ParryAscend,
            ThunderDragon or DodgeDragon=>SpiralSword,ParryMirror=>SakuraFollowup,
            FlowingSweep=>RisingBridge,RisingBridge=>6,
            LowReap=>LoftingDraw,DoubleFang=>WheelFinish,
            RetreatDraw=>6,
            ParryAscend or LoftingDraw or LowVault or VaultSweep or ScissorKick or CloudRake or AirPierce=>AerialCascade,
            AirStep or AirCross or AirWheel=>AerialCascade,
            AirFan or WallRebound or AerialCascade or 3 or AirBladeFirst or AirBladeSecond or AirBladeThird=>20,
            _=>-1
        };
        static int HeavyLightBranch(int move)=>move==0 || move==18?CrossDraw:NextLight(move);
        static int HeavyHeavyBranch(int move)=>move==0?18:move==18?19:LightHeavyBranch(move);
        public static bool IsComboEnd(bool heavy,int move)=>
            (heavy?HeavyLightBranch(move):NextLight(move))<0 && (heavy?HeavyHeavyBranch(move):LightHeavyBranch(move))<0;
        static float HeavyLinkProgress(int move)=>move==ParryMirror?.76f:move==AerialCascade?.80f:move==FallingDraw?.72f:move==AirFan?.78f:move==DoubleFang?.74f:move==ParryTriple?.70f:move==6?.70f:move==DodgeDragon?.84f:move==ThunderDragon?.62f:move==SpiralSword?.55f:.66f;
        public static bool IsAirBlade(int move)=>move>=AirBladeFirst && move<=AirBladeThird;
        public static bool IsDive(int move)=>move==20 || move==AirSandDive;
        public static bool HasSwordContact(int move)=>move!=SwordGuardReturn && move!=AerialCascade && !IsBodyStrike(move) && move!=LowVault && move!=AirStep && move!=AirFan && move!=WallRebound && move!=DoubleFang && move!=3 && move!=6 && !IsAirBlade(move) && !IsDive(move) && move!=ParryTriple && move!=ThunderDragon && move!=SpiralSword && move!=DodgeDragon;
        public bool IsFinishingCut=>Combo==WheelFinish || Combo==RollFlash || Combo==6 || Combo==14 || Combo==17 || Combo==19 || IsDive(Combo) || Combo==24;
    }
}
