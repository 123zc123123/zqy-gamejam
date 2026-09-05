using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>为无预制体 Demo 创建统一的手机端 uGUI/TMP 界面。</summary>
    internal static class DouQuquUiFactory
    {
        // 这些界面是在运行时创建的，不能依赖场景里预先拖好的 TMP 字体引用。
        // 优先使用项目/TMP 默认字体；默认字体不存在时从 Windows 中文字体动态生成，
        // 这样中文、英文和数字都能在手机端 Demo 中正常显示。
        private static TMP_FontAsset runtimeFontAsset;
        private static readonly Color BackgroundColor = new Color(0.035f, 0.075f, 0.13f, 1f);
        private static readonly Color PanelColor = new Color(0.07f, 0.14f, 0.22f, 0.96f);
        private static readonly Color AccentColor = new Color(0.30f, 0.76f, 0.95f, 1f);
        private static readonly Color TextColor = new Color(0.92f, 0.97f, 1f, 1f);

        /// <summary>创建适配横屏手机的根 Canvas，并补齐触摸事件系统。</summary>
        public static TMP_FontAsset Font => GetRuntimeFontAsset();

        public static RectTransform CreateScreen(string name)
        {
            EnsureEventSystem();
            GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = canvasObject.GetComponent<RectTransform>();
            Stretch(root);
            Image background = canvasObject.AddComponent<Image>();
            background.color = BackgroundColor;
            return root;
        }

        public static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.SetParent(parent, false);
            SetRect(panel, anchorMin, anchorMax, offsetMin, offsetMax);
            panelObject.GetComponent<Image>().color = PanelColor;
            return panel;
        }

        public static TMP_Text CreateText(Transform parent, string name, string content, float fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = GetRuntimeFontAsset();
            text.text = content;
            text.fontSize = fontSize;
            text.color = TextColor;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Action clicked,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            Image image = buttonObject.GetComponent<Image>();
            image.color = AccentColor;
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = AccentColor;
            colors.highlightedColor = new Color(0.46f, 0.86f, 1f, 1f);
            colors.pressedColor = new Color(0.18f, 0.58f, 0.78f, 1f);
            colors.disabledColor = new Color(0.25f, 0.32f, 0.38f, 0.75f);
            button.colors = colors;
            if (clicked != null) button.onClick.AddListener(() => clicked());
            TMP_Text text = CreateText(rect, "Label", label, 34f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.color = new Color(0.025f, 0.10f, 0.16f, 1f);
            text.fontStyle = FontStyles.Bold;
            return button;
        }

        public static TMP_InputField CreateInput(Transform parent, string name, string placeholder,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject inputObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            RectTransform rect = inputObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            inputObject.GetComponent<Image>().color = new Color(0.88f, 0.94f, 0.97f, 1f);

            RectTransform viewport = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(rect, false);
            SetRect(viewport, Vector2.zero, Vector2.one, new Vector2(24f, 10f), new Vector2(-24f, -10f));
            TMP_Text text = CreateText(viewport, "Text", string.Empty, 34f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineLeft);
            text.color = new Color(0.04f, 0.10f, 0.16f, 1f);
            TMP_Text hint = CreateText(viewport, "Placeholder", placeholder, 32f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineLeft);
            hint.color = new Color(0.28f, 0.38f, 0.45f, 0.75f);
            hint.fontStyle = FontStyles.Italic;

            TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = hint;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 20;
            return input;
        }

        public static Image CreateAvatar(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject avatarObject = new GameObject("PlayerAvatar", typeof(RectTransform), typeof(Image));
            RectTransform rect = avatarObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            Image avatar = avatarObject.GetComponent<Image>();
            avatar.color = new Color(0.94f, 0.63f, 0.22f, 1f);
            CreateText(rect, "AvatarMark", "蟋蟀", 30f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return avatar;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static TMP_FontAsset GetRuntimeFontAsset()
        {
            if (runtimeFontAsset != null) return runtimeFontAsset;

            // 主字体是资源圆体 Medium 烘焙的 DouQuquChinese SDF。动态图集会按新字补字形。
            // TMP 默认 LiberationSans 不含中文，先取它会把中文变成方框。
            runtimeFontAsset = Resources.Load<TMP_FontAsset>("Fonts/DouQuquChinese SDF");
            if (runtimeFontAsset == null)
                runtimeFontAsset = Resources.Load<TMP_FontAsset>("Fonts/NotoSansSC-VF SDF");
            if (runtimeFontAsset == null)
                runtimeFontAsset = TMP_Settings.defaultFontAsset;

            if (runtimeFontAsset == null)
            {
                UnityEngine.Font dynamicFont = UnityEngine.Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimSun", "Arial" }, 64);
                if (dynamicFont != null)
                {
                    runtimeFontAsset = TMP_FontAsset.CreateFontAsset(dynamicFont);
                    // 允许 TMP 在首次遇到中文字符时把字形加入图集。
                    runtimeFontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                    runtimeFontAsset.isMultiAtlasTexturesEnabled = true;
                }
            }

            // 系统字体不可用时，至少保证英文和数字仍有可显示的 TMP 字体。
            if (runtimeFontAsset == null)
                runtimeFontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            return runtimeFontAsset;
        }

        private static void Stretch(RectTransform rect)
        {
            SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }
    }
}
