using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FigmaPlayerCardLayout))]
public class FigmaPlayerCardLayoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var layout = (FigmaPlayerCardLayout)target;
        EditorGUILayout.HelpBox(
            "左边：直接在 BattlePlayer 里拖 player（头像+名字）、TopRow（积分）、CricketRow。\n" +
            "右边：勾选 Right Side，整组按左边镜像（头像靠右，积分和三槽靠左，第 1 只仍贴头像）。",
            MessageType.Info);

        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("按左边默认结构重排"))
        {
            Undo.RecordObject(layout, "Apply default left player card layout");
            layout.ApplyDefaultLeftLayout();
            EditorUtility.SetDirty(layout);
        }

        if (GUILayout.Button("刷新右侧镜像"))
        {
            Undo.RecordObject(layout, "Refresh right player card layout");
            layout.ApplyLayout();
            EditorUtility.SetDirty(layout);
        }
    }
}
