using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 身下耐力条：World Space Canvas + Image。
    /// 底板 / 描边 / 填充九宫；填充从左连续裁，不再显示分格线。
    /// </summary>
    [ExecuteAlways]
    public sealed class StaminaBar : MonoBehaviour
    {
        public const int MaxSlots = 8;
        /// <summary>顶视相机 / 场地都是 Euler(90)。-90 会从 Canvas 背面看，体力条上下颠倒。</summary>
        public static readonly Quaternion GroundRotation = Quaternion.Euler(90f, 0f, 0f);
        private const string TexRoot = "Battle/Entities/Textures/";
        private const float Pixels = 100f;
        private const float BgPx = 50f;
        private const float DefaultHudW = 600f;
        private const float DefaultHudH = 100f;
        private const float FillPx = 40f;
        private const float OutlinePx = 68f;
        private const float InsetPx = (BgPx - FillPx) * 0.5f;
        private const float OutlinePadPx = OutlinePx - BgPx;

        [SerializeField] private Color okColor = Color.white;
        [SerializeField] private Color warnColor = new Color(1f, 0.78f, 0.28f, 1f);
        [SerializeField] private Color lowColor = new Color(1f, 0.38f, 0.32f, 1f);
        [SerializeField] private Color hotColor = new Color(1f, 0.55f, 0.18f, 1f);
        [SerializeField, Min(0f)]
        [Tooltip("长度 = ((成长倍数-1)×此系数+1)×预制体初始长度")]
        private float lengthGrowScale = 1f;
        [SerializeField] private float pendingAlpha = 0.4f;

        private Sprite bgSprite;
        private Sprite outlineSprite;
        private Sprite fillSprite;
        private Sprite iconSprite;
        private RectTransform hud;
        private RectTransform fillArea;
        private RectTransform pendingClip;
        private RectTransform remainClip;
        private RectTransform hotClip;
        private Image bg;
        private Image outline;
        private Image icon;
        private Image pendingFill;
        private Image remainFill;
        private Image hotFill;
        private float authoredHudW;
        private float authoredHudH;
        private bool capturedSize;
        private float grow = 1f;
        private float lastRatio = 1f;
        private float lastPending;
        private float lastHot;
        private int lastSlots = 5;
        private bool laidOut;

        private void Awake()
        {
            EnsureReady();
        }

        private void OnEnable()
        {
            if (IsPrefabAsset()) return;
            EnsureReady();
            if (!Application.isPlaying) ShowAuthored();
        }

        /// <summary>
        /// 实心 = 当前耐力。蓄力时 pendingRatio 是本次将扣的比例，画在当前值往回的半透明段。
        /// </summary>
        public void Apply(float currentRatio, int slots, Vector3 worldCenter, float bugRadius, float pendingRatio = 0f, float hotGate = 0f)
        {
            EnsureReady();
            gameObject.SetActive(true);
            transform.position = worldCenter + Vector3.up * 0.1f + Vector3.back * (bugRadius * 1.85f);
            transform.rotation = GroundRotation;
            Layout(currentRatio, slots, pendingRatio, hotGate);
        }

        /// <summary>编辑器生成预制体时写入初始 Hud 尺寸，局内不再用半径改宽度。</summary>
        public void Bake(float bugRadius)
        {
            EnsureReady();
            if (hud != null && hud.sizeDelta.x < 1f)
                hud.sizeDelta = new Vector2(DefaultHudW, DefaultHudH);
            capturedSize = false;
            grow = 1f;
            Layout(1f, 5, 0f, 0f);
        }

        /// <summary>只刷新格数和填充。长度跟预制体初始值，再按成长倍数拉长。</summary>
        public void ApplyFill(float currentRatio, int slots, float pendingRatio = 0f, float hotGate = 0f)
        {
            EnsureReady();
            gameObject.SetActive(true);
            Layout(currentRatio, slots, pendingRatio, hotGate);
        }

        /// <summary>蛐蛐成长倍数。长度 = ((grow-1)×缩放系数+1)×初始长度。</summary>
        public void SetGrow(float nextGrow)
        {
            grow = Mathf.Max(0.05f, nextGrow);
            if (laidOut) Layout(lastRatio, lastSlots, lastPending, lastHot);
        }

        public void ShowAuthored()
        {
            EnsureReady();
            gameObject.SetActive(true);
            grow = 1f;
            Layout(1f, 5, 0f, 0f);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>缺节点时在实例上补齐，不改预制体资产。</summary>
        public void EnsureReady()
        {
            if (IsPrefabAsset()) return;
            LoadSprites();
            StripLegacy();
            EnsureCanvas();
            bg = EnsureImage("Bg", hud, true);
            fillArea = EnsureRect("FillArea", hud);
            pendingClip = EnsureClip("PendingClip", fillArea);
            remainClip = EnsureClip("RemainClip", fillArea);
            hotClip = EnsureClip("HotClip", fillArea);
            if (!pendingClip.gameObject.activeSelf) pendingClip.gameObject.SetActive(true);
            if (!remainClip.gameObject.activeSelf) remainClip.gameObject.SetActive(true);
            if (!hotClip.gameObject.activeSelf) hotClip.gameObject.SetActive(true);
            pendingFill = EnsureImage("Pending", pendingClip, true);
            remainFill = EnsureImage("Remain", remainClip, true);
            hotFill = EnsureImage("Hot", hotClip, true);
            // 兼容旧预制体中已经保存的分隔层：不销毁资源，只在实例上关闭。
            Transform legacyDividers = hud.Find("Dividers");
            if (legacyDividers != null) legacyDividers.gameObject.SetActive(false);
            outline = EnsureImage("Outline", hud, true);
            icon = EnsureImage("Icon", hud, false);
            bg.transform.SetSiblingIndex(0);
            fillArea.SetSiblingIndex(1);
            outline.transform.SetSiblingIndex(2);
            icon.transform.SetSiblingIndex(3);
        }

        private void Layout(float currentRatio, int slots, float pendingRatio, float hotGate)
        {
            currentRatio = Mathf.Clamp01(currentRatio);
            pendingRatio = Mathf.Clamp(pendingRatio, 0f, currentRatio);
            float remainRatio = Mathf.Max(0f, currentRatio - pendingRatio);
            slots = Mathf.Clamp(slots, 3, MaxSlots);
            hotGate = Mathf.Clamp01(hotGate);
            lastRatio = currentRatio;
            lastPending = pendingRatio;
            lastHot = hotGate;
            lastSlots = slots;
            laidOut = true;

            CaptureAuthoredSize();
            float bgW = authoredHudW * LengthMul();
            float bgH = authoredHudH;
            float fillW = Mathf.Max(2f, bgW - InsetPx * 2f);
            float fillH = Mathf.Max(2f, bgH - InsetPx * 2f);
            Color tint = currentRatio <= 0.2f ? lowColor : (currentRatio <= 0.4f ? warnColor : okColor);
            tint.a = 1f;
            Color ghost = new Color(tint.r, tint.g, tint.b, pendingAlpha);

            if (Application.isPlaying || !Mathf.Approximately(LengthMul(), 1f))
                hud.sizeDelta = new Vector2(bgW, bgH);
            Stretch(bg.rectTransform);
            Paint(bg, bgSprite, true, Color.white, true);
            SetCenter(outline.rectTransform, Vector2.zero, new Vector2(bgW + OutlinePadPx, bgH + OutlinePadPx));
            Paint(outline, outlineSprite, true, Color.white, true);
            SetCenter(fillArea, Vector2.zero, new Vector2(fillW, fillH));

            bool showPending = pendingRatio > 0.001f && currentRatio > remainRatio + 0.001f;
            bool showRemain = remainRatio > 0.001f;
            bool showHot = hotGate > 0.001f && remainRatio > hotGate + 0.001f;
            LayoutClip(pendingClip, pendingFill, 0f, fillW * currentRatio, fillW, fillH, ghost, showPending);
            LayoutClip(remainClip, remainFill, 0f, fillW * remainRatio, fillW, fillH, tint, showRemain);
            LayoutClip(hotClip, hotFill, fillW * hotGate, fillW * Mathf.Max(0f, remainRatio - hotGate), fillW, fillH, hotColor, showHot);
            ApplyIconSprite();
        }

        private float LengthMul()
        {
            return (grow - 1f) * lengthGrowScale + 1f;
        }

        private void CaptureAuthoredSize()
        {
            if (hud == null) return;
            if (capturedSize && Application.isPlaying) return;
            authoredHudW = hud.sizeDelta.x;
            authoredHudH = hud.sizeDelta.y;
            if (authoredHudW < 1f) authoredHudW = DefaultHudW;
            if (authoredHudH < 1f) authoredHudH = DefaultHudH;
            capturedSize = true;
        }

        private void LayoutClip(
            RectTransform clip,
            Image fill,
            float start,
            float clipW,
            float fillW,
            float fillH,
            Color color,
            bool on)
        {
            if (clip == null) return;
            if (!clip.gameObject.activeSelf) clip.gameObject.SetActive(true);
            bool show = on && clipW > 0.5f;
            if (!show || fill == null)
            {
                if (fill != null) fill.enabled = false;
                return;
            }

            SetLeft(clip, start, new Vector2(clipW, fillH));
            float cap = fillSprite != null ? Mathf.Max(0f, fillSprite.border.x) : 0f;
            float shift = cap > 0f && clipW < cap ? cap : 0f;
            SetLeft(fill.rectTransform, -start - shift, new Vector2(fillW, fillH));
            Paint(fill, fillSprite, true, color, true);
        }

        /// <summary>只挂图，不改预制体里定好的大小和位置。</summary>
        private void ApplyIconSprite()
        {
            if (icon == null) return;
            if (iconSprite == null)
            {
                icon.gameObject.SetActive(false);
                return;
            }

            icon.gameObject.SetActive(true);
            Paint(icon, iconSprite, false, Color.white, true);
        }

        private void EnsureCanvas()
        {
            hud = transform.Find("Hud") as RectTransform;
            if (hud == null)
            {
                GameObject go = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
                go.transform.SetParent(transform, false);
                hud = go.GetComponent<RectTransform>();
            }

            hud.localPosition = Vector3.zero;
            hud.localScale = Vector3.one / Pixels;
            hud.anchorMin = hud.anchorMax = new Vector2(0.5f, 0.5f);
            hud.pivot = new Vector2(0.5f, 0.5f);
            Canvas canvas = hud.GetComponent<Canvas>();
            if (canvas == null) canvas = hud.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 28;
            GraphicRaycaster raycaster = hud.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;
        }

        private static RectTransform EnsureRect(string childName, Transform parent)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                RectTransform rt = existing as RectTransform;
                if (rt != null) return rt;
                return existing.gameObject.AddComponent<RectTransform>();
            }

            GameObject child = new GameObject(childName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        private static RectTransform EnsureClip(string childName, Transform parent)
        {
            RectTransform rt = EnsureRect(childName, parent);
            if (rt.GetComponent<RectMask2D>() == null)
                rt.gameObject.AddComponent<RectMask2D>();
            return rt;
        }

        private Image EnsureImage(string childName, Transform parent, bool sliced)
        {
            bool created = parent.Find(childName) == null;
            RectTransform rt = EnsureRect(childName, parent);
            Image image = rt.GetComponent<Image>();
            if (image == null) image = rt.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.fillCenter = true;
            if (created && childName == "Icon") InitIconRect(rt);
            return image;
        }

        private void InitIconRect(RectTransform rt)
        {
            float height = BgPx * 1.25f;
            float aspect = iconSprite != null
                ? iconSprite.rect.width / Mathf.Max(1f, iconSprite.rect.height)
                : 64f / 93f;
            SetCenter(rt, new Vector2(-BgPx, 0f), new Vector2(height * aspect, height));
        }

        private static void Paint(Image image, Sprite sprite, bool sliced, Color color, bool on)
        {
            if (image == null) return;
            if (!on || sprite == null)
            {
                image.enabled = false;
                return;
            }

            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.fillCenter = true;
            image.color = color;
            image.raycastTarget = false;
            image.enabled = true;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        private static void SetCenter(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        private static void SetLeft(RectTransform rt, float x, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = size;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        private void LoadSprites()
        {
            if (bgSprite == null) bgSprite = Resources.Load<Sprite>(TexRoot + "bg");
            if (outlineSprite == null) outlineSprite = Resources.Load<Sprite>(TexRoot + "outline");
            if (fillSprite == null) fillSprite = Resources.Load<Sprite>(TexRoot + "fill");
            if (iconSprite == null) iconSprite = Resources.Load<Sprite>(TexRoot + "体力icon");
        }

        private void StripLegacy()
        {
            DestroyNamed("Plate");
            DestroyNamed("Bg");
            DestroyNamed("Outline");
            DestroyNamed("Icon");
            DestroyNamed("Pending");
            DestroyNamed("Remain");
            DestroyNamed("Hot");
            DestroyNamed("PendingMask");
            DestroyNamed("RemainMask");
            DestroyNamed("HotMask");
            DestroyNamed("Dividers");
            for (int i = 0; i < MaxSlots; i++)
            {
                DestroyNamed("Track_" + i);
                DestroyNamed("Fill_" + i);
                DestroyNamed("HotFill_" + i);
                DestroyNamed("Preview_" + i);
            }
        }

        private void DestroyNamed(string childName)
        {
            Transform child = transform.Find(childName);
            if (child == null || child.name == "Hud") return;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        bool IsPrefabAsset()
        {
#if UNITY_EDITOR
            return !Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject);
#else
            return false;
#endif
        }
    }
}
