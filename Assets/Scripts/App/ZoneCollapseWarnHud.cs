using System.Collections;
using TMPro;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 缩圈预告时屏幕上方「区域即将崩塌！」：红字、baibian 白边、72 号。
    /// 入场 / 出场微上浮 + 透明度；停留时 1 秒一轮呼吸闪烁。收口后出场。
    /// </summary>
    public sealed class ZoneCollapseWarnHud : MonoBehaviour
    {
        public const string Message = "区域即将崩塌！";
        public const string BaibianMat = "Fonts/Chinese SDF baibian";
        public const float FontSize = 72f;
        public const float AnimT = 0.28f;
        public const float FloatPx = 16f;
        public const float RestFromTop = -280f;
        public const float BreathPeriod = 1f;
        public const float BreathMin = 0.4f;

        static readonly Color WarnRed = Color.red;

        MatchController match;
        RectTransform rt;
        CanvasGroup group;
        TextMeshProUGUI label;
        Vector2 restPos;
        float fade;
        float shownAt;
        bool lastWarn;
        bool known;
        Coroutine playing;

        public void Bind(MatchController matchController, RectTransform hudRoot)
        {
            match = matchController;
            Ensure(hudRoot);
            lastWarn = false;
            known = false;
        }

        void LateUpdate()
        {
            bool warn = match != null && match.ZoneWarn;
            if (!known)
            {
                known = true;
                lastWarn = warn;
                if (warn) Show();
            }
            else
            {
                if (warn && !lastWarn) Show();
                else if (!warn && lastWarn) HideAfterCollapse();
                lastWarn = warn;
            }

            ApplyVisibleAlpha();
        }

        public void Show()
        {
            Ensure(transform as RectTransform);
            if (rt == null) return;
            shownAt = Time.unscaledTime;
            rt.gameObject.SetActive(true);
            Play(true);
            TryTellFirstCollapse();
        }

        static void TryTellFirstCollapse()
        {
            if (DialogueBoxView.IsPlaying) return;
            if (!PlayerDataService.TryConsumeFirstZoneTalk()) return;
            DialogueBoxView.Play(TutorialDirector.IdBattleZone);
        }

        public void Hide()
        {
            if (rt == null || !rt.gameObject.activeSelf)
            {
                StopPlay();
                fade = 0f;
                ApplyVisibleAlpha();
                return;
            }
            Play(false);
        }

        void HideAfterCollapse()
        {
            float delay = match != null && match.Knobs != null
                ? Mathf.Max(0f, match.Knobs.zoneFadeT)
                : 0f;
            StopPlay();
            if (!isActiveAndEnabled || delay <= 0f)
            {
                Hide();
                return;
            }
            playing = StartCoroutine(HideAfter(delay));
        }

        IEnumerator HideAfter(float delay)
        {
            float age = 0f;
            while (age < delay)
            {
                age += Time.deltaTime;
                yield return null;
            }
            playing = null;
            Hide();
        }

        /// <summary>u=0 入场起 / 出场起；u=1 入场停在原位 / 出场完全隐掉。</summary>
        public static void Sample(bool enter, float u, out float alpha, out float y)
        {
            u = Mathf.Clamp01(u);
            float e = 1f - (1f - u) * (1f - u);
            if (enter)
            {
                alpha = e;
                y = Mathf.Lerp(-FloatPx, 0f, e);
            }
            else
            {
                alpha = 1f - e;
                y = Mathf.Lerp(0f, FloatPx, e);
            }
        }

        /// <summary>age=0 最亮，半周期最暗，满周期回到最亮。</summary>
        public static float Breath(float age)
        {
            float wave = 0.5f + 0.5f * Mathf.Cos(age * Mathf.PI * 2f / BreathPeriod);
            return Mathf.Lerp(BreathMin, 1f, wave);
        }

        void Ensure(RectTransform hudRoot)
        {
            if (rt != null) return;
            if (hudRoot == null) hudRoot = transform as RectTransform;
            Transform hud = FindNamed(hudRoot, "HUD");
            Transform parent = hud != null ? hud : hudRoot;
            if (parent == null) return;

            GameObject go = new GameObject("ZoneCollapseWarn", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(CanvasGroup));
            rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            restPos = new Vector2(0f, RestFromTop);
            rt.anchoredPosition = restPos + Vector2.up * -FloatPx;
            rt.sizeDelta = new Vector2(1040f, 100f);
            rt.localScale = Vector3.one;
            rt.SetAsLastSibling();

            label = go.GetComponent<TextMeshProUGUI>();
            label.text = Message;
            label.fontSize = FontSize;
            label.fontStyle = FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.extraPadding = true;
            label.color = WarnRed;
            UiFonts.Apply(label);
            label.fontStyle = FontStyles.Normal;
            label.color = WarnRed;
            Material baibian = Resources.Load<Material>(BaibianMat);
            if (baibian != null) label.fontSharedMaterial = baibian;

            group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            fade = 0f;
            go.SetActive(false);
        }

        void Play(bool enter)
        {
            StopPlay();
            if (!isActiveAndEnabled || rt == null)
            {
                Apply(enter, 1f);
                return;
            }
            playing = StartCoroutine(PlayRoutine(enter));
        }

        IEnumerator PlayRoutine(bool enter)
        {
            if (enter)
            {
                fade = 0f;
                rt.anchoredPosition = restPos + Vector2.up * -FloatPx;
                ApplyVisibleAlpha();
            }

            float startA = fade;
            float startY = rt.anchoredPosition.y - restPos.y;
            float endA, endY;
            Sample(enter, 1f, out endA, out endY);

            float age = 0f;
            while (age < AnimT)
            {
                age += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(age / AnimT);
                float e = 1f - (1f - u) * (1f - u);
                fade = Mathf.LerpUnclamped(startA, endA, e);
                rt.anchoredPosition = restPos + Vector2.up * Mathf.LerpUnclamped(startY, endY, e);
                ApplyVisibleAlpha();
                yield return null;
            }

            Apply(enter, 1f);
            playing = null;
        }

        void Apply(bool enter, float u)
        {
            float alpha, y;
            Sample(enter, u, out alpha, out y);
            fade = alpha;
            if (rt != null)
            {
                rt.anchoredPosition = restPos + Vector2.up * y;
                if (!enter && u >= 1f) rt.gameObject.SetActive(false);
            }
            ApplyVisibleAlpha();
        }

        void ApplyVisibleAlpha()
        {
            if (group == null) return;
            bool on = rt != null && rt.gameObject.activeSelf;
            float breath = on ? Breath(Time.unscaledTime - shownAt) : 1f;
            group.alpha = fade * breath;
        }

        void StopPlay()
        {
            if (playing == null) return;
            StopCoroutine(playing);
            playing = null;
        }

        void OnDisable()
        {
            StopPlay();
        }

        static Transform FindNamed(Transform root, string objectName)
        {
            if (root == null) return null;
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
