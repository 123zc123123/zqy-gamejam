using UnityEditor;
using UnityEngine;

namespace DouQuqu.Editor
{
    [CustomEditor(typeof(CricketVisual))]
    public sealed class CricketVisualEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("改描边颜色 / 宽度 / 软边后，看 Scene 或 Prefab 视图即可。触角和尾刺不描边。", MessageType.Info);
            if (!GUILayout.Button("刷新描边预览")) return;

            CricketVisual visual = (CricketVisual)target;
            Undo.RegisterFullObjectHierarchyUndo(visual.gameObject, "Preview Cricket Outline");
            visual.BindHierarchy();
            visual.ApplyOutline();
            EditorUtility.SetDirty(visual);
            SceneView.RepaintAll();
        }
    }
}
