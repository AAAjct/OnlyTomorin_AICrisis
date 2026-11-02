using UnityEngine;

namespace TomorinTrial
{
    public enum TrialBoon : byte { None, Vitality, Edge, Resolve, LightStep, SpiritDew, Breaker }
    public enum TrialBossBoon : byte { None, IronScales, RagingTide, SwiftCurrent }
    public static class TrialRunBoons
    {
        public static string Name(TrialBoon boon)
        {
            switch(boon)
            {case TrialBoon.Vitality:return "长生 · 气血";case TrialBoon.Edge:return "锋芒 · 刀势";case TrialBoon.Resolve:return "定心 · 气力";case TrialBoon.LightStep:return "轻身 · 闪避";case TrialBoon.SpiritDew:return "甘露 · 疗愈";case TrialBoon.Breaker:return "破阵 · 架势";default:return "未选择";}
        }
        public static string Description(TrialBoon boon)
        {
            switch(boon)
            {case TrialBoon.Vitality:return "最大气血 +20";case TrialBoon.Edge:return "刀剑伤害 +12%";case TrialBoon.Resolve:return "最大气力 +15";case TrialBoon.LightStep:return "闪避消耗降低 4 点气力";case TrialBoon.SpiritDew:return "每次灵露多恢复 12 点气血";case TrialBoon.Breaker:return "造成的架势伤害 +15%";default:return "";}
        }
        public static string BossName(TrialBossBoon boon)=>boon==TrialBossBoon.IronScales?"铁鳞 · 减伤 8%":boon==TrialBossBoon.RagingTide?"怒潮 · 伤害 +8%":boon==TrialBossBoon.SwiftCurrent?"迅流 · 技能速度 +6%":"";
    }
    public sealed partial class TrialPlayer
    {
        public TrialBoon Boon {get;private set;}
        public TrialBoon OfferA {get;private set;}
        public TrialBoon OfferB {get;private set;}
        public TrialBoon OfferC {get;private set;}
        public bool ChoosingBoon=>Boon==TrialBoon.None && OfferA!=TrialBoon.None && !game.IsArena;
        public float DodgeCost=>Boon==TrialBoon.LightStep?21:25;
        public void RollRunBoons()
        {
            if(game.IsArena || Boon!=TrialBoon.None || OfferA!=TrialBoon.None)return;
            OfferA=(TrialBoon)Random.Range(1,7);
            do{OfferB=(TrialBoon)Random.Range(1,7);}while(OfferB==OfferA);
            do{OfferC=(TrialBoon)Random.Range(1,7);}while(OfferC==OfferA || OfferC==OfferB);
        }
        public bool ChooseBoon(TrialBoon choice)
        {
            if(!ChoosingBoon || choice==TrialBoon.None || choice!=OfferA && choice!=OfferB && choice!=OfferC)return false;
            Boon=choice;MaxHealth=choice==TrialBoon.Vitality?180:160;Health=MaxHealth;
            MaxStamina=BaseStamina+(choice==TrialBoon.Resolve?15:0);Stamina=MaxStamina;
            if(this==game.Player)game.FinishBoonChoice();return true;
        }
        public void ResetRunBoon(){Boon=OfferA=OfferB=OfferC=TrialBoon.None;MaxHealth=160;MaxStamina=BaseStamina;}
    }
}
