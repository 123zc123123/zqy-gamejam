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

        public void Show(string rank, string displayName, string description, Sprite sprite, string subtitle = null, string[] stats = null, bool[] strongStats = null)
        {
            CacheButtons();
            CacheLabels();
            BringToFront();
            gameObject.SetActive(true);
            Write(titleText, "◇ " + (string.IsNullOrEmpty(displayName) ? "促织" : displayName) + " ◇");
            Write(nameText, string.IsNullOrEmpty(subtitle) ? (displayName ?? "") : subtitle);
            Write(rankText, rank ?? "");
            Write(descriptionText, description ?? "");
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
            gameObject.SetActive(false);
            Closed?.Invoke();
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
