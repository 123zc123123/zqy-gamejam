using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZqyGameJam.UI.QuquXiangqing
{
    /// <summary>蛐蛐详情弹层。育虫盘、背包点虫子时打开。</summary>
    public sealed class QuquXiangqingView : MonoBehaviour
    {
        public Button sellButton;
        public Button storeButton;
        public Button closeButton;
        public TMP_Text titleText;
        public TMP_Text nameText;
        public TMP_Text rankText;
        public TMP_Text descriptionText;
        public Image portrait;
        public TMP_Text[] statValues;

        public event System.Action Closed;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (sellButton != null) sellButton.onClick.AddListener(() => Debug.Log("蛐蛐详情：出售暂未接入"));
            if (storeButton != null) storeButton.onClick.AddListener(() => Debug.Log("蛐蛐详情：收入背包暂未接入"));
            CacheLabels();
        }

        static readonly Color StatNormal = new Color(0.176471f, 0.352941f, 0.152941f, 1f);
        static readonly Color StatStrong = new Color(0.619608f, 0.164706f, 0.168627f, 1f);

        public void Show(string rank, string displayName, string description, Sprite sprite, string subtitle = null, string[] stats = null, bool[] strongStats = null)
        {
            CacheLabels();
            gameObject.SetActive(true);
            Write(titleText, "◇ " + (string.IsNullOrEmpty(displayName) ? "促织" : displayName) + " ◇");
            Write(nameText, string.IsNullOrEmpty(subtitle) ? (displayName ?? "") : subtitle);
            Write(rankText, rank ?? "");
            Write(descriptionText, description ?? "");
            if (portrait != null)
            {
                portrait.sprite = sprite;
                portrait.enabled = sprite != null;
                portrait.preserveAspect = true;
            }
            WriteStats(stats, strongStats);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        private static void Write(TMP_Text label, string value)
        {
            if (label == null) return;
            label.text = value;
            label.ForceMeshUpdate();
        }

        private void WriteStats(string[] stats, bool[] strongStats)
        {
            if (statValues == null) return;
            for (int i = 0; i < statValues.Length; i++)
            {
                TMP_Text label = statValues[i];
                string value = stats != null && i < stats.Length && !string.IsNullOrEmpty(stats[i])
                    ? stats[i]
                    : "—";
                bool strong = strongStats != null && i < strongStats.Length && strongStats[i] && value != "—";
                Write(label, value);
                if (label == null) continue;
                label.fontStyle = strong ? FontStyles.Bold : FontStyles.Normal;
                label.color = strong ? StatStrong : StatNormal;
            }
        }

        private void CacheLabels()
        {
            TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text label = labels[i];
                if (label == null) continue;
                string objectName = label.name;
                string text = label.text ?? "";
                if (titleText == null && (objectName == "TitleText" || text.IndexOf("促织", System.StringComparison.Ordinal) >= 0))
                    titleText = label;
                else if (nameText == null && (objectName == "NameText" || text.IndexOf("正紫龟", System.StringComparison.Ordinal) >= 0))
                    nameText = label;
                else if (rankText == null && (objectName == "RankText" || text.IndexOf("领军将", System.StringComparison.Ordinal) >= 0))
                    rankText = label;
                else if (descriptionText == null && (objectName == "DescriptionText" || text.IndexOf("龟形", System.StringComparison.Ordinal) >= 0))
                    descriptionText = label;
            }

            if (statValues == null || statValues.Length < 6)
                statValues = new TMP_Text[6];
            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text label = labels[i];
                if (label == null) continue;
                int index = StatValueIndex(label.name);
                if (index >= 0 && index < statValues.Length) statValues[index] = label;
            }

            if (portrait == null)
            {
                string[] portraitNames = { "Portrait", "CricketPortrait", "violet-cricket-illustration", "InsectPortraitArea" };
                for (int i = 0; i < portraitNames.Length && portrait == null; i++)
                {
                    Transform found = FindDeep(transform, portraitNames[i]);
                    if (found == null) continue;
                    portrait = found.GetComponent<Image>();
                    if (portrait == null) portrait = found.GetComponentInChildren<Image>(true);
                }
            }
        }

        private static int StatValueIndex(string objectName)
        {
            if (string.IsNullOrEmpty(objectName) || objectName.IndexOf("_Value", System.StringComparison.Ordinal) < 0)
                return -1;
            if (objectName.IndexOf("Stat01", System.StringComparison.Ordinal) >= 0) return 0;
            if (objectName.IndexOf("Stat02", System.StringComparison.Ordinal) >= 0) return 1;
            if (objectName.IndexOf("Stat03", System.StringComparison.Ordinal) >= 0) return 2;
            if (objectName.IndexOf("Stat04", System.StringComparison.Ordinal) >= 0) return 3;
            if (objectName.IndexOf("Stat05", System.StringComparison.Ordinal) >= 0) return 4;
            if (objectName.IndexOf("Stat06", System.StringComparison.Ordinal) >= 0) return 5;
            return -1;
        }

        private static Transform FindDeep(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDeep(root.GetChild(i), objectName);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
