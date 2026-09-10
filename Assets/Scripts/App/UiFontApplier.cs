using UnityEngine;

namespace DouQuqu
{
    /// <summary>挂在页根上：进游戏时把子树 TMP 换成 UiFonts 里的字体。</summary>
    public sealed class UiFontApplier : MonoBehaviour
    {
        private void Awake()
        {
            UiFonts.ApplyTree(transform);
        }
    }
}
