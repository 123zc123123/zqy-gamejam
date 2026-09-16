using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 任务说明遮罩：点空白关闭；点其他可点按钮时先关说明，再执行该按钮。
    /// </summary>
    public sealed class QuestHelpPopup : MonoBehaviour, IPointerClickHandler
    {
        public Button ignoreButton;

        public void OnPointerClick(PointerEventData eventData)
        {
            gameObject.SetActive(false);
            if (eventData == null || EventSystem.current == null) return;

            List<RaycastResult> hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                GameObject hit = hits[i].gameObject;
                if (hit == null) continue;
                Button button = hit.GetComponentInParent<Button>();
                if (button == null || !button.interactable) continue;
                if (ignoreButton != null && button == ignoreButton) return;
                button.onClick.Invoke();
                return;
            }
        }
    }
}
