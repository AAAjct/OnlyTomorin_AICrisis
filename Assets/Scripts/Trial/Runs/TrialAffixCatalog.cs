namespace TomorinTrial
{
    public enum TrialAffix { Vitality, Stamina, Edge, Breaker, Haste, Recovery, Armor, Critical, Execution, Experience, Regeneration, Leech, BloodHarvest, SecondWind, FlaskPower, Vacuum, Shockwave, ChainLightning, Frost, Burn, KillBlast, Thorns, ParryHeal, DodgeNova, HeavyImpact, Momentum, Berserker, LastBastion, Reach, EliteHunter, Count }
    public static class TrialAffixCatalog
    {
        static readonly string[] names={"长生","定心","锋芒","破阵","疾行","回气","铁衣","会心","斩决","悟道","春生","饮血","收割","乘胜","甘露","归墟引","震剑","连锁雷鸣","寒刃","业火","殉爆","荆棘","返生格挡","闪身雷环","重锤刀势","势如破竹","狂战","背水","长锋","讨伐者"};
        static readonly string[] descriptions={"最大气血 +25，立即恢复 25","最大气力 +50，立即恢复 50","所有攻击伤害 +10%","架势伤害 +15%","移动速度 +6%","气力自然恢复 +12%","受到伤害降低 6%（最高 48%）","每层增加 8% 暴击率，暴击伤害 160%","对生命低于 30% 的敌人伤害 +20%","击杀经验 +15%","每秒恢复 0.6 气血","攻击命中恢复 1 气血（每 0.25 秒）","击杀恢复 4 气血","击杀额外恢复 25 气力","药瓶额外恢复 18 气血","普通轻击命中，将 6 米内小怪拉向命中点（1 秒冷却）","普通轻击命中产生 3.5 米冲击波（1.2 秒冷却）","命中向附近最多 3 个敌人传导闪电（1 秒冷却）","命中减速小怪 18%，持续 2 秒（最高 54%）","命中灼烧敌人 4 秒，每秒 5 点伤害","击杀在 4 米内引爆，造成每层 24 点伤害","受伤时反伤攻击者 18 点（0.5 秒冷却）","完美弹反恢复 8 气血","闪避产生 4 米冲击波（2 秒冷却）","重击伤害与架势伤害 +15%","每次击杀增伤 3%，持续 5 秒，最多 10 层","气血低于 50% 时伤害 +15%","气血低于 30% 时额外减伤 12%","近战刀剑范围 +8%（最高 40%）","对 Boss 伤害 +12%"};
        public static string Name(TrialAffix affix)=>names[(int)affix];
        public static string Description(TrialAffix affix)=>descriptions[(int)affix];
        public static int MaxStacks(TrialAffix affix)=>affix==TrialAffix.Vitality || affix==TrialAffix.Stamina || affix==TrialAffix.Edge?0:affix==TrialAffix.Frost?3:affix==TrialAffix.Armor?8:affix==TrialAffix.Critical?10:5;
    }
}
