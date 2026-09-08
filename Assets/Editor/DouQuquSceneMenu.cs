using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DouQuqu.Editor
{
    /// <summary>运行时只留登录、大厅、对局。育虫盘是大厅 Prefab，不再单独建 Scene。</summary>
    public static class DouQuquSceneMenu
    {
        public const string BattlePath = "Assets/Scenes/Demo.unity";
        public const string LobbyPath = "Assets/Scenes/MainMenu.unity";
        private const string MainMenuPrefabPath = "Assets/Resources/MainMenu/Prefabs/DouQuquMainMenu.prefab";
        private const string MergePrefabPath = "Assets/Resources/Merge/Prefabs/Canvas.prefab";
        private const string BattleEntrancePrefabPath = "Assets/Resources/BattleEntrance/Prefabs/BattleEntrance.prefab";
        private const string HeroSelectionPrefabPath = "Assets/Resources/HeroSelection/Prefabs/FigmaImport_cricket-battle-royale_55_4.prefab";

        [MenuItem("DouQuqu/Open Battle Scene")]
        public static void OpenBattleScene()
        {
            OpenScene(BattlePath);
        }

        [MenuItem("DouQuqu/Open Lobby Scene")]
        public static void OpenLobbyScene()
        {
            OpenScene(LobbyPath);
        }

        [MenuItem("DouQuqu/Ping MainMenu Prefab")]
        public static void PingMainMenuPrefab()
        {
            PingPrefab(MainMenuPrefabPath, "村子主页");
        }

        [MenuItem("DouQuqu/Ping Merge Prefab")]
        public static void PingMergePrefab()
        {
            PingPrefab(MergePrefabPath, "育虫盘");
        }

        [MenuItem("DouQuqu/Ping BattleEntrance Prefab")]
        public static void PingBattleEntrancePrefab()
        {
            PingPrefab(BattleEntrancePrefabPath, "进战页");
        }

        [MenuItem("DouQuqu/Ping HeroSelection Prefab")]
        public static void PingHeroSelectionPrefab()
        {
            PingPrefab(HeroSelectionPrefabPath, "选虫页");
        }

        private static void PingPrefab(string path, string label)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("[DouQuqu] 找不到" + label + " Prefab：" + path);
                return;
            }
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        private static void OpenScene(string path)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(path);
        }
    }
}
