using UnityEngine;
using UnityEngine.U2D.Animation;

namespace DouQuqu
{
    /// <summary>
    /// 局内蛐蛐表现：根节点只负责朝向和缩放。
    /// 主体（头 / 胸腹 / 腿）共用描边 shader：八向平移 + 贴图 alpha，再靠 stencil 合成外轮廓。
    /// 触角、尾刺不描边。
    /// </summary>
    public sealed class CricketVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer antenna;
        [SerializeField] private SpriteRenderer[] antennae;
        [SerializeField] private SpriteRenderer[] parts;
        [SerializeField] private Color outlineColor = Color.black;
        [SerializeField] private float outlineWidth = 16f;
        [SerializeField] private float outlineSoftness = 4f;

        private MaterialPropertyBlock propertyBlock;
        private SpriteRenderer armorGlow;
        private Sprite glowSprite;
        private static readonly Color ArmorGold = new Color(1f, 0.82f, 0.18f, 1f);
        private static readonly Color ArmorTint = new Color(1f, 0.93f, 0.55f, 1f);

        public SpriteRenderer BodyRenderer
        {
            get
            {
                if (body == null) BindHierarchy();
                return body;
            }
        }

        public SpriteRenderer AntennaRenderer
        {
            get
            {
                if (antenna == null) BindHierarchy();
                return antenna;
            }
        }

        /// <summary>只用身体外接尺寸对齐碰撞圆，避免触角把整只虫缩得过小。</summary>
        public float VisualSize
        {
            get
            {
                if (body == null || body.sprite == null) BindHierarchy();
                if (body == null || body.sprite == null) return 1f;
                Vector3 size = body.sprite.bounds.size;
                return Mathf.Max(size.x, size.y);
            }
        }

        private void Awake()
        {
            BindHierarchy();
        }

        private void OnEnable()
        {
            BindHierarchy();
            ApplyOutline();
        }

        public void BindParts(SpriteRenderer bodyRenderer, SpriteRenderer antennaRenderer)
        {
            body = bodyRenderer;
            antenna = antennaRenderer;
            if (antennaRenderer != null)
                antennae = new[] { antennaRenderer };
        }

        /// <summary>
        /// 换皮只换贴图，不换带权重的网格。
        /// 各套 PSB 图层打包位置接近，用 Default 网格的 UV 采样目标皮肤图集，动画才能继续播。
        /// </summary>
        public void ApplySkin(string label)
        {
            if (string.IsNullOrEmpty(label) || label == "1-1") label = "Default";
            SpriteLibrary library = GetComponentInChildren<SpriteLibrary>(true);
            SpriteLibraryAsset asset = library != null ? library.spriteLibraryAsset : null;
            if (asset == null) return;
            if (parts == null || parts.Length == 0) BindHierarchy();
            if (parts == null || parts.Length == 0) return;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

            for (int i = 0; i < parts.Length; i++)
            {
                SpriteRenderer renderer = parts[i];
                if (renderer == null) continue;
                string category = renderer.gameObject.name;
                Sprite meshSprite = asset.GetSprite(category, "Default");
                if (meshSprite != null) renderer.sprite = meshSprite;
                Sprite look = asset.GetSprite(category, label);
                if (look == null) look = meshSprite;
                renderer.GetPropertyBlock(propertyBlock);
                if (look != null && look.texture != null)
                    propertyBlock.SetTexture("_MainTex", look.texture);
                renderer.SetPropertyBlock(propertyBlock);
                ApplyOutlineBlock(renderer, outlineColor, outlineWidth);
            }
        }

        public static string SkinLabel(int quality, int temperament)
        {
            return Mathf.Clamp(quality, 1, 4) + "-" + Mathf.Clamp(temperament, 1, 4);
        }

        public void BindHierarchy()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            parts = renderers;
            int antennaCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (IsAntenna(renderers[i].name)) antennaCount++;
            }

            SpriteRenderer[] foundAntennae = antennaCount > 0 ? new SpriteRenderer[antennaCount] : System.Array.Empty<SpriteRenderer>();
            int antennaIndex = 0;
            SpriteRenderer foundBody = null;
            SpriteRenderer foundAntenna = null;
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                string partName = renderer.name;
                if (foundBody == null && IsBody(partName))
                    foundBody = renderer;
                if (IsAntenna(partName))
                {
                    foundAntennae[antennaIndex++] = renderer;
                    if (foundAntenna == null) foundAntenna = renderer;
                }
            }

            if (foundBody != null) body = foundBody;
            if (foundAntenna != null) antenna = foundAntenna;
            if (foundAntennae.Length > 0) antennae = foundAntennae;
            if (body == null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (!IsAntenna(renderers[i].name) && !IsTail(renderers[i].name))
                    {
                        body = renderers[i];
                        break;
                    }
                }
            }
        }

        private void ApplyOutline()
        {
            if (parts == null || parts.Length == 0) return;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                ApplyOutlineBlock(parts[i], outlineColor, outlineWidth);
            }
        }

        /// <summary>全员黑描边，不染色贴图。队伍色走脚下圈。吕布霸体改金描边和金光。</summary>
        public void ApplyTeam(bool ally, bool charging, bool armorGlowOn = false)
        {
            if (parts == null || parts.Length == 0) BindHierarchy();
            if (parts == null || parts.Length == 0) return;

            _ = ally;
            _ = charging;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

            Color outline = armorGlowOn ? ArmorGold : outlineColor;
            Color tint = armorGlowOn ? ArmorTint : Color.white;
            float width = armorGlowOn ? outlineWidth * 1.7f : outlineWidth;
            for (int i = 0; i < parts.Length; i++)
            {
                SpriteRenderer renderer = parts[i];
                if (renderer == null) continue;
                ApplyOutlineBlock(renderer, outline, width);
                renderer.color = tint;
            }
            RefreshArmorGlow(armorGlowOn);
        }

        private void RefreshArmorGlow(bool on)
        {
            if (!on)
            {
                if (armorGlow != null) armorGlow.enabled = false;
                return;
            }

            if (armorGlow == null)
            {
                Transform existing = transform.Find("ArmorGlow");
                GameObject go = existing != null ? existing.gameObject : new GameObject("ArmorGlow");
                if (existing == null) go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                armorGlow = go.GetComponent<SpriteRenderer>();
                if (armorGlow == null) armorGlow = go.AddComponent<SpriteRenderer>();
                armorGlow.sprite = GlowSprite();
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null) armorGlow.sharedMaterial = new Material(shader);
                armorGlow.sortingOrder = -2;
            }

            float pulse = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.time * 9f));
            float size = VisualSize * (1.55f + 0.12f * Mathf.Sin(Time.time * 7f));
            armorGlow.enabled = true;
            armorGlow.color = new Color(1f, 0.78f, 0.12f, 0.42f * pulse);
            Vector3 spriteSize = armorGlow.sprite != null ? armorGlow.sprite.bounds.size : Vector3.one;
            armorGlow.transform.localScale = new Vector3(
                size / Mathf.Max(0.01f, spriteSize.x),
                size / Mathf.Max(0.01f, spriteSize.y),
                1f);
        }

        private Sprite GlowSprite()
        {
            if (glowSprite != null) return glowSprite;
            const int n = 64;
            Texture2D texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            float mid = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x - mid) / mid;
                    float v = (y - mid) / mid;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = r >= 1f ? 0f : Mathf.Clamp01(1f - r);
                    a = a * a;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            texture.Apply();
            glowSprite = Sprite.Create(texture, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), n);
            glowSprite.name = "LuBuArmorGlow";
            return glowSprite;
        }

        private void ApplyOutlineBlock(SpriteRenderer renderer, Color outline, float width)
        {
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_OutlineColor", outline);
            propertyBlock.SetFloat("_OutlineWidth", WritesOutline(renderer.name) ? width : 0f);
            propertyBlock.SetFloat("_OutlineSoftness", outlineSoftness);

            Sprite sprite = renderer.sprite;
            if (sprite != null)
                propertyBlock.SetFloat("_PixelsPerUnit", Mathf.Max(1f, sprite.pixelsPerUnit));

            renderer.SetPropertyBlock(propertyBlock);
        }

        public static bool WritesOutline(string partName)
        {
            return !IsAntenna(partName) && !IsTail(partName);
        }

        public static bool IsBody(string partName)
        {
            return partName == "chest&body" || partName == "Body";
        }

        public static bool IsAntenna(string partName)
        {
            return partName == "Antenna"
                || partName == "chujiao-l"
                || partName == "chujiao-r"
                || partName.StartsWith("chujiao");
        }

        public static bool IsTail(string partName)
        {
            return partName == "weiba"
                || partName == "weiba-l"
                || partName == "weiba-r"
                || partName.StartsWith("weiba");
        }
    }
}
