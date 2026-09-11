using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 进战镜头：先框整张开局棋盘，再落到己方角，停稳后中央 3-2-1 再开赛。
    /// 2D Board 由 BattleBoardFollow 跟着 3D 相机，这里只改镜头和 HUD 显隐。
    /// </summary>
    public static class BattleIntro
    {
        private const float HoldSeconds = 0.65f;
        private const float ZoomSeconds = 2.4f;
        private const int CountdownSeconds = 3;

        public static void HideChrome(RectTransform hudRoot)
        {
            RectTransform host = HostOf(hudRoot);
            if (host == null) return;
            Transform leftover = host.Find("BattleIntroShot");
            if (leftover != null) UnityEngine.Object.Destroy(leftover.gameObject);
            foreach (GameObject node in ChromeOf(host, null, null))
            {
                if (node == null) continue;
                if (node.name == "Board") continue;
                node.SetActive(false);
            }

            Transform board = host.Find("Board");
            if (board != null) board.gameObject.SetActive(true);
        }

        public static IEnumerator Play(RectTransform hudRoot, RectTransform pit, BattleCamera cam = null, int localPlayerId = 0, bool dropToCorner = false)
        {
            if (hudRoot == null) yield break;

            RectTransform host = HostOf(hudRoot);
            if (host == null) host = hudRoot;
            HideChrome(hudRoot);
            Transform board = FindNamed(host, "Board");
            if (board != null) board.gameObject.SetActive(true);
            if (cam != null) cam.FrameOpeningPanorama();

            RectTransform shot = FindNamed(host, "ArenaBackgroundScenery") as RectTransform;
            if (shot != null) shot.gameObject.SetActive(true);

            float hold = 0f;
            while (hold < HoldSeconds)
            {
                hold += Time.unscaledDeltaTime;
                yield return null;
            }

            if (dropToCorner && cam != null) cam.FrameCorner(localPlayerId, ZoomSeconds);

            float elapsed = 0f;
            while (elapsed < ZoomSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (dropToCorner && cam != null) cam.FrameCorner(localPlayerId, 0f);
            yield return Countdown(host);
            ShowChrome(host, pit, shot);
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
            TMP_Text label = UiFactory.CreateText(
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
                if (name == "BattleIntroShot" || name == "BattleCount321" || name == "Board"
                    || name == "ArenaBackgroundScenery" || name == "Battlefield")
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
            RectTransform named = FindNamed(hudRoot, "Battle") as RectTransform;
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
