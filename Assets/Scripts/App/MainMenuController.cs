using System.Collections;
using System.Collections.Generic;
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
        public static bool PendingEnterReveal;

        const float ButtonFloatSeconds = 0.2f;
        const float ButtonStaggerSeconds = 0.06f;
        const float ButtonFloatPx = 28f;
        const float HudAppearSeconds = 0.22f;
        const float HudSlidePx = 18f;
        const float SceneSettleSeconds = 1.8f;
        const float UiRevealDelaySeconds = 0.72f;
        static readonly Vector2 TitleFromLogin = new Vector2(0f, 400f);
        static readonly Vector2 TitleAtHome = new Vector2(-4.03f, 570.5f);

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
            EmphasizeBattleButton(menu.transform);

            CacheHud(menu.transform);
            RefreshHud();

            bool reveal = PendingEnterReveal;
            PendingEnterReveal = false;
            bool onHome = Lobby.Instance == null || Lobby.Instance.CurrentPage == Lobby.Page.Home;
            if (reveal && onHome && menu.activeInHierarchy)
            {
                StartCoroutine(PlayEnterReveal(menu.transform));
                return true;
            }

            TutorialDirector.OnHomeReady();
            return true;
        }

        // “斗蛐蛐”是主玩法，只突出这一项；不加粗、不加阴影，避免复杂字形糊成一团。
        private static void EmphasizeBattleButton(Transform menu)
        {
            GameObject battle = FindNamed(menu, "MenuButtonBattle");
            if (battle == null) return;
            battle.transform.localScale = Vector3.one * 1.24f;
            Sprite battleBase = Resources.Load<Sprite>("MainMenu/Textures/BattleButtonBase");
            Image battleImage = battle.GetComponent<Image>();
            if (battleBase != null && battleImage != null)
            {
                battleImage.sprite = battleBase;
                battleImage.color = Color.white;
                battleImage.preserveAspect = true;
            }
            Color accent = new Color(1f, 0.78f, 0.18f, 1f);
            Text[] legacy = battle.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < legacy.Length; i++)
            {
                legacy[i].fontSize = Mathf.RoundToInt(legacy[i].fontSize * 1.34f);
                legacy[i].color = accent;
            }
            TMP_Text[] tmp = battle.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmp.Length; i++)
            {
                tmp[i].fontSize *= 1.34f;
                tmp[i].color = accent;
                tmp[i].fontStyle &= ~FontStyles.Bold;
                tmp[i].outlineWidth = 0f;
            }
        }

        private IEnumerator PlayEnterReveal(Transform menu)
        {
            RectTransform hudTop = FindRect(menu, "HUDTop");
            RectTransform hudBottom = FindRect(menu, "BottomBarContainer");
            List<RectTransform> buttons = CollectMenuButtons(menu);
            Vector2[] buttonRest = new Vector2[buttons.Count];
            CanvasGroup[] buttonGroups = new CanvasGroup[buttons.Count];
            for (int i = 0; i < buttons.Count; i++)
            {
                buttonRest[i] = buttons[i].anchoredPosition;
                buttonGroups[i] = HideForFloat(buttons[i], ButtonFloatPx);
            }

            Vector2 hudTopRest = hudTop != null ? hudTop.anchoredPosition : Vector2.zero;
            Vector2 hudBottomRest = hudBottom != null ? hudBottom.anchoredPosition : Vector2.zero;
            CanvasGroup hudTopGroup = HideForSlide(hudTop, HudSlidePx);
            CanvasGroup hudBottomGroup = HideForSlide(hudBottom, -HudSlidePx);

            StartCoroutine(PlaySceneSettle(menu));

            float delay = 0f;
            while (delay < UiRevealDelaySeconds)
            {
                delay += Time.unscaledDeltaTime;
                yield return null;
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                StartCoroutine(FloatIn(buttons[i], buttonGroups[i], buttonRest[i], ButtonFloatPx, ButtonFloatSeconds));
                if (i < buttons.Count - 1)
                {
                    float wait = 0f;
                    while (wait < ButtonStaggerSeconds)
                    {
                        wait += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
            }

            float last = 0f;
            while (last < ButtonFloatSeconds)
            {
                last += Time.unscaledDeltaTime;
                yield return null;
            }

            if (hudTop != null)
                StartCoroutine(SlideIn(hudTop, hudTopGroup, hudTopRest, HudSlidePx, HudAppearSeconds));
            if (hudBottom != null)
                StartCoroutine(SlideIn(hudBottom, hudBottomGroup, hudBottomRest, -HudSlidePx, HudAppearSeconds));

            float hudWait = 0f;
            while (hudWait < HudAppearSeconds)
            {
                hudWait += Time.unscaledDeltaTime;
                yield return null;
            }

            TutorialDirector.OnHomeReady();
        }

        private IEnumerator PlaySceneSettle(Transform menu)
        {
            RectTransform bg = FindRect(menu, "VillageBackground");
            RectTransform title = FindRect(menu, "BannerTitle");
            GameObject overlayGo = FindNamed(menu, "VillageOverlay");
            Image overlay = overlayGo != null ? overlayGo.GetComponent<Image>() : null;

            Vector3 bgFrom = new Vector3(LoginController.LoginBackgroundScale, LoginController.LoginBackgroundScale, 1f);
            Vector3 bgTo = Vector3.one;
            if (bg != null) bg.localScale = bgFrom;
            if (title != null) title.anchoredPosition = TitleFromLogin;

            Color overlayFrom = new Color(0f, 0f, 0f, 0.667f);
            Color overlayTo = new Color(overlayFrom.r, overlayFrom.g, overlayFrom.b, 0f);
            bool overlayWasEnabled = false;
            if (overlay != null)
            {
                overlayWasEnabled = overlay.enabled;
                overlay.enabled = true;
                overlay.color = overlayFrom;
            }

            float t = 0f;
            while (t < SceneSettleSeconds)
            {
                t += Time.unscaledDeltaTime;
                float u = EaseInOut(Mathf.Clamp01(t / SceneSettleSeconds));
                if (bg != null) bg.localScale = Vector3.LerpUnclamped(bgFrom, bgTo, u);
                if (title != null) title.anchoredPosition = Vector2.LerpUnclamped(TitleFromLogin, TitleAtHome, u);
                if (overlay != null) overlay.color = Color.Lerp(overlayFrom, overlayTo, u);
                yield return null;
            }

            if (bg != null) bg.localScale = bgTo;
            if (title != null) title.anchoredPosition = TitleAtHome;
            if (overlay != null)
            {
                overlay.color = overlayTo;
                overlay.enabled = overlayWasEnabled;
            }
        }

        private static List<RectTransform> CollectMenuButtons(Transform menu)
        {
            var list = new List<RectTransform>();
            GameObject container = FindNamed(menu, "MenuButtonsContainer");
            Transform root = container != null ? container.transform : menu;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name.StartsWith("MenuButton", System.StringComparison.Ordinal))
                    list.Add(child as RectTransform);
            }

            list.Sort((a, b) => b.anchoredPosition.y.CompareTo(a.anchoredPosition.y));
            return list;
        }

        private static RectTransform FindRect(Transform root, string objectName)
        {
            GameObject go = FindNamed(root, objectName);
            return go != null ? go.transform as RectTransform : null;
        }

        private static CanvasGroup HideForFloat(RectTransform rect, float fromBelow)
        {
            if (rect == null) return null;
            CanvasGroup group = EnsureGroup(rect.gameObject);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            Vector2 pos = rect.anchoredPosition;
            pos.y -= fromBelow;
            rect.anchoredPosition = pos;
            return group;
        }

        private static CanvasGroup HideForSlide(RectTransform rect, float fromY)
        {
            if (rect == null) return null;
            CanvasGroup group = EnsureGroup(rect.gameObject);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            Vector2 pos = rect.anchoredPosition;
            pos.y += fromY;
            rect.anchoredPosition = pos;
            return group;
        }

        private static IEnumerator FloatIn(RectTransform rect, CanvasGroup group, Vector2 rest, float fromBelow, float seconds)
        {
            if (rect == null) yield break;
            Vector2 from = rest;
            from.y -= fromBelow;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float u = EaseOutCubic(Mathf.Clamp01(t / seconds));
                rect.anchoredPosition = Vector2.LerpUnclamped(from, rest, u);
                if (group != null) group.alpha = u;
                yield return null;
            }

            rect.anchoredPosition = rest;
            if (group != null)
            {
                group.alpha = 1f;
                group.blocksRaycasts = true;
                group.interactable = true;
            }
        }

        private static IEnumerator SlideIn(RectTransform rect, CanvasGroup group, Vector2 rest, float fromY, float seconds)
        {
            if (rect == null) yield break;
            Vector2 from = rest;
            from.y += fromY;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float u = EaseOutCubic(Mathf.Clamp01(t / seconds));
                rect.anchoredPosition = Vector2.LerpUnclamped(from, rest, u);
                if (group != null) group.alpha = u;
                yield return null;
            }

            rect.anchoredPosition = rest;
            if (group != null)
            {
                group.alpha = 1f;
                group.blocksRaycasts = true;
                group.interactable = true;
            }
        }

        private static CanvasGroup EnsureGroup(GameObject go)
        {
            CanvasGroup group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            return group;
        }

        static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        static float EaseInOut(float t)
        {
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
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
