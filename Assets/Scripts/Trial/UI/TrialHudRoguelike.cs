using UnityEngine;
using UnityEngine.UI;

namespace TomorinTrial
{
    public sealed partial class TrialHud
    {
        GameObject runChoicePanel;
        Text runHeading,runProgress,experienceLabel,upgradeAvailableLabel;
        Image experienceBar;
        Button upgradeButton;
        readonly Button[] runButtons=new Button[3];
        int shownRunLevel=-1,shownPending=-1;
        string runProgressCache;
        int experienceShown=-1,experienceLevelShown=-1;
        int upgradeCountShown=-1,upgradeBindingShown=-1;
        bool upgradePadShown,upgradeTouchShown;
        void UpdateRoguelikeHud()
        {
            if(!game.IsRunStage)return;
            if(!runProgress)
            {
                runProgress=Label("Run progress",root,"",new Vector2(.5f,1),new Vector2(-430,-18),new Vector2(860,40),21,gold,TextAnchor.MiddleCenter);
                if(game.IsEndless)
                {
                    var vitals=stats.GetComponent<RectTransform>();vitals.sizeDelta=new Vector2(480,145);
                    experienceBar=Bar("Experience",vitals,new Vector2(96,-86),new Vector2(310,7),new Color(.25f,.75f,.85f));
                    hpText.rectTransform.anchoredPosition=new Vector2(96,-105);
                    experienceLabel=Label("Experience count",vitals,"",new Vector2(0,1),new Vector2(224,-105),new Vector2(182,19),12,new Color(.35f,.8f,.9f),TextAnchor.UpperRight);
                    upgradeAvailableLabel=Label("Upgrade available",vitals,"可升级",new Vector2(0,1),new Vector2(416,-105),new Vector2(64,19),12,gold);
                    upgradeButton=Button("Open upgrade",root,new Vector2(540,180),new Vector2(225,48),"",()=>game.Player.RequestRunUpgrade());
                    var rect=upgradeButton.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=Vector2.zero;
                    if(touchRoot){rect.SetParent(touchRoot,false);rect.anchorMin=rect.anchorMax=new Vector2(1,0);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(-320,640);rect.sizeDelta=new Vector2(164,86);}
                }
            }
            runProgress.gameObject.SetActive(game.IsEndless && game.Mode==TrialMode.Playing);
            objectiveText.gameObject.SetActive(false);
            if(experienceBar)experienceBar.transform.parent.gameObject.SetActive(game.Mode==TrialMode.Playing);
            string caption=game.Run.Status;
            if(caption!=runProgressCache){runProgressCache=caption;runProgress.text=caption;}
            if(experienceBar)experienceBar.fillAmount=(float)game.Player.RunExperience/game.Player.ExperienceRequired;
            if(experienceLabel && (experienceShown!=game.Player.RunExperience || experienceLevelShown!=game.Player.RunLevel))
            {experienceShown=game.Player.RunExperience;experienceLevelShown=game.Player.RunLevel;experienceLabel.text=$"Lv.{game.Player.RunLevel} · {game.Player.RunExperience}/{game.Player.ExperienceRequired} EXP";}
            if(upgradeAvailableLabel)upgradeAvailableLabel.gameObject.SetActive(!touchRoot && game.Player.Alive && game.Player.PendingRunChoices>0 && game.Mode==TrialMode.Playing && !game.Player.ChoosingRunAffix);
            if(upgradeButton)
            {
                if(touchRoot && upgradeButton.transform.parent!=touchRoot)
                {var rect=upgradeButton.GetComponent<RectTransform>();rect.SetParent(touchRoot,false);rect.anchorMin=rect.anchorMax=new Vector2(1,0);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(-320,640);rect.sizeDelta=new Vector2(164,86);}
                bool available=touchRoot && game.Player.PendingRunChoices>0 && game.Mode==TrialMode.Playing && !game.Player.ChoosingRunAffix;
                upgradeButton.gameObject.SetActive(available);
                if(upgradeCountShown!=game.Player.PendingRunChoices || upgradeBindingShown!=TrialBindings.Version || upgradePadShown!=game.Input.UsingGamepad || upgradeTouchShown!=(bool)touchRoot)
                {
                    upgradeCountShown=game.Player.PendingRunChoices;upgradeBindingShown=TrialBindings.Version;upgradePadShown=game.Input.UsingGamepad;upgradeTouchShown=touchRoot;
                    upgradeButton.GetComponentInChildren<Text>().text=touchRoot?$"升级 ×{upgradeCountShown}":$"升级 ×{upgradeCountShown}  [{TrialBindings.Display("Upgrade",upgradePadShown?2:0)}]";
                }
            }
            bool choosing=game.Player.ChoosingRunAffix && game.Mode==TrialMode.Playing;
            if(choosing && !runChoicePanel)
            {
                var overlay=Panel("Level up choices",root,Vector2.zero,Vector2.zero,Vector2.zero,new Color(.015f,.025f,.035f,.91f));
                overlay.anchorMax=Vector2.one;overlay.offsetMin=overlay.offsetMax=Vector2.zero;overlay.GetComponent<Image>().raycastTarget=true;runChoicePanel=overlay.gameObject;
                var card=Panel("Affix cards",overlay,new Vector2(.5f,.5f),new Vector2(-570,240),new Vector2(1140,480),black);
                TrialModalBackdrop.Bind(overlay,card,()=>game.Player.RequestCloseRunUpgrade());
                runHeading=Label("Level up",card,"",new Vector2(0,1),new Vector2(35,-25),new Vector2(1070,60),34,gold);
                Label("Choice help",card,TrialNetworkManager.SessionActive?"选择一项强化 · 同名词条可叠加 · 联机选择期间战斗继续":"选择一项强化 · 同名词条可叠加 · 选择期间战斗暂停",new Vector2(0,1),new Vector2(35,-97),new Vector2(1070,40),20,ivory);
                for(int i=0;i<3;i++)
                {
                    int slot=i;runButtons[i]=Button("Affix "+i,card,new Vector2(35+i*365,-168),new Vector2(340,265),"",()=>game.Player.RequestRunAffix(slot));
                    var label=runButtons[i].GetComponentInChildren<Text>();label.fontSize=22;label.alignment=TextAnchor.MiddleCenter;
                    var nav=new Navigation{mode=Navigation.Mode.Explicit};runButtons[i].navigation=nav;
                }
                for(int i=0;i<3;i++){var nav=runButtons[i].navigation;nav.selectOnLeft=runButtons[(i+2)%3];nav.selectOnRight=runButtons[(i+1)%3];runButtons[i].navigation=nav;}
            }
            if(runChoicePanel)runChoicePanel.SetActive(choosing);
            if(choosing)
            {
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
                UpdateChoiceFocus(runButtons);
                if(shownRunLevel!=game.Player.RunLevel || shownPending!=game.Player.PendingRunChoices)
                {
                    shownRunLevel=game.Player.RunLevel;shownPending=game.Player.PendingRunChoices;
                    runHeading.text=$"升级 · Lv.{shownRunLevel} · 词条三选一"+(shownPending>1?$"（待选 {shownPending} 次）":"");
                    for(int i=0;i<3;i++){var affix=game.Player.RunOffers[i];runButtons[i].GetComponentInChildren<Text>().text=TrialAffixCatalog.Name(affix)+$"  {game.Player.AffixStacks(affix)} → {game.Player.AffixStacks(affix)+1}"+"\n\n"+TrialAffixCatalog.Description(affix);}
                    runButtons[0].Select();
                }
            }
            else {shownPending=-1;shownRunLevel=-1;}
            if(game.Mode==TrialMode.Dead)menuCaption.text=game.IsEndless?$"抵达第 {game.Run.Wave} 波 · Lv.{game.Player.RunLevel} · 击杀 {game.Run.Kills}":$"已击破 {game.Run.BossesDefeated} / {TrialEndlessRun.BossCount} 位 Boss";
        }
    }
}
