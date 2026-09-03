using UnityEngine;
using UnityEngine.Rendering;

namespace DouQuqu
{
    /// <summary>
    /// 身周分格耐力环。表现层预制体，不参与碰撞 / 出圈。
    /// </summary>
    public sealed class DouQuquStaminaRing : MonoBehaviour
    {
        public const int MaxSlots = 8;

        [SerializeField] private Color okColor = new Color(0.48f, 0.84f, 0.64f, 0.95f);
        [SerializeField] private Color warnColor = new Color(0.90f, 0.70f, 0.31f, 0.95f);
        [SerializeField] private Color lowColor = new Color(0.88f, 0.35f, 0.27f, 0.95f);
        [SerializeField] private Color trackColor = new Color(0.08f, 0.12f, 0.11f, 0.48f);
        [SerializeField] private float radiusScale = 1.52f;
        [SerializeField] private float widthScale = 0.12f;
        [SerializeField] private float minWidth = 0.08f;
        [SerializeField] private float minRadius = 0.4f;
        [SerializeField] private float heightOffset = 0.08f;
        [SerializeField] private float gap = 0.1f;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private float pendingAlpha = 0.4f;
        [SerializeField] private LineRenderer[] tracks = new LineRenderer[MaxSlots];
        [SerializeField] private LineRenderer[] fills = new LineRenderer[MaxSlots];
        [SerializeField] private LineRenderer[] previews = new LineRenderer[MaxSlots];

        /// <summary>
        /// 实心 = 当前耐力。蓄力时 pendingRatio 是本次将扣的比例，画在当前值往回的半透明段。
        /// </summary>
        public void Apply(float currentRatio, int slots, Vector3 worldCenter, float bugRadius, float pendingRatio = 0f)
        {
            EnsureReady();
            gameObject.SetActive(true);
            transform.position = worldCenter + Vector3.up * heightOffset;
            currentRatio = Mathf.Clamp01(currentRatio);
            pendingRatio = Mathf.Clamp(pendingRatio, 0f, currentRatio);
            float remainRatio = Mathf.Max(0f, currentRatio - pendingRatio);
            slots = Mathf.Clamp(slots, 3, MaxSlots);
            Color color = currentRatio <= 0.2f ? lowColor : (currentRatio <= 0.4f ? warnColor : okColor);
            Color ghost = new Color(color.r, color.g, color.b, color.a * pendingAlpha);
            float radius = Mathf.Max(minRadius, bugRadius * radiusScale);
            float width = Mathf.Max(minWidth, bugRadius * widthScale);
            float span = Mathf.PI * 2f / slots;
            for (int i = 0; i < MaxSlots; i++)
            {
                bool on = i < slots;
                if (tracks[i] != null) tracks[i].enabled = on;
                if (fills[i] != null) fills[i].enabled = false;
                if (previews[i] != null) previews[i].enabled = false;
                if (!on) continue;
                float start = i * span + gap;
                float end = (i + 1) * span - gap;
                float arc = end - start;
                SetArc(tracks[i], transform.position, radius, start, end, trackColor, width);
                float remainFill = Mathf.Clamp01(remainRatio * slots - i);
                float currentFill = Mathf.Clamp01(currentRatio * slots - i);
                if (currentFill > remainFill + 0.001f)
                    SetArc(previews[i], transform.position, radius, start + arc * remainFill, start + arc * currentFill, ghost, width);
                if (remainFill > 0.001f)
                    SetArc(fills[i], transform.position, radius, start, start + arc * remainFill, color, width);
            }
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>预制体可预置子物体；缺省时在实例上补齐，不改预制体资产。</summary>
        public void EnsureReady()
        {
            if (tracks == null || tracks.Length != MaxSlots) tracks = new LineRenderer[MaxSlots];
            if (fills == null || fills.Length != MaxSlots) fills = new LineRenderer[MaxSlots];
            if (previews == null || previews.Length != MaxSlots) previews = new LineRenderer[MaxSlots];
            Material material = LineMaterial();
            for (int i = 0; i < MaxSlots; i++)
            {
                if (tracks[i] == null) tracks[i] = CreateLine("Track_" + i, material);
                if (previews[i] == null) previews[i] = CreateLine("Preview_" + i, material);
                if (fills[i] == null) fills[i] = CreateLine("Fill_" + i, material);
                tracks[i].sortingOrder = 20;
                previews[i].sortingOrder = 21;
                fills[i].sortingOrder = 22;
            }
        }

        private LineRenderer CreateLine(string childName, Material material)
        {
            Transform existing = transform.Find(childName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(childName);
            if (existing == null) child.transform.SetParent(transform, false);
            LineRenderer line = child.GetComponent<LineRenderer>();
            if (line == null) line = child.AddComponent<LineRenderer>();
            ConfigureLine(line, material);
            line.enabled = false;
            return line;
        }

        private Material LineMaterial()
        {
            if (lineMaterial != null) return lineMaterial;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            lineMaterial = new Material(shader);
            return lineMaterial;
        }

        private static void ConfigureLine(LineRenderer line, Material material)
        {
            line.useWorldSpace = true;
            line.loop = false;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            if (material != null) line.sharedMaterial = material;
        }

        private static void SetArc(LineRenderer line, Vector3 center, float radius, float start, float end, Color color, float width)
        {
            if (line == null || end - start < 0.001f)
            {
                if (line != null) line.enabled = false;
                return;
            }
            int count = Mathf.Max(2, Mathf.CeilToInt(28f * (end - start) / (Mathf.PI * 2f)));
            line.enabled = true;
            line.positionCount = count;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            for (int i = 0; i < count; i++)
            {
                float t = Mathf.Lerp(start, end, count == 1 ? 0f : i / (float)(count - 1));
                line.SetPosition(i, new Vector3(center.x + radius * Mathf.Sin(t), center.y, center.z + radius * Mathf.Cos(t)));
            }
        }
    }
}
