using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 引导遮罩：四周压暗、中间镂空露出原按钮，只在洞口画一圈描边。
    /// CanvasScaler 从目标根 Canvas 原样拷贝，热区与按钮对齐。
    /// 点击走热区转发，不把按钮挪出原层级。
    /// </summary>
    public sealed class TutorialSpotlight : MonoBehaviour
    {
        const int SortingOrder = 400;
        static TutorialSpotlight instance;

        const float BorderThickness = 5f;
        static readonly Color BorderColor = new Color(1f, 0.86f, 0.28f, 1f);

        GameObject target;
        RectTransform overlay;
        Image dim;
        RectTransform[] shades;
        RectTransform[] borders;
        Image[] borderImages;
        RectTransform hole;
        Button holeButton;
        TMP_Text hintLabel;

        public static void Show(GameObject targetButton, string hint)
        {
            if (targetButton == null)
            {
                Hide();
                return;
            }

            if (instance != null && instance.overlay != null && instance.overlay.Find("Frame") != null)
            {
                Object.Destroy(instance.overlay.gameObject);
                instance = null;
            }
            if (instance == null) instance = Create();
            instance.Attach(targetButton, hint);
        }

        public static void Hide()
        {
            if (instance != null)
            {
                instance.target = null;
                if (instance.overlay != null) instance.overlay.gameObject.SetActive(false);
            }
            DestroyStrays();
        }

        static void DestroyStrays()
        {
            DestroyNamed("TutorialDim");
            DestroyNamed("TutorialHint");
        }

        static void DestroyNamed(string objectName)
        {
            GameObject go = GameObject.Find(objectName);
            if (go != null) Object.Destroy(go);
        }

        static TutorialSpotlight Create()
        {
            RectTransform root = UiFactory.CreateOverlay("TutorialSpotlightCanvas", SortingOrder);
            Object.DontDestroyOnLoad(root.gameObject);
            TutorialSpotlight view = root.gameObject.AddComponent<TutorialSpotlight>();
            view.overlay = root;

            Image dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0f);
            dim.raycastTarget = false;
            view.dim = dim;

            view.shades = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject shadeGo = new GameObject("Shade" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                shadeGo.transform.SetParent(root, false);
                view.shades[i] = shadeGo.GetComponent<RectTransform>();
                Image shadeImage = shadeGo.GetComponent<Image>();
                shadeImage.color = new Color(0.02f, 0.02f, 0.04f, 0.62f);
                shadeImage.raycastTarget = true;
            }

            view.borders = new RectTransform[4];
            view.borderImages = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject borderGo = new GameObject("Border" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                borderGo.transform.SetParent(root, false);
                view.borders[i] = borderGo.GetComponent<RectTransform>();
                Image borderImage = borderGo.GetComponent<Image>();
                borderImage.color = BorderColor;
                borderImage.raycastTarget = false;
                view.borderImages[i] = borderImage;
            }

            GameObject holeGo = new GameObject("Hole", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            holeGo.transform.SetParent(root, false);
            view.hole = holeGo.GetComponent<RectTransform>();
            Image holeImage = holeGo.GetComponent<Image>();
            holeImage.color = new Color(1f, 1f, 1f, 0f);
            holeImage.raycastTarget = true;
            view.holeButton = holeGo.GetComponent<Button>();
            view.holeButton.transition = Selectable.Transition.None;
            view.holeButton.onClick.AddListener(view.OnHoleClicked);

            view.hintLabel = UiFactory.CreateText(root, "Hint", string.Empty, 36f,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(220f, 48f),
                TextAlignmentOptions.MidlineLeft);
            view.hintLabel.color = new Color(0.96f, 0.86f, 0.45f, 1f);
            view.hintLabel.raycastTarget = false;
            UiFonts.Apply(view.hintLabel);
            return view;
        }

        void Attach(GameObject targetButton, string hint)
        {
            target = targetButton;
            SyncScalerFrom(RootCanvas(targetButton));
            overlay.gameObject.SetActive(true);
            hintLabel.text = string.IsNullOrEmpty(hint) ? string.Empty : hint;
            hintLabel.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            SyncHole();
        }

        void LateUpdate()
        {
            if (target == null || !target.activeInHierarchy)
            {
                if (overlay != null && overlay.gameObject.activeSelf)
                    overlay.gameObject.SetActive(false);
                target = null;
                return;
            }
            SyncHole();
            PulseBorder();
        }

        void SyncScalerFrom(Canvas source)
        {
            if (source == null || overlay == null) return;
            CanvasScaler from = source.GetComponent<CanvasScaler>();
            CanvasScaler to = overlay.GetComponent<CanvasScaler>();
            if (from == null || to == null) return;
            to.uiScaleMode = from.uiScaleMode;
            to.referenceResolution = from.referenceResolution;
            to.screenMatchMode = from.screenMatchMode;
            to.matchWidthOrHeight = from.matchWidthOrHeight;
            to.referencePixelsPerUnit = from.referencePixelsPerUnit;
        }

        static Canvas RootCanvas(GameObject go)
        {
            if (go == null) return null;
            Canvas canvas = go.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.rootCanvas : null;
        }

        static Camera CanvasCamera(Canvas canvas)
        {
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return canvas.worldCamera;
        }

        RectTransform VisualRect()
        {
            if (target == null) return null;
            return target.GetComponent<RectTransform>();
        }

        void SyncHole()
        {
            if (hole == null || target == null) return;
            RectTransform source = VisualRect();
            if (source == null) return;

            Canvas srcCanvas = RootCanvas(target);
            Camera srcCam = CanvasCamera(srcCanvas);
            Camera holeCam = CanvasCamera(overlay != null ? overlay.GetComponent<Canvas>() : null);

            Vector3[] corners = new Vector3[4];
            source.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(srcCam, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(srcCam, corners[2]);
            Vector2 size = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
            if (size.x < 44f) size.x = 44f;
            if (size.y < 44f) size.y = 44f;
            Vector2 center = (min + max) * 0.5f;

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, center, holeCam, out local);
            Vector2 holeSize = size + new Vector2(12f, 12f);
            PlaceRect(hole, local, holeSize);
            LayoutBorders(local, holeSize);
            LayoutShades(local, holeSize);

            hole.SetAsLastSibling();
            if (hintLabel != null) hintLabel.transform.SetAsLastSibling();

            if (hintLabel != null && hintLabel.gameObject.activeSelf)
            {
                RectTransform hintRect = hintLabel.rectTransform;
                hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0.5f);
                hintRect.pivot = new Vector2(0f, 0.5f);
                hintRect.anchoredPosition = local + new Vector2(holeSize.x * 0.5f + 18f, 0f);
                hintRect.sizeDelta = new Vector2(260f, 48f);
            }
        }

        void LayoutBorders(Vector2 holeCenter, Vector2 holeSize)
        {
            if (borders == null) return;
            float t = BorderThickness;
            float hl = holeCenter.x - holeSize.x * 0.5f;
            float hr = holeCenter.x + holeSize.x * 0.5f;
            float hb = holeCenter.y - holeSize.y * 0.5f;
            float ht = holeCenter.y + holeSize.y * 0.5f;
            PlaceRect(borders[0], new Vector2(holeCenter.x, ht + t * 0.5f), new Vector2(holeSize.x + t * 2f, t));
            PlaceRect(borders[1], new Vector2(holeCenter.x, hb - t * 0.5f), new Vector2(holeSize.x + t * 2f, t));
            PlaceRect(borders[2], new Vector2(hl - t * 0.5f, holeCenter.y), new Vector2(t, holeSize.y));
            PlaceRect(borders[3], new Vector2(hr + t * 0.5f, holeCenter.y), new Vector2(t, holeSize.y));
        }

        void PulseBorder()
        {
            if (borderImages == null) return;
            float pulse = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2.4f));
            Color color = new Color(BorderColor.r, BorderColor.g, BorderColor.b, pulse);
            for (int i = 0; i < borderImages.Length; i++)
            {
                if (borderImages[i] != null) borderImages[i].color = color;
            }
        }

        void LayoutShades(Vector2 holeCenter, Vector2 holeSize)
        {
            if (shades == null || overlay == null) return;
            Rect area = overlay.rect;
            float left = -area.width * 0.5f;
            float right = area.width * 0.5f;
            float bottom = -area.height * 0.5f;
            float top = area.height * 0.5f;
            float hl = holeCenter.x - holeSize.x * 0.5f;
            float hr = holeCenter.x + holeSize.x * 0.5f;
            float hb = holeCenter.y - holeSize.y * 0.5f;
            float ht = holeCenter.y + holeSize.y * 0.5f;
            PlaceShade(0, Mid(left, right), Mid(ht, top), right - left, Mathf.Max(0f, top - ht));
            PlaceShade(1, Mid(left, right), Mid(bottom, hb), right - left, Mathf.Max(0f, hb - bottom));
            PlaceShade(2, Mid(left, hl), Mid(hb, ht), Mathf.Max(0f, hl - left), Mathf.Max(0f, ht - hb));
            PlaceShade(3, Mid(hr, right), Mid(hb, ht), Mathf.Max(0f, right - hr), Mathf.Max(0f, ht - hb));
        }

        void PlaceShade(int index, float x, float y, float w, float h)
        {
            if (shades == null || index < 0 || index >= shades.Length || shades[index] == null) return;
            PlaceRect(shades[index], new Vector2(x, y), new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h)));
        }

        static void PlaceRect(RectTransform rect, Vector2 pos, Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        static float Mid(float a, float b)
        {
            return (a + b) * 0.5f;
        }

        void OnHoleClicked()
        {
            if (target == null) return;
            Button button = target.GetComponent<Button>();
            if (button == null) button = target.GetComponentInChildren<Button>(true);
            if (button == null) button = target.GetComponentInParent<Button>();
            if (button != null && button.interactable) button.onClick.Invoke();
        }
    }
}
