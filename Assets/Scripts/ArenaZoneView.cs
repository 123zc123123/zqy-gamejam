using UnityEngine;
using UnityEngine.Rendering;

namespace DouQuqu
{
    /// <summary>当前有效区边 + 预告环带。收口时环带一起渐隐，轮廓不是出局边。</summary>
    public sealed class ArenaZoneView : MonoBehaviour
    {
        private const int CornerSegments = 10;
        private LineRenderer currentEdge;
        private LineRenderer warnEdge;
        private MeshFilter fillFilter;
        private MeshRenderer fillRenderer;
        private Mesh fillMesh;
        private Material lineMaterial;
        private Material fillMaterial;
        private int lastTier = -1;
        private float lastElapsed;
        private float fadeStartElapsed = -1f;
        private float fadeDuration;
        private float fadeOuterW;
        private float fadeOuterD;
        private float fadeOuterC;
        private float fadeInnerW;
        private float fadeInnerD;
        private float fadeInnerC;

        public static ArenaZoneView Ensure()
        {
            ArenaZoneView view = FindObjectOfType<ArenaZoneView>();
            if (view != null) return view;
            GameObject go = new GameObject("ArenaZoneView");
            view = go.AddComponent<ArenaZoneView>();
            return view;
        }

        public void Refresh(MatchKnobs knobs, float elapsed, bool schedule)
        {
            EnsureLines();
            DrawRoundedRect(currentEdge, Rules.ArenaHalfWidth, Rules.ArenaHalfDepth, Rules.ArenaCorner, new Color(1f, 0.92f, 0.45f, 0.95f), 0.42f);
            currentEdge.enabled = true;

            if (elapsed + 0.05f < lastElapsed)
            {
                lastTier = -1;
                fadeStartElapsed = -1f;
            }

            int tier = schedule ? Rules.ZoneTierAt(knobs, elapsed) : 3;
            if (lastTier >= 0 && tier > lastTier)
                BeginFade(knobs, lastTier, tier, elapsed);
            lastTier = tier;
            lastElapsed = elapsed;

            bool warn = schedule && Rules.IsZoneWarn(knobs, elapsed);
            if (warn)
            {
                int next = Rules.ZoneWarnTier(knobs, elapsed);
                float scale = Rules.ZoneScaleOf(knobs, next);
                float innerW = Rules.DefaultArenaHalfWidth * scale;
                float innerD = Rules.DefaultArenaHalfDepth * scale;
                float innerC = Rules.DefaultArenaCorner * scale;
                DrawRoundedRect(warnEdge, innerW, innerD, innerC, new Color(1f, 0.95f, 0.85f, 0.85f), 0.28f);
                warnEdge.enabled = true;
                float pulse = 0.48f + 0.36f * (0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.PI * 4f));
                DrawFill(
                    Rules.ArenaHalfWidth, Rules.ArenaHalfDepth, Rules.ArenaCorner,
                    innerW, innerD, innerC,
                    new Color(0.96f, 0.08f, 0.06f, pulse));
            }
            else
            {
                warnEdge.enabled = false;
                if (fadeStartElapsed < 0f) HideFill();
            }

            if (fadeStartElapsed >= 0f)
            {
                float u = fadeDuration <= 0f ? 1f : Mathf.Clamp01((elapsed - fadeStartElapsed) / fadeDuration);
                float alpha = 0.72f * (1f - u);
                if (alpha <= 0.01f) HideFill();
                else
                {
                    DrawFill(
                        fadeOuterW, fadeOuterD, fadeOuterC,
                        fadeInnerW, fadeInnerD, fadeInnerC,
                        new Color(0.96f, 0.08f, 0.06f, alpha));
                }
            }
        }

        private void BeginFade(MatchKnobs knobs, int fromTier, int toTier, float elapsed)
        {
            fadeDuration = knobs != null ? Mathf.Max(0f, knobs.zoneFadeT) : 0.5f;
            fadeStartElapsed = elapsed;
            float outer = Rules.ZoneScaleOf(knobs, fromTier);
            float inner = Rules.ZoneScaleOf(knobs, toTier);
            fadeOuterW = Rules.DefaultArenaHalfWidth * outer;
            fadeOuterD = Rules.DefaultArenaHalfDepth * outer;
            fadeOuterC = Rules.DefaultArenaCorner * outer;
            fadeInnerW = Rules.DefaultArenaHalfWidth * inner;
            fadeInnerD = Rules.DefaultArenaHalfDepth * inner;
            fadeInnerC = Rules.DefaultArenaCorner * inner;
            if (fadeDuration <= 0f) fadeStartElapsed = -1f;
        }

        private void EnsureLines()
        {
            if (lineMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                lineMaterial = new Material(shader) { name = "ArenaZoneLine" };
            }

            if (fillMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                fillMaterial = new Material(shader) { name = "ArenaZoneFill" };
                fillMaterial.mainTexture = Texture2D.whiteTexture;
                fillMaterial.color = Color.white;
            }

            if (currentEdge == null) currentEdge = CreateLine("CurrentEdge", 20);
            if (warnEdge == null) warnEdge = CreateLine("WarnEdge", 21);
            if (fillFilter == null)
            {
                Transform existing = transform.Find("WarnFill");
                GameObject child = existing != null ? existing.gameObject : new GameObject("WarnFill");
                if (existing == null) child.transform.SetParent(transform, false);
                fillFilter = child.GetComponent<MeshFilter>();
                if (fillFilter == null) fillFilter = child.AddComponent<MeshFilter>();
                fillRenderer = child.GetComponent<MeshRenderer>();
                if (fillRenderer == null) fillRenderer = child.AddComponent<MeshRenderer>();
                fillRenderer.sharedMaterial = fillMaterial;
                fillRenderer.shadowCastingMode = ShadowCastingMode.Off;
                fillRenderer.receiveShadows = false;
                fillRenderer.sortingOrder = 8;
                fillMesh = new Mesh { name = "ArenaZoneFill" };
                fillFilter.sharedMesh = fillMesh;
                fillRenderer.enabled = false;
            }
        }

        private LineRenderer CreateLine(string childName, int sorting)
        {
            Transform existing = transform.Find(childName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(childName);
            if (existing == null) child.transform.SetParent(transform, false);
            LineRenderer line = child.GetComponent<LineRenderer>();
            if (line == null) line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.useWorldSpace = true;
            line.loop = true;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.sortingOrder = sorting;
            line.enabled = false;
            return line;
        }

        private void DrawFill(
            float outerW, float outerD, float outerC,
            float innerW, float innerD, float innerC,
            Color color)
        {
            if (fillMesh == null || fillRenderer == null) return;
            if (innerW >= outerW - 0.05f || innerD >= outerD - 0.05f)
            {
                HideFill();
                return;
            }

            Vector3[] outer = RoundedRectPoints(outerW, outerD, outerC, 0.03f);
            Vector3[] inner = RoundedRectPoints(innerW, innerD, innerC, 0.03f);
            int n = outer.Length;
            var verts = new Vector3[n * 2];
            var colors = new Color[n * 2];
            var tris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                verts[i] = outer[i];
                verts[n + i] = inner[i];
                colors[i] = color;
                colors[n + i] = color;
                int next = (i + 1) % n;
                int t = i * 6;
                tris[t] = i;
                tris[t + 1] = n + next;
                tris[t + 2] = next;
                tris[t + 3] = i;
                tris[t + 4] = n + i;
                tris[t + 5] = n + next;
            }

            fillMesh.Clear();
            fillMesh.vertices = verts;
            fillMesh.colors = colors;
            fillMesh.triangles = tris;
            fillMesh.RecalculateBounds();
            fillRenderer.enabled = true;
        }

        private void HideFill()
        {
            if (fillRenderer != null) fillRenderer.enabled = false;
            fadeStartElapsed = -1f;
        }

        private static void DrawRoundedRect(LineRenderer line, float halfW, float halfD, float corner, Color color, float width)
        {
            if (line == null) return;
            Vector3[] points = RoundedRectPoints(halfW, halfD, corner, 0.04f);
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
        }

        private static Vector3[] RoundedRectPoints(float halfW, float halfD, float corner, float y)
        {
            float cr = Mathf.Clamp(corner, 0.01f, Mathf.Min(halfW, halfD) * 0.95f);
            Vector2[] centers =
            {
                new Vector2(halfW - cr, halfD - cr),
                new Vector2(-(halfW - cr), halfD - cr),
                new Vector2(-(halfW - cr), -(halfD - cr)),
                new Vector2(halfW - cr, -(halfD - cr))
            };
            var points = new Vector3[CornerSegments * 4];
            int index = 0;
            for (int c = 0; c < 4; c++)
            {
                float start = c * 90f * Mathf.Deg2Rad;
                for (int s = 0; s < CornerSegments; s++)
                {
                    float a = start + 90f * Mathf.Deg2Rad * s / CornerSegments;
                    Vector2 p = centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * cr;
                    points[index++] = new Vector3(p.x, y, p.y);
                }
            }

            return points;
        }
    }
}
