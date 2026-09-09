using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 成虫单位：预制体是摆放真源。根不缩放；局内只跟位置、跳跃高度和成长。
    /// 预制体里 Body / 脚下圈 / 耐力条都正对镜头，方便对大小和位置。
    /// 进战斗后再把圈和耐力条躺到地面上。
    /// </summary>
    [ExecuteAlways]
    public sealed class CricketUnit : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private GroundMarker marker;
        [SerializeField] private StaminaBar bar;

        private bool captured;
        private Vector3 bodyRestPosition;
        private Vector3 bodyRestScale = Vector3.one;
        private Vector3 markerRestPosition;
        private Vector3 markerRestScale = Vector3.one;
        private Vector3 barRestPosition;
        private Vector3 barRestScale = Vector3.one;

        public Transform Body
        {
            get
            {
                Bind();
                return body;
            }
        }

        public GameObject BodyObject
        {
            get
            {
                Transform t = Body;
                return t != null ? t.gameObject : null;
            }
        }

        public GroundMarker Marker
        {
            get
            {
                Bind();
                return marker;
            }
        }

        public StaminaBar Bar
        {
            get
            {
                Bind();
                return bar;
            }
        }

        private void Awake()
        {
            Bind();
            CaptureAuthored();
        }

        private void OnEnable()
        {
            Bind();
            if (!Application.isPlaying) ShowAuthored();
        }

        public void Bind()
        {
            if (body == null)
            {
                Transform found = transform.Find("Body");
                if (found != null) body = found;
            }
            if (marker == null) marker = GetComponentInChildren<GroundMarker>(true);
            if (bar == null) bar = GetComponentInChildren<StaminaBar>(true);
        }

        public void CaptureAuthored()
        {
            Bind();
            if (body != null)
            {
                bodyRestPosition = body.localPosition;
                bodyRestScale = body.localScale;
            }
            if (marker != null)
            {
                markerRestPosition = marker.transform.localPosition;
                markerRestScale = marker.transform.localScale;
            }
            if (bar != null)
            {
                barRestPosition = bar.transform.localPosition;
                barRestScale = bar.transform.localScale;
            }
            captured = true;
        }

        /// <summary>局内：根跟着虫子走，圈和条躺到地面，缩放按预制体 × 成长。</summary>
        public void ApplyMotion(float jumpHeight, float grow)
        {
            if (!captured) CaptureAuthored();
            grow = Mathf.Max(0.05f, grow);

            if (body != null)
            {
                Vector3 pos = bodyRestPosition;
                pos.y += jumpHeight;
                body.localPosition = pos;
                body.localScale = bodyRestScale * grow;
            }

            if (marker != null)
            {
                Vector3 p = markerRestPosition;
                marker.transform.localPosition = new Vector3(p.x, 0.03f, p.y);
                marker.transform.localScale = markerRestScale * grow;
            }

            if (bar != null)
            {
                Vector3 p = barRestPosition;
                bar.transform.localPosition = new Vector3(p.x, 0.1f, p.y);
                bar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                bar.transform.localScale = barRestScale * grow;
            }
        }

        /// <summary>脚下圈躺在地面，阴影朝向跟 Body 走。</summary>
        public void AlignMarkerToBody()
        {
            Bind();
            if (marker == null || body == null) return;
            marker.transform.rotation = body.rotation;
        }

        /// <summary>编辑器里不改 Transform，只保证圈和条看得见。</summary>
        public void ShowAuthored()
        {
            Bind();
            transform.localScale = Vector3.one;
            if (marker != null) marker.ShowAuthored();
            if (bar != null) bar.ShowAuthored();
        }
    }
}
