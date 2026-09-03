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
        public Text nameText;
        public Text rankText;
        public Text descriptionText;
        public Image portrait;

        public event System.Action Closed;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (sellButton != null) sellButton.onClick.AddListener(() => Debug.Log("蛐蛐详情：出售暂未接入"));
            if (storeButton != null) storeButton.onClick.AddListener(() => Debug.Log("蛐蛐详情：收入背包暂未接入"));
            CacheLabels();
        }

        public void Show(string rank, string displayName, string description, Sprite sprite)
        {
            CacheLabels();
            gameObject.SetActive(true);
            if (rankText != null) rankText.text = rank ?? "";
            if (nameText != null) nameText.text = displayName ?? "";
            if (descriptionText != null) descriptionText.text = description ?? "";
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

        private void CacheLabels()
        {
            if (nameText == null) nameText = FindLabel("NameText");
            if (rankText == null) rankText = FindLabel("RankText");
            if (descriptionText == null) descriptionText = FindLabel("DescriptionText");
            if (portrait == null)
            {
                Transform found = FindDeep(transform, "Portrait");
                if (found != null) portrait = found.GetComponent<Image>();
            }
        }

        private Text FindLabel(string objectName)
        {
            Transform found = FindDeep(transform, objectName);
            return found != null ? found.GetComponent<Text>() : null;
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
