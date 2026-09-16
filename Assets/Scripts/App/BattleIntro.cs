using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

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
                node.SetActive(false);
            }

            Transform design = FindNamed(host, "defaultDesignSize");
            if (design != null) design.gameObject.SetActive(true);
            Transform board = FindNamed(host, "Board");
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
            GameObject prefab = Resources.Load<GameObject>("Battle/Hud/Prefabs/BattleCount321");
            GameObject go;
            if (prefab != null)
            {
                go = UnityEngine.Object.Instantiate(prefab, hudRoot, false);
                go.name = "BattleCount321";
            }
            else
            {
                Debug.LogWarning("[DouQuqu] 缺少 BattleCount321 预制，倒数不会显示。");
                go = new GameObject("BattleCount321", typeof(RectTransform));
                go.transform.SetParent(hudRoot, false);
            }

            RectTransform overlay = go.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            overlay.SetAsLastSibling();
            go.SetActive(true);

            Color sideColor = PlayerPalette.OutlineColor(localPlayerId);
            string colorWord = PlayerPalette.TeamWord(localPlayerId);
            string hex = ColorUtility.ToHtmlStringRGB(sideColor);
            Transform sideNode = overlay.Find("SideLabel");
            TMP_Text side = sideNode != null ? sideNode.GetComponent<TMP_Text>() : null;
            if (side != null)
            {
                side.text = "你是<color=#" + hex + ">" + colorWord + "</color>色方！";
                side.richText = true;
            }

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
                    || name == "defaultDesignSize" || name == "ArenaBackgroundScenery"
                    || name == "Battlefield" || name == "liewen")
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
