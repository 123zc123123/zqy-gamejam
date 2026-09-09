using UnityEngine.SceneManagement;

namespace DouQuqu
{
    /// <summary>集中保存场景名称。大厅页走 Prefab 路由，只有登录和对局才真正切 Scene。</summary>
    public static class SceneNames
    {
        public const string Login = "Login";
        public const string MainMenu = "MainMenu";
        public const string Merge = "Merge";
        public const string HeroSelection = "HeroSelection";
        public const string Collection = "Collection";
        public const string Battle = "Battle_Main";
        public const string BattleDemo = "Demo";
        public const string BattleEntrance = "Battle_Enter";
        public const string BattleEnter = BattleEntrance;
        public const string Shop = "Shop";
        public const string Ranking = "Ranking";

        /// <summary>大厅页切换 Prefab；登录 / 对局仍 LoadScene(Single)。</summary>
        public static void Load(string sceneName)
        {
            if (Lobby.TryShow(sceneName)) return;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
