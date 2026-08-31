using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace DouQuqu
{
    [Serializable]
    /// <summary>UDP 外层消息；body 按 type 保存对应的 JSON 数据。</summary>
    public sealed class LanEnvelope
    {
        public string type;
        public int senderId;
        public string body;
    }

    [Serializable]
    /// <summary>主机为客户端分配固定玩家槽位时返回的数据。</summary>
    public sealed class LanWelcome
    {
        public int playerId;
        public int playerCount;
        public int seed;
    }

    [Serializable]
    /// <summary>主机广播的局域网发现响应。</summary>
    public sealed class LanHostInfo
    {
        public string hostName;
        public int port;
        public int playerCount;
    }

    [Serializable]
    /// <summary>主机和客户端都能看到的一个大厅槽位。</summary>
    public sealed class LanPlayerSlot
    {
        public int playerId;
        public string playerName;
        public bool connected;
        public bool ready;
    }

    [Serializable]
    /// <summary>玩家加入或准备状态变化后发送的大厅快照。</summary>
    public sealed class LanLobbySnapshot
    {
        public int capacity;
        public LanPlayerSlot[] slots;
    }

    /// <summary>
    /// Demo 使用的最小局域网传输层。通过 UDP 广播发现房间，通过 UDP 数据报
    /// 传输指令和快照，不依赖外部网络包。主机拥有权威状态并发送 MatchSnapshot。
    /// </summary>
    public sealed class DouQuquLanSession : MonoBehaviour
    {
        // 固定端口让 Demo 无需额外配置；发现和对局使用不同 Socket，隔离广播流量。
        public const int SessionPort = 28777;
        public const int DiscoveryPort = 28778;

        [SerializeField] private DouQuquMatchController match;
        [SerializeField] private float snapshotInterval = 0.08f;
        [SerializeField] private string advertisedName = "DouQuqu Host";
        [SerializeField] private string localPlayerName = "Player";

        private UdpClient sessionSocket;
        private UdpClient discoverySocket;
        private IPEndPoint hostEndpoint;
        // 使用端点文本作为键；UDP 客户端可能使用动态源端口，主机只为该端点分配一次槽位。
        private readonly Dictionary<string, IPEndPoint> clients = new Dictionary<string, IPEndPoint>();
        private readonly Dictionary<string, int> clientIds = new Dictionary<string, int>();
        private float snapshotTimer;
        private float discoveryTimer;
        private int nextPlayerId = 1;
        private int lastSnapshotTick = -1;
        private int roomCapacity = DouQuquMatchController.MaxPlayers;
        private LanPlayerSlot[] slots = new LanPlayerSlot[DouQuquMatchController.MaxPlayers];
        private bool running;

        public bool IsRunning => running;
        public bool IsHost { get; private set; }
        public int LocalPlayerId { get; private set; } = -1;
        public string HostAddress => hostEndpoint == null ? string.Empty : hostEndpoint.Address.ToString();
        public IReadOnlyList<LanPlayerSlot> Slots => slots;
        public bool CanStart
        {
            get
            {
                if (!IsHost) return false;
                int connected = 0;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!slots[i].connected) continue;
                    connected++;
                    if (i != 0 && !slots[i].ready) return false;
                }
                return connected >= 1;
            }
        }

        public event Action<int> PlayerJoined;
        public event Action<string> HostDiscovered;
        public event Action<string> NetworkError;
        public event Action<LanLobbySnapshot> LobbyChanged;

        /// <summary>设置下一次 HELLO 数据包中发送的本地玩家名。</summary>
        public void SetLocalPlayerName(string playerName)
        {
            if (!string.IsNullOrWhiteSpace(playerName)) localPlayerName = playerName.Trim();
        }

        private void Awake()
        {
            if (match == null) match = GetComponent<DouQuquMatchController>();
        }

        private void Update()
        {
            if (!running) return;
            PollDiscovery();
            PollSession();
            if (!IsHost && discoverySocket != null)
            {
                discoveryTimer -= Time.unscaledDeltaTime;
                if (discoveryTimer <= 0f)
                {
                    discoveryTimer = 1f;
                    SendDiscovery();
                }
            }
            if (IsHost && match != null && match.IsStarted)
            {
                snapshotTimer -= Time.unscaledDeltaTime;
                if (snapshotTimer <= 0f)
                {
                    snapshotTimer = snapshotInterval;
                    BroadcastSnapshot();
                }
            }
        }

        private void OnDestroy()
        {
            Stop();
        }

        /// <summary>启动最多接受四个槽位的权威主机。</summary>
        public void StartHost(int players = 4)
        {
            Stop();
            try
            {
                IsHost = true;
                LocalPlayerId = 0;
                sessionSocket = new UdpClient(SessionPort);
                sessionSocket.EnableBroadcast = true;
                discoverySocket = new UdpClient(DiscoveryPort);
                discoverySocket.EnableBroadcast = true;
                match?.Configure(MatchRunMode.Host, Mathf.Clamp(players, 1, DouQuquMatchController.MaxPlayers));
                match?.ResetMatch(players, Environment.TickCount);
                running = true;
                roomCapacity = Mathf.Clamp(players, 1, DouQuquMatchController.MaxPlayers);
                ResetSlots();
                slots[0].connected = true;
                slots[0].ready = true;
                slots[0].playerName = string.IsNullOrWhiteSpace(localPlayerName) ? "Host" : localPlayerName;
                nextPlayerId = 1;
                lastSnapshotTick = -1;
                discoveryTimer = 0f;
                NotifyLobbyChanged();
            }
            catch (Exception exception)
            {
                NetworkError?.Invoke("无法创建局域网房间: " + exception.Message);
                Stop();
            }
        }

        /// <summary>启动客户端并开始 UDP 广播发现。</summary>
        public void StartClient()
        {
            Stop();
            try
            {
                IsHost = false;
                LocalPlayerId = -1;
                sessionSocket = new UdpClient(0);
                sessionSocket.EnableBroadcast = true;
                discoverySocket = new UdpClient(0);
                discoverySocket.EnableBroadcast = true;
                running = true;
                ResetSlots();
                lastSnapshotTick = -1;
                discoveryTimer = 0f;
                SendDiscovery();
            }
            catch (Exception exception)
            {
                NetworkError?.Invoke("无法启动局域网客户端: " + exception.Message);
                Stop();
            }
        }

        /// <summary>通过 IPv4 地址或可解析的主机名加入房间。</summary>
        public bool JoinAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return false;
            if (!running) StartClient();
            if (IsHost || sessionSocket == null) return false;
            IPAddress ip;
            if (!IPAddress.TryParse(address, out ip))
            {
                try { ip = Dns.GetHostAddresses(address)[0]; }
                catch (Exception exception)
                {
                    NetworkError?.Invoke("地址无效: " + exception.Message);
                    return false;
                }
            }
            hostEndpoint = new IPEndPoint(ip, SessionPort);
            SendEnvelope(sessionSocket, hostEndpoint, "HELLO", localPlayerName ?? string.Empty, -1);
            return true;
        }

        /// <summary>停止 Socket、对局推进以及当前大厅成员关系。</summary>
        public void Stop()
        {
            if (match != null && match.IsStarted) match.StopMatch();
            running = false;
            hostEndpoint = null;
            clients.Clear();
            clientIds.Clear();
            ResetSlots();
            if (sessionSocket != null) sessionSocket.Close();
            if (discoverySocket != null) discoverySocket.Close();
            sessionSocket = null;
            discoverySocket = null;
            IsHost = false;
            LocalPlayerId = -1;
            lastSnapshotTick = -1;
            discoveryTimer = 0f;
        }

        /// <summary>所有已连接客户端准备后，由主机启动对局。</summary>
        public void StartMatchAsHost()
        {
            if (!IsHost || match == null || !CanStart) return;
            match.StartMatch();
            BroadcastSnapshot();
        }

        /// <summary>设置本地准备标记；客户端模式下会发送给主机。</summary>
        public void SetReady(bool ready)
        {
            if (LocalPlayerId < 0 || LocalPlayerId >= slots.Length) return;
            if (IsHost)
            {
                slots[LocalPlayerId].ready = ready;
                BroadcastLobby();
            }
            else if (hostEndpoint != null)
            {
                SendEnvelope(sessionSocket, hostEndpoint, "READY", ready ? "1" : "0", LocalPlayerId);
            }
        }

        /// <summary>把本地输入发送给主机；如果自身是主机则立即应用。</summary>
        public void SendInput(Vector2 direction, bool held, bool released, float charge01 = 0f, bool distanceCharge = false)
        {
            if (LocalPlayerId < 0 || match == null) return;
            InputFrame frame = new InputFrame(LocalPlayerId, direction, held, released, 0, distanceCharge, charge01);
            if (IsHost)
            {
                match.SetInput(frame);
            }
            else if (hostEndpoint != null)
            {
                SendEnvelope(sessionSocket, hostEndpoint, "INPUT", JsonUtility.ToJson(frame), LocalPlayerId);
            }
        }

        // 发现流程无状态：客户端可以重复 DISCOVER，主机可以重复 HOST 回复，
        // 不会因此创建重复槽位。
        private void PollDiscovery()
        {
            if (discoverySocket == null) return;
            while (discoverySocket.Available > 0)
            {
                try
                {
                    IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = discoverySocket.Receive(ref endpoint);
                    string text = Encoding.UTF8.GetString(data);
                    if (text == "DISCOVER" && IsHost)
                    {
                        LanHostInfo info = new LanHostInfo { hostName = advertisedName, port = SessionPort, playerCount = match == null ? 0 : match.Bugs.Length };
                        SendRaw(discoverySocket, endpoint, Encoding.UTF8.GetBytes("HOST|" + JsonUtility.ToJson(info)));
                    }
                    else if (text.StartsWith("HOST|", StringComparison.Ordinal) && !IsHost)
                    {
                        LanHostInfo info = JsonUtility.FromJson<LanHostInfo>(text.Substring(5));
                        hostEndpoint = new IPEndPoint(endpoint.Address, info.port);
                        HostDiscovered?.Invoke(hostEndpoint.Address + ":" + info.port);
                        SendEnvelope(sessionSocket, hostEndpoint, "HELLO", localPlayerName ?? string.Empty, -1);
                    }
                }
                catch (Exception exception)
                {
                    NetworkError?.Invoke("局域网发现失败: " + exception.Message);
                    break;
                }
            }
        }

        // 在 Unity 主线程轮询 UDP，回调可直接更新 MatchController，无需跨线程同步。
        private void PollSession()
        {
            if (sessionSocket == null) return;
            while (sessionSocket.Available > 0)
            {
                try
                {
                    IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = sessionSocket.Receive(ref endpoint);
                    LanEnvelope envelope = JsonUtility.FromJson<LanEnvelope>(Encoding.UTF8.GetString(data));
                    if (envelope == null || string.IsNullOrEmpty(envelope.type)) continue;
                    if (IsHost) HandleHostMessage(endpoint, envelope);
                    else HandleClientMessage(envelope);
                }
                catch (Exception exception)
                {
                    NetworkError?.Invoke("局域网数据包错误: " + exception.Message);
                    break;
                }
            }
        }

        // 只有主机以权威身份接受 HELLO/INPUT/READY；槽位由网络端点决定，
        // 不信任数据包中自报的 senderId。
        private void HandleHostMessage(IPEndPoint endpoint, LanEnvelope envelope)
        {
            string key = endpoint.ToString();
            if (envelope.type == "HELLO")
            {
                if (!clientIds.ContainsKey(key))
                {
                    if (clientIds.Count >= roomCapacity - 1) return;
                    clientIds[key] = nextPlayerId++;
                    clients[key] = endpoint;
                    int assigned = clientIds[key];
                    slots[assigned].connected = true;
                    slots[assigned].ready = false;
                    slots[assigned].playerName = string.IsNullOrWhiteSpace(envelope.body) ? "Player " + (assigned + 1) : envelope.body;
                    match?.SetPlayerHuman(assigned, true);
                    PlayerJoined?.Invoke(clientIds[key]);
                }
                int id = clientIds[key];
                LanWelcome welcome = new LanWelcome
                {
                    playerId = id,
                    playerCount = match == null ? 0 : match.Bugs.Length,
                    seed = 0
                };
                SendEnvelope(sessionSocket, endpoint, "WELCOME", JsonUtility.ToJson(welcome), 0);
                BroadcastLobby();
                if (match != null && match.IsStarted) SendSnapshot(endpoint);
            }
            else if (envelope.type == "INPUT" && match != null)
            {
                InputFrame frame = JsonUtility.FromJson<InputFrame>(envelope.body);
                if (frame == null || !clientIds.ContainsKey(key)) return;
                frame.playerId = clientIds[key];
                match.SetInput(frame);
            }
            else if (envelope.type == "READY" && clientIds.ContainsKey(key))
            {
                int id = clientIds[key];
                slots[id].ready = envelope.body == "1";
                BroadcastLobby();
            }
        }

        // 客户端按 tick 顺序应用快照，忽略延迟到达的旧 UDP 数据包。
        private void HandleClientMessage(LanEnvelope envelope)
        {
            if (envelope.type == "WELCOME")
            {
                LanWelcome welcome = JsonUtility.FromJson<LanWelcome>(envelope.body);
                if (welcome == null) return;
                LocalPlayerId = welcome.playerId;
                match?.Configure(MatchRunMode.Client, welcome.playerCount);
                if (match != null) match.SetPlayerHuman(LocalPlayerId, true);
                PlayerJoined?.Invoke(LocalPlayerId);
            }
            else if (envelope.type == "SNAPSHOT" && match != null)
            {
                MatchSnapshot snapshot = JsonUtility.FromJson<MatchSnapshot>(envelope.body);
                if (snapshot != null && snapshot.tick >= lastSnapshotTick)
                {
                    lastSnapshotTick = snapshot.tick;
                    match.ApplySnapshot(snapshot);
                }
            }
            else if (envelope.type == "LOBBY")
            {
                LanLobbySnapshot lobby = JsonUtility.FromJson<LanLobbySnapshot>(envelope.body);
                if (lobby != null && lobby.slots != null)
                {
                    roomCapacity = lobby.capacity;
                    slots = lobby.slots;
                    LobbyChanged?.Invoke(lobby);
                }
            }
        }

        private void SendDiscovery()
        {
            if (discoverySocket == null) return;
            SendRaw(discoverySocket, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort), Encoding.UTF8.GetBytes("DISCOVER"));
        }

        // 对局快照以主机为权威；客户端不自行模拟后再尝试回滚同步。
        private void BroadcastSnapshot()
        {
            foreach (IPEndPoint endpoint in clients.Values) SendSnapshot(endpoint);
        }

        private void BroadcastLobby()
        {
            LanLobbySnapshot lobby = CaptureLobby();
            string body = JsonUtility.ToJson(lobby);
            foreach (IPEndPoint endpoint in clients.Values)
                SendEnvelope(sessionSocket, endpoint, "LOBBY", body, 0);
            LobbyChanged?.Invoke(lobby);
        }

        private LanLobbySnapshot CaptureLobby()
        {
            LanPlayerSlot[] copy = new LanPlayerSlot[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                LanPlayerSlot source = slots[i];
                copy[i] = new LanPlayerSlot
                {
                    playerId = source.playerId,
                    playerName = source.playerName,
                    connected = source.connected,
                    ready = source.ready
                };
            }
            return new LanLobbySnapshot { capacity = roomCapacity, slots = copy };
        }

        private void NotifyLobbyChanged()
        {
            LobbyChanged?.Invoke(CaptureLobby());
        }

        private void ResetSlots()
        {
            slots = new LanPlayerSlot[DouQuquMatchController.MaxPlayers];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new LanPlayerSlot { playerId = i, playerName = "Player " + (i + 1), connected = false, ready = false };
        }

        private void SendSnapshot(IPEndPoint endpoint)
        {
            if (match == null || endpoint == null) return;
            SendEnvelope(sessionSocket, endpoint, "SNAPSHOT", JsonUtility.ToJson(match.CaptureSnapshot()), 0);
        }

        // 所有协议消息共用此信封，新增指令时无需增加 Socket 或序列化路径。
        private void SendEnvelope(UdpClient socket, IPEndPoint endpoint, string type, string body, int senderId)
        {
            if (socket == null || endpoint == null) return;
            LanEnvelope envelope = new LanEnvelope { type = type, body = body ?? string.Empty, senderId = senderId };
            SendRaw(socket, endpoint, Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope)));
        }

        private void SendRaw(UdpClient socket, IPEndPoint endpoint, byte[] data)
        {
            try { socket.Send(data, data.Length, endpoint); }
            catch (Exception exception) { NetworkError?.Invoke("发送失败: " + exception.Message); }
        }
    }
}
