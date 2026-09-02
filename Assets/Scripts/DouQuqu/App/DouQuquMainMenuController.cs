using UnityEngine;

namespace DouQuqu
{
    /// <summary>主界面只负责三个玩法入口和退出当前账号。</summary>
    public sealed class DouQuquMainMenuController : MonoBehaviour
    {
        private void Start()
        {
            if (!DouQuquPlayerDataService.RequireLogin()) return;
            RectTransform root = DouQuquUiFactory.CreateScreen("MainMenuCanvas");
            RectTransform panel = DouQuquUiFactory.CreatePanel(root, "MainMenuPanel",
                new Vector2(0.30f, 0.08f), new Vector2(0.70f, 0.92f), Vector2.zero, Vector2.zero);
            DouQuquUiFactory.CreateText(panel, "Title", "主界面", 58f,
                new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.96f), Vector2.zero, Vector2.zero);
            DouQuquUiFactory.CreateText(panel, "PlayerName", "玩家：" + DouQuquPlayerDataService.CurrentPlayerName, 28f,
                new Vector2(0.08f, 0.74f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);

            DouQuquUiFactory.CreateButton(panel, "MergeButton", "按钮 1 · 合成",
                () => DouQuquSceneNames.Load(DouQuquSceneNames.Merge),
                new Vector2(0.16f, 0.55f), new Vector2(0.84f, 0.67f), Vector2.zero, Vector2.zero);
            DouQuquUiFactory.CreateButton(panel, "MatchButton", "按钮 2 · 匹配",
                () => DouQuquSceneNames.Load(DouQuquSceneNames.Matchmaking),
                new Vector2(0.16f, 0.38f), new Vector2(0.84f, 0.50f), Vector2.zero, Vector2.zero);
            DouQuquUiFactory.CreateButton(panel, "CollectionButton", "按钮 3 · 图鉴",
                () => DouQuquSceneNames.Load(DouQuquSceneNames.Collection),
                new Vector2(0.16f, 0.21f), new Vector2(0.84f, 0.33f), Vector2.zero, Vector2.zero);
            DouQuquUiFactory.CreateButton(panel, "LogoutButton", "退出登录", Logout,
                new Vector2(0.31f, 0.06f), new Vector2(0.69f, 0.14f), Vector2.zero, Vector2.zero);
        }

        private void Logout()
        {
            DouQuquAppServices.Instance.Network.Stop();
            DouQuquPlayerDataService.Logout();
            DouQuquSceneNames.Load(DouQuquSceneNames.Login);
        }
    }
}
