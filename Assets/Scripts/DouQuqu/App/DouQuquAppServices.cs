using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 跨场景保留的应用服务根节点。局域网 Socket 必须在匹配界面切换到战斗场景时继续存活，
    /// 因此网络会话由此对象统一持有，而不是挂在某一个会被卸载的界面场景中。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class DouQuquAppServices : MonoBehaviour
    {
        private static DouQuquAppServices instance;

        public static DouQuquAppServices Instance
        {
            get
            {
                EnsureCreated();
                return instance;
            }
        }

        public DouQuquLanSession Network { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateBeforeFirstScene()
        {
            EnsureCreated();
        }

        private static void EnsureCreated()
        {
            if (instance != null) return;
            DouQuquAppServices existing = FindObjectOfType<DouQuquAppServices>();
            if (existing != null)
            {
                instance = existing;
                return;
            }
            GameObject root = new GameObject("DouQuquAppServices");
            instance = root.AddComponent<DouQuquAppServices>();
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
            Network = GetComponent<DouQuquLanSession>();
            if (Network == null) Network = gameObject.AddComponent<DouQuquLanSession>();
        }

        private void OnApplicationQuit()
        {
            if (Network != null) Network.Stop();
        }
    }
}
