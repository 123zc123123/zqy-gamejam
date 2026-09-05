using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 用 SVG 生成顶视蟋蟀预制体：均匀缩放、不要 Unity 碰撞体（玩法碰撞是规则层的圆）。
    /// 菜单：DouQuqu / Rebuild Body Prefabs
    /// </summary>
    public static class DouQuquBodyPrefabBuilder
    {
        private struct BodyEntry
        {
            public readonly string SpritePath;
            public readonly string PrefabPath;
            public readonly int SortingOrder;

            public BodyEntry(string spritePath, string prefabPath, int sortingOrder)
            {
                SpritePath = spritePath;
                PrefabPath = prefabPath;
                SortingOrder = sortingOrder;
            }
        }

        private static readonly BodyEntry[] Entries =
        {
            new BodyEntry("Assets/Resources/Battle/Entities/Textures/DouQuqu_QingTou.svg", "Assets/Resources/Battle/Entities/Prefabs/DouQuqu_QingTou.prefab", 20),
            new BodyEntry("Assets/Resources/Battle/Entities/Textures/DouQuqu_YouHulu.svg", "Assets/Resources/Battle/Entities/Prefabs/DouQuqu_YouHulu.prefab", 20),
            new BodyEntry("Assets/Resources/Battle/Entities/Textures/DouQuqu_Cricket.svg", "Assets/Resources/Battle/Entities/Prefabs/DouQuqu_Cricket.prefab", 20),
            new BodyEntry("Assets/Resources/Battle/Entities/Textures/DouQuqu_Baby.svg", "Assets/Resources/Battle/Entities/Prefabs/DouQuqu_Baby.prefab", 18)
        };

        [MenuItem("DouQuqu/Rebuild Body Prefabs")]
        public static void Rebuild()
        {
            AssetDatabase.Refresh();
            int built = 0;
            for (int i = 0; i < Entries.Length; i++)
                if (BuildOne(Entries[i])) built++;

            AssignToOpenScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DouQuqu] 身体预制体已重建：" + built + "/" + Entries.Length + "。玩法碰撞仍是半径 " + DouQuquRules.DefaultKnobs().bugR + " 的圆，视觉会按直径对齐。");
        }

        private static bool BuildOne(BodyEntry entry)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.SpritePath);
            if (sprite == null)
            {
                Debug.LogError("[DouQuqu] 找不到 Sprite，等 SVG 导入后再跑一次：" + entry.SpritePath);
                return false;
            }

            GameObject root;
            bool existed = System.IO.File.Exists(entry.PrefabPath);
            if (existed)
            {
                root = PrefabUtility.LoadPrefabContents(entry.PrefabPath);
            }
            else
            {
                root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(entry.PrefabPath));
            }

            try
            {
                root.transform.localPosition = Vector3.zero;
                root.transform.localScale = Vector3.one;
                root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                Strip<MeshFilter>(root);
                Strip<MeshRenderer>(root);
                Strip<Collider>(root);
                Strip<Collider2D>(root);

                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = Color.white;
                renderer.sortingOrder = entry.SortingOrder;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                if (existed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, entry.PrefabPath);
                }
                else
                {
                    PrefabUtility.SaveAsPrefabAsset(root, entry.PrefabPath);
                    Object.DestroyImmediate(root);
                    root = null;
                }
                return true;
            }
            finally
            {
                if (root != null && existed) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AssignToOpenScene()
        {
            DouQuquDemoView view = Object.FindObjectOfType<DouQuquDemoView>();
            if (view == null) return;
            SerializedObject so = new SerializedObject(view);
            // 局内成虫已切到零件预制体时，不要把青头 / 油葫芦槽盖回旧 SVG。
            if (!System.IO.File.Exists(DouQuquCricketPrefabBuilder.PrefabPath))
            {
                SetPrefab(so, "bugPrefab", "Assets/Resources/Battle/Entities/Prefabs/DouQuqu_QingTou.prefab");
                SetPrefab(so, "qingTouPrefab", "Assets/Resources/Battle/Entities/Prefabs/DouQuqu_QingTou.prefab");
                SetPrefab(so, "youHuluPrefab", "Assets/Resources/Battle/Entities/Prefabs/DouQuqu_YouHulu.prefab");
            }
            SetPrefab(so, "babyPrefab", "Assets/Resources/Battle/Entities/Prefabs/DouQuqu_Baby.prefab");
            SerializedProperty tint = so.FindProperty("tintPlayers");
            if (tint != null) tint.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
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
