using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>全屏遮罩，把目标按钮抬到遮罩之上接收点击，避免跨 Canvas 挖洞点不中。</summary>
    public sealed class TutorialSpotlight : MonoBehaviour
    {
        const int SortingOrder = 400;
        static TutorialSpotlight instance;
        GameObject target;
        RectTransform hole;
        Button holeButton;
        TMP_Text hintLabel;
        Canvas liftedCanvas;
        GraphicRaycaster liftedRaycaster;
        bool addedCanvas;
        bool addedRaycaster;
        bool oldOverride;
        int oldOrder;

        public static void Show(GameObject targetButton, string hint)
        {
            if (targetButton == null)
            {
                Hide();
                return;
            }

            if (instance == null) instance = Create();
            instance.RestoreLift();
            instance.target = targetButton;
            instance.LiftTarget(targetButton);
            instance.hintLabel.text = string.IsNullOrEmpty(hint) ? string.Empty : hint;
            instance.gameObject.SetActive(true);
            instance.SyncHole();
        }

        public static void Hide()
        {
            if (instance == null) return;
            instance.RestoreLift();
            instance.target = null;
            instance.gameObject.SetActive(false);
        }

        static TutorialSpotlight Create()
        {
            RectTransform root = UiFactory.CreateOverlay("TutorialSpotlightCanvas", SortingOrder);
            TutorialSpotlight view = root.gameObject.AddComponent<TutorialSpotlight>();
            Image dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.02f, 0.04f, 0.62f);
            dim.raycastTarget = true;

            GameObject holeGo = new GameObject("Hole", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            holeGo.transform.SetParent(root, false);
            view.hole = holeGo.GetComponent<RectTransform>();
            Image holeImage = holeGo.GetComponent<Image>();
            holeImage.color = new Color(1f, 1f, 1f, 0.02f);
            holeImage.raycastTarget = true;
            view.holeButton = holeGo.GetComponent<Button>();
            view.holeButton.transition = Selectable.Transition.None;
            view.holeButton.onClick.AddListener(view.OnHoleClicked);

            view.hintLabel = UiFactory.CreateText(root, "Hint", string.Empty, 36f,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-280f, 0f), new Vector2(280f, 48f));
            view.hintLabel.color = new Color(0.96f, 0.86f, 0.45f, 1f);
            view.hintLabel.raycastTarget = false;
            UiFonts.ApplyTree(root);
            return view;
        }

        void LateUpdate()
        {
            if (target == null || !target.activeInHierarchy)
            {
                if (hole != null) hole.gameObject.SetActive(false);
                return;
            }
            SyncHole();
        }

        void LiftTarget(GameObject go)
        {
            if (go == null) return;
            liftedCanvas = go.GetComponent<Canvas>();
            if (liftedCanvas == null)
            {
                liftedCanvas = go.AddComponent<Canvas>();
                addedCanvas = true;
            }
            else addedCanvas = false;
            oldOverride = liftedCanvas.overrideSorting;
            oldOrder = liftedCanvas.sortingOrder;
            liftedCanvas.overrideSorting = true;
            liftedCanvas.sortingOrder = SortingOrder + 10;
            liftedRaycaster = go.GetComponent<GraphicRaycaster>();
            if (liftedRaycaster == null)
            {
                liftedRaycaster = go.AddComponent<GraphicRaycaster>();
                addedRaycaster = true;
            }
            else addedRaycaster = false;
        }

        void RestoreLift()
        {
            if (addedRaycaster && liftedRaycaster != null) Destroy(liftedRaycaster);
            if (liftedCanvas != null)
            {
                if (addedCanvas) Destroy(liftedCanvas);
                else
                {
                    liftedCanvas.overrideSorting = oldOverride;
                    liftedCanvas.sortingOrder = oldOrder;
                }
            }
            liftedCanvas = null;
            liftedRaycaster = null;
            addedCanvas = false;
            addedRaycaster = false;
        }

        void OnDestroy()
        {
            RestoreLift();
        }

        void SyncHole()
        {
            if (hole == null || target == null) return;
            RectTransform source = target.GetComponent<RectTransform>();
            if (source == null)
            {
                hole.gameObject.SetActive(false);
                return;
            }

            hole.gameObject.SetActive(true);
            hole.SetAsLastSibling();
            if (hintLabel != null) hintLabel.transform.SetAsLastSibling();
            Canvas srcCanvas = source.GetComponentInParent<Canvas>();
            Camera cam = srcCanvas != null && srcCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? srcCanvas.worldCamera
                : null;
            Vector3[] corners = new Vector3[4];
            source.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            Vector2 size = max - min;
            if (size.x < 80f) size.x = 80f;
            if (size.y < 80f) size.y = 80f;
            Vector2 center = (min + max) * 0.5f;
            hole.anchorMin = hole.anchorMax = new Vector2(0.5f, 0.5f);
            hole.pivot = new Vector2(0.5f, 0.5f);
            Vector2 local;
            RectTransform parent = hole.parent as RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, center, cam, out local);
            hole.anchoredPosition = local;
            hole.sizeDelta = size + new Vector2(24f, 24f);
            if (hintLabel != null)
            {
                RectTransform hintRect = hintLabel.rectTransform;
                hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0.5f);
                hintRect.pivot = new Vector2(0.5f, 0f);
                hintRect.anchoredPosition = local + new Vector2(0f, size.y * 0.5f + 28f);
                hintRect.sizeDelta = new Vector2(560f, 48f);
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
