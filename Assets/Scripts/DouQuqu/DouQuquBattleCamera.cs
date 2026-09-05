using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 顶视正交相机：把整罐场地框进当前屏幕，竖屏时场地占满大部分画面。
    /// 摇杆按场地在屏幕上的矩形来摆，叠在罐子上，而不是贴在窗口空白角落。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class DouQuquBattleCamera : MonoBehaviour
    {
        [SerializeField] private float padding = 1.6f;
        [SerializeField] private bool lockPortrait = true;
        [SerializeField] private bool fillView;

        private Camera cam;
        private int lastWidth;
        private int lastHeight;

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
            Fit();
        }

        private void LateUpdate()
        {
            Camera battleCam = Cam;
            int width = battleCam != null ? Mathf.Max(1, battleCam.pixelWidth) : Screen.width;
            int height = battleCam != null ? Mathf.Max(1, battleCam.pixelHeight) : Screen.height;
            if (width != lastWidth || height != lastHeight)
                Fit();
        }

        /// <summary>
        /// 按当前渲染目标宽高比把整场（加边）塞进画面。
        /// 嵌进 HUD 的 Battlefield 时用 RenderTexture 的像素尺寸，而不是整个 Game 窗口。
        /// </summary>
        public void Fit()
        {
            Camera battleCam = Cam;
            if (battleCam == null) return;
            battleCam.orthographic = true;
            lastWidth = Mathf.Max(1, battleCam.pixelWidth);
            lastHeight = Mathf.Max(1, battleCam.pixelHeight);
            float aspect = lastWidth / (float)lastHeight;
            float pad = Mathf.Max(0f, padding);
            float needX = DouQuquRules.ArenaHalfWidth + pad;
            float needZ = DouQuquRules.ArenaHalfDepth + pad;
            float contain = Mathf.Max(needZ, needX / Mathf.Max(0.01f, aspect));
            float cover = Mathf.Min(needZ, needX / Mathf.Max(0.01f, aspect));
            battleCam.orthographicSize = fillView ? cover : contain;
        }

        /// <summary>嵌进 HUD 时铺满 Battlefield，避免罐子两侧留出空边。</summary>
        public void UseHudFill()
        {
            fillView = true;
            padding = 0f;
            Fit();
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
            float viewWidth = viewHeight * Mathf.Max(0.01f, battleCam.aspect);
            float arenaWidth = DouQuquRules.ArenaHalfWidth * 2f;
            float arenaHeight = DouQuquRules.ArenaHalfDepth * 2f;
            float width = Mathf.Clamp01(arenaWidth / viewWidth);
            float height = Mathf.Clamp01(arenaHeight / viewHeight);
            return new Rect(0.5f - width * 0.5f, 0.5f - height * 0.5f, width, height);
        }
    }
}
