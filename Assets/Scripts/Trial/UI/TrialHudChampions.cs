using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialHud
    {
        int championNameKind=-1,championNamePhase=-1,championNameParty=-1,championKills=-1;
        int championSkillKind=-1,championSkillIndex=-99,championSkillMode=-1;
        string championNameCache,championSkillCache;
        RectTransform twinBars;
        UnityEngine.UI.Image twinFeibiHealth,twinNuonuoHealth,twinFeibiPosture,twinNuonuoPosture;
        string ChampionName(TrialEnemy enemy)
        {
            var brain=enemy.Champion;int kind=(int)brain.Kind;
            if(championNameKind!=kind || championNamePhase!=brain.Phase || championNameParty!=game.PartySize || championKills!=brain.KillStacks)
            {
                championNameKind=kind;championNamePhase=brain.Phase;championNameParty=game.PartySize;
                championKills=brain.KillStacks;
                championNameCache=(game.Level==TrialLevel.Twins?"菲比 ＆ 糯糯 · 双生莲庭":enemy.DisplayName)+" · "+(brain.Phase==3?"决意":brain.Phase==2?"解放":"初式")+(game.PartySize>1?$" · {game.PartySize} 人难度":"");
                if(brain.IsRemnant && brain.KillStacks>0)championNameCache+=$" · 吞噬 {brain.KillStacks}";
            }
            return championNameCache;
        }
        string ChampionSkill(TrialEnemy enemy)
        {
            var brain=enemy.Champion;int kind=(int)brain.Kind,mode=brain.WindingUp?1:brain.Recovering?2:enemy.Guarding?4:brain.Regrouping?5:brain.SkillName.Length>0?3:0;
            if(kind!=championSkillKind || brain.MoveIndex!=championSkillIndex || mode!=championSkillMode)
            {
                championSkillKind=kind;championSkillIndex=brain.MoveIndex;championSkillMode=mode;
                championSkillCache=brain.SkillName+(mode==1?" · "+brain.Counter:mode==2?" · 收招破绽":"");
            }
            return championSkillCache;
        }
        bool UpdateTwinBars()
        {
            bool twins=game.Level==TrialLevel.Twins;
            bossHealth.transform.parent.gameObject.SetActive(!twins);
            posture.transform.parent.gameObject.SetActive(!twins);
            if(twinBars)twinBars.gameObject.SetActive(twins);
            if(!twins)return false;
            if(!twinBars)
            {
                twinBars=Panel("Twin boss bars",bossPanel,new Vector2(0,1),Vector2.zero,new Vector2(880,112),Color.clear);
                (twinFeibiHealth,twinFeibiPosture)=BuildTwinBars("菲比",ChampionKind.FeiBi,0);
                (twinNuonuoHealth,twinNuonuoPosture)=BuildTwinBars("糯糯",ChampionKind.NuoNuo,450);
            }
            foreach(var enemy in game.Enemies)
            {
                if(!enemy || !enemy.Champion)continue;
                switch(enemy.Champion.Kind)
                {
                    case ChampionKind.FeiBi: FillTwinBars(enemy,twinFeibiHealth,twinFeibiPosture);break;
                    case ChampionKind.NuoNuo: FillTwinBars(enemy,twinNuonuoHealth,twinNuonuoPosture);break;
                }
            }
            return true;
        }

        (UnityEngine.UI.Image health,UnityEngine.UI.Image posture) BuildTwinBars(string label,ChampionKind kind,float x)
        {
            Label(label,twinBars,label,new Vector2(0,1),new Vector2(x,-57),new Vector2(430,20),15,ivory,TextAnchor.MiddleCenter);
            var health=Bar(kind+" health",twinBars,new Vector2(x,-81),new Vector2(430,8),TrialChampionCatalog.Color(kind));
            var posture=Bar(kind+" posture",twinBars,new Vector2(x,-96),new Vector2(430,5),gold);
            return (health,posture);
        }

        static void FillTwinBars(TrialEnemy enemy,UnityEngine.UI.Image health,UnityEngine.UI.Image posture)
        {
            health.fillAmount=enemy.Health/enemy.MaxHealth;
            posture.fillAmount=enemy.Alive?enemy.Posture/enemy.PostureLimit:0;
        }
    }
}
