using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 战斗全屏背景：正式图放在 Resources/Battle/Textures/，
    /// 导入成 Sprite (2D and UI) 并接到 Battle HUD 里的 ArenaBackgroundScenery。
    /// 桌面拆成 tableBg + tableOutline；outline 九宫格适配 tableBg。
    /// 不手改 .prefab / .meta。
    /// </summary>
    public sealed class BattleBackgroundImporter : AssetPostprocessor
    {
        private const string TexturePath = "Assets/Resources/Battle/Textures/bg_big.png";
        private const string TableBgPath = "Assets/Resources/Battle/Textures/tableBg.png";
        private const string TableOutlinePath = "Assets/Resources/Battle/Textures/tableOutline.png";
        private const string PrefabPath = "Assets/Resources/Battle/Hud/Prefabs/Battle.prefab";

        /// <summary>九宫格边 left, bottom, right, top。包住四角金属片，避免拉边上的角件。</summary>
        private static readonly Vector4 OutlineSpriteBorder = new Vector4(130f, 131f, 126f, 130f);

        /// <summary>
        /// outline 内洞相对整图的 inset（left, bottom, right, top）。
        /// 内洞透明区对齐 tableBg，所以 outline 比 tableBg 外扩这么多。可再细调。
        /// </summary>
        private static readonly Vector4 OutlineInnerPadding = new Vector4(79f, 74f, 83f, 80f);

        [InitializeOnLoadMethod]
        private static void EnsureImported()
        {
            EditorApplication.delayCall += EnsureSpriteImports;
        }

        [MenuItem("DouQuqu/Assign Battle Background")]
        public static void AssignBattleBackground()
        {
            Apply();
        }

        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (path != TexturePath && path != TableBgPath && path != TableOutlinePath) return;
            ApplySpriteSettings((TextureImporter)assetImporter, path);
        }

        private static void EnsureSpriteImports()
        {
            EnsureSprite(TexturePath, false);
            EnsureSprite(TableBgPath, true);
            EnsureSprite(TableOutlinePath, false);
        }

        private static void Apply()
        {
            EnsureSpriteImports();

            Sprite bg = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
            Sprite tableBg = AssetDatabase.LoadAssetAtPath<Sprite>(TableBgPath);
            Sprite tableOutline = AssetDatabase.LoadAssetAtPath<Sprite>(TableOutlinePath);
            if (bg == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform scenery = FindNamed(root.transform, "ArenaBackgroundScenery");
                if (scenery == null) return;
                Image image = scenery.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = bg;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    image.color = Color.white;
                }
                EnsureTable(scenery, tableBg, tableOutline);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureTable(Transform scenery, Sprite tableBg, Sprite tableOutline)
        {
            Transform existing = scenery.Find("BattleTable");
            GameObject go = existing != null
                ? existing.gameObject
                : new GameObject("BattleTable", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform table = go.GetComponent<RectTransform>();
            table.SetParent(scenery, false);
            table.anchorMin = new Vector2(0.5f, 0.5f);
            table.anchorMax = new Vector2(0.5f, 0.5f);
            table.pivot = new Vector2(0.5f, 0.5f);
            table.anchoredPosition = Vector2.zero;
            if (existing == null || table.sizeDelta.x < 1f || table.sizeDelta.y < 1f)
            {
                Vector2 size = tableBg != null ? tableBg.rect.size : new Vector2(2070f, 3063f);
                table.sizeDelta = size;
            }

            Image tableImage = go.GetComponent<Image>();
            if (tableImage != null)
            {
                tableImage.sprite = null;
                tableImage.color = Color.clear;
                tableImage.raycastTarget = false;
                tableImage.enabled = false;
            }

            StretchImage(EnsureChild(table, "tableBg"), tableBg, Image.Type.Simple, Vector2.zero, Vector2.zero, true);
            StretchImage(
                EnsureChild(table, "tableOutline"),
                tableOutline,
                Image.Type.Sliced,
                new Vector2(-OutlineInnerPadding.x, -OutlineInnerPadding.y),
                new Vector2(OutlineInnerPadding.z, OutlineInnerPadding.w),
                false);
        }

        private static RectTransform EnsureChild(RectTransform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            GameObject go = existing != null
                ? existing.gameObject
                : new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void StretchImage(
            RectTransform rect,
            Sprite sprite,
            Image.Type type,
            Vector2 offsetMin,
            Vector2 offsetMax,
            bool fillCenter)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
            Image image = rect.GetComponent<Image>();
            if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            image.fillCenter = fillCenter;
            image.preserveAspect = false;
            image.raycastTarget = false;
            image.color = sprite != null ? Color.white : Color.clear;
            image.enabled = true;
        }

        private static Transform FindNamed(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamed(root.GetChild(i), objectName);
                if (hit != null) return hit;
            }

            return null;
        }

        private static void EnsureSprite(string path, bool large)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || AlreadySprite(importer, path, large)) return;
            ApplySpriteSettings(importer, path);
            importer.SaveAndReimport();
        }

        private static bool AlreadySprite(TextureImporter importer, string path, bool large)
        {
            int maxSize = large ? 4096 : 2048;
            Vector4 border = path.Replace('\\', '/') == TableOutlinePath ? OutlineSpriteBorder : Vector4.zero;
            return importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == SpriteImportMode.Single
                && !importer.mipmapEnabled
                && importer.maxTextureSize >= maxSize
                && importer.spriteBorder == border;
        }

        private static void ApplySpriteSettings(TextureImporter importer, string path)
        {
            bool large = path.Replace('\\', '/') == TableBgPath;
            bool outline = path.Replace('\\', '/') == TableOutlinePath;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = large ? 4096 : 2048;
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
            settings.spriteBorder = outline ? OutlineSpriteBorder : Vector4.zero;
            importer.SetTextureSettings(settings);
            importer.spriteBorder = outline ? OutlineSpriteBorder : Vector4.zero;
        }
    }
}
