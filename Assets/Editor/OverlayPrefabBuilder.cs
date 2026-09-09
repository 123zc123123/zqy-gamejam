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
        private const string UnitPath = "Assets/Resources/Battle/Entities/Prefabs/CricketUnit.prefab";
        private const string CricketPath = "Assets/Art/Characters/Cricket.prefab";
        private const string FillSvg = "Assets/Resources/Battle/Entities/Textures/ChargeFill.svg";
        private const string ChevronSvg = "Assets/Resources/Battle/Entities/Textures/ChargeChevron.svg";
        private const string CircleSrc = "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Circle.png";
        private const string CirclePath = "Assets/Resources/Battle/Entities/Textures/Circle.png";
        private const string ShadowPath = "Assets/Art/Characters/shadow-default.png";
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
                if (!File.Exists(UnitPath) && File.Exists(MarkerPath) && File.Exists(BarPath))
                {
                    BuildUnit();
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
            BuildUnit();
            AssignToBattleScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DouQuqu] 覆盖层预制体已重建：耐力环、耐力条、蓄力箭头、脚下圈、成虫单位。");
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

        [MenuItem("DouQuqu/Rebuild Ground Marker Prefab")]
        public static void RebuildMarkerMenu()
        {
            BuildMarker();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DouQuqu] 脚下圈预制体已重建，阴影用 shadow-default 并挂上 Sprites-Default：" + MarkerPath);
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

                Transform extraCircle = root.transform.Find("Circle");
                if (extraCircle != null) Object.DestroyImmediate(extraCircle.gameObject);
                Transform ringChild = root.transform.Find("Ring");
                if (ringChild != null)
                {
                    Transform hole = ringChild.Find("Hole");
                    if (hole != null) Object.DestroyImmediate(hole.gameObject);
                    SpriteRenderer leftoverSprite = ringChild.GetComponent<SpriteRenderer>();
                    if (leftoverSprite != null) Object.DestroyImmediate(leftoverSprite);
                    SpriteMask leftoverMask = ringChild.GetComponent<SpriteMask>();
                    if (leftoverMask != null) Object.DestroyImmediate(leftoverMask);
                }

                GroundMarker marker = root.GetComponent<GroundMarker>();
                if (marker == null) marker = root.AddComponent<GroundMarker>();
                Sprite circle = EnsureCircleSprite();
                Sprite shadowArt = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowPath);
                Material line = LineMaterial();
                Material spritesDefault = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                SerializedObject so = new SerializedObject(marker);
                SetObject(so, "circleSprite", circle);
                if (shadowArt != null) SetObject(so, "shadowSprite", shadowArt);
                SetObject(so, "lineMaterial", line);
                so.ApplyModifiedPropertiesWithoutUndo();
                marker.Preview();
                Transform shadowT = root.transform.Find("Shadow");
                if (shadowT != null)
                {
                    SpriteRenderer shadowRenderer = shadowT.GetComponent<SpriteRenderer>();
                    if (shadowRenderer != null)
                    {
                        if (shadowArt != null) shadowRenderer.sprite = shadowArt;
                        if (spritesDefault != null) shadowRenderer.sharedMaterial = spritesDefault;
                        shadowRenderer.color = Color.white;
                    }
                }
                Transform fillT = root.transform.Find("Fill");
                if (fillT != null)
                {
                    SpriteRenderer fillRenderer = fillT.GetComponent<SpriteRenderer>();
                    if (fillRenderer != null && spritesDefault != null)
                        fillRenderer.sharedMaterial = spritesDefault;
                }

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
            SetPrefab(so, "cricketUnitPrefab", UnitPath);
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

        private static void BuildUnit()
        {
            GameObject cricket = AssetDatabase.LoadAssetAtPath<GameObject>(CricketPath);
            GameObject markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarkerPath);
            GameObject barPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarPath);
            if (markerPrefab == null || barPrefab == null)
            {
                Debug.LogError("[DouQuqu] 拼 CricketUnit 需要先有 GroundMarker 和 StaminaBar。");
                return;
            }

            GameObject root;
            bool existed = File.Exists(UnitPath);
            if (existed) root = PrefabUtility.LoadPrefabContents(UnitPath);
            else root = new GameObject("CricketUnit");

            try
            {
                root.name = "CricketUnit";
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                CricketUnit unit = root.GetComponent<CricketUnit>();
                if (unit == null) unit = root.AddComponent<CricketUnit>();

                Transform body = root.transform.Find("Body");
                if (body == null && cricket != null)
                {
                    GameObject bodyGo = (GameObject)PrefabUtility.InstantiatePrefab(cricket, root.transform);
                    bodyGo.name = "Body";
                    body = bodyGo.transform;
                }

                Transform markerT = EnsureNested(root.transform, "GroundMarker", markerPrefab);
                Transform barT = EnsureNested(root.transform, "StaminaBar", barPrefab);

                SerializedObject so = new SerializedObject(unit);
                if (body != null) SetObject(so, "body", body);
                SetObject(so, "marker", markerT != null ? markerT.GetComponent<GroundMarker>() : null);
                SetObject(so, "bar", barT != null ? barT.GetComponent<StaminaBar>() : null);
                so.ApplyModifiedPropertiesWithoutUndo();
                LayoutUnitAuthored(root, body, markerT, barT);

                if (existed) PrefabUtility.SaveAsPrefabAsset(root, UnitPath);
                else
                {
                    PrefabUtility.SaveAsPrefabAsset(root, UnitPath);
                    Object.DestroyImmediate(root);
                    root = null;
                }
            }
            finally
            {
                if (root != null && existed) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 单位预制体按局内初始大小正对镜头摆：蛐蛐直径对齐 bugR=1.8，圈和耐力条在同一平面上看得见。
        /// </summary>
        private static void LayoutUnitAuthored(GameObject root, Transform body, Transform markerT, Transform barT)
        {
            const float bugR = 1.8f;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            if (body != null)
            {
                body.localPosition = new Vector3(0f, 0.35f, 0f);
                body.localRotation = Quaternion.identity;
                CricketVisual visual = body.GetComponent<CricketVisual>();
                float size = visual != null ? visual.VisualSize : 1f;
                float scale = (2f * bugR) / Mathf.Max(0.05f, size);
                body.localScale = Vector3.one * scale;
            }

            if (markerT != null)
            {
                markerT.localPosition = Vector3.zero;
                markerT.localRotation = Quaternion.identity;
                markerT.localScale = Vector3.one;
                GroundMarker marker = markerT.GetComponent<GroundMarker>();
                if (marker != null) marker.Bake(bugR);
            }

            if (barT != null)
            {
                barT.localPosition = new Vector3(0f, -2.12f, 0f);
                barT.localRotation = Quaternion.identity;
                barT.localScale = Vector3.one;
                StaminaBar bar = barT.GetComponent<StaminaBar>();
                if (bar != null) bar.Bake(bugR);
            }
        }

        private static Transform EnsureNested(Transform parent, string objectName, GameObject prefab)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null) return existing;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = objectName;
            return instance.transform;
        }

        private static Sprite EnsureCircleSprite()
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
            if (existing != null) return existing;
            if (!AssetDatabase.CopyAsset(CircleSrc, CirclePath))
            {
                Debug.LogError("[DouQuqu] 拷贝 Unity 2D Circle 失败：" + CircleSrc);
                return AssetDatabase.LoadAssetAtPath<Sprite>(CircleSrc);
            }

            AssetDatabase.ImportAsset(CirclePath);
            return AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
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
