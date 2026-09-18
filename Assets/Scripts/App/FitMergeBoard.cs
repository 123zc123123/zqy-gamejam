using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 合成盘按 BoardBase 设计尺寸，等比缩放到顶/底栏之间。
    /// 尺寸和边距从预制体节点读取；只在 Play 里改坐标。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class FitMergeBoard : MonoBehaviour
    {
        static readonly string[] TopLimitNames =
        {
            "main-event-actions", "MainEventActions",
            "event-title-panel", "EventTitlePanel"
        };
        static readonly string[] BottomLimitNames =
        {
            "bottom-event-carousel", "BottomEventCarousel"
        };

        RectTransform rect;
        RectTransform cachedParent;
        readonly Vector3[] corners = new Vector3[4];
        Vector2 lastParentSize;
        float lastTop;
        float lastBottom;
        float lastScale = -1f;
        Vector2 lastPos;
        bool applying;

        void OnEnable()
        {
            rect = transform as RectTransform;
            lastScale = -1f;
            if (Application.isPlaying) Apply();
        }

        void Start()
        {
            lastScale = -1f;
            Apply();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            Apply();
        }

        void OnRectTransformDimensionsChange()
        {
            if (applying) return;
            if (!Application.isPlaying) return;
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;
            lastScale = -1f;
            Apply();
        }

        void Apply()
        {
            if (!Application.isPlaying) return;
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;
            if (applying) return;
            if (rect == null) rect = transform as RectTransform;
            if (rect == null) return;

            RectTransform parent = rect.parent as RectTransform;
            if (parent == null) return;
            if (parent.rect.width < 2f || parent.rect.height < 2f) return;
            applying = true;
            try
            {
                ApplyUnlocked(parent);
            }
            finally
            {
                applying = false;
            }
        }

        void ApplyUnlocked(RectTransform parent)
        {
            if (parent.rect.width < 2f || parent.rect.height < 2f) return;

            Vector2 parentSize = parent.rect.size;
            RectTransform frameRt = ResolveFrame();
            Vector2 designSize = frameRt.sizeDelta;
            Vector2 boardSize = rect.sizeDelta;
            float side = SidePadding(ReferenceWidth(parent), designSize.x);
            ResolvePads(parent, parentSize, out float top, out float bottom);

            if (cachedParent == parent
                && (parentSize - lastParentSize).sqrMagnitude < 0.25f
                && Mathf.Abs(top - lastTop) < 0.5f
                && Mathf.Abs(bottom - lastBottom) < 0.5f
                && lastScale > 0f)
                return;

            Vector2 pos;
            float scale;
            if (!TryLayout(parentSize, designSize, top, bottom, side, side, out pos, out scale))
                return;

            LockBoardRect(boardSize);
            if (Mathf.Abs(scale - lastScale) > 0.001f || (pos - lastPos).sqrMagnitude > 0.05f)
            {
                rect.localScale = new Vector3(scale, scale, 1f);
                rect.anchoredPosition = pos;
            }

            cachedParent = parent;
            lastParentSize = parentSize;
            lastTop = top;
            lastBottom = bottom;
            lastScale = scale;
            lastPos = pos;
        }

        void LockBoardRect(Vector2 boardSize)
        {
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            if ((rect.anchorMin - pivot).sqrMagnitude > 0.0001f
                || (rect.anchorMax - pivot).sqrMagnitude > 0.0001f)
            {
                rect.anchorMin = pivot;
                rect.anchorMax = pivot;
            }

            if ((rect.pivot - pivot).sqrMagnitude > 0.0001f)
                rect.pivot = pivot;

            if ((rect.sizeDelta - boardSize).sqrMagnitude > 0.25f)
                rect.sizeDelta = boardSize;
        }

        void ResolvePads(RectTransform parent, Vector2 parentSize, out float top, out float bottom)
        {
            top = 0f;
            bottom = 0f;

            RectTransform topLimit = FindNamed(parent, TopLimitNames);
            if (topLimit == null) topLimit = FindNamed(parent.root, TopLimitNames);
            if (topLimit != null)
                top = Mathf.Max(0f, parent.rect.yMax - LocalMinY(topLimit, parent));

            float bottomTop = parent.rect.yMin;
            bool foundBottom = false;
            RectTransform bottomLimit = FindNamed(parent, BottomLimitNames);
            if (bottomLimit == null) bottomLimit = FindNamed(parent.root, BottomLimitNames);
            if (bottomLimit != null)
            {
                bottomTop = LocalMaxY(bottomLimit, parent);
                foundBottom = true;
            }

            GameObject lobbyNavGo = GameObject.Find("LobbyNavCanvas");
            if (lobbyNavGo != null && lobbyNavGo.activeInHierarchy)
            {
                RectTransform lobbyNav = lobbyNavGo.transform as RectTransform;
                if (lobbyNav != null)
                {
                    bottomTop = Mathf.Max(bottomTop, LocalMaxY(lobbyNav, parent));
                    foundBottom = true;
                }
            }

            if (foundBottom)
                bottom = Mathf.Max(0f, bottomTop - parent.rect.yMin);

            if (top + bottom >= parentSize.y - 2f)
            {
                top = 0f;
                bottom = 0f;
            }
        }

        RectTransform ResolveFrame()
        {
            RectTransform named = FindNamed(rect, "棋盘底", "BoardBase");
            if (named != null) return named;

            RectTransform best = rect;
            float bestArea = -1f;
            for (int i = 0; i < transform.childCount; i++)
            {
                RectTransform child = transform.GetChild(i) as RectTransform;
                if (child == null) continue;
                float area = Mathf.Abs(child.sizeDelta.x * child.sizeDelta.y);
                if (area > bestArea)
                {
                    bestArea = area;
                    best = child;
                }
            }

            return best != null ? best : rect;
        }

        static RectTransform FindNamed(Transform root, params string[] names)
        {
            if (root == null || names == null) return null;
            for (int i = 0; i < names.Length; i++)
            {
                if (string.IsNullOrEmpty(names[i])) continue;
                RectTransform hit = FindNamedOne(root, names[i]);
                if (hit != null) return hit;
            }
            return null;
        }

        static RectTransform FindNamedOne(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrEmpty(objectName)) return null;
            if (root.name == objectName) return root as RectTransform;
            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform hit = FindNamedOne(root.GetChild(i), objectName);
                if (hit != null) return hit;
            }
            return null;
        }

        float LocalMinY(RectTransform target, RectTransform parent)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return parent.rect.yMax;
            target.GetWorldCorners(corners);
            float min = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                float y = parent.InverseTransformPoint(corners[i]).y;
                if (y < min) min = y;
            }
            return min;
        }

        float LocalMaxY(RectTransform target, RectTransform parent)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return parent.rect.yMin;
            target.GetWorldCorners(corners);
            float max = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float y = parent.InverseTransformPoint(corners[i]).y;
                if (y > max) max = y;
            }
            return max;
        }

        static float ReferenceWidth(RectTransform parent)
        {
            CanvasScaler scaler = parent.GetComponentInParent<CanvasScaler>();
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                return scaler.referenceResolution.x;
            return parent.rect.width;
        }

        public static float SidePadding(float referenceWidth, float designWidth)
        {
            if (referenceWidth < 1f || designWidth < 1f) return 0f;
            return Mathf.Max(0f, (referenceWidth - designWidth) * 0.5f);
        }

        /// <summary>
        /// 在父节点可用矩形里等比放下 designSize，返回中心点和缩放。
        /// </summary>
        public static bool TryLayout(
            Vector2 parentSize,
            Vector2 designSize,
            float top,
            float bottom,
            float left,
            float right,
            out Vector2 anchoredPosition,
            out float scale)
        {
            anchoredPosition = Vector2.zero;
            scale = 1f;
            if (parentSize.x < 2f || parentSize.y < 2f) return false;
            if (designSize.x < 1f || designSize.y < 1f) return false;

            top = Mathf.Max(0f, top);
            bottom = Mathf.Max(0f, bottom);
            left = Mathf.Max(0f, left);
            right = Mathf.Max(0f, right);

            float availW = parentSize.x - left - right;
            float availH = parentSize.y - top - bottom;
            if (availW < 1f || availH < 1f) return false;

            scale = Mathf.Min(availW / designSize.x, availH / designSize.y);
            if (scale < 0.01f) return false;

            float xMin = -parentSize.x * 0.5f + left;
            float xMax = parentSize.x * 0.5f - right;
            float yMin = -parentSize.y * 0.5f + bottom;
            float yMax = parentSize.y * 0.5f - top;
            anchoredPosition = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
            return true;
        }
    }
}
