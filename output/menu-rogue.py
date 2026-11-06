from pathlib import Path
p=Path('Assets/Scripts/Menu/InkLevelSelection.cs');s=p.read_text(encoding='utf-8');s=s.replace('GameObject levelList;','GameObject levelList;\n        UnityEngine.UI.ScrollRect levelScroll;\n        RectTransform levelContent;')
a=s.index('                arenaRow=Button(');b=s.index('\n            RefreshDifficultySelection();',a)
s=s[:a]+'''                var viewport=Panel("Level viewport",card,24,150,852,586,Color.clear);
                viewport.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
                viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
                levelContent=Panel("Level rows",viewport,0,0,832,900,Color.clear);
                levelScroll=viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
                levelScroll.viewport=viewport;levelScroll.content=levelContent;levelScroll.horizontal=false;
                levelScroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;levelScroll.scrollSensitivity=38;
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
''' +s[b:]
a=s.index('        void SelectListedLevel(')
s=s[:a]+'''        UnityEngine.UI.Button CreateLevelRow(TrialLevel level,string description)
        {
            var button=Button(levelContent,TrialChampionCatalog.LevelName(level),0,0,832,98,"");
            var title=button.GetComponentInChildren<UnityEngine.UI.Text>();
            title.rectTransform.anchoredPosition=new Vector2(28,-8);title.rectTransform.sizeDelta=new Vector2(780,34);title.alignment=TextAnchor.MiddleLeft;title.fontSize=24;
            Label(button.transform,description,28,57,780,28,16,Grey).alignment=TextAnchor.MiddleLeft;
            Line(button.transform,28,97,770,1,new Color(.13f,.14f,.13f,.12f));
            button.gameObject.AddComponent<InkLevelRowFocus>().Scroll=levelScroll;
            button.onClick.AddListener(()=>SelectListedLevel(level));return button;
        }
''' +s[a:];p.write_text(s,encoding='utf-8')
