using System;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 固定步长移动模型，不持有 GameObject 引用，未来可由表现层自由绑定蟋蟀和幼虫预制体。
    /// </summary>
    public sealed class DouQuquMovementSystem
    {
        // 以下是模拟阈值，不是表现调参：用于判断角色是否停稳，以及何时可以重新蓄力。
        private const float SettleSpeed = 0.06f;
        private const float Epsilon = 0.03f;

        /// <summary>兼容旧调用的一体化入口；控制器使用拆分入口以便在移动与蓄力之间插入碰撞。</summary>
        public void Tick(MatchState state, InputFrame[] inputs, float dt, Action<string, Vector3> emit, Action<BugState> onOut)
        {
            TickMotion(state, inputs, dt, emit, onOut);
            MarkBabyOutOfBounds(state, emit);
            TickCharge(state, inputs, dt);
        }

        /// <summary>推进一个移动子步中的位置和速度。</summary>
        public void TickMotion(MatchState state, InputFrame[] inputs, float dt, Action<string, Vector3> emit, Action<BugState> onOut)
        {
            for (int i = 0; i < state.bugs.Length; i++)
                StepBugMotion(state, state.bugs[i], inputs != null && i < inputs.Length ? inputs[i] : null, dt, emit, onOut);
            for (int i = 0; i < state.babies.Count; i++)
                StepBaby(state, state.babies[i], dt, emit);
        }

        /// <summary>应用一整帧的蓄力或松开决定。</summary>
        public void TickCharge(MatchState state, InputFrame[] inputs, float dt)
        {
            for (int i = 0; i < state.bugs.Length; i++)
            {
                StepBugCharge(state.knobs, state.bugs[i], inputs != null && i < inputs.Length ? inputs[i] : null, dt);
                DouQuquRules.TickStamina(state.knobs, state.bugs[i], dt);
            }
        }

        /// <summary>所有移动子步完成后应用出圈规则。</summary>
        public void MarkBabyOutOfBounds(MatchState state, Action<string, Vector3> emit)
        {
            for (int i = 0; i < state.babies.Count; i++)
            {
                BabyState baby = state.babies[i];
                if (!baby.alive || DouQuquRules.InsideArena(baby.position)) continue;
                baby.alive = false;
                baby.charging = false;
                baby.airborne = true;
                emit?.Invoke("baby-out", baby.position);
            }
        }

        private void StepBugMotion(MatchState state, BugState bug, InputFrame input, float dt, Action<string, Vector3> emit, Action<BugState> onOut)
        {
            if (!bug.alive)
            {
                bug.height = Mathf.Max(-2f, bug.height - dt * 2.4f);
                return;
            }

            // 保存本段运动的真实起点，碰撞系统用它做扫掠圆测试，避免高速穿透。
            bug.previousPosition = bug.position;
            DouQuquRules.TickBuffs(state.knobs, bug, dt);

            bool held = input != null && input.held;
            // 松开方向键或进入死区时保持上一次瞄准；原型只在收到非零方向时更新瞄准。
            // 每帧重置会让排队中的蓄力意外转向 +Z。
            Vector2 direction = input == null ? bug.chargeDirection : input.Direction;
            if (held && direction.sqrMagnitude > 0.0001f) bug.chargeDirection = direction.normalized;
            bug.holding = held;

            // 蓄力在所有移动子步后统一判断。若本段开始时正在蓄力，会保持原地直到 TickCharge；
            // 碰撞仍可清除蓄力标记，使下一子步恢复运动。
            if (bug.charging && !IsSettled(bug) && (input == null || !input.released))
            {
                bug.charging = false;
                bug.pendingCharge = held;
                bug.chargeTime = 0f;
            }
            if (bug.charging) return;

            IntegrateGroundOrAir(state.knobs, bug, dt);
            if (IsSettled(bug)) bug.lastHitId = -1;
            if (!DouQuquRules.InsideArena(bug.position)) onOut?.Invoke(bug);
        }

        private void StepBugCharge(MatchKnobs knobs, BugState bug, InputFrame input, float dt)
        {
            if (!bug.alive) return;
            StepCharge(knobs, bug, input, dt);
        }

        // 碰撞可能取消蓄力；pendingCharge 记录按键仍被按住，角色停稳后才恢复蓄力。
        private void StepCharge(MatchKnobs knobs, BugState bug, InputFrame input, float dt)
        {
            bool held = input != null && input.held;
            bool released = input != null && input.released;
            if (released && bug.charging)
            {
                if (bug.chargeTime + 1e-4f >= knobs.tChargeMin) Launch(knobs, bug);
                else
                {
                    bug.charging = false;
                    bug.chargeTime = 0f;
                }
                return;
            }
            if (bug.charging && !IsSettled(bug))
            {
                bug.charging = false;
                bug.pendingCharge = held;
                bug.chargeTime = 0f;
            }
            if (IsSettled(bug) && bug.pendingCharge && held)
            {
                bug.pendingCharge = false;
                BeginCharge(knobs, bug);
            }
            if (IsSettled(bug) && held && !bug.charging)
                BeginCharge(knobs, bug);
            if (!bug.charging) return;

            float cap = DouQuquRules.StaminaChargeTCap(knobs, bug);
            // 反弹：按住计时。蓄力不超过当前耐力能负担的比例。
            bug.chargeTime = Mathf.Min(cap, bug.chargeTime + dt);
            if (!released && held) return;
            if (bug.chargeTime + 1e-4f < knobs.tChargeMin)
            {
                bug.charging = false;
                bug.chargeTime = 0f;
                return;
            }
            Launch(knobs, bug);
        }

        // 地面运动受摩擦减速；空中运动沿用平面速度，并单独积分垂直抛物线。
        private void IntegrateGroundOrAir(MatchKnobs knobs, BugState bug, float dt)
        {
            if (bug.airborne)
            {
                bug.verticalVelocity -= DouQuquRules.Gravity(knobs) * dt;
                bug.height += bug.verticalVelocity * dt;
                bug.position += bug.velocity * dt;
                if (bug.height <= 0f && bug.verticalVelocity <= 0f)
                {
                    bug.height = 0f;
                    bug.verticalVelocity = 0f;
                    bug.airborne = false;
                    ApplyGroundFriction(knobs, bug, dt * 0.25f);
                }
            }
            else
            {
                ApplyGroundFriction(knobs, bug, dt);
                bug.position += bug.velocity * dt;
                bug.height = 0f;
                bug.verticalVelocity = 0f;
            }
        }

        // 幼虫自主行动：锁定最近敌人，停稳后蓄力，较短的攻击计时结束后发起冲撞。
        private void StepBaby(MatchState state, BabyState baby, float dt, Action<string, Vector3> emit)
        {
            if (!baby.alive) return;
            baby.previousPosition = baby.position;
            baby.attackCooldown = Mathf.Max(0f, baby.attackCooldown - dt);
            DouQuquRules.TickBuffs(state.knobs, baby, dt);
            if (state.elapsed >= baby.lifeEnd)
            {
                baby.alive = false;
                emit?.Invoke("baby-dead", baby.position);
                return;
            }

            BugState target = FindBabyTarget(state, baby);
            if (target != null)
            {
                Vector3 delta = target.position - baby.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.0001f) baby.chargeDirection = new Vector2(delta.x, delta.z).normalized;
            }

            if (baby.airborne)
            {
                baby.verticalVelocity -= DouQuquRules.Gravity(state.knobs) * dt;
                baby.height += baby.verticalVelocity * dt;
                baby.position += baby.velocity * dt;
                if (baby.height <= 0f && baby.verticalVelocity <= 0f)
                {
                    baby.height = 0f;
                    baby.verticalVelocity = 0f;
                    baby.airborne = false;
                    ApplyBabyFriction(state.knobs, baby, dt * 0.25f);
                }
            }
            else
            {
                ApplyBabyFriction(state.knobs, baby, dt);
                baby.position += baby.velocity * dt;
                baby.height = 0f;
                baby.verticalVelocity = 0f;
            }

            if (baby.attackCooldown <= 0f && !baby.airborne && IsBabySettled(baby))
            {
                if (!baby.charging) { baby.charging = true; baby.chargeTime = 0f; }
                baby.chargeTime = Mathf.Min(DouQuquRules.BabyChargeTime(state.knobs), baby.chargeTime + dt);
                if (baby.chargeTime >= DouQuquRules.BabyChargeTime(state.knobs) - 1e-4f)
                    LaunchBaby(state.knobs, baby);
            }
            else if (!IsBabySettled(baby))
            {
                baby.charging = false;
                baby.chargeTime = 0f;
            }

        }

        private BugState FindBabyTarget(MatchState state, BabyState baby)
        {
            BugState best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < state.bugs.Length; i++)
            {
                BugState candidate = state.bugs[i];
                if (!candidate.alive || candidate.id == baby.ownerId) continue;
                float distance = (candidate.position - baby.position).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = candidate; }
            }
            return best;
        }

        // 起跳把蓄力时间换算成平面冲量和垂直升力；质量只影响碰撞，不影响自身起跳速度。
        private void Launch(MatchKnobs knobs, BugState bug)
        {
            float cost = DouQuquRules.JumpStaminaCost(knobs, bug);
            if (bug.stamina + 1e-6f < cost)
            {
                bug.charging = false;
                bug.chargeTime = 0f;
                return;
            }
            bug.stamina = Mathf.Max(0f, bug.stamina - cost);
            float speed = DouQuquRules.JumpDeltaV(knobs, bug);
            Vector2 direction = bug.chargeDirection.sqrMagnitude > 0.0001f ? bug.chargeDirection.normalized : Vector2.up;
            bug.velocity = new Vector3(direction.x * speed, 0f, direction.y * speed);
            bug.initialSpeed = speed;
            bug.verticalVelocity = speed * DouQuquRules.TanTheta(knobs);
            bug.height = 0.02f;
            bug.airborne = true;
            bug.slideMu = knobs.mu;
            bug.chargeTime = 0f;
            bug.charging = false;
            bug.pendingCharge = false;
        }

        private void LaunchBaby(MatchKnobs knobs, BabyState baby)
        {
            float speed = DouQuquRules.BabyJumpSpeed(knobs, baby);
            Vector2 direction = baby.chargeDirection.sqrMagnitude > 0.0001f ? baby.chargeDirection.normalized : Vector2.up;
            baby.velocity = new Vector3(direction.x * speed, 0f, direction.y * speed);
            baby.initialSpeed = speed;
            baby.verticalVelocity = speed * DouQuquRules.TanTheta(knobs);
            baby.height = 0.02f;
            baby.airborne = true;
            baby.slideMu = knobs.mu;
            baby.chargeTime = 0f;
            baby.charging = false;
            baby.attackCooldown = DouQuquRules.BabyAttackCooldown(knobs);
        }

        private bool BeginCharge(MatchKnobs knobs, BugState bug)
        {
            if (!DouQuquRules.CanStartCharge(knobs, bug)) return false;
            bug.charging = true;
            bug.chargeTime = 0f;
            bug.pendingCharge = false;
            return true;
        }

        private bool IsSettled(BugState bug)
        {
            return !bug.airborne && bug.height <= Epsilon && new Vector2(bug.velocity.x, bug.velocity.z).magnitude < SettleSpeed;
        }

        private bool IsBabySettled(BabyState baby)
        {
            return !baby.airborne && baby.height <= Epsilon && new Vector2(baby.velocity.x, baby.velocity.z).magnitude < SettleSpeed;
        }

        private void ApplyGroundFriction(MatchKnobs knobs, BugState bug, float dt)
        {
            float speed = new Vector2(bug.velocity.x, bug.velocity.z).magnitude;
            if (speed < SettleSpeed) { bug.velocity = Vector3.zero; bug.initialSpeed = 0f; bug.hitTier = HitTier.None; return; }
            float next = speed - DouQuquRules.FrictionAcceleration(knobs, bug.slideMu > 0f ? bug.slideMu : knobs.mu) * dt;
            if (next <= SettleSpeed) { bug.velocity = Vector3.zero; bug.initialSpeed = 0f; bug.hitTier = HitTier.None; return; }
            bug.velocity *= next / speed;
        }

        private void ApplyBabyFriction(MatchKnobs knobs, BabyState baby, float dt)
        {
            float speed = new Vector2(baby.velocity.x, baby.velocity.z).magnitude;
            if (speed < SettleSpeed) { baby.velocity = Vector3.zero; baby.initialSpeed = 0f; baby.hitTier = HitTier.None; return; }
            float mu = baby.slideMu > 0f ? baby.slideMu : knobs.mu;
            float next = speed - DouQuquRules.FrictionAcceleration(knobs, mu) * dt;
            if (next <= SettleSpeed) { baby.velocity = Vector3.zero; baby.initialSpeed = 0f; baby.hitTier = HitTier.None; return; }
            baby.velocity *= next / speed;
        }
    }
}
