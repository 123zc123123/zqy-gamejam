using System.Collections;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>本机蟋蟀撞人：震屏；单机再顿帧。完美落点撞击更重。不改联机模拟。</summary>
    public sealed class BattleHitFeel : MonoBehaviour
    {
        public const float HitShake = 0.48f;
        public const float HitShakeT = 0.22f;
        public const float HitStop = 0.08f;
        public const float PerfectShake = 1.05f;
        public const float PerfectShakeT = 0.42f;
        public const float PerfectStop = 0.16f;
        const float TimeScale = 0.04f;

        MatchController match;
        Coroutine stopping;

        public void Bind(MatchController controller)
        {
            match = controller;
        }

        public void Play(bool perfect)
        {
            BattleCamera cam = FindObjectOfType<BattleCamera>();
            if (cam != null)
                cam.Shake(perfect ? PerfectShake : HitShake, perfect ? PerfectShakeT : HitShakeT);
            if (match != null && match.RunMode != MatchRunMode.Offline) return;
            if (stopping != null) StopCoroutine(stopping);
            stopping = StartCoroutine(Hitstop(perfect ? PerfectStop : HitStop));
        }

        IEnumerator Hitstop(float seconds)
        {
            float prev = Time.timeScale;
            if (prev < 0.02f) prev = 1f;
            Time.timeScale = TimeScale;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = prev;
            stopping = null;
        }
    }
}
