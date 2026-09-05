using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 按「美术零件 / 运行时 Prefab」拆局内蛐蛐：身体描边、触角不描边。
    /// 菜单：DouQuqu / Rebuild Character Cricket Prefab
    /// </summary>
    public static class DouQuquCricketPrefabBuilder
    {
        public const string BodyPath = "Assets/Art/Characters/Cricket_Body.png";
        public const string AntennaPath = "Assets/Art/Characters/Cricket_Antenna.png";
        public const string ShaderPath = "Assets/Art/Characters/Shaders/SpriteOutline.shader";
        public const string MaterialPath = "Assets/Art/Characters/Materials/CricketBodyOutline.mat";
        public const string PrefabPath = "Assets/Art/Characters/Cricket.prefab";
        public const string BattleScenePath = "Assets/Scenes/Demo.unity";

        [InitializeOnLoadMethod]
        private static void AutoBuildIfMissing()
        {
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(PrefabPath)) return;
                if (File.Exists(DouQuquCricketSkeletalBuilder.PsbPath)) return;
                if (!File.Exists(BodyPath) || !File.Exists(AntennaPath)) return;
                RebuildPlaceholder();
            };
        }

        [MenuItem("DouQuqu/Rebuild Character Cricket Prefab")]
        public static void Rebuild()
        {
            DouQuquCricketSkeletalBuilder.Rebuild();
        }

        [MenuItem("DouQuqu/Rebuild Placeholder Cricket Prefab (PNG)")]
        public static void RebuildPlaceholder()
        {
            EnsureFolders();
            AssetDatabase.Refresh();
            DouQuquCricketArtImporter.ReimportIfNeeded();

            Sprite body = AssetDatabase.LoadAssetAtPath<Sprite>(BodyPath);
            Sprite antenna = AssetDatabase.LoadAssetAtPath<Sprite>(AntennaPath);
            if (body == null || antenna == null)
            {
                Debug.LogError("[DouQuqu] 蛐蛐零件图还没导入成 Sprite。Body=" + body + " Antenna=" + antenna);
                return;
            }

            Material outline = BuildOutlineMaterial();
            if (outline == null)
            {
                Debug.LogError("[DouQuqu] 找不到 DouQuqu/SpriteOutline，确认 shader 已编译。");
                return;
            }

            BuildPrefab(body, antenna, outline);
            AssignToBattleScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DouQuqu] 局内蛐蛐预制体已生成：" + PrefabPath + "。触角位置在 Prefab 的 Antenna 节点上调。");
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets/Art", "Characters");
            CreateFolder("Assets/Art/Characters", "Shaders");
            CreateFolder("Assets/Art/Characters", "Materials");
        }

        private static void CreateFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static Material BuildOutlineMaterial()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) shader = Shader.Find("DouQuqu/SpriteOutline");
            if (shader == null) return null;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_Color", Color.white);
            material.SetColor("_OutlineColor", Color.black);
            material.SetFloat("_OutlineWidth", 20f);
            material.SetFloat("_OutlineSoftness", 6f);
            material.SetFloat("_OutlineAlphaCutoff", 0.12f);
            material.SetFloat("_PixelsPerUnit", 100f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildPrefab(Sprite bodySprite, Sprite antennaSprite, Material outline)
        {
            GameObject root;
            bool existed = File.Exists(PrefabPath);
            if (existed)
                root = PrefabUtility.LoadPrefabContents(PrefabPath);
            else
                root = new GameObject("Cricket");

            Vector3 savedAntennaPos = new Vector3(0f, 0f, -0.02f);
            Transform existingAntenna = root.transform.Find("Antenna");
            bool flipLegacyAntennaOffset = false;
            if (existingAntenna != null)
            {
                savedAntennaPos = existingAntenna.localPosition;
                // 旧预制体零件旋转是 identity、XY 按头朝上贴图调过；转 180 后 XY 要一起反。
                if (Quaternion.Angle(existingAntenna.localRotation, Quaternion.identity) < 1f)
                    flipLegacyAntennaOffset = true;
            }

            try
            {
                root.name = "Cricket";
                root.transform.localPosition = Vector3.zero;
                root.transform.localScale = Vector3.one;
                root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Strip<MeshFilter>(root);
                Strip<MeshRenderer>(root);
                Strip<Collider>(root);
                Strip<Collider2D>(root);
                Strip<SpriteRenderer>(root);

                // 贴图头朝上；DemoView.FaceXz 带 +180 且覆盖根旋转，零件绕 Z 转 180 才头朝运动方向。
                Quaternion partRotation = Quaternion.Euler(0f, 0f, 180f);

                SpriteRenderer body = EnsureChildRenderer(root.transform, "Body", 20);
                body.sprite = bodySprite;
                body.sharedMaterial = outline;
                body.color = Color.white;
                body.transform.localRotation = partRotation;

                SpriteRenderer antenna = EnsureChildRenderer(root.transform, "Antenna", 21);
                antenna.sprite = antennaSprite;
                antenna.sharedMaterial = DefaultSpriteMaterial();
                antenna.color = Color.white;
                antenna.transform.localRotation = partRotation;
                if (flipLegacyAntennaOffset)
                    savedAntennaPos = new Vector3(-savedAntennaPos.x, -savedAntennaPos.y, savedAntennaPos.z);
                antenna.transform.localPosition = savedAntennaPos;

                DouQuquCricketVisual visual = root.GetComponent<DouQuquCricketVisual>();
                if (visual == null) visual = root.AddComponent<DouQuquCricketVisual>();
                visual.BindParts(body, antenna);
                visual.ApplyTeam(true, false);
                EditorUtility.SetDirty(visual);

                if (existed)
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                else
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                    Object.DestroyImmediate(root);
                    root = null;
                }
            }
            finally
            {
                if (root != null && existed) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static SpriteRenderer EnsureChildRenderer(Transform parent, string childName, int sorting)
        {
            Transform existing = parent.Find(childName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(childName);
            if (existing == null) child.transform.SetParent(parent, false);
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            if (existing == null) child.transform.localPosition = Vector3.zero;

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = child.AddComponent<SpriteRenderer>();
            renderer.color = Color.white;
            renderer.sortingOrder = sorting;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.drawMode = SpriteDrawMode.Simple;
            return renderer;
        }

        private static Material DefaultSpriteMaterial()
        {
            Material material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            if (material != null) return material;
            Shader shader = Shader.Find("Sprites/Default");
            return shader != null ? new Material(shader) : null;
        }

        private static void AssignToBattleScene()
        {
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BattleScenePath);
            bool opened = false;
            if (scene != null && EditorSceneManager.GetActiveScene().path != BattleScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(BattleScenePath);
                opened = true;
            }

            DouQuquDemoView view = Object.FindObjectOfType<DouQuquDemoView>();
            if (view == null)
            {
                Debug.LogWarning("[DouQuqu] 当前场景没有 DouQuquDemoView，预制体已生成但未挂到局内。打开 Demo 后再跑一次菜单。");
                return;
            }

            SerializedObject so = new SerializedObject(view);
            SetPrefab(so, "bugPrefab", PrefabPath);
            SetPrefab(so, "qingTouPrefab", PrefabPath);
            SetPrefab(so, "youHuluPrefab", PrefabPath);
            SerializedProperty tint = so.FindProperty("tintPlayers");
            if (tint != null) tint.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            if (opened)
                Debug.Log("[DouQuqu] 已把 Cricket.prefab 挂到 Demo 的青头 / 油葫芦 / 默认槽。");
        }

        private static void SetPrefab(SerializedObject so, string field, string path)
        {
            SerializedProperty property = so.FindProperty(field);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (property != null && prefab != null) property.objectReferenceValue = prefab;
        }

        private static void Strip<T>(GameObject root) where T : Component
        {
            T[] components = root.GetComponents<T>();
            for (int i = 0; i < components.Length; i++)
                Object.DestroyImmediate(components[i]);
        }
    }
}
