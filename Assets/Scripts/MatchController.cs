using System;
using System.Collections.Generic;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 持有一局对战的 Unity 组件。移动、碰撞、经济和巢穴职责拆到独立系统，
    /// 同一份状态既可本地驱动，也可由局域网主机权威驱动。
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        // Demo 固定最多四名玩家；输入、状态数组和局域网槽位统一使用此常量。
        public const int MaxPlayers = 4;
        public const int LivesPerPlayer = 3;
        // 使用固定模拟时间，保证主机和客户端快照可以确定性重放。
        public const float FixedDeltaTime = 1f / 60f;
        private const int MovementSubsteps = 6;

        [SerializeField, InspectorCn("运行模式")] private MatchRunMode runMode = MatchRunMode.Offline;
        [SerializeField, InspectorCn("玩家数")] private int configuredPlayers = MaxPlayers;
        // 单机 Demo 默认只把 0 号槽位交给真人，其余槽位由 AI 驱动；可调为 1~4 兼容本地多人键盘。
        [SerializeField, Range(1, MaxPlayers), InspectorCn("单机真人数")] private int offlineHumanPlayers = 1;
        [SerializeField, InspectorCn("由 Unity 推进")] private bool tickFromUnity = true;
        [SerializeField, InspectorCn("对局旋钮")] private MatchKnobs knobs;

        private readonly InputFrame[] inputs = new InputFrame[MaxPlayers];
        private readonly MovementSystem movement = new MovementSystem();
        private readonly CollisionSystem collision = new CollisionSystem();
        private readonly EconomySystem economy = new EconomySystem();
        private readonly NestSystem nestSystem = new NestSystem();
        private readonly AISystem ai = new AISystem();

        // 主机/单机推进权威状态；客户端开启预测时也推进本地副本，快照到达后再回滚。
        private MatchState state;
        private CricketPick[][] pendingRoster;
        private float accumulator;
        private int inputSequence;
        // 独立权威对象只推进规则，不创建编辑器调参 UI。
        [NonSerialized] private bool headlessSimulation;

        public MatchRunMode RunMode => runMode;
        public MatchKnobs Knobs => state != null && state.knobs != null ? state.knobs : knobs;
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
        public MatchPhase Phase => state == null ? MatchPhase.Probe : Rules.Phase(ActiveKnobs, state.elapsed);
        public int ZoneTier => state != null && state.playerCount <= 1 ? Rules.LastZoneTier : Rules.ZoneTierAt(ActiveKnobs, Elapsed);
        public bool ZoneWarn => state != null && state.playerCount > 1 && Rules.IsZoneWarn(ActiveKnobs, Elapsed);

        public event Action<MatchSnapshot> SnapshotReady;
        public event Action<MatchState> StateChanged;
        public event Action<int, int> CricketOut;
        public event Action<int, int> CricketIn;
        public event Action<int> PlayerEliminated;
        public event Action<string, Vector3> GameplayEvent;
        public event Action<int> ZoneSnapped;
        bool tutorialHoldClock;
        bool tutorialSuppressSpawns;

        private void Awake()
        {
            if (knobs == null) knobs = Rules.DefaultKnobs();
            configuredPlayers = Mathf.Clamp(configuredPlayers, 1, MaxPlayers);
            for (int i = 0; i < inputs.Length; i++) inputs[i] = new InputFrame(i, Vector2.up, false, false);
        }

        private void Start()
        {
            if (!headlessSimulation) KnobSaveHud.Ensure(this);
        }

        /// <summary>标记为跨场景的无界面权威模拟对象。</summary>
        public void SetHeadlessSimulation(bool value)
        {
            headlessSimulation = value;
        }

        /// <summary>把当前旋钮写成 JSON，供退出 Play 后覆写 Demo 场景 Inspector。</summary>
        public string CaptureKnobsJson()
        {
            if (knobs == null) knobs = Rules.DefaultKnobs();
            return JsonUtility.ToJson(knobs);
        }

        /// <summary>用 JSON 覆写当前旋钮对象；编辑器退出 Play 后写回场景时也会走这里。</summary>
        public void ApplyKnobsJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            if (knobs == null) knobs = Rules.DefaultKnobs();
            JsonUtility.FromJsonOverwrite(json, knobs);
            knobs.EnsureJumpKnobs();
            if (state != null) state.knobs = knobs;
        }

        /// <summary>记下局内旋钮。Unity 不能在 Play 里持久化场景，退出 Play 后才会覆写 Demo Inspector。</summary>
        public bool TrySaveKnobsToScene()
        {
            if (knobs == null) knobs = Rules.DefaultKnobs();
            PlayerPrefs.SetString(KnobSaveKeys.Json, JsonUtility.ToJson(knobs));
            PlayerPrefs.SetInt(KnobSaveKeys.Dirty, 1);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>只改当局规则，不覆写场景 Inspector 上的旋钮。</summary>
        public void OverlayRuntimeKnobs(MatchKnobs runtime)
        {
            if (runtime == null || state == null) return;
            state.knobs = runtime;
            Rules.ApplyZoneAt(runtime, state.elapsed);
        }

        private MatchKnobs ActiveKnobs
        {
            get
            {
                if (state != null && state.knobs != null) return state.knobs;
                if (knobs == null) knobs = Rules.DefaultKnobs();
                return knobs;
            }
        }

        // Unity 可变帧时间累积为固定模拟 Tick；单帧上限避免暂停后一次跳过过长对局时间。
        private void Update()
        {
            if (!tickFromUnity || !IsStarted || IsOver) return;
            accumulator += Mathf.Min(Time.deltaTime, 0.1f);
            while (accumulator >= FixedDeltaTime)
            {
                Tick(FixedDeltaTime);
                accumulator -= FixedDeltaTime;
            }
        }

        /// <summary>
        /// 匹配选人完成后写入上场顺序。未调用时默认每人三只占位蟋蟀。
        /// 须在 StartMatch 前设置。
        /// </summary>
        public void SetRoster(int playerId, CricketPick[] picks)
        {
            configuredPlayers = Mathf.Clamp(configuredPlayers, 1, MaxPlayers);
            if (playerId < 0 || playerId >= MaxPlayers) return;
            StoreRoster(ref pendingRoster, configuredPlayers, playerId, picks);
            if (state != null)
            {
                StoreRoster(ref state.roster, configuredPlayers, playerId, picks);
                if (state.bugs != null && playerId < state.bugs.Length)
                    ApplyPickToBug(state.bugs[playerId], playerId, CricketIndex(playerId));
            }
        }

        public int CricketIndex(int playerId)
        {
            if (state == null || state.cricketIndex == null || playerId < 0 || playerId >= state.cricketIndex.Length) return 0;
            return state.cricketIndex[playerId];
        }

        public CricketPick RosterPick(int playerId, int slot)
        {
            return GetPick(playerId, slot);
        }

        public int LivesLeft(int playerId)
        {
            if (state == null || state.bugs == null || playerId < 0 || playerId >= state.bugs.Length) return 0;
            if (state.playerIn != null && playerId < state.playerIn.Length && !state.playerIn[playerId]) return 0;
            int unused = 0;
            for (int slot = CricketIndex(playerId) + 1; slot < LivesPerPlayer; slot++)
            {
                CricketPick later = GetPick(playerId, slot);
                if (later != null && later.HasBug()) unused++;
            }
            int current = state.bugs[playerId] != null && state.bugs[playerId].alive ? 1 : 0;
            return current + unused + ExtraLives(playerId);
        }

        private int ExtraLives(int playerId)
        {
            int extra = 0;
            BugState bug = state != null && state.bugs != null && playerId >= 0 && playerId < state.bugs.Length
                ? state.bugs[playerId] : null;
            if (bug != null && bug.alive) extra += Mathf.Max(0, bug.guanYuReviveLeft);
            int currentSlot = CricketIndex(playerId);
            int revive = knobs != null ? Mathf.Max(0, knobs.guanYuRevives) : 1;
            for (int slot = currentSlot + 1; slot < LivesPerPlayer; slot++)
            {
                CricketPick pick = GetPick(playerId, slot);
                if (pick != null && pick.quality >= 4 && pick.temperament == (int)CricketTemperament.ChenWen)
                    extra += revive;
            }
            return extra;
        }

        public bool PlayerStillIn(int playerId)
        {
            if (state == null || state.playerIn == null || playerId < 0 || playerId >= state.playerIn.Length) return false;
            return state.playerIn[playerId];
        }

        public int Place(int playerId)
        {
            if (state == null || state.place == null || playerId < 0 || playerId >= state.place.Length) return 0;
            return state.place[playerId];
        }

        public int MatchScore(int playerId)
        {
            if (state == null || state.matchScore == null || playerId < 0 || playerId >= state.matchScore.Length) return 0;
            return state.matchScore[playerId];
        }

        public int KillStreak(int playerId)
        {
            if (state == null || state.killStreak == null || playerId < 0 || playerId >= state.killStreak.Length) return 0;
            return state.killStreak[playerId];
        }

        /// <summary>在重置前配置运行模式和玩家数量。</summary>
        public void Configure(MatchRunMode mode, int playerCount, MatchKnobs matchKnobs = null)
        {
            runMode = mode;
            configuredPlayers = Mathf.Clamp(playerCount, 1, MaxPlayers);
            // 客户端副本用于本地输入预测；权威结果仍只来自房主快照。
            if (mode == MatchRunMode.Client) tickFromUnity = true;
            if (matchKnobs != null)
            {
                knobs = matchKnobs;
                if (state != null) state.knobs = knobs;
            }
        }

        /// <summary>重建新局的实体和确定性游标；seed 为零时使用时间种子。</summary>
        public void ResetMatch(int playerCount = -1, int seed = 0)
        {
            if (knobs == null) knobs = Rules.DefaultKnobs();
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
            if (configuredPlayers <= 1) Rules.ApplyZoneTier(knobs, Rules.LastZoneTier);
            else Rules.ApplyZoneAt(knobs, 0f);
            state.homeSpawn = new Vector3[configuredPlayers];
            for (int i = 0; i < configuredPlayers; i++)
            {
                if (configuredPlayers <= 1) state.homeSpawn[i] = Rules.SoloPullbackPoint();
                else if (TutorialDirector.NeedsBattleLesson)
                    state.homeSpawn[i] = TutorialDirector.BattleOpeningSpawn(i, knobs.spawnEdge);
                else state.homeSpawn[i] = Rules.OpeningSpawn(i, knobs.spawnEdge);
            }
            state.bugs = new BugState[configuredPlayers];
            state.humanPlayers = new bool[configuredPlayers];
            state.idlePlayers = new bool[configuredPlayers];
            CricketPick[] localPicks = AppServices.PendingLocalPicks;
            if (localPicks != null) StoreRoster(ref pendingRoster, configuredPlayers, 0, localPicks);
            EnsureRoster(configuredPlayers);
            for (int i = 0; i < state.bugs.Length; i++)
            {
                Vector3 spawn = state.homeSpawn[i];
                state.bugs[i] = new BugState(i, spawn, knobs);
                state.bugs[i].chargeDirection = (new Vector2(-spawn.x, -spawn.z)).normalized;
                if (state.bugs[i].chargeDirection.sqrMagnitude < 0.01f) state.bugs[i].chargeDirection = Vector2.up;
                state.bugs[i].slideMu = Rules.GripOf(knobs, state.bugs[i]);
                ApplyPickToBug(state.bugs[i], i, 0);
                if (!Rules.InsideArena(state.bugs[i].position, state.bugs[i].radius))
                {
                    state.bugs[i].position = Rules.ClampInsideArena(state.bugs[i].position, state.bugs[i].radius);
                    state.bugs[i].previousPosition = state.bugs[i].position;
                }
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
            if (state.idlePlayers != null && frame.playerId < state.idlePlayers.Length && state.idlePlayers[frame.playerId]) return;
            if (state.playerIn != null && frame.playerId < state.playerIn.Length && !state.playerIn[frame.playerId]) return;
            InputFrame current = inputs[frame.playerId];
            if (current != null && frame.sequence > 0 && frame.sequence <= current.sequence) return;
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

        /// <summary>木桩槽位：不读输入、不跑人机，站着不跳。</summary>
        public void SetPlayerIdle(int playerId, bool idle)
        {
            if (state == null || state.idlePlayers == null || playerId < 0 || playerId >= state.idlePlayers.Length) return;
            state.idlePlayers[playerId] = idle;
            if (!idle) return;
            inputs[playerId] = new InputFrame(playerId, Vector2.up, false, false);
            BugState bug = playerId < state.bugs.Length ? state.bugs[playerId] : null;
            if (bug == null) return;
            bug.holding = false;
            bug.charging = false;
            bug.pendingCharge = false;
        }

        public void SetTutorialFreeze(bool on)
        {
            tutorialHoldClock = on;
            tutorialSuppressSpawns = on;
            if (state == null) return;
            if (on) state.nextNestAt = float.MaxValue;
            else if (state.nextNestAt > 1e8f)
                state.nextNestAt = state.elapsed + Mathf.Max(0f, ActiveKnobs.nestGap);
        }

        /// <summary>只放行比赛时钟。人机冻结和停投放仍由 SetTutorialFreeze 管。</summary>
        public void SetTutorialHoldClock(bool on)
        {
            tutorialHoldClock = on;
        }

        public void ClearPickups()
        {
            if (state == null || state.pickups == null) return;
            state.pickups.Clear();
            StateChanged?.Invoke(state);
        }

        public void SpawnTutorialPickup(string kind, Vector3 at)
        {
            if (state == null) return;
            state.pickups.Add(new PickupState(state.nextPickupId++, at, kind));
            StateChanged?.Invoke(state);
        }

        public void SpawnTutorialNest(Vector3 at, float hp)
        {
            if (state == null) return;
            state.nest = new NestState
            {
                position = at,
                hp = Mathf.Max(1f, hp),
                alive = true
            };
            state.nextNestAt = float.MaxValue;
            StateChanged?.Invoke(state);
        }

        /// <summary>教学特写：进度条走完后立刻孵，不把成败交给下一帧模拟时钟。</summary>
        public bool ForceHatchEgg(EggState egg)
        {
            if (state == null || egg == null || !egg.alive) return false;
            if (!nestSystem.HatchEgg(state, egg, Emit)) return false;
            nestSystem.TickAfterCollision(state, Emit);
            StateChanged?.Invoke(state);
            return true;
        }

        /// <summary>教学木桩被弹出局后拉回原位，避免下一课没靶子。</summary>
        public void RestoreTutorialDummy(int playerId, Vector3 at)
        {
            if (state == null || state.bugs == null || playerId < 0 || playerId >= state.bugs.Length) return;
            BugState bug = state.bugs[playerId];
            if (bug == null) return;
            if (state.playerIn != null && playerId < state.playerIn.Length)
                state.playerIn[playerId] = true;
            if (state.place != null && playerId < state.place.Length)
                state.place[playerId] = 0;
            RecycleBug(bug, at);
            SetPlayerIdle(playerId, true);
            StateChanged?.Invoke(state);
        }

        public void MovePlayerTo(int playerId, Vector3 at)
        {
            if (state == null || state.bugs == null || playerId < 0 || playerId >= state.bugs.Length) return;
            BugState bug = state.bugs[playerId];
            if (bug == null) return;
            at.y = 0f;
            bug.position = at;
            bug.previousPosition = at;
            bug.velocity = Vector3.zero;
            bug.height = 0f;
            bug.verticalVelocity = 0f;
            bug.airborne = false;
            StateChanged?.Invoke(state);
        }

        public Vector3 PointInward(int playerId, float distance)
        {
            if (state == null || state.bugs == null || playerId < 0 || playerId >= state.bugs.Length)
                return Vector3.zero;
            BugState bug = state.bugs[playerId];
            Vector3 pos = bug.position;
            float need = (bug.radius + 2.8f);
            Vector3 inward = new Vector3(-pos.x, 0f, -pos.z);
            if (inward.sqrMagnitude < 0.01f) inward = Vector3.forward;
            inward.Normalize();
            Vector3[] tries =
            {
                pos + inward * distance,
                pos + inward * (distance + 3f),
                pos + Vector3.Cross(Vector3.up, inward) * distance,
                pos - Vector3.Cross(Vector3.up, inward) * distance
            };
            for (int i = 0; i < tries.Length; i++)
            {
                Vector3 at = Rules.ClampInsideArena(tries[i], 2.4f);
                at.y = 0f;
                float gap = Vector2.Distance(new Vector2(at.x, at.z), new Vector2(pos.x, pos.z));
                if (gap >= need) return at;
            }
            return Rules.ClampInsideArena(pos + inward * need, 2.4f);
        }

        public Vector3 PointOutward(int playerId, float distance)
        {
            if (state == null || state.bugs == null || playerId < 0 || playerId >= state.bugs.Length)
                return Vector3.zero;
            Vector3 pos = state.bugs[playerId].position;
            Vector3 outward = new Vector3(pos.x, 0f, pos.z);
            if (outward.sqrMagnitude < 0.01f) outward = Vector3.back;
            outward.Normalize();
            BugState bug = state.bugs[playerId];
            float pad = (bug != null ? bug.radius : 1f) + 1.2f;
            return Rules.ClampInsideArena(pos + outward * distance, pad);
        }

        /// <summary>推进一个权威模拟片段；移动/碰撞分步执行，再结算经济、巢穴和蓄力。</summary>
        public void Tick(float dt)
        {
            if (state == null || !state.started || state.over || dt <= 0f) return;
            dt = Mathf.Min(dt, 0.1f);
            MatchKnobs active = ActiveKnobs;
            float previousElapsed = state.elapsed;
            int previousTier = ZoneTier;
            if (!tutorialHoldClock)
            {
                state.elapsed += dt;
                if (TutorialBattleDirector.LessonsActive)
                    state.elapsed = Mathf.Min(state.elapsed, Rules.TutorialElapsedCap(active));
            }
            state.tick++;
            int tier = ZoneTier;
            if (tier != previousTier)
            {
                Rules.ApplyZoneTier(active, tier);
                NotifyZoneIfChanged(previousTier);
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
                economy.ResolvePickups(state, AddGrow, Emit);
            }
            for (int i = 0; i < state.bugs.Length; i++)
                if (state.bugs[i].alive && !Rules.InsideArena(state.bugs[i].position)) MarkOut(state.bugs[i]);
            movement.MarkBabyOutOfBounds(state, Emit);
            if (!tutorialSuppressSpawns) economy.Tick(state, AddGrow, Emit);
            // 巢穴计时在拾取结算后执行；蓄力/松开只在完整固定 Tick 末采样一次。
            nestSystem.TickBeforeCollision(state, dt, Emit);
            nestSystem.TickAfterCollision(state, Emit);
            movement.TickCharge(state, inputs, dt);
            MatchPhase phase = Rules.Phase(active, state.elapsed);
            if (phase == MatchPhase.Rage && previousElapsed < active.regTime)
            {
                Rules.EnterRage(active, state.bugs);
                GameplayEvent?.Invoke("rage-start", Vector3.zero);
            }
            CheckEnd(phase);
            if (runMode == MatchRunMode.Host && state.over) SnapshotReady?.Invoke(CaptureSnapshot());
            StateChanged?.Invoke(state);
            for (int i = 0; i < inputs.Length; i++) if (inputs[i] != null) inputs[i].released = false;
        }

        /// <summary>将权威状态复制为可由 Unity JSON 序列化的快照。</summary>
        public MatchSnapshot CaptureSnapshot(bool includeKnobs = true)
        {
            if (state == null) return null;
            MatchSnapshot snapshot = new MatchSnapshot
            {
                version = 9,
                tick = state.tick,
                playerCount = state.playerCount,
                randomSeed = state.randomSeed,
                elapsed = state.elapsed,
                started = state.started,
                over = state.over,
                winnerId = state.winnerId,
                phase = Rules.Phase(ActiveKnobs, state.elapsed),
                knobs = includeKnobs ? ActiveKnobs : null,
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
                nestChainActive = state.nestChainActive,
                cricketIndex = CopyInts(state.cricketIndex),
                playerIn = CopyBools(state.playerIn),
                place = CopyInts(state.place),
                matchScore = CopyInts(state.matchScore),
                killStreak = CopyInts(state.killStreak)
            };
            snapshot.lastInputSequence = new int[state.bugs.Length];
            for (int i = 0; i < snapshot.lastInputSequence.Length; i++)
                snapshot.lastInputSequence[i] = inputs[i] == null ? 0 : inputs[i].sequence;
            PackRoster(snapshot);
            for (int i = 0; i < state.bugs.Length; i++)
            {
                BugState b = state.bugs[i];
                snapshot.bugs[i] = new BugSnapshot
                {
                    id = b.id, catalogId = b.catalogId, alive = b.alive, position = b.position, velocity = b.velocity,
                    chargeDirection = b.chargeDirection,
                    height = b.height, verticalVelocity = b.verticalVelocity, radius = b.radius,
                    chargeTime = b.chargeTime, stamina = b.stamina, grow = b.grow, score = b.score, lastHitId = b.lastHitId,
                    buffSizeT = b.buffSizeT, buffShieldT = b.buffShieldT, buffChargeT = b.buffChargeT,
                    charging = b.charging, airborne = b.airborne, hitTier = (int)b.hitTier,
                    launchVelocity = b.launchVelocity,
                    guanYuReviveLeft = b.guanYuReviveLeft, guanYuGhost = b.guanYuGhost, luBuArmorT = b.luBuArmorT,
                    diaochanStealArmed = b.diaochanStealArmed
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
                snapshot.eggs[i] = new EggSnapshot { position = e.position, velocity = e.velocity, ownerId = e.ownerId, remaining = Mathf.Max(0f, e.hatchAt - state.elapsed), hatchDuration = e.hatchDuration, alive = e.alive };
            }
            for (int i = 0; i < state.babies.Count; i++)
            {
                BabyState b = state.babies[i];
                snapshot.babies[i] = new BabySnapshot { id = b.id, ownerId = b.ownerId, position = b.position, velocity = b.velocity,
                    chargeDirection = b.chargeDirection,
                    height = b.height, verticalVelocity = b.verticalVelocity, charging = b.charging, grow = b.grow, score = b.score,
                    buffSizeT = b.buffSizeT, buffShieldT = b.buffShieldT, buffChargeT = b.buffChargeT,
                    hitTier = (int)b.hitTier, remaining = Mathf.Max(0f, b.lifeEnd - state.elapsed), alive = b.alive,
                    launchVelocity = b.launchVelocity };
            }
            return snapshot;
        }

        /// <summary>用主机快照替换本地状态，并重建游标，避免恢复权威后重复生成或复用 ID。</summary>
        public void ApplySnapshot(MatchSnapshot snapshot)
        {
            if (snapshot == null || snapshot.bugs == null) return;
            int snapshotPlayers = Mathf.Clamp(snapshot.playerCount, 1, MaxPlayers);
            if (snapshot.bugs.Length != snapshotPlayers) return;
            // 客户端不跑 Tick，收口事件只能在这里按档位变化补发，桌面裁切和镜头才跟得上。
            int previousTier = ZoneTier;
            bool knobsChanged = snapshot.knobs != null;
            if (knobsChanged) knobs = snapshot.knobs;
            if (state == null || state.bugs.Length != snapshot.playerCount)
            {
                knobs = knobs ?? Rules.DefaultKnobs();
                ResetMatch(snapshot.playerCount, snapshot.randomSeed);
                knobsChanged = true;
            }
            state.knobs = knobs;
            state.tick = snapshot.tick;
            state.randomSeed = snapshot.randomSeed;
            state.playerCount = Mathf.Clamp(snapshot.playerCount, 1, MaxPlayers);
            state.elapsed = snapshot.elapsed;
            state.started = snapshot.started;
            state.over = snapshot.over;
            state.winnerId = snapshot.winnerId;
            if (state.playerCount <= 1) Rules.ApplyZoneTier(knobs, Rules.LastZoneTier);
            else Rules.ApplyZoneAt(knobs, state.elapsed);
            NotifyZoneIfChanged(previousTier);
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
                b.id = s.id; b.catalogId = s.catalogId; b.alive = s.alive; b.position = s.position; b.previousPosition = s.position - s.velocity * FixedDeltaTime;
                b.velocity = s.velocity; b.height = s.height; b.verticalVelocity = s.verticalVelocity; b.airborne = s.airborne || s.height > 0.03f || s.verticalVelocity > 0f;
                b.radius = s.radius; b.chargeTime = s.chargeTime; b.grow = s.grow; b.score = s.score; b.lastHitId = s.lastHitId;
                b.stamina = snapshot.version >= 5 ? Mathf.Max(0f, s.stamina) : Rules.StaminaMaxOf(knobs, b);
                b.buffSizeT = s.buffSizeT; b.buffShieldT = s.buffShieldT; b.buffChargeT = s.buffChargeT; b.charging = s.charging;
                b.hitTier = Rules.CanonicalHitTier((HitTier)Mathf.Clamp(s.hitTier, 0, (int)HitTier.Slip));
                b.launchVelocity = snapshot.version >= 8 ? s.launchVelocity : Rules.Planar(s.velocity);
                b.initialSpeed = new Vector2(b.launchVelocity.x, b.launchVelocity.z).magnitude;
                if (snapshot.version >= 9 && s.chargeDirection.sqrMagnitude > 0.0001f)
                    b.chargeDirection = s.chargeDirection.normalized;
                b.guanYuReviveLeft = s.guanYuReviveLeft;
                b.luBuArmorT = s.luBuArmorT;
                b.diaochanStealArmed = s.diaochanStealArmed;
            }
            ApplyPickupSnapshots(snapshot.pickups);
            if (snapshot.version < 4)
            {
                state.nextPickupId = 0;
                for (int i = 0; i < state.pickups.Count; i++)
                    state.nextPickupId = Mathf.Max(state.nextPickupId, state.pickups[i].id + 1);
            }
            ApplyEggSnapshots(snapshot.eggs);
            ApplyBabySnapshots(snapshot.babies, snapshot.version);
            if (snapshot.version < 4)
            {
                state.nextBabyId = 100;
                for (int i = 0; i < state.babies.Count; i++)
                    state.nextBabyId = Mathf.Max(state.nextBabyId, state.babies[i].id + 1);
            }
            UnpackRoster(snapshot);
            if (state.bugs != null)
            {
                for (int i = 0; i < state.bugs.Length; i++)
                {
                    BugState bug = state.bugs[i];
                    CricketPick pick = GetPick(i, CricketIndex(i));
                    int catalog = pick == null ? 0 : pick.catalogId;
                    int quality = pick == null ? 1 : Mathf.Clamp(pick.quality, 1, 4);
                    int temperament = pick == null ? 1 : Mathf.Clamp(pick.temperament, 1, 4);
                    if (knobsChanged || bug.catalogId != catalog || bug.quality != quality || bug.temperament != temperament)
                        ApplyPickToBug(bug, i, CricketIndex(i), false);
                    if (snapshot.bugs == null || i >= snapshot.bugs.Length) continue;
                    bug.guanYuReviveLeft = snapshot.bugs[i].guanYuReviveLeft;
                    bug.luBuArmorT = snapshot.bugs[i].luBuArmorT;
                    bug.diaochanStealArmed = snapshot.bugs[i].diaochanStealArmed;
                    if (snapshot.bugs[i].guanYuGhost)
                        Rules.EnterGuanYuGhost(knobs, bug);
                    else
                        bug.guanYuGhost = false;
                }
            }
            ApplyNestSnapshot(snapshot.nest);
            if (snapshot.version < 4)
            {
                state.nestChainActive = (state.nest != null && state.nest.alive) || state.eggs.Count > 0 || state.babies.Count > 0;
                state.nextNestAt = state.nestChainActive
                    ? float.MaxValue
                    : (state.elapsed < knobs.nestFirstT ? knobs.nestFirstT : state.elapsed + Mathf.Max(0f, knobs.nestGap));
            }
            StateChanged?.Invoke(state);
        }

        void ApplyPickupSnapshots(PickupSnapshot[] snaps)
        {
            if (snaps == null)
            {
                state.pickups.Clear();
                return;
            }
            ResizeList(state.pickups, snaps.Length, () => new PickupState(0, Vector3.zero, string.Empty));
            for (int i = 0; i < snaps.Length; i++)
            {
                PickupSnapshot s = snaps[i];
                PickupState p = state.pickups[i];
                p.id = s.id;
                p.position = s.position;
                p.kind = s.kind;
                p.alive = s.alive;
            }
        }

        void ApplyEggSnapshots(EggSnapshot[] snaps)
        {
            if (snaps == null)
            {
                state.eggs.Clear();
                return;
            }
            ResizeList(state.eggs, snaps.Length, () => new EggState());
            for (int i = 0; i < snaps.Length; i++)
            {
                EggSnapshot s = snaps[i];
                EggState e = state.eggs[i];
                e.position = s.position;
                e.previousPosition = s.position - s.velocity * FixedDeltaTime;
                e.velocity = s.velocity;
                e.ownerId = s.ownerId;
                e.hatchAt = state.elapsed + s.remaining;
                e.hatchDuration = s.hatchDuration > 0.01f ? s.hatchDuration : Mathf.Max(s.remaining, 0.01f);
                e.alive = s.alive;
            }
        }

        void ApplyBabySnapshots(BabySnapshot[] snaps, int version)
        {
            if (snaps == null)
            {
                state.babies.Clear();
                return;
            }
            ResizeList(state.babies, snaps.Length, () => new BabyState());
            for (int i = 0; i < snaps.Length; i++)
            {
                BabySnapshot s = snaps[i];
                Vector2 restoredDirection = version >= 9 ? s.chargeDirection : new Vector2(s.velocity.x, s.velocity.z);
                restoredDirection = restoredDirection.sqrMagnitude > 0.0001f ? restoredDirection.normalized : Vector2.up;
                BabyState baby = state.babies[i];
                baby.id = s.id;
                baby.ownerId = s.ownerId;
                baby.position = s.position;
                baby.previousPosition = s.position - s.velocity * FixedDeltaTime;
                baby.velocity = s.velocity;
                baby.chargeDirection = restoredDirection;
                baby.height = s.height;
                baby.verticalVelocity = s.verticalVelocity;
                baby.charging = s.charging;
                baby.airborne = s.height > 0.03f || s.verticalVelocity > 0f;
                baby.grow = s.grow;
                baby.score = s.score;
                baby.buffSizeT = s.buffSizeT;
                baby.buffShieldT = s.buffShieldT;
                baby.buffChargeT = s.buffChargeT;
                baby.hitTier = Rules.CanonicalHitTier((HitTier)Mathf.Clamp(s.hitTier, 0, (int)HitTier.Slip));
                baby.lifeEnd = state.elapsed + s.remaining;
                baby.alive = s.alive;
                baby.launchVelocity = version >= 8 ? s.launchVelocity : Rules.Planar(s.velocity);
                baby.initialSpeed = version >= 8
                    ? new Vector2(s.launchVelocity.x, s.launchVelocity.z).magnitude
                    : new Vector2(s.velocity.x, s.velocity.z).magnitude;
                Rules.RefreshBabyBody(knobs, baby);
            }
        }

        void ApplyNestSnapshot(NestSnapshot snap)
        {
            if (snap == null)
            {
                state.nest = null;
                return;
            }
            if (state.nest == null) state.nest = new NestState();
            state.nest.position = snap.position;
            state.nest.hp = snap.hp;
            state.nest.alive = snap.alive;
        }

        static void ResizeList<T>(List<T> list, int count, Func<T> create)
        {
            while (list.Count < count) list.Add(create());
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
        }

        /// <summary>档位变化时通知表现层。主机走 Tick，客户端走 ApplySnapshot。</summary>
        void NotifyZoneIfChanged(int previousTier)
        {
            int tier = ZoneTier;
            if (tier == previousTier) return;
            ZoneSnapped?.Invoke(tier);
            GameplayEvent?.Invoke("zone-snap", Vector3.zero);
        }

        private void OnNestHit(BugState bug)
        {
            if (state.nest != null && state.nest.hp <= 0f) state.pendingNestOwnerId = bug.id;
        }

        // 护盾只执行一次救援投影；单机把唯一角色拉回场内，多人模式则将其淘汰。
        private void MarkOut(BugState bug)
        {
            if (!bug.alive) return;
            if (Rules.TryShieldSave(knobs, bug))
            {
                bug.previousPosition = bug.position;
                Emit("shield-save", bug.position);
                return;
            }
            if (state.playerCount == 1)
            {
                bug.position = Rules.SoloPullbackPoint();
                bug.previousPosition = bug.position;
                bug.velocity = Vector3.zero;
                bug.height = 0f;
                bug.verticalVelocity = 0f;
                bug.airborne = false;
                bug.charging = false;
                bug.chargeDirection = Vector2.up;
                bug.slideMu = Rules.GripOf(knobs, bug);
                Emit("solo-pullback", bug.position);
                return;
            }
            if (Rules.ConsumeGuanYuRevive(bug))
            {
                int reviveLeft = bug.guanYuReviveLeft;
                RecycleBug(bug, SpawnPoint(bug.id));
                ApplyPickToBug(bug, bug.id, CricketIndex(bug.id));
                bug.guanYuReviveLeft = reviveLeft;
                Rules.EnterGuanYuGhost(knobs, bug);
                Emit("revive", bug.position);
                return;
            }
            bug.alive = false;
            bug.charging = false;
            bug.holding = false;
            bug.pendingCharge = false;
            bug.airborne = true;
            ResetKillStreak(bug.id);
            BugState killer = FindBug(bug.lastHitId);
            if (killer != null && killer != bug) AwardKill(killer);
            int slot = CricketIndex(bug.id);
            CricketOut?.Invoke(bug.id, slot);
            Emit("out", bug.position);
            if (TrySpawnNext(bug.id)) return;
            EliminatePlayer(bug.id);
        }

        private BugState FindBug(int id)
        {
            for (int i = 0; i < state.bugs.Length; i++) if (state.bugs[i].id == id && state.bugs[i].alive) return state.bugs[i];
            return null;
        }

        private void AddGrow(BugState bug)
        {
            if (bug == null || !bug.alive) return;
            if (bug.grow < Rules.GrowMax) bug.grow++;
            Rules.RefreshBody(knobs, bug);
            Emit("grow", bug.position);
        }

        private void AwardKill(BugState killer)
        {
            if (killer == null || !killer.alive || state == null) return;
            int id = killer.id;
            EnsureScoreArrays(state.playerCount);
            if (id < 0 || id >= state.killStreak.Length) return;
            state.killStreak[id]++;
            int points = Rules.KillScoreBase * state.killStreak[id];
            state.matchScore[id] += points;
            killer.score = state.matchScore[id];
            Emit("kill", killer.position);
        }

        private void ResetKillStreak(int playerId)
        {
            if (state == null || state.killStreak == null || playerId < 0 || playerId >= state.killStreak.Length) return;
            state.killStreak[playerId] = 0;
        }

        private void EnsureScoreArrays(int playerCount)
        {
            playerCount = Mathf.Max(1, playerCount);
            if (state.matchScore == null || state.matchScore.Length < playerCount)
                state.matchScore = CopyInts(state.matchScore, playerCount);
            if (state.killStreak == null || state.killStreak.Length < playerCount)
                state.killStreak = CopyInts(state.killStreak, playerCount);
        }

        private void CheckEnd(MatchPhase phase)
        {
            if (state.playerCount <= 1) return;
            int remaining = CountPlayersIn();
            if (remaining > 1 && phase != MatchPhase.Over) return;
            if (remaining == 1)
            {
                int winner = FindOnlyPlayerIn();
                if (winner >= 0) AwardPlace(winner, 1);
                state.winnerId = winner;
            }
            else
            {
                RankSurvivorsAtTimeUp();
            }
            state.over = true;
            state.started = false;
            Emit("match-over", Vector3.zero);
        }

        /// <summary>时间到了：没出局的人占前面名次，生命多的靠前，相同再比本局积分。</summary>
        private void RankSurvivorsAtTimeUp()
        {
            int[] ids = new int[state.playerCount];
            int count = 0;
            for (int i = 0; i < state.playerCount; i++)
            {
                if (!PlayerStillIn(i) || Place(i) > 0) continue;
                ids[count++] = i;
            }
            for (int a = 0; a < count; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    int cmp = LivesLeft(ids[b]).CompareTo(LivesLeft(ids[a]));
                    if (cmp == 0) cmp = MatchScore(ids[b]).CompareTo(MatchScore(ids[a]));
                    if (cmp == 0) cmp = ids[a].CompareTo(ids[b]);
                    if (cmp < 0) continue;
                    int tmp = ids[a];
                    ids[a] = ids[b];
                    ids[b] = tmp;
                }
            }
            for (int i = 0; i < count; i++) AwardPlace(ids[i], i + 1);
            state.winnerId = count > 0 ? ids[0] : -1;
        }

        private int FindOnlyAlive()
        {
            for (int i = 0; i < state.bugs.Length; i++) if (state.bugs[i].alive) return state.bugs[i].id;
            return -1;
        }

        private int FindOnlyPlayerIn()
        {
            if (state.playerIn == null) return FindOnlyAlive();
            for (int i = 0; i < state.playerIn.Length; i++) if (state.playerIn[i]) return i;
            return -1;
        }

        private int CountPlayersIn()
        {
            if (state.playerIn == null) return 0;
            int count = 0;
            for (int i = 0; i < state.playerIn.Length; i++) if (state.playerIn[i]) count++;
            return count;
        }

        private void EliminatePlayer(int playerId)
        {
            if (state.playerIn == null || playerId < 0 || playerId >= state.playerIn.Length) return;
            if (!state.playerIn[playerId]) return;
            state.playerIn[playerId] = false;
            AwardPlace(playerId, CountPlayersIn() + 1);
            PlayerEliminated?.Invoke(playerId);
            Emit("player-out", state.bugs[playerId].position);
        }

        private void AwardPlace(int playerId, int place)
        {
            if (state.place == null || playerId < 0 || playerId >= state.place.Length) return;
            if (state.place[playerId] > 0) return;
            state.place[playerId] = Mathf.Max(1, place);
        }

        private bool TrySpawnNext(int playerId)
        {
            if (state.cricketIndex == null || playerId < 0 || playerId >= state.cricketIndex.Length) return false;
            int next = state.cricketIndex[playerId] + 1;
            if (next >= LivesPerPlayer) return false;
            CricketPick nextPick = GetPick(playerId, next);
            if (nextPick == null || !nextPick.HasBug()) return false;
            state.cricketIndex[playerId] = next;
            RecycleBug(state.bugs[playerId], SpawnPoint(playerId));
            ApplyPickToBug(state.bugs[playerId], playerId, next);
            CricketIn?.Invoke(playerId, next);
            Emit("cricket-in", state.bugs[playerId].position);
            return true;
        }

        private void RecycleBug(BugState bug, Vector3 spawn)
        {
            bug.alive = true;
            bug.position = spawn;
            bug.previousPosition = spawn;
            bug.velocity = Vector3.zero;
            Rules.ClearLaunch(bug);
            bug.height = 0f;
            bug.verticalVelocity = 0f;
            bug.airborne = false;
            bug.charging = false;
            bug.holding = false;
            bug.pendingCharge = false;
            bug.chargeTime = 0f;
            bug.diaochanStealArmed = false;
            Rules.ClearLaunch(bug);
            bug.chargeDirection = new Vector2(-spawn.x, -spawn.z);
            if (bug.chargeDirection.sqrMagnitude < 0.01f) bug.chargeDirection = Vector2.up;
            else bug.chargeDirection.Normalize();
            bug.slideMu = Rules.GripOf(knobs, bug);
            bug.grow = 0;
            bug.lastHitId = -1;
            bug.hitTier = HitTier.None;
            bug.buffSizeT = 0f;
            bug.buffShieldT = 0f;
            bug.buffChargeT = 0f;
            bug.rageSize = false;
            bug.rageCharge = false;
            bug.score = 0;
            bug.stamina = Rules.StaminaMaxOf(knobs, bug);
            Rules.RefreshBody(knobs, bug);
        }

        private Vector3 SpawnPoint(int playerId)
        {
            if (state != null && state.playerCount <= 1) return Rules.SoloPullbackPoint();
            Vector3 home = state != null && state.homeSpawn != null && playerId >= 0 && playerId < state.homeSpawn.Length
                ? state.homeSpawn[playerId]
                : Rules.OpeningSpawn(playerId, ActiveKnobs.spawnEdge);
            float radius = 0f;
            if (state != null && state.bugs != null && playerId >= 0 && playerId < state.bugs.Length && state.bugs[playerId] != null)
                radius = state.bugs[playerId].radius;
            return Rules.RespawnPoint(home, radius, ActiveKnobs.spawnEdge);
        }

        private void EnsureRoster(int playerCount)
        {
            if (state == null) return;
            playerCount = Mathf.Clamp(playerCount, 1, MaxPlayers);
            state.roster = CloneRoster(pendingRoster, playerCount);
            state.cricketIndex = new int[playerCount];
            state.playerIn = new bool[playerCount];
            state.place = new int[playerCount];
            state.matchScore = new int[playerCount];
            state.killStreak = new int[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                state.playerIn[i] = true;
                if (state.roster[i] == null) state.roster[i] = DefaultPicks();
            }
        }

        private void ApplyPickToBug(BugState bug, int playerId, int slot, bool refillStamina = true)
        {
            if (bug == null) return;
            CricketPick pick = GetPick(playerId, slot);
            bug.catalogId = pick == null ? 0 : pick.catalogId;
            int quality = pick == null ? 1 : Mathf.Clamp(pick.quality, 1, 4);
            int temperament = pick == null ? 1 : Mathf.Clamp(pick.temperament, 1, 4);
            bug.quality = quality;
            bug.temperament = temperament;
            CricketCatalog.ApplyCombatBias(bug, quality, temperament);
            Rules.RefreshBody(knobs, bug);
            bug.slideMu = Rules.GripOf(knobs, bug);
            bug.guanYuGhost = false;
            if (refillStamina)
            {
                bug.stamina = Rules.StaminaMaxOf(knobs, bug);
                bug.guanYuReviveLeft = Rules.IsGuanYu(bug) ? Mathf.Max(0, knobs.guanYuRevives) : 0;
                bug.luBuArmorT = 0f;
                bug.diaochanStealArmed = false;
            }
        }

        private CricketPick GetPick(int playerId, int slot)
        {
            if (state == null || state.roster == null || playerId < 0 || playerId >= state.roster.Length) return null;
            CricketPick[] picks = state.roster[playerId];
            if (picks == null || slot < 0 || slot >= picks.Length) return null;
            return picks[slot];
        }

        private static void StoreRoster(ref CricketPick[][] target, int playerCount, int playerId, CricketPick[] picks)
        {
            playerCount = Mathf.Max(playerCount, playerId + 1);
            if (target == null || target.Length < playerCount)
            {
                CricketPick[][] next = new CricketPick[playerCount][];
                if (target != null)
                    for (int i = 0; i < target.Length; i++) next[i] = target[i];
                target = next;
            }
            target[playerId] = DefaultPicks();
            if (picks == null) return;
            for (int i = 0; i < LivesPerPlayer && i < picks.Length; i++)
            {
                CricketPick pick = picks[i] ?? new CricketPick();
                target[playerId][i] = new CricketPick
                {
                    catalogId = pick.catalogId,
                    quality = Mathf.Clamp(pick.quality, 1, 4),
                    temperament = Mathf.Clamp(pick.temperament, 1, 4)
                };
            }
        }

        private static CricketPick[][] CloneRoster(CricketPick[][] source, int playerCount)
        {
            CricketPick[][] copy = new CricketPick[playerCount][];
            for (int i = 0; i < playerCount; i++)
            {
                copy[i] = DefaultPicks();
                if (source == null || i >= source.Length || source[i] == null) continue;
                for (int s = 0; s < LivesPerPlayer && s < source[i].Length; s++)
                {
                    CricketPick pick = source[i][s] ?? new CricketPick();
                    copy[i][s] = new CricketPick
                    {
                        catalogId = pick.catalogId,
                        quality = pick.quality,
                        temperament = pick.temperament
                    };
                }
            }
            return copy;
        }

        private static CricketPick[] DefaultPicks()
        {
            CricketPick[] picks = new CricketPick[LivesPerPlayer];
            for (int i = 0; i < picks.Length; i++)
                picks[i] = new CricketPick { catalogId = 0, quality = 1, temperament = 1 };
            return picks;
        }

        private void PackRoster(MatchSnapshot snapshot)
        {
            int count = state.playerCount;
            snapshot.rosterCatalog = new int[count * LivesPerPlayer];
            snapshot.rosterQuality = new int[count * LivesPerPlayer];
            snapshot.rosterTemperament = new int[count * LivesPerPlayer];
            if (state.roster == null) return;
            for (int i = 0; i < count; i++)
            {
                CricketPick[] picks = i < state.roster.Length ? state.roster[i] : null;
                for (int s = 0; s < LivesPerPlayer; s++)
                {
                    int index = i * LivesPerPlayer + s;
                    CricketPick pick = picks != null && s < picks.Length ? picks[s] : null;
                    snapshot.rosterCatalog[index] = pick == null ? 0 : pick.catalogId;
                    snapshot.rosterQuality[index] = pick == null ? 1 : pick.quality;
                    snapshot.rosterTemperament[index] = pick == null ? 1 : pick.temperament;
                }
            }
        }

        private void UnpackRoster(MatchSnapshot snapshot)
        {
            int count = state.playerCount;
            if (snapshot.version < 6 || snapshot.cricketIndex == null)
            {
                EnsureRoster(count);
                if (state.bugs == null) return;
                for (int i = 0; i < count && i < state.bugs.Length; i++)
                {
                    if (state.bugs[i] != null && state.bugs[i].alive) continue;
                    state.cricketIndex[i] = LivesPerPlayer;
                    state.playerIn[i] = false;
                }
                return;
            }

            CopyIntsInPlace(ref state.cricketIndex, snapshot.cricketIndex, count);
            CopyBoolsInPlace(ref state.playerIn, snapshot.playerIn, count);
            CopyIntsInPlace(ref state.place, snapshot.place, count);
            CopyIntsInPlace(ref state.matchScore, snapshot.matchScore, count);
            if (snapshot.version >= 7)
                CopyIntsInPlace(ref state.killStreak, snapshot.killStreak, count);
            else if (state.killStreak == null || state.killStreak.Length != count)
                state.killStreak = new int[count];
            EnsureRoster(count);
            if (snapshot.rosterCatalog == null) return;
            for (int i = 0; i < count; i++)
            {
                CricketPick[] picks = state.roster[i];
                for (int s = 0; s < LivesPerPlayer; s++)
                {
                    int index = i * LivesPerPlayer + s;
                    if (index >= snapshot.rosterCatalog.Length) break;
                    CricketPick pick = picks[s] ?? new CricketPick();
                    pick.catalogId = snapshot.rosterCatalog[index];
                    pick.quality = snapshot.rosterQuality != null && index < snapshot.rosterQuality.Length ? snapshot.rosterQuality[index] : 1;
                    pick.temperament = snapshot.rosterTemperament != null && index < snapshot.rosterTemperament.Length ? snapshot.rosterTemperament[index] : 1;
                    picks[s] = pick;
                }
            }
        }

        private static int[] CopyInts(int[] source, int length = -1)
        {
            if (source == null) return length > 0 ? new int[length] : Array.Empty<int>();
            int count = length > 0 ? length : source.Length;
            int[] copy = new int[count];
            Array.Copy(source, copy, Mathf.Min(source.Length, count));
            return copy;
        }

        static void CopyIntsInPlace(ref int[] dest, int[] source, int length)
        {
            if (dest == null || dest.Length != length) dest = new int[length];
            if (source == null)
            {
                Array.Clear(dest, 0, length);
                return;
            }
            int n = Mathf.Min(source.Length, length);
            Array.Copy(source, dest, n);
            if (n < length) Array.Clear(dest, n, length - n);
        }

        private static bool[] CopyBools(bool[] source, int length = -1)
        {
            if (source == null)
            {
                bool[] empty = length > 0 ? new bool[length] : Array.Empty<bool>();
                for (int i = 0; i < empty.Length; i++) empty[i] = true;
                return empty;
            }
            int count = length > 0 ? length : source.Length;
            bool[] copy = new bool[count];
            Array.Copy(source, copy, Mathf.Min(source.Length, count));
            return copy;
        }

        static void CopyBoolsInPlace(ref bool[] dest, bool[] source, int length)
        {
            if (dest == null || dest.Length != length) dest = new bool[length];
            if (source == null)
            {
                for (int i = 0; i < length; i++) dest[i] = true;
                return;
            }
            int n = Mathf.Min(source.Length, length);
            Array.Copy(source, dest, n);
            for (int i = n; i < length; i++) dest[i] = true;
        }

        private void Emit(string kind, Vector3 position)
        {
            GameplayEvent?.Invoke(kind, position);
        }
    }
}
