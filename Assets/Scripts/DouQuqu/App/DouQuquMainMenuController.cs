using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 主界面：优先绑美术页上的入口按钮；没有美术页时退回代码占位菜单。
    /// </summary>
    public sealed class DouQuquMainMenuController : MonoBehaviour
    {
        private void Start()
        {
            if (!DouQuquPlayerDataService.RequireLogin()) return;
            if (TryBindArtMenu()) return;
            BuildLegacyUi();
        }

        private bool TryBindArtMenu()
        {
            GameObject menu = FindNamed("DouQuquMainMenu");
            if (menu == null) menu = FindNamed("MenuButtonsContainer");
            if (menu == null) return false;

            Bind(menu, "MenuButtonBattle", DouQuquSceneNames.BattleEnter);
            Bind(menu, "MenuButtonBreeding", DouQuquSceneNames.Merge);
            Bind(menu, "MenuButtonCatalogue", DouQuquSceneNames.Collection);
            BindClick(menu, "SideButtonActivity", DouQuquActivityPopup.Show);

            Text profileName = FindLabel(menu.transform, "ProfileName");
            if (profileName != null)
                profileName.text = DouQuquPlayerDataService.CurrentPlayerName;
            return true;
        }

        private static void Bind(GameObject root, string objectName, string sceneName)
        {
            BindClick(root, objectName, () => DouQuquSceneNames.Load(sceneName));
        }

        private static void BindClick(GameObject root, string objectName, UnityEngine.Events.UnityAction clicked)
        {
            GameObject target = FindNamed(root.transform, objectName);
            if (target == null)
            {
                Debug.LogWarning("[DouQuqu] 主界面没有按钮 " + objectName);
                return;
            }

            Button button = target.GetComponent<Button>();
            if (button == null) button = target.AddComponent<Button>();

            Image[] images = target.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
                images[i].raycastTarget = false;

            Image hit = target.GetComponent<Image>();
            if (hit == null) hit = target.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            button.targetGraphic = hit;

            Text[] labels = target.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
                labels[i].raycastTarget = false;

            button.transition = Selectable.Transition.ColorTint;
            button.onClick.RemoveAllListeners();
            if (clicked != null) button.onClick.AddListener(clicked);
        }

        private void BuildLegacyUi()
        {
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

        private static GameObject FindNamed(string objectName)
        {
            Transform[] transforms = FindObjectsOfType<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t.name == objectName && t.gameObject.scene.IsValid())
                    return t.gameObject;
            }
            return null;
        }

        private static GameObject FindNamed(Transform root, string objectName)
        {
            if (root.name == objectName) return root.gameObject;
            for (int i = 0; i < root.childCount; i++)
            {
                GameObject hit = FindNamed(root.GetChild(i), objectName);
                if (hit != null) return hit;
            }
            return null;
        }

        private static Text FindLabel(Transform root, string objectName)
        {
            GameObject go = FindNamed(root, objectName);
            return go != null ? go.GetComponent<Text>() : null;
        }
    }
}
