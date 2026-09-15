using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>主界面「活动介绍」与进战页「玩法规则」共用同一形式的说明弹层。</summary>
    public sealed class ActivityPopup : MonoBehaviour
    {
        public const string ActivityPrefabPath = "Common/Prefabs/活动介绍";
        public const string RulesPrefabPath = "Common/Prefabs/玩法介绍";

        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text body;
        [SerializeField] Button closeButton;
        [SerializeField] Button dimButton;

        static ActivityPopup instance;
        static string loadedPath;

        const string ActivityBody =
            "一戳一蹦跶 · 斗蛐蛐\n\n把对手撞出圈即胜。\n一局 2 分钟；1:30 末口并全员狂暴。\n\n停稳后才能蓄力；蓄不满松手会取消。\n空中不能转向。护盾只挡一次出圈。";

        const string RulesBody =
            "四人同场，把对手撞出圈即胜。\n一局 2 分钟；1:30 末口并全员狂暴。\n\n停稳后才能蓄力；蓄不满松手会取消。\n空中不能转向。护盾只挡一次出圈。\n\n随机匹配与好友组队各开一房，满员后进入准备。";

        public static void Show()
        {
            ShowActivity();
        }

        public static void ShowActivity()
        {
            Show(ActivityPrefabPath, "活动介绍", ActivityBody);
        }

        public static void ShowRules()
        {
            Show(RulesPrefabPath, "玩法规则", RulesBody);
        }

        public static void ShowMessage(string titleText, string bodyText)
        {
            Show(ActivityPrefabPath, titleText, bodyText);
        }

        public static void Hide()
        {
            if (instance != null) instance.gameObject.SetActive(false);
        }

        void Awake()
        {
            ResolveRefs();
            BindButtons();
            UiFonts.ApplyTree(transform);
        }

        void ResolveRefs()
        {
            if (title == null)
            {
                Transform found = transform.Find("Panel/Title");
                if (found != null) title = found.GetComponent<TMP_Text>();
            }

            if (body == null)
            {
                Transform found = transform.Find("Panel/Body");
                if (found != null) body = found.GetComponent<TMP_Text>();
            }

            if (closeButton == null)
            {
                Transform found = transform.Find("Panel/CloseButton");
                if (found != null) closeButton = found.GetComponent<Button>();
            }

            if (dimButton == null)
            {
                Transform found = transform.Find("Dim");
                if (found != null) dimButton = found.GetComponent<Button>();
            }
        }

        void BindButtons()
        {
            Wire(dimButton, Hide);
            Wire(closeButton, Hide);
        }

        static void Wire(Button button, UnityEngine.Events.UnityAction clicked)
        {
            if (button == null) return;
            button.onClick.RemoveListener(clicked);
            button.onClick.AddListener(clicked);
        }

        static void Show(string prefabPath, string titleText, string bodyText)
        {
            ActivityPopup popup = Ensure(prefabPath);
            if (popup == null) return;
            popup.ResolveRefs();
            if (popup.title != null) popup.title.text = titleText;
            if (popup.body != null) popup.body.text = bodyText;
            popup.gameObject.SetActive(true);
        }

        static ActivityPopup Ensure(string prefabPath)
        {
            if (instance != null && loadedPath == prefabPath)
                return instance;

            if (instance != null)
            {
                instance.gameObject.SetActive(false);
                Object.Destroy(instance.gameObject);
                instance = null;
                loadedPath = null;
            }

            GameObject prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab != null)
            {
                GameObject go = Object.Instantiate(prefab);
                go.name = prefab.name;
                instance = go.GetComponent<ActivityPopup>();
                if (instance == null) instance = go.AddComponent<ActivityPopup>();
                loadedPath = prefabPath;
                return instance;
            }

            instance = CreateRuntime();
            loadedPath = prefabPath;
            return instance;
        }

        static ActivityPopup CreateRuntime()
        {
            GameObject canvasObject = new GameObject("ActivityPopupCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            ActivityPopup popup = canvasObject.AddComponent<ActivityPopup>();
            BuildVisual(canvasObject.transform, popup, "活动介绍", ActivityBody);
            return popup;
        }

        internal static void BuildVisual(Transform root, ActivityPopup popup, string titleText, string bodyText)
        {
            Image dim = MakeImage(root, "Dim", new Color(0.05f, 0.03f, 0.02f, 0.62f));
            Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;

            Image panel = MakeImage(root, "Panel", new Color(0.97f, 0.93f, 0.82f, 1f));
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(820f, 980f);
            panel.raycastTarget = true;
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.38f, 0.16f, 1f);
            outline.effectDistance = new Vector2(5f, -5f);

            TMP_Text title = MakeLabel(panelRect, "Title", titleText, 58f,
                new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.94f),
                new Color(0.45f, 0.18f, 0.12f, 1f), TextAlignmentOptions.Center);

            TMP_Text body = MakeLabel(panelRect, "Body", bodyText, 34f,
                new Vector2(0.10f, 0.24f), new Vector2(0.90f, 0.76f),
                new Color(0.28f, 0.16f, 0.10f, 1f), TextAlignmentOptions.TopLeft);

            Button close = MakeCloseButton(panelRect);
            close.transform.SetAsLastSibling();

            popup.title = title;
            popup.body = body;
            popup.closeButton = close;
            popup.dimButton = dimButton;
        }

        static Image MakeImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        static TMP_Text MakeLabel(Transform parent, string name, string content, float size,
            Vector2 anchorMin, Vector2 anchorMax, Color color, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = UiFactory.Font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        static Button MakeCloseButton(Transform parent)
        {
            GameObject prefab = Resources.Load<GameObject>("Common/Prefabs/btn-ready");
            Sprite blue = Resources.Load<Sprite>("Common/Textures/蓝色bg");
            GameObject go;
            if (prefab != null)
            {
                go = Object.Instantiate(prefab, parent, false);
            }
            else
            {
                go = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(parent, false);
                MakeLabel(go.transform, "Label", "知道了", 56f, Vector2.zero, Vector2.one,
                    Color.white, TextAlignmentOptions.Center);
            }

            go.name = "CloseButton";
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(321f, 141f);
            rect.anchoredPosition = new Vector2(0f, -350f);
            rect.localScale = Vector3.one;

            Image image = go.GetComponent<Image>();
            if (image != null)
            {
                if (blue != null) image.sprite = blue;
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = true;
            }

            TMP_Text tmp = go.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = "知道了";
                tmp.color = Color.white;
                tmp.raycastTarget = false;
            }

            Button button = go.GetComponent<Button>();
            if (button == null) button = go.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            button.colors = colors;
            return button;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
