using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>买完卵：锁操作，指引手带着底栏滑到育虫盘，再只放行该入口。</summary>
    public sealed class TutorialShopNavHint : MonoBehaviour
    {
        const float ScrollSeconds = 1.15f;

        static TutorialShopNavHint instance;

        BottomNavBar bar;
        ScrollRect scroll;
        BottomNavTab breed;
        Image blocker;
        bool scrolling;
        bool spotlighted;
        float startNorm;
        float targetNorm;
        float clock;

        public static void Begin()
        {
            BottomNavBar nav = Object.FindObjectOfType<BottomNavBar>();
            if (nav == null || !nav.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[DouQuqu] 找不到底栏，无法做滑动引导");
                return;
            }

            if (instance == null)
            {
                GameObject host = new GameObject("TutorialShopNavHint");
                instance = host.AddComponent<TutorialShopNavHint>();
                Object.DontDestroyOnLoad(host);
            }

            instance.Run(nav);
        }

        public static void Stop()
        {
            if (instance == null) return;
            instance.Finish(false);
        }

        void Run(BottomNavBar nav)
        {
            enabled = true;
            bar = nav;
            scroll = nav.Scroll;
            breed = nav.FindTab(BottomNavTab.NavModule.Breeding);
            spotlighted = false;
            scrolling = false;
            clock = 0f;

            if (scroll != null) scroll.enabled = false;
            EnsureBlocker(nav);
            SetBlocker(true);

            Canvas canvas = nav.GetComponentInParent<Canvas>();
            Transform fingerRoot = canvas != null ? canvas.transform : nav.transform;
            TutorialFingerHint.ShowNavSwipe(fingerRoot);

            if (breed == null || scroll == null)
            {
                RevealBreed();
                return;
            }

            Canvas.ForceUpdateCanvases();
            startNorm = scroll.horizontalNormalizedPosition;
            targetNorm = nav.NormalizedToReveal(breed.transform as RectTransform);
            if (Mathf.Abs(targetNorm - startNorm) < 0.02f)
            {
                RevealBreed();
                return;
            }

            scrolling = true;
        }

        void Update()
        {
            if (!enabled || !scrolling) return;
            clock += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(clock / ScrollSeconds);
            u = u * u * (3f - 2f * u);
            if (scroll != null)
                scroll.horizontalNormalizedPosition = Mathf.Lerp(startNorm, targetNorm, u);
            if (u < 1f) return;
            scrolling = false;
            RevealBreed();
        }

        void RevealBreed()
        {
            if (spotlighted) return;
            spotlighted = true;
            scrolling = false;
            TutorialFingerHint.Hide();
            SetBlocker(false);
            if (scroll != null) scroll.enabled = false;
            PlayerDataService.SetTutorialStep(TutorialDirector.StepClickBreed);
            if (breed != null)
                TutorialSpotlight.Show(breed.gameObject, "点击育虫盘");
        }

        void Finish(bool keepSpotlight)
        {
            scrolling = false;
            spotlighted = false;
            if (scroll != null) scroll.enabled = true;
            SetBlocker(false);
            TutorialFingerHint.Hide();
            if (!keepSpotlight) TutorialSpotlight.Hide();
            bar = null;
            scroll = null;
            breed = null;
            enabled = false;
        }

        void EnsureBlocker(BottomNavBar nav)
        {
            if (blocker != null) return;
            Canvas canvas = nav.GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : nav.transform;
            GameObject go = new GameObject("TutorialNavBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            blocker = go.GetComponent<Image>();
            blocker.color = new Color(0.02f, 0.02f, 0.04f, 0.45f);
            blocker.raycastTarget = true;
        }

        void SetBlocker(bool on)
        {
            if (blocker == null) return;
            blocker.gameObject.SetActive(on);
            if (on) blocker.transform.SetAsLastSibling();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            if (blocker != null) Destroy(blocker.gameObject);
        }
    }
}
