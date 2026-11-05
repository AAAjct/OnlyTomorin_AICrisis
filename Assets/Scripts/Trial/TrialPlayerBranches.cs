namespace TomorinTrial
{
    public sealed partial class TrialPlayer
    {
        // Stable move IDs also drive the replicated rig; no animation events or local input shortcuts.
        public const int AirBladeFirst=26,AirBladeSecond=27,AirBladeThird=28,AirSandDive=29,SakuraFollowup=30;
        public const int ParryTriple=31,FireDragon=32,ParryDodge=33,BehindThrust=34,SpiralSword=35,DodgeDragon=36;
        static int NextLight(int move)=>move==3?AirBladeFirst:move==AirBladeFirst?AirBladeSecond:move==AirBladeSecond?AirBladeThird:move==AirBladeThird?AirSandDive:move<3?move+1:0;
        static int LightHeavyBranch(int move)=>move==3 || IsAirBlade(move)?20:move==8?25:move==16?15:move<3?move+1:0;
        static int HeavyLightBranch(int move)=>move==20?SakuraFollowup:move==5 || move==25?22:move==6 || move==19?23:16;
        public static bool IsAirBlade(int move)=>move>=AirBladeFirst && move<=AirBladeThird;
        public static bool IsDive(int move)=>move==20 || move==AirSandDive;
        public static bool HasSwordContact(int move)=>move!=3 && move!=6 && !IsAirBlade(move) && !IsDive(move) && move!=ParryTriple && move!=FireDragon && move!=SpiralSword && move!=DodgeDragon;
        public bool IsFinishingCut=>Combo==6 || Combo==14 || Combo==17 || Combo==19 || IsDive(Combo) || Combo==24;
    }
}
