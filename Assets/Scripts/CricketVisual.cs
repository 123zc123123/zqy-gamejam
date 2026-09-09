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
                ApplyOutlineBlock(renderer, outlineColor);
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
                ApplyOutlineBlock(parts[i], outlineColor);
            }
        }

        /// <summary>全员黑描边，不染色贴图。队伍色走脚下圈。</summary>
        public void ApplyTeam(bool ally, bool charging)
        {
            if (parts == null || parts.Length == 0) BindHierarchy();
            if (parts == null || parts.Length == 0) return;

            _ = ally;
            _ = charging;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

            for (int i = 0; i < parts.Length; i++)
            {
                SpriteRenderer renderer = parts[i];
                if (renderer == null) continue;
                ApplyOutlineBlock(renderer, outlineColor);
                renderer.color = Color.white;
            }
        }

        private void ApplyOutlineBlock(SpriteRenderer renderer, Color outline)
        {
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_OutlineColor", outline);
            propertyBlock.SetFloat("_OutlineWidth", WritesOutline(renderer.name) ? outlineWidth : 0f);
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
