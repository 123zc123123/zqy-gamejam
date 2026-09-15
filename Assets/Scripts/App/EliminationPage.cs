using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>出局结算：三条命用完且对局未结束时，只写自己的名次和两项得分。</summary>
    public sealed class EliminationPage : MonoBehaviour
    {
        const string RankTextureFolder = "Settlement/Textures/";
        static readonly string[] RankLabels = { "第1名", "第2名", "第3名", "第4名" };

        private void Awake()
        {
            UiFonts.ApplyTree(transform);
        }

        public void Bind(MatchController match, int localPlayerId)
        {
            if (match == null) return;
            UiFonts.ApplyTree(transform);

            int place = match.Place(localPlayerId);
            if (place <= 0) place = 4;
            int placeScore = PlayerDataService.PointsForPlace(place);
            int killScore = match.MatchScore(localPlayerId);

            SetText(transform, "Title", "您已出局");
            SetText(transform, "PlaceLabel", "本场名次");
            SetText(transform, "PlaceValue", RankLabels[Mathf.Clamp(place, 1, RankLabels.Length) - 1]);
            SetText(transform, "PlaceScore", placeScore.ToString());
            SetText(transform, "KillScore", killScore.ToString());
            BindRank(place);
        }

        void BindRank(int place)
        {
            int clamped = Mathf.Clamp(place, 1, 4);
            Transform rank = FindNamed(transform, "Rank");
            if (rank == null) return;
            Image image = rank.GetComponent<Image>();
            if (image == null) return;
            Sprite icon = Resources.Load<Sprite>(RankTextureFolder + "rank" + clamped + "-icon");
            if (icon != null) image.sprite = icon;
            image.preserveAspect = true;
        }

        static void SetText(Transform root, string name, string value)
        {
            Transform t = FindNamed(root, name);
            if (t == null) return;
            TMP_Text tmp = t.GetComponent<TMP_Text>();
            if (tmp != null) tmp.text = value;
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
