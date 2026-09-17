using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>成虫合成精品：预制体负责遮黑、立绘和两套字；极品金闪碎金仍是运行时画的。</summary>
    public sealed class FinestRevealFx : MonoBehaviour
    {
        public const string PrefabResourcePath = "Merge/Prefabs/MergeReveal";

        private const int BurstCount = 36;
        private const int RayCount = 12;
        private const float HoldSeconds = 2.35f;
        private const float ShakeSeconds = 0.85f;
        private const float NormalIntroSeconds = 0.45f;
        private const float OutroSeconds = 0.28f;
        public const float CollectFlySeconds = 0.62f;
        public const float CollectBounceAt = 0.92f;
        private const float CollectEndScale = 0.2f;

        [Header("预制体")]
        [InspectorCn("遮黑")] [SerializeField] private Image veil;
        [InspectorCn("遮黑按钮")] [SerializeField] private Button veilButton;
        [InspectorCn("蛐蛐立绘")] [SerializeField] private Image portrait;
        [InspectorCn("极品特效根")] [SerializeField] private RectTransform fxRoot;
        [InspectorCn("普通字")] [SerializeField] private GameObject normalGroup;
        [InspectorCn("极品字")] [SerializeField] private GameObject legendaryGroup;
        [InspectorCn("普通标题")] [SerializeField] private TMP_Text normalTitle;
        [InspectorCn("普通名字")] [SerializeField] private TMP_Text normalName;
        [InspectorCn("普通性格标签")] [SerializeField] private TMP_Text normalTemperamentLabel;
        [InspectorCn("普通性格")] [SerializeField] private TMP_Text normalTemperament;
        [InspectorCn("普通提示")] [SerializeField] private TMP_Text normalHint;
        [InspectorCn("极品标题")] [SerializeField] private TMP_Text legendaryTitle;
        [InspectorCn("极品副标题")] [SerializeField] private TMP_Text legendarySubtitle;
        [InspectorCn("极品提示")] [SerializeField] private TMP_Text legendaryHint;

        private Canvas canvas;
        private RectTransform overlay;
        private Image flash;
        private Image ringA;
        private Image ringB;
        private Image portraitGlow;
        private RectTransform burstRoot;
        private Image[] shards;
        private Image[] rays;
        private TMP_Text title;
        private TMP_Text subtitle;
        private TMP_Text hint;
        private Vector2 shakeOrigin;
        private Coroutine playing;
        private Action pendingCollect;
        private bool awaitingClick;
        private bool clicked;
        private bool veilStyleCached;
        private Color veilFace = Color.black;
        private float veilAlpha = 0.78f;

        public static void Play(
            RectTransform cell,
            string titleText,
            string nameText,
            string detailText,
            Sprite face,
            bool legendary,
            Color accent,
            Action collected)
        {
            Play(cell, titleText, nameText, detailText, face, legendary, accent, null, collected);
        }

        public static void Play(
            RectTransform cell,
            string titleText,
            string nameText,
            string detailText,
            Sprite face,
            bool legendary,
            Color accent,
            RectTransform collectTarget,
            Action collected)
        {
            FinestRevealFx fx = FindInstance();
            if (fx == null)
            {
                Debug.LogError("[DouQuqu] 找不到合成展示预制体 " + PrefabResourcePath);
                return;
            }
            fx.PlayNow(cell, titleText, nameText, detailText, face, legendary, accent, collectTarget, collected);
        }

        private static FinestRevealFx FindInstance()
        {
            FinestRevealFx fx = FindObjectOfType<FinestRevealFx>(true);
            if (fx != null) return fx;
            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null) return null;
            GameObject instance = Instantiate(prefab);
            instance.name = "MergeReveal";
            fx = instance.GetComponent<FinestRevealFx>();
            if (fx == null) fx = instance.AddComponent<FinestRevealFx>();
            return fx;
        }

        private void Awake()
        {
            Bind();
        }

        private void OnEnable()
        {
            if (veilButton != null)
            {
                veilButton.onClick.RemoveListener(OnVeilClicked);
                veilButton.onClick.AddListener(OnVeilClicked);
            }
        }

        private void OnDisable()
        {
            awaitingClick = false;
            playing = null;
            if (veilButton != null) veilButton.onClick.RemoveListener(OnVeilClicked);
            if (overlay != null) overlay.anchoredPosition = shakeOrigin;
            if (veil != null) veil.raycastTarget = false;
            if (portrait != null)
            {
                portrait.rectTransform.anchoredPosition = Vector2.zero;
                portrait.rectTransform.localRotation = Quaternion.identity;
                portrait.rectTransform.localScale = Vector3.one;
                portrait.color = Color.white;
            }
            Action collect = pendingCollect;
            pendingCollect = null;
            collect?.Invoke();
        }

        private void PlayNow(
            RectTransform cell,
            string titleText,
            string nameText,
            string detailText,
            Sprite face,
            bool legendary,
            Color accent,
            RectTransform collectTarget,
            Action collected)
        {
            Bind();
            if (canvas != null) canvas.enabled = true;
            if (pendingCollect != null && pendingCollect != collected)
            {
                Action previous = pendingCollect;
                pendingCollect = null;
                previous.Invoke();
            }
            pendingCollect = collected;
            gameObject.SetActive(true);
            if (playing != null) StopCoroutine(playing);
            playing = StartCoroutine(PlayRoutine(cell, titleText, nameText, detailText, face, legendary, accent, collectTarget));
        }

        private IEnumerator PlayRoutine(
            RectTransform cell,
            string titleText,
            string nameText,
            string detailText,
            Sprite face,
            bool legendary,
            Color accent,
            RectTransform collectTarget)
        {
            overlay.SetAsLastSibling();
            RestoreDrawOrder();
            shakeOrigin = overlay.anchoredPosition;
            awaitingClick = false;
            clicked = false;
            SelectCopy(legendary, titleText, nameText, detailText, accent);
            if (legendary) EnsureLegendaryFx();

            if (veil != null)
            {
                veil.raycastTarget = true;
                Color c = legendary ? new Color(0.02f, 0.01f, 0f, 0f) : new Color(veilFace.r, veilFace.g, veilFace.b, 0f);
                veil.color = c;
            }
            if (flash != null) flash.color = new Color(1f, 0.95f, 0.55f, 0f);
            if (portrait != null)
            {
                portrait.sprite = face;
                portrait.enabled = face != null;
                portrait.preserveAspect = true;
                portrait.rectTransform.anchoredPosition = Vector2.zero;
                portrait.rectTransform.localRotation = Quaternion.identity;
                portrait.rectTransform.localScale = Vector3.one * 0.15f;
                portrait.color = Color.white;
            }
            if (portraitGlow != null)
            {
                portraitGlow.rectTransform.localScale = Vector3.one * 0.4f;
                portraitGlow.color = new Color(1f, 0.78f, 0.15f, 0f);
                portraitGlow.gameObject.SetActive(legendary);
            }
            if (title != null)
            {
                title.alpha = 0f;
                title.rectTransform.localScale = Vector3.one * 0.4f;
            }
            if (subtitle != null) subtitle.alpha = 0f;
            SetBodyAlpha(0f);
            if (hint != null) hint.alpha = 0f;

            if (legendary)
                yield return PlayLegendaryIntro(cell);
            else
                yield return PlayNormalIntro();

            awaitingClick = true;
            while (!clicked)
            {
                if (hint != null)
                    hint.alpha = 0.55f + 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f));
                yield return null;
            }
            awaitingClick = false;

            Action collect = pendingCollect;
            pendingCollect = null;
            collect?.Invoke();

            yield return PlayOutro(cell, collectTarget);
            playing = null;
            gameObject.SetActive(false);
        }

        private void SelectCopy(bool legendary, string titleText, string nameText, string detailText, Color accent)
        {
            if (normalGroup != null) normalGroup.SetActive(!legendary);
            if (legendaryGroup != null) legendaryGroup.SetActive(legendary);
            title = legendary ? legendaryTitle : normalTitle;
            subtitle = legendary ? legendarySubtitle : null;
            hint = legendary ? legendaryHint : normalHint;
            if (title != null)
            {
                title.text = string.IsNullOrEmpty(titleText) ? title.text : titleText;
                if (!legendary)
                {
                    Color c = accent;
                    c.a = title.color.a;
                    title.color = c;
                }
            }
            if (legendary)
            {
                if (subtitle != null && !string.IsNullOrEmpty(detailText))
                    subtitle.text = detailText;
                return;
            }
            if (normalName != null && !string.IsNullOrEmpty(nameText))
                normalName.text = nameText;
            if (normalTemperament != null && !string.IsNullOrEmpty(detailText))
                normalTemperament.text = detailText;
        }

        private IEnumerator PlayLegendaryIntro(RectTransform cell)
        {
            Vector2 burstAt = Vector2.zero;
            SeedBurst(burstAt);
            SeedRays();
            if (cell != null) cell.localScale = Vector3.one * 1.2f;

            float t = 0f;
            while (t < HoldSeconds)
            {
                t += Time.unscaledDeltaTime;
                float slam = Mathf.Clamp01(t / 0.18f);

                if (veil != null)
                    veil.color = new Color(0.03f, 0.015f, 0f, 0.88f * Mathf.SmoothStep(0f, 1f, slam));

                float flashA = 0f;
                if (t < 0.16f) flashA = 1f;
                else if (t < 0.28f) flashA = 0.85f;
                else if (t < 0.42f) flashA = 0.55f;
                else flashA = 0.12f * (1f - Mathf.Clamp01((t - 0.42f) / 0.5f));
                if (flash != null)
                    flash.color = new Color(1f, 0.93f, 0.42f, flashA);

                float shakeU = 1f - Mathf.Clamp01(t / ShakeSeconds);
                float amp = (t < 0.22f ? 72f : 38f) * shakeU * shakeU;
                overlay.anchoredPosition = shakeOrigin + new Vector2(
                    (UnityEngine.Random.value * 2f - 1f) * amp,
                    (UnityEngine.Random.value * 2f - 1f) * amp);

                float ringU = Mathf.Clamp01(t / 1.1f);
                if (ringA != null)
                {
                    float s = Mathf.Lerp(0.15f, 8.5f, ringU);
                    ringA.rectTransform.localScale = new Vector3(s, s, 1f);
                    ringA.color = new Color(1f, 0.84f, 0.2f, 0.95f * (1f - ringU));
                }
                if (ringB != null)
                {
                    float s = Mathf.Lerp(0.08f, 6.2f, Mathf.Clamp01((t - 0.08f) / 1.05f));
                    ringB.rectTransform.localScale = new Vector3(s, s, 1f);
                    ringB.color = new Color(1f, 0.72f, 0.08f, 0.75f * (1f - Mathf.Clamp01((t - 0.08f) / 1.05f)));
                }

                StepBurst(Mathf.Clamp01(t / 1.15f), 1f);
                StepRays(t, 1f);

                float pop = t < 0.35f
                    ? Mathf.SmoothStep(0.15f, 1.18f, t / 0.35f)
                    : Mathf.Lerp(1.18f, 1f, Mathf.Clamp01((t - 0.35f) / 0.25f));
                if (portrait != null && portrait.enabled)
                    portrait.rectTransform.localScale = Vector3.one * pop;
                if (portraitGlow != null)
                {
                    float g = 1.35f + 0.18f * Mathf.Sin(t * 9f);
                    portraitGlow.rectTransform.localScale = Vector3.one * pop * g;
                    portraitGlow.color = new Color(1f, 0.78f, 0.12f, 0.55f);
                }

                if (title != null)
                {
                    float ts = t < 0.4f ? Mathf.SmoothStep(0.35f, 1.2f, slam) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((t - 0.4f) / 0.2f));
                    title.rectTransform.localScale = Vector3.one * ts;
                    title.alpha = Mathf.Clamp01((t - 0.08f) / 0.12f);
                }
                if (subtitle != null)
                    subtitle.alpha = Mathf.Clamp01((t - 0.22f) / 0.18f);

                if (cell != null)
                    cell.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Mathf.Clamp01(t / 0.5f));

                yield return null;
            }

            overlay.anchoredPosition = shakeOrigin;
            if (flash != null) flash.color = new Color(1f, 0.93f, 0.42f, 0f);
            if (ringA != null) ringA.color = new Color(1f, 0.84f, 0.2f, 0f);
            if (ringB != null) ringB.color = new Color(1f, 0.72f, 0.08f, 0f);
            HideBurst();
            HideRays();
            if (cell != null) cell.localScale = Vector3.one;
            if (portrait != null && portrait.enabled)
                portrait.rectTransform.localScale = Vector3.one;
            if (title != null)
            {
                title.rectTransform.localScale = Vector3.one;
                title.alpha = 1f;
            }
            if (subtitle != null) subtitle.alpha = 1f;
            if (veil != null)
                veil.color = new Color(0.03f, 0.015f, 0f, 0.88f);
        }

        private IEnumerator PlayNormalIntro()
        {
            HideBurst();
            HideRays();
            if (flash != null) flash.color = new Color(1f, 0.93f, 0.42f, 0f);
            if (ringA != null) ringA.color = new Color(1f, 0.84f, 0.2f, 0f);
            if (ringB != null) ringB.color = new Color(1f, 0.72f, 0.08f, 0f);
            overlay.anchoredPosition = shakeOrigin;

            float t = 0f;
            while (t < NormalIntroSeconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / NormalIntroSeconds);
                float slam = Mathf.SmoothStep(0f, 1f, u);
                if (veil != null)
                    veil.color = new Color(veilFace.r, veilFace.g, veilFace.b, veilAlpha * slam);
                float pop = Mathf.SmoothStep(0.2f, 1f, u);
                if (portrait != null && portrait.enabled)
                    portrait.rectTransform.localScale = Vector3.one * pop;
                if (title != null)
                {
                    title.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, slam);
                    title.alpha = slam;
                }
                SetBodyAlpha(Mathf.Clamp01((u - 0.2f) / 0.5f));
                yield return null;
            }

            if (veil != null) veil.color = new Color(veilFace.r, veilFace.g, veilFace.b, veilAlpha);
            if (portrait != null && portrait.enabled)
                portrait.rectTransform.localScale = Vector3.one;
            if (title != null)
            {
                title.rectTransform.localScale = Vector3.one;
                title.alpha = 1f;
            }
            SetBodyAlpha(1f);
        }

        private IEnumerator PlayOutro(RectTransform cell, RectTransform collectTarget)
        {
            overlay.anchoredPosition = shakeOrigin;
            HideBurst();
            HideRays();

            Vector3 flyStart = portrait != null ? portrait.rectTransform.position : Vector3.zero;
            Vector3 flyEnd = Vector3.zero;
            bool fly = portrait != null
                && portrait.enabled
                && portrait.sprite != null
                && TryWorldCenter(collectTarget, out flyEnd);
            Vector3 flyControl = flyStart;
            if (fly)
            {
                flyControl = ArcControl(flyStart, flyEnd);
                portrait.rectTransform.SetAsLastSibling();
            }

            Color veilStart = veil != null ? veil.color : Color.clear;
            Color glowStart = portraitGlow != null ? portraitGlow.color : Color.clear;
            float portraitScale = portrait != null ? portrait.rectTransform.localScale.x : 1f;
            float duration = fly ? CollectFlySeconds : OutroSeconds;
            float t = 0f;
            bool bounced = false;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float fade = 1f - Mathf.Clamp01(t / OutroSeconds);
                if (veil != null)
                {
                    Color c = veilStart;
                    c.a = veilStart.a * fade;
                    veil.color = c;
                }
                if (portraitGlow != null)
                {
                    Color c = glowStart;
                    c.a = glowStart.a * fade;
                    portraitGlow.color = c;
                }
                if (title != null) title.alpha = fade;
                if (subtitle != null) subtitle.alpha = fade;
                SetBodyAlpha(fade);
                if (hint != null) hint.alpha = fade;

                if (fly)
                {
                    float u = FlyProgress(t);
                    portrait.rectTransform.position = QuadBezier(flyStart, flyControl, flyEnd, u);
                    portrait.rectTransform.localScale = Vector3.one * Mathf.Lerp(portraitScale, CollectEndScale, u);
                    portrait.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, -28f, u));
                    Color pc = portrait.color;
                    pc.a = u < 0.85f ? 1f : 1f - (u - 0.85f) / 0.15f;
                    portrait.color = pc;
                    if (!bounced && u >= CollectBounceAt)
                    {
                        bounced = true;
                        UiButtonBounce.Play(collectTarget);
                    }
                }
                else if (portrait != null)
                {
                    portrait.rectTransform.localScale = Vector3.one * portraitScale * Mathf.Lerp(0.85f, 1f, fade);
                    Color c = portrait.color;
                    c.a = fade;
                    portrait.color = c;
                }
                yield return null;
            }

            if (collectTarget != null && !bounced) UiButtonBounce.Play(collectTarget);

            if (veil != null)
            {
                Color c = veilFace;
                c.a = 0f;
                veil.color = c;
                veil.raycastTarget = false;
            }
            if (flash != null) flash.color = new Color(1f, 0.93f, 0.42f, 0f);
            if (ringA != null) ringA.color = new Color(1f, 0.84f, 0.2f, 0f);
            if (ringB != null) ringB.color = new Color(1f, 0.72f, 0.08f, 0f);
            if (portrait != null)
            {
                portrait.enabled = false;
                portrait.color = Color.white;
                portrait.rectTransform.anchoredPosition = Vector2.zero;
                portrait.rectTransform.localRotation = Quaternion.identity;
                portrait.rectTransform.localScale = Vector3.one;
            }
            if (portraitGlow != null) portraitGlow.color = new Color(1f, 0.78f, 0.12f, 0f);
            if (title != null) title.alpha = 0f;
            if (subtitle != null) subtitle.alpha = 0f;
            SetBodyAlpha(0f);
            if (hint != null) hint.alpha = 0f;
            if (cell != null) cell.localScale = Vector3.one;
            HideBurst();
            HideRays();
        }

        /// <summary>立绘和背包不在同一个 Canvas，用世界坐标才能落到按钮中心。</summary>
        public static bool TryWorldCenter(RectTransform target, out Vector3 world)
        {
            world = Vector3.zero;
            if (target == null) return false;
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            if ((corners[2] - corners[0]).sqrMagnitude < 1f) return false;
            world = (corners[0] + corners[2]) * 0.5f;
            return true;
        }

        public static Vector3 ArcControl(Vector3 start, Vector3 end)
        {
            float dist = Vector3.Distance(start, end);
            return (start + end) * 0.5f + Vector3.up * Mathf.Clamp(dist * 0.35f, 140f, 360f);
        }

        public static float FlyProgress(float elapsed)
        {
            float u = Mathf.Clamp01(elapsed / CollectFlySeconds);
            return u * u;
        }

        public static Vector3 QuadBezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private void RestoreDrawOrder()
        {
            int i = 0;
            if (veil != null) veil.transform.SetSiblingIndex(i++);
            if (fxRoot != null) fxRoot.SetSiblingIndex(i++);
            if (portrait != null) portrait.transform.SetSiblingIndex(i++);
            if (normalGroup != null) normalGroup.transform.SetSiblingIndex(i++);
            if (legendaryGroup != null) legendaryGroup.transform.SetSiblingIndex(i++);
        }

        private void SetBodyAlpha(float alpha)
        {
            if (normalName != null) normalName.alpha = alpha;
            if (normalTemperamentLabel != null) normalTemperamentLabel.alpha = alpha;
            if (normalTemperament != null) normalTemperament.alpha = alpha;
        }

        private void OnVeilClicked()
        {
            if (!awaitingClick) return;
            clicked = true;
        }

        private void Bind()
        {
            if (overlay == null) overlay = GetComponent<RectTransform>();
            if (canvas == null) canvas = GetComponent<Canvas>();
            if (veil == null)
            {
                Transform found = FindNamed(transform, "Veil");
                if (found != null) veil = found.GetComponent<Image>();
            }
            if (veilButton == null && veil != null)
                veilButton = veil.GetComponent<Button>();
            if (portrait == null)
            {
                Transform found = FindNamed(transform, "Portrait");
                if (found != null) portrait = found.GetComponent<Image>();
            }
            if (fxRoot == null)
            {
                Transform found = FindNamed(transform, "FxRoot");
                if (found != null) fxRoot = found as RectTransform ?? found.GetComponent<RectTransform>();
            }
            if (normalGroup == null)
            {
                Transform found = FindNamed(transform, "Normal");
                if (found != null) normalGroup = found.gameObject;
            }
            if (legendaryGroup == null)
            {
                Transform found = FindNamed(transform, "Legendary");
                if (found != null) legendaryGroup = found.gameObject;
            }
            if (normalTitle == null) normalTitle = FindText(normalGroup, "Title");
            if (normalName == null) normalName = FindText(normalGroup, "Name");
            if (normalTemperamentLabel == null) normalTemperamentLabel = FindText(normalGroup, "TemperamentLabel");
            if (normalTemperament == null) normalTemperament = FindText(normalGroup, "Temperament");
            if (normalHint == null) normalHint = FindText(normalGroup, "Hint");
            if (legendaryTitle == null) legendaryTitle = FindText(legendaryGroup, "Title");
            if (legendarySubtitle == null) legendarySubtitle = FindText(legendaryGroup, "Subtitle");
            if (legendaryHint == null) legendaryHint = FindText(legendaryGroup, "Hint");
            CacheVeilStyle();
        }

        private void CacheVeilStyle()
        {
            if (veilStyleCached || veil == null) return;
            veilFace = veil.color;
            veilFace.a = 1f;
            veilAlpha = veil.color.a > 0.01f ? veil.color.a : 0.78f;
            veilStyleCached = true;
        }

        private void EnsureLegendaryFx()
        {
            if (fxRoot == null)
            {
                GameObject go = new GameObject("FxRoot", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                fxRoot = go.GetComponent<RectTransform>();
                Stretch(fxRoot);
                fxRoot.SetSiblingIndex(veil != null ? veil.transform.GetSiblingIndex() + 1 : 0);
            }
            if (flash == null)
            {
                flash = CreateImage(fxRoot, "Flash", new Color(1f, 0.93f, 0.42f, 0f));
                Stretch(flash.rectTransform);
            }
            if (burstRoot == null)
            {
                burstRoot = new GameObject("Burst", typeof(RectTransform)).GetComponent<RectTransform>();
                burstRoot.SetParent(fxRoot, false);
                burstRoot.anchoredPosition = Vector2.zero;
                burstRoot.sizeDelta = Vector2.zero;
            }
            if (rays == null)
            {
                rays = new Image[RayCount];
                for (int i = 0; i < RayCount; i++)
                {
                    rays[i] = CreateImage(burstRoot, "Ray_" + i, new Color(1f, 0.82f, 0.2f, 0f));
                    rays[i].rectTransform.sizeDelta = new Vector2(70f, 1600f);
                    rays[i].rectTransform.pivot = new Vector2(0.5f, 0f);
                    rays[i].gameObject.SetActive(false);
                }
            }
            if (ringA == null)
            {
                ringA = CreateImage(fxRoot, "RingA", new Color(1f, 0.84f, 0.2f, 0f));
                ringA.rectTransform.sizeDelta = new Vector2(220f, 220f);
                ringA.sprite = RingSprite();
            }
            if (ringB == null)
            {
                ringB = CreateImage(fxRoot, "RingB", new Color(1f, 0.72f, 0.08f, 0f));
                ringB.rectTransform.sizeDelta = new Vector2(220f, 220f);
                ringB.sprite = RingSprite();
            }
            if (shards == null)
            {
                shards = new Image[BurstCount];
                for (int i = 0; i < BurstCount; i++)
                {
                    shards[i] = CreateImage(burstRoot, "Shard_" + i, Color.white);
                    shards[i].gameObject.SetActive(false);
                }
            }
            if (portraitGlow == null)
            {
                portraitGlow = CreateImage(fxRoot, "PortraitGlow", new Color(1f, 0.78f, 0.12f, 0f));
                portraitGlow.rectTransform.sizeDelta = new Vector2(640f, 640f);
                portraitGlow.sprite = GlowSprite();
            }
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
                float angle = (i / (float)shards.Length) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.2f, 0.2f);
                shard.rectTransform.anchoredPosition = at;
                shard.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                shard.rectTransform.sizeDelta = new Vector2(UnityEngine.Random.Range(36f, 90f), UnityEngine.Random.Range(12f, 28f));
                shard.rectTransform.localScale = Vector3.one * UnityEngine.Random.Range(0.85f, 1.4f);
                shard.color = new Color(1f, UnityEngine.Random.Range(0.7f, 0.95f), UnityEngine.Random.Range(0.08f, 0.35f), 1f);
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

        private static TMP_Text FindText(GameObject group, string childName)
        {
            if (group == null) return null;
            Transform found = FindNamed(group.transform, childName);
            return found != null ? found.GetComponent<TMP_Text>() : null;
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamed(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
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
    }
}
