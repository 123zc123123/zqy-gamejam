using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 战斗 HUD 小摇杆：平时一直显示在战场底部；
    /// 点战场任意位置会把摇杆召唤到手指处，往后拉、松手朝反方向跳。
    /// </summary>
    public sealed class HudStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const float Size = 72f;
        private const float Knob = 28f;
        private const float Travel = 22f;
        private const float DeadZone = 0.12f;

        private MatchController match;
        private LanSession lan;
        private StickTheme theme;
        private float padSize = Size;
        private float knobSize = Knob;
        private float travel = Travel;
        private RectTransform pit;
        private RectTransform catcher;
        private RectTransform pad;
        private RectTransform handle;
        private RectTransform dir;
        private Canvas canvas;
        private int playerId;
        private Vector2 flyDirection = Vector2.up;
        private bool holding;
        private bool summoned;
        private static Sprite circleSprite;

        public static HudStick Create(RectTransform battlefield, Canvas hudCanvas, MatchController controller, int localPlayerId, LanSession session = null)
        {
            GameObject host = new GameObject("HudStick", typeof(RectTransform));
            RectTransform hostRect = host.GetComponent<RectTransform>();
            hostRect.SetParent(hudCanvas.transform, false);
            Stretch(hostRect);
            hostRect.SetAsLastSibling();

            HudStick stick = host.AddComponent<HudStick>();
            stick.match = controller;
            stick.lan = session;
            stick.pit = battlefield;
            stick.canvas = hudCanvas;
            stick.playerId = Mathf.Max(0, localPlayerId);
            stick.theme = Resources.Load<StickTheme>("Battle/Stick/StickTheme");
            float canvasW = 1080f;
            RectTransform canvasRect = hudCanvas.transform as RectTransform;
            if (canvasRect != null && canvasRect.rect.width > 1f)
                canvasW = canvasRect.rect.width;
            float phoneScale = canvasW / StickTheme.PrototypePhoneWidth;
            stick.padSize = (stick.theme != null ? stick.theme.StickSize() : Size) * phoneScale;
            stick.knobSize = (stick.theme != null ? stick.theme.KnobSize() : Knob) * phoneScale;
            stick.travel = (stick.theme != null ? stick.theme.Travel() : Travel) * phoneScale;
            float cap = canvasW * 0.28f;
            if (stick.padSize > cap && stick.padSize > 0.01f)
            {
                float shrink = cap / stick.padSize;
                stick.padSize *= shrink;
                stick.knobSize *= shrink;
                stick.travel *= shrink;
            }
            const float radiusScale = 0.7f;
            stick.padSize *= radiusScale;
            stick.knobSize *= radiusScale;
            stick.travel *= radiusScale;

            stick.catcher = CreateCatcher(hostRect);
            stick.pad = stick.CreatePad(stick.catcher);
            stick.dir = stick.CreateDir(stick.pad);
            stick.handle = stick.CreateHandle(stick.pad);
            stick.PlaceDefault();
            stick.ApplyLook(false);
            return stick;
        }

        private static RectTransform CreateCatcher(RectTransform parent)
        {
            GameObject go = new GameObject("StickCatcher", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            HudStickForwarder.Bind(go, parent.GetComponent<HudStick>());
            return rect;
        }

        private RectTransform CreatePad(RectTransform parent)
        {
            GameObject go = new GameObject("Pad", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(padSize, padSize);
            UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = CircleSprite();
            image.color = new Color(0.08f, 0.07f, 0.06f, 0.88f);
            image.raycastTarget = true;
            HudStickForwarder.Bind(go, this);
            return rect;
        }

        private RectTransform CreateDir(RectTransform padRect)
        {
            Sprite sprite = Resources.Load<Sprite>("Battle/Stick/Stick_Dir");
            if (sprite == null) return null;
            GameObject go = new GameObject("Dir", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(padRect, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(padSize, padSize);
            UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = new Color(0.22f, 0.18f, 0.12f, 1f);
            image.preserveAspect = true;
            image.raycastTarget = false;
            go.SetActive(true);
            return rect;
        }

        private RectTransform CreateHandle(RectTransform padRect)
        {
            GameObject go = new GameObject("Knob", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(padRect, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(knobSize, knobSize);
            UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
            ApplyStickSprite(image, theme != null ? theme.knobSprite : null, new Color(0.32f, 0.18f, 0.12f, 1f));
            if (theme != null && theme.knobSprite != null)
                image.color = new Color(0.38f, 0.24f, 0.16f, 1f);
            image.raycastTarget = false;
            return rect;
        }

        static void ApplyStickSprite(UnityEngine.UI.Image image, Sprite sprite, Color fallback)
        {
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = true;
                return;
            }
            image.sprite = CircleSprite();
            image.color = fallback;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static Sprite CircleSprite()
        {
            if (circleSprite != null) return circleSprite;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "HudStickCircleTex";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;

            float r = (size - 1) * 0.5f;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r;
                    float dy = y - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(r - d) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            circleSprite.name = "HudStickCircle";
            circleSprite.hideFlags = HideFlags.HideAndDontSave;
            return circleSprite;
        }

        private void LateUpdate()
        {
            SyncCatcher();
            if (!holding && !summoned) PlaceDefault();
            if (!holding) return;
            SendInput(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!enabled || DialogueBoxView.IsPlaying) return;
            SummonTo(eventData.position, eventData.pressEventCamera);
            summoned = true;
            holding = true;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
            flyDirection = Vector2.up;
            ApplyLook(true);
            SetDir(false, flyDirection);
            SendInput(false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!enabled || !holding || DialogueBoxView.IsPlaying) return;
            UpdateDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!enabled || !holding) return;
            UpdateDrag(eventData);
            holding = false;
            SendInput(true);
            if (handle != null) handle.anchoredPosition = Vector2.zero;
            ApplyLook(false);
            SetDir(false, flyDirection);
        }

        private void SummonTo(Vector2 screenPoint, Camera eventCamera)
        {
            if (pad == null || catcher == null) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(catcher, screenPoint, eventCamera, out local))
                return;
            float half = padSize * 0.5f;
            Rect area = catcher.rect;
            local.x = Mathf.Clamp(local.x, area.xMin + half, area.xMax - half);
            local.y = Mathf.Clamp(local.y, area.yMin + half, area.yMax - half);
            pad.anchorMin = new Vector2(0.5f, 0.5f);
            pad.anchorMax = new Vector2(0.5f, 0.5f);
            pad.pivot = new Vector2(0.5f, 0.5f);
            pad.anchoredPosition = local;
        }

        private void PlaceDefault()
        {
            SyncCatcher();
            if (pad == null || catcher == null) return;
            Rect area = catcher.rect;
            pad.anchoredPosition = new Vector2(0f, area.yMin + padSize * 0.5f + 18f);
        }

        private void SyncCatcher()
        {
            if (catcher == null || pit == null) return;
            Vector3[] corners = new Vector3[4];
            pit.GetWorldCorners(corners);
            Camera eventCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 min, max;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(catcher.parent as RectTransform, RectTransformUtility.WorldToScreenPoint(eventCam, corners[0]), eventCam, out min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(catcher.parent as RectTransform, RectTransformUtility.WorldToScreenPoint(eventCam, corners[2]), eventCam, out max);
            Vector2 center = (min + max) * 0.5f;
            Vector2 size = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
            catcher.anchorMin = new Vector2(0.5f, 0.5f);
            catcher.anchorMax = new Vector2(0.5f, 0.5f);
            catcher.pivot = new Vector2(0.5f, 0.5f);
            catcher.anchoredPosition = center;
            catcher.sizeDelta = size;
        }

        private void UpdateDrag(PointerEventData eventData)
        {
            if (pad == null || handle == null) return;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(pad, eventData.position, eventData.pressEventCamera, out local);
            if (local.magnitude > travel) local = local.normalized * travel;
            handle.anchoredPosition = local;
            float mag = local.magnitude / Mathf.Max(1f, padSize * 0.5f);
            if (mag < DeadZone)
            {
                SetDir(false, flyDirection);
                return;
            }
            flyDirection = InputDirectionSettings.ReverseDrag ? -local.normalized : local.normalized;
            SetDir(true, flyDirection);
        }

        void ApplyLook(bool active)
        {
            UnityEngine.UI.Image padImage = pad != null ? pad.GetComponent<UnityEngine.UI.Image>() : null;
            if (padImage != null)
                padImage.color = active
                    ? new Color(0.08f, 0.07f, 0.06f, 0.88f)
                    : new Color(0.08f, 0.07f, 0.06f, 0.32f);

            UnityEngine.UI.Image dirImage = dir != null ? dir.GetComponent<UnityEngine.UI.Image>() : null;
            if (dirImage != null)
                dirImage.color = active
                    ? new Color(0.22f, 0.18f, 0.12f, 1f)
                    : new Color(0.22f, 0.18f, 0.12f, 0.32f);

            UnityEngine.UI.Image knobImage = handle != null ? handle.GetComponent<UnityEngine.UI.Image>() : null;
            if (knobImage != null)
                knobImage.color = active
                    ? new Color(0.95f, 0.16f, 0.10f, 1f)
                    : new Color(0.72f, 0.22f, 0.16f, 0.42f);
        }

        void SetDir(bool aiming, Vector2 direction)
        {
            if (dir == null) return;
            dir.gameObject.SetActive(true);
            if (!aiming || direction.sqrMagnitude < 0.0001f)
            {
                dir.localEulerAngles = Vector3.zero;
                return;
            }
            float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            dir.localEulerAngles = new Vector3(0f, 0f, -angle);
        }

        private void SendInput(bool released)
        {
            if (!enabled) return;
            Vector2 dir = flyDirection.sqrMagnitude > 0.0001f ? flyDirection : Vector2.up;
            if (lan != null && lan.IsRunning)
            {
                lan.SendInput(dir, !released, released);
                return;
            }
            if (match == null || !match.IsStarted) return;
            match.SetInput(new InputFrame(playerId, dir, !released, released));
        }
    }

    /// <summary>把战场热区和底盘上的指针事件转给摇杆。</summary>
    public sealed class HudStickForwarder : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private HudStick stick;

        public static void Bind(GameObject target, HudStick owner)
        {
            HudStickForwarder forwarder = target.GetComponent<HudStickForwarder>();
            if (forwarder == null) forwarder = target.AddComponent<HudStickForwarder>();
            forwarder.stick = owner;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (stick != null) stick.OnPointerDown(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (stick != null) stick.OnDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (stick != null) stick.OnPointerUp(eventData);
        }
    }
}
