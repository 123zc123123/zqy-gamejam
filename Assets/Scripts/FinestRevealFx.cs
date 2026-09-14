using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>成虫合成抽出极品：全屏蒙版、金闪、碎金震爆、震屏。</summary>
    public sealed class FinestRevealFx : MonoBehaviour
    {
        private const int BurstCount = 36;
        private const int RayCount = 12;
        private const float HoldSeconds = 2.35f;
        private const float ShakeSeconds = 0.85f;

        private Canvas canvas;
        private RectTransform overlay;
        private Image veil;
        private Image flash;
        private Image ringA;
        private Image ringB;
        private Image portrait;
        private Image portraitGlow;
        private RectTransform burstRoot;
        private Image[] shards;
        private Image[] rays;
        private TMP_Text title;
        private TMP_Text subtitle;
        private Vector2 shakeOrigin;
        private Coroutine playing;

        public static void Play(RectTransform cell, string cricketName, string idiom, Sprite face)
        {
            FinestRevealFx fx = FindObjectOfType<FinestRevealFx>();
            if (fx == null)
            {
                GameObject host = new GameObject("FinestRevealFxHost", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5200;
                canvas.overrideSorting = true;
                CanvasScaler scaler = host.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 1f;
                fx = host.AddComponent<FinestRevealFx>();
            }
            fx.PlayNow(cell, cricketName, idiom, face);
        }

        private void PlayNow(RectTransform cell, string cricketName, string idiom, Sprite face)
        {
            EnsureOverlay();
            if (playing != null) StopCoroutine(playing);
            playing = StartCoroutine(PlayRoutine(cell, cricketName, idiom, face));
        }

        private IEnumerator PlayRoutine(RectTransform cell, string cricketName, string idiom, Sprite face)
        {
            overlay.SetAsLastSibling();
            shakeOrigin = overlay.anchoredPosition;
            Vector2 burstAt = Vector2.zero;

            if (veil != null)
            {
                veil.raycastTarget = true;
                veil.color = new Color(0.02f, 0.01f, 0f, 0f);
            }
            if (flash != null) flash.color = new Color(1f, 0.95f, 0.55f, 0f);
            if (portrait != null)
            {
                portrait.sprite = face;
                portrait.enabled = face != null;
                portrait.preserveAspect = true;
                portrait.rectTransform.localScale = Vector3.one * 0.15f;
                portrait.color = Color.white;
            }
            if (portraitGlow != null)
            {
                portraitGlow.rectTransform.localScale = Vector3.one * 0.4f;
                portraitGlow.color = new Color(1f, 0.78f, 0.15f, 0f);
            }
            if (title != null)
            {
                title.text = "极 品";
                title.alpha = 0f;
                title.rectTransform.localScale = Vector3.one * 0.4f;
            }
            if (subtitle != null)
            {
                string nameLine = string.IsNullOrEmpty(cricketName) ? "" : cricketName;
                string idiomLine = string.IsNullOrEmpty(idiom) ? "" : idiom;
                subtitle.text = string.IsNullOrEmpty(idiomLine) ? nameLine : nameLine + "  ·  " + idiomLine;
                subtitle.alpha = 0f;
            }

            SeedBurst(burstAt);
            SeedRays();
            if (cell != null) cell.localScale = Vector3.one * 1.2f;

            float t = 0f;
            while (t < HoldSeconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / HoldSeconds);
                float slam = Mathf.Clamp01(t / 0.18f);
                float fade = t > HoldSeconds - 0.45f ? 1f - (t - (HoldSeconds - 0.45f)) / 0.45f : 1f;
                fade = Mathf.Clamp01(fade);

                if (veil != null)
                    veil.color = new Color(0.03f, 0.015f, 0f, 0.88f * Mathf.SmoothStep(0f, 1f, slam) * fade);

                float flashA = 0f;
                if (t < 0.16f) flashA = 1f;
                else if (t < 0.28f) flashA = 0.85f;
                else if (t < 0.42f) flashA = 0.55f;
                else flashA = 0.12f * (1f - Mathf.Clamp01((t - 0.42f) / 0.5f));
                if (flash != null)
                    flash.color = new Color(1f, 0.93f, 0.42f, flashA * fade);

                float shakeU = 1f - Mathf.Clamp01(t / ShakeSeconds);
                float amp = (t < 0.22f ? 72f : 38f) * shakeU * shakeU;
                overlay.anchoredPosition = shakeOrigin + new Vector2(
                    (Random.value * 2f - 1f) * amp,
                    (Random.value * 2f - 1f) * amp);

                float ringU = Mathf.Clamp01(t / 1.1f);
                if (ringA != null)
                {
                    float s = Mathf.Lerp(0.15f, 8.5f, ringU);
                    ringA.rectTransform.localScale = new Vector3(s, s, 1f);
                    ringA.color = new Color(1f, 0.84f, 0.2f, 0.95f * (1f - ringU) * fade);
                }
                if (ringB != null)
                {
                    float s = Mathf.Lerp(0.08f, 6.2f, Mathf.Clamp01((t - 0.08f) / 1.05f));
                    ringB.rectTransform.localScale = new Vector3(s, s, 1f);
                    ringB.color = new Color(1f, 0.72f, 0.08f, 0.75f * (1f - Mathf.Clamp01((t - 0.08f) / 1.05f)) * fade);
                }

                StepBurst(Mathf.Clamp01(t / 1.15f), fade);
                StepRays(t, fade);

                float pop = t < 0.35f
                    ? Mathf.SmoothStep(0.15f, 1.18f, t / 0.35f)
                    : Mathf.Lerp(1.18f, 1f, Mathf.Clamp01((t - 0.35f) / 0.25f));
                if (portrait != null && portrait.enabled)
                    portrait.rectTransform.localScale = Vector3.one * pop * fade;
                if (portraitGlow != null)
                {
                    float g = 1.35f + 0.18f * Mathf.Sin(t * 9f);
                    portraitGlow.rectTransform.localScale = Vector3.one * pop * g;
                    portraitGlow.color = new Color(1f, 0.78f, 0.12f, 0.55f * fade);
                }

                if (title != null)
                {
                    float ts = t < 0.4f ? Mathf.SmoothStep(0.35f, 1.2f, slam) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((t - 0.4f) / 0.2f));
                    title.rectTransform.localScale = Vector3.one * ts;
                    title.alpha = fade * Mathf.Clamp01((t - 0.08f) / 0.12f);
                }
                if (subtitle != null)
                    subtitle.alpha = fade * Mathf.Clamp01((t - 0.22f) / 0.18f);

                if (cell != null)
                    cell.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Mathf.Clamp01(t / 0.5f));

                yield return null;
            }

            overlay.anchoredPosition = shakeOrigin;
            if (veil != null)
            {
                veil.color = new Color(0.03f, 0.015f, 0f, 0f);
                veil.raycastTarget = false;
            }
            if (flash != null) flash.color = new Color(1f, 0.93f, 0.42f, 0f);
            if (ringA != null) ringA.color = new Color(1f, 0.84f, 0.2f, 0f);
            if (ringB != null) ringB.color = new Color(1f, 0.72f, 0.08f, 0f);
            if (portrait != null) portrait.enabled = false;
            if (portraitGlow != null) portraitGlow.color = new Color(1f, 0.78f, 0.12f, 0f);
            if (title != null) title.alpha = 0f;
            if (subtitle != null) subtitle.alpha = 0f;
            if (cell != null) cell.localScale = Vector3.one;
            HideBurst();
            HideRays();
            playing = null;
        }

        private void SeedBurst(Vector2 at)
        {
            if (shards == null) return;
            burstRoot.anchoredPosition = at;
            for (int i = 0; i < shards.Length; i++)
            {
                Image shard = shards[i];
                if (shard == null) continue;
                shard.gameObject.SetActive(true);
                float angle = (i / (float)shards.Length) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                shard.rectTransform.anchoredPosition = at;
                shard.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                shard.rectTransform.sizeDelta = new Vector2(Random.Range(36f, 90f), Random.Range(12f, 28f));
                shard.rectTransform.localScale = Vector3.one * Random.Range(0.85f, 1.4f);
                shard.color = new Color(1f, Random.Range(0.7f, 0.95f), Random.Range(0.08f, 0.35f), 1f);
            }
        }

        private void StepBurst(float u, float fade)
        {
            if (shards == null) return;
            Vector2 at = burstRoot.anchoredPosition;
            float dist = 980f * Mathf.SmoothStep(0f, 1f, u);
            for (int i = 0; i < shards.Length; i++)
            {
                Image shard = shards[i];
                if (shard == null) continue;
                float angle = (i / (float)shards.Length) * Mathf.PI * 2f;
                shard.rectTransform.anchoredPosition = at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
                Color color = shard.color;
                color.a = (1f - u) * fade;
                shard.color = color;
            }
        }

        private void HideBurst()
        {
            if (shards == null) return;
            for (int i = 0; i < shards.Length; i++)
                if (shards[i] != null) shards[i].gameObject.SetActive(false);
        }

        private void SeedRays()
        {
            if (rays == null) return;
            for (int i = 0; i < rays.Length; i++)
            {
                if (rays[i] == null) continue;
                rays[i].gameObject.SetActive(true);
                float angle = i * (360f / rays.Length);
                rays[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                rays[i].color = new Color(1f, 0.82f, 0.2f, 0.55f);
            }
        }

        private void StepRays(float t, float fade)
        {
            if (rays == null) return;
            float spin = t * 42f;
            float pulse = 0.45f + 0.35f * (0.5f + 0.5f * Mathf.Sin(t * 10f));
            for (int i = 0; i < rays.Length; i++)
            {
                if (rays[i] == null) continue;
                float angle = i * (360f / rays.Length) + spin;
                rays[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                rays[i].color = new Color(1f, 0.82f, 0.18f, pulse * fade);
            }
        }

        private void HideRays()
        {
            if (rays == null) return;
            for (int i = 0; i < rays.Length; i++)
                if (rays[i] != null) rays[i].gameObject.SetActive(false);
        }

        private void EnsureOverlay()
        {
            if (overlay != null) return;
            canvas = GetComponent<Canvas>();
            overlay = GetComponent<RectTransform>();
            Stretch(overlay);

            veil = CreateImage(overlay, "Veil", new Color(0.03f, 0.015f, 0f, 0f));
            Stretch(veil.rectTransform);
            veil.raycastTarget = false;

            flash = CreateImage(overlay, "Flash", new Color(1f, 0.93f, 0.42f, 0f));
            Stretch(flash.rectTransform);
            flash.raycastTarget = false;

            burstRoot = new GameObject("Burst", typeof(RectTransform)).GetComponent<RectTransform>();
            burstRoot.SetParent(overlay, false);
            burstRoot.anchoredPosition = Vector2.zero;
            burstRoot.sizeDelta = Vector2.zero;

            rays = new Image[RayCount];
            for (int i = 0; i < RayCount; i++)
            {
                rays[i] = CreateImage(burstRoot, "Ray_" + i, new Color(1f, 0.82f, 0.2f, 0f));
                rays[i].rectTransform.sizeDelta = new Vector2(70f, 1600f);
                rays[i].rectTransform.pivot = new Vector2(0.5f, 0f);
                rays[i].gameObject.SetActive(false);
            }

            ringA = CreateImage(overlay, "RingA", new Color(1f, 0.84f, 0.2f, 0f));
            ringA.rectTransform.sizeDelta = new Vector2(220f, 220f);
            ringA.sprite = RingSprite();
            ringB = CreateImage(overlay, "RingB", new Color(1f, 0.72f, 0.08f, 0f));
            ringB.rectTransform.sizeDelta = new Vector2(220f, 220f);
            ringB.sprite = RingSprite();

            shards = new Image[BurstCount];
            for (int i = 0; i < BurstCount; i++)
            {
                shards[i] = CreateImage(burstRoot, "Shard_" + i, Color.white);
                shards[i].gameObject.SetActive(false);
            }

            portraitGlow = CreateImage(overlay, "PortraitGlow", new Color(1f, 0.78f, 0.12f, 0f));
            portraitGlow.rectTransform.sizeDelta = new Vector2(640f, 640f);
            portraitGlow.sprite = GlowSprite();

            portrait = CreateImage(overlay, "Portrait", Color.white);
            portrait.rectTransform.sizeDelta = new Vector2(420f, 420f);
            portrait.enabled = false;

            title = CreateText(overlay, "Title", 120f, new Vector2(0.06f, 0.72f), new Vector2(0.94f, 0.88f));
            title.color = new Color(1f, 0.86f, 0.28f, 1f);
            subtitle = CreateText(overlay, "Subtitle", 48f, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.28f));
            subtitle.color = new Color(1f, 0.93f, 0.7f, 1f);
        }

        private static TMP_Text CreateText(Transform parent, string name, float size, Vector2 min, Vector2 max)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            TMP_Text text = go.GetComponent<TextMeshProUGUI>();
            text.font = UiFactory.Font;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.outlineWidth = 0.28f;
            text.outlineColor = new Color(0.22f, 0.08f, 0f, 1f);
            text.raycastTarget = false;
            text.alpha = 0f;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite ringSprite;
        private static Sprite glowSprite;

        private static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            ringSprite = RadialSprite(96, 0.78f, 1f, "FinestRing");
            return ringSprite;
        }

        private static Sprite GlowSprite()
        {
            if (glowSprite != null) return glowSprite;
            glowSprite = RadialSprite(96, 0f, 1f, "FinestGlow");
            return glowSprite;
        }

        private static Sprite RadialSprite(int n, float inner, float outer, string spriteName)
        {
            Texture2D texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            float mid = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x - mid) / mid;
                    float v = (y - mid) / mid;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a;
                    if (r >= outer) a = 0f;
                    else if (inner <= 0f) a = Mathf.Clamp01(1f - r / outer);
                    else if (r < inner) a = 0f;
                    else a = 1f - (r - inner) / Mathf.Max(0.001f, outer - inner);
                    if (inner <= 0f) a *= a;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
                }
            }
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), n);
            sprite.name = spriteName;
            return sprite;
        }

        private void OnDisable()
        {
            if (overlay != null) overlay.anchoredPosition = shakeOrigin;
            if (veil != null) veil.raycastTarget = false;
        }
    }
}
