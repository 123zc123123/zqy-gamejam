using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZqyGameJam.UI.AdditionalScreens.Editor
{
    public static class CricketAdditionalScreensBuilder
    {
        private const string Root = "Assets/Game/UI/AdditionalScreens";
        private const string Prefabs = Root + "/Prefabs";
        private const string Parts = Prefabs + "/Parts";
        private const string Scenes = Root + "/Scenes";
        private const string Textures = Root + "/Textures/Figma";

        private sealed class ButtonSpec
        {
            public readonly string Name;
            public readonly Vector2 Position;
            public readonly Vector2 Size;
            public ButtonSpec(string name, float x, float y, float width, float height)
            { Name = name; Position = new Vector2(x, y); Size = new Vector2(width, height); }
        }

        private sealed class RegionSpec
        {
            public readonly string Name;
            public readonly string Texture;
            public readonly Vector2 Size;
            public readonly Vector2 Position;
            public readonly ButtonSpec[] Buttons;
            public RegionSpec(string name, string texture, float width, float height, float x, float y, params ButtonSpec[] buttons)
            { Name = name; Texture = texture; Size = new Vector2(width, height); Position = new Vector2(x, y); Buttons = buttons; }
        }

[MenuItem("Tools/Cricket UI/Build Figma Screen Prefabs")]
        public static void Build()
        {
            EnsureFolder("Assets/Game"); EnsureFolder("Assets/Game/UI"); EnsureFolder(Root);
            EnsureFolder(Prefabs); EnsureFolder(Parts); EnsureFolder(Scenes);
            EnsureFolder(Root + "/Textures"); EnsureFolder(Textures); AssetDatabase.Refresh();

            // Delete each root before its nested canvas/parts, preventing transient
            // missing-nested-prefab imports while rebuilding existing pages.
            foreach (string id in new[] { "Screen10_593", "Screen63_5", "Screen10_368" })
            {
                AssetDatabase.DeleteAsset(Prefabs + "/" + id + ".prefab");
                AssetDatabase.DeleteAsset(Prefabs + "/" + id + "Canvas.prefab");
            }
            DeleteLegacyScreen10368Parts();

            BuildPage("Screen10_593", CreateEvent593Regions());
            BuildPage("Screen63_5", CreateBattle63Regions());
            BuildPage("Screen10_368", CreateRegistry368Regions());
            UpdateBuildSettings();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Screen10_593.prefab");
            Debug.Log("Figma uGUI screens built as peer root prefabs: Screen10_593, Screen63_5, Screen10_368.");
        }

        private static RegionSpec[] CreateEvent593Regions()
        {
            return new[] {
                Region("Background", "Event593_Background.png", 1080, 1920, 0, 0),
                Region("UserHud", "Event593_UserHud.png", 972, 100, 0, 856),
                Region("EventTitle", "Event593_Title.png", 972, 140, 0, 712),
                Region("Progress", "Event593_Progress.png", 651, 110, -22.5f, 563),
                Region("Rewards", "Event593_Rewards.png", 814, 195, -2, -490.5f),
                Region("Actions", "Event593_Actions.png", 972, 97, 8, -678.5f,
                    Button("RulesButton", -421, -4, 130, 105), Button("StartMatchButton", 18, -4, 282, 105), Button("RankingButton", 439, -4, 94, 89)),
                Region("BottomCarousel", "Event593_Carousel.png", 1080, 200, 0, -860,
                    Button("BattleTabButton", -218.5f, 0, 219, 150), Button("RaiseTabButton", 57.5f, 0, 219, 150), Button("RegistryTabButton", 342.5f, 0, 219, 150))
            };
        }

        private static RegionSpec[] CreateBattle63Regions()
        {
            return new[] {
                Region("Background", "Battle63_Background.png", 1080, 1920, 0, 0),
                Region("TopBar", "Battle63_TopBar.png", 1080, 140, 0, 890,
                    Button("BackButton", -454, -20, 64, 64), Button("SettingsButton", 454, -20, 64, 64)),
                Region("Player2Zone", "Battle63_Player2.png", 430, 625, -271, 487.5f),
                Region("Player3Zone", "Battle63_Player3.png", 430, 615, 271, 492.5f),
                Region("ArenaRing", "Battle63_ArenaRing.png", 486, 475, -91, 161.5f),
                Region("Player4Zone", "Battle63_Player4.png", 430, 571, -271, -255.5f),
                Region("MyLineupZone", "Battle63_MyLineup.png", 430, 575, 271, -253.5f),
                Region("SelectionDock", "Battle63_SelectionDock.png", 1080, 500, 0, -710,
                    Button("CricketCard1Button", -357, 56, 190, 242), Button("CricketCard2Button", -155, 56, 190, 242),
                    Button("CricketCard3Button", 47, 56, 190, 242), Button("CricketCard4Button", 249, 56, 190, 242),
                    Button("PreviousCricketButton", -492, 56, 48, 48), Button("NextCricketButton", 492, 56, 48, 48),
                    Button("ExitRoomButton", -264.5f, -121, 503, 80), Button("ReadyButton", 263.5f, -121, 505, 80))
            };
        }

        private static RegionSpec[] CreateRegistry368Regions()
        {
            var catalogButtons = new List<ButtonSpec>(); float[] xs = { -322, 0, 322 }; float[] ys = { 464, 208, -48 }; int index = 1;
            for (int row = 0; row < ys.Length; row++) for (int column = 0; column < xs.Length; column++)
                catalogButtons.Add(Button("CricketCell" + index++ + "Button", xs[column], ys[row], 280, 224));
            catalogButtons.Add(Button("PreviousPageButton", -430, -533, 64, 64));
            catalogButtons.Add(Button("NextPageButton", 430, -533, 64, 64));
            return new[] {
                Region("Background", "Registry368_Background.png", 1080, 1920, 0, 0),
                Region("TopBar", "Registry368_TopBar.png", 972, 120, 0, 846, Button("BackButton", -450, 0, 72, 72)),
                Region("FeaturedCricket", "Registry368_Featured.png", 972, 268, 0, 620),
                Region("FilterSortBar", "Registry368_Filter.png", 972, 80, 0, 414,
                    Button("SortButton", -390.5f, 0, 159, 54), Button("DirectoryButton", 367.5f, 0.5f, 63, 45), Button("PawnshopButton", 438.5f, 0, 63, 45)),
                Region("Catalog", "Registry368_Catalog.png", 972, 1200, 0, -258, catalogButtons.ToArray())
            };
        }

        private static RegionSpec Region(string name, string texture, float width, float height, float x, float y, params ButtonSpec[] buttons)
        { return new RegionSpec(name, texture, width, height, x, y, buttons); }
        private static ButtonSpec Button(string name, float x, float y, float width, float height)
        { return new ButtonSpec(name, x, y, width, height); }

private static void BuildPage(string id, RegionSpec[] regions)
        {
            string canvasPath = Prefabs + "/" + id + "Canvas.prefab", pagePath = Prefabs + "/" + id + ".prefab", scenePath = Scenes + "/" + id + ".unity";
            AssetDatabase.DeleteAsset(pagePath);
            AssetDatabase.DeleteAsset(canvasPath);
            AssetDatabase.DeleteAsset(scenePath);
            var regionPrefabs = new List<GameObject>();
            foreach (RegionSpec spec in regions)
            {
                string partPath = Parts + "/" + id + "_" + spec.Name + ".prefab"; AssetDatabase.DeleteAsset(partPath);
                Sprite sprite = PrepareSprite(Textures + "/" + spec.Texture);
                if (sprite == null) throw new FileNotFoundException("Missing Figma texture", Textures + "/" + spec.Texture);
                regionPrefabs.Add(SaveRegion(id, spec, sprite, partPath));
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject canvasObject = new GameObject(id + "Canvas"); Canvas canvas = canvasObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0.5f; scaler.referencePixelsPerUnit = 100;
            canvasObject.AddComponent<GraphicRaycaster>(); RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero; canvasRect.anchorMax = Vector2.one; canvasRect.offsetMin = Vector2.zero; canvasRect.offsetMax = Vector2.zero;
            foreach (GameObject regionPrefab in regionPrefabs) PrefabUtility.InstantiatePrefab(regionPrefab, canvasObject.transform);
            GameObject savedCanvas = PrefabUtility.SaveAsPrefabAsset(canvasObject, canvasPath); Object.DestroyImmediate(canvasObject);
            GameObject page = new GameObject(id); PrefabUtility.InstantiatePrefab(savedCanvas, page.transform); PrefabUtility.SaveAsPrefabAsset(page, pagePath); Object.DestroyImmediate(page);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject pageInstance = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(pagePath)) as GameObject;
            SceneManager.MoveGameObjectToScene(pageInstance, SceneManager.GetActiveScene()); CreateSceneSupport(); EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
        }

        private static GameObject SaveRegion(string pageId, RegionSpec spec, Sprite sprite, string path)
        {
            GameObject region = CreateRect(pageId + "_" + spec.Name, spec.Size, spec.Position); Image image = region.AddComponent<Image>();
            image.sprite = sprite; image.color = Color.white; image.preserveAspect = false; image.raycastTarget = false;
            foreach (ButtonSpec buttonSpec in spec.Buttons) AddButton(region.transform, buttonSpec);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(region, path); Object.DestroyImmediate(region); return saved;
        }

        private static void AddButton(Transform parent, ButtonSpec spec)
        {
            GameObject go = CreateRect(spec.Name, spec.Size, spec.Position); go.transform.SetParent(parent, false);
            Image target = go.AddComponent<Image>(); target.color = new Color(1, 1, 1, 0); target.raycastTarget = true;
            Button button = go.AddComponent<Button>(); button.targetGraphic = target; Navigation navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        }

        private static GameObject CreateRect(string name, Vector2 size, Vector2 position)
        {
            GameObject go = new GameObject(name); RectTransform rect = go.AddComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f); rect.sizeDelta = size; rect.anchoredPosition = position; return go;
        }

        private static Sprite PrepareSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter; if (importer == null) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void CreateSceneSupport()
        {
            GameObject cameraObject = new GameObject("Main Camera"); Camera camera = cameraObject.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black; camera.transform.position = new Vector3(0, 0, -10); cameraObject.tag = "MainCamera";
            GameObject lightObject = new GameObject("Directional Light"); Light light = lightObject.AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 1; lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);
            GameObject eventSystem = new GameObject("EventSystem"); eventSystem.AddComponent<EventSystem>(); eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static void DeleteLegacyScreen10368Parts()
        {
            string[] legacy = { "Background", "Header", "Footer", "BackButton", "CardA", "CardB", "CardC", "PrimaryButton" };
            foreach (string suffix in legacy) AssetDatabase.DeleteAsset(Parts + "/Screen10_368" + suffix + ".prefab");
        }

        private static void UpdateBuildSettings()
        {
            string[] paths = { "Assets/Game/UI/Home/Scenes/CricketHomepage.unity", Scenes + "/Screen10_511.unity", Scenes + "/Screen10_593.unity", Scenes + "/Screen63_5.unity", Scenes + "/Screen10_368.unity" };
            var scenes = new List<EditorBuildSettingsScene>(); foreach (string path in paths) if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return; string parent = Path.GetDirectoryName(path).Replace("\\", "/"), name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent, name);
        }
    }
}
