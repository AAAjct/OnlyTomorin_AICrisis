using System;
using System.Threading.Tasks;
using Mirror;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialNetworkManager
    {
        TelepathyTransport directTransport;
        TrialRelayTransport relayTransport;
        Task relaySignIn;
        int relayAttempt;
        bool preparingRelay;
        float relayBindDeadline;
        string pendingRoomCode;

        public string RelayRoomCode { get; private set; }
        public bool RelaySession => (preparingRelay && !PublicDirectHost) || (SessionActive && UsingRelay);
        bool UsingRelay => relayTransport && transport == relayTransport;
        string HostStatus => UsingRelay
            ? string.IsNullOrEmpty(RelayRoomCode)
                ? (PublicRelayHost && relayBindDeadline == 0 ? "正在登记公开房间…" : "正在连接 Unity Relay…")
                : (PublicRelayHost ? "公开房间" : "私人房间") + " · 房间码 " + RelayRoomCode
            : PublicDirectHost ? (publicListingCreated ? "公开房间 · IP" : "正在登记公开房间…") : "房间已创建 · TCP " + GamePort;

        public void HostRelay(string nickname, bool publicRoom = false, string roomName = null)
        {
            if (Busy) return;
            SaveNickname(nickname);
            ConfigureRoomName(roomName);
            _ = ConnectRelay(true, null, PlayerName, publicRoom);
        }
        public void JoinRelay(string code, string nickname) => _ = ConnectRelay(false, code, nickname);

        Task EnsureRelaySignIn()
        {
            if (relaySignIn == null || relaySignIn.IsCompleted) relaySignIn = SignInToRelay();
            return relaySignIn;
        }

        async Task SignInToRelay()
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        async Task ConnectRelay(bool host, string code, string nickname, bool publicRoom = false, string publicRoomId = null)
        {
            if (Busy) return;
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                SetStatus("连接失败：当前 Relay 传输需要支持 UDP 的客户端");
                return;
            }

            code = code?.Trim().ToUpperInvariant();
            if (!host && string.IsNullOrEmpty(code) && string.IsNullOrEmpty(publicRoomId))
            {
                SetStatus("请输入房主分享的房间码");
                return;
            }

            int attempt = ++relayAttempt;
            preparingRelay = true;
            PublicRelayHost = host && publicRoom;
            SetStatus(host ? "正在申请 Relay 房间…" : "正在加入 Relay 房间…");
            try
            {
                await EnsureRelaySignIn();
                if (!this || attempt != relayAttempt) return;

                RelayServerData data;
                string roomCode = null;
                if (host)
                {
                    // Reserve the full range up front so lobby edits keep the same allocation and join code.
                    var allocation = await RelayService.Instance.CreateAllocationAsync(MaxRoomCapacity - 1);
                    if (!this || attempt != relayAttempt) return;
                    data = allocation.ToRelayServerData("dtls");
                    roomCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                }
                else
                {
                    if (publicRoomId != null)
                    {
                        code = await GetPublicRoomCode(publicRoomId);
                        if (!this || attempt != relayAttempt) return;
                    }
                    var allocation = await RelayService.Instance.JoinAllocationAsync(code);
                    if (!this || attempt != relayAttempt) return;
                    data = allocation.ToRelayServerData("dtls");
                }
                if (!this || attempt != relayAttempt) return;

                if (!relayTransport) relayTransport = gameObject.AddComponent<TrialRelayTransport>();
                relayTransport.Configure(data);
                transport = Transport.active = relayTransport;
                SaveNickname(nickname);
                ResetConnectionAttempt();
                if (host)
                {
                    maxConnections = RoomCapacity;
                    pendingRoomCode = roomCode;
                    relayBindDeadline = Time.unscaledTime + 12;
                    StartHost();
                }
                else
                {
                    connectDeadline = Time.unscaledTime + 12;
                    StartClient();
                }
                preparingRelay = false;
                Changed?.Invoke();
            }
            catch (Exception error)
            {
                if (!this || attempt != relayAttempt) return;
                Leave();
                SetStatus((host ? "创建失败" : "连接失败") + " · " + error.Message);
            }
        }

        void CancelRelay()
        {
            relayAttempt++;
            preparingRelay = false;
            relayBindDeadline = 0;
            pendingRoomCode = RelayRoomCode = null;
            PublicRelayHost = false;
            PublicDirectHost = false;
            publicDirectAddress = null;
            publicDirectPort = 0;
            RemovePublicListing();
        }

        void UpdateRelayHost()
        {
            if (!UsingRelay || !NetworkServer.active) return;
            var state = relayTransport.RelayStatus;
            if (state == RelayConnectionStatus.AllocationInvalid ||
                (relayBindDeadline > 0 && Time.unscaledTime > relayBindDeadline))
            {
                Leave();
                SetStatus("连接失败：Relay 房间已失效或中继连接超时");
            }
            else if (state == RelayConnectionStatus.Established && relayBindDeadline > 0)
            {
                relayBindDeadline = 0;
                if (PublicRelayHost) SetStatus(HostStatus);
                else FinishRelayHost(pendingRoomCode);
            }
        }

        void FinishRelayHost(string code)
        {
            RelayRoomCode = code;
            pendingRoomCode = null;
            SetStatus(HostStatus);
        }

        void SelectDirectTransport()
        {
            transport = Transport.active = directTransport;
        }
    }
}
