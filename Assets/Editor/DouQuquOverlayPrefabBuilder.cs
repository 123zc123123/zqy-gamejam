using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 生成耐力环和蓄力箭头预制体，并挂到打开的战斗场景 DemoView。
    /// 菜单：DouQuqu / Rebuild Overlay Prefabs
    /// </summary>
    public static class DouQuquOverlayPrefabBuilder
    {
        private const string RingPath = "Assets/Prefabs/DouQuqu/DouQuqu_StaminaRing.prefab";
        private const string ArrowPath = "Assets/Prefabs/DouQuqu/DouQuqu_ChargeArrow.prefab";
        private const string BattleScene = "Assets/Scenes/DouQuquDemo.unity";

        [MenuItem("DouQuqu/Rebuild Overlay Prefabs")]
        public static void Rebuild()
        {
            Material lineMaterial = LineMaterial();
            BuildRing(lineMaterial);
            BuildArrow(lineMaterial);
            AssignToBattleScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DouQuqu] 覆盖层预制体已重建：耐力环、蓄力箭头。");
        }

        private static void BuildRing(Material lineMaterial)
        {
            GameObject root;
            bool existed = System.IO.File.Exists(RingPath);
            if (existed) root = PrefabUtility.LoadPrefabContents(RingPath);
            else root = new GameObject("DouQuqu_StaminaRing");

            try
            {
                root.name = "DouQuqu_StaminaRing";
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                DouQuquStaminaRing ring = root.GetComponent<DouQuquStaminaRing>();
                if (ring == null) ring = root.AddComponent<DouQuquStaminaRing>();
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

        private static void BuildArrow(Material lineMaterial)
        {
            GameObject root;
            bool existed = System.IO.File.Exists(ArrowPath);
            if (existed) root = PrefabUtility.LoadPrefabContents(ArrowPath);
            else root = new GameObject("DouQuqu_ChargeArrow");

            try
            {
                root.name = "DouQuqu_ChargeArrow";
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                DouQuquChargeArrow arrow = root.GetComponent<DouQuquChargeArrow>();
                if (arrow == null) arrow = root.AddComponent<DouQuquChargeArrow>();
                SerializedObject so = new SerializedObject(arrow);
                SerializedProperty material = so.FindProperty("lineMaterial");
                if (material != null) material.objectReferenceValue = lineMaterial;
                so.ApplyModifiedPropertiesWithoutUndo();
                arrow.EnsureReady();
                LineRenderer line = root.GetComponent<LineRenderer>();
                if (line != null)
                {
                    line.enabled = false;
                    line.shadowCastingMode = ShadowCastingMode.Off;
                    line.receiveShadows = false;
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

        private static void AssignToBattleScene()
        {
            var scene = EditorSceneManager.OpenScene(BattleScene);
            DouQuquDemoView view = Object.FindObjectOfType<DouQuquDemoView>();
            if (view == null)
            {
                Debug.LogWarning("[DouQuqu] 战斗场景里没有 DouQuquDemoView，覆盖层预制体未自动挂上。");
                return;
            }

            SerializedObject so = new SerializedObject(view);
            SetPrefab(so, "staminaRingPrefab", RingPath);
            SetPrefab(so, "chargeArrowPrefab", ArrowPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            EditorSceneManager.SaveScene(scene);
        }

        private static void SetPrefab(SerializedObject so, string field, string path)
        {
            SerializedProperty property = so.FindProperty(field);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (property != null && prefab != null) property.objectReferenceValue = prefab;
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
