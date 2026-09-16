using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 导入 Epic Toon FX，并把战斗要用的几条拷进 Resources/Battle/Fx。
    /// 菜单：DouQuqu / Import Battle FX
    /// </summary>
    [InitializeOnLoad]
    public static class BattleFxImporter
    {
        const string PackagePath = @"C:\baidunetdiskdownload\Epic Toon FX 1.4.unitypackage";
        const string PackageFolder = "Assets/Epic Toon FX";
        const string DestFolder = "Assets/Resources/Battle/Fx";
        const string LibFolder = DestFolder + "/Lib";
        const string BattleScene = "Assets/Scenes/Demo.unity";
        const string SessionKey = "DouQuqu.BattleFxImported";

        static BattleFxImporter()
        {
            EditorApplication.delayCall += TryImportOnce;
        }

        static void TryImportOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryImportOnce;
                return;
            }
            if (SessionState.GetBool(SessionKey, false)) return;
            if (File.Exists(DestFolder + "/hit.prefab"))
            {
                SessionState.SetBool(SessionKey, true);
                return;
            }
            SessionState.SetBool(SessionKey, true);
            ImportAndWire();
        }

        static readonly string[][] Copies =
        {
            new[] { "hit", "Assets/Epic Toon FX/Prefabs/Combat/Brawling/RoundHit/RoundHitYellow.prefab" },
            new[] { "dust", "Assets/Epic Toon FX/Prefabs/Environment/Dust/DustDirtyPoof.prefab" },
            new[] { "shieldBurst", "Assets/Epic Toon FX/Prefabs/Combat/Magic/Nova/MagicNovaBlue.prefab" },
            new[] { "shieldLoop", "Assets/Epic Toon FX/Prefabs/Combat/Magic/Shield/ShieldBlue.prefab" },
            new[] { "heart", "Assets/Epic Toon FX/Prefabs/Misc/HeartPoof.prefab" },
            new[] { "sparkle", "Assets/Epic Toon FX/Prefabs/Interactive/Loot/ItemSparkle/ItemSparkleBlue.prefab" },
            new[] { "buff", "Assets/Epic Toon FX/Prefabs/Combat/Magic/Buff/MagicBuffYellow.prefab" },
            new[] { "nestHit", "Assets/Epic Toon FX/Prefabs/Environment/Sparks/SparkRadialExplosionYellow.prefab" },
            new[] { "nestBreak", "Assets/Epic Toon FX/Prefabs/Combat/Explosions/StarExplosion/StarExplosionOrange.prefab" },
            new[] { "hatch", "Assets/Epic Toon FX/Prefabs/Interactive/Sparkle/TinySparkle.prefab" },
            new[] { "rage", "Assets/Epic Toon FX/Prefabs/Combat/Magic/Nova/MagicNovaYellow.prefab" },
            new[] { "revive", "Assets/Epic Toon FX/Prefabs/Combat/Magic/Aura/MagicAuraGreen.prefab" },
            new[] { "confetti", "Assets/Epic Toon FX/Prefabs/Environment/Confetti/Blast/ConfettiBlastRainbow.prefab" }
        };

        [MenuItem("DouQuqu/Import Battle FX")]
        public static void ImportAndWire()
        {
            if (!Directory.Exists(PackageFolder))
            {
                if (!File.Exists(PackagePath))
                {
                    Debug.LogError("[DouQuqu] 找不到 Epic Toon FX 包：" + PackagePath);
                    return;
                }
                AssetDatabase.ImportPackage(PackagePath, false);
                AssetDatabase.Refresh();
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Battle"))
                AssetDatabase.CreateFolder("Assets/Resources", "Battle");
            if (!AssetDatabase.IsValidFolder(DestFolder))
                AssetDatabase.CreateFolder("Assets/Resources/Battle", "Fx");

            int copied = 0;
            List<string> missing = new List<string>();
            for (int i = 0; i < Copies.Length; i++)
            {
                string dest = DestFolder + "/" + Copies[i][0] + ".prefab";
                string src = Copies[i][1];
                if (AssetDatabase.LoadAssetAtPath<GameObject>(src) == null)
                {
                    missing.Add(src);
                    continue;
                }
                if (AssetDatabase.LoadAssetAtPath<GameObject>(dest) != null)
                    AssetDatabase.DeleteAsset(dest);
                if (AssetDatabase.CopyAsset(src, dest)) copied++;
                else missing.Add(src);
            }

            WireDemoScene();
            EmbedDependencies();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (missing.Count > 0)
                Debug.LogWarning("[DouQuqu] 战斗特效缺源：" + string.Join("\n", missing.ToArray()));
            Debug.Log("[DouQuqu] 战斗特效已拷 " + copied + " 条到 " + DestFolder + "，依赖已收进 Lib。");
        }

        [MenuItem("DouQuqu/Embed Battle FX Dependencies")]
        public static void EmbedDependencies()
        {
            EnsureFolder(LibFolder);
            EnsureFolder(LibFolder + "/Textures");
            EnsureFolder(LibFolder + "/Materials");
            EnsureFolder(LibFolder + "/Models");

            string[] prefabs = Directory.GetFiles(DestFolder, "*.prefab");
            Dictionary<string, string> map = new Dictionary<string, string>();
            for (int i = 0; i < prefabs.Length; i++)
            {
                string prefab = prefabs[i].Replace('\\', '/');
                string[] deps = AssetDatabase.GetDependencies(prefab, true);
                for (int d = 0; d < deps.Length; d++)
                {
                    string src = deps[d].Replace('\\', '/');
                    if (src.IndexOf("/Epic Toon FX/", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string ext = Path.GetExtension(src).ToLowerInvariant();
                    string destDir = LibFolder;
                    if (ext == ".png" || ext == ".jpg" || ext == ".tga") destDir = LibFolder + "/Textures";
                    else if (ext == ".mat") destDir = LibFolder + "/Materials";
                    else if (ext == ".fbx") destDir = LibFolder + "/Models";
                    else continue;
                    string dest = destDir + "/" + Path.GetFileName(src);
                    string oldGuid = AssetDatabase.AssetPathToGUID(src);
                    if (string.IsNullOrEmpty(oldGuid) || map.ContainsKey(oldGuid)) continue;
                    if (File.Exists(dest)) AssetDatabase.DeleteAsset(dest);
                    if (!AssetDatabase.CopyAsset(src, dest)) continue;
                    map[oldGuid] = AssetDatabase.AssetPathToGUID(dest);
                }
            }

            RemapGuids(DestFolder, map);
            RemapGuids(LibFolder + "/Materials", map);
            AssetDatabase.Refresh();
            Debug.Log("[DouQuqu] 战斗特效依赖已嵌入 " + map.Count + " 个资源。");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static void RemapGuids(string folder, Dictionary<string, string> map)
        {
            if (!Directory.Exists(folder) || map.Count == 0) return;
            string[] files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string ext = Path.GetExtension(files[i]).ToLowerInvariant();
                if (ext != ".prefab" && ext != ".mat" && ext != ".asset") continue;
                string text = File.ReadAllText(files[i]);
                string orig = text;
                foreach (KeyValuePair<string, string> pair in map)
                    text = text.Replace(pair.Key, pair.Value);
                if (text != orig) File.WriteAllText(files[i], text);
            }
        }

        static void WireDemoScene()
        {
            if (!File.Exists(BattleScene)) return;
            Scene active = EditorSceneManager.GetActiveScene();
            Scene battle;
            bool additive = false;
            if (active.path == BattleScene)
            {
                battle = active;
            }
            else
            {
                battle = EditorSceneManager.OpenScene(BattleScene, OpenSceneMode.Additive);
                additive = true;
            }

            DemoView view = null;
            GameObject[] roots = battle.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                view = roots[i].GetComponentInChildren<DemoView>(true);
                if (view != null) break;
            }
            if (view == null)
            {
                Debug.LogWarning("[DouQuqu] Demo 场景没有 DemoView，特效组件未挂上。");
                if (additive) EditorSceneManager.CloseScene(battle, true);
                return;
            }

            if (view.GetComponent<BattleFx>() == null)
                view.gameObject.AddComponent<BattleFx>();
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(battle);
            EditorSceneManager.SaveScene(battle);
            if (additive) EditorSceneManager.CloseScene(battle, true);
        }
    }
}
