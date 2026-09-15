using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DouQuqu
{
    /// <summary>展会笔记本账本：UDP 发现 + HTTP 登录/存档/排行。找不到服务器则不用。</summary>
    public sealed class VenueClient : MonoBehaviour
    {
        public const int DiscoveryPort = 28779;

        [Serializable]
        public sealed class RankingResponse
        {
            public PlayerProfile[] players;
        }

        [Serializable]
        sealed class VenuePlayerPayload
        {
            public string playerId;
            public string playerName;
            public long updatedAtUtcTicks;
            public int score;
            public int gold;
            public int eggs;
            public bool economyReady;
            public CricketCollectionEntry[] crickets;
            public CricketBackpackEntry[] backpack;
        }

        public static VenueClient Instance { get; private set; }
        public string BaseUrl { get; private set; }
        public bool HasServer => !string.IsNullOrEmpty(BaseUrl);
        public PlayerProfile[] CachedRanking { get; private set; }

        UdpClient socket;
        float listenUntil;
        UnityWebRequest activeLogin;

        public static VenueClient Ensure()
        {
            if (Instance != null) return Instance;
            AppServices host = AppServices.Instance;
            if (host == null) return null;
            Instance = host.GetComponent<VenueClient>();
            if (Instance == null) Instance = host.gameObject.AddComponent<VenueClient>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) return;
            Instance = this;
            BeginListen();
        }

        private void OnDestroy()
        {
            if (socket != null)
            {
                socket.Close();
                socket = null;
            }
        }

        void BeginListen()
        {
            try
            {
                socket = new UdpClient(DiscoveryPort);
                socket.EnableBroadcast = true;
                socket.Client.Blocking = false;
                listenUntil = Time.unscaledTime + 8f;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Venue] 无法监听账本广播: " + exception.Message);
            }
        }

        private void Update()
        {
            if (socket == null) return;
            while (socket.Available > 0)
            {
                try
                {
                    IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = socket.Receive(ref from);
                    string text = Encoding.UTF8.GetString(data);
                    if (!text.StartsWith("VENUE|", StringComparison.Ordinal)) continue;
                    string url = text.Substring(6).Trim().TrimEnd('/');
                    if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        if (BaseUrl != url)
                            Debug.Log("[Venue] 账本 " + url);
                        BaseUrl = url;
                    }
                }
                catch
                {
                    break;
                }
            }
            if (!HasServer && Time.unscaledTime > listenUntil && socket != null)
            {
                socket.Close();
                socket = null;
            }
        }

        public void AbortLogin()
        {
            if (activeLogin == null) return;
            activeLogin.Abort();
            activeLogin = null;
        }

        public IEnumerator PushCurrent(Action<bool> done)
        {
            if (!HasServer || PlayerDataService.CurrentPlayer == null)
            {
                if (done != null) done(false);
                yield break;
            }

            AbortLogin();
            string json = ToVenueJson(PlayerDataService.CurrentPlayer);
            using (UnityWebRequest req = new UnityWebRequest(BaseUrl + "/player", "PUT"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 2;
                activeLogin = req;
                yield return req.SendWebRequest();
                if (activeLogin == req) activeLogin = null;
                bool ok = req.result == UnityWebRequest.Result.Success;
                if (!ok)
                    Debug.LogWarning("[Venue] 本机档上传失败: " + req.error);
                if (done != null) done(ok);
            }
        }

        static string ToVenueJson(PlayerProfile player)
        {
            VenuePlayerPayload payload = new VenuePlayerPayload
            {
                playerId = player.playerId,
                playerName = player.playerName,
                updatedAtUtcTicks = player.updatedAtUtcTicks,
                score = player.score,
                gold = player.gold,
                eggs = player.eggs,
                economyReady = player.economyReady,
                crickets = ToArray(player.crickets),
                backpack = ToArray(player.backpack)
            };
            return JsonUtility.ToJson(payload);
        }

        static T[] ToArray<T>(System.Collections.Generic.List<T> list)
        {
            if (list == null || list.Count == 0) return new T[0];
            T[] result = new T[list.Count];
            list.CopyTo(result);
            return result;
        }

        public IEnumerator RefreshRanking(Action done)
        {
            if (!HasServer)
            {
                if (done != null) done();
                yield break;
            }
            using (UnityWebRequest req = UnityWebRequest.Get(BaseUrl + "/ranking"))
            {
                req.timeout = 4;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    RankingResponse parsed = JsonUtility.FromJson<RankingResponse>(req.downloadHandler.text);
                    if (parsed != null) CachedRanking = parsed.players;
                }
            }
            if (done != null) done();
        }
    }
}
