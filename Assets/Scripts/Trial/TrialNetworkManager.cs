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
    public sealed class TrialNetworkManager : NetworkManager
    {
        public const string MenuScene = "Assets/Scenes/InkStart.unity";
        public const string GameScene = "Assets/Scenes/AshenCourt.unity";
        public static TrialNetworkManager Current => singleton as TrialNetworkManager;
        public static bool SessionActive => NetworkServer.active || NetworkClient.active;
        public string Status { get; private set; } = "尚未连接";
        public string[] Members { get; private set; } = Array.Empty<string>();
        public int LocalId { get; private set; } = -1;
        public bool IsHost => NetworkServer.active;
        public bool Busy => NetworkClient.active || NetworkServer.active;
        public ushort GamePort => ((TelepathyTransport)transport).port;
        public string PlayerName { get; set; } = "旅人";
        public TrialLevel SelectedLevel { get; private set; }
        public string LevelName => SelectedLevel==TrialLevel.Arena?"竞技场":"庭院 (Boss · 大肥鱼)";
        public void SelectLevel(TrialLevel level)
        {
            if(level!=TrialLevel.BigFish && level!=TrialLevel.Arena)return;
            if(Busy && (!IsHost || SceneManager.GetActiveScene().path!=MenuScene))return;
            SelectedLevel=level;if(IsHost)BroadcastLobby();Changed?.Invoke();
        }
        public event Action Changed;
        readonly Dictionary<int, string> names = new Dictionary<int, string>();
        readonly Dictionary<int, TrialPlayer> actors = new Dictionary<int, TrialPlayer>();
        readonly Dictionary<int, TrialInput> inputs = new Dictionary<int, TrialInput>();
        readonly Dictionary<int, TrialControls> controls = new Dictionary<int, TrialControls>();
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
            maxConnections=int.MaxValue;
            Application.runInBackground=true;
            Application.logMessageReceivedThreaded += CaptureTransportFailure;
        }

        public override void OnDestroy()
        {
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
        public bool Configure(string address, string portText, string nickname)
        {
            if (Busy) return false;
            if (!TryPort(portText, out var port)) { SetStatus("端口请输入 1–65535 之间的整数"); return false; }
            address = (address ?? "").Trim();
            if (address.Length == 0 || address.Contains("://") || address.Contains("/") || address.Contains(" "))
            { SetStatus("请输入 IP 或域名，端口填写在右侧"); return false; }
            networkAddress = address; ((TelepathyTransport)transport).port = port;
            PlayerName = string.IsNullOrWhiteSpace(nickname) ? "旅人" : nickname.Trim().Substring(0, Math.Min(12, nickname.Trim().Length));
            PlayerPrefs.SetString("Ink.Address", address); PlayerPrefs.SetInt("Ink.Port", port); PlayerPrefs.SetString("Ink.Name", PlayerName); PlayerPrefs.Save();
            return true;
        }
        public void Host()
        {
            if (Busy) return;
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
            SetStatus("正在创建房间…");
            try { StartHost(); if (!transport.ServerActive()) { Leave(); SetStatus("创建失败：端口可能已被占用，请更换端口"); } }
            catch (Exception e) { Leave(); SetStatus("创建失败：" + e.Message); }
        }
        public void Join()
        {
            if (Busy) return;
            ResetConnectionAttempt(); connectDeadline = Time.unscaledTime + 12;
            SetStatus("正在连接 " + networkAddress + ":" + GamePort + "…");
            try { StartClient(); } catch (Exception e) { Leave(); SetStatus("连接失败：" + e.Message); }
        }
        public void BeginTogether()
        {
            if(SelectedLevel==TrialLevel.Arena && names.Count<2){SetStatus("竞技场至少需要 2 名玩家，请等待对手加入");return;}
            if (NetworkServer.active && SceneManager.GetActiveScene().path == MenuScene) ServerChangeScene(GameScene);
        }
        public void RetryChallenge()
        {
            if(NetworkServer.active)RestartTogether();
            else if(NetworkClient.isConnected && NetworkClient.ready)NetworkClient.Send(new TrialRetryChallenge());
        }
        public void ChooseBoon(TrialBoon boon)
        {
            if(NetworkServer.active && boundGame)boundGame.Player.ChooseBoon(boon);
            else if(NetworkClient.isConnected && NetworkClient.ready)NetworkClient.Send(new TrialChooseBoon{boon=boon});
        }
        void RestartTogether()
        {
            if(!boundGame || boundGame.Mode!=TrialMode.Victory)return;
            controls.Clear();lastInput.Clear();reviveAt.Clear();pending=lastSent=default;
            boundGame.ConfigureSession(SelectedLevel);boundGame.SetPartySize(names.Count);
            foreach(var pair in actors)
                if(pair.Value && pair.Value!=boundGame.Player){pair.Value.ResetRunBoon();pair.Value.RollRunBoons();pair.Value.Restore(boundGame.CoopSpawn(pair.Key));}
            nextSend=0;
        }
        public void Leave()
        {
            intentionalStop = true;
            connectDeadline = 0;
            if (NetworkServer.active && NetworkClient.active) StopHost();
            else if (NetworkClient.active) StopClient();
            else if (NetworkServer.active) StopServer();
            Members = Array.Empty<string>(); LocalId = -1; hasSnapshot = false;
            SetStatus("已离开房间");
        }
        public void SetStatus(string message) { Status = message; Changed?.Invoke(); Debug.Log("[InkNet] " + message); }
        public override void OnStartServer()
        {
            names.Clear(); controls.Clear(); lastInput.Clear();
            NetworkServer.RegisterHandler<TrialHello>(ReceiveHello);
            NetworkServer.RegisterHandler<TrialControls>(ReceiveControls);
            NetworkServer.RegisterHandler<TrialChooseBoon>((connection,message)=>{if(actors.TryGetValue(connection.connectionId,out var actor) && actor)actor.ChooseBoon(message.boon);});
            NetworkServer.RegisterHandler<TrialRetryChallenge>((connection,message)=>{if(names.ContainsKey(connection.connectionId))RestartTogether();});
            SetStatus("房间已创建 · TCP " + GamePort);
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
            NetworkClient.Send(new TrialHello { name = PlayerName });
            SetStatus(NetworkServer.active ? "房间已创建 · TCP " + GamePort + " · 等待同伴" : "TCP 已连接 · 正在加入房间…");
        }
        void ReceiveHello(NetworkConnectionToClient connection, TrialHello message)
        {
            string value = (message.name ?? "旅人").Replace("<", "").Replace(">", "").Replace("\n", "").Replace("\r", "");
            names[connection.connectionId] = value.Substring(0, Math.Min(12, value.Length));
            if(boundGame && NetworkServer.active)boundGame.SetPartySize(names.Count);
            BroadcastLobby();
        }
        void BroadcastLobby()
        {
            var ids = new int[names.Count]; var labels = new string[names.Count]; int i = 0;
            foreach (var item in names) { ids[i] = item.Key; labels[i++] = item.Value; }
            foreach (var connection in NetworkServer.connections.Values)
            {
                var state=new TrialLobby { self = connection.connectionId, ids = ids, names = labels, level=SelectedLevel };
                if(ids.Length<=128)connection.Send(state);
                else foreach(var part in TrialPayloadParts.Split(state,1,payloadSerial++))connection.Send(part);
            }
        }
        void ReceiveLobby(TrialLobby message)
        {
            LocalId = message.self; Members = message.names; joinedServer = true; connectDeadline = 0;
            SelectedLevel=message.level;
            if(!NetworkServer.active){names.Clear();for(int i=0;i<Math.Min(message.ids.Length,message.names.Length);i++)names[message.ids[i]]=message.names[i];}
            SetStatus(NetworkServer.active ? "房间已创建 · TCP " + GamePort + " · 等待同伴" : "已加入房间 · 等待房主启程");
        }
        void ReceiveControls(NetworkConnectionToClient connection, TrialControls value)
        {
            int id = connection.connectionId;
            if (!names.ContainsKey(id) || !float.IsFinite(value.yaw) || !float.IsFinite(value.move.x) || !float.IsFinite(value.move.y)) return;
            // TCP may deliver several inputs in the same frame after jitter.
            // Keep the newest held state and every pending button edge.
            lastInput[id] = Time.unscaledTime;
            value.move = Vector2.ClampMagnitude(value.move, 1);
            if (controls.TryGetValue(id, out var previous)) value.pressed |= previous.pressed;
            controls[id] = value;
        }
        public override void OnServerDisconnect(NetworkConnectionToClient connection)
        {
            int id = connection.connectionId;
            names.Remove(id); controls.Remove(id); lastInput.Remove(id); reviveAt.Remove(id);
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
                : $"连接失败：{networkAddress}:{GamePort} 未完成房间握手。房主请确认本地监听端口与穿透目标一致"));
        }
        public override void OnClientError(TransportError error, string reason)
        {
            connectionFailure = $"连接失败：{error} · {reason}";
            SetStatus(connectionFailure);
        }
        public override void OnServerError(NetworkConnectionToClient connection, TransportError error, string reason) { SetStatus("网络错误：" + reason); }
        public override void OnStopServer() { names.Clear(); controls.Clear(); lastInput.Clear(); actors.Clear(); inputs.Clear(); reviveAt.Clear(); boundGame = null; }
        public override void OnStopClient() { actors.Clear(); inputs.Clear(); boundGame = null; Members=Array.Empty<string>();Changed?.Invoke(); }
        public override void Update()
        {
            base.Update();
            if (NetworkServer.active && !transport.ServerActive())
            { Leave(); SetStatus("创建失败：TCP 监听已停止，请检查本地端口是否被系统保留或占用，并同步修改穿透映射"); }
            if (connectDeadline > 0 && !joinedServer && Time.unscaledTime > connectDeadline)
            {
                string failure = NetworkClient.isConnected ? "连接失败：房间握手超时，请确认穿透目标是游戏监听端口，且双方游戏版本一致"
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
            boundGame = game; actors.Clear(); inputs.Clear(); reviveAt.Clear(); hasSnapshot = false;
            pending = lastSent = default; nextSend = nextHeartbeat = 0; liveActors.Clear(); removedActors.Clear(); game.ConfigureSession(SelectedLevel);game.SetPartySize(names.Count);
        }
        public void TickGame(TrialGame game)
        {
            Bind(game);
            bool paused = game.Mode == TrialMode.Paused;
            if (game.Input.Pause && game.Mode != TrialMode.Victory) { game.HandlePauseInput(); paused = game.Mode == TrialMode.Paused; }
            if (NetworkServer.active) TickHost(game, paused);
            else TickPeer(game, paused);
        }
        void TickHost(TrialGame game, bool paused)
        {
            game.SetPartySize(names.Count);
            foreach (var member in names)
            {
                int id = member.Key;
                if (!actors.TryGetValue(id, out var actor) || !actor)
                {
                    actor = id == LocalId ? game.Player : game.CreateCompanion(id);
                    actors[id] = actor;
                    if(game.IsArena){actor.Restore(SafestArenaSpawn(game,id));actor.GrantSpawnProtection();}
                    if (id != LocalId) inputs[id] = game.gameObject.AddComponent<TrialInput>();
                }
                TrialInput input;
                if (id == LocalId) input = game.Input;
                else
                {
                    var state = controls.TryGetValue(id, out var received) ? received : default;
                    if (!lastInput.TryGetValue(id, out var at) || Time.unscaledTime - at > .5f) state = default;
                    input = inputs[id]; input.ApplyNetwork(state); state.pressed = 0; controls[id] = state;
                }
                if (game.Mode == TrialMode.Victory) continue;
                if(actor.ChoosingBoon)continue;
                if (!actor.Alive)
                {
                    if (!reviveAt.ContainsKey(id)) reviveAt[id] = Time.time + (game.IsArena?3:5);
                    if (Time.time >= reviveAt[id]) { actor.Restore(game.IsArena?SafestArenaSpawn(game,id):game.CoopSpawn(id));if(game.IsArena)actor.GrantSpawnProtection();reviveAt.Remove(id); }
                    continue;
                }
                if (id == LocalId) input.SetNetworkYaw(game.CameraRig.transform.eulerAngles.y);
                if (!(id == LocalId && paused)) actor.Tick(Time.deltaTime, input);
                if (!game.IsArena && input.Interact && Vector3.Distance(actor.transform.position, game.World.ShrinePosition) < 2.6f && !HasNearbyEnemy(game,actor))
                { actor.Restore(game.World.ShrinePosition + Vector3.right * 1.5f); }
            }
            if (game.Mode != TrialMode.Victory)
            {
                game.AdvanceNetworkTime(Time.deltaTime);
                if(!game.IsArena)foreach (var enemy in game.Enemies) { game.SetCombatTarget(NearestPlayer(enemy.transform.position, game.Player)); enemy.Tick(Time.deltaTime); }
                game.SetCombatTarget(game.Player);
            }
            if (!paused && game.Mode!=TrialMode.Victory && !game.Player.ChoosingBoon) game.CameraRig.Look(game.Input.Look);
            if (Time.unscaledTime < nextSend) return;
            nextSend = Time.unscaledTime + 1f / 30;
            var players = new TrialActorState[actors.Count]; int index = 0;
            foreach (var item in actors) players[index++] = item.Value.CaptureNetwork(item.Key);
            var enemies = new TrialActorState[game.Enemies.Count];
            for (int i = 0; i < enemies.Length; i++) { enemies[i] = game.Enemies[i].CaptureNetwork(i); enemies[i].awake = game.Enemies[i].Awakened; }
            var snapshot = new TrialWorldState { players = players, enemies = enemies, boss = game.Enemies[enemies.Length - 1].Boss.CaptureNetwork(), gate = game.GateOpen, arena = game.World.ArenaEntrance.activeSelf, guards = game.DefeatedGuards, victory = game.Mode == TrialMode.Victory, seconds = game.PlaySeconds,level=game.Level,partySize=game.PartySize };
            var fragments=players.Length>48?TrialPayloadParts.Split(snapshot,0,payloadSerial++):null;
            foreach (var connection in NetworkServer.connections.Values)
                if (connection.isReady && !(connection is LocalConnectionToClient))
                {if(fragments==null)connection.Send(snapshot);else foreach(var part in fragments)connection.Send(part);}
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
        public IEnumerable<TrialPlayer> Players => actors.Values;
        internal TrialPlayer PlayerById(int id)=>actors.TryGetValue(id,out var actor)?actor:null;
        public void BroadcastCombat(TrialPlayer player, TrialCombatCue cue,Vector3? point=null)
        {
            if (!NetworkServer.active || !boundGame) return;
            foreach (var actor in actors)
            {
                if (actor.Value != player) continue;
                var value = new TrialCombatEvent { player = actor.Key, cue = cue, position = point??player.transform.position, forward = player.transform.forward };
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
            bool blocked=paused || game.Mode==TrialMode.Victory || game.Player.ChoosingBoon;
            if (!blocked) game.CameraRig.Look(game.Input.Look);
            var current = blocked ? default : game.Input.CaptureNetwork(game.CameraRig.transform.eulerAngles.y);
            pending.move = current.move; pending.yaw = current.yaw; pending.held = current.held; pending.pressed |= current.pressed;
            bool edge = pending.pressed != 0 || pending.held != lastSent.held;
            bool changed = pending.move != lastSent.move || Mathf.Abs(Mathf.DeltaAngle(pending.yaw, lastSent.yaw)) > .1f;
            if (NetworkClient.isConnected && NetworkClient.ready && (edge || (changed && Time.unscaledTime >= nextSend) || Time.unscaledTime >= nextHeartbeat))
            {
                NetworkClient.Send(pending); lastSent = pending; pending.pressed = 0;
                nextSend = Time.unscaledTime + 1f / 60; nextHeartbeat = Time.unscaledTime + .1f;
            }
            if (!hasSnapshot) return;
            if(game.Level!=latest.level)game.ConfigureSession(latest.level);
            float snapshotAge = Mathf.Min(Time.unscaledTime - snapshotAt, .1f);
            liveActors.Clear();
            foreach (var received in latest.players)
            {
                var state = received; state.actionTime += snapshotAge;
                liveActors.Add(state.id);
                if (!actors.TryGetValue(state.id, out var actor) || !actor) { actor = state.id == LocalId ? game.Player : game.CreateCompanion(state.id); actors[state.id] = actor; }
                actor.ApplyNetwork(state, Time.deltaTime);
            }
            removedActors.Clear();
            foreach (var pair in actors) if (!liveActors.Contains(pair.Key)) { if (pair.Value && pair.Value != game.Player) Destroy(pair.Value.gameObject); removedActors.Add(pair.Key); }
            foreach (int id in removedActors) actors.Remove(id);
            for (int i = 0; i < Mathf.Min(latest.enemies.Length, game.Enemies.Count); i++)
            { var state = latest.enemies[i]; state.actionTime += snapshotAge; game.Enemies[i].ApplyReplica(state, Time.deltaTime); }
            var boss = latest.boss; boss.age += snapshotAge;
            game.Enemies[game.Enemies.Count - 1].Boss.ApplyNetwork(boss);
            game.ApplyNetworkWorld(latest);
        }
        public static string LocalAddresses()
        {
            try { var values = new List<string>(); foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList) if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)) values.Add(ip.ToString()); return string.Join("  /  ", values); }
            catch { return "请查看系统网络设置中的 IPv4 地址"; }
        }
    }
}
