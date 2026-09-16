using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 进战镜头：先框整张大背景，再缩到玩家。多人落到己方角，自己练缩到当前有效区。
    /// 停稳后中央 3-2-1、开罐！再开赛。场地底图钉在世界里，这里只改镜头和 HUD 显隐。
    /// </summary>
    public static class BattleIntro
    {
        private const float HoldSeconds = 1.2f;
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

            if (cam != null)
            {
                if (dropToCorner) cam.FrameCorner(localPlayerId, ZoomSeconds);
                else cam.FrameCurrentZone(ZoomSeconds);
            }

            float elapsed = 0f;
            while (elapsed < ZoomSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (cam != null)
            {
                if (dropToCorner) cam.FrameCorner(localPlayerId, 0f);
                else cam.FrameCurrentZone(0f);
            }
            yield return Countdown(host, localPlayerId);
            ShowChrome(host, pit, shot);
        }

        private static IEnumerator Countdown(RectTransform hudRoot, int localPlayerId)
        {
            RectTransform overlay = CreateCountdown(hudRoot, localPlayerId);
            Transform countNode = overlay.Find("CountLabel");
            TMP_Text label = countNode != null ? countNode.GetComponent<TMP_Text>() : overlay.GetComponentInChildren<TMP_Text>();
            for (int n = CountdownSeconds; n >= 1; n--)
            {
                if (label != null) label.text = n.ToString();
                yield return WaitUnscaled(1f);
            }

            if (label != null) label.text = "开罐！";
            yield return WaitUnscaled(1f);

            if (overlay != null) UnityEngine.Object.Destroy(overlay.gameObject);
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float wait = 0f;
            while (wait < seconds)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static RectTransform CreateCountdown(RectTransform hudRoot, int localPlayerId)
        {
            GameObject go = new GameObject("BattleCount321", typeof(RectTransform));
            RectTransform overlay = go.GetComponent<RectTransform>();
            overlay.SetParent(hudRoot, false);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            overlay.SetAsLastSibling();

            Color sideColor = PlayerPalette.OutlineColor(localPlayerId);
            string colorWord = PlayerPalette.TeamWord(localPlayerId);
            string hex = ColorUtility.ToHtmlStringRGB(sideColor);
            TMP_Text side = UiFactory.CreateText(
                overlay,
                "SideLabel",
                "你是<color=#" + hex + ">" + colorWord + "</color>色方！",
                152f,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            RectTransform sideRect = side.rectTransform;
            sideRect.pivot = new Vector2(0.5f, 0.5f);
            sideRect.sizeDelta = new Vector2(1040f, 220f);
            sideRect.anchoredPosition = new Vector2(0f, 320f);
            side.color = Color.white;
            side.richText = true;
            side.fontStyle = FontStyles.Bold;
            side.enableAutoSizing = false;
            side.enableWordWrapping = false;
            side.overflowMode = TextOverflowModes.Overflow;
            side.characterSpacing = 4f;
            side.extraPadding = true;
            ApplySharpBlackOutline(side, 0.22f);

            TMP_Text label = UiFactory.CreateText(
                overlay,
                "CountLabel",
                "3",
                280f,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            RectTransform countRect = label.rectTransform;
            countRect.pivot = new Vector2(0.5f, 0.5f);
            countRect.sizeDelta = new Vector2(900f, 420f);
            countRect.anchoredPosition = new Vector2(0f, -40f);
            label.color = Color.white;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.extraPadding = true;
            ApplySharpBlackOutline(label, 0.18f);
            return overlay;
        }

        static void ApplySharpBlackOutline(TMP_Text label, float width)
        {
            if (label == null || label.fontSharedMaterial == null) return;
            Material material = new Material(label.fontSharedMaterial);
            material.EnableKeyword("OUTLINE_ON");
            material.SetFloat("_OutlineWidth", width);
            material.SetColor("_OutlineColor", Color.black);
            material.SetFloat("_FaceDilate", 0.12f);
            material.SetFloat("_OutlineSoftness", 0f);
            label.fontMaterial = material;
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
                    || name == "ArenaBackgroundScenery" || name == "Battlefield" || name == "liewen")
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
