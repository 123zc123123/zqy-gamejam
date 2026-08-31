using System;
using System.Collections.Generic;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>决定由哪个组件推进模拟。</summary>
    public enum MatchRunMode { Offline, Host, Client }

    /// <summary>用于选择命中后摩擦力的碰撞响应档位。</summary>
    public enum HitTier { None, Normal, Control, Slip }

    [Serializable]
    /// <summary>
    /// 一次完整输入采样。方向以 X/Z 标量保存，便于 Unity JSON 工具序列化为局域网消息。
    /// </summary>
    public sealed class InputFrame
    {
        public int playerId;
        public int sequence;
        public float x;
        public float z;
        public bool held;
        public bool released;
        // 触摸方向盘使用“拖动距离决定蓄力”；键盘/旧网络包为 false 时仍沿用按住时长蓄力。
        public bool distanceCharge;
        // 触摸点距离除以方向盘最大半径后的 0~1 蓄力比例。
        public float charge01;

        /// <summary>以 Unity 的 X/Y 向量形式返回保存的平面瞄准方向。</summary>
        public Vector2 Direction => new Vector2(x, z);

        public InputFrame() { }

        public InputFrame(int id, Vector2 direction, bool isHeld, bool isReleased, int inputSequence = 0, bool useDistanceCharge = false, float normalizedCharge = 0f)
        {
            playerId = id;
            x = direction.x;
            z = direction.y;
            held = isHeld;
            released = isReleased;
            sequence = inputSequence;
            distanceCharge = useDistanceCharge;
            charge01 = Mathf.Clamp01(normalizedCharge);
        }
    }

    [Serializable]
    /// <summary>记录玩家可以撞击的静态巢穴权威状态。</summary>
    public sealed class NestState
    {
        public Vector3 position;
        public float hp;
        public bool alive;

        // 接触 ID 仅在运行时使用，避免持续重叠在相邻 Tick 被重复计为多次撞击。
        [NonSerialized] public readonly HashSet<int> touching = new HashSet<int>();
    }

    [Serializable]
    /// <summary>记录巢穴孵化出的幼虫运行时状态。</summary>
    public sealed class BabyState
    {
        public int id;
        public int ownerId = -1;
        public Vector3 position;
        public Vector3 previousPosition;
        public Vector3 velocity;
        public float radius;
        public float mass;
        public float height;
        public float verticalVelocity;
        public float chargeTime;
        public float initialSpeed;
        public float slideMu;
        public Vector2 chargeDirection = Vector2.up;
        public float lifeEnd;
        public float attackCooldown;
        public int grow;
        public int score;
        public float buffSizeT;
        public float buffShieldT;
        public float buffChargeT;
        public bool charging;
        public bool holding;
        public bool pendingCharge;
        public bool airborne;
        public HitTier hitTier = HitTier.None;
        public bool alive = true;

        public BabyState() { }

        public BabyState(int babyId, Vector3 at, int owner, float end, MatchKnobs knobs)
        {
            id = babyId;
            ownerId = owner;
            position = at;
            previousPosition = at;
            lifeEnd = end;
            DouQuquRules.RefreshBabyBody(knobs, this);
        }
    }

    [Serializable]
    /// <summary>随网络快照发送的可序列化蟋蟀状态。</summary>
    public sealed class BugSnapshot
    {
        public int id;
        public bool alive;
        public Vector3 position;
        public Vector3 velocity;
        public float height;
        public float verticalVelocity;
        public float radius;
        public float chargeTime;
        public int grow;
        public int score;
        public int lastHitId;
        public float buffSizeT;
        public float buffShieldT;
        public float buffChargeT;
        public bool charging;
        public bool airborne;
        public int hitTier;
    }

    [Serializable]
    /// <summary>随网络快照发送的可序列化拾取物状态。</summary>
    public sealed class PickupSnapshot
    {
        public int id;
        public bool alive;
        public string kind;
        public Vector3 position;
    }

    [Serializable]
    /// <summary>可序列化蛋状态；remaining 是相对于快照时刻的剩余时间。</summary>
    public sealed class EggSnapshot
    {
        public Vector3 position;
        public Vector3 velocity;
        public int ownerId;
        public float remaining;
        public bool alive;
    }

    [Serializable]
    /// <summary>可序列化幼虫状态；remaining 是相对于快照时刻的剩余时间。</summary>
    public sealed class BabySnapshot
    {
        public int id;
        public int ownerId;
        public Vector3 position;
        public Vector3 velocity;
        public float height;
        public float verticalVelocity;
        public bool charging;
        public int grow;
        public int score;
        public float buffSizeT;
        public float buffShieldT;
        public float buffChargeT;
        public int hitTier;
        public float remaining;
        public bool alive;
    }

    [Serializable]
    /// <summary>随网络快照发送的可序列化巢穴状态。</summary>
    public sealed class NestSnapshot
    {
        public Vector3 position;
        public float hp;
        public bool alive;
    }

    [Serializable]
    /// <summary>
    /// 完整权威状态。v4 包含经济和巢穴游标，使客户端或恢复后的主机可以继续确定性运行。
    /// </summary>
    public sealed class MatchSnapshot
    {
        public int version = 4;
        public int tick;
        public int playerCount;
        public int randomSeed;
        public float elapsed;
        public bool started;
        public bool over;
        public int winnerId = -1;
        public MatchPhase phase;
        public MatchKnobs knobs;
        public BugSnapshot[] bugs;
        public PickupSnapshot[] pickups;
        public EggSnapshot[] eggs;
        public BabySnapshot[] babies;
        public NestSnapshot nest;
        // 保存临时模拟游标，应用快照后可以安全恢复权威，避免复用 ID 或提前生成经济/巢穴波次。
        public float lastHeartAt;
        public int nextItemIndex;
        public string lastItemKind;
        public int nextPickupId;
        public int nextBabyId;
        public float nextNestAt;
        public float lastNestClearAt;
        public int pendingNestOwnerId;
        public bool nestChainActive;
    }

    [Serializable]
    /// <summary>
    /// 由控制器和纯逻辑系统共同使用的可变模拟状态。仅运行时游标标记为 NonSerialized，
    /// 应用快照时单独重建或传递。
    /// </summary>
    public sealed class MatchState
    {
        public int playerCount;
        public int randomSeed;
        public int tick;
        public float elapsed;
        public bool started;
        public bool over;
        public int winnerId = -1;
        public MatchKnobs knobs;
        public BugState[] bugs = new BugState[0];
        public bool[] humanPlayers = new bool[0];
        public readonly List<PickupState> pickups = new List<PickupState>();
        public readonly List<EggState> eggs = new List<EggState>();
        public readonly List<BabyState> babies = new List<BabyState>();
        public NestState nest;

        [NonSerialized] public float lastHeartAt = -1f;
        [NonSerialized] public int nextItemIndex;
        [NonSerialized] public string lastItemKind;
        [NonSerialized] public int nextPickupId;
        [NonSerialized] public int nextBabyId = 100;
        [NonSerialized] public float nextNestAt;
        [NonSerialized] public float lastNestClearAt = -1f;
        [NonSerialized] public int pendingNestOwnerId = -1;
        [NonSerialized] public bool nestChainActive;
    }
}
