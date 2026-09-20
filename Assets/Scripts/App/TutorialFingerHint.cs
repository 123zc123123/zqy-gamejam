using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>跳跃课：按开局选的方向循环演示点屏幕并拖动摇杆。</summary>
    public sealed class TutorialFingerHint : MonoBehaviour
    {
        const string ResourcePath = "Battle/Hud/Textures/GuideFinger";
        const float Cycle = 2.2f;
        const float Size = 240f;
        const float NavSize = 180f;

        static TutorialFingerHint instance;

        RectTransform rect;
        CanvasGroup group;
        Vector2 home;
        Vector2 drag;
        float clock;
        float size = Size;
        bool jumpHint;

        public static void Show(Transform hudRoot)
        {
            if (hudRoot == null) return;
            if (instance == null) instance = Create(hudRoot);
            else instance.Attach(hudRoot);
            instance.clock = 0f;
            instance.size = Size;
            instance.jumpHint = true;
            instance.gameObject.SetActive(true);
        }

        public static void ShowNavSwipe(Transform barRoot)
        {
            if (barRoot == null) return;
            if (instance == null) instance = Create(barRoot);
            instance.AttachSwipe(barRoot);
            instance.clock = 0f;
            instance.jumpHint = false;
            instance.gameObject.SetActive(true);
        }

        public static void Hide()
        {
            if (instance == null) return;
            instance.gameObject.SetActive(false);
        }

        static TutorialFingerHint Create(Transform hudRoot)
        {
            GameObject go = new GameObject("TutorialFingerHint", typeof(RectTransform), typeof(CanvasGroup), typeof(CanvasRenderer), typeof(Image));
            TutorialFingerHint hint = go.AddComponent<TutorialFingerHint>();
            hint.rect = go.GetComponent<RectTransform>();
            hint.group = go.GetComponent<CanvasGroup>();
            Image image = go.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>(ResourcePath);
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            hint.Attach(hudRoot);
            return hint;
        }

        void AttachSwipe(Transform barRoot)
        {
            size = NavSize;
            rect.SetParent(barRoot, false);
            rect.SetAsLastSibling();
            bool fullCanvas = barRoot.GetComponent<Canvas>() != null;
            if (fullCanvas)
                rect.anchorMin = rect.anchorMax = new Vector2(0.32f, 0.09f);
            else
                rect.anchorMin = rect.anchorMax = new Vector2(0.28f, 0.55f);
            rect.pivot = new Vector2(0.22f, 0.84f);
            rect.sizeDelta = new Vector2(size, size);
            home = Vector2.zero;
            drag = new Vector2(260f, 0f);
            rect.anchoredPosition = home;
            rect.localScale = Vector3.one;
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        void Attach(Transform hudRoot)
        {
            rect.SetParent(hudRoot, false);
            rect.SetAsLastSibling();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.32f);
            rect.pivot = new Vector2(0.22f, 0.84f);
            size = Size;
            rect.sizeDelta = new Vector2(size, size);
            home = new Vector2(90f, -40f);
            drag = home + JumpDragDelta();
            rect.anchoredPosition = home;
            rect.localRotation = Quaternion.identity;
            rect.localScale = JumpHintScale();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        /// <summary>原来那套往后拖；选「同向」时整段拖法取反，手也倒过来。</summary>
        public static Vector2 JumpDragDelta()
        {
            InputDirectionSettings.Load();
            Vector2 reverse = new Vector2(-260f, 250f);
            return InputDirectionSettings.ReverseDrag ? reverse : -reverse;
        }

        public static Vector3 JumpHintScale()
        {
            InputDirectionSettings.Load();
            return InputDirectionSettings.ReverseDrag ? Vector3.one : new Vector3(-1f, -1f, 1f);
        }

        void LateUpdate()
        {
            if (!isActiveAndEnabled) return;
            transform.SetAsLastSibling();
            clock += Time.unscaledDeltaTime;
            float t = clock % Cycle;
            float alpha = 1f;
            Vector2 pos = home;
            float scale = 1f;

            if (t < 0.28f)
            {
                alpha = Mathf.Clamp01(t / 0.18f);
                pos = home;
                scale = 1f;
            }
            else if (t < 0.42f)
            {
                float u = (t - 0.28f) / 0.14f;
                pos = home;
                scale = Mathf.Lerp(1f, 0.86f, u);
            }
            else if (t < 1.25f)
            {
                float u = (t - 0.42f) / 0.83f;
                u = u * u * (3f - 2f * u);
                pos = Vector2.Lerp(home, drag, u);
                scale = 0.86f;
            }
            else if (t < 1.45f)
            {
                pos = drag;
                scale = 0.86f;
            }
            else if (t < 1.75f)
            {
                float u = (t - 1.45f) / 0.3f;
                pos = drag;
                scale = Mathf.Lerp(0.86f, 1f, u);
                alpha = 1f - u;
            }
            else
            {
                pos = home;
                scale = 1f;
                alpha = 0f;
            }

            rect.anchoredPosition = pos;
            Vector3 hint = jumpHint ? JumpHintScale() : Vector3.one;
            rect.localScale = new Vector3(hint.x * scale, hint.y * scale, hint.z);
            group.alpha = alpha;
        }
    }
}
