using System;
using System.Collections.Generic;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 确定性的 XZ 平面碰撞处理。每个碰撞都从上一位置扫到当前位置，
    /// 使用连续圆测试，避免高速跳跃穿过目标。
    /// </summary>
    public sealed class DouQuquCollisionSystem
    {
        private const float Elasticity = 1f;

        /// <summary>
        /// 处理一帧内的所有角色、巢穴和蛋碰撞，最后统一检查蟋蟀是否出圈。
        /// </summary>
        public void Resolve(MatchState state, Action<string, Vector3> emit, Action<BugState> onOut, Action<BugState> onNestHit)
        {
            ResolveBugPairs(state, emit);
            ResolveBabyPairs(state, emit);
            ResolveNest(state, emit, onNestHit);
            ResolveEggs(state, emit);
            for (int i = 0; i < state.bugs.Length; i++)
                if (state.bugs[i].alive && !DouQuquRules.InsideArena(state.bugs[i].position)) onOut?.Invoke(state.bugs[i]);
        }

        // 蟋蟀两两检测；i<j 保证同一对只处理一次。
        private void ResolveBugPairs(MatchState state, Action<string, Vector3> emit)
        {
            for (int i = 0; i < state.bugs.Length; i++)
            {
                BugState a = state.bugs[i];
                if (!a.alive) continue;
                for (int j = i + 1; j < state.bugs.Length; j++)
                {
                    BugState b = state.bugs[j];
                    if (!b.alive) continue;
                    Vector3 normal;
                    if (!MoveToContact(a, b, a.radius + b.radius, out normal)) continue;
                    Separate(a, b, normal);
                    BouncePair(state.knobs, a, b, normal, emit);
                }
            }
        }

        // 先处理幼虫之间，再处理幼虫与蟋蟀；同一拥有者的单位不会互相攻击。
        private void ResolveBabyPairs(MatchState state, Action<string, Vector3> emit)
        {
            for (int i = 0; i < state.babies.Count; i++)
            {
                BabyState a = state.babies[i];
                if (!a.alive) continue;
                for (int j = i + 1; j < state.babies.Count; j++)
                {
                    BabyState b = state.babies[j];
                    if (!b.alive || a.ownerId == b.id || b.ownerId == a.id) continue;
                    Vector3 normal;
                    if (!MoveToContact(a.position, a.previousPosition, b.position, b.previousPosition, a.radius + b.radius, out normal, out a.position, out b.position)) continue;
                    Separate(a, b, normal);
                    BounceBabyPair(a, b, normal);
                }
            }
            for (int i = 0; i < state.babies.Count; i++)
            {
                BabyState baby = state.babies[i];
                if (!baby.alive) continue;
                for (int j = 0; j < state.bugs.Length; j++)
                {
                    BugState bug = state.bugs[j];
                    if (!bug.alive || baby.ownerId == bug.id) continue;
                    Vector3 normal;
                    Vector3 babyPos;
                    Vector3 bugPos;
                    if (!MoveToContact(baby.position, baby.previousPosition, bug.position, bug.previousPosition, baby.radius + bug.radius, out normal, out babyPos, out bugPos)) continue;
                    baby.position = babyPos;
                    bug.position = bugPos;
                    Separate(baby, bug, normal);
                    BounceBabyBug(state.knobs, baby, bug, normal, emit);
                }
            }
        }

        // 巢穴只在新的接触瞬间扣血，持续重叠由 touching 集合去重。
        private void ResolveNest(MatchState state, Action<string, Vector3> emit, Action<BugState> onNestHit)
        {
            NestState nest = state.nest;
            if (nest == null || !nest.alive) return;
            HashSet<int> currentTouching = new HashSet<int>();
            List<DouQuquRules.NestHit> hits = new List<DouQuquRules.NestHit>();
            for (int i = 0; i < state.bugs.Length; i++)
            {
                BugState bug = state.bugs[i];
                if (!bug.alive || bug.height > 0.4f) continue;
                Vector3 normal;
                Vector3 bugPosition;
                if (!MoveToContact(bug.position, bug.previousPosition, nest.position, nest.position, bug.radius + state.knobs.nestR, out normal, out bugPosition, out _)) continue;
                bug.position = bugPosition;
                currentTouching.Add(bug.id);
                // 巢穴是静态物体，取蟋蟀相对巢穴的法向速度。
                float normalVelocity = Vector3.Dot(-bug.velocity, normal);
                SeparateStatic(bug, nest.position, state.knobs.nestR, normal);
                BounceStatic(state.knobs, bug, normal, state.knobs.nestMass, emit);
                if (DouQuquRules.IsNewNestContact(nest.touching.Contains(bug.id), normalVelocity))
                {
                    hits.Add(new DouQuquRules.NestHit(bug.id, normalVelocity));
                    onNestHit?.Invoke(bug);
                    emit?.Invoke("nest-hit", nest.position);
                }
            }
            nest.touching.Clear();
            foreach (int id in currentTouching) nest.touching.Add(id);
            if (hits.Count == 0) return;
            DouQuquRules.NestHitResult result = DouQuquRules.ResolveNestHits(nest.hp, hits);
            nest.hp = result.hp;
            if (!result.exploded) return;
            state.pendingNestOwnerId = result.ownerId;
            nest.alive = false;
            nest.hp = 0f;
            emit?.Invoke("nest-break", nest.position);
        }

        // 蛋既会被角色撞开，也会在到达孵化时间后由巢穴系统转换成幼虫。
        private void ResolveEggs(MatchState state, Action<string, Vector3> emit)
        {
            for (int i = 0; i < state.eggs.Count; i++)
            {
                EggState egg = state.eggs[i];
                if (!egg.alive) continue;
                for (int j = 0; j < state.bugs.Length; j++)
                {
                    BugState bug = state.bugs[j];
                    if (!bug.alive || bug.height > 0.4f) continue;
                    Vector3 normal;
                    Vector3 bugPosition;
                    Vector3 eggPosition;
                    if (!MoveToContact(bug.position, bug.previousPosition, egg.position, egg.previousPosition, bug.radius + state.knobs.eggR, out normal, out bugPosition, out eggPosition)) continue;
                    bug.position = bugPosition;
                    egg.position = eggPosition;
                    Separate(bug, ref egg.position, state.knobs.eggR, normal, state.knobs.eggMass);
                    float normalSpeed = Vector3.Dot(egg.velocity - bug.velocity, normal);
                    Vector3 eggVelocity = egg.velocity;
                    HitTier bugTier = normalSpeed < -0.0001f
                        ? TierForEggContact(state.knobs, bug, eggVelocity, normal)
                        : HitTier.None;
                    BounceMasses(ref bug.velocity, ref eggVelocity, bug.mass, state.knobs.eggMass, normal);
                    egg.velocity = eggVelocity;
                    if (normalSpeed < -0.0001f)
                    {
                        bug.initialSpeed = bug.velocity.magnitude;
                        FaceVelocity(bug);
                        // 蛋没有玩家归属，不能作为淘汰时的击杀者。
                        bug.lastHitId = -1;
                        ApplyHitSlide(state.knobs, bug, bugTier);
                        emit?.Invoke("egg-hit", egg.position);
                    }
                }
            }
        }

        private bool MoveToContact(BugState a, BugState b, float radius, out Vector3 normal)
        {
            Vector3 aPosition;
            Vector3 bPosition;
            bool result = MoveToContact(a.position, a.previousPosition, b.position, b.previousPosition, radius, out normal, out aPosition, out bPosition);
            if (result) { a.position = aPosition; b.position = bPosition; }
            return result;
        }

        // 先检查当前重叠，否则求两段相对运动与接触圆的最早交点。
        private bool MoveToContact(Vector3 a, Vector3 aPrevious, Vector3 b, Vector3 bPrevious, float radius,
            out Vector3 normal, out Vector3 aContact, out Vector3 bContact)
        {
            Vector3 delta = b - a;
            delta.y = 0f;
            if (delta.sqrMagnitude < radius * radius)
            {
                float distance = new Vector2(delta.x, delta.z).magnitude;
                if (distance < 0.0001f) { normal = Vector3.right; }
                else normal = new Vector3(delta.x / distance, 0f, delta.z / distance);
                aContact = a;
                bContact = b;
                return true;
            }
            Vector3 start = bPrevious - aPrevious;
            start.y = 0f;
            Vector3 end = delta;
            Vector3 travel = end - start;
            float aa = Vector3.Dot(travel, travel);
            if (aa < 1e-10f) { normal = Vector3.zero; aContact = a; bContact = b; return false; }
            float bb = 2f * Vector3.Dot(start, travel);
            float cc = Vector3.Dot(start, start) - radius * radius;
            float discriminant = bb * bb - 4f * aa * cc;
            if (discriminant < 0f) { normal = Vector3.zero; aContact = a; bContact = b; return false; }
            float t = (-bb - Mathf.Sqrt(discriminant)) / (2f * aa);
            if (t < 0f || t > 1f) { normal = Vector3.zero; aContact = a; bContact = b; return false; }
            aContact = Vector3.Lerp(aPrevious, a, t);
            bContact = Vector3.Lerp(bPrevious, b, t);
            Vector3 contactDelta = bContact - aContact;
            contactDelta.y = 0f;
            float contactDistance = new Vector2(contactDelta.x, contactDelta.z).magnitude;
            normal = contactDistance < 0.0001f ? Vector3.right : new Vector3(contactDelta.x / contactDistance, 0f, contactDelta.z / contactDistance);
            return true;
        }

        // 使用质量加权的一维弹性冲量，并分别计算双方的命中档位。
        private void BouncePair(MatchKnobs knobs, BugState a, BugState b, Vector3 normal, Action<string, Vector3> emit)
        {
            Vector3 relative = b.velocity - a.velocity;
            float normalSpeed = Vector3.Dot(relative, normal);
            if (normalSpeed >= -0.0001f) return;
            // 命中档位描述的是碰撞发生前的来势。必须在冲量改变速度前计算，
            // 否则非对称碰撞可能把 Control/Slip 判反。
            HitTier tierA = TierFor(knobs, a, b, normal);
            HitTier tierB = TierFor(knobs, b, a, -normal);
            float inverseA = 1f / Mathf.Max(0.01f, a.mass);
            float inverseB = 1f / Mathf.Max(0.01f, b.mass);
            float impulse = -(1f + Elasticity) * normalSpeed / (inverseA + inverseB);
            a.velocity -= normal * impulse * inverseA;
            b.velocity += normal * impulse * inverseB;
            a.initialSpeed = a.velocity.magnitude;
            b.initialSpeed = b.velocity.magnitude;
            FaceVelocity(a);
            FaceVelocity(b);
            a.lastHitId = b.id;
            b.lastHitId = a.id;
            ApplyHitSlide(knobs, a, tierA);
            ApplyHitSlide(knobs, b, tierB);
            emit?.Invoke("hit", (a.position + b.position) * 0.5f);
        }

        private void BounceBabyPair(BabyState a, BabyState b, Vector3 normal)
        {
            Vector3 va = a.velocity;
            Vector3 vb = b.velocity;
            BounceMasses(ref va, ref vb, a.mass, b.mass, normal);
            a.velocity = va;
            b.velocity = vb;
            a.initialSpeed = a.velocity.magnitude;
            b.initialSpeed = b.velocity.magnitude;
            FaceVelocity(a);
            FaceVelocity(b);
        }

        private void BounceBabyBug(MatchKnobs knobs, BabyState baby, BugState bug, Vector3 normal, Action<string, Vector3> emit)
        {
            Vector3 babyVelocity = baby.velocity;
            Vector3 bugVelocity = bug.velocity;
            float speed = Vector3.Dot(bugVelocity - babyVelocity, normal);
            HitTier bugTier = HitTier.None;
            HitTier babyTier = HitTier.None;
            if (speed < -0.0001f)
                bugTier = TierForBabyContact(knobs, baby, bug, normal, out babyTier);
            BounceMasses(ref babyVelocity, ref bugVelocity, baby.mass, bug.mass, normal);
            baby.velocity = babyVelocity;
            bug.velocity = bugVelocity;
            if (speed < -0.0001f)
            {
                baby.initialSpeed = baby.velocity.magnitude;
                bug.initialSpeed = bug.velocity.magnitude;
                FaceVelocity(baby);
                FaceVelocity(bug);
                bug.lastHitId = DouQuquRules.HitCreditId(baby);
                bug.hitTier = bugTier;
                baby.hitTier = babyTier;
                bug.slideMu = MuForTier(knobs, bugTier);
                baby.slideMu = MuForTier(knobs, babyTier);
                bug.charging = false;
                bug.chargeTime = 0f;
                bug.pendingCharge = bug.holding;
                baby.charging = false;
                baby.chargeTime = 0f;
                baby.pendingCharge = true;
                bug.airborne = false;
                bug.height = 0f;
                bug.verticalVelocity = 0f;
                baby.airborne = false;
                baby.height = 0f;
                baby.verticalVelocity = 0f;
                emit?.Invoke("baby-hit", bug.position);
            }
        }

        private HitTier TierForBabyContact(MatchKnobs knobs, BabyState baby, BugState bug, Vector3 normal, out HitTier babyTier)
        {
            float bugNormal = Vector3.Dot(bug.velocity, normal);
            float babyNormal = Vector3.Dot(baby.velocity, normal);
            HitTier bugTier;
            if (Mathf.Abs(bugNormal) + 1e-9f >= Mathf.Abs(babyNormal)) bugTier = HitTier.Control;
            else
            {
                float bugInitial = (!bug.airborne && bug.charging)
                    ? DouQuquRules.ChargeDelta(knobs, bug) * Mathf.Max(0f, knobs.rChargeScale)
                    : (bug.initialSpeed > 0f ? bug.initialSpeed : bug.velocity.magnitude);
                float babyInitial = baby.initialSpeed > 0f ? baby.initialSpeed : baby.velocity.magnitude;
                float bugNormalInitial = bug.velocity.magnitude < 1e-8f ? 0f : bugInitial * bugNormal / bug.velocity.magnitude;
                float babyNormalInitial = baby.velocity.magnitude < 1e-8f ? 0f : babyInitial * babyNormal / baby.velocity.magnitude;
                float deltaMomentum = baby.mass * (-babyNormalInitial) - bug.mass * bugNormalInitial;
                float vmax = DouQuquRules.PanelVMax(knobs);
                float resistance = knobs.rStand * vmax * bug.mass;
                if (vmax > 1e-6f)
                    resistance += Mathf.Clamp(bugInitial / vmax, 0f, 1f) * (knobs.rMax * bug.mass * vmax - knobs.rStand * vmax * bug.mass);
                bugTier = deltaMomentum <= resistance ? HitTier.Normal : HitTier.Slip;
            }
            babyTier = bugTier == HitTier.Control ? HitTier.Slip : HitTier.Control;
            return bugTier;
        }

        private float MuForTier(MatchKnobs knobs, HitTier tier)
        {
            if (tier == HitTier.Control) return knobs.mu * knobs.muCtrlScale;
            if (tier == HitTier.Slip) return knobs.mu * knobs.muSlipScale;
            return knobs.mu;
        }

        private void BounceMasses(ref Vector3 aVelocity, ref Vector3 bVelocity, float massA, float massB, Vector3 normal)
        {
            Vector3 relative = bVelocity - aVelocity;
            float normalSpeed = Vector3.Dot(relative, normal);
            if (normalSpeed >= -0.0001f) return;
            float inverseA = 1f / Mathf.Max(0.01f, massA);
            float inverseB = 1f / Mathf.Max(0.01f, massB);
            float impulse = -(1f + Elasticity) * normalSpeed / (inverseA + inverseB);
            aVelocity -= normal * impulse * inverseA;
            bVelocity += normal * impulse * inverseB;
        }

        private void SeparateStatic(BugState bug, Vector3 center, float radius, Vector3 normal)
        {
            Vector3 delta = bug.position - center;
            float distance = new Vector2(delta.x, delta.z).magnitude;
            float overlap = Mathf.Max(0f, bug.radius + radius - distance);
            bug.position -= normal * (overlap + 0.001f);
        }

        private void Separate(BugState a, BugState b, Vector3 normal)
        {
            Vector3 delta = b.position - a.position;
            float distance = new Vector2(delta.x, delta.z).magnitude;
            float overlap = Mathf.Max(0f, a.radius + b.radius - distance);
            float inverseA = 1f / Mathf.Max(0.01f, a.mass);
            float inverseB = 1f / Mathf.Max(0.01f, b.mass);
            float inverse = inverseA + inverseB;
            a.position -= normal * overlap * inverseA / inverse;
            b.position += normal * overlap * inverseB / inverse;
        }

        private void Separate(BabyState a, BabyState b, Vector3 normal)
        {
            Vector3 delta = b.position - a.position;
            float distance = new Vector2(delta.x, delta.z).magnitude;
            float overlap = Mathf.Max(0f, a.radius + b.radius - distance);
            float inverseA = 1f / Mathf.Max(0.01f, a.mass);
            float inverseB = 1f / Mathf.Max(0.01f, b.mass);
            float inverse = inverseA + inverseB;
            a.position -= normal * overlap * inverseA / inverse;
            b.position += normal * overlap * inverseB / inverse;
        }

        private void Separate(BabyState baby, BugState bug, Vector3 normal)
        {
            Vector3 delta = bug.position - baby.position;
            float distance = new Vector2(delta.x, delta.z).magnitude;
            float overlap = Mathf.Max(0f, baby.radius + bug.radius - distance);
            float inverseA = 1f / Mathf.Max(0.01f, baby.mass);
            float inverseB = 1f / Mathf.Max(0.01f, bug.mass);
            float inverse = inverseA + inverseB;
            baby.position -= normal * overlap * inverseA / inverse;
            bug.position += normal * overlap * inverseB / inverse;
        }

        private void Separate(BugState bug, ref Vector3 eggPosition, float eggRadius, Vector3 normal, float eggMass)
        {
            Vector3 delta = eggPosition - bug.position;
            float distance = new Vector2(delta.x, delta.z).magnitude;
            float overlap = Mathf.Max(0f, bug.radius + eggRadius - distance);
            float inverseA = 1f / Mathf.Max(0.01f, bug.mass);
            float inverseB = 1f / Mathf.Max(0.01f, eggMass);
            float inverse = inverseA + inverseB;
            bug.position -= normal * overlap * inverseA / inverse;
            eggPosition += normal * overlap * inverseB / inverse;
        }

        private void BounceStatic(MatchKnobs knobs, BugState bug, Vector3 normal, float staticMass, Action<string, Vector3> emit)
        {
            // normal 从蟋蟀指向静态物体；用相对速度判断是否正在接近。
            // 只有接近巢穴时才施加反弹，离开巢穴时不重复反弹。
            float normalSpeed = Vector3.Dot(-bug.velocity, normal);
            if (normalSpeed >= -0.0001f) return;
            float inverseBug = 1f / Mathf.Max(0.01f, bug.mass);
            float inverseStatic = 1f / Mathf.Max(0.01f, staticMass);
            float impulse = -(1f + Elasticity) * normalSpeed / (inverseBug + inverseStatic);
            bug.velocity -= normal * impulse * inverseBug;
            bug.initialSpeed = bug.velocity.magnitude;
            FaceVelocity(bug);
            bug.lastHitId = -1;
            // 对静止巢穴而言，运动中的蟋蟀视为控制方，
            // 与原型中的静态占位刚体碰撞路径一致。
            ApplyHitSlide(knobs, bug, HitTier.Control);
            emit?.Invoke("hit", bug.position);
        }

        private HitTier TierFor(MatchKnobs knobs, BugState me, BugState other, Vector3 normal)
        {
            return TierForContact(knobs, me, other.mass, other.velocity, other.initialSpeed, normal);
        }

        private HitTier TierForEggContact(MatchKnobs knobs, BugState bug, Vector3 eggVelocity, Vector3 normal)
        {
            return TierForContact(knobs, bug, knobs.eggMass, eggVelocity, eggVelocity.magnitude, normal);
        }

        // 先比较法向速度决定控制方，否则用初始动量差与抗性判断 Normal/Slip。
        private HitTier TierForContact(MatchKnobs knobs, BugState me, float otherMass, Vector3 otherVelocity, float otherInitialSpeed, Vector3 normal)
        {
            float meNormal = Vector3.Dot(me.velocity, normal);
            float otherNormal = Vector3.Dot(otherVelocity, normal);
            if (Mathf.Abs(meNormal) + 1e-9f >= Mathf.Abs(otherNormal)) return HitTier.Control;
            float meInitial = InitialNormalSpeed(me, normal);
            float otherSpeed = otherVelocity.magnitude;
            float otherInitial = otherSpeed < 1e-8f ? 0f : otherInitialSpeed * otherNormal / otherSpeed;
            float deltaMomentum = otherMass * (-otherInitial) - me.mass * meInitial;
            return deltaMomentum <= ResistanceOf(knobs, me) ? HitTier.Normal : HitTier.Slip;
        }

        private float InitialNormalSpeed(BugState bug, Vector3 normal)
        {
            float speed = bug.velocity.magnitude;
            if (speed < 1e-8f) return 0f;
            float initial = bug.initialSpeed > 0f ? bug.initialSpeed : speed;
            return initial * Vector3.Dot(bug.velocity, normal) / speed;
        }

        private float ResistanceOf(MatchKnobs knobs, BugState bug)
        {
            // 与原型 resistOf() 相同的阻力插值公式。
            float vmax = DouQuquRules.PanelVMax(knobs);
            float v0 = knobs.rStand * vmax;
            float resistanceSpeed = (!bug.airborne && bug.charging)
                ? DouQuquRules.ChargeDelta(knobs, bug) * Mathf.Max(0f, knobs.rChargeScale)
                : (bug.initialSpeed > 0f ? bug.initialSpeed : bug.velocity.magnitude);
            float t = vmax < 1e-6f ? 0f : Mathf.Clamp(resistanceSpeed / vmax, 0f, 1f);
            return v0 * bug.mass + t * (knobs.rMax * bug.mass * vmax - v0 * bug.mass);
        }

        // 命中后落地滑行，并清除当前蓄力；若仍按住按键则交给 pendingCharge 续蓄。
        private void ApplyHitSlide(MatchKnobs knobs, BugState bug, HitTier tier)
        {
            bug.hitTier = tier;
            bug.airborne = false;
            bug.height = 0f;
            bug.verticalVelocity = 0f;
            if (tier == HitTier.Control) bug.slideMu = knobs.mu * knobs.muCtrlScale;
            else if (tier == HitTier.Slip) bug.slideMu = knobs.mu * knobs.muSlipScale;
            else bug.slideMu = knobs.mu;
            bug.charging = false;
            bug.chargeTime = 0f;
            bug.pendingCharge = bug.holding;
        }

        private void FaceVelocity(BugState bug)
        {
            Vector2 velocity = new Vector2(bug.velocity.x, bug.velocity.z);
            if (velocity.sqrMagnitude > 0.0144f) bug.chargeDirection = velocity.normalized;
        }

        private void FaceVelocity(BabyState baby)
        {
            Vector2 velocity = new Vector2(baby.velocity.x, baby.velocity.z);
            if (velocity.sqrMagnitude > 0.0144f) baby.chargeDirection = velocity.normalized;
        }
    }
}
