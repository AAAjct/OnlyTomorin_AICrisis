using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TomorinTrial
{
    // The host owns the existing combat simulation. Peers send controls, never damage or transforms.
    public sealed partial class TrialNetworkManager : NetworkManager
    {
        public const string MenuScene = "Assets/Scenes/InkStart.unity";
        public const string GameScene = "Assets/Scenes/AshenCourt.unity";
        public static TrialNetworkManager Current => singleton as TrialNetworkManager;
        public static bool SessionActive => NetworkServer.active || NetworkClient.active;
        static bool reopenMultiplayerMenu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMultiplayerMenuRequest() => reopenMultiplayerMenu = false;

        internal static bool ConsumeMultiplayerMenuRequest()
        {
            bool reopen = reopenMultiplayerMenu;
            reopenMultiplayerMenu = false;
            return reopen;
        }

        public string Status { get; private set; } = "尚未连接";
        public string[] Members { get; private set; } = Array.Empty<string>();
        public int LocalId { get; private set; } = -1;
        internal float LocalRespawnRemaining
        {
            get
            {
                if(NetworkServer.active)return RespawnRemaining(LocalId);
                if(hasSnapshot)
                    foreach(var player in latest.players)
                        if(player.id==LocalId)return player.respawnRemaining;
                return 0;
            }
        }

        float RespawnRemaining(int id)=>reviveAt.TryGetValue(id,out var deadline)?Mathf.Max(0,deadline-Time.time):0;
        public const int MinRoomCapacity = 2;
        public const int MaxRoomCapacity = 16;
        public int RoomCapacity { get; private set; } = 8;
        int remoteRoomCapacity;
        public int ActiveRoomCapacity => IsHost ? RoomCapacity : remoteRoomCapacity;
        public int RoomCapacityFloor => IsHost ? Mathf.Max(MinRoomCapacity, NetworkServer.connections.Count) : MinRoomCapacity;
        public bool CanAdjustRoomCapacity => !Busy || (IsHost && SceneManager.GetActiveScene().path == MenuScene &&
            (!NetworkServer.isLoadingScene || networkSceneName == MenuScene));
        public bool IsHost => NetworkServer.active;
        public bool Busy => preparingRelay || SessionActive;
        public ushort GamePort => directTransport.port;
        public string PlayerName { get; set; }
        internal static string SavedPlayerName
        {
            get
            {
                string value=PlayerPrefs.GetString("Ink.Name","");
                if(!string.IsNullOrWhiteSpace(value) && value!="旅人" && value!="房主")return value;
                value="玩家"+Guid.NewGuid().ToString("N").Substring(0,8);
                PlayerPrefs.SetString("Ink.Name",value);
                PlayerPrefs.Save();
                return value;
            }
        }
        public TrialLevel SelectedLevel { get; private set; }
        public TrialDifficulty SelectedDifficulty { get; private set; }
        public void SelectDifficulty(TrialDifficulty difficulty)
        {
            if(difficulty>TrialDifficulty.Hard || Busy && (!IsHost || SceneManager.GetActiveScene().path!=MenuScene))return;
            SelectedDifficulty=difficulty;if(IsHost)BroadcastLobby();Changed?.Invoke();
        }
        public string LevelName => TrialChampionCatalog.LevelName(SelectedLevel);
        public void SelectLevel(TrialLevel level)
        {
            if(level>TrialLevel.BossRush)return;
            if(Busy && (!IsHost || SceneManager.GetActiveScene().path!=MenuScene))return;
            SelectedLevel=level;if(IsHost)BroadcastLobby();Changed?.Invoke();
        }
        public event Action Changed;
        readonly Dictionary<int, string> names = new Dictionary<int, string>();
        readonly Dictionary<int, TrialOutfit> outfits = new Dictionary<int, TrialOutfit>();
        readonly Dictionary<int, TrialPlayer> actors = new Dictionary<int, TrialPlayer>();
        readonly Dictionary<int, TrialInput> inputs = new Dictionary<int, TrialInput>();
        readonly Dictionary<int, TrialControls> controls = new Dictionary<int, TrialControls>();
        readonly Dictionary<int, Queue<TrialControls>> controlEdges = new Dictionary<int, Queue<TrialControls>>();
        readonly Dictionary<int, float> lastInput = new Dictionary<int, float>();
        readonly Dictionary<int, float> reviveAt = new Dictionary<int, float>();
        readonly HashSet<int> liveActors = new HashSet<int>();
        readonly List<int> removedActors = new List<int>();
        TrialGame boundGame;
        float nextSend, nextHeartbeat, snapshotAt, connectDeadline;
        bool intentionalStop;
        TrialControls pending;
        TrialControls lastSent;
        TrialWorldState latest;
        bool hasSnapshot;
        bool previousBusy;
        bool joinedServer;
        int payloadSerial;
        readonly TrialPayloadJoiner worldJoiner=new TrialPayloadJoiner(),lobbyJoiner=new TrialPayloadJoiner();
        string connectionFailure;
        string transportFailure;

        public override void Awake()
        {
            if(singleton && singleton!=this){Destroy(gameObject);return;}
            base.Awake();
            directTransport = transport as TelepathyTransport;
            PlayerName=SavedPlayerName;
            Application.runInBackground=true;
            Application.logMessageReceivedThreaded += CaptureTransportFailure;
        }

        public override void OnDestroy()
        {
            CancelRelay();
            Application.logMessageReceivedThreaded -= CaptureTransportFailure;
            base.OnDestroy();
        }

        // Telepathy reports socket failures from its receive thread through logs,
        // without invoking Transport.OnClientError. Consume them on the main thread.
        void CaptureTransportFailure(string message, string stackTrace, LogType type)
        {
            if (!message.StartsWith("Client Recv: failed to connect to ip=", StringComparison.Ordinal)) return;
            int reason = message.IndexOf("reason=", StringComparison.Ordinal);
            if (reason < 0) return;
            string detail = message.Substring(reason + 7).Split('\n', '\r')[0];
            Interlocked.Exchange(ref transportFailure, detail);
        }

        void ResetConnectionAttempt()
        {
            worldJoiner.Reset();lobbyJoiner.Reset();
            intentionalStop = false; joinedServer = false; connectionFailure = null;
            Interlocked.Exchange(ref transportFailure, null);
        }

        public static bool TryPort(string text, out ushort port) => ushort.TryParse(text, out port) && port > 0;
        public static bool TryFindFreeTcpPort(out int port)
        {
            const int minPort=10000;
            const int maxPort=30000;
            const int portCount=maxPort-minPort+1;
            int start=new System.Random().Next(portCount);
            port=0;
            for(int i=0;i<portCount;i++)
            {
                int candidate=minPort+(start+i)%portCount;
                TcpListener listener=null;
                try
                {
                    listener=TcpListener.Create(candidate);
                    listener.ExclusiveAddressUse=true;
                    listener.Start();
                    port=candidate;
                    return true;
                }
                catch(SocketException error)
                {
                    if(error.SocketErrorCode!=SocketError.AddressAlreadyInUse &&
                       error.SocketErrorCode!=SocketError.AccessDenied)return false;
                }
                finally {listener?.Stop();}
            }
            return false;
        }
        public bool Configure(string address, string portText, string nickname) => ConfigureConnection(address, portText, nickname, joining: true);
        internal bool ConfigureHostPort(string portText, string nickname) => ConfigureConnection(null, portText, nickname, joining: false);

        bool ConfigureConnection(string address, string portText, string nickname, bool joining)
        {
            if (Busy) return false;
            if (!TryPort(portText, out var port)) { SetStatus("端口请输入 1–65535 之间的整数"); return false; }
            if (joining)
            {
                address = (address ?? "").Trim();
                if (address.Length == 0 || address.Contains("://") || address.Contains("/") || address.Contains(" "))
                { SetStatus("请输入有效的 IP 或域名"); return false; }
                networkAddress = address;
                PlayerPrefs.SetString("Ink.Address", address);
            }
            directTransport.port = port;
            PlayerPrefs.SetInt("Ink.Port", port);
            SaveNickname(nickname);
            return true;
        }
        void SaveNickname(string nickname)
        {
            var value = string.IsNullOrWhiteSpace(nickname) ? SavedPlayerName : nickname.Trim();
            PlayerName = value.Substring(0, Math.Min(12, value.Length));
            PlayerPrefs.SetString("Ink.Name", PlayerName); PlayerPrefs.Save();
        }
        public void AdjustRoomCapacity(int change)
        {
            if (!CanAdjustRoomCapacity) return;
            RoomCapacity = (int)Math.Clamp((long)RoomCapacity + change, RoomCapacityFloor, MaxRoomCapacity);
            if (IsHost)
            {
                maxConnections = NetworkServer.maxConnections = RoomCapacity;
                BroadcastLobby();
            }
            Changed?.Invoke();
        }

        public void Host()
        {
            if (Busy) return;
            SelectDirectTransport();
            ResetConnectionAttempt();
            // Telepathy binds on a background thread; check synchronously so a
            // denied/occupied port cannot produce a successful local-only host.
            TcpListener listener = null;
            try { listener = TcpListener.Create(GamePort); listener.Start(); }
            catch (SocketException e)
            {
                string cause = e.SocketErrorCode == SocketError.AccessDenied
                    ? "端口被系统保留或禁止使用，请更换本地端口并同步修改穿透映射"
                    : e.SocketErrorCode == SocketError.AddressAlreadyInUse ? "端口已被占用，请更换本地端口" : e.Message;
                SetStatus($"创建失败：TCP {GamePort} · {cause}");
                Debug.LogWarning($"[InkNet] Listen failed: {e.SocketErrorCode} ({e.NativeErrorCode}) {e.Message}");
                return;
            }
            finally { listener?.Stop(); }
            maxConnections = RoomCapacity;
            SetStatus("正在创建房间…");
            try { StartHost(); if (!transport.ServerActive()) { Leave(); SetStatus("创建失败：端口可能已被占用，请更换端口"); } }
            catch (Exception e) { Leave(); SetStatus("创建失败：" + e.Message); }
        }
        public void Join()
        {
            if (Busy) return;
            SelectDirectTransport();
            ResetConnectionAttempt(); connectDeadline = Time.unscaledTime + 12;
            SetStatus("正在连接 " + networkAddress + ":" + GamePort + "…");
            try { StartClient(); } catch (Exception e) { Leave(); SetStatus("连接失败：" + e.Message); }
        }
        public void BeginTogether()
        {
            if (!PublicRoomReady || (UsingRelay && string.IsNullOrEmpty(RelayRoomCode))) return;
            if(SelectedLevel==TrialLevel.Arena && names.Count<2){SetStatus("竞技场至少需要 2 名玩家，请等待对手加入");return;}
            if (NetworkServer.active && SceneManager.GetActiveScene().path == MenuScene) ServerChangeScene(TrialChampionCatalog.Scene(SelectedLevel));
        }
        public bool WaitingForBoons
        {
            get
            {
                if(!boundGame || boundGame.IsArena)return false;
                if(!NetworkServer.active)return hasSnapshot && latest.waitingForBoons;
                foreach(var id in names.Keys)
                    if(!NetworkServer.connections.TryGetValue(id,out var connection) || !connection.isReady ||
                       !actors.TryGetValue(id,out var actor) || !actor || actor.ChoosingBoon && !actor.ChoosingRunAffix)return true;
                return false;
            }
        }
        public void ReturnToRoom()
        {
            if(!boundGame || boundGame.Mode!=TrialMode.Victory)return;
            if(NetworkServer.active)ReturnTogether();
            else if(NetworkClient.isConnected && NetworkClient.ready)NetworkClient.Send(new TrialReturnToRoom());
        }
        void ReturnTogether()
        {
            if(!boundGame || boundGame.Mode!=TrialMode.Victory)return;
            Time.timeScale=1;controls.Clear();controlEdges.Clear();lastInput.Clear();reviveAt.Clear();hasSnapshot=false;
            boundGame=null;actors.Clear();inputs.Clear();pending=lastSent=default;
            ServerChangeScene(MenuScene);
        }
        public void RetryChallenge()
        {
            if(NetworkServer.active)RestartTogether();
            // Retry is a host-only decision; reject it at both UI and protocol boundaries.
        }
        public void RestorePartyForBossRound()
        {
            foreach(var pair in actors)if(pair.Value)pair.Value.RestoreForBossRound(boundGame.CoopSpawn(pair.Key));
            reviveAt.Clear();
        }
        public void RunUpgrade(int slot)
        {
            if(NetworkServer.active && boundGame)ApplyRunUpgrade(boundGame.Player,slot);
            else if(NetworkClient.isConnected && NetworkClient.ready)NetworkClient.Send(new TrialRunUpgrade{slot=slot});
        }
        static void ApplyRunUpgrade(TrialPlayer actor,int slot)
        {
            if(slot==-1)actor.OpenRunUpgrade();
            else if(slot==-2)actor.CloseRunUpgrade();
            else actor.ChooseRunAffix(slot);
        }
        public void ChooseBoon(TrialBoon boon)
        {
            if(NetworkServer.active && boundGame)boundGame.Player.ChooseBoon(boon);
            else if(NetworkClient.isConnected && NetworkClient.ready)NetworkClient.Send(new TrialChooseBoon{boon=boon});
        }
        void RestartTogether()
        {
            if(!boundGame || (boundGame.Mode!=TrialMode.Victory && boundGame.Mode!=TrialMode.Dead))return;
            controls.Clear();controlEdges.Clear();lastInput.Clear();reviveAt.Clear();pending=lastSent=default;
            boundGame.SetDifficulty(SelectedDifficulty);boundGame.ConfigureSession(SelectedLevel);boundGame.SetPartySize(names.Count);
            foreach(var pair in actors)
                if(pair.Value && pair.Value!=boundGame.Player){pair.Value.ResetSpellProgress();pair.Value.ResetRunBoon();pair.Value.ResetRoguelike();pair.Value.RollRunBoons();pair.Value.Restore(boundGame.CoopSpawn(pair.Key));}
            nextSend=0;
        }
        public void Leave() => Leave(reopenMenu: true);

        internal void Leave(bool reopenMenu)
        {
            reopenMultiplayerMenu = reopenMenu && SessionActive &&
                (SceneManager.GetActiveScene().path != MenuScene || loadingSceneAsync != null);
            CancelRelay();
            intentionalStop = true;
            connectDeadline = 0;
            if (NetworkServer.active && NetworkClient.active) StopHost();
            else if (NetworkClient.active) StopClient();
            else if (NetworkServer.active) StopServer();
            Members = Array.Empty<string>(); LocalId = -1; hasSnapshot = false;
            SetStatus("已离开房间");
        }

        public override void ServerChangeScene(string newSceneName)
        {
            // Mirror requests the offline scene on host shutdown even when it is already open.
            if (!NetworkServer.active && newSceneName == offlineScene && loadingSceneAsync == null &&
                SceneManager.GetActiveScene().path == newSceneName)
                return;

            base.ServerChangeScene(newSceneName);
        }

        public void SetStatus(string message) { Status = message; Changed?.Invoke(); Debug.Log("[InkNet] " + message); }
        public override void OnStartServer()
        {
            names.Clear(); outfits.Clear(); controls.Clear(); controlEdges.Clear(); lastInput.Clear();
            NetworkServer.RegisterHandler<TrialHello>(ReceiveHello);
            NetworkServer.RegisterHandler<TrialControls>(ReceiveControls);
            NetworkServer.RegisterHandler<TrialRunUpgrade>((connection,message)=>{if(boundGame && boundGame.Mode==TrialMode.Playing && actors.TryGetValue(connection.connectionId,out var actor) && actor)ApplyRunUpgrade(actor,message.slot);});
            NetworkServer.RegisterHandler<TrialChooseBoon>((connection,message)=>{if(actors.TryGetValue(connection.connectionId,out var actor) && actor)actor.ChooseBoon(message.boon);});
            NetworkServer.RegisterHandler<TrialRetryChallenge>((connection,message)=>{if(connection is LocalConnectionToClient)RestartTogether();});
            NetworkServer.RegisterHandler<TrialReturnToRoom>((connection,message)=>{if(names.ContainsKey(connection.connectionId))ReturnTogether();});
            SetStatus(HostStatus);
        }
        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<TrialLobby>(ReceiveLobby);
            NetworkClient.RegisterHandler<TrialWorldState>(ReceiveWorld);
            NetworkClient.RegisterHandler<TrialCombatEvent>(ReceiveCombat);
            NetworkClient.RegisterHandler<TrialPayloadPart>(value=>
            {
                if(value.kind>1)return;
                if(!(value.kind==0?worldJoiner:lobbyJoiner).Add(value,out var bytes))return;
                using(var reader=NetworkReaderPool.Get(new ArraySegment<byte>(bytes)))
                {if(value.kind==0)ReceiveWorld(reader.Read<TrialWorldState>());else ReceiveLobby(reader.Read<TrialLobby>());}
            });
        }
        public override void OnClientConnect()
        {
            base.OnClientConnect();
            NetworkClient.Send(new TrialHello { name = PlayerName, outfit = TrialAppearance.Saved });
            SetStatus(NetworkServer.active ? (UsingRelay ? HostStatus : HostStatus + " · 等待同伴")
                : (UsingRelay ? "Relay" : "TCP") + " 已连接 · 正在加入房间…");
        }
        void ReceiveHello(NetworkConnectionToClient connection, TrialHello message)
        {
            string value = (message.name ?? "旅人").Replace("<", "").Replace(">", "").Replace("\n", "").Replace("\r", "");
            names[connection.connectionId] = value.Substring(0, Math.Min(12, value.Length));
            outfits[connection.connectionId] = TrialAppearance.Normalize(message.outfit);
            if(boundGame && NetworkServer.active)boundGame.SetPartySize(names.Count);
            BroadcastLobby();
        }
        void BroadcastLobby()
        {
            var ids = new int[names.Count]; var labels = new string[names.Count]; var looks = new TrialOutfit[names.Count]; int i = 0;
            foreach (var item in names) { ids[i] = item.Key; labels[i] = item.Value; looks[i++] = OutfitOf(item.Key); }
            foreach (var connection in NetworkServer.connections.Values)
            {
                var state=new TrialLobby { self = connection.connectionId, ids = ids, names = labels, outfits = looks, level=SelectedLevel, difficulty=SelectedDifficulty, capacity=RoomCapacity, roomName=RoomName };
                if(ids.Length<=128)connection.Send(state);
                else foreach(var part in TrialPayloadParts.Split(state,1,payloadSerial++))connection.Send(part);
            }
        }
        void ReceiveLobby(TrialLobby message)
        {
            LocalId = message.self; Members = message.names; joinedServer = true; connectDeadline = 0;
            SelectedLevel=message.level;SelectedDifficulty=message.difficulty;
            if(!NetworkServer.active)
            {
                remoteRoomCapacity = message.capacity;
                remoteRoomName = string.IsNullOrWhiteSpace(message.roomName) ? "房间" : message.roomName;
                names.Clear(); outfits.Clear();
                for(int i=0;i<Math.Min(message.ids.Length,message.names.Length);i++)
                {
                    int id=message.ids[i];
                    names[id]=message.names[i];
                    if(message.outfits!=null && i<message.outfits.Length)outfits[id]=TrialAppearance.Normalize(message.outfits[i]);
                }
            }
            SetStatus(NetworkServer.active ? (UsingRelay ? HostStatus : HostStatus + " · 等待同伴") : "已加入房间 · 等待房主启程");
        }
        void ReceiveControls(NetworkConnectionToClient connection, TrialControls value)
        {
            int id = connection.connectionId;
            if (!names.ContainsKey(id) || !float.IsFinite(value.yaw) || !float.IsFinite(value.move.x) || !float.IsFinite(value.move.y)) return;
            // Several TCP packets can arrive in one frame. OR-ing their edges
            // loses repeated light taps and reverses L,L,H; retain packet order.
            lastInput[id] = Time.unscaledTime;
            value.move = Vector2.ClampMagnitude(value.move, 1);
            controls.TryGetValue(id,out var previous);
            if(value.pressed!=0 || value.spells!=0 || value.held!=previous.held)
            {
                if(!controlEdges.TryGetValue(id,out var edges))controlEdges[id]=edges=new Queue<TrialControls>();
                if(edges.Count<8)edges.Enqueue(value);
            }
            value.pressed=value.spells=0;
            controls[id] = value;
        }
        public override void OnServerDisconnect(NetworkConnectionToClient connection)
        {
            int id = connection.connectionId;
            names.Remove(id); outfits.Remove(id); controls.Remove(id); controlEdges.Remove(id); lastInput.Remove(id); reviveAt.Remove(id);
            if (actors.TryGetValue(id, out var actor))
            {
                // The scene owns the local player; its HUD/camera still use it
                // until StopHost finishes loading the menu.
                if (actor && boundGame && actor != boundGame.Player) Destroy(actor.gameObject);
                actors.Remove(id);
            }
            if (inputs.TryGetValue(id, out var input)) { if (input) Destroy(input); inputs.Remove(id); }
            base.OnServerDisconnect(connection); BroadcastLobby();
            if(boundGame)boundGame.SetPartySize(names.Count);
        }
        public override void OnClientDisconnect()
        {
            connectDeadline = 0; Members = Array.Empty<string>(); hasSnapshot = false;
            string detail = Interlocked.Exchange(ref transportFailure, null);
            if (!string.IsNullOrEmpty(detail)) connectionFailure = $"连接失败：{networkAddress}:{GamePort} · {detail}";
            SetStatus(intentionalStop ? "已离开房间" : connectionFailure ?? (joinedServer
                ? "连接已断开：房主或网络关闭了连接"
                : UsingRelay ? "连接失败：未完成 Relay 房间握手，请检查房间码和游戏版本"
                : $"连接失败：{networkAddress}:{GamePort} 未完成房间握手。房主请确认本地监听端口与穿透目标一致"));
        }
        public override void OnClientError(TransportError error, string reason)
        {
            connectionFailure = $"连接失败：{error} · {reason}";
            SetStatus(connectionFailure);
        }
        public override void OnServerError(NetworkConnectionToClient connection, TransportError error, string reason) { SetStatus("网络错误：" + reason); }
        public override void OnStopServer() { CancelRelay(); names.Clear(); outfits.Clear(); controls.Clear(); controlEdges.Clear(); lastInput.Clear(); actors.Clear(); inputs.Clear(); reviveAt.Clear(); boundGame = null; }
        public override void OnStopClient() { actors.Clear(); inputs.Clear(); outfits.Clear(); boundGame = null; Members=Array.Empty<string>();Changed?.Invoke(); }
        public override void Update()
        {
            base.Update();
            UpdateRelayHost();
            UpdatePublicListing();
            if (!UsingRelay && NetworkServer.active && !transport.ServerActive())
            { Leave(); SetStatus("创建失败：TCP 监听已停止，请检查本地端口是否被系统保留或占用，并同步修改穿透映射"); }
            if (connectDeadline > 0 && !joinedServer && Time.unscaledTime > connectDeadline)
            {
                string failure = UsingRelay ? "连接失败：Relay 连接超时，请确认房间仍在线且游戏版本一致"
                    : NetworkClient.isConnected ? "连接失败：房间握手超时，请确认穿透目标是游戏监听端口，且双方游戏版本一致"
                    : "连接超时，请检查地址、TCP 端口、防火墙或穿透映射";
                Leave(); SetStatus(failure);
            }
        }
        public override void LateUpdate()
        {
            base.LateUpdate();
            if(previousBusy!=Busy){previousBusy=Busy;Changed?.Invoke();}
        }
        void Bind(TrialGame game)
        {
            if (boundGame == game) return;
            boundGame = game; actors.Clear(); inputs.Clear(); controlEdges.Clear(); reviveAt.Clear(); hasSnapshot = false;
            pending = lastSent = default; nextSend = nextHeartbeat = 0; liveActors.Clear(); removedActors.Clear(); game.SetDifficulty(SelectedDifficulty);game.ConfigureSession(SelectedLevel);game.SetPartySize(names.Count);
        }
        public void TickGame(TrialGame game)
        {
            Bind(game);
            if(game.Input.Upgrade && !game.Player.ChoosingBoon)RunUpgrade(-1);
            bool paused = game.Mode == TrialMode.Paused;
            if (game.Input.Pause && game.Mode != TrialMode.Victory && game.Mode != TrialMode.Dead) { game.HandlePauseInput(); paused = game.Mode == TrialMode.Paused; }
            if (NetworkServer.active) TickHost(game, paused);
            else TickPeer(game, paused);
        }
        void TickHost(TrialGame game, bool paused)
        {
            if(game.PartySize!=Mathf.Max(1,names.Count))game.SetPartySize(names.Count);
            CheckPartyDefeat(game);
            bool waiting=WaitingForBoons;
            foreach (var member in names)
            {
                int id = member.Key;
                if (!actors.TryGetValue(id, out var actor) || !actor)
                {
                    actor = id == LocalId ? game.Player : game.CreateCompanion(id,OutfitOf(id));
                    actors[id] = actor;
                    if(game.IsArena){actor.Restore(SafestArenaSpawn(game,id));actor.GrantSpawnProtection();}
                    if (id != LocalId) inputs[id] = game.gameObject.AddComponent<TrialInput>();
                }
                TrialInput input;
                if (id == LocalId) input = game.Input;
                else
                {
                    var state = controls.TryGetValue(id, out var received) ? received : default;
                    if (!lastInput.TryGetValue(id, out var at) || Time.unscaledTime - at > .5f)
                    {state=default;if(controlEdges.TryGetValue(id,out var stale))stale.Clear();}
                    else if(controlEdges.TryGetValue(id,out var edges) && edges.Count>0)state=edges.Dequeue();
                    input = inputs[id]; input.ApplyNetwork(state);
                }
                if (game.HasResult)
                {
                    if(game.ResultInProgress)actor.AdvanceResultPose(Time.deltaTime);
                    continue;
                }
                if(waiting || actor.ChoosingBoon)continue;
                if (!actor.Alive)
                {
                    if (!reviveAt.ContainsKey(id)) reviveAt[id] = Time.time + TrialGame.RespawnDelay;
                    if (Time.time >= reviveAt[id]) { if(game.IsBossRush)actor.RestoreForBossRound(game.CoopSpawn(id));else actor.Restore(game.IsArena?SafestArenaSpawn(game,id):game.CoopSpawn(id));if(game.IsArena)actor.GrantSpawnProtection();reviveAt.Remove(id); }
                    continue;
                }
                reviveAt.Remove(id);
                if (id == LocalId) input.SetNetworkYaw(game.CameraRig.transform.eulerAngles.y);
                if (!(id == LocalId && paused)) actor.Tick(Time.deltaTime, input);
                if (!game.IsArena && !game.IsRunStage && input.Interact && Vector3.Distance(actor.transform.position, game.World.ShrinePosition) < 2.6f && !HasNearbyEnemy(game,actor))
                { actor.Restore(game.World.ShrinePosition + Vector3.right * 1.5f); }
            }
            CheckPartyDefeat(game);
            if (!waiting && !WaitingForBoons && game.Mode != TrialMode.Victory && game.Mode != TrialMode.Dead)
            {
                game.AdvanceNetworkTime(Time.deltaTime);
                if(!game.IsArena)foreach (var enemy in game.Enemies)
                {
                    game.SetCombatTarget(NearestPlayer(enemy.transform.position, game.Player));
                    enemy.Tick(Time.deltaTime);
                    if(game.HasResult)break;
                }
                game.SetCombatTarget(game.Player);
                CheckPartyDefeat(game);
                if(game.Run!=null && !game.HasResult && !WaitingForBoons)game.Run.Tick(Time.deltaTime);
            }
            if (!paused && game.Mode!=TrialMode.Victory && game.Mode!=TrialMode.Dead && !game.Player.ChoosingBoon) game.CameraRig.Look(game.Input.Look);
            if (Time.unscaledTime < nextSend) return;
            // Preserve cadence across frame jitter instead of rounding a 60 Hz
            // deadline up to every second rendered frame.
            nextSend = Mathf.Max(nextSend + 1f / (game.IsChampionStage?30:60), Time.unscaledTime);
            bool hasPeer=false;
            foreach(var connection in NetworkServer.connections.Values)
                if(connection.isReady && !(connection is LocalConnectionToClient)){hasPeer=true;break;}
            if(!hasPeer)return;
            var players = new TrialActorState[actors.Count]; int index = 0;
            foreach (var item in actors)
            {
                var state=item.Value.CaptureNetwork(item.Key);
                state.respawnRemaining=item.Value.Alive?0:RespawnRemaining(item.Key);
                players[index++]=state;
            }
            var enemies = new TrialActorState[game.Enemies.Count];
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = game.Enemies[i];
                enemies[i] = enemy.CaptureNetwork(game.IsRunStage ? game.RunEnemyId(enemy) : i);
                enemies[i].awake = enemy.Awakened;
            }
            var fish=enemies.Length>0?game.Enemies[enemies.Length-1].Boss:null;
            var champions=game.IsChampionStage || game.IsRunStage?new TrialChampionState[enemies.Length]:Array.Empty<TrialChampionState>();
            for(int i=0;i<champions.Length;i++)champions[i]=game.Enemies[i].Champion?game.Enemies[i].Champion.CaptureNetwork():default;
            var runBosses=game.IsRunStage?new TrialBossState[enemies.Length]:Array.Empty<TrialBossState>();
            for(int i=0;i<runBosses.Length;i++)runBosses[i]=game.Enemies[i].Boss?game.Enemies[i].Boss.CaptureNetwork():default;
            var snapshot = new TrialWorldState { players = players, enemies = enemies, boss = fish?fish.CaptureNetwork():default, champions=champions, runBosses=runBosses,runWave=game.Run?.Wave??0,runKills=game.Run?.Kills??0,runBossesDefeated=game.Run?.BossesDefeated??0, gate = game.GateOpen, arena = game.World.ArenaEntrance.activeSelf, guards = game.DefeatedGuards, victory = game.Mode == TrialMode.Victory, defeat = game.Mode == TrialMode.Dead, seconds = game.PlaySeconds,level=game.Level,difficulty=game.Difficulty,partySize=game.PartySize,waitingForBoons=WaitingForBoons };
            snapshot.frostOrbs=game.Run?.FrostOrbs?.CaptureNetwork();
            var fragments=UsingRelay || players.Length>48 || game.IsRunStage?TrialPayloadParts.Split(snapshot,0,payloadSerial++):null;
            foreach (var connection in NetworkServer.connections.Values)
                if (connection.isReady && !(connection is LocalConnectionToClient))
                {if(fragments==null)connection.Send(snapshot);else foreach(var part in fragments)connection.Send(part);}
        }
        void CheckPartyDefeat(TrialGame game)
        {
            if(game.IsArena || names.Count==0 || (game.Mode!=TrialMode.Playing && game.Mode!=TrialMode.Paused))return;
            // Missing actors are still joining. Decide before respawn timers run,
            // and again after combat, so a team wipe cannot revive itself.
            foreach(var id in names.Keys)
                if(!actors.TryGetValue(id,out var actor) || !actor || actor.Alive)return;
            reviveAt.Clear();game.EnterDefeat();
        }
        static bool HasNearbyEnemy(TrialGame game,TrialPlayer player)
        {foreach(var enemy in game.Enemies)if(enemy.Alive && enemy.Awakened && (enemy.transform.position-player.transform.position).sqrMagnitude<81)return true;return false;}
        Vector3 SafestArenaSpawn(TrialGame game,int id)
        {
            Vector3 best=game.ArenaSpawn(id);float distance=-1;
            for(int i=0;i<16;i++)
            {
                var point=game.ArenaSpawn(id+i);float nearest=float.MaxValue;
                foreach(var pair in actors)if(pair.Key!=id && pair.Value && pair.Value.Alive)nearest=Mathf.Min(nearest,(pair.Value.transform.position-point).sqrMagnitude);
                if(nearest>distance){distance=nearest;best=point;}
            }
            return best;
        }
        public string NameOf(TrialPlayer player)
        {foreach(var pair in actors)if(pair.Value==player)return names.TryGetValue(pair.Key,out var label)?label:"旅人";return "旅人";}
        TrialOutfit OutfitOf(int id)=>outfits.TryGetValue(id,out var outfit)?outfit:TrialOutfit.Default;
        public string ArenaScoreboard()
        {
            string result="竞技场 · 自由决斗";
            foreach(var pair in actors)if(pair.Value)result+=$"\n{NameOf(pair.Value)}  {pair.Value.Kills} 击杀 / {pair.Value.Deaths} 阵亡";
            return result;
        }
        TrialPlayer NearestPlayer(Vector3 point, TrialPlayer fallback)
        {
            TrialPlayer nearest = fallback; float best = float.MaxValue;
            foreach (var actor in actors.Values) if (actor && actor.Alive && !actor.ChoosingBoon) { float d = (actor.transform.position - point).sqrMagnitude; if (d < best) { nearest = actor; best = d; } }
            return nearest;
        }
        public Dictionary<int,TrialPlayer>.ValueCollection Players => actors.Values;
        internal TrialPlayer PlayerById(int id)=>actors.TryGetValue(id,out var actor)?actor:null;
        internal int IdOf(TrialPlayer actor)
        {
            foreach(var pair in actors)if(pair.Value==actor)return pair.Key;
            return int.MinValue;
        }
        public void BroadcastSkillImpact(Vector3 point,Color tint)
            =>BroadcastEnemyCombat(new TrialCombatEvent{player=-1,cue=TrialCombatCue.SkillImpact,position=point,forward=new Vector3(tint.r,tint.g,tint.b)});
        internal void BroadcastEnemyCombat(TrialCombatEvent value)
        {
            if(!NetworkServer.active || !boundGame)return;
            boundGame.Effects.PlayCombat(value,false);
            foreach(var connection in NetworkServer.connections.Values)
                if(connection.isReady && !(connection is LocalConnectionToClient))connection.Send(value);
        }
        public void BroadcastCombat(TrialPlayer player, TrialCombatCue cue,Vector3? point=null,Vector3? direction=null)
        {BroadcastSwordCombat(player,cue,point??player.transform.position,direction??player.transform.forward,Vector3.zero,0);}
        public void BroadcastSwordCombat(TrialPlayer player,TrialCombatCue cue,Vector3 point,Vector3 direction,Vector3 end,float amount,byte style=0,int target=0)
        {
            if (!NetworkServer.active || !boundGame) return;
            foreach (var actor in actors)
            {
                if (actor.Value != player) continue;
                var value = new TrialCombatEvent { player = actor.Key, cue = cue, position = point, forward = direction,end=end,amount=amount,style=style,target=target };
                boundGame.Effects.PlayCombat(value, actor.Key == LocalId);
                foreach (var connection in NetworkServer.connections.Values)
                    if (connection.isReady && !(connection is LocalConnectionToClient)) connection.Send(value);
                return;
            }
        }
        void ReceiveCombat(TrialCombatEvent value)
        {
            if (NetworkServer.active || !boundGame || !boundGame.Ready) return;
            boundGame.Effects.PlayCombat(value, value.player == LocalId);
        }
        void ReceiveWorld(TrialWorldState state) { if (NetworkServer.active) return; latest = state; snapshotAt = Time.unscaledTime; hasSnapshot = true; }
        void TickPeer(TrialGame game, bool paused)
        {
            bool blocked=paused || game.Mode==TrialMode.Victory || game.Mode==TrialMode.Dead || game.Player.ChoosingBoon || WaitingForBoons;
            if (!blocked) game.CameraRig.Look(game.Input.Look);
            var current = blocked ? default : game.Input.CaptureNetwork(game.CameraRig.transform.eulerAngles.y);
            pending.move = current.move; pending.yaw = current.yaw; pending.held = current.held; pending.pressed |= current.pressed;
            pending.spells = blocked ? (byte)0 : (byte)(pending.spells | current.spells);
            bool edge = pending.pressed != 0 || pending.spells != 0 || pending.held != lastSent.held;
            bool changed = pending.move != lastSent.move || Mathf.Abs(Mathf.DeltaAngle(pending.yaw, lastSent.yaw)) > .1f;
            if (NetworkClient.isConnected && NetworkClient.ready && (edge || (changed && Time.unscaledTime >= nextSend) || Time.unscaledTime >= nextHeartbeat))
            {
                NetworkClient.Send(pending); lastSent = pending; pending.pressed = pending.spells = 0;
                nextSend = Mathf.Max(nextSend + 1f / 60,Time.unscaledTime); nextHeartbeat = Time.unscaledTime + .1f;
            }
            if (!hasSnapshot) return;
            if(game.Difficulty!=latest.difficulty)game.SetDifficulty(latest.difficulty);
            if(game.Level!=latest.level)game.ConfigureSession(latest.level);
            float snapshotAge = latest.waitingForBoons?0:Mathf.Min(Time.unscaledTime - snapshotAt, .1f);
            if(latest.victory || latest.defeat)snapshotAge=0;
            liveActors.Clear();
            foreach (var received in latest.players)
            {
                var state = received; state.actionTime += snapshotAge;
                state.runFrostRemaining=Mathf.Max(0,state.runFrostRemaining-snapshotAge);
                liveActors.Add(state.id);
                if (!actors.TryGetValue(state.id, out var actor) || !actor) { actor = state.id == LocalId ? game.Player : game.CreateCompanion(state.id,OutfitOf(state.id)); actors[state.id] = actor; }
                actor.ApplyNetwork(state, Time.deltaTime);
            }
            removedActors.Clear();
            foreach (var pair in actors) if (!liveActors.Contains(pair.Key)) { if (pair.Value && pair.Value != game.Player) Destroy(pair.Value.gameObject); removedActors.Add(pair.Key); }
            foreach (int id in removedActors) actors.Remove(id);
            if(game.IsRunStage)game.SyncRunReplicas(latest.enemies);
            for (int i = 0; i < Mathf.Min(latest.enemies.Length, game.Enemies.Count); i++)
            { var state = latest.enemies[i]; state.actionTime += snapshotAge; game.Enemies[i].ApplyReplica(state, Time.deltaTime); }
            var boss = latest.boss; boss.age += snapshotAge;
            var fish=!game.IsRunStage && game.Enemies.Count>0?game.Enemies[game.Enemies.Count-1].Boss:null;if(fish)fish.ApplyNetwork(boss);
            if(game.IsRunStage && latest.runBosses!=null)for(int i=0;i<Mathf.Min(latest.runBosses.Length,game.Enemies.Count);i++)if(game.Enemies[i].Boss)game.Enemies[i].Boss.ApplyNetwork(latest.runBosses[i]);
            if(game.Run!=null)game.Run.ApplyNetwork(latest.runWave,latest.runKills,latest.runBossesDefeated);
            game.Run?.FrostOrbs?.ApplyNetwork(latest.frostOrbs,snapshotAge);
            if(latest.champions!=null)for(int i=0;i<Mathf.Min(latest.champions.Length,game.Enemies.Count);i++)if(game.Enemies[i].Champion)game.Enemies[i].Champion.ApplyNetwork(latest.champions[i],snapshotAge);
            game.ApplyNetworkWorld(latest);
            game.Player.UpdateLock(!blocked && game.Input.Lock);
        }
        public static string LocalAddresses()
        {
            try { var values = new List<string>(); foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList) if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)) values.Add(ip.ToString()); return string.Join("  /  ", values); }
            catch { return "请查看系统网络设置中的 IPv4 地址"; }
        }
    }
}
