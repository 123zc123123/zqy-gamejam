using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>结果页：名次、头像、名字、排名得分、击杀得分。</summary>
    public sealed class SettlementPage : MonoBehaviour
    {
        const int RowCount = 4;
        const string RowPrefabPath = "Settlement/Prefabs/Parts/PlayerRow";
        const string RankTextureFolder = "Settlement/Textures/";
        const string RankIconFolder = "Common/Textures/";
        static readonly string[] RankLabels = { "第1名", "第2名", "第3名", "第4名" };
        static readonly Vector2[] DefaultRowPositions =
        {
            new Vector2(1.5f, 353f),
            new Vector2(1.5f, 79f),
            new Vector2(1.5f, -195f),
            new Vector2(1.5f, -469f)
        };

        private void Awake()
        {
            EnsureRows();
            UiFonts.ApplyTree(transform);
        }

        public void Bind(MatchController match, MatchKind kind, int localPlayerId)
        {
            if (match == null) return;
            EnsureRows();
            UiFonts.ApplyTree(transform);
            HideSpectateButton();
            int playerCount = Mathf.Clamp(match.ConfiguredPlayers, 0, RowCount);
            int[] order = SortByPlace(match, playerCount);
            bool award = kind == MatchKind.Random || kind == MatchKind.Friend;

            for (int i = 0; i < RowCount; i++)
            {
                Transform row = transform.Find("PlayerRow" + (i + 1));
                if (row == null) continue;
                if (i >= playerCount)
                {
                    row.gameObject.SetActive(false);
                    continue;
                }

                row.gameObject.SetActive(true);
                int playerId = order[i];
                int place = match.Place(playerId);
                if (place <= 0) place = i + 1;
                int placeScore = PlayerDataService.PointsForPlace(place);
                int killScore = match.MatchScore(playerId);

                SetText(row, "Name", DisplayName(match, playerId, localPlayerId));
                SetText(row, "PlaceScore", placeScore.ToString());
                SetText(row, "KillScore", killScore.ToString());
                BindRank(row, place);
                PlayerPalette.BindAvatar(row);
                PlayerPalette.PaintOutline(row, playerId);
                PlayerPalette.SetMeSign(row, playerId == localPlayerId);

                if (award && playerId == localPlayerId)
                    PlayerDataService.AwardMatchRewards(place, killScore);
            }
        }

        public void HideForSpectate()
        {
            HideNamed("BlackBackground");
            HideNamed("Banner");
            for (int i = 0; i < RowCount; i++)
                HideNamed("PlayerRow" + (i + 1));
            HideNamed("观战");
        }

        static int[] SortByPlace(MatchController match, int playerCount)
        {
            int[] order = new int[playerCount];
            for (int i = 0; i < playerCount; i++) order[i] = i;
            System.Array.Sort(order, (a, b) =>
            {
                int pa = match.Place(a);
                int pb = match.Place(b);
                if (pa <= 0) pa = 99;
                if (pb <= 0) pb = 99;
                int cmp = pa.CompareTo(pb);
                if (cmp != 0) return cmp;
                return match.MatchScore(b).CompareTo(match.MatchScore(a));
            });
            return order;
        }

        void EnsureRows()
        {
            bool missing = false;
            for (int i = 0; i < RowCount; i++)
            {
                if (transform.Find("PlayerRow" + (i + 1)) == null)
                {
                    missing = true;
                    break;
                }
            }

            if (!missing) return;

            GameObject prefab = Resources.Load<GameObject>(RowPrefabPath);
            if (prefab == null) return;

            Transform footer = transform.Find("退出");
            if (footer == null) footer = transform.Find("返回");
            int insertAt = footer != null ? footer.GetSiblingIndex() : transform.childCount;
            for (int i = 0; i < RowCount; i++)
            {
                string rowName = "PlayerRow" + (i + 1);
                Transform existing = transform.Find(rowName);
                if (existing != null) continue;

                GameObject row = Instantiate(prefab, transform, false);
                row.name = rowName;
                row.transform.SetSiblingIndex(insertAt + i);
                RectTransform rect = row.GetComponent<RectTransform>();
                if (rect == null) continue;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = DefaultRowPositions[i];
                rect.localScale = Vector3.one;
            }
        }

        static void BindRank(Transform row, int place)
        {
            int clamped = Mathf.Clamp(place, 1, RowCount);
            Sprite bg = Resources.Load<Sprite>(RankTextureFolder + "rank" + clamped + "-bg");
            Sprite icon = Resources.Load<Sprite>(RankIconFolder + "rank" + clamped + "-icon");

            Image rowImage = row.GetComponent<Image>();
            if (rowImage != null && bg != null) rowImage.sprite = bg;

            Transform rank = FindNamed(row, "Rank");
            if (rank != null)
            {
                Image rankImage = rank.GetComponent<Image>();
                if (rankImage != null && icon != null)
                {
                    rankImage.sprite = icon;
                    rankImage.preserveAspect = true;
                }
                SetText(rank, "RankText", RankLabels[clamped - 1]);
            }
        }

        static string DisplayName(MatchController match, int playerId, int localPlayerId)
        {
            LanSession net = AppServices.Instance != null ? AppServices.Instance.Network : null;
            if (net != null && net.Slots != null)
            {
                for (int i = 0; i < net.Slots.Count; i++)
                {
                    LanPlayerSlot slot = net.Slots[i];
                    if (slot != null && slot.playerId == playerId && !string.IsNullOrEmpty(slot.playerName))
                        return slot.playerName;
                }
            }

            if (playerId == localPlayerId)
            {
                if (PlayerDataService.IsLoggedIn && !string.IsNullOrEmpty(PlayerDataService.CurrentPlayerName))
                    return PlayerDataService.CurrentPlayerName;
            }

            if (playerId != localPlayerId)
            {
                int bot = playerId > localPlayerId ? playerId - 1 : playerId;
                return TrainingCamp.BotName(bot);
            }

            return "玩家" + (playerId + 1);
        }

        static void SetText(Transform root, string name, string value)
        {
            Transform t = FindNamed(root, name);
            if (t == null) return;
            TMP_Text tmp = t.GetComponent<TMP_Text>();
            if (tmp != null) tmp.text = value;
        }

        void HideSpectateButton()
        {
            Transform watch = FindNamed(transform, "观战");
            if (watch != null) watch.gameObject.SetActive(false);
            Transform exit = FindNamed(transform, "退出");
            RectTransform exitRect = exit != null ? exit as RectTransform : null;
            if (exitRect != null) exitRect.anchoredPosition = Vector2.zero;
        }

        void HideNamed(string objectName)
        {
            Transform found = transform.Find(objectName);
            if (found != null) found.gameObject.SetActive(false);
        }

        static Transform FindNamed(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            Transform direct = root.Find(objectName);
            if (direct != null) return direct;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform nested = FindNamed(root.GetChild(i), objectName);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
