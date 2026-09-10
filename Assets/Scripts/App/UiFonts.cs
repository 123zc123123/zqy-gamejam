using TMPro;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 全项目 UI 字体真源。换字体只改这份资源和 Chinese SDF，不要在各个 Prefab 上改。
    /// 加载路径：Resources/Fonts/UiFonts
    /// </summary>
    [CreateAssetMenu(menuName = "DouQuqu/UI Fonts", fileName = "UiFonts")]
    public sealed class UiFonts : ScriptableObject
    {
        public const string ResourcePath = "Fonts/UiFonts";

        [System.Serializable]
        public sealed class Slot
        {
            [InspectorCn("样式名", "Title / Subtitle / Name / Score / Rank / Button；空则走默认")]
            public string id;
            [InspectorCn("字体", "空 = 用上面那份默认字体")]
            public TMP_FontAsset font;
            [InspectorCn("字重", "Normal / Bold")]
            public FontStyles fontStyle = FontStyles.Normal;
        }

        [InspectorCn("默认字体", "运行时主字体；源 TTF 在 Assets/Fonts/")]
        public TMP_FontAsset font;
        [InspectorCn("样式槽", "按节点名套用；字体槽留空则仍用默认字体")]
        public Slot[] slots;

        static UiFonts cached;

        public static UiFonts Current
        {
            get
            {
                if (cached == null) cached = Resources.Load<UiFonts>(ResourcePath);
                return cached;
            }
        }

        public static TMP_FontAsset Font
        {
            get
            {
                UiFonts theme = Current;
                if (theme != null && theme.font != null) return theme.font;
                return Resources.Load<TMP_FontAsset>("Fonts/Chinese SDF");
            }
        }

        public static void Apply(TMP_Text text)
        {
            if (text == null) return;
            TMP_FontAsset asset = Resolve(text.gameObject.name);
            if (asset == null) return;
            if (text.font != asset) text.font = asset;
            Slot slot = FindSlot(text.gameObject.name);
            if (slot != null && slot.fontStyle != FontStyles.Normal)
                text.fontStyle = slot.fontStyle;
        }

        public static void ApplyTree(Transform root)
        {
            if (root == null) return;
            TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++) Apply(labels[i]);
        }

        static TMP_FontAsset Resolve(string nodeName)
        {
            Slot slot = FindSlot(nodeName);
            if (slot != null && slot.font != null) return slot.font;
            return Font;
        }

        static Slot FindSlot(string nodeName)
        {
            UiFonts theme = Current;
            if (theme == null || theme.slots == null || string.IsNullOrEmpty(nodeName)) return null;
            for (int i = 0; i < theme.slots.Length; i++)
            {
                Slot slot = theme.slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.id)) continue;
                if (Matches(nodeName, slot.id)) return slot;
            }
            return null;
        }

        static bool Matches(string nodeName, string id)
        {
            if (string.Equals(nodeName, id, System.StringComparison.OrdinalIgnoreCase)) return true;
            if (id == "Score" && (nodeName == "PlaceScore" || nodeName == "KillScore")) return true;
            if (id == "Rank" && (nodeName == "RankText" || nodeName == "第1名" || nodeName == "第2名" || nodeName == "第3名" || nodeName == "第4名")) return true;
            if (id == "Button" && (nodeName == "返回" || nodeName == "ReturnButton")) return true;
            if (id == "Name" && (nodeName == "玩家1" || nodeName == "PlayerName")) return true;
            return false;
        }
    }
}
