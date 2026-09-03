using UnityEngine;
using UnityEngine.EventSystems;

namespace DouQuqu
{
    /// <summary>美术格子上的拖放入口，把指针事件转给育虫盘表现层。</summary>
    public sealed class DouQuquBreedingCell : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public int Index { get; set; }
        public DouQuquBreedingBoardView View { get; set; }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging) return;
            if (View != null) View.OnCellClicked(Index);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (View != null) View.BeginDrag(Index, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (View != null) View.Drag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (View != null) View.EndDrag(eventData);
        }
    }
}
