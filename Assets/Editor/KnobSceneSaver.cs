using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DouQuqu.Editor
{
    /// <summary>退出 Play 后把局内记下的旋钮写回 Demo 场景 MatchController Inspector。</summary>
    [InitializeOnLoad]
    internal static class KnobSceneSaver
    {
        private const string DemoPath = SceneMenu.BattlePath;

        static KnobSceneSaver()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            if (PlayerPrefs.GetInt(KnobSaveKeys.Dirty, 0) != 1) return;
            EditorApplication.delayCall += ApplyPending;
        }

        [MenuItem("DouQuqu/Apply Pending Match Knobs")]
        public static void ApplyPending()
        {
            if (PlayerPrefs.GetInt(KnobSaveKeys.Dirty, 0) != 1)
            {
                Debug.Log("[DouQuqu] 没有待写入的对局旋钮。");
                return;
            }

            string json = PlayerPrefs.GetString(KnobSaveKeys.Json, string.Empty);
            PlayerPrefs.DeleteKey(KnobSaveKeys.Dirty);
            PlayerPrefs.DeleteKey(KnobSaveKeys.Json);
            PlayerPrefs.Save();
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[DouQuqu] 待写入的对局旋钮是空的。");
                return;
            }

            Scene demo = EditorSceneManager.GetSceneByPath(DemoPath);
            bool openedHere = false;
            if (!demo.IsValid() || !demo.isLoaded)
            {
                demo = EditorSceneManager.OpenScene(DemoPath, OpenSceneMode.Additive);
                openedHere = true;
            }

            MatchController controller = FindController(demo);
            if (controller == null)
            {
                Debug.LogError("[DouQuqu] Demo 场景里没有 MatchController，旋钮未写入。");
                if (openedHere) EditorSceneManager.CloseScene(demo, true);
                return;
            }

            Undo.RecordObject(controller, "覆写对局旋钮");
            controller.ApplyKnobsJson(json);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(demo);
            if (!EditorSceneManager.SaveScene(demo))
            {
                Debug.LogError("[DouQuqu] 保存 Demo 场景失败。");
                return;
            }

            MatchKnobs knobs = controller.Knobs;
            Debug.Log(string.Format(
                "[DouQuqu] 已覆写 Demo 对局旋钮并保存场景。蓄满时间={0} 点跳距离={1} 距离比={2}",
                knobs != null ? knobs.tChargeMax : 0f,
                knobs != null ? knobs.dMin : 0f,
                knobs != null ? knobs.jumpDistRatio : 0f));

            if (openedHere)
                EditorSceneManager.CloseScene(demo, true);
        }

        private static MatchController FindController(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                MatchController found = roots[i].GetComponentInChildren<MatchController>(true);
                if (found != null) return found;
            }

            return null;
        }
    }
}
