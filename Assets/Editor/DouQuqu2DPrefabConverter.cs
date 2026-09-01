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
            new PrefabEntry("Assets/Prefabs/DouQuqu/DouQuqu_Cricket.prefab", "Assets/Art/DouQuqu2D/DouQuqu_Cricket.svg", true, 20),
            new PrefabEntry("Assets/Prefabs/DouQuqu/DouQuqu_Baby.prefab", "Assets/Art/DouQuqu2D/DouQuqu_Baby.svg", true, 18),
            new PrefabEntry("Assets/Prefabs/DouQuqu/DouQuqu_Egg.prefab", "Assets/Art/DouQuqu2D/DouQuqu_Egg.svg", true, 10),
            new PrefabEntry("Assets/Prefabs/DouQuqu/DouQuqu_Nest.prefab", "Assets/Art/DouQuqu2D/DouQuqu_Nest.svg", false, 5),
            new PrefabEntry("Assets/Prefabs/DouQuqu/DouQuqu_Heart.prefab", "Assets/Art/DouQuqu2D/DouQuqu_Heart.svg", false, 12),
            new PrefabEntry("Assets/Prefabs/DouQuqu/DouQuqu_Size.prefab", "Assets/Art/DouQuqu2D/DouQuqu_Size.svg", false, 12),
            new PrefabEntry("Assets/Prefabs/DouQuqu/DouQuqu_Shield.prefab", "Assets/Art/DouQuqu2D/DouQuqu_Shield.svg", false, 12),
            new PrefabEntry("Assets/Prefabs/DouQuqu/DouQuqu_Charge.prefab", "Assets/Art/DouQuqu2D/DouQuqu_Charge.svg", true, 12),
            new PrefabEntry("Assets/Prefabs/DouQuqu/DouQuqu_MergePiece.prefab", "Assets/Art/DouQuqu2D/DouQuqu_MergePiece.svg", false, 15)
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

                Collider2D collider;
                if (entry.Capsule)
                {
                    CapsuleCollider2D capsule = root.AddComponent<CapsuleCollider2D>();
                    capsule.direction = CapsuleDirection2D.Vertical;
                    capsule.size = new Vector2(1f, 1.8f);
                    collider = capsule;
                }
                else
                {
                    CircleCollider2D circle = root.AddComponent<CircleCollider2D>();
                    circle.radius = 0.5f;
                    collider = circle;
                }
                // 2D 碰撞体作为表现/检测层，不接管现有的确定性规则碰撞。
                collider.isTrigger = true;

                // 当前竞技场是 XZ 平面，绕 X 轴 90 度让 Sprite 的 XY 面朝向顶视相机。
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
