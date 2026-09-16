using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace DouQuqu
{
    /// <summary>
    /// 战斗美术 HUD：把 Demo 的真实对战场画进中间 Battlefield。
    /// Battlefield 上已有 Image，不能再挂 RawImage，所以在子节点 BattleView 里显示。
    /// </summary>
    public sealed class BattleHudBinder : MonoBehaviour
    {
        private RectTransform pit;
        private RawImage view;
        private Camera battleCam;
        private Camera hudCam;
        private BattleCamera fitter;
        private RenderTexture target;
        private Vector2Int lastPixels;
        private MatchController boundMatch;
        private int localPlayerId;
        private RectTransform boardRoot;
        private BattleTableShrink tableShrink;
        private static BattleHudBinder instance;

        private void Awake()
        {
            instance = this;
        }

        private IEnumerator Start()
        {
            pit = FindNamed(transform, "Battlefield") as RectTransform;
            if (pit == null)
            {
                Debug.LogWarning("[DouQuqu] 战斗 HUD 里没有 Battlefield，无法嵌入对战场。");
                yield break;
            }

            boardRoot = FindNamed(transform, "Board") as RectTransform;
            if (boardRoot != null) boardRoot.gameObject.SetActive(true);
            BattleIntro.HideChrome(transform as RectTransform);
            if (boardRoot != null) boardRoot.gameObject.SetActive(true);

            Canvas hud = GetComponent<Canvas>();
            if (hud != null)
            {
                hud.renderMode = RenderMode.ScreenSpaceOverlay;
                hud.sortingOrder = 100;
                hud.enabled = true;
            }

            RectMask2D clip = GetComponent<RectMask2D>();
            if (clip == null) clip = gameObject.AddComponent<RectMask2D>();
            clip.enabled = true;

            // 必须先摘掉 HUD 的 MainCamera，否则 Demo 会把顶视组件挂到平视相机上，
            // 场地在 XZ 平面会被拍成一条细线。
            hudCam = Camera.main;
            if (hudCam != null)
            {
                hudCam.tag = "Untagged";
                hudCam.cullingMask = 0;
                hudCam.clearFlags = CameraClearFlags.SolidColor;
                hudCam.backgroundColor = Color.black;
                hudCam.depth = -2;
                hudCam.targetTexture = null;
                hudCam.enabled = true;
            }

            PreparePitView();
            FitPitToDesign();
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return LoadDemoIfNeeded();
            BindZoneCamera();
            ApplyFieldZoneScales();
            BindBattleCamera();
            BindBoardFollow();
            BindTableShrink();
            RefreshTarget(true);
            SilenceHudRaycasts();
            BindMatchClock();
            BindScoreHud();
            GroundMarker.SyncFromHud(transform);
            bool dropToCorner = boundMatch != null && boundMatch.ConfiguredPlayers > 1;
            if (fitter != null)
            {
                MatchKnobs knobs = boundMatch != null ? boundMatch.Knobs : null;
                if (knobs != null)
                    fitter.UseIntroScales(knobs.zoneScale0, Rules.LastZoneScale(knobs));
                BindIntroPanorama();
                fitter.FrameOpeningPanorama();
            }
            yield return BattleIntro.Play(transform as RectTransform, pit, fitter, localPlayerId, dropToCorner);
            if (fitter != null) fitter.FollowLocalPlayer(boundMatch, localPlayerId);
            RefreshTarget(true);
            BindStick();
            BindMatchClock();
            BindScoreHud();
            GroundMarker.SyncFromHud(transform);
            StartMatchIfNeeded();
        }

        private void LateUpdate()
        {
            if (boardRoot == null)
                boardRoot = FindNamed(transform, "Board") as RectTransform;
            if (boardRoot != null && !boardRoot.gameObject.activeSelf)
                boardRoot.gameObject.SetActive(true);
            TickTableShrinkWarn();
            if (pit == null || battleCam == null || view == null) return;
            RefreshTarget(false);
        }

        private void TickTableShrinkWarn()
        {
            if (tableShrink == null || boundMatch == null) return;
            MatchKnobs knobs = boundMatch.Knobs;
            if (knobs == null || !boundMatch.ZoneWarn)
            {
                tableShrink.SetWarn(-1, 0f);
                return;
            }

            int current = boundMatch.ZoneTier;
            int next = Rules.ZoneWarnTier(knobs, boundMatch.Elapsed);
            float[] snaps = Rules.ZoneSnapTimes(knobs);
            if (current < 0 || current >= snaps.Length || next <= current)
            {
                tableShrink.SetWarn(-1, 0f);
                return;
            }

            float warn = Mathf.Max(0.0001f, knobs.zoneWarnT);
            float progress = 1f - (snaps[current] - boundMatch.Elapsed) / warn;
            tableShrink.SetWarn(next, Mathf.Clamp01(progress));
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (boundMatch != null)
            {
                boundMatch.ZoneSnapped -= OnZoneSnapped;
                boundMatch.GameplayEvent -= OnGameplayEvent;
            }
            ReleaseTarget();
            Rules.ResetArenaSize();
        }

        private void BindZoneCamera()
        {
            boundMatch = UnityEngine.Object.FindObjectOfType<MatchController>();
            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            localPlayerId = network != null && network.LocalPlayerId >= 0 ? network.LocalPlayerId : 0;
            if (boundMatch != null)
            {
                boundMatch.ZoneSnapped += OnZoneSnapped;
                boundMatch.GameplayEvent -= OnGameplayEvent;
                boundMatch.GameplayEvent += OnGameplayEvent;
            }
        }

        private void OnGameplayEvent(string kind, Vector3 world)
        {
            int amount;
            if (TryParseTagged(kind, "steal-gain:", out amount))
                ShowStaminaDelta(world, "耐力+" + amount, new Color(1f, 0.35f, 0.82f, 1f));
            else if (TryParseTagged(kind, "steal-loss:", out amount))
                ShowStaminaDelta(world, "耐力-" + amount, new Color(1f, 0.18f, 0.16f, 1f));
        }

        static bool TryParseTagged(string kind, string prefix, out int amount)
        {
            amount = 0;
            if (string.IsNullOrEmpty(kind) || !kind.StartsWith(prefix)) return false;
            return int.TryParse(kind.Substring(prefix.Length), out amount);
        }

        static float lastDeltaAt = -1f;
        static string lastDeltaText;

        public static void ShowStaminaDelta(Vector3 world, string text, Color color)
        {
            if (text == lastDeltaText && Time.unscaledTime - lastDeltaAt < 0.05f) return;
            lastDeltaText = text;
            lastDeltaAt = Time.unscaledTime;
            RectTransform canvasRt = EnsureDeltaCanvas();
            if (canvasRt == null) return;

            Vector2 screen = ToScreen(world);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out local);

            GameObject go = new GameObject("StaminaDelta");
            go.transform.SetParent(canvasRt, false);
            go.transform.SetAsLastSibling();
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = local;
            rt.sizeDelta = new Vector2(220f, 56f);

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/Chinese SDF");
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = 36f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            if (font != null)
            {
                label.fontMaterial = new Material(font.material);
                label.fontMaterial.SetColor("_FaceColor", Color.white);
            }
            label.color = color;
            label.faceColor = color;
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color(0f, 0f, 0f, 0.9f);
            go.AddComponent<HudStaminaDrift>().Begin(2.2f);
        }

        static RectTransform EnsureDeltaCanvas()
        {
            GameObject found = GameObject.Find("StaminaDeltaCanvas");
            if (found != null) return found.transform as RectTransform;
            GameObject go = new GameObject("StaminaDeltaCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            canvas.overrideSorting = true;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;
            GraphicRaycaster raycaster = go.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;
            Object.DontDestroyOnLoad(go);
            return go.transform as RectTransform;
        }

        static Vector2 ToScreen(Vector3 world)
        {
            if (instance != null && instance.battleCam != null && instance.view != null)
            {
                Vector3 vp = instance.battleCam.WorldToViewportPoint(world);
                Vector3[] corners = new Vector3[4];
                instance.view.rectTransform.GetWorldCorners(corners);
                return new Vector2(
                    Mathf.Lerp(corners[0].x, corners[2].x, Mathf.Clamp01(vp.x)),
                    Mathf.Lerp(corners[0].y, corners[2].y, Mathf.Clamp01(vp.y)));
            }
            Camera cam = instance != null ? instance.battleCam : Camera.main;
            if (cam != null && cam.targetTexture == null)
            {
                Vector3 screen = cam.WorldToScreenPoint(world);
                return new Vector2(screen.x, screen.y);
            }
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
        }

        private void BindBoardFollow()
        {
            RectTransform board = FindNamed(transform, "Board") as RectTransform;
            if (board == null || pit == null) return;
            boardRoot = board;
            board.gameObject.SetActive(true);
            if (pit.parent == board && board.parent != null)
            {
                int index = board.GetSiblingIndex();
                pit.SetParent(board.parent, true);
                pit.SetSiblingIndex(index + 1);
                pit.localScale = Vector3.one;
            }

            BattleBoardFollow.Bind(board, pit, fitter, OpeningArtScale());
        }

        private void BindTableShrink()
        {
            tableShrink = BattleTableShrink.Bind(transform as RectTransform, pit);
            if (tableShrink == null || boundMatch == null) return;
            tableShrink.SnapTo(boundMatch.ZoneTier, 0f, true);
        }

        /// <summary>
        /// 三档场地边长跟 Battlefield 里 field-0 / field-1 / field-2 走。
        /// 末档 field-2 = 1，另外两档按相对它的矩形尺寸。
        /// </summary>
        private void ApplyFieldZoneScales()
        {
            if (pit == null || boundMatch == null) return;
            MatchKnobs knobs = boundMatch.Knobs;
            if (knobs == null) return;
            RectTransform field0 = FindNamed(pit, "field-0") as RectTransform;
            RectTransform field1 = FindNamed(pit, "field-1") as RectTransform;
            RectTransform field2 = FindNamed(pit, "field-2") as RectTransform;
            if (field0 == null || field1 == null || field2 == null) return;

            float baseline = FieldSpan(field2);
            if (baseline < 1f) return;
            knobs.zoneScale0 = Mathf.Max(0.01f, FieldSpan(field0) / baseline);
            knobs.zoneScale1 = Mathf.Max(0.01f, FieldSpan(field1) / baseline);
            knobs.zoneScale2 = 1f;
            if (boundMatch.State != null) boundMatch.State.knobs = knobs;
            if (boundMatch.ConfiguredPlayers <= 1)
                Rules.SetArenaScale(Rules.LastZoneScale(knobs));
            else
                Rules.ApplyZoneAt(knobs, boundMatch.Elapsed);
        }

        private static float FieldSpan(RectTransform field)
        {
            Rect rect = field.rect;
            return 0.5f * (Mathf.Abs(rect.width) + Mathf.Abs(rect.height));
        }

        private float OpeningArtScale()
        {
            if (boundMatch != null && boundMatch.Knobs != null)
                return Mathf.Max(0.01f, boundMatch.Knobs.zoneScale0);
            return 2f;
        }

        /// <summary>
        /// 开场全景按 bg 整组（bigBg + foucusBg）相对桌子的尺寸框。
        /// 不改 table / bigBg / foucusBg 的预制体尺寸。
        /// </summary>
        private void BindIntroPanorama()
        {
            if (fitter == null) return;
            RectTransform table = FindNamed(transform, "BattleTable") as RectTransform;
            RectTransform bg = FindNamed(transform, "bg") as RectTransform;
            if (bg == null) bg = FindNamed(transform, "bigBg") as RectTransform;
            if (bg == null) bg = FindNamed(transform, "ArenaBackgroundScenery") as RectTransform;
            if (table == null || bg == null) return;
            AlignBgScaleToTable(bg, table);
            Vector2 tableSize = WorldRectSize(table);
            Vector2 bgSize = WorldRectSize(bg);
            fitter.UsePanoramaArt(tableSize, bgSize, WorldCenterOffset(bg, table));
        }

        /// <summary>
        /// bigBg 与 foucusBg 保持相对关系；只把它们的父节点 bg 缩放到和桌子同一套 localScale。
        /// </summary>
        private static void AlignBgScaleToTable(RectTransform bg, RectTransform table)
        {
            if (bg == null || table == null) return;
            Vector3 scale = table.localScale;
            if (scale.x < 0.01f) scale.x = 1f;
            if (scale.y < 0.01f) scale.y = 1f;
            if (scale.z < 0.01f) scale.z = 1f;
            bg.localScale = scale;
        }

        private static Vector2 WorldRectSize(RectTransform rect)
        {
            if (rect == null) return Vector2.one;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return new Vector2(
                Vector3.Distance(corners[0], corners[3]),
                Vector3.Distance(corners[0], corners[1]));
        }

        private static Vector2 WorldCenterOffset(RectTransform from, RectTransform origin)
        {
            if (from == null || origin == null) return Vector2.zero;
            Vector3[] fromCorners = new Vector3[4];
            Vector3[] originCorners = new Vector3[4];
            from.GetWorldCorners(fromCorners);
            origin.GetWorldCorners(originCorners);
            Vector3 fromCenter = (fromCorners[0] + fromCorners[2]) * 0.5f;
            Vector3 originCenter = (originCorners[0] + originCorners[2]) * 0.5f;
            return new Vector2(fromCenter.x - originCenter.x, fromCenter.y - originCenter.y);
        }

        private void OnZoneSnapped(int tier)
        {
            float fade = boundMatch != null && boundMatch.Knobs != null
                ? boundMatch.Knobs.zoneFadeT
                : 0.5f;
            if (tableShrink != null) tableShrink.SnapTo(tier, fade, false);
            if (fitter == null || boundMatch == null) return;
            float settle = boundMatch.Knobs != null ? boundMatch.Knobs.camSettleT : 0.8f;
            fitter.PullIntoZone(settle);
        }

        private void PreparePitView()
        {
            // 战场在背景之上、头像/轨道之下，HUD 贴图盖住 3D 边缘。
            if (pit.parent != null && pit.parent.childCount > 1)
                pit.SetSiblingIndex(1);

            UnityEngine.UI.Image image = pit.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                image.enabled = false;
                image.raycastTarget = false;
                image.color = Color.clear;
            }

            Transform ring = pit.Find("PitRing");
            if (ring != null) Destroy(ring.gameObject);

            for (int i = 0; i < pit.childCount; i++)
            {
                Transform child = pit.GetChild(i);
                if (child == null || !child.name.StartsWith("field-")) continue;
                UnityEngine.UI.Image fieldImage = child.GetComponent<UnityEngine.UI.Image>();
                if (fieldImage != null) fieldImage.enabled = false;
                UnityEngine.UI.Outline fieldOutline = child.GetComponent<UnityEngine.UI.Outline>();
                if (fieldOutline != null) fieldOutline.enabled = false;
            }

            Transform display = pit.Find("BattleView");
            if (display == null)
            {
                GameObject go = new GameObject("BattleView", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                display = go.transform;
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.SetParent(pit, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }

            view = display.GetComponent<RawImage>();
            if (view == null) view = display.gameObject.AddComponent<RawImage>();
            view.color = Color.white;
            view.raycastTarget = false;
            view.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        /// <summary>对战场铺满 1080×1920 设计画布，不再用旧罐子窗 920×1369。</summary>
        private void FitPitToDesign()
        {
            if (pit == null) return;
            pit.anchorMin = Vector2.zero;
            pit.anchorMax = Vector2.one;
            pit.pivot = new Vector2(0.5f, 0.5f);
            pit.offsetMin = Vector2.zero;
            pit.offsetMax = Vector2.zero;
            pit.localScale = Vector3.one;
            pit.localRotation = Quaternion.identity;
        }

        private static IEnumerator LoadDemoIfNeeded()
        {
            Scene demo = SceneManager.GetSceneByName(SceneNames.BattleDemo);
            if (demo.IsValid() && demo.isLoaded) yield break;

            AsyncOperation op = SceneManager.LoadSceneAsync(SceneNames.BattleDemo, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError("[DouQuqu] 无法加载战斗场景 " + SceneNames.BattleDemo);
                yield break;
            }

            while (!op.isDone) yield return null;
        }

        private void BindBattleCamera()
        {
            battleCam = FindDemoCamera();
            if (battleCam == null)
            {
                Debug.LogError("[DouQuqu] 战斗场景里没有相机。");
                return;
            }

            BattleCamera stray = hudCam != null ? hudCam.GetComponent<BattleCamera>() : null;
            if (stray != null) Destroy(stray);

            fitter = battleCam.GetComponent<BattleCamera>();
            if (fitter == null) fitter = battleCam.gameObject.AddComponent<BattleCamera>();
            fitter.UseHudFill();

            battleCam.transform.position = new Vector3(0f, 50f, 0f);
            battleCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            battleCam.tag = "MainCamera";
            battleCam.enabled = true;
            battleCam.orthographic = true;
            battleCam.clearFlags = CameraClearFlags.SolidColor;
            Color clearSand = BattleBoard.Sand;
            clearSand.a = 0f;
            battleCam.backgroundColor = clearSand;
            battleCam.depth = -1;
            BattleBoard.HideSurface();

            AudioListener keep = battleCam.GetComponent<AudioListener>();
            if (keep == null) keep = battleCam.gameObject.AddComponent<AudioListener>();
            keep.enabled = true;
            AudioListener[] listeners = UnityEngine.Object.FindObjectsOfType<AudioListener>();
            for (int i = 0; i < listeners.Length; i++)
                if (listeners[i] != null && listeners[i] != keep) listeners[i].enabled = false;

            UIDocument stickHud = UnityEngine.Object.FindObjectOfType<UIDocument>();
            if (stickHud != null) stickHud.enabled = false;
            TouchInput touch = UnityEngine.Object.FindObjectOfType<TouchInput>();
            if (touch != null) touch.enabled = false;
        }

        private void SilenceHudRaycasts()
        {
            UnityEngine.UI.Image[] images = GetComponentsInChildren<UnityEngine.UI.Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null) continue;
                if (images[i].GetComponent<HudStick>() != null) continue;
                if (images[i].GetComponentInParent<EliminationPage>() != null) continue;
                images[i].raycastTarget = false;
            }

            RawImage[] raws = GetComponentsInChildren<RawImage>(true);
            for (int i = 0; i < raws.Length; i++)
                if (raws[i] != null) raws[i].raycastTarget = false;
        }

        private static void StartMatchIfNeeded()
        {
            MatchController match = UnityEngine.Object.FindObjectOfType<MatchController>();
            if (match != null && !match.IsStarted)
                match.StartMatch();
        }

        private void BindMatchClock()
        {
            Transform clock = FindNamed(transform, "Countdown");
            if (clock == null) return;
            MatchClockHud hud = clock.GetComponent<MatchClockHud>();
            if (hud == null) hud = clock.gameObject.AddComponent<MatchClockHud>();
            hud.Bind(UnityEngine.Object.FindObjectOfType<MatchController>());
        }

        private void BindScoreHud()
        {
            BattleScoreHud hud = GetComponent<BattleScoreHud>();
            if (hud == null) hud = gameObject.AddComponent<BattleScoreHud>();
            hud.Bind(UnityEngine.Object.FindObjectOfType<MatchController>());
        }

        private void BindStick()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (pit == null || canvas == null || canvas.transform.Find("HudStick") != null) return;
            MatchController match = UnityEngine.Object.FindObjectOfType<MatchController>();
            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            int localId = network != null && network.LocalPlayerId >= 0 ? network.LocalPlayerId : 0;
            HudStick.Create(pit, canvas, match, localId, network);
        }

        private static Camera FindDemoCamera()
        {
            Scene demo = SceneManager.GetSceneByName(SceneNames.BattleDemo);
            if (demo.IsValid() && demo.isLoaded)
            {
                GameObject[] roots = demo.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    Camera cam = roots[i].GetComponentInChildren<Camera>(true);
                    if (cam != null) return cam;
                }
            }

            return Camera.main;
        }

        private void RefreshTarget(bool force)
        {
            if (pit == null || battleCam == null || view == null) return;
            Vector2Int pixels = PixelSize(pit);
            if (!force && target != null
                && Mathf.Abs(pixels.x - lastPixels.x) < 8
                && Mathf.Abs(pixels.y - lastPixels.y) < 8)
                return;

            lastPixels = pixels;
            if (target != null)
            {
                battleCam.targetTexture = null;
                view.texture = null;
                target.Release();
                Destroy(target);
                target = null;
            }

            // The cricket outline shader uses stencil to keep its eight outline passes
            // from painting back over the sprite body. A 16-bit depth target has no
            // stencil attachment, while the 24-bit variant requests one.
            target = new RenderTexture(pixels.x, pixels.y, 24, RenderTextureFormat.ARGB32)
            {
                name = "Battlefield",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            target.Create();
            battleCam.targetTexture = target;
            view.texture = target;
            if (fitter != null) fitter.Fit();
        }

        private void ReleaseTarget()
        {
            if (battleCam != null) battleCam.targetTexture = null;
            if (view != null) view.texture = null;
            if (target == null) return;
            target.Release();
            Destroy(target);
            target = null;
        }

        private static Vector2Int PixelSize(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            int w = Mathf.Max(32, Mathf.RoundToInt(Mathf.Abs(corners[2].x - corners[0].x)));
            int h = Mathf.Max(32, Mathf.RoundToInt(Mathf.Abs(corners[2].y - corners[0].y)));
            return new Vector2Int(Mathf.Min(w, 2048), Mathf.Min(h, 2048));
        }

        private static Transform FindNamed(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamed(root.GetChild(i), objectName);
                if (hit != null) return hit;
            }

            return null;
        }
    }

    sealed class HudStaminaDrift : MonoBehaviour
    {
        float life = 1.1f;
        float age;
        RectTransform rt;
        Vector2 start;
        CanvasGroup group;

        public void Begin(float duration)
        {
            life = Mathf.Max(0.2f, duration);
            rt = transform as RectTransform;
            start = rt != null ? rt.anchoredPosition : Vector2.zero;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / life);
            if (rt != null) rt.anchoredPosition = start + Vector2.up * (28f * t);
            if (group != null) group.alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
