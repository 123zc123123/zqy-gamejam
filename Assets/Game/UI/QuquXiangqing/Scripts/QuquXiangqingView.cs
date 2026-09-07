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

        public event System.Action Closed;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (sellButton != null) sellButton.onClick.AddListener(() => Debug.Log("蛐蛐详情：出售暂未接入"));
            if (storeButton != null) storeButton.onClick.AddListener(() => Debug.Log("蛐蛐详情：收入背包暂未接入"));
            CacheLabels();
        }

        public void Show(string rank, string displayName, string description, Sprite sprite, string subtitle = null)
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
