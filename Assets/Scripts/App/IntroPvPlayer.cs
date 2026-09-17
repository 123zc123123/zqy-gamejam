using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DouQuqu
{
    /// <summary>
    /// 新账号登录后播 PV：播完停最后一帧 2 秒，后台进斗蛐蛐进战页，再淡出。
    /// 播放 5 秒后右上角出跳过；点了立刻结束 PV，不再停最后一帧。
    /// </summary>
    public sealed class IntroPvPlayer : MonoBehaviour
    {
        public const string ClipResourcePath = "Login/Videos/newpv";
        public const float HoldLastFrameSeconds = 2f;
        public const float FadeSeconds = 0.85f;
        public const float SkipButtonDelaySeconds = 5f;
        const int SortingOrder = 520;

        public static bool IsCovering { get; private set; }

        CanvasGroup group;
        VideoPlayer player;
        GameObject skipButton;
        bool ended;
        bool skipped;

        public static void BeginThenLoadMainMenu()
        {
            if (IsCovering) return;
            GameObject root = new GameObject("IntroPvCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            Object.DontDestroyOnLoad(root);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            Image dim = root.AddComponent<Image>();
            dim.color = Color.black;
            dim.raycastTarget = true;

            IntroPvPlayer host = root.AddComponent<IntroPvPlayer>();
            host.group = group;
            IsCovering = true;
            host.StartCoroutine(host.Run());
        }

        IEnumerator Run()
        {
            VideoClip clip = Resources.Load<VideoClip>(ClipResourcePath);
            if (clip != null)
                yield return PlayClip(clip);

            if (!skipped)
            {
                float hold = 0f;
                while (hold < HoldLastFrameSeconds)
                {
                    hold += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            TutorialDirector.RouteAfterLogin();
            SceneNames.Load(SceneNames.MainMenu);

            float wait = 0f;
            while (Lobby.Instance == null && wait < 8f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            if (Lobby.Instance != null && TutorialDirector.Step == TutorialDirector.StepClickTraining)
                Lobby.Show(Lobby.Page.BattleEnter);
            yield return null;
            yield return null;

            float t = 0f;
            while (t < FadeSeconds && group != null)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(t / FadeSeconds);
                yield return null;
            }

            IsCovering = false;
            TutorialDirector.OnBattleEnterReady();
            if (gameObject != null) Destroy(gameObject);
        }

        IEnumerator PlayClip(VideoClip clip)
        {
            GameObject imageGo = new GameObject("Pv", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageGo.transform.SetParent(transform, false);
            RectTransform rect = imageGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            RawImage view = imageGo.GetComponent<RawImage>();
            view.color = Color.white;
            view.raycastTarget = true;
            AspectRatioFitter fitter = imageGo.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            if (clip.width > 0.01 && clip.height > 0.01)
                fitter.aspectRatio = (float)clip.width / (float)clip.height;

            AudioSource audio = gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;

            int rw = Mathf.Max(16, (int)clip.width);
            int rh = Mathf.Max(16, (int)clip.height);
            RenderTexture rt = new RenderTexture(rw, rh, 0, RenderTextureFormat.ARGB32);
            rt.Create();
            view.texture = rt;

            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = false;
            player.skipOnDrop = true;
            player.waitForFirstFrame = true;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = rt;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.SetTargetAudioSource(0, audio);
            player.clip = clip;
            ended = false;
            player.loopPointReached += OnEnded;
            player.errorReceived += OnError;
            player.Prepare();
            float prepareWait = 0f;
            while (!player.isPrepared && prepareWait < 8f)
            {
                prepareWait += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!player.isPrepared)
            {
                ended = true;
                yield break;
            }

            player.Play();
            skipButton = CreateSkipButton();
            float playWait = 0f;
            while (!ended && player != null && playWait < clip.length + 4f)
            {
                playWait += Time.unscaledDeltaTime;
                if (skipButton != null && !skipButton.activeSelf && playWait >= SkipButtonDelaySeconds)
                    skipButton.SetActive(true);
                if (!player.isPlaying && player.time >= clip.length - 0.05)
                    break;
                yield return null;
            }

            if (skipButton != null) skipButton.SetActive(false);
            if (player != null) player.Pause();
        }

        GameObject CreateSkipButton()
        {
            GameObject go = new GameObject("Skip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(180f, 72f);
            rect.anchoredPosition = new Vector2(-40f, -48f);

            Image image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.45f);
            image.raycastTarget = true;

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.SetParent(go.transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
            label.font = UiFactory.Font;
            label.text = "跳过";
            label.fontSize = 40f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            Button button = go.GetComponent<Button>();
            button.onClick.AddListener(Skip);
            go.SetActive(false);
            go.transform.SetAsLastSibling();
            return go;
        }

        void Skip()
        {
            if (skipped) return;
            skipped = true;
            ended = true;
            if (skipButton != null) skipButton.SetActive(false);
            if (player != null) player.Pause();
        }

        void OnEnded(VideoPlayer source)
        {
            ended = true;
        }

        void OnError(VideoPlayer source, string message)
        {
            ended = true;
        }

        void OnDestroy()
        {
            if (player != null)
            {
                player.loopPointReached -= OnEnded;
                player.errorReceived -= OnError;
            }
            if (IsCovering && group != null && group.gameObject == gameObject)
                IsCovering = false;
        }
    }
}
