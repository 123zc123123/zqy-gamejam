using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 主界面：优先绑美术页上的入口按钮；没有美术页时退回代码占位菜单。
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private Text profileName;
        private TMP_Text profileNameTmp;
        private Text goldText;
        private TMP_Text goldTmp;

        private void Awake()
        {
            if (GetComponent<Lobby>() == null)
                gameObject.AddComponent<Lobby>();
        }

        private void OnEnable()
        {
            PlayerDataService.PlayerDataChanged += RefreshHud;
        }

        private void OnDisable()
        {
            PlayerDataService.PlayerDataChanged -= RefreshHud;
        }

        private void Start()
        {
            if (!PlayerDataService.RequireLogin()) return;
            if (TryBindArtMenu()) return;
            if (Lobby.Instance != null) return;
            BuildLegacyUi();
        }

        private bool TryBindArtMenu()
        {
            GameObject menu = FindNamed("MainMenu") ?? FindNamed("DouQuquMainMenu");
            if (menu == null) menu = FindNamed("MenuButtonsContainer");
            if (menu == null) return false;

            Bind(menu, "MenuButtonBattle", SceneNames.BattleEnter);
            Bind(menu, "MenuButtonBreeding", SceneNames.Merge);
            Bind(menu, "MenuButtonCatalogue", SceneNames.Collection);
            Bind(menu, "MenuButtonShop", SceneNames.Shop);
            Bind(menu, "MenuButtonRanking", SceneNames.Ranking);
            BindClick(menu, "SideButtonActivity", ActivityPopup.ShowActivity);
            TutorialDirector.OnHomeReady();

            CacheHud(menu.transform);
            RefreshHud();
            return true;
        }

        private static void Bind(GameObject root, string objectName, string sceneName)
        {
            BindClick(root, objectName, () => SceneNames.Load(sceneName));
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

            TMP_Text[] tmpLabels = target.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmpLabels.Length; i++)
                tmpLabels[i].raycastTarget = false;

            button.transition = Selectable.Transition.ColorTint;
            button.onClick.RemoveAllListeners();
            if (clicked != null) button.onClick.AddListener(clicked);
        }

        private void BuildLegacyUi()
        {
            RectTransform root = UiFactory.CreateScreen("MainMenuCanvas");
            RectTransform panel = UiFactory.CreatePanel(root, "MainMenuPanel",
                new Vector2(0.30f, 0.08f), new Vector2(0.70f, 0.92f), Vector2.zero, Vector2.zero);
            UiFactory.CreateText(panel, "Title", "主界面", 58f,
                new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.96f), Vector2.zero, Vector2.zero);
            UiFactory.CreateText(panel, "PlayerName", "玩家：" + PlayerDataService.CurrentPlayerName, 28f,
                new Vector2(0.08f, 0.74f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);

            UiFactory.CreateButton(panel, "MergeButton", "按钮 1 · 合成",
                () => SceneNames.Load(SceneNames.Merge),
                new Vector2(0.16f, 0.55f), new Vector2(0.84f, 0.67f), Vector2.zero, Vector2.zero);
            UiFactory.CreateButton(panel, "MatchButton", "按钮 2 · 匹配",
                () => SceneNames.Load(SceneNames.BattleEnter),
                new Vector2(0.16f, 0.38f), new Vector2(0.84f, 0.50f), Vector2.zero, Vector2.zero);
            UiFactory.CreateButton(panel, "CollectionButton", "按钮 3 · 图鉴",
                () => SceneNames.Load(SceneNames.Collection),
                new Vector2(0.16f, 0.21f), new Vector2(0.84f, 0.33f), Vector2.zero, Vector2.zero);
            UiFactory.CreateButton(panel, "LogoutButton", "退出登录", Logout,
                new Vector2(0.31f, 0.06f), new Vector2(0.69f, 0.14f), Vector2.zero, Vector2.zero);
        }

        private void Logout()
        {
            AppServices.Instance.Network.Stop();
            PlayerDataService.Logout();
            SceneNames.Load(SceneNames.Login);
        }

        private void CacheHud(Transform menu)
        {
            GameObject hud = FindNamed(menu, "HUDTop");
            Transform root = hud != null ? hud.transform : menu;

            GameObject nameGo = FindNamed(root, "ProfileName") ?? FindNamed(root, "PlayerName");
            if (nameGo != null)
            {
                profileNameTmp = nameGo.GetComponent<TMP_Text>();
                profileName = nameGo.GetComponent<Text>();
            }

            GameObject goldGo = FindGoldDisplay(root);
            if (goldGo != null)
            {
                goldTmp = goldGo.GetComponentInChildren<TMP_Text>(true);
                if (goldTmp == null) goldText = goldGo.GetComponentInChildren<Text>(true);
            }

            if (profileNameTmp == null && profileName == null)
                Debug.LogWarning("[DouQuqu] 主界面顶部没有玩家名 ProfileName");
            if (goldTmp == null && goldText == null)
                Debug.LogWarning("[DouQuqu] 主界面顶部没有金币 GoldDisplay");
        }

        private void RefreshHud()
        {
            if (!PlayerDataService.IsLoggedIn) return;

            string playerName = PlayerDataService.CurrentPlayerName;
            if (profileNameTmp != null) profileNameTmp.text = playerName;
            else if (profileName != null) profileName.text = playerName;

            string gold = PlayerDataService.FormatGold(PlayerDataService.Gold);
            if (goldTmp != null) goldTmp.text = gold;
            else if (goldText != null) goldText.text = gold;
        }

        private static GameObject FindGoldDisplay(Transform root)
        {
            GameObject exact = FindNamed(root, "GoldDisplay");
            if (exact != null) return exact;
            return FindGoldDisplayLoose(root);
        }

        private static GameObject FindGoldDisplayLoose(Transform root)
        {
            if (IsGoldDisplayName(root.name)) return root.gameObject;
            for (int i = 0; i < root.childCount; i++)
            {
                GameObject hit = FindGoldDisplayLoose(root.GetChild(i));
                if (hit != null) return hit;
            }
            return null;
        }

        private static bool IsGoldDisplayName(string objectName)
        {
            return objectName == "GoldDisplay"
                || objectName.StartsWith("GoldDisplay (", System.StringComparison.Ordinal);
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
    }
}
