using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 把 Battle 预制体里的 2D Board 对到 3D 顶视镜头。
    /// Board 按开局档（默认 ×2）作者；镜头框哪一块世界，棋盘就平移缩放到哪一块。
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class BattleBoardFollow : MonoBehaviour
    {
        private RectTransform board;
        private RectTransform table;
        private RectTransform pit;
        private BattleCamera fitter;
        private float worldHalfW;
        private float worldHalfD;

        public static BattleBoardFollow Bind(RectTransform board, RectTransform pit, BattleCamera fitter, float openingScale)
        {
            if (board == null) return null;
            BattleBoardFollow follow = board.GetComponent<BattleBoardFollow>();
            if (follow == null) follow = board.gameObject.AddComponent<BattleBoardFollow>();
            follow.board = board;
            follow.pit = pit;
            follow.fitter = fitter;
            follow.table = FindNamed(board, "BattleTable") as RectTransform;
            if (follow.table == null) follow.table = board;
            float scale = Mathf.Max(0.01f, openingScale);
            follow.worldHalfW = Rules.DefaultArenaHalfWidth * scale;
            follow.worldHalfD = Rules.DefaultArenaHalfDepth * scale;
            follow.Apply();
            return follow;
        }

        private void LateUpdate()
        {
            Apply();
        }

        public void Apply()
        {
            if (board == null || pit == null) return;
            Camera cam = fitter != null ? fitter.Cam : null;
            if (cam == null) return;

            RectTransform map = table != null ? table : board;
            float tableW = Mathf.Max(1f, map.rect.width);
            float tableH = Mathf.Max(1f, map.rect.height);
            float pxX = tableW / Mathf.Max(0.01f, worldHalfW * 2f);
            float pxZ = tableH / Mathf.Max(0.01f, worldHalfD * 2f);

            float viewHalfD = Mathf.Max(0.01f, cam.orthographicSize);
            float viewHalfW = viewHalfD * Mathf.Max(0.01f, cam.aspect);
            float scaleW = pit.rect.width / Mathf.Max(0.01f, viewHalfW * 2f * pxX);
            float scaleH = pit.rect.height / Mathf.Max(0.01f, viewHalfD * 2f * pxZ);
            float scale = Mathf.Max(scaleW, scaleH);
            board.localScale = new Vector3(scale, scale, 1f);

            Vector3 focus = cam.transform.position;
            Vector2 pitPos = pit.anchoredPosition;
            if (pit.parent != board.parent)
                pitPos = (Vector2)board.parent.InverseTransformPoint(pit.position);
            board.anchoredPosition = pitPos - new Vector2(focus.x * pxX, focus.z * pxZ) * scale;
        }

        private static Transform FindNamed(Transform root, string objectName)
        {
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
