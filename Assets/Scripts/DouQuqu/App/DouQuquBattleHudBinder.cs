using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace DouQuqu
{
    /// <summary>
    /// 战斗美术 HUD：把 DouQuquDemo 的真实对战场画进中间 Battlefield。
    /// Battlefield 上已有 Image，不能再挂 RawImage，所以在子节点 BattleView 里显示。
    /// </summary>
    public sealed class DouQuquBattleHudBinder : MonoBehaviour
    {
        private RectTransform pit;
        private RawImage view;
        private Camera battleCam;
        private Camera hudCam;
        private DouQuquBattleCamera fitter;
        private RenderTexture target;
        private Vector2Int lastPixels;

        private IEnumerator Start()
        {
            pit = FindNamed(transform, "Battlefield") as RectTransform;
            if (pit == null)
            {
                Debug.LogWarning("[DouQuqu] 战斗 HUD 里没有 Battlefield，无法嵌入对战场。");
                yield break;
            }

            Canvas hud = GetComponent<Canvas>();
            if (hud != null)
            {
                hud.renderMode = RenderMode.ScreenSpaceOverlay;
                hud.sortingOrder = 100;
                hud.enabled = true;
            }

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
            yield return null;
            Canvas.ForceUpdateCanvases();
            FitPitToHud();
            yield return LoadDemoIfNeeded();
            BindBattleCamera();
            RefreshTarget(true);
            SilenceHudRaycasts();
            BindStick();
        }

        private void LateUpdate()
        {
            if (pit == null || battleCam == null || view == null) return;
            RefreshTarget(false);
        }

        private void OnDestroy()
        {
            ReleaseTarget();
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

        /// <summary>
        /// 新背景擂台比旧红框大。把 Battlefield 拉到顶栏头像和底栏卡之间，铺满坑。
        /// </summary>
        private void FitPitToHud()
        {
            if (pit == null) return;
            RectTransform host = pit.parent as RectTransform;
            RectTransform top = FindNamed(transform, "PlayersRow") as RectTransform;
            RectTransform bottom = FindNamed(transform, "ScrollableTrack") as RectTransform;
            float topEdge = top != null ? top.anchoredPosition.y - top.rect.height * 0.5f : 400f;
            float bottomEdge = bottom != null ? bottom.anchoredPosition.y + bottom.rect.height * 0.5f : -400f;
            float hostWidth = host != null ? host.rect.width : 1080f;
            const float inset = 6f;
            float yMax = topEdge - inset;
            float yMin = bottomEdge + inset;
            pit.anchorMin = new Vector2(0.5f, 0.5f);
            pit.anchorMax = new Vector2(0.5f, 0.5f);
            pit.pivot = new Vector2(0.5f, 0.5f);
            pit.anchoredPosition = new Vector2(0f, (yMax + yMin) * 0.5f);
            pit.sizeDelta = new Vector2(Mathf.Max(200f, hostWidth - 40f), Mathf.Max(200f, yMax - yMin));
        }

        private static IEnumerator LoadDemoIfNeeded()
        {
            Scene demo = SceneManager.GetSceneByName(DouQuquSceneNames.BattleDemo);
            if (demo.IsValid() && demo.isLoaded) yield break;

            AsyncOperation op = SceneManager.LoadSceneAsync(DouQuquSceneNames.BattleDemo, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError("[DouQuqu] 无法加载战斗场景 " + DouQuquSceneNames.BattleDemo);
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

            DouQuquBattleCamera stray = hudCam != null ? hudCam.GetComponent<DouQuquBattleCamera>() : null;
            if (stray != null) Destroy(stray);

            fitter = battleCam.GetComponent<DouQuquBattleCamera>();
            if (fitter == null) fitter = battleCam.gameObject.AddComponent<DouQuquBattleCamera>();
            fitter.UseHudFill();

            battleCam.transform.position = new Vector3(0f, 50f, 0f);
            battleCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            battleCam.tag = "MainCamera";
            battleCam.enabled = true;
            battleCam.orthographic = true;
            battleCam.clearFlags = CameraClearFlags.SolidColor;
            battleCam.backgroundColor = DouQuquBattleBoard.Sand;
            battleCam.depth = -1;
            DouQuquBattleBoard.Install();

            AudioListener keep = battleCam.GetComponent<AudioListener>();
            if (keep == null) keep = battleCam.gameObject.AddComponent<AudioListener>();
            keep.enabled = true;
            AudioListener[] listeners = Object.FindObjectsOfType<AudioListener>();
            for (int i = 0; i < listeners.Length; i++)
                if (listeners[i] != null && listeners[i] != keep) listeners[i].enabled = false;

            UIDocument stickHud = Object.FindObjectOfType<UIDocument>();
            if (stickHud != null) stickHud.enabled = false;
            DouQuquTouchInput touch = Object.FindObjectOfType<DouQuquTouchInput>();
            if (touch != null) touch.enabled = false;
        }

        private void SilenceHudRaycasts()
        {
            UnityEngine.UI.Image[] images = GetComponentsInChildren<UnityEngine.UI.Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null) continue;
                if (images[i].GetComponent<DouQuquHudStick>() != null) continue;
                images[i].raycastTarget = false;
            }

            RawImage[] raws = GetComponentsInChildren<RawImage>(true);
            for (int i = 0; i < raws.Length; i++)
                if (raws[i] != null) raws[i].raycastTarget = false;
        }

        private void BindStick()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (pit == null || canvas == null || canvas.transform.Find("HudStick") != null) return;
            DouQuquMatchController match = Object.FindObjectOfType<DouQuquMatchController>();
            DouQuquLanSession network = DouQuquAppServices.Instance != null ? DouQuquAppServices.Instance.Network : null;
            int localId = network != null && network.LocalPlayerId >= 0 ? network.LocalPlayerId : 0;
            DouQuquHudStick.Create(pit, canvas, match, localId);
        }

        private static Camera FindDemoCamera()
        {
            Scene demo = SceneManager.GetSceneByName(DouQuquSceneNames.BattleDemo);
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

            target = new RenderTexture(pixels.x, pixels.y, 16, RenderTextureFormat.ARGB32)
            {
                name = "DouQuquBattlefield",
                antiAliasing = 2,
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
}
