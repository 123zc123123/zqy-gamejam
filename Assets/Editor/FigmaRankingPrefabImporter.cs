using FigmaUiImporter;
using FigmaUiImporter.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class FigmaRankingPrefabImporter
{
    const string PrefabFolder = "Assets/Resources/Ranking/Prefabs/Parts";
    const string ResourcesPrefabFolder = "Assets/Resources/Ranking/Prefabs";
    const string AvatarPath = "Assets/Resources/Settlement/Textures/Figma/Avatar.png";
    const string CrownPath = "Assets/Resources/Settlement/Textures/Figma/Crown.svg";
    const string AwardPath = "Assets/Resources/Settlement/Textures/Figma/Award.svg";
    const string TrophyPath = "Assets/Resources/Settlement/Textures/Figma/Trophy.svg";
    const string HashPath = "Assets/Resources/Settlement/Textures/Figma/Hash.svg";

    static readonly Rect PageBounds = new Rect(13168f, 36f, 1080f, 1920f);
    static readonly Rect BannerBounds = new Rect(13168f, 36f, 1080f, 157f);
    static readonly Rect ListBounds = new Rect(13237f, 369f, 881f, 1279f);
    static readonly Rect NavBounds = new Rect(13168f, 1756f, 1080f, 200f);
    static readonly Rect RowBounds = new Rect(13363f, 421f, 665f, 254f);
    static readonly Rect BadgeBounds = new Rect(13819f, 511f, 185f, 74f);

    static readonly Rect[] RowSlots =
    {
        new Rect(13363f, 421f, 665f, 254f),
        new Rect(13372f, 713f, 658f, 254f),
        new Rect(13370f, 996f, 660f, 254f),
        new Rect(13374f, 1279f, 656f, 254f)
    };

    static readonly string[] PlayerNames = { "玩家1", "玩家2", "玩家3", "玩家4" };
    static readonly string[] PlayerScores = { "+1200", "+980", "+750", "+320" };
    static readonly string[] RankLabels = { "第1名", "第2名", "第3名", "第4名" };
    static readonly string[] RankIconPaths = { CrownPath, AwardPath, TrophyPath, HashPath };
    static readonly Color[] RankColors =
    {
        new Color(0.9608f, 0.6196f, 0.0431f, 1f),
        new Color(0.6941f, 0.7098f, 0.7451f, 1f),
        new Color(0.7725f, 0.6275f, 0.3490f, 1f),
        new Color(0.4196f, 0.4471f, 0.5020f, 1f)
    };

    static readonly Color PageFill = new Color(0.0392f, 0.0431f, 0.0627f, 1f);
    static readonly Color MenuFill = new Color(0.3059f, 0.3451f, 0.2784f, 1f);
    static readonly Color TabFill = new Color(0.3761f, 0.4808f, 0.3028f, 1f);
    static readonly Color TitleColor = new Color(1f, 0.95f, 0.80f, 1f);
    static readonly Color Cream = new Color(0.9608f, 0.9255f, 0.8235f, 1f);
    static readonly Color GoldLine = new Color(0.7725f, 0.6275f, 0.3490f, 1f);

    [MenuItem("Tools/Figma2Unity/Rebuild Ranking Nested Prefabs")]
    public static void Rebuild()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(ResourcesPrefabFolder);

        Canvas canvas = GetOrCreateCanvas();
        GameObject staging = CreateRect("RankingStaging", canvas.transform, Vector2.zero, new Vector2(8f, 8f));
        staging.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            GameObject badge = BuildRankBadge(staging.transform);
            string badgePath = SavePrefab(badge, "RankBadge.prefab");
            Object.DestroyImmediate(badge);

            GameObject row = BuildPlayerRow(
                staging.transform,
                AssetDatabase.LoadAssetAtPath<GameObject>(badgePath));
            string rowPath = SavePrefab(row, "PlayerRow.prefab");
            Object.DestroyImmediate(row);

            GameObject nav = BuildBottomNav(staging.transform);
            string navPath = SavePrefab(nav, "下方菜单.prefab");
            Object.DestroyImmediate(nav);

            GameObject page = BuildRankingPage(
                canvas.transform,
                AssetDatabase.LoadAssetAtPath<GameObject>(rowPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(navPath));
            string pagePath = ResourcesPrefabFolder + "/Ranking.prefab";
            PrefabUtility.SaveAsPrefabAsset(page, pagePath);

            Selection.activeGameObject = page;
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(pagePath));
            Debug.Log("Ranking nested prefabs ready:\n" + badgePath + "\n" + rowPath + "\n" + navPath + "\n" + pagePath);
        }
        finally
        {
            if (staging != null) Object.DestroyImmediate(staging);
        }
    }

    static GameObject BuildRankBadge(Transform parent)
    {
        GameObject root = CreateColorImage("RankBadge", parent, BadgeBounds, BadgeBounds, Color.white, "165:10", "COMPONENT");
        AddOutline(root, RankColors[0], 2f);
        CreateSpriteImage("RankIcon", root.transform, BadgeBounds, new Rect(13829f, 518f, 60f, 60f), CrownPath, "145:45", "FRAME");
        CreateText("RankLabel", root.transform, BadgeBounds, new Rect(13889f, 524f, 100f, 48f), "第1名", 40f, RankColors[0], TextAlignmentOptions.Left, "145:16");
        return root;
    }

    static GameObject BuildPlayerRow(Transform parent, GameObject badgePrefab)
    {
        GameObject root = CreateColorImage("PlayerRow", parent, RowBounds, RowBounds, Color.white, "165:25", "COMPONENT");
        AddOutline(root, Color.white, 1f);

        GameObject left = CreateBoundObject("left", root.transform, RowBounds, new Rect(13387f, 462f, 199f, 166f), "151:249", "FRAME");
        GameObject avatar = CreateCircle("avatar", root.transform, RowBounds, new Rect(13387f, 429f, 180f, 180f), Color.white, "165:24");
        Mask mask = avatar.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        CreateSpriteImage("avatar-image", avatar.transform, new Rect(13387f, 429f, 180f, 180f), new Rect(13387f, 429f, 180f, 180f), AvatarPath, "163:2", "ELLIPSE");
        CreateText("玩家1", left.transform, new Rect(13387f, 462f, 199f, 166f), new Rect(13428f, 622f, 158f, 46f), "玩家1", 40f, new Color(0.12f, 0.10f, 0.08f, 1f), TextAlignmentOptions.Center, "151:251");

        CreateText("+1200", root.transform, RowBounds, new Rect(13680f, 536f, 115f, 44f), "+1200", 28f, new Color(0.18f, 0.16f, 0.14f, 1f), TextAlignmentOptions.Left, "151:253");

        GameObject right = CreateBoundObject("right", root.transform, RowBounds, new Rect(13819f, 511f, 185f, 74f), "145:12", "FRAME");
        InstantiatePrefab(badgePrefab, right.transform, new Rect(13819f, 511f, 185f, 74f), BadgeBounds, "rank-badge", "165:23", "INSTANCE");
        return root;
    }

    static GameObject BuildBottomNav(Transform parent)
    {
        GameObject root = CreateColorImage("下方菜单", parent, NavBounds, NavBounds, MenuFill, "205:775", "COMPONENT");
        CreateTab(root.transform, new Rect(13380f, 1781f, 219f, 150f), "斗蛐蛐", "I220:1048;205:773");
        CreateTab(root.transform, new Rect(13658f, 1781f, 219f, 150f), "育虫室", "I220:1048;205:757");
        CreateTab(root.transform, new Rect(13936f, 1781f, 219f, 150f), "蛐蛐谱", "I220:1048;205:760");

        GameObject back = CreateBoundObject("返回", root.transform, NavBounds, new Rect(13184f, 1776f, 180f, 172f), "I220:1048;205:774", "INSTANCE");
        CreateCircle("Ellipse 1", back.transform, new Rect(13184f, 1776f, 180f, 172f), new Rect(13192f, 1781f, 150f, 150f), MenuFill, "17:2");
        AddOutline(FindNamed(back.transform, "Ellipse 1").gameObject, GoldLine, 3f);
        CreateText("←", back.transform, new Rect(13184f, 1776f, 180f, 172f), new Rect(13228f, 1818f, 91f, 58f), "←", 48f, Cream, TextAlignmentOptions.Center, "23:23");
        return root;
    }

    static void CreateTab(Transform parent, Rect bounds, string label, string nodeId)
    {
        GameObject tab = CreateColorImage("tab-0", parent, NavBounds, bounds, TabFill, nodeId, "INSTANCE");
        AddOutline(tab, GoldLine, 3f);
        CreateText(label, tab.transform, bounds, new Rect(bounds.x + 38f, bounds.y + 52f, 144f, 58f), label, 48f, Cream, TextAlignmentOptions.Center, nodeId + ";22:2");
    }

    static GameObject BuildRankingPage(Transform canvas, GameObject rowPrefab, GameObject navPrefab)
    {
        Transform existing = canvas.Find("Ranking");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        GameObject root = CreateBoundObject("Ranking", canvas, PageBounds, PageBounds, "220:994", "FRAME");
        root.AddComponent<RectMask2D>();
        CreateColorImage("page-fill", root.transform, PageBounds, PageBounds, PageFill, "220:994-fill", "FRAME");
        CreateColorImage("black-background", root.transform, PageBounds, PageBounds, Color.black, "220:995", "RECTANGLE");

        GameObject banner = CreateColorImage("title-banner", root.transform, PageBounds, BannerBounds, MenuFill, "220:1107", "FRAME");
        CreateText("排行榜", banner.transform, BannerBounds, new Rect(13612f, 76f, 192f, 77f), "排行榜", 64f, TitleColor, TextAlignmentOptions.Center, "220:1108");

        GameObject list = CreateBoundObject("player-list", root.transform, PageBounds, ListBounds, "220:1059", "FRAME");
        for (int i = 0; i < RowSlots.Length; i++)
        {
            GameObject row = InstantiatePrefab(rowPrefab, list.transform, ListBounds, RowSlots[i], "player-row", "220:106" + i, "INSTANCE");
            ApplyRowVariant(row, i);
        }

        InstantiatePrefab(navPrefab, root.transform, PageBounds, NavBounds, "下方菜单", "220:1048", "INSTANCE");
        return root;
    }

    static void ApplyRowVariant(GameObject row, int index)
    {
        SetChildText(row, "玩家1", PlayerNames[index]);
        SetChildText(row, "+1200", PlayerScores[index]);

        Transform badge = FindNamed(row.transform, "rank-badge");
        if (badge == null) return;

        SetChildText(badge.gameObject, "RankLabel", RankLabels[index]);
        Image badgeImage = badge.GetComponent<Image>();
        if (badgeImage != null)
        {
            badgeImage.color = Color.white;
            PrefabUtility.RecordPrefabInstancePropertyModifications(badgeImage);
        }

        Outline outline = badge.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = RankColors[index];
            PrefabUtility.RecordPrefabInstancePropertyModifications(outline);
        }

        Transform icon = FindNamed(badge, "RankIcon");
        if (icon != null)
        {
            Image iconImage = icon.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.sprite = LoadSprite(RankIconPaths[index]);
                iconImage.color = RankColors[index];
                PrefabUtility.RecordPrefabInstancePropertyModifications(iconImage);
            }
        }

        Transform label = FindNamed(badge, "RankLabel");
        if (label != null)
        {
            TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                text.color = RankColors[index];
                PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            }
        }
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

    static GameObject CreateBoundObject(string name, Transform parent, Rect parentBounds, Rect nodeBounds, string nodeId, string nodeType)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        ApplyRect(go.GetComponent<RectTransform>(), parentBounds, nodeBounds);
        Bind(go, nodeId, name, nodeType, string.Empty, go.transform.GetSiblingIndex());
        return go;
    }

    static GameObject CreateSpriteImage(string name, Transform parent, Rect parentBounds, Rect nodeBounds, string assetPath, string nodeId, string nodeType)
    {
        GameObject go = CreateBoundObject(name, parent, parentBounds, nodeBounds, nodeId, nodeType);
        Image image = go.AddComponent<Image>();
        image.sprite = LoadSprite(assetPath);
        image.raycastTarget = false;
        image.preserveAspect = true;
        Bind(go, nodeId, name, nodeType, assetPath, go.transform.GetSiblingIndex());
        return go;
    }

    static GameObject CreateColorImage(string name, Transform parent, Rect parentBounds, Rect nodeBounds, Color color, string nodeId, string nodeType)
    {
        GameObject go = CreateBoundObject(name, parent, parentBounds, nodeBounds, nodeId, nodeType);
        Image image = go.AddComponent<Image>();
        image.sprite = UiSprite();
        image.color = color;
        image.raycastTarget = false;
        Bind(go, nodeId, name, nodeType, string.Empty, go.transform.GetSiblingIndex());
        return go;
    }

    static GameObject CreateCircle(string name, Transform parent, Rect parentBounds, Rect nodeBounds, Color color, string nodeId)
    {
        GameObject go = CreateBoundObject(name, parent, parentBounds, nodeBounds, nodeId, "ELLIPSE");
        Image image = go.AddComponent<Image>();
        image.sprite = KnobSprite();
        image.color = color;
        image.raycastTarget = false;
        return go;
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
        FigmaFontMappingAsset mapping = AssetDatabase.LoadAssetAtPath<FigmaFontMappingAsset>("Assets/Editor/FigmaFontMapping.asset");
        if (mapping != null && mapping.DefaultFont != null) text.font = mapping.DefaultFont;
        text.text = characters;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.richText = false;
        text.margin = Vector4.zero;
        if (fontSize >= 48f) text.fontStyle = FontStyles.Bold;
    }

    static void SetChildText(GameObject instance, string childName, string value)
    {
        Transform named = FindNamed(instance.transform, childName);
        TextMeshProUGUI text = named != null
            ? named.GetComponent<TextMeshProUGUI>()
            : instance.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text == null) return;
        text.text = value;
        PrefabUtility.RecordPrefabInstancePropertyModifications(text);
    }

    static Transform FindNamed(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindNamed(root.GetChild(i), name);
            if (found != null) return found;
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
        if (binding == null) binding = go.AddComponent<FigmaLayerBinding>();
        binding.SetLayerInfo(nodeId, nodeName, nodeType, assetPath, siblingIndex);
    }

    static void AddOutline(GameObject go, Color color, float size)
    {
        Outline outline = go.GetComponent<Outline>();
        if (outline == null) outline = go.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(size, -size);
        outline.useGraphicAlpha = true;
    }

    static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new System.InvalidOperationException("Missing sprite: " + path);
        return sprite;
    }

    static Sprite UiSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    static Sprite KnobSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
    }

    static string SavePrefab(GameObject source, string fileName)
    {
        string path = PrefabFolder + "/" + fileName;
        GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(source, path, InteractionMode.AutomatedAction);
        if (saved == null) throw new System.InvalidOperationException("Failed to save prefab: " + path);
        return path;
    }

    static Canvas GetOrCreateCanvas()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas != null) return canvas;
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
        if (AssetDatabase.IsValidFolder(assetFolder)) return;
        string[] parts = assetFolder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
