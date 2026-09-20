using UnityEngine;

namespace DouQuqu
{
    /// <summary>卵头顶孵化进度：满→空为剩余时间，最后约 1/4 变亮。不写数字。</summary>
    public sealed class HatchBar : MonoBehaviour
    {
        public const float LateRatio = 0.25f;
        static readonly Color Track = new Color(0.12f, 0.1f, 0.08f, 0.85f);
        static readonly Color FillOk = new Color(0.95f, 0.82f, 0.28f, 1f);
        static readonly Color FillLate = new Color(1f, 0.95f, 0.55f, 1f);

        SpriteRenderer track;
        SpriteRenderer fill;
        Transform fillRoot;
        float width = 1.1f;

        public static float RemainRatio(float elapsed, float hatchAt, float duration)
        {
            float left = hatchAt - elapsed;
            if (left <= 0f) return 0f;
            float dur = Mathf.Max(0.0001f, duration);
            return Mathf.Clamp01(left / dur);
        }

        public void Apply(float remain, Vector3 eggWorld, float eggRadius)
        {
            Ensure();
            remain = Mathf.Clamp01(remain);
            float r = Mathf.Max(0.2f, eggRadius);
            width = r * 2.2f;
            float height = r * 0.28f;
            Transform hud = transform.Find("HatchHud");
            if (hud == null)
            {
                Ensure();
                hud = transform.Find("HatchHud");
            }
            if (hud != null)
            {
                hud.position = eggWorld + Vector3.up * (r * 1.15f + 0.08f);
                hud.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            track.transform.localPosition = Vector3.zero;
            track.transform.localScale = new Vector3(width, height, 1f);
            track.enabled = true;
            fillRoot.localPosition = new Vector3(-width * 0.5f * (1f - remain), 0f, -0.01f);
            fillRoot.localScale = new Vector3(Mathf.Max(0.001f, width * remain), height * 0.72f, 1f);
            fill.enabled = remain > 0.001f;
            fill.color = remain <= LateRatio ? FillLate : FillOk;
            Transform hudBar = transform.Find("HatchHud");
            if (hudBar != null) hudBar.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (track != null) track.enabled = false;
            if (fill != null) fill.enabled = false;
            Transform hudBar = transform.Find("HatchHud");
            if (hudBar != null) hudBar.gameObject.SetActive(false);
        }

        void Ensure()
        {
            if (track != null && fill != null) return;
            Transform hud = transform.Find("HatchHud");
            if (hud == null)
            {
                GameObject hudGo = new GameObject("HatchHud");
                hudGo.transform.SetParent(transform, false);
                hud = hudGo.transform;
            }
            Sprite sprite = WhiteSprite();
            track = Child(hud, "Track", sprite, Track, 40);
            fillRoot = Child(hud, "Fill", sprite, FillOk, 41).transform;
            fill = fillRoot.GetComponent<SpriteRenderer>();
        }

        SpriteRenderer Child(Transform parent, string name, Sprite sprite, Color color, int order)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name);
            if (existing == null) go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.identity;
            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        static Sprite cached;

        static Sprite WhiteSprite()
        {
            if (cached != null) return cached;
            Texture2D texture = Texture2D.whiteTexture;
            cached = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            return cached;
        }
    }
}
