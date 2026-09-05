using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 进战镜头：先看到整张大场景，再按像素对齐缩到 1080×1920，
    /// table 与场景同一比例、同一缩放，停稳后中央 3-2-1 再开赛。
    /// </summary>
    public static class DouQuquBattleIntro
    {
        private const float HoldSeconds = 0.65f;
        private const float ZoomSeconds = 2.4f;
        private const int CountdownSeconds = 3;
        private const float RefWidth = 1080f;
        private const float RefHeight = 1920f;

        public static void HideChrome(RectTransform hudRoot)
        {
            RectTransform host = HostOf(hudRoot);
            if (host == null) return;
            Transform leftover = host.Find("BattleIntroShot");
            if (leftover != null) UnityEngine.Object.Destroy(leftover.gameObject);
            foreach (GameObject node in ChromeOf(host, null, null))
                if (node != null) node.SetActive(false);
        }

        public static IEnumerator Play(RectTransform hudRoot, RectTransform pit)
        {
            if (hudRoot == null) yield break;

            RectTransform host = HostOf(hudRoot);
            if (host == null) host = hudRoot;
            HideChrome(hudRoot);

            RectTransform shot = FindNamed(host, "ArenaBackgroundScenery") as RectTransform;
            if (shot == null)
            {
                yield return Countdown(host);
                ShowChrome(host, pit, null);
                yield break;
            }

            shot.gameObject.SetActive(true);
            float texW = Mathf.Max(1f, shot.rect.width);
            float texH = Mathf.Max(1f, shot.rect.height);
            float startScale = Mathf.Min(RefWidth / texW, RefHeight / texH);
            float endScale = 1f;
            SetScale(shot, pit, startScale);

            float hold = 0f;
            while (hold < HoldSeconds)
            {
                hold += Time.unscaledDeltaTime;
                yield return null;
            }

            float elapsed = 0f;
            while (elapsed < ZoomSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseInShortOutLong(Mathf.Clamp01(elapsed / ZoomSeconds));
                SetScale(shot, pit, Mathf.Lerp(startScale, endScale, t));
                yield return null;
            }

            SetScale(shot, pit, endScale);
            yield return Countdown(host);
            ShowChrome(host, pit, shot);
        }

        private static void SetScale(RectTransform shot, RectTransform pit, float scale)
        {
            Vector3 value = Vector3.one * scale;
            if (shot != null) shot.localScale = value;
            if (pit != null && (shot == null || pit.parent != shot && !pit.IsChildOf(shot)))
                pit.localScale = value;
        }

        private static float EaseInShortOutLong(float t)
        {
            const float split = 0.18f;
            if (t < split)
            {
                float u = t / split;
                return split * u * u * u;
            }

            float v = (t - split) / (1f - split);
            float w = 1f - v;
            return split + (1f - split) * (1f - w * w * w * w);
        }

        private static IEnumerator Countdown(RectTransform hudRoot)
        {
            RectTransform overlay = CreateCountdown(hudRoot);
            TMP_Text label = overlay.GetComponentInChildren<TMP_Text>();
            for (int n = CountdownSeconds; n >= 1; n--)
            {
                if (label != null) label.text = n.ToString();
                float wait = 0f;
                while (wait < 1f)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (overlay != null) UnityEngine.Object.Destroy(overlay.gameObject);
        }

        private static RectTransform CreateCountdown(RectTransform hudRoot)
        {
            GameObject go = new GameObject("BattleCount321", typeof(RectTransform));
            RectTransform overlay = go.GetComponent<RectTransform>();
            overlay.SetParent(hudRoot, false);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            overlay.SetAsLastSibling();
            TMP_Text label = DouQuquUiFactory.CreateText(
                overlay,
                "CountLabel",
                "3",
                280f,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            label.color = Color.white;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            Outline outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(6f, -6f);
            return overlay;
        }

        private static void ShowChrome(RectTransform host, RectTransform pit, RectTransform shot)
        {
            foreach (GameObject node in ChromeOf(host, pit, shot))
                if (node != null) node.SetActive(true);
        }

        private static List<GameObject> ChromeOf(RectTransform host, RectTransform pit, RectTransform shot)
        {
            var list = new List<GameObject>();
            if (host == null) return list;
            for (int i = 0; i < host.childCount; i++)
            {
                Transform child = host.GetChild(i);
                if (child == null) continue;
                if (pit != null && child == pit) continue;
                if (shot != null && child == shot) continue;
                string name = child.name;
                if (name == "BattleIntroShot" || name == "BattleCount321" || name == "ArenaBackgroundScenery" || name == "Battlefield")
                    continue;
                list.Add(child.gameObject);
            }

            if (host.parent != null)
            {
                Transform stick = host.parent.Find("HudStick");
                if (stick != null) list.Add(stick.gameObject);
            }

            return list;
        }

        private static RectTransform HostOf(RectTransform hudRoot)
        {
            if (hudRoot == null) return null;
            RectTransform named = FindNamed(hudRoot, "douququzhandou") as RectTransform;
            return named != null ? named : hudRoot;
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
