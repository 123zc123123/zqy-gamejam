using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 跨场景保留的应用服务根节点。局域网 Socket 必须在匹配界面切换到战斗场景时继续存活，
    /// 因此网络会话由此对象统一持有，而不是挂在某一个会被卸载的界面场景中。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class AppServices : MonoBehaviour
    {
        private static AppServices instance;

        public static AppServices Instance
        {
            get
            {
                EnsureCreated();
                return instance;
            }
        }

        public LanSession Network { get; private set; }

        /// <summary>进战入口种类。选虫页和战斗开局读取；战斗读走后清空。</summary>
        public static MatchKind PendingMatchKind { get; set; }

        /// <summary>选虫页锁定后带到战斗场景的己方三槽。战斗开局读走即清空。</summary>
        public static CricketPick[] PendingLocalPicks { get; set; }

        public static CricketPick[] TakePendingLocalPicks()
        {
            CricketPick[] picks = PendingLocalPicks;
            PendingLocalPicks = null;
            return picks;
        }

        public static MatchKind TakePendingMatchKind()
        {
            MatchKind kind = PendingMatchKind;
            PendingMatchKind = MatchKind.None;
            return kind;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateBeforeFirstScene()
        {
            EnsureCreated();
        }

        private static void EnsureCreated()
        {
            if (instance != null) return;
            AppServices existing = FindObjectOfType<AppServices>();
            if (existing != null)
            {
                instance = existing;
                return;
            }
            GameObject root = new GameObject("AppServices");
            instance = root.AddComponent<AppServices>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            Network = GetComponent<LanSession>();
            if (Network == null) Network = gameObject.AddComponent<LanSession>();
            if (GetComponent<VenueClient>() == null) gameObject.AddComponent<VenueClient>();
        }

        private void OnApplicationQuit()
        {
            if (Network != null) Network.Stop();
        }
    }
}
