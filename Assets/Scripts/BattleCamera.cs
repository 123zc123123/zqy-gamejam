using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 顶视正交相机：取景框完整进画面（contain，不裁战斗区），软跟随自己的虫，夹在当前有效区内。
    /// 世界显示尺来自 HUD 的 defaultDesignSize（1080×1920）；窄屏等比例缩小这块框，不再 cover 裁切。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Camera))]
    public sealed class BattleCamera : MonoBehaviour
    {
        [SerializeField] private float padding = 1.6f;
        [SerializeField] private bool lockPortrait = true;
        [SerializeField] private bool fillView;

        private Camera cam;
        private int lastWidth;
        private int lastHeight;
        private Vector3 targetCenter;
        private float targetHalfW;
        private float targetHalfD;
        private Vector3 fromCenter;
        private float fromSize;
        private float toSize;
        private float settleDuration;
        private float settleElapsed = 1f;
        private bool hasFrame;
        private bool followEnabled;
        private bool godView;
        private bool chasing;
        private MatchController followMatch;
        private int followPlayerId;
        private float snapPullDuration;
        private float snapPullElapsed = 1f;
        private float introOpenHalfW;
        private float introOpenHalfD;
        private float introLastHalfW;
        private float introLastHalfD;
        private float introPanoramaHalfW;
        private float introPanoramaHalfD;
        private Vector3 introPanoramaCenter;
        private bool hasIntroScales;
        private bool hasPanorama;
        private float designAspect = 1080f / 1920f;
        private bool hasDesignAspect;

        public Camera Cam
        {
            get
            {
                if (cam == null) cam = GetComponent<Camera>();
                return cam;
            }
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (lockPortrait)
            {
                Screen.orientation = ScreenOrientation.Portrait;
                Screen.autorotateToPortrait = true;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = false;
                Screen.autorotateToLandscapeRight = false;
            }
        }

        private void OnEnable()
        {
            if (!hasFrame) FrameCurrentZone(0f);
            else ApplyImmediate();
        }

        private void LateUpdate()
        {
            Camera battleCam = Cam;
            int width = battleCam != null ? Mathf.Max(1, battleCam.pixelWidth) : Screen.width;
            int height = battleCam != null ? Mathf.Max(1, battleCam.pixelHeight) : Screen.height;
            bool resized = width != lastWidth || height != lastHeight;
            if (resized)
            {
                lastWidth = width;
                lastHeight = height;
                toSize = ComputeSize();
                if (followEnabled) ApplySize(toSize);
                else if (settleElapsed >= settleDuration) ApplyImmediate();
            }

            if (godView)
            {
                targetCenter = Vector3.zero;
                targetHalfW = Mathf.Max(0.01f, Rules.ArenaHalfWidth);
                targetHalfD = Mathf.Max(0.01f, Rules.ArenaHalfDepth);
                hasFrame = true;
                toSize = ComputeSize();
                ApplyImmediate();
                return;
            }

            if (followEnabled)
            {
                UpdateFollow();
                return;
            }

            if (settleDuration <= 0f || settleElapsed >= settleDuration) return;
            settleElapsed += Time.deltaTime;
            float u = Mathf.Clamp01(settleElapsed / Mathf.Max(0.0001f, settleDuration));
            u = u * u * (3f - 2f * u);
            Vector3 pos = Vector3.Lerp(fromCenter, targetCenter, u);
            transform.position = new Vector3(pos.x, transform.position.y, pos.z);
            ApplySize(Mathf.Lerp(fromSize, toSize, u));
        }

        /// <summary>
        /// 按当前渲染目标宽高比把当前框（加边）塞进画面。
        /// 嵌进 HUD 的 Battlefield 时用 RenderTexture 的像素尺寸，而不是整个 Game 窗口。
        /// </summary>
        public void Fit()
        {
            if (!hasFrame)
            {
                FrameCurrentZone(0f);
                return;
            }

            toSize = ComputeSize();
            if (followEnabled) ApplySize(toSize);
            else if (settleElapsed >= settleDuration) ApplyImmediate();
        }

        /// <summary>世界显示：取景框完整放进 defaultDesignSize / Battlefield，不裁战斗区域。</summary>
        public void UseHudFill()
        {
            fillView = false;
            padding = 0f;
            Fit();
        }

        /// <summary>镜头宽高比锁在设计框上，不用整屏或被裁过的 RenderTexture。</summary>
        public void UseDesignFrame(float width, float height)
        {
            designAspect = Mathf.Max(0.01f, width) / Mathf.Max(0.01f, height);
            hasDesignAspect = true;
            Fit();
        }

        public void FrameWorld(Vector3 center, float halfW, float halfD, float duration)
        {
            followEnabled = false;
            Camera battleCam = Cam;
            if (battleCam == null) return;
            battleCam.orthographic = true;
            lastWidth = Mathf.Max(1, battleCam.pixelWidth);
            lastHeight = Mathf.Max(1, battleCam.pixelHeight);
            fromCenter = transform.position;
            fromSize = battleCam.orthographicSize;
            targetCenter = new Vector3(center.x, transform.position.y, center.z);
            targetHalfW = Mathf.Max(0.01f, halfW);
            targetHalfD = Mathf.Max(0.01f, halfD);
            hasFrame = true;
            toSize = ComputeSize();
            settleDuration = Mathf.Max(0f, duration);
            settleElapsed = settleDuration <= 0f ? 1f : 0f;
            if (settleDuration <= 0f) ApplyImmediate();
        }

        /// <summary>开场全景用大背景相对桌子的尺寸；落到己方角时窗口用末档。</summary>
        public void UseIntroScales(float openingScale, float lastScale)
        {
            openingScale = Mathf.Max(0.01f, openingScale);
            lastScale = Mathf.Max(0.01f, lastScale);
            introOpenHalfW = Rules.DefaultArenaHalfWidth * openingScale;
            introOpenHalfD = Rules.DefaultArenaHalfDepth * openingScale;
            introLastHalfW = Rules.DefaultArenaHalfWidth * lastScale;
            introLastHalfD = Rules.DefaultArenaHalfDepth * lastScale;
            hasIntroScales = true;
        }

        public void UseIntroZone(MatchKnobs knobs)
        {
            Vector2 open = Rules.ZoneHalfExtents(knobs, 0);
            Vector2 last = Rules.ZoneHalfExtents(knobs, Rules.LastZoneTier);
            introOpenHalfW = Mathf.Max(0.01f, open.x);
            introOpenHalfD = Mathf.Max(0.01f, open.y);
            introLastHalfW = Mathf.Max(0.01f, last.x);
            introLastHalfD = Mathf.Max(0.01f, last.y);
            hasIntroScales = true;
        }

        /// <summary>
        /// 桌子对应开局档世界；大背景按预制体相对桌子的宽高，映射成开场全景框。
        /// bgCenterOffset 是背景中心相对桌子中心、与 tableSize 同一套 UI 单位。
        /// 不改 Board / table / bg 的预制体尺寸。
        /// </summary>
        public void UsePanoramaArt(Vector2 tableSize, Vector2 bgSize, Vector2 bgCenterOffset)
        {
            float tableW = Mathf.Max(1f, tableSize.x);
            float tableH = Mathf.Max(1f, tableSize.y);
            float bgW = Mathf.Max(1f, bgSize.x);
            float bgH = Mathf.Max(1f, bgSize.y);
            float openW = hasIntroScales ? introOpenHalfW : Rules.DefaultArenaHalfWidth;
            float openD = hasIntroScales ? introOpenHalfD : Rules.DefaultArenaHalfDepth;
            introPanoramaHalfW = openW * (bgW / tableW);
            introPanoramaHalfD = openD * (bgH / tableH);
            introPanoramaCenter = new Vector3(
                openW * 2f * (bgCenterOffset.x / tableW),
                0f,
                openD * 2f * (bgCenterOffset.y / tableH));
            hasPanorama = true;
        }

        public void FrameOpeningPanorama()
        {
            float halfW = hasPanorama
                ? introPanoramaHalfW
                : (hasIntroScales ? introOpenHalfW : Rules.ArenaHalfWidth);
            float halfD = hasPanorama
                ? introPanoramaHalfD
                : (hasIntroScales ? introOpenHalfD : Rules.ArenaHalfDepth);
            fillView = false;
            padding = 0f;
            FrameWorld(hasPanorama ? introPanoramaCenter : Vector3.zero, halfW, halfD, 0f);
        }

        public void FrameCorner(int playerId, float duration)
        {
            Vector2 sign = Rules.CornerSign(playerId);
            Vector3 center = new Vector3(
                sign.x * Rules.ArenaHalfWidth * 0.5f,
                0f,
                sign.y * Rules.ArenaHalfDepth * 0.5f);
            float halfW = hasIntroScales ? introLastHalfW : Rules.ArenaHalfWidth * 0.5f;
            float halfD = hasIntroScales ? introLastHalfD : Rules.ArenaHalfDepth * 0.5f;
            fillView = false;
            padding = 0f;
            FrameWorld(center, halfW, halfD, duration);
        }

        public void FrameCurrentZone(float duration)
        {
            fillView = false;
            padding = 0f;
            FrameWorld(Vector3.zero, Rules.ArenaHalfWidth, Rules.ArenaHalfDepth, duration);
        }

        /// <summary>观战上帝视角：镜头拉到整块当前有效区，不再跟人。</summary>
        public void EnterGodView()
        {
            godView = true;
            followEnabled = false;
            fillView = false;
            padding = 0f;
            FrameWorld(Vector3.zero, Rules.ArenaHalfWidth, Rules.ArenaHalfDepth, 0f);
        }

        /// <summary>对局中软跟随自己的虫；窗口锁成最后一档大小。</summary>
        public void FollowLocalPlayer(MatchController match, int playerId)
        {
            godView = false;
            followMatch = match;
            followPlayerId = playerId;
            followEnabled = true;
            chasing = true;
            snapPullElapsed = 1f;
            fillView = false;
            padding = 0f;
            Vector2 last = Rules.ZoneHalfExtents(match != null ? match.Knobs : null, Rules.LastZoneTier);
            targetHalfW = Mathf.Max(0.01f, last.x);
            targetHalfD = Mathf.Max(0.01f, last.y);
            hasFrame = true;
            toSize = ComputeSize();
            ApplySize(toSize);
        }

        /// <summary>收口后把镜头收进新有效区，再继续跟人。</summary>
        public void PullIntoZone(float duration)
        {
            snapPullDuration = Mathf.Max(0f, duration);
            snapPullElapsed = snapPullDuration <= 0f ? 1f : 0f;
            if (snapPullDuration > 0f || !followEnabled) return;
            Vector2 half = ViewHalf();
            Vector3 pos = Rules.ClampCameraCenter(transform.position, half.x, half.y);
            transform.position = new Vector3(pos.x, transform.position.y, pos.z);
        }

        /// <summary>
        /// 场地在相机画面中的归一化矩形。原点在左下，宽高为 0~1。
        /// UI 用它把摇杆摆到罐子上，而不是整个 Game 窗口的角落。
        /// </summary>
        public Rect ArenaViewNormalized()
        {
            Camera battleCam = Cam;
            if (battleCam == null) return new Rect(0f, 0f, 1f, 1f);
            float viewHeight = 2f * Mathf.Max(0.01f, battleCam.orthographicSize);
            float viewWidth = viewHeight * FrameAspect();
            float arenaWidth = (hasFrame ? targetHalfW : Rules.ArenaHalfWidth) * 2f;
            float arenaHeight = (hasFrame ? targetHalfD : Rules.ArenaHalfDepth) * 2f;
            float width = Mathf.Clamp01(arenaWidth / viewWidth);
            float height = Mathf.Clamp01(arenaHeight / viewHeight);
            return new Rect(0.5f - width * 0.5f, 0.5f - height * 0.5f, width, height);
        }

        private void UpdateFollow()
        {
            Camera battleCam = Cam;
            if (battleCam == null) return;
            Vector2 half = ViewHalf();
            Vector3 now = transform.position;
            Vector3 desired = now;
            Vector3 player;
            bool havePlayer = TryFollowPosition(out player);
            MatchKnobs knobs = followMatch != null ? followMatch.Knobs : null;
            float deadzone = knobs != null ? Mathf.Clamp01(knobs.camDeadzone) : 0.15f;
            float followT = knobs != null ? Mathf.Max(0.05f, knobs.camFollowT) : 0.22f;

            if (havePlayer)
            {
                float ox = player.x - now.x;
                float oz = player.z - now.z;
                if (Mathf.Abs(ox) > half.x * deadzone || Mathf.Abs(oz) > half.y * deadzone)
                    chasing = true;
                if (Mathf.Abs(ox) < 0.08f && Mathf.Abs(oz) < 0.08f)
                    chasing = false;
                if (chasing)
                {
                    desired.x = player.x;
                    desired.z = player.z;
                }
            }

            desired = Rules.ClampCameraCenter(desired, half.x, half.y);
            Vector3 clampedNow = Rules.ClampCameraCenter(now, half.x, half.y);
            float tau = followT;
            if ((now - clampedNow).sqrMagnitude > 0.0001f)
            {
                float settle = knobs != null ? Mathf.Max(0.05f, knobs.camSettleT) : 0.8f;
                if (snapPullElapsed < snapPullDuration)
                    settle = Mathf.Max(0.05f, snapPullDuration);
                tau = Mathf.Min(tau, settle);
            }

            if (snapPullElapsed < snapPullDuration)
                snapPullElapsed += Time.deltaTime;

            float u = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, tau));
            Vector3 next = Vector3.Lerp(now, desired, u);
            next = Rules.ClampCameraCenter(next, half.x, half.y);
            transform.position = new Vector3(next.x, now.y, next.z);
            ApplySize(toSize);
        }

        private bool TryFollowPosition(out Vector3 player)
        {
            player = Vector3.zero;
            if (followMatch == null || followMatch.State == null || followMatch.State.bugs == null)
                return false;
            BugState[] bugs = followMatch.State.bugs;
            if (followPlayerId < 0 || followPlayerId >= bugs.Length) return false;
            BugState bug = bugs[followPlayerId];
            if (bug == null || !bug.alive) return false;
            player = bug.position;
            return true;
        }

        private Vector2 ViewHalf()
        {
            Camera battleCam = Cam;
            float size = battleCam != null ? Mathf.Max(0.01f, battleCam.orthographicSize) : 1f;
            return new Vector2(size * FrameAspect(), size);
        }

        private float FrameAspect()
        {
            if (hasDesignAspect) return Mathf.Max(0.01f, designAspect);
            Camera battleCam = Cam;
            return battleCam != null ? Mathf.Max(0.01f, battleCam.aspect) : 1f;
        }

        private void ApplyImmediate()
        {
            transform.position = new Vector3(targetCenter.x, transform.position.y, targetCenter.z);
            ApplySize(toSize);
        }

        private void ApplySize(float size)
        {
            Camera battleCam = Cam;
            if (battleCam == null) return;
            battleCam.orthographic = true;
            battleCam.orthographicSize = Mathf.Max(0.01f, size);
        }

        private float ComputeSize()
        {
            Camera battleCam = Cam;
            float aspect = hasDesignAspect ? designAspect : 1f;
            if (battleCam != null)
            {
                lastWidth = Mathf.Max(1, battleCam.pixelWidth);
                lastHeight = Mathf.Max(1, battleCam.pixelHeight);
                if (!hasDesignAspect)
                    aspect = lastWidth / (float)lastHeight;
            }

            float pad = Mathf.Max(0f, padding);
            float halfW = (hasFrame ? targetHalfW : Rules.ArenaHalfWidth) + pad;
            float halfD = (hasFrame ? targetHalfD : Rules.ArenaHalfDepth) + pad;
            float contain = Mathf.Max(halfD, halfW / Mathf.Max(0.01f, aspect));
            float cover = Mathf.Min(halfD, halfW / Mathf.Max(0.01f, aspect));
            return fillView ? cover : contain;
        }
    }
}
