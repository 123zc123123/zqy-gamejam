using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>排行榜：本机各登录名按账号积分从高到低。</summary>
    public sealed class RankingController : MonoBehaviour
    {
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
            List<PlayerProfile> ranks = PlayerDataService.GetRankingSnapshot();
            if (VenueClient.Instance != null && VenueClient.Instance.CachedRanking != null
                && VenueClient.Instance.CachedRanking.Length > 0)
            {
                ranks = new List<PlayerProfile>(VenueClient.Instance.CachedRanking);
            }
            List<Transform> rows = CollectRows(pageRoot.transform);
            string self = PlayerDataService.CurrentPlayerName;
            for (int i = 0; i < rows.Count; i++)
            {
                Transform row = rows[i];
                if (row == null) continue;
                if (i >= ranks.Count)
                {
                    row.gameObject.SetActive(false);
                    continue;
                }
                row.gameObject.SetActive(true);
                PlayerProfile entry = ranks[i];
                bool mine = !string.IsNullOrEmpty(self)
                    && string.Equals(self, entry.playerName, System.StringComparison.OrdinalIgnoreCase);
                WriteRow(row, i + 1, entry.playerName, entry.score, mine);
            }
        }

        private static void WriteRow(Transform row, int rank, string playerName, int score, bool mine)
        {
            SetNamed(row, "玩家1", playerName);
            SetNamed(row, "1200", score.ToString());
            SetNamed(row, "1", rank.ToString());
            TMP_Text[] labels = row.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                if (mine) labels[i].fontStyle = FontStyles.Bold;
            }
        }

        private static void SetNamed(Transform row, string objectName, string value)
        {
            Transform found = FindNamed(row, objectName);
            if (found == null) return;
            TMP_Text label = found.GetComponent<TMP_Text>();
            if (label != null) label.text = value;
        }

        private static List<Transform> CollectRows(Transform root)
        {
            List<Transform> rows = new List<Transform>();
            CollectNamed(root, "PlayerRowTop", rows);
            CollectNamed(root, "PlayerRow", rows);
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
