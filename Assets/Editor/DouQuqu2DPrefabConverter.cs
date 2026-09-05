using System;
using UnityEditor;
using UnityEngine;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 一次性把蟋蟀 Demo 的表现预制体切换为 SVG 贴图和 2D 碰撞体。
    /// 通过 Unity 菜单执行，避免手工编辑预制体 YAML；核心对局仍由确定性规则驱动。
    /// </summary>
    public static class DouQuqu2DPrefabConverter
    {
        private struct PrefabEntry
        {
            public readonly string PrefabPath;
            public readonly string SpritePath;
            public readonly bool Capsule;
            public readonly int SortingOrder;

            public PrefabEntry(string prefabPath, string spritePath, bool capsule, int sortingOrder)
            {
                PrefabPath = prefabPath;
                SpritePath = spritePath;
                Capsule = capsule;
                SortingOrder = sortingOrder;
            }
        }

        private static readonly PrefabEntry[] Entries =
        {
            new PrefabEntry("Assets/Resources/Battle/Entities/Prefabs/DouQuqu_Cricket.prefab", "Assets/Resources/Battle/Entities/Textures/DouQuqu_Cricket.svg", true, 20),
            new PrefabEntry("Assets/Resources/Battle/Entities/Prefabs/DouQuqu_Baby.prefab", "Assets/Resources/Battle/Entities/Textures/DouQuqu_Baby.svg", true, 18),
            new PrefabEntry("Assets/Resources/Battle/Entities/Prefabs/DouQuqu_Egg.prefab", "Assets/Resources/Battle/Entities/Textures/DouQuqu_Egg.svg", true, 10),
            new PrefabEntry("Assets/Resources/Battle/Entities/Prefabs/DouQuqu_Nest.prefab", "Assets/Resources/Battle/Entities/Textures/DouQuqu_Nest.svg", false, 5),
            new PrefabEntry("Assets/Resources/Battle/Entities/Prefabs/DouQuqu_Heart.prefab", "Assets/Resources/Battle/Entities/Textures/DouQuqu_Heart.svg", false, 12),
            new PrefabEntry("Assets/Resources/Battle/Entities/Prefabs/DouQuqu_Size.prefab", "Assets/Resources/Battle/Entities/Textures/DouQuqu_Size.svg", false, 12),
            new PrefabEntry("Assets/Resources/Battle/Entities/Prefabs/DouQuqu_Shield.prefab", "Assets/Resources/Battle/Entities/Textures/DouQuqu_Shield.svg", false, 12),
            new PrefabEntry("Assets/Resources/Battle/Entities/Prefabs/DouQuqu_Charge.prefab", "Assets/Resources/Battle/Entities/Textures/DouQuqu_Charge.svg", true, 12),
            new PrefabEntry("Assets/Resources/Battle/Entities/Prefabs/DouQuqu_MergePiece.prefab", "Assets/Resources/Battle/Entities/Textures/DouQuqu_MergePiece.svg", false, 15)
        };

        [MenuItem("DouQuqu/Convert Demo Prefabs To 2D")]
        public static void Convert()
        {
            int converted = 0;
            for (int i = 0; i < Entries.Length; i++)
            {
                if (ConvertOne(Entries[i])) converted++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DouQuqu] 2D 预制体转换完成：" + converted + "/" + Entries.Length);
        }

        private static bool ConvertOne(PrefabEntry entry)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(entry.PrefabPath);
            if (root == null)
            {
                Debug.LogError("[DouQuqu] 无法打开预制体：" + entry.PrefabPath);
                return false;
            }

            try
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.SpritePath);
                if (sprite == null)
                {
                    Debug.LogError("[DouQuqu] 找不到 SVG Sprite：" + entry.SpritePath);
                    return false;
                }

                Remove< MeshFilter >(root);
                Remove< MeshRenderer >(root);
                Remove< Collider >(root);
                Remove< Collider2D >(root);

                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = entry.SortingOrder;
                renderer.receiveShadows = false;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // 玩法碰撞是 DouQuquCollisionSystem 的圆，不要挂 Unity Collider，避免 Scene 里看到比贴图大一圈的胶囊。
                root.transform.localScale = Vector3.one;
                root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                PrefabUtility.SaveAsPrefabAsset(root, entry.PrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Remove<T>(GameObject root) where T : Component
        {
            T[] components = root.GetComponents<T>();
            for (int i = 0; i < components.Length; i++)
                UnityEngine.Object.DestroyImmediate(components[i]);
        }
    }
}
