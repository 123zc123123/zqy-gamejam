using System;
using System.Collections.Generic;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>负责饲料球、限时道具、生成位置约束和拾取效果。</summary>
    public sealed class EconomySystem
    {
        /// <summary>按对局时间补饲料球和道具。拾取在移动子步里单独结算。</summary>
        public void Tick(MatchState state, Action<BugState> addGrow, Action<string, Vector3> emit)
        {
            SpawnHearts(state, emit);
            SpawnItems(state, emit);
        }

        /// <summary>重置后创建确定性的开局饲料球。</summary>
        public void SeedHearts(MatchState state)
        {
            state.pickups.Clear();
            state.nextPickupId = 0;
            int count = Mathf.Min(state.knobs.heartStart, state.knobs.heartCap);
            for (int i = 0; i < count; i++)
                state.pickups.Add(new PickupState(state.nextPickupId++, PlacePoint(state, state.knobs.heartMinEdge, false), "heart"));
        }

        // 只看地面 XZ，不看高度。用本子步起点→终点扫掠，空中擦过也算。
        // 多名角色同时压上时按 bugs 数组顺序，先到先得。
        public void ResolvePickups(MatchState state, Action<BugState> addGrow, Action<string, Vector3> emit)
        {
            if (state == null || state.pickups == null) return;
            for (int i = state.pickups.Count - 1; i >= 0; i--)
            {
                PickupState pickup = state.pickups[i];
                if (!pickup.alive) continue;
                float pickupR = pickup.kind == "heart" ? 0.3f : state.knobs.itemR;
                for (int b = 0; b < state.bugs.Length; b++)
                {
                    BugState bug = state.bugs[b];
                    if (!bug.alive) continue;
                    if (!SweepHits(bug.previousPosition, bug.position, pickup.position, bug.radius + pickupR)) continue;
                    if (pickup.kind == "heart") addGrow?.Invoke(bug);
                    else Rules.ApplyItem(state.knobs, bug, pickup.kind);
                    pickup.alive = false;
                    emit?.Invoke("pickup:" + pickup.kind, pickup.position);
                    break;
                }
                if (pickup.alive && Rules.BabyCanLoot(state.knobs))
                {
                    for (int b = 0; b < state.babies.Count; b++)
                    {
                        BabyState baby = state.babies[b];
                        if (!baby.alive) continue;
                        if (!SweepHits(baby.previousPosition, baby.position, pickup.position, baby.radius + pickupR)) continue;
                        if (pickup.kind == "heart") AddBabyGrow(state.knobs, baby);
                        else Rules.ApplyItem(state.knobs, baby, pickup.kind);
                        pickup.alive = false;
                        emit?.Invoke("pickup:" + pickup.kind, pickup.position);
                        break;
                    }
                }
            }
            state.pickups.RemoveAll(p => !p.alive);
        }

        static bool SweepHits(Vector3 from, Vector3 to, Vector3 target, float radius)
        {
            Vector2 a = new Vector2(from.x, from.z);
            Vector2 b = new Vector2(to.x, to.z);
            Vector2 p = new Vector2(target.x, target.z);
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            float t = lengthSq < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
            Vector2 closest = a + ab * t;
            return (p - closest).sqrMagnitude <= radius * radius;
        }

        private void AddBabyGrow(MatchKnobs knobs, BabyState baby)
        {
            if (baby.grow < 6) baby.grow++;
            baby.score++;
            Rules.RefreshBabyBody(knobs, baby);
        }

        // 饲料球补充受场上存量上限限制，并延迟到开放阶段，保持原型节奏。
        private void SpawnHearts(MatchState state, Action<string, Vector3> emit)
        {
            int live = Count(state, "heart");
            if (!Rules.ShouldRefillHeart(state.knobs, state.elapsed, state.lastHeartAt, live)) return;
            PickupState pickup = new PickupState(state.nextPickupId++, PlacePoint(state, state.knobs.heartMinEdge, false), "heart");
            state.pickups.Add(pickup);
            state.lastHeartAt = state.elapsed;
            emit?.Invoke("heart-spawn", pickup.position);
        }

        // 通过 nextItemIndex 消耗道具时间表；即使某个 Tick 延迟，也只补发一次，
        // 不会重复生成同一时间点的道具。
        private void SpawnItems(MatchState state, Action<string, Vector3> emit)
        {
            int live = CountItems(state);
            List<string> due = Rules.DueItemSpawns(state.knobs, state.elapsed, ref state.nextItemIndex, ref state.lastItemKind, live, stateRandom(state));
            for (int i = 0; i < due.Count; i++)
            {
                string kind = Rules.IsRage(state.knobs, state.elapsed) ? "shield" : due[i];
                if (kind == "size") kind = "shield";
                PickupState pickup = new PickupState(state.nextPickupId++, PlacePoint(state, state.knobs.itemMinEdge, true), kind);
                state.pickups.Add(pickup);
                emit?.Invoke("item-spawn:" + kind, pickup.position);
            }
        }

        private System.Random stateRandom(MatchState state)
        {
            // 位置随机性由对局种子决定；这里使用确定性派生随机数，系统本身不保存随机状态。
            return new System.Random(state.randomSeed + state.nextPickupId * 7919 + state.nextItemIndex);
        }

        private int Count(MatchState state, string kind)
        {
            int count = 0;
            for (int i = 0; i < state.pickups.Count; i++) if (state.pickups[i].alive && state.pickups[i].kind == kind) count++;
            return count;
        }

        private int CountItems(MatchState state)
        {
            int count = 0;
            for (int i = 0; i < state.pickups.Count; i++) if (state.pickups[i].alive && state.pickups[i].kind != "heart") count++;
            return count;
        }

        // 先多次尝试合法位置，失败后回退到场地内夹取点，
        // 确保场地拥挤时游戏仍能继续推进。
        private Vector3 PlacePoint(MatchState state, float margin, bool ring)
        {
            System.Random random = new System.Random(state.randomSeed + state.nextPickupId * 7919 + state.tick * 17);
            for (int attempt = 0; attempt < 80; attempt++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = ring
                    ? state.knobs.itemRingMin + (float)random.NextDouble() * (state.knobs.itemRingMax - state.knobs.itemRingMin)
                    : 2f + (float)random.NextDouble() * 20f;
                Vector3 candidate = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (Rules.ArenaSdf(candidate.x, candidate.z) > -margin) continue;
                bool blocked = false;
                for (int i = 0; i < state.bugs.Length; i++)
                    if (state.bugs[i].alive && Vector2.Distance(new Vector2(candidate.x, candidate.z), new Vector2(state.bugs[i].position.x, state.bugs[i].position.z)) < state.knobs.itemMinBug) { blocked = true; break; }
                for (int i = 0; !blocked && i < state.pickups.Count; i++)
                    if (state.pickups[i].alive && Vector2.Distance(new Vector2(candidate.x, candidate.z), new Vector2(state.pickups[i].position.x, state.pickups[i].position.z)) < (state.pickups[i].kind == "heart" ? state.knobs.itemMinHeart : state.knobs.itemMinItem)) blocked = true;
                if (!blocked && state.nest != null && state.nest.alive && Vector2.Distance(new Vector2(candidate.x, candidate.z), new Vector2(state.nest.position.x, state.nest.position.z)) < 2.2f) blocked = true;
                for (int i = 0; !blocked && i < state.eggs.Count; i++)
                    if (state.eggs[i].alive && Vector2.Distance(new Vector2(candidate.x, candidate.z), new Vector2(state.eggs[i].position.x, state.eggs[i].position.z)) < 1.8f) blocked = true;
                if (!blocked) return candidate;
            }
            return Rules.ClampInsideArena(new Vector3(0f, 0f, ring ? state.knobs.itemRingMin : 0f), margin);
        }
    }
}
