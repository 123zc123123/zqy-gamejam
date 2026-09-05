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
    /// <summary>Rebuilds Figma node 91:8 as a faithful, component-level uGUI prefab hierarchy.</summary>
    public static class BreedingBoardFigmaBuilder
    {
        const string Root = "Assets/Resources/Merge";
        const string Prefabs = Root + "/Prefabs";
        const string Parts = Prefabs + "/Parts";
        const string Scenes = "Assets/Game/UI/BreedingBoard/Scenes";
        const string Textures = Root + "/Textures/Figma";
        const string PagePath = Prefabs + "/育虫盘.prefab";
        const string CanvasPath = Prefabs + "/Canvas.prefab";
        const string ScenePath = Scenes + "/BreedingBoard.unity";
        const string BackgroundTexture = Textures + "/BreedingBoard_Background_108_93.png";
        const string CoinTexture = Textures + "/BreedingBoard_Coins.png";
        static readonly Color Gold = new Color(0.898f, 0.768f, 0.561f, 1f);
        static readonly Color GoldLine = new Color(0.7725f, 0.6275f, 0.349f, 1f);

        [MenuItem("Tools/Cricket UI/Rebuild Breeding Board (Figma 91:8, Modular)")]
        public static void Build()
        {
            EnsureFolder("Assets/Game"); EnsureFolder("Assets/Game/UI"); EnsureFolder(Root);
            EnsureFolder(Prefabs); EnsureFolder(Parts); EnsureFolder(Scenes); EnsureFolder(Root + "/Textures"); EnsureFolder(Textures);
            AssetDatabase.Refresh();
            DeleteOldAssets();
            Sprite background = PrepareSprite(BackgroundTexture);
            Sprite coin = PrepareSprite(CoinTexture);
            if (background == null) throw new FileNotFoundException("Missing Figma background export", BackgroundTexture);
            if (coin == null) throw new FileNotFoundException("Missing Figma coin export", CoinTexture);

            GameObject backgroundPrefab = BuildImagePart("育虫盘底图1", new Vector2(1080,1920), Color.white, background);
            GameObject titleText = BuildTextPart("banner-text", "育虫盘", new Vector2(144,73), 48, Gold, TextAnchor.MiddleCenter);
            GameObject titlePanel = BuildPanelPart("event-title-panel", new Vector2(390,105), new Vector2(0,766.5f), new Color(0.11f,0.11f,0.14f,0.82f), GoldLine, 2, false);
            AddNested(titlePanel, titleText, new Vector2(0,0));
            titlePanel = SaveExisting(titlePanel, Parts + "/EventTitlePanel.prefab");

            GameObject rulesLabel = BuildTextPart("玩法规则", "玩法规则", new Vector2(80,30), 20, Gold, TextAnchor.MiddleCenter);
            GameObject fileIcon = BuildTextPart("file-text", "▤", new Vector2(53,49), 34, Gold, TextAnchor.MiddleCenter);
            GameObject rules = BuildButtonPart("btn-left-rules", new Vector2(130,105), new Color(0,0,0,0.82f), GoldLine);
            AddNested(rules, fileIcon, new Vector2(-0.5f,19));
            AddNested(rules, rulesLabel, new Vector2(0,-25.5f));
            rules = SaveExisting(rules, Parts + "/RulesButton.prefab");

            GameObject ellipse2 = BuildImagePart("Ellipse 2", new Vector2(180,180), Color.white, PrepareSprite(Textures + "/Ellipse2.png")); ellipse2 = SaveExisting(ellipse2, Parts + "/ArenaRing.prefab");
            GameObject arenaCountText = BuildTextPart("99", "99", new Vector2(44,55), 36, Color.white, TextAnchor.MiddleCenter);
            GameObject arenaCount = BuildImagePart("ArenaStatus", new Vector2(90,90), Color.white, PrepareSprite(Textures + "/Ellipse3.png"));
            if (arenaCount == null) arenaCount = BuildPanelPart("Frame 2", new Vector2(90,90), Vector2.zero, new Color(0.12f,0.12f,0.16f,0.86f), GoldLine, 1, false);
            AddNested(arenaCount, arenaCountText, new Vector2(0,0));
            arenaCount = SaveExisting(arenaCount, Parts + "/ArenaStatus.prefab");

            GameObject backpackLabel = BuildTextPart("背包", "背包", new Vector2(80,61), 40, Gold, TextAnchor.MiddleCenter);
            GameObject backpack = BuildButtonPart("btn-left-rules", new Vector2(138,106), new Color(0,0,0,0.82f), GoldLine);
            AddNested(backpack, backpackLabel, Vector2.zero);
            backpack = SaveExisting(backpack, Parts + "/BackpackButton.prefab");

            GameObject actions = BuildEmptyPart("main-event-actions", new Vector2(972,208), new Vector2(8,656));
            AddNested(actions, rules, new Vector2(-421,0.5f));
            AddNested(actions, ellipse2, new Vector2(-29,-24));
            AddNested(actions, arenaCount, new Vector2(61,34));
            AddNested(actions, backpack, new Vector2(371,-5));
            actions = SaveExisting(actions, Parts + "/MainEventActions.prefab");

            GameObject boardBase = BuildPanelPart("棋盘底", new Vector2(952,1193), Vector2.zero, new Color(0.824f,0.769f,0.522f,0.7f), new Color(0.55f,0.4f,0.26f,0.35f), 3, false);
            boardBase = SaveExisting(boardBase, Parts + "/BoardBase.prefab");
            GameObject cellPrefab = BuildPanelPart("Cell", new Vector2(213,213), Vector2.zero, new Color(1f,0.9221698f,0.8443396f,1f), new Color(0.9528302f,0.87595683f,0.8224902f,1f), 2, true);
            cellPrefab = SaveExisting(cellPrefab, Parts + "/Cell.prefab");
            GameObject board = BuildEmptyPart("棋盘", new Vector2(912,1145), new Vector2(0,81.5f));
            AddNested(board, boardBase, Vector2.zero);
            float[] xs = {-349.5f,-116.5f,116.5f,349.5f};
            float[] ys = {466,233,0,-233,-466};
            int cell = 1;
            foreach (float y in ys) foreach (float x in xs)
            {
                AddNested(board, cellPrefab, new Vector2(x,y));
                board.transform.GetChild(board.transform.childCount - 1).name = "Cell " + cell;
                cell++;
            }
            board = SaveExisting(board, Parts + "/Board.prefab");

            GameObject backIcon = BuildImagePart("返回icon", new Vector2(150,150), Color.white, PrepareSprite(Textures + "/BackCircle.png"));
            Color arrowColor = new Color(0.384f, 0.380f, 0.20f, 1f);
            GameObject arrow = BuildEmptyPart("Arrow", new Vector2(110,110), Vector2.zero);
            GameObject arrowShaft = BuildPanelPart("ArrowShaft", new Vector2(96,18), Vector2.zero, arrowColor, Color.clear, 0, false);
            GameObject arrowUpper = BuildPanelPart("ArrowUpper", new Vector2(66,18), Vector2.zero, arrowColor, Color.clear, 0, false);
            GameObject arrowLower = BuildPanelPart("ArrowLower", new Vector2(66,18), Vector2.zero, arrowColor, Color.clear, 0, false);
            arrowUpper.transform.localRotation = Quaternion.Euler(0,0,45);
            arrowLower.transform.localRotation = Quaternion.Euler(0,0,-45);
            AddNested(arrow, arrowShaft, new Vector2(10,0));
            AddNested(arrow, arrowUpper, new Vector2(-25,23));
            AddNested(arrow, arrowLower, new Vector2(-25,-23));
            AddNested(backIcon, arrow, Vector2.zero);
            Image backImage = backIcon != null ? backIcon.GetComponent<Image>() : null;
            if (backImage != null) backImage.raycastTarget = true;
            if (backImage != null) { Button backButton = backIcon.AddComponent<Button>(); backButton.targetGraphic = backImage; Navigation nav = backButton.navigation; nav.mode = Navigation.Mode.None; backButton.navigation = nav; }
            backIcon = SaveExisting(backIcon, Parts + "/BackIcon.prefab");
            GameObject tabPrefab = BuildBottomNavTab();
            GameObject carousel = BuildPanelPart("bottom-event-carousel", new Vector2(1080,200), new Vector2(0,-860), new Color(0.624f,0.604f,0.431f,1), Color.clear, 0, false);
            AddNested(carousel, backIcon, new Vector2(-441,12));
            BuildCarouselTabs(carousel, tabPrefab);
            if (carousel.GetComponent<DouQuqu.DouQuquBottomNavBar>() == null)
                carousel.AddComponent<DouQuqu.DouQuquBottomNavBar>();
            carousel = SaveExisting(carousel, Parts + "/BottomEventCarousel.prefab");

            GameObject goldIcon = BuildImagePart("Coins", new Vector2(38,38), Color.white, coin);
            GameObject amount = BuildTextPart("18,450", "18,450", new Vector2(90,32), 20, Color.white, TextAnchor.MiddleCenter);
            GameObject gold = BuildPanelPart("GoldDisplay", new Vector2(172,58), new Vector2(381,832), new Color(0,0,0,0.5f), Color.clear, 0, false);
            AddNested(gold, goldIcon, new Vector2(-51,-1)); AddNested(gold, amount, new Vector2(19,-1));
            gold = SaveExisting(gold, Parts + "/GoldDisplay.prefab");

            GameObject canvas = BuildEmptyPart("Canvas", new Vector2(1080,1920), Vector2.zero);
            Canvas c = canvas.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvas.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080,1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 1f; scaler.referencePixelsPerUnit = 100;
            canvas.AddComponent<GraphicRaycaster>(); ConfigureCanvas(canvas.GetComponent<RectTransform>());
            AddNested(canvas, backgroundPrefab, Vector2.zero);
            AddNested(canvas, titlePanel, new Vector2(0,766.5f));
            AddNested(canvas, actions, new Vector2(8,656));
            AddNested(canvas, carousel, new Vector2(0,-860));
            AddNested(canvas, board, new Vector2(0,81.5f));
            AddNested(canvas, gold, new Vector2(381,832));
            GameObject savedCanvas = SavePrefab(canvas, CanvasPath);

            GameObject page = BuildEmptyPart("育虫盘", new Vector2(1080,1920), Vector2.zero);
            AddNested(page, savedCanvas, Vector2.zero); SavePrefab(page, PagePath);
            SaveScene();
            AppendBuildSettings(ScenePath);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PagePath);
            Debug.Log("Rebuilt Figma 91:8 with peer-level component prefabs and Figma-matching hierarchy.");
        }

        static void DeleteOldAssets()
        {
            // Keep current modular prefab paths so their GUIDs remain stable while rebuilding.
            // Deleting nested assets first causes transient missing-prefab imports in parent prefabs.
            AssetDatabase.DeleteAsset(Prefabs + "/BreedingBoard_Hud.prefab");
            AssetDatabase.DeleteAsset(Parts + "/BreedingBoard_Background.prefab");
            AssetDatabase.DeleteAsset(Parts + "/BreedingBoard_InteractionOverlay.prefab");
            AssetDatabase.DeleteAsset(Textures + "/BreedingBoard_91_8.png");
            for (int i = 1; i <= 20; i++)
                AssetDatabase.DeleteAsset(Parts + "/Cell" + i + ".prefab");
        }

        static GameObject BuildBottomNavTab()
        {
            GameObject tab = BuildButtonPart("BottomNavTab", new Vector2(219,150), new Color(0.384f,0.380f,0.20f,1), new Color(0.341f,0.36f,0.09f,1));
            GameObject icon = BuildImagePart("Icon", new Vector2(72,72), Color.white, null);
            Image iconImage = icon.GetComponent<Image>();
            iconImage.enabled = false;
            iconImage.preserveAspect = true;
            AddNested(tab, icon, new Vector2(0,22));
            GameObject text = BuildTextPart("Label", "功能", new Vector2(120,48), 40, Gold, TextAnchor.MiddleCenter);
            AddNested(tab, text, new Vector2(-0.5f,-39));
            LayoutElement layout = tab.GetComponent<LayoutElement>();
            if (layout == null) layout = tab.AddComponent<LayoutElement>();
            layout.preferredWidth = 219f;
            layout.preferredHeight = 150f;
            layout.minWidth = 219f;
            layout.minHeight = 150f;
            DouQuqu.DouQuquBottomNavTab hook = tab.GetComponent<DouQuqu.DouQuquBottomNavTab>();
            if (hook == null) hook = tab.AddComponent<DouQuqu.DouQuquBottomNavTab>();
            hook.Configure(DouQuqu.DouQuquBottomNavTab.NavModule.Battle, "功能", "");
            SaveExisting(tab, Parts + "/BottomNavTab.prefab");
            return AssetDatabase.LoadAssetAtPath<GameObject>(Parts + "/BottomNavTab.prefab");
        }

        static void BuildCarouselTabs(GameObject carousel, GameObject tabPrefab)
        {
            GameObject viewport = BuildPanelPart("TabViewport", new Vector2(780,150), new Vector2(62,0), new Color(1,1,1,0), Color.clear, 0, false);
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.raycastTarget = true;
            if (viewport.GetComponent<RectMask2D>() == null) viewport.AddComponent<RectMask2D>();
            viewport.transform.SetParent(carousel.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchoredPosition = new Vector2(62f, 0f);

            GameObject content = BuildEmptyPart("TabContent", new Vector2(0,150), Vector2.zero);
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0.5f);
            contentRect.anchorMax = new Vector2(0f, 0.5f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            HorizontalLayoutGroup layout = content.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 61.5f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.content = contentRect;
            scroll.viewport = viewportRect;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 40f;

            var tabs = new[]
            {
                new { name = "BattleTab", module = DouQuqu.DouQuquBottomNavTab.NavModule.Battle, label = "斗蛐蛐" },
                new { name = "BreedingTab", module = DouQuqu.DouQuquBottomNavTab.NavModule.Breeding, label = "育虫盘" },
                new { name = "RegistryTab", module = DouQuqu.DouQuquBottomNavTab.NavModule.Registry, label = "蛐蛐谱" },
                new { name = "RankingTab", module = DouQuqu.DouQuquBottomNavTab.NavModule.Ranking, label = "排行榜" },
                new { name = "ShopTab", module = DouQuqu.DouQuquBottomNavTab.NavModule.Shop, label = "小铺" },
                new { name = "AcademyTab", module = DouQuqu.DouQuquBottomNavTab.NavModule.Academy, label = "日勤学" }
            };
            for (int i = 0; i < tabs.Length; i++)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(tabPrefab, content.transform) as GameObject;
                if (instance == null) continue;
                instance.name = tabs[i].name;
                DouQuqu.DouQuquBottomNavTab hook = instance.GetComponent<DouQuqu.DouQuquBottomNavTab>();
                if (hook == null) hook = instance.AddComponent<DouQuqu.DouQuquBottomNavTab>();
                hook.Configure(tabs[i].module, tabs[i].label, "");
            }
        }

        static GameObject BuildEmptyPart(string name, Vector2 size, Vector2 position)
        {
            GameObject go = CreateRect(name,size,position); return go;
        }

        static GameObject BuildImagePart(string name, Vector2 size, Color color, Sprite sprite)
        {
            if (sprite == null && name == "ArenaStatus") return null;
            GameObject go = CreateRect(name,size,Vector2.zero); Image image = go.AddComponent<Image>(); image.color = color; image.sprite = sprite; image.preserveAspect = sprite != null; image.raycastTarget = false; return go;
        }

        static GameObject BuildPanelPart(string name, Vector2 size, Vector2 position, Color color, Color outline, float width, bool button)
        {
            GameObject go = CreateRect(name,size,position); Image image = go.AddComponent<Image>(); image.color = color; image.raycastTarget = button;
            if (outline.a > 0 && width > 0) { Outline o = go.AddComponent<Outline>(); o.effectColor = outline; o.effectDistance = new Vector2(width,width); }
            if (button) { Button b = go.AddComponent<Button>(); b.targetGraphic = image; Navigation n = b.navigation; n.mode = Navigation.Mode.None; b.navigation = n; }
            return go;
        }

        static GameObject BuildButtonPart(string name, Vector2 size, Color color, Color outline)
        {
            return BuildPanelPart(name,size,Vector2.zero,color,outline,outline.a > 0 ? 1 : 0,true);
        }

        static GameObject BuildTextPart(string name,string value,Vector2 size,int fontSize,Color color,TextAnchor anchor)
        {
            GameObject go = CreateRect(name,size,Vector2.zero); Text text = go.AddComponent<Text>(); text.text=value; text.fontSize=fontSize; text.color=color; text.alignment=anchor; text.horizontalOverflow=HorizontalWrapMode.Overflow; text.verticalOverflow=VerticalWrapMode.Overflow; text.raycastTarget=false; return go;
        }

static void AddNested(GameObject parent, GameObject child, Vector2 position)
        {
            if (parent == null || child == null) return;
            GameObject instance;
            string assetPath = AssetDatabase.GetAssetPath(child);
            if (!string.IsNullOrEmpty(assetPath))
                instance = PrefabUtility.InstantiatePrefab(child, parent.transform) as GameObject;
            else
                instance = Object.Instantiate(child, parent.transform);
            if (instance == null) return;
            string displayName = child.name;
            if (displayName == "EventTitlePanel") displayName = "event-title-panel";
            else if (displayName == "MainEventActions") displayName = "main-event-actions";
            else if (displayName == "Board") displayName = "棋盘";
            else if (displayName == "BoardBase") displayName = "棋盘底";
            else if (displayName == "RulesButton" || displayName == "BackpackButton") displayName = "btn-left-rules";
            else if (displayName == "ArenaStatus") displayName = "Frame 2";
            else if (displayName == "BottomEventCarousel") displayName = "bottom-event-carousel";
            else if (displayName == "BackIcon") displayName = "返回icon";
            else if (displayName == "BottomNavTab") displayName = "BottomNavTab";
            else if (displayName.StartsWith("Cell") && displayName.Length > 4)
                displayName = "Cell " + displayName.Substring(4);
            instance.name = displayName;
            RectTransform rect = instance.GetComponent<RectTransform>();
            if (rect != null) rect.anchoredPosition = position;
        }

        static GameObject SaveExisting(GameObject go,string path)
        {
            GameObject saved = SavePrefab(go,path); AssetDatabase.SaveAssets(); return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

static GameObject SavePrefab(GameObject go,string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go,path);
            Object.DestroyImmediate(go);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void ConfigureCanvas(RectTransform rect) { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero; rect.pivot=new Vector2(0.5f,0.5f); }
        static GameObject CreateRect(string name,Vector2 size,Vector2 position)
        {
            GameObject go=new GameObject(name); RectTransform r=go.AddComponent<RectTransform>(); r.anchorMin=r.anchorMax=new Vector2(0.5f,0.5f); r.pivot=new Vector2(0.5f,0.5f); r.sizeDelta=size; r.anchoredPosition=position; return go;
        }
        static Sprite PrepareSprite(string path)
        {
            TextureImporter i=AssetImporter.GetAtPath(path) as TextureImporter; if(i!=null){i.textureType=TextureImporterType.Sprite;i.spriteImportMode=SpriteImportMode.Single;i.mipmapEnabled=false;i.textureCompression=TextureImporterCompression.Uncompressed;i.SaveAndReimport();} return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static void SaveScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            GameObject page=PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PagePath)) as GameObject; SceneManager.MoveGameObjectToScene(page,SceneManager.GetActiveScene());
            GameObject cam=new GameObject("Main Camera"); Camera camera=cam.AddComponent<Camera>(); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(0.03f,0.04f,0.06f); camera.transform.position=new Vector3(0,0,-10); cam.tag="MainCamera";
            GameObject light=new GameObject("Directional Light"); Light l=light.AddComponent<Light>(); l.type=LightType.Directional; l.intensity=1; l.transform.rotation=Quaternion.Euler(50,-30,0);
            GameObject es=new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>(); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);
        }
        static void AppendBuildSettings(string path){var list=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);bool found=false;foreach(var s in list)if(s.path==path)found=true;if(!found)list.Add(new EditorBuildSettingsScene(path,true));EditorBuildSettings.scenes=list.ToArray();}
        static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace("\\","/");string name=Path.GetFileName(path);if(!AssetDatabase.IsValidFolder(parent))EnsureFolder(parent);AssetDatabase.CreateFolder(parent,name);}
    }
}