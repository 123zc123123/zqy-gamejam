using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>排行榜：本机各登录名按账号积分从高到低。</summary>
    public sealed class RankingController : MonoBehaviour
    {
        private const int PodiumCount = 3;
        private const float ListRowStartY = -84f;
        private const float ListRowStep = 218f;
        private const float ListRowHeight = 168f;
        private const float ListTopPad = 20f;
        private const string TextureFolder = "Ranking/Textures/";
        private static readonly string[] NameNodes = { "name", "玩家1" };
        private static readonly string[] ScoreNodes = { "count", "1200" };
        private static readonly string[] RankNodes = { "1" };
        private static readonly string[] RankTitleNodes = { "第1名" };

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
            int listCount = Mathf.Max(0, ranks.Count - PodiumCount);
            EnsureListRows(pageRoot.transform, listCount);
            List<Transform> listRows = CollectRows(pageRoot.transform, "PlayerRow");
            BindRows(podiumRows, ranks, 0, self);
            BindRows(listRows, ranks, PodiumCount, self);
            FitListHeight(listRows, listCount);
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

        private static void EnsureListRows(Transform root, int needed)
        {
            List<Transform> rows = CollectRows(root, "PlayerRow");
            if (rows.Count == 0 || needed <= rows.Count) return;

            Transform template = rows[0];
            Transform parent = template.parent;
            for (int i = rows.Count; i < needed; i++)
            {
                GameObject clone = Object.Instantiate(template.gameObject, parent, false);
                clone.name = "PlayerRow-" + (i + 1);
                clone.SetActive(true);
            }
        }

        private static void FitListHeight(List<Transform> rows, int visibleCount)
        {
            int count = Mathf.Max(0, visibleCount);
            Transform listHost = rows.Count > 0 ? rows[0].parent : null;
            RectTransform listRect = listHost as RectTransform;
            if (listRect == null) return;

            VerticalLayoutGroup layout = listHost.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    RectTransform row = rows[i] as RectTransform;
                    if (row == null) continue;
                    row.anchoredPosition = new Vector2(row.anchoredPosition.x, ListRowStartY - ListRowStep * i);
                }
            }

            LayoutElement listElement = listHost.GetComponent<LayoutElement>();
            float listHeight = count <= 0 ? 0f : ListRowHeight + ListRowStep * (count - 1);
            if (listElement != null)
            {
                listElement.minHeight = listHeight;
                listElement.preferredHeight = listHeight;
            }

            listRect.sizeDelta = new Vector2(listRect.sizeDelta.x, listHeight);
            RectTransform content = listHost.parent as RectTransform;
            if (content != null)
                content.sizeDelta = new Vector2(content.sizeDelta.x, count <= 0 ? 0f : ListTopPad + listHeight);

            if (layout != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(listRect);
        }

        private static void BindRows(List<Transform> rows, List<PlayerProfile> ranks,
            int rankOffset, string self)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                Transform row = rows[i];
                if (row == null) continue;
                int rankIndex = rankOffset + i;
                if (rankIndex >= ranks.Count)
                {
                    row.gameObject.SetActive(false);
                    continue;
                }

                row.gameObject.SetActive(true);
                PlayerProfile entry = ranks[rankIndex];
                bool mine = !string.IsNullOrEmpty(self)
                    && string.Equals(self, entry.playerName, System.StringComparison.OrdinalIgnoreCase);
                WriteRow(row, rankIndex + 1, entry, mine);
            }
        }

        private static void WriteRow(Transform row, int rank, PlayerProfile entry, bool mine)
        {
            string playerName = entry != null ? entry.playerName : string.Empty;
            int score = entry != null ? entry.score : 0;
            SetNamedAny(row, playerName, NameNodes);
            SetNamedAny(row, score.ToString(), ScoreNodes);
            SetNamed(row, "分数", "分数");
            SetNamedAny(row, rank.ToString(), RankNodes);
            SetNamedAny(row, "第" + rank + "名", RankTitleNodes);
            PlayerPalette.BindAvatar(row, true);
            ApplyChrome(row, rank, mine);
        }

        private static bool IsPodiumRow(Transform row)
        {
            return MatchesRowName(row.name, "PlayerRowTop") || FindNamed(row, "numberBg") == null;
        }

        private static void ApplyChrome(Transform row, int rank, bool mine)
        {
            if (IsPodiumRow(row))
            {
                int place = Mathf.Clamp(rank, 1, 3);
                SetImageSprite(row, "ranking-" + place, "bg", "Rectangle 25");
                SetImageSprite(row, "countBg-" + place, "countBg");
                Transform badge = FindNamed(row, "rank-badge");
                if (badge != null) badge.gameObject.SetActive(place == 1);
                return;
            }

            SetImageSprite(row, mine ? "ranking-self" : "ranking-defaultBg", "bg");
            SetImageSprite(row, mine ? "numberBg-self" : "numberBg", "numberBg");
            SetImageSprite(row, mine ? "countBg-self" : "countBg", "countBg");
        }

        private static void SetImageSprite(Transform row, string textureName, params string[] objectNames)
        {
            Sprite sprite = Resources.Load<Sprite>(TextureFolder + textureName);
            if (sprite == null) return;
            for (int i = 0; i < objectNames.Length; i++)
            {
                Transform found = FindNamed(row, objectNames[i]);
                if (found == null) continue;
                Image image = found.GetComponent<Image>();
                if (image == null) continue;
                image.sprite = sprite;
                return;
            }
        }

        private static void SetNamedAny(Transform row, string value, string[] objectNames)
        {
            for (int i = 0; i < objectNames.Length; i++)
                SetNamed(row, objectNames[i], value);
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
                int ia = RowSuffix(a.name, objectName);
                int ib = RowSuffix(b.name, objectName);
                if (ia != ib) return ia.CompareTo(ib);
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
            if (MatchesRowName(root.name, objectName)) into.Add(root);
            for (int i = 0; i < root.childCount; i++)
                CollectNamed(root.GetChild(i), objectName, into);
        }

        private static bool MatchesRowName(string name, string objectName)
        {
            if (name == objectName) return true;
            return name.StartsWith(objectName + "-", System.StringComparison.Ordinal);
        }

        private static int RowSuffix(string name, string objectName)
        {
            if (string.IsNullOrEmpty(name) || name == objectName) return 0;
            string prefix = objectName + "-";
            if (!name.StartsWith(prefix, System.StringComparison.Ordinal)) return 0;
            int index;
            return int.TryParse(name.Substring(prefix.Length), out index) ? index : 0;
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
