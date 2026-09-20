using UnityEngine;
using UnityEngine.Rendering;

namespace DouQuqu
{
    /// <summary>
    /// 蓄力指示：身后 strength 条（等比缩放 + 矩形窗口按长度揭示），身前加粗三角虚线 + 同尺寸描边落点。
    /// </summary>
    public sealed class ChargeArrow : MonoBehaviour
    {
        public const int MaxChevrons = 48;
        private const string TriangleResource = "Battle/Entities/Textures/go_triangle";
        private const string StrengthResource = "Battle/Entities/Textures/strength";

        [SerializeField] private Sprite fillSprite;
        [SerializeField] private Sprite chevronSprite;
        [SerializeField] private SpriteRenderer fill;
        [SerializeField] private LineRenderer endpoint;
        [SerializeField] private SpriteRenderer[] chevrons = new SpriteRenderer[MaxChevrons];
        [SerializeField] private float minDistance = 0.02f;

        private Sprite fallbackFill;
        private Sprite fallbackChevron;
        private Sprite runtimeTriangle;
        private Sprite runtimeStrength;
        private Sprite barWindow;
        private Texture barWindowTex;
        private float barWindowSpan = -1f;
        private Material spriteMaterial;
        private Material lineMaterial;

        public void Apply(
            bool charging,
            float distance,
            float fillAmount,
            Vector2 direction,
            Vector3 origin,
            float radius,
            Color playerColor,
            float barRatio = 3f,
            float alphaMin = 0.4f,
            float alphaMax = 1f)
        {
            EnsureReady();
            if (!charging || distance < minDistance || direction.sqrMagnitude < 0.0001f)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);
            Vector2 dir = direction.normalized;
            transform.position = origin;
            transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);

            float p = Mathf.Clamp01(fillAmount);
            float ratio = Mathf.Clamp(barRatio, 1.5f, 8f);
            float a0 = Mathf.Clamp01(alphaMin);
            float a1 = Mathf.Clamp01(alphaMax);
            if (a1 < a0)
            {
                float swap = a0;
                a0 = a1;
                a1 = swap;
            }

            float r = Mathf.Max(0.12f, radius);
            Color paint = playerColor;
            paint.a = 1f;
            LayoutBar(distance / ratio, r, new Color(paint.r, paint.g, paint.b, Mathf.Lerp(a0, a1, p)));
            LayoutDash(distance, r, new Color(paint.r, paint.g, paint.b, a1));
        }

        public void Hide()
        {
            if (fill != null) fill.enabled = false;
            if (endpoint != null) endpoint.enabled = false;
            if (chevrons != null)
                for (int i = 0; i < chevrons.Length; i++)
                    if (chevrons[i] != null) chevrons[i].enabled = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public void EnsureReady()
        {
            StripIncompatibleFill();
            StripNamedChild("BarMask");
            StripNamedChild("BarFill");
            if (fill == null) fill = CreateSpriteChild("Fill", UsableBarSprite(), 24);
            if (endpoint == null) endpoint = CreateEndpointRing();

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
            Sprite mark = UsableTriangleSprite();
            for (int i = 0; i < MaxChevrons; i++)
            {
                if (chevrons[i] != null)
                {
                    chevrons[i].maskInteraction = SpriteMaskInteraction.None;
                    continue;
                }
                Transform existing = folder.Find("Chevron_" + i);
                if (existing != null) chevrons[i] = existing.GetComponent<SpriteRenderer>();
                if (chevrons[i] == null) chevrons[i] = CreateSpriteChild("Chevron_" + i, mark, 26, folder);
                if (chevrons[i] != null) chevrons[i].maskInteraction = SpriteMaskInteraction.None;
            }
        }

        private void LayoutBar(float barLength, float radius, Color color)
        {
            Sprite band = UsableBarSprite();
            if (fill == null || band == null || barLength < 0.02f)
            {
                if (fill != null) fill.enabled = false;
                return;
            }

            float circleR = GroundMarker.CircleRadius(radius);
            float width = circleR * 2f;
            Vector3 size = band.bounds.size;
            float artLen = size.x / Mathf.Max(0.01f, size.y) * width;
            float visibleLen = Mathf.Min(barLength, Mathf.Max(0.01f, artLen));
            float uSpan = visibleLen / Mathf.Max(0.01f, artLen);
            Sprite window = BarWindow(band, uSpan);
            float zStart = -(circleR + visibleLen);
            float zEnd = -circleR;
            LayoutGroundSprite(
                fill,
                window,
                new Vector3(0f, 0.04f, LocalZForSpriteX(window, zStart, zEnd)),
                new Vector2(width, visibleLen),
                color,
                24,
                true);
        }

        /// <summary>
        /// Fill 上不能同时挂 SpriteRenderer 和 MeshRenderer。旧版往 Fill 加 Mesh 会直接抛错，蓄力条和虚线都画不出来。
        /// </summary>
        private void StripIncompatibleFill()
        {
            Transform child = transform.Find("Fill");
            if (child == null) return;
            MeshFilter filter = child.GetComponent<MeshFilter>();
            MeshRenderer mesh = child.GetComponent<MeshRenderer>();
            if (filter != null) Object.DestroyImmediate(filter);
            if (mesh != null) Object.DestroyImmediate(mesh);
            if (fill == null) fill = child.GetComponent<SpriteRenderer>();
        }

        private void StripNamedChild(string childName)
        {
            Transform leftover = transform.Find(childName);
            if (leftover == null) return;
            if (Application.isPlaying) Object.Destroy(leftover.gameObject);
            else Object.DestroyImmediate(leftover.gameObject);
        }

        private Sprite BarWindow(Sprite band, float uSpan)
        {
            uSpan = Mathf.Clamp(uSpan, 0.02f, 1f);
            Texture2D tex = band != null ? band.texture as Texture2D : null;
            if (tex == null) return band;
            if (barWindow != null && barWindowTex == tex && Mathf.Abs(barWindowSpan - uSpan) < 0.004f)
                return barWindow;

            barWindowTex = tex;
            barWindowSpan = uSpan;
            if (barWindow != null)
            {
                if (fill != null && fill.sprite == barWindow) fill.sprite = band;
                DestroyCompat(barWindow);
                barWindow = null;
            }

            Rect tr = band.textureRect;
            float cropW = Mathf.Max(1f, tr.width * uSpan);
            Rect crop = new Rect(tr.x, tr.y, cropW, tr.height);
            if (crop.xMax > tr.xMax) crop.width = tr.xMax - crop.x;
            barWindow = Sprite.Create(
                tex,
                crop,
                new Vector2(0f, 0.5f),
                Mathf.Max(1f, band.pixelsPerUnit),
                0,
                SpriteMeshType.FullRect);
            if (barWindow != null)
            {
                barWindow.name = "strength_window";
                barWindow.hideFlags = HideFlags.HideAndDontSave;
            }
            return barWindow != null ? barWindow : band;
        }

        private void LayoutDash(float distance, float radius, Color color)
        {
            Sprite mark = UsableTriangleSprite();
            float circleR = GroundMarker.CircleRadius(radius);
            LayoutEndpointRing(distance, circleR, GroundMarker.CircleStroke(radius), color);

            float triLen = Mathf.Clamp(radius * 0.34f, 0.22f, 0.5f) * 4.5f;
            float triWidth = triLen * 0.72f;
            float start = circleR;
            float stop = distance - circleR;
            int n = 0;
            if (mark != null && stop > start + triLen * 0.35f)
            {
                float span = stop - start;
                int count = Mathf.Clamp(Mathf.FloorToInt(span / (triLen * 1.15f * 0.6f)), 1, MaxChevrons);
                float step = span / count;
                for (; n < count; n++)
                {
                    float along = start + step * (n + 0.5f);
                    float z0 = along - triLen * 0.5f;
                    LayoutGroundSprite(
                        chevrons[n],
                        mark,
                        new Vector3(0f, 0.05f, LocalZForSpriteY(mark, z0, z0 + triLen)),
                        new Vector2(triWidth, triLen),
                        color,
                        26);
                }
            }
            for (int i = n; i < MaxChevrons; i++)
                if (chevrons[i] != null) chevrons[i].enabled = false;
        }

        private LineRenderer CreateEndpointRing()
        {
            Transform existing = transform.Find("Endpoint");
            GameObject child = existing != null ? existing.gameObject : new GameObject("Endpoint");
            if (existing == null) child.transform.SetParent(transform, false);

            SpriteRenderer leftover = child.GetComponent<SpriteRenderer>();
            if (leftover != null) Object.DestroyImmediate(leftover);

            LineRenderer line = child.GetComponent<LineRenderer>();
            if (line == null) line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.TransformZ;
            line.sortingOrder = 28;
            line.enabled = false;
            Material material = LineMaterial();
            if (material != null) line.sharedMaterial = material;
            child.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            child.transform.localPosition = Vector3.zero;
            child.transform.localScale = Vector3.one;
            return line;
        }

        private void LayoutEndpointRing(float distance, float circleR, float stroke, Color color)
        {
            if (endpoint == null || circleR < 0.01f)
            {
                if (endpoint != null) endpoint.enabled = false;
                return;
            }

            endpoint.enabled = true;
            endpoint.loop = true;
            endpoint.positionCount = GroundMarker.RingPoints;
            endpoint.startWidth = stroke;
            endpoint.endWidth = stroke;
            endpoint.startColor = color;
            endpoint.endColor = color;
            endpoint.sortingOrder = 28;
            endpoint.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            endpoint.transform.localPosition = new Vector3(0f, 0.06f, distance);
            endpoint.transform.localScale = Vector3.one;
            Material material = LineMaterial();
            if (material != null) endpoint.sharedMaterial = material;
            for (int i = 0; i < GroundMarker.RingPoints; i++)
            {
                float t = i / (float)GroundMarker.RingPoints * Mathf.PI * 2f;
                endpoint.SetPosition(i, new Vector3(circleR * Mathf.Sin(t), circleR * Mathf.Cos(t), 0f));
            }
        }

        private SpriteRenderer CreateSpriteChild(string childName, Sprite sprite, int sorting, Transform parent = null)
        {
            Transform existing = (parent != null ? parent : transform).Find(childName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(childName);
            if (existing == null) child.transform.SetParent(parent != null ? parent : transform, false);
            MeshFilter filter = child.GetComponent<MeshFilter>();
            MeshRenderer mesh = child.GetComponent<MeshRenderer>();
            if (filter != null) Object.DestroyImmediate(filter);
            if (mesh != null) Object.DestroyImmediate(mesh);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = child.AddComponent<SpriteRenderer>();
            ConfigureRenderer(renderer, sprite, sorting);
            renderer.enabled = false;
            child.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            child.transform.localPosition = Vector3.zero;
            child.transform.localScale = Vector3.one;
            return renderer;
        }

        private void LayoutGroundSprite(SpriteRenderer renderer, Sprite sprite, Vector3 localPos, Vector2 worldSize, Color color, int sorting, bool alongSpriteX = false)
        {
            if (renderer == null || sprite == null)
            {
                if (renderer != null) renderer.enabled = false;
                return;
            }
            ConfigureRenderer(renderer, sprite, sorting);
            renderer.maskInteraction = SpriteMaskInteraction.None;
            renderer.color = color;
            renderer.enabled = true;
            renderer.transform.localPosition = localPos;
            Vector3 size = sprite.bounds.size;
            if (alongSpriteX)
            {
                renderer.transform.localRotation = Quaternion.Euler(90f, 0f, -90f);
                float sx = worldSize.y / Mathf.Max(0.01f, size.x);
                float sy = worldSize.x / Mathf.Max(0.01f, size.y);
                renderer.transform.localScale = new Vector3(sx, sy, 1f);
            }
            else
            {
                renderer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                float sx = worldSize.x / Mathf.Max(0.01f, size.x);
                float sy = worldSize.y / Mathf.Max(0.01f, size.y);
                renderer.transform.localScale = new Vector3(sx, sy, 1f);
            }
        }

        private void ConfigureRenderer(SpriteRenderer renderer, Sprite sprite, int sorting)
        {
            renderer.sprite = sprite;
            renderer.sharedMaterial = SpriteMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sorting;
        }

        /// <summary>精灵躺在 XZ 上，本地 +Y 对准父节点 +Z。按 pivot 把 [zStart, zEnd] 对上。</summary>
        private static float LocalZForSpriteY(Sprite sprite, float zStart, float zEnd)
        {
            float len = zEnd - zStart;
            if (sprite == null || sprite.rect.height < 0.01f) return (zStart + zEnd) * 0.5f;
            float pivotY = Mathf.Clamp01(sprite.pivot.y / sprite.rect.height);
            return zStart + pivotY * len;
        }

        /// <summary>strength 沿精灵 +X，Euler(90,0,-90) 后 +X 朝向本地 -Z。pivot 0 贴 zEnd（圆后沿）。</summary>
        private static float LocalZForSpriteX(Sprite sprite, float zStart, float zEnd)
        {
            float len = zEnd - zStart;
            if (sprite == null || sprite.rect.width < 0.01f) return (zStart + zEnd) * 0.5f;
            float pivotX = Mathf.Clamp01(sprite.pivot.x / sprite.rect.width);
            return zEnd - pivotX * len;
        }

        private Sprite UsableBarSprite()
        {
            if (IsNamed(fillSprite, "strength")) return fillSprite;
            Sprite loaded = Resources.Load<Sprite>(StrengthResource);
            if (loaded != null) return loaded;
            Texture2D texture = Resources.Load<Texture2D>(StrengthResource);
            if (texture != null)
            {
                if (runtimeStrength == null || runtimeStrength.texture != texture)
                    runtimeStrength = MakeSprite(texture, new Vector2(0.5f, 0.5f), "strength");
                return runtimeStrength;
            }
            if (fillSprite != null && fillSprite.bounds.size.sqrMagnitude > 0.0001f) return fillSprite;
            return FallbackFill();
        }

        private Sprite UsableTriangleSprite()
        {
            if (IsNamed(chevronSprite, "go_triangle")) return chevronSprite;
            Sprite loaded = Resources.Load<Sprite>(TriangleResource);
            if (loaded != null) return loaded;
            Texture2D texture = Resources.Load<Texture2D>(TriangleResource);
            if (texture != null)
            {
                if (runtimeTriangle == null || runtimeTriangle.texture != texture)
                    runtimeTriangle = MakeSprite(texture, new Vector2(0.5f, 0.5f), "go_triangle");
                return runtimeTriangle;
            }
            if (chevronSprite != null && chevronSprite.bounds.size.sqrMagnitude > 0.0001f) return chevronSprite;
            return FallbackChevron();
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
            fallbackChevron = Sprite.Create(WhiteTexture(), new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
            fallbackChevron.name = "ChargeChevronFallback";
            return fallbackChevron;
        }

        private static Sprite MakeSprite(Texture2D texture, Vector2 pivot, string spriteName)
        {
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                pivot,
                100f);
            sprite.name = spriteName;
            return sprite;
        }

        private static bool IsNamed(Sprite sprite, string token)
        {
            if (sprite == null || sprite.bounds.size.sqrMagnitude < 0.0001f) return false;
            if (sprite.name != null && sprite.name.IndexOf(token) >= 0) return true;
            return sprite.texture != null && sprite.texture.name != null && sprite.texture.name.IndexOf(token) >= 0;
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

        private void OnDestroy()
        {
            if (barWindow != null)
            {
                DestroyCompat(barWindow);
                barWindow = null;
            }
        }

        private static void DestroyCompat(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        private Material SpriteMaterial()
        {
            if (spriteMaterial != null) return spriteMaterial;
            spriteMaterial = MakeUnlitMaterial("ChargeArrowSprite");
            return spriteMaterial;
        }

        private Material LineMaterial()
        {
            if (lineMaterial != null) return lineMaterial;
            lineMaterial = MakeUnlitMaterial("ChargeArrowLine");
            return lineMaterial;
        }

        private static Material MakeUnlitMaterial(string materialName)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return null;
            Material material = new Material(shader);
            material.name = materialName;
            return material;
        }
    }
}
