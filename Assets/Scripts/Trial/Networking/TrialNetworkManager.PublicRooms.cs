using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialNetworkManager
    {
        const string RoomVersionKey = "tomorin-room";
        const string RoomSlotsKey = "free-slots";
        const string RoomRelayKey = "relay-code";
        const string RoomTransportKey = "transport";
        const string RoomAddressKey = "address";
        const string RoomPortKey = "port";
        const string RoomLevelKey = "level";
        const string RoomDifficultyKey = "difficulty";
        static string RoomVersion => "room-2/" + Application.version;

        // Lobby advertises the room. Mirror tracks actual occupancy, including room-code invites.
        string publicListingId;
        Task publicListingRequest;
        float nextListingRequest, listingHeartbeat;
        (int capacity, int slots, TrialLevel level, TrialDifficulty difficulty, bool visible) publishedListing;
        double nextRoomQuery;
        string publicDirectAddress;
        ushort publicDirectPort;
        bool publicListingCreated;
        string hostRoomName, remoteRoomName;

        public string SavedRoomName => PlayerPrefs.GetString("Ink.RoomName", PlayerName + "的房间");
        public string RoomName => IsHost ? hostRoomName ?? SavedRoomName : SessionActive ? remoteRoomName ?? "房间" : SavedRoomName;

        void ConfigureRoomName(string requested)
        {
            string value = requested == null ? SavedRoomName : requested.Trim();
            value = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            if (string.IsNullOrWhiteSpace(value)) value = PlayerName + "的房间";
            var characters = StringInfo.ParseCombiningCharacters(value);
            hostRoomName = characters.Length > 24 ? value.Substring(0, characters[24]) : value;
            PlayerPrefs.SetString("Ink.RoomName", hostRoomName);
            PlayerPrefs.Save();
        }

        public void JoinRoom(string target, string nickname)
        {
            if (Busy) return;
            target = target?.Trim() ?? "";
            if (InkMainMenu.TrySplitEndpoint(target, out var address, out var port))
            {
                if (Configure(address, port, nickname)) Join();
                return;
            }
            bool code = target.Length > 0 && target.Length <= 16;
            foreach (char c in target)
                if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9')) code = false;
            if (code) JoinRelay(target, nickname);
            else SetStatus("请输入 IP:端口 或房间码");
        }

        public bool PublicRelayHost { get; private set; }
        public bool PublicDirectHost { get; private set; }
        bool PublicRoomHost => PublicRelayHost || PublicDirectHost;
        public bool PublicRoomReady => !PublicRoomHost || publicListingCreated;

        internal void HostDirect(string localPort, string nickname, bool publicRoom, string publicEndpoint, string roomName = null)
        {
            if (Busy) return;
            if (!ConfigureHostPort(localPort, nickname)) return;
            ConfigureRoomName(roomName);
            if (!publicRoom) { Host(); return; }
            if (!InkMainMenu.TrySplitEndpoint(publicEndpoint, out var address, out var port) ||
                Uri.CheckHostName(address) == UriHostNameType.Unknown || !TryPort(port, out var externalPort))
            {
                SetStatus("请填写公网域名或 IP 及端口");
                return;
            }
            PlayerPrefs.SetString("Ink.PublicEndpoint",publicEndpoint.Trim());
            PlayerPrefs.Save();
            _ = HostPublicDirect(address, externalPort);
        }

        async Task HostPublicDirect(string address, ushort port)
        {
            int attempt = ++relayAttempt;
            PublicDirectHost = true;
            publicDirectAddress = address;
            publicDirectPort = port;
            preparingRelay = true;
            SetStatus("正在创建公开房间…");
            try
            {
                await EnsureRelaySignIn();
                if (!this || attempt != relayAttempt) return;
                preparingRelay = false;
                Host();
                if (!IsHost) { CancelRelay(); return; }
                SetStatus("正在登记公开房间…");
            }
            catch (Exception error)
            {
                if (!this || attempt != relayAttempt) return;
                Leave();
                SetStatus("创建公开房间失败 · " + error.Message);
            }
        }

        internal readonly struct PublicRoom
        {
            public readonly string Id, Name;
            public readonly int Players, Capacity;
            public readonly TrialLevel Level;
            public readonly TrialDifficulty Difficulty;

            public PublicRoom(Lobby room, int available, TrialLevel level, TrialDifficulty difficulty)
            {
                Id = room.Id;
                Name = room.Name;
                Capacity = room.MaxPlayers;
                Players = Capacity - available;
                Level = level;
                Difficulty = difficulty;
            }
        }

        internal async Task<(List<PublicRoom> rooms, string continuation)> SearchPublicRooms(string keyword, string continuation)
        {
            await EnsureRelaySignIn();
            double now = Time.realtimeSinceStartupAsDouble;
            double requestAt = Math.Max(now, nextRoomQuery);
            nextRoomQuery = requestAt + 1.1;
            if (requestAt > now) await Task.Delay(TimeSpan.FromSeconds(requestAt - now));

            var query = new QueryLobbiesOptions
            {
                Count = 20,
                ContinuationToken = continuation,
                Filters = new List<QueryFilter>
                {
                    new(QueryFilter.FieldOptions.S1, RoomVersion, QueryFilter.OpOptions.EQ),
                    new(QueryFilter.FieldOptions.N1, "0", QueryFilter.OpOptions.GT)
                },
                Order = new List<QueryOrder> { new(false, QueryOrder.FieldOptions.Created) }
            };
            if (!string.IsNullOrWhiteSpace(keyword))
                query.Filters.Add(new QueryFilter(QueryFilter.FieldOptions.Name, keyword.Trim(), QueryFilter.OpOptions.CONTAINS));

            var response = await LobbyService.Instance.QueryLobbiesAsync(query);
            if (response?.Results == null) throw new InvalidOperationException("Unity Lobby 未返回搜索结果，请重试");
            var rooms = new List<PublicRoom>(response.Results.Count);
            foreach (var lobby in response.Results)
                if (TryReadPublicRoom(lobby, out var room)) rooms.Add(room);
            return (rooms, response.Results.Count == query.Count ? response.ContinuationToken : null);
        }

        internal void JoinPublicRoom(string id, string nickname) => _ = ConnectPublicRoom(id, nickname);

        async Task ConnectPublicRoom(string id, string nickname)
        {
            if (Busy) return;
            int attempt = ++relayAttempt;
            preparingRelay = true;
            SetStatus("正在加入房间…");
            try
            {
                await EnsureRelaySignIn();
                if (!this || attempt != relayAttempt) return;
                var lobby = await GetPublicRoom(id);
                if (!this || attempt != relayAttempt) return;
                preparingRelay = false;
                if (TryReadDirectEndpoint(lobby, out var address, out var port))
                {
                    if (Configure(address, port.ToString(CultureInfo.InvariantCulture), nickname)) Join();
                }
                else await ConnectRelay(false, lobby.Data[RoomRelayKey].Value, nickname);
            }
            catch (Exception error)
            {
                if (!this || attempt != relayAttempt) return;
                Leave();
                SetStatus("加入房间失败 · " + error.Message);
            }
        }

        static bool TryReadPublicRoom(Lobby lobby, out PublicRoom room)
        {
            room = default;
            if (lobby == null || lobby.IsPrivate || lobby.IsLocked || lobby.HasPassword || lobby.Data == null ||
                lobby.MaxPlayers < MinRoomCapacity || lobby.MaxPlayers > MaxRoomCapacity ||
                !lobby.Data.TryGetValue(RoomVersionKey, out var version) || version.Value != RoomVersion ||
                !lobby.Data.TryGetValue(RoomSlotsKey, out var slots) || !int.TryParse(slots.Value, out int available) ||
                available <= 0 || available >= lobby.MaxPlayers ||
                !lobby.Data.TryGetValue(RoomLevelKey, out var level) || !byte.TryParse(level.Value, out byte levelId) ||
                levelId > (byte)TrialLevel.BossRush ||
                !lobby.Data.TryGetValue(RoomDifficultyKey, out var difficulty) || !byte.TryParse(difficulty.Value, out byte difficultyId) ||
                difficultyId > (byte)TrialDifficulty.Hard) return false;

            if (lobby.Data.TryGetValue(RoomTransportKey, out var transport) && transport.Value != "relay")
            {
                if (transport.Value != "tcp" || !TryReadDirectEndpoint(lobby, out _, out _)) return false;
            }
            else if (!lobby.Data.TryGetValue(RoomRelayKey, out var relay) || string.IsNullOrEmpty(relay.Value)) return false;

            room = new PublicRoom(lobby, available, (TrialLevel)levelId, (TrialDifficulty)difficultyId);
            return true;
        }

        static bool TryReadDirectEndpoint(Lobby lobby, out string address, out ushort port)
        {
            address = null;
            port = 0;
            if (lobby.Data == null || !lobby.Data.TryGetValue(RoomTransportKey, out var transport) || transport.Value != "tcp" ||
                !lobby.Data.TryGetValue(RoomAddressKey, out var host) || Uri.CheckHostName(host.Value) == UriHostNameType.Unknown ||
                !lobby.Data.TryGetValue(RoomPortKey, out var number) || !TryPort(number.Value, out port)) return false;
            address = host.Value;
            return true;
        }

        static async Task<string> GetPublicRoomCode(string id)
        {
            var lobby = await GetPublicRoom(id);
            if (TryReadDirectEndpoint(lobby, out _, out _)) throw new InvalidOperationException("此房间使用 IP 连接");
            return lobby.Data[RoomRelayKey].Value;
        }

        static async Task<Lobby> GetPublicRoom(string id)
        {
            try
            {
                var lobby = await LobbyService.Instance.GetLobbyAsync(id);
                if (TryReadPublicRoom(lobby, out _)) return lobby;
            }
            catch (LobbyServiceException error) when (error.Reason == LobbyExceptionReason.LobbyNotFound) { }
            throw new InvalidOperationException("房间已满、已开战或已关闭，请重新搜索");
        }

        void UpdatePublicListing()
        {
            string code = string.IsNullOrEmpty(RelayRoomCode) ? pendingRoomCode : RelayRoomCode;
            float now = Time.unscaledTime;
            if (!PublicRoomHost || !IsHost || (PublicRelayHost && (relayBindDeadline > 0 || string.IsNullOrEmpty(code))) ||
                now < nextListingRequest || publicListingRequest is { IsCompleted: false }) return;

            var settings = (RoomCapacity, Mathf.Max(0, RoomCapacity - Mathf.Max(1, names.Count)),
                SelectedLevel, SelectedDifficulty, CanAdjustRoomCapacity);
            if (!string.IsNullOrEmpty(publicListingId) && settings == publishedListing && now < listingHeartbeat) return;

            nextListingRequest = now + 1.25f;
            publicListingRequest = SyncPublicListing(relayAttempt, code, settings);
        }

        async Task SyncPublicListing(int attempt, string code,
            (int capacity, int slots, TrialLevel level, TrialDifficulty difficulty, bool visible) settings)
        {
            try
            {
                if (string.IsNullOrEmpty(publicListingId))
                {
                    var lobby = await LobbyService.Instance.CreateLobbyAsync(RoomName, settings.capacity,
                        new CreateLobbyOptions { IsPrivate = !settings.visible, Data = ListingData(code, settings) });
                    if (string.IsNullOrEmpty(lobby?.Id)) throw new InvalidOperationException("Unity Lobby 未返回房间 ID");
                    if (!this || attempt != relayAttempt)
                    {
                        await DeletePublicListing(lobby.Id);
                        return;
                    }
                    publicListingId = lobby.Id;
                    publicListingCreated = true;
                    if (PublicRelayHost) FinishRelayHost(code);
                    else SetStatus(HostStatus);
                }
                else if (settings != publishedListing)
                {
                    await LobbyService.Instance.UpdateLobbyAsync(publicListingId, new UpdateLobbyOptions
                    {
                        MaxPlayers = settings.capacity,
                        IsPrivate = !settings.visible,
                        Data = ListingData(code, settings)
                    });
                }
                else await LobbyService.Instance.SendHeartbeatPingAsync(publicListingId);

                if (!this || attempt != relayAttempt) return;
                publishedListing = settings;
                listingHeartbeat = Time.unscaledTime + 15f;
            }
            catch (Exception error)
            {
                if (!this || attempt != relayAttempt) return;
                if (!publicListingCreated)
                {
                    Leave();
                    SetStatus("创建公开房间失败 · Unity Lobby · " + error.Message);
                    return;
                }
                if (error is LobbyServiceException lobbyError && lobbyError.Reason == LobbyExceptionReason.LobbyNotFound)
                    publicListingId = null;
                nextListingRequest = Time.unscaledTime + 5f;
                Debug.LogWarning("[InkNet] 公开房间列表更新失败，稍后重试 · " + error.Message);
            }
        }

        Dictionary<string, DataObject> ListingData(string code,
            (int capacity, int slots, TrialLevel level, TrialDifficulty difficulty, bool visible) settings)
        {
            var data = new Dictionary<string, DataObject>
            {
                [RoomVersionKey] = new(DataObject.VisibilityOptions.Public, RoomVersion, DataObject.IndexOptions.S1),
                [RoomSlotsKey] = new(DataObject.VisibilityOptions.Public, settings.slots.ToString(CultureInfo.InvariantCulture), DataObject.IndexOptions.N1),
                [RoomTransportKey] = new(DataObject.VisibilityOptions.Public, PublicDirectHost ? "tcp" : "relay"),
                [RoomLevelKey] = new(DataObject.VisibilityOptions.Public, ((byte)settings.level).ToString(CultureInfo.InvariantCulture)),
                [RoomDifficultyKey] = new(DataObject.VisibilityOptions.Public, ((byte)settings.difficulty).ToString(CultureInfo.InvariantCulture))
            };
            if (PublicDirectHost)
            {
                data[RoomAddressKey] = new DataObject(DataObject.VisibilityOptions.Public, publicDirectAddress);
                data[RoomPortKey] = new DataObject(DataObject.VisibilityOptions.Public, publicDirectPort.ToString(CultureInfo.InvariantCulture));
            }
            else data[RoomRelayKey] = new DataObject(DataObject.VisibilityOptions.Public, code);
            return data;
        }

        void RemovePublicListing()
        {
            string id = publicListingId;
            publicListingId = null;
            publicListingCreated = false;
            nextListingRequest = listingHeartbeat = 0;
            if (!string.IsNullOrEmpty(id)) _ = DeletePublicListing(id);
        }

        static async Task DeletePublicListing(string id)
        {
            try { await LobbyService.Instance.DeleteLobbyAsync(id); }
            catch (LobbyServiceException error) when (error.Reason == LobbyExceptionReason.LobbyNotFound) { }
            catch (Exception error) { Debug.LogWarning("[InkNet] 公开房间列表注销失败 · " + error.Message); }
        }
    }
}
