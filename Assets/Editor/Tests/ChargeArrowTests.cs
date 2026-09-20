using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class ChargeArrowTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }

        [Test]
        public void ApplyPutsBarBehindAndEndpointOnLanding()
        {
            ChargeArrow arrow = CreateArrow();
            const float radius = 1.8f;
            const float distance = 6f;
            float circleR = GroundMarker.CircleRadius(radius);
            arrow.Apply(true, distance, 0.5f, Vector2.up, Vector3.zero, radius, Color.red, 3f, 0.4f, 1f);

            Transform fill = arrow.transform.Find("Fill");
            Assert.IsNotNull(fill);
            SpriteRenderer bar = fill.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(bar);
            Assert.IsTrue(bar.enabled);
            Assert.IsNull(fill.GetComponent<MeshRenderer>());
            Assert.That(bar.bounds.max.z, Is.EqualTo(-circleR).Within(0.12f));
            Assert.That(bar.bounds.size.x, Is.EqualTo(circleR * 2f).Within(0.12f));

            Transform endpoint = arrow.transform.Find("Endpoint");
            Assert.IsNotNull(endpoint);
            SpriteRenderer ring = endpoint.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(ring);
            Assert.IsTrue(ring.enabled);
            Assert.IsNull(endpoint.GetComponent<LineRenderer>());
            Assert.That(ring.bounds.center.z, Is.EqualTo(distance).Within(0.08f));
            Assert.That(ring.bounds.size.x, Is.EqualTo(circleR * 2f).Within(0.12f));
            Assert.That(ring.bounds.size.z, Is.EqualTo(circleR * 2f).Within(0.12f));

            Transform first = arrow.transform.Find("Chevrons/Chevron_0");
            Assert.IsNotNull(first);
            Assert.IsTrue(first.GetComponent<SpriteRenderer>().enabled);
            Assert.Greater(first.localPosition.z, circleR - 0.05f);
            Assert.Less(first.localPosition.z, distance);
        }

        [Test]
        public void TapJumpUsesMinAlphaFullChargeUsesMax()
        {
            ChargeArrow arrow = CreateArrow();
            arrow.Apply(true, 3f, 0f, Vector2.right, Vector3.zero, 1.8f, Color.green, 3f, 0.4f, 1f);
            Assert.AreEqual(0.4f, arrow.transform.Find("Fill").GetComponent<SpriteRenderer>().color.a, 0.02f);

            arrow.Apply(true, 9f, 1f, Vector2.right, Vector3.zero, 1.8f, Color.green, 3f, 0.4f, 1f);
            Assert.AreEqual(1f, arrow.transform.Find("Fill").GetComponent<SpriteRenderer>().color.a, 0.02f);
        }

        [Test]
        public void BarUsesMaskAndKeepsStrengthAspect()
        {
            ChargeArrow arrow = CreateArrow();
            const float radius = 1.8f;
            const float distance = 6f;
            const float ratio = 3f;
            float circleR = GroundMarker.CircleRadius(radius);
            arrow.Apply(true, distance, 0.5f, Vector2.up, Vector3.zero, radius, Color.red, ratio, 0.4f, 1f);

            Transform fill = arrow.transform.Find("Fill");
            Assert.IsNotNull(fill);
            SpriteRenderer bar = fill.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(bar);
            Assert.IsTrue(bar.enabled);
            Assert.That(bar.transform.localScale.x, Is.EqualTo(bar.transform.localScale.y).Within(0.08f));
            Assert.That(bar.bounds.size.x, Is.EqualTo(circleR * 2f).Within(0.12f));
            Assert.Less(bar.sprite.rect.width + 0.5f, bar.sprite.texture.width);
        }

        [Test]
        public void PrefabStyleFillSpriteRendererDoesNotThrow()
        {
            GameObject go = new GameObject("ChargeArrow");
            created.Add(go);
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(go.transform, false);
            fill.AddComponent<SpriteRenderer>();
            ChargeArrow arrow = go.AddComponent<ChargeArrow>();
            Assert.DoesNotThrow(() => arrow.Apply(true, 6f, 0.5f, Vector2.up, Vector3.zero, 1.8f, Color.red, 3f, 0.4f, 1f));
            Assert.IsTrue(go.transform.Find("Fill").GetComponent<SpriteRenderer>().enabled);
            Assert.IsTrue(go.transform.Find("Chevrons/Chevron_0").GetComponent<SpriteRenderer>().enabled);
        }

        [Test]
        public void HideTurnsOffBarDashAndEndpoint()
        {
            ChargeArrow arrow = CreateArrow();
            arrow.Apply(true, 6f, 0.4f, Vector2.up, Vector3.zero, 1.8f, Color.blue, 3f, 0.4f, 1f);
            arrow.Hide();
            Assert.IsFalse(arrow.gameObject.activeSelf);
        }

        [Test]
        public void JumpPreviewUsesGripSoMarkerMatchesDesignedLanding()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            BugState bug = new BugState(0, Vector3.zero, knobs);
            bug.gripMul = 1.74f;
            bug.chargeTime = knobs.tChargeMax;
            float designed = Rules.JumpDistance(knobs, bug, bug.chargeTime);
            float speed = Rules.JumpDeltaV(knobs, bug, bug.chargeTime);
            Assert.That(Rules.JumpRange(knobs, speed, bug), Is.EqualTo(designed).Within(0.01f));
            Assert.Greater(Rules.JumpRange(knobs, speed), designed + 0.05f);
        }

        [Test]
        public void SimulatedLandingCenterMatchesJumpDistance()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            BugState bug = new BugState(0, Vector3.zero, knobs);
            bug.alive = true;
            bug.gripMul = 1.74f;
            bug.charging = true;
            bug.chargeTime = knobs.tChargeMax;
            bug.chargeDirection = Vector2.up;
            bug.stamina = 99f;
            float designed = Rules.JumpDistance(knobs, bug, bug.chargeTime);

            MatchState state = new MatchState();
            state.knobs = knobs;
            state.bugs = new[] { bug };
            MovementSystem movement = new MovementSystem();
            InputFrame[] release = { new InputFrame(0, Vector2.up, false, true) };
            InputFrame[] idle = { new InputFrame(0, Vector2.up, false, false) };
            const int substeps = 6;
            float dt = MatchController.FixedDeltaTime;
            float subDt = dt / substeps;
            for (int s = 0; s < substeps; s++)
                movement.TickMotion(state, release, subDt, null, null);
            movement.TickCharge(state, release, dt);

            bool settled = false;
            for (int i = 0; i < 480 && !settled; i++)
            {
                for (int s = 0; s < substeps; s++)
                    movement.TickMotion(state, idle, subDt, null, null);
                movement.TickCharge(state, idle, dt);
                settled = !bug.airborne && bug.height <= 0.03f && Rules.IsPlanarSettled(bug.velocity);
            }

            Assert.IsTrue(settled);
            float traveled = new Vector2(bug.position.x, bug.position.z).magnitude;
            Assert.That(traveled, Is.EqualTo(designed).Within(0.12f));
        }

        ChargeArrow CreateArrow()
        {
            GameObject go = new GameObject("ChargeArrow");
            created.Add(go);
            return go.AddComponent<ChargeArrow>();
        }
    }
}
