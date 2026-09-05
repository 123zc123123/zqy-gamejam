using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 战斗全屏背景：正式图放在 zhandou/Textures/（不进 Figma），
    /// 导入成 Sprite (2D and UI) 并接到 ArenaBackgroundScenery。
    /// 不手改 .prefab / .meta。
    /// </summary>
    public sealed class DouQuquBattleBackgroundImporter : AssetPostprocessor
    {
        private const string TexturePath = "Assets/Game/UI/zhandou/Textures/ArenaBackgroundScenery.png";
        private const string PrefabPath = "Assets/Game/UI/zhandou/Prefabs/Leaf/ArenaBackgroundScenery.prefab";

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

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null) return;
            Image existing = prefabAsset.GetComponent<Image>();
            if (existing != null && existing.sprite == sprite) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Image image = root.GetComponent<Image>();
                if (image == null) return;
                image.sprite = sprite;
                image.preserveAspect = true;
                image.color = Color.white;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
