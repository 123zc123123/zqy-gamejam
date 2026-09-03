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
    /// 对局规则参数。字段直接暴露给 Unity Inspector，便于 Demo 调整数值；
    /// 计算时会在规则函数内部做必要的下限保护。
    /// </summary>
    public sealed class MatchKnobs
    {
        // 蓄力、碰撞和移动参数。
        [FormerlySerializedAs("tMin")]
        public float tChargeMin = 0f; // 蓄力下限（秒）；未满松手取消；0 = 点一下就跳
        [FormerlySerializedAs("tMax")]
        public float tChargeMax = 0.5f; // 蓄满时间（秒）；蓄满后可继续按，速度不再涨
        public float tFloor = 0.12f; // 每次有效起跳额外加算的时间；点跳距离由它反推
        public float staminaMax = 5f; // 耐力上限；开局满；不参与跳跃和碰撞公式
        public float staminaCost = 0.8f; // 蓄满时的蓄力耐力；实际扣 = 该值 × 蓄力比例
        public float staminaJump = 0.2f; // 每次有效起跳固定加扣；点跳只扣这一笔
        public float staminaRegen = 0.48f; // 落地未蓄力时的耐力恢复（/秒）；空中不恢复
        public float staminaRegenCharge = 0.5f; // 蓄力时恢复 = staminaRegen × 该值；0 = 蓄力不回
        public int staminaSlots = 5; // 身周耐力圆环格数
        [HideInInspector]
        public float dMin = 8f; // 废止：旧版点跳距离，现由 tFloor 反推
        public float vRate = 80f; // A1，水平加速度（Δv_x / 秒）；出手速度 = A1 × (蓄力时间 + tFloor)
        public float theta = 15f; // 起跳仰角（度）；与 μ 一起定空中匀速占比
        public float mass = 1f; // 基础质量；对撞分速度，也进抵抗
        public float gravity = 80f; // 重力；抛物线与落地减速都用
        public float mu = 1.8f; // 地面摩擦；落地匀减速 a = μg，也改匀速占比
        public float rStand = 0.4f; // 站立抵抗系数 K0；相对满蓄速度的倍数
        public float rMax = 0.4f; // 满速抵抗系数 K1；相对满蓄速度的倍数
        public float rChargeScale = 0.5f; // 静止蓄力时的抵抗折扣
        public float muCtrlScale = 1.2f; // 可控档摩擦倍率；只改撞后滑多远
        public float muSlipScale = 0.8f; // 失衡档摩擦倍率；只改撞后滑多远
        public float growPer = 0.16f; // 每层成长给半径和质量加的倍率
        public float bugR = 1.8f; // 开局碰撞半径；成长和增大再乘
        public float sizeScale = 1.3f; // 增大倍率；拾取与狂暴共用，半径和质量同乘
        public float sizeT = 6f; // 增大持续（秒）；仅拾取，狂暴不读
        public float shieldT = 8f; // 护盾持续（秒）；未被消耗也会到期
        public float chargeScale = 1.25f; // 蓄力强化倍率；拾取与狂暴共用，A1 × s、Tmax / s
        public float chargeBuffT = 5f; // 蓄力强化持续（秒）；仅拾取，狂暴不读
        // AI 决策参数：攻击范围可在 Inspector 中调节；安全边距用于限制主动起跳路线。
        public float aiAttackRange = 14f; // 人机主动起跳的攻击距离
        public float aiSafeEdgeMargin = 4f; // 人机贴边安全距；路线太贴圈则改方向
        // 经济系统和阶段时间参数。
        public float itemR = 1.35f; // 限时道具拾取半径（饲料球另用固定半径）
        public float regTime = 90f; // 正赛时长（秒）；到点未结束则进加时
        public float otTime = 30f; // 加时 / 狂暴时长（秒）；与正赛相加为硬截止
        public int heartStart = 4; // 开局饲料球数量
        public int heartCap = 6; // 场上饲料球上限；不足才补，每次 1 颗
        public int heartBatch = 6; // 未使用；饲料球补货仍每次 1 颗
        public int itemBatch = 3; // 到点补几颗限时道具，不超过 itemCap
        public int itemCap = 3; // 场上限时道具上限；已满则挂起
        public float heartGap = 7f; // 正赛补饲料球间隔（秒）
        public float heartGapOt = 5f; // 加时补饲料球间隔（秒）
        public float heartOpenAt = 20f; // 开始补饲料球的时间（秒）；此前只吃开局那批
        public float v3Rate = 23f; // 未使用；旧版 A1 遗留
        public float itemMinEdge = 2.4f; // 限时道具离罐边的最小距离
        public float itemMinBug = 3f; // 投放点离活虫的最小距离
        public float itemMinHeart = 1.6f; // 投放点离已有饲料球的最小距离
        public float itemMinItem = 2f; // 投放点离已有限时道具的最小距离
        public float itemRingMin = 6f; // 限时道具刷在环带上的内半径
        public float itemRingMax = 12f; // 限时道具刷在环带上的外半径
        public float heartMinEdge = 1.4f; // 饲料球离罐边的最小距离
        public float heartMinBug = 1.3f; // 未使用；投放避虫走 itemMinBug
        public float heartMinHeart = 1.25f; // 未使用；投放避球走 itemMinHeart
        public float shieldPad = 0.08f; // 护盾拉回区内后再往里留的余量
        // 巢穴、蛋和幼虫生命周期参数。
        public float nestHP = 4f; // 房子血量；一次有效撞击 -1
        public float nestMass = 3f; // 房子质量；不位移，只用于对撞分速度
        public float nestR = 2.4f; // 房子碰撞半径
        public int nestEggN = 5; // 房子爆开散落的卵数
        public float eggHatchT = 5f; // 卵孵化基准时间（秒）
        public float eggHatchGap = 0.28f; // 第 i 枚卵再加 i × 该值；0 = 只靠随机错开
        public float eggHatchJitter = 0.15f; // 孵化时间随机抖动上限（秒）
        public float eggScatterV = 8f; // 卵散开初速；当前爆开不读此项
        public float eggR = 0.55f; // 卵碰撞 / 出圈半径
        public float eggMass = 0.2f; // 卵被踢时的质量
        public float babyLifeT = 12f; // 崽寿命（秒）；孵化起算，出圈也会死
        public float babyRScale = 0.4f; // 崽半径 = bugR × 该值
        public float babyMass = 0.5f; // 崽质量；不吃饲主成长
        public float babyA1Scale = 0.4f; // 崽 A1 = 面板 A1 × 该值；不另改 Tmax
        public float babyChargeT = 0.8f; // 崽自动蓄多久再跳（秒）
        public float babyAtkCd = 0.8f; // 崽两次起跳最短间隔（秒）；0 = 落地即可再蓄
        public bool babyCanLoot = false; // 崽能否吃饲料球 / 限时道具；默认关
        public int nestCap = 1; // 场上巢上限；整条链算 1 个
        public float nestFirstT = 25f; // 首栋房子出现时间（秒）
        public float nestGap = 12f; // 上一窝彻底结束后，下一栋再等的间隔（秒）
    }

    /// <summary>
    /// 纯规则函数集合，不保存对局状态，保证主机、单机和测试复用同一套计算。
    /// </summary>
    public static class DouQuquRules
    {
        public const float ArenaHalfWidth = 21.2f;
        public const float ArenaHalfDepth = 31.8f;
        public const float ArenaCorner = 7.2f;
        public static readonly string[] ItemKinds = { "size", "shield", "charge" };

        /// <summary>返回一份默认规则参数。</summary>
        public static MatchKnobs DefaultKnobs()
        {
            return new MatchKnobs();
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
            return bug.rageCharge || bug.buffChargeT > 0f;
        }

        /// <summary>判断蟋蟀的护盾是否仍有效。</summary>
        public static bool ShieldActive(BugState bug)
        {
            return bug.buffShieldT > 0f;
        }

        /// <summary>计算成长层数对体型和质量产生的倍率。</summary>
        public static float GrowRate(MatchKnobs knobs, BugState bug)
        {
            return 1f + bug.grow * Mathf.Max(0f, knobs.growPer);
        }

        /// <summary>按成长和临时增大效果刷新蟋蟀碰撞半径与质量。</summary>
        public static void RefreshBody(MatchKnobs knobs, BugState bug)
        {
            float g = GrowRate(knobs, bug);
            float size = SizeActive(bug) ? knobs.sizeScale : 1f;
            bug.radius = knobs.bugR * g * size;
            bug.mass = Mathf.Max(0.08f, knobs.mass) * g * size;
        }

        /// <summary>按成长和临时增大效果刷新幼虫碰撞半径与质量。</summary>
        public static void RefreshBabyBody(MatchKnobs knobs, BabyState baby)
        {
            if (baby == null) return;
            float g = 1f + baby.grow * Mathf.Max(0f, knobs.growPer);
            float size = baby.buffSizeT > 0f ? knobs.sizeScale : 1f;
            baby.radius = knobs.bugR * Mathf.Max(0f, knobs.babyRScale) * g * size;
            baby.mass = Mathf.Max(0.05f, knobs.babyMass) * g * size;
        }

        /// <summary>返回当前增益下的蟋蟀蓄力速度。</summary>
        public static float EffectiveChargeSpeed(MatchKnobs knobs, BugState bug)
        {
            return knobs.vRate * (ChargeActive(bug) ? knobs.chargeScale : 1f);
        }

        /// <summary>返回当前增益下蓄力条的最大持续时间。</summary>
        public static float EffectiveChargeTime(MatchKnobs knobs, BugState bug)
        {
            float scale = ChargeActive(bug) ? knobs.chargeScale : 1f;
            return knobs.tChargeMax / Mathf.Max(0.01f, scale);
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

        /// <summary>落地恢复耐力，含滑行；空中不恢复；蓄力中乘 staminaRegenCharge。</summary>
        public static void TickStamina(MatchKnobs knobs, BugState bug, float dt)
        {
            if (bug == null || !bug.alive || bug.airborne) return;
            float max = Mathf.Max(0f, knobs.staminaMax);
            float regen = Mathf.Max(0f, knobs.staminaRegen);
            if (bug.charging) regen *= Mathf.Max(0f, knobs.staminaRegenCharge);
            bug.stamina = Mathf.Clamp(bug.stamina + regen * dt, 0f, max);
        }

        /// <summary>起跳力气。每次有效起跳都加上，不受蓄力强化缩短。</summary>
        public static float TFloor(MatchKnobs knobs)
        {
            return Mathf.Max(0f, knobs.tFloor);
        }

        /// <summary>未强化满蓄水平速度 v_max = A1 (T_max + t_floor)。</summary>
        public static float PanelVMax(MatchKnobs knobs)
        {
            return Mathf.Max(0f, knobs.vRate) * (Mathf.Max(0f, knobs.tChargeMax) + TFloor(knobs));
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

        /// <summary>点跳水平速度 = A1' t_floor。蓄力强化会抬高，t_floor 本身不缩短。</summary>
        public static float JumpSpeedMin(MatchKnobs knobs, BugState bug = null)
        {
            float rate = bug == null ? Mathf.Max(0f, knobs.vRate) : EffectiveChargeSpeed(knobs, bug);
            return rate * TFloor(knobs);
        }

        /// <summary>跳出力气对应的水平速度：Δv_x = A1' (t_蓄 + t_floor)。</summary>
        public static float JumpDeltaV(MatchKnobs knobs, BugState bug, float chargeTime)
        {
            float tAcc = Mathf.Clamp(chargeTime, 0f, EffectiveChargeTime(knobs, bug));
            return EffectiveChargeSpeed(knobs, bug) * (tAcc + TFloor(knobs));
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
            float rate = Mathf.Max(0f, knobs.vRate) * Mathf.Max(0f, knobs.babyA1Scale);
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
        public static float[] BabyChargeStats(MatchKnobs knobs)
        {
            return new[] { Mathf.Max(0f, knobs.vRate) * Mathf.Max(0f, knobs.babyA1Scale), BabyChargeTime(knobs) };
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

        /// <summary>进入加时狂暴：存活蟋蟀获得永久增大和蓄力强化。</summary>
        public static void EnterRage(MatchKnobs knobs, BugState[] bugs)
        {
            for (int i = 0; i < bugs.Length; i++)
            {
                BugState bug = bugs[i];
                if (!bug.alive) continue;
                bug.rageSize = true;
                bug.rageCharge = true;
                bug.buffSizeT = 0f;
                bug.buffChargeT = 0f;
                RefreshBody(knobs, bug);
            }
        }

        /// <summary>选择与上一种不同的道具类型，并更新类型游标。</summary>
        public static string PickItemKind(ref string lastKind, float roll)
        {
            int selected = 0;
            if (lastKind == "size") selected = roll < 0.5f ? 1 : 2;
            else if (lastKind == "shield") selected = roll < 0.5f ? 0 : 2;
            else if (lastKind == "charge") selected = roll < 0.5f ? 0 : 1;
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
        public bool alive = true;
        public Vector3 position;
        public Vector3 previousPosition;
        public Vector3 velocity;
        public float verticalVelocity;
        public float height;
        public float initialSpeed;
        public float slideMu;
        public float chargeTime;
        public float stamina;
        public Vector2 chargeDirection = Vector2.up;
        public float radius;
        public float mass;
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
