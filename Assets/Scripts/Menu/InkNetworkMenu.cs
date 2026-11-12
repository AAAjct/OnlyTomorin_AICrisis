using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace TomorinTrial
{
    public sealed partial class InkMainMenu
    {
        const float NetworkWidth = 1320, NetworkHeight = 800;
        const int CreateRoomOffsetY = 80;
        const int RoomRosterPageSize = 4;
        readonly Text[] rosterNames = new Text[RoomRosterPageSize];
        RectTransform lobbyForm, roomSetup, roomRoster, capacityControls, inviteCode;
        Button joinRoomTab, createRoomTab, configureTunnel, changeLevel, copyRoomCode;
        Button decreaseCapacity, increaseCapacity, previousRosterPage, nextRosterPage;
        Button relayHostMode, directHostMode;
        Text networkTitle, roomType, pendingStatus, joinStatus, levelCaption, capacityValue;
        Text addressLabel, portLabel, inviteCodeValue, rosterPageLabel, leaveLabel;
        Text nicknameLabel;
        InputField roomName, publicEndpoint;
        Text roomNameLabel, publicEndpointLabel;
        bool relayMode, creatingRoom, roomFormOpen, joinConnectionFeedback;
        bool hostUsesRelay = true;
        int rosterPage;
        float rosterNameWidth;
        static readonly Color RoomRuleTint = new(.13f, .14f, .13f, .18f);

        void InstallNetworkLayout()
        {
            // The saved title scene and the editor builder both provide these bindings.
            foreach (var label in multiplayer.GetComponentsInChildren<Text>(true))
                if (label.text == "局域网联机") label.text = "多人联机";
            foreach (Transform child in network) child.gameObject.SetActive(false);
            Place(network, 0, 0, NetworkWidth, NetworkHeight);
            var background = network.GetComponent<Image>();
            background.color = Paper;
            background.raycastTarget = true;
            Label(network, "联机", 32, 34, 190, 58, 36, Ink);
            Line(network, 258, 32, 1, 736, RoomRuleTint);
            networkTitle = Label(network, "公共房间", 302, 38, 610, 58, 32, Ink);
            roomType = Label(network, "", 997, 47, 279, 42, 18, Grey, TextAnchor.MiddleRight);
            StyleLobbyButton(back, network, "返回", 24, 712, 208, 48);

            joinRoomTab = LobbyButton(network, "加入房间", 24, 304, 208, 52);
            createRoomTab = LobbyButton(network, "创建房间", 24, 236, 208, 52);
            joinRoomTab.onClick.AddListener(() => SelectRoomTab(false));
            createRoomTab.onClick.AddListener(() => SelectRoomTab(true));
            BuildLobbyForm();
            BuildRoomSettings();
            BuildRoomRoster();
            InstallPublicRoomEntry();
            BuildRoomBrowser();

            MoveLobbyControl(status.rectTransform, network, 302, 697, 974, 52);
            status.fontSize = 17;
            status.verticalOverflow = VerticalWrapMode.Truncate;
            pendingStatus = Label(network, "", 302, 280, 974, 120, 23, Grey, TextAnchor.MiddleCenter);
            pendingStatus.verticalOverflow = VerticalWrapMode.Truncate;
            StyleLobbyButton(leave, network, "离开房间", 24, 712, 208, 48);
            leaveLabel = leave.GetComponentInChildren<Text>(true);
            StyleLobbyButton(begin, network, "开始游戏", 1030, 706, 246, 58, primary: true);

            relayMode = manager.Busy ? manager.RelaySession : Application.platform != RuntimePlatform.WebGLPlayer;
            port.text = PlayerPrefs.GetInt("Ink.Port", 7777).ToString();
            string savedAddress = PlayerPrefs.GetString("Ink.Address", "127.0.0.1");
            address.text = (savedAddress.Contains(":") ? "[" + savedAddress + "]" : savedAddress) + ":" + port.text;
            nickname.text = TrialNetworkManager.SavedPlayerName;

            host.onClick.AddListener(() =>
            {
                joinConnectionFeedback = false;
                if (relayMode) manager.HostRelay(nickname.text, createPublicRoom, roomName.text);
                else manager.HostDirect(port.text, nickname.text, createPublicRoom, publicEndpoint.text, roomName.text);
            });
            join.onClick.AddListener(() =>
            {
                joinConnectionFeedback = true;
                manager.JoinRoom(address.text, nickname.text);
            });
            begin.onClick.AddListener(manager.BeginTogether);
            leave.onClick.AddListener(LeaveNetworkRoom);
            back.onClick.AddListener(() => { if (roomFormOpen) OpenRoomBrowser(); else Show(home); });
        }

        void BuildLobbyForm()
        {
            lobbyForm = Panel("Room connection form", network, 408, 208, 748, 340, Color.clear);
            nicknameLabel = InstallLobbyField(nickname, "昵称", 24, 58, 432);
            MoveLobbyControl(nicknameLabel.rectTransform, network, 1010, 29, 266, 25);
            MoveLobbyControl((RectTransform)nickname.transform, network, 1010, 58, 266, 42);
            Place(nickname.textComponent.rectTransform, 12, 0, 242, 42);
            if (nickname.transform.Find("Ink rule") is RectTransform nicknameRule) Place(nicknameRule, 0, 41, 266, 1);
            addressLabel = InstallLobbyField(address, "IP:端口 / 房间码", 0, 80, 700);
            address.characterLimit = 260;
            portLabel = InstallLobbyField(port, "端口", 500, 80, 200);
            configureTunnel = LobbyButton(lobbyForm, "随机端口", 0, 80, 480, 48);
            configureTunnel.onClick.AddListener(ConfigureHostTunnel);
            StyleLobbyButton(join, lobbyForm, "加入", 0, 178, 700, 54, primary: true);
            joinStatus = Label(lobbyForm, "", 0, 248, 700, 92, 17, Red);
            joinStatus.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void BuildRoomSettings()
        {
            roomName = Field(network, "房间名", 432, 112 + CreateRoomOffsetY, 700, manager.SavedRoomName);
            roomNameLabel = network.Find("房间名").GetComponent<Text>();
            MoveLobbyControl(roomNameLabel.rectTransform, network, 302, 112 + CreateRoomOffsetY, 110, 48);
            roomNameLabel.fontSize = 24;
            roomName.textComponent.fontSize = 26;
            roomName.characterLimit = 24;
            WireLobbyField(roomName);
            roomSetup = Panel("Room settings summary", network, 408, 150, 748, 560, Color.clear);
            Label(roomSetup, "当前关卡", 24, 19, 432, 26, 17, Grey);
            levelCaption = Label(roomSetup, "", 24, 47, 490, 54, 24, Ink);
            levelCaption.resizeTextForBestFit = true;
            levelCaption.resizeTextMinSize = 14;
            levelCaption.resizeTextMaxSize = 24;
            levelCaption.verticalOverflow = VerticalWrapMode.Truncate;
            changeLevel = LobbyButton(roomSetup, "更改", 548, 47, 176, 48);
            changeLevel.onClick.AddListener(OpenNetworkLevels);

            Line(roomSetup, 24, 124, 700, 1, RoomRuleTint);
            capacityControls = Rect("Room capacity", roomSetup, new Vector2(24, 160), new Vector2(700, 55));
            Label(capacityControls, "人数上限", 0, 0, 192, 55, 18, Ink);
            decreaseCapacity = LobbyButton(capacityControls, "−", 480, 0, 62, 54);
            increaseCapacity = LobbyButton(capacityControls, "+", 638, 0, 62, 54);
            capacityValue = Label(capacityControls, "", 542, 0, 96, 54, 24, Ink, TextAnchor.MiddleCenter);
            decreaseCapacity.onClick.AddListener(() => manager.AdjustRoomCapacity(-1));
            increaseCapacity.onClick.AddListener(() => manager.AdjustRoomCapacity(1));
            relayHostMode = LobbyButton(roomSetup, "房间码", 24, 252, 340, 48);
            directHostMode = LobbyButton(roomSetup, "IP开放", 384, 252, 340, 48);
            relayHostMode.onClick.AddListener(() => SelectHostConnection(true));
            directHostMode.onClick.AddListener(() => SelectHostConnection(false));
            publicEndpoint = Field(roomSetup, "公网入口", 24, 422, 700, PlayerPrefs.GetString("Ink.PublicEndpoint", ""));
            publicEndpointLabel = roomSetup.Find("公网入口").GetComponent<Text>();
            publicEndpoint.characterLimit = 260;
            WireLobbyField(publicEndpoint);

            inviteCode = Rect("Room invitation", roomSetup, new Vector2(24, 288), new Vector2(700, 82));
            Line(inviteCode, 0, 0, 432, 1, RoomRuleTint);
            Label(inviteCode, "邀请房间码", 0, 7, 240, 25, 16, Grey);
            inviteCodeValue = Label(inviteCode, "", 0, 32, 220, 47, 28, Ink);
            copyRoomCode = LobbyButton(inviteCode, "复制", 520, 31, 180, 47);
            copyRoomCode.onClick.AddListener(() =>
            {
                GUIUtility.systemCopyBuffer = manager.RelayRoomCode;
                status.color = Grey;
                status.text = "已复制邀请房间码";
            });
            StyleLobbyButton(host, roomSetup, "创建房间", 24, 414, 700, 54, primary: true);
        }

        void BuildRoomRoster()
        {
            roomRoster = Panel("Room members", network, 302, 160, 460, 480, Color.clear);
            MoveLobbyControl(members.rectTransform, roomRoster, 24, 19, 432, 42);
            members.fontSize = 23;
            members.resizeTextForBestFit = true;
            members.resizeTextMinSize = 14;
            members.resizeTextMaxSize = 23;
            members.verticalOverflow = VerticalWrapMode.Truncate;
            members.gameObject.SetActive(true);
            Line(roomRoster, 24, 69, 432, 1, RoomRuleTint);
            for (int slot = 0; slot < rosterNames.Length; slot++)
            {
                var label = Label(roomRoster, "", 24, 82 + slot * 42, 432, 38, 20, Ink);
                label.gameObject.name = "Room member " + (slot + 1);
                label.verticalOverflow = VerticalWrapMode.Truncate;
                rosterNames[slot] = label;
            }
            previousRosterPage = RosterPageButton("上一页", 24, -1);
            rosterPageLabel = Label(roomRoster, "", 178, 282, 124, 48, 17, Grey, TextAnchor.MiddleCenter);
            nextRosterPage = RosterPageButton("下一页", 326, 1);
        }

        void SelectRoomTab(bool create)
        {
            creatingRoom = create;
            roomFormOpen = true;
            relayMode = create && hostUsesRelay && Application.platform != RuntimePlatform.WebGLPlayer;
            RefreshNetwork();
        }

        void SelectHostConnection(bool useRelay)
        {
            hostUsesRelay = relayMode = useRelay;
            RefreshNetwork();
        }

        void RefreshNetwork()
        {
            if (!manager || !lobbyForm) return;
            bool offline = !manager.Busy;
            bool create = offline && roomFormOpen && creatingRoom;
            bool joinRoom = offline && roomFormOpen && !creatingRoom;
            bool directory = offline && !roomFormOpen;
            bool hasCode = !string.IsNullOrEmpty(manager.RelayRoomCode);
            bool inRoom = !offline && manager.Members.Length > 0 &&
                (!manager.IsHost || (manager.PublicRoomReady && (!manager.RelaySession || hasCode)));
            bool connecting = !offline && !inRoom;
            bool canEditRoom = create || (inRoom && manager.IsHost && manager.CanAdjustRoomCapacity);

            networkTitle.text = inRoom ? manager.RoomName : connecting ? "连接中" : directory ? "公共房间" : create ? "创建房间" : "加入房间";
            networkTitle.resizeTextForBestFit = true;
            networkTitle.resizeTextMinSize = 18;
            networkTitle.resizeTextMaxSize = 32;
            roomType.gameObject.SetActive(inRoom);
            roomType.text = manager.RelaySession
                ? (manager.IsHost ? (manager.PublicRelayHost ? "公开房间" : "私人房间") : "房间码联机")
                : manager.PublicDirectHost ? "公开房间 · IP" : "IP 直连";
            back.gameObject.SetActive(offline);
            joinRoomTab.gameObject.SetActive(offline);
            createRoomTab.gameObject.SetActive(offline);
            joinRoomTab.GetComponent<InkButtonMotion>().Emphasized = joinRoom;
            createRoomTab.GetComponent<InkButtonMotion>().Emphasized = create;
            nickname.gameObject.SetActive(offline);
            nicknameLabel.gameObject.SetActive(offline);
            roomBrowser.gameObject.SetActive(directory);

            lobbyForm.gameObject.SetActive(joinRoom);
            host.gameObject.SetActive(create);
            join.gameObject.SetActive(joinRoom);
            ShowLobbyField(address, addressLabel, joinRoom);
            ShowLobbyField(roomName, roomNameLabel, create);
            ShowLobbyField(port, portLabel, create && !relayMode);
            configureTunnel.gameObject.SetActive(create && !relayMode);
            bool directHost = create && !relayMode;
            portLabel.text = directHost ? "本地端口" : "端口";
            SetLobbyControlArea((RectTransform)port.transform, directHost ? roomSetup : lobbyForm,
                directHost ? 524 : 500, directHost ? 338 : 80, 200, 48, 748, directHost ? 560 : 340);
            SetLobbyControlArea(portLabel.rectTransform, directHost ? roomSetup : lobbyForm,
                directHost ? 524 : 500, directHost ? 308 : 50, 200, 28, 748, directHost ? 560 : 340);
            SetLobbyControlArea((RectTransform)configureTunnel.transform, roomSetup, 360, 338, 140, 48, 748, 560);
            ShowLobbyField(publicEndpoint, publicEndpointLabel, directHost && createPublicRoom);
            SetLobbyControlArea((RectTransform)host.transform, roomSetup, 24, directHost && createPublicRoom ? 486 : 414, 700, 54, 748, 560);
            RefreshPublicRoomEntry(create, directory);

            roomSetup.gameObject.SetActive(create || inRoom);
            relayHostMode.gameObject.SetActive(create && Application.platform != RuntimePlatform.WebGLPlayer);
            directHostMode.gameObject.SetActive(create && Application.platform != RuntimePlatform.WebGLPlayer);
            relayHostMode.GetComponent<InkButtonMotion>().Emphasized = relayMode;
            directHostMode.GetComponent<InkButtonMotion>().Emphasized = !relayMode;
            float settingsX = inRoom ? 802 : 408;
            float settingsWidth = inRoom ? 474 : 748;
            float settingsOffsetY = create ? CreateRoomOffsetY : 0;
            float settingsBottom = create ? 750 : 710;
            roomSetup.anchorMin = new Vector2(settingsX / NetworkWidth, 1 - settingsBottom / NetworkHeight);
            roomSetup.anchorMax = new Vector2((settingsX + settingsWidth) / NetworkWidth, 1 - (150 + settingsOffsetY) / NetworkHeight);
            SetLobbyControlArea(status.rectTransform, network, 302, create ? 737 : 697, 974, 52, NetworkWidth, NetworkHeight);
            levelCaption.text = manager.LevelName + (manager.SelectedLevel == TrialLevel.Arena ? "" : " · " + TrialDifficultySettings.Label(manager.SelectedDifficulty));
            changeLevel.gameObject.SetActive(canEditRoom);
            if (!canEditRoom) CloseNetworkLevels();
            RefreshNetworkLevels();
            capacityControls.gameObject.SetActive(canEditRoom);
            capacityValue.text = manager.RoomCapacity.ToString();
            decreaseCapacity.interactable = canEditRoom && manager.RoomCapacity > manager.RoomCapacityFloor;
            increaseCapacity.interactable = canEditRoom && manager.RoomCapacity < TrialNetworkManager.MaxRoomCapacity;
            inviteCode.gameObject.SetActive(inRoom && hasCode);
            inviteCodeValue.text = manager.RelayRoomCode ?? "";

            roomRoster.gameObject.SetActive(inRoom);
            RefreshRoomRoster();
            begin.gameObject.SetActive(inRoom && manager.IsHost);
            begin.interactable = manager.SelectedLevel != TrialLevel.Arena || manager.Members.Length >= 2;
            leave.gameObject.SetActive(!offline);
            leaveLabel.text = connecting ? "取消连接" : "离开房间";

            pendingStatus.gameObject.SetActive(connecting);
            pendingStatus.text = manager.Status;
            string message = manager.Status;
            bool problem = message.Contains("失败") || message.Contains("断开") || message.Contains("错误");
            status.color = problem ? Red : Grey;
            status.text = connecting || message == "尚未连接" || message == "已离开房间" || (inRoom && manager.IsHost && !problem)
                ? (inRoom && manager.IsHost && !begin.interactable ? "竞技场至少需要 2 名玩家" : "") : message;
            joinStatus.text = joinConnectionFeedback ? status.text : "";
            joinStatus.color = status.color;
            joinStatus.gameObject.SetActive(joinRoom && joinConnectionFeedback);
            status.gameObject.SetActive(!joinRoom && (!joinConnectionFeedback || inRoom));
            RefreshNetworkNavigation();
        }

        void LeaveNetworkRoom()
        {
            manager.Leave();
        }

        int RosterPageCount => Mathf.Max(1, (manager.Members.Length + RoomRosterPageSize - 1) / RoomRosterPageSize);

        void RefreshRoomRoster()
        {
            members.text = $"房间人数  {manager.Members.Length} / {manager.ActiveRoomCapacity}";
            int pageCount = RosterPageCount;
            rosterPage = Mathf.Min(rosterPage, pageCount - 1);
            bool showPaging = pageCount > 1;
            previousRosterPage.gameObject.SetActive(showPaging);
            nextRosterPage.gameObject.SetActive(showPaging);
            rosterPageLabel.gameObject.SetActive(showPaging);
            rosterPageLabel.text = $"{rosterPage + 1} / {pageCount}";
            RefreshRosterNames();
        }

        void RefreshRosterNames()
        {
            rosterNameWidth = rosterNames[0].rectTransform.rect.width;
            for (int slot = 0; slot < rosterNames.Length; slot++)
            {
                int index = rosterPage * RoomRosterPageSize + slot;
                var label = rosterNames[slot];
                label.text = index < manager.Members.Length ? FitRosterName(label, manager.Members[index]) : "";
            }
        }

        static string FitRosterName(Text label, string name)
        {
            float width = label.rectTransform.rect.width * label.pixelsPerUnit;
            var settings = label.GetGenerationSettings(Vector2.zero);
            var generator = label.cachedTextGeneratorForLayout;
            if (generator.GetPreferredWidth(name, settings) <= width) return name;

            var starts = StringInfo.ParseCombiningCharacters(name);
            for (int i = starts.Length - 1; i >= 0; i--)
            {
                string shortened = name.Substring(0, starts[i]) + "…";
                if (generator.GetPreferredWidth(shortened, settings) <= width) return shortened;
            }
            return "";
        }

        Button RosterPageButton(string title, int x, int direction)
        {
            var button = LobbyButton(roomRoster, title, x, 282, 130, 48);
            button.onClick.AddListener(() =>
            {
                int pageCount = RosterPageCount;
                rosterPage = (rosterPage + direction + pageCount) % pageCount;
                RefreshRoomRoster();
            });
            return button;
        }

        public static bool TrySplitEndpoint(string value, out string hostName, out string portNumber)
        {
            hostName = portNumber = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            value = value.Trim();
            int separator = value.LastIndexOf(':');
            if (separator <= 0 || separator == value.Length - 1) return false;
            string candidate = value.Substring(0, separator);
            // Accept hostname/IPv4:port and bracketed IPv6; never split a bare IPv6 address.
            if (candidate.Contains(":") && !(candidate.StartsWith("[") && candidate.EndsWith("]"))) return false;
            if (!ushort.TryParse(value.Substring(separator + 1), out var number) || number == 0) return false;
            hostName = candidate.Trim('[', ']').Trim();
            if (System.Uri.CheckHostName(hostName) == System.UriHostNameType.Unknown) return false;
            portNumber = number.ToString();
            return true;
        }

        void LateUpdate()
        {
            if (roomRoster && roomRoster.gameObject.activeInHierarchy &&
                !Mathf.Approximately(rosterNameWidth, rosterNames[0].rectTransform.rect.width)) RefreshRosterNames();
        }

        void ConfigureHostTunnel()
        {
            if (manager.Busy) return;
            if (!TrialNetworkManager.TryFindFreeTcpPort(out var freePort))
            {
                status.color = Red;
                status.text = "未能分配可用的 TCP 端口，请重试。";
                return;
            }
            port.SetTextWithoutNotify(freePort.ToString());
            status.color = Grey;
            status.text = $"请将穿透服务的本地 TCP 端口设为 {freePort}";
        }

        Text InstallLobbyField(InputField field, string title, int x, int y, int width)
        {
            MoveLobbyControl((RectTransform)field.transform, lobbyForm, x, y, width, 48);
            Place(field.textComponent.rectTransform, 13, 0, width - 26, 48);
            if (field.transform.Find("Ink rule") is RectTransform rule) rule.sizeDelta = new Vector2(width, 1);
            WireLobbyField(field);
            return Label(lobbyForm, title, x, y - 30, width, 28, 17, Grey);
        }

        void WireLobbyField(InputField field)
        {
            field.gameObject.SetActive(true);
            field.shouldActivateOnSelect = false;
            field.gameObject.AddComponent<TrialSettingsFocus>().CoolFocus = true;
            AddLobbyBorder(field.transform, "Field border");
            field.onEndEdit.AddListener(_ => networkEditEndedFrame = Time.frameCount);
        }

        static void ShowLobbyField(InputField field, Text label, bool visible)
        {
            field.gameObject.SetActive(visible);
            label.gameObject.SetActive(visible);
        }

        Button LobbyButton(RectTransform parent, string title, int x, int y, int width, int height, bool primary = false)
        {
            var button = Button(parent, title, x, y, width, height, "");
            StyleLobbyButton(button, parent, title, x, y, width, height, primary);
            return button;
        }

        void StyleLobbyButton(Button button, RectTransform parent, string title, int x, int y, int width, int height, bool primary = false)
        {
            MoveLobbyControl((RectTransform)button.transform, parent, x, y, width, height);
            var motion = button.GetComponent<InkButtonMotion>();
            motion.PreserveLayout = true;
            motion.FlatBackground = true;
            motion.Emphasized = primary;
            var labels = button.GetComponentsInChildren<Text>(true);
            var caption = labels[0];
            for (int i = 1; i < labels.Length; i++) labels[i].gameObject.SetActive(false);
            caption.text = title;
            Place(caption.rectTransform, 7, 0, width - 14, height);
            caption.alignment = TextAnchor.MiddleCenter;
            caption.fontSize = primary ? 22 : 18;
            caption.resizeTextForBestFit = true;
            caption.resizeTextMinSize = 12;
            caption.resizeTextMaxSize = caption.fontSize;
            caption.verticalOverflow = VerticalWrapMode.Truncate;
            AddLobbyButtonBorder(button);
            button.gameObject.SetActive(true);
        }

        void AddLobbyButtonBorder(Button button)
        {
            button.GetComponent<InkButtonMotion>().BorderGraphics = AddLobbyBorder(button.transform, "Button border");
        }

        Image[] AddLobbyBorder(Transform target, string name)
        {
            if (target.Find(name) is Transform existing) return existing.GetComponentsInChildren<Image>();
            var border = Rect(name, target, Vector2.zero, Vector2.zero);
            Stretch(border);
            var tint = new Color(.14f, .16f, .15f, .55f);
            const float thickness = 2f;
            var top = Panel("Top", border, 0, 0, 0, 0, tint);
            top.anchorMin = new Vector2(0, 1); top.anchorMax = Vector2.one;
            top.offsetMin = new Vector2(0, -thickness); top.offsetMax = Vector2.zero;
            var bottom = Panel("Bottom", border, 0, 0, 0, 0, tint);
            bottom.anchorMin = Vector2.zero; bottom.anchorMax = new Vector2(1, 0);
            bottom.offsetMin = Vector2.zero; bottom.offsetMax = new Vector2(0, thickness);
            var left = Panel("Left", border, 0, 0, 0, 0, tint);
            left.anchorMin = Vector2.zero; left.anchorMax = new Vector2(0, 1);
            left.offsetMin = Vector2.zero; left.offsetMax = new Vector2(thickness, 0);
            var right = Panel("Right", border, 0, 0, 0, 0, tint);
            right.anchorMin = new Vector2(1, 0); right.anchorMax = Vector2.one;
            right.offsetMin = new Vector2(-thickness, 0); right.offsetMax = Vector2.zero;
            return border.GetComponentsInChildren<Image>();
        }

        static void MoveLobbyControl(RectTransform control, RectTransform parent, int x, int y, int width, int height)
        {
            control.SetParent(parent, false);
            control.anchorMin = control.anchorMax = control.pivot = new Vector2(0, 1);
            Place(control, x, y, width, height);
        }

        static void SetLobbyControlArea(RectTransform control, RectTransform parent, float x, float y, float width, float height, float parentWidth, float parentHeight)
        {
            control.SetParent(parent, false);
            control.anchorMin = new Vector2(x / parentWidth, 1 - (y + height) / parentHeight);
            control.anchorMax = new Vector2((x + width) / parentWidth, 1 - y / parentHeight);
            control.offsetMin = control.offsetMax = Vector2.zero;
        }

        static void FitLobbyCard(RectTransform card, Vector2 min, Vector2 max)
        {
            TrialMenuLayout.StretchContents(card);
            card.anchorMin = min;
            card.anchorMax = max;
            card.offsetMin = card.offsetMax = Vector2.zero;
        }
    }
}
