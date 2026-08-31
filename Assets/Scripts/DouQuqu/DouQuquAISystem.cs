using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 用输入帧模拟真人操作的简易 AI：先蓄力，再松手跳跃，不直接改写速度或位置。
    /// 决策优先级是攻击范围内的蟋蟀、最近道具、最近的其他蟋蟀。
    /// </summary>
    public sealed class DouQuquAISystem
    {
        private const float DefaultAttackRange = 14f;
        private const float DefaultSafeEdgeMargin = 4f;
        private const float BoundarySampleStep = 0.5f;
        private const float ReleaseSafetyBuffer = 0.4f;

        // 每个槽位独立维护思考、蓄力和本次松手时刻，避免 AI 直接瞬移或瞬发。
        private readonly float[] decisionTimers = new float[DouQuquMatchController.MaxPlayers];
        private readonly float[] chargeTimers = new float[DouQuquMatchController.MaxPlayers];
        private readonly float[] releaseAfter = new float[DouQuquMatchController.MaxPlayers];
        private readonly bool[] charging = new bool[DouQuquMatchController.MaxPlayers];

        /// <summary>重置对局时清空每个槽位的 AI 状态。</summary>
        public void Reset(int count)
        {
            for (int i = 0; i < decisionTimers.Length; i++)
            {
                decisionTimers[i] = 0.35f + i * 0.17f;
                chargeTimers[i] = 0f;
                releaseAfter[i] = 0f;
                charging[i] = false;
            }
        }

        /// <summary>
        /// 只为存活且非真人控制的槽位生成输入帧；实际移动、碰撞和出圈仍由权威控制器负责。
        /// </summary>
        public void Tick(MatchState state, InputFrame[] inputs, float dt)
        {
            if (state == null || inputs == null || state.bugs == null || state.humanPlayers == null) return;
            MatchKnobs knobs = state.knobs ?? DouQuquRules.DefaultKnobs();
            dt = Mathf.Max(0f, dt);

            for (int i = 0; i < state.bugs.Length && i < inputs.Length; i++)
            {
                if (i >= state.humanPlayers.Length || state.humanPlayers[i]) continue;
                BugState bug = state.bugs[i];
                if (bug == null || !bug.alive)
                {
                    charging[i] = false;
                    chargeTimers[i] = 0f;
                    continue;
                }

                decisionTimers[i] -= dt;
                if (charging[i])
                {
                    // 蓄力期间若被碰撞，移动系统会把输入暂存为 pendingCharge；
                    // 等角色重新停稳前暂停计时，并重新计算边界安全方向和可蓄力时长。
                    if (bug.pendingCharge && !bug.charging)
                    {
                        bug.chargeDirection = KeepInsideDirection(state, bug, bug.chargeDirection, knobs);
                        chargeTimers[i] = 0f;
                        releaseAfter[i] = ChooseReleaseTime(state, bug, bug.chargeDirection, knobs);
                    }
                    // AI 和真人一样，按住期间持续保持 held，达到本次安全蓄力时刻后再发送 released。
                    chargeTimers[i] += dt;
                    bool release = chargeTimers[i] >= releaseAfter[i];
                    inputs[i] = new InputFrame(i, bug.chargeDirection, !release, release);
                    if (release)
                    {
                        charging[i] = false;
                        chargeTimers[i] = 0f;
                        decisionTimers[i] = 0.2f + 0.25f * (i + 1);
                    }
                    continue;
                }

                // 只有停稳且不在空中时才开始下一次真人式操作。
                if (decisionTimers[i] > 0f || bug.airborne || bug.velocity.sqrMagnitude > 0.1f) continue;

                Vector2 direction = ChooseDirection(state, bug, i);
                if (direction.sqrMagnitude <= 0.0001f) direction = Vector2.up;
                bug.chargeDirection = direction.normalized;
                releaseAfter[i] = ChooseReleaseTime(state, bug, bug.chargeDirection, knobs);
                chargeTimers[i] = 0f;
                charging[i] = true;
                inputs[i] = new InputFrame(i, bug.chargeDirection, true, false);
            }
        }

        /// <summary>
        /// 按“攻击范围内敌人 → 道具 → 最近敌人”选择目标，再对方向做安全边界修正。
        /// </summary>
        private Vector2 ChooseDirection(MatchState state, BugState bug, int id)
        {
            MatchKnobs knobs = state.knobs ?? DouQuquRules.DefaultKnobs();
            float attackRange = knobs.aiAttackRange > 0f ? knobs.aiAttackRange : DefaultAttackRange;

            // 第一优先级：攻击设定范围内最近的其他蟋蟀。
            BugState target = FindNearestEnemy(state, bug, attackRange);
            if (target != null)
                return KeepInsideDirection(state, bug, DirectionTo(bug.position, target.position), knobs);

            // 第二优先级：没有近身目标时，前往最近的存活道具。
            PickupState pickup = FindNearestPickup(state, bug);
            if (pickup != null)
                return KeepInsideDirection(state, bug, DirectionTo(bug.position, pickup.position), knobs);

            // 第三优先级：没有道具时锁定最近的蟋蟀，持续向其移动。
            target = FindNearestEnemy(state, bug, float.MaxValue);
            if (target != null)
                return KeepInsideDirection(state, bug, DirectionTo(bug.position, target.position), knobs);

            // 只剩自己时仍保持确定性移动方向；边界修正会把方向拉回场内。
            return KeepInsideDirection(state, bug, RandomDirection(id, state.tick), knobs);
        }

        /// <summary>根据当前方向到场地边缘的距离，计算本次允许的最长蓄力时间。</summary>
        private float ChooseReleaseTime(MatchState state, BugState bug, Vector2 direction, MatchKnobs knobs)
        {
            float cap = DouQuquRules.EffectiveChargeTime(knobs, bug);
            if (cap <= 0.0001f) return DouQuquMatchController.FixedDeltaTime;

            float maxSpeed = Mathf.Max(
                DouQuquRules.JumpSpeedMin(knobs),
                DouQuquRules.EffectiveChargeSpeed(knobs, bug) * cap);
            float safeRange = SafeTravelDistance(state, bug, direction, DouQuquRules.JumpRange(knobs, maxSpeed));
            safeRange = Mathf.Max(0f, safeRange - ReleaseSafetyBuffer);

            float minSpeed = DouQuquRules.JumpSpeedMin(knobs);
            float minRange = DouQuquRules.JumpRange(knobs, minSpeed);
            if (safeRange <= minRange + 0.001f)
                return Mathf.Min(cap, DouQuquMatchController.FixedDeltaTime);

            // JumpRange 随蓄力单调增加，用二分搜索得到不越界的最大蓄力时间。
            float low = 0f;
            float high = cap;
            for (int i = 0; i < 10; i++)
            {
                float mid = (low + high) * 0.5f;
                float speed = Mathf.Max(minSpeed, DouQuquRules.EffectiveChargeSpeed(knobs, bug) * mid);
                if (DouQuquRules.JumpRange(knobs, speed) <= safeRange) low = mid;
                else high = mid;
            }
            return Mathf.Clamp(Mathf.Max(DouQuquMatchController.FixedDeltaTime, low), 0f, cap);
        }

        /// <summary>
        /// 如果蟋蟀已经靠近边缘且方向指向外侧，就优先转向场内；远离边缘时仍保留目标优先级。
        /// </summary>
        private Vector2 KeepInsideDirection(MatchState state, BugState bug, Vector2 desired, MatchKnobs knobs)
        {
            if (desired.sqrMagnitude <= 0.0001f) return Vector2.up;
            desired.Normalize();

            Vector2 gradient = DouQuquRules.ArenaGradient(bug.position.x, bug.position.z);
            if (gradient.sqrMagnitude <= 0.0001f) return desired;

            Vector2 outward = gradient.normalized;
            float outwardPart = Vector2.Dot(desired, outward);
            float safeMargin = knobs.aiSafeEdgeMargin > 0f ? knobs.aiSafeEdgeMargin : DefaultSafeEdgeMargin;
            safeMargin += Mathf.Max(0f, bug.radius);
            bool nearEdge = DouQuquRules.ArenaSdf(bug.position.x, bug.position.z) > -safeMargin;
            // 即使还没进入可调安全边距，只要最小跳跃距离已经放不下，也必须先转向场内。
            float minimumJumpRange = DouQuquRules.JumpRange(knobs, DouQuquRules.JumpSpeedMin(knobs));
            bool minimumJumpDoesNotFit = SafeTravelDistance(state, bug, desired, minimumJumpRange) + ReleaseSafetyBuffer < minimumJumpRange;
            if ((!nearEdge && !minimumJumpDoesNotFit) || outwardPart <= 0f) return desired;

            Vector2 tangent = desired - outward * outwardPart;
            Vector2 inward = -outward;
            if (tangent.sqrMagnitude <= 0.0001f) return inward.normalized;
            // 保留少量切向分量，表现为玩家在边缘附近“绕开”而不是突然反向。
            return (tangent.normalized * 0.55f + inward * 0.9f).normalized;
        }

        /// <summary>沿预定方向采样场地 SDF，得到不出圈的最大平面滑行距离。</summary>
        private float SafeTravelDistance(MatchState state, BugState bug, Vector2 direction, float maxDistance)
        {
            if (maxDistance <= 0f || !DouQuquRules.InsideArena(bug.position)) return 0f;
            direction = direction.sqrMagnitude <= 0.0001f ? Vector2.up : direction.normalized;
            int samples = Mathf.Max(1, Mathf.CeilToInt(maxDistance / BoundarySampleStep));
            float lastSafe = 0f;
            float firstUnsafe = maxDistance;
            for (int i = 1; i <= samples; i++)
            {
                float distance = Mathf.Min(maxDistance, i * BoundarySampleStep);
                Vector3 point = bug.position + new Vector3(direction.x * distance, 0f, direction.y * distance);
                if (!DouQuquRules.InsideArena(point))
                {
                    firstUnsafe = distance;
                    break;
                }
                lastSafe = distance;
            }

            if (lastSafe >= maxDistance - 0.0001f) return maxDistance;
            // 在最后一个安全采样点和第一个危险点之间细化边界，减少采样步长造成的误差。
            float low = lastSafe;
            float high = firstUnsafe;
            for (int i = 0; i < 6; i++)
            {
                float mid = (low + high) * 0.5f;
                Vector3 point = bug.position + new Vector3(direction.x * mid, 0f, direction.y * mid);
                if (DouQuquRules.InsideArena(point)) low = mid;
                else high = mid;
            }
            return low;
        }

        private BugState FindNearestEnemy(MatchState state, BugState source, float maxRange)
        {
            BugState best = null;
            float bestDistance = float.MaxValue;
            float maxDistanceSqr = maxRange == float.MaxValue ? float.MaxValue : maxRange * maxRange;
            for (int i = 0; i < state.bugs.Length; i++)
            {
                BugState candidate = state.bugs[i];
                if (candidate == null || !candidate.alive || candidate == source) continue;
                Vector3 delta = candidate.position - source.position;
                delta.y = 0f;
                float distance = delta.sqrMagnitude;
                if (distance > maxDistanceSqr || distance >= bestDistance) continue;
                bestDistance = distance;
                best = candidate;
            }
            return best;
        }

        private PickupState FindNearestPickup(MatchState state, BugState source)
        {
            PickupState best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < state.pickups.Count; i++)
            {
                PickupState pickup = state.pickups[i];
                if (pickup == null || !pickup.alive || !DouQuquRules.InsideArena(pickup.position)) continue;
                Vector3 delta = pickup.position - source.position;
                delta.y = 0f;
                float distance = delta.sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = pickup;
            }
            return best;
        }

        private Vector2 DirectionTo(Vector3 source, Vector3 target)
        {
            Vector3 delta = target - source;
            return new Vector2(delta.x, delta.z).normalized;
        }

        private Vector2 RandomDirection(int id, int tick)
        {
            float angle = (id * 1.37f + tick * 0.071f) % (Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }
}
