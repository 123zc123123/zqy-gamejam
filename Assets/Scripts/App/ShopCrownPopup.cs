using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>买到皇冠后的弹窗：展示贴图，点「立即装扮」给对战蟋蟀戴上。</summary>
    public sealed class ShopCrownPopup : MonoBehaviour
    {
        public const string CrownSpritePath = "Shop/Textures/crown";
        public const string ReadyPrefabPath = "Common/Prefabs/btn-ready";

        System.Action equipped;

        public static ShopCrownPopup Show(System.Action onEquipped)
        {
            RectTransform overlay = UiFactory.CreateOverlay("ShopCrownPopupCanvas", 290);
            Image dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0.05f, 0.03f, 0.02f, 0.62f);
            dim.raycastTarget = true;

            ShopCrownPopup popup = overlay.gameObject.AddComponent<ShopCrownPopup>();
            popup.equipped = onEquipped;
            popup.Build(overlay, dim);
            UiFonts.ApplyTree(overlay);
            return popup;
        }

        public void Close()
        {
            if (gameObject != null) Destroy(gameObject);
        }

        void Build(RectTransform overlay, Image dim)
        {
            Button dimButton = overlay.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.targetGraphic = dim;
            dimButton.onClick.AddListener(Close);

            GameObject panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelGo.transform.SetParent(overlay, false);
            RectTransform panel = panelGo.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(720f, 860f);
            panel.anchoredPosition = Vector2.zero;
            Image panelImage = panelGo.GetComponent<Image>();
            panelImage.color = new Color(0.97f, 0.93f, 0.82f, 1f);
            panelImage.raycastTarget = true;

            TMP_Text title = MakeLabel(panel, "Title", "皇冠", 52f,
                new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.96f));

            Image portrait = MakeImage(panel, "Crown");
            RectTransform portraitRect = portrait.rectTransform;
            portraitRect.anchorMin = new Vector2(0.5f, 0.5f);
            portraitRect.anchorMax = new Vector2(0.5f, 0.5f);
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.sizeDelta = new Vector2(520f, 320f);
            portraitRect.anchoredPosition = new Vector2(0f, 70f);
            Sprite crown = Resources.Load<Sprite>(CrownSpritePath);
            if (crown != null) portrait.sprite = crown;
            portrait.preserveAspect = true;
            portrait.color = Color.white;
            portrait.raycastTarget = false;

            Button wear = MakeWearButton(panel);
            wear.onClick.AddListener(EquipNow);
            title.raycastTarget = false;
        }

        void EquipNow()
        {
            System.Action callback = equipped;
            equipped = null;
            Close();
            if (callback != null) callback.Invoke();
        }

        static Image MakeImage(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        static TMP_Text MakeLabel(Transform parent, string name, string content, float size,
            Vector2 anchorMin, Vector2 anchorMax)
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
            text.color = new Color(0.28f, 0.16f, 0.10f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }

        static Button MakeWearButton(Transform parent)
        {
            GameObject prefab = Resources.Load<GameObject>(ReadyPrefabPath);
            Sprite blue = Resources.Load<Sprite>("Common/Textures/蓝色bg");
            GameObject go;
            if (prefab != null)
                go = Instantiate(prefab, parent, false);
            else
            {
                go = new GameObject("WearButton", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(parent, false);
                MakeLabel(go.transform, "Label", "立即装扮", 48f, Vector2.zero, Vector2.one);
            }

            go.name = "WearButton";
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(360f, 141f);
            rect.anchoredPosition = new Vector2(0f, -280f);
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
                tmp.text = "立即装扮";
                tmp.color = Color.white;
                tmp.raycastTarget = false;
            }

            Button button = go.GetComponent<Button>();
            if (button == null) button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            if (image != null) button.targetGraphic = image;
            return button;
        }
    }
}
