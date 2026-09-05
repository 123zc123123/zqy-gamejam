using UnityEngine;
using UnityEngine.SceneManagement;

namespace DouQuqu
{
    /// <summary>
    /// 红框里的棋盘底。旧 Ground / 围栏关掉，铺一块可替换的板。
    /// 正式图放到 Resources/DouQuqu/BattleBoard.png 即可，不必改代码。
    /// </summary>
    public static class DouQuquBattleBoard
    {
        public const string SlotName = "BattleBoard";
        public const string ResourcePath = "DouQuqu/BattleBoard";

        public static readonly Color Sand = new Color(0.76f, 0.62f, 0.40f, 1f);

        public static void Install()
        {
            HideLegacyArena();
            EnsureBoard();
        }

        private static void HideLegacyArena()
        {
            Scene demo = SceneManager.GetSceneByName(DouQuquSceneNames.BattleDemo);
            if (demo.IsValid() && demo.isLoaded)
            {
                GameObject[] roots = demo.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                    HideIn(roots[i].transform);
                return;
            }

            HideByName("Ground");
            HideByName("ArenaBorder_North");
            HideByName("ArenaBorder_South");
            HideByName("ArenaBorder_East");
            HideByName("ArenaBorder_West");
        }

        private static void HideIn(Transform root)
        {
            string objectName = root.name;
            if (objectName == "Ground" || objectName.StartsWith("ArenaBorder"))
                root.gameObject.SetActive(false);
            for (int i = 0; i < root.childCount; i++)
                HideIn(root.GetChild(i));
        }

        private static void HideByName(string objectName)
        {
            GameObject go = GameObject.Find(objectName);
            if (go != null) go.SetActive(false);
        }

        private static void EnsureBoard()
        {
            GameObject board = GameObject.Find(SlotName);
            if (board == null)
            {
                board = GameObject.CreatePrimitive(PrimitiveType.Plane);
                board.name = SlotName;
                board.transform.position = Vector3.zero;
                board.transform.rotation = Quaternion.identity;
                float planeSize = 10f;
                board.transform.localScale = new Vector3(
                    DouQuquRules.ArenaHalfWidth * 2f / planeSize,
                    1f,
                    DouQuquRules.ArenaHalfDepth * 2f / planeSize);
                Collider hit = board.GetComponent<Collider>();
                if (hit != null) Object.Destroy(hit);
                Scene demo = SceneManager.GetSceneByName(DouQuquSceneNames.BattleDemo);
                if (demo.IsValid() && demo.isLoaded)
                    SceneManager.MoveGameObjectToScene(board, demo);
            }

            Renderer renderer = board.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = MakeMaterial();
        }

        private static Material MakeMaterial()
        {
            Texture2D art = Resources.Load<Texture2D>(ResourcePath);
            if (art != null)
            {
                Shader textured = Shader.Find("Unlit/Texture");
                if (textured == null) textured = Shader.Find("Sprites/Default");
                Material painted = new Material(textured);
                painted.name = "DouQuquBattleBoard";
                painted.mainTexture = art;
                return painted;
            }

            Shader unlit = Shader.Find("Unlit/Texture");
            if (unlit == null) unlit = Shader.Find("Unlit/Color");
            Material placeholder = new Material(unlit);
            placeholder.name = "DouQuquBattleBoardPlaceholder";
            if (placeholder.HasProperty("_MainTex"))
                placeholder.mainTexture = MakeSand();
            placeholder.color = Color.white;
            return placeholder;
        }

        private static Texture2D MakeSand()
        {
            const int size = 128;
            Texture2D sand = new Texture2D(size, size, TextureFormat.RGB24, false)
            {
                name = "DouQuquBattleBoardSand",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color light = new Color(0.80f, 0.66f, 0.44f);
            Color dark = new Color(0.60f, 0.44f, 0.26f);
            Color rim = new Color(0.42f, 0.30f, 0.16f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f);
                    float v = y / (size - 1f);
                    float grain = Mathf.PerlinNoise(u * 9f, v * 9f);
                    Color color = Color.Lerp(light, dark, grain);
                    float edge = Mathf.Min(u, v, 1f - u, 1f - v);
                    if (edge < 0.08f)
                        color = Color.Lerp(rim, color, edge / 0.08f);
                    sand.SetPixel(x, y, color);
                }
            }

            sand.Apply(false, false);
            return sand;
        }
    }
}
