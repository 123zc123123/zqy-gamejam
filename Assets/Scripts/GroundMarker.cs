using UnityEngine;
using UnityEngine.Rendering;

namespace DouQuqu
{
    /// <summary>
    /// 蟋蟀脚下的玩家色圆：半透明填充 + 描边，外加落地震影。
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

        [SerializeField] private Sprite circleSprite;
        [SerializeField] private Sprite shadowSprite;
        [SerializeField] private SpriteRenderer shadow;
        [SerializeField] private SpriteRenderer fill;
        [SerializeField] private LineRenderer ring;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private float ringScale = 1.38f;
        [SerializeField] private float ringWidthScale = 0.1f;
        [SerializeField] private float fillAlpha = 0.22f;
        [SerializeField] private float chargeFillAlpha = 0.34f;
        [SerializeField] private float shadowScale = 1.08f;
        [SerializeField] private float shadowOffsetScale = 0.22f;
        [SerializeField] private float heightOffset = 0.03f;
        private Material spriteMaterial;

        public static Color ColorForPlayer(int playerId)
        {
            int index = Mathf.Abs(playerId) % PlayerColors.Length;
            return PlayerColors[index];
        }

        private void OnEnable()
        {
            EnsureReady();
            if (Application.isPlaying) return;
            if (GetComponentInParent<CricketUnit>() != null)
            {
                ShowAuthored();
                return;
            }
            Preview();
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
            LayoutMarker(radius, lift, paint, charging ? chargeFillAlpha : fillAlpha);
        }

        /// <summary>只改玩家色和蓄力透明度，不改预制体里摆好的位置和大小。</summary>
        public void Paint(Color playerColor, bool charging)
        {
            EnsureReady();
            gameObject.SetActive(true);
            Color paint = charging ? Color.Lerp(playerColor, Color.white, 0.28f) : playerColor;
            float fillA = charging ? chargeFillAlpha : fillAlpha;
            if (fill != null)
            {
                fill.enabled = true;
                fill.color = new Color(paint.r, paint.g, paint.b, fillA);
            }
            if (ring != null)
            {
                ring.enabled = true;
                Color stroke = new Color(paint.r, paint.g, paint.b, 0.94f);
                ring.startColor = stroke;
                ring.endColor = stroke;
            }
            if (shadow != null) shadow.enabled = true;
        }

        /// <summary>按碰撞半径把 Fill / Ring / Shadow 烘焙进子物体缩放，不改本节点 Transform。</summary>
        public void Bake(float bugRadius)
        {
            EnsureReady();
            LayoutMarker(Mathf.Max(0.2f, bugRadius), 1f, PlayerColors[0], fillAlpha);
        }

        /// <summary>预制体 / 编辑器里正对镜头，不躺到地面、不改父节点。</summary>
        public void Preview()
        {
            EnsureReady();
            transform.rotation = Quaternion.identity;
            LayoutMarker(PreviewRadius, 1f, PlayerColors[0], Mathf.Max(fillAlpha, 0.35f));
        }

        /// <summary>单位预制体里只保证看得见，不改已经摆好的 Transform。</summary>
        public void ShowAuthored()
        {
            EnsureReady();
            gameObject.SetActive(true);
            if (fill != null) fill.enabled = true;
            if (ring != null) ring.enabled = true;
            if (shadow != null) shadow.enabled = true;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public void EnsureReady()
        {
            DestroyChild("Hole");
            if (shadow == null) shadow = CreateDisc("Shadow", -22);
            if (fill == null) fill = CreateDisc("Fill", -21);
            if (ring == null) ring = CreateRing("Ring", -20);
            BindShadowSprite();
            if (fill != null) fill.sprite = circleSprite;
            EnsureSpriteMaterial(shadow);
            EnsureSpriteMaterial(fill);
            if (ring != null && ring.sharedMaterial == null)
            {
                Material material = LineMaterial();
                if (material != null) ring.sharedMaterial = material;
            }
        }

        private void LayoutMarker(float radius, float lift, Color paint, float fillA)
        {
            float ringRadius = radius * ringScale;
            LayoutShadow(radius * shadowScale * lift, lift);
            if (shadow != null)
                shadow.transform.localPosition = new Vector3(0f, -radius * shadowOffsetScale * lift, 0f);
            LayoutDisc(fill, circleSprite, ringRadius, new Color(paint.r, paint.g, paint.b, fillA), -21);
            LayoutRing(ringRadius, new Color(paint.r, paint.g, paint.b, 0.94f), ringRadius * ringWidthScale);
        }

        private void LayoutShadow(float worldRadius, float lift)
        {
            Sprite sprite = ShadowSprite();
            LayoutDisc(shadow, sprite, worldRadius, new Color(1f, 1f, 1f, lift), -22, true);
        }

        private void LayoutDisc(SpriteRenderer renderer, Sprite sprite, float worldRadius, Color color, int sorting, bool keepAspect = false)
        {
            if (renderer == null || sprite == null || worldRadius < 0.01f)
            {
                if (renderer != null) renderer.enabled = false;
                return;
            }

            EnsureSpriteMaterial(renderer);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sorting;
            renderer.enabled = true;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.transform.localRotation = Quaternion.identity;
            if (renderer != shadow) renderer.transform.localPosition = Vector3.zero;
            Vector3 size = sprite.bounds.size;
            float diameter = worldRadius * 2f;
            if (keepAspect)
            {
                float scale = diameter / Mathf.Max(0.01f, Mathf.Max(size.x, size.y));
                renderer.transform.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                renderer.transform.localScale = new Vector3(
                    diameter / Mathf.Max(0.01f, size.x),
                    diameter / Mathf.Max(0.01f, size.y),
                    1f);
            }
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

        private void BindShadowSprite()
        {
            if (shadow == null) return;
            if (shadowSprite == null && shadow.sprite != null && shadow.sprite != circleSprite)
                shadowSprite = shadow.sprite;
            Sprite sprite = ShadowSprite();
            if (sprite != null) shadow.sprite = sprite;
        }

        private Sprite ShadowSprite()
        {
            if (shadowSprite != null) return shadowSprite;
            if (shadow != null && shadow.sprite != null) return shadow.sprite;
            return circleSprite;
        }

        private void EnsureSpriteMaterial(SpriteRenderer renderer)
        {
            if (renderer == null) return;
            Material current = renderer.sharedMaterial;
            if (current != null && current.shader != null && current.shader.name != "Hidden/InternalErrorShader")
                return;
#if UNITY_EDITOR
            Material builtin = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            if (builtin != null)
            {
                renderer.sharedMaterial = builtin;
                return;
            }
#endif
            if (spriteMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) return;
                spriteMaterial = new Material(shader);
                spriteMaterial.name = "GroundMarkerSprite";
                spriteMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            renderer.sharedMaterial = spriteMaterial;
        }

        private SpriteRenderer CreateDisc(string childName, int sorting)
        {
            Transform existing = transform.Find(childName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(childName);
            if (existing == null) child.transform.SetParent(transform, false);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = child.AddComponent<SpriteRenderer>();
            if (renderer.sprite == null) renderer.sprite = circleSprite;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sorting;
            renderer.enabled = false;
            EnsureSpriteMaterial(renderer);
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

            SpriteMask leftoverMask = child.GetComponent<SpriteMask>();
            if (leftoverMask != null)
            {
                if (Application.isPlaying) Object.Destroy(leftoverMask);
                else Object.DestroyImmediate(leftoverMask);
            }

            SpriteRenderer leftoverSprite = child.GetComponent<SpriteRenderer>();
            if (leftoverSprite != null)
            {
                if (Application.isPlaying) Object.Destroy(leftoverSprite);
                else Object.DestroyImmediate(leftoverSprite);
            }

            Transform hole = child.transform.Find("Hole");
            if (hole != null)
            {
                if (Application.isPlaying) Object.Destroy(hole.gameObject);
                else Object.DestroyImmediate(hole.gameObject);
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

        private void DestroyChild(string childName)
        {
            Transform existing = transform.Find(childName);
            if (existing == null)
            {
                Transform ringChild = transform.Find("Ring");
                if (ringChild != null) existing = ringChild.Find(childName);
            }
            if (existing == null) return;
            if (Application.isPlaying) Object.Destroy(existing.gameObject);
            else Object.DestroyImmediate(existing.gameObject);
        }

        private Material LineMaterial()
        {
            if (lineMaterial != null) return lineMaterial;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            lineMaterial = shader != null ? new Material(shader) : null;
            return lineMaterial;
        }
    }
}
