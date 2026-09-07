using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 战斗 HUD：头像旁积分显示局内击杀分；头像右上角显示当前连杀。
    /// </summary>
    public sealed class DouQuquBattleScoreHud : MonoBehaviour
    {
        const int PlayerCount = 4;
        const float BadgeSize = 44f;

        DouQuquMatchController match;
        readonly TMP_Text[] scores = new TMP_Text[PlayerCount];
        readonly TMP_Text[] streaks = new TMP_Text[PlayerCount];
        readonly int[] lastScore = { int.MinValue, int.MinValue, int.MinValue, int.MinValue };
        readonly int[] lastStreak = { int.MinValue, int.MinValue, int.MinValue, int.MinValue };

        public void Bind(DouQuquMatchController matchController)
        {
            match = matchController;
            CacheCards();
            Refresh(true);
        }

        void LateUpdate()
        {
            Refresh(false);
        }

        void CacheCards()
        {
            for (int i = 0; i < PlayerCount; i++)
            {
                if (scores[i] != null && streaks[i] != null) continue;
                Transform card = FindNamed(transform, "FigmaPlayer" + (i + 1));
                if (card == null) continue;
                Transform scoreNode = FindNamed(card, "Score");
                if (scoreNode != null) scores[i] = scoreNode.GetComponent<TMP_Text>();
                Transform avatar = FindNamed(card, "AvatarFrame");
                if (avatar != null) streaks[i] = EnsureBadge(avatar as RectTransform, scores[i]);
            }
        }

        void Refresh(bool force)
        {
            CacheCards();
            if (match == null) match = Object.FindObjectOfType<DouQuquMatchController>();
            for (int i = 0; i < PlayerCount; i++)
            {
                int score = match != null ? match.MatchScore(i) : 0;
                int streak = match != null ? match.KillStreak(i) : 0;
                if (scores[i] != null && (force || score != lastScore[i]))
                {
                    scores[i].text = score.ToString();
                    lastScore[i] = score;
                }

                if (streaks[i] == null || (!force && streak == lastStreak[i])) continue;
                lastStreak[i] = streak;
                streaks[i].text = streak > 0 ? streak.ToString() : "";
                streaks[i].gameObject.SetActive(streak > 0);
            }
        }

        static TMP_Text EnsureBadge(RectTransform avatar, TMP_Text scoreSample)
        {
            if (avatar == null) return null;
            Transform existing = avatar.Find("KillStreak");
            RectTransform rect;
            if (existing != null)
            {
                rect = existing as RectTransform;
            }
            else
            {
                GameObject go = new GameObject("KillStreak", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                rect = go.GetComponent<RectTransform>();
                rect.SetParent(avatar, false);
            }

            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(6f, 6f);
            rect.sizeDelta = new Vector2(BadgeSize, BadgeSize);
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();

            TMP_Text text = rect.GetComponent<TMP_Text>();
            text.alignment = TextAlignmentOptions.Midline;
            text.fontSize = 28f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(1f, 0.86f, 0.28f, 1f);
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            if (scoreSample != null && scoreSample.font != null)
            {
                text.font = scoreSample.font;
                text.fontSharedMaterial = scoreSample.fontSharedMaterial;
            }

            Outline outline = rect.GetComponent<Outline>();
            if (outline == null) outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.15f, 0.08f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            return text;
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
