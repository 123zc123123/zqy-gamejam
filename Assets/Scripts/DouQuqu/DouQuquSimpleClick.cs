using UnityEngine;
using UnityEngine.EventSystems;

namespace DouQuqu
{
    /// <summary>给没有完整 Button 动画的图片挂点击。</summary>
    public sealed class DouQuquSimpleClick : MonoBehaviour, IPointerClickHandler
    {
        public System.Action Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Clicked != null) Clicked();
        }
    }
}
