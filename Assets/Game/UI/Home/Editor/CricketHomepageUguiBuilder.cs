using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZqyGameJam.UI.Home.Editor
{
    public static class CricketHomepageUguiBuilder
    {
        private const string RootFolder = "Assets/Game/UI/Home/Prefabs";
        private const string PartsFolder = RootFolder + "/Parts";
        private const string PrefabPath = RootFolder + "/CricketHomepage.prefab";
        private const string CanvasPath = RootFolder + "/CricketCanvas.prefab";
        private const string ScenePath = "Assets/Game/UI/Home/Scenes/CricketHomepage.unity";
        private const string BackgroundPath = "Assets/FigmaImport/CricketUI/Textures/village-background.png";
        private const string AvatarPath = "Assets/FigmaImport/CricketUI/Textures/player-profile.png";

        [MenuItem("Tools/Cricket UI/Rebuild Modular Canvas Prefabs")]
        public static void Rebuild()
        {
            EnsureFolder("Assets/Game");
            EnsureFolder("Assets/Game/UI");
            EnsureFolder("Assets/Game/UI/Home");
            EnsureFolder(RootFolder);
            EnsureFolder("Assets/Game/UI/Home/Scenes");
            EnsureFolder(PartsFolder);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(PrefabPath);
            AssetDatabase.DeleteAsset(CanvasPath);
            AssetDatabase.DeleteAsset(PartsFolder);
            EnsureFolder(PartsFolder);
            AssetDatabase.Refresh();

            Sprite background = PrepareSprite(BackgroundPath);
            Sprite avatar = PrepareSprite(AvatarPath);

            GameObject backgroundPrefab = SaveBackgroundPart(background);
            GameObject topShadePrefab = SaveTopShadePart();
            GameObject playerProfilePrefab = SavePlayerProfilePart(avatar);
            GameObject resourcePrefab = SaveResourcePart();
            GameObject attendancePrefab = SaveAttendancePart();

            string[] buttonNames = { "FestivalButton", "CageButton", "ArenaButton", "CollectionButton", "TrainingButton", "GiftButton", "ShopButton", "RankButton", "BattleButton", "ShareButton", "EnterButton" };
            string[] buttonLabels = { "蟋蟀祭", "草笼棚", "角斗场", "收藏谱", "培养室", "礼", "店", "榜", "战", "享", "展开" };
            Vector2[] buttonPositions = {
                new Vector2(-315, 570), new Vector2(315, 570), new Vector2(-235, 155),
                new Vector2(250, -60), new Vector2(-245, -350), new Vector2(-485, 470),
                new Vector2(-485, 320), new Vector2(-485, 170), new Vector2(-485, 20),
                new Vector2(-485, -130), new Vector2(-425, -760)
            };
            Vector2[] buttonSizes = {
                new Vector2(174, 174), new Vector2(174, 174), new Vector2(238, 238),
                new Vector2(210, 210), new Vector2(230, 230), new Vector2(106, 106),
                new Vector2(106, 106), new Vector2(106, 106), new Vector2(106, 106),
                new Vector2(106, 106), new Vector2(170, 170)
            };
            Color[] buttonColors = {
                new Color(0.67f, 0.28f, 0.19f, 0.94f), new Color(0.26f, 0.46f, 0.29f, 0.94f),
                new Color(0.55f, 0.29f, 0.17f, 0.95f), new Color(0.23f, 0.35f, 0.45f, 0.95f),
                new Color(0.36f, 0.43f, 0.22f, 0.95f), new Color(0.55f, 0.27f, 0.23f, 0.95f),
                new Color(0.26f, 0.41f, 0.31f, 0.95f), new Color(0.28f, 0.35f, 0.45f, 0.95f),
                new Color(0.55f, 0.40f, 0.19f, 0.95f), new Color(0.36f, 0.31f, 0.43f, 0.95f),
                new Color(0.78f, 0.39f, 0.20f, 0.98f)
            };
            GameObject[] buttonPrefabs = new GameObject[buttonNames.Length];
            for (int i = 0; i < buttonNames.Length; i++)
            {
                buttonPrefabs[i] = SaveButtonPart(buttonNames[i], buttonLabels[i], buttonPositions[i], buttonSizes[i], buttonColors[i]);
            }

            GameObject sectionPrefab = SaveLabelPart("SectionLabel", "山野入口", 30, new Vector2(0, 360), new Color(0.12f, 0.18f, 0.12f), new Vector2(400, 50));
            GameObject hintPrefab = SaveLabelPart("Hint", "点击任意入口开始探索", 24, new Vector2(80, -835), new Color(0.12f, 0.18f, 0.12f), new Vector2(520, 45));

            GameObject canvasPrefab = BuildCanvasPrefab(backgroundPrefab, topShadePrefab, playerProfilePrefab, resourcePrefab, attendancePrefab, buttonPrefabs, sectionPrefab, hintPrefab);
            BuildRootPrefab(canvasPrefab, buttonNames);
            BuildScene();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Modular Cricket Canvas prefabs rebuilt. Root: " + PrefabPath);
        }

        private static GameObject BuildCanvasPrefab(GameObject background, GameObject topShade, GameObject playerProfile, GameObject resource, GameObject attendance, GameObject[] buttons, GameObject section, GameObject hint)
        {
            GameObject canvasObject = new GameObject("Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            canvasObject.AddComponent<GraphicRaycaster>();
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            AddNested(canvasObject.transform, background);
            AddNested(canvasObject.transform, topShade);
            AddNested(canvasObject.transform, playerProfile);
            AddNested(canvasObject.transform, resource);
            AddNested(canvasObject.transform, attendance);
            AddNested(canvasObject.transform, section);
            AddNested(canvasObject.transform, hint);
            for (int i = 0; i < buttons.Length; i++) AddNested(canvasObject.transform, buttons[i]);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(canvasObject, CanvasPath);
            Object.DestroyImmediate(canvasObject);
            return saved;
        }

        private static void BuildRootPrefab(GameObject canvasPrefab, string[] buttonNames)
        {
            GameObject root = new GameObject("CricketHomepage");
            CricketHomepageView view = root.AddComponent<CricketHomepageView>();
            GameObject canvasInstance = AddNested(root.transform, canvasPrefab);
            Transform canvas = canvasInstance.transform;

            SerializedObject serializedView = new SerializedObject(view);
            SetRef(serializedView, "playerName", FindText(canvas, "CricketPlayerProfile/PlayerName"));
            SetRef(serializedView, "playerPower", FindText(canvas, "CricketPlayerProfile/PlayerPower"));
            SetRef(serializedView, "coins", FindText(canvas, "CricketResourceHud/Coins"));
            SetRef(serializedView, "attendanceTitle", FindText(canvas, "CricketAttendance/AttendanceTitle"));
            SetRef(serializedView, "attendanceTime", FindText(canvas, "CricketAttendance/AttendanceTime"));
            for (int i = 0; i < buttonNames.Length; i++)
            {
                SetRef(serializedView, ToFieldName(buttonNames[i]), FindButton(canvas, "Cricket" + buttonNames[i]));
            }
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildScene()
        {
            GameObject rootInstance = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) as GameObject;
            SceneManager.MoveGameObjectToScene(rootInstance, SceneManager.GetActiveScene());

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.12f, 0.10f);
            camera.transform.position = new Vector3(0, 0, -10);
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
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static GameObject SaveBackgroundPart(Sprite sprite)
        {
            GameObject go = CreateRect("Background", new Vector2(1080, 1920), Vector2.zero);
            Image image = go.AddComponent<Image>();
            image.color = new Color(0.78f, 0.84f, 0.78f, 1f);
            image.sprite = sprite;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return SavePart(go, "CricketBackground.prefab");
        }

        private static GameObject SaveTopShadePart()
        {
            GameObject go = CreateRect("TopShade", new Vector2(1080, 300), new Vector2(0, 810));
            Image image = go.AddComponent<Image>();
            image.color = new Color(0.10f, 0.18f, 0.16f, 0.18f);
            AddText(go.transform, "Title", "蟋蟀祭 · 山野会馆", 42, new Vector2(0, 15), Color.white, TextAnchor.MiddleCenter, new Vector2(760, 80));
            return SavePart(go, "CricketTopShade.prefab");
        }

        private static GameObject SavePlayerProfilePart(Sprite avatar)
        {
            GameObject go = CreateRect("PlayerProfile", new Vector2(330, 132), new Vector2(-330, 825));
            Image panel = go.AddComponent<Image>();
            panel.color = new Color(0.08f, 0.12f, 0.10f, 0.86f);
            AddImage(go.transform, "Avatar", new Vector2(100, 100), new Vector2(-100, 0), Color.white, avatar);
            AddText(go.transform, "PlayerName", "少侠·小明", 30, new Vector2(65, 25), new Color(1f, 0.92f, 0.65f), TextAnchor.MiddleLeft, new Vector2(220, 44));
            AddText(go.transform, "PlayerPower", "最强战力 12888", 22, new Vector2(65, -25), new Color(0.8f, 0.9f, 0.82f), TextAnchor.MiddleLeft, new Vector2(220, 36));
            return SavePart(go, "CricketPlayerProfile.prefab");
        }

        private static GameObject SaveResourcePart()
        {
            GameObject go = CreateRect("ResourceHud", new Vector2(300, 92), new Vector2(350, 835));
            Image panel = go.AddComponent<Image>();
            panel.color = new Color(0.08f, 0.12f, 0.10f, 0.84f);
            AddText(go.transform, "CoinIcon", "铜钱", 22, new Vector2(-75, 0), new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleCenter, new Vector2(90, 40));
            AddText(go.transform, "Coins", "12,580", 28, new Vector2(70, 0), Color.white, TextAnchor.MiddleCenter, new Vector2(130, 44));
            return SavePart(go, "CricketResourceHud.prefab");
        }

        private static GameObject SaveAttendancePart()
        {
            GameObject go = CreateRect("Attendance", new Vector2(330, 120), new Vector2(350, 690));
            Image panel = go.AddComponent<Image>();
            panel.color = new Color(0.12f, 0.16f, 0.13f, 0.86f);
            AddText(go.transform, "AttendanceTitle", "今日活动", 25, new Vector2(0, 25), new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleCenter, new Vector2(300, 38));
            AddText(go.transform, "AttendanceTime", "剩余 02:18:40", 23, new Vector2(0, -22), Color.white, TextAnchor.MiddleCenter, new Vector2(300, 36));
            return SavePart(go, "CricketAttendance.prefab");
        }

        private static GameObject SaveButtonPart(string name, string label, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = CreateRect(name, size, position);
            Image image = go.AddComponent<Image>();
            image.color = color;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(Mathf.Min(color.r + 0.12f, 1f), Mathf.Min(color.g + 0.12f, 1f), Mathf.Min(color.b + 0.12f, 1f), 1f);
            colors.pressedColor = new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, 1f);
            button.colors = colors;
            AddText(go.transform, "Label", label, Mathf.RoundToInt(size.x * 0.16f), Vector2.zero, Color.white, TextAnchor.MiddleCenter, size - new Vector2(18, 18));
            return SavePart(go, "Cricket" + name + ".prefab");
        }

        private static GameObject SaveLabelPart(string name, string value, int size, Vector2 position, Color color, Vector2 dimensions)
        {
            GameObject go = CreateRect(name, dimensions, position);
            AddText(go.transform, "Text", value, size, Vector2.zero, color, TextAnchor.MiddleCenter, dimensions);
            return SavePart(go, "Cricket" + name + ".prefab");
        }

        private static GameObject SavePart(GameObject go, string fileName)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, PartsFolder + "/" + fileName);
            Object.DestroyImmediate(go);
            return saved;
        }

        private static GameObject AddNested(Transform parent, GameObject prefab)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            return instance;
        }

        private static GameObject CreateRect(string name, Vector2 size, Vector2 position)
        {
            GameObject go = new GameObject(name);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return go;
        }

        private static Image AddImage(Transform parent, string name, Vector2 size, Vector2 position, Color color, Sprite sprite)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Text AddText(Transform parent, string name, string value, int size, Vector2 position, Color color, TextAnchor anchor, Vector2 dimensions)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
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

        private static Text FindText(Transform root, string path)
        {
            Transform target = root.Find(path);
            return target != null ? target.GetComponent<Text>() : null;
        }

        private static Button FindButton(Transform root, string name)
        {
            Transform target = root.Find(name);
            return target != null ? target.GetComponent<Button>() : null;
        }

        private static string ToFieldName(string buttonName)
        {
            return char.ToLowerInvariant(buttonName[0]) + buttonName.Substring(1);
        }

        private static void SetRef(SerializedObject obj, string property, Object value)
        {
            SerializedProperty prop = obj.FindProperty(property);
            if (prop != null) prop.objectReferenceValue = value;
        }

        private static Sprite PrepareSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
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
