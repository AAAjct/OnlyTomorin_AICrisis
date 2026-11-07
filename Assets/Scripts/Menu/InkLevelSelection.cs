using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TomorinTrial
{
    public sealed partial class InkMainMenu
    {
        GameObject levelList;
        Button courtyardRow;
        TrialLevel soloLevel;
        Button[] soloDifficulty, networkDifficulty;
        Text soloDifficultyHint, networkDifficultyHint;
        List<(TrialLevel level, Button button, Text caption)> networkLevelRows;
        static readonly TrialDifficulty[] Difficulties = {TrialDifficulty.Easy, TrialDifficulty.Normal, TrialDifficulty.Hard};

        void OpenLevelList()
        {
            if (!levelList)
            {
                var overlay = Panel("Solo level list", root, 0, 0, 1600, 900, new Color(.03f,.04f,.04f,.7f));
                Stretch(overlay);
                levelList = overlay.gameObject;
                var card = Panel("Level list card", overlay, 0, 0, 900, 830, Paper);
                card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f,.5f);
                card.anchoredPosition = Vector2.zero;
                TrialModalBackdrop.Bind(overlay, card, CloseLevelList);
                Line(card, 0, 0, 3, 830, Red);
                Label(card, "单人关卡", 32, 20, 820, 58, 32, Ink);
                soloDifficulty = BuildDifficultyButtons(card, 24, 82, 155, difficulty =>
                {
                    TrialDifficultySettings.Solo = difficulty;
                    RefreshDifficultyButtons(soloDifficulty, soloDifficultyHint, difficulty, true);
                });
                soloDifficultyHint = Label(card, "", 550, 82, 320, 48, 15, Grey);
                var scroll = BuildLevelScroll(card, 24, 150, 852, 586);
                var rows = BuildLevelRows(scroll, 828, false, level =>
                {
                    soloLevel = level;
                    CloseLevelList();
                    StartCoroutine(LoadSolo());
                });
                courtyardRow = rows[0].button;
                var backButton = Button(card, "返回", 24, 766, 220, 48, "←");
                backButton.onClick.AddListener(CloseLevelList);
                for (int i = 0; i < rows.Count; i++)
                    SetDirectionalNavigation(rows[i].button, up:i>0 ? rows[i-1].button : soloDifficulty[0],
                        down:i+1<rows.Count ? rows[i+1].button : backButton);
                WireDifficultyNavigation(soloDifficulty, backButton, courtyardRow);
                SetDirectionalNavigation(backButton, up:rows[rows.Count-1].button, down:soloDifficulty[0]);
            }
            RefreshDifficultyButtons(soloDifficulty, soloDifficultyHint, TrialDifficultySettings.Solo, true);
            levelList.SetActive(true);
            levelList.transform.SetAsLastSibling();
            courtyardRow.Select();
        }

        void BuildNetworkLevels()
        {
            networkLevels = Panel("Network levels", root, 0, 0, 670, 770, Paper);
            networkLevels.GetComponent<Image>().raycastTarget = true;
            Line(networkLevels, 0, 0, 1, 770, new Color(.13f,.14f,.13f,.18f));
            Label(networkLevels, "联机关卡", 38, 24, 590, 64, 34, Ink);
            networkDifficulty = BuildDifficultyButtons(networkLevels, 38, 96, 148, manager.SelectDifficulty);
            networkDifficultyHint = Label(networkLevels, "", 38, 146, 590, 31, 16, Grey);
            var scroll = BuildLevelScroll(networkLevels, 38, 187, 590, 550);
            networkLevelRows = BuildLevelRows(scroll, 566, true, manager.SelectLevel);
        }

        void RefreshNetworkLevels(bool editable)
        {
            RefreshDifficultyButtons(networkDifficulty, networkDifficultyHint, manager.SelectedDifficulty, editable);
            foreach (var row in networkLevelRows)
            {
                row.button.interactable = editable;
                row.caption.text = (row.level == manager.SelectedLevel ? "●  " : "") + TrialChampionCatalog.LevelName(row.level);
            }
        }

        Button[] BuildDifficultyButtons(RectTransform card, int x, int y, int width, UnityAction<TrialDifficulty> select)
        {
            var buttons = new Button[Difficulties.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                var difficulty = Difficulties[i];
                buttons[i] = Button(card, TrialDifficultySettings.Label(difficulty), x+i*(width+15), y, width, 44, "");
                buttons[i].onClick.AddListener(() => select(difficulty));
            }
            return buttons;
        }

        void RefreshDifficultyButtons(Button[] buttons, Text hint, TrialDifficulty difficulty, bool editable)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].interactable = editable;
                buttons[i].GetComponentInChildren<Text>().text = (Difficulties[i]==difficulty ? "● " : "") +
                    TrialDifficultySettings.Label(Difficulties[i]);
            }
            hint.text = !editable ? "由房主选择关卡和难度" : difficulty switch
            {
                TrialDifficulty.Easy => "敌人生命 75% · 伤害 70%",
                TrialDifficulty.Hard => "敌人生命 130% · 伤害 130%",
                _ => "敌人生命与伤害 100%"
            };
        }

        void WireDifficultyNavigation(Button[] buttons, Selectable up, Selectable down, Selectable leftColumn = null)
        {
            for (int i = 0; i < buttons.Length; i++)
                SetDirectionalNavigation(buttons[i], up:up, down:down,
                    left:i==0 && leftColumn ? leftColumn : buttons[(i+buttons.Length-1)%buttons.Length],
                    right:buttons[(i+1)%buttons.Length]);
        }

        ScrollRect BuildLevelScroll(RectTransform card, int x, int y, int width, int height)
        {
            var viewport = Panel("Level viewport", card, x, y, width-24, height, Color.clear);
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Level rows", viewport, Vector2.zero, Vector2.zero);
            content.anchorMax = Vector2.one;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 14;
            var track = Panel("Level scrollbar", card, x+width-14, y, 14, height, new Color(.13f,.14f,.13f,.10f));
            track.GetComponent<Image>().raycastTarget = true;
            var handle = Panel("Level scroll handle", track, 0, 0, 0, 0, Red);
            Stretch(handle);
            handle.offsetMin = new Vector2(2,2);
            handle.offsetMax = new Vector2(-2,-2);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.navigation = new Navigation {mode=Navigation.Mode.None};
            scroll.verticalScrollbar = scrollbar;
            return scroll;
        }

        List<(TrialLevel level, Button button, Text caption)> BuildLevelRows(ScrollRect scroll, int width,
            bool multiplayerList, UnityAction<TrialLevel> select)
        {
            const int step = 106;
            var rows = new List<(TrialLevel level, Button button, Text caption)>();
            if (multiplayerList) Add(TrialLevel.Arena, "玩家自由决斗 · 至少两人");
            Add(TrialLevel.BigFish, "清理守卫，再迎战大肥鱼");
            Add(TrialLevel.Twins, "双人近战 · 交替夹击");
            Add(TrialLevel.Anon, "架剑格挡 · 侧闪反击");
            Add(TrialLevel.Taffy, "实体弹道 · 掩体与换弹");
            Add(TrialLevel.Penguin, "重锤锻击 · 扩散冰环");
            Add(TrialLevel.YaYa, "长鞭距离战 · 内外安全区");
            Add(TrialLevel.Remnant, "极限 Boss · 读指令 · 联机击杀成长");
            Add(TrialLevel.Endless, "持续刷怪 · 每 5 波精英 · 经验升级 / 30 种词条三选一");
            Add(TrialLevel.BossRush, "8 位 Boss 逐个挑战 · 初始 3 瓶药不补充 · 战后回满血量与耐力");
            scroll.content.sizeDelta = new Vector2(0, rows.Count*step);
            return rows;

            void Add(TrialLevel level, string description)
            {
                var button = Button(scroll.content, TrialChampionCatalog.LevelName(level), 0, rows.Count*step, width, 98, "");
                button.GetComponent<InkButtonMotion>().PreserveLayout = true;
                var title = button.GetComponentInChildren<Text>();
                Place(title.rectTransform, 28, 8, width-56, 34);
                title.fontSize = 24;
                Label(button.transform, description, 28, 49, width-56, 40, 16, Grey);
                Line(button.transform, 28, 97, width-56, 1, new Color(.13f,.14f,.13f,.12f));
                var rect = (RectTransform)button.transform;
                TrialMenuLayout.StretchContents(rect);
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = new Vector2(0, 98);
                button.gameObject.AddComponent<InkLevelRowFocus>().Scroll = scroll;
                button.onClick.AddListener(() => select(level));
                rows.Add((level, button, title));
            }
        }

        void CloseLevelList()
        {
            levelList.SetActive(false);
            solo.Select();
        }

        bool UpdateLevelList()
        {
            if (!levelList || !levelList.activeSelf) return false;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame==true || Gamepad.current?.buttonEast.wasPressedThisFrame==true)
                CloseLevelList();
            else if (NavigationActive && EventSystem.current)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                if (!selected || !selected.transform.IsChildOf(levelList.transform)) courtyardRow.Select();
            }
            return true;
        }
    }
}
