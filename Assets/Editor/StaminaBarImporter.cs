using UnityEditor;
using UnityEngine;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 耐力条切图导入成 Sprite。首次打默认九宫；之后以 Sprite Editor 为准。
    /// </summary>
    public sealed class StaminaBarImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/Battle/Entities/Textures";
        private static readonly string[] Files =
        {
            Folder + "/bg.png",
            Folder + "/outline.png",
            Folder + "/fill.png",
            Folder + "/middleLine.png",
            Folder + "/体力icon.png"
        };

        [InitializeOnLoadMethod]
        private static void EnsureImportedAsSprites()
        {
            EditorApplication.delayCall += ImportAll;
        }

        [MenuItem("DouQuqu/Import Stamina Bar Sprites")]
        public static void ImportMenu()
        {
            ImportAll();
        }

        public static void ImportAll()
        {
            for (int i = 0; i < Files.Length; i++)
                ImportPath(Files[i]);
        }

        private void OnPreprocessTexture()
        {
            if (!IsTarget(assetPath)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            if (importer.textureType == TextureImporterType.Sprite) return;
            ApplySpriteSettings(importer, true);
        }

        private static void ImportPath(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || AlreadySprite(importer)) return;
            ApplySpriteSettings(importer, true);
            importer.SaveAndReimport();
        }

        private static bool IsTarget(string path)
        {
            path = path.Replace('\\', '/');
            for (int i = 0; i < Files.Length; i++)
                if (path == Files[i]) return true;
            return false;
        }

        private static bool AlreadySprite(TextureImporter importer)
        {
            return importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == SpriteImportMode.Single
                && !importer.mipmapEnabled
                && importer.spritePixelsPerUnit == 100f;
        }

        private static void ApplySpriteSettings(TextureImporter importer, bool applyDefaultBorder)
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
            if (applyDefaultBorder)
            {
                Vector4 border = BorderOf(importer.assetPath);
                settings.spriteBorder = border;
                importer.SetTextureSettings(settings);
                importer.spriteBorder = border;
            }
            else
            {
                importer.SetTextureSettings(settings);
            }
        }

        /// <summary>仅首次导入用。之后九宫以 Sprite Editor 为准。</summary>
        private static Vector4 BorderOf(string path)
        {
            path = path.Replace('\\', '/');
            if (path.EndsWith("/bg.png")) return new Vector4(20f, 20f, 20f, 20f);
            if (path.EndsWith("/outline.png")) return new Vector4(29f, 29f, 29f, 29f);
            if (path.EndsWith("/fill.png")) return new Vector4(15f, 15f, 15f, 15f);
            return Vector4.zero;
        }
    }
}
