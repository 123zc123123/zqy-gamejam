using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>结果页：名次、头像、名字、排名得分、击杀得分。</summary>
    public sealed class SettlementPage : MonoBehaviour
    {
        const int RowCount = 4;
        static readonly string[] RankLabels = { "第1名", "第2名", "第3名", "第4名" };

        private void Awake()
        {
            UiFonts.ApplyTree(transform);
        }

        public void Bind(MatchController match, MatchKind kind, int localPlayerId)
        {
            if (match == null) return;
            UiFonts.ApplyTree(transform);
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
                Transform rank = row.Find("Rank");
                if (rank != null)
                    SetText(rank, "RankText", RankLabels[Mathf.Clamp(place - 1, 0, RankLabels.Length - 1)]);
                BindAvatar(row, match, playerId);

                if (award && playerId == localPlayerId)
                    PlayerDataService.AwardPlaceRewards(place);
            }
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

        static void BindAvatar(Transform row, MatchController match, int playerId)
        {
            Transform avatar = row.Find("Avatar");
            if (avatar == null) return;
            Image image = avatar.GetComponentInChildren<Image>(true);
            if (image == null) return;
            int slot = match.CricketIndex(playerId);
            CricketPick pick = match.RosterPick(playerId, slot);
            Sprite portrait = pick != null ? CricketCatalog.Portrait(pick.quality, pick.temperament) : null;
            if (portrait != null) image.sprite = portrait;
            image.preserveAspect = true;
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
            Transform t = root.Find(name);
            if (t == null) return;
            TMP_Text tmp = t.GetComponent<TMP_Text>();
            if (tmp != null) tmp.text = value;
        }
    }
}
