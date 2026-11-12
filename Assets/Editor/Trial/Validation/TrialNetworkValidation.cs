using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mirror;
using UnityEditor;
using UnityEngine;

namespace TomorinTrial.Editor
{
    // Independent TCP endpoints exercise the real Mirror framing and game handlers.
    // No second NetworkClient singleton is used alongside the host.
    public static partial class TrialNetworkValidation
    {
        public static string Status { get; private set; } = "Not run";
        static readonly List<string> results = new List<string>();
        static readonly List<TrialCombatEvent> events = new List<TrialCombatEvent>();
        static Telepathy.Client client;
        static Telepathy.Server server;
        static Unbatcher incoming;
        static IEnumerator routine;
        static double resumeAt, deadline, heartbeat;
        static int remoteId, serverPeer;
        static int receivedCapacity;
        static TrialControls held;
        static TrialWorldState world;
        static bool lobby, sceneReceived, peerReady;
        static int runtimeErrors;
        static string reportName="CombatNetworkValidation.txt";
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Tools/Tomorin/Verify combat networking (Play Mode)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || routine != null || !TrialNetworkManager.Current || TrialNetworkManager.SessionActive)
                throw new InvalidOperationException("Start InkStart in Play Mode, outside a network session.");
            results.Clear(); events.Clear(); Status = "Running";reportName="CombatNetworkValidation.txt";
            runtimeErrors = 0; Application.logMessageReceived += CaptureError;
            routine = Checks(); resumeAt = heartbeat = 0;
            EditorApplication.update += Step;
        }

        [MenuItem("Tools/Tomorin/Verify room capacity (Play Mode)")]
        public static void RunRoomCapacity()
        {
            if(!EditorApplication.isPlaying || routine!=null || !TrialNetworkManager.Current || TrialNetworkManager.SessionActive)
                throw new InvalidOperationException("Start InkStart in Play Mode, outside a network session.");
            results.Clear();events.Clear();Status="Running room capacity checks";reportName="RoomCapacityValidation.txt";
            runtimeErrors=0;Application.logMessageReceived+=CaptureError;
            routine=RoomCapacityChecks();resumeAt=heartbeat=0;EditorApplication.update+=Step;
        }

        static void Check(bool value, string label)
        { if (!value) throw new Exception(label); results.Add(label); }
        static void CaptureError(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeErrors++; }
        static bool Waiting(bool complete)
        {
            if (!complete && EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Network condition timed out");
            return !complete;
        }
        static void Until() { deadline = EditorApplication.timeSinceStartup + 12; }
        static string Notice(TrialGame game) => (string)typeof(TrialHud).GetField("notice", Private).GetValue(game.Hud);
        static void ResetShake(TrialGame game) => typeof(TrialCamera).GetField("shake", Private).SetValue(game.CameraRig, 0f);
        static float Shake(TrialGame game) => (float)typeof(TrialCamera).GetField("shake", Private).GetValue(game.CameraRig);
        static int Sparks(TrialGame game) => game.Effects.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount);

        static void Send<T>(T message) where T : struct, NetworkMessage => SendMany(message);
        static void SendMany<T>(params T[] messages) where T : struct, NetworkMessage
        {
            var batch = new Batcher(16384);
            foreach (var message in messages)
                using (var writer = NetworkWriterPool.Get()) { NetworkMessages.Pack(message, writer); batch.AddMessage(writer.ToArraySegment(), Time.realtimeSinceStartupAsDouble); }
            using (var writer = NetworkWriterPool.Get())
                while (batch.GetBatch(writer))
                {
                    if (client != null) client.Send(writer.ToArraySegment()); else server.Send(serverPeer, writer.ToArraySegment());
                    writer.Reset();
                }
        }

        static void Receive(ArraySegment<byte> data)
        {
            if (!incoming.AddBatch(data)) throw new Exception("Invalid network batch");
            while (incoming.GetNextMessage(out var segment, out _))
            {
                using (var reader = NetworkReaderPool.Get(segment))
                {
                    if (!NetworkMessages.UnpackId(reader, out var id)) throw new Exception("Missing message ID");
                    if (id == NetworkMessageId<TrialLobby>.Id) { var value = reader.Read<TrialLobby>(); remoteId = value.self; receivedLevel=value.level;receivedCapacity=value.capacity;lobby = true; }
                    else if (id == NetworkMessageId<TrialCombatEvent>.Id) events.Add(reader.Read<TrialCombatEvent>());
                    else if (id == NetworkMessageId<TrialWorldState>.Id) world = reader.Read<TrialWorldState>();
                    else if (id == NetworkMessageId<SceneMessage>.Id) { reader.Read<SceneMessage>(); sceneReceived = true; }
                    else if (server != null && id == NetworkMessageId<TrialHello>.Id)
                    {
                        reader.Read<TrialHello>(); Send(new TrialLobby { self = 1, ids = new[] { 0, 1 }, names = new[] { "Authority", "Peer" }, capacity = 8 });
                        Send(new SceneMessage { sceneName = TrialNetworkManager.GameScene });
                    }
                    else if (server != null && id == NetworkMessageId<ReadyMessage>.Id) peerReady = true;
                    else if (server != null && id == NetworkMessageId<NetworkPingMessage>.Id)
                    { var ping = reader.Read<NetworkPingMessage>(); Send(new NetworkPongMessage { localTime = ping.localTime }); }
                }
            }
        }

        static void Step()
        {
            try
            {
                client?.Tick(1000); server?.Tick(1000);
                TickEncounterPeers();
                if (client != null && client.Connected && EditorApplication.timeSinceStartup >= heartbeat)
                { Send(held); heartbeat = EditorApplication.timeSinceStartup + .1; }
                if (EditorApplication.timeSinceStartup < resumeAt) return;
                if (!routine.MoveNext()) { Finish(null); return; }
                resumeAt = EditorApplication.timeSinceStartup + (routine.Current is float seconds ? seconds : 0);
            }
            catch (Exception error) { Finish(error); }
        }

        static IEnumerator RoomCapacityChecks()
        {
            var network=TrialNetworkManager.Current;
            var transport=(TelepathyTransport)network.transport;
            ushort originalPort=transport.port;
            int originalCapacity=network.RoomCapacity;
            var originalLevel=network.SelectedLevel;
            string originalAddress=network.networkAddress;
            try
            {
                if(!TrialNetworkManager.TryFindFreeTcpPort(out var port))throw new InvalidOperationException("No validation TCP port available");
                transport.port=(ushort)port;network.SelectLevel(TrialLevel.BigFish);network.Host();ConnectEncounterPeer(network);
                Until();while(Waiting(lobby && network.Members.Length==2))yield return .02f;
                Check(receivedCapacity==originalCapacity,"The initial host cap reaches the independent TCP peer");
                network.AdjustRoomCapacity(int.MinValue);
                Until();while(Waiting(receivedCapacity==2))yield return .02f;
                Check(network.RoomCapacity==2 && NetworkServer.maxConnections==2 && network.Members.Length==2,
                    "Host can lower the live room cap to its current occupants and broadcast it");
                var menu=UnityEngine.Object.FindAnyObjectByType<InkMainMenu>();
                var minus=(UnityEngine.UI.Button)typeof(InkMainMenu).GetField("decreaseCapacity",Private).GetValue(menu);
                var plus=(UnityEngine.UI.Button)typeof(InkMainMenu).GetField("increaseCapacity",Private).GetValue(menu);
                Check(!minus.interactable && plus.interactable,"Host lobby enables capacity editing and disables lowering below occupancy");
                network.AdjustRoomCapacity(1);
                Until();while(Waiting(receivedCapacity==3))yield return .02f;
                ConnectExtraPeer(network,0);
                Until();while(Waiting(network.Members.Length==3))yield return .02f;
                Check(NetworkServer.maxConnections==3,"Raising the live cap admits another independent TCP player");
                network.AdjustRoomCapacity(-1);
                Check(network.RoomCapacity==3 && network.Members.Length==3,"Lowering capacity preserves all connected players");
                foreach(var extra in encounterPeers)extra.Disconnect();encounterPeers.Clear();
                Until();while(Waiting(network.Members.Length==2))yield return .02f;
                network.AdjustRoomCapacity(int.MaxValue);
                Until();while(Waiting(receivedCapacity==TrialNetworkManager.MaxRoomCapacity))yield return .02f;
                Check(NetworkServer.maxConnections==TrialNetworkManager.MaxRoomCapacity,"Live capacity clamps to the supported maximum");
                network.AdjustRoomCapacity(originalCapacity-network.RoomCapacity);
                network.BeginTogether();
                network.AdjustRoomCapacity(1);
                Check(network.RoomCapacity==originalCapacity,"Capacity changes are blocked once the host starts loading the match");
                Until();while(Waiting(sceneReceived && UnityEngine.Object.FindAnyObjectByType<TrialGame>()?.Ready==true))yield return .02f;
                network.AdjustRoomCapacity(1);
                Check(network.RoomCapacity==originalCapacity,"Capacity changes are restricted to the room lobby");
                network.ServerChangeScene(TrialNetworkManager.MenuScene);
                Until();while(Waiting(UnityEngine.Object.FindAnyObjectByType<InkMainMenu>()!=null && network.CanAdjustRoomCapacity))yield return .02f;
                yield return .1f;
                menu=UnityEngine.Object.FindAnyObjectByType<InkMainMenu>();
                plus=(UnityEngine.UI.Button)typeof(InkMainMenu).GetField("increaseCapacity",Private).GetValue(menu);
                Check(plus.interactable==(originalCapacity<TrialNetworkManager.MaxRoomCapacity),"Capacity controls refresh when the host returns to the lobby");

                client.Disconnect();client=null;network.Leave();
                incoming=new Unbatcher();server=new Telepathy.Server(16384){NoDelay=true};
                server.OnConnected=(id,address)=>serverPeer=id;
                server.OnData=(id,data)=>Receive(data);
                if(!TrialNetworkManager.TryFindFreeTcpPort(out var peerPort) || !server.Start(peerPort))
                    throw new InvalidOperationException("Could not start the validation authority");
                transport.port=(ushort)peerPort;network.networkAddress="127.0.0.1";network.Join();
                Until();while(Waiting(network.LocalId==1 && network.ActiveRoomCapacity==8))yield return .02f;
                network.AdjustRoomCapacity(1);
                Check(!network.CanAdjustRoomCapacity && network.RoomCapacity==originalCapacity && network.ActiveRoomCapacity==8,
                    "The real client displays the authority's cap and cannot change it or overwrite its own hosting preference");
                Send(new TrialLobby{self=1,ids=new[]{0,1},names=new[]{"Authority","Peer"},capacity=11});
                Until();while(Waiting(network.ActiveRoomCapacity==11))yield return .02f;
                Check(runtimeErrors==0,"The real client receives live capacity changes without runtime errors");
            }
            finally
            {
                foreach(var extra in encounterPeers)extra.Disconnect();encounterPeers.Clear();
                if(TrialNetworkManager.SessionActive)network.Leave();
                transport.port=originalPort;
                network.networkAddress=originalAddress;
                network.AdjustRoomCapacity(originalCapacity-network.RoomCapacity);
                network.SelectLevel(originalLevel);
            }
        }

        static IEnumerator Checks()
        {
            var network = TrialNetworkManager.Current;
            var transport = (TelepathyTransport)network.transport;
            ushort originalPort = transport.port;
            string originalName = network.PlayerName;
            try
            {
                transport.port = 18100; network.PlayerName = "HostValidation"; network.Host();
                Check(NetworkServer.active, "Host starts on the validation TCP port");
                incoming = new Unbatcher(); lobby = sceneReceived = false; held = default;
                client = new Telepathy.Client(16384) { NoDelay = true };
                client.OnConnected = () => { Send(new ReadyMessage()); Send(new TrialHello { name = "RemoteValidation" }); };
                client.OnData = Receive; client.Connect("127.0.0.1", transport.port);
                Until(); while (Waiting(lobby && network.Members.Length == 2)) yield return .02f;
                Check(remoteId != network.LocalId, "Independent TCP peer receives its own player ID");
                network.BeginTogether();
                Until(); while (Waiting(sceneReceived && UnityEngine.Object.FindAnyObjectByType<TrialGame>()?.Ready == true)) yield return .02f;
                Send(new ReadyMessage());
                var game = UnityEngine.Object.FindAnyObjectByType<TrialGame>();
                Until(); while (Waiting(network.Players.Count() == 2 && world.players != null)) yield return .02f;
                var remote = network.Players.First(p => p != game.Player);
                game.Player.ChooseBoon(game.Player.OfferA);Send(new TrialChooseBoon{boon=remote.OfferA});
                Until();while(Waiting(!remote.ChoosingBoon))yield return .02f;
                foreach (var enemy in game.Enemies) enemy.Teleport(new Vector3(12, 0, 36));
                held = new TrialControls { held = 1 }; Send(held);
                Until(); while (Waiting(remote.Guarding)) yield return .01f;
                game.Hud.Notify("HOST_SENTINEL", 10); ResetShake(game);
                float health = remote.Health; int parries = remote.SuccessfulParries;
                remote.ReceiveHit(null, 30, 20);
                Check(remote.SuccessfulParries == parries + 1 && remote.Health == health, "Remote timed guard authoritatively parries without damage");
                Check(Notice(game) == "HOST_SENTINEL" && Shake(game) == 0, "Remote parry does not notify or shake the host");
                Until(); while (Waiting(events.Count(e => e.cue == TrialCombatCue.Parry && e.player == remoteId) == 1)) yield return .01f;
                yield return .12f;
                Check(events.Count(e => e.cue == TrialCombatCue.Parry) == 1, "Remote peer receives exactly one reliable parry event with correct ownership");
                yield return .2f;
                remote.ReceiveHit(null, 30, 20);
                Until(); while (Waiting(events.Any(e => e.cue == TrialCombatCue.Block && e.player == remoteId))) yield return .01f;
                Check(remote.Health < health && Notice(game) == "HOST_SENTINEL", "Held guard broadcasts block feedback without replacing host UI");
                remote.ReceiveHit(null, 99999, 100, true);
                Until(); while (Waiting(events.Any(e => e.cue == TrialCombatCue.Death && e.player == remoteId))) yield return .01f;
                Check(game.Deaths == 0 && Notice(game) == "HOST_SENTINEL", "Remote death leaves host death count and notice unchanged");
                remote.Restore(game.Checkpoint + Vector3.right * 2); held = default;
                // All three messages arrive in one batch; the middle attack must survive.
                SendMany(default(TrialControls), new TrialControls { pressed = 1 }, default(TrialControls));
                Until(); while (Waiting(remote.Action == FighterAction.Light)) yield return .01f;
                Check(remote.Stamina < remote.MaxStamina, "Attack edge survives a same-frame TCP input burst");
                yield return .7f;
                var localInput = game.gameObject.AddComponent<TrialInput>();
                localInput.ApplyNetwork(new TrialControls { held = 1 }); game.Player.Tick(0, localInput);
                game.Player.ReceiveHit(null, 30, 20); UnityEngine.Object.Destroy(localInput);
                Check(Notice(game) == "完美格挡 · 下一击强化" && Shake(game) > 0, "Host retains its own parry HUD and camera feedback");
                Until(); while (Waiting(events.Any(e => e.cue == TrialCombatCue.Parry && e.player == network.LocalId))) yield return .01f;
                Check(events.Count(e => e.cue == TrialCombatCue.Parry && e.player == network.LocalId) == 1, "Host parry broadcasts exactly one correctly owned event");
                var bossState = game.Enemies.Last().Boss.CaptureNetwork();
                Check(bossState.hazards.Length == 0, "Inactive boss hazards consume no snapshot entries");
                using (var writer = NetworkWriterPool.Get())
                {
                    var snapshot = new TrialWorldState { players = new TrialActorState[2], enemies = new TrialActorState[4], boss = bossState };
                    writer.Write(snapshot); int compact = writer.Position; writer.Reset();
                    snapshot.boss.hazards = new TrialHazardState[32]; writer.Write(snapshot); int baseline = writer.Position;
                    Check(compact * 30 < baseline * 20, $"Idle snapshot payload: {baseline} -> {compact} bytes; target payload/sec {baseline * 20} -> {compact * 30}");
                }
                remote.ReceiveHit(null,99999,100,true);yield return .1f;
                Check(game.Mode==TrialMode.Playing && game.Player.Alive,"One downed companion does not fail a living party");
                yield return 5.2f;
                Check(remote.Alive && game.Mode==TrialMode.Playing,"A downed companion still respawns while a teammate survives");
                var movingBoss=game.Enemies.Last();
                typeof(TrialFighter).GetProperty(nameof(TrialFighter.MoveBlend)).SetValue(movingBoss,1f);
                game.Player.ReceiveHit(null,99999,100,true);remote.ReceiveHit(null,99999,100,true);
                yield return .2f;
                Check(game.Mode==TrialMode.Dead && world.defeat && Time.timeScale==.5f,"Team wipe starts slow motion on the host and replicates the defeat snapshot");
                Check(movingBoss.MoveBlend==0 && world.enemies.Last().movement==0,"Team wipe stops the boss gait on the host and in the peer snapshot");
                float failedAt=game.PlaySeconds;yield return 5.2f;
                Check(!game.Player.Alive && !remote.Alive && game.Mode==TrialMode.Dead && game.PlaySeconds==failedAt && Time.timeScale==0,"Team defeat pauses after slow motion and cancels automatic respawns");
                Send(new TrialRetryChallenge());yield return .15f;
                Check(game.Mode==TrialMode.Dead && world.defeat,"A peer cannot bypass the host-only retry");
                var retry=game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(button=>button.name=="Begin");
                Check(retry.interactable,"The host can retry from the defeat menu");
                retry.onClick.Invoke();yield return .25f;
                Check(game.Mode==TrialMode.Playing && game.Player.Alive && remote.Alive && !world.defeat,"Host retry from the defeat menu restores the entire party immediately");
                game.Player.ChooseBoon(game.Player.OfferA);remote.ChooseBoon(remote.OfferA);
                // Capture a real host-produced world for the receiving-client test.
                world.players = new[] { game.Player.CaptureNetwork(0), remote.CaptureNetwork(1) };
                held = default; client.Disconnect(); client = null; network.Leave();
                Until(); while (Waiting(UnityEngine.Object.FindAnyObjectByType<InkMainMenu>() != null)) yield return .03f;
                yield return .2f;

                incoming = new Unbatcher(); peerReady = false;
                server = new Telepathy.Server(16384) { NoDelay = true };
                server.OnConnected = (id, address) => serverPeer = id;
                server.OnData = (id, data) => Receive(data);
                Check(server.Start(18101), "Independent authority starts for the real receiving-client test");
                network = TrialNetworkManager.Current; ((TelepathyTransport)network.transport).port = 18101; network.networkAddress = "127.0.0.1"; network.Join();
                Until(); while (Waiting(UnityEngine.Object.FindAnyObjectByType<TrialGame>()?.Ready == true && peerReady)) yield return .03f;
                game = UnityEngine.Object.FindAnyObjectByType<TrialGame>();
                Send(world); yield return .15f;
                Check(network.LocalId == 1 && !NetworkServer.active, "Game runs as remote client with the assigned ID");
                world.defeat=true;Send(world);yield return .15f;
                Check(game.Mode==TrialMode.Dead && Time.timeScale==.5f && !game.CameraRig.Frozen && Cursor.visible,"Receiving client starts authoritative team defeat in slow motion and releases the cursor");
                world.defeat=false;Send(world);yield return .15f;
                Check(game.Mode==TrialMode.Playing && !game.CameraRig.Frozen && Time.timeScale>0,"Receiving client resumes after authoritative retry");
                Check(game.Player.MaxStamina==world.players.First(p=>p.id==1).maxStamina && game.Player.MaxStamina>=180,"The real receiving client retains the authority's increased maximum stamina");
                world.seconds+=17;var fragments=TrialPayloadParts.Split(world,0,123,128);
                foreach(var fragment in fragments)Send(fragment);
                yield return .15f;
                Check(fragments.Length>1 && Mathf.Abs(game.PlaySeconds-world.seconds)<.01f && game.Player.Health==world.players.First(p=>p.id==1).health,"Real client reassembles fragmented authoritative snapshots and applies their changed world time");
                world.boss.hazards = new[] { new TrialHazardState { active = true, position = game.Player.transform.position, radius = .3f } };
                Send(world); yield return .08f;
                Check(game.Enemies.Last().Boss.ActiveHazards == 1, "Client displays active hazards from the compact snapshot");
                world.boss.hazards = Array.Empty<TrialHazardState>(); Send(world); yield return .08f;
                Check(game.Enemies.Last().Boss.ActiveHazards == 0, "Shrinking hazard snapshots clear expired visuals");
                game.Hud.Notify("PEER_SENTINEL", 10); ResetShake(game); int before = Sparks(game);
                var cue = new TrialCombatEvent { player = 0, cue = TrialCombatCue.Parry, position = game.Player.transform.position, forward = Vector3.forward };
                Send(cue); yield return .08f;
                Check(Notice(game) == "PEER_SENTINEL" && Shake(game) == 0 && Sparks(game) > before, "Observer sees remote parry sparks without receiving its HUD or camera feedback");
                cue.player = 1; Send(cue);
                Until(); while (Waiting(Notice(game) == "完美格挡 · 下一击强化")) yield return .01f;
                Check(Shake(game) > 0, "Real remote client receives its own parry notice and camera feedback");
                game.Hud.Notify("ONCE_SENTINEL", 10); yield return .15f;
                Check(Notice(game) == "ONCE_SENTINEL", "Reapplying snapshots does not replay parry notifications");
                cue.cue = TrialCombatCue.Death; Send(cue);
                Until(); while (Waiting(game.Deaths == 1)) yield return .01f;
                Check(Notice(game) == "5 秒后重生", "Remote client receives its own death feedback");
                network.Leave(); server.Stop(); server = null;
                yield return .2f;
                Check(runtimeErrors == 0, "Host, remote client and disconnect transitions produce no runtime errors");
            }
            finally
            {
                if (TrialNetworkManager.Current)
                {
                    if (TrialNetworkManager.SessionActive) TrialNetworkManager.Current.Leave();
                    ((TelepathyTransport)TrialNetworkManager.Current.transport).port = originalPort;
                    TrialNetworkManager.Current.PlayerName = originalName;
                }
            }
        }

        static void Finish(Exception error)
        {
            EditorApplication.update -= Step;
            Application.logMessageReceived -= CaptureError;
            var current = routine; routine = null; (current as IDisposable)?.Dispose();
            client?.Disconnect(); client = null; server?.Stop(); server = null;
            Status = error == null ? "Passed " + results.Count + " checks" : "FAILED after " + results.Count + ": " + error.Message;
            Directory.CreateDirectory("Logs/Trial");
            File.WriteAllText("Logs/Trial/"+reportName, Status + "\n" + string.Join("\n", results) + (error == null ? "" : "\n" + error));
            if (error == null) Debug.Log("[Combat network] " + Status); else Debug.LogError("[Combat network] " + Status);
        }
    }
}
