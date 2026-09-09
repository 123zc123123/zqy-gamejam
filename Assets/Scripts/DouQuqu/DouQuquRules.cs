using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace DouQuqu
{
    /// <summary>按对局时间划分的玩法阶段。</summary>
    public enum MatchPhase
    {
        Probe,
        Open,
        Close,
        Rage,
        Over
    }

    [Serializable]
    /// <summary>
    /// 对局规则参数。真源是场景里 MatchController 的 Inspector；
    /// 类字段默认值只在新挂组件、还没有序列化值时用。
    /// 计算时会在规则函数内部做必要的下限保护。
    /// </summary>
    public sealed class MatchKnobs : ISerializationCallbackReceiver
    {
        [Header("蓄力")]
        [FormerlySerializedAs("tMin")]
        [InspectorCn("蓄力下限", "未满松手取消；0 = 点一下就跳")]
        public float tChargeMin = 0f;
        [FormerlySerializedAs("tMax")]
        [InspectorCn("蓄满时间", "按满要多久；蓄满后可继续按，距离不再涨。不进跳出力气")]
        public float tChargeMax = 1f;
        [InspectorCn("点跳距离", "点一下的水平总位移。满蓄 = 该值 × 距离比。改重力/仰角/摩擦不改落点")]
        public float dMin = 1f;
        [InspectorCn("满蓄距离比", "满蓄水平总位移 / 点跳水平总位移。蓄力进度对距离线性")]
        [Range(1.2f, 8f)]
        public float jumpDistRatio = 3f;
        [HideInInspector]
        public float tFloor = 0.3f;
        [InspectorCn("幼虫蓄力速度", "崽 A1 = 该值 × 崽蓄力速度倍率。玩家跳跃不读")]
        public float vRate = 40f;
        [InspectorCn("起跳仰角", "度；与摩擦一起定空中匀速占比")]
        public float theta = 15f;
        [InspectorCn("蓄力强化倍率", "拾取与狂暴共用。加快蓄满、点跳变大；未强化满蓄距离不变")]
        public float chargeScale = 1.25f;
        [InspectorCn("蓄力强化持续", "秒；仅拾取，狂暴不读")]
        public float chargeBuffT = 5f;
        [InspectorCn("狂暴加成", "加时全员蓄力速度、耐力恢复同乘；不变大")]
        public float rageBoost = 1.25f;

        [Header("耐力")]
        [InspectorCn("耐力上限", "开局满；不参与跳跃和碰撞公式")]
        public float staminaMax = 3f;
        [InspectorCn("蓄满耐力", "蓄满时的蓄力耐力；实际扣 = 该值 × 蓄力比例")]
        public float staminaCost = 0.7f;
        [InspectorCn("起跳耐力", "每次有效起跳固定加扣；点跳只扣这一笔")]
        public float staminaJump = 0.3f;
        [InspectorCn("耐力恢复", "落地未蓄力时每秒恢复；空中不恢复")]
        public float staminaRegen = 1f;
        [InspectorCn("蓄力时恢复倍率", "蓄力时恢复 = 耐力恢复 × 该值；0 = 蓄力不回")]
        public float staminaRegenCharge = 0f;
        [InspectorCn("耐力格数", "身周耐力圆环格数")]
        public int staminaSlots = 3;

        [Header("跳跃与碰撞")]
        [InspectorCn("基础质量", "对撞分速度，也进抵抗")]
        public float mass = 1f;
        [InspectorCn("重力", "抛物线与落地减速都用")]
        public float gravity = 120f;
        [InspectorCn("地面摩擦", "落地匀减速 a = μg，也改匀速占比")]
        public float mu = 1.8f;
        [InspectorCn("抵抗系数", "R = K × 质量 × 出发法向速度；K 大则更难失衡")]
        [Range(0f, 2f)]
        public float resistK = 1f;
        [InspectorCn("失衡摩擦倍率", "失衡档 μ′ = μ × 该值；小于 1 滑得更远")]
        [Range(0.3f, 1f)]
        public float muSlipScale = 0.8f;
        [HideInInspector] public float rStand = 0.4f;
        [HideInInspector] public float rMax = 0.4f;
        [HideInInspector] public float rChargeScale = 0.5f;
        [HideInInspector] public float muCtrlScale = 1f;
        [HideInInspector] public int resistSchema;
        [HideInInspector] public int jumpKnobSchema;

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            EnsureJumpKnobs();
            if (resistSchema >= 1) return;
            resistK = 1f;
            muSlipScale = 0.8f;
            resistSchema = 1;
        }

        /// <summary>旧档把 tFloor×vRate 折成点跳距离，只跑一次。</summary>
        public void EnsureJumpKnobs()
        {
            if (jumpDistRatio < 1f) jumpDistRatio = 3f;
            if (jumpKnobSchema >= 1)
            {
                if (dMin < 0f) dMin = 0f;
                return;
            }
            float g = Mathf.Max(0.01f, gravity);
            float tan = Mathf.Tan(theta * Mathf.Deg2Rad);
            float friction = Mathf.Max(0.0001f, mu);
            float speed = Mathf.Max(0f, vRate) * Mathf.Max(0f, tFloor);
            float air = 2f * speed * speed * tan / g;
            float ground = speed * speed / (2f * friction * g);
            dMin = Mathf.Max(0.01f, air + ground);
            jumpKnobSchema = 1;
        }
        [InspectorCn("每层成长", "每层给半径和质量加的倍率")]
        public float growPer = 0.16f;
        [InspectorCn("开局半径", "成长和增大再乘")]
        public float bugR = 1.8f;

        [Header("体型与护盾")]
        [InspectorCn("增大倍率", "拾取与狂暴共用，半径和质量同乘")]
        public float sizeScale = 1.3f;
        [InspectorCn("增大持续", "秒；仅拾取，狂暴不读")]
        public float sizeT = 6f;
        [InspectorCn("护盾持续", "秒；未被消耗也会到期")]
        public float shieldT = 90f;
        [InspectorCn("护盾拉回余量", "护盾拉回区内后再往里留的余量")]
        public float shieldPad = 0.08f;

        [Header("人机")]
        [InspectorCn("人机攻击距离", "人机主动起跳的攻击距离")]
        public float aiAttackRange = 14f;
        [InspectorCn("人机贴边安全距", "路线太贴圈则改方向")]
        public float aiSafeEdgeMargin = 4f;

        [Header("对局与投放")]
        [InspectorCn("正赛时长", "秒；到点未结束则进加时")]
        public float regTime = 90f;
        [InspectorCn("加时时长", "秒；与正赛相加为硬截止")]
        public float otTime = 30f;
        [InspectorCn("开局饲料球", "开局饲料球数量")]
        public int heartStart = 4;
        [InspectorCn("场上饲料球上限", "不足才补，每次 1 颗")]
        public int heartCap = 6;
        [InspectorCn("饲料球补货批量", "未使用；饲料球补货仍每次 1 颗")]
        public int heartBatch = 6;
        [InspectorCn("道具补货数量", "到点补几颗限时道具，不超过场上上限")]
        public int itemBatch = 3;
        [InspectorCn("场上道具上限", "已满则挂起")]
        public int itemCap = 3;
        [InspectorCn("饲料球间隔", "正赛补饲料球间隔（秒）")]
        public float heartGap = 7f;
        [InspectorCn("加时饲料球间隔", "加时补饲料球间隔（秒）")]
        public float heartGapOt = 5f;
        [InspectorCn("开始补饲料球", "秒；此前只吃开局那批")]
        public float heartOpenAt = 20f;
        [InspectorCn("道具拾取半径", "饲料球另用固定半径")]
        public float itemR = 1.35f;
        [HideInInspector]
        public float v3Rate = 23f;
        [InspectorCn("道具离边", "限时道具离罐边的最小距离")]
        public float itemMinEdge = 2.4f;
        [InspectorCn("道具离虫", "投放点离活虫的最小距离")]
        public float itemMinBug = 3f;
        [InspectorCn("道具离饲料球", "投放点离已有饲料球的最小距离")]
        public float itemMinHeart = 1.6f;
        [InspectorCn("道具间距", "投放点离已有限时道具的最小距离")]
        public float itemMinItem = 2f;
        [InspectorCn("道具环带内径", "限时道具刷在环带上的内半径")]
        public float itemRingMin = 6f;
        [InspectorCn("道具环带外径", "限时道具刷在环带上的外半径")]
        public float itemRingMax = 12f;
        [InspectorCn("饲料球离边", "饲料球离罐边的最小距离")]
        public float heartMinEdge = 1.4f;
        [HideInInspector]
        public float heartMinBug = 1.3f;
        [HideInInspector]
        public float heartMinHeart = 1.25f;

        [Header("巢穴与幼虫")]
        [InspectorCn("房子血量", "一次有效撞击 -1")]
        public float nestHP = 4f;
        [InspectorCn("房子质量", "不位移，只用于对撞分速度")]
        public float nestMass = 3f;
        [InspectorCn("房子半径", "房子碰撞半径")]
        public float nestR = 2.4f;
        [InspectorCn("散落卵数", "房子爆开散落的卵数")]
        public int nestEggN = 5;
        [InspectorCn("孵化时间", "卵孵化基准时间（秒）")]
        public float eggHatchT = 5f;
        [InspectorCn("孵化错开", "第 i 枚卵再加 i × 该值；0 = 只靠随机错开")]
        public float eggHatchGap = 0.28f;
        [InspectorCn("孵化抖动", "孵化时间随机抖动上限（秒）")]
        public float eggHatchJitter = 0.15f;
        [HideInInspector]
        public float eggScatterV = 8f;
        [InspectorCn("卵半径", "卵碰撞 / 出圈半径")]
        public float eggR = 0.55f;
        [InspectorCn("卵质量", "卵被踢时的质量")]
        public float eggMass = 0.2f;
        [InspectorCn("崽寿命", "秒；孵化起算，出圈也会死")]
        public float babyLifeT = 12f;
        [InspectorCn("崽半径倍率", "崽半径 = 开局半径 × 该值")]
        public float babyRScale = 0.4f;
        [InspectorCn("崽质量", "不吃饲主成长")]
        public float babyMass = 0.5f;
        [InspectorCn("崽蓄力速度倍率", "崽 A1 = 幼虫蓄力速度 × 该值")]
        public float babyA1Scale = 0.4f;
        [InspectorCn("崽蓄力时间", "崽自动蓄多久再跳（秒）")]
        public float babyChargeT = 0.8f;
        [InspectorCn("崽攻击间隔", "崽两次起跳最短间隔（秒）；0 = 落地即可再蓄")]
        public float babyAtkCd = 0.8f;
        [InspectorCn("崽能拾取", "崽能否吃饲料球 / 限时道具")]
        public bool babyCanLoot = false;
        [InspectorCn("场上巢上限", "整条链算 1 个")]
        public int nestCap = 1;
        [InspectorCn("首栋出现时间", "秒")]
        public float nestFirstT = 25f;
        [InspectorCn("下一栋间隔", "上一窝彻底结束后，下一栋再等的间隔（秒）")]
        public float nestGap = 12f;
    }

    /// <summary>
    /// 纯规则函数集合，不保存对局状态，保证主机、单机和测试复用同一套计算。
    /// </summary>
    public static class DouQuquRules
    {
        public const float DefaultArenaHalfWidth = 21.2f;
        public const float DefaultArenaHalfDepth = 31.8f;
        public const float DefaultArenaCorner = 7.2f;
        public static float ArenaHalfWidth = DefaultArenaHalfWidth;
        public static float ArenaHalfDepth = DefaultArenaHalfDepth;
        public static float ArenaCorner = DefaultArenaCorner;
        public static readonly string[] ItemKinds = { "shield", "charge" };
        public const int KillScoreBase = 10;

        public static void ResetArenaSize()
        {
            ArenaHalfWidth = DefaultArenaHalfWidth;
            ArenaHalfDepth = DefaultArenaHalfDepth;
            ArenaCorner = DefaultArenaCorner;
        }

        /// <summary>
        /// HUD 场地窗矩形控制出界：高度保持默认世界尺度，宽度跟矩形宽高比。
        /// </summary>
        public static void ApplyArenaFromRect(float width, float height)
        {
            float w = Mathf.Max(1f, width);
            float h = Mathf.Max(1f, height);
            ArenaHalfDepth = DefaultArenaHalfDepth;
            ArenaHalfWidth = DefaultArenaHalfDepth * (w / h);
            ArenaCorner = Mathf.Min(DefaultArenaCorner, ArenaHalfWidth * 0.95f, ArenaHalfDepth * 0.95f);
        }

        /// <summary>返回一份默认规则参数。</summary>
        public static MatchKnobs DefaultKnobs()
        {
            MatchKnobs knobs = new MatchKnobs();
            knobs.EnsureJumpKnobs();
            return knobs;
        }

        /// <summary>返回经济系统使用的默认参数，目前与完整默认参数相同。</summary>
        public static MatchKnobs DefaultItemKnobs()
        {
            return DefaultKnobs();
        }

        /// <summary>返回正赛和加时相加后的硬截止时间。</summary>
        public static float HardStop(MatchKnobs knobs)
        {
            return knobs.regTime + knobs.otTime;
        }

        /// <summary>判断给定时间是否处于加时狂暴阶段。</summary>
        public static bool IsRage(MatchKnobs knobs, float time)
        {
            return time >= knobs.regTime && time < HardStop(knobs);
        }

        /// <summary>
        /// HUD 倒计时秒数：正赛显示距加时，狂暴显示距硬截止。未开赛按正赛全长。
        /// </summary>
        public static float RemainingClock(MatchKnobs knobs, float time, bool started)
        {
            if (knobs == null) knobs = DefaultKnobs();
            if (!started) return Mathf.Max(0f, knobs.regTime);
            if (IsRage(knobs, time)) return Mathf.Max(0f, HardStop(knobs) - time);
            return Mathf.Max(0f, knobs.regTime - time);
        }

        /// <summary>与 HTML 原型相同：ceil 到整秒，再格式成 m:ss。</summary>
        public static string FormatClock(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (total / 60) + ":" + (total % 60).ToString("00");
        }

        /// <summary>根据时间返回当前玩法阶段。</summary>
        public static MatchPhase Phase(MatchKnobs knobs, float time)
        {
            if (time >= HardStop(knobs)) return MatchPhase.Over;
            if (time >= knobs.regTime) return MatchPhase.Rage;
            if (time >= 70f) return MatchPhase.Close;
            if (time >= knobs.heartOpenAt) return MatchPhase.Open;
            return MatchPhase.Probe;
        }

        /// <summary>判断蟋蟀是否处于增大效果中。</summary>
        public static bool SizeActive(BugState bug)
        {
            return bug.rageSize || bug.buffSizeT > 0f;
        }

        /// <summary>判断蟋蟀是否处于蓄力强化中。</summary>
        public static bool ChargeActive(BugState bug)
        {
            return bug != null && bug.buffChargeT > 0f;
        }

        /// <summary>判断蟋蟀的护盾是否仍有效。</summary>
        public static bool ShieldActive(BugState bug)
        {
            return bug.buffShieldT > 0f;
        }

        /// <summary>成长层倍率。半径、质量各乘一次；玩家跳跃乘在点跳距离 dMin 上。</summary>
        public static float GrowRate(MatchKnobs knobs, BugState bug)
        {
            if (bug == null) return 1f;
            return 1f + bug.grow * Mathf.Max(0f, knobs.growPer);
        }

        /// <summary>计算幼虫自身成长层对体型、质量和蓄力速度产生的倍率。</summary>
        public static float GrowRate(MatchKnobs knobs, BabyState baby)
        {
            if (baby == null) return 1f;
            return 1f + baby.grow * Mathf.Max(0f, knobs.growPer);
        }

        /// <summary>按开局体型、成长和临时增大刷新碰撞半径与质量。</summary>
        public static void RefreshBody(MatchKnobs knobs, BugState bug)
        {
            float g = GrowRate(knobs, bug);
            float size = SizeActive(bug) ? knobs.sizeScale : 1f;
            float massMul = Mathf.Max(0.01f, bug.massMul);
            float sizeMul = Mathf.Sqrt(massMul);
            bug.radius = knobs.bugR * sizeMul * g * size;
            bug.mass = Mathf.Max(0.08f, knobs.mass) * massMul * g * size;
        }

        /// <summary>按成长和临时增大效果刷新幼虫碰撞半径与质量。</summary>
        public static void RefreshBabyBody(MatchKnobs knobs, BabyState baby)
        {
            if (baby == null) return;
            float g = GrowRate(knobs, baby);
            float size = baby.buffSizeT > 0f ? knobs.sizeScale : 1f;
            baby.radius = knobs.bugR * Mathf.Max(0f, knobs.babyRScale) * g * size;
            baby.mass = Mathf.Max(0.05f, knobs.babyMass) * g * size;
        }

        static float StatMul(float value)
        {
            return Mathf.Max(0.01f, value);
        }

        /// <summary>返回当前增益下的蟋蟀蓄力速度。</summary>
        public static float EffectiveChargeSpeed(MatchKnobs knobs, BugState bug)
        {
            float mul = bug == null ? 1f : StatMul(bug.chargeSpeedMul);
            float rate = knobs.vRate * mul * GrowRate(knobs, bug);
            if (ChargeActive(bug)) rate *= knobs.chargeScale;
            if (bug != null && bug.rageCharge) rate *= Mathf.Max(0.01f, knobs.rageBoost);
            return rate;
        }

        /// <summary>返回当前增益下蓄力条的最大持续时间。</summary>
        public static float EffectiveChargeTime(MatchKnobs knobs, BugState bug)
        {
            float scale = ChargeActive(bug) ? knobs.chargeScale : 1f;
            float mul = bug == null ? 1f : StatMul(bug.chargeTimeMul);
            return knobs.tChargeMax * mul / Mathf.Max(0.01f, scale);
        }

        /// <summary>将蓄力时间换算成当前蟋蟀的冲撞速度增量。</summary>
        public static float ChargeDelta(MatchKnobs knobs, BugState bug)
        {
            return EffectiveChargeSpeed(knobs, bug) * Mathf.Clamp(bug.chargeTime, 0f, EffectiveChargeTime(knobs, bug));
        }

        /// <summary>蓄满一次 T_max 的蓄力耐力。</summary>
        public static float StaminaFullCost(MatchKnobs knobs)
        {
            return Mathf.Max(0f, knobs.staminaCost);
        }

        /// <summary>每次有效起跳固定加扣的起跳耐力。</summary>
        public static float StaminaJumpCost(MatchKnobs knobs)
        {
            return Mathf.Max(0f, knobs.staminaJump);
        }

        /// <summary>当前蓄力相对有效 T_max 的比例，满蓄为 1。</summary>
        public static float ChargeProgress(MatchKnobs knobs, BugState bug)
        {
            if (bug == null) return 0f;
            float tMax = Mathf.Max(0.000001f, EffectiveChargeTime(knobs, bug));
            return Mathf.Clamp01(bug.chargeTime / tMax);
        }

        /// <summary>本次起跳耐力消耗 = 蓄力耐力 + 起跳耐力。</summary>
        public static float JumpStaminaCost(MatchKnobs knobs, BugState bug)
        {
            return StaminaFullCost(knobs) * ChargeProgress(knobs, bug) + StaminaJumpCost(knobs);
        }

        /// <summary>当前耐力能负担的最长蓄力时间。先留起跳耐力，剩下的才拿去蓄。</summary>
        public static float StaminaChargeTCap(MatchKnobs knobs, BugState bug)
        {
            float tMax = EffectiveChargeTime(knobs, bug);
            float full = StaminaFullCost(knobs);
            if (full <= 0.000001f) return tMax;
            float stamina = bug == null ? 0f : bug.stamina;
            return tMax * Mathf.Clamp01((stamina - StaminaJumpCost(knobs)) / full);
        }

        /// <summary>耐力不少于起跳耐力才能进入蓄力。tChargeMin&gt;0 时还要能蓄到取消线。小蟋蟀不读耐力。</summary>
        public static bool CanStartCharge(MatchKnobs knobs, BugState bug)
        {
            if (bug == null) return false;
            if (bug.stamina + 0.000001f < StaminaJumpCost(knobs)) return false;
            if (knobs.tChargeMin > 0.000001f && StaminaChargeTCap(knobs, bug) + 0.000001f < knobs.tChargeMin)
                return false;
            return true;
        }

        /// <summary>这只虫的耐力上限 = 面板 staminaMax × 耐力上限详情值。</summary>
        public static float StaminaMaxOf(MatchKnobs knobs, BugState bug)
        {
            float mul = bug == null ? 1f : StatMul(bug.staminaMaxMul);
            return Mathf.Max(0f, knobs.staminaMax) * mul;
        }

        /// <summary>这只虫的抓地摩擦 = 面板 μ × 抓地力详情值。</summary>
        public static float GripOf(MatchKnobs knobs, BugState bug)
        {
            float mul = bug == null ? 1f : StatMul(bug.gripMul);
            return Mathf.Max(0.0001f, knobs.mu) * mul;
        }

        /// <summary>水平速度低于此视为 0。不是旧的 0.06 停稳死区。</summary>
        public const float SettleSnap = 1e-4f;

        public static Vector3 Planar(Vector3 velocity)
        {
            return new Vector3(velocity.x, 0f, velocity.z);
        }

        public static bool IsPlanarSettled(Vector3 velocity)
        {
            return new Vector2(velocity.x, velocity.z).sqrMagnitude <= SettleSnap * SettleSnap;
        }

        public static Vector3 LaunchOf(BugState bug)
        {
            if (bug == null) return Vector3.zero;
            Vector3 launch = Planar(bug.launchVelocity);
            if (launch.sqrMagnitude > SettleSnap * SettleSnap) return launch;
            if (bug.initialSpeed <= SettleSnap) return Vector3.zero;
            Vector2 dir = new Vector2(bug.velocity.x, bug.velocity.z);
            if (dir.sqrMagnitude < SettleSnap * SettleSnap) dir = bug.chargeDirection;
            if (dir.sqrMagnitude < SettleSnap * SettleSnap) return Vector3.zero;
            dir.Normalize();
            return new Vector3(dir.x * bug.initialSpeed, 0f, dir.y * bug.initialSpeed);
        }

        public static Vector3 LaunchOf(BabyState baby)
        {
            if (baby == null) return Vector3.zero;
            Vector3 launch = Planar(baby.launchVelocity);
            if (launch.sqrMagnitude > SettleSnap * SettleSnap) return launch;
            if (baby.initialSpeed <= SettleSnap) return Vector3.zero;
            Vector2 dir = new Vector2(baby.velocity.x, baby.velocity.z);
            if (dir.sqrMagnitude < SettleSnap * SettleSnap) dir = baby.chargeDirection;
            if (dir.sqrMagnitude < SettleSnap * SettleSnap) return Vector3.zero;
            dir.Normalize();
            return new Vector3(dir.x * baby.initialSpeed, 0f, dir.y * baby.initialSpeed);
        }

        public static void SetLaunch(BugState bug, Vector3 launch)
        {
            if (bug == null) return;
            launch = Planar(launch);
            bug.launchVelocity = launch;
            bug.initialSpeed = new Vector2(launch.x, launch.z).magnitude;
        }

        public static void SetLaunch(BabyState baby, Vector3 launch)
        {
            if (baby == null) return;
            launch = Planar(launch);
            baby.launchVelocity = launch;
            baby.initialSpeed = new Vector2(launch.x, launch.z).magnitude;
        }

        public static void ClearLaunch(BugState bug)
        {
            if (bug == null) return;
            bug.launchVelocity = Vector3.zero;
            bug.initialSpeed = 0f;
        }

        public static void ClearLaunch(BabyState baby)
        {
            if (baby == null) return;
            baby.launchVelocity = Vector3.zero;
            baby.initialSpeed = 0f;
        }

        public static HitTier CanonicalHitTier(HitTier tier)
        {
            return tier == HitTier.Normal ? HitTier.Control : tier;
        }

        public static bool IsHitSliding(HitTier tier)
        {
            HitTier canonical = CanonicalHitTier(tier);
            return canonical == HitTier.Control || canonical == HitTier.Slip;
        }

        /// <summary>撞后滑行中再撞：这次计算改用当前速度当出发速度。</summary>
        public static void UseCurrentAsLaunchIfHitSliding(BugState bug)
        {
            if (bug == null || !IsHitSliding(bug.hitTier)) return;
            SetLaunch(bug, bug.velocity);
        }

        public static void UseCurrentAsLaunchIfHitSliding(BabyState baby)
        {
            if (baby == null || !IsHitSliding(baby.hitTier)) return;
            SetLaunch(baby, baby.velocity);
        }

        /// <summary>朝对方为正的出发法向速度。不到 0 当 0，只给抵抗和动量用。</summary>
        public static float LaunchTowardClamped(Vector3 launch, Vector3 normalToOther)
        {
            return Mathf.Max(0f, Vector3.Dot(Planar(launch), normalToOther));
        }

        /// <summary>R = K m v，v 是出发速度朝对方的法向分量。</summary>
        public static float ResistanceOf(MatchKnobs knobs, float mass, Vector3 launch, Vector3 normalToOther)
        {
            float k = knobs == null ? 1f : Mathf.Max(0f, knobs.resistK);
            return k * Mathf.Max(0.01f, mass) * LaunchTowardClamped(launch, normalToOther);
        }

        /// <summary>
        /// 可控 / 失衡。已失衡则保持失衡。谁更快比出发速度朝对方的分量（不取绝对值）；
        /// 更慢再拿 Δp 对 R。
        /// </summary>
        public static HitTier HitTierFor(MatchKnobs knobs, HitTier current, float mass, Vector3 launch, float otherMass, Vector3 otherLaunch, Vector3 normalToOther)
        {
            if (CanonicalHitTier(current) == HitTier.Slip) return HitTier.Slip;
            float meToward = Vector3.Dot(Planar(launch), normalToOther);
            float otherToward = Vector3.Dot(Planar(otherLaunch), -normalToOther);
            if (meToward + 1e-9f >= otherToward) return HitTier.Control;
            float deltaP = Mathf.Max(0.01f, otherMass) * Mathf.Max(0f, otherToward)
                - Mathf.Max(0.01f, mass) * Mathf.Max(0f, meToward);
            return deltaP > ResistanceOf(knobs, mass, launch, normalToOther) ? HitTier.Slip : HitTier.Control;
        }

        public static float SlideMuFor(MatchKnobs knobs, BugState bug, HitTier tier)
        {
            float mu = GripOf(knobs, bug);
            if (CanonicalHitTier(tier) == HitTier.Slip) return mu * Mathf.Clamp(knobs.muSlipScale, 0.3f, 1f);
            return mu;
        }

        public static float SlideMuFor(MatchKnobs knobs, HitTier tier)
        {
            float mu = Mathf.Max(0.0001f, knobs.mu);
            if (CanonicalHitTier(tier) == HitTier.Slip) return mu * Mathf.Clamp(knobs.muSlipScale, 0.3f, 1f);
            return mu;
        }

        /// <summary>落地恢复耐力，含滑行；空中不恢复；蓄力中乘 staminaRegenCharge。</summary>
        public static void TickStamina(MatchKnobs knobs, BugState bug, float dt)
        {
            if (bug == null || !bug.alive || bug.airborne) return;
            float max = StaminaMaxOf(knobs, bug);
            float regen = Mathf.Max(0f, knobs.staminaRegen) * StatMul(bug.staminaRegenMul);
            if (bug.rageCharge) regen *= Mathf.Max(0.01f, knobs.rageBoost);
            if (bug.charging) regen *= Mathf.Max(0f, knobs.staminaRegenCharge);
            bug.stamina = Mathf.Clamp(bug.stamina + regen * dt, 0f, max);
        }

        /// <summary>满蓄水平总位移 / 点跳水平总位移。小于 1 当作 3。</summary>
        public static float JumpDistRatio(MatchKnobs knobs)
        {
            float r = knobs == null ? 3f : knobs.jumpDistRatio;
            return r > 1f ? r : 3f;
        }

        /// <summary>点跳水平总位移。品质和成长都直接乘距离。</summary>
        public static float DMinOf(MatchKnobs knobs, BugState bug = null)
        {
            float d = Mathf.Max(0f, knobs.dMin);
            float mul = bug == null ? 1f : StatMul(bug.dMinMul);
            return d * mul * GrowRate(knobs, bug);
        }

        /// <summary>把水平总位移反推成出手速度。D = v²/g × (2tanθ + 1/(2μ))。</summary>
        public static float JumpSpeedFromDistance(MatchKnobs knobs, float distance)
        {
            float g = Gravity(knobs);
            float tangent = TanTheta(knobs);
            float mu = Mathf.Max(0.0001f, knobs.mu);
            float coeff = 2f * tangent + 1f / (2f * mu);
            return Mathf.Sqrt(Mathf.Max(0f, distance) * g / Mathf.Max(1e-6f, coeff));
        }

        /// <summary>未强化满蓄水平速度，对应距离 dMin × R。</summary>
        public static float PanelVMax(MatchKnobs knobs, BugState bug = null)
        {
            return JumpSpeedFromDistance(knobs, DMinOf(knobs, bug)) * Mathf.Sqrt(JumpDistRatio(knobs));
        }

        /// <summary>返回带下限保护的重力值。</summary>
        public static float Gravity(MatchKnobs knobs)
        {
            return Mathf.Max(0.01f, knobs.gravity);
        }

        /// <summary>返回斜坡角度的正切值，用于计算垂直起跳速度。</summary>
        public static float TanTheta(MatchKnobs knobs)
        {
            return Mathf.Tan(knobs.theta * Mathf.Deg2Rad);
        }

        /// <summary>把摩擦系数换算成平面减速度。</summary>
        public static float FrictionAcceleration(MatchKnobs knobs, float friction = -1f)
        {
            float mu = friction < 0f ? knobs.mu : friction;
            return Mathf.Max(0.0001f, mu * Gravity(knobs));
        }

        /// <summary>点跳水平速度。蓄力强化把点跳距离乘 s²，速度乘 s。</summary>
        public static float JumpSpeedMin(MatchKnobs knobs, BugState bug = null)
        {
            float s = ChargeActive(bug) ? Mathf.Max(0.01f, knobs.chargeScale) : 1f;
            return JumpSpeedFromDistance(knobs, DMinOf(knobs, bug)) * s;
        }

        /// <summary>出手水平速度。距离对蓄力进度线性；满蓄钉在未强化的 dMin × R。成长已含在 dMin 里。</summary>
        public static float JumpDeltaV(MatchKnobs knobs, BugState bug, float chargeTime)
        {
            float tMax = Mathf.Max(0.000001f, EffectiveChargeTime(knobs, bug));
            float p = Mathf.Clamp(chargeTime, 0f, tMax) / tMax;
            float s = ChargeActive(bug) ? Mathf.Max(0.01f, knobs.chargeScale) : 1f;
            float r = JumpDistRatio(knobs);
            float rEff = Mathf.Max(1f, r / (s * s));
            float v0 = JumpSpeedFromDistance(knobs, DMinOf(knobs, bug));
            return v0 * s * Mathf.Sqrt(1f + p * (rEff - 1f));
        }

        /// <summary>用当前蓄力时间算出手速度。</summary>
        public static float JumpDeltaV(MatchKnobs knobs, BugState bug)
        {
            return JumpDeltaV(knobs, bug, bug == null ? 0f : bug.chargeTime);
        }

        /// <summary>返回幼虫一次攻击的蓄力时长下限保护值。</summary>
        public static float BabyChargeTime(MatchKnobs knobs)
        {
            return Mathf.Max(0.02f, knobs.babyChargeT);
        }

        /// <summary>根据幼虫蓄力时间计算其冲撞速度。</summary>
        public static float BabyChargeSpeed(MatchKnobs knobs, BabyState baby)
        {
            float rate = Mathf.Max(0f, knobs.vRate) * Mathf.Max(0f, knobs.babyA1Scale) * GrowRate(knobs, baby);
            return rate * Mathf.Clamp(baby == null ? 0f : baby.chargeTime, 0f, BabyChargeTime(knobs));
        }

        /// <summary>返回幼虫起跳速度；当前规则与蓄力速度相同。</summary>
        public static float BabyJumpSpeed(MatchKnobs knobs, BabyState baby)
        {
            return BabyChargeSpeed(knobs, baby);
        }

        /// <summary>返回幼虫攻击后的冷却时长。</summary>
        public static float BabyAttackCooldown(MatchKnobs knobs)
        {
            return Mathf.Max(0f, knobs.babyAtkCd);
        }

        /// <summary>判断幼虫是否可以拾取场上的资源。</summary>
        public static bool BabyCanLoot(MatchKnobs knobs)
        {
            return knobs != null && knobs.babyCanLoot;
        }

        /// <summary>返回幼虫蓄力速度和蓄力时长，供面板或调试信息展示。</summary>
        public static float[] BabyChargeStats(MatchKnobs knobs, BabyState baby = null)
        {
            float rate = Mathf.Max(0f, knobs.vRate) * Mathf.Max(0f, knobs.babyA1Scale) * GrowRate(knobs, baby);
            return new[] { rate, BabyChargeTime(knobs) };
        }

        /// <summary>返回幼虫本次蓄力产生的速度增量。</summary>
        public static float BabyChargeDelta(MatchKnobs knobs, BabyState baby)
        {
            return BabyChargeSpeed(knobs, baby);
        }

        /// <summary>兼容旧接口，返回幼虫攻击冷却。</summary>
        public static float BabyAttackCd(MatchKnobs knobs)
        {
            return BabyAttackCooldown(knobs);
        }

        /// <summary>递减幼虫攻击冷却并限制为非负数。</summary>
        public static void TickBabyAttackCooldown(BabyState baby, float dt)
        {
            if (baby != null) baby.attackCooldown = Mathf.Max(0f, baby.attackCooldown - dt);
        }

        /// <summary>判断幼虫当前是否可以开始蓄力。</summary>
        public static bool CanBabyCharge(BabyState baby)
        {
            return baby == null || baby.attackCooldown <= 1e-6f;
        }

        /// <summary>估算给定起跳速度在空中和地面阶段的总滑行距离。</summary>
        public static float JumpRange(MatchKnobs knobs, float speed)
        {
            float g = Gravity(knobs);
            float tangent = TanTheta(knobs);
            float mu = Mathf.Max(0.0001f, knobs.mu);
            float air = 2f * speed * speed * tangent / g;
            float ground = speed * speed / (2f * mu * g);
            return air + ground;
        }

        /// <summary>将一种道具效果应用到蟋蟀，并刷新受影响的体型。</summary>
        public static void ApplyItem(MatchKnobs knobs, BugState bug, string kind)
        {
            if (kind == "size")
            {
                if (!bug.rageSize) bug.buffSizeT = knobs.sizeT;
                RefreshBody(knobs, bug);
            }
            else if (kind == "shield")
            {
                bug.buffShieldT = knobs.shieldT;
            }
            else if (kind == "charge" && !bug.rageCharge)
            {
                bug.buffChargeT = knobs.chargeBuffT;
            }
        }

        /// <summary>将一种道具效果应用到幼虫，并刷新受影响的体型。</summary>
        public static void ApplyItem(MatchKnobs knobs, BabyState baby, string kind)
        {
            if (baby == null) return;
            if (kind == "size")
            {
                baby.buffSizeT = knobs.sizeT;
                RefreshBabyBody(knobs, baby);
            }
            else if (kind == "shield") baby.buffShieldT = knobs.shieldT;
            else if (kind == "charge") baby.buffChargeT = knobs.chargeBuffT;
        }

        /// <summary>递减蟋蟀的限时增益，并在增大结束时恢复体型。</summary>
        public static void TickBuffs(MatchKnobs knobs, BugState bug, float dt)
        {
            bool refresh = false;
            if (!bug.rageSize && bug.buffSizeT > 0f)
            {
                bug.buffSizeT -= dt;
                if (bug.buffSizeT <= 0f)
                {
                    bug.buffSizeT = 0f;
                    refresh = true;
                }
            }
            if (bug.buffShieldT > 0f)
                bug.buffShieldT = Mathf.Max(0f, bug.buffShieldT - dt);
            if (!bug.rageCharge && bug.buffChargeT > 0f)
                bug.buffChargeT = Mathf.Max(0f, bug.buffChargeT - dt);
            if (refresh) RefreshBody(knobs, bug);
        }

        /// <summary>递减幼虫的限时增益，并在增大结束时恢复体型。</summary>
        public static void TickBuffs(MatchKnobs knobs, BabyState baby, float dt)
        {
            if (baby == null) return;
            if (baby.buffSizeT > 0f)
            {
                baby.buffSizeT = Mathf.Max(0f, baby.buffSizeT - dt);
                if (baby.buffSizeT <= 0f) RefreshBabyBody(knobs, baby);
            }
            baby.buffShieldT = Mathf.Max(0f, baby.buffShieldT - dt);
            baby.buffChargeT = Mathf.Max(0f, baby.buffChargeT - dt);
        }

        /// <summary>进入加时狂暴：全员蓄力速度与耐力恢复同乘 rageBoost，体型不变。</summary>
        public static void EnterRage(MatchKnobs knobs, BugState[] bugs)
        {
            for (int i = 0; i < bugs.Length; i++)
            {
                BugState bug = bugs[i];
                if (!bug.alive) continue;
                bool wasBig = bug.rageSize;
                bug.rageSize = false;
                bug.rageCharge = true;
                if (wasBig) RefreshBody(knobs, bug);
            }
        }

        /// <summary>选择与上一种不同的道具类型，并更新类型游标。</summary>
        public static string PickItemKind(ref string lastKind, float roll)
        {
            int selected = 0;
            if (lastKind == "shield") selected = 1;
            else if (lastKind == "charge") selected = 0;
            else selected = Mathf.Clamp(Mathf.FloorToInt(roll * ItemKinds.Length), 0, ItemKinds.Length - 1);
            lastKind = ItemKinds[selected];
            return lastKind;
        }

        /// <summary>按旧版调用方式判断是否应补充饲料球。</summary>
        public static bool ShouldRefillHeart(MatchKnobs knobs, float time, float lastHeartAt, int liveCount)
        {
            if (time < knobs.heartOpenAt || liveCount >= knobs.heartCap) return false;
            if (lastHeartAt < 0f) return true;
            float gap = IsRage(knobs, time) ? knobs.heartGapOt : knobs.heartGap;
            return time - lastHeartAt >= gap;
        }

        /// <summary>返回场上允许同时存在的巢穴链数量上限。</summary>
        public static int NestFieldCap(MatchState state)
        {
            return Mathf.Max(1, state == null || state.knobs == null ? 1 : state.knobs.nestCap);
        }

        /// <summary>判断当前时间是否满足生成新巢穴的条件。</summary>
        public static bool ShouldSpawnNest(MatchState state, int liveCount)
        {
            if (state == null || IsRage(state.knobs, state.elapsed)) return false;
            if (state.elapsed + 1e-9f < state.knobs.nestFirstT || liveCount >= NestFieldCap(state)) return false;
            if (state.lastNestClearAt < 0f) return true;
            return state.elapsed - state.lastNestClearAt + 1e-9f >= state.knobs.nestGap;
        }

        /// <summary>记录巢穴链清空的时间，用于计算下一次生成间隔。</summary>
        public static void MarkNestCleared(MatchState state)
        {
            if (state != null) state.lastNestClearAt = state.elapsed;
        }

        /// <summary>判断开放阶段首次补球是否应立即执行。</summary>
        public static bool HeartBurstFill(MatchState state)
        {
            return state != null && state.lastHeartAt < 0f && state.elapsed + 1e-9f >= state.knobs.heartOpenAt;
        }

        /// <summary>返回场上饲料球数量上限。</summary>
        public static int HeartFieldCap(MatchState state)
        {
            return Mathf.Max(0, state == null || state.knobs == null ? 0 : state.knobs.heartCap);
        }

        /// <summary>返回当前补球波次可生成的数量。</summary>
        public static int HeartWaveSize(MatchState state, int fieldCount)
        {
            return Mathf.Max(0, HeartFieldCap(state) - Mathf.Max(0, fieldCount)) > 0 ? 1 : 0;
        }

        /// <summary>按当前状态判断是否应补充一颗饲料球。</summary>
        public static bool ShouldRefillHeart(MatchState state, int fieldCount)
        {
            if (state == null || state.elapsed + 1e-9f < state.knobs.heartOpenAt || HeartWaveSize(state, fieldCount) <= 0) return false;
            if (HeartBurstFill(state)) return true;
            float gap = IsRage(state.knobs, state.elapsed) ? state.knobs.heartGapOt : state.knobs.heartGap;
            return state.elapsed - state.lastHeartAt + 1e-9f >= gap;
        }

        /// <summary>记录最近一次补充饲料球的时间。</summary>
        public static void MarkHeartFilled(MatchState state)
        {
            if (state != null) state.lastHeartAt = state.elapsed;
        }

        /// <summary>
        /// 消耗已经到点的道具时间表；返回本次需要生成的类型列表，且不超过场上上限。
        /// </summary>
        public static List<string> DueItemSpawns(MatchKnobs knobs, float time, ref int nextIndex, ref string lastKind, int fieldCount, System.Random random)
        {
            float[] times = { 20f, 42f, 60f, 74f, 85f, 94f, 102f, 110f };
            List<string> result = new List<string>();
            int cap = Mathf.Max(0, knobs.itemCap);
            while (nextIndex < times.Length && time >= times[nextIndex] && fieldCount + result.Count < cap)
            {
                int room = cap - fieldCount - result.Count;
                int count = Mathf.Min(Mathf.Max(1, knobs.itemBatch), room);
                for (int i = 0; i < count; i++)
                    result.Add(PickItemKind(ref lastKind, random == null ? UnityEngine.Random.value : (float)random.NextDouble()));
                nextIndex++;
            }
            return result;
        }

        /// <summary>在场地内寻找避开角色和巢穴的随机位置。</summary>
        public static Vector3 PlacePoint(System.Random random, IReadOnlyList<BugState> bugs, Vector3? nestPosition, float margin)
        {
            for (int attempt = 0; attempt < 80; attempt++)
            {
                float x = NextFloat(random, -ArenaHalfWidth + margin, ArenaHalfWidth - margin);
                float z = NextFloat(random, -ArenaHalfDepth + margin, ArenaHalfDepth - margin);
                Vector3 point = new Vector3(x, 0f, z);
                if (ArenaSdf(point.x, point.z) > -margin) continue;
                bool blocked = false;
                if (bugs != null)
                    for (int i = 0; i < bugs.Count; i++)
                        if (bugs[i] != null && bugs[i].alive && new Vector2(bugs[i].position.x - x, bugs[i].position.z - z).magnitude < bugs[i].radius + 2f) blocked = true;
                if (nestPosition.HasValue && new Vector2(nestPosition.Value.x - x, nestPosition.Value.z - z).magnitude < margin + 1.5f) blocked = true;
                if (!blocked) return point;
            }
            return Vector3.zero;
        }

        /// <summary>生成指定数量、均匀随机方向的蛋初速度。</summary>
        public static List<Vector3> ScatterEggs(int count, System.Random random, float speed)
        {
            List<Vector3> eggs = new List<Vector3>();
            for (int i = 0; i < Mathf.Max(0, count); i++)
            {
                float angle = NextFloat(random, 0f, Mathf.PI * 2f);
                eggs.Add(new Vector3(Mathf.Cos(angle) * speed, 0f, Mathf.Sin(angle) * speed));
            }
            return eggs;
        }

        /// <summary>生成带间隔和抖动并打乱顺序的孵化时间。</summary>
        public static float[] EggHatchTimes(int count, MatchKnobs knobs, System.Random random)
        {
            float[] values = new float[Mathf.Max(0, count)];
            for (int i = 0; i < values.Length; i++)
                values[i] = knobs.eggHatchT + i * Mathf.Max(0f, knobs.eggHatchGap) + NextFloat(random, 0f, Mathf.Max(0f, knobs.eggHatchJitter));
            for (int i = values.Length - 1; i > 0; i--)
            {
                int j = random == null ? UnityEngine.Random.Range(0, i + 1) : random.Next(i + 1);
                float temp = values[i]; values[i] = values[j]; values[j] = temp;
            }
            return values;
        }

        private static float NextFloat(System.Random random, float min, float max)
        {
            return min + (float)(random == null ? UnityEngine.Random.value : random.NextDouble()) * (max - min);
        }

        /// <summary>返回蟋蟀命中时的击杀归属 ID。</summary>
        public static int HitCreditId(BugState hitter)
        {
            return hitter == null ? -1 : hitter.id;
        }

        /// <summary>幼虫命中时将击杀归属记到其拥有者。</summary>
        public static int HitCreditId(BabyState hitter)
        {
            return hitter == null ? -1 : hitter.ownerId;
        }

        /// <summary>判断本次巢穴接触是否是新的、且确实向巢穴接近的撞击。</summary>
        public static bool IsNewNestContact(bool wasTouching, float normalVelocity)
        {
            return !wasTouching && normalVelocity < -0.0001f;
        }

        /// <summary>按法向速度顺序消耗巢穴耐久，并在最后一击时确定归属。</summary>
        public static NestHitResult ResolveNestHits(float hp, List<NestHit> hits)
        {
            int remaining = Mathf.Max(0, Mathf.RoundToInt(hp));
            if (remaining <= 0 || hits == null || hits.Count == 0)
                return new NestHitResult(remaining, -1, false);
            hits.Sort((a, b) => a.normalVelocity.CompareTo(b.normalVelocity) != 0
                ? a.normalVelocity.CompareTo(b.normalVelocity) : a.playerId.CompareTo(b.playerId));
            int owner = -1;
            bool exploded = false;
            for (int i = 0; i < hits.Count && remaining > 0; i++)
            {
                remaining--;
                if (remaining == 0)
                {
                    exploded = true;
                    float velocity = hits[i].normalVelocity;
                    bool tie = false;
                    for (int j = 0; j < hits.Count; j++)
                        if (j != i && Mathf.Abs(hits[j].normalVelocity - velocity) <= 1e-5f) { tie = true; break; }
                    owner = tie ? -1 : hits[i].playerId;
                }
            }
            return new NestHitResult(remaining, owner, exploded);
        }

        /// <summary>一次巢穴撞击的归属和法向速度。</summary>
        public readonly struct NestHit
        {
            public readonly int playerId;
            public readonly float normalVelocity;
            public NestHit(int id, float velocity) { playerId = id; normalVelocity = velocity; }
        }

        /// <summary>巢穴批量受击后的剩余耐久、归属和爆裂结果。</summary>
        public readonly struct NestHitResult
        {
            public readonly int hp;
            public readonly int ownerId;
            public readonly bool exploded;
            public NestHitResult(int remaining, int owner, bool didExplode) { hp = remaining; ownerId = owner; exploded = didExplode; }
        }

        /// <summary>多人同时出局时按离中心距离和成长层数决出胜者。</summary>
        public static int CenterWinner(BugState[] bugs)
        {
            int winner = -1;
            float bestDistance = float.MaxValue;
            float bestGrow = float.MinValue;
            bool tie = false;
            for (int i = 0; i < bugs.Length; i++)
            {
                BugState bug = bugs[i];
                if (!bug.alive) continue;
                float distance = new Vector2(bug.position.x, bug.position.z).magnitude;
                if (distance < bestDistance - 0.0001f || (Mathf.Abs(distance - bestDistance) <= 0.0001f && bug.grow > bestGrow))
                {
                    winner = bug.id;
                    bestDistance = distance;
                    bestGrow = bug.grow;
                    tie = false;
                }
                else if (Mathf.Abs(distance - bestDistance) <= 0.0001f && Mathf.Abs(bug.grow - bestGrow) <= 0.0001f)
                {
                    tie = true;
                }
            }
            return tie ? -1 : winner;
        }

        public static Vector3 ClampInsideArena(Vector3 p, float pad = 0f)
        {
            // 使用与原型相同的圆角矩形 SDF 将点投回场内，保留圆角而不是退化成轴对齐矩形。
            for (int i = 0; i < 12; i++)
            {
                float sdf = ArenaSdf(p.x, p.z);
                if (sdf <= -pad) break;
                Vector2 gradient = ArenaGradient(p.x, p.z);
                float length = gradient.magnitude;
                if (length < 0.0001f) break;
                float step = Mathf.Max(0.04f, sdf + pad);
                p.x -= gradient.x / length * step;
                p.z -= gradient.y / length * step;
            }
            p.x = Mathf.Clamp(p.x, -ArenaHalfWidth + pad, ArenaHalfWidth - pad);
            p.z = Mathf.Clamp(p.z, -ArenaHalfDepth + pad, ArenaHalfDepth - pad);
            p.y = 0f;
            return p;
        }

        public static bool InsideArena(Vector3 p, float pad = 0f)
        {
            return ArenaSdf(p.x, p.z) <= -pad;
        }

        /// <summary>圆角矩形场地的有符号距离（场内为负值）。</summary>
        public static float ArenaSdf(float x, float z)
        {
            float qx = Mathf.Abs(x) - (ArenaHalfWidth - ArenaCorner);
            float qz = Mathf.Abs(z) - (ArenaHalfDepth - ArenaCorner);
            float ox = Mathf.Max(qx, 0f);
            float oz = Mathf.Max(qz, 0f);
            float outside = Mathf.Sqrt(ox * ox + oz * oz);
            float inside = Mathf.Min(Mathf.Max(qx, qz), 0f);
            return outside + inside - ArenaCorner;
        }

        /// <summary>用有限差分计算场地 SDF 的外法线方向。</summary>
        public static Vector2 ArenaGradient(float x, float z)
        {
            const float epsilon = 0.08f;
            float dx = (ArenaSdf(x + epsilon, z) - ArenaSdf(x - epsilon, z)) / (2f * epsilon);
            float dz = (ArenaSdf(x, z + epsilon) - ArenaSdf(x, z - epsilon)) / (2f * epsilon);
            return new Vector2(dx, dz);
        }

        /// <summary>角色出圈时应用原型中的护盾救援投影。</summary>
        public static bool TryShieldSave(MatchKnobs knobs, BugState bug)
        {
            if (bug == null || InsideArena(bug.position)) return false;
            if (!ShieldActive(bug)) return false;
            bug.buffShieldT = 0f;
            float pad = bug.radius + Mathf.Max(0f, knobs.shieldPad);
            bug.position = ClampInsideArena(bug.position, pad);
            return true;
        }
    }

    [Serializable]
    public sealed class BugState
    {
        public int id;
        public int catalogId;
        public bool alive = true;
        public Vector3 position;
        public Vector3 previousPosition;
        public Vector3 velocity;
        public float verticalVelocity;
        public float height;
        public Vector3 launchVelocity;
        public float initialSpeed;
        public float slideMu;
        public float chargeTime;
        public float stamina;
        public Vector2 chargeDirection = Vector2.up;
        public float radius;
        public float mass;
        public float massMul = 1f;
        public float gripMul = 1f;
        public float chargeSpeedMul = 1f;
        public float chargeTimeMul = 1f;
        public float staminaRegenMul = 1f;
        public float staminaMaxMul = 1f;
        public float tFloorMul = 1f;
        public int quality;
        public int temperament;
        public float dMinMul = 1f;
        public int grow;
        public int lastHitId = -1;
        public HitTier hitTier = HitTier.None;
        public float buffSizeT;
        public float buffShieldT;
        public float buffChargeT;
        public bool rageSize;
        public bool rageCharge;
        public bool charging;
        public bool holding;
        public bool pendingCharge;
        public bool airborne;
        public int score;

        public BugState(int playerId, Vector3 spawn, MatchKnobs knobs)
        {
            id = playerId;
            position = spawn;
            previousPosition = spawn;
            stamina = knobs == null ? 5f : Mathf.Max(0f, knobs.staminaMax);
            DouQuquRules.RefreshBody(knobs, this);
        }
    }

    [Serializable]
    public sealed class PickupState
    {
        public int id;
        public Vector3 position;
        public bool alive = true;
        public string kind;

        public PickupState(int pickupId, Vector3 at, string pickupKind)
        {
            id = pickupId;
            position = at;
            kind = pickupKind;
        }
    }

    [Serializable]
    public sealed class EggState
    {
        public Vector3 position;
        public Vector3 previousPosition;
        public Vector3 velocity;
        public int ownerId = -1;
        public float hatchAt;
        public bool alive = true;
    }
}
