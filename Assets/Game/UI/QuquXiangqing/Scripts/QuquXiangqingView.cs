using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZqyGameJam.UI.QuquXiangqing
{
    /// <summary>蛐蛐详情弹层。育虫盘、背包点虫子时打开。</summary>
    public sealed class QuquXiangqingView : MonoBehaviour
    {
        public Button sellButton;
        public Button storeButton;
        public Button closeButton;
        public TMP_Text titleText;
        public TMP_Text nameText;
        public TMP_Text rankText;
        public TMP_Text descriptionText;
        public Image portrait;
        public TMP_Text[] statValues;

        public event System.Action Closed;
        public event System.Action Confirmed;
        public event System.Action Sold;

        private Vector2 sellHome;
        private Vector2 storeHome;
        private Vector2 closeHome;
        private bool actionHomesCaptured;

        public const int OverlaySortingOrder = 400;
        public const string PrefabResourcePath = "Collection/Prefabs/CricketDetail";
        private const string DimmerName = "Dimmer";
        private static readonly Color DimmerColor = new Color(0.05f, 0.03f, 0.02f, 0.65f);

        /// <summary>实例化详情覆盖层，盖在大厅底栏之上。</summary>
        public static QuquXiangqingView InstantiateOverlay(GameObject prefab)
        {
            if (prefab == null) return null;
            GameObject instance = Instantiate(prefab);
            instance.name = "蛐蛐详情";
            instance.transform.localScale = Vector3.one;
            Canvas[] canvases = instance.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = OverlaySortingOrder + i;
                canvas.enabled = true;
                RectTransform rect = canvas.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.localScale = Vector3.one;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 1f;
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            QuquXiangqingView view = instance.GetComponent<QuquXiangqingView>();
            if (view == null) view = instance.GetComponentInChildren<QuquXiangqingView>(true);
            if (view == null) view = instance.AddComponent<QuquXiangqingView>();
            view.Closed += () => instance.SetActive(false);
            return view;
        }

        private void Awake()
        {
            EnsureDimmer();
            EnsureHelpUi();
            CacheButtons();
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
                closeButton.onClick.AddListener(Hide);
            }
            if (sellButton != null)
            {
                sellButton.onClick.RemoveAllListeners();
                sellButton.onClick.AddListener(OnSellClicked);
            }
            if (storeButton != null)
            {
                storeButton.onClick.RemoveAllListeners();
                storeButton.onClick.AddListener(OnStoreClicked);
            }
            CacheLabels();
        }

        /// <summary>匹配页入槽：隐藏出售，确认钮改成「确定」。</summary>
        public void SetPickMode(bool matchPick)
        {
            CacheButtons();
            if (sellButton != null) sellButton.gameObject.SetActive(!matchPick);
            if (storeButton != null) storeButton.gameObject.SetActive(true);
            TMP_Text storeLabel = storeButton != null ? storeButton.GetComponentInChildren<TMP_Text>(true) : null;
            if (storeLabel != null) storeLabel.text = matchPick ? "确定" : "收入背包";
            RelayoutActions();
        }

        /// <summary>图鉴只看不操作：隐藏出售和收入背包。</summary>
        public void SetCatalogMode()
        {
            CacheButtons();
            if (sellButton != null) sellButton.gameObject.SetActive(false);
            if (storeButton != null) storeButton.gameObject.SetActive(false);
            RelayoutActions();
        }

        /// <summary>合成盘背包里点开：保留出售，不显示收入背包。</summary>
        public void SetBackpackMode()
        {
            CacheButtons();
            if (sellButton != null) sellButton.gameObject.SetActive(true);
            if (storeButton != null) storeButton.gameObject.SetActive(false);
            RelayoutActions();
        }

        public void SetSellPrice(int gold)
        {
            CacheButtons();
            if (sellButton == null) return;
            TMP_Text label = sellButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "售卖 " + gold;
        }

        private void OnStoreClicked()
        {
            if (Confirmed != null) Confirmed.Invoke();
            else Debug.Log("蛐蛐详情：收入背包暂未接入");
        }

        private void OnSellClicked()
        {
            if (Sold != null) Sold.Invoke();
            else Debug.Log("蛐蛐详情：出售暂未接入");
        }

        static readonly Color StatNormal = new Color(0.176471f, 0.352941f, 0.152941f, 1f);
        static readonly Color StatStrong = new Color(0.619608f, 0.164706f, 0.168627f, 1f);
        private Color descriptionHome = Color.white;
        private bool descriptionHomeCaptured;

        public void Show(string rank, string displayName, string description, Sprite sprite, string subtitle = null, string[] stats = null, bool[] strongStats = null, Color? descriptionColor = null)
        {
            EnsureDimmer();
            EnsureHelpUi();
            CacheButtons();
            CacheLabels();
            BringToFront();
            gameObject.SetActive(true);
            Write(titleText, "◇ " + (string.IsNullOrEmpty(displayName) ? "促织" : displayName) + " ◇");
            Write(nameText, string.IsNullOrEmpty(subtitle) ? (displayName ?? "") : subtitle);
            Write(rankText, rank ?? "");
            Write(descriptionText, description ?? "");
            if (descriptionText != null)
            {
                if (!descriptionHomeCaptured)
                {
                    descriptionHome = descriptionText.color;
                    descriptionHomeCaptured = true;
                }
                descriptionText.color = descriptionColor ?? descriptionHome;
            }
            if (portrait != null)
            {
                portrait.sprite = sprite;
                portrait.enabled = sprite != null;
                portrait.preserveAspect = true;
            }
            WriteStats(stats, strongStats);
            RelayoutActions();
        }

        private void RelayoutActions()
        {
            CaptureActionHomes();
            SetAnchored(sellButton, sellHome);
            SetAnchored(storeButton, storeHome);
            SetAnchored(closeButton, closeHome);
            bool sellOn = sellButton != null && sellButton.gameObject.activeSelf;
            bool storeOn = storeButton != null && storeButton.gameObject.activeSelf;
            bool closeOn = closeButton != null && closeButton.gameObject.activeSelf;
            if (sellOn && !storeOn && closeOn)
                SetAnchored(closeButton, storeHome);
        }

        private void CaptureActionHomes()
        {
            if (actionHomesCaptured) return;
            sellHome = ReadAnchored(sellButton);
            storeHome = ReadAnchored(storeButton);
            closeHome = ReadAnchored(closeButton);
            actionHomesCaptured = sellButton != null || storeButton != null || closeButton != null;
        }

        private static Vector2 ReadAnchored(Button button)
        {
            if (button == null) return Vector2.zero;
            RectTransform rect = button.transform as RectTransform;
            return rect != null ? rect.anchoredPosition : Vector2.zero;
        }

        private static void SetAnchored(Button button, Vector2 position)
        {
            if (button == null) return;
            RectTransform rect = button.transform as RectTransform;
            if (rect != null) rect.anchoredPosition = position;
        }

        public void Hide()
        {
            HideHelp();
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        /// <summary>详情卡外铺一层遮黑；点遮黑关弹窗，点卡片本身不关。</summary>
        private void EnsureDimmer()
        {
            Transform existing = transform.Find(DimmerName);
            GameObject dim = existing != null ? existing.gameObject : null;
            if (dim == null)
            {
                dim = new GameObject(DimmerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                RectTransform dimRect = dim.GetComponent<RectTransform>();
                dimRect.SetParent(transform, false);
                dimRect.SetAsFirstSibling();
                dimRect.anchorMin = Vector2.zero;
                dimRect.anchorMax = Vector2.one;
                dimRect.pivot = new Vector2(0.5f, 0.5f);
                dimRect.offsetMin = Vector2.zero;
                dimRect.offsetMax = Vector2.zero;
                dimRect.localScale = Vector3.one;
            }
            else dim.transform.SetAsFirstSibling();

            Image dimImage = dim.GetComponent<Image>();
            dimImage.color = DimmerColor;
            dimImage.raycastTarget = true;

            Button dimButton = dim.GetComponent<Button>();
            if (dimButton == null) dimButton = dim.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.targetGraphic = dimImage;
            dimButton.onClick.RemoveListener(Hide);
            dimButton.onClick.AddListener(Hide);

            Transform surface = transform.Find("PageSurface");
            if (surface == null) return;
            Image[] images = surface.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null) continue;
                images[i].raycastTarget = true;
            }
        }

        public void BringToFront()
        {
            Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].overrideSorting = true;
                canvases[i].sortingOrder = OverlaySortingOrder + i;
            }
        }

        private static void Write(TMP_Text label, string value)
        {
            if (label == null) return;
            label.text = value;
            label.ForceMeshUpdate();
        }

        private void WriteStats(string[] stats, bool[] strongStats)
        {
            if (statValues == null) return;
            for (int i = 0; i < statValues.Length; i++)
            {
                TMP_Text label = statValues[i];
                string value = stats != null && i < stats.Length && !string.IsNullOrEmpty(stats[i])
                    ? stats[i]
                    : "—";
                bool strong = strongStats != null && i < strongStats.Length && strongStats[i] && value != "—";
                Write(label, value);
                if (label == null) continue;
                label.fontStyle = strong ? FontStyles.Bold : FontStyles.Normal;
                label.color = strong ? StatStrong : StatNormal;
            }
        }

        private void CacheButtons()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null) continue;
                string hint = ButtonHint(button);
                if (sellButton == null && (hint.IndexOf("售卖", System.StringComparison.Ordinal) >= 0 || hint.IndexOf("出售", System.StringComparison.Ordinal) >= 0))
                    sellButton = button;
                else if (storeButton == null && (hint.IndexOf("收入背包", System.StringComparison.Ordinal) >= 0 || hint.IndexOf("确定", System.StringComparison.Ordinal) >= 0 || hint.IndexOf("store", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    storeButton = button;
                else if (closeButton == null && (hint.IndexOf("关闭", System.StringComparison.Ordinal) >= 0 || hint.IndexOf("close", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    closeButton = button;
            }
        }

        private static string ButtonHint(Button button)
        {
            if (button == null) return string.Empty;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            string text = label != null ? label.text ?? string.Empty : string.Empty;
            return button.name + " " + text;
        }

        private void CacheLabels()
        {
            TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text label = labels[i];
                if (label == null) continue;
                string objectName = label.name;
                string text = label.text ?? "";
                if (titleText == null && (objectName == "TitleText" || text.IndexOf("促织", System.StringComparison.Ordinal) >= 0))
                    titleText = label;
                else if (nameText == null && (objectName == "NameText" || text.IndexOf("正紫龟", System.StringComparison.Ordinal) >= 0))
                    nameText = label;
                else if (rankText == null && (objectName == "RankText" || text.IndexOf("领军将", System.StringComparison.Ordinal) >= 0))
                    rankText = label;
                else if (descriptionText == null && (objectName == "DescriptionText" || text.IndexOf("龟形", System.StringComparison.Ordinal) >= 0))
                    descriptionText = label;
            }

            if (statValues == null || statValues.Length < 6)
                statValues = new TMP_Text[6];
            int filled = 0;
            for (int i = 0; i < labels.Length && filled < statValues.Length; i++)
            {
                TMP_Text label = labels[i];
                if (label == null || label.name != "Value") continue;
                statValues[filled++] = label;
            }
            if (filled == 0)
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    TMP_Text label = labels[i];
                    if (label == null) continue;
                    int index = StatValueIndex(label.name);
                    if (index >= 0 && index < statValues.Length) statValues[index] = label;
                }
            }

            if (portrait == null)
            {
                string[] portraitNames = { "Portrait", "CricketPortrait", "violet-cricket-illustration", "InsectPortraitArea" };
                for (int i = 0; i < portraitNames.Length && portrait == null; i++)
                {
                    Transform found = FindDeep(transform, portraitNames[i]);
                    if (found == null) continue;
                    portrait = found.GetComponent<Image>();
                    if (portrait == null) portrait = found.GetComponentInChildren<Image>(true);
                }
            }
        }

        private static int StatValueIndex(string objectName)
        {
            if (string.IsNullOrEmpty(objectName) || objectName.IndexOf("_Value", System.StringComparison.Ordinal) < 0)
                return -1;
            if (objectName.IndexOf("Stat01", System.StringComparison.Ordinal) >= 0) return 0;
            if (objectName.IndexOf("Stat02", System.StringComparison.Ordinal) >= 0) return 1;
            if (objectName.IndexOf("Stat03", System.StringComparison.Ordinal) >= 0) return 2;
            if (objectName.IndexOf("Stat04", System.StringComparison.Ordinal) >= 0) return 3;
            if (objectName.IndexOf("Stat05", System.StringComparison.Ordinal) >= 0) return 4;
            if (objectName.IndexOf("Stat06", System.StringComparison.Ordinal) >= 0) return 5;
            return -1;
        }

        const string HelpButtonName = "StatHelpButton";
        const string HelpPopupName = "StatHelpPopup";
        static readonly Color HelpInk = new Color(0.16f, 0.11f, 0.07f, 1f);
        static readonly Color HelpMuted = new Color(0.40f, 0.32f, 0.24f, 1f);
        static readonly Color HelpPaper = new Color(0.98f, 0.95f, 0.88f, 1f);
        static readonly Color HelpAccent = new Color(0.52f, 0.30f, 0.14f, 1f);
        static readonly string[] HelpNames =
        {
            "重量", "抓地力", "蓄力速度", "蓄力时间上限", "耐力恢复速度", "耐力上限"
        };
        static readonly string[] HelpDescs =
        {
            "影响初始的体积与碰撞力量",
            "影响受撞击后的移动距离与时间",
            "影响相同时间蓄力的进度",
            "影响蓄力的最大时长即跳跃的最远距离",
            "影响落地状态的耐力恢复速度",
            "影响最大耐力值"
        };

        GameObject helpPopup;

        void EnsureHelpUi()
        {
            EnsureHelpButton();
            EnsureHelpPopup();
        }

        void EnsureHelpButton()
        {
            Transform page = FindDeep(transform, "PageSurface");
            Transform existing = FindDeep(transform, HelpButtonName);
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject(HelpButtonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(page != null ? page : transform, false);
            }

            Transform label = go.transform.Find("Label");
            if (label != null) Destroy(label.gameObject);

            RectTransform rect = go.GetComponent<RectTransform>();
            if (page != null && go.transform.parent != page) go.transform.SetParent(page, false);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-28f, -28f);
            rect.sizeDelta = new Vector2(72f, 72f);
            rect.localScale = Vector3.one;

            Image image = go.GetComponent<Image>();
            if (image == null) image = go.AddComponent<Image>();
            Sprite icon = Resources.Load<Sprite>("Collection/Textures/StatHelpIcon");
            image.sprite = icon;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = true;

            Button button = go.GetComponent<Button>();
            if (button == null) button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;
            button.onClick.RemoveListener(ShowHelp);
            button.onClick.AddListener(ShowHelp);
        }

        void EnsureHelpPopup()
        {
            Transform existing = transform.Find(HelpPopupName);
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            helpPopup = new GameObject(HelpPopupName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image), typeof(Button));
            RectTransform root = helpPopup.GetComponent<RectTransform>();
            root.SetParent(transform, false);
            root.SetAsLastSibling();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.localScale = Vector3.one;
            Canvas canvas = helpPopup.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = OverlaySortingOrder + 80;
            canvas.pixelPerfect = true;
            CanvasScaler scaler = helpPopup.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            Image dim = helpPopup.GetComponent<Image>();
            dim.color = new Color(0.04f, 0.03f, 0.02f, 0.58f);
            dim.raycastTarget = true;
            Button dimButton = helpPopup.GetComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.targetGraphic = dim;
            dimButton.onClick.AddListener(HideHelp);

            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.SetParent(root, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(860f, 1080f);
            panelRect.anchoredPosition = Vector2.zero;
            Image paper = panel.GetComponent<Image>();
            paper.color = HelpPaper;
            paper.raycastTarget = true;

            TMP_Text title = CreateTmp(panelRect, "Title", "六维介绍", 42f, HelpInk);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -40f);
            titleRect.sizeDelta = new Vector2(740f, 56f);
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;

            GameObject line = new GameObject("Rule", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform lineRect = line.GetComponent<RectTransform>();
            lineRect.SetParent(panelRect, false);
            lineRect.anchorMin = new Vector2(0.5f, 1f);
            lineRect.anchorMax = new Vector2(0.5f, 1f);
            lineRect.pivot = new Vector2(0.5f, 1f);
            lineRect.anchoredPosition = new Vector2(0f, -108f);
            lineRect.sizeDelta = new Vector2(680f, 2f);
            line.GetComponent<Image>().color = new Color(HelpAccent.r, HelpAccent.g, HelpAccent.b, 0.35f);
            line.GetComponent<Image>().raycastTarget = false;

            float rowH = 128f;
            float startY = -140f;
            for (int i = 0; i < HelpNames.Length; i++)
            {
                GameObject row = new GameObject("Row" + i, typeof(RectTransform));
                RectTransform rowRect = row.GetComponent<RectTransform>();
                rowRect.SetParent(panelRect, false);
                rowRect.anchorMin = new Vector2(0.5f, 1f);
                rowRect.anchorMax = new Vector2(0.5f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, startY - i * rowH);
                rowRect.sizeDelta = new Vector2(720f, rowH - 12f);

                TMP_Text name = CreateTmp(rowRect, "Name", HelpNames[i], 32f, HelpAccent);
                RectTransform nameRect = name.rectTransform;
                nameRect.anchorMin = new Vector2(0f, 1f);
                nameRect.anchorMax = new Vector2(1f, 1f);
                nameRect.pivot = new Vector2(0.5f, 1f);
                nameRect.anchoredPosition = Vector2.zero;
                nameRect.sizeDelta = new Vector2(0f, 40f);
                name.alignment = TextAlignmentOptions.Left;
                name.fontStyle = FontStyles.Bold;

                TMP_Text desc = CreateTmp(rowRect, "Desc", HelpDescs[i], 28f, HelpMuted);
                RectTransform descRect = desc.rectTransform;
                descRect.anchorMin = new Vector2(0f, 0f);
                descRect.anchorMax = new Vector2(1f, 1f);
                descRect.offsetMin = new Vector2(0f, 4f);
                descRect.offsetMax = new Vector2(0f, -42f);
                desc.alignment = TextAlignmentOptions.TopLeft;
                desc.enableWordWrapping = true;
                desc.lineSpacing = 8f;
            }

            GameObject ok = new GameObject("Ok", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform okRect = ok.GetComponent<RectTransform>();
            okRect.SetParent(panelRect, false);
            okRect.anchorMin = new Vector2(0.5f, 0f);
            okRect.anchorMax = new Vector2(0.5f, 0f);
            okRect.pivot = new Vector2(0.5f, 0f);
            okRect.anchoredPosition = new Vector2(0f, 40f);
            okRect.sizeDelta = new Vector2(280f, 72f);
            Image okImage = ok.GetComponent<Image>();
            okImage.color = HelpAccent;
            Button okButton = ok.GetComponent<Button>();
            okButton.targetGraphic = okImage;
            okButton.onClick.AddListener(HideHelp);
            TMP_Text okLabel = CreateTmp(okRect, "Label", "知道了", 32f, Color.white);
            RectTransform okLabelRect = okLabel.rectTransform;
            okLabelRect.anchorMin = Vector2.zero;
            okLabelRect.anchorMax = Vector2.one;
            okLabelRect.offsetMin = Vector2.zero;
            okLabelRect.offsetMax = Vector2.zero;
            okLabel.alignment = TextAlignmentOptions.Center;
            okLabel.raycastTarget = false;

            helpPopup.SetActive(false);
        }

        void ShowHelp()
        {
            EnsureHelpPopup();
            if (helpPopup == null) return;
            helpPopup.transform.SetAsLastSibling();
            helpPopup.SetActive(true);
        }

        void HideHelp()
        {
            if (helpPopup != null) helpPopup.SetActive(false);
        }

        static TMP_Text CreateTmp(Transform parent, string objectName, string text, float size, Color color)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/Chinese SDF");
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.raycastTarget = false;
            label.extraPadding = true;
            label.enableAutoSizing = false;
            label.overflowMode = TextOverflowModes.Overflow;
            if (label.fontSharedMaterial != null)
            {
                label.fontMaterial = new Material(label.fontSharedMaterial);
                label.fontMaterial.SetFloat("_OutlineWidth", 0f);
                label.fontMaterial.SetFloat("_OutlineSoftness", 0f);
                label.fontMaterial.SetFloat("_FaceDilate", 0f);
            }
            return label;
        }

        private static Transform FindDeep(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDeep(root.GetChild(i), objectName);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
