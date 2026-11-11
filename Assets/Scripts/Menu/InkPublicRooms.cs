using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TomorinTrial
{
    public sealed partial class InkMainMenu
    {
        Button roomVisibility, browseRooms, searchRooms, moreRooms;
        Text roomVisibilityLabel, roomSearchStatus;
        RectTransform roomBrowser;
        ScrollRect roomResults;
        readonly List<Button> roomRows = new();
        readonly HashSet<string> displayedRooms = new();
        bool createPublicRoom, roomSearchPending;
        string roomContinuation;
        bool RoomBrowserOpen => roomBrowser && roomBrowser.gameObject.activeInHierarchy;

        void InstallPublicRoomEntry()
        {
            roomVisibility = LobbyButton(roomSetup, "私人", 24, 338, 700, 48);
            roomVisibilityLabel = roomVisibility.GetComponentInChildren<Text>();
            roomVisibility.onClick.AddListener(() =>
            {
                createPublicRoom = !createPublicRoom;
                RefreshNetwork();
            });
            browseRooms = LobbyButton(network, "公共房间", 24, 168, 208, 52);
            browseRooms.onClick.AddListener(OpenRoomBrowser);
        }

        void RefreshPublicRoomEntry(bool create, bool directory)
        {
            roomVisibility.gameObject.SetActive(create);
            SetLobbyControlArea((RectTransform)roomVisibility.transform,roomSetup,24,338,relayMode?700:320,48,748,560);
            roomVisibilityLabel.text = createPublicRoom ? "房间：公开" : "房间：私人";
            browseRooms.gameObject.SetActive(!manager.Busy);
            browseRooms.GetComponent<InkButtonMotion>().Emphasized = directory;
        }

        void OpenRoomBrowser()
        {
            if (manager.Busy) return;
            roomFormOpen = false;
            relayMode = Application.platform != RuntimePlatform.WebGLPlayer;
            RefreshNetwork();
            if (!roomSearchPending) SearchPublicRooms(false);
        }

        void CloseRoomBrowser()
        {
            if (!RoomBrowserOpen) return;
            roomBrowser.gameObject.SetActive(false);
        }

        void BuildRoomBrowser()
        {
            roomBrowser = Panel("Public room browser", network, 302, 124, 974, 540, Color.clear);
            var card = roomBrowser;
            searchRooms = LobbyButton(card, "刷新", 838, 0, 136, 44);
            searchRooms.onClick.AddListener(() => SearchPublicRooms(false));
            Label(card,"房间",16,58,300,32,16,Grey);
            Label(card,"关卡",354,58,300,32,16,Grey);
            Label(card,"难度",678,58,100,32,16,Grey);
            Label(card,"人数",796,58,100,32,16,Grey);
            Line(card,0,96,974,1,RoomRuleTint);
            roomSearchStatus = Label(card, "", 0, 180, 974, 120, 21, Grey,TextAnchor.MiddleCenter);
            roomSearchStatus.verticalOverflow = VerticalWrapMode.Truncate;
            roomResults = BuildLevelScroll(card, 0, 104, 974, 388);
            roomResults.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            moreRooms = LobbyButton(card, "更多", 838, 496, 136, 44);
            moreRooms.onClick.AddListener(() => SearchPublicRooms(true));
        }

        async void SearchPublicRooms(bool append)
        {
            if (roomSearchPending || manager.Busy || !RoomBrowserOpen) return;
            if (!append)
            {
                foreach (var row in roomRows) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
                roomRows.Clear();
                displayedRooms.Clear();
                roomResults.StopMovement();
                roomResults.content.anchoredPosition = Vector2.zero;
                roomResults.content.sizeDelta = Vector2.zero;
                roomContinuation = null;
            }

            roomSearchPending = true;
            roomSearchStatus.text = roomRows.Count==0?"加载中…":"";
            RefreshRoomBrowserNavigation();
            try
            {
                var result = await manager.SearchPublicRooms(null, roomContinuation);
                if (!this) return;
                roomContinuation = result.continuation;
                foreach (var room in result.rooms)
                    if (displayedRooms.Add(room.Id)) AddPublicRoomRow(room);
                roomSearchStatus.text = roomRows.Count == 0 ? "暂无公开房间" : "";
            }
            catch (Exception error)
            {
                if (this) roomSearchStatus.text = "搜索失败 · " + error.Message;
            }
            finally
            {
                if (this)
                {
                    roomSearchPending = false;
                    RefreshRoomBrowserNavigation();
                }
            }
        }

        void AddPublicRoomRow(TrialNetworkManager.PublicRoom room)
        {
            const int width = 950, height = 66;
            var button = Button(roomResults.content, room.Name, 0, roomRows.Count * height, width, height, "");
            button.GetComponent<InkButtonMotion>().PreserveLayout = true;
            button.GetComponent<InkButtonMotion>().FlatBackground = true;
            AddLobbyButtonBorder(button);
            var title = button.GetComponentInChildren<Text>();
            Place(title.rectTransform, 16, 0, 308, height);
            title.fontSize = 21;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 12;
            title.resizeTextMaxSize = 21;
            Label(button.transform, TrialChampionCatalog.LevelName(room.Level), 354, 0, 300, height, 17, Ink).verticalOverflow = VerticalWrapMode.Truncate;
            Label(button.transform, room.Level==TrialLevel.Arena?"—":TrialDifficultySettings.Label(room.Difficulty), 678, 0, 100, height, 17, Ink).verticalOverflow = VerticalWrapMode.Truncate;
            Label(button.transform, $"{room.Players}/{room.Capacity}", 796, 0, 80, height, 17, Ink).verticalOverflow = VerticalWrapMode.Truncate;
            Label(button.transform, "加入", 880, 0, 70, height, 17, Red).verticalOverflow = VerticalWrapMode.Truncate;
            Line(button.transform, 16, height - 1, width - 32, 1, new Color(.13f, .14f, .13f, .12f));
            var row = (RectTransform)button.transform;
            TrialMenuLayout.StretchContents(row);
            row.anchorMax = Vector2.one;
            row.sizeDelta = new Vector2(0, height);
            button.gameObject.AddComponent<InkLevelRowFocus>().Scroll = roomResults;
            button.onClick.AddListener(() =>
            {
                CloseRoomBrowser();
                manager.JoinPublicRoom(room.Id, nickname.text);
            });
            roomRows.Add(button);
            roomResults.content.sizeDelta = new Vector2(0, roomRows.Count * height);
        }

        void RefreshRoomBrowserNavigation()
        {
            bool ready = !roomSearchPending;
            searchRooms.interactable = ready;
            moreRooms.gameObject.SetActive(!string.IsNullOrEmpty(roomContinuation));
            moreRooms.interactable = ready;
            for (int i = 0; i < roomRows.Count; i++)
                roomRows[i].interactable = ready;
            RefreshNetworkNavigation();
        }
    }
}
