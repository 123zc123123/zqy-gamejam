using UnityEngine;
using UnityEngine.Rendering;

namespace DouQuqu
{
    /// <summary>
    /// 蟋蟀脚下的玩家色圆圈和落地震影。表现层，不参与碰撞 / 出圈。
    /// </summary>
    public sealed class DouQuquGroundMarker : MonoBehaviour
    {
        public static readonly Color[] PlayerColors =
        {
            new Color(0.16f, 0.80f, 0.69f, 1f),
            new Color(0.89f, 0.20f, 0.52f, 1f),
            new Color(0.35f, 0.82f, 0.28f, 1f),
            new Color(1.00f, 0.78f, 0.18f, 1f)
        };

        [SerializeField] private SpriteRenderer shadow;
        [SerializeField] private SpriteRenderer fill;
        [SerializeField] private SpriteRenderer ring;
        [SerializeField] private float ringScale = 1.38f;
        [SerializeField] private float shadowScale = 1.08f;
        [SerializeField] private float shadowOffsetScale = 0.22f;
        [SerializeField] private float heightOffset = 0.03f;

        private static Sprite fillSprite;
        private static Sprite ringSprite;
        private static Sprite shadowSprite;
        private Material spriteMaterial;

        public static Color ColorForPlayer(int playerId)
        {
            int index = Mathf.Abs(playerId) % PlayerColors.Length;
            return PlayerColors[index];
        }

        public void Apply(Vector3 worldCenter, float bugRadius, Color playerColor, float height = 0f, bool charging = false)
        {
            EnsureReady();
            gameObject.SetActive(true);
            transform.position = new Vector3(worldCenter.x, heightOffset, worldCenter.z);
            transform.rotation = Quaternion.identity;

            float lift = 1f / (1f + Mathf.Max(0f, height) * 0.55f);
            float radius = Mathf.Max(0.2f, bugRadius);
            Color paint = charging ? Color.Lerp(playerColor, Color.white, 0.28f) : playerColor;
            Layout(shadow, shadowSprite, radius * shadowScale * lift, new Color(0.02f, 0.02f, 0.02f, 0.55f * lift), -22);
            if (shadow != null)
                shadow.transform.localPosition = new Vector3(0f, 0f, -radius * shadowOffsetScale * lift);
            Layout(fill, fillSprite, radius * ringScale, new Color(paint.r, paint.g, paint.b, charging ? 0.34f : 0.22f), -21);
            Layout(ring, ringSprite, radius * ringScale, new Color(paint.r, paint.g, paint.b, 0.94f), -20);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public void EnsureReady()
        {
            if (shadow == null) shadow = CreateSprite("Shadow", -22);
            if (fill == null) fill = CreateSprite("Fill", -21);
            if (ring == null) ring = CreateSprite("Ring", -20);
            shadow.sprite = ShadowSprite();
            fill.sprite = FillSprite();
            ring.sprite = RingSprite();
        }

        private void Layout(SpriteRenderer renderer, Sprite sprite, float worldRadius, Color color, int sorting)
        {
            if (renderer == null || sprite == null || worldRadius < 0.01f)
            {
                if (renderer != null) renderer.enabled = false;
                return;
            }

            renderer.sprite = sprite;
            renderer.sharedMaterial = SpriteMaterial();
            renderer.color = color;
            renderer.sortingOrder = sorting;
            renderer.enabled = true;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            renderer.transform.localPosition = Vector3.zero;
            Vector3 size = sprite.bounds.size;
            float diameter = worldRadius * 2f;
            renderer.transform.localScale = new Vector3(
                diameter / Mathf.Max(0.01f, size.x),
                diameter / Mathf.Max(0.01f, size.y),
                1f);
        }

        private SpriteRenderer CreateSprite(string childName, int sorting)
        {
            Transform existing = transform.Find(childName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(childName);
            if (existing == null) child.transform.SetParent(transform, false);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = child.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = SpriteMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sorting;
            renderer.enabled = false;
            child.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            child.transform.localPosition = Vector3.zero;
            child.transform.localScale = Vector3.one;
            return renderer;
        }

        private static Sprite FillSprite()
        {
            if (fillSprite == null) fillSprite = MakeDisc("GroundFill", 128, 0f, 1f, 0.045f, false);
            return fillSprite;
        }

        private static Sprite RingSprite()
        {
            if (ringSprite == null) ringSprite = MakeDisc("GroundRing", 128, 0.82f, 1f, 0.04f, false);
            return ringSprite;
        }

        private static Sprite ShadowSprite()
        {
            if (shadowSprite == null) shadowSprite = MakeDisc("GroundShadow", 128, 0f, 1f, 0.12f, true);
            return shadowSprite;
        }

        private static Sprite MakeDisc(string spriteName, int pixels, float inner, float outer, float softness, bool quadratic)
        {
            Texture2D texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
            texture.name = spriteName + "Tex";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;

            Color[] colors = new Color[pixels * pixels];
            float center = (pixels - 1) * 0.5f;
            float inv = 1f / Mathf.Max(0.001f, center);
            float soft = Mathf.Max(0.004f, softness);
            for (int y = 0; y < pixels; y++)
            {
                for (int x = 0; x < pixels; x++)
                {
                    float dx = (x - center) * inv;
                    float dy = (y - center) * inv;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((outer - r) / soft);
                    if (inner > 0f) alpha *= Mathf.Clamp01((r - inner) / soft);
                    if (quadratic) alpha *= alpha;
                    colors[y * pixels + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(colors);
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, pixels, pixels), new Vector2(0.5f, 0.5f), pixels);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private Material SpriteMaterial()
        {
            if (spriteMaterial != null) return spriteMaterial;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            spriteMaterial = shader != null ? new Material(shader) : null;
            return spriteMaterial;
        }
    }
}
