using UnityEngine;
using UnityEngine.UI;

namespace ZqyGameJam.UI.QuquXiangqing
{
    public sealed class QuquXiangqingView : MonoBehaviour
    {
        public Button sellButton;
        public Button storeButton;
        public Button closeButton;

        private void Awake()
        {
            Bind(sellButton, "sell");
            Bind(storeButton, "store");
            Bind(closeButton, "close");
        }

        private static void Bind(Button button, string action)
        {
            if (button != null) button.onClick.AddListener(() => Debug.Log("蛐蛐详情页 action: " + action));
        }
    }
}
