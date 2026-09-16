using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class MatchStartFreezeTests
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
        public void AiDoesNotMoveUntilStartMatch()
        {
            MatchController match = CreateMatch();
            match.Configure(MatchRunMode.Offline, MatchController.MaxPlayers, Rules.DefaultKnobs());
            match.ResetMatch(MatchController.MaxPlayers, 20260828);
            Assert.IsFalse(match.IsStarted);

            Vector3[] before = CapturePositions(match);
            for (int i = 0; i < 180; i++)
                match.Tick(MatchController.FixedDeltaTime);

            Assert.IsFalse(match.IsStarted);
            AssertPositions(before, match);
            Assert.IsFalse(AnyAirborne(match));
        }

        [Test]
        public void AiCanMoveAfterStartMatch()
        {
            MatchController match = CreateMatch();
            match.Configure(MatchRunMode.Offline, MatchController.MaxPlayers, Rules.DefaultKnobs());
            match.ResetMatch(MatchController.MaxPlayers, 20260828);
            match.StartMatch();
            Vector3[] before = CapturePositions(match);
            for (int i = 0; i < 240; i++)
                match.Tick(MatchController.FixedDeltaTime);

            Assert.IsTrue(match.IsStarted);
            Assert.IsTrue(MovedOrAirborne(before, match));
        }

        MatchController CreateMatch()
        {
            GameObject go = new GameObject("MatchStartFreeze");
            created.Add(go);
            return go.AddComponent<MatchController>();
        }

        static Vector3[] CapturePositions(MatchController match)
        {
            BugState[] bugs = match.Bugs;
            Vector3[] positions = new Vector3[bugs.Length];
            for (int i = 0; i < bugs.Length; i++)
                positions[i] = bugs[i].position;
            return positions;
        }

        static void AssertPositions(Vector3[] before, MatchController match)
        {
            BugState[] bugs = match.Bugs;
            Assert.AreEqual(before.Length, bugs.Length);
            for (int i = 0; i < bugs.Length; i++)
            {
                Assert.AreEqual(before[i].x, bugs[i].position.x, 1e-4f);
                Assert.AreEqual(before[i].z, bugs[i].position.z, 1e-4f);
            }
        }

        static bool AnyAirborne(MatchController match)
        {
            BugState[] bugs = match.Bugs;
            for (int i = 0; i < bugs.Length; i++)
                if (bugs[i] != null && bugs[i].airborne) return true;
            return false;
        }

        static bool MovedOrAirborne(Vector3[] before, MatchController match)
        {
            if (AnyAirborne(match)) return true;
            BugState[] bugs = match.Bugs;
            for (int i = 0; i < bugs.Length; i++)
            {
                if (bugs[i] == null) continue;
                if ((bugs[i].position - before[i]).sqrMagnitude > 0.01f) return true;
            }
            return false;
        }
    }
}
