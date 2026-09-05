using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 战斗 HUD 小摇杆：平时一直显示在战场底部；
    /// 点战场任意位置会把摇杆召唤到手指处，往后拉、松手朝反方向跳。
    /// </summary>
    public sealed class DouQuquHudStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const float Size = 72f;
        private const float Knob = 28f;
        private const float Travel = 22f;
        private const float DeadZone = 0.12f;

        private DouQuquMatchController match;
        private RectTransform pit;
        private RectTransform catcher;
        private RectTransform pad;
        private RectTransform handle;
        private Canvas canvas;
        private int playerId;
        private Vector2 flyDirection = Vector2.up;
        private bool holding;
        private bool summoned;
        private static Sprite circleSprite;

        public static DouQuquHudStick Create(RectTransform battlefield, Canvas hudCanvas, DouQuquMatchController controller, int localPlayerId)
        {
            GameObject host = new GameObject("HudStick", typeof(RectTransform));
            RectTransform hostRect = host.GetComponent<RectTransform>();
            hostRect.SetParent(hudCanvas.transform, false);
            Stretch(hostRect);
            hostRect.SetAsLastSibling();

            DouQuquHudStick stick = host.AddComponent<DouQuquHudStick>();
            stick.match = controller;
            stick.pit = battlefield;
            stick.canvas = hudCanvas;
            stick.playerId = Mathf.Max(0, localPlayerId);

            stick.catcher = CreateCatcher(hostRect);
            stick.pad = CreatePad(stick.catcher);
            stick.handle = CreateHandle(stick.pad);
            stick.PlaceDefault();
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
            DouQuquHudStickForwarder.Bind(go, parent.GetComponent<DouQuquHudStick>());
            return rect;
        }

        private static RectTransform CreatePad(RectTransform parent)
        {
            GameObject go = new GameObject("Pad", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Size, Size);
            UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = CircleSprite();
            image.color = new Color(0.06f, 0.07f, 0.06f, 0.72f);
            image.raycastTarget = true;
            DouQuquHudStick owner = parent.GetComponentInParent<DouQuquHudStick>();
            DouQuquHudStickForwarder.Bind(go, owner);
            return rect;
        }

        private static RectTransform CreateHandle(RectTransform pad)
        {
            GameObject go = new GameObject("Knob", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(pad, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(Knob, Knob);
            UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = CircleSprite();
            image.color = new Color(0.54f, 0.29f, 0.23f, 0.95f);
            image.raycastTarget = false;
            return rect;
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
            SummonTo(eventData.position, eventData.pressEventCamera);
            summoned = true;
            holding = true;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
            flyDirection = Vector2.up;
            SendInput(false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!holding) return;
            UpdateDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!holding) return;
            UpdateDrag(eventData);
            holding = false;
            SendInput(true);
            if (handle != null) handle.anchoredPosition = Vector2.zero;
        }

        private void SummonTo(Vector2 screenPoint, Camera eventCamera)
        {
            if (pad == null || catcher == null) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(catcher, screenPoint, eventCamera, out local))
                return;
            float half = Size * 0.5f;
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
            pad.anchoredPosition = new Vector2(0f, area.yMin + Size * 0.5f + 18f);
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
            if (local.magnitude > Travel) local = local.normalized * Travel;
            handle.anchoredPosition = local;
            float mag = local.magnitude / Mathf.Max(1f, Size * 0.5f);
            if (mag < DeadZone) return;
            flyDirection = -local.normalized;
        }

        private void SendInput(bool released)
        {
            if (match == null || !match.IsStarted) return;
            Vector2 dir = flyDirection.sqrMagnitude > 0.0001f ? flyDirection : Vector2.up;
            match.SetInput(new InputFrame(playerId, dir, !released, released));
        }
    }

    /// <summary>把战场热区和底盘上的指针事件转给摇杆。</summary>
    public sealed class DouQuquHudStickForwarder : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private DouQuquHudStick stick;

        public static void Bind(GameObject target, DouQuquHudStick owner)
        {
            DouQuquHudStickForwarder forwarder = target.GetComponent<DouQuquHudStickForwarder>();
            if (forwarder == null) forwarder = target.AddComponent<DouQuquHudStickForwarder>();
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
