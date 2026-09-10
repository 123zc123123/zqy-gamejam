using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class FigmaSettlementPrefabOrganizer
{
    const string ScreenPath = "Assets/Figma/Screens/battle-over_143_193.prefab";
    const string RowSourcePath = "Assets/Figma/Components/player-row_292_1108.prefab";
    const string AvatarSourcePath = "Assets/Figma/Components/avatar_165_3.prefab";
    const string RankBadgeSourcePath = "Assets/Figma/Components/rank-badge-Rank=1st_165_10.prefab";
    const string PrefabFolder = "Assets/Resources/Settlement/Prefabs";
    const string PartsFolder = PrefabFolder + "/Parts";
    const string TextureFolder = "Assets/Resources/Settlement/Textures";
    const string JiesuanPath = PrefabFolder + "/Jiesuan.prefab";
    const string PlayerRowPath = PartsFolder + "/PlayerRow.prefab";
    const string AvatarPath = PartsFolder + "/Avatar.prefab";
    const string RankBadgePath = PartsFolder + "/RankBadge.prefab";
    const string AvatarTexPath = TextureFolder + "/Avatar.png";
    const string CrownTexPath = TextureFolder + "/Crown.png";
    const string FigmaAvatarFill = "Assets/Figma/ImageFills/c608a2a5c9616b884bd9d590d8a91b86c900a981.png";
    const string FigmaCrown = "Assets/Figma/ServerRenderedImages/145_46.png";

    static readonly int[] PlaceScores = { 50, 30, 10, 5 };
    static readonly string[] RankLabels = { "第1名", "第2名", "第3名", "第4名" };
    static readonly string[] PlayerNames = { "玩家1", "玩家2", "玩家3", "玩家4" };

    [MenuItem("Tools/Figma Bridge/Organize Settlement Prefabs")]
    public static void Organize()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath) == null)
        {
            Debug.LogError("缺少 Bridge 结果页：" + ScreenPath);
            return;
        }

        EnsureFolder(PartsFolder);
        EnsureFolder(TextureFolder);
        CopyTexture(FigmaAvatarFill, AvatarTexPath);
        CopyTexture(FigmaCrown, CrownTexPath);

        CopyPrefab(RowSourcePath, PlayerRowPath);
        CopyPrefab(AvatarSourcePath, AvatarPath);
        CopyPrefab(RankBadgeSourcePath, RankBadgePath);

        RenamePlayerRow(PlayerRowPath);
        ExtractPartFromRow(PlayerRowPath, "Avatar", AvatarPath);
        ExtractPartFromRow(PlayerRowPath, "Rank", RankBadgePath);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RankBadgePath) != null)
            RenameRankBadge(RankBadgePath);
        BuildJiesuanFromScreen();
        DeleteLegacyPrefabs();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(JiesuanPath));
        Debug.Log("Settlement organized:\n" + JiesuanPath + "\n" + PlayerRowPath + "\n" + AvatarPath + "\n" + RankBadgePath);
    }

    static void RenamePlayerRow(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = "PlayerRow";
            RenameRowChildren(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void RenameRankBadge(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = "RankBadge";
            Transform label = FindByName(root.transform, "第1名");
            if (label != null) label.name = "RankText";
            Transform icon = root.transform.Find("crown");
            if (icon != null) icon.name = "RankIcon";
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void BuildJiesuanFromScreen()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
        GameObject staging = new GameObject("SettlementStaging");
        staging.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, staging.transform);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "Jiesuan";

            Transform banner = FindByName(instance.transform, "banner");
            if (banner != null) banner.name = "Banner";
            Transform title = FindByName(instance.transform, "title");
            if (title != null) title.name = "Title";
            Transform subtitle = FindByName(instance.transform, "subtitle");
            if (subtitle != null) subtitle.name = "Subtitle";
            Transform bg = FindByName(instance.transform, "black-background");
            if (bg != null) bg.name = "BlackBackground";

            List<Transform> rows = new List<Transform>();
            for (int i = 0; i < instance.transform.childCount; i++)
            {
                Transform child = instance.transform.GetChild(i);
                if (child.name == "player-row" || child.name.StartsWith("PlayerRow"))
                    rows.Add(child);
            }
            rows.Sort((a, b) => b.GetComponent<RectTransform>().anchoredPosition.y
                .CompareTo(a.GetComponent<RectTransform>().anchoredPosition.y));

            for (int i = 0; i < rows.Count; i++)
            {
                Transform row = rows[i];
                row.name = "PlayerRow" + (i + 1);
                RenameRowChildren(row);
                ApplyRowSample(row, i);
            }

            Transform ret = FindByName(instance.transform, "Frame 4");
            if (ret == null) ret = FindByName(instance.transform, "返回");
            if (ret != null)
            {
                ret.name = "返回";
                if (ret.GetComponent<Button>() == null) ret.gameObject.AddComponent<Button>();
                Button button = ret.GetComponent<Button>();
                button.transition = Selectable.Transition.None;
            }

            PrefabUtility.SaveAsPrefabAsset(instance, JiesuanPath);
        }
        finally
        {
            Object.DestroyImmediate(staging);
        }
    }

    static void RenameRowChildren(Transform row)
    {
        Transform avatar = FindByName(row, "avatar");
        if (avatar != null) avatar.name = "Avatar";

        Transform name = FindByName(row, "玩家1");
        if (name != null) name.name = "Name";

        Transform badge = FindByName(row, "rank-badge");
        if (badge != null)
        {
            badge.name = "Rank";
            Transform rankText = FindByName(badge, "第1名");
            if (rankText == null) rankText = FindByName(badge, "RankText");
            if (rankText != null) rankText.name = "RankText";
            Transform icon = badge.Find("crown");
            if (icon == null) icon = FindByName(badge, "RankIcon");
            if (icon != null) icon.name = "RankIcon";
        }

        List<TMP_Text> scores = new List<TMP_Text>();
        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].transform.parent != row) continue;
            if (texts[i].name == "1200" || texts[i].name == "PlaceScore" || texts[i].name == "KillScore")
                scores.Add(texts[i]);
        }
        scores.Sort((a, b) =>
        {
            RectTransform ra = a.transform as RectTransform;
            RectTransform rb = b.transform as RectTransform;
            return ra.anchoredPosition.x.CompareTo(rb.anchoredPosition.x);
        });
        if (scores.Count >= 1) scores[0].name = "PlaceScore";
        if (scores.Count >= 2) scores[1].name = "KillScore";
    }

    static void ApplyRowSample(Transform row, int index)
    {
        if (index < 0 || index >= PlaceScores.Length) return;
        SetText(row, "Name", PlayerNames[index]);
        SetText(row, "PlaceScore", PlaceScores[index].ToString());
        SetText(row, "KillScore", "0");
        Transform rank = FindByName(row, "Rank");
        if (rank != null) SetText(rank, "RankText", RankLabels[index]);
    }

    static void SetText(Transform root, string name, string value)
    {
        Transform t = FindByName(root, name);
        if (t == null) return;
        TMP_Text tmp = t.GetComponent<TMP_Text>();
        if (tmp != null) tmp.text = value;
    }

    static void DeleteLegacyPrefabs()
    {
        string[] keep =
        {
            JiesuanPath,
            PlayerRowPath,
            AvatarPath,
            RankBadgePath
        };
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (path.StartsWith(PartsFolder + "/")) continue;
            bool keepThis = false;
            for (int k = 0; k < keep.Length; k++)
            {
                if (path == keep[k]) keepThis = true;
            }
            if (keepThis) continue;
            AssetDatabase.DeleteAsset(path);
        }

        if (AssetDatabase.IsValidFolder(PrefabFolder + "/Leaf"))
            AssetDatabase.DeleteAsset(PrefabFolder + "/Leaf");
    }

    static void ExtractPartFromRow(string rowPath, string childName, string destPath)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(destPath) != null) return;
        GameObject row = PrefabUtility.LoadPrefabContents(rowPath);
        try
        {
            Transform child = FindByName(row.transform, childName);
            if (child == null) return;
            GameObject clone = Object.Instantiate(child.gameObject);
            clone.name = childName == "Rank" ? "RankBadge" : childName;
            PrefabUtility.SaveAsPrefabAsset(clone, destPath);
            Object.DestroyImmediate(clone);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(row);
        }
    }

    static void CopyPrefab(string src, string dest)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(src) == null) return;
        EnsureFolder(System.IO.Path.GetDirectoryName(dest).Replace("\\", "/"));
        if (AssetDatabase.LoadAssetAtPath<Object>(dest) != null)
            AssetDatabase.DeleteAsset(dest);
        AssetDatabase.CopyAsset(src, dest);
    }

    static void CopyTexture(string src, string dest)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(src) == null) return;
        if (AssetDatabase.LoadAssetAtPath<Object>(dest) != null)
            AssetDatabase.DeleteAsset(dest);
        AssetDatabase.CopyAsset(src, dest);
    }

    static Transform FindByName(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform hit = FindByName(root.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
        string name = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
