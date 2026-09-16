using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 把 Battle 预制体里的 2D Board 钉在世界 XZ 上。
    /// BattleTable 对齐开局档场地，镜头只看不拖底图。
    /// </summary>
    public static class BattleBoardWorld
    {
        public const string RootName = "BattleWorld";
        public const float FloorY = -0.05f;

        public static RectTransform Place(RectTransform board, Camera battleCam, float openingScale)
        {
            if (board == null) return null;

            Transform root = EnsureRoot(battleCam);
            if (board.parent != root)
                board.SetParent(root, false);

            board.gameObject.SetActive(true);
            board.anchorMin = new Vector2(0.5f, 0.5f);
            board.anchorMax = new Vector2(0.5f, 0.5f);
            board.pivot = new Vector2(0.5f, 0.5f);
            board.anchoredPosition3D = Vector3.zero;
            board.localRotation = Quaternion.Euler(90f, 0f, 0f);
            board.localScale = Vector3.one;
            board.localPosition = new Vector3(0f, FloorY, 0f);

            Canvas canvas = board.GetComponent<Canvas>();
            if (canvas == null) canvas = board.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = battleCam;
            canvas.overrideSorting = true;
            canvas.sortingOrder = -100;
            canvas.pixelPerfect = false;
            canvas.enabled = true;

            GraphicRaycaster rays = board.GetComponent<GraphicRaycaster>();
            if (rays != null) rays.enabled = false;
            SilenceRaycasts(board);

            RectTransform table = FindNamed(board, "BattleTable") as RectTransform;
            if (table == null) table = board;
            Canvas.ForceUpdateCanvases();
            AlignTableToArena(board, table, openingScale);
            return board;
        }

        /// <summary>桌子铺满开局档矩形，中心在世界原点。镜头移动不会改这个。</summary>
        public static void AlignTableToArena(RectTransform board, RectTransform table, float openingScale)
        {
            if (board == null || table == null) return;
            float scale = Mathf.Max(0.01f, openingScale);
            float halfW = Rules.DefaultArenaHalfWidth * scale;
            float halfD = Rules.DefaultArenaHalfDepth * scale;

            Vector3 local = board.localScale;
            if (Mathf.Abs(local.x) < 0.0001f) local.x = 1f;
            if (Mathf.Abs(local.y) < 0.0001f) local.y = 1f;
            if (Mathf.Abs(local.z) < 0.0001f) local.z = 1f;
            board.localScale = local;

            Vector2 tableSpan = PlanarSpan(table);
            float scaleX = (halfW * 2f) / Mathf.Max(0.01f, tableSpan.x) * Mathf.Abs(local.x);
            float scaleY = (halfD * 2f) / Mathf.Max(0.01f, tableSpan.y) * Mathf.Abs(local.y);
            board.localScale = new Vector3(scaleX, scaleY, local.z);

            Vector3 tableCenter = PlanarCenter(table);
            Vector3 pos = board.position;
            board.position = new Vector3(pos.x - tableCenter.x, FloorY, pos.z - tableCenter.z);
        }

        public static Vector2 PlanarSpan(RectTransform rect)
        {
            Vector3[] corners = Corners(rect);
            return new Vector2(
                Mathf.Max(0.01f, Mathf.Abs(corners[2].x - corners[0].x)),
                Mathf.Max(0.01f, Mathf.Abs(corners[2].z - corners[0].z)));
        }

        public static Vector3 PlanarCenter(RectTransform rect)
        {
            Vector3[] corners = Corners(rect);
            return (corners[0] + corners[2]) * 0.5f;
        }

        public static Vector2 PlanarOffset(RectTransform from, RectTransform origin)
        {
            Vector3 delta = PlanarCenter(from) - PlanarCenter(origin);
            return new Vector2(delta.x, delta.z);
        }

        private static Vector3[] Corners(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            if (rect != null) rect.GetWorldCorners(corners);
            return corners;
        }

        private static Transform EnsureRoot(Camera battleCam)
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null) root = new GameObject(RootName);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Scene demo = SceneManager.GetSceneByName(SceneNames.BattleDemo);
            if (demo.IsValid() && demo.isLoaded && root.scene != demo)
                SceneManager.MoveGameObjectToScene(root, demo);
            else if (battleCam != null && battleCam.gameObject.scene.IsValid()
                && root.scene != battleCam.gameObject.scene)
                SceneManager.MoveGameObjectToScene(root, battleCam.gameObject.scene);

            return root.transform;
        }

        private static void SilenceRaycasts(RectTransform board)
        {
            Graphic[] graphics = board.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                if (graphics[i] != null) graphics[i].raycastTarget = false;
        }

        private static Transform FindNamed(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamed(root.GetChild(i), objectName);
                if (hit != null) return hit;
            }

            return null;
        }
    }
}
