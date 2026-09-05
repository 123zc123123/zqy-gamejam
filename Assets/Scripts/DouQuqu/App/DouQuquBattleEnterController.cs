using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>斗蛐蛐入口页：开始匹配进匹配，底栏用各功能页共用的可横滑入口。</summary>
    public sealed class DouQuquBattleEnterController : MonoBehaviour
    {
        private void Start()
        {
            if (!DouQuquPlayerDataService.RequireLogin()) return;
            Bind("StartMatchButton", DouQuquSceneNames.Matchmaking);
            GameObject canvas = FindNamed("Canvas");
            if (canvas != null) DouQuquBottomNavBar.EnsureOn(canvas.transform);
            else EnsureBackButton();
        }

        private static void Bind(string objectName, string sceneName)
        {
            GameObject go = FindNamed(objectName);
            if (go == null)
            {
                Debug.LogWarning("[DouQuqu] 入口页没有按钮 " + objectName);
                return;
            }
            BindGo(go, sceneName);
        }

        private static void BindGo(GameObject go, string sceneName)
        {
            Button button = go.GetComponent<Button>();
            if (button == null) button = go.AddComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => DouQuquSceneNames.Load(sceneName));
        }

        private static void EnsureBackButton()
        {
            GameObject existing = FindNamed("BackButton");
            if (existing != null)
            {
                BindGo(existing, DouQuquSceneNames.MainMenu);
                return;
            }

            GameObject carousel = FindNamed("BottomCarousel");
            if (carousel == null)
            {
                Debug.LogWarning("[DouQuqu] 入口页没有 BottomCarousel，无法加返回按钮");
                return;
            }

            GameObject back = new GameObject("BackButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            back.transform.SetParent(carousel.transform, false);
            RectTransform rect = back.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-450f, 0f);
            rect.sizeDelta = new Vector2(140f, 140f);
            Image image = back.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            BindGo(back, DouQuquSceneNames.MainMenu);
        }

        private static GameObject FindNamed(string objectName)
        {
            Transform[] transforms = FindObjectsOfType<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t.name == objectName && t.gameObject.scene.IsValid())
                    return t.gameObject;
            }
            return null;
        }
    }
}
