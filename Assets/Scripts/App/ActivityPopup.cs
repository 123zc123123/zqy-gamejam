using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 通用说明弹层：一份 Prefab，文案按 id 从 JSON 填入。
    /// 主界面活动介绍、进战玩法规则、育虫盘规则都走这里。
    /// </summary>
    public sealed class ActivityPopup : MonoBehaviour
    {
        public const string PrefabPath = "Common/Prefabs/InfoPopup";
        public const string CatalogPath = "Common/Text/info-popups";
        public const string ActivityId = "activity";
        public const string RulesId = "rules";
        public const string MergeId = "merge";
        public const string QuestId = "quest";

        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text body;
        [SerializeField] TMP_Text closeLabel;
        [SerializeField] Button closeButton;
        [SerializeField] Button dimButton;

        static ActivityPopup instance;
        static System.Action pendingClose;
        static Catalog catalog;

        const string DefaultClose = "知道了";

        public static void Show()
        {
            ShowId(ActivityId);
        }

        public static void ShowActivity()
        {
            ShowId(ActivityId);
        }

        public static void ShowRules()
        {
            ShowId(RulesId);
        }

        public static void ShowMerge()
        {
            ShowId(MergeId);
        }

        public static void ShowQuest()
        {
            ShowId(QuestId);
        }

        public static string GetBody(string id)
        {
            Entry entry = LoadCatalog().Find(id);
            return entry == null ? string.Empty : entry.Body;
        }

        public static void ShowId(string id)
        {
            Entry entry = LoadCatalog().Find(id);
            if (entry == null)
            {
                Debug.LogWarning("[DouQuqu] 没有说明文案 " + id);
                return;
            }

            ShowMessage(entry.title, entry.Body, entry.Close);
        }

        public static void ShowMessage(string titleText, string bodyText)
        {
            ShowMessage(titleText, bodyText, DefaultClose, null);
        }

        public static void ShowMessage(string titleText, string bodyText, string closeText)
        {
            ShowMessage(titleText, bodyText, closeText, null);
        }

        public static void ShowMessage(string titleText, string bodyText, System.Action onClose)
        {
            ShowMessage(titleText, bodyText, DefaultClose, onClose);
        }

        public static void ShowMessage(string titleText, string bodyText, string closeText, System.Action onClose)
        {
            pendingClose = onClose;
            ActivityPopup popup = Ensure();
            if (popup == null) return;
            popup.Apply(titleText, bodyText, closeText);
            Canvas canvas = popup.GetComponent<Canvas>();
            if (canvas == null) canvas = popup.GetComponentInParent<Canvas>();
            if (canvas != null) canvas.sortingOrder = onClose != null ? 420 : 250;
            popup.gameObject.SetActive(true);
        }

        public static void Hide()
        {
            if (instance != null) instance.gameObject.SetActive(false);
            System.Action close = pendingClose;
            pendingClose = null;
            if (close != null) close.Invoke();
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

            if (closeLabel == null && closeButton != null)
                closeLabel = closeButton.GetComponentInChildren<TMP_Text>(true);
        }

        void BindButtons()
        {
            Wire(dimButton, Hide);
            Wire(closeButton, Hide);
        }

        void Apply(string titleText, string bodyText, string closeText)
        {
            ResolveRefs();
            if (title != null) title.text = titleText ?? string.Empty;
            if (body != null) body.text = bodyText ?? string.Empty;
            if (closeLabel != null) closeLabel.text = string.IsNullOrEmpty(closeText) ? DefaultClose : closeText;
        }

        static void Wire(Button button, UnityEngine.Events.UnityAction clicked)
        {
            if (button == null) return;
            button.onClick.RemoveListener(clicked);
            button.onClick.AddListener(clicked);
        }

        static ActivityPopup Ensure()
        {
            if (instance != null) return instance;

            GameObject prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab != null)
            {
                GameObject go = Instantiate(prefab);
                go.name = prefab.name;
                instance = go.GetComponent<ActivityPopup>();
                if (instance == null) instance = go.AddComponent<ActivityPopup>();
                return instance;
            }

            instance = CreateRuntime();
            return instance;
        }

        static Catalog LoadCatalog()
        {
            if (catalog != null) return catalog;

            catalog = new Catalog();
            TextAsset asset = Resources.Load<TextAsset>(CatalogPath);
            if (asset != null && !string.IsNullOrEmpty(asset.text))
            {
                CatalogDto dto = JsonUtility.FromJson<CatalogDto>(asset.text);
                catalog.Add(dto);
            }

            if (catalog.Count == 0) catalog.AddFallback();
            return catalog;
        }

        static ActivityPopup CreateRuntime()
        {
            GameObject canvasObject = new GameObject("InfoPopup",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            ActivityPopup popup = canvasObject.AddComponent<ActivityPopup>();
            BuildVisual(canvasObject.transform, popup);
            canvasObject.SetActive(false);
            return popup;
        }

        static void BuildVisual(Transform root, ActivityPopup popup)
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

            TMP_Text title = MakeLabel(panelRect, "Title", string.Empty, 58f,
                new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.94f),
                new Color(0.45f, 0.18f, 0.12f, 1f), TextAlignmentOptions.Center);

            TMP_Text body = MakeLabel(panelRect, "Body", string.Empty, 34f,
                new Vector2(0.10f, 0.24f), new Vector2(0.90f, 0.76f),
                new Color(0.28f, 0.16f, 0.10f, 1f), TextAlignmentOptions.TopLeft);

            Button close = MakeCloseButton(panelRect);
            close.transform.SetAsLastSibling();

            popup.title = title;
            popup.body = body;
            popup.closeButton = close;
            popup.closeLabel = close.GetComponentInChildren<TMP_Text>(true);
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
                go = Instantiate(prefab, parent, false);
            }
            else
            {
                go = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(parent, false);
                MakeLabel(go.transform, "Label", DefaultClose, 56f, Vector2.zero, Vector2.one,
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
                tmp.text = DefaultClose;
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

        sealed class Catalog
        {
            readonly Entry[] buffer = new Entry[8];
            int count;

            public int Count { get { return count; } }

            public void Add(CatalogDto dto)
            {
                if (dto == null || dto.entries == null) return;
                for (int i = 0; i < dto.entries.Length; i++)
                {
                    EntryDto raw = dto.entries[i];
                    if (raw == null || string.IsNullOrEmpty(raw.id)) continue;
                    Add(new Entry(raw.id, raw.title, raw.close, raw.lines));
                }
            }

            public void AddFallback()
            {
                Add(new Entry(ActivityId, "活动介绍", DefaultClose, new[]
                {
                    "一戳一蹦跶 · 斗蛐蛐",
                    "",
                    "把对手撞出圈即胜。",
                    "一局 2 分钟；1:30 末口并全员狂暴。",
                    "",
                    "停稳后才能蓄力；蓄不满松手会取消。",
                    "空中不能转向。护盾只挡一次出圈。"
                }));
                Add(new Entry(RulesId, "玩法规则", DefaultClose, new[]
                {
                    "四人同场，把对手撞出圈即胜。",
                    "一局 2 分钟；1:30 末口并全员狂暴。",
                    "",
                    "停稳后才能蓄力；蓄不满松手会取消。",
                    "空中不能转向。护盾只挡一次出圈。",
                    "",
                    "随机匹配与好友组队各开一房，满员后进入准备。"
                }));
                Add(new Entry(QuestId, "任务说明", DefaultClose, new[]
                {
                    "参与任意对局（好友组队、随机匹配、训练）均局数+1。",
                    "对局2次、4次均可自选神级蛐蛐。"
                }));
            }

            public Entry Find(string id)
            {
                for (int i = 0; i < count; i++)
                {
                    if (buffer[i].id == id) return buffer[i];
                }

                return null;
            }

            void Add(Entry entry)
            {
                if (entry == null || count >= buffer.Length) return;
                for (int i = 0; i < count; i++)
                {
                    if (buffer[i].id == entry.id)
                    {
                        buffer[i] = entry;
                        return;
                    }
                }

                buffer[count++] = entry;
            }
        }

        sealed class Entry
        {
            public readonly string id;
            public readonly string title;
            readonly string close;
            readonly string body;

            public Entry(string id, string title, string close, string[] lines)
            {
                this.id = id;
                this.title = title ?? string.Empty;
                this.close = close;
                body = JoinLines(lines);
            }

            public string Close
            {
                get { return string.IsNullOrEmpty(close) ? DefaultClose : close; }
            }

            public string Body
            {
                get { return body; }
            }

            static string JoinLines(string[] lines)
            {
                if (lines == null || lines.Length == 0) return string.Empty;
                return string.Join("\n", lines);
            }
        }

        [Serializable]
        sealed class CatalogDto
        {
            public EntryDto[] entries;
        }

        [Serializable]
        sealed class EntryDto
        {
            public string id;
            public string title;
            public string close;
            public string[] lines;
        }
    }
}
