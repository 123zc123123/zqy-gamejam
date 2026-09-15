using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 父节点宽度小于设计宽度时等比缩小；超过设计宽度时不放大，只把宽度拉到父节点。
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ShrinkToFitWidth : MonoBehaviour
    {
        [SerializeField] float designWidth = 1080f;

        RectTransform rect;
        float lastParentWidth = -1f;

        void OnEnable()
        {
            rect = transform as RectTransform;
            lastParentWidth = -1f;
            Apply();
        }

        void OnDisable()
        {
            if (rect == null) return;
            rect.localScale = Vector3.one;
        }

        void LateUpdate()
        {
            Apply();
        }

        void OnRectTransformDimensionsChange()
        {
            lastParentWidth = -1f;
            Apply();
        }

        void Apply()
        {
            if (rect == null) rect = transform as RectTransform;
            if (rect == null) return;

            RectTransform parent = rect.parent as RectTransform;
            if (parent == null) return;

            float parentWidth = parent.rect.width;
            if (parentWidth <= 1f) return;
            if (Mathf.Abs(parentWidth - lastParentWidth) < 0.5f) return;
            lastParentWidth = parentWidth;

            float width = Mathf.Max(1f, designWidth);
            if (parentWidth < width)
            {
                float scale = parentWidth / width;
                rect.localScale = new Vector3(scale, scale, 1f);
                Vector2 size = rect.sizeDelta;
                size.x = width;
                rect.sizeDelta = size;
            }
            else
            {
                rect.localScale = Vector3.one;
                Vector2 size = rect.sizeDelta;
                size.x = parentWidth;
                rect.sizeDelta = size;
            }
        }
    }
}
