#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 从资源圆体 Medium 生成运行时中文 TMP 动态字体。
    /// 只预烘焙常用字；后续新字靠动态图集从源 TTF 补，源文件不放 Resources。
    /// </summary>
    internal static class DouQuquFontGenerator
    {
        private const string SourcePath = "Assets/Fonts/ResourceHanRoundedCN-Medium.ttf";
        private const string TargetPath = "Assets/Resources/Fonts/DouQuquChinese SDF.asset";

        // 先写入当前界面会用到的字。其余中文在运行时由动态图集补，不必一次打满字库。
        private const string DemoCharacters =
            "斗蟋蟀演示输入名称后登录成功主界面玩家按钮合成匹配图鉴退出返回局域网计时开始取消正在搜索等待槽位机器人进入游戏结束胜利你是本局赢家获胜者还没有前往将两只二级棋子到三级即可获得拖动它同等级棋盘满先空格完成抽卡结果操作最高级请将内松手必须是没有移动已取消分数最近生成并从消失保底进度按住蓄力合成完成获得道具当前等级点搜索秒成功剩余时间即将开放育虫盘图鉴活动排行商店学院金币规则背包放卵幼虫中虫成虫精品凡品灵品仙品极品" +
            "，。！？：；（）《》【】·…　/=-+%0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        [MenuItem("DouQuqu/Generate Chinese TMP Font")]
        public static void Generate()
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (source == null)
            {
                Debug.LogError("找不到中文字体源文件：" + SourcePath);
                return;
            }

            // 换源字体时必须重建字形表，但要保住现有 SDF 的 GUID，避免场景引用丢字。
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TargetPath);
            TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(
                source,
                64,
                8,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);
            if (created == null)
            {
                Debug.LogError("TMP 字体资产创建失败：" + SourcePath);
                return;
            }

            created.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            created.isMultiAtlasTexturesEnabled = true;
            created.name = "DouQuquChinese SDF";

            if (fontAsset == null)
            {
                AssetDatabase.CreateAsset(created, TargetPath);
                fontAsset = created;
            }
            else
            {
                // ClearFontAssetData 会按 GUID / EditorRef 还原源字体，三处都要一起换。
                SerializedObject so = new SerializedObject(fontAsset);
                SerializedProperty sourceProp = so.FindProperty("m_SourceFontFile");
                SerializedProperty editorRefProp = so.FindProperty("m_SourceFontFile_EditorRef");
                SerializedProperty guidProp = so.FindProperty("m_SourceFontFileGUID");
                if (sourceProp != null)
                    sourceProp.objectReferenceValue = source;
                if (editorRefProp != null)
                    editorRefProp.objectReferenceValue = source;
                if (guidProp != null)
                    guidProp.stringValue = AssetDatabase.AssetPathToGUID(SourcePath);
                so.ApplyModifiedPropertiesWithoutUndo();
                fontAsset.faceInfo = created.faceInfo;
                fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                fontAsset.isMultiAtlasTexturesEnabled = true;
                fontAsset.ClearFontAssetData(true);
                fontAsset.name = "DouQuquChinese SDF";
                UnityEngine.Object.DestroyImmediate(created);
            }

            // CreateFontAsset 会在内存中创建材质和 0x0 图集纹理；必须作为子资源保存，
            // 否则重新加载后 m_AtlasTextures 会变成空引用，TryAddCharacters 无法工作。
            EnsureSubAsset(fontAsset, fontAsset.material, "DouQuquChinese SDF Material");
            Texture2D atlas = fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0
                ? fontAsset.atlasTextures[0]
                : null;
            EnsureSubAsset(fontAsset, atlas, "DouQuquChinese SDF Atlas");

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;
            if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0 || fontAsset.atlasTextures[0] == null)
            {
                Debug.LogError("TMP 字体图集初始化失败。");
                return;
            }

            // 先让 TMP 在编辑器内生成字形并写回 Character/Glyph 表。
            bool success = fontAsset.TryAddCharacters(DemoCharacters, out string missing);
            EditorUtility.SetDirty(fontAsset);
            if (fontAsset.material != null) EditorUtility.SetDirty(fontAsset.material);
            for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                if (fontAsset.atlasTextures[i] != null) EditorUtility.SetDirty(fontAsset.atlasTextures[i]);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!success && !string.IsNullOrEmpty(missing))
                Debug.LogWarning("部分字符未能加入 TMP 图集：" + missing);
            else
                Debug.Log("斗蟋蟀中文 TMP 字体已生成：" + TargetPath);
        }

        private static void EnsureSubAsset(Object parent, Object child, string childName)
        {
            if (child == null) return;
            child.name = childName;
            string childPath = AssetDatabase.GetAssetPath(child);
            if (string.IsNullOrEmpty(childPath))
                AssetDatabase.AddObjectToAsset(child, parent);
        }
    }
}
#endif
