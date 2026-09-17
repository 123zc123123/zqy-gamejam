using System.Collections.Generic;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>客机表现插值；权威状态仍由快照覆盖，这里只动 Transform。</summary>
    public static class ClientVisualSmoothing
    {
        public static Vector3 Step(
            Vector3 current,
            Vector3 target,
            HashSet<int> initialized,
            int id,
            float snapDistance,
            float smoothing,
            float dt,
            bool enabled)
        {
            if (!enabled) return target;
            if (initialized == null) return target;
            float snapSqr = snapDistance * snapDistance;
            if (initialized.Add(id) || (target - current).sqrMagnitude >= snapSqr)
                return target;
            float blend = 1f - Mathf.Exp(-smoothing * Mathf.Max(0f, dt));
            return Vector3.LerpUnclamped(current, target, blend);
        }
    }
}
