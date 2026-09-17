using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 生成合成成功展示预制体：遮黑、立绘、普通字 / 极品字。
    /// 菜单：DouQuqu / Rebuild Merge Reveal Prefab
    /// </summary>
    public static class MergeRevealPrefabBuilder
    {
        private const string PrefabPath = "Assets/Resources/Merge/Prefabs/MergeReveal.prefab";
        private const string FontPath = "Assets/Resources/Fonts/Chinese SDF.asset";

        [InitializeOnLoadMethod]
        private static void AutoBuildIfMissing()
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

        [MenuItem("DouQuqu/Rebuild Merge Reveal Prefab")]
        public static void Rebuild()
        {
            Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DouQuqu] 合成展示预制体已重建：" + PrefabPath);
        }

        private static void Build()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                Debug.LogError("[DouQuqu] 找不到中文 TMP 字体：" + FontPath);
                return;
            }

            string folder = Path.GetDirectoryName(PrefabPath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == PrefabPath)
            {
                Debug.LogWarning("[DouQuqu] 请先关掉 MergeReveal 预制体编辑再重建。");
                return;
            }

            if (File.Exists(PrefabPath))
                AssetDatabase.DeleteAsset(PrefabPath);

            GameObject root = new GameObject("MergeReveal", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                root.name = "MergeReveal";
                Canvas canvas = root.GetComponent<Canvas>();
                if (canvas == null) canvas = root.AddComponent<Canvas>();
                canvas.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 5200;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 1f;
                if (root.GetComponent<GraphicRaycaster>() == null)
                    root.AddComponent<GraphicRaycaster>();
                Stretch(root.GetComponent<RectTransform>());

                Image veil = EnsureImage(root.transform, "Veil", new Color(0f, 0f, 0f, 0.78f));
                Stretch(veil.rectTransform);
                veil.raycastTarget = true;
                Button veilButton = veil.GetComponent<Button>();
                if (veilButton == null) veilButton = veil.gameObject.AddComponent<Button>();
                veilButton.transition = Selectable.Transition.None;
                veilButton.targetGraphic = veil;
                veilButton.navigation = new Navigation { mode = Navigation.Mode.None };

                RectTransform fxRoot = EnsureRect(root.transform, "FxRoot");
                Stretch(fxRoot);

                Image portrait = EnsureImage(root.transform, "Portrait", Color.white);
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;
                portrait.enabled = true;
                RectTransform portraitRect = portrait.rectTransform;
                portraitRect.anchorMin = new Vector2(0.5f, 0.5f);
                portraitRect.anchorMax = new Vector2(0.5f, 0.5f);
                portraitRect.pivot = new Vector2(0.5f, 0.5f);
                portraitRect.sizeDelta = new Vector2(420f, 420f);
                portraitRect.anchoredPosition = Vector2.zero;

                GameObject normal = EnsureGroup(root.transform, "Normal");
                Transform leftoverSubtitle = normal.transform.Find("Subtitle");
                if (leftoverSubtitle != null) Object.DestroyImmediate(leftoverSubtitle.gameObject);
                TMP_Text normalTitle = EnsureText(normal.transform, "Title", font, 96f, "凡品",
                    new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.88f),
                    new Color(0.86f, 0.86f, 0.84f, 1f), FontStyles.Bold, 0.22f);
                TMP_Text normalName = EnsureText(normal.transform, "Name", font, 48f, "土狗",
                    new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.32f),
                    new Color(0.94f, 0.94f, 0.92f, 1f), FontStyles.Bold, 0.2f);
                TMP_Text normalTemperamentLabel = EnsureText(normal.transform, "TemperamentLabel", font, 40f, "性格",
                    new Vector2(0.5f, 0.19f), new Vector2(0.5f, 0.19f),
                    new Color(0.78f, 0.78f, 0.74f, 1f), FontStyles.Bold, 0.18f);
                PinBesideCenter(normalTemperamentLabel.rectTransform, true);
                normalTemperamentLabel.alignment = TextAlignmentOptions.MidlineRight;
                TMP_Text normalTemperament = EnsureText(normal.transform, "Temperament", font, 40f, "耐战",
                    new Vector2(0.5f, 0.19f), new Vector2(0.5f, 0.19f),
                    new Color(0.94f, 0.94f, 0.92f, 1f), FontStyles.Bold, 0.18f);
                PinBesideCenter(normalTemperament.rectTransform, false);
                normalTemperament.alignment = TextAlignmentOptions.MidlineLeft;
                TMP_Text normalHint = EnsureText(normal.transform, "Hint", font, 32f, "点击收入背包",
                    new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.14f),
                    new Color(1f, 1f, 1f, 1f), FontStyles.Normal, 0.16f);

                GameObject legendary = EnsureGroup(root.transform, "Legendary");
                TMP_Text legendaryTitle = EnsureText(legendary.transform, "Title", font, 120f, "极 品",
                    new Vector2(0.06f, 0.72f), new Vector2(0.94f, 0.88f),
                    new Color(1f, 0.86f, 0.28f, 1f), FontStyles.Bold, 0.28f);
                TMP_Text legendarySubtitle = EnsureText(legendary.transform, "Subtitle", font, 48f, "吕布  天下无双",
                    new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.28f),
                    new Color(1f, 0.93f, 0.7f, 1f), FontStyles.Bold, 0.22f);
                TMP_Text legendaryHint = EnsureText(legendary.transform, "Hint", font, 32f, "点击收入背包",
                    new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.14f),
                    new Color(1f, 0.96f, 0.78f, 1f), FontStyles.Normal, 0.16f);
                legendary.SetActive(false);
                normal.SetActive(true);

                veil.transform.SetSiblingIndex(0);
                fxRoot.SetSiblingIndex(1);
                portrait.transform.SetSiblingIndex(2);
                normal.transform.SetSiblingIndex(3);
                legendary.transform.SetSiblingIndex(4);

                FinestRevealFx view = root.GetComponent<FinestRevealFx>();
                if (view == null) view = root.AddComponent<FinestRevealFx>();
                SerializedObject so = new SerializedObject(view);
                SetObject(so, "veil", veil);
                SetObject(so, "veilButton", veilButton);
                SetObject(so, "portrait", portrait);
                SetObject(so, "fxRoot", fxRoot);
                SetObject(so, "normalGroup", normal);
                SetObject(so, "legendaryGroup", legendary);
                SetObject(so, "normalTitle", normalTitle);
                SetObject(so, "normalName", normalName);
                SetObject(so, "normalTemperamentLabel", normalTemperamentLabel);
                SetObject(so, "normalTemperament", normalTemperament);
                SetObject(so, "normalHint", normalHint);
                SetObject(so, "legendaryTitle", legendaryTitle);
                SetObject(so, "legendarySubtitle", legendarySubtitle);
                SetObject(so, "legendaryHint", legendaryHint);
                so.ApplyModifiedPropertiesWithoutUndo();

                Stretch(root.GetComponent<RectTransform>());
                canvas.enabled = true;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        private static void SetObject(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property != null) property.objectReferenceValue = value;
        }

        private static GameObject EnsureGroup(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            return go;
        }

        private static RectTransform EnsureRect(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                RectTransform rect = existing as RectTransform;
                return rect != null ? rect : existing.gameObject.AddComponent<RectTransform>();
            }
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static Image EnsureImage(Transform parent, string name, Color color)
        {
            Transform existing = parent.Find(name);
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image == null)
            {
                GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                if (existing == null) go.transform.SetParent(parent, false);
                image = go.GetComponent<Image>();
                if (image == null) image = go.AddComponent<Image>();
            }
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text EnsureText(
            Transform parent,
            string name,
            TMP_FontAsset font,
            float size,
            string content,
            Vector2 min,
            Vector2 max,
            Color color,
            FontStyles style,
            float outline)
        {
            Transform existing = parent.Find(name);
            TextMeshProUGUI text = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
            if (text == null)
            {
                GameObject go = existing != null
                    ? existing.gameObject
                    : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                if (existing == null) go.transform.SetParent(parent, false);
                text = go.GetComponent<TextMeshProUGUI>();
                if (text == null) text = go.AddComponent<TextMeshProUGUI>();
            }
            RectTransform rect = text.rectTransform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void PinBesideCenter(RectTransform rect, bool leftOfCenter)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.5f, 0.19f);
            rect.anchorMax = new Vector2(0.5f, 0.19f);
            rect.pivot = leftOfCenter ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(280f, 56f);
            rect.anchoredPosition = new Vector2(leftOfCenter ? -6f : 6f, 0f);
        }
    }
}
