using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 战斗场景和育虫盘场景分开打开。育虫盘场景若不存在，菜单会按当前 HUD 资源创建一份。
    /// </summary>
    public static class DouQuquSceneMenu
    {
        public const string BattlePath = "Assets/Scenes/DouQuquDemo.unity";
        public const string MergePath = "Assets/Scenes/DouQuquMerge.unity";
        private const string MergeUxmlPath = "Assets/UI/DouQuquMergeHUD.uxml";
        private const string PanelSettingsPath = "Assets/FigmaImport/CricketUI/UI/CricketPanelSettings.asset";

        [MenuItem("DouQuqu/Open Battle Scene")]
        public static void OpenBattleScene()
        {
            OpenScene(BattlePath);
        }

        [MenuItem("DouQuqu/Open Merge Scene")]
        public static void OpenMergeScene()
        {
            EnsureMergeScene(false);
            OpenScene(MergePath);
        }

        [MenuItem("DouQuqu/Create Merge Scene")]
        public static void CreateMergeScene()
        {
            EnsureMergeScene(true);
            OpenScene(MergePath);
        }

        private static void OpenScene(string path)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(path);
        }

        private static void EnsureMergeScene(bool forceRecreate)
        {
            if (!forceRecreate && System.IO.File.Exists(MergePath)) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Camera camera = Object.FindObjectOfType<Camera>();
            if (camera != null)
            {
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.03f, 0.06f, 0.11f, 1f);
                camera.transform.SetPositionAndRotation(new Vector3(0f, 1f, -10f), Quaternion.identity);
            }

            GameObject root = new GameObject("DouQuquMergeRuntime");
            UIDocument document = root.AddComponent<UIDocument>();
            DouQuquMergeBoard board = root.AddComponent<DouQuquMergeBoard>();
            DouQuquMergeBoardView view = root.AddComponent<DouQuquMergeBoardView>();

            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MergeUxmlPath);
            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            SerializedObject documentSo = new SerializedObject(document);
            SerializedProperty source = documentSo.FindProperty("sourceAsset");
            SerializedProperty panelProp = documentSo.FindProperty("m_PanelSettings");
            if (source != null) source.objectReferenceValue = uxml;
            if (panelProp != null) panelProp.objectReferenceValue = panel;
            documentSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject viewSo = new SerializedObject(view);
            SetObject(viewSo, "board", board);
            SetObject(viewSo, "uiDocument", document);
            SerializedProperty autoReset = viewSo.FindProperty("autoReset");
            if (autoReset != null) autoReset.boolValue = true;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MergePath);
            AddToBuildSettings(MergePath);
            AssetDatabase.Refresh();
            Debug.Log("[DouQuqu] 已创建育虫盘场景：" + MergePath);
        }

        private static void SetObject(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }

        private static void AddToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
                if (scenes[i].path == scenePath) return;

            EditorBuildSettingsScene[] next = new EditorBuildSettingsScene[scenes.Length + 1];
            for (int i = 0; i < scenes.Length; i++) next[i] = scenes[i];
            next[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = next;
        }
    }
}
