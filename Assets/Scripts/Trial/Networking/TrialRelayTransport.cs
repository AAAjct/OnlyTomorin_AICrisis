using System;
using System.Collections.Generic;
using Mirror;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using NetworkConnection = Unity.Networking.Transport.NetworkConnection;
using SendStatus = Unity.Networking.Transport.Error.StatusCode;

namespace TomorinTrial
{
    public sealed class TrialRelayTransport : Transport
    {
        const int PacketSize = 16384;
        const int QueueLimit = 128;

        sealed class Peer
        {
            public readonly NetworkConnection Connection;
            public readonly Queue<byte[]> Pending = new Queue<byte[]>();
            public Peer(NetworkConnection connection) => Connection = connection;
        }

        readonly Dictionary<int, Peer> peers = new Dictionary<int, Peer>();
        readonly Dictionary<NetworkConnection, int> ids = new Dictionary<NetworkConnection, int>();
        readonly List<int> failed = new List<int>();
        readonly byte[] receive = new byte[PacketSize];
        RelayServerData allocation;
        NetworkDriver driver;
        NetworkPipeline pipeline;
        Peer client;
        int nextId;
        bool listening, connected;

        internal void Configure(RelayServerData data) => allocation = data;
        internal RelayConnectionStatus RelayStatus => driver.IsCreated
            ? driver.GetRelayConnectionStatus() : RelayConnectionStatus.NotEstablished;

        public override bool Available() => UnityEngine.Application.platform != UnityEngine.RuntimePlatform.WebGLPlayer;
        public override int GetMaxPacketSize(int channelId = Channels.Reliable) => PacketSize;
        public override int GetBatchThreshold(int channelId = Channels.Reliable) => 1100;
        public override bool ClientConnected() => connected;
        public override bool ServerActive() => listening;
        public override Uri ServerUri() => new Uri("relay://unity");
        public override string ServerGetClientAddress(int connectionId) => "Unity Relay";

        void Open()
        {
            var settings = new NetworkSettings(Allocator.Temp);
            try
            {
                settings.WithRelayParameters(ref allocation);
                settings.WithFragmentationStageParameters(PacketSize);
                settings.WithReliableStageParameters(windowSize: 64);
                driver = NetworkDriver.Create(settings);
                pipeline = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
                if (driver.Bind(NetworkEndpoint.AnyIpv4) != 0)
                    throw new InvalidOperationException("无法绑定 Relay UDP 套接字");
            }
            catch { Shutdown(); throw; }
            finally { settings.Dispose(); }
        }

        public override void ServerStart()
        {
            Open();
            if (driver.Listen() != 0)
            {
                Shutdown();
                throw new InvalidOperationException("无法启动 Relay 监听");
            }
            listening = true;
        }

        public override void ClientConnect(string address)
        {
            Open();
            var connection = driver.Connect(allocation.Endpoint);
            if (!connection.IsCreated)
            {
                Shutdown();
                throw new InvalidOperationException("无法建立 Relay 连接");
            }
            client = new Peer(connection);
        }

        public override void ServerEarlyUpdate()
        {
            if (!listening) return;
            driver.ScheduleUpdate().Complete();
            while (listening)
            {
                var accepted = driver.Accept();
                if (!accepted.IsCreated) break;
                int id = ++nextId;
                peers.Add(id, new Peer(accepted));
                ids.Add(accepted, id);
                OnServerConnectedWithAddress?.Invoke(id, ServerGetClientAddress(id));
            }
            while (listening)
            {
                var type = driver.PopEvent(out var connection, out var reader);
                if (type == NetworkEvent.Type.Empty) break;
                if (!ids.TryGetValue(connection, out int id)) continue;
                if (type == NetworkEvent.Type.Data)
                    OnServerDataReceived?.Invoke(id, Read(reader), Channels.Reliable);
                else if (type == NetworkEvent.Type.Disconnect)
                    Forget(id);
            }
        }

        public override void ClientEarlyUpdate()
        {
            if (client == null) return;
            driver.ScheduleUpdate().Complete();
            while (client != null)
            {
                var type = driver.PopEventForConnection(client.Connection, out var reader);
                if (type == NetworkEvent.Type.Empty) break;
                if (type == NetworkEvent.Type.Connect)
                {
                    connected = true;
                    OnClientConnected?.Invoke();
                }
                else if (type == NetworkEvent.Type.Data)
                    OnClientDataReceived?.Invoke(Read(reader), Channels.Reliable);
                else if (type == NetworkEvent.Type.Disconnect)
                    ClientDisconnect();
            }
        }

        ArraySegment<byte> Read(DataStreamReader reader)
        {
            reader.ReadBytes(receive.AsSpan(0, reader.Length));
            return new ArraySegment<byte>(receive, 0, reader.Length);
        }

        int Send(Peer peer, ArraySegment<byte> data)
        {
            int result = driver.BeginSend(pipeline, peer.Connection, out var writer, data.Count);
            if (result != 0) return result;
            if (writer.WriteBytes(data.AsSpan())) return driver.EndSend(writer);
            driver.AbortSend(writer);
            return (int)SendStatus.NetworkPacketOverflow;
        }

        bool SendOrQueue(Peer peer, ArraySegment<byte> data)
        {
            if (peer.Pending.Count == 0)
            {
                int result = Send(peer, data);
                if (result >= 0) return true;
                if (result != (int)SendStatus.NetworkSendQueueFull) return false;
            }
            if (peer.Pending.Count >= QueueLimit) return false;
            peer.Pending.Enqueue(data.AsSpan().ToArray());
            return true;
        }

        bool Flush(Peer peer)
        {
            while (peer.Pending.Count > 0)
            {
                int result = Send(peer, new ArraySegment<byte>(peer.Pending.Peek()));
                if (result < 0) return result == (int)SendStatus.NetworkSendQueueFull;
                peer.Pending.Dequeue();
            }
            return true;
        }

        public override void ClientSend(ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            if (connected && !SendOrQueue(client, segment)) ClientSendFailed();
        }

        void ClientSendFailed()
        {
            OnClientError?.Invoke(TransportError.Unexpected, "Relay 发送失败或队列已满");
            ClientDisconnect();
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            if (peers.TryGetValue(connectionId, out var peer) && !SendOrQueue(peer, segment))
                failed.Add(connectionId);
        }

        public override void ClientLateUpdate()
        {
            if (!connected) return;
            if (!Flush(client)) ClientSendFailed();
            else driver.ScheduleFlushSend().Complete();
        }

        public override void ServerLateUpdate()
        {
            if (!listening) return;
            foreach (var pair in peers)
                if (!Flush(pair.Value)) failed.Add(pair.Key);
            // Disconnect callbacks can broadcast, so process newly failed sends next frame.
            for (int index = failed.Count - 1; index >= 0 && listening; index--)
            {
                int id = failed[index];
                failed.RemoveAt(index);
                ServerDisconnect(id);
            }
            if (listening) driver.ScheduleFlushSend().Complete();
        }

        void Forget(int id)
        {
            if (!peers.Remove(id, out var peer)) return;
            ids.Remove(peer.Connection);
            OnServerDisconnected?.Invoke(id);
        }

        public override void ServerDisconnect(int connectionId)
        {
            if (!peers.TryGetValue(connectionId, out var peer)) return;
            driver.Disconnect(peer.Connection);
            Forget(connectionId);
        }

        public override void ClientDisconnect()
        {
            if (client == null) return;
            driver.Disconnect(client.Connection);
            driver.ScheduleUpdate().Complete();
            Shutdown();
            OnClientDisconnected?.Invoke();
        }

        public override void ServerStop()
        {
            if (!listening) return;
            foreach (var peer in peers.Values) driver.Disconnect(peer.Connection);
            driver.ScheduleUpdate().Complete();
            Shutdown();
        }

        public override void Shutdown()
        {
            client = null;
            listening = connected = false;
            peers.Clear();
            ids.Clear();
            failed.Clear();
            nextId = 0;
            if (driver.IsCreated) driver.Dispose();
            driver = default;
        }

        void OnDestroy() => Shutdown();
    }
}
