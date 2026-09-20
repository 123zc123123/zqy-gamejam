using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>出局结算：三条命用完且对局未结束时，只写自己的名次和两项得分。</summary>
    public sealed class EliminationPage : MonoBehaviour
    {
        const string RankIconFolder = "Common/Textures/";
        static readonly string[] RankLabels = { "第1名", "第2名", "第3名", "第4名" };

        public event Action WatchClicked;
        public event Action ExitClicked;

        private void Awake()
        {
            UiFonts.ApplyTree(transform);
            BindButtons();
        }

        private void OnEnable()
        {
            BindButtons();
        }

        public void Bind(MatchController match, int localPlayerId)
        {
            if (match == null) return;
            UiFonts.ApplyTree(transform);
            BindButtons();

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
            BindButtons();
        }

        void BindButtons()
        {
            Transform footer = FindNamed(transform, "Footer");
            if (footer != null) footer.SetAsLastSibling();
            BindClick(FindNamed(transform, "观战"), OnWatch);
            BindClick(FindNamed(transform, "退出"), OnExit);
        }

        void OnWatch()
        {
            if (WatchClicked != null) WatchClicked.Invoke();
        }

        void OnExit()
        {
            if (ExitClicked != null) ExitClicked.Invoke();
        }

        static void BindClick(Transform root, UnityEngine.Events.UnityAction clicked)
        {
            if (root == null) return;
            Button button = root.GetComponent<Button>();
            if (button == null) button = root.gameObject.AddComponent<Button>();
            Image image = root.GetComponent<Image>();
            if (image == null) image = root.gameObject.AddComponent<Image>();
            image.raycastTarget = true;
            button.targetGraphic = image;
            button.interactable = true;
            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] == null || graphics[i].gameObject == root.gameObject) continue;
                graphics[i].raycastTarget = false;
            }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(clicked);
        }

        void BindRank(int place)
        {
            int clamped = Mathf.Clamp(place, 1, 4);
            Transform rank = FindNamed(transform, "Rank");
            if (rank == null) return;
            Image image = rank.GetComponent<Image>();
            if (image == null) return;
            Sprite icon = Resources.Load<Sprite>(RankIconFolder + "rank" + clamped + "-icon");
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
