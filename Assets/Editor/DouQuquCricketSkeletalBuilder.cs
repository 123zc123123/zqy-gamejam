using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D.Animation;

namespace DouQuqu.Editor
{
    /// <summary>
    /// Default 骨骼蛐蛐管线：共享 Skeleton、Sprite Library、Animator、局内 Prefab。
    /// 菜单：DouQuqu / Rebuild Skeletal Cricket Prefab
    /// </summary>
    public static class DouQuquCricketSkeletalBuilder
    {
        public const string PsbPath = "Assets/Art/Characters/Skins/1-1.psb";
        public const string LayerNamesPath = "Assets/Art/Characters/Skins/LAYER_NAMES.txt";
        public const string RigFolder = "Assets/Art/Characters/Rig";
        public const string SkeletonPath = "Assets/Art/Characters/Rig/CricketSkeleton.asset";
        public const string SpriteLibraryPath = "Assets/Art/Characters/Rig/CricketSkins.asset";
        public const string AnimFolder = "Assets/Animations/Characters/Crickets";
        public const string IdleClipPath = "Assets/Animations/Characters/Crickets/Cricket_Idle.anim";
        public const string ControllerPath = "Assets/Animations/Characters/Crickets/Cricket.controller";
        public const string ShaderPath = "Assets/Art/Characters/Shaders/SpriteOutline.shader";
        public const string MaterialPath = "Assets/Art/Characters/Materials/CricketBodyOutline.mat";
        public const string PrefabPath = "Assets/Art/Characters/Cricket.prefab";
        public const string BattleScenePath = "Assets/Scenes/Demo.unity";
        public const string DefaultLabel = "Default";

        [InitializeOnLoadMethod]
        private static void AutoBuildIfMissing()
        {
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(PrefabPath)) return;
                if (!File.Exists(PsbPath)) return;
                Rebuild();
            };
        }

        [MenuItem("DouQuqu/Rebuild Skeletal Cricket Prefab")]
        public static void Rebuild()
        {
            EnsureFolders();
            if (!ExtractSharedSkeleton())
            {
                Debug.LogError("[DouQuqu] 无法从 1-1.psb 抽出 Skeleton Asset。");
                return;
            }

            SpriteLibraryAsset library = BuildSpriteLibrary();
            if (library == null)
            {
                Debug.LogError("[DouQuqu] Sprite Library 创建失败。");
                return;
            }

            RuntimeAnimatorController controller = BuildAnimator();
            Material outline = BuildOutlineMaterial();
            if (outline == null)
            {
                Debug.LogError("[DouQuqu] 找不到 DouQuqu/SpriteOutline，确认 shader 已编译。");
                return;
            }

            if (!BuildRuntimePrefab(library, controller, outline))
            {
                Debug.LogError("[DouQuqu] 骨骼 Prefab 生成失败。");
                return;
            }

            AssignToBattleScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DouQuqu] Default 骨骼蛐蛐已接到 " + PrefabPath + "。");
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets/Art", "Characters");
            CreateFolder("Assets/Art/Characters", "Rig");
            CreateFolder("Assets/Art/Characters", "Skins");
            CreateFolder("Assets/Art/Characters", "Shaders");
            CreateFolder("Assets/Art/Characters", "Materials");
            CreateFolder("Assets/Animations", "Characters");
            CreateFolder("Assets/Animations/Characters", "Crickets");
        }

        private static void CreateFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static bool ExtractSharedSkeleton()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(PsbPath);
            SkeletonAsset source = null;
            for (int i = 0; i < assets.Length; i++)
            {
                source = assets[i] as SkeletonAsset;
                if (source != null) break;
            }

            if (source == null) return false;

            SkeletonAsset existing = AssetDatabase.LoadAssetAtPath<SkeletonAsset>(SkeletonPath);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<SkeletonAsset>();
                existing.SetSpriteBones(source.GetSpriteBones());
                AssetDatabase.CreateAsset(existing, SkeletonPath);
            }
            else
            {
                existing.SetSpriteBones(source.GetSpriteBones());
                EditorUtility.SetDirty(existing);
            }

            return true;
        }

        private static SpriteLibraryAsset BuildSpriteLibrary()
        {
            HashSet<string> expected = ReadLayerNames();
            SpriteLibraryAsset library = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(SpriteLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SpriteLibraryAsset>();
                AssetDatabase.CreateAsset(library, SpriteLibraryPath);
                library = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(SpriteLibraryPath);
            }

            int added = AddSkinLabel(library, expected, PsbPath, DefaultLabel);
            for (int quality = 1; quality <= 4; quality++)
            {
                for (int temperament = 1; temperament <= 4; temperament++)
                {
                    string label = quality + "-" + temperament;
                    string path = "Assets/Art/Characters/Skins/" + label + ".psb";
                    if (!File.Exists(path))
                        path = "Assets/Art/Characters/Skins/" + Mathf.Min(quality, 3) + "-" + temperament + ".psb";
                    added += AddSkinLabel(library, expected, path, label);
                }
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("[DouQuqu] Sprite Library 标签写入：" + added);
            return library;
        }

        private static int AddSkinLabel(SpriteLibraryAsset library, HashSet<string> expected, string psbPath, string label)
        {
            if (library == null || string.IsNullOrEmpty(psbPath) || !File.Exists(psbPath)) return 0;
            AssetDatabase.ImportAsset(psbPath, ImportAssetOptions.ImportRecursive);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(psbPath);
            if (assets == null) return 0;
            SerializedObject so = new SerializedObject(library);
            SerializedProperty categories = so.FindProperty("m_Labels");
            if (categories == null) return 0;
            int added = 0;
            for (int i = 0; i < assets.Length; i++)
            {
                Sprite sprite = assets[i] as Sprite;
                if (sprite == null) continue;
                if (expected.Count > 0 && !expected.Contains(sprite.name)) continue;
                if (WriteCategoryLabel(categories, sprite.name, label, sprite))
                    added++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return added;
        }

        private static bool WriteCategoryLabel(SerializedProperty categories, string category, string label, Sprite sprite)
        {
            SerializedProperty categoryEntry = null;
            for (int i = 0; i < categories.arraySize; i++)
            {
                SerializedProperty entry = categories.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("m_Name").stringValue == category)
                {
                    categoryEntry = entry;
                    break;
                }
            }
            if (categoryEntry == null)
            {
                categories.arraySize++;
                categoryEntry = categories.GetArrayElementAtIndex(categories.arraySize - 1);
                categoryEntry.FindPropertyRelative("m_Name").stringValue = category;
                categoryEntry.FindPropertyRelative("m_Hash").intValue = LibraryHash(category);
            }

            SerializedProperty labels = categoryEntry.FindPropertyRelative("m_CategoryList");
            SerializedProperty labelEntry = null;
            for (int i = 0; i < labels.arraySize; i++)
            {
                SerializedProperty entry = labels.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("m_Name").stringValue == label)
                {
                    labelEntry = entry;
                    break;
                }
            }
            if (labelEntry == null)
            {
                labels.arraySize++;
                labelEntry = labels.GetArrayElementAtIndex(labels.arraySize - 1);
            }
            labelEntry.FindPropertyRelative("m_Name").stringValue = label;
            labelEntry.FindPropertyRelative("m_Hash").intValue = LibraryHash(label);
            labelEntry.FindPropertyRelative("m_Sprite").objectReferenceValue = sprite;
            return true;
        }

        static int LibraryHash(string value)
        {
            return Animator.StringToHash(value) & 0x3FFFFFFF;
        }

        [MenuItem("DouQuqu/Refresh Cricket Skin Library")]
        public static void RefreshSkinLibrary()
        {
            SpriteLibraryAsset library = BuildSpriteLibrary();
            if (library == null)
            {
                Debug.LogError("[DouQuqu] Sprite Library 刷新失败。");
                return;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DouQuqu] 皮肤 Library 已刷新：" + SpriteLibraryPath);
        }

        private static HashSet<string> ReadLayerNames()
        {
            var names = new HashSet<string>();
            TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(LayerNamesPath);
            if (text == null || string.IsNullOrEmpty(text.text)) return names;
            string[] lines = text.text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("Photoshop") || line.StartsWith("New skins") || line.StartsWith("Id is"))
                    continue;
                names.Add(line);
            }

            return names;
        }

        private static RuntimeAnimatorController BuildAnimator()
        {
            AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
            if (idle == null)
            {
                idle = new AnimationClip { name = "Cricket_Idle", frameRate = 12f };
                AssetDatabase.CreateAsset(idle, IdleClipPath);
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idleState = FindState(machine, "Idle");
            if (idleState == null)
            {
                idleState = machine.AddState("Idle");
                machine.defaultState = idleState;
            }

            idleState.motion = idle;
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(idle);
            return controller;
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string stateName)
        {
            ChildAnimatorState[] states = machine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state != null && states[i].state.name == stateName)
                    return states[i].state;
            }

            return null;
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
            material.SetFloat("_OutlineWidth", 16f);
            material.SetFloat("_OutlineSoftness", 4f);
            material.SetFloat("_OutlineAlphaCutoff", 0.12f);
            material.SetFloat("_PixelsPerUnit", 100f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static bool BuildRuntimePrefab(
            SpriteLibraryAsset library,
            RuntimeAnimatorController controller,
            Material outline)
        {
            GameObject source = AssetDatabase.LoadMainAssetAtPath(PsbPath) as GameObject;
            if (source == null) return false;

            Material defaultMat = DefaultSpriteMaterial();
            GameObject wrapper = new GameObject("Cricket");
            try
            {
                wrapper.transform.localPosition = Vector3.zero;
                wrapper.transform.localScale = Vector3.one;
                wrapper.transform.localRotation = Quaternion.identity;

                GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(source, wrapper.transform);
                rig.name = "Rig";
                rig.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                rig.transform.localScale = Vector3.one;

                Transform body = FindChildByName(rig.transform, "chest&body");
                if (body != null)
                    rig.transform.localPosition = -(rig.transform.localRotation * body.localPosition);
                else
                    rig.transform.localPosition = Vector3.zero;

                SpriteLibrary spriteLibrary = rig.GetComponent<SpriteLibrary>();
                if (spriteLibrary == null) spriteLibrary = rig.AddComponent<SpriteLibrary>();
                spriteLibrary.spriteLibraryAsset = library;

                Animator animator = rig.GetComponent<Animator>();
                if (animator == null) animator = rig.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                SpriteRenderer[] renderers = rig.GetComponentsInChildren<SpriteRenderer>(true);
                SpriteRenderer bodyRenderer = null;
                SpriteRenderer firstAntenna = null;
                for (int i = 0; i < renderers.Length; i++)
                {
                    SpriteRenderer renderer = renderers[i];
                    renderer.color = Color.white;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    bool antenna = DouQuquCricketVisual.IsAntenna(renderer.name);
                    bool isBody = DouQuquCricketVisual.IsBody(renderer.name);
                    // 全部件共用描边材质：须/尾 width=0，仍写 stencil，避免主体描边盖到须上。
                    renderer.sharedMaterial = outline != null ? outline : defaultMat;
                    if (isBody) bodyRenderer = renderer;
                    if (antenna && firstAntenna == null) firstAntenna = renderer;

                    SpriteResolver resolver = renderer.GetComponent<SpriteResolver>();
                    if (resolver == null) resolver = renderer.gameObject.AddComponent<SpriteResolver>();
                    resolver.SetCategoryAndLabel(renderer.name, DefaultLabel);
                    resolver.ResolveSpriteToSpriteRenderer();
                }

                DouQuquCricketVisual visual = wrapper.GetComponent<DouQuquCricketVisual>();
                if (visual == null) visual = wrapper.AddComponent<DouQuquCricketVisual>();
                visual.BindParts(bodyRenderer, firstAntenna);
                visual.BindHierarchy();
                visual.ApplyTeam(true, false);
                if (wrapper.GetComponent<DouQuquCricketAnim>() == null)
                    wrapper.AddComponent<DouQuquCricketAnim>();

                string prefabDir = Path.GetDirectoryName(PrefabPath).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(prefabDir))
                    EnsureFolders();

                PrefabUtility.SaveAsPrefabAsset(wrapper, PrefabPath);
                EditorUtility.SetDirty(visual);
                return File.Exists(PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(wrapper);
            }
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root.name == childName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildByName(root.GetChild(i), childName);
                if (found != null) return found;
            }

            return null;
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
                Debug.Log("[DouQuqu] 已把骨骼 Cricket.prefab 挂到 Demo 的青头 / 油葫芦 / 默认槽。");
        }

        private static void SetPrefab(SerializedObject so, string field, string path)
        {
            SerializedProperty property = so.FindProperty(field);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (property != null && prefab != null) property.objectReferenceValue = prefab;
        }
    }
}
