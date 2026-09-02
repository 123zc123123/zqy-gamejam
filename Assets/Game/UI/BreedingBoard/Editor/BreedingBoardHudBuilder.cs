// TMPro is not required for these editor-generated labels.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZqyGameJam.UI.BreedingBoard.Editor
{
    /// <summary>Builds the backpack and top-right gold HUD from Figma node 91:8.</summary>
    public static class BreedingBoardHudBuilder
    {
        private const string Root = "Assets/Game/UI/BreedingBoard";
        private const string Prefabs = Root + "/Prefabs";
        private const string Scenes = Root + "/Scenes";
        private const string HudPath = Prefabs + "/BreedingBoard_Hud.prefab";
        private const string CanvasPath = Prefabs + "/BreedingBoardCanvas.prefab";
        private const string ScenePath = Scenes + "/BreedingBoard.unity";
        private const string CoinPath = Root + "/Textures/Figma/BreedingBoard_Coins.png";

        [MenuItem("Tools/Cricket UI/Add Breeding Board HUD (Backpack + Gold)")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            EnsureCoinSpriteImport();
            Sprite coinSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CoinPath);
            if (coinSprite == null)
                throw new System.IO.FileNotFoundException("Missing Figma coins PNG sprite", CoinPath);

            AssetDatabase.DeleteAsset(HudPath);
            GameObject hud = CreateRect("BreedingBoard_Hud", new Vector2(1080f, 1920f), Vector2.zero, null);
            ConfigureFullCanvasRect(hud.GetComponent<RectTransform>());
            BuildGoldDisplay(hud.transform, coinSprite);
            BuildBackpackButton(hud.transform);
            GameObject savedHud = SavePrefab(hud, HudPath);

            GameObject canvas = PrefabUtility.LoadPrefabContents(CanvasPath);
            Transform oldHud = canvas.transform.Find("BreedingBoard_Hud");
            if (oldHud != null)
                Object.DestroyImmediate(oldHud.gameObject);
            PrefabUtility.InstantiatePrefab(savedHud, canvas.transform);
            PrefabUtility.SaveAsPrefabAsset(canvas, CanvasPath);
            PrefabUtility.UnloadPrefabContents(canvas);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Added Figma 91:8 Backpack and GoldDisplay HUD prefabs.");
        }

        private static void EnsureCoinSpriteImport()
        {
            TextureImporter importer = AssetImporter.GetAtPath(CoinPath) as TextureImporter;
            if (importer == null)
                return;
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
        }

private static void BuildGoldDisplay(Transform parent, Sprite coinSprite)
        {
            GameObject gold = CreateRect("GoldDisplay", new Vector2(172f, 58f), new Vector2(381f, 832f), parent);
            Image panel = gold.AddComponent<Image>();
            panel.color = new Color(0f, 0f, 0f, 0.5f);
            panel.raycastTarget = false;

            GameObject icon = CreateRect("CoinsIcon", new Vector2(38f, 38f), new Vector2(-51f, 0f), gold.transform);
            Image iconImage = icon.AddComponent<Image>();
            iconImage.sprite = coinSprite;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            GameObject amount = CreateRect("Amount", new Vector2(90f, 32f), new Vector2(30f, 0f), gold.transform);
            Text text = amount.AddComponent<Text>();
            text.text = "18,450";
            text.fontSize = 20;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
        }

private static void BuildBackpackButton(Transform parent)
        {
            GameObject backpack = CreateRect("BackpackButton", new Vector2(138f, 106f), new Vector2(379f, -651f), parent);
            Image image = backpack.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.82f);
            image.raycastTarget = true;

            Outline outline = backpack.AddComponent<Outline>();
            outline.effectColor = new Color(0.7725f, 0.6275f, 0.349f, 1f);
            outline.effectDistance = new Vector2(2f, 2f);

            Button button = backpack.AddComponent<Button>();
            button.targetGraphic = image;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            GameObject label = CreateRect("Label", new Vector2(138f, 106f), Vector2.zero, backpack.transform);
            Text text = label.AddComponent<Text>();
            text.text = "背包";
            text.fontSize = 40;
            text.color = new Color(0.898f, 0.768f, 0.561f, 1f);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
        }

        private static GameObject CreateRect(string name, Vector2 size, Vector2 position, Transform parent)
        {
            GameObject go = new GameObject(name);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        private static void ConfigureFullCanvasRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static GameObject SavePrefab(GameObject go, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }
    }
}
