using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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
        public event Action Changed;
        readonly Dictionary<int, string> names = new Dictionary<int, string>();
        readonly Dictionary<int, TrialPlayer> actors = new Dictionary<int, TrialPlayer>();
        readonly Dictionary<int, TrialInput> inputs = new Dictionary<int, TrialInput>();
        readonly Dictionary<int, TrialControls> controls = new Dictionary<int, TrialControls>();
        readonly Dictionary<int, float> lastInput = new Dictionary<int, float>();
        readonly Dictionary<int, float> reviveAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> lastPacket = new Dictionary<int, float>();
        TrialGame boundGame;
        float nextSend, connectDeadline;
        bool intentionalStop;
        TrialControls pending;
        TrialWorldState latest;
        bool hasSnapshot;
        bool previousBusy;

        public override void Awake()
        {
            if(singleton && singleton!=this){Destroy(gameObject);return;}
            base.Awake();
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
            intentionalStop = false; SetStatus("正在创建房间…");
            try { StartHost(); if (!transport.ServerActive()) { Leave(); SetStatus("创建失败：端口可能已被占用，请更换端口"); } }
            catch (Exception e) { Leave(); SetStatus("创建失败：" + e.Message); }
        }
        public void Join()
        {
            if (Busy) return;
            intentionalStop = false; connectDeadline = Time.unscaledTime + 12;
            SetStatus("正在连接 " + networkAddress + ":" + GamePort + "…");
            try { StartClient(); } catch (Exception e) { Leave(); SetStatus("连接失败：" + e.Message); }
        }
        public void BeginTogether()
        {
            if (NetworkServer.active && SceneManager.GetActiveScene().path == MenuScene) ServerChangeScene(GameScene);
        }
        public void Leave()
        {
            intentionalStop = true;
            if (NetworkServer.active && NetworkClient.active) StopHost();
            else if (NetworkClient.active) StopClient();
            else if (NetworkServer.active) StopServer();
            Members = Array.Empty<string>(); LocalId = -1; hasSnapshot = false;
            SetStatus("已离开房间");
        }
        public void SetStatus(string message) { Status = message; Changed?.Invoke(); Debug.Log("[InkNet] " + message); }
        public override void OnStartServer()
        {
            names.Clear(); controls.Clear(); lastPacket.Clear();
            NetworkServer.RegisterHandler<TrialHello>(ReceiveHello);
            NetworkServer.RegisterHandler<TrialControls>(ReceiveControls);
            SetStatus("房间已创建 · TCP " + GamePort);
        }
        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<TrialLobby>(ReceiveLobby);
            NetworkClient.RegisterHandler<TrialWorldState>(ReceiveWorld);
        }
        public override void OnClientConnect()
        {
            base.OnClientConnect(); connectDeadline = 0;
            NetworkClient.Send(new TrialHello { name = PlayerName });
            SetStatus(NetworkServer.active ? "房间已创建 · 等待同伴" : "已加入房间 · 等待房主启程");
        }
        void ReceiveHello(NetworkConnectionToClient connection, TrialHello message)
        {
            string value = (message.name ?? "旅人").Replace("<", "").Replace(">", "").Replace("\n", "").Replace("\r", "");
            names[connection.connectionId] = value.Substring(0, Math.Min(12, value.Length));
            BroadcastLobby();
        }
        void BroadcastLobby()
        {
            var ids = new int[names.Count]; var labels = new string[names.Count]; int i = 0;
            foreach (var item in names) { ids[i] = item.Key; labels[i++] = item.Value; }
            foreach (var connection in NetworkServer.connections.Values)
                connection.Send(new TrialLobby { self = connection.connectionId, ids = ids, names = labels });
        }
        void ReceiveLobby(TrialLobby message) { LocalId = message.self; Members = message.names; Changed?.Invoke(); }
        void ReceiveControls(NetworkConnectionToClient connection, TrialControls value)
        {
            int id = connection.connectionId;
            if (!names.ContainsKey(id) || !float.IsFinite(value.yaw) || !float.IsFinite(value.move.x) || !float.IsFinite(value.move.y)) return;
            if (lastPacket.TryGetValue(id, out var at) && Time.unscaledTime - at < .012f) return;
            lastPacket[id] = lastInput[id] = Time.unscaledTime;
            value.move = Vector2.ClampMagnitude(value.move, 1);
            if (controls.TryGetValue(id, out var previous)) value.pressed |= previous.pressed;
            controls[id] = value;
        }
        public override void OnServerDisconnect(NetworkConnectionToClient connection)
        {
            int id = connection.connectionId;
            names.Remove(id); controls.Remove(id); lastInput.Remove(id); lastPacket.Remove(id); reviveAt.Remove(id);
            if (actors.TryGetValue(id, out var actor)) { if (actor) Destroy(actor.gameObject); actors.Remove(id); }
            if (inputs.TryGetValue(id, out var input)) { if (input) Destroy(input); inputs.Remove(id); }
            base.OnServerDisconnect(connection); BroadcastLobby();
        }
        public override void OnClientDisconnect()
        {
            connectDeadline = 0; Members = Array.Empty<string>(); hasSnapshot = false;
            SetStatus(intentionalStop ? "已离开房间" : "连接已断开，请检查地址、端口与房主状态");
        }
        public override void OnClientError(TransportError error, string reason) { SetStatus("连接失败：请检查地址、TCP 端口和映射设置"); }
        public override void OnServerError(NetworkConnectionToClient connection, TransportError error, string reason) { SetStatus("网络错误：" + reason); }
        public override void OnStopServer() { names.Clear(); controls.Clear(); actors.Clear(); inputs.Clear(); reviveAt.Clear(); boundGame = null; }
        public override void OnStopClient() { actors.Clear(); inputs.Clear(); boundGame = null; Members=Array.Empty<string>();Changed?.Invoke(); }
        public override void Update()
        {
            base.Update();
            if (connectDeadline > 0 && !NetworkClient.isConnected && Time.unscaledTime > connectDeadline)
            { Leave(); SetStatus("连接超时，请检查地址、TCP 端口、防火墙或穿透映射"); }
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
            nextSend = 0; game.StartTrial();
        }
        public void TickGame(TrialGame game)
        {
            Bind(game);
            bool paused = game.Mode == TrialMode.Paused;
            if (game.Input.Pause && game.Mode != TrialMode.Victory) { if (paused) game.Resume(); else game.Pause(); paused = !paused; }
            if (NetworkServer.active) TickHost(game, paused);
            else TickPeer(game, paused);
        }
        void TickHost(TrialGame game, bool paused)
        {
            foreach (var member in names)
            {
                int id = member.Key;
                if (!actors.TryGetValue(id, out var actor) || !actor)
                {
                    actor = id == LocalId ? game.Player : game.CreateCompanion(id);
                    actors[id] = actor;
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
                if (!actor.Alive)
                {
                    if (!reviveAt.ContainsKey(id)) reviveAt[id] = Time.unscaledTime + 5;
                    if (Time.unscaledTime >= reviveAt[id]) { actor.Restore(game.Checkpoint + Vector3.right * (id % 4)); reviveAt.Remove(id); }
                    continue;
                }
                if (id == LocalId) input.SetNetworkYaw(game.CameraRig.transform.eulerAngles.y);
                if (!(id == LocalId && paused)) actor.Tick(Time.deltaTime, input);
                if (input.Interact && Vector3.Distance(actor.transform.position, game.World.ShrinePosition) < 2.6f)
                { actor.Restore(game.World.ShrinePosition + Vector3.right * 1.5f); }
            }
            if (game.Mode != TrialMode.Victory)
            {
                game.AdvanceNetworkTime(Time.deltaTime);
                foreach (var enemy in game.Enemies) { game.SetCombatTarget(NearestPlayer(enemy.transform.position, game.Player)); enemy.Tick(Time.deltaTime); }
                game.SetCombatTarget(game.Player);
            }
            if (!paused) game.CameraRig.Look(game.Input.Look);
            if (Time.unscaledTime < nextSend) return;
            nextSend = Time.unscaledTime + .05f;
            var players = new TrialActorState[actors.Count]; int index = 0;
            foreach (var item in actors) players[index++] = item.Value.CaptureNetwork(item.Key);
            var enemies = new TrialActorState[game.Enemies.Count];
            for (int i = 0; i < enemies.Length; i++) { enemies[i] = game.Enemies[i].CaptureNetwork(i); enemies[i].awake = game.Enemies[i].Awakened; }
            NetworkServer.SendToAll(new TrialWorldState { players = players, enemies = enemies, boss = game.Enemies[enemies.Length - 1].Boss.CaptureNetwork(), gate = game.GateOpen, guards = game.DefeatedGuards, victory = game.Mode == TrialMode.Victory, seconds = game.PlaySeconds });
        }
        TrialPlayer NearestPlayer(Vector3 point, TrialPlayer fallback)
        {
            TrialPlayer nearest = fallback; float best = float.MaxValue;
            foreach (var actor in actors.Values) if (actor && actor.Alive) { float d = (actor.transform.position - point).sqrMagnitude; if (d < best) { nearest = actor; best = d; } }
            return nearest;
        }
        public IEnumerable<TrialPlayer> Players => actors.Values;
        void ReceiveWorld(TrialWorldState state) { if (NetworkServer.active) return; latest = state; hasSnapshot = true; }
        void TickPeer(TrialGame game, bool paused)
        {
            if (!paused) game.CameraRig.Look(game.Input.Look);
            var current = paused ? default : game.Input.CaptureNetwork(game.CameraRig.transform.eulerAngles.y);
            pending.move = current.move; pending.yaw = current.yaw; pending.held = current.held; pending.pressed |= current.pressed;
            if (NetworkClient.isConnected && NetworkClient.ready && Time.unscaledTime >= nextSend)
            { NetworkClient.Send(pending); pending.pressed = 0; nextSend = Time.unscaledTime + .033f; }
            if (!hasSnapshot) return;
            var live = new HashSet<int>();
            foreach (var state in latest.players)
            {
                live.Add(state.id);
                if (!actors.TryGetValue(state.id, out var actor) || !actor) { actor = state.id == LocalId ? game.Player : game.CreateCompanion(state.id); actors[state.id] = actor; }
                actor.ApplyNetwork(state, Time.deltaTime);
            }
            var removed = new List<int>();
            foreach (var pair in actors) if (!live.Contains(pair.Key)) { if (pair.Value && pair.Value != game.Player) Destroy(pair.Value.gameObject); removed.Add(pair.Key); }
            foreach (int id in removed) actors.Remove(id);
            for (int i = 0; i < Mathf.Min(latest.enemies.Length, game.Enemies.Count); i++) game.Enemies[i].ApplyReplica(latest.enemies[i], Time.deltaTime);
            game.Enemies[game.Enemies.Count - 1].Boss.ApplyNetwork(latest.boss);
            game.ApplyNetworkWorld(latest);
        }
        public static string LocalAddresses()
        {
            try { var values = new List<string>(); foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList) if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)) values.Add(ip.ToString()); return string.Join("  /  ", values); }
            catch { return "请查看系统网络设置中的 IPv4 地址"; }
        }
    }
}
