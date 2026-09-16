using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>功能页共用底栏：返回 + 可横滑入口。村子主页不挂这份栏。未实装的模块弹出即将开放。</summary>
    public sealed class BottomNavBar : MonoBehaviour
    {
        private const string CommonPrefabPath = "Common/Prefabs/BottomEventCarousel";

        private GameObject comingSoonRoot;
        private Text comingSoonLabel;
        private float comingSoonUntil;

        /// <summary>大厅壳占用底栏后，把各页自带的旧底栏藏起来。</summary>
        public static void SuppressEmbedded(Transform canvasRoot)
        {
            if (canvasRoot == null) return;
            HideLegacyCarousels(canvasRoot, null);
            BottomNavBar[] bars = canvasRoot.GetComponentsInChildren<BottomNavBar>(true);
            for (int i = 0; i < bars.Length; i++)
            {
                if (bars[i] != null) bars[i].gameObject.SetActive(false);
            }
        }

        /// <summary>把同一份可横滑底栏挂到任意功能页 Canvas 上。已有则复用。</summary>
        public static BottomNavBar EnsureOn(Transform canvasRoot)
        {
            if (canvasRoot == null) return null;

            BottomNavBar existing = canvasRoot.GetComponentInChildren<BottomNavBar>(true);
            if (existing != null)
            {
                HideLegacyCarousels(canvasRoot, existing.transform);
                ApplyLayout(existing.transform as RectTransform);
                return existing;
            }

            GameObject prefab = Resources.Load<GameObject>(CommonPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[DouQuqu] 找不到通用底栏 BottomEventCarousel");
                return null;
            }

            RectTransform legacy = FindLegacyCarousel(canvasRoot);
            GameObject instance = Object.Instantiate(prefab, canvasRoot, false);
            instance.name = "BottomEventCarousel";
            if (legacy != null) legacy.gameObject.SetActive(false);
            ApplyLayout(instance.GetComponent<RectTransform>());
            HideLegacyCarousels(canvasRoot, instance.transform);
            return instance.GetComponent<BottomNavBar>();
        }

        private void Awake()
        {
            ApplyLayout(transform as RectTransform);
            Bind();
        }

        private void Start()
        {
            BottomNavTab[] tabs = GetComponentsInChildren<BottomNavTab>(true);
            for (int i = 0; i < tabs.Length; i++)
            {
                if (!tabs[i].IsSelected) continue;
                EnsureTabVisible(tabs[i].GetComponent<RectTransform>());
                return;
            }

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
                back.onClick.AddListener(() => Go(SceneNames.MainMenu));
            }

            BottomNavTab[] tabs = GetComponentsInChildren<BottomNavTab>(true);
            for (int i = 0; i < tabs.Length; i++)
            {
                BottomNavTab tab = tabs[i];
                Button button = tab.Button;
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                BottomNavTab.NavModule module = tab.ModuleId;
                string title = string.IsNullOrEmpty(tab.DisplayName) ? tab.gameObject.name : tab.DisplayName;
                button.onClick.AddListener(() => OnTabClicked(module, title));
            }
        }

        public void Highlight(BottomNavTab.NavModule current, bool hasCurrent)
        {
            BottomNavTab[] tabs = GetComponentsInChildren<BottomNavTab>(true);
            BottomNavTab selected = null;
            for (int i = 0; i < tabs.Length; i++)
            {
                BottomNavTab tab = tabs[i];
                bool on = hasCurrent && tab.ModuleId == current;
                tab.SetSelected(on);
                if (on) selected = tab;
            }

            if (selected != null)
                EnsureTabVisible(selected.GetComponent<RectTransform>());
        }

        public void HighlightNone()
        {
            Highlight(BottomNavTab.NavModule.Battle, false);
        }

        public BottomNavTab FindTab(BottomNavTab.NavModule module)
        {
            BottomNavTab[] tabs = GetComponentsInChildren<BottomNavTab>(true);
            for (int i = 0; i < tabs.Length; i++)
                if (tabs[i] != null && tabs[i].ModuleId == module) return tabs[i];
            return null;
        }

        public ScrollRect Scroll
        {
            get { return GetComponentInChildren<ScrollRect>(true); }
        }

        public float NormalizedToReveal(RectTransform tab)
        {
            ScrollRect scroll = Scroll;
            if (scroll == null || scroll.content == null || tab == null)
                return 0f;

            Canvas.ForceUpdateCanvases();
            RectTransform viewport = scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
            if (viewport == null) return scroll.horizontalNormalizedPosition;

            float viewW = viewport.rect.width;
            float contentW = scroll.content.rect.width;
            if (contentW <= viewW + 1f) return 0f;

            Vector3[] tabCorners = new Vector3[4];
            Vector3[] viewCorners = new Vector3[4];
            tab.GetWorldCorners(tabCorners);
            viewport.GetWorldCorners(viewCorners);
            float tabLeft = scroll.content.InverseTransformPoint(tabCorners[0]).x;
            float tabRight = scroll.content.InverseTransformPoint(tabCorners[2]).x;
            float viewLeft = scroll.content.InverseTransformPoint(viewCorners[0]).x;
            float viewRight = scroll.content.InverseTransformPoint(viewCorners[2]).x;

            float shift = 0f;
            if (tabLeft < viewLeft) shift = tabLeft - viewLeft;
            else if (tabRight > viewRight) shift = tabRight - viewRight;
            if (Mathf.Abs(shift) < 1f) return scroll.horizontalNormalizedPosition;

            float maxScroll = contentW - viewW;
            float next = Mathf.Clamp(scroll.horizontalNormalizedPosition * maxScroll + shift, 0f, maxScroll);
            return next / maxScroll;
        }

        private void EnsureTabVisible(RectTransform tab)
        {
            if (tab == null) return;
            ScrollRect scroll = GetComponentInChildren<ScrollRect>(true);
            if (scroll == null || scroll.content == null) return;

            Canvas.ForceUpdateCanvases();
            RectTransform viewport = scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
            if (viewport == null) return;

            float viewW = viewport.rect.width;
            float contentW = scroll.content.rect.width;
            if (contentW <= viewW + 1f)
            {
                scroll.horizontalNormalizedPosition = 0f;
                return;
            }

            Vector3[] tabCorners = new Vector3[4];
            Vector3[] viewCorners = new Vector3[4];
            tab.GetWorldCorners(tabCorners);
            viewport.GetWorldCorners(viewCorners);
            float tabLeft = scroll.content.InverseTransformPoint(tabCorners[0]).x;
            float tabRight = scroll.content.InverseTransformPoint(tabCorners[2]).x;
            float viewLeft = scroll.content.InverseTransformPoint(viewCorners[0]).x;
            float viewRight = scroll.content.InverseTransformPoint(viewCorners[2]).x;

            float shift = 0f;
            if (tabLeft < viewLeft) shift = tabLeft - viewLeft;
            else if (tabRight > viewRight) shift = tabRight - viewRight;
            if (Mathf.Abs(shift) < 1f) return;

            float maxScroll = contentW - viewW;
            float next = Mathf.Clamp(scroll.horizontalNormalizedPosition * maxScroll + shift, 0f, maxScroll);
            scroll.horizontalNormalizedPosition = next / maxScroll;
        }

        private void OnTabClicked(BottomNavTab.NavModule module, string title)
        {
            switch (module)
            {
                case BottomNavTab.NavModule.Battle:
                    Go(SceneNames.BattleEnter);
                    return;
                case BottomNavTab.NavModule.Breeding:
                    Go(SceneNames.Merge);
                    return;
                case BottomNavTab.NavModule.Registry:
                    Go(SceneNames.Collection);
                    return;
                case BottomNavTab.NavModule.Shop:
                    Go(SceneNames.Shop);
                    return;
                case BottomNavTab.NavModule.Ranking:
                    Go(SceneNames.Ranking);
                    return;
                default:
                    ShowComingSoon(title);
                    return;
            }
        }

        private static void Go(string sceneName)
        {
            SceneNames.Load(sceneName);
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

        public static void NotifyComingSoon(string title)
        {
            BottomNavBar bar = FindObjectOfType<BottomNavBar>(true);
            if (bar == null)
            {
                Debug.Log("[DouQuqu] " + title + "即将开放");
                return;
            }

            bar.ShowComingSoon(title);
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
            "bottom-event-carousel",
            "下方菜单"
        };

        private static RectTransform FindLegacyCarousel(Transform canvasRoot)
        {
            for (int i = 0; i < LegacyCarouselNames.Length; i++)
            {
                Transform found = FindNamed(canvasRoot, LegacyCarouselNames[i]);
                if (found == null) continue;
                if (found.GetComponent<BottomNavBar>() != null) continue;
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
                if (found.GetComponent<BottomNavBar>() != null) continue;
                found.gameObject.SetActive(false);
            }
        }

        // 设计稿 1080 宽：返回钮左边距 24，Tab 视口左 174、右留 31.4。
        private const float BarHeight = 200f;
        private const float BackSize = 150f;
        private const float BackLeftMargin = 24f;
        private const float BackCenterY = 12f;
        private const float ViewportLeft = 174f;
        private const float ViewportRightMargin = 31.4f;
        private const float ViewportCenterY = 17.9343f;
        private const float ViewportHeight = 145.5313f;

        /// <summary>底栏拉满父级宽度；返回按钮按设计钉在左边，窄屏不再被居中裁掉。</summary>
        private static void ApplyLayout(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, BarHeight);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            Transform back = FindNamed(rect, "返回icon");
            if (back == null) back = FindNamed(rect, "BackIcon");
            RectTransform backRect = back as RectTransform;
            if (backRect != null)
            {
                backRect.anchorMin = new Vector2(0f, 0.5f);
                backRect.anchorMax = new Vector2(0f, 0.5f);
                backRect.pivot = new Vector2(0.5f, 0.5f);
                backRect.sizeDelta = new Vector2(BackSize, BackSize);
                backRect.anchoredPosition = new Vector2(BackLeftMargin + BackSize * 0.5f, BackCenterY);
                backRect.localScale = Vector3.one;
            }

            RectTransform viewport = FindNamed(rect, "TabViewport") as RectTransform;
            if (viewport == null) return;
            viewport.anchorMin = new Vector2(0f, 0.5f);
            viewport.anchorMax = new Vector2(1f, 0.5f);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.anchoredPosition = new Vector2((ViewportLeft - ViewportRightMargin) * 0.5f, ViewportCenterY);
            viewport.sizeDelta = new Vector2(-(ViewportLeft + ViewportRightMargin), ViewportHeight);
            viewport.localScale = Vector3.one;
        }
    }
}
