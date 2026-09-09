using UnityEditor;
using UnityEngine;

namespace DouQuqu.Editor
{
    [CustomPropertyDrawer(typeof(InspectorCnAttribute))]
    public sealed class InspectorCnDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            InspectorCnAttribute attr = (InspectorCnAttribute)attribute;
            GUIContent content = new GUIContent(attr.Name, string.IsNullOrEmpty(attr.Tip) ? label.tooltip : attr.Tip);
            RangeAttribute range = null;
            if (fieldInfo != null)
            {
                object[] ranges = fieldInfo.GetCustomAttributes(typeof(RangeAttribute), true);
                if (ranges != null && ranges.Length > 0) range = (RangeAttribute)ranges[0];
            }
            if (range != null && property.propertyType == SerializedPropertyType.Float)
                EditorGUI.Slider(position, property, range.min, range.max, content);
            else if (range != null && property.propertyType == SerializedPropertyType.Integer)
                EditorGUI.IntSlider(position, property, (int)range.min, (int)range.max, content);
            else
                EditorGUI.PropertyField(position, property, content, true);
        }
    }
}
