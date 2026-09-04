using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 局内蛐蛐零件表现：身体描边、触角不描边。
    /// 根节点只负责朝向和缩放，零件以后换成骨骼时继续挂在这棵树下。
    /// </summary>
    public sealed class DouQuquCricketVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer antenna;
        [SerializeField] private Color allyOutline = new Color(0.22f, 0.92f, 0.82f, 1f);
        [SerializeField] private Color enemyOutline = new Color(0.95f, 0.25f, 0.22f, 1f);
        [SerializeField] private float outlineWidth = 20f;
        [SerializeField] private float outlineSoftness = 6f;

        private MaterialPropertyBlock propertyBlock;

        public SpriteRenderer BodyRenderer
        {
            get { return body; }
        }

        public SpriteRenderer AntennaRenderer
        {
            get { return antenna; }
        }

        /// <summary>只用身体外接尺寸对齐碰撞圆，避免触角大画布把整只虫缩得过小。</summary>
        public float VisualSize
        {
            get
            {
                if (body == null || body.sprite == null) return 1f;
                Vector3 size = body.sprite.bounds.size;
                return Mathf.Max(size.x, size.y);
            }
        }

        public void BindParts(SpriteRenderer bodyRenderer, SpriteRenderer antennaRenderer)
        {
            body = bodyRenderer;
            antenna = antennaRenderer;
        }

        /// <summary>只改身体描边色，不染色贴图，也不动触角。</summary>
        public void ApplyTeam(bool ally, bool charging)
        {
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
            if (antenna != null) antenna.color = Color.white;
        }
    }
}
