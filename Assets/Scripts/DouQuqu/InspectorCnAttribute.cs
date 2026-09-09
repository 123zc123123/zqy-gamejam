using UnityEngine;

namespace DouQuqu
{
    /// <summary>Inspector 显示中文名。代码字段名不变。</summary>
    public sealed class InspectorCnAttribute : PropertyAttribute
    {
        public readonly string Name;
        public readonly string Tip;

        public InspectorCnAttribute(string name, string tip = "")
        {
            Name = name;
            Tip = tip ?? "";
        }
    }
}
