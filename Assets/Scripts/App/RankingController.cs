using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>排行榜：本机各登录名按账号积分从高到低。</summary>
    public sealed class RankingController : MonoBehaviour
    {
        private const int PodiumCount = 3;

        private GameObject pageRoot;

        public void BindPage(GameObject root)
        {
            if (root == null) return;
            pageRoot = root;
            BottomNavBar.SuppressEmbedded(root.transform);
            UiFonts.ApplyTree(root.transform);
            if (!PlayerDataService.RequireLogin()) return;
            if (VenueClient.Instance != null && VenueClient.Instance.HasServer)
                VenueClient.Instance.StartCoroutine(VenueClient.Instance.RefreshRanking(Refresh));
            else
                Refresh();
        }

        private void OnEnable()
        {
            PlayerDataService.PlayerDataChanged += Refresh;
            if (pageRoot != null) Refresh();
        }

        private void OnDisable()
        {
            PlayerDataService.PlayerDataChanged -= Refresh;
        }

        private void Refresh()
        {
            if (pageRoot == null) return;
            List<PlayerProfile> source = PlayerDataService.GetRankingSnapshot();
            if (VenueClient.Instance != null && VenueClient.Instance.CachedRanking != null
                && VenueClient.Instance.CachedRanking.Length > 0)
            {
                source = new List<PlayerProfile>(VenueClient.Instance.CachedRanking);
            }

            // 服务端榜单也在客户端统一过滤和排序，避免零分或乱序数据破坏界面名次。
            List<PlayerProfile> ranks = PrepareRanking(source);
            string self = PlayerDataService.CurrentPlayerName;

            // 前三领奖台和下方普通列表使用不同的坐标系，必须分开排序与绑定。
            List<Transform> podiumRows = CollectRows(pageRoot.transform, "PlayerRowTop");
            List<Transform> listRows = CollectRows(pageRoot.transform, "PlayerRow");
            BindRows(podiumRows, ranks, 0, self, true);
            BindRows(listRows, ranks, PodiumCount, self, false);
        }

        private static List<PlayerProfile> PrepareRanking(List<PlayerProfile> source)
        {
            List<PlayerProfile> result = new List<PlayerProfile>();
            if (source == null) return result;

            for (int i = 0; i < source.Count; i++)
            {
                PlayerProfile entry = source[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.playerName) || entry.score <= 0)
                    continue;

                // 只在分数严格更高时前插，保持同分玩家原有顺序。
                int insertAt = result.Count;
                while (insertAt > 0 && result[insertAt - 1].score < entry.score)
                    insertAt--;
                result.Insert(insertAt, entry);
            }

            return result;
        }

        private static void BindRows(List<Transform> rows, List<PlayerProfile> ranks,
            int rankOffset, string self, bool keepEmptySlot)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                Transform row = rows[i];
                if (row == null) continue;
                int rankIndex = rankOffset + i;
                if (rankIndex >= ranks.Count)
                {
                    if (keepEmptySlot)
                    {
                        row.gameObject.SetActive(true);
                        ClearPodiumRow(row, rankIndex + 1);
                    }
                    else
                    {
                        row.gameObject.SetActive(false);
                    }
                    continue;
                }

                row.gameObject.SetActive(true);
                PlayerProfile entry = ranks[rankIndex];
                bool mine = !string.IsNullOrEmpty(self)
                    && string.Equals(self, entry.playerName, System.StringComparison.OrdinalIgnoreCase);
                WriteRow(row, rankIndex + 1, entry.playerName, entry.score, mine);
            }
        }

        private static void WriteRow(Transform row, int rank, string playerName, int score, bool mine)
        {
            SetNamed(row, "玩家1", playerName);
            SetNamed(row, "1200", score.ToString());
            SetNamed(row, "分数", "分数");
            SetNamed(row, "1", rank.ToString());
            SetNamed(row, "第1名", "第" + rank + "名");
            SetAvatarVisible(row, true);
            TMP_Text[] labels = row.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                labels[i].fontStyle = mine ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        /// <summary>领奖台没有玩家时保留卡片和名次，只清空玩家内容。</summary>
        private static void ClearPodiumRow(Transform row, int rank)
        {
            SetNamed(row, "玩家1", string.Empty);
            SetNamed(row, "1200", string.Empty);
            SetNamed(row, "分数", "分数");
            SetNamed(row, "第1名", "第" + rank + "名");
            SetAvatarVisible(row, false);
        }

        private static void SetAvatarVisible(Transform row, bool visible)
        {
            Transform avatarRoot = FindNamed(row, "avatar");
            if (avatarRoot == null) return;
            // AvatarBackground 是独立的白色圆底，只切换真正的头像图案。
            Transform avatarImage = avatarRoot.Find("avatar");
            UnityEngine.UI.Image avatar = avatarImage != null
                ? avatarImage.GetComponent<UnityEngine.UI.Image>()
                : avatarRoot.GetComponent<UnityEngine.UI.Image>();
            if (avatar != null) avatar.enabled = visible;
        }

        private static void SetNamed(Transform row, string objectName, string value)
        {
            Transform found = FindNamed(row, objectName);
            if (found == null) return;
            TMP_Text label = found.GetComponent<TMP_Text>();
            if (label != null) label.text = value;
        }

        private static List<Transform> CollectRows(Transform root, string objectName)
        {
            List<Transform> rows = new List<Transform>();
            CollectNamed(root, objectName, rows);
            rows.Sort((a, b) =>
            {
                RectTransform ra = a as RectTransform;
                RectTransform rb = b as RectTransform;
                float ya = ra != null ? ra.anchoredPosition.y : 0f;
                float yb = rb != null ? rb.anchoredPosition.y : 0f;
                int cmp = yb.CompareTo(ya);
                if (cmp != 0) return cmp;
                return a.GetSiblingIndex().CompareTo(b.GetSiblingIndex());
            });
            return rows;
        }

        private static void CollectNamed(Transform root, string objectName, List<Transform> into)
        {
            if (root.name == objectName) into.Add(root);
            for (int i = 0; i < root.childCount; i++)
                CollectNamed(root.GetChild(i), objectName, into);
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
