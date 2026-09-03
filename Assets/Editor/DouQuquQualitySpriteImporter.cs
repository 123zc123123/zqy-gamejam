using UnityEditor;
using UnityEngine;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 把育虫盘品质立绘导入成 Sprite (2D and UI)，避免默认 Texture 类型。
    /// 不手改 .meta；由 TextureImporter.SaveAndReimport 写导入设置。
    /// </summary>
    public sealed class DouQuquQualitySpriteImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/Merge/MergeQualities";

        [InitializeOnLoadMethod]
        private static void EnsureImportedAsSprites()
        {
            EditorApplication.delayCall += ReimportIfNeeded;
        }

        [MenuItem("DouQuqu/Import Quality Portraits")]
        public static void ImportQualityPortraits()
        {
            ReimportIfNeeded();
        }

        private static void ReimportIfNeeded()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) return;
            string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { Folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || AlreadySprite(importer)) continue;
                ApplySpriteSettings(importer);
                importer.SaveAndReimport();
            }
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(Folder + "/")) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            ApplySpriteSettings(importer);
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
