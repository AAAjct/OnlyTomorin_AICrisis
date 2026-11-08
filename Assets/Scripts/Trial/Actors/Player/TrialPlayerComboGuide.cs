using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialPlayer
    {
        public static string SwordMoveName(bool heavy,int move)=>move switch
        {
            0=>heavy?"蓄势横劈":"起手挑斩",1=>"横斩",2=>"进步直刺",3=>"空跃",
            4=>"反向斜劈",5=>"樱花突刺",6=>"后撤三月刃",9=>"闪身横斩",
            11=>"架势直刺",12=>"奔跑横斩",14=>"破势处决",17=>"蓄力下劈",
            18=>"反向上挑",19=>"重劈终结",20=>"插剑落地",22=>"收刀斩",
            AirBladeFirst=>"空中剑气一",AirBladeSecond=>"空中剑气二",AirBladeThird=>"空中剑气三",
            AirSandDive=>"俯冲落地",SakuraFollowup=>"追击樱花突刺",
            ParryTriple=>"格挡三连刺",ThunderDragon=>"雷吼炮·雷龙",BehindThrust=>"背闪突刺",SpiralSword=>"投刀回旋",DodgeDragon=>"后跃雷吼炮·雷龙",
            FlowingSweep=>"回身横扫",RisingBridge=>"进步上挑",MoonChase=>"月刃追刺",
            RollThrust=>"翻滚前刺",RollFlash=>"翻滚一闪",CrossDraw=>"交错斩",LowReap=>"低扫",
            DoubleFang=>"二连斩",WheelFinish=>"回旋终结",RetreatDraw=>"后撤抽刀",ThreadPierce=>"穿针突刺",
            AirStep=>"小跃",AirCross=>"空中交错",AirWheel=>"空中回旋",AirFan=>"三连飞刃",WallRebound=>"蹬墙借步",
            GatheredPierce=>"蓄势穿针",StormWheel=>"风轮",GuardNeedle=>"反架针刺",GuardBloom=>"散华扇刃",AirPierce=>"浮空穿针",
            HiltStrike=>"剑柄击",HeelTurn=>"扫腿收势",LowVault=>"低跃",VaultSweep=>"腾身横扫",
            OrbitCut=>"绕身回刀",FallingDraw=>"落地抽刀",ElbowBreak=>"贴身肘击",ScissorKick=>"空中剪脚",
            SheathTurn=>"纳刀收势",CloudRake=>"云回挑斩",LoftingDraw=>"挑空抽刀",AerialCascade=>"空中三飞刃",
            SwordRecallCut=>"追剑收斩",SwordRecallRise=>"追剑升龙",SwordGuardReturn=>"接刀返斩",ParryMirror=>"影返交错",ParryAscend=>"踏空回斩",
            _=>"剑招"
        };

        public string ComboGuideTitle
        {
            get
            {
                if(SwordGuardReturning)return "投刀完美格挡 · 接刀返斩";
                if(ParryDodgeFollowup)return "完美格挡 · 远闪追击";
                if(ParryEmpowered)return "完美格挡 · 选择反击";
                if(weavePending)return "格挡变招 · "+SwordMoveName(GuardComboHeavy(GuardFollowupMove),GuardFollowupMove);
                if(Action==FighterAction.Charge)return Combo==ThunderDragon?"强化 · 雷吼炮·雷龙蓄力":"蓄力 · 松开重击出招";
                if(CanLinkDodgeAttack)return "闪避 · 选择追击";
                if(LandingSakuraReady)return "落地 · 樱花追击";
                bool attacking=Action==FighterAction.Light || Action==FighterAction.Heavy;
                if(!attacking && Time.time>=chainUntil)return "剑术 · 选择起手";
                bool heavy=attacking?Action==FighterAction.Heavy:chainHeavy;
                string route=Combo==ParryMirror || Combo==ParryAscend?"镜返":!heavy && Combo<=2?$"地面 {Combo+1}/4":Combo==WheelFinish?"地面 4/4":heavy && (Combo==0 || Combo==18 || Combo==19)?$"重击 {(Combo==0?1:Combo==18?2:3)}/3":IsAerialMove(Combo)?"空中":"派生";
                return route+" · "+SwordMoveName(heavy,Combo);
            }
        }

        public string ComboGuideHint(string light,string heavy,string jump,string guard="格挡")
        {
            if(SwordGuardReturning)return "瞬移接刀 → 快速返斩至投刀起点";
            if(AutoSwordThrowGuard)return "投刀自动格挡 · 完美格挡后瞬移接刀返斩\n"+light+" 追剑收斩 / "+heavy+" 追剑升龙";
            if(ParryDodgeFollowup)return light+" 背闪突刺 / "+heavy+" 回旋刀";
            if(ParryEmpowered)return light+" 三连刺 / "+heavy+" 雷吼炮·雷龙 / 闪避远闪";
            if(Time.time<blinkReadyUntil)return light+" 闪身横斩 / "+heavy+" 后跃雷吼炮·雷龙";
            if(Action==FighterAction.Charge)return $"蓄力 {ChargeAmount*100:0}% · 松开释放 · 格挡 / 闪避取消";
            if(CanLinkDodgeAttack)return light+" 前刺 / "+heavy+" 一闪 / "+guard+" 投刀";
            if(LandingSakuraReady)return light+" → 追击樱花突刺 / "+heavy+" 重新起手\n触地轻击直接续接 · 松开停止";
            if(Action==FighterAction.Stagger || Action==FighterAction.Heal)return "动作结束后重新起手";
            if(SwordRecallReady && !weavePending)return light+" → 追剑收斩 / "+heavy+" → 追剑升龙\n瞬移至飞剑实际位置 · 收剑后继续连招";
            if(weavePending)return "架势完成后自动出招 · 无需补按攻击\n长按轻 / 重可接后续 · 闪避取消";
            bool attacking=Action==FighterAction.Light || Action==FighterAction.Heavy;
            if(!attacking && Time.time>=chainUntil)return "长按 "+light+" 地面四连 / "+heavy+" 重击三连\n"+jump+" 起跳 · 连段中 "+guard+" 直接变招";
            bool fromHeavy=attacking?Action==FighterAction.Heavy:chainHeavy;
            int l=fromHeavy?HeavyLightBranch(Combo):NextLight(Combo),h=fromHeavy?HeavyHeavyBranch(Combo):LightHeavyBranch(Combo);
            int guardMove=GuardComboMove(fromHeavy,Combo,IsAerialMove(Combo) && transform.position.y>launchFloor+.35f);
            string guardHint="\n"+guard+" → "+SwordMoveName(GuardComboHeavy(guardMove),guardMove);
            if(l<0 && h<0)return "终结 · 松开停止 / 长按重新起手"+guardHint;
            string phase=!attacking?"可续接":Progress>(fromHeavy?HeavyLinkProgress(Combo):LightLinkProgress(Combo))?"可接招":"可预输入";
            return phase+" · "+(l>=0?light+" → "+SwordMoveName(false,l):"轻击收招")+" / "+(h>=0?heavy+" → "+SwordMoveName(true,h):"重击收招")+guardHint;
        }
    }
}
