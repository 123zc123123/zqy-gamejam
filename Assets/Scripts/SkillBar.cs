using UnityEngine;
using UnityEngine.Rendering;

namespace DouQuqu
{
    /// <summary>吕布头上的霸体进度条。满到空 = 霸体剩余。</summary>
    public sealed class SkillBar : MonoBehaviour
    {
        [SerializeField] private Color fillColor = new Color(0.2f, 0.95f, 1f, 1f);
        [SerializeField] private Color trackColor = new Color(0.06f, 0.08f, 0.12f, 0.95f);
        [SerializeField] private Color plateColor = new Color(0.02f, 0.03f, 0.05f, 0.95f);
        [SerializeField] private float widthScale = 4.6f;
        [SerializeField] private float thickness = 0.42f;
        [SerializeField] private float heightScale = 2.35f;

        private SpriteRenderer plate;
        private SpriteRenderer track;
        private SpriteRenderer fill;
        private Sprite whiteSprite;
        private Material spriteMaterial;

        public void Apply(float ratio, Vector3 worldCenter, float bugRadius, float jumpHeight)
        {
            EnsureReady();
            gameObject.SetActive(true);
            ratio = Mathf.Clamp01(ratio);
            float width = Mathf.Max(2.6f, bugRadius * widthScale);
            float thick = Mathf.Max(0.32f, thickness);
            float above = Mathf.Max(2.2f, bugRadius * heightScale) + jumpHeight;
            transform.position = worldCenter + Vector3.up * 0.35f + Vector3.back * above;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Layout(plate, Vector3.zero, new Vector2(width + thick * 0.55f, thick + thick * 0.55f), plateColor, 39);
            Layout(track, Vector3.zero, new Vector2(width, thick), trackColor, 40);
            if (ratio <= 0.001f)
            {
                if (fill != null) fill.enabled = false;
                return;
            }
            float fillW = width * ratio;
            Layout(fill, new Vector3((fillW - width) * 0.5f, 0f, 0f), new Vector2(fillW, thick), fillColor, 41);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void EnsureReady()
        {
            if (plate == null) plate = CreateSprite("Plate", 39);
            if (track == null) track = CreateSprite("Track", 40);
            if (fill == null) fill = CreateSprite("Fill", 41);
        }

        private void Layout(SpriteRenderer renderer, Vector3 localPos, Vector2 worldSize, Color color, int sorting)
        {
            if (renderer == null) return;
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
            renderer.sortingOrder = sorting;
            return renderer;
        }

        private Sprite WhiteSprite()
        {
            if (whiteSprite != null) return whiteSprite;
            Texture2D texture = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            whiteSprite.name = "SkillBarWhite";
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
