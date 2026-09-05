using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 局内蛐蛐表现：根节点只负责朝向和缩放。
    /// 兼容旧 Body/Antenna 两件套，也兼容 defaultCrickets 骨骼层级。
    /// 描边只打在身体上，触角不描边。
    /// </summary>
    public sealed class DouQuquCricketVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer antenna;
        [SerializeField] private SpriteRenderer[] antennae;
        [SerializeField] private Color allyOutline = new Color(0.22f, 0.92f, 0.82f, 1f);
        [SerializeField] private Color enemyOutline = new Color(0.95f, 0.25f, 0.22f, 1f);
        [SerializeField] private float outlineWidth = 20f;
        [SerializeField] private float outlineSoftness = 6f;

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

        public void BindParts(SpriteRenderer bodyRenderer, SpriteRenderer antennaRenderer)
        {
            body = bodyRenderer;
            antenna = antennaRenderer;
            if (antennaRenderer != null)
                antennae = new[] { antennaRenderer };
        }

        public void BindHierarchy()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            int antennaCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (IsAntenna(renderers[i].name)) antennaCount++;
            }

            SpriteRenderer[] foundAntennae = antennaCount > 0 ? new SpriteRenderer[antennaCount] : System.Array.Empty<SpriteRenderer>();
            int antennaIndex = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                string partName = renderer.name;
                if (body == null && IsBody(partName))
                    body = renderer;
                if (IsAntenna(partName))
                {
                    foundAntennae[antennaIndex++] = renderer;
                    if (antenna == null) antenna = renderer;
                }
            }

            if (foundAntennae.Length > 0) antennae = foundAntennae;
            if (body == null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (!IsAntenna(renderers[i].name))
                    {
                        body = renderers[i];
                        break;
                    }
                }
            }
        }

        /// <summary>只改身体描边色，不染色贴图，也不动触角。</summary>
        public void ApplyTeam(bool ally, bool charging)
        {
            if (body == null) BindHierarchy();
            if (body == null) return;
            Color outline = ally ? allyOutline : enemyOutline;
            if (charging) outline = Color.Lerp(outline, Color.white, 0.22f);
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            body.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_OutlineColor", outline);
            propertyBlock.SetFloat("_OutlineWidth", outlineWidth);
            propertyBlock.SetFloat("_OutlineSoftness", outlineSoftness);
            body.SetPropertyBlock(propertyBlock);
            body.color = Color.white;
            if (antennae != null)
            {
                for (int i = 0; i < antennae.Length; i++)
                {
                    if (antennae[i] != null) antennae[i].color = Color.white;
                }
            }
            else if (antenna != null)
            {
                antenna.color = Color.white;
            }
        }

        private static bool IsBody(string partName)
        {
            return partName == "chest&body" || partName == "Body";
        }

        private static bool IsAntenna(string partName)
        {
            return partName == "Antenna"
                || partName == "chujiao-l"
                || partName == "chujiao-r"
                || partName.StartsWith("chujiao");
        }
    }
}
