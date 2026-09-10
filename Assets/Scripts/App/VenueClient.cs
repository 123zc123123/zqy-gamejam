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
        public sealed class LoginResponse
        {
            public PlayerProfile player;
            public string error;
        }

        [Serializable]
        public sealed class RankingResponse
        {
            public PlayerProfile[] players;
        }

        public static VenueClient Instance { get; private set; }
        public string BaseUrl { get; private set; }
        public bool HasServer => !string.IsNullOrEmpty(BaseUrl);
        public PlayerProfile[] CachedRanking { get; private set; }

        UdpClient socket;
        float listenUntil;

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

        public IEnumerator Login(string playerName, Action<PlayerProfile, string> done)
        {
            if (!HasServer)
            {
                if (done != null) done(null, null);
                yield break;
            }
            string json = "{\"playerName\":\"" + Escape(playerName) + "\"}";
            using (UnityWebRequest req = new UnityWebRequest(BaseUrl + "/login", "POST"))
            {
                byte[] body = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 4;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    if (done != null) done(null, "连不上展会账本，改用本机存档");
                    yield break;
                }
                LoginResponse parsed = JsonUtility.FromJson<LoginResponse>(req.downloadHandler.text);
                if (parsed == null || parsed.player == null)
                {
                    if (done != null) done(null, parsed != null ? parsed.error : "登录失败");
                    yield break;
                }
                if (done != null) done(parsed.player, null);
            }
        }

        public IEnumerator PushCurrent()
        {
            if (!HasServer || PlayerDataService.CurrentPlayer == null) yield break;
            string json = JsonUtility.ToJson(PlayerDataService.CurrentPlayer);
            using (UnityWebRequest req = new UnityWebRequest(BaseUrl + "/player", "PUT"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 4;
                yield return req.SendWebRequest();
            }
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

        static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
