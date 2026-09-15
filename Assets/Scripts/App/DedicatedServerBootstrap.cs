using UnityEngine;
using UnityEngine.SceneManagement;

namespace DouQuqu
{
    /// <summary>
    /// 独立局域网服务器启动器。
    /// 同一个 Unity 包使用 -douququ-server 启动时，只保留网络和战斗模拟，不创建客户端界面。
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class DedicatedServerBootstrap : MonoBehaviour
    {
        private const string ServerArgument = "-douququ-server";
        private const float SelectionTimeout = 8f;

        private LanSession network;
        private MatchController serverMatch;
        private float selectionRemaining = -1f;
        private bool battleBound;

        /// <summary>带服务器参数启动时，在首个场景加载前创建持久根节点。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateOnServerLaunch()
        {
            if (!HasServerArgument()) return;
            if (FindObjectOfType<DedicatedServerBootstrap>() != null) return;
            new GameObject("DouQuquDedicatedServer").AddComponent<DedicatedServerBootstrap>();
        }

        private static bool HasServerArgument()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], ServerArgument, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // 无画面包在窗口失焦或后台运行时仍需持续推进固定 Tick。
            Application.runInBackground = true;
            SuppressClientScene();
            network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            if (network == null)
            {
                Debug.LogError("[DouQuqu] 独立服务器无法找到 LanSession。");
                return;
            }

            GameObject matchObject = new GameObject("DedicatedServerMatch");
            matchObject.transform.SetParent(transform, false);
            serverMatch = matchObject.AddComponent<MatchController>();
            network.MatchReady += OnMatchReady;
            network.BattleReady += OnBattleReady;
            network.NetworkError += OnNetworkError;
            network.StartDedicatedServer(serverMatch, MatchController.MaxPlayers, 10f);
        }

        private void SuppressClientScene()
        {
            // 服务器复用同一个 Unity 包，但不应启动登录/大厅的客户端脚本和 UI。
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i] != null && roots[i] != gameObject) roots[i].SetActive(false);
        }

        private void OnDestroy()
        {
            if (network == null) return;
            network.MatchReady -= OnMatchReady;
            network.BattleReady -= OnBattleReady;
            network.NetworkError -= OnNetworkError;
        }

        private void OnMatchReady()
        {
            // 给手机端留出选虫时间；超时由服务器用默认阵容补齐。
            selectionRemaining = SelectionTimeout;
        }

        private void Update()
        {
            if (network == null || !network.IsDedicatedServer || !network.IsMatchReady
                || network.IsBattleStarting) return;

            if (network.CanPrepareBattle)
            {
                network.PrepareBattle();
                return;
            }

            if (selectionRemaining < 0f) return;
            selectionRemaining -= Time.unscaledDeltaTime;
            if (selectionRemaining <= 0f) network.PrepareDedicatedBattle();
        }

        private void OnBattleReady()
        {
            if (battleBound || serverMatch == null) return;
            battleBound = true;
            network.BindMatchController(serverMatch);
        }

        private static void OnNetworkError(string message)
        {
            Debug.LogWarning("[DouQuqu] 独立服务器网络提示: " + message);
        }
    }
}
