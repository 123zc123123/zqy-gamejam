using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FigmaPlayerCardLayout))]
public class FigmaPlayerCardLayoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var layout = (FigmaPlayerCardLayout)target;
        EditorGUILayout.HelpBox(
            "左边：直接在 Scene / Prefab 里拖 AvatarFrame、InfoColumn、名字、积分和三只蛐蛐。\n" +
            "右边：勾选 Right Side，内部位置按左边预制体镜像，不要手改。",
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
