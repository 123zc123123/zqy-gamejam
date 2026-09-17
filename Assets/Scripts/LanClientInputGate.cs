using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 客机输入发送策略：普通采样 30Hz，按下/松开边沿立刻发。
    /// 松开连发若干包，并在短时间内按 30Hz 重复 held=false，降低 UDP 单包丢失导致一直蓄力。
    /// </summary>
    public sealed class LanClientInputGate
    {
        public const float SampleInterval = 1f / 30f;
        public const float RepeatAfterReleaseSeconds = 0.2f;
        public const int ReleaseBurstCopies = 3;

        private bool hasSent;
        private bool lastHeld;
        private Vector2 lastDirection = Vector2.up;
        private float nextSendAt;
        private float repeatUntil;

        public int CopiesFor(float now, bool held, bool released)
        {
            bool edge = released || !hasSent || held != lastHeld;
            if (!edge && now < nextSendAt) return 0;
            return released ? ReleaseBurstCopies : 1;
        }

        public void NoteSent(float now, Vector2 direction, bool held, bool released)
        {
            hasSent = true;
            lastHeld = held;
            if (direction.sqrMagnitude > 0.0001f) lastDirection = direction;
            nextSendAt = now + SampleInterval;
            if (!held || released) repeatUntil = now + RepeatAfterReleaseSeconds;
            else repeatUntil = 0f;
        }

        public bool TryRepeat(float now, out Vector2 direction, out bool held, out bool released)
        {
            direction = lastDirection;
            held = false;
            released = true;
            if (!hasSent || lastHeld) return false;
            if (now >= repeatUntil || now < nextSendAt) return false;
            return true;
        }

        public void Reset()
        {
            hasSent = false;
            lastHeld = false;
            lastDirection = Vector2.up;
            nextSendAt = 0f;
            repeatUntil = 0f;
        }
    }
}
