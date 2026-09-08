using UnityEngine;
using UnityEngine.Rendering;

namespace DouQuqu
{
    /// <summary>
    /// 蓄力落点走廊。色带和箭头用 SVG 精灵铺在地面上，长度随蓄力拉伸。
    /// </summary>
    public sealed class DouQuquChargeArrow : MonoBehaviour
    {
        public const int MaxChevrons = 16;

        [SerializeField] private Sprite fillSprite;
        [SerializeField] private Sprite chevronSprite;
        [SerializeField] private SpriteRenderer fill;
        [SerializeField] private SpriteRenderer[] chevrons = new SpriteRenderer[MaxChevrons];
        [SerializeField] private float minDistance = 0.05f;

        private Sprite fallbackFill;
        private Sprite fallbackChevron;
        private Material spriteMaterial;

        public void Apply(bool charging, float distance, float fillAmount, Vector2 direction, Vector3 origin, float radius, Color playerColor)
        {
            EnsureReady();
            Sprite bandSprite = UsableSprite(fillSprite, true);
            if (!charging || distance < minDistance || direction.sqrMagnitude < 0.0001f || bandSprite == null)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);
            Vector2 dir = direction.normalized;
            transform.position = origin;
            transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);

            bool full = fillAmount >= 0.98f;
            float halfW = Mathf.Max(0.35f, radius);
            float peak = full ? 0.78f : 0.42f + fillAmount * 0.32f;
            Color band = ChargeBand(playerColor, full);
            Color arrow = ChargeMark(playerColor);
            LayoutGroundSprite(fill, bandSprite, new Vector3(0f, 0.04f, 0f), new Vector2(halfW * 2f, distance), new Color(band.r, band.g, band.b, peak), 24);

            Sprite mark = UsableSprite(chevronSprite, false);
            float arrowLen = Mathf.Min(1.15f, Mathf.Max(0.55f, halfW * 0.85f));
            float arrowW = halfW * 0.42f;
            float gap = Mathf.Max(1.6f, arrowLen * 2.1f);
            float first = Mathf.Min(distance * 0.22f, gap);
            int n = 0;
            for (float along = first; along < distance - arrowLen * 1.2f && n < MaxChevrons; along += gap)
            {
                float t = along / Mathf.Max(0.0001f, distance);
                float fade = Fade(t);
                float alpha = (0.45f + fade * (full ? 0.5f : 0.35f)) * Mathf.Lerp(0.75f, 1f, fillAmount);
                LayoutGroundSprite(
                    chevrons[n],
                    mark,
                    new Vector3(0f, 0.05f, along + arrowLen),
                    new Vector2(arrowW * 2f, arrowLen * 1.35f),
                    new Color(arrow.r, arrow.g, arrow.b, Mathf.Clamp01(alpha)),
                    26);
                n++;
            }
            for (int i = n; i < MaxChevrons; i++)
                if (chevrons[i] != null) chevrons[i].enabled = false;
        }

        public void Hide()
        {
            if (fill != null) fill.enabled = false;
            if (chevrons != null)
                for (int i = 0; i < chevrons.Length; i++)
                    if (chevrons[i] != null) chevrons[i].enabled = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public void EnsureReady()
        {
            if (fill == null) fill = CreateSpriteChild("Fill", UsableSprite(fillSprite, true), 24);
            SpriteRenderer[] old = chevrons;
            if (chevrons == null || chevrons.Length != MaxChevrons)
            {
                chevrons = new SpriteRenderer[MaxChevrons];
                if (old != null)
                    for (int i = 0; i < old.Length && i < MaxChevrons; i++)
                        chevrons[i] = old[i];
            }
            Transform folder = transform.Find("Chevrons");
            if (folder == null)
            {
                GameObject go = new GameObject("Chevrons");
                go.transform.SetParent(transform, false);
                folder = go.transform;
            }
            Sprite mark = UsableSprite(chevronSprite, false);
            for (int i = 0; i < MaxChevrons; i++)
            {
                if (chevrons[i] != null) continue;
                Transform existing = folder.Find("Chevron_" + i);
                if (existing != null) chevrons[i] = existing.GetComponent<SpriteRenderer>();
                if (chevrons[i] == null) chevrons[i] = CreateSpriteChild("Chevron_" + i, mark, 26, folder);
            }
        }

        private SpriteRenderer CreateSpriteChild(string childName, Sprite sprite, int sorting, Transform parent = null)
        {
            Transform existing = (parent != null ? parent : transform).Find(childName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(childName);
            if (existing == null) child.transform.SetParent(parent != null ? parent : transform, false);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = child.AddComponent<SpriteRenderer>();
            ConfigureRenderer(renderer, sprite, sorting);
            renderer.enabled = false;
            child.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            child.transform.localPosition = Vector3.zero;
            child.transform.localScale = Vector3.one;
            return renderer;
        }

        private void LayoutGroundSprite(SpriteRenderer renderer, Sprite sprite, Vector3 localPos, Vector2 worldSize, Color color, int sorting)
        {
            if (renderer == null || sprite == null)
            {
                if (renderer != null) renderer.enabled = false;
                return;
            }
            ConfigureRenderer(renderer, sprite, sorting);
            renderer.color = color;
            renderer.enabled = true;
            renderer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            renderer.transform.localPosition = localPos;
            Vector3 size = sprite.bounds.size;
            float sx = worldSize.x / Mathf.Max(0.01f, size.x);
            float sy = worldSize.y / Mathf.Max(0.01f, size.y);
            renderer.transform.localScale = new Vector3(sx, sy, 1f);
        }

        private void ConfigureRenderer(SpriteRenderer renderer, Sprite sprite, int sorting)
        {
            renderer.sprite = sprite;
            renderer.sharedMaterial = SpriteMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sorting;
        }

        private Sprite UsableSprite(Sprite authored, bool fillPivot)
        {
            if (authored != null && authored.bounds.size.sqrMagnitude > 0.0001f) return authored;
            return fillPivot ? FallbackFill() : FallbackChevron();
        }

        private Sprite FallbackFill()
        {
            if (fallbackFill != null) return fallbackFill;
            fallbackFill = Sprite.Create(WhiteTexture(), new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0f), 8f);
            fallbackFill.name = "ChargeFillFallback";
            return fallbackFill;
        }

        private Sprite FallbackChevron()
        {
            if (fallbackChevron != null) return fallbackChevron;
            fallbackChevron = Sprite.Create(WhiteTexture(), new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 1f), 8f);
            fallbackChevron.name = "ChargeChevronFallback";
            return fallbackChevron;
        }

        private static Texture2D WhiteTexture()
        {
            Texture2D texture = Texture2D.whiteTexture;
            if (texture != null) return texture;
            texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private Material SpriteMaterial()
        {
            if (spriteMaterial != null) return spriteMaterial;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            spriteMaterial = shader != null ? new Material(shader) : null;
            return spriteMaterial;
        }

        private static float Fade(float t)
        {
            if (t < 0.16f) return t / 0.16f;
            if (t > 0.84f) return (1f - t) / 0.16f;
            return 1f;
        }

        /// <summary>色带跟玩家色同色相；满蓄再提亮一档，暗槽位抬到能看清。</summary>
        private static Color ChargeBand(Color player, bool full)
        {
            float h, s, v;
            Color.RGBToHSV(player, out h, out s, out v);
            v = Mathf.Clamp(Mathf.Max(v, 0.52f) + (full ? 0.18f : 0f), 0f, 1f);
            s = Mathf.Clamp(s, 0.4f, 0.95f);
            Color color = Color.HSVToRGB(h, s, v);
            color.a = 1f;
            return color;
        }

        /// <summary>箭头比色带更浅，仍保持该玩家的色相。</summary>
        private static Color ChargeMark(Color player)
        {
            float h, s, v;
            Color.RGBToHSV(player, out h, out s, out v);
            Color color = Color.HSVToRGB(h, Mathf.Clamp(s * 0.45f, 0.15f, 0.7f), 1f);
            color.a = 1f;
            return color;
        }
    }
}
