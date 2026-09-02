#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 为运行时创建的 Demo UI 生成包含中文字符的 TMP 动态字体资产。
    /// 该脚本只在编辑器中使用，不会进入最终包体。
    /// </summary>
    internal static class DouQuquFontGenerator
    {
        private const string SourcePath = "Assets/Resources/Fonts/NotoSansSC-VF.ttf";
        private const string TargetPath = "Assets/Resources/Fonts/DouQuquChinese SDF.asset";

        // Demo 当前会用到的中文字符。加入常用标点，避免状态文本出现方框。
        private const string DemoCharacters =
            "斗蟋蟀演示输入名称后登录成功主界面玩家按钮合成匹配图鉴退出返回局域网计时开始取消正在搜索等待槽位机器人进入游戏结束胜利你是本局赢家获胜者还没有前往将两只二级棋子到三级即可获得拖动它同等级棋盘满先空格完成抽卡结果操作最高级请将内松手必须是没有移动已取消分数最近生成并从消失保底进度按住蓄力合成完成获得道具当前等级点搜索秒成功" +
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

            // 已存在时复用资产，避免重复创建 GUID；若资产异常则直接重建内存对象。
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TargetPath);
            if (fontAsset == null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    source,
                    64,
                    8,
                    GlyphRenderMode.SDFAA,
                    2048,
                    2048,
                    AtlasPopulationMode.Dynamic,
                    true);
                if (fontAsset == null)
                {
                    Debug.LogError("TMP 字体资产创建失败：" + SourcePath);
                    return;
                }

                fontAsset.name = "DouQuquChinese SDF";
                AssetDatabase.CreateAsset(fontAsset, TargetPath);
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
