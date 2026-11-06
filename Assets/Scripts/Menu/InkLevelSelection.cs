using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TomorinTrial
{
    public sealed partial class InkMainMenu
    {
        GameObject levelList;
        UnityEngine.UI.ScrollRect levelScroll;
        RectTransform levelContent;
        bool listForNetwork;
        UnityEngine.UI.Button courtyardRow,arenaRow,endlessRow,bossRushRow;
        UnityEngine.UI.Text listTitle;
        GameObject selectionReturn;
        TrialLevel soloLevel;
        readonly UnityEngine.UI.Button[] difficultyButtons=new UnityEngine.UI.Button[3];
        UnityEngine.UI.Text difficultyHint;

        void RefreshDifficultySelection()
        {
            var selected=listForNetwork?manager.SelectedDifficulty:TrialDifficultySettings.Solo;
            var choices=new[]{TrialDifficulty.Easy,TrialDifficulty.Normal,TrialDifficulty.Hard};
            for(int i=0;i<choices.Length;i++)
            {
                var text=difficultyButtons[i].GetComponentInChildren<UnityEngine.UI.Text>();
                text.text=(choices[i]==selected?"● ":"")+TrialDifficultySettings.Label(choices[i]);
            }
            difficultyHint.text=selected==TrialDifficulty.Easy?"敌人生命 75% · 伤害 70%":selected==TrialDifficulty.Hard?"敌人生命 130% · 伤害 130%":"敌人生命与伤害 100%";
        }
        readonly System.Collections.Generic.List<UnityEngine.UI.Button> challengeRows=new System.Collections.Generic.List<UnityEngine.UI.Button>();

        void OpenLevelList(bool multiplayerList)
        {
            if(multiplayerList && manager.Busy && !manager.IsHost)return;
            if(!levelList)
            {
                var overlay=Panel("Shared level list",root,0,0,1600,900,new Color(.03f,.04f,.04f,.7f));Stretch(overlay);overlay.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;levelList=overlay.gameObject;
                var card=Panel("Level list card",overlay,0,0,900,830,Paper);card.anchorMin=card.anchorMax=card.pivot=new Vector2(.5f,.5f);card.anchoredPosition=Vector2.zero;
                Line(card,0,0,3,830,Red);listTitle=Label(card,"选择关卡",32,20,820,58,32,Ink);
                var choices=new[]{TrialDifficulty.Easy,TrialDifficulty.Normal,TrialDifficulty.Hard};
                for(int i=0;i<choices.Length;i++)
                {
                    var choice=choices[i];
                    difficultyButtons[i]=Button(card,TrialDifficultySettings.Label(choice),24+i*170,82,155,48,"");
                    difficultyButtons[i].onClick.AddListener(()=>
                    {
                        if(listForNetwork)manager.SelectDifficulty(choice);else TrialDifficultySettings.Solo=choice;
                        RefreshDifficultySelection();
                    });
                }
                difficultyHint=Label(card,"",550,82,320,48,15,Grey);
                var viewport=Panel("Level viewport",card,24,150,832,586,Color.clear);
                viewport.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
                viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
                levelContent=Panel("Level rows",viewport,0,0,832,900,Color.clear);
                levelScroll=viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
                levelScroll.viewport=viewport;levelScroll.content=levelContent;levelScroll.horizontal=false;
                levelScroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;levelScroll.scrollSensitivity=38;
                var scrollTrack=Panel("Level scrollbar",card,860,150,16,586,new Color(.13f,.14f,.13f,.10f));
                scrollTrack.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
                var scrollHandle=Panel("Level scroll handle",scrollTrack,0,0,10,100,Red);
                scrollHandle.anchorMin=Vector2.zero;scrollHandle.anchorMax=Vector2.one;
                scrollHandle.offsetMin=new Vector2(3,3);scrollHandle.offsetMax=new Vector2(-3,-3);
                var scrollbar=scrollTrack.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
                scrollbar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;
                scrollbar.handleRect=scrollHandle;scrollbar.targetGraphic=scrollHandle.GetComponent<UnityEngine.UI.Image>();
                scrollbar.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
                levelScroll.verticalScrollbar=scrollbar;
                levelScroll.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.Permanent;
                arenaRow=CreateLevelRow(TrialLevel.Arena,"玩家自由决斗 · 至少两人");
                courtyardRow=CreateLevelRow(TrialLevel.BigFish,"清理守卫，再迎战大肥鱼");
                Button(card,"返回",24,766,220,48,"←").onClick.AddListener(CloseLevelList);
                Label(card,"滚轮 / 拖动浏览 · 方向键 / 手柄选择",302,774,550,30,15,Grey);
                for(int i=0;i<6;i++)
                {
                    var level=(TrialLevel)((int)TrialLevel.Twins+i);
                    challengeRows.Add(CreateLevelRow(level,level==TrialLevel.Remnant?"极限 Boss · 读指令 · 联机击杀成长":level==TrialLevel.Twins?"双人近战 · 交替夹击":level==TrialLevel.Anon?"架剑格挡 · 侧闪反击":level==TrialLevel.Taffy?"实体弹道 · 掩体与换弹":level==TrialLevel.Penguin?"重锤锻击 · 扩散冰环":"长鞭距离战 · 内外安全区"));
                }
                endlessRow=CreateLevelRow(TrialLevel.Endless,"持续刷怪 · 每 5 波精英 · 经验升级 / 30 种词条三选一");
                bossRushRow=CreateLevelRow(TrialLevel.BossRush,"8 位 Boss 逐个挑战 · 初始 3 瓶药不补充 · 战后回满血量与耐力");
            }
            selectionReturn=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            endlessRow.gameObject.SetActive(!multiplayerList);bossRushRow.gameObject.SetActive(!multiplayerList);
            listForNetwork=multiplayerList;arenaRow.gameObject.SetActive(multiplayerList);
            var rows=new System.Collections.Generic.List<UnityEngine.UI.Button>();
            if(multiplayerList)rows.Add(arenaRow);
            rows.Add(courtyardRow);rows.AddRange(challengeRows);
            if(!multiplayerList){rows.Add(endlessRow);rows.Add(bossRushRow);}
            for(int i=0;i<rows.Count;i++)
            {
                rows[i].GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-i*106);
                var nav=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnUp=i>0?rows[i-1]:difficultyButtons[0],selectOnDown=i+1<rows.Count?rows[i+1]:rows[i]};
                rows[i].navigation=nav;
            }
            levelContent.sizeDelta=new Vector2(832,rows.Count*106);levelContent.anchoredPosition=Vector2.zero;

            RefreshDifficultySelection();
            listTitle.text=multiplayerList?"联机关卡":"单人游玩 · 选择关卡";
            levelList.SetActive(true);levelList.transform.SetAsLastSibling();
            (multiplayerList?arenaRow:courtyardRow).Select();
        }
        UnityEngine.UI.Button CreateLevelRow(TrialLevel level,string description)
        {
            var button=Button(levelContent,TrialChampionCatalog.LevelName(level),0,0,832,98,"");
            button.GetComponent<InkButtonMotion>().PreserveLayout=true;
            var title=button.GetComponentInChildren<UnityEngine.UI.Text>();
            title.rectTransform.anchoredPosition=new Vector2(28,-8);title.rectTransform.sizeDelta=new Vector2(780,34);title.alignment=TextAnchor.MiddleLeft;title.fontSize=24;
            Label(button.transform,description,28,57,780,28,16,Grey).alignment=TextAnchor.MiddleLeft;
            Line(button.transform,28,97,770,1,new Color(.13f,.14f,.13f,.12f));
            button.gameObject.AddComponent<InkLevelRowFocus>().Scroll=levelScroll;
            button.onClick.AddListener(()=>SelectListedLevel(level));return button;
        }
        void SelectListedLevel(TrialLevel level)
        {
            if(listForNetwork)manager.SelectLevel(level);
            CloseLevelList();
            if(!listForNetwork){soloLevel=level;StartCoroutine(LoadSolo());}
        }
        void CloseLevelList(){if(levelList)levelList.SetActive(false);if(selectionReturn && EventSystem.current)EventSystem.current.SetSelectedGameObject(selectionReturn);}
        bool UpdateLevelList()
        {
            if(!levelList || !levelList.activeSelf)return false;
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true || Gamepad.current?.buttonEast.wasPressedThisFrame==true)CloseLevelList();
            return true;
        }
    }
}
