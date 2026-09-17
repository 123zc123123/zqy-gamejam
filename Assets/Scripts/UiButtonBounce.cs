using System.Collections;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>任意按钮的一次性弹跳。收到物品、点中反馈都能用。</summary>
    public sealed class UiButtonBounce : MonoBehaviour
    {
        public const float Duration = 0.36f;

        Vector3 restScale = Vector3.one;
        Coroutine playing;

        public static void Play(RectTransform target)
        {
            if (target == null) return;
            UiButtonBounce bounce = target.GetComponent<UiButtonBounce>();
            if (bounce == null) bounce = target.gameObject.AddComponent<UiButtonBounce>();
            bounce.PlayNow();
        }

        public void PlayNow()
        {
            if (!isActiveAndEnabled) return;
            if (playing != null)
            {
                StopCoroutine(playing);
                transform.localScale = restScale;
            }
            else
            {
                restScale = transform.localScale;
                if (restScale.sqrMagnitude < 1e-6f) restScale = Vector3.one;
            }
            playing = StartCoroutine(BounceRoutine());
        }

        private void OnDisable()
        {
            if (playing != null)
            {
                StopCoroutine(playing);
                playing = null;
            }
            transform.localScale = restScale;
        }

        private IEnumerator BounceRoutine()
        {
            float t = 0f;
            while (t < Duration)
            {
                t += Time.unscaledDeltaTime;
                transform.localScale = restScale * ScaleAt(t / Duration);
                yield return null;
            }
            transform.localScale = restScale;
            playing = null;
        }

        /// <summary>1 → 放大 → 回缩 → 微弹 → 1。</summary>
        public static float ScaleAt(float u)
        {
            u = Mathf.Clamp01(u);
            if (u < 0.22f) return Mathf.Lerp(1f, 1.22f, u / 0.22f);
            if (u < 0.48f) return Mathf.Lerp(1.22f, 0.9f, (u - 0.22f) / 0.26f);
            if (u < 0.74f) return Mathf.Lerp(0.9f, 1.06f, (u - 0.48f) / 0.26f);
            return Mathf.Lerp(1.06f, 1f, (u - 0.74f) / 0.26f);
        }
    }
}
