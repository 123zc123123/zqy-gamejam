using UnityEngine;
using UnityEngine.Rendering;

namespace DouQuqu
{
    /// <summary>
    /// 蓄力方向与落点预览箭头。表现层预制体，不参与碰撞。
    /// </summary>
    public sealed class DouQuquChargeArrow : MonoBehaviour
    {
        [SerializeField] private LineRenderer line;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color chargingColor = new Color(0.94f, 0.90f, 0.72f, 0.88f);
        [SerializeField] private Color chargingDim = new Color(0.77f, 0.65f, 0.45f, 0.45f);
        [SerializeField] private Color fullColor = new Color(0.98f, 0.86f, 0.45f, 0.95f);
        [SerializeField] private float minDistance = 0.35f;

        public void Apply(bool charging, float distance, float fill, Vector2 direction, Vector3 origin, float radius)
        {
            EnsureReady();
            if (!charging || distance < minDistance || direction.sqrMagnitude < 0.0001f)
            {
                Hide();
                return;
            }
            gameObject.SetActive(true);
            transform.position = origin;
            Vector2 dir = direction.normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            float head = Mathf.Min(1.35f, distance * 0.2f);
            float width = Mathf.Max(0.28f, radius * 0.55f);
            Vector3 tip = origin + new Vector3(dir.x, 0f, dir.y) * distance;
            Vector3 neck = tip - new Vector3(dir.x, 0f, dir.y) * head;
            Vector3 left = neck + new Vector3(perp.x, 0f, perp.y) * width;
            Vector3 right = neck - new Vector3(perp.x, 0f, perp.y) * width;
            line.enabled = true;
            line.positionCount = 5;
            line.SetPosition(0, origin);
            line.SetPosition(1, tip);
            line.SetPosition(2, left);
            line.SetPosition(3, tip);
            line.SetPosition(4, right);
            float shaft = Mathf.Lerp(0.12f, 0.28f, fill);
            line.startWidth = shaft;
            line.endWidth = shaft;
            Color color = fill >= 0.98f ? fullColor : Color.Lerp(chargingDim, chargingColor, fill);
            line.startColor = color;
            line.endColor = color;
        }

        public void Hide()
        {
            if (line != null) line.enabled = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public void EnsureReady()
        {
            if (line == null) line = GetComponent<LineRenderer>();
            if (line == null) line = gameObject.AddComponent<LineRenderer>();
            if (lineMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                lineMaterial = new Material(shader);
            }
            line.useWorldSpace = true;
            line.loop = false;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            line.sharedMaterial = lineMaterial;
        }
    }
}
