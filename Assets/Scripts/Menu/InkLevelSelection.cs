using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TomorinTrial
{
    public sealed partial class InkMainMenu
    {
        GameObject levelList;
        bool listForNetwork;
        UnityEngine.UI.Button courtyardRow,arenaRow;
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
                arenaRow=Button(card,"竞技场",24,150,845,70,"");
                Label(arenaRow.transform,"玩家自由决斗 · 至少两人",44,43,770,26,15,Grey);
                courtyardRow=Button(card,"庭院 (Boss · 大肥鱼)",24,228,845,70,"");
                Label(courtyardRow.transform,"清理守卫，再迎战大肥鱼",44,43,770,26,15,Grey);
                Button(card,"返回",24,748,220,55,"←").onClick.AddListener(CloseLevelList);
                arenaRow.onClick.AddListener(()=>SelectListedLevel(TrialLevel.Arena));
                courtyardRow.onClick.AddListener(()=>SelectListedLevel(TrialLevel.BigFish));
                for(int i=0;i<6;i++)
                {
                    var level=(TrialLevel)((int)TrialLevel.Twins+i);
                    var row=Button(card,TrialChampionCatalog.LevelName(level),24,290+i*64,845,60,"");challengeRows.Add(row);
                    Label(row.transform,level==TrialLevel.Remnant?"极限 Boss · 读指令 · 联机击杀成长":level==TrialLevel.Twins?"双人近战 · 交替夹击":level==TrialLevel.Anon?"架剑格挡 · 侧闪反击":level==TrialLevel.Taffy?"实体弹道 · 掩体与换弹":level==TrialLevel.Penguin?"重锤锻击 · 扩散冰环":"长鞭距离战 · 内外安全区",44,36,770,23,15,Grey);
                    row.onClick.AddListener(()=>SelectListedLevel(level));
                }
                var allRows=new System.Collections.Generic.List<UnityEngine.UI.Button>(challengeRows){arenaRow,courtyardRow};
                foreach(var row in allRows)
                {var text=row.GetComponentInChildren<UnityEngine.UI.Text>();text.rectTransform.sizeDelta=new Vector2(780,40);text.fontSize=24;}
            }
            selectionReturn=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            listForNetwork=multiplayerList;arenaRow.gameObject.SetActive(multiplayerList);
            courtyardRow.GetComponent<RectTransform>().anchoredPosition=new Vector2(24,multiplayerList?-228:-150);
            for(int i=0;i<challengeRows.Count;i++)challengeRows[i].GetComponent<RectTransform>().anchoredPosition=new Vector2(24,-(multiplayerList?306:228)-i*64);
            RefreshDifficultySelection();
            listTitle.text=multiplayerList?"联机关卡":"单人游玩 · 选择关卡";
            levelList.SetActive(true);levelList.transform.SetAsLastSibling();
            (multiplayerList?arenaRow:courtyardRow).Select();
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
