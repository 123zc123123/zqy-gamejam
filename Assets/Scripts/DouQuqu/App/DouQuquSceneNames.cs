using UnityEngine.SceneManagement;

namespace DouQuqu
{
    /// <summary>集中保存场景名称，避免各界面散落字符串并便于统一维护构建顺序。</summary>
    public static class DouQuquSceneNames
    {
        public const string Login = "DouQuquLogin";
        public const string MainMenu = "DouQuquMainMenu";
        public const string Merge = "DouQuquMerge";
        public const string Matchmaking = "DouQuquMatchmaking";
        public const string Collection = "DouQuquCollection";
        public const string Battle = "douququzhandou";
        public const string BattleDemo = "DouQuquDemo";

        /// <summary>以单场景模式切换，确保上一界面的对象和资源会被完整卸载。</summary>
        public static void Load(string sceneName)
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
