using UnityEngine;

namespace DouQuqu
{
    /// <summary>排行榜：绑 Ranking Prefab。名次数据未接，展示 Figma 样例行。</summary>
    public sealed class DouQuquRankingController : MonoBehaviour
    {
        public void BindPage(GameObject root)
        {
            if (root == null) return;
            DouQuquBottomNavBar.SuppressEmbedded(root.transform);
        }
    }
}
