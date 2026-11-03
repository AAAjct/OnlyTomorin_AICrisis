using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialHud
    {
        int championNameKind=-1,championNamePhase=-1,championNameParty=-1;
        int championSkillKind=-1,championSkillIndex=-99,championSkillMode=-1;
        string championNameCache,championSkillCache;
        UnityEngine.UI.Image twinFeibiHealth,twinNuonuoHealth;
        string ChampionName(TrialEnemy enemy)
        {
            var brain=enemy.Champion;int kind=(int)brain.Kind;
            if(championNameKind!=kind || championNamePhase!=brain.Phase || championNameParty!=game.PartySize)
            {
                championNameKind=kind;championNamePhase=brain.Phase;championNameParty=game.PartySize;
                championNameCache=(game.Level==TrialLevel.Twins?"菲比 ＆ 糯糯 · 双生莲庭":enemy.DisplayName)+" · "+(brain.Phase==3?"决意":brain.Phase==2?"解放":"初式")+(game.PartySize>1?$" · {game.PartySize} 人难度":"");
            }
            return championNameCache;
        }
        string ChampionSkill(TrialEnemy enemy)
        {
            var brain=enemy.Champion;int kind=(int)brain.Kind,mode=brain.WindingUp?1:brain.Recovering?2:brain.SkillName.Length>0?3:0;
            if(kind!=championSkillKind || brain.MoveIndex!=championSkillIndex || mode!=championSkillMode)
            {
                championSkillKind=kind;championSkillIndex=brain.MoveIndex;championSkillMode=mode;
                championSkillCache=brain.SkillName+(mode==1?" · "+brain.Counter:mode==2?" · 收招破绽":"");
            }
            return championSkillCache;
        }
        void UpdateTwinBars()
        {
            if(game.Level!=TrialLevel.Twins)return;
            if(!twinFeibiHealth)
            {
                twinFeibiHealth=Bar("Feibi health",bossPanel,new Vector2(0,-36),new Vector2(430,8),TrialChampionCatalog.Color(ChampionKind.FeiBi));
                twinNuonuoHealth=Bar("Nuonuo health",bossPanel,new Vector2(450,-36),new Vector2(430,8),TrialChampionCatalog.Color(ChampionKind.NuoNuo));
                bossHealth.transform.parent.gameObject.SetActive(false);
            }
            twinFeibiHealth.fillAmount=game.Enemies[0].Health/game.Enemies[0].MaxHealth;
            twinNuonuoHealth.fillAmount=game.Enemies[1].Health/game.Enemies[1].MaxHealth;
        }
    }
}
