using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>各功能页共用底栏：返回 + 可横滑的六个功能入口。未实装的模块弹出即将开放。</summary>
    public sealed class DouQuquBottomNavBar : MonoBehaviour
    {
        private const string CommonPrefabPath = "Common/Prefabs/BottomEventCarousel";
        private const string MergePrefabPath = "Merge/Prefabs/Parts/BottomEventCarousel";

        private GameObject comingSoonRoot;
        private Text comingSoonLabel;
        private float comingSoonUntil;

        /// <summary>把同一份可横滑底栏挂到任意功能页 Canvas 上。已有则复用。</summary>
        public static DouQuquBottomNavBar EnsureOn(Transform canvasRoot)
        {
            if (canvasRoot == null) return null;

            DouQuquBottomNavBar existing = canvasRoot.GetComponentInChildren<DouQuquBottomNavBar>(true);
            if (existing != null)
            {
                HideLegacyCarousels(canvasRoot, existing.transform);
                return existing;
            }

            GameObject prefab = Resources.Load<GameObject>(CommonPrefabPath);
            if (prefab == null) prefab = Resources.Load<GameObject>(MergePrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[DouQuqu] 找不到通用底栏 BottomEventCarousel");
                return null;
            }

            RectTransform legacy = FindLegacyCarousel(canvasRoot);
            GameObject instance = Object.Instantiate(prefab, canvasRoot, false);
            instance.name = "BottomEventCarousel";
            RectTransform rect = instance.GetComponent<RectTransform>();
            if (legacy != null)
            {
                CopyRect(legacy, rect);
                legacy.gameObject.SetActive(false);
            }
            else
            {
                PlaceAtBottom(rect);
            }

            HideLegacyCarousels(canvasRoot, instance.transform);
            return instance.GetComponent<DouQuquBottomNavBar>();
        }

        private void Awake()
        {
            Bind();
        }

        private void Start()
        {
            ResetScroll();
        }

        private void ResetScroll()
        {
            ScrollRect scroll = GetComponentInChildren<ScrollRect>(true);
            if (scroll != null) scroll.horizontalNormalizedPosition = 0f;
        }

        private void Update()
        {
            if (comingSoonRoot == null || !comingSoonRoot.activeSelf) return;
            if (Time.unscaledTime >= comingSoonUntil) comingSoonRoot.SetActive(false);
        }

        private void Bind()
        {
            Button back = FindBackButton();
            if (back != null)
            {
                back.onClick.RemoveAllListeners();
                back.onClick.AddListener(() => Go(DouQuquSceneNames.MainMenu));
            }

            DouQuquBottomNavTab[] tabs = GetComponentsInChildren<DouQuquBottomNavTab>(true);
            for (int i = 0; i < tabs.Length; i++)
            {
                DouQuquBottomNavTab tab = tabs[i];
                Button button = tab.Button;
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                DouQuquBottomNavTab.NavModule module = tab.ModuleId;
                string title = string.IsNullOrEmpty(tab.DisplayName) ? tab.gameObject.name : tab.DisplayName;
                button.onClick.AddListener(() => OnTabClicked(module, title));
            }
        }

        private void OnTabClicked(DouQuquBottomNavTab.NavModule module, string title)
        {
            switch (module)
            {
                case DouQuquBottomNavTab.NavModule.Battle:
                    Go(DouQuquSceneNames.BattleEnter);
                    return;
                case DouQuquBottomNavTab.NavModule.Breeding:
                    Go(DouQuquSceneNames.Merge);
                    return;
                case DouQuquBottomNavTab.NavModule.Registry:
                    Go(DouQuquSceneNames.Collection);
                    return;
                default:
                    ShowComingSoon(title);
                    return;
            }
        }

        private static void Go(string sceneName)
        {
            if (SceneManager.GetActiveScene().name == sceneName) return;
            DouQuquSceneNames.Load(sceneName);
        }

        private Button FindBackButton()
        {
            Transform found = FindNamed(transform, "返回icon");
            if (found == null) found = FindNamed(transform, "BackIcon");
            if (found == null) return null;
            Button button = found.GetComponent<Button>();
            if (button == null) button = found.GetComponentInParent<Button>();
            return button;
        }

        private void ShowComingSoon(string title)
        {
            EnsureComingSoon();
            comingSoonLabel.text = title + "即将开放";
            comingSoonRoot.SetActive(true);
            comingSoonUntil = Time.unscaledTime + 1.6f;
        }

        private void EnsureComingSoon()
        {
            if (comingSoonRoot != null) return;
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            comingSoonRoot = new GameObject("ComingSoonToast", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            comingSoonRoot.transform.SetParent(parent, false);
            RectTransform rootRect = comingSoonRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            Image dim = comingSoonRoot.GetComponent<Image>();
            dim.color = new Color(0.05f, 0.03f, 0.02f, 0.35f);
            dim.raycastTarget = true;
            Button dimButton = comingSoonRoot.GetComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(() => comingSoonRoot.SetActive(false));

            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(comingSoonRoot.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 180f);
            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.97f, 0.93f, 0.82f, 1f);
            panelImage.raycastTarget = false;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(panel.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(24f, 16f);
            labelRect.offsetMax = new Vector2(-24f, -16f);
            comingSoonLabel = labelObject.GetComponent<Text>();
            comingSoonLabel.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 36);
            comingSoonLabel.fontSize = 40;
            comingSoonLabel.alignment = TextAnchor.MiddleCenter;
            comingSoonLabel.color = new Color(0.28f, 0.16f, 0.10f, 1f);
            comingSoonLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            comingSoonLabel.verticalOverflow = VerticalWrapMode.Overflow;
            comingSoonLabel.raycastTarget = false;
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamed(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        private static readonly string[] LegacyCarouselNames =
        {
            "BottomCarousel",
            "Screen10_593_BottomCarousel",
            "bottom-event-carousel"
        };

        private static RectTransform FindLegacyCarousel(Transform canvasRoot)
        {
            for (int i = 0; i < LegacyCarouselNames.Length; i++)
            {
                Transform found = FindNamed(canvasRoot, LegacyCarouselNames[i]);
                if (found == null) continue;
                if (found.GetComponent<DouQuquBottomNavBar>() != null) continue;
                return found as RectTransform;
            }
            return null;
        }

        private static void HideLegacyCarousels(Transform canvasRoot, Transform keep)
        {
            for (int i = 0; i < LegacyCarouselNames.Length; i++)
            {
                Transform found = FindNamed(canvasRoot, LegacyCarouselNames[i]);
                if (found == null || found == keep) continue;
                if (keep != null && found.IsChildOf(keep)) continue;
                if (found.GetComponent<DouQuquBottomNavBar>() != null) continue;
                found.gameObject.SetActive(false);
            }
        }

        private static void CopyRect(RectTransform source, RectTransform dest)
        {
            if (source == null || dest == null) return;
            dest.anchorMin = source.anchorMin;
            dest.anchorMax = source.anchorMax;
            dest.pivot = source.pivot;
            dest.anchoredPosition = source.anchoredPosition;
            dest.sizeDelta = source.sizeDelta;
            dest.localRotation = source.localRotation;
            dest.localScale = Vector3.one;
        }

        private static void PlaceAtBottom(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1080f, 200f);
            rect.localScale = Vector3.one;
        }
    }
}
