using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialPlayer
    {
        // Stable move IDs also drive the replicated rig; no animation events or local input shortcuts.
        public const int AirBladeFirst=26,AirBladeSecond=27,AirBladeThird=28,AirSandDive=29,SakuraFollowup=30;
        public const int ParryTriple=31,FireDragon=32,ParryDodge=33,BehindThrust=34,SpiralSword=35,DodgeDragon=36;
        public const int FlowingSweep=37,RisingBridge=38,MoonChase=39;
        public const int RollThrust=40,RollFlash=41,CrossDraw=42,LowReap=43,DoubleFang=44,WheelFinish=45,RetreatDraw=46,ThreadPierce=47;
        public const int AirStep=48,AirCross=49,AirWheel=50,AirFan=51,WallRebound=52;
        public const int GatheredPierce=53,StormWheel=54,GuardNeedle=55,GuardBloom=56,AirPierce=57;
        public const int HiltStrike=58,HeelTurn=59,LowVault=60,VaultSweep=61,OrbitCut=62,FallingDraw=63,ElbowBreak=64,ScissorKick=65,SheathTurn=66,CloudRake=67;
        public const int LoftingDraw=68,AerialCascade=69;
        public static bool IsMartialMove(int move)=>move>=HiltStrike && move<=LoftingDraw;
        public static bool IsBodyStrike(int move)=>move==HiltStrike || move==HeelTurn || move==ElbowBreak || move==ScissorKick;
        public static bool IsMartialAir(int move)=>move==LoftingDraw || move==LowVault || move==VaultSweep || move==ScissorKick || move==CloudRake;
        public static bool IsLowAirMove(int move)=>move==AerialCascade || IsMartialAir(move) || move==AirPierce || move>=AirStep && move<=WallRebound;
        public static bool IsExtendedMove(int move)=>move>=RollThrust && move<=ThreadPierce || move>=GatheredPierce && move<=GuardBloom;
        const float ChainGrace=.55f;
        // Two future presses are enough for L,L,H. Consume only the oldest press;
        // contact holds never age the queue, and a committed move renews the next lease.
        struct AttackPress {public bool heavy,held;}
        readonly Queue<AttackPress> attackInputs=new Queue<AttackPress>(2);
        float attackInputAge;
        void CollectAttackInputs(float dt,TrialInput input)
        {
            if(!CombatFrozen && attackInputs.Count>0)
            {
                attackInputAge+=dt;
                bool attacking=Action==FighterAction.Light || Action==FighterAction.Heavy;
                float lease=attacking?Mathf.Max(InputBuffer,ActionDuration*(Action==FighterAction.Light?LightLinkProgress(Combo):HeavyLinkProgress(Combo))+.15f):InputBuffer;
                if(attackInputAge>lease)ClearAttackInputs();
            }
            if(input.Attack)QueueAttack(false);
            if(input.Heavy)QueueAttack(true);
            CollectHeldAttack(dt,input);
        }
        void QueueAttack(bool heavy,bool held=false)
        {
            if(attackInputs.Count>=2)return;
            if(attackInputs.Count==0)attackInputAge=0;
            attackInputs.Enqueue(new AttackPress{heavy=heavy,held=held});
        }
        bool AttackQueued(bool heavy)=>attackInputs.Count>0 && attackInputs.Peek().heavy==heavy;
        void ConsumeAttackInput()
        {if(attackInputs.Count>0)attackInputs.Dequeue();attackInputAge=0;}
        void ClearAttackInputs()
        {attackInputs.Clear();attackInputAge=0;bufferedLight=bufferedHeavy=-10;}
        static int NextLight(int move)=>move switch
        {
            0=>1,1=>2,2=>3,3=>AirBladeFirst,
            LoftingDraw=>VaultSweep,AerialCascade=>CloudRake,
            HiltStrike=>HeelTurn,HeelTurn=>LowVault,LowVault=>VaultSweep,VaultSweep=>ScissorKick,
            ScissorKick=>CloudRake,CloudRake=>FallingDraw,OrbitCut=>SheathTurn,SheathTurn=>HiltStrike,FallingDraw=>22,ElbowBreak=>OrbitCut,
            GatheredPierce=>LowVault,StormWheel=>AirStep,GuardNeedle=>HiltStrike,GuardBloom=>ThreadPierce,AirPierce=>VaultSweep,
            AirBladeFirst=>AirBladeSecond,AirBladeSecond=>AirBladeThird,AirBladeThird=>AirSandDive,
            22=>CrossDraw,23=>2,16=>1,8=>1,9=>1,11=>1,12=>1,
            RollThrust=>CrossDraw,RollFlash=>LowReap,CrossDraw=>HiltStrike,LowReap=>DoubleFang,
            DoubleFang=>WheelFinish,WheelFinish=>AirStep,RetreatDraw=>ThreadPierce,ThreadPierce=>CrossDraw,
            AirStep=>AirCross,AirCross=>AirWheel,AirWheel=>AirFan,AirFan=>AirSandDive,WallRebound=>AirFan,
            AirSandDive=>SakuraFollowup,SakuraFollowup=>22,BehindThrust=>22,ParryTriple=>22,
            FlowingSweep=>2,RisingBridge=>3,MoonChase=>22,_=>0
        };
        // Return final move IDs; mapping twice used to depend on the caller's action state.
        static int LightHeavyBranch(int move)=>move switch
        {
            0=>4,1=>5,2=>6,3=>20,
            LoftingDraw=>AerialCascade,AerialCascade=>20,
            HiltStrike=>ElbowBreak,HeelTurn=>LoftingDraw,LowVault=>ScissorKick,VaultSweep=>AerialCascade,
            ScissorKick=>AerialCascade,CloudRake=>20,OrbitCut=>LowVault,SheathTurn=>CloudRake,FallingDraw=>LowVault,ElbowBreak=>SheathTurn,
            GatheredPierce=>LoftingDraw,StormWheel=>RetreatDraw,GuardNeedle=>GuardBloom,GuardBloom=>AirStep,AirPierce=>AerialCascade,
            RollThrust=>RollFlash,CrossDraw=>LoftingDraw,RollFlash or LowReap=>AirStep,
            DoubleFang=>ThreadPierce,WheelFinish=>RetreatDraw,RetreatDraw=>6,ThreadPierce=>AirStep,
            AirStep=>WallRebound,AirCross or AirWheel=>AerialCascade,AirFan or WallRebound=>20,
            AirBladeFirst or AirBladeSecond or AirBladeThird=>20,
            8 or 11 or ParryTriple=>25,16=>15,
            AirSandDive=>SakuraFollowup,22 or SakuraFollowup or BehindThrust or MoonChase=>FlowingSweep,
            23 or FlowingSweep=>RisingBridge,9 or 12=>24,RisingBridge=>6,_=>18
        };
        static int HeavyLightBranch(int move)=>move switch
        {
            20 or AirSandDive=>SakuraFollowup,5 or 25 or SakuraFollowup=>22,
            LoftingDraw=>VaultSweep,AerialCascade=>CloudRake,
            HiltStrike=>HeelTurn,HeelTurn=>LowVault,LowVault=>VaultSweep,VaultSweep=>ScissorKick,
            ScissorKick=>CloudRake,CloudRake=>FallingDraw,OrbitCut=>SheathTurn,SheathTurn=>HiltStrike,FallingDraw=>22,ElbowBreak=>OrbitCut,
            GatheredPierce=>LowVault,StormWheel=>AirStep,GuardNeedle=>HiltStrike,GuardBloom=>ThreadPierce,AirPierce=>VaultSweep,
            RollThrust=>CrossDraw,RollFlash=>LowReap,CrossDraw=>HiltStrike,LowReap=>DoubleFang,
            DoubleFang=>WheelFinish,WheelFinish=>CrossDraw,RetreatDraw=>ThreadPierce,ThreadPierce=>CrossDraw,
            AirStep=>AirCross,AirCross=>AirWheel,AirWheel=>AirFan,AirFan=>AirSandDive,WallRebound=>AirFan,
            6 or FireDragon or SpiralSword=>MoonChase,19 or 18=>23,
            4 or 21 or 24 or 15=>FlowingSweep,13 or 17 or DodgeDragon=>RisingBridge,
            FlowingSweep=>2,RisingBridge=>3,MoonChase=>22,_=>16
        };
        static int HeavyHeavyBranch(int move)=>move switch
        {
            0=>18,18=>19,19=>WheelFinish,4=>21,21 or FlowingSweep or 13 or 17 or 24 or DodgeDragon=>RisingBridge,
            LoftingDraw=>AerialCascade,AerialCascade=>20,
            HiltStrike=>ElbowBreak,HeelTurn=>LoftingDraw,LowVault=>ScissorKick,VaultSweep=>AerialCascade,
            ScissorKick=>AerialCascade,CloudRake=>20,OrbitCut=>LowVault,SheathTurn=>CloudRake,FallingDraw=>LowVault,ElbowBreak=>SheathTurn,
            GatheredPierce=>LoftingDraw,StormWheel=>RetreatDraw,GuardNeedle=>GuardBloom,GuardBloom=>AirStep,AirPierce=>AerialCascade,
            RollThrust=>RollFlash,CrossDraw=>LoftingDraw,RollFlash or LowReap=>AirStep,
            DoubleFang=>ThreadPierce,WheelFinish=>RetreatDraw,RetreatDraw=>6,ThreadPierce=>AirStep,
            AirStep=>WallRebound,AirCross or AirWheel=>AerialCascade,AirFan or WallRebound=>20,
            5 or 25 or SakuraFollowup or MoonChase=>FlowingSweep,
            6 or FireDragon or SpiralSword=>MoonChase,RisingBridge=>6,
            20 or AirSandDive=>SakuraFollowup,_=>18
        };
        static float HeavyLinkProgress(int move)=>move==AerialCascade?.80f:move==FallingDraw?.72f:move==AirFan?.78f:move==DoubleFang?.74f:move==ParryTriple?.70f:move==6?.70f:move==DodgeDragon?.84f:move==FireDragon?.62f:move==SpiralSword?.55f:.66f;
        public static bool IsAirBlade(int move)=>move>=AirBladeFirst && move<=AirBladeThird;
        public static bool IsDive(int move)=>move==20 || move==AirSandDive;
        public static bool HasSwordContact(int move)=>move!=AerialCascade && !IsBodyStrike(move) && move!=LowVault && move!=AirStep && move!=AirFan && move!=WallRebound && move!=DoubleFang && move!=3 && move!=6 && !IsAirBlade(move) && !IsDive(move) && move!=ParryTriple && move!=FireDragon && move!=SpiralSword && move!=DodgeDragon;
        public bool IsFinishingCut=>Combo==WheelFinish || Combo==RollFlash || Combo==6 || Combo==14 || Combo==17 || Combo==19 || IsDive(Combo) || Combo==24;
    }
}
