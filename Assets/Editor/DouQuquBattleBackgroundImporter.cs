using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 战斗全屏背景：正式图放在 Resources/Battle/Board/Textures/，
    /// 导入成 Sprite (2D and UI) 并接到 ArenaBackgroundScenery。
    /// 不手改 .prefab / .meta。
    /// </summary>
    public sealed class DouQuquBattleBackgroundImporter : AssetPostprocessor
    {
        private const string TexturePath = "Assets/Resources/Battle/Board/Textures/bg_big.png";
        private const string TableTexturePath = "Assets/Resources/Battle/Board/Textures/battleTable-default.png";
        private const string PrefabPath = "Assets/Resources/Battle/Board/Prefabs/ArenaBackgroundScenery.prefab";

        [InitializeOnLoadMethod]
        private static void EnsureAssigned()
        {
            EditorApplication.delayCall += Apply;
        }

        [MenuItem("DouQuqu/Assign Battle Background")]
        public static void AssignBattleBackground()
        {
            Apply();
        }

        private void OnPreprocessTexture()
        {
            if (assetPath.Replace('\\', '/') != TexturePath) return;
            ApplySpriteSettings((TextureImporter)assetImporter);
        }

        private static void Apply()
        {
            TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer != null && !AlreadySprite(importer))
            {
                ApplySpriteSettings(importer);
                importer.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
            if (sprite == null) return;
            Sprite tableSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TableTexturePath);

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Image image = root.GetComponent<Image>();
                if (image == null) return;
                image.sprite = sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = Color.white;
                RectTransform rect = root.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
                EnsureTable(root.transform, tableSprite);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureTable(Transform scenery, Sprite tableSprite)
        {
            Transform existing = scenery.Find("BattleTable");
            GameObject go = existing != null ? existing.gameObject : new GameObject("BattleTable", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform table = go.GetComponent<RectTransform>();
            table.SetParent(scenery, false);
            table.anchorMin = new Vector2(0.5f, 0.5f);
            table.anchorMax = new Vector2(0.5f, 0.5f);
            table.pivot = new Vector2(0.5f, 0.5f);
            table.anchoredPosition = Vector2.zero;
            Vector2 size = tableSprite != null ? tableSprite.rect.size : new Vector2(1015f, 1486f);
            table.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.sprite = tableSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = tableSprite != null ? Color.white : Color.clear;
        }

        private static bool AlreadySprite(TextureImporter importer)
        {
            return importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == SpriteImportMode.Single
                && !importer.mipmapEnabled;
        }

        private static void ApplySpriteSettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.alphaIsTransparency = true;
            settings.mipmapEnabled = false;
            importer.SetTextureSettings(settings);
        }
    }
}
