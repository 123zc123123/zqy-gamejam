using UnityEngine.SceneManagement;

namespace DouQuqu
{
    /// <summary>集中保存场景名称，避免各界面散落字符串并便于统一维护构建顺序。</summary>
    public static class DouQuquSceneNames
    {
        public const string Login = "Login";
        public const string MainMenu = "MainMenu";
        public const string Merge = "Merge";
        public const string Matchmaking = "Battle_Matchmaking";
        public const string Collection = "Collection";
        public const string Battle = "Battle_Main";
        public const string BattleDemo = "Demo";
        public const string BattleEnter = "Battle_Enter";

        /// <summary>以单场景模式切换，确保上一界面的对象和资源会被完整卸载。</summary>
        public static void Load(string sceneName)
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
