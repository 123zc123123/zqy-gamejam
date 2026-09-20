using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 宝箱领奖弹层：半透明黑底、任选一只、四只 PackCricket、确认。
    /// 菜单：DouQuqu / Rebuild Quest Chest Pick Prefab
    /// </summary>
    public static class QuestChestPickPrefabBuilder
    {
        public const string PrefabPath = "Assets/Resources/Common/Prefabs/QuestChestPick.prefab";
        const string FontPath = "Assets/Resources/Fonts/Chinese SDF.asset";
        const string PackPath = "Assets/Resources/Common/Prefabs/PackCricket.prefab";
        const string ReadyPath = "Assets/Resources/Common/Prefabs/btn-ready.prefab";
        const string InspectPath = "Assets/Resources/Collection/Textures/StatHelpIcon.png";
        const string BluePath = "Assets/Resources/Common/Textures/蓝色bg.png";

        static readonly Color Dim = new Color(0f, 0f, 0f, 0.72f);
        static readonly Color TitleColor = new Color(0.96f, 0.90f, 0.62f, 1f);

        [InitializeOnLoadMethod]
        static void AutoBuildIfMissing()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                if (File.Exists(PrefabPath)) return;
                PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.assetPath == PrefabPath) return;
                Build();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            };
        }

        [MenuItem("DouQuqu/Rebuild Quest Chest Pick Prefab")]
        public static void Rebuild()
        {
            Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DouQuqu] 宝箱领奖预制体已重建：" + PrefabPath);
        }

        static void Build()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            GameObject packPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackPath);
            GameObject readyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ReadyPath);
            Sprite inspect = AssetDatabase.LoadAssetAtPath<Sprite>(InspectPath);
            Sprite blue = AssetDatabase.LoadAssetAtPath<Sprite>(BluePath);
            if (font == null)
            {
                Debug.LogError("[DouQuqu] 找不到中文 TMP 字体：" + FontPath);
                return;
            }
            if (packPrefab == null)
            {
                Debug.LogError("[DouQuqu] 找不到 PackCricket：" + PackPath);
                return;
            }

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == PrefabPath)
            {
                Debug.LogWarning("[DouQuqu] 请先关掉 QuestChestPick 预制体编辑再重建。");
                return;
            }

            if (File.Exists(PrefabPath))
                AssetDatabase.DeleteAsset(PrefabPath);

            GameObject root = new GameObject("QuestChestPick", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 280;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 1f;
                Stretch(root.GetComponent<RectTransform>());

                Image dimmer = MakeImage(root.transform, "Dimmer", Dim);
                Stretch(dimmer.rectTransform);
                dimmer.raycastTarget = true;

                TextMeshProUGUI title = MakeText(root.transform, "Title", font, 42f, "任选一只", TitleColor);
                RectTransform titleRect = title.rectTransform;
                titleRect.anchorMin = titleRect.anchorMax = titleRect.pivot = new Vector2(0.5f, 0.5f);
                titleRect.sizeDelta = new Vector2(800f, 80f);
                titleRect.anchoredPosition = new Vector2(0f, 280f);

                float spacing = 200f;
                float startX = -1.5f * spacing;
                const float packY = 20f;
                const float packScale = 0.55f;
                for (int t = 1; t <= 4; t++)
                {
                    float x = startX + (t - 1) * spacing;
                    GameObject pack = (GameObject)PrefabUtility.InstantiatePrefab(packPrefab, root.transform);
                    pack.name = "PackCricket_4_" + t;
                    RectTransform packRect = pack.GetComponent<RectTransform>();
                    packRect.anchorMin = packRect.anchorMax = packRect.pivot = new Vector2(0.5f, 0.5f);
                    packRect.sizeDelta = new Vector2(288f, 288f);
                    packRect.anchoredPosition = new Vector2(x, packY);
                    packRect.localScale = new Vector3(packScale, packScale, 1f);
                    packRect.localRotation = Quaternion.identity;

                    Image inspectImage = MakeImage(root.transform, "Inspect_" + t, Color.white);
                    inspectImage.sprite = inspect;
                    inspectImage.preserveAspect = true;
                    inspectImage.raycastTarget = true;
                    RectTransform inspectRect = inspectImage.rectTransform;
                    inspectRect.anchorMin = inspectRect.anchorMax = inspectRect.pivot = new Vector2(0.5f, 0.5f);
                    inspectRect.sizeDelta = new Vector2(68f, 68f);
                    inspectRect.anchoredPosition = new Vector2(x, packY - 124f);
                    Button inspectButton = inspectImage.gameObject.AddComponent<Button>();
                    inspectButton.transition = Selectable.Transition.None;
                    inspectButton.targetGraphic = inspectImage;
                }

                GameObject confirm;
                if (readyPrefab != null)
                    confirm = (GameObject)PrefabUtility.InstantiatePrefab(readyPrefab, root.transform);
                else
                    confirm = new GameObject("Confirm", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                confirm.name = "Confirm";
                confirm.transform.SetParent(root.transform, false);
                RectTransform confirmRect = confirm.GetComponent<RectTransform>();
                confirmRect.anchorMin = confirmRect.anchorMax = confirmRect.pivot = new Vector2(0.5f, 0.5f);
                confirmRect.sizeDelta = new Vector2(321f, 141f);
                confirmRect.anchoredPosition = new Vector2(0f, -430f);
                confirmRect.localScale = Vector3.one;
                Image confirmImage = confirm.GetComponent<Image>();
                if (confirmImage != null)
                {
                    if (blue != null) confirmImage.sprite = blue;
                    confirmImage.color = Color.white;
                    confirmImage.preserveAspect = true;
                    confirmImage.raycastTarget = true;
                }
                TMP_Text confirmLabel = confirm.GetComponentInChildren<TMP_Text>(true);
                if (confirmLabel != null)
                {
                    confirmLabel.text = "确认";
                    confirmLabel.color = Color.white;
                    confirmLabel.raycastTarget = false;
                }
                Button confirmButton = confirm.GetComponent<Button>();
                if (confirmButton == null) confirmButton = confirm.AddComponent<Button>();
                confirmButton.transition = Selectable.Transition.None;
                if (confirmImage != null) confirmButton.targetGraphic = confirmImage;

                if (root.GetComponent<QuestChestPickPopup>() == null)
                    root.AddComponent<QuestChestPickPopup>();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static Image MakeImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TextMeshProUGUI MakeText(Transform parent, string name, TMP_FontAsset font, float size, string content, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        static void Stretch(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
