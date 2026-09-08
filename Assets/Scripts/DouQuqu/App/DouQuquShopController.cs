using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>百戏市集：金币买虫卵；其余价签仍提示即将开放。</summary>
    public sealed class DouQuquShopController : MonoBehaviour
    {
        private Transform pageRoot;
        private GameObject toastRoot;
        private Text toastLabel;
        private float toastUntil;
        private TMP_Text goldLabel;

        public void BindPage(GameObject root)
        {
            if (root == null) return;
            pageRoot = root.transform;
            DouQuquBottomNavBar.SuppressEmbedded(pageRoot);
            WireHelp(pageRoot);
            WirePriceButtons(pageRoot);
            CacheGoldLabel(pageRoot);
            RefreshGold();
        }

        private void OnEnable()
        {
            DouQuquPlayerDataService.PlayerDataChanged += RefreshGold;
            RefreshGold();
        }

        private void OnDisable()
        {
            DouQuquPlayerDataService.PlayerDataChanged -= RefreshGold;
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
                button.onClick.RemoveAllListeners();
                if (IsEggOffer(button.transform))
                {
                    SetPriceText(button.transform, DouQuquPlayerDataService.EggShopPrice.ToString());
                    button.onClick.AddListener(BuyEggs);
                }
                else
                {
                    button.onClick.AddListener(() => ShowToast("兑换即将开放"));
                }
            }
        }

        private void BuyEggs()
        {
            if (DouQuquPlayerDataService.Eggs >= DouQuquPlayerDataService.EggCap)
            {
                ShowToast("虫卵已满");
                return;
            }
            if (!DouQuquPlayerDataService.TryBuyEggs())
            {
                ShowToast("金币不足");
                return;
            }
            ShowToast("买到虫卵 +" + DouQuquPlayerDataService.EggShopCount);
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
                goldLabel.text = DouQuquPlayerDataService.FormatGold(DouQuquPlayerDataService.Gold);
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
