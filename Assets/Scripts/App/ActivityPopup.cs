using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>主界面「活动介绍」与进战页「玩法规则」共用同一形式的说明弹层。</summary>
    public sealed class ActivityPopup : MonoBehaviour
    {
        private static ActivityPopup instance;
        private TMP_Text title;
        private TMP_Text body;

        private const string ActivityBody =
            "一戳一蹦跶 · 斗蛐蛐\n\n把对手撞出圈即胜。\n一局 2 分钟；1:30 末口并全员狂暴。\n\n停稳后才能蓄力；蓄不满松手会取消。\n空中不能转向。护盾只挡一次出圈。";

        private const string RulesBody =
            "四人同场，把对手撞出圈即胜。\n一局 2 分钟；1:30 末口并全员狂暴。\n\n停稳后才能蓄力；蓄不满松手会取消。\n空中不能转向。护盾只挡一次出圈。\n\n随机匹配与好友组队各开一房，满员后进入准备。";

        public static void Show()
        {
            ShowActivity();
        }

        public static void ShowActivity()
        {
            Show("活动介绍", ActivityBody);
        }

        public static void ShowRules()
        {
            Show("玩法规则", RulesBody);
        }

        private static void Show(string titleText, string bodyText)
        {
            if (instance == null) instance = Create();
            if (instance.title != null) instance.title.text = titleText;
            if (instance.body != null) instance.body.text = bodyText;
            instance.gameObject.SetActive(true);
        }

        public static void Hide()
        {
            if (instance != null) instance.gameObject.SetActive(false);
        }

        private static ActivityPopup Create()
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

            Image dim = MakeImage(canvasObject.transform, "Dim", new Color(0.05f, 0.03f, 0.02f, 0.62f));
            Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(Hide);

            Image panel = MakeImage(canvasObject.transform, "Panel", new Color(0.97f, 0.93f, 0.82f, 1f));
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(820f, 980f);
            panel.raycastTarget = true;
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.38f, 0.16f, 1f);
            outline.effectDistance = new Vector2(5f, -5f);

            popup.title = MakeLabel(panelRect, "Title", "活动介绍", 58f,
                new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.94f),
                new Color(0.45f, 0.18f, 0.12f, 1f), TextAlignmentOptions.Center);

            popup.body = MakeLabel(panelRect, "Body", ActivityBody, 34f,
                new Vector2(0.10f, 0.24f), new Vector2(0.90f, 0.76f),
                new Color(0.28f, 0.16f, 0.10f, 1f), TextAlignmentOptions.TopLeft);

            Button close = MakeButton(panelRect, "CloseButton", "知道了",
                new Vector2(0.22f, 0.07f), new Vector2(0.78f, 0.18f), Hide);
            close.transform.SetAsLastSibling();

            return popup;
        }

        private static Image MakeImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text MakeLabel(Transform parent, string name, string content, float size,
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

        private static Button MakeButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction clicked)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            Color fill = new Color(0.62f, 0.16f, 0.17f, 1f);
            image.color = fill;
            Button button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = fill;
            colors.highlightedColor = new Color(0.74f, 0.24f, 0.22f, 1f);
            colors.pressedColor = new Color(0.48f, 0.10f, 0.12f, 1f);
            button.colors = colors;
            button.onClick.AddListener(clicked);
            MakeLabel(rect, "Label", label, 40f, Vector2.zero, Vector2.one,
                new Color(0.98f, 0.93f, 0.78f, 1f), TextAlignmentOptions.Center);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
