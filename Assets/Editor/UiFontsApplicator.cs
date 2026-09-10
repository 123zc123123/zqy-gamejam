using DouQuqu;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class UiFontsApplicator
{
    const string ThemePath = "Assets/Resources/Fonts/UiFonts.asset";
    const string FontPath = "Assets/Resources/Fonts/Chinese SDF.asset";

    [MenuItem("DouQuqu/Apply UI Fonts To Resources Prefabs")]
    public static void ApplyToResourcesPrefabs()
    {
        EnsureTheme();
        TMP_FontAsset font = UiFonts.Font;
        if (font == null)
        {
            Debug.LogError("UiFonts 没有字体。请把 Chinese SDF 拖进 Assets/Resources/Fonts/UiFonts。");
            return;
        }

        int texts = 0;
        int prefabs = 0;
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
                bool dirty = false;
                for (int t = 0; t < labels.Length; t++)
                {
                    if (labels[t].font == font) continue;
                    labels[t].font = font;
                    dirty = true;
                    texts++;
                }
                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabs++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("UiFonts 已写入 " + prefabs + " 个 Prefab，" + texts + " 处 TMP 文本。");
    }

    [MenuItem("DouQuqu/Ping UI Fonts")]
    public static void PingTheme()
    {
        EnsureTheme();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(ThemePath));
    }

    public static UiFonts EnsureTheme()
    {
        UiFonts theme = AssetDatabase.LoadAssetAtPath<UiFonts>(ThemePath);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<UiFonts>();
            theme.font = font;
            theme.slots = DefaultSlots();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }
        else
        {
            SerializedObject so = new SerializedObject(theme);
            SerializedProperty fontProp = so.FindProperty("font");
            if (fontProp != null && fontProp.objectReferenceValue == null && font != null)
            {
                fontProp.objectReferenceValue = font;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            SerializedProperty slotsProp = so.FindProperty("slots");
            if (slotsProp != null && slotsProp.arraySize == 0)
            {
                theme.slots = DefaultSlots();
                EditorUtility.SetDirty(theme);
            }
        }

        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        return theme;
    }

    static UiFonts.Slot[] DefaultSlots()
    {
        return new[]
        {
            NewSlot("Title"),
            NewSlot("Subtitle"),
            NewSlot("Name"),
            NewSlot("Score"),
            NewSlot("Rank"),
            NewSlot("Button")
        };
    }

    static UiFonts.Slot NewSlot(string id)
    {
        return new UiFonts.Slot { id = id, font = null, fontStyle = FontStyles.Normal };
    }
}
