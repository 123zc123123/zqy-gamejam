using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 生成耐力环、身下耐力条和蓄力箭头预制体，并挂到战斗场景 DemoView。
    /// 菜单：DouQuqu / Rebuild Overlay Prefabs
    /// </summary>
    public static class OverlayPrefabBuilder
    {
        private const string RingPath = "Assets/Resources/Battle/Entities/Prefabs/StaminaRing.prefab";
        private const string BarPath = "Assets/Resources/Battle/Entities/Prefabs/StaminaBar.prefab";
        private const string ArrowPath = "Assets/Resources/Battle/Entities/Prefabs/ChargeArrow.prefab";
        private const string MarkerPath = "Assets/Resources/Battle/Entities/Prefabs/GroundMarker.prefab";
        private const string FillSvg = "Assets/Resources/Battle/Entities/Textures/ChargeFill.svg";
        private const string ChevronSvg = "Assets/Resources/Battle/Entities/Textures/ChargeChevron.svg";
        private const string GroundFillPng = "Assets/Resources/Battle/Entities/Textures/GroundFill.png";
        private const string BattleScene = "Assets/Scenes/Demo.unity";

        [InitializeOnLoadMethod]
        private static void AutoBuildBarIfMissing()
        {
            EditorApplication.delayCall += () =>
            {
                bool built = false;
                if (!File.Exists(BarPath))
                {
                    BuildBar();
                    built = true;
                }
                if (!File.Exists(MarkerPath))
                {
                    BuildMarker();
                    built = true;
                }
                if (!built) return;
                AssignBarToBattleScene(false);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            };
        }

        [MenuItem("DouQuqu/Rebuild Overlay Prefabs")]
        public static void Rebuild()
        {
            Material lineMaterial = LineMaterial();
            BuildRing(lineMaterial);
            BuildBar();
            BuildArrow();
            BuildMarker();
            AssignToBattleScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DouQuqu] 覆盖层预制体已重建：耐力环、耐力条、蓄力箭头、脚下圈。");
        }

        [MenuItem("DouQuqu/Rebuild Stamina Bar Prefab")]
        public static void RebuildBarMenu()
        {
            BuildBar();
            AssignBarToBattleScene(true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DouQuqu] 身下耐力条预制体已重建：" + BarPath);
        }

        private static void BuildRing(Material lineMaterial)
        {
            GameObject root;
            bool existed = System.IO.File.Exists(RingPath);
            if (existed) root = PrefabUtility.LoadPrefabContents(RingPath);
            else root = new GameObject("StaminaRing");

            try
            {
                root.name = "StaminaRing";
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                StaminaRing ring = root.GetComponent<StaminaRing>();
                if (ring == null) ring = root.AddComponent<StaminaRing>();
                SerializedObject so = new SerializedObject(ring);
                SerializedProperty material = so.FindProperty("lineMaterial");
                if (material != null) material.objectReferenceValue = lineMaterial;
                so.ApplyModifiedPropertiesWithoutUndo();
                ring.EnsureReady();

                if (existed) PrefabUtility.SaveAsPrefabAsset(root, RingPath);
                else
                {
                    PrefabUtility.SaveAsPrefabAsset(root, RingPath);
                    Object.DestroyImmediate(root);
                    root = null;
                }
            }
            finally
            {
                if (root != null && existed) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildArrow()
        {
            Sprite fillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FillSvg);
            Sprite chevronSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ChevronSvg);
            if (fillSprite == null || chevronSprite == null)
            {
                Debug.LogError("[DouQuqu] 找不到蓄力 SVG，等导入后再跑一次：" + FillSvg + " / " + ChevronSvg);
                return;
            }

            GameObject root;
            bool existed = System.IO.File.Exists(ArrowPath);
            if (existed) root = PrefabUtility.LoadPrefabContents(ArrowPath);
            else root = new GameObject("ChargeArrow");

            try
            {
                root.name = "ChargeArrow";
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                Strip<MeshFilter>(root);
                Strip<MeshRenderer>(root);
                Strip<LineRenderer>(root);

                ChargeArrow arrow = root.GetComponent<ChargeArrow>();
                if (arrow == null) arrow = root.AddComponent<ChargeArrow>();
                SerializedObject so = new SerializedObject(arrow);
                SetObject(so, "fillSprite", fillSprite);
                SetObject(so, "chevronSprite", chevronSprite);
                so.ApplyModifiedPropertiesWithoutUndo();
                arrow.EnsureReady();
                SpriteRenderer fillRenderer = root.transform.Find("Fill") != null
                    ? root.transform.Find("Fill").GetComponent<SpriteRenderer>()
                    : null;
                if (fillRenderer != null)
                {
                    fillRenderer.sprite = fillSprite;
                    fillRenderer.enabled = true;
                }

                if (existed) PrefabUtility.SaveAsPrefabAsset(root, ArrowPath);
                else
                {
                    PrefabUtility.SaveAsPrefabAsset(root, ArrowPath);
                    Object.DestroyImmediate(root);
                    root = null;
                }
            }
            finally
            {
                if (root != null && existed) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildMarker()
        {
            GameObject root;
            bool existed = File.Exists(MarkerPath);
            if (existed) root = PrefabUtility.LoadPrefabContents(MarkerPath);
            else root = new GameObject("GroundMarker");

            try
            {
                root.name = "GroundMarker";
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                Transform ringChild = root.transform.Find("Ring");
                if (ringChild != null)
                {
                    SpriteRenderer leftover = ringChild.GetComponent<SpriteRenderer>();
                    if (leftover != null) Object.DestroyImmediate(leftover);
                }

                GroundMarker marker = root.GetComponent<GroundMarker>();
                if (marker == null) marker = root.AddComponent<GroundMarker>();
                Sprite disc = EnsureDiscSprite();
                Material line = LineMaterial();
                SerializedObject so = new SerializedObject(marker);
                SetObject(so, "fillSprite", disc);
                SetObject(so, "lineMaterial", line);
                so.ApplyModifiedPropertiesWithoutUndo();
                marker.Preview();

                if (existed) PrefabUtility.SaveAsPrefabAsset(root, MarkerPath);
                else
                {
                    PrefabUtility.SaveAsPrefabAsset(root, MarkerPath);
                    Object.DestroyImmediate(root);
                    root = null;
                }
            }
            finally
            {
                if (root != null && existed) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildBar()
        {
            GameObject root;
            bool existed = File.Exists(BarPath);
            if (existed) root = PrefabUtility.LoadPrefabContents(BarPath);
            else root = new GameObject("StaminaBar");

            try
            {
                root.name = "StaminaBar";
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                StaminaBar bar = root.GetComponent<StaminaBar>();
                if (bar == null) bar = root.AddComponent<StaminaBar>();
                bar.EnsureReady();

                if (existed) PrefabUtility.SaveAsPrefabAsset(root, BarPath);
                else
                {
                    PrefabUtility.SaveAsPrefabAsset(root, BarPath);
                    Object.DestroyImmediate(root);
                    root = null;
                }
            }
            finally
            {
                if (root != null && existed) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AssignToBattleScene()
        {
            AssignBarToBattleScene(true);
        }

        /// <summary>
        /// 把覆盖层预制体挂到战斗场景 DemoView。forceOpen 时允许加性打开战斗场景，不切走当前场景。
        /// </summary>
        private static void AssignBarToBattleScene(bool forceOpen)
        {
            Scene battle;
            bool additive = false;
            Scene active = EditorSceneManager.GetActiveScene();
            if (active.path == BattleScene)
                battle = active;
            else if (!forceOpen && !File.Exists(BarPath))
                return;
            else if (!forceOpen)
            {
                battle = default;
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                {
                    Scene open = EditorSceneManager.GetSceneAt(i);
                    if (open.path == BattleScene)
                    {
                        battle = open;
                        break;
                    }
                }
                if (!battle.IsValid())
                {
                    battle = EditorSceneManager.OpenScene(BattleScene, OpenSceneMode.Additive);
                    additive = true;
                }
            }
            else
            {
                battle = EditorSceneManager.OpenScene(BattleScene, OpenSceneMode.Additive);
                additive = battle != active;
            }

            DemoView view = FindView(battle);
            if (view == null)
            {
                Debug.LogWarning("[DouQuqu] 战斗场景里没有 DemoView，覆盖层预制体未自动挂上。");
                if (additive) EditorSceneManager.CloseScene(battle, true);
                return;
            }

            SerializedObject so = new SerializedObject(view);
            SetPrefab(so, "staminaRingPrefab", RingPath);
            SetPrefab(so, "staminaBarPrefab", BarPath);
            SetPrefab(so, "chargeArrowPrefab", ArrowPath);
            SetPrefab(so, "groundMarkerPrefab", MarkerPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(battle);
            EditorSceneManager.SaveScene(battle);
            if (additive) EditorSceneManager.CloseScene(battle, true);
        }

        private static DemoView FindView(Scene scene)
        {
            if (!scene.IsValid()) return null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DemoView view = roots[i].GetComponentInChildren<DemoView>(true);
                if (view != null) return view;
            }
            return null;
        }

        private static void SetPrefab(SerializedObject so, string field, string path)
        {
            SerializedProperty property = so.FindProperty(field);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (property != null && prefab != null) property.objectReferenceValue = prefab;
        }

        private static void SetObject(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property != null) property.objectReferenceValue = value;
        }

        private static void Strip<T>(GameObject root) where T : Component
        {
            T[] components = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
                Object.DestroyImmediate(components[i]);
        }

        private static Sprite EnsureDiscSprite()
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(GroundFillPng);
            if (existing != null) return existing;

            const int pixels = 128;
            Texture2D texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
            Color[] colors = new Color[pixels * pixels];
            float center = (pixels - 1) * 0.5f;
            float inv = 1f / Mathf.Max(0.001f, center);
            const float softness = 0.045f;
            for (int y = 0; y < pixels; y++)
            {
                for (int x = 0; x < pixels; x++)
                {
                    float dx = (x - center) * inv;
                    float dy = (y - center) * inv;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((1f - r) / softness);
                    colors[y * pixels + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(colors);
            texture.Apply(false, false);
            File.WriteAllBytes(GroundFillPng, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(GroundFillPng);
            TextureImporter importer = AssetImporter.GetAtPath(GroundFillPng) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixels;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(GroundFillPng);
        }

        private static Material LineMaterial()
        {
            Material material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            if (material != null) return material;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            return shader != null ? new Material(shader) : null;
        }
    }
}
