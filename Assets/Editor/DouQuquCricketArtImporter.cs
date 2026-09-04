using UnityEditor;
using UnityEngine;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 局内蛐蛐零件图导入为 Sprite，Mesh Type = Full Rect，给身体描边留出边缘采样。
    /// 不手改 .meta。
    /// </summary>
    public sealed class DouQuquCricketArtImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Art/Characters/Crickets";

        [InitializeOnLoadMethod]
        private static void EnsureImportedAsSprites()
        {
            EditorApplication.delayCall += ReimportIfNeeded;
        }

        private void OnPreprocessTexture()
        {
            if (!IsCricketTexture(assetPath)) return;
            ApplySpriteSettings((TextureImporter)assetImporter);
        }

        public static void ReimportIfNeeded()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) return;
            string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { Folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsCricketTexture(path)) continue;
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || AlreadySprite(importer)) continue;
                ApplySpriteSettings(importer);
                importer.SaveAndReimport();
            }
        }

        private static bool IsCricketTexture(string path)
        {
            path = path.Replace('\\', '/');
            if (!path.StartsWith(Folder + "/")) return false;
            if (path.Contains("/Materials/") || path.Contains("/Shaders/")) return false;
            return path.EndsWith(".png") || path.EndsWith(".PNG");
        }

        private static bool AlreadySprite(TextureImporter importer)
        {
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            return importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == SpriteImportMode.Single
                && !importer.mipmapEnabled
                && settings.spriteMeshType == SpriteMeshType.FullRect;
        }

        public static void ApplySpriteSettings(TextureImporter importer)
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
            importer.maxTextureSize = 2048;
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
