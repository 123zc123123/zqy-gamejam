using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>百戏市集：金币买虫卵；左上角皇冠可买；其余价签仍提示即将开放。</summary>
    public sealed class ShopController : MonoBehaviour
    {
        public const string CrownCardName = "parchment-scroll-card-crown";
        public Button EggOfferButton { get; private set; }
        public Button CrownOfferButton { get; private set; }
        private Transform pageRoot;
        private GameObject toastRoot;
        private Text toastLabel;
        private float toastUntil;
        private TMP_Text goldLabel;

        public void BindPage(GameObject root)
        {
            if (root == null) return;
            pageRoot = root.transform;
            BottomNavBar.SuppressEmbedded(pageRoot);
            WireHelp(pageRoot);
            PrepareCrownCard(pageRoot);
            WirePriceButtons(pageRoot);
            WireCrownButton(pageRoot);
            CacheGoldLabel(pageRoot);
            RefreshGold();
        }

        private void OnEnable()
        {
            PlayerDataService.PlayerDataChanged += RefreshGold;
            RefreshGold();
        }

        private void OnDisable()
        {
            PlayerDataService.PlayerDataChanged -= RefreshGold;
        }

        private void Update()
        {
            if (toastRoot == null || !toastRoot.activeSelf) return;
            if (Time.unscaledTime >= toastUntil) toastRoot.SetActive(false);
        }

        private void WireHelp(Transform root)
        {
            Transform help = FindNamed(root, "circle-help");
            if (help == null) return;
            Button button = EnsureButton(help.gameObject);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ShowToast("金币可在对局结算获得"));
        }

        private void WirePriceButtons(Transform root)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || !IsPriceButton(button)) continue;
                if (IsUnderCrownCard(button.transform)) continue;
                button.onClick.RemoveAllListeners();
                if (IsEggOffer(button.transform))
                {
                    EggOfferButton = button;
                    SetPriceText(button.transform, PlayerDataService.EggShopPrice.ToString());
                    button.onClick.AddListener(BuyEggs);
                }
                else
                {
                    button.onClick.AddListener(() => ShowToast("兑换即将开放"));
                }
            }
        }

        void PrepareCrownCard(Transform root)
        {
            Transform card = FindTopLeftParchment(root);
            if (card == null) return;
            card.name = CrownCardName;
            SetCardTitle(card, "皇冠");
            Sprite crown = Resources.Load<Sprite>(ShopCrownPopup.CrownSpritePath);
            if (crown != null) SetCardPortrait(card, crown);
            Button price = FindPriceButton(card);
            if (price != null)
                SetPriceText(price.transform, PlayerDataService.CrownShopPrice.ToString());
        }

        void WireCrownButton(Transform root)
        {
            Transform card = FindNamed(root, CrownCardName);
            if (card == null) card = FindTopLeftParchment(root);
            if (card == null) return;
            Button price = FindPriceButton(card);
            if (price == null) return;
            CrownOfferButton = price;
            price.onClick.RemoveAllListeners();
            price.onClick.AddListener(BuyCrown);
        }

        void BuyCrown()
        {
            if (TutorialDirector.BlocksCrownPurchase) return;
            if (PlayerDataService.CrownOwned)
            {
                ShowCrownPopup();
                return;
            }
            if (!PlayerDataService.TryBuyCrown())
            {
                ShowToast("金币不足");
                return;
            }
            ShowCrownPopup();
        }

        void ShowCrownPopup()
        {
            ShopCrownPopup.Show(() =>
            {
                if (!PlayerDataService.EquipCrown())
                    ShowToast("还没有这件装饰");
            });
        }

        public static Transform FindTopLeftParchment(Transform root)
        {
            if (root == null) return null;
            Transform best = null;
            Vector2 bestPos = Vector2.zero;
            CollectTopLeft(root, ref best, ref bestPos);
            return best;
        }

        static void CollectTopLeft(Transform node, ref Transform best, ref Vector2 bestPos)
        {
            if (node == null) return;
            if (IsParchmentCard(node))
            {
                RectTransform rect = node as RectTransform;
                Vector2 pos = rect != null ? rect.anchoredPosition : Vector2.zero;
                if (best == null
                    || pos.y > bestPos.y + 1f
                    || (Mathf.Abs(pos.y - bestPos.y) <= 1f && pos.x < bestPos.x))
                {
                    best = node;
                    bestPos = pos;
                }
            }
            for (int i = 0; i < node.childCount; i++)
                CollectTopLeft(node.GetChild(i), ref best, ref bestPos);
        }

        static bool IsParchmentCard(Transform node)
        {
            if (node == null) return false;
            string name = node.name;
            return name.IndexOf("parchment-scroll-card", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("ParchmentScrollCard", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsUnderCrownCard(Transform from)
        {
            Transform walk = from;
            int guard = 0;
            while (walk != null && guard++ < 8)
            {
                if (walk.name == CrownCardName) return true;
                walk = walk.parent;
            }
            return false;
        }

        static Button FindPriceButton(Transform card)
        {
            Button[] buttons = card.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (IsPriceButton(buttons[i])) return buttons[i];
            }
            return null;
        }

        static void SetCardTitle(Transform card, string title)
        {
            TMP_Text[] labels = card.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                string text = labels[i].text ?? "";
                if (text.IndexOf("幼虫", System.StringComparison.Ordinal) >= 0
                    || text.IndexOf("虫卵", System.StringComparison.Ordinal) >= 0
                    || labels[i].gameObject.name.IndexOf("幼虫", System.StringComparison.Ordinal) >= 0)
                {
                    labels[i].text = title;
                    return;
                }
            }
            if (labels.Length > 0 && labels[0] != null) labels[0].text = title;
        }

        static void SetCardPortrait(Transform card, Sprite sprite)
        {
            Image[] images = card.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null) continue;
                if (image.gameObject.name == "Rectangle"
                    || (image.transform.parent != null && image.transform.parent.name == "image-frame"))
                {
                    image.sprite = sprite;
                    image.color = Color.white;
                    image.preserveAspect = true;
                    return;
                }
            }
        }

        private void BuyEggs()
        {
            if (PlayerDataService.Eggs >= PlayerDataService.EggCap)
            {
                ShowToast("虫卵已满");
                return;
            }
            if (!PlayerDataService.TryBuyEggs())
            {
                ShowToast("金币不足");
                return;
            }
            ShowToast("买到虫卵 +" + PlayerDataService.EggShopCount);
            TutorialDirector.OnBoughtEggs();
        }

        private void CacheGoldLabel(Transform root)
        {
            Transform gold = FindNamed(root, "GoldDisplay");
            if (gold != null) goldLabel = gold.GetComponentInChildren<TMP_Text>(true);
            if (goldLabel != null) return;
            TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].text.IndexOf(',') >= 0)
                {
                    goldLabel = labels[i];
                    return;
                }
            }
        }

        private void RefreshGold()
        {
            if (goldLabel == null && pageRoot != null) CacheGoldLabel(pageRoot);
            if (goldLabel != null)
                goldLabel.text = PlayerDataService.FormatGold(PlayerDataService.Gold);
        }

        private static bool IsPriceButton(Button button)
        {
            if (button.gameObject.name.IndexOf("price-button", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return button.transform.parent != null &&
                button.transform.parent.name.IndexOf("price-button", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsEggOffer(Transform from)
        {
            Transform walk = from;
            int guard = 0;
            while (walk != null && guard++ < 8)
            {
                TMP_Text[] labels = walk.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < labels.Length; i++)
                {
                    string text = labels[i] != null ? labels[i].text : "";
                    if (text.IndexOf("幼虫", System.StringComparison.Ordinal) >= 0 ||
                        text.IndexOf("虫卵", System.StringComparison.Ordinal) >= 0)
                        return true;
                }
                if (walk.name.IndexOf("Parchment", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    break;
                walk = walk.parent;
            }
            return false;
        }

        private static void SetPriceText(Transform from, string price)
        {
            TMP_Text[] labels = from.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                string text = labels[i].text ?? "";
                int n;
                if (int.TryParse(text.Replace(",", ""), out n) || labels[i].name.IndexOf("38888", System.StringComparison.Ordinal) >= 0)
                    labels[i].text = price;
            }
        }

        private static Button EnsureButton(GameObject go)
        {
            Button button = go.GetComponent<Button>();
            if (button == null) button = go.AddComponent<Button>();
            Image image = go.GetComponent<Image>();
            if (image == null) image = go.GetComponentInChildren<Image>(true);
            if (image != null)
            {
                image.raycastTarget = true;
                button.targetGraphic = image;
            }
            return button;
        }

        private void ShowToast(string message)
        {
            EnsureToast();
            toastLabel.text = message;
            toastRoot.SetActive(true);
            toastUntil = Time.unscaledTime + 1.6f;
        }

        private void EnsureToast()
        {
            if (toastRoot != null) return;
            Canvas canvas = pageRoot != null ? pageRoot.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : (pageRoot != null ? pageRoot : transform);

            toastRoot = new GameObject("ShopToast", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            toastRoot.transform.SetParent(parent, false);
            RectTransform rootRect = toastRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            Image dim = toastRoot.GetComponent<Image>();
            dim.color = new Color(0.05f, 0.03f, 0.02f, 0.35f);
            Button dimButton = toastRoot.GetComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(() => toastRoot.SetActive(false));

            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(toastRoot.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 180f);
            panel.GetComponent<Image>().color = new Color(0.97f, 0.93f, 0.82f, 1f);
            panel.GetComponent<Image>().raycastTarget = false;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(panel.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(24f, 16f);
            labelRect.offsetMax = new Vector2(-24f, -16f);
            toastLabel = labelObject.GetComponent<Text>();
            toastLabel.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 36);
            toastLabel.fontSize = 40;
            toastLabel.alignment = TextAnchor.MiddleCenter;
            toastLabel.color = new Color(0.28f, 0.16f, 0.10f, 1f);
            toastLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            toastLabel.verticalOverflow = VerticalWrapMode.Overflow;
            toastLabel.raycastTarget = false;
            toastRoot.SetActive(false);
        }

        private static Transform FindNamed(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamed(root.GetChild(i), objectName);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
