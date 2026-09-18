using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 合成盘按设计尺寸等比缩放到标题/底栏之间的可用区域。
    /// 棋盘底和 20 格都是子节点，跟着这一层一起变，避免只拉伸外框。
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class FitMergeBoard : MonoBehaviour
    {
        public const float DefaultSidePadding = 64f;
        public const float DefaultGap = 12f;
        public const float DefaultFallbackTop = 320f;
        public const float DefaultFallbackBottom = 240f;
        public static readonly Vector2 DefaultDesignSize = new Vector2(952f, 1193f);
        public static readonly Vector2 DefaultBoardSize = new Vector2(912f, 1145f);

        [SerializeField] Vector2 designSize = new Vector2(952f, 1193f);
        [SerializeField] Vector2 boardSize = new Vector2(912f, 1145f);
        [SerializeField] float sidePadding = 64f;
        [SerializeField] float gap = 12f;
        [SerializeField] float fallbackTop = 320f;
        [SerializeField] float fallbackBottom = 240f;
        [SerializeField] string topLimitName = "main-event-actions";
        [SerializeField] string bottomLimitName = "bottom-event-carousel";

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
            Apply();
        }

        void Start()
        {
            lastScale = -1f;
            Apply();
        }

        void LateUpdate()
        {
            Apply();
        }

        void OnRectTransformDimensionsChange()
        {
            if (applying) return;
            lastScale = -1f;
            Apply();
        }

        void Apply()
        {
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
            ResolvePads(parent, parentSize, out float top, out float bottom);

            if (cachedParent == parent
                && (parentSize - lastParentSize).sqrMagnitude < 0.25f
                && Mathf.Abs(top - lastTop) < 0.5f
                && Mathf.Abs(bottom - lastBottom) < 0.5f
                && lastScale > 0f)
                return;

            Vector2 pos;
            float scale;
            if (!TryLayout(parentSize, designSize, top, bottom, sidePadding, sidePadding, out pos, out scale))
                return;

            LockBoardRect();
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

        void LockBoardRect()
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
            top = Mathf.Max(0f, fallbackTop);
            bottom = Mathf.Max(0f, fallbackBottom);

            RectTransform topLimit = FindLimit(parent, topLimitName, "MainEventActions");
            if (topLimit != null)
            {
                float topBottom = LocalMinY(topLimit, parent);
                top = Mathf.Max(0f, parent.rect.yMax - topBottom + gap);
            }

            RectTransform bottomLimit = FindLimit(parent, bottomLimitName, "BottomEventCarousel");
            float bottomTop = parent.rect.yMin + bottom;
            if (bottomLimit != null)
                bottomTop = Mathf.Max(bottomTop, LocalMaxY(bottomLimit, parent));

            GameObject lobbyNavGo = GameObject.Find("LobbyNavCanvas");
            if (lobbyNavGo != null && lobbyNavGo.activeInHierarchy)
            {
                RectTransform lobbyNav = lobbyNavGo.transform as RectTransform;
                if (lobbyNav != null)
                    bottomTop = Mathf.Max(bottomTop, LocalMaxY(lobbyNav, parent));
            }

            bottom = Mathf.Max(0f, bottomTop - parent.rect.yMin + gap);
            if (top + bottom >= parentSize.y - 2f)
            {
                top = Mathf.Max(0f, fallbackTop);
                bottom = Mathf.Max(0f, fallbackBottom);
            }
        }

        float LocalMinY(RectTransform target, RectTransform parent)
        {
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
            target.GetWorldCorners(corners);
            float max = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float y = parent.InverseTransformPoint(corners[i]).y;
                if (y > max) max = y;
            }
            return max;
        }

        static RectTransform FindLimit(RectTransform parent, params string[] names)
        {
            if (parent == null || names == null) return null;
            for (int i = 0; i < names.Length; i++)
            {
                if (string.IsNullOrEmpty(names[i])) continue;
                RectTransform found = FindNamed(parent, names[i]);
                if (found == null) found = FindNamed(parent.root, names[i]);
                if (found != null) return found;
            }
            return null;
        }

        static RectTransform FindNamed(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrEmpty(objectName)) return null;
            if (root.name == objectName) return root as RectTransform;
            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform hit = FindNamed(root.GetChild(i), objectName);
                if (hit != null) return hit;
            }
            return null;
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
