using System.Collections;
using TMPro;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 缩圈预告时屏幕上方「区域即将崩塌！」；入场 / 出场都是微上浮 + 透明度。
    /// 收口后出场。不加粗。
    /// </summary>
    public sealed class ZoneCollapseWarnHud : MonoBehaviour
    {
        public const string Message = "区域即将崩塌！";
        public const float FontSize = 42f;
        public const float AnimT = 0.28f;
        public const float FloatPx = 16f;
        public const float RestFromTop = -280f;

        MatchController match;
        RectTransform rt;
        CanvasGroup group;
        TextMeshProUGUI label;
        Material faceMat;
        Vector2 restPos;
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
                return;
            }

            if (warn && !lastWarn) Show();
            else if (!warn && lastWarn) HideAfterCollapse();
            lastWarn = warn;
        }

        public void Show()
        {
            Ensure(transform as RectTransform);
            if (rt == null) return;
            rt.gameObject.SetActive(true);
            Play(true);
        }

        public void Hide()
        {
            if (rt == null || !rt.gameObject.activeSelf)
            {
                StopPlay();
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
            rt.sizeDelta = new Vector2(900f, 56f);
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
            label.color = Color.white;
            UiFonts.Apply(label);
            label.fontStyle = FontStyles.Normal;
            if (label.font != null)
            {
                faceMat = new Material(label.font.material);
                label.fontMaterial = faceMat;
                label.outlineWidth = 0.18f;
                label.outlineColor = new Color(0.12f, 0.08f, 0.05f, 0.92f);
            }

            group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
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
                group.alpha = 0f;
                rt.anchoredPosition = restPos + Vector2.up * -FloatPx;
            }

            float startA = group.alpha;
            float startY = rt.anchoredPosition.y - restPos.y;
            float endA, endY;
            Sample(enter, 1f, out endA, out endY);

            float age = 0f;
            while (age < AnimT)
            {
                age += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(age / AnimT);
                float e = 1f - (1f - u) * (1f - u);
                group.alpha = Mathf.LerpUnclamped(startA, endA, e);
                rt.anchoredPosition = restPos + Vector2.up * Mathf.LerpUnclamped(startY, endY, e);
                yield return null;
            }

            Apply(enter, 1f);
            playing = null;
        }

        void Apply(bool enter, float u)
        {
            float alpha, y;
            Sample(enter, u, out alpha, out y);
            if (group != null) group.alpha = alpha;
            if (rt != null)
            {
                rt.anchoredPosition = restPos + Vector2.up * y;
                if (!enter && u >= 1f) rt.gameObject.SetActive(false);
            }
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

        void OnDestroy()
        {
            StopPlay();
            if (faceMat == null) return;
            if (Application.isPlaying) Destroy(faceMat);
            else DestroyImmediate(faceMat);
            faceMat = null;
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
