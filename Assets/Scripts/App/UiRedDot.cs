using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>挂在按钮或图标右上角的红色小圆点。</summary>
    public static class UiRedDot
    {
        const string ChildName = "RedDot";
        const float Size = 20f;
        static Sprite circle;

        public static void Set(RectTransform host, bool on)
        {
            if (host == null) return;
            Transform existing = host.Find(ChildName);
            if (!on)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                return;
            }

            Image image;
            RectTransform rect;
            if (existing == null)
            {
                GameObject go = new GameObject(ChildName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Canvas));
                go.transform.SetParent(host, false);
                rect = go.GetComponent<RectTransform>();
                image = go.GetComponent<Image>();
                image.sprite = Circle();
                image.color = new Color(0.86f, 0.16f, 0.14f, 1f);
                image.raycastTarget = false;
                image.preserveAspect = true;
                Canvas canvas = go.GetComponent<Canvas>();
                canvas.overrideSorting = false;
                canvas.pixelPerfect = false;
            }
            else
            {
                existing.gameObject.SetActive(true);
                rect = existing as RectTransform;
                image = existing.GetComponent<Image>();
            }

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Size, Size);
            rect.anchoredPosition = new Vector2(2f, 2f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.SetAsLastSibling();
            if (image != null) image.enabled = true;
        }

        static Sprite Circle()
        {
            if (circle != null) return circle;
            const int s = 32;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float cx = (s - 1) * 0.5f;
            float r = s * 0.5f - 1.2f;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cx));
                    float a = Mathf.Clamp01(r - d + 0.6f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply(false, false);
            circle = Sprite.Create(tex, new Rect(0f, 0f, s, s), new Vector2(0.5f, 0.5f), 100f);
            circle.name = "UiRedDotCircle";
            return circle;
        }
    }
}
