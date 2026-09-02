using UnityEngine;
using UnityEngine.UIElements;

namespace DouQuqu
{
    /// <summary>
    /// 手机虚拟方向盘：按住方向盘中心并拖动，拖动方向是瞄准方向，
    /// 拖动距离是蓄力比例，松手时发送一次 released 让模拟层起跳。
    /// </summary>
    public sealed class DouQuquTouchInput : MonoBehaviour
    {
        [SerializeField] private DouQuquMatchController match;
        [SerializeField] private DouQuquLanSession lan;
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private int playerId;
        [SerializeField, Range(0.05f, 0.5f)] private float deadZone = 0.08f;

        private VisualElement pad;
        private VisualElement handle;
        private Label chargeLabel;
        private int pointerId = -1;
        private Vector2 direction = Vector2.up;
        private float charge01;
        private bool holding;

        public Vector2 Direction => direction;
        public float Charge01 => charge01;
        public bool IsHolding => holding;

        /// <summary>战斗场景加载后绑定持久网络会话以及本机被分配的玩家槽位。</summary>
        public void BindRuntime(DouQuquMatchController controller, DouQuquLanSession session, int localPlayerId)
        {
            match = controller;
            lan = session;
            playerId = Mathf.Max(0, localPlayerId);
        }

        private void Awake()
        {
            if (match == null) match = GetComponent<DouQuquMatchController>();
            if (lan == null) lan = GetComponent<DouQuquLanSession>();
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            if (uiDocument == null) return;
            VisualElement root = uiDocument.rootVisualElement;
            pad = root.Q<VisualElement>("direction-pad");
            handle = root.Q<VisualElement>("direction-handle");
            chargeLabel = root.Q<Label>("charge-label");
            if (pad == null) return;
            pad.RegisterCallback<PointerDownEvent>(OnPointerDown);
            pad.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            pad.RegisterCallback<PointerUpEvent>(OnPointerUp);
            pad.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            pad.RegisterCallback<GeometryChangedEvent>(OnPadGeometryChanged);
            ResetVisual();
        }

        private void OnDisable()
        {
            if (pad != null)
            {
                pad.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                pad.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                pad.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                pad.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
                pad.UnregisterCallback<GeometryChangedEvent>(OnPadGeometryChanged);
            }
            if (holding) SendInput(true);
            pointerId = -1;
            holding = false;
        }

        private void Update()
        {
            // 按住期间持续发送最新拖动距离；局域网客户端也只发送自己的槽位。
            if (holding) SendInput(false);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (pointerId >= 0) return;
            pointerId = evt.pointerId;
            holding = true;
            pad.CapturePointer(pointerId);
            UpdateDrag(evt.position);
            SendInput(false);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!holding || evt.pointerId != pointerId) return;
            UpdateDrag(evt.position);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!holding || evt.pointerId != pointerId) return;
            UpdateDrag(evt.position);
            holding = false;
            SendInput(true);
            ResetVisual();
            pad.ReleasePointer(pointerId);
            pointerId = -1;
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (!holding || evt.pointerId != pointerId) return;
            holding = false;
            SendInput(true);
            ResetVisual();
            pad.ReleasePointer(pointerId);
            pointerId = -1;
            evt.StopPropagation();
        }

        /// <summary>UI Toolkit 首次布局或屏幕旋转后重新居中手柄。</summary>
        private void OnPadGeometryChanged(GeometryChangedEvent evt)
        {
            if (!holding) ResetVisual();
        }

        /// <summary>把屏幕坐标转换为方向盘局部偏移，并把半径限制在圆盘范围内。</summary>
        private void UpdateDrag(Vector2 panelPosition)
        {
            if (pad == null) return;
            Rect rect = pad.worldBound;
            Vector2 center = rect.center;
            Vector2 offset = panelPosition - center;
            float maxRadius = Mathf.Max(1f, Mathf.Min(rect.width, rect.height) * 0.38f);
            float distance = offset.magnitude;
            if (distance > maxRadius) offset = offset / distance * maxRadius;
            float normalized = Mathf.Clamp01(distance / maxRadius);
            charge01 = normalized < deadZone ? 0f : normalized;
            // UI Toolkit 的屏幕 Y 轴向下，转换为游戏平面时取反，使“向上拖”对应世界 +Z。
            if (offset.sqrMagnitude > 0.0001f) direction = new Vector2(offset.x, -offset.y).normalized;
            SetHandleOffset(offset);
            if (chargeLabel != null) chargeLabel.text = "蓄力 " + Mathf.RoundToInt(charge01 * 100f) + "%";
        }

        private void SetHandleOffset(Vector2 offset)
        {
            if (handle == null) return;
            float width = handle.resolvedStyle.width;
            float height = handle.resolvedStyle.height;
            if (float.IsNaN(width) || width <= 0f) width = 72f;
            if (float.IsNaN(height) || height <= 0f) height = 72f;
            handle.style.left = pad.contentRect.width * 0.5f - width * 0.5f + offset.x;
            // style.top 同样使用屏幕坐标，拖动向上时手柄应向上移动。
            handle.style.top = pad.contentRect.height * 0.5f - height * 0.5f + offset.y;
        }

        private void ResetVisual()
        {
            charge01 = 0f;
            if (chargeLabel != null) chargeLabel.text = "按住拖动蓄力";
            if (pad != null && handle != null)
            {
                // OnEnable 可能早于首次布局；等 GeometryChangedEvent 后再计算中心。
                if (pad.contentRect.width <= 0f || pad.contentRect.height <= 0f) return;
                float width = handle.resolvedStyle.width;
                float height = handle.resolvedStyle.height;
                if (float.IsNaN(width) || width <= 0f) width = 72f;
                if (float.IsNaN(height) || height <= 0f) height = 72f;
                handle.style.left = pad.contentRect.width * 0.5f - width * 0.5f;
                handle.style.top = pad.contentRect.height * 0.5f - height * 0.5f;
            }
        }

        private void SendInput(bool released)
        {
            if (match == null || !match.IsStarted) return;
            if (lan != null && lan.IsRunning)
                lan.SendInput(direction, !released, released, charge01, true);
            else
                match.SetInput(new InputFrame(playerId, direction, !released, released, 0, true, charge01));
        }
    }
}
