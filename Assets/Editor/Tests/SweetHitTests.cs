using NUnit.Framework;
using UnityEngine;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class SweetHitTests
    {
        [Test]
        public void LandingCircleIsJumpDistanceAhead()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            knobs.dMin = 3f;
            knobs.tChargeMax = 0.9f;
            knobs.chargeSlope = 13f;
            BugState bug = new BugState(0, Vector3.zero, knobs);
            bug.chargeDirection = Vector2.up;
            bug.chargeTime = 0.9f;
            bug.radius = 1.8f;
            float dist = Rules.JumpDistance(knobs, bug, bug.chargeTime);
            Rules.ArmJumpSweet(bug, dist, knobs);
            Assert.IsTrue(bug.jumpSweetArmed);
            Assert.AreEqual(0f, bug.jumpSweetLanding.x, 0.02f);
            Assert.AreEqual(dist, bug.jumpSweetLanding.z, 0.02f);
            Assert.AreEqual(Rules.SweetRadiusOf(knobs, 1.8f), bug.jumpSweetRadius, 0.01f);
        }

        [Test]
        public void CollisionInsideRecordedCircleCountsAsSweet()
        {
            BugState bug = new BugState(0, Vector3.zero, Rules.DefaultKnobs());
            bug.jumpSweetArmed = true;
            bug.jumpSweetLanding = new Vector3(0f, 0f, 10f);
            bug.jumpSweetRadius = 2f;
            Assert.IsTrue(Rules.IsJumpSweetHit(bug, new Vector3(0f, 0f, 10f)));
            Assert.IsTrue(Rules.IsJumpSweetHit(bug, new Vector3(0f, 0f, 11.9f)));
            Assert.IsFalse(Rules.IsJumpSweetHit(bug, new Vector3(0f, 0f, 12.1f)));
        }

        [Test]
        public void SweetRadiusFollowsScaleKnob()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            knobs.sweetRScale = 2f;
            Assert.AreEqual(3.6f, Rules.SweetRadiusOf(knobs, 1.8f), 1e-4f);
        }

        [Test]
        public void HatchBarDrainsFromFullToEmpty()
        {
            Assert.AreEqual(1f, HatchBar.RemainRatio(0f, 5f, 5f), 1e-4f);
            Assert.AreEqual(0.4f, HatchBar.RemainRatio(3f, 5f, 5f), 1e-4f);
            Assert.AreEqual(0f, HatchBar.RemainRatio(5f, 5f, 5f), 1e-4f);
        }

        [Test]
        public void KnockbackTriplesDistanceKeepsTime()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            knobs.mu = 1.4f;
            knobs.gravity = 120f;
            BugState bug = new BugState(0, Vector3.zero, knobs);
            bug.velocity = new Vector3(0f, 0f, 10f);
            bug.slideMu = 1.4f;
            Rules.SetLaunch(bug, bug.velocity);
            float g = Rules.Gravity(knobs);
            float t0 = 10f / (1.4f * g);
            float d0 = 100f / (2f * 1.4f * g);
            Rules.ScaleKnockback(bug, 3f);
            float t1 = bug.velocity.z / (bug.slideMu * g);
            float d1 = bug.velocity.z * bug.velocity.z / (2f * bug.slideMu * g);
            Assert.AreEqual(t0, t1, 1e-4f);
            Assert.AreEqual(3f * d0, d1, 1e-4f);
        }
    }
}
