using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>百戏市集：绑 Shop Prefab。兑换经济未接，按钮提示即将开放。</summary>
    public sealed class DouQuquShopController : MonoBehaviour
    {
        private Transform pageRoot;
        private GameObject toastRoot;
        private Text toastLabel;
        private float toastUntil;

        public void BindPage(GameObject root)
        {
            if (root == null) return;
            pageRoot = root.transform;
            DouQuquBottomNavBar.SuppressEmbedded(pageRoot);
            WireHelp(pageRoot);
            WirePriceButtons(pageRoot);
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
            button.onClick.AddListener(() => ShowToast("脸谱币可在活动中获得"));
        }

        private void WirePriceButtons(Transform root)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null) continue;
                if (button.gameObject.name.IndexOf("price-button", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    button.transform.parent != null &&
                    button.transform.parent.name.IndexOf("price-button", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => ShowToast("兑换即将开放"));
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
