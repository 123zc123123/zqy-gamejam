using UnityEngine;
using UnityEngine.Rendering;

namespace DouQuqu
{
    /// <summary>
    /// 身下分格耐力条。表现层预制体，不参与碰撞 / 出圈。旧圆环可同时保留。
    /// </summary>
    public sealed class DouQuquStaminaBar : MonoBehaviour
    {
        public const int MaxSlots = 8;

        [SerializeField] private Color okColor = new Color(0.48f, 0.84f, 0.64f, 0.95f);
        [SerializeField] private Color warnColor = new Color(0.90f, 0.70f, 0.31f, 0.95f);
        [SerializeField] private Color lowColor = new Color(0.88f, 0.35f, 0.27f, 0.95f);
        [SerializeField] private Color trackColor = new Color(0.08f, 0.12f, 0.11f, 0.72f);
        [SerializeField] private Color plateColor = new Color(0.05f, 0.07f, 0.07f, 0.55f);
        [SerializeField] private float widthScale = 2.2f;
        [SerializeField] private float thicknessScale = 0.22f;
        [SerializeField] private float belowScale = 1.18f;
        [SerializeField] private float minWidth = 1.2f;
        [SerializeField] private float minThickness = 0.12f;
        [SerializeField] private float minBelow = 0.55f;
        [SerializeField] private float heightOffset = 0.1f;
        [SerializeField] private float gap = 0.12f;
        [SerializeField] private float pendingAlpha = 0.4f;
        [SerializeField] private SpriteRenderer plate;
        [SerializeField] private SpriteRenderer[] tracks = new SpriteRenderer[MaxSlots];
        [SerializeField] private SpriteRenderer[] fills = new SpriteRenderer[MaxSlots];
        [SerializeField] private SpriteRenderer[] previews = new SpriteRenderer[MaxSlots];

        private Sprite whiteSprite;
        private Material spriteMaterial;

        /// <summary>
        /// 实心 = 当前耐力。蓄力时 pendingRatio 是本次将扣的比例，画在当前值往回的半透明段。
        /// </summary>
        public void Apply(float currentRatio, int slots, Vector3 worldCenter, float bugRadius, float pendingRatio = 0f)
        {
            EnsureReady();
            gameObject.SetActive(true);
            currentRatio = Mathf.Clamp01(currentRatio);
            pendingRatio = Mathf.Clamp(pendingRatio, 0f, currentRatio);
            float remainRatio = Mathf.Max(0f, currentRatio - pendingRatio);
            slots = Mathf.Clamp(slots, 3, MaxSlots);

            float width = Mathf.Max(minWidth, bugRadius * widthScale);
            float thickness = Mathf.Max(minThickness, bugRadius * thicknessScale);
            float below = Mathf.Max(minBelow, bugRadius * belowScale);
            transform.position = worldCenter + Vector3.up * heightOffset + Vector3.back * below;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Color color = currentRatio <= 0.2f ? lowColor : (currentRatio <= 0.4f ? warnColor : okColor);
            Color ghost = new Color(color.r, color.g, color.b, color.a * pendingAlpha);
            float gapW = Mathf.Max(0.02f, thickness * gap);
            float slotW = (width - gapW * (slots - 1)) / slots;
            float origin = -width * 0.5f;

            Layout(plate, Vector3.zero, new Vector2(width + thickness * 0.45f, thickness + thickness * 0.55f), plateColor, 28);
            for (int i = 0; i < MaxSlots; i++)
            {
                bool on = i < slots;
                if (tracks[i] != null) tracks[i].enabled = on;
                if (fills[i] != null) fills[i].enabled = false;
                if (previews[i] != null) previews[i].enabled = false;
                if (!on) continue;

                float left = origin + i * (slotW + gapW);
                float centerX = left + slotW * 0.5f;
                Layout(tracks[i], new Vector3(centerX, 0f, 0f), new Vector2(slotW, thickness), trackColor, 29);

                float remainFill = Mathf.Clamp01(remainRatio * slots - i);
                float currentFill = Mathf.Clamp01(currentRatio * slots - i);
                if (currentFill > remainFill + 0.001f)
                {
                    float pendingW = slotW * (currentFill - remainFill);
                    float pendingX = left + slotW * remainFill + pendingW * 0.5f;
                    Layout(previews[i], new Vector3(pendingX, 0f, 0f), new Vector2(pendingW, thickness), ghost, 30);
                }
                if (remainFill > 0.001f)
                {
                    float fillW = slotW * remainFill;
                    float fillX = left + fillW * 0.5f;
                    Layout(fills[i], new Vector3(fillX, 0f, 0f), new Vector2(fillW, thickness), color, 31);
                }
            }
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>预制体可预置子物体；缺省时在实例上补齐，不改预制体资产。</summary>
        public void EnsureReady()
        {
            if (tracks == null || tracks.Length != MaxSlots) tracks = new SpriteRenderer[MaxSlots];
            if (fills == null || fills.Length != MaxSlots) fills = new SpriteRenderer[MaxSlots];
            if (previews == null || previews.Length != MaxSlots) previews = new SpriteRenderer[MaxSlots];
            if (plate == null) plate = CreateSprite("Plate", 28);
            for (int i = 0; i < MaxSlots; i++)
            {
                if (tracks[i] == null) tracks[i] = CreateSprite("Track_" + i, 29);
                if (previews[i] == null) previews[i] = CreateSprite("Preview_" + i, 30);
                if (fills[i] == null) fills[i] = CreateSprite("Fill_" + i, 31);
            }
        }

        private void Layout(SpriteRenderer renderer, Vector3 localPos, Vector2 worldSize, Color color, int sorting)
        {
            if (renderer == null)
                return;
            if (worldSize.x < 0.001f || worldSize.y < 0.001f)
            {
                renderer.enabled = false;
                return;
            }

            Sprite sprite = WhiteSprite();
            renderer.sprite = sprite;
            renderer.sharedMaterial = SpriteMaterial();
            renderer.color = color;
            renderer.sortingOrder = sorting;
            renderer.enabled = true;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localPosition = localPos;
            Vector3 size = sprite.bounds.size;
            renderer.transform.localScale = new Vector3(
                worldSize.x / Mathf.Max(0.01f, size.x),
                worldSize.y / Mathf.Max(0.01f, size.y),
                1f);
        }

        private SpriteRenderer CreateSprite(string childName, int sorting)
        {
            Transform existing = transform.Find(childName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(childName);
            if (existing == null) child.transform.SetParent(transform, false);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = WhiteSprite();
            renderer.sharedMaterial = SpriteMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sorting;
            renderer.enabled = false;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localPosition = Vector3.zero;
            child.transform.localScale = Vector3.one;
            return renderer;
        }

        private Sprite WhiteSprite()
        {
            if (whiteSprite != null) return whiteSprite;
            Texture2D texture = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            whiteSprite.name = "StaminaBarWhite";
            return whiteSprite;
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
