using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 局内骨骼动画：把 charging / airborne / hitTier 写进 Animator。
    /// 蓄力冻在 Jump 第 3 帧（12fps → 0.25s）。连撞规则在 Controller 里。
    /// </summary>
    public sealed class CricketAnim : MonoBehaviour
    {
        public const string ChargeState = "Charge";
        public const float ChargeHoldTime = 0.25f;

        const float SettleSpeed = Rules.SettleSnap;

        static readonly int ChargingId = Animator.StringToHash("Charging");
        static readonly int AirborneId = Animator.StringToHash("Airborne");
        static readonly int SettledId = Animator.StringToHash("Settled");
        static readonly int CrashId = Animator.StringToHash("Crash");
        static readonly int CrashControlId = Animator.StringToHash("CrashControl");

        Animator animator;
        float chargeHoldNorm = 0.2f;

        void Awake()
        {
            Bind();
        }

        public void Apply(BugState bug)
        {
            if (bug == null || !bug.alive)
            {
                ApplyIdle();
                return;
            }

            if (!Bind()) return;

            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.SetLayerWeight(0, 1f);
            bool crashing = bug.hitTier != HitTier.None;
            bool settled = IsSettled(bug);
            animator.SetBool(ChargingId, bug.charging && !crashing);
            animator.SetBool(AirborneId, bug.airborne && !crashing);
            animator.SetBool(SettledId, settled && !crashing);
            animator.SetBool(CrashId, crashing);
            animator.SetBool(CrashControlId, Rules.CanonicalHitTier(bug.hitTier) == HitTier.Control);
            if (bug.charging && !crashing) HoldChargeFrame();
        }

        void ApplyIdle()
        {
            if (!Bind()) return;
            animator.SetBool(ChargingId, false);
            animator.SetBool(AirborneId, false);
            animator.SetBool(SettledId, true);
            animator.SetBool(CrashId, false);
            animator.SetBool(CrashControlId, false);
        }

        static bool IsSettled(BugState bug)
        {
            if (bug.airborne || bug.height > 0.03f) return false;
            Vector2 planar = new Vector2(bug.velocity.x, bug.velocity.z);
            return planar.magnitude < SettleSpeed;
        }

        void HoldChargeFrame()
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (!info.IsName(ChargeState))
            {
                if (animator.IsInTransition(0))
                {
                    AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                    if (!next.IsName(ChargeState)) return;
                    info = next;
                }
                else return;
            }

            animator.Play(info.fullPathHash, 0, chargeHoldNorm);
        }

        bool Bind()
        {
            if (animator != null) return true;
            animator = GetComponentInChildren<Animator>(true);
            if (animator == null) return false;
            animator.applyRootMotion = false;
            if (animator.runtimeAnimatorController != null)
            {
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (clip != null && clip.name == "Cricket_Jump" && clip.length > 0.01f)
                    {
                        chargeHoldNorm = Mathf.Clamp01(ChargeHoldTime / clip.length);
                        break;
                    }
                }
            }

            return true;
        }
    }
}
