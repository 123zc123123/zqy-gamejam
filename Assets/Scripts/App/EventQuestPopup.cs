using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 活动页中部「蛐蛐新手」在当前界面弹出日勤任务，不切场景。
    /// </summary>
    public sealed class EventQuestPopup : MonoBehaviour
    {
        [SerializeField] private GameObject popupRoot;

        private void Awake()
        {
            if (popupRoot == null)
            {
                Transform found = transform.Find("Popup_renwu");
                if (found != null) popupRoot = found.gameObject;
            }

            Hide();
            BindOpenTargets();
            BindCloseTargets();
            BindClaimTargets();
        }

        public void Show()
        {
            if (popupRoot != null) popupRoot.SetActive(true);
        }

        public void Hide()
        {
            if (popupRoot != null) popupRoot.SetActive(false);
        }

        private void BindOpenTargets()
        {
            Transform progress = FindDirect(transform, "progress-card");
            if (progress == null) progress = FindNamed(transform, "progress-card");
            if (progress != null) BindButton(progress.gameObject, Show);
        }

        private void BindCloseTargets()
        {
            if (popupRoot == null) return;

            Transform dim = popupRoot.transform.Find("Dim");
            if (dim != null) BindButton(dim.gameObject, Hide);

            Transform back = FindNamed(popupRoot.transform, "返回");
            if (back != null) BindButton(back.gameObject, Hide);
        }

        private void BindClaimTargets()
        {
            if (popupRoot == null) return;
            Transform[] children = popupRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name != "领取") continue;
                GameObject go = children[i].gameObject;
                BindButton(go, () => MarkClaimed(go));
            }
        }

        private static void MarkClaimed(GameObject go)
        {
            Button button = go.GetComponent<Button>();
            if (button != null) button.interactable = false;
            Image image = go.GetComponent<Image>();
            if (image != null)
            {
                Color color = image.color;
                color.a = 0.45f;
                image.color = color;
            }

            TMP_Text tmp = go.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = "已领取";
            Text legacy = go.GetComponentInChildren<Text>(true);
            if (legacy != null) legacy.text = "已领取";
        }

        private static void BindButton(GameObject go, UnityEngine.Events.UnityAction clicked)
        {
            if (go == null) return;
            Image image = go.GetComponent<Image>();
            if (image == null) image = go.AddComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
                if (image.color.a <= 0.01f && image.sprite == null)
                    image.color = new Color(1f, 1f, 1f, 0.01f);
            }

            Button button = go.GetComponent<Button>();
            if (button == null) button = go.AddComponent<Button>();
            if (button == null) return;
            button.transition = Selectable.Transition.None;
            if (image != null) button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(clicked);
        }

        private static Transform FindDirect(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name) return child;
            }

            return null;
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root.name == name) return root;
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != root && children[i].name == name) return children[i];
            }

            return null;
        }
    }
}
