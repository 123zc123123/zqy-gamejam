using System;
using System.Collections.Generic;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>负责巢穴→蛋→幼虫的生命周期和确定性计时器。</summary>
    public sealed class DouQuquNestSystem
    {
        /// <summary>推进蛋、幼虫寿命和巢穴生成计时。</summary>
        public void TickBeforeCollision(MatchState state, float dt, Action<string, Vector3> emit)
        {
            // 控制器在移动/碰撞子步后调用；方法名为兼容第一版迁移代码而保留。
            StepEggs(state, dt, emit);
            StepBabyLife(state, emit);
            StepNestSpawn(state, emit);
        }

        /// <summary>处理巢穴爆裂、清理死亡实体并结束本轮巢穴链。</summary>
        public void TickAfterCollision(MatchState state, Action<string, Vector3> emit)
        {
            if (state.nest != null && !state.nest.alive)
            {
                SpawnEggs(state, state.nest.position, emit);
                state.nest = null;
                state.nestChainActive = true;
                state.pendingNestOwnerId = -1;
            }
            state.eggs.RemoveAll(e => !e.alive);
            state.babies.RemoveAll(b => !b.alive);
            if (state.nestChainActive && !HasLiveChain(state))
            {
                state.nestChainActive = false;
                state.lastNestClearAt = state.elapsed;
                state.nextNestAt = state.elapsed + Mathf.Max(0f, state.knobs.nestGap);
            }
        }

        // 同一时间只允许一条巢穴→蛋→幼虫链存在，狂暴阶段也不会生成巢穴。
        private void StepNestSpawn(MatchState state, Action<string, Vector3> emit)
        {
            if (state.nest != null || HasLiveChain(state) || DouQuquRules.IsRage(state.knobs, state.elapsed) || state.elapsed + 1e-9f < state.nextNestAt) return;
            state.nest = new NestState
            {
                position = PlaceNest(state),
                hp = Mathf.Max(0f, state.knobs.nestHP),
                alive = true
            };
            state.nextNestAt = float.MaxValue;
            emit?.Invoke("nest-spawn", state.nest.position);
        }

        // 巢穴爆裂时一次性生成蛋，并为每个蛋保存独立的孵化时间和拥有者。
        private void SpawnEggs(MatchState state, Vector3 center, Action<string, Vector3> emit)
        {
            int count = Mathf.Max(0, state.knobs.nestEggN);
            System.Random random = new System.Random(state.randomSeed + state.tick * 7919);
            float[] hatches = DouQuquRules.EggHatchTimes(count, state.knobs, random);
            List<Vector3> positions = new List<Vector3>(count);
            float pad = state.knobs.eggR + 0.08f;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = Vector3.zero;
                bool placed = false;
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    float x = (float)(random.NextDouble() * 2.0 - 1.0) * DouQuquRules.ArenaHalfWidth;
                    float z = (float)(random.NextDouble() * 2.0 - 1.0) * DouQuquRules.ArenaHalfDepth;
                    if (DouQuquRules.ArenaSdf(x, z) > -pad) continue;
                    bool packed = false;
                    for (int j = 0; j < positions.Count; j++)
                        if (Vector2.Distance(new Vector2(x, z), new Vector2(positions[j].x, positions[j].z)) < Mathf.Max(0.7f, state.knobs.eggR * 2f)) { packed = true; break; }
                    if (packed) continue;
                    position = new Vector3(x, 0f, z);
                    placed = true;
                    break;
                }
                if (!placed) position = DouQuquRules.ClampInsideArena(center, pad);
                positions.Add(position);
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                Vector3 velocity = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * state.knobs.eggScatterV;
                state.eggs.Add(new EggState
                {
                    position = position,
                    previousPosition = position,
                    velocity = velocity,
                    ownerId = state.pendingNestOwnerId,
                    hatchAt = state.elapsed + hatches[i],
                    alive = true
                });
            }
            emit?.Invoke("nest-eggs", center);
        }

        // 蛋先受摩擦和碰撞影响；到达 hatchAt 后转成带寿命的幼虫。
        private void StepEggs(MatchState state, float dt, Action<string, Vector3> emit)
        {
            float friction = DouQuquRules.FrictionAcceleration(state.knobs);
            for (int i = 0; i < state.eggs.Count; i++)
            {
                EggState egg = state.eggs[i];
                if (!egg.alive) continue;
                egg.previousPosition = egg.position;
                float speed = new Vector2(egg.velocity.x, egg.velocity.z).magnitude;
                if (speed > 1e-5f)
                {
                    float next = Mathf.Max(0f, speed - friction * dt);
                    egg.velocity *= next / speed;
                }
                else egg.velocity = Vector3.zero;
                egg.position += egg.velocity * dt;
                if (!DouQuquRules.InsideArena(egg.position))
                {
                    egg.alive = false;
                    emit?.Invoke("egg-out", egg.position);
                    continue;
                }
                if (state.elapsed + 1e-9f >= egg.hatchAt)
                {
                    egg.alive = false;
                    BabyState baby = new BabyState(state.nextBabyId++, egg.position, egg.ownerId, state.elapsed + state.knobs.babyLifeT, state.knobs)
                    {
                        velocity = egg.velocity * 0.25f,
                        attackCooldown = 0f,
                        slideMu = state.knobs.mu,
                        chargeDirection = new Vector2(egg.velocity.x, egg.velocity.z).sqrMagnitude > 0.001f ? new Vector2(egg.velocity.x, egg.velocity.z).normalized : Vector2.up
                    };
                    state.babies.Add(baby);
                    emit?.Invoke("egg-hatch", egg.position);
                }
            }
        }

        // 生命周期独立于移动系统，保证幼虫在没有移动时也会准时死亡。
        private void StepBabyLife(MatchState state, Action<string, Vector3> emit)
        {
            for (int i = 0; i < state.babies.Count; i++)
            {
                BabyState baby = state.babies[i];
                if (baby.alive && state.elapsed + 1e-9f >= baby.lifeEnd)
                {
                    baby.alive = false;
                    baby.charging = false;
                    emit?.Invoke("baby-dead", baby.position);
                }
            }
        }

        private Vector3 PlaceNest(MatchState state)
        {
            return DouQuquRules.PlacePoint(new System.Random(state.randomSeed + state.tick * 31), state.bugs, null, 6f);
        }

        private bool HasLiveChain(MatchState state)
        {
            for (int i = 0; i < state.eggs.Count; i++) if (state.eggs[i].alive) return true;
            for (int i = 0; i < state.babies.Count; i++) if (state.babies[i].alive) return true;
            return false;
        }
    }

}
