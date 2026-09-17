using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DouQuqu
{
    /// <summary>当前有效区边 + 预告环带。收口时环带一起渐隐，轮廓不是出局边。</summary>
    public sealed class ArenaZoneView : MonoBehaviour
    {
        private const int EdgeSamples = 8;
        private const float DashLength = 1.6f;
        private const float GapLength = 1.0f;
        private const float LineY = 0.04f;
        private const float FillY = 0.03f;
        private MeshFilter currentFilter;
        private MeshRenderer currentRenderer;
        private Mesh currentMesh;
        private MeshFilter warnFilter;
        private MeshRenderer warnRenderer;
        private Mesh warnMesh;
        private MeshFilter fillFilter;
        private MeshRenderer fillRenderer;
        private Mesh fillMesh;
        private Material lineMaterial;
        private Material fillMaterial;
        private readonly List<Vector3> dashVerts = new List<Vector3>(256);
        private readonly List<Color> dashColors = new List<Color>(256);
        private readonly List<int> dashTris = new List<int>(512);
        private int lastTier = -1;
        private float lastElapsed;
        private float fadeStartElapsed = -1f;
        private float fadeDuration;
        private float fadeOuterW;
        private float fadeOuterD;
        private float fadeInnerW;
        private float fadeInnerD;
        private bool tutorialBoundPulse;
        private static ArenaZoneView cached;
        private float lastDashW = -1f;
        private float lastDashD = -1f;
        private float lastDashWidth = -1f;
        private Color lastDashColor;

        public static ArenaZoneView Ensure()
        {
            if (cached != null) return cached;
            cached = FindObjectOfType<ArenaZoneView>();
            if (cached != null) return cached;
            GameObject go = new GameObject("ArenaZoneView");
            cached = go.AddComponent<ArenaZoneView>();
            return cached;
        }

        void OnDestroy()
        {
            if (cached == this) cached = null;
        }

        public void SetTutorialBoundPulse(bool on)
        {
            tutorialBoundPulse = on;
            if (!on) return;
            if (warnRenderer != null) warnRenderer.enabled = false;
            HideFill();
        }

        public void Refresh(MatchKnobs knobs, float elapsed, bool schedule)
        {
            EnsureMeshes();
            Color dash = new Color(1f, 0.92f, 0.45f, 0.95f);
            float dashWidth = 0.42f;
            if (tutorialBoundPulse)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2.4f);
                dash = new Color(1f, 0.16f + 0.2f * (1f - pulse), 0.08f, 0.55f + 0.45f * pulse);
                dashWidth = 0.62f;
            }
            DrawCurrentDash(dash, dashWidth);

            if (elapsed + 0.05f < lastElapsed)
            {
                lastTier = -1;
                fadeStartElapsed = -1f;
            }

            int tier = schedule ? Rules.ZoneTierAt(knobs, elapsed) : Rules.LastZoneTier;
            if (lastTier >= 0 && tier > lastTier)
                BeginFade(knobs, lastTier, tier, elapsed);
            lastTier = tier;
            lastElapsed = elapsed;

            bool warn = !tutorialBoundPulse && schedule && Rules.IsZoneWarn(knobs, elapsed);
            if (warn)
            {
                int next = Rules.ZoneWarnTier(knobs, elapsed);
                Vector2 inner = Rules.ZoneHalfExtents(knobs, next);
                float innerW = inner.x;
                float innerD = inner.y;
                DrawDashedRect(warnMesh, warnRenderer, innerW, innerD, new Color(1f, 0.95f, 0.85f, 0.85f), 0.28f);
                float pulse = 0.48f + 0.36f * (0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.PI * 4f));
                DrawFill(
                    Rules.ArenaHalfWidth, Rules.ArenaHalfDepth,
                    innerW, innerD,
                    new Color(0.96f, 0.08f, 0.06f, pulse));
            }
            else
            {
                if (warnRenderer != null) warnRenderer.enabled = false;
                if (fadeStartElapsed < 0f) HideFill();
            }

            if (!tutorialBoundPulse && fadeStartElapsed >= 0f)
            {
                float u = fadeDuration <= 0f ? 1f : Mathf.Clamp01((elapsed - fadeStartElapsed) / fadeDuration);
                float alpha = 0.72f * (1f - u);
                if (alpha <= 0.01f) HideFill();
                else
                {
                    DrawFill(
                        fadeOuterW, fadeOuterD,
                        fadeInnerW, fadeInnerD,
                        new Color(0.96f, 0.08f, 0.06f, alpha));
                }
            }
        }

        private void BeginFade(MatchKnobs knobs, int fromTier, int toTier, float elapsed)
        {
            fadeDuration = knobs != null ? Mathf.Max(0f, knobs.zoneFadeT) : 0.5f;
            fadeStartElapsed = elapsed;
            Vector2 outer = Rules.ZoneHalfExtents(knobs, fromTier);
            Vector2 inner = Rules.ZoneHalfExtents(knobs, toTier);
            fadeOuterW = outer.x;
            fadeOuterD = outer.y;
            fadeInnerW = inner.x;
            fadeInnerD = inner.y;
            if (fadeDuration <= 0f) fadeStartElapsed = -1f;
        }

        private void EnsureMeshes()
        {
            HideLegacyLines();
            if (lineMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                lineMaterial = new Material(shader) { name = "ArenaZoneLine" };
                lineMaterial.mainTexture = Texture2D.whiteTexture;
                lineMaterial.color = Color.white;
            }

            if (fillMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                fillMaterial = new Material(shader) { name = "ArenaZoneFill" };
                fillMaterial.mainTexture = Texture2D.whiteTexture;
                fillMaterial.color = Color.white;
            }

            EnsureEdge("CurrentDash", 20, ref currentFilter, ref currentRenderer, ref currentMesh);
            EnsureEdge("WarnDash", 21, ref warnFilter, ref warnRenderer, ref warnMesh);
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

        private void EnsureEdge(string childName, int sorting, ref MeshFilter filter, ref MeshRenderer renderer, ref Mesh mesh)
        {
            if (filter != null) return;
            Transform existing = transform.Find(childName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(childName);
            if (existing == null) child.transform.SetParent(transform, false);
            filter = child.GetComponent<MeshFilter>();
            if (filter == null) filter = child.AddComponent<MeshFilter>();
            renderer = child.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = lineMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sorting;
            mesh = new Mesh { name = childName };
            filter.sharedMesh = mesh;
            renderer.enabled = false;
        }

        private void HideLegacyLines()
        {
            HideChildRenderer("CurrentEdge");
            HideChildRenderer("WarnEdge");
        }

        private void HideChildRenderer(string childName)
        {
            Transform existing = transform.Find(childName);
            if (existing == null) return;
            LineRenderer line = existing.GetComponent<LineRenderer>();
            if (line != null) line.enabled = false;
            existing.gameObject.SetActive(false);
        }

        private void DrawFill(
            float outerW, float outerD,
            float innerW, float innerD,
            Color color)
        {
            if (fillMesh == null || fillRenderer == null) return;
            if (innerW >= outerW - 0.05f || innerD >= outerD - 0.05f)
            {
                HideFill();
                return;
            }

            Vector3[] outer = RectPoints(outerW, outerD, FillY, EdgeSamples);
            Vector3[] inner = RectPoints(innerW, innerD, FillY, EdgeSamples);
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

        void DrawCurrentDash(Color dash, float dashWidth)
        {
            float halfW = Rules.ArenaHalfWidth;
            float halfD = Rules.ArenaHalfDepth;
            if (currentRenderer != null && currentRenderer.enabled
                && Mathf.Abs(lastDashW - halfW) < 0.001f
                && Mathf.Abs(lastDashD - halfD) < 0.001f
                && Mathf.Abs(lastDashWidth - dashWidth) < 0.001f
                && lastDashColor == dash)
                return;
            lastDashW = halfW;
            lastDashD = halfD;
            lastDashWidth = dashWidth;
            lastDashColor = dash;
            DrawDashedRect(currentMesh, currentRenderer, halfW, halfD, dash, dashWidth);
        }

        private void DrawDashedRect(Mesh mesh, MeshRenderer renderer, float halfW, float halfD, Color color, float width)
        {
            if (mesh == null || renderer == null) return;
            dashVerts.Clear();
            dashColors.Clear();
            dashTris.Clear();
            AppendDashedRect(dashVerts, dashColors, dashTris, halfW, halfD, LineY, width, color);
            mesh.Clear();
            if (dashVerts.Count == 0)
            {
                renderer.enabled = false;
                return;
            }

            mesh.SetVertices(dashVerts);
            mesh.SetColors(dashColors);
            mesh.SetTriangles(dashTris, 0);
            mesh.RecalculateBounds();
            renderer.enabled = true;
        }

        private static void AppendDashedRect(
            List<Vector3> verts, List<Color> colors, List<int> tris,
            float halfW, float halfD, float y, float width, Color color)
        {
            Vector2[] corners =
            {
                new Vector2(halfW, halfD),
                new Vector2(-halfW, halfD),
                new Vector2(-halfW, -halfD),
                new Vector2(halfW, -halfD)
            };
            float half = Mathf.Max(0.02f, width * 0.5f);
            float period = DashLength + GapLength;
            for (int c = 0; c < 4; c++)
            {
                Vector2 a = corners[c];
                Vector2 b = corners[(c + 1) % 4];
                Vector2 delta = b - a;
                float length = delta.magnitude;
                if (length < 0.01f) continue;
                Vector2 dir = delta / length;
                Vector2 n = new Vector2(-dir.y, dir.x);
                AppendQuad(verts, colors, tris, a, a + dir * Mathf.Min(DashLength, length), n, half, y, color);
                if (length > DashLength * 2f + 0.08f)
                    AppendQuad(verts, colors, tris, b - dir * DashLength, b, n, half, y, color);
                float t = period;
                float end = Mathf.Max(period, length - DashLength);
                while (t < end)
                {
                    float t1 = Mathf.Min(t + DashLength, end);
                    if (t1 - t >= 0.08f)
                        AppendQuad(verts, colors, tris, a + dir * t, a + dir * t1, n, half, y, color);
                    t += period;
                }
            }
        }

        private static void AppendQuad(
            List<Vector3> verts, List<Color> colors, List<int> tris,
            Vector2 a, Vector2 b, Vector2 n, float half, float y, Color color)
        {
            int i = verts.Count;
            verts.Add(new Vector3(a.x + n.x * half, y, a.y + n.y * half));
            verts.Add(new Vector3(a.x - n.x * half, y, a.y - n.y * half));
            verts.Add(new Vector3(b.x - n.x * half, y, b.y - n.y * half));
            verts.Add(new Vector3(b.x + n.x * half, y, b.y + n.y * half));
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            tris.Add(i);
            tris.Add(i + 1);
            tris.Add(i + 2);
            tris.Add(i);
            tris.Add(i + 2);
            tris.Add(i + 3);
        }

        private static Vector3[] RectPoints(float halfW, float halfD, float y, int perSide)
        {
            perSide = Mathf.Max(1, perSide);
            Vector2[] corners =
            {
                new Vector2(halfW, halfD),
                new Vector2(-halfW, halfD),
                new Vector2(-halfW, -halfD),
                new Vector2(halfW, -halfD)
            };
            var points = new Vector3[perSide * 4];
            int index = 0;
            for (int c = 0; c < 4; c++)
            {
                Vector2 a = corners[c];
                Vector2 b = corners[(c + 1) % 4];
                for (int s = 0; s < perSide; s++)
                {
                    float t = s / (float)perSide;
                    Vector2 p = Vector2.LerpUnclamped(a, b, t);
                    points[index++] = new Vector3(p.x, y, p.y);
                }
            }

            return points;
        }
    }
}
