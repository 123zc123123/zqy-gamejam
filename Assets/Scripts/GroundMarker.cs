using UnityEngine;
using UnityEngine.Rendering;

namespace DouQuqu
{
    /// <summary>
    /// 蟋蟀脚下的玩家色圆圈和落地震影。表现层，不参与碰撞 / 出圈。
    /// 预制体里圆面朝前，进战斗再躺到地面上。
    /// </summary>
    [ExecuteAlways]
    public sealed class GroundMarker : MonoBehaviour
    {
        /// <summary>与战斗 HUD Player1–4 头像底色同一套：棕、红、绿、蓝。</summary>
        public static readonly Color[] PlayerColors =
        {
            new Color(0.90f, 0.68f, 0.08f, 1f),
            new Color(0.62f, 0.17f, 0.17f, 1f),
            new Color(0.18f, 0.35f, 0.15f, 1f),
            new Color(0.43f, 0.53f, 0.87f, 1f)
        };

        public static void SyncFromHud(Transform hudRoot)
        {
            if (hudRoot == null) return;
            for (int i = 0; i < PlayerColors.Length; i++)
            {
                Transform card = FindNamed(hudRoot, "Player" + (i + 1));
                if (card == null) card = FindNamed(hudRoot, "FigmaPlayer" + (i + 1));
                if (card == null) continue;
                UnityEngine.UI.Image plate = card.GetComponent<UnityEngine.UI.Image>();
                if (plate == null) continue;
                Color c = plate.color;
                c.a = 1f;
                PlayerColors[i] = c;
            }
        }

        static Transform FindNamed(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamed(root.GetChild(i), objectName);
                if (hit != null) return hit;
            }
            return null;
        }

        private const int RingPoints = 48;
        private const float PreviewRadius = 0.5f;

        [SerializeField] private Sprite fillSprite;
        [SerializeField] private SpriteRenderer shadow;
        [SerializeField] private SpriteRenderer fill;
        [SerializeField] private LineRenderer ring;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private float ringScale = 1.38f;
        [SerializeField] private float ringWidthScale = 0.18f;
        [SerializeField] private float shadowScale = 1.08f;
        [SerializeField] private float shadowOffsetScale = 0.22f;
        [SerializeField] private float heightOffset = 0.03f;

        private static Sprite discSprite;
        private Material spriteMaterial;

        public static Color ColorForPlayer(int playerId)
        {
            int index = Mathf.Abs(playerId) % PlayerColors.Length;
            return PlayerColors[index];
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) Preview();
        }

        public void Apply(Vector3 worldCenter, float bugRadius, Color playerColor, float height = 0f, bool charging = false)
        {
            EnsureReady();
            gameObject.SetActive(true);
            transform.position = new Vector3(worldCenter.x, heightOffset, worldCenter.z);
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            float lift = 1f / (1f + Mathf.Max(0f, height) * 0.55f);
            float radius = Mathf.Max(0.2f, bugRadius);
            Color paint = charging ? Color.Lerp(playerColor, Color.white, 0.28f) : playerColor;
            LayoutMarker(radius, lift, paint, charging ? 0.34f : 0.22f);
        }

        /// <summary>预制体 / 编辑器里正对镜头的预览，不躺到地面。</summary>
        public void Preview()
        {
            EnsureReady();
            transform.rotation = Quaternion.identity;
            Color paint = PlayerColors[0];
            LayoutMarker(PreviewRadius, 1f, paint, 0.45f);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public void EnsureReady()
        {
            if (shadow == null) shadow = CreateSprite("Shadow", -22);
            if (fill == null) fill = CreateSprite("Fill", -21);
            if (ring == null) ring = CreateRing("Ring", -20);
            Sprite disc = UsableDisc();
            if (shadow != null) shadow.sprite = disc;
            if (fill != null) fill.sprite = disc;
        }

        private void LayoutMarker(float radius, float lift, Color paint, float fillAlpha)
        {
            Sprite disc = UsableDisc();
            float ringRadius = radius * ringScale;
            Layout(shadow, disc, radius * shadowScale * lift, new Color(0.02f, 0.02f, 0.02f, 0.55f * lift), -22);
            if (shadow != null)
                shadow.transform.localPosition = new Vector3(0f, -radius * shadowOffsetScale * lift, 0f);
            Layout(fill, disc, ringRadius, new Color(paint.r, paint.g, paint.b, fillAlpha), -21);
            LayoutRing(ringRadius, new Color(paint.r, paint.g, paint.b, 0.94f), ringRadius * ringWidthScale);
        }

        private Sprite UsableDisc()
        {
            if (fillSprite != null && fillSprite.bounds.size.sqrMagnitude > 0.0001f) return fillSprite;
            return Disc();
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
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localPosition = Vector3.zero;
            Vector3 size = sprite.bounds.size;
            float diameter = worldRadius * 2f;
            renderer.transform.localScale = new Vector3(
                diameter / Mathf.Max(0.01f, size.x),
                diameter / Mathf.Max(0.01f, size.y),
                1f);
        }

        private void LayoutRing(float worldRadius, Color color, float width)
        {
            if (ring == null || worldRadius < 0.01f)
            {
                if (ring != null) ring.enabled = false;
                return;
            }

            ring.enabled = true;
            ring.loop = true;
            ring.positionCount = RingPoints;
            ring.startWidth = width;
            ring.endWidth = width;
            ring.startColor = color;
            ring.endColor = color;
            ring.sortingOrder = -20;
            ring.transform.localPosition = Vector3.zero;
            ring.transform.localRotation = Quaternion.identity;
            ring.transform.localScale = Vector3.one;
            for (int i = 0; i < RingPoints; i++)
            {
                float t = i / (float)RingPoints * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(worldRadius * Mathf.Sin(t), worldRadius * Mathf.Cos(t), 0f));
            }
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
            child.transform.localRotation = Quaternion.identity;
            child.transform.localPosition = Vector3.zero;
            child.transform.localScale = Vector3.one;
            return renderer;
        }

        private LineRenderer CreateRing(string childName, int sorting)
        {
            Transform existing = transform.Find(childName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(childName);
            if (existing == null) child.transform.SetParent(transform, false);
            SpriteRenderer leftover = child.GetComponent<SpriteRenderer>();
            if (leftover != null)
            {
                if (Application.isPlaying) Object.Destroy(leftover);
                else Object.DestroyImmediate(leftover);
            }

            LineRenderer line = child.GetComponent<LineRenderer>();
            if (line == null) line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.TransformZ;
            line.sortingOrder = sorting;
            line.enabled = false;
            Material material = LineMaterial();
            if (material != null) line.sharedMaterial = material;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localPosition = Vector3.zero;
            child.transform.localScale = Vector3.one;
            return line;
        }

        private static Sprite Disc()
        {
            if (discSprite != null) return discSprite;
            const int pixels = 128;
            Texture2D texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
            texture.name = "GroundFillTex";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;

            Color[] colors = new Color[pixels * pixels];
            float center = (pixels - 1) * 0.5f;
            float inv = 1f / Mathf.Max(0.001f, center);
            const float softness = 0.045f;
            for (int y = 0; y < pixels; y++)
            {
                for (int x = 0; x < pixels; x++)
                {
                    float dx = (x - center) * inv;
                    float dy = (y - center) * inv;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((1f - r) / softness);
                    colors[y * pixels + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(colors);
            texture.Apply(false, false);
            discSprite = Sprite.Create(texture, new Rect(0f, 0f, pixels, pixels), new Vector2(0.5f, 0.5f), pixels);
            discSprite.name = "GroundFill";
            discSprite.hideFlags = HideFlags.HideAndDontSave;
            return discSprite;
        }

        private Material SpriteMaterial()
        {
            if (spriteMaterial != null) return spriteMaterial;
            spriteMaterial = MakeUnlit();
            return spriteMaterial;
        }

        private Material LineMaterial()
        {
            if (lineMaterial != null) return lineMaterial;
            lineMaterial = MakeUnlit();
            return lineMaterial;
        }

        private static Material MakeUnlit()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            return shader != null ? new Material(shader) : null;
        }
    }
}
