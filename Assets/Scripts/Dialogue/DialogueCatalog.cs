using System.Collections.Generic;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 从 Resources/Dialogue 加载全部对白组。id 来自 frontmatter，不靠文件名。
    /// </summary>
    public static class DialogueCatalog
    {
        public const string ResourceFolder = "Dialogue";
        static Dictionary<string, DialogueGroup> groups;

        public static DialogueGroup Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            DialogueGroup group;
            return groups.TryGetValue(id, out group) ? group : null;
        }

        public static IReadOnlyCollection<string> Ids
        {
            get
            {
                EnsureLoaded();
                return groups.Keys;
            }
        }

        public static void Reload()
        {
            groups = null;
            EnsureLoaded();
        }

        static void EnsureLoaded()
        {
            if (groups != null) return;
            groups = new Dictionary<string, DialogueGroup>();
            TextAsset[] assets = Resources.LoadAll<TextAsset>(ResourceFolder);
            for (int i = 0; i < assets.Length; i++)
            {
                TextAsset asset = assets[i];
                if (asset == null || string.IsNullOrEmpty(asset.text)) continue;
                if (asset.name.Equals("README", System.StringComparison.OrdinalIgnoreCase)) continue;
                DialogueGroup group = DialogueMarkdown.Parse(asset.text, asset.name);
                if (group == null || string.IsNullOrEmpty(group.id) || group.lines == null || group.lines.Count == 0)
                    continue;
                groups[group.id] = group;
            }
        }
    }
}
