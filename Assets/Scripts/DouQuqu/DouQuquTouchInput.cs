using UnityEngine;
using UnityEngine.UIElements;

namespace DouQuqu
{
    /// <summary>
    /// 默认反弹：点哪召出摇杆，按住计时，往后拉、朝反方向跳。杆头跟手指。
    /// </summary>
    public sealed class DouQuquTouchInput : MonoBehaviour
    {
        [SerializeField] private DouQuquMatchController match;
        [SerializeField] private DouQuquLanSession lan;
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private DouQuquStickTheme theme;
        [SerializeField] private int playerId;
        [SerializeField] private InputVersion inputVersion = InputVersion.Rebound;
        [SerializeField, Range(0.05f, 0.3f)] private float deadZone = 0.12f;

        private VisualElement root;
        private VisualElement pad;
        private VisualElement center;
        private VisualElement handle;
        private Label hint;
        private DouQuquBattleCamera battleCamera;
        private int pointerId = -1;
        private Vector2 pullDirection = Vector2.down;
        private Vector2 flyDirection = Vector2.up;
        private bool holding;
        private float stickPx = DouQuquStickTheme.PrototypeSummonedSize;
        private float knobPx = DouQuquStickTheme.PrototypeSummonedKnob;
        private float travelPx = DouQuquStickTheme.PrototypeSummonedTravel;
        private int lastScreenWidth;
        private int lastScreenHeight;

        public Vector2 FlyDirection => flyDirection;
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
            if (theme == null) theme = Resources.Load<DouQuquStickTheme>("DouQuquStickTheme");
        }

        private void OnEnable()
        {
            if (uiDocument == null) return;
            root = uiDocument.rootVisualElement;
            pad = root.Q<VisualElement>("stick-base") ?? root.Q<VisualElement>("direction-pad");
            center = root.Q<VisualElement>("stick-center") ?? root.Q<VisualElement>("direction-ring");
            handle = root.Q<VisualElement>("stick-knob") ?? root.Q<VisualElement>("direction-handle");
            hint = root.Q<Label>("stick-hint") ?? root.Q<Label>("touch-hint");
            if (root != null) root.pickingMode = PickingMode.Position;
            if (pad != null) pad.pickingMode = PickingMode.Ignore;
            ApplyTheme();
            HideStick();
            if (root == null) return;
            root.RegisterCallback<PointerDownEvent>(OnPointerDown);
            root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            root.RegisterCallback<PointerUpEvent>(OnPointerUp);
            root.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        }

        private void OnDisable()
        {
            if (root != null)
            {
                root.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                root.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                root.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                root.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
                root.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            }
            if (holding) SendInput(true);
            pointerId = -1;
            holding = false;
            HideStick();
        }

        private void Update()
        {
            if (holding) SendInput(false);
        }

        private void LateUpdate()
        {
            if (Screen.width == lastScreenWidth && Screen.height == lastScreenHeight) return;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            RefreshStickMetrics();
        }

        public void ApplyTheme()
        {
            if (pad == null) return;
            bool useFixed = inputVersion == InputVersion.Slide;
            if (center != null) center.style.display = useFixed ? DisplayStyle.Flex : DisplayStyle.None;
            ApplySlot(pad, theme != null && theme.HasBaseSprite ? theme.baseSprite : null,
                theme != null ? theme.baseFill : new Color(0.055f, 0.063f, 0.055f, 0.58f),
                theme != null ? theme.baseRim : new Color(0.769f, 0.647f, 0.455f, 0.40f));
            ApplySlot(center, theme != null && theme.HasCenterSprite ? theme.centerSprite : null,
                theme != null ? theme.centerFill : new Color(0.937f, 0.902f, 0.824f, 1f),
                Color.clear);
            ApplySlot(handle, theme != null && theme.HasKnobSprite ? theme.knobSprite : null,
                theme != null ? theme.knobFill : new Color(0.541f, 0.290f, 0.227f, 0.95f),
                Color.clear);
            if (hint != null)
            {
                hint.style.display = DisplayStyle.Flex;
                if (inputVersion == InputVersion.Rebound) hint.text = "往后拉，松手朝反方向跳";
                else if (inputVersion == InputVersion.Slide) hint.text = "从圆心拉出长度再松手";
                else hint.text = "推向要去的方向，按住蓄力";
            }
        }

        private static void ApplySlot(VisualElement element, Sprite sprite, Color fill, Color rim)
        {
            if (element == null) return;
            if (sprite != null)
            {
                element.style.backgroundImage = new StyleBackground(sprite);
                element.style.backgroundColor = Color.clear;
                element.style.borderLeftWidth = 0;
                element.style.borderRightWidth = 0;
                element.style.borderTopWidth = 0;
                element.style.borderBottomWidth = 0;
                element.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            else
            {
                element.style.backgroundImage = StyleKeyword.None;
                element.style.backgroundColor = fill;
                if (rim.a > 0.01f)
                {
                    element.style.borderLeftWidth = 2;
                    element.style.borderRightWidth = 2;
                    element.style.borderTopWidth = 2;
                    element.style.borderBottomWidth = 2;
                    element.style.borderLeftColor = rim;
                    element.style.borderRightColor = rim;
                    element.style.borderTopColor = rim;
                    element.style.borderBottomColor = rim;
                }
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (pointerId >= 0) return;
            if (match != null && !match.IsStarted) return;
            RefreshStickMetrics();
            pointerId = evt.pointerId;
            holding = true;
            root.CapturePointer(pointerId);
            ShowStickAt(evt.position);
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
            HideStick();
            if (root.HasPointerCapture(pointerId)) root.ReleasePointer(pointerId);
            pointerId = -1;
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (!holding || evt.pointerId != pointerId) return;
            holding = false;
            SendInput(true);
            HideStick();
            if (root.HasPointerCapture(pointerId)) root.ReleasePointer(pointerId);
            pointerId = -1;
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            RefreshStickMetrics();
        }

        private void RefreshStickMetrics()
        {
            if (root == null || pad == null) return;
            float panelWidth = root.layout.width;
            if (panelWidth < 8f) return;
            float phoneScale = panelWidth / DouQuquStickTheme.PrototypePhoneWidth;
            bool useFixed = inputVersion == InputVersion.Slide;
            stickPx = (theme != null ? theme.StickSize(useFixed) : DouQuquStickTheme.PrototypeSummonedSize) * phoneScale;
            knobPx = (theme != null ? theme.KnobSize(useFixed) : DouQuquStickTheme.PrototypeSummonedKnob) * phoneScale;
            travelPx = (theme != null ? theme.Travel(useFixed) : DouQuquStickTheme.PrototypeSummonedTravel) * phoneScale;
            pad.style.width = stickPx;
            pad.style.height = stickPx;
            pad.style.marginLeft = 0;
            SetRound(pad, stickPx);
            if (center != null)
            {
                float dot = Mathf.Max(6f, 10f * phoneScale);
                center.style.width = dot;
                center.style.height = dot;
                center.style.left = (stickPx - dot) * 0.5f;
                center.style.top = (stickPx - dot) * 0.5f;
                center.style.marginLeft = 0;
                center.style.marginTop = 0;
                SetRound(center, dot);
            }
            if (handle != null)
            {
                handle.style.width = knobPx;
                handle.style.height = knobPx;
                handle.style.marginLeft = 0;
                handle.style.marginTop = 0;
                SetRound(handle, knobPx);
            }
        }

        private void ShowStickAt(Vector2 panelPosition)
        {
            if (root == null || pad == null) return;
            Rect bound = root.worldBound;
            float localX = panelPosition.x - bound.x;
            float localY = panelPosition.y - bound.y;
            float left = Mathf.Clamp(localX - stickPx * 0.5f, 0f, Mathf.Max(0f, bound.width - stickPx));
            float top = Mathf.Clamp(localY - stickPx * 0.5f, 0f, Mathf.Max(0f, bound.height - stickPx));
            pad.style.left = left;
            pad.style.top = top;
            pad.style.bottom = StyleKeyword.Null;
            pad.style.display = DisplayStyle.Flex;
            SetHandleOffset(Vector2.zero);
        }

        private void HideStick()
        {
            if (pad != null) pad.style.display = DisplayStyle.None;
            SetHandleOffset(Vector2.zero);
        }

        private static void SetRound(VisualElement element, float size)
        {
            float radius = size * 0.5f;
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        private void UpdateDrag(Vector2 panelPosition)
        {
            if (pad == null) return;
            Rect rect = pad.worldBound;
            Vector2 offset = panelPosition - rect.center;
            float maxRadius = Mathf.Max(1f, travelPx);
            float distance = offset.magnitude;
            if (distance > maxRadius) offset = offset / distance * maxRadius;
            float mag = Mathf.Clamp01(distance / Mathf.Max(1f, stickPx * 0.5f));
            SetHandleOffset(offset);
            if (mag < deadZone) return;
            Vector2 pull = new Vector2(offset.x, -offset.y).normalized;
            pullDirection = pull;
            flyDirection = inputVersion == InputVersion.Rebound ? -pull : pull;
        }

        private void SetHandleOffset(Vector2 offset)
        {
            if (handle == null || pad == null) return;
            handle.style.left = stickPx * 0.5f - knobPx * 0.5f + offset.x;
            handle.style.top = stickPx * 0.5f - knobPx * 0.5f + offset.y;
        }

        private void SendInput(bool released)
        {
            if (match == null || !match.IsStarted) return;
            Vector2 dir = flyDirection.sqrMagnitude > 0.0001f ? flyDirection : Vector2.up;
            if (lan != null && lan.IsRunning)
                lan.SendInput(dir, !released, released, 0f, false);
            else
                match.SetInput(new InputFrame(playerId, dir, !released, released, 0, false, 0f));
        }
    }
}
