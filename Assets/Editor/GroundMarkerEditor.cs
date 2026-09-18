using UnityEditor;
using UnityEngine;

namespace DouQuqu.Editor
{
    [CustomEditor(typeof(GroundMarker))]
    public sealed class GroundMarkerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (!GUILayout.Button("Bake 到 Fill / Ring / Shadow")) return;

            GroundMarker marker = (GroundMarker)target;
            Undo.RegisterFullObjectHierarchyUndo(marker.gameObject, "Bake Ground Marker");
            marker.Bake();
            EditorUtility.SetDirty(marker);
            foreach (Transform child in marker.GetComponentsInChildren<Transform>(true))
                EditorUtility.SetDirty(child.gameObject);
        }
    }
}
