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
        public string roomCode;
        public string electionId;
        public string hostAddress;
        public bool dedicated;
    }

    [Serializable]
    /// <summary>主机和客户端都能看到的一个大厅槽位。</summary>
    public sealed class LanPlayerSlot
    {
        public int playerId;
        public string playerName;
        public bool connected;
        public bool ready;
        public bool selectionReady;
        public bool isBot;
    }

    [Serializable]
    /// <summary>玩家加入或准备状态变化后发送的大厅快照。</summary>
    public sealed class LanLobbySnapshot
    {
        public int capacity;
        public LanPlayerSlot[] slots;
        public bool matchStarting;
        public bool battleStarting;
        public int matchSeed;
    }

    [Serializable]
    /// <summary>选虫页提交给房主的锁定状态和三只蛐蛐阵容。</summary>
    public sealed class LanSelectionSubmission
    {
        public bool ready;
        public CricketPick[] picks;
    }

    /// <summary>
    /// Demo 使用的最小局域网传输层。通过 UDP 广播发现房间，通过 UDP 数据报
    /// 传输指令和快照，不依赖外部网络包。主机拥有权威状态并发送 MatchSnapshot。
    /// </summary>
    public sealed class LanSession : MonoBehaviour
    {
        // 固定端口让 Demo 无需额外配置；发现和对局使用不同 Socket，隔离广播流量。
        public const int SessionPort = 28777;
        public const int DiscoveryPort = 28778;

        [SerializeField] private MatchController match;
        // 20Hz 快照配合客户端表现插值。原来的 12.5Hz 在真机上会明显看到逐帧跳动，
        // 再继续提高则会让完整 JSON 快照带来更多 GC 和带宽压力。
        [SerializeField] private float snapshotInterval = 0.05f;
        [SerializeField] private string advertisedName = "DouQuqu Host";
        [SerializeField] private string localPlayerName = "Player";

        private UdpClient sessionSocket;
        private UdpClient discoverySocket;
        private IPEndPoint hostEndpoint;
        // 使用端点文本作为键；UDP 客户端可能使用动态源端口，主机只为该端点分配一次槽位。
        private readonly Dictionary<string, IPEndPoint> clients = new Dictionary<string, IPEndPoint>();
        private readonly Dictionary<string, int> clientIds = new Dictionary<string, int>();
        private readonly Dictionary<string, float> clientLastSeenAt = new Dictionary<string, float>();
        private readonly List<string> timedOutClients = new List<string>();
        private float snapshotTimer;
        private float discoveryTimer;
        private float heartbeatTimer;
        private bool awaitingWelcome;
        private float welcomeTimeoutRemaining;
        private float helloRetryTimer;
        private string rejectedHostKey = string.Empty;
        private float rejectedHostUntil;
        private string hostElectionId = string.Empty;
        private int lastSnapshotTick = -1;
        private const float InputSendInterval = 1f / 30f;
        private int outgoingInputSequence;
        private float nextInputSendAt;
        private bool hasSentInput;
        private bool lastSentInputHeld;
        private int roomCapacity = MatchController.MaxPlayers;
        private LanPlayerSlot[] slots = new LanPlayerSlot[MatchController.MaxPlayers];
        private bool running;
        // 独立服务器模式不占用玩家槽位；IsHost 仍为 true，复用现有主机协议。
        private bool dedicatedServer;
        // 普通房主淘汰后把权威 MatchController 暂存到 AppServices，允许表现层返回大厅。
        private bool persistentMatchController;
        private bool automaticMatchmaking;
        private float automaticElapsed;
        private float hostPromotionDelay;
        private float automaticTimeout = 30f;
        private bool matchReady;
        private bool matchReadyEventRaised;
        private bool battleStarting;
        private bool battleStartEventRaised;
        private int matchSeed;
        private float startBroadcastRemaining;
        private float startBroadcastTick;
        private string finalSnapshotBody;
        private float finalSnapshotBroadcastRemaining;
        private float finalSnapshotBroadcastTick;
        private bool pendingReadyRequest;
        private bool pendingReadyValue;
        private float readyRequestRemaining;
        private float readyRequestTick;
        private bool pendingSelectionReadyRequest;
        private bool pendingSelectionReadyValue;
        private CricketPick[] pendingSelectionPicks;
        private CricketPick[][] selectedRosters = new CricketPick[MatchController.MaxPlayers][];
        private float selectionReadyRequestRemaining;
        private float selectionReadyRequestTick;
        private MatchSnapshot pendingSnapshot;
        private int pendingWelcomePlayerCount;
        private string roomCode = string.Empty;
        private bool searchingRoom;
        private float roomSearchElapsed;
        private const float RoomSearchTimeout = 1.4f;
        private const float HeartbeatInterval = 1f;
        private const float ClientTimeout = 5f;
        private const float WelcomeTimeout = 3f;
        private const float HelloRetryInterval = 0.35f;
        private const float RejectedHostCooldown = 8f;

        public string RoomCode => roomCode;
        public bool IsRunning => running;
        public bool IsHost { get; private set; }
        public bool IsDedicatedServer => dedicatedServer;
        public int LocalPlayerId { get; private set; } = -1;
        public bool IsAutomaticMatchmaking => automaticMatchmaking && !matchReady;
        public bool IsMatchReady => matchReady;
        public bool IsBattleStarting => battleStarting;
        public float MatchmakingElapsed => automaticElapsed;
        public float MatchmakingTimeRemaining => Mathf.Max(0f, automaticTimeout - automaticElapsed);
        public string HostAddress => hostEndpoint == null ? string.Empty : hostEndpoint.Address.ToString();
        public IReadOnlyList<LanPlayerSlot> Slots => slots;
        public bool LocalPlayerReady
        {
            get
            {
                if (!IsHost && pendingReadyRequest) return pendingReadyValue;
                return LocalPlayerId >= 0
                    && LocalPlayerId < slots.Length
                    && slots[LocalPlayerId] != null
                    && slots[LocalPlayerId].ready;
            }
        }
        public bool LocalSelectionReady
        {
            get
            {
                if (!IsHost && pendingSelectionReadyRequest) return pendingSelectionReadyValue;
                return LocalPlayerId >= 0
                    && LocalPlayerId < slots.Length
                    && slots[LocalPlayerId] != null
                    && slots[LocalPlayerId].selectionReady;
            }
        }
        public bool CanStart
        {
            get
            {
                if (!IsHost) return false;
                return ConnectedHumanCount() >= roomCapacity;
            }
        }
        /// <summary>兼容旧流程的延迟开战开关；当前所有玩家准备完成后立即开战。</summary>
        public bool DeferBattleUntilSelectionTimeout { get; set; }

        public bool CanPrepareBattle
        {
            get
            {
                if (!IsHost || !matchReady) return false;
                for (int i = 0; i < roomCapacity && i < slots.Length; i++)
                {
                    if (!slots[i].connected) continue;
                    if (!slots[i].isBot && !slots[i].selectionReady) return false;
                }
                return ConnectedHumanCount() >= 1;
            }
        }

        public event Action<int> PlayerJoined;
        public event Action<string> HostDiscovered;
        public event Action<string> NetworkError;
        public event Action<LanLobbySnapshot> LobbyChanged;
        public event Action MatchReady;
        public event Action BattleReady;

        /// <summary>设置下一次 HELLO 数据包中发送的本地玩家名。</summary>
        public void SetLocalPlayerName(string playerName)
        {
            if (!string.IsNullOrWhiteSpace(playerName)) localPlayerName = playerName.Trim();
        }

        public static string NormalizeRoomCode(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            return raw.Trim().ToUpperInvariant();
        }

        /// <summary>同一 Wi-Fi、同一房号：先找到房则加入，约 1.4 秒没人则自己当房主。</summary>
        public bool JoinOrCreateRoom(string code, string playerName)
        {
            string normalized = NormalizeRoomCode(code);
            if (normalized.Length == 0)
            {
                NetworkError?.Invoke("请输入房间号");
                return false;
            }
            SetLocalPlayerName(playerName);
            StartClient();
            if (!running) return false;
            roomCode = normalized;
            searchingRoom = true;
            roomSearchElapsed = 0f;
            automaticMatchmaking = false;
            matchReady = false;
            SendDiscovery();
            NotifyLobbyChanged();
            return true;
        }

        /// <summary>
        /// 开始一键匹配。先查找局域网房间，短时间没有响应则自动创建主机；
        /// 房间满四人立即开始，否则总等待三十秒后用机器人补齐。
        /// </summary>
        public void StartAutomaticMatchmaking(string playerName, float timeoutSeconds = 30f)
        {
            SetLocalPlayerName(playerName);
            StartClient();
            if (!running) return;
            automaticMatchmaking = true;
            automaticElapsed = 0f;
            automaticTimeout = Mathf.Max(2f, timeoutSeconds);
            // 使用时间、名字和实例 ID 生成随机抖动，降低多台设备同时创建房间的概率。
            int jitterSeed = Environment.TickCount ^ GetInstanceID() ^ (localPlayerName == null ? 0 : localPlayerName.GetHashCode());
            System.Random jitter = new System.Random(jitterSeed);
            hostPromotionDelay = 0.9f + (float)jitter.NextDouble() * 0.9f;
            matchReady = false;
            matchReadyEventRaised = false;
            battleStarting = false;
            battleStartEventRaised = false;
            matchSeed = 0;
            startBroadcastRemaining = 0f;
            pendingSelectionReadyRequest = false;
            selectionReadyRequestRemaining = 0f;
            selectionReadyRequestTick = 0f;
            pendingSnapshot = null;
            SendDiscovery();
            NotifyLobbyChanged();
        }

        /// <summary>离开匹配界面时取消当前匹配和所有 Socket。</summary>
        public void CancelAutomaticMatchmaking()
        {
            Stop();
        }

        /// <summary>主动离开好友房；主机离开时通知其他设备重新选举房主。</summary>
        public void LeaveRoom()
        {
            if (running)
            {
                if (IsHost) BroadcastHostLeft();
                else if (hostEndpoint != null) SendEnvelope(sessionSocket, hostEndpoint, "LEAVE", string.Empty, LocalPlayerId);
            }
            Stop();
        }

        /// <summary>
        /// 战斗场景加载后把新场景中的权威控制器接到持久网络会话。
        /// 主机创建四槽状态并标记真人/机器人，客户端等待并应用主机快照。
        /// </summary>
        public void BindMatchController(MatchController matchController)
        {
            if (match != null) match.SnapshotReady -= OnSnapshotReady;
            match = matchController;
            if (match == null || !matchReady) return;
            match.SnapshotReady -= OnSnapshotReady;
            match.SnapshotReady += OnSnapshotReady;
            finalSnapshotBody = null;
            finalSnapshotBroadcastRemaining = 0f;
            finalSnapshotBroadcastTick = 0f;
            if (IsHost)
            {
                match.Configure(MatchRunMode.Host, roomCapacity, CompetitiveMatch.WithDuration(match.Knobs));
                match.ResetMatch(roomCapacity, matchSeed);
                for (int i = 0; i < roomCapacity && i < slots.Length; i++)
                {
                    CricketPick[] roster = selectedRosters != null && i < selectedRosters.Length
                        ? selectedRosters[i]
                        : null;
                    // 旧客户端或独立服务器超时未提交阵容时也必须有三条命，
                    // 避免第一只出场后被权威端直接判定为整名玩家淘汰。
                    match.SetRoster(i, roster ?? TrainingCamp.PicksForBot(i));
                    match.SetPlayerHuman(i, slots[i].connected && !slots[i].isBot);
                }
                match.StartMatch();
                BroadcastSnapshot();
            }
            else
            {
                int playerCount = pendingWelcomePlayerCount > 0 ? pendingWelcomePlayerCount : roomCapacity;
                match.Configure(MatchRunMode.Client, playerCount, CompetitiveMatch.WithDuration(match.Knobs));
                if (LocalPlayerId >= 0) match.SetPlayerHuman(LocalPlayerId, true);
                if (pendingSnapshot != null)
                {
                    match.ApplySnapshot(pendingSnapshot);
                    pendingSnapshot = null;
                }
            }
        }

        /// <summary>
        /// 普通房主淘汰后，把当前快照复制到跨场景对象，避免卸载战斗场景时停止权威模拟。
        /// 独立服务器和已经结束的对局不需要复制；返回 false 时调用方应结束网络会话。
        /// </summary>
        public bool DetachMatchControllerForSceneTransition()
        {
            if (persistentMatchController) return true;
            if (!running || !IsHost || dedicatedServer || match == null || match.IsOver || !match.IsStarted)
                return false;

            MatchSnapshot snapshot = match.CaptureSnapshot();
            if (snapshot == null || snapshot.bugs == null) return false;

            GameObject root = new GameObject("LanHostMatchAuthority");
            if (AppServices.Instance != null)
                root.transform.SetParent(AppServices.Instance.transform, false);
            else
                DontDestroyOnLoad(root);
            MatchController authority = root.AddComponent<MatchController>();
            authority.SetHeadlessSimulation(true);
            authority.Configure(MatchRunMode.Host, snapshot.playerCount, snapshot.knobs);
            authority.ApplySnapshot(snapshot);
            for (int i = 0; i < roomCapacity && i < slots.Length; i++)
            {
                LanPlayerSlot slot = slots[i];
                authority.SetPlayerHuman(i, slot != null && slot.connected && !slot.isBot);
            }

            // 这里不能再次调用 BindMatchController：本局已经在运行，重复绑定会重置快照；
            // 直接接管事件即可继续推进，并保留最终帧的冗余广播。
            if (match != null) match.SnapshotReady -= OnSnapshotReady;
            match = authority;
            match.SnapshotReady -= OnSnapshotReady;
            match.SnapshotReady += OnSnapshotReady;
            finalSnapshotBody = null;
            finalSnapshotBroadcastRemaining = 0f;
            finalSnapshotBroadcastTick = 0f;
            persistentMatchController = true;
            BroadcastSnapshot();
            return true;
        }

        private void Awake()
        {
            if (match == null) match = GetComponent<MatchController>();
        }

        private void Update()
        {
            if (!running) return;
            PollDiscovery();
            PollSession();
            TickWelcomeHandshake();
            TickHeartbeat();
            TickClientTimeouts();
            // 客户端持续找房；尚未锁定对局的主机也广播发现，用于检测同房号/随机池双房主。
            if (discoverySocket != null && (!IsHost || (!matchReady && !battleStarting)))
            {
                discoveryTimer -= Time.unscaledDeltaTime;
                if (discoveryTimer <= 0f)
                {
                    discoveryTimer = IsHost ? 0.35f : 1f;
                    SendDiscovery();
                }
            }
            TickAutomaticMatchmaking();
            TickRoomSearch();
            TickReadyRequest();
            TickSelectionReadyRequest();
            if (IsHost && match != null && match.IsStarted)
            {
                snapshotTimer -= Time.unscaledDeltaTime;
                if (snapshotTimer <= 0f)
                {
                    snapshotTimer = snapshotInterval;
                    BroadcastSnapshot();
                }
            }
            TickStartBroadcast();
            TickFinalSnapshotBroadcast();
        }

        private void OnDestroy()
        {
            Stop();
        }

        /// <summary>启动最多接受四个槽位的权威主机。</summary>
        public void StartHost(int players = 4)
        {
            Stop();
            dedicatedServer = false;
            try
            {
                IsHost = true;
                hostElectionId = Guid.NewGuid().ToString("N");
                LocalPlayerId = 0;
                sessionSocket = new UdpClient(SessionPort);
                sessionSocket.EnableBroadcast = true;
                discoverySocket = new UdpClient(DiscoveryPort);
                discoverySocket.EnableBroadcast = true;
                match?.Configure(MatchRunMode.Host, Mathf.Clamp(players, 1, MatchController.MaxPlayers));
                match?.ResetMatch(players, Environment.TickCount);
                running = true;
                roomCapacity = Mathf.Clamp(players, 1, MatchController.MaxPlayers);
                ResetSlots();
                slots[0].connected = true;
                slots[0].ready = true;
                slots[0].selectionReady = false;
                slots[0].isBot = false;
                slots[0].playerName = string.IsNullOrWhiteSpace(localPlayerName) ? "Host" : localPlayerName;
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

        /// <summary>
        /// 启动不占用玩家槽位的独立局域网服务器。
        /// 服务器只推进 MatchController，不显示战斗界面；手机端作为四个客户端加入。
        /// </summary>
        public void StartDedicatedServer(MatchController serverMatch, int players = MatchController.MaxPlayers,
            float matchmakingTimeout = 30f)
        {
            Stop();
            dedicatedServer = true;
            match = serverMatch;
            roomCapacity = Mathf.Clamp(players, 1, MatchController.MaxPlayers);
            automaticTimeout = Mathf.Max(2f, matchmakingTimeout);
            try
            {
                IsHost = true;
                hostElectionId = Guid.NewGuid().ToString("N");
                // 独立服务器不是玩家，客户端从 0 号槽位开始分配。
                LocalPlayerId = -1;
                sessionSocket = new UdpClient(SessionPort);
                sessionSocket.EnableBroadcast = true;
                discoverySocket = new UdpClient(DiscoveryPort);
                discoverySocket.EnableBroadcast = true;
                if (match != null)
                {
                    match.Configure(MatchRunMode.Host, roomCapacity);
                    match.ResetMatch(roomCapacity, Environment.TickCount);
                }

                running = true;
                automaticMatchmaking = true;
                automaticElapsed = 0f;
                hostPromotionDelay = 0f;
                matchReady = false;
                matchReadyEventRaised = false;
                battleStarting = false;
                battleStartEventRaised = false;
                matchSeed = 0;
                startBroadcastRemaining = 0f;
                pendingSnapshot = null;
                ResetSlots();
                lastSnapshotTick = -1;
                discoveryTimer = 0f;
                NotifyLobbyChanged();
            }
            catch (Exception exception)
            {
                NetworkError?.Invoke("无法创建独立局域网服务器: " + exception.Message);
                Stop();
            }
        }

        /// <summary>启动客户端并开始 UDP 广播发现。</summary>
        public void StartClient()
        {
            Stop();
            dedicatedServer = false;
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
            BeginWelcomeHandshake(new IPEndPoint(ip, SessionPort));
            return true;
        }

        /// <summary>停止 Socket、对局推进以及当前大厅成员关系。</summary>
        public void Stop()
        {
            if (match != null && match.IsStarted) match.StopMatch();
            if (match != null) match.SnapshotReady -= OnSnapshotReady;
            if (persistentMatchController && match != null)
            {
                // 新一局或退出应用时清理跨场景的无界面权威对象，避免每局累积。
                if (Application.isPlaying) Destroy(match.gameObject);
                else DestroyImmediate(match.gameObject);
            }
            match = null;
            running = false;
            dedicatedServer = false;
            persistentMatchController = false;
            hostEndpoint = null;
            clients.Clear();
            clientIds.Clear();
            clientLastSeenAt.Clear();
            timedOutClients.Clear();
            ResetSlots();
            if (sessionSocket != null) sessionSocket.Close();
            if (discoverySocket != null) discoverySocket.Close();
            sessionSocket = null;
            discoverySocket = null;
            IsHost = false;
            hostElectionId = string.Empty;
            LocalPlayerId = -1;
            lastSnapshotTick = -1;
            discoveryTimer = 0f;
            automaticMatchmaking = false;
            automaticElapsed = 0f;
            hostPromotionDelay = 0f;
            searchingRoom = false;
            roomSearchElapsed = 0f;
            roomCode = string.Empty;
            matchReady = false;
            matchReadyEventRaised = false;
            battleStarting = false;
            battleStartEventRaised = false;
            matchSeed = 0;
            startBroadcastRemaining = 0f;
            startBroadcastTick = 0f;
            finalSnapshotBody = null;
            finalSnapshotBroadcastRemaining = 0f;
            finalSnapshotBroadcastTick = 0f;
            pendingReadyRequest = false;
            readyRequestRemaining = 0f;
            readyRequestTick = 0f;
            pendingSelectionReadyRequest = false;
            pendingSelectionPicks = null;
            selectionReadyRequestRemaining = 0f;
            selectionReadyRequestTick = 0f;
            DeferBattleUntilSelectionTimeout = false;
            pendingSnapshot = null;
            pendingWelcomePlayerCount = 0;
            heartbeatTimer = 0f;
            awaitingWelcome = false;
            welcomeTimeoutRemaining = 0f;
            helloRetryTimer = 0f;
            rejectedHostKey = string.Empty;
            rejectedHostUntil = 0f;
        }

        /// <summary>选虫锁定后由房主统一广播开战，客户端不能单方面进入战斗。</summary>
        public void PrepareBattle()
        {
            if (!running) return;
            if (!IsHost || battleStarting) return;
            if (matchSeed == 0) matchSeed = Environment.TickCount;
            matchReady = true;
            battleStarting = true;
            if (IsHost)
            {
                startBroadcastRemaining = 1.2f;
                startBroadcastTick = 0f;
                BroadcastLobby();
                foreach (IPEndPoint endpoint in clients.Values)
                    SendEnvelope(sessionSocket, endpoint, "MATCH_START", matchSeed.ToString(), 0);
            }
            RaiseBattleReady();
        }

        /// <summary>所有已连接客户端准备后，由主机启动对局。</summary>
        public void StartMatchAsHost()
        {
            if (!IsHost || matchReady || !CanStart) return;
            MarkMatchReady();
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
                // 本机先更新显示，并短时间重发直到主机大厅快照确认，降低 UDP 单包丢失影响。
                pendingReadyRequest = true;
                pendingReadyValue = ready;
                readyRequestRemaining = 2f;
                readyRequestTick = 0f;
                NotifyLobbyChanged();
                SendReadyRequest();
            }
        }

        /// <summary>选虫页锁定状态；房主收齐所有真人后统一进入战斗。</summary>
        public void SetSelectionReady(bool ready, CricketPick[] picks = null)
        {
            if (LocalPlayerId < 0 || LocalPlayerId >= slots.Length) return;
            if (ready && picks != null) pendingSelectionPicks = CopySelectionPicks(picks);
            if (!ready) pendingSelectionPicks = null;
            if (IsHost)
            {
                if (ready) StoreSelectionRoster(LocalPlayerId, pendingSelectionPicks);
                else ClearSelectionRoster(LocalPlayerId);
                slots[LocalPlayerId].selectionReady = ready;
                BroadcastLobby();
                TryPrepareBattleFromSelectionReady();
            }
            else if (hostEndpoint != null)
            {
                pendingSelectionReadyRequest = true;
                pendingSelectionReadyValue = ready;
                selectionReadyRequestRemaining = 2f;
                selectionReadyRequestTick = 0f;
                NotifyLobbyChanged();
                SendSelectionReadyRequest();
            }
        }

        /// <summary>把本地输入发送给主机；如果自身是主机则立即应用。</summary>
        public void SendInput(Vector2 direction, bool held, bool released)
        {
            if (LocalPlayerId < 0) return;
            if (IsHost)
            {
                if (match != null) match.SetInput(new InputFrame(LocalPlayerId, direction, held, released));
            }
            else if (hostEndpoint != null)
            {
                // 摇杆脚本会逐渲染帧调用这里。限制普通采样频率，避免高刷手机用大量
                // 重复 JSON/UDP 包堵住房主主线程；按下、松开边沿始终立即发送。
                float now = Time.unscaledTime;
                bool inputEdge = released || !hasSentInput || held != lastSentInputHeld;
                if (!inputEdge && now < nextInputSendAt) return;

                InputFrame frame = new InputFrame(LocalPlayerId, direction, held, released, ++outgoingInputSequence);
                SendEnvelope(sessionSocket, hostEndpoint, "INPUT", JsonUtility.ToJson(frame), LocalPlayerId);
                hasSentInput = true;
                lastSentInputHeld = held;
                nextInputSendAt = now + InputSendInterval;
            }
        }

        /// <summary>推进自动匹配计时、自动建房以及四人满员/超时补机器人逻辑。</summary>
        private void TickAutomaticMatchmaking()
        {
            if (!automaticMatchmaking || matchReady) return;
            automaticElapsed += Time.unscaledDeltaTime;
            if (!IsHost)
            {
                if (hostEndpoint == null && automaticElapsed >= hostPromotionDelay)
                    PromoteToAutomaticHost();
                return;
            }

            if (ConnectedHumanCount() >= roomCapacity)
            {
                CompleteAutomaticMatch(false);
                return;
            }
            if (automaticElapsed >= automaticTimeout) CompleteAutomaticMatch(true);
        }

        private void TickRoomSearch()
        {
            if (!searchingRoom || matchReady) return;
            if (hostEndpoint != null || IsHost)
            {
                searchingRoom = false;
                return;
            }
            roomSearchElapsed += Time.unscaledDeltaTime;
            if (roomSearchElapsed < RoomSearchTimeout) return;
            string keepCode = roomCode;
            string keepName = localPlayerName;
            StartHost(MatchController.MaxPlayers);
            roomCode = keepCode;
            SetLocalPlayerName(keepName);
            if (!running)
            {
                StartClient();
                roomCode = keepCode;
                searchingRoom = true;
                roomSearchElapsed = 0f;
                return;
            }
            if (slots[0] != null)
                slots[0].playerName = string.IsNullOrWhiteSpace(keepName) ? "Host" : keepName;
            searchingRoom = false;
            NotifyLobbyChanged();
        }

        /// <summary>没有发现房间时升为主机；端口已被占用则退回客户端继续发现。</summary>
        private void PromoteToAutomaticHost()
        {
            float elapsed = automaticElapsed;
            float timeout = automaticTimeout;
            string playerName = localPlayerName;
            StartHost(MatchController.MaxPlayers);
            if (!running)
            {
                StartClient();
                if (!running) return;
                automaticMatchmaking = true;
                automaticElapsed = elapsed;
                automaticTimeout = timeout;
                hostPromotionDelay = elapsed + 1f;
                SetLocalPlayerName(playerName);
                return;
            }
            automaticMatchmaking = true;
            automaticElapsed = elapsed;
            automaticTimeout = timeout;
            SetLocalPlayerName(playerName);
            slots[0].playerName = string.IsNullOrWhiteSpace(playerName) ? "Host" : playerName;
            NotifyLobbyChanged();
        }

        /// <summary>由主机锁定房间；未连接的槽位按需转成机器人。</summary>
        private void CompleteAutomaticMatch(bool fillBots)
        {
            if (!IsHost || matchReady) return;
            if (fillBots)
            {
                for (int i = 0; i < roomCapacity && i < slots.Length; i++)
                {
                    if (slots[i].connected) continue;
                    slots[i].connected = true;
                    slots[i].ready = true;
                    slots[i].selectionReady = true;
                    slots[i].isBot = true;
                    slots[i].playerName = "机器人 " + (i + 1);
                }
            }
            MarkMatchReady();
        }

        /// <summary>统一锁定房间并通知所有设备进入选虫页。</summary>
        private void MarkMatchReady()
        {
            matchSeed = Environment.TickCount;
            matchReady = true;
            battleStarting = false;
            battleStartEventRaised = false;
            searchingRoom = false;
            ResetSelectionReadyForConnectedPlayers();
            BroadcastLobby();
            RaiseMatchReady();
        }

        private int ConnectedHumanCount()
        {
            int count = 0;
            for (int i = 0; i < roomCapacity && i < slots.Length; i++)
                if (slots[i].connected && !slots[i].isBot) count++;
            return count;
        }

        /// <summary>短时间重复广播开战消息，降低一次 UDP 丢包造成客户端留在匹配页的概率。</summary>
        private void TickStartBroadcast()
        {
            if (!IsHost || !battleStarting || startBroadcastRemaining <= 0f) return;
            startBroadcastRemaining -= Time.unscaledDeltaTime;
            startBroadcastTick -= Time.unscaledDeltaTime;
            if (startBroadcastTick > 0f) return;
            startBroadcastTick = 0.18f;
            foreach (IPEndPoint endpoint in clients.Values)
                SendEnvelope(sessionSocket, endpoint, "MATCH_START", matchSeed.ToString(), 0);
        }

        /// <summary>客户端重复发送准备状态，直到主机广播的大厅状态确认或超时。</summary>
        private void TickReadyRequest()
        {
            if (IsHost || !pendingReadyRequest || hostEndpoint == null) return;
            if (LocalPlayerId >= 0 && LocalPlayerId < slots.Length && slots[LocalPlayerId].ready == pendingReadyValue)
            {
                pendingReadyRequest = false;
                NotifyLobbyChanged();
                return;
            }

            readyRequestRemaining -= Time.unscaledDeltaTime;
            if (readyRequestRemaining <= 0f)
            {
                pendingReadyRequest = false;
                NotifyLobbyChanged();
                return;
            }

            readyRequestTick -= Time.unscaledDeltaTime;
            if (readyRequestTick > 0f) return;
            SendReadyRequest();
        }

        private void SendReadyRequest()
        {
            if (sessionSocket == null || hostEndpoint == null || LocalPlayerId < 0) return;
            readyRequestTick = 0.2f;
            SendEnvelope(sessionSocket, hostEndpoint, "READY", pendingReadyValue ? "1" : "0", LocalPlayerId);
        }

        /// <summary>客户端重复发送选虫锁定状态，直到主机大厅快照确认或超时。</summary>
        private void TickSelectionReadyRequest()
        {
            if (IsHost || !pendingSelectionReadyRequest || hostEndpoint == null) return;
            if (LocalPlayerId >= 0 && LocalPlayerId < slots.Length
                && slots[LocalPlayerId].selectionReady == pendingSelectionReadyValue)
            {
                pendingSelectionReadyRequest = false;
                NotifyLobbyChanged();
                return;
            }

            selectionReadyRequestRemaining -= Time.unscaledDeltaTime;
            if (selectionReadyRequestRemaining <= 0f)
            {
                pendingSelectionReadyRequest = false;
                NotifyLobbyChanged();
                return;
            }

            selectionReadyRequestTick -= Time.unscaledDeltaTime;
            if (selectionReadyRequestTick > 0f) return;
            SendSelectionReadyRequest();
        }

        private void SendSelectionReadyRequest()
        {
            if (sessionSocket == null || hostEndpoint == null || LocalPlayerId < 0) return;
            selectionReadyRequestTick = 0.2f;
            LanSelectionSubmission submission = new LanSelectionSubmission
            {
                ready = pendingSelectionReadyValue,
                picks = pendingSelectionReadyValue ? pendingSelectionPicks : null
            };
            SendEnvelope(sessionSocket, hostEndpoint, "SELECT_READY", JsonUtility.ToJson(submission), LocalPlayerId);
        }

        /// <summary>客户端定时向房主报活；即使停在选虫或加载界面，也能检测到直接关游戏。</summary>
        private void TickHeartbeat()
        {
            if (IsHost || sessionSocket == null || hostEndpoint == null || LocalPlayerId < 0) return;
            heartbeatTimer -= Time.unscaledDeltaTime;
            if (heartbeatTimer > 0f) return;
            heartbeatTimer = HeartbeatInterval;
            SendEnvelope(sessionSocket, hostEndpoint, "PING", string.Empty, LocalPlayerId);
        }

        /// <summary>房主将超时客户端移出；房间已锁定后原席位改由人机接管。</summary>
        private void TickClientTimeouts()
        {
            if (!IsHost || clientLastSeenAt.Count == 0) return;
            float now = Time.unscaledTime;
            timedOutClients.Clear();
            foreach (KeyValuePair<string, float> pair in clientLastSeenAt)
            {
                if (now - pair.Value >= ClientTimeout)
                    timedOutClients.Add(pair.Key);
            }

            if (timedOutClients.Count == 0) return;
            for (int i = 0; i < timedOutClients.Count; i++)
                RemoveClientSlot(timedOutClients[i]);
            BroadcastLobby();
            TryPrepareBattleFromSelectionReady();
        }

        /// <summary>捕获主机的结束帧；该帧需要冗余发送，不能依赖普通周期快照。</summary>
        private void OnSnapshotReady(MatchSnapshot snapshot)
        {
            if (!IsHost || snapshot == null || !snapshot.over) return;
            finalSnapshotBody = JsonUtility.ToJson(snapshot);
            finalSnapshotBroadcastRemaining = 2f;
            finalSnapshotBroadcastTick = 0f;
        }

        /// <summary>重复发送最终状态，避免某个客户端因 UDP 丢掉最后一包而无法进入结算。</summary>
        private void TickFinalSnapshotBroadcast()
        {
            if (!IsHost || finalSnapshotBroadcastRemaining <= 0f || string.IsNullOrEmpty(finalSnapshotBody)) return;
            finalSnapshotBroadcastRemaining -= Time.unscaledDeltaTime;
            finalSnapshotBroadcastTick -= Time.unscaledDeltaTime;
            if (finalSnapshotBroadcastTick > 0f) return;
            finalSnapshotBroadcastTick = 0.12f;
            foreach (IPEndPoint endpoint in clients.Values)
                SendEnvelope(sessionSocket, endpoint, "SNAPSHOT", finalSnapshotBody, 0);
        }

        private void RaiseMatchReady()
        {
            if (matchReadyEventRaised) return;
            matchReadyEventRaised = true;
            MatchReady?.Invoke();
        }

        private void RaiseBattleReady()
        {
            if (battleStartEventRaised) return;
            battleStartEventRaised = true;
            BattleReady?.Invoke();
        }

        private void TryPrepareBattleFromSelectionReady()
        {
            // 准备状态全部满足后立即开战；保留旧属性只是为了兼容已有调用方。
            if (CanPrepareBattle) PrepareBattle();
        }

        /// <summary>兼容旧的倒计时入口；现在只要所有玩家准备完成就会提前开战。</summary>
        public void StartBattleAfterSelectionTimeout()
        {
            DeferBattleUntilSelectionTimeout = false;
            if (!IsHost) return;
            if (CanPrepareBattle) PrepareBattle();
        }

        /// <summary>
        /// 独立服务器的选虫兜底：超时后把未提交选虫的真人按默认阵容锁定，
        /// 这样服务器不依赖任何客户端必须停留在选虫界面。
        /// </summary>
        public void PrepareDedicatedBattle()
        {
            if (!dedicatedServer || !IsHost || !matchReady || battleStarting) return;
            for (int i = 0; i < roomCapacity && i < slots.Length; i++)
            {
                if (slots[i] == null || !slots[i].connected) continue;
                slots[i].selectionReady = true;
            }
            BroadcastLobby();
            PrepareBattle();
        }

        private void ResetSelectionReadyForConnectedPlayers()
        {
            selectedRosters = new CricketPick[MatchController.MaxPlayers][];
            pendingSelectionPicks = null;
            for (int i = 0; i < roomCapacity && i < slots.Length; i++)
            {
                if (!slots[i].connected) continue;
                slots[i].selectionReady = slots[i].isBot;
            }
        }

        private int FindAvailableClientSlot()
        {
            int firstSlot = dedicatedServer ? 0 : 1;
            for (int i = firstSlot; i < roomCapacity && i < slots.Length; i++)
            {
                if (!slots[i].connected) return i;
            }
            return -1;
        }

        private void RemoveClientSlot(string key)
        {
            int id;
            if (!clientIds.TryGetValue(key, out id)) return;
            clientIds.Remove(key);
            clients.Remove(key);
            clientLastSeenAt.Remove(key);
            if (id < 0 || id >= slots.Length || slots[id] == null) return;

            // 进入选虫页后房间阵容已经锁定。此时玩家离开不能再留下一个永远
            // 等不到确认的空席位，直接沿用该槽位并交给确定性 AI；没有提交阵容
            // 时 BindMatchController 会自动使用机器人的默认三只虫。
            if (matchReady)
            {
                slots[id].connected = true;
                slots[id].ready = true;
                slots[id].selectionReady = true;
                slots[id].isBot = true;
                slots[id].playerName = "机器人 " + (id + 1);
                if (match != null) match.SetPlayerHuman(id, false);
                return;
            }

            slots[id].connected = false;
            slots[id].ready = false;
            slots[id].selectionReady = false;
            slots[id].isBot = false;
            slots[id].playerName = "Player " + (id + 1);
            ClearSelectionRoster(id);
            if (match != null) match.SetPlayerHuman(id, false);
        }

        private void BroadcastHostLeft()
        {
            LanLobbySnapshot lobby = CaptureLobby();
            string body = JsonUtility.ToJson(lobby);
            foreach (IPEndPoint endpoint in clients.Values)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                    SendEnvelope(sessionSocket, endpoint, "HOST_LEFT", body, 0);
            }
        }

        private void HandleHostLeft(LanLobbySnapshot lobby)
        {
            if (lobby == null || lobby.slots == null || matchReady || battleStarting) return;
            int oldLocalId = LocalPlayerId;
            string keepCode = roomCode;
            string keepName = localPlayerName;
            int capacity = lobby.capacity > 0 ? lobby.capacity : roomCapacity;
            int nextHost = ElectNextHost(lobby);
            if (nextHost < 0) return;

            Stop();
            roomCode = keepCode;
            SetLocalPlayerName(keepName);

            if (oldLocalId == nextHost)
            {
                StartHost(capacity);
                roomCode = keepCode;
                if (slots[0] != null)
                    slots[0].playerName = string.IsNullOrWhiteSpace(keepName) ? "Host" : keepName;
                NotifyLobbyChanged();
                return;
            }

            StartClient();
            if (!running) return;
            roomCode = keepCode;
            searchingRoom = true;
            roomSearchElapsed = 0f;
            SendDiscovery();
            NotifyLobbyChanged();
        }

        private static int ElectNextHost(LanLobbySnapshot lobby)
        {
            for (int i = 1; i < lobby.slots.Length; i++)
            {
                LanPlayerSlot slot = lobby.slots[i];
                if (slot == null || !slot.connected || slot.isBot) continue;
                return slot.playerId;
            }
            return -1;
        }

        // 客户端通过发现加入房间；未开战的主机也参与发现，以消除同房号/随机池双房主。
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
                    if (IsHost && (text == "DISCOVER" || text.StartsWith("DISCOVER|", StringComparison.Ordinal)))
                    {
                        string asked = text.StartsWith("DISCOVER|", StringComparison.Ordinal) ? text.Substring(9) : string.Empty;
                        if (asked.Length > 0 && !string.Equals(asked, roomCode, StringComparison.OrdinalIgnoreCase))
                            continue;
                        LanHostInfo info = new LanHostInfo
                        {
                            hostName = advertisedName,
                            port = SessionPort,
                            playerCount = ConnectedHumanCount(),
                            roomCode = roomCode,
                            electionId = hostElectionId,
                            dedicated = dedicatedServer
                        };
                        SendRaw(discoverySocket, endpoint, Encoding.UTF8.GetBytes("HOST|" + JsonUtility.ToJson(info)));
                    }
                    else if (text.StartsWith("HOST|", StringComparison.Ordinal))
                    {
                        LanHostInfo info = JsonUtility.FromJson<LanHostInfo>(text.Substring(5));
                        if (info == null || !MatchesDiscoveryPool(info)) continue;
                        if (IsHost)
                        {
                            if (ShouldYieldHostElection(info))
                            {
                                DemoteToDiscoveredHost(endpoint.Address, info);
                                return;
                            }
                            continue;
                        }
                        ConnectToDiscoveredHost(endpoint.Address, info);
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
            if (clientIds.ContainsKey(key)) clientLastSeenAt[key] = Time.unscaledTime;
            if (envelope.type == "HELLO")
            {
                if (matchReady) return;
                if (!clientIds.ContainsKey(key))
                {
                    int assigned = FindAvailableClientSlot();
                    if (assigned < 0) return;
                    clientIds[key] = assigned;
                    clients[key] = endpoint;
                    clientLastSeenAt[key] = Time.unscaledTime;
                    slots[assigned].connected = true;
                    slots[assigned].ready = automaticMatchmaking;
                    slots[assigned].selectionReady = false;
                    slots[assigned].isBot = false;
                    slots[assigned].playerName = string.IsNullOrWhiteSpace(envelope.body) ? "Player " + (assigned + 1) : envelope.body;
                    match?.SetPlayerHuman(assigned, true);
                    PlayerJoined?.Invoke(clientIds[key]);
                }
                int id = clientIds[key];
                LanWelcome welcome = new LanWelcome
                {
                    playerId = id,
                    playerCount = roomCapacity,
                    seed = matchSeed
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
            else if (envelope.type == "SELECT_READY" && clientIds.ContainsKey(key))
            {
                int id = clientIds[key];
                LanSelectionSubmission submission = ParseSelectionSubmission(envelope.body);
                bool ready = submission != null && submission.ready;
                if (ready) StoreSelectionRoster(id, submission.picks);
                else ClearSelectionRoster(id);
                slots[id].selectionReady = ready;
                BroadcastLobby();
                TryPrepareBattleFromSelectionReady();
            }
            else if (envelope.type == "LEAVE" && clientIds.ContainsKey(key))
            {
                RemoveClientSlot(key);
                BroadcastLobby();
                TryPrepareBattleFromSelectionReady();
            }
        }

        // 客户端按 tick 顺序应用快照，忽略延迟到达的旧 UDP 数据包。
        private void HandleClientMessage(LanEnvelope envelope)
        {
            if (envelope.type == "WELCOME")
            {
                LanWelcome welcome = JsonUtility.FromJson<LanWelcome>(envelope.body);
                if (welcome == null) return;
                awaitingWelcome = false;
                welcomeTimeoutRemaining = 0f;
                helloRetryTimer = 0f;
                rejectedHostKey = string.Empty;
                rejectedHostUntil = 0f;
                LocalPlayerId = welcome.playerId;
                pendingWelcomePlayerCount = Mathf.Clamp(welcome.playerCount, 1, MatchController.MaxPlayers);
                if (welcome.seed != 0) matchSeed = welcome.seed;
                match?.Configure(MatchRunMode.Client, pendingWelcomePlayerCount);
                if (match != null) match.SetPlayerHuman(LocalPlayerId, true);
                PlayerJoined?.Invoke(LocalPlayerId);
            }
            else if (envelope.type == "SNAPSHOT")
            {
                MatchSnapshot snapshot = JsonUtility.FromJson<MatchSnapshot>(envelope.body);
                if (snapshot != null && snapshot.tick >= lastSnapshotTick)
                {
                    lastSnapshotTick = snapshot.tick;
                    if (match != null) match.ApplySnapshot(snapshot);
                    else pendingSnapshot = snapshot;
                }
            }
            else if (envelope.type == "LOBBY")
            {
                LanLobbySnapshot lobby = JsonUtility.FromJson<LanLobbySnapshot>(envelope.body);
                if (lobby != null && lobby.slots != null)
                {
                    roomCapacity = lobby.capacity;
                    slots = lobby.slots;
                    if (pendingReadyRequest && LocalPlayerId >= 0 && LocalPlayerId < slots.Length
                        && slots[LocalPlayerId] != null && slots[LocalPlayerId].ready == pendingReadyValue)
                        pendingReadyRequest = false;
                    if (pendingSelectionReadyRequest && LocalPlayerId >= 0 && LocalPlayerId < slots.Length
                        && slots[LocalPlayerId] != null && slots[LocalPlayerId].selectionReady == pendingSelectionReadyValue)
                        pendingSelectionReadyRequest = false;
                    if (lobby.matchStarting)
                    {
                        matchSeed = lobby.matchSeed;
                        matchReady = true;
                        RaiseMatchReady();
                    }
                    if (lobby.battleStarting)
                    {
                        matchSeed = lobby.matchSeed;
                        matchReady = true;
                        battleStarting = true;
                        RaiseBattleReady();
                    }
                    LobbyChanged?.Invoke(lobby);
                }
            }
            else if (envelope.type == "MATCH_START")
            {
                int parsedSeed;
                if (int.TryParse(envelope.body, out parsedSeed)) matchSeed = parsedSeed;
                matchReady = true;
                battleStarting = true;
                RaiseBattleReady();
            }
            else if (envelope.type == "HOST_LEFT")
            {
                LanLobbySnapshot lobby = JsonUtility.FromJson<LanLobbySnapshot>(envelope.body);
                HandleHostLeft(lobby);
            }
            else if (envelope.type == "HOST_REDIRECT")
            {
                LanHostInfo info = JsonUtility.FromJson<LanHostInfo>(envelope.body);
                HandleHostRedirect(info);
            }
        }

        private void HandleHostRedirect(LanHostInfo info)
        {
            IPAddress address;
            if (info == null || string.IsNullOrEmpty(info.hostAddress)
                || !IPAddress.TryParse(info.hostAddress, out address)) return;

            bool keepAutomatic = automaticMatchmaking;
            float keepElapsed = automaticElapsed;
            float keepTimeout = automaticTimeout;
            string keepCode = roomCode;
            string keepName = localPlayerName;

            StartClient();
            if (!running) return;
            automaticMatchmaking = keepAutomatic;
            automaticElapsed = keepElapsed;
            automaticTimeout = keepTimeout;
            hostPromotionDelay = keepElapsed + 3f;
            roomCode = keepCode;
            SetLocalPlayerName(keepName);
            ConnectToDiscoveredHost(address, info);
            NotifyLobbyChanged();
        }

        private void SendDiscovery()
        {
            if (discoverySocket == null) return;
            string payload = roomCode.Length > 0 ? "DISCOVER|" + roomCode : "DISCOVER";
            SendRaw(discoverySocket, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort), Encoding.UTF8.GetBytes(payload));
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
                    ready = source.ready,
                    selectionReady = source.selectionReady,
                    isBot = source.isBot
                };
            }
            return new LanLobbySnapshot
            {
                capacity = roomCapacity,
                slots = copy,
                matchStarting = matchReady,
                battleStarting = battleStarting,
                matchSeed = matchSeed
            };
        }

        private void NotifyLobbyChanged()
        {
            LobbyChanged?.Invoke(CaptureLobby());
        }

        private void ResetSlots()
        {
            selectedRosters = new CricketPick[MatchController.MaxPlayers][];
            slots = new LanPlayerSlot[MatchController.MaxPlayers];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new LanPlayerSlot
                {
                    playerId = i,
                    playerName = "Player " + (i + 1),
                    connected = false,
                    ready = false,
                    selectionReady = false,
                    isBot = false
                };
        }

        private static LanSelectionSubmission ParseSelectionSubmission(string body)
        {
            // 接受旧版本只发送 1/0 的消息；阵容缺失时 BindMatchController 会补三只默认虫。
            if (body == "1") return new LanSelectionSubmission { ready = true };
            if (body == "0") return new LanSelectionSubmission { ready = false };
            if (string.IsNullOrWhiteSpace(body)) return new LanSelectionSubmission();
            try
            {
                return JsonUtility.FromJson<LanSelectionSubmission>(body) ?? new LanSelectionSubmission();
            }
            catch (Exception)
            {
                return new LanSelectionSubmission();
            }
        }

        private bool MatchesDiscoveryPool(LanHostInfo info)
        {
            if (info == null) return false;
            if (automaticMatchmaking)
                return string.IsNullOrEmpty(info.roomCode);
            if (!string.IsNullOrEmpty(roomCode))
                return string.Equals(info.roomCode, roomCode, StringComparison.OrdinalIgnoreCase);
            return true;
        }

        private bool ShouldYieldHostElection(LanHostInfo remote)
        {
            if (!IsHost || dedicatedServer || matchReady || battleStarting || remote == null || remote.dedicated)
                return remote != null && remote.dedicated && !dedicatedServer && !matchReady && !battleStarting;
            // 旧版本没有 electionId，不能参与确定性比较；由新版本主机保持现状。
            if (string.IsNullOrEmpty(hostElectionId) || string.IsNullOrEmpty(remote.electionId)) return false;
            // GUID 均匀随机；字典序较小者胜出，因此双方一定对同一结果达成一致。
            return string.CompareOrdinal(hostElectionId, remote.electionId) > 0;
        }

        private void ConnectToDiscoveredHost(IPAddress address, LanHostInfo info)
        {
            if (address == null || info == null || IsHost || sessionSocket == null || LocalPlayerId >= 0) return;
            IPEndPoint endpoint = new IPEndPoint(address, info.port > 0 ? info.port : SessionPort);
            string key = endpoint.ToString();
            if (key == rejectedHostKey && Time.unscaledTime < rejectedHostUntil) return;
            if (awaitingWelcome && hostEndpoint != null && hostEndpoint.Equals(endpoint)) return;

            searchingRoom = false;
            HostDiscovered?.Invoke(endpoint.Address + ":" + endpoint.Port);
            BeginWelcomeHandshake(endpoint);
        }

        private void BeginWelcomeHandshake(IPEndPoint endpoint)
        {
            if (endpoint == null || sessionSocket == null) return;
            hostEndpoint = endpoint;
            awaitingWelcome = true;
            welcomeTimeoutRemaining = WelcomeTimeout;
            helloRetryTimer = 0f;
            SendHello();
        }

        private void TickWelcomeHandshake()
        {
            if (!awaitingWelcome || IsHost || LocalPlayerId >= 0 || hostEndpoint == null) return;
            welcomeTimeoutRemaining -= Time.unscaledDeltaTime;
            helloRetryTimer -= Time.unscaledDeltaTime;
            if (helloRetryTimer <= 0f) SendHello();
            if (welcomeTimeoutRemaining > 0f) return;

            rejectedHostKey = hostEndpoint.ToString();
            rejectedHostUntil = Time.unscaledTime + RejectedHostCooldown;
            hostEndpoint = null;
            awaitingWelcome = false;
            helloRetryTimer = 0f;

            // 好友房回到搜房流程；随机匹配会在下一帧按原累计时间自动升为房主。
            if (!automaticMatchmaking && !string.IsNullOrEmpty(roomCode))
            {
                searchingRoom = true;
                roomSearchElapsed = 0f;
            }
            NotifyLobbyChanged();
        }

        private void SendHello()
        {
            if (sessionSocket == null || hostEndpoint == null) return;
            helloRetryTimer = HelloRetryInterval;
            SendEnvelope(sessionSocket, hostEndpoint, "HELLO", localPlayerName ?? string.Empty, -1);
        }

        private void DemoteToDiscoveredHost(IPAddress address, LanHostInfo info)
        {
            if (address == null || info == null || !IsHost || matchReady || battleStarting) return;

            info.hostAddress = address.ToString();
            string redirectBody = JsonUtility.ToJson(info);
            foreach (IPEndPoint client in clients.Values)
                for (int attempt = 0; attempt < 3; attempt++)
                    SendEnvelope(sessionSocket, client, "HOST_REDIRECT", redirectBody, 0);

            bool keepAutomatic = automaticMatchmaking;
            float keepElapsed = automaticElapsed;
            float keepTimeout = automaticTimeout;
            string keepCode = roomCode;
            string keepName = localPlayerName;

            StartClient();
            if (!running) return;
            automaticMatchmaking = keepAutomatic;
            automaticElapsed = keepElapsed;
            automaticTimeout = keepTimeout;
            hostPromotionDelay = keepElapsed + 3f;
            roomCode = keepCode;
            SetLocalPlayerName(keepName);
            ConnectToDiscoveredHost(address, info);
            NotifyLobbyChanged();
        }

        private void StoreSelectionRoster(int playerId, CricketPick[] picks)
        {
            if (playerId < 0 || playerId >= MatchController.MaxPlayers) return;
            if (selectedRosters == null || selectedRosters.Length != MatchController.MaxPlayers)
                selectedRosters = new CricketPick[MatchController.MaxPlayers][];
            selectedRosters[playerId] = CopySelectionPicks(picks);
        }

        private void ClearSelectionRoster(int playerId)
        {
            if (selectedRosters == null || playerId < 0 || playerId >= selectedRosters.Length) return;
            selectedRosters[playerId] = null;
        }

        private static CricketPick[] CopySelectionPicks(CricketPick[] source)
        {
            if (source == null) return null;
            CricketPick[] copy = new CricketPick[Mathf.Min(MatchController.LivesPerPlayer, source.Length)];
            for (int i = 0; i < copy.Length; i++)
            {
                CricketPick pick = source[i];
                copy[i] = pick == null
                    ? new CricketPick()
                    : new CricketPick
                    {
                        catalogId = pick.catalogId,
                        quality = Mathf.Clamp(pick.quality, 1, 4),
                        temperament = Mathf.Clamp(pick.temperament, 1, 4)
                    };
            }
            return copy;
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
