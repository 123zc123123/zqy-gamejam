using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 全屏图按 sprite 比例 cover 父节点：窄屏裁左右，宽屏裁上下，不露底。
    /// 节点保持 stretch 锚点，只改 sizeDelta。
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class CoverFit : MonoBehaviour
    {
        public const float DefaultAspect = 1080f / 1920f;

        [SerializeField] float fallbackAspect = DefaultAspect;

        RectTransform rect;
        Image image;
        Vector2 lastParentSize;
        float lastAspect = -1f;
        bool applying;

        void OnEnable()
        {
            rect = transform as RectTransform;
            image = GetComponent<Image>();
            lastAspect = -1f;
            Apply();
        }

        void Start()
        {
            lastAspect = -1f;
            Apply();
        }

        void LateUpdate()
        {
            Apply();
        }

        void OnRectTransformDimensionsChange()
        {
            if (applying) return;
            lastAspect = -1f;
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
            Vector2 parentSize = parent.rect.size;
            float aspect = ResolveAspect();
            if (aspect < 0.01f) return;

            if ((parentSize - lastParentSize).sqrMagnitude < 0.25f
                && Mathf.Abs(aspect - lastAspect) < 0.0001f
                && lastAspect > 0f)
                return;

            Vector2 sizeDelta;
            if (!TryLayout(parentSize, aspect, out sizeDelta))
                return;

            LockStretch();
            if ((rect.sizeDelta - sizeDelta).sqrMagnitude > 0.05f)
                rect.sizeDelta = sizeDelta;

            lastParentSize = parentSize;
            lastAspect = aspect;
        }

        void LockStretch()
        {
            Vector2 zero = Vector2.zero;
            Vector2 one = Vector2.one;
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            if ((rect.anchorMin - zero).sqrMagnitude > 0.0001f)
                rect.anchorMin = zero;
            if ((rect.anchorMax - one).sqrMagnitude > 0.0001f)
                rect.anchorMax = one;
            if ((rect.pivot - pivot).sqrMagnitude > 0.0001f)
                rect.pivot = pivot;
            if (rect.anchoredPosition.sqrMagnitude > 0.05f)
                rect.anchoredPosition = Vector2.zero;
            if ((rect.localScale - Vector3.one).sqrMagnitude > 0.0001f)
                rect.localScale = Vector3.one;
        }

        float ResolveAspect()
        {
            if (image == null) image = GetComponent<Image>();
            if (image != null && image.sprite != null)
            {
                Rect spriteRect = image.sprite.rect;
                if (spriteRect.height > 1f && spriteRect.width > 1f)
                    return spriteRect.width / spriteRect.height;
            }

            return fallbackAspect > 0.01f ? fallbackAspect : DefaultAspect;
        }

        /// <summary>
        /// stretch 锚点下，算出刚好 cover 父节点的 sizeDelta。
        /// </summary>
        public static bool TryLayout(Vector2 parentSize, float aspect, out Vector2 sizeDelta)
        {
            sizeDelta = Vector2.zero;
            if (parentSize.x < 2f || parentSize.y < 2f) return false;
            if (aspect < 0.01f) return false;

            float parentAspect = parentSize.x / parentSize.y;
            if (parentAspect > aspect)
            {
                float height = parentSize.x / aspect;
                sizeDelta = new Vector2(0f, height - parentSize.y);
            }
            else
            {
                float width = parentSize.y * aspect;
                sizeDelta = new Vector2(width - parentSize.x, 0f);
            }

            return true;
        }
    }
}
