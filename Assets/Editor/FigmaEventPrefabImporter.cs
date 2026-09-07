using System.IO;
using DouQuqu;
using FigmaUiImporter;
using FigmaUiImporter.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class FigmaEventPrefabImporter
{
    const string PrefabFolder = "Assets/FigmaImports/Prefabs";
    const string ResourcesPrefabFolder = "Assets/Resources/BattleEntrance/Prefabs";
    const string DerivedFolder = "Assets/FigmaImports/5jALrfLrmLV0NIOxaQURCu/derived";
    const string EventSliceFolder = "Assets/FigmaImports/5jALrfLrmLV0NIOxaQURCu/10_593/layers";
    const string MenuPrefabPath = PrefabFolder + "/FigmaImport_下方菜单_205_775.prefab";

    static readonly Rect EventBounds = new Rect(2059f, 0f, 1080f, 1920f);
    static readonly Rect RenwuBounds = new Rect(10875f, 36f, 1080f, 1920f);
    static readonly Rect ProgressBounds = new Rect(2273f, 366f, 651f, 110f);
    static readonly Rect PlayerCardBounds = new Rect(2167f, 822f, 218f, 239f);
    static readonly Rect BadgeBounds = new Rect(2285f, 366f, 100f, 100f);
    static readonly Rect IconSlotBounds = new Rect(11291f, 622f, 100f, 100f);
    static readonly Rect ClaimBounds = new Rect(11656f, 640f, 131f, 70f);
    static readonly Rect RewardTierBounds = new Rect(10965f, 548f, 853f, 216f);
    static readonly Rect MatchCtaBounds = new Rect(2729f, 1380f, 340f, 273f);
    static readonly Rect TeamCtaBounds = new Rect(2200f, 1380f, 340f, 273f);
    static readonly Rect SideButtonBounds = new Rect(2937f, 1175f, 204f, 204f);
    static readonly Rect MenuBounds = new Rect(2059f, 1720f, 1080f, 200f);

    static readonly Rect[] PlayerCardSlots =
    {
        new Rect(2167f, 822f, 218f, 239f),
        new Rect(2395f, 822f, 218f, 239f),
        new Rect(2623f, 822f, 218f, 239f),
        new Rect(2851f, 822f, 218f, 239f)
    };

    static readonly string[] PlayerNames = { "玩家一", "玩家二", "玩家三", "玩家四" };

    static readonly Rect[] RewardTierSlots =
    {
        new Rect(10965f, 548f, 853f, 216f),
        new Rect(10965f, 814f, 853f, 216f),
        new Rect(10965f, 1080f, 853f, 216f),
        new Rect(10965f, 1346f, 853f, 216f),
        new Rect(10965f, 1612f, 853f, 216f)
    };

    static readonly Color TitleColor = new Color(0.9608f, 0.9255f, 0.8235f, 1f);
    static readonly Color Gold = new Color(0.7725f, 0.6275f, 0.3490f, 1f);
    static readonly Color MenuFill = new Color(0.3059f, 0.3451f, 0.2784f, 1f);
    static readonly Color MatchFill = new Color(0.5192f, 0.0899f, 0.0936f, 1f);
    static readonly Color PanelGray = new Color(0.8510f, 0.8510f, 0.8510f, 1f);
    static readonly Color RewardPanel = new Color(0.1490f, 0.1216f, 0.2196f, 1f);
    static readonly Color RewardTitle = new Color(1f, 0.92f, 0.70f, 1f);
    static readonly Color RewardProgress = new Color(0.85f, 0.75f, 0.55f, 1f);
    static readonly Color ClaimFill = new Color(0.6196f, 0.1647f, 0.1686f, 1f);
    static readonly Color TrackFill = new Color(0.1804f, 0.1098f, 0.0863f, 1f);
    static readonly Color AvatarBorder = new Color(0.2902f, 0.3294f, 0.2902f, 1f);
    static readonly Color[] IconSlotFills =
    {
        new Color(0.85f, 0.65f, 0.20f, 1f),
        new Color(0.45f, 0.28f, 0.58f, 1f),
        new Color(0.25f, 0.48f, 0.72f, 1f)
    };

    [MenuItem("Tools/Figma2Unity/Rebuild event-face-painting Nested Prefabs")]
    public static void Rebuild()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(DerivedFolder);
        EnsureFolder(ResourcesPrefabFolder);

        CropSprite(EventSliceFolder + "/238_647.png", new RectInt(0, 39, 200, 200), DerivedFolder + "/player-avatar.png");
        CropSprite(EventSliceFolder + "/238_529.png", new RectInt(543, 6, 102, 83), DerivedFolder + "/quest-chest.png");

        Canvas canvas = GetOrCreateCanvas();
        GameObject staging = CreateRect("FigmaImport_EventStaging", canvas.transform, Vector2.zero, new Vector2(8f, 8f));
        staging.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            GameObject badge = BuildLevelBadge(staging.transform);
            string badgePath = SavePrefab(badge, "FigmaImport_level-badge_241_2151.prefab");
            Object.DestroyImmediate(badge);

            GameObject iconSlot = BuildIconSlot(staging.transform);
            string iconSlotPath = SavePrefab(iconSlot, "FigmaImport_icon-slot_228_738.prefab");
            Object.DestroyImmediate(iconSlot);

            GameObject claim = BuildClaimButton(staging.transform);
            string claimPath = SavePrefab(claim, "FigmaImport_claim-button_241_2090.prefab");
            Object.DestroyImmediate(claim);

            GameObject progress = BuildProgressCard(
                staging.transform,
                AssetDatabase.LoadAssetAtPath<GameObject>(badgePath));
            string progressPath = SavePrefab(progress, "FigmaImport_progress-card_238_528.prefab");
            Object.DestroyImmediate(progress);

            GameObject playerCard = BuildPlayerCard(staging.transform);
            string playerCardPath = SavePrefab(playerCard, "FigmaImport_player-card_169_54.prefab");
            Object.DestroyImmediate(playerCard);

            GameObject matchCta = BuildMatchCta(staging.transform);
            string matchCtaPath = SavePrefab(matchCta, "FigmaImport_start-game-cta_161_341.prefab");
            Object.DestroyImmediate(matchCta);

            GameObject teamCta = BuildTeamCta(staging.transform);
            string teamCtaPath = SavePrefab(teamCta, "FigmaImport_start-game-cta_238_726.prefab");
            Object.DestroyImmediate(teamCta);

            GameObject sideButton = BuildSideButton(staging.transform);
            string sideButtonPath = SavePrefab(sideButton, "FigmaImport_SideButton_185_164.prefab");
            Object.DestroyImmediate(sideButton);

            GameObject rewardTier = BuildRewardTier(
                staging.transform,
                AssetDatabase.LoadAssetAtPath<GameObject>(badgePath),
                AssetDatabase.LoadAssetAtPath<GameObject>(iconSlotPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(claimPath));
            string rewardTierPath = SavePrefab(rewardTier, "FigmaImport_reward-tier_217_497.prefab");
            Object.DestroyImmediate(rewardTier);

            GameObject renwu = BuildRenwuPage(
                canvas.transform,
                AssetDatabase.LoadAssetAtPath<GameObject>(progressPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(rewardTierPath));
            string renwuPath = SavePrefab(renwu, "FigmaImport_renwu_217_333.prefab");
            Object.DestroyImmediate(renwu);

            GameObject page = BuildEventPage(
                canvas.transform,
                AssetDatabase.LoadAssetAtPath<GameObject>(progressPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(playerCardPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(matchCtaPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(teamCtaPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(sideButtonPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(MenuPrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(renwuPath));
            string pagePath = SavePrefab(page, "FigmaImport_event-face-painting_10_593.prefab");
            PrefabUtility.SaveAsPrefabAsset(page, ResourcesPrefabFolder + "/BattleEntrance.prefab");

            Selection.activeGameObject = page;
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(pagePath));
            Debug.Log("event-face-painting nested prefabs ready:\n" + pagePath + "\n" + renwuPath);
        }
        finally
        {
            if (staging != null) Object.DestroyImmediate(staging);
        }
    }

    static GameObject BuildLevelBadge(Transform parent)
    {
        GameObject root = CreateBoundObject("level-badge", parent, BadgeBounds, BadgeBounds, "241:2151", "COMPONENT");
        CreateCircle("Ellipse 6", root.transform, BadgeBounds, BadgeBounds, PanelGray, "238:522");
        CreateText("1", root.transform, BadgeBounds, new Rect(2314f, 379f, 43f, 63f), "1", 60f, Color.black, TextAlignmentOptions.Center, "238:523");
        return root;
    }

    static GameObject BuildIconSlot(Transform parent)
    {
        GameObject root = CreateBoundObject("icon-slot-1", parent, IconSlotBounds, IconSlotBounds, "228:738", "COMPONENT");
        Image background = root.AddComponent<Image>();
        background.sprite = UiSprite();
        background.color = IconSlotFills[0];
        background.raycastTarget = false;
        AddOutline(root, Gold, 2f);
        CreateColorImage("icon", root.transform, IconSlotBounds, new Rect(11322f, 637.5f, 38f, 38f), new Color(1f, 0.92f, 0.65f, 0.85f), "228:736", "RECTANGLE");
        CreateText("x1", root.transform, IconSlotBounds, new Rect(11328f, 677.5f, 26f, 29f), "x1", 24f, Color.white, TextAlignmentOptions.Center, "228:737");
        return root;
    }

    static GameObject BuildClaimButton(Transform parent)
    {
        GameObject root = CreateBoundObject("领取", parent, ClaimBounds, ClaimBounds, "241:2090", "COMPONENT");
        CreateColorImage("Rectangle 18", root.transform, ClaimBounds, ClaimBounds, ClaimFill, "241:2065", "RECTANGLE");
        CreateText("领取", root.transform, ClaimBounds, new Rect(11682f, 651f, 80f, 48f), "领取", 40f, TitleColor, TextAlignmentOptions.Center, "228:764");
        EnableRaycast(root);
        return root;
    }

    static GameObject BuildProgressCard(Transform parent, GameObject badgePrefab)
    {
        GameObject root = CreateBoundObject("progress-card", parent, ProgressBounds, ProgressBounds, "238:528", "COMPONENT");
        Image background = root.AddComponent<Image>();
        background.sprite = UiSprite();
        background.color = new Color(0f, 0f, 0f, 0.5f);
        background.raycastTarget = true;
        CreateText("蛐蛐新手", root.transform, ProgressBounds, new Rect(2527f, 377f, 128f, 49f), "蛐蛐新手", 32f, TitleColor, TextAlignmentOptions.Center, "10:613");
        GameObject track = CreateColorImage("slider-track", root.transform, ProgressBounds, new Rect(2401f, 426f, 402f, 40f), TrackFill, "10:616", "FRAME");
        CreateColorImage("Rectangle", track.transform, new Rect(2401f, 426f, 402f, 40f), new Rect(2401f, 426f, 248f, 40f), Gold, "10:617", "RECTANGLE");
        CreateText("0 / 175", track.transform, new Rect(2401f, 426f, 402f, 40f), new Rect(2575f, 431f, 79f, 29f), "0 / 175", 24f, TitleColor, TextAlignmentOptions.Center, "10:615");
        CreateSpriteImage("chest", root.transform, ProgressBounds, new Rect(2816f, 387f, 102f, 83f), DerivedFolder + "/quest-chest.png", "238:527", "RECTANGLE");
        CreateText("?", root.transform, ProgressBounds, new Rect(2894f, 371f, 30f, 30f), "?", 20f, Gold, TextAlignmentOptions.Center, "10:697");
        InstantiatePrefab(badgePrefab, root.transform, ProgressBounds, BadgeBounds, "level-badge", "I238:529;241:2152", "INSTANCE");
        return root;
    }

    static GameObject BuildPlayerCard(Transform parent)
    {
        GameObject root = CreateBoundObject("player-card", parent, PlayerCardBounds, PlayerCardBounds, "169:54", "COMPONENT");
        Rect groupBounds = new Rect(2176f, 833f, 200f, 200f);
        GameObject group = CreateBoundObject("Group 2", root.transform, PlayerCardBounds, groupBounds, "238:656", "GROUP");
        GameObject border = CreateCircle("avatar-border", group.transform, groupBounds, groupBounds, AvatarBorder, "169:56");
        AddOutline(border, Gold, 2f);
        Mask mask = border.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        CreateSpriteImage("avatar-image", border.transform, groupBounds, new Rect(2192.67f, 849.67f, 166.67f, 166.67f), DerivedFolder + "/player-avatar.png", "169:57", "ELLIPSE");
        CreateText("player-name", root.transform, PlayerCardBounds, new Rect(2222f, 995f, 108f, 55f), "玩家一", 36f, Color.white, TextAlignmentOptions.Center, "169:60");
        return root;
    }

    static GameObject BuildMatchCta(Transform parent)
    {
        GameObject root = CreateBoundObject("StartMatchButton", parent, MatchCtaBounds, MatchCtaBounds, "161:341", "COMPONENT");
        Image background = root.AddComponent<Image>();
        background.sprite = UiSprite();
        background.color = MatchFill;
        background.raycastTarget = true;
        AddOutline(root, Gold, 3f);
        CreateText("随机匹配", root.transform, MatchCtaBounds, new Rect(2768f, 1480f, 262f, 73f), "随机匹配", 48f, TitleColor, TextAlignmentOptions.Center, "10:654");
        return root;
    }

    static GameObject BuildTeamCta(Transform parent)
    {
        GameObject root = CreateBoundObject("好友组队", parent, TeamCtaBounds, TeamCtaBounds, "238:726", "COMPONENT");
        Image background = root.AddComponent<Image>();
        background.sprite = UiSprite();
        background.color = MenuFill;
        background.raycastTarget = true;
        AddOutline(root, Gold, 3f);
        CreateText("好友组队", root.transform, TeamCtaBounds, new Rect(2274f, 1427f, 192f, 73f), "好友组队", 48f, TitleColor, TextAlignmentOptions.Center, "238:722");
        GameObject room = CreateColorImage("Rectangle 8", root.transform, TeamCtaBounds, new Rect(2234f, 1500f, 163f, 104f), TitleColor, "238:724", "RECTANGLE");
        CreateText("房间号：", room.transform, new Rect(2234f, 1500f, 163f, 104f), new Rect(2238f, 1517f, 154f, 73f), "房间号：", 36f, MenuFill, TextAlignmentOptions.Center, "238:725");
        GameObject confirm = CreateBoundObject("确认", root.transform, TeamCtaBounds, new Rect(2410f, 1508f, 110f, 88f), "238:727", "FRAME");
        CreateColorImage("确认底", confirm.transform, new Rect(2410f, 1508f, 110f, 88f), new Rect(2410f, 1508f, 110f, 88f), MatchFill, "238:728", "RECTANGLE");
        CreateText("确认", confirm.transform, new Rect(2410f, 1508f, 110f, 88f), new Rect(2422f, 1528f, 86f, 48f), "确认", 36f, TitleColor, TextAlignmentOptions.Center, "238:729");
        EnableRaycast(confirm);
        return root;
    }

    static GameObject BuildSideButton(Transform parent)
    {
        GameObject root = CreateBoundObject("SideButton_活动介绍", parent, SideButtonBounds, SideButtonBounds, "185:164", "COMPONENT");
        GameObject frame = CreateColorImage("Frame", root.transform, SideButtonBounds, new Rect(2977f, 1215f, 120f, 120f), MenuFill, "185:165", "FRAME");
        AddOutline(frame, Gold, 5f);
        CreateText("玩法规则", frame.transform, new Rect(2977f, 1215f, 120f, 120f), new Rect(2989f, 1263f, 96f, 37f), "玩法规则", 24f, TitleColor, TextAlignmentOptions.Center, "185:166");
        EnableRaycast(root);
        return root;
    }

    static GameObject BuildRewardTier(Transform parent, GameObject badgePrefab, GameObject iconSlotPrefab, GameObject claimPrefab)
    {
        GameObject root = CreateBoundObject("reward-tier", parent, RewardTierBounds, RewardTierBounds, "217:497", "COMPONENT");
        Rect panelBounds = new Rect(11265f, 542f, 544f, 222f);
        GameObject panel = CreateBoundObject("Group 15", root.transform, RewardTierBounds, panelBounds, "241:2394", "GROUP");
        GameObject panelFill = CreateColorImage("Rectangle 17", panel.transform, panelBounds, panelBounds, RewardPanel, "241:1558", "RECTANGLE");
        AddOutline(panelFill, new Color(0.898f, 0.7686f, 0.5608f, 1f), 5f);
        CreateText("reward-title", panel.transform, panelBounds, new Rect(11291f, 571f, 144f, 44f), "对战一局", 36f, RewardTitle, TextAlignmentOptions.Left, "228:744");
        CreateText("progress-text", panel.transform, panelBounds, new Rect(11480f, 576f, 122f, 34f), "(0/3000)", 28f, RewardProgress, TextAlignmentOptions.Left, "228:745");

        GameObject icons = CreateBoundObject("reward-icons", panel.transform, panelBounds, new Rect(11291f, 622f, 320f, 100f), "228:1116", "FRAME");
        Rect[] iconSlots =
        {
            new Rect(11291f, 622f, 100f, 100f),
            new Rect(11401f, 622f, 100f, 100f),
            new Rect(11511f, 622f, 100f, 100f)
        };
        for (int i = 0; i < iconSlots.Length; i++)
        {
            GameObject slot = InstantiatePrefab(iconSlotPrefab, icons.transform, new Rect(11291f, 622f, 320f, 100f), iconSlots[i], "icon-slot-1", "228:1117", "INSTANCE");
            Image fill = slot.GetComponent<Image>();
            if (fill != null)
            {
                fill.color = IconSlotFills[i];
                PrefabUtility.RecordPrefabInstancePropertyModifications(fill);
            }
        }

        InstantiatePrefab(claimPrefab, panel.transform, panelBounds, ClaimBounds, "领取", "I228:1394;241:2091", "INSTANCE");

        GameObject labelGroup = CreateBoundObject("Group 14", root.transform, RewardTierBounds, new Rect(10976f, 620f, 269f, 66f), "241:2338", "GROUP");
        InstantiatePrefabScaled(badgePrefab, labelGroup.transform, new Rect(10976f, 620f, 269f, 66f), new Rect(10976f, 620f, 66f, 66f), "level-badge", "I228:1394;241:2164", "INSTANCE", 0.66f);
        CreateText("蛐蛐新手", labelGroup.transform, new Rect(10976f, 620f, 269f, 66f), new Rect(11085f, 625f, 160f, 61f), "蛐蛐新手", 40f, TitleColor, TextAlignmentOptions.Left, "241:2201");
        return root;
    }

    static GameObject BuildRenwuPage(Transform canvas, GameObject progressPrefab, GameObject rewardTierPrefab)
    {
        Transform existing = canvas.Find("FigmaImport_renwu_217_333");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        GameObject root = CreateBoundObject("FigmaImport_renwu_217_333", canvas, RenwuBounds, RenwuBounds, "217:333", "FRAME");
        CreateColorImage("Rectangle 19", root.transform, RenwuBounds, new Rect(10946f, 272f, 936f, 1396f), PanelGray, "241:2139", "RECTANGLE");

        GameObject frame15 = CreateBoundObject("Frame 15", root.transform, RenwuBounds, new Rect(10965f, 516f, 886f, 1119f), "241:2594", "FRAME");
        frame15.AddComponent<RectMask2D>();
        CreateColorImage("Rectangle 20", frame15.transform, new Rect(10965f, 516f, 886f, 1119f), new Rect(10965f, 516f, 886f, 1119f), new Color(0.5452f, 0.5452f, 0.5452f, 1f), "241:2593", "RECTANGLE");
        CreateColorImage("Rectangle 21", frame15.transform, new Rect(10965f, 516f, 886f, 1119f), new Rect(10987.72f, 516f, 33.13f, 1119f), Color.black, "241:2615", "RECTANGLE");

        GameObject list = CreateBoundObject("Frame 1", frame15.transform, new Rect(10965f, 516f, 886f, 1119f), new Rect(10965f, 548f, 853f, 1280f), "228:1514", "FRAME");
        for (int i = 0; i < RewardTierSlots.Length; i++)
        {
            InstantiatePrefab(rewardTierPrefab, list.transform, new Rect(10965f, 548f, 853f, 1280f), RewardTierSlots[i], "reward-tier", "228:1394", "INSTANCE");
        }

        InstantiatePrefab(progressPrefab, root.transform, RenwuBounds, new Rect(11085f, 339f, 651f, 110f), "progress-card", "241:2140", "INSTANCE");

        GameObject banner = CreateColorImage("title-banner", root.transform, RenwuBounds, new Rect(10946f, 155f, 936f, 117f), MenuFill, "218:440", "FRAME");
        Rect bannerBounds = new Rect(10946f, 155f, 936f, 117f);
        CreateText("日勤", banner.transform, bannerBounds, new Rect(11350f, 175f, 128f, 77f), "日勤", 64f, new Color(1f, 0.95f, 0.8f, 1f), TextAlignmentOptions.Center, "218:441");
        GameObject info = CreateColorImage("Rectangle 23", banner.transform, bannerBounds, new Rect(10965f, 178f, 45f, 59f), PanelGray, "241:2638", "RECTANGLE");
        CreateText("i", info.transform, new Rect(10965f, 178f, 45f, 59f), new Rect(10965f, 178f, 45f, 59f), "i", 24f, Color.black, TextAlignmentOptions.Center, "241:2639");
        GameObject back = CreateColorImage("返回", banner.transform, bannerBounds, new Rect(11741f, 191f, 124f, 61f), PanelGray, "241:2641", "RECTANGLE");
        CreateText("返回", back.transform, new Rect(11741f, 191f, 124f, 61f), new Rect(11757f, 206f, 87f, 46f), "返回", 24f, Color.black, TextAlignmentOptions.Center, "241:2642");
        EnableRaycast(back);
        return root;
    }

    static GameObject BuildEventPage(
        Transform canvas,
        GameObject progressPrefab,
        GameObject playerCardPrefab,
        GameObject matchCtaPrefab,
        GameObject teamCtaPrefab,
        GameObject sideButtonPrefab,
        GameObject menuPrefab,
        GameObject renwuPrefab)
    {
        Transform existing = canvas.Find("FigmaImport_event-face-painting_10_593");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        GameObject root = CreateBoundObject("FigmaImport_event-face-painting_10_593", canvas, EventBounds, EventBounds, "10:593", "FRAME");
        root.AddComponent<RectMask2D>();
        root.AddComponent<DouQuquEventQuestPopup>();

        CreateSpriteImage("festive-courtyard-bg", root.transform, EventBounds, EventBounds, EventSliceFolder + "/10_594.png", "10:594", "RECTANGLE");
        CreateText("斗蛐蛐", root.transform, EventBounds, new Rect(2452f, 95f, 288f, 146f), "斗蛐蛐", 96f, TitleColor, TextAlignmentOptions.Center, "10:606");
        InstantiatePrefab(sideButtonPrefab, root.transform, EventBounds, SideButtonBounds, "SideButton_活动介绍", "205:764", "INSTANCE");

        GameObject players = CreateBoundObject("Group 19", root.transform, EventBounds, new Rect(2167f, 822f, 902f, 239f), "241:2681", "GROUP");
        players.SetActive(false);
        for (int i = 0; i < PlayerCardSlots.Length; i++)
        {
            GameObject card = InstantiatePrefab(playerCardPrefab, players.transform, new Rect(2167f, 822f, 902f, 239f), PlayerCardSlots[i], "player-card", "169:54", "INSTANCE");
            SetChildText(card, "player-name", PlayerNames[i]);
        }

        InstantiatePrefab(progressPrefab, root.transform, EventBounds, ProgressBounds, "progress-card", "238:529", "INSTANCE");

        GameObject leave = CreateBoundObject("Group 11", root.transform, EventBounds, new Rect(2918f, 14f, 183f, 81f), "240:1362", "GROUP");
        leave.SetActive(false);
        CreateColorImage("Rectangle 9", leave.transform, new Rect(2918f, 14f, 183f, 81f), new Rect(2918f, 14f, 183f, 81f), PanelGray, "238:654", "RECTANGLE");
        CreateText("离开房间", leave.transform, new Rect(2918f, 14f, 183f, 81f), new Rect(2946f, 34f, 139f, 29f), "离开房间", 32f, Color.black, TextAlignmentOptions.Center, "238:655");
        EnableRaycast(leave);

        GameObject actions = CreateBoundObject("Group 5", root.transform, EventBounds, new Rect(2200f, 1380f, 869f, 273f), "238:739", "GROUP");
        InstantiatePrefab(teamCtaPrefab, actions.transform, new Rect(2200f, 1380f, 869f, 273f), TeamCtaBounds, "好友组队", "238:734", "INSTANCE");
        InstantiatePrefab(matchCtaPrefab, actions.transform, new Rect(2200f, 1380f, 869f, 273f), MatchCtaBounds, "StartMatchButton", "238:686", "INSTANCE");

        GameObject ready = InstantiatePrefab(matchCtaPrefab, root.transform, EventBounds, new Rect(2429f, 1380f, 340f, 273f), "准备", "ready", "INSTANCE");
        SetChildText(ready, "随机匹配", "准备");
        SetChildText(ready, "开始匹配", "准备");
        EnableRaycast(ready);
        ready.SetActive(false);

        if (menuPrefab != null)
        {
            InstantiatePrefab(menuPrefab, root.transform, EventBounds, MenuBounds, "下方菜单", "205:775", "COMPONENT");
        }

        GameObject popup = CreateBoundObject("Popup_renwu", root.transform, EventBounds, EventBounds, "217:333", "FRAME");
        popup.SetActive(false);
        Image dim = CreateColorImage("Dim", popup.transform, EventBounds, EventBounds, new Color(0.05f, 0.03f, 0.02f, 0.62f), "dim", "RECTANGLE").GetComponent<Image>();
        dim.raycastTarget = true;
        GameObject renwu = InstantiatePrefab(renwuPrefab, popup.transform, EventBounds, EventBounds, "FigmaImport_renwu_217_333", "217:333", "INSTANCE");
        Transform nestedProgress = FindNamed(renwu.transform, "progress-card");
        if (nestedProgress != null)
        {
            Button nestedButton = nestedProgress.GetComponent<Button>();
            if (nestedButton != null) Object.DestroyImmediate(nestedButton);
            Image nestedImage = nestedProgress.GetComponent<Image>();
            if (nestedImage != null) nestedImage.raycastTarget = false;
        }

        SerializedObject so = new SerializedObject(root.GetComponent<DouQuquEventQuestPopup>());
        so.FindProperty("popupRoot").objectReferenceValue = popup;
        so.ApplyModifiedPropertiesWithoutUndo();
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

    static GameObject InstantiatePrefabScaled(
        GameObject prefab,
        Transform parent,
        Rect parentBounds,
        Rect nodeBounds,
        string name,
        string nodeId,
        string nodeType,
        float scale)
    {
        GameObject instance = InstantiatePrefab(prefab, parent, parentBounds, nodeBounds, name, nodeId, nodeType);
        RectTransform rect = instance.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(nodeBounds.width / scale, nodeBounds.height / scale);
        rect.localScale = new Vector3(scale, scale, 1f);
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
        FigmaFontMappingAsset mapping = AssetDatabase.LoadAssetAtPath<FigmaFontMappingAsset>("Assets/FigmaImports/FigmaFontMapping.asset");
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

    static void EnableRaycast(GameObject go)
    {
        Image image = go.GetComponent<Image>();
        if (image == null)
        {
            image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.01f);
            image.sprite = UiSprite();
        }

        image.raycastTarget = true;
        if (go.GetComponent<Button>() == null) go.AddComponent<Button>().transition = Selectable.Transition.None;
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

    static void CropSprite(string sourcePath, RectInt pixels, string destPath)
    {
        byte[] bytes = File.ReadAllBytes(sourcePath);
        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(bytes))
        {
            Object.DestroyImmediate(source);
            throw new System.InvalidOperationException("Failed to load " + sourcePath);
        }

        int x = Mathf.Clamp(pixels.x, 0, source.width - 1);
        int y = Mathf.Clamp(pixels.y, 0, source.height - 1);
        int width = Mathf.Min(pixels.width, source.width - x);
        int height = Mathf.Min(pixels.height, source.height - y);
        Color[] colors = source.GetPixels(x, y, width, height);
        Texture2D cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
        cropped.SetPixels(colors);
        cropped.Apply();
        EnsureFolder(Path.GetDirectoryName(destPath).Replace("\\", "/"));
        File.WriteAllBytes(destPath, cropped.EncodeToPNG());
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(cropped);
        AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(destPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
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
