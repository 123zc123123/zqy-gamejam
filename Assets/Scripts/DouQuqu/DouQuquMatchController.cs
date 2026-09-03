using System;
using System.Collections.Generic;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 持有一局对战的 Unity 组件。移动、碰撞、经济和巢穴职责拆到独立系统，
    /// 同一份状态既可本地驱动，也可由局域网主机权威驱动。
    /// </summary>
    public sealed class DouQuquMatchController : MonoBehaviour
    {
        // Demo 固定最多四名玩家；输入、状态数组和局域网槽位统一使用此常量。
        public const int MaxPlayers = 4;
        // 使用固定模拟时间，保证主机和客户端快照可以确定性重放。
        public const float FixedDeltaTime = 1f / 60f;
        private const int MovementSubsteps = 6;

        [SerializeField] private MatchRunMode runMode = MatchRunMode.Offline;
        [SerializeField] private int configuredPlayers = MaxPlayers;
        // 单机 Demo 默认只把 0 号槽位交给真人，其余槽位由 AI 驱动；可调为 1~4 兼容本地多人键盘。
        [SerializeField, Range(1, MaxPlayers)] private int offlineHumanPlayers = 1;
        [SerializeField] private bool tickFromUnity = true;
        [SerializeField] private MatchKnobs knobs;

        private readonly InputFrame[] inputs = new InputFrame[MaxPlayers];
        private readonly DouQuquMovementSystem movement = new DouQuquMovementSystem();
        private readonly DouQuquCollisionSystem collision = new DouQuquCollisionSystem();
        private readonly DouQuquEconomySystem economy = new DouQuquEconomySystem();
        private readonly DouQuquNestSystem nestSystem = new DouQuquNestSystem();
        private readonly DouQuquAISystem ai = new DouQuquAISystem();

        // 只有主机/单机控制器推进状态；局域网客户端只应用快照，
        // 将此对象作为表现层读取模型。
        private MatchState state;
        private float accumulator;
        private int inputSequence;

        public MatchRunMode RunMode => runMode;
        public MatchKnobs Knobs => knobs;
        public MatchState State => state;
        public int ConfiguredPlayers => configuredPlayers;
        public int OfflineHumanPlayers => offlineHumanPlayers;
        public BugState[] Bugs => state == null ? new BugState[0] : state.bugs;
        public IReadOnlyList<PickupState> Pickups => state == null ? (IReadOnlyList<PickupState>)Array.Empty<PickupState>() : state.pickups;
        public IReadOnlyList<EggState> Eggs => state == null ? (IReadOnlyList<EggState>)Array.Empty<EggState>() : state.eggs;
        public IReadOnlyList<BabyState> Babies => state == null ? (IReadOnlyList<BabyState>)Array.Empty<BabyState>() : state.babies;
        public NestState Nest => state == null ? null : state.nest;
        public float Elapsed => state == null ? 0f : state.elapsed;
        public bool IsStarted => state != null && state.started;
        public bool IsOver => state != null && state.over;
        public int WinnerId => state == null ? -1 : state.winnerId;
        public MatchPhase Phase => state == null ? MatchPhase.Probe : DouQuquRules.Phase(knobs ?? state.knobs ?? DouQuquRules.DefaultKnobs(), state.elapsed);

        public event Action<MatchSnapshot> SnapshotReady;
        public event Action<MatchState> StateChanged;
        public event Action<int> PlayerEliminated;
        public event Action<string, Vector3> GameplayEvent;

        private void Awake()
        {
            if (knobs == null) knobs = DouQuquRules.DefaultKnobs();
            configuredPlayers = Mathf.Clamp(configuredPlayers, 1, MaxPlayers);
            for (int i = 0; i < inputs.Length; i++) inputs[i] = new InputFrame(i, Vector2.up, false, false);
        }

        // Unity 可变帧时间累积为固定模拟 Tick；单帧上限避免暂停后一次跳过过长对局时间。
        private void Update()
        {
            if (!tickFromUnity || runMode == MatchRunMode.Client || !IsStarted || IsOver) return;
            accumulator += Mathf.Min(Time.deltaTime, 0.1f);
            while (accumulator >= FixedDeltaTime)
            {
                Tick(FixedDeltaTime);
                accumulator -= FixedDeltaTime;
            }
        }

        /// <summary>在重置前配置运行模式和玩家数量。</summary>
        public void Configure(MatchRunMode mode, int playerCount, MatchKnobs matchKnobs = null)
        {
            runMode = mode;
            configuredPlayers = Mathf.Clamp(playerCount, 1, MaxPlayers);
            if (matchKnobs != null)
            {
                knobs = matchKnobs;
                if (state != null) state.knobs = knobs;
            }
        }

        /// <summary>重建新局的实体和确定性游标；seed 为零时使用时间种子。</summary>
        public void ResetMatch(int playerCount = -1, int seed = 0)
        {
            if (knobs == null) knobs = DouQuquRules.DefaultKnobs();
            configuredPlayers = Mathf.Clamp(playerCount < 1 ? configuredPlayers : playerCount, 1, MaxPlayers);
            offlineHumanPlayers = Mathf.Clamp(offlineHumanPlayers, 1, configuredPlayers);
            state = new MatchState
            {
                playerCount = configuredPlayers,
                randomSeed = seed == 0 ? Environment.TickCount : seed,
                knobs = knobs,
                nextNestAt = knobs.nestFirstT,
                lastHeartAt = -1f,
                nextItemIndex = 0,
                lastItemKind = null,
                nextPickupId = 0,
                nextBabyId = 100,
                pendingNestOwnerId = -1
            };
            accumulator = 0f;
            inputSequence = 0;
            for (int i = 0; i < inputs.Length; i++) inputs[i] = new InputFrame(i, Vector2.up, false, false);
            Vector3[] spawns =
            {
                new Vector3(0f, 0f, -DouQuquRules.ArenaHalfDepth * 0.38f),
                new Vector3(0f, 0f, DouQuquRules.ArenaHalfDepth * 0.38f),
                new Vector3(-DouQuquRules.ArenaHalfWidth * 0.38f, 0f, 0f),
                new Vector3(DouQuquRules.ArenaHalfWidth * 0.38f, 0f, 0f)
            };
            state.bugs = new BugState[configuredPlayers];
            state.humanPlayers = new bool[configuredPlayers];
            for (int i = 0; i < state.bugs.Length; i++)
            {
                state.bugs[i] = new BugState(i, spawns[i], knobs);
                state.bugs[i].chargeDirection = (new Vector2(-spawns[i].x, -spawns[i].z)).normalized;
                state.bugs[i].slideMu = knobs.mu;
                // 客户端不推进本地模拟；主机和离线模式只保留本地真人槽位，其余交给确定性 AI。
                state.humanPlayers[i] = runMode == MatchRunMode.Client || i < offlineHumanPlayers;
            }
            ai.Reset(configuredPlayers);
            economy.SeedHearts(state);
            GameplayEvent?.Invoke("match-reset", Vector3.zero);
            StateChanged?.Invoke(state);
        }

        /// <summary>将已准备状态设为运行中，并发出 match-start 事件。</summary>
        public void StartMatch()
        {
            if (state == null || state.bugs.Length == 0) ResetMatch();
            state.started = true;
            state.over = false;
            state.winnerId = -1;
            GameplayEvent?.Invoke("match-start", Vector3.zero);
            StateChanged?.Invoke(state);
        }

        /// <summary>停止模拟，并将当前状态标记为结束。</summary>
        public void StopMatch()
        {
            if (state == null) return;
            state.started = false;
            state.over = true;
            GameplayEvent?.Invoke("match-stop", Vector3.zero);
            StateChanged?.Invoke(state);
        }

        /// <summary>接收玩家最新输入帧；序号让延迟到达的 UDP 包不会覆盖新输入。</summary>
        public void SetInput(InputFrame frame)
        {
            if (state == null || frame == null || frame.playerId < 0 || frame.playerId >= state.bugs.Length || state.over) return;
            InputFrame current = inputs[frame.playerId];
            if (current != null && frame.sequence > 0 && frame.sequence < current.sequence) return;
            frame.sequence = frame.sequence > 0 ? frame.sequence : ++inputSequence;
            inputs[frame.playerId] = frame;
        }

        /// <summary>供本地键盘或脚本输入使用的便捷重载。</summary>
        public void SetInput(int playerId, Vector2 direction, bool held, bool released = false)
        {
            SetInput(new InputFrame(playerId, direction, held, released, ++inputSequence));
        }

        /// <summary>标记槽位由真人控制还是由内置 AI 控制。</summary>
        public void SetPlayerHuman(int playerId, bool human)
        {
            if (state == null || playerId < 0 || playerId >= state.humanPlayers.Length) return;
            state.humanPlayers[playerId] = human;
        }

        /// <summary>推进一个权威模拟片段；移动/碰撞分步执行，再结算经济、巢穴和蓄力。</summary>
        public void Tick(float dt)
        {
            if (state == null || !state.started || state.over || dt <= 0f) return;
            dt = Mathf.Min(dt, 0.1f);
            state.elapsed += dt;
            state.tick++;
            MatchPhase phase = DouQuquRules.Phase(knobs, state.elapsed);
            if (phase == MatchPhase.Rage && state.elapsed - dt < knobs.regTime)
            {
                DouQuquRules.EnterRage(knobs, state.bugs);
                GameplayEvent?.Invoke("rage-start", Vector3.zero);
            }

            ai.Tick(state, inputs, dt);
            float subDt = dt / MovementSubsteps;
            // 先重复执行扫掠移动和碰撞，再判定淘汰，确保高速命中在出圈归因前完成。
            for (int sub = 0; sub < MovementSubsteps; sub++)
            {
                // 出圈统一在所有扫掠子步结束后处理；若在子步内淘汰，
                // 后续子步可能失去完成最终碰撞的机会。
                movement.TickMotion(state, inputs, subDt, Emit, null);
                collision.Resolve(state, Emit, null, OnNestHit);
            }
            for (int i = 0; i < state.bugs.Length; i++)
                if (state.bugs[i].alive && !DouQuquRules.InsideArena(state.bugs[i].position)) MarkOut(state.bugs[i]);
            movement.MarkBabyOutOfBounds(state, Emit);
            economy.Tick(state, AddGrow, Emit);
            // 巢穴计时在拾取结算后执行；蓄力/松开只在完整固定 Tick 末采样一次。
            nestSystem.TickBeforeCollision(state, dt, Emit);
            nestSystem.TickAfterCollision(state, Emit);
            movement.TickCharge(state, inputs, dt);
            CheckEnd(phase);
            if (runMode == MatchRunMode.Host) SnapshotReady?.Invoke(CaptureSnapshot());
            StateChanged?.Invoke(state);
            for (int i = 0; i < inputs.Length; i++) if (inputs[i] != null) inputs[i].released = false;
        }

        /// <summary>将权威状态复制为可由 Unity JSON 序列化的快照。</summary>
        public MatchSnapshot CaptureSnapshot()
        {
            if (state == null) return null;
            MatchSnapshot snapshot = new MatchSnapshot
            {
                version = 5,
                tick = state.tick,
                playerCount = state.playerCount,
                randomSeed = state.randomSeed,
                elapsed = state.elapsed,
                started = state.started,
                over = state.over,
                winnerId = state.winnerId,
                phase = DouQuquRules.Phase(knobs, state.elapsed),
                knobs = knobs,
                bugs = new BugSnapshot[state.bugs.Length],
                pickups = new PickupSnapshot[state.pickups.Count],
                eggs = new EggSnapshot[state.eggs.Count],
                babies = new BabySnapshot[state.babies.Count],
                nest = state.nest == null ? null : new NestSnapshot { position = state.nest.position, hp = state.nest.hp, alive = state.nest.alive },
                lastHeartAt = state.lastHeartAt,
                nextItemIndex = state.nextItemIndex,
                lastItemKind = state.lastItemKind,
                nextPickupId = state.nextPickupId,
                nextBabyId = state.nextBabyId,
                nextNestAt = state.nextNestAt,
                lastNestClearAt = state.lastNestClearAt,
                pendingNestOwnerId = state.pendingNestOwnerId,
                nestChainActive = state.nestChainActive
            };
            for (int i = 0; i < state.bugs.Length; i++)
            {
                BugState b = state.bugs[i];
                snapshot.bugs[i] = new BugSnapshot
                {
                    id = b.id, alive = b.alive, position = b.position, velocity = b.velocity,
                    height = b.height, verticalVelocity = b.verticalVelocity, radius = b.radius,
                    chargeTime = b.chargeTime, stamina = b.stamina, grow = b.grow, score = b.score, lastHitId = b.lastHitId,
                    buffSizeT = b.buffSizeT, buffShieldT = b.buffShieldT, buffChargeT = b.buffChargeT,
                    charging = b.charging, airborne = b.airborne, hitTier = (int)b.hitTier
                };
            }
            for (int i = 0; i < state.pickups.Count; i++)
            {
                PickupState p = state.pickups[i];
                snapshot.pickups[i] = new PickupSnapshot { id = p.id, alive = p.alive, kind = p.kind, position = p.position };
            }
            for (int i = 0; i < state.eggs.Count; i++)
            {
                EggState e = state.eggs[i];
                snapshot.eggs[i] = new EggSnapshot { position = e.position, velocity = e.velocity, ownerId = e.ownerId, remaining = Mathf.Max(0f, e.hatchAt - state.elapsed), alive = e.alive };
            }
            for (int i = 0; i < state.babies.Count; i++)
            {
                BabyState b = state.babies[i];
                snapshot.babies[i] = new BabySnapshot { id = b.id, ownerId = b.ownerId, position = b.position, velocity = b.velocity,
                    height = b.height, verticalVelocity = b.verticalVelocity, charging = b.charging, grow = b.grow, score = b.score,
                    buffSizeT = b.buffSizeT, buffShieldT = b.buffShieldT, buffChargeT = b.buffChargeT,
                    hitTier = (int)b.hitTier, remaining = Mathf.Max(0f, b.lifeEnd - state.elapsed), alive = b.alive };
            }
            return snapshot;
        }

        /// <summary>用主机快照替换本地状态，并重建游标，避免恢复权威后重复生成或复用 ID。</summary>
        public void ApplySnapshot(MatchSnapshot snapshot)
        {
            if (snapshot == null || snapshot.bugs == null) return;
            int snapshotPlayers = Mathf.Clamp(snapshot.playerCount, 1, MaxPlayers);
            if (snapshot.bugs.Length != snapshotPlayers) return;
            if (snapshot.knobs != null) knobs = snapshot.knobs;
            if (state == null || state.bugs.Length != snapshot.playerCount)
            {
                knobs = knobs ?? DouQuquRules.DefaultKnobs();
                ResetMatch(snapshot.playerCount, snapshot.randomSeed);
            }
            state.knobs = knobs;
            state.tick = snapshot.tick;
            state.randomSeed = snapshot.randomSeed;
            state.playerCount = Mathf.Clamp(snapshot.playerCount, 1, MaxPlayers);
            state.elapsed = snapshot.elapsed;
            state.started = snapshot.started;
            state.over = snapshot.over;
            state.winnerId = snapshot.winnerId;
            if (snapshot.version >= 4)
            {
                state.lastHeartAt = snapshot.lastHeartAt;
                state.nextItemIndex = Mathf.Max(0, snapshot.nextItemIndex);
                state.lastItemKind = snapshot.lastItemKind;
                state.nextPickupId = Mathf.Max(0, snapshot.nextPickupId);
                state.nextBabyId = Mathf.Max(100, snapshot.nextBabyId);
                state.nextNestAt = snapshot.nextNestAt;
                state.lastNestClearAt = snapshot.lastNestClearAt;
                state.pendingNestOwnerId = snapshot.pendingNestOwnerId;
                state.nestChainActive = snapshot.nestChainActive;
            }
            for (int i = 0; i < snapshot.bugs.Length && i < state.bugs.Length; i++)
            {
                BugSnapshot s = snapshot.bugs[i];
                BugState b = state.bugs[i];
                b.id = s.id; b.alive = s.alive; b.position = s.position; b.previousPosition = s.position - s.velocity * FixedDeltaTime;
                b.velocity = s.velocity; b.height = s.height; b.verticalVelocity = s.verticalVelocity; b.airborne = s.airborne || s.height > 0.03f || s.verticalVelocity > 0f;
                b.radius = s.radius; b.chargeTime = s.chargeTime; b.grow = s.grow; b.score = s.score; b.lastHitId = s.lastHitId;
                b.stamina = snapshot.version >= 5 ? Mathf.Max(0f, s.stamina) : Mathf.Max(0f, knobs.staminaMax);
                b.buffSizeT = s.buffSizeT; b.buffShieldT = s.buffShieldT; b.buffChargeT = s.buffChargeT; b.charging = s.charging;
                b.hitTier = (HitTier)Mathf.Clamp(s.hitTier, 0, (int)HitTier.Slip);
            }
            state.pickups.Clear();
            if (snapshot.pickups != null)
                for (int i = 0; i < snapshot.pickups.Length; i++)
                {
                    PickupSnapshot p = snapshot.pickups[i];
                    state.pickups.Add(new PickupState(p.id, p.position, p.kind) { alive = p.alive });
                }
            if (snapshot.version < 4)
            {
                state.nextPickupId = 0;
                for (int i = 0; i < state.pickups.Count; i++)
                    state.nextPickupId = Mathf.Max(state.nextPickupId, state.pickups[i].id + 1);
            }
            state.eggs.Clear();
            if (snapshot.eggs != null)
                for (int i = 0; i < snapshot.eggs.Length; i++)
                {
                    EggSnapshot e = snapshot.eggs[i];
                    state.eggs.Add(new EggState { position = e.position, previousPosition = e.position - e.velocity * FixedDeltaTime, velocity = e.velocity, ownerId = e.ownerId, hatchAt = state.elapsed + e.remaining, alive = e.alive });
                }
            state.babies.Clear();
            if (snapshot.babies != null)
                for (int i = 0; i < snapshot.babies.Length; i++)
                {
                    BabySnapshot b = snapshot.babies[i];
                    BabyState restoredBaby = new BabyState { id = b.id, ownerId = b.ownerId, position = b.position, previousPosition = b.position - b.velocity * FixedDeltaTime,
                        velocity = b.velocity, height = b.height, verticalVelocity = b.verticalVelocity, charging = b.charging,
                        airborne = b.height > 0.03f || b.verticalVelocity > 0f, grow = b.grow, score = b.score,
                        buffSizeT = b.buffSizeT, buffShieldT = b.buffShieldT, buffChargeT = b.buffChargeT,
                        hitTier = (HitTier)Mathf.Clamp(b.hitTier, 0, (int)HitTier.Slip),
                        lifeEnd = state.elapsed + b.remaining, alive = b.alive,
                        radius = knobs.bugR * knobs.babyRScale, mass = knobs.babyMass };
                    DouQuquRules.RefreshBabyBody(knobs, restoredBaby);
                    state.babies.Add(restoredBaby);
                }
            if (snapshot.version < 4)
            {
                state.nextBabyId = 100;
                for (int i = 0; i < state.babies.Count; i++)
                    state.nextBabyId = Mathf.Max(state.nextBabyId, state.babies[i].id + 1);
            }
            state.nest = snapshot.nest == null ? null : new NestState { position = snapshot.nest.position, hp = snapshot.nest.hp, alive = snapshot.nest.alive };
            if (snapshot.version < 4)
            {
                state.nestChainActive = (state.nest != null && state.nest.alive) || state.eggs.Count > 0 || state.babies.Count > 0;
                state.nextNestAt = state.nestChainActive
                    ? float.MaxValue
                    : (state.elapsed < knobs.nestFirstT ? knobs.nestFirstT : state.elapsed + Mathf.Max(0f, knobs.nestGap));
            }
            StateChanged?.Invoke(state);
        }

        private void OnNestHit(BugState bug)
        {
            if (state.nest != null && state.nest.hp <= 0f) state.pendingNestOwnerId = bug.id;
        }

        // 护盾只执行一次救援投影；单机把唯一角色拉回场内，多人模式则将其淘汰。
        private void MarkOut(BugState bug)
        {
            if (!bug.alive) return;
            if (DouQuquRules.TryShieldSave(knobs, bug))
            {
                bug.previousPosition = bug.position;
                Emit("shield-save", bug.position);
                return;
            }
            if (state.playerCount == 1)
            {
                bug.position = new Vector3(0f, 0f, -DouQuquRules.ArenaHalfDepth * 0.38f);
                bug.previousPosition = bug.position;
                bug.velocity = Vector3.zero;
                bug.height = 0f;
                bug.verticalVelocity = 0f;
                bug.airborne = false;
                bug.charging = false;
                bug.chargeDirection = Vector2.up;
                bug.slideMu = knobs.mu;
                Emit("solo-pullback", bug.position);
                return;
            }
            bug.alive = false;
            bug.charging = false;
            bug.holding = false;
            bug.pendingCharge = false;
            bug.airborne = true;
            BugState killer = FindBug(bug.lastHitId);
            if (killer != null && killer != bug) AddGrow(killer);
            PlayerEliminated?.Invoke(bug.id);
            Emit("out", bug.position);
        }

        private BugState FindBug(int id)
        {
            for (int i = 0; i < state.bugs.Length; i++) if (state.bugs[i].id == id && state.bugs[i].alive) return state.bugs[i];
            return null;
        }

        private void AddGrow(BugState bug)
        {
            if (bug == null || !bug.alive) return;
            if (bug.grow < 6) bug.grow++;
            bug.score++;
            DouQuquRules.RefreshBody(knobs, bug);
            Emit("grow", bug.position);
        }

        private void CheckEnd(MatchPhase phase)
        {
            if (state.playerCount <= 1) return;
            int alive = 0;
            for (int i = 0; i < state.bugs.Length; i++) if (state.bugs[i].alive) alive++;
            if (alive > 1 && phase != MatchPhase.Over) return;
            state.winnerId = alive == 1 ? FindOnlyAlive() : DouQuquRules.CenterWinner(state.bugs);
            state.over = true;
            state.started = false;
            Emit("match-over", Vector3.zero);
        }

        private int FindOnlyAlive()
        {
            for (int i = 0; i < state.bugs.Length; i++) if (state.bugs[i].alive) return state.bugs[i].id;
            return -1;
        }

        private void Emit(string kind, Vector3 position)
        {
            GameplayEvent?.Invoke(kind, position);
        }
    }
}
