using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZqyGameJam.UI.BreedingBoard.Editor
{
    /// <summary>Builds the Figma node 91:8 (育虫盘) as modular uGUI prefabs.</summary>
    public static class BreedingBoardFigmaBuilder
    {
        private const string Root = "Assets/Game/UI/BreedingBoard";
        private const string Prefabs = Root + "/Prefabs";
        private const string Parts = Prefabs + "/Parts";
        private const string Scenes = Root + "/Scenes";
        private const string TexturePath = Root + "/Textures/Figma/BreedingBoard_91_8.png";
        private const string PagePath = Prefabs + "/BreedingBoard.prefab";
        private const string CanvasPath = Prefabs + "/BreedingBoardCanvas.prefab";
        private const string BackgroundPath = Parts + "/BreedingBoard_Background.prefab";
        private const string OverlayPath = Parts + "/BreedingBoard_InteractionOverlay.prefab";
        private const string ScenePath = Scenes + "/BreedingBoard.unity";

        private readonly struct ButtonSpec
        {
            public readonly string Name;
            public readonly Vector2 Position;
            public readonly Vector2 Size;

            public ButtonSpec(string name, Vector2 position, Vector2 size)
            {
                Name = name;
                Position = position;
                Size = size;
            }
        }

        [MenuItem("Tools/Cricket UI/Build Breeding Board (Figma 91:8)")]
        public static void Build()
        {
            EnsureFolder("Assets/Game");
            EnsureFolder("Assets/Game/UI");
            EnsureFolder(Root);
            EnsureFolder(Prefabs);
            EnsureFolder(Parts);
            EnsureFolder(Scenes);
            EnsureFolder(Root + "/Textures");
            EnsureFolder(Root + "/Textures/Figma");

            AssetDatabase.Refresh();
            Sprite sprite = PrepareSprite(TexturePath);
            if (sprite == null) throw new FileNotFoundException("Missing Figma export", TexturePath);

            AssetDatabase.DeleteAsset(PagePath);
            AssetDatabase.DeleteAsset(CanvasPath);
            AssetDatabase.DeleteAsset(BackgroundPath);
            AssetDatabase.DeleteAsset(OverlayPath);
            AssetDatabase.DeleteAsset(ScenePath);

            GameObject background = SaveBackground(sprite);
            GameObject overlay = SaveInteractionOverlay();
            GameObject canvas = SaveCanvas(background, overlay);
            SavePage(canvas);
            SaveScene();
            AppendBuildSettings(ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PagePath);
            Debug.Log("Built Figma 91:8 BreedingBoard prefabs and scene.");
        }

        private static GameObject SaveBackground(Sprite sprite)
        {
            GameObject go = CreateRect("BreedingBoard_Background", new Vector2(1080f, 1920f), Vector2.zero);
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return SavePrefab(go, BackgroundPath);
        }

        private static GameObject SaveInteractionOverlay()
        {
            GameObject go = CreateRect("BreedingBoard_InteractionOverlay", new Vector2(1080f, 1920f), Vector2.zero);
            foreach (ButtonSpec spec in BuildButtonSpecs()) AddButton(go.transform, spec);
            return SavePrefab(go, OverlayPath);
        }

        private static IEnumerable<ButtonSpec> BuildButtonSpecs()
        {
            // Coordinates are converted from Figma's 1080x1920 top-left space to
            // a centered Unity Canvas (x right, y up).
            yield return new ButtonSpec("RulesButton", new Vector2(-413f, -656f), new Vector2(130f, 105f));
            yield return new ButtonSpec("BackButton", new Vector2(-441f, 848f), new Vector2(150f, 150f));
            yield return new ButtonSpec("BattleTabButton", new Vector2(-218.5f, 860f), new Vector2(219f, 150f));
            yield return new ButtonSpec("BreedingTabButton", new Vector2(57.5f, 860f), new Vector2(219f, 150f));
            yield return new ButtonSpec("RegistryTabButton", new Vector2(342.5f, 860f), new Vector2(219f, 150f));

            int index = 1;
            float[] xs = { -349.5f, -116.5f, 116.5f, 349.5f };
            float[] ys = { 547.5f, 314.5f, 81.5f, -151.5f, -384.5f };
            foreach (float y in ys)
                foreach (float x in xs)
                    yield return new ButtonSpec("BoardCell" + index++, new Vector2(x, y), new Vector2(213f, 213f));
        }

        private static GameObject SaveCanvas(GameObject background, GameObject overlay)
        {
            GameObject go = new GameObject("BreedingBoardCanvas");
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            go.AddComponent<GraphicRaycaster>();
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            PrefabUtility.InstantiatePrefab(background, go.transform);
            PrefabUtility.InstantiatePrefab(overlay, go.transform);
            return SavePrefab(go, CanvasPath);
        }

        private static void SavePage(GameObject canvas)
        {
            GameObject page = new GameObject("BreedingBoard");
            PrefabUtility.InstantiatePrefab(canvas, page.transform);
            SavePrefab(page, PagePath);
        }

        private static void SaveScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject page = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PagePath)) as GameObject;
            SceneManager.MoveGameObjectToScene(page, SceneManager.GetActiveScene());

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.04f, 0.06f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.tag = "MainCamera";

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        }

        private static void AppendBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool exists = false;
            foreach (EditorBuildSettingsScene scene in scenes)
                if (scene.path == scenePath) exists = true;
            if (!exists) scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void AddButton(Transform parent, ButtonSpec spec)
        {
            GameObject go = CreateRect(spec.Name, spec.Size, spec.Position);
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
        }

        private static GameObject CreateRect(string name, Vector2 size, Vector2 position)
        {
            GameObject go = new GameObject(name);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return go;
        }

        private static GameObject SavePrefab(GameObject go, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        private static Sprite PrepareSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
