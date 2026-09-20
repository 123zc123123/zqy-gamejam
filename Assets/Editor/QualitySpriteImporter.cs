using UnityEditor;
using UnityEngine;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 把育虫盘品质立绘导入成 Sprite (2D and UI)，避免默认 Texture 类型。
    /// 不手改 .meta；由 TextureImporter.SaveAndReimport 写导入设置。
    /// </summary>
    public sealed class QualitySpriteImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/Merge/MergeQualities";
        private const string PackBgPrefix = "Assets/Resources/Common/Textures/PackCricketBg-";
        private const string QualityLabelPrefix = "Assets/Resources/Common/Textures/qualityLable-";
        private const string QualityLabelAltPrefix = "Assets/Resources/Common/Textures/qualityLabel-";
        private const string PackTabPrefix = "Assets/Resources/Common/Textures/tab";
        private const string PackSelectLine = "Assets/Resources/HeroSelection/Textures/选择线.png";
        private const string PackPanelBg = "Assets/Resources/HeroSelection/Textures/PackBg.png";
        private const string BackArrow = "Assets/Resources/Common/Textures/backArrow.png";
        /// <summary>九宫格边：left, bottom, right, top。顶 200 不拉，底 100 作为中心拉伸。</summary>
        private static readonly Vector4 PackPanelBorder = new Vector4(0f, 0f, 0f, 200f);
        /// <summary>品质铭牌竖图两端尖角：bottom 22、top 20，中间大理石可拉长。</summary>
        private static readonly Vector4 QualityLabelBorder = new Vector4(0f, 22f, 0f, 20f);

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
            ReimportFolder(Folder);
            ReimportFolder("Assets/Resources/Common/Textures");
            ReimportFolder("Assets/Resources/HeroSelection/Textures");
        }

        private static void ReimportFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsTarget(path)) continue;
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || AlreadySprite(importer)) continue;
                ApplySpriteSettings(importer);
                importer.SaveAndReimport();
            }
        }

        private void OnPreprocessTexture()
        {
            if (!IsTarget(assetPath)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            ApplySpriteSettings(importer);
        }

        private static bool IsTarget(string path)
        {
            path = path.Replace('\\', '/');
            if (path.StartsWith(Folder + "/")) return true;
            if (path == PackPanelBg || path == PackSelectLine || path == BackArrow) return true;
            if (path.StartsWith(PackTabPrefix) && (path.EndsWith(".png") || path.EndsWith(".PNG"))) return true;
            if ((path.StartsWith(QualityLabelPrefix) || path.StartsWith(QualityLabelAltPrefix))
                && (path.EndsWith(".png") || path.EndsWith(".PNG")))
                return true;
            return path.StartsWith(PackBgPrefix) && (path.EndsWith(".png") || path.EndsWith(".PNG"));
        }

        private static bool IsPackPanel(string path)
        {
            return path.Replace('\\', '/') == PackPanelBg;
        }

        private static bool AlreadySprite(TextureImporter importer)
        {
            if (importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.mipmapEnabled)
                return false;
            if (IsPackPanel(importer.assetPath) && importer.spriteBorder != PackPanelBorder)
                return false;
            if (IsQualityLabel(importer.assetPath) && importer.spriteBorder != QualityLabelBorder)
                return false;
            return true;
        }

        private static bool IsQualityLabel(string path)
        {
            path = path.Replace('\\', '/');
            return (path.StartsWith(QualityLabelPrefix) || path.StartsWith(QualityLabelAltPrefix))
                && (path.EndsWith(".png") || path.EndsWith(".PNG"));
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
            if (IsPackPanel(importer.assetPath))
                settings.spriteBorder = PackPanelBorder;
            if (IsQualityLabel(importer.assetPath))
            {
                settings.spriteBorder = QualityLabelBorder;
                settings.spriteMeshType = SpriteMeshType.FullRect;
            }
            importer.SetTextureSettings(settings);
            if (IsPackPanel(importer.assetPath))
                importer.spriteBorder = PackPanelBorder;
            if (IsQualityLabel(importer.assetPath))
                importer.spriteBorder = QualityLabelBorder;
        }
    }
}
