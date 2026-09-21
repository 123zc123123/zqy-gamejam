using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class LanClientFeelTests
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
            Rules.ResetArenaSize();
        }

        [Test]
        public void InputGateThrottlesHeldSamplesTo30Hz()
        {
            LanClientInputGate gate = new LanClientInputGate();
            Assert.AreEqual(1, gate.CopiesFor(0f, true, false));
            gate.NoteSent(0f, Vector2.up, true, false);
            Assert.AreEqual(0, gate.CopiesFor(0.01f, true, false));
            Assert.AreEqual(1, gate.CopiesFor(LanClientInputGate.SampleInterval, true, false));
        }

        [Test]
        public void InputGateBurstsOnReleaseEvenWithinInterval()
        {
            LanClientInputGate gate = new LanClientInputGate();
            gate.NoteSent(0f, Vector2.up, true, false);
            Assert.AreEqual(LanClientInputGate.ReleaseBurstCopies, gate.CopiesFor(0.01f, false, true));
        }

        [Test]
        public void InputGateRepeatsReleaseThenStops()
        {
            LanClientInputGate gate = new LanClientInputGate();
            gate.NoteSent(0f, Vector2.right, false, true);
            Vector2 direction;
            bool held;
            bool released;
            Assert.IsFalse(gate.TryRepeat(0.01f, out direction, out held, out released));
            Assert.IsTrue(gate.TryRepeat(LanClientInputGate.SampleInterval, out direction, out held, out released));
            Assert.AreEqual(Vector2.right, direction);
            Assert.IsFalse(held);
            Assert.IsTrue(released);
            gate.NoteSent(LanClientInputGate.SampleInterval, direction, held, released);
            Assert.IsFalse(gate.TryRepeat(LanClientInputGate.RepeatAfterReleaseSeconds + 0.05f, out direction, out held, out released));
        }

        [Test]
        public void InputGateHoldCancelsReleaseRepeat()
        {
            LanClientInputGate gate = new LanClientInputGate();
            gate.NoteSent(0f, Vector2.up, false, true);
            gate.NoteSent(0.02f, Vector2.up, true, false);
            Vector2 direction;
            bool held;
            bool released;
            Assert.IsFalse(gate.TryRepeat(LanClientInputGate.SampleInterval, out direction, out held, out released));
        }

        [Test]
        public void VisualSmoothingSnapsFirstThenLerpsSmallDelta()
        {
            HashSet<int> initialized = new HashSet<int>();
            Vector3 start = Vector3.zero;
            Vector3 first = ClientVisualSmoothing.Step(start, new Vector3(1f, 0f, 0f), initialized, 0, 2.5f, 24f, 0.016f, true);
            Assert.AreEqual(1f, first.x, 1e-4f);
            Vector3 second = ClientVisualSmoothing.Step(first, new Vector3(1.2f, 0f, 0f), initialized, 0, 2.5f, 24f, 0.016f, true);
            Assert.Greater(second.x, first.x);
            Assert.Less(second.x, 1.2f);
        }

        [Test]
        public void VisualSmoothingDisabledReturnsTarget()
        {
            HashSet<int> initialized = new HashSet<int>();
            Vector3 result = ClientVisualSmoothing.Step(Vector3.zero, new Vector3(3f, 0f, 1f), initialized, 7, 2.5f, 24f, 0.016f, false);
            Assert.AreEqual(3f, result.x, 1e-4f);
            Assert.AreEqual(1f, result.z, 1e-4f);
        }

        [Test]
        public void VisualSmoothingSnapsWhenDeltaExceedsLimit()
        {
            HashSet<int> initialized = new HashSet<int> { 0 };
            Vector3 result = ClientVisualSmoothing.Step(Vector3.zero, new Vector3(4f, 0f, 0f), initialized, 0, 2.5f, 24f, 0.016f, true);
            Assert.AreEqual(4f, result.x, 1e-4f);
        }

        [Test]
        public void ExtraReleaseAfterLaunchDoesNotRelaunch()
        {
            MatchController match = CreateMatch();
            match.Configure(MatchRunMode.Offline, 1, Rules.DefaultKnobs());
            match.ResetMatch(1, 20260918);
            match.StartMatch();
            match.SetInput(0, Vector2.up, true, false);
            for (int i = 0; i < 60; i++)
                match.Tick(MatchController.FixedDeltaTime);

            BugState bug = match.Bugs[0];
            Assert.IsTrue(bug.charging);
            float staminaBefore = bug.stamina;

            match.SetInput(0, Vector2.up, false, true);
            match.Tick(MatchController.FixedDeltaTime);
            Assert.IsFalse(bug.charging);
            Assert.Less(bug.stamina, staminaBefore);
            float staminaFloor = bug.stamina;

            for (int i = 0; i < 6; i++)
            {
                match.SetInput(0, Vector2.up, false, true);
                match.Tick(MatchController.FixedDeltaTime);
                Assert.IsFalse(bug.charging);
                Assert.GreaterOrEqual(bug.stamina + 1e-3f, staminaFloor);
                staminaFloor = bug.stamina;
            }
        }

        [Test]
        public void CameraFallsBackToSnapshotWhenNoVisual()
        {
            MatchController match = CreateMatch();
            match.Configure(MatchRunMode.Client, 1, Rules.DefaultKnobs());
            match.ResetMatch(1, 20260918);
            match.Bugs[0].position = new Vector3(5f, 0f, -3f);

            GameObject camGo = new GameObject("BattleCam");
            created.Add(camGo);
            camGo.AddComponent<Camera>().orthographic = true;
            BattleCamera cam = camGo.AddComponent<BattleCamera>();
            cam.FollowLocalPlayer(match, 0);

            Vector3 follow;
            Assert.IsTrue(cam.TryReadFollowPosition(out follow));
            Assert.AreEqual(5f, follow.x, 1e-4f);
            Assert.AreEqual(-3f, follow.z, 1e-4f);
        }

        [Test]
        public void CameraPrefersSmoothedVisualOverSnapshot()
        {
            GameObject root = new GameObject("ClientFeelRoot");
            created.Add(root);
            MatchController match = root.AddComponent<MatchController>();
            DemoView view = root.AddComponent<DemoView>();
            match.Configure(MatchRunMode.Client, 1, Rules.DefaultKnobs());
            match.ResetMatch(1, 20260918);
            match.Bugs[0].position = new Vector3(2f, 0f, 4f);
            view.BindMatch(match);

            GameObject visual = new GameObject("Bug_0");
            created.Add(visual);
            visual.transform.position = new Vector3(8f, 0f, 1f);
            typeof(DemoView)
                .GetField("bugViews", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(view, new Dictionary<int, GameObject> { { 0, visual } });

            GameObject camGo = new GameObject("BattleCam");
            created.Add(camGo);
            camGo.AddComponent<Camera>().orthographic = true;
            BattleCamera cam = camGo.AddComponent<BattleCamera>();
            cam.FollowLocalPlayer(match, 0);

            Vector3 follow;
            Assert.IsTrue(cam.TryReadFollowPosition(out follow));
            Assert.AreEqual(8f, follow.x, 1e-4f);
            Assert.AreEqual(1f, follow.z, 1e-4f);
        }

        [Test]
        public void NestedSnapshotEnvelopeExceedsUdpBudget()
        {
            MatchController match = CreateMatch();
            match.Configure(MatchRunMode.Offline, MatchController.MaxPlayers, Rules.DefaultKnobs());
            match.ResetMatch(MatchController.MaxPlayers, 20260918);
            match.StartMatch();
            MatchSnapshot snapshot = match.CaptureSnapshot(true);
            string body = JsonUtility.ToJson(snapshot);
            LanEnvelope envelope = new LanEnvelope
            {
                type = "SNAPSHOT",
                senderId = 0,
                tick = snapshot.tick,
                body = body
            };
            int bytes = Encoding.UTF8.GetByteCount(JsonUtility.ToJson(envelope));
            Assert.Greater(bytes, LanSession.UdpSafePayloadBytes);
        }

        [Test]
        public void ApplyingSnapshotKeepsAuthoritativeLifeAndRosterState()
        {
            MatchController match = CreateMatch();
            match.Configure(MatchRunMode.Client, 2, Rules.DefaultKnobs());
            match.ResetMatch(2, 20260918);

            MatchSnapshot snapshot = match.CaptureSnapshot(true);
            snapshot.cricketIndex[0] = 2;
            snapshot.playerIn[0] = false;
            snapshot.place[0] = 2;
            snapshot.matchScore[0] = 17;

            int[] cricketIndexStorage = match.State.cricketIndex;
            bool[] playerInStorage = match.State.playerIn;

            match.ApplySnapshot(snapshot);

            Assert.AreSame(cricketIndexStorage, match.State.cricketIndex);
            Assert.AreSame(playerInStorage, match.State.playerIn);
            Assert.AreEqual(2, match.CricketIndex(0));
            Assert.IsFalse(match.PlayerStillIn(0));
            Assert.AreEqual(2, match.Place(0));
            Assert.AreEqual(17, match.MatchScore(0));
        }

        MatchController CreateMatch()
        {
            GameObject go = new GameObject("LanClientFeel");
            created.Add(go);
            return go.AddComponent<MatchController>();
        }
    }
}
