using DouQuqu;
using FigmaUiImporter;
using FigmaUiImporter.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class FigmaCollectionPrefabImporter
{
    const string PrefabFolder = "Assets/FigmaImports/Prefabs";
    const string ResourcesPrefabFolder = "Assets/Resources/Collection/Prefabs";
    const string CardSliceFolder = "Assets/FigmaImports/5jALrfLrmLV0NIOxaQURCu/240_935/layers";
    const string PageSliceFolder = "Assets/FigmaImports/5jALrfLrmLV0NIOxaQURCu/10_6/layers";
    const string MenuSliceFolder = "Assets/FigmaImports/5jALrfLrmLV0NIOxaQURCu/10_593/layers";

    static readonly Rect PageBounds = new Rect(5933f, 36f, 1080f, 1920f);
    static readonly Rect CardBounds = new Rect(6108f, 338f, 156f, 271f);
    static readonly Rect Badge1Bounds = new Rect(6137f, 344f, 98f, 47f);
    static readonly Rect Badge3Bounds = new Rect(6137f, 481f, 98f, 55f);
    static readonly Rect TabBounds = new Rect(6145f, 1781f, 219f, 150f);
    static readonly Rect BackBounds = new Rect(5949f, 1776f, 180f, 172f);
    static readonly Rect MenuBounds = new Rect(5933f, 1756f, 1080f, 200f);
    static readonly Rect Frame8Bounds = new Rect(6108f, 338f, 729f, 1240f);

    static readonly Rect[] CardSlots =
    {
        new Rect(6108f, 338f, 156f, 271f),
        new Rect(6295.25f, 338f, 156f, 271f),
        new Rect(6482.5f, 338f, 156f, 271f),
        new Rect(6669.75f, 338f, 156f, 271f),
        new Rect(6108f, 658f, 156f, 271f),
        new Rect(6295.25f, 658f, 156f, 271f),
        new Rect(6482.5f, 658f, 156f, 271f),
        new Rect(6669.75f, 658f, 156f, 271f),
        new Rect(6108f, 978f, 156f, 271f),
        new Rect(6295.25f, 978f, 156f, 271f),
        new Rect(6482.5f, 978f, 156f, 271f),
        new Rect(6669.75f, 978f, 156f, 271f),
        new Rect(6108f, 1298f, 156f, 271f),
        new Rect(6295.25f, 1298f, 156f, 271f),
        new Rect(6482.5f, 1298f, 156f, 271f),
        new Rect(6669.75f, 1298f, 156f, 271f)
    };

    static readonly Color TitleColor = new Color(0.9607843f, 0.9254902f, 0.8235294f, 1f);
    static readonly Color CountColor = new Color(0.2901961f, 0.3058824f, 0.3176471f, 1f);
    static readonly Color NameColor = new Color(0.1019608f, 0.1019608f, 0.1137255f, 1f);
    static readonly Color BadgeFill = new Color(217f / 255f, 217f / 255f, 217f / 255f, 1f);
    static readonly Color MenuFill = new Color(78f / 255f, 88f / 255f, 71f / 255f, 1f);
    static readonly Color TabFill = new Color(96f / 255f, 123f / 255f, 77f / 255f, 1f);

    [MenuItem("Tools/Figma2Unity/Rebuild cricket-collection Nested Prefabs")]
    public static void Rebuild()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(ResourcesPrefabFolder);

        Canvas canvas = GetOrCreateCanvas();
        GameObject staging = CreateRect("FigmaImport_Staging", canvas.transform, Vector2.zero, new Vector2(8f, 8f));
        staging.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            GameObject badge1 = BuildQualityBadge(staging.transform);
            string badge1Path = SavePrefab(badge1, "FigmaImport_品级_240_876.prefab");
            Object.DestroyImmediate(badge1);

            GameObject badge3 = BuildTemperamentBadge(staging.transform);
            string badge3Path = SavePrefab(badge3, "FigmaImport_性格_240_901.prefab");
            Object.DestroyImmediate(badge3);

            GameObject card = BuildCricketCard(
                staging.transform,
                AssetDatabase.LoadAssetAtPath<GameObject>(badge1Path),
                AssetDatabase.LoadAssetAtPath<GameObject>(badge3Path));
            string cardPath = SavePrefab(card, "FigmaImport_CricketCard_240_874.prefab");
            SavePrefab(card, "FigmaImport_CricketCard_240_935.prefab");
            Object.DestroyImmediate(card);

            GameObject tab = BuildTab(staging.transform);
            string tabPath = SavePrefab(tab, "FigmaImport_tab-0_205_756.prefab");
            Object.DestroyImmediate(tab);

            GameObject back = BuildBackButton(staging.transform);
            string backPath = SavePrefab(back, "FigmaImport_返回_205_772.prefab");
            Object.DestroyImmediate(back);

            GameObject menu = BuildBottomMenu(
                staging.transform,
                AssetDatabase.LoadAssetAtPath<GameObject>(tabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(backPath));
            string menuPath = SavePrefab(menu, "FigmaImport_下方菜单_205_775.prefab");
            Object.DestroyImmediate(menu);

            GameObject page = BuildCollectionPage(
                canvas.transform,
                AssetDatabase.LoadAssetAtPath<GameObject>(cardPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(menuPath));
            string pagePath = SavePrefab(page, "FigmaImport_cricket-collection_10_6.prefab");
            PrefabUtility.SaveAsPrefabAsset(page, ResourcesPrefabFolder + "/FigmaImport_cricket-collection_10_6.prefab");

            Selection.activeGameObject = page;
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(pagePath));
            Debug.Log(
                "cricket-collection nested prefabs ready:\n" +
                badge1Path + "\n" +
                badge3Path + "\n" +
                cardPath + "\n" +
                tabPath + "\n" +
                backPath + "\n" +
                menuPath + "\n" +
                pagePath);
        }
        finally
        {
            if (staging != null)
            {
                Object.DestroyImmediate(staging);
            }
        }
    }

    static GameObject BuildQualityBadge(Transform parent)
    {
        GameObject root = CreateBoundObject("品级", parent, Badge1Bounds, Badge1Bounds, "240:876", "COMPONENT");
        GameObject group = CreateBoundObject("Group 9", root.transform, Badge1Bounds, new Rect(6137f, 344f, 98f, 55f), "240:1258", "GROUP");
        CreateColorImage("Rectangle 14", group.transform, Badge1Bounds, new Rect(6137f, 344f, 98f, 47f), BadgeFill, "240:875", "RECTANGLE");
        CreateText(
            "一品",
            group.transform,
            Badge1Bounds,
            new Rect(6147f, 344f, 72f, 55f),
            DouQuquCricketCatalog.QualityName(1),
            36f,
            Color.white,
            TextAlignmentOptions.TopLeft,
            "10:46");
        return root;
    }

    static GameObject BuildTemperamentBadge(Transform parent)
    {
        GameObject root = CreateBoundObject("性格", parent, Badge3Bounds, Badge3Bounds, "240:901", "COMPONENT");
        GameObject group = CreateBoundObject("Group 10", root.transform, Badge3Bounds, new Rect(6137f, 481f, 98f, 55f), "240:1310", "GROUP");
        CreateColorImage("Rectangle 14", group.transform, Badge3Bounds, new Rect(6137f, 481f, 98f, 47f), BadgeFill, "240:895", "RECTANGLE");
        CreateText(
            "性格",
            group.transform,
            Badge3Bounds,
            new Rect(6147f, 481f, 72f, 55f),
            DouQuquCricketCatalog.TemperamentName(1),
            36f,
            Color.white,
            TextAlignmentOptions.TopLeft,
            "240:896");
        return root;
    }

    static GameObject BuildCricketCard(Transform parent, GameObject badge1Prefab, GameObject badge3Prefab)
    {
        GameObject root = CreateBoundObject("CricketCard", parent, CardBounds, CardBounds, "240:874", "COMPONENT");
        GameObject group = CreateBoundObject("Group 8", root.transform, CardBounds, CardBounds, "240:1097", "GROUP");
        CreateSpriteImage(
            "Rectangle 15",
            group.transform,
            CardBounds,
            new Rect(6108f, 338f, 156f, 271f),
            CardSliceFolder + "/I240_935;240_885.png",
            "240:885",
            "RECTANGLE");
        CreateSpriteImage(
            "Rectangle",
            group.transform,
            CardBounds,
            new Rect(6116f, 368f, 140f, 140f),
            CardSliceFolder + "/I240_935;10_44.png",
            "10:44",
            "RECTANGLE");
        CreateText(
            "名字",
            group.transform,
            CardBounds,
            new Rect(6108f, 540f, 156f, 55f),
            "灼云展",
            36f,
            NameColor,
            TextAlignmentOptions.Top,
            "10:48");
        InstantiatePrefab(badge1Prefab, group.transform, CardBounds, Badge1Bounds, "品级", "I240:935;240:877", "INSTANCE");
        InstantiatePrefab(badge3Prefab, group.transform, CardBounds, Badge3Bounds, "性格", "I240:935;240:902", "INSTANCE");
        return root;
    }

    static GameObject BuildTab(Transform parent)
    {
        GameObject root = CreateBoundObject("tab-0", parent, TabBounds, TabBounds, "205:756", "COMPONENT");
        Image background = root.AddComponent<Image>();
        background.sprite = UiSprite();
        background.color = TabFill;
        background.raycastTarget = false;
        CreateText(
            "斗蛐蛐",
            root.transform,
            TabBounds,
            new Rect(6183f, 1833f, 144f, 58f),
            "斗蛐蛐",
            48f,
            TitleColor,
            TextAlignmentOptions.TopLeft,
            "22:2");
        return root;
    }

    static GameObject BuildBackButton(Transform parent)
    {
        GameObject root = CreateBoundObject("返回", parent, BackBounds, BackBounds, "205:772", "COMPONENT");
        Image rootImage = root.AddComponent<Image>();
        rootImage.sprite = LoadSprite(MenuSliceFolder + "/205_774.png");
        rootImage.raycastTarget = false;
        CreateBoundObject("Ellipse 1", root.transform, BackBounds, new Rect(5957f, 1781f, 150f, 150f), "17:2", "ELLIPSE");
        CreateBoundObject("Arrow 1", root.transform, BackBounds, new Rect(5993f, 1856f, 91f, 20f), "23:23", "VECTOR");
        return root;
    }

    static GameObject BuildBottomMenu(Transform parent, GameObject tabPrefab, GameObject backPrefab)
    {
        GameObject root = CreateBoundObject("下方菜单", parent, MenuBounds, MenuBounds, "205:775", "COMPONENT");
        Image background = root.AddComponent<Image>();
        background.sprite = UiSprite();
        background.color = MenuFill;
        background.raycastTarget = false;

        GameObject tab0 = InstantiatePrefab(tabPrefab, root.transform, MenuBounds, new Rect(6145f, 1781f, 219f, 150f), "tab-0", "205:773", "INSTANCE");
        SetChildText(tab0, "斗蛐蛐");
        GameObject tab1 = InstantiatePrefab(tabPrefab, root.transform, MenuBounds, new Rect(6423f, 1781f, 219f, 150f), "tab-0", "205:757", "INSTANCE");
        SetChildText(tab1, "育虫室");
        GameObject tab2 = InstantiatePrefab(tabPrefab, root.transform, MenuBounds, new Rect(6701f, 1781f, 219f, 150f), "tab-0", "205:760", "INSTANCE");
        SetChildText(tab2, "蛐蛐谱");
        InstantiatePrefab(backPrefab, root.transform, MenuBounds, BackBounds, "返回", "205:774", "INSTANCE");
        return root;
    }

    static GameObject BuildCollectionPage(Transform canvas, GameObject cardPrefab, GameObject menuPrefab)
    {
        Transform existing = canvas.Find("FigmaImport_cricket-collection_10_6");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject root = CreateBoundObject("FigmaImport_cricket-collection_10_6", canvas, PageBounds, PageBounds, "10:6", "FRAME");
        root.AddComponent<RectMask2D>();

        CreateSpriteImage(
            "WoodGrain",
            root.transform,
            PageBounds,
            PageBounds,
            PageSliceFolder + "/10_7.png",
            "10:7",
            "RECTANGLE");
        CreateText(
            "蛐蛐谱",
            root.transform,
            PageBounds,
            new Rect(6334f, 59f, 288f, 146f),
            "蛐蛐谱",
            96f,
            TitleColor,
            TextAlignmentOptions.Top,
            "10:19");
        CreateText(
            "收集总数_ 1 _ 5",
            root.transform,
            PageBounds,
            new Rect(6363f, 259f, 242f, 55f),
            "收集总数: 16 / 16",
            36f,
            CountColor,
            TextAlignmentOptions.TopLeft,
            "10:131");

        GameObject frame8 = CreateBoundObject("Frame 8", root.transform, PageBounds, Frame8Bounds, "240:1096", "FRAME");
        for (int i = 0; i < CardSlots.Length; i++)
        {
            int quality = i / 4 + 1;
            int temperament = i % 4 + 1;
            GameObject card = InstantiatePrefab(
                cardPrefab,
                frame8.transform,
                Frame8Bounds,
                CardSlots[i],
                "CricketCard",
                "240:874",
                "INSTANCE");
            ApplyCardVariant(card, quality, temperament);
        }

        InstantiatePrefab(menuPrefab, root.transform, PageBounds, MenuBounds, "下方菜单", "205:784", "INSTANCE");
        return root;
    }

    static GameObject InstantiatePrefab(
        GameObject prefab,
        Transform parent,
        Rect parentBounds,
        Rect nodeBounds,
        string name,
        string nodeId,
        string nodeType)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = name;
        ApplyRect(instance.GetComponent<RectTransform>(), parentBounds, nodeBounds);
        Bind(instance, nodeId, name, nodeType, string.Empty, instance.transform.GetSiblingIndex());
        return instance;
    }

    static GameObject CreateBoundObject(
        string name,
        Transform parent,
        Rect parentBounds,
        Rect nodeBounds,
        string nodeId,
        string nodeType)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        ApplyRect(go.GetComponent<RectTransform>(), parentBounds, nodeBounds);
        Bind(go, nodeId, name, nodeType, string.Empty, go.transform.GetSiblingIndex());
        return go;
    }

    static void CreateSpriteImage(
        string name,
        Transform parent,
        Rect parentBounds,
        Rect nodeBounds,
        string assetPath,
        string nodeId,
        string nodeType)
    {
        GameObject go = CreateBoundObject(name, parent, parentBounds, nodeBounds, nodeId, nodeType);
        Image image = go.AddComponent<Image>();
        image.sprite = LoadSprite(assetPath);
        image.raycastTarget = false;
        Bind(go, nodeId, name, nodeType, assetPath, go.transform.GetSiblingIndex());
    }

    static void CreateColorImage(
        string name,
        Transform parent,
        Rect parentBounds,
        Rect nodeBounds,
        Color color,
        string nodeId,
        string nodeType)
    {
        GameObject go = CreateBoundObject(name, parent, parentBounds, nodeBounds, nodeId, nodeType);
        Image image = go.AddComponent<Image>();
        image.sprite = UiSprite();
        image.color = color;
        image.raycastTarget = false;
        Bind(go, nodeId, name, nodeType, string.Empty, go.transform.GetSiblingIndex());
    }

    static void CreateText(
        string name,
        Transform parent,
        Rect parentBounds,
        Rect nodeBounds,
        string characters,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        string nodeId)
    {
        GameObject go = CreateBoundObject(name, parent, parentBounds, nodeBounds, nodeId, "TEXT");
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        FigmaFontMappingAsset mapping = AssetDatabase.LoadAssetAtPath<FigmaFontMappingAsset>(
            "Assets/FigmaImports/FigmaFontMapping.asset");
        if (mapping != null && mapping.DefaultFont != null)
        {
            text.font = mapping.DefaultFont;
        }

        text.text = characters;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.richText = false;
        text.margin = Vector4.zero;
    }

    static void SetChildText(GameObject instance, string value)
    {
        TextMeshProUGUI text = instance.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text == null)
        {
            return;
        }

        text.text = value;
        PrefabUtility.RecordPrefabInstancePropertyModifications(text);
    }

    static void ApplyCardVariant(GameObject card, int quality, int temperament)
    {
        ApplyBadgeVariant(card.transform, "品级", DouQuquCricketCatalog.QualityName(quality), DouQuquCricketCatalog.QualityColors[quality]);
        ApplyBadgeVariant(card.transform, "性格", DouQuquCricketCatalog.TemperamentName(temperament), DouQuquCricketCatalog.TemperamentColors[temperament]);
    }

    static void ApplyBadgeVariant(Transform root, string badgeName, string label, Color color)
    {
        Transform badge = FindNamed(root, badgeName);
        if (badge == null)
        {
            return;
        }

        TextMeshProUGUI text = badge.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            text.text = label;
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
        }

        Transform fill = FindNamed(badge, "Rectangle 14");
        if (fill != null)
        {
            Image image = fill.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
                PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            }
        }
    }

    static Transform FindNamed(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindNamed(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    static void ApplyRect(RectTransform rect, Rect parentBounds, Rect nodeBounds)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = ToUnityAnchoredPosition(parentBounds, nodeBounds);
        rect.sizeDelta = new Vector2(nodeBounds.width, nodeBounds.height);
    }

    static Vector2 ToUnityAnchoredPosition(Rect rootBounds, Rect nodeBounds)
    {
        float localLeft = nodeBounds.x - rootBounds.x;
        float localTop = nodeBounds.y - rootBounds.y;
        float unityX = localLeft + nodeBounds.width * 0.5f - rootBounds.width * 0.5f;
        float unityY = rootBounds.height * 0.5f - localTop - nodeBounds.height * 0.5f;
        return new Vector2(unityX, unityY);
    }

    static void Bind(GameObject go, string nodeId, string nodeName, string nodeType, string assetPath, int siblingIndex)
    {
        FigmaLayerBinding binding = go.GetComponent<FigmaLayerBinding>();
        if (binding == null)
        {
            binding = go.AddComponent<FigmaLayerBinding>();
        }

        binding.SetLayerInfo(nodeId, nodeName, nodeType, assetPath, siblingIndex);
    }

    static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            throw new System.InvalidOperationException("Missing sprite: " + path);
        }

        return sprite;
    }

    static Sprite UiSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    static string SavePrefab(GameObject source, string fileName)
    {
        string path = PrefabFolder + "/" + fileName;
        GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(source, path, InteractionMode.AutomatedAction);
        if (saved == null)
        {
            throw new System.InvalidOperationException("Failed to save prefab: " + path);
        }

        return path;
    }

    static Canvas GetOrCreateCanvas()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            return canvas;
        }

        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        return canvas;
    }

    static GameObject CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return go;
    }

    static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string[] parts = assetFolder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
