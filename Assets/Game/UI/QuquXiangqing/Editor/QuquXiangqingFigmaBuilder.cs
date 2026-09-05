using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZqyGameJam.UI.QuquXiangqing.Editor
{
    /// <summary>Builds Figma node 10:527 as a standalone, debug-friendly uGUI page.</summary>
    public static class QuquXiangqingFigmaBuilder
    {
        private const string Root = "Assets/Resources/Collection";
        private const string Prefabs = Root + "/Prefabs";
        private const string Parts = Prefabs + "/Parts";
        private const string Textures = Root + "/Textures/Figma";
        private const string Scripts = "Assets/Game/UI/QuquXiangqing/Scripts";
        private const string PagePath = Prefabs + "/详情页.prefab";
        private const string CanvasPath = Prefabs + "/Canvas.prefab";
        private const string ScenePath = "Assets/Scenes/Preview/ququxiangqing.unity";
        private const string ExportPath = Textures + "/QuquXiangqing_10_527.png";
        private const string CricketPath = Textures + "/VioletCricketIllustration.png";
        private const string ViewScriptPath = Scripts + "/QuquXiangqingView.cs";

        // Legacy builder retained for reference; use QuquXiangqingModularFigmaBuilder from the Unity menu.
        public static void Build()
        {
            EnsureFolder("Assets/Game");
            EnsureFolder("Assets/Game/UI");
            EnsureFolder(Root);
            EnsureFolder(Prefabs);
            EnsureFolder(Parts);
            EnsureFolder(Textures);
            EnsureFolder(Scripts);
            AssetDatabase.Refresh();

            Sprite export = PrepareSprite(ExportPath);
            Sprite cricket = PrepareSprite(CricketPath);
            if (export == null) throw new FileNotFoundException("Missing Figma export", ExportPath);
            if (cricket == null) throw new FileNotFoundException("Missing cricket illustration", CricketPath);

            DeleteGeneratedPrefabs();
            EnsureViewScript();

            GameObject background = SavePart(BuildImage("Background", new Vector2(972, 1336), export, false), "Background.prefab");
            GameObject header = SavePart(BuildEmpty("Header", new Vector2(972, 150), new Vector2(0, 520)), "Header.prefab");
            GameObject portrait = SavePart(BuildImage("CricketPortrait", new Vector2(260, 260), cricket, false), "Portrait.prefab");
            GameObject nameTag = SavePart(BuildEmpty("NameTag", new Vector2(240, 70), new Vector2(0, 235)), "NameTag.prefab");
            GameObject stats = SavePart(BuildEmpty("StatsTable", new Vector2(876, 279), new Vector2(0, -210)), "StatsTable.prefab");
            GameObject actions = SaveActionButtons();
            GameObject overlay = SaveInteractionOverlay();

            GameObject canvas = BuildCanvas(background, header, portrait, nameTag, stats, actions, overlay);
            GameObject root = BuildPage(canvas);
            BuildScene();
            AppendBuildSettings(ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PagePath);
            Debug.Log("Built Figma 10:527 standalone page: " + PagePath);
        }

        private static void DeleteGeneratedPrefabs()
        {
            AssetDatabase.DeleteAsset(PagePath);
            AssetDatabase.DeleteAsset(CanvasPath);
            if (AssetDatabase.IsValidFolder(Parts))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Parts }))
                    AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        private static GameObject BuildCanvas(GameObject background, GameObject header, GameObject portrait, GameObject nameTag, GameObject stats, GameObject actions, GameObject overlay)
        {
            GameObject canvasObject = new GameObject("Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(972, 1336);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100;
            canvasObject.AddComponent<GraphicRaycaster>();
            ConfigureCanvas(canvasObject.GetComponent<RectTransform>());

            AddNested(canvasObject.transform, background, Vector2.zero);
            AddNested(canvasObject.transform, header, Vector2.zero);
            // Portrait/name/stats are retained as reusable prefabs; the exported page already contains them.
            AddNested(canvasObject.transform, actions, Vector2.zero);
            AddNested(canvasObject.transform, overlay, Vector2.zero);
            return SavePrefab(canvasObject, CanvasPath);
        }

        private static GameObject BuildPage(GameObject canvas)
        {
            GameObject page = BuildEmpty("详情页", new Vector2(972, 1336), Vector2.zero);
            QuquXiangqingView view = page.AddComponent<QuquXiangqingView>();
            GameObject canvasInstance = AddNested(page.transform, canvas, Vector2.zero);
            view.sellButton = FindButton(canvasInstance.transform, "btn-售卖");
            view.storeButton = FindButton(canvasInstance.transform, "btn-收入背包");
            view.closeButton = FindButton(canvasInstance.transform, "btn-关闭");
            return SavePrefab(page, PagePath);
        }

        private static GameObject SaveActionButtons()
        {
            GameObject root = BuildEmpty("ActionButtonStack", new Vector2(972, 260), new Vector2(0, -468));
            AddNested(root.transform, SaveButton("btn-售卖", "售卖 236", new Vector2(0, 72), new Color(0.18f, 0.11f, 0.09f, 0.02f)), Vector2.zero);
            AddNested(root.transform, SaveButton("btn-收入背包", "收入背包", new Vector2(0, -16), new Color(0.62f, 0.16f, 0.17f, 0.02f)), Vector2.zero);
            AddNested(root.transform, SaveButton("btn-关闭", "关闭", new Vector2(0, -104), new Color(0.18f, 0.11f, 0.09f, 0.02f)), Vector2.zero);
            return SavePart(root, "ActionButtonStack.prefab");
        }

        private static GameObject SaveButton(string name, string label, Vector2 position, Color color)
        {
            GameObject go = BuildEmpty(name, new Vector2(480, 72), position);
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            // Label is invisible by default because the exported Figma image supplies the exact typography.
            Text text = AddText(go.transform, "Label", label, 28, Vector2.zero, new Color(1, 1, 1, 0), TextAnchor.MiddleCenter, new Vector2(480, 72));
            text.raycastTarget = false;
            return SavePart(go, name + ".prefab");
        }

        private static GameObject SaveInteractionOverlay()
        {
            GameObject go = BuildEmpty("InteractionOverlay", new Vector2(972, 1336), Vector2.zero);
            AddNested(go.transform, SaveHitArea("portrait-hit", new Vector2(260, 260), new Vector2(0, 295)), Vector2.zero);
            AddNested(go.transform, SaveHitArea("stats-hit", new Vector2(876, 279), new Vector2(0, -210)), Vector2.zero);
            return SavePart(go, "InteractionOverlay.prefab");
        }

        private static GameObject SaveHitArea(string name, Vector2 size, Vector2 position)
        {
            GameObject go = BuildEmpty(name, size, position);
            Image image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0);
            image.raycastTarget = true;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return SavePart(go, "" + name + ".prefab");
        }

        private static void BuildScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject page = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PagePath)) as GameObject;
            SceneManager.MoveGameObjectToScene(page, SceneManager.GetActiveScene());
            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.06f, 0.04f);
            camera.transform.position = new Vector3(0, 0, -10);
            cameraObject.tag = "MainCamera";            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        }

        private static GameObject BuildImage(string name, Vector2 size, Sprite sprite, bool raycast)
        {
            GameObject go = BuildEmpty(name, size, Vector2.zero);
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = false;
            image.raycastTarget = raycast;
            return go;
        }

        private static GameObject BuildEmpty(string name, Vector2 size, Vector2 position)
        {
            GameObject go = new GameObject(name);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return go;
        }

        private static Text AddText(Transform parent, string name, string value, int size, Vector2 position, Color color, TextAnchor anchor, Vector2 dimensions)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = position;
            Text text = go.AddComponent<Text>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, size);
            return text;
        }

        private static GameObject AddNested(Transform parent, GameObject prefab, Vector2 position)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance != null)
            {
                RectTransform rect = instance.GetComponent<RectTransform>();
                if (rect != null) rect.anchoredPosition = position;
            }
            return instance;
        }

        private static GameObject SavePart(GameObject go, string fileName)
        {
            return SavePrefab(go, Parts + "/" + fileName);
        }

        private static GameObject SavePrefab(GameObject go, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static Button FindButton(Transform root, string name)
        {
            Transform target = root.Find("ActionButtonStack/" + name);
            return target != null ? target.GetComponent<Button>() : null;
        }

        private static void ConfigureCanvas(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Sprite PrepareSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string name = Path.GetFileName(parent == path ? path : path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void AppendBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;
            foreach (EditorBuildSettingsScene scene in scenes) if (scene.path == path) found = true;
            if (!found) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureViewScript()
        {
            if (File.Exists(ViewScriptPath)) return;
            File.WriteAllText(ViewScriptPath,
                "using UnityEngine; using UnityEngine.UI;\n" +
                "namespace ZqyGameJam.UI.QuquXiangqing { public sealed class QuquXiangqingView : MonoBehaviour { " +
                "public Button sellButton; public Button storeButton; public Button closeButton; " +
                "private void Awake(){ Bind(sellButton, \"sell\"); Bind(storeButton, \"store\"); Bind(closeButton, \"close\"); } " +
                "private void Bind(Button b,string action){ if(b!=null) b.onClick.AddListener(()=>Debug.Log(\"详情页 action: \"+action)); } } }\n");
            AssetDatabase.ImportAsset(ViewScriptPath);
        }
    }
}
