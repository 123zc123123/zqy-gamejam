using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 新手遮罩画在目标所在的根 Canvas 上（共用同一套 CanvasScaler），
    /// 再把目标按钮提到遮罩之后，适配拉长也不会点不中。
    /// </summary>
    public sealed class TutorialSpotlight : MonoBehaviour
    {
        static TutorialSpotlight instance;

        GameObject target;
        Transform originalParent;
        int originalSibling;
        Vector3 originalScale;
        Quaternion originalRotation;
        Image dim;
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
            if (instance == null) return;
            instance.Restore();
        }

        static TutorialSpotlight Create()
        {
            GameObject host = new GameObject("TutorialSpotlightHost");
            TutorialSpotlight view = host.AddComponent<TutorialSpotlight>();
            Object.DontDestroyOnLoad(host);
            return view;
        }

        void Attach(GameObject targetButton, string hint)
        {
            Restore();
            Canvas canvas = RootCanvas(targetButton);
            if (canvas == null)
            {
                Debug.LogWarning("[DouQuqu] 引导遮罩找不到目标 Canvas");
                return;
            }

            target = targetButton;
            originalParent = target.transform.parent;
            originalSibling = target.transform.GetSiblingIndex();
            originalScale = target.transform.localScale;
            originalRotation = target.transform.localRotation;

            RectTransform canvasRect = canvas.transform as RectTransform;
            EnsureDim(canvasRect);
            dim.transform.SetParent(canvasRect, false);
            Stretch(dim.rectTransform);
            dim.gameObject.SetActive(true);
            dim.transform.SetAsLastSibling();

            target.transform.SetParent(canvasRect, true);
            target.transform.SetAsLastSibling();

            EnsureHint(canvasRect);
            hintLabel.text = string.IsNullOrEmpty(hint) ? string.Empty : hint;
            hintLabel.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            hintLabel.transform.SetAsLastSibling();
            PlaceHint();
        }

        void Restore()
        {
            if (target != null && originalParent != null)
            {
                target.transform.SetParent(originalParent, true);
                target.transform.SetSiblingIndex(originalSibling);
                target.transform.localScale = originalScale;
                target.transform.localRotation = originalRotation;
            }

            target = null;
            originalParent = null;
            if (dim != null) dim.gameObject.SetActive(false);
            if (hintLabel != null) hintLabel.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (target == null || !target.activeInHierarchy)
            {
                if (hintLabel != null) hintLabel.gameObject.SetActive(false);
                return;
            }

            if (dim != null) dim.transform.SetAsLastSibling();
            target.transform.SetAsLastSibling();
            if (hintLabel != null && hintLabel.gameObject.activeSelf)
            {
                hintLabel.transform.SetAsLastSibling();
                PlaceHint();
            }
        }

        void OnDestroy()
        {
            Restore();
        }

        static Canvas RootCanvas(GameObject go)
        {
            if (go == null) return null;
            Canvas canvas = go.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.rootCanvas : null;
        }

        void EnsureDim(RectTransform canvasRect)
        {
            if (dim != null) return;
            GameObject go = new GameObject("TutorialDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(canvasRect, false);
            dim = go.GetComponent<Image>();
            dim.color = new Color(0.02f, 0.02f, 0.04f, 0.62f);
            dim.raycastTarget = true;
        }

        void EnsureHint(RectTransform canvasRect)
        {
            if (hintLabel != null) return;
            hintLabel = UiFactory.CreateText(canvasRect, "TutorialHint", string.Empty, 36f,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(220f, 48f),
                TextAlignmentOptions.MidlineLeft);
            hintLabel.color = new Color(0.96f, 0.86f, 0.45f, 1f);
            hintLabel.raycastTarget = false;
            UiFonts.Apply(hintLabel);
        }

        void PlaceHint()
        {
            if (hintLabel == null || target == null) return;
            RectTransform source = target.GetComponent<RectTransform>();
            RectTransform hintRect = hintLabel.rectTransform;
            RectTransform parent = hintRect.parent as RectTransform;
            if (source == null || parent == null) return;

            Canvas canvas = parent.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector3[] corners = new Vector3[4];
            source.GetWorldCorners(corners);
            Vector2 right = RectTransformUtility.WorldToScreenPoint(cam, (corners[2] + corners[3]) * 0.5f);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, right, cam, out local);
            hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0.5f);
            hintRect.pivot = new Vector2(0f, 0.5f);
            hintRect.anchoredPosition = local + new Vector2(16f, 0f);
            hintRect.sizeDelta = new Vector2(220f, 48f);
        }

        static void Stretch(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
