using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 引导遮罩用独立 Overlay，但 CanvasScaler 从目标根 Canvas 原样拷贝
    ///（主页 match=0 按宽，遮罩也按宽），热区与按钮始终对齐。
    /// 点击走热区转发，不把按钮挪出原层级，避免挡住选虫。
    /// </summary>
    public sealed class TutorialSpotlight : MonoBehaviour
    {
        const int SortingOrder = 400;
        static TutorialSpotlight instance;

        GameObject target;
        RectTransform overlay;
        Image dim;
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
            dim.color = new Color(0.02f, 0.02f, 0.04f, 0.62f);
            dim.raycastTarget = true;
            view.dim = dim;

            GameObject holeGo = new GameObject("Hole", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            holeGo.transform.SetParent(root, false);
            view.hole = holeGo.GetComponent<RectTransform>();
            Image holeImage = holeGo.GetComponent<Image>();
            holeImage.color = new Color(1f, 1f, 1f, 0.01f);
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
            RectTransform root = target.GetComponent<RectTransform>();
            if (root == null) return null;
            Transform label = target.transform.Find("Label");
            if (label != null) return label as RectTransform;
            return root;
        }

        void SyncHole()
        {
            if (hole == null || target == null) return;
            RectTransform source = VisualRect();
            if (source == null) return;

            hole.SetAsLastSibling();
            if (hintLabel != null) hintLabel.transform.SetAsLastSibling();

            Canvas srcCanvas = RootCanvas(target);
            Camera srcCam = CanvasCamera(srcCanvas);
            Camera holeCam = CanvasCamera(overlay != null ? overlay.GetComponent<Canvas>() : null);

            Vector3[] corners = new Vector3[4];
            source.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(srcCam, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(srcCam, corners[2]);
            Vector2 size = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
            if (size.x < 80f) size.x = 80f;
            if (size.y < 80f) size.y = 80f;
            Vector2 center = (min + max) * 0.5f;

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, center, holeCam, out local);
            hole.anchorMin = hole.anchorMax = new Vector2(0.5f, 0.5f);
            hole.pivot = new Vector2(0.5f, 0.5f);
            hole.anchoredPosition = local;
            hole.sizeDelta = size + new Vector2(24f, 24f);

            if (hintLabel != null && hintLabel.gameObject.activeSelf)
            {
                RectTransform hintRect = hintLabel.rectTransform;
                hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0.5f);
                hintRect.pivot = new Vector2(0f, 0.5f);
                hintRect.anchoredPosition = local + new Vector2(size.x * 0.5f + 16f, 0f);
                hintRect.sizeDelta = new Vector2(220f, 48f);
            }
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
