using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class ZoneScheduleTests
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
        public void Z1_DefaultSnapsAt30_60_90()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            float[] snaps = Rules.ZoneSnapTimes(knobs);
            Assert.AreEqual(3, snaps.Length);
            Assert.AreEqual(30f, snaps[0], 1e-4f);
            Assert.AreEqual(60f, snaps[1], 1e-4f);
            Assert.AreEqual(90f, snaps[2], 1e-4f);
        }

        [Test]
        public void Z2_SnapIsInstantNoLerp()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            Assert.AreEqual(0, Rules.ZoneTierAt(knobs, 29.9f));
            Assert.AreEqual(2f, Rules.ZoneScaleOf(knobs, Rules.ZoneTierAt(knobs, 29.9f)), 1e-4f);
            Assert.AreEqual(1, Rules.ZoneTierAt(knobs, 30f));
            Assert.AreEqual(1.5f, Rules.ZoneScaleOf(knobs, Rules.ZoneTierAt(knobs, 30f)), 1e-4f);
            Assert.AreEqual(2, Rules.ZoneTierAt(knobs, 60f));
            Assert.AreEqual(1.2f, Rules.ZoneScaleOf(knobs, Rules.ZoneTierAt(knobs, 60f)), 1e-4f);
        }

        [Test]
        public void Z3_WarnKeepsCurrentSdf()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            Assert.IsTrue(Rules.IsZoneWarn(knobs, 25f));
            Assert.IsTrue(Rules.IsZoneWarn(knobs, 29.9f));
            Assert.IsFalse(Rules.IsZoneWarn(knobs, 24.9f));
            Assert.AreEqual(0, Rules.ZoneTierAt(knobs, 27f));
            Rules.ApplyZoneAt(knobs, 27f);
            Vector3 inCurrentOutNext = new Vector3(35f, 0f, 0f);
            Assert.IsTrue(Rules.InsideArena(inCurrentOutNext));
            Rules.SetArenaScale(Rules.ZoneScaleOf(knobs, 1));
            Assert.IsFalse(Rules.InsideArena(inCurrentOutNext));
        }

        [Test]
        public void Z4_ThirdSnapIsRageAndNoFourth()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            Assert.AreEqual(Rules.LastZoneTier, Rules.ZoneTierAt(knobs, 90f));
            Assert.IsTrue(Rules.IsRage(knobs, 90f));
            Assert.AreEqual(Rules.LastZoneTier, Rules.ZoneTierAt(knobs, 90.01f));
            Assert.AreEqual(Rules.LastZoneTier, Rules.ZoneTierAt(knobs, 119f));
            Assert.IsFalse(Rules.IsZoneWarn(knobs, 95f));
        }

        [Test]
        public void Z5_HoldChangeMovesFirstSnap()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            knobs.zoneHold0 = 20f;
            float[] snaps = Rules.ZoneSnapTimes(knobs);
            Assert.AreEqual(25f, snaps[0], 1e-4f);
            Assert.AreEqual(0, Rules.ZoneTierAt(knobs, 24.9f));
            Assert.AreEqual(1, Rules.ZoneTierAt(knobs, 25f));
        }

        [Test]
        public void TutorialElapsedCapStopsBeforeFirstWarn()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            float cap = Rules.TutorialElapsedCap(knobs);
            float[] snaps = Rules.ZoneSnapTimes(knobs);
            Assert.Less(cap, snaps[0] - knobs.zoneWarnT);
            Assert.IsFalse(Rules.IsZoneWarn(knobs, cap));
        }

        [Test]
        public void ClampIntoNextTierPullsCornerInside()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            Rules.SetArenaScale(knobs.zoneScale0);
            Vector3 corner = Rules.OpeningSpawn(0, knobs.spawnEdge);
            Vector3 safe = Rules.ClampIntoZoneTier(knobs, corner, 2f, 1);
            Vector2 half = Rules.ZoneHalfExtents(knobs, 1);
            Assert.LessOrEqual(Mathf.Abs(safe.x) + 2f, half.x + 0.01f);
            Assert.LessOrEqual(Mathf.Abs(safe.z) + 2f, half.y + 0.01f);
        }

        [Test]
        public void Z6_OpeningSpawnsAreInsetCorners()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            Rules.SetArenaScale(knobs.zoneScale0);
            Vector3 p1 = Rules.OpeningSpawn(0, knobs.spawnEdge);
            Vector3 p2 = Rules.OpeningSpawn(1, knobs.spawnEdge);
            Vector3 p3 = Rules.OpeningSpawn(2, knobs.spawnEdge);
            Vector3 p4 = Rules.OpeningSpawn(3, knobs.spawnEdge);
            Assert.Less(p1.x, 0f);
            Assert.Greater(p1.z, 0f);
            Assert.Greater(p2.x, 0f);
            Assert.Greater(p2.z, 0f);
            Assert.Less(p3.x, 0f);
            Assert.Less(p3.z, 0f);
            Assert.Greater(p4.x, 0f);
            Assert.Less(p4.z, 0f);
            Assert.AreEqual(-knobs.spawnEdge, Rules.ArenaSdf(p1.x, p1.z), 0.08f);
            Assert.AreEqual(-knobs.spawnEdge, Rules.ArenaSdf(p2.x, p2.z), 0.08f);
            Assert.AreEqual(Rules.ArenaHalfWidth - knobs.spawnEdge, Mathf.Abs(p1.x), 1e-4f);
            Assert.AreEqual(Rules.ArenaHalfDepth - knobs.spawnEdge, Mathf.Abs(p1.z), 1e-4f);
        }

        [Test]
        public void Z7_RespawnHomeIfStillInside()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            Rules.SetArenaScale(Rules.LastZoneScale(knobs));
            Vector3 home = new Vector3(-6f, 0f, 8f);
            Assert.IsTrue(Rules.InsideArena(home, 1.8f));
            Vector3 back = Rules.RespawnPoint(home, 1.8f, knobs.spawnEdge);
            Assert.AreEqual(home.x, back.x, 1e-4f);
            Assert.AreEqual(home.z, back.z, 1e-4f);
        }

        [Test]
        public void Z8_RespawnAlongHomeToCenterWhenEaten()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            Rules.SetArenaScale(knobs.zoneScale0);
            Vector3 home = Rules.OpeningSpawn(0, knobs.spawnEdge);
            Rules.SetArenaScale(Rules.LastZoneScale(knobs));
            Assert.IsFalse(Rules.InsideArena(home, 1.8f));
            Vector3 spawn = Rules.RespawnPoint(home, 1.8f, knobs.spawnEdge);
            Assert.IsTrue(Rules.InsideArena(spawn, 1.8f));
            float homeLen = new Vector2(home.x, home.z).magnitude;
            float spawnLen = new Vector2(spawn.x, spawn.z).magnitude;
            Assert.Less(spawnLen, homeLen);
            float cross = home.x * spawn.z - home.z * spawn.x;
            Assert.AreEqual(0f, cross, 0.05f);
        }

        [Test]
        public void Z9_ShieldSaveUsesNewZone()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            Rules.SetArenaScale(Rules.LastZoneScale(knobs));
            BugState bug = new BugState(0, new Vector3(40f, 0f, 0f), knobs);
            bug.buffShieldT = 10f;
            Assert.IsFalse(Rules.InsideArena(bug.position));
            Assert.IsTrue(Rules.TryShieldSave(knobs, bug));
            Assert.IsTrue(Rules.InsideArena(bug.position, bug.radius));
            Assert.Greater(Mathf.Abs(bug.position.x), 1f);
            Assert.AreEqual(0f, bug.buffShieldT);
        }

        [Test]
        public void Z10_NoShieldMeansOut()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            Rules.SetArenaScale(Rules.LastZoneScale(knobs));
            BugState bug = new BugState(0, new Vector3(40f, 0f, 0f), knobs);
            bug.buffShieldT = 0f;
            Assert.IsFalse(Rules.TryShieldSave(knobs, bug));
            Assert.IsFalse(Rules.InsideArena(bug.position));
        }

        [Test]
        public void Z11_ScheduleOffStaysLastTier()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            knobs.zoneSchedule = false;
            Assert.AreEqual(Rules.LastZoneTier, Rules.ZoneTierAt(knobs, 0f));
            Assert.AreEqual(Rules.LastZoneTier, Rules.ZoneTierAt(knobs, 40f));
            Assert.AreEqual(Rules.LastZoneTier, Rules.ZoneTierAt(knobs, 90f));
            Assert.IsFalse(Rules.IsZoneWarn(knobs, 37f));
        }

        [Test]
        public void Z12_SoloPullbackIsSouthNotCorner()
        {
            Rules.SetArenaScale(1f);
            Vector3 pull = Rules.SoloPullbackPoint();
            Assert.AreEqual(0f, pull.x, 1e-4f);
            Assert.Less(pull.z, 0f);
            Vector3 corner = Rules.OpeningSpawn(0, 4f);
            Assert.Greater(Mathf.Abs(corner.x - pull.x), 0.5f);
        }

        [Test]
        public void Z13_PlacePointStaysInCurrentZone()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            Rules.SetArenaScale(knobs.zoneScale1);
            System.Random random = new System.Random(7);
            for (int i = 0; i < 20; i++)
            {
                Vector3 point = Rules.PlacePoint(random, null, null, knobs.heartMinEdge);
                Assert.IsTrue(Rules.InsideArena(point, knobs.heartMinEdge - 0.01f), "point " + point);
                Assert.LessOrEqual(Mathf.Abs(point.x), Rules.ArenaHalfWidth);
                Assert.LessOrEqual(Mathf.Abs(point.z), Rules.ArenaHalfDepth);
            }
        }

        [Test]
        public void Z14_CameraClampStaysInsideZone()
        {
            Rules.SetArenaScale(2f);
            Vector3 c = Rules.ClampCameraCenter(new Vector3(100f, 50f, 80f), 10f, 12f);
            Assert.AreEqual(Rules.ArenaHalfWidth - 0.5f, c.x, 1e-4f);
            Assert.AreEqual(Rules.ArenaHalfDepth - 0.5f, c.z, 1e-4f);
            Assert.AreEqual(50f, c.y, 1e-4f);
        }

        [Test]
        public void Z15_CameraClampPinsWhenViewFillsZone()
        {
            Rules.SetArenaScale(1f);
            Vector3 c = Rules.ClampCameraCenter(new Vector3(8f, 50f, 8f), Rules.ArenaHalfWidth, Rules.ArenaHalfDepth);
            Assert.AreEqual(0f, c.x, 1e-4f);
            Assert.AreEqual(0f, c.z, 1e-4f);
        }

        [Test]
        public void Z17_ClockCountsFullTwoMinutes()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            Assert.AreEqual(120f, Rules.HardStop(knobs), 1e-4f);
            Assert.AreEqual(120f, Rules.RemainingClock(knobs, 0f, false), 1e-4f);
            Assert.AreEqual(120f, Rules.RemainingClock(knobs, 0f, true), 1e-4f);
            Assert.AreEqual(60f, Rules.RemainingClock(knobs, 60f, true), 1e-4f);
            Assert.AreEqual(30f, Rules.RemainingClock(knobs, 90f, true), 1e-4f);
            Assert.AreEqual("2:00", Rules.FormatClock(Rules.RemainingClock(knobs, 0f, true)));
        }

        [Test]
        public void Z18_CompetitiveMatchOverridesDemoTrainingKnobs()
        {
            MatchKnobs demo = Rules.DefaultKnobs();
            demo.regTime = 600f;
            demo.otTime = 0f;
            demo.zoneSchedule = false;

            MatchKnobs ranked = CompetitiveMatch.WithDuration(demo);
            Assert.AreEqual(90f, ranked.regTime, 1e-4f);
            Assert.AreEqual(30f, ranked.otTime, 1e-4f);
            Assert.AreEqual(120f, Rules.HardStop(ranked), 1e-4f);
            Assert.IsTrue(ranked.zoneSchedule);
            Assert.AreEqual("2:00", Rules.FormatClock(Rules.RemainingClock(ranked, 0f, false)));

            MatchKnobs training = TrainingCamp.WithDuration(demo);
            Assert.AreEqual(90f, training.regTime, 1e-4f);
            Assert.AreEqual(30f, training.otTime, 1e-4f);
            Assert.AreEqual(120f, Rules.HardStop(training), 1e-4f);
            Assert.IsTrue(training.zoneSchedule);
            Assert.AreEqual(0, Rules.ZoneTierAt(training, 0f));
            Assert.AreEqual(1, Rules.ZoneTierAt(training, 30f));
            Assert.AreEqual(2, Rules.ZoneTierAt(training, 60f));
            Assert.AreEqual(Rules.LastZoneTier, Rules.ZoneTierAt(training, 90f));
            Assert.AreEqual("2:00", Rules.FormatClock(Rules.RemainingClock(training, 0f, false)));
        }

        [Test]
        public void Z16_TwoSnapMigratesToThreeSnaps()
        {
            MatchKnobs knobs = new MatchKnobs();
            knobs.zoneScale1 = 1.2f;
            knobs.zoneScale2 = 1f;
            knobs.zoneScale3 = 1f;
            knobs.zoneHold0 = 55f;
            knobs.zoneHold1 = 25f;
            knobs.zoneHold2 = 15f;
            knobs.zoneSnapSchema = 2;
            knobs.OnAfterDeserialize();
            Assert.AreEqual(3, knobs.zoneSnapSchema);
            Assert.AreEqual(1.5f, knobs.zoneScale1, 1e-4f);
            Assert.AreEqual(1.2f, knobs.zoneScale2, 1e-4f);
            Assert.AreEqual(1f, knobs.zoneScale3, 1e-4f);
            Assert.AreEqual(25f, knobs.zoneHold0, 1e-4f);
            Assert.AreEqual(25f, knobs.zoneHold1, 1e-4f);
            Assert.AreEqual(25f, knobs.zoneHold2, 1e-4f);
            float[] snaps = Rules.ZoneSnapTimes(knobs);
            Assert.AreEqual(3, snaps.Length);
            Assert.AreEqual(30f, snaps[0], 1e-4f);
            Assert.AreEqual(60f, snaps[1], 1e-4f);
            Assert.AreEqual(90f, snaps[2], 1e-4f);
        }

        [Test]
        public void Z19_OriginalField3WidthMapsToCan()
        {
            Vector2 last = Rules.FieldToArenaHalf(Rules.FieldRulerWidth, 1370f);
            Assert.AreEqual(Rules.DefaultArenaHalfWidth, last.x, 1e-4f);
            Assert.AreEqual(1370f * Rules.MetersPerFieldUnit * 0.5f, last.y, 1e-4f);
        }

        [Test]
        public void Z20_ResizingField3ChangesLastTier()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            Rules.ApplyFieldRects(
                knobs,
                new Vector2(1840f, 2740f),
                new Vector2(1291.3494f, 1921.9755f),
                new Vector2(1104f, 1644f),
                new Vector2(1104f, 1644f));
            Assert.AreEqual(1104f / Rules.FieldRulerWidth, knobs.zoneScale3, 1e-4f);
            Assert.Greater(knobs.zoneScale3, 1f);
            Vector2 last = Rules.ZoneHalfExtents(knobs, Rules.LastZoneTier);
            Assert.AreEqual(Rules.DefaultArenaHalfWidth * (1104f / 920f), last.x, 1e-3f);
            Assert.AreNotEqual(Rules.DefaultArenaHalfWidth, last.x);
        }

        [Test]
        public void Z21_Field0ConvertsThroughSameRuler()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            Rules.ApplyFieldRects(
                knobs,
                new Vector2(1840f, 2740f),
                new Vector2(1291.3494f, 1921.9755f),
                new Vector2(1104f, 1644f),
                new Vector2(920f, 1370f));
            Assert.AreEqual(2f, knobs.zoneScale0, 1e-4f);
            Assert.AreEqual(1291.3494f / Rules.FieldRulerWidth, knobs.zoneScale1, 1e-4f);
            Assert.AreEqual(1.2f, knobs.zoneScale2, 1e-4f);
            Assert.AreEqual(1f, knobs.zoneScale3, 1e-4f);
            Vector2 open = Rules.ZoneHalfExtents(knobs, 0);
            Vector2 last = Rules.ZoneHalfExtents(knobs, Rules.LastZoneTier);
            Assert.AreEqual(42.4f, open.x, 1e-3f);
            Assert.AreEqual(2740f * Rules.MetersPerFieldUnit * 0.5f, open.y, 1e-3f);
            Assert.AreEqual(21.2f, last.x, 1e-3f);
            Vector2 design = Rules.DesignViewHalfExtents(1080f, 1920f);
            Assert.AreEqual(last.x * 1080f / Rules.FieldRulerWidth, design.x, 1e-4f);
            Assert.Greater(design.x, last.x);
            Rules.ApplyZoneTier(knobs, 0);
            Assert.AreEqual(open.x, Rules.ArenaHalfWidth, 1e-3f);
            Assert.AreEqual(open.y, Rules.ArenaHalfDepth, 1e-3f);
        }

        [Test]
        public void ClientApplySnapshotFiresZoneSnappedWhenElapsedCrossesHold()
        {
            MatchKnobs knobs = CompetitiveMatch.WithDuration(Rules.DefaultKnobs());
            MatchController host = CreateMatch();
            host.Configure(MatchRunMode.Offline, MatchController.MaxPlayers, knobs);
            host.ResetMatch(MatchController.MaxPlayers, 20260918);
            host.StartMatch();
            MatchSnapshot before = host.CaptureSnapshot();
            before.elapsed = 29.9f;
            MatchSnapshot after = host.CaptureSnapshot();
            after.elapsed = 30f;

            MatchController client = CreateMatch();
            client.Configure(MatchRunMode.Client, MatchController.MaxPlayers, knobs);
            client.ResetMatch(MatchController.MaxPlayers, 20260918);
            int snapped = -1;
            client.ZoneSnapped += tier => snapped = tier;

            client.ApplySnapshot(before);
            Assert.AreEqual(-1, snapped);
            Assert.AreEqual(0, client.ZoneTier);

            client.ApplySnapshot(after);
            Assert.AreEqual(1, snapped);
            Assert.AreEqual(1, client.ZoneTier);
            Assert.AreEqual(Rules.ZoneHalfExtents(knobs, 1).x, Rules.ArenaHalfWidth, 1e-3f);
        }

        [Test]
        public void ClientApplySnapshotDoesNotRefireSameTier()
        {
            MatchKnobs knobs = CompetitiveMatch.WithDuration(Rules.DefaultKnobs());
            MatchController host = CreateMatch();
            host.Configure(MatchRunMode.Offline, MatchController.MaxPlayers, knobs);
            host.ResetMatch(MatchController.MaxPlayers, 20260918);
            MatchSnapshot snap = host.CaptureSnapshot();
            snap.elapsed = 30f;

            MatchController client = CreateMatch();
            client.Configure(MatchRunMode.Client, MatchController.MaxPlayers, knobs);
            client.ResetMatch(MatchController.MaxPlayers, 20260918);
            int fires = 0;
            client.ZoneSnapped += _ => fires++;
            client.ApplySnapshot(snap);
            client.ApplySnapshot(snap);
            Assert.AreEqual(1, fires);
        }

        [Test]
        public void ClientApplySnapshotWithoutKnobsKeepsExistingKnobs()
        {
            MatchKnobs knobs = CompetitiveMatch.WithDuration(Rules.DefaultKnobs());
            knobs.zoneHold0 = 40f;
            MatchController host = CreateMatch();
            host.Configure(MatchRunMode.Offline, MatchController.MaxPlayers, knobs);
            host.ResetMatch(MatchController.MaxPlayers, 20260918);
            MatchSnapshot withKnobs = host.CaptureSnapshot(true);
            MatchSnapshot withoutKnobs = host.CaptureSnapshot(false);
            Assert.IsNotNull(withKnobs.knobs);
            Assert.IsNull(withoutKnobs.knobs);

            MatchController client = CreateMatch();
            client.Configure(MatchRunMode.Client, MatchController.MaxPlayers, Rules.DefaultKnobs());
            client.ResetMatch(MatchController.MaxPlayers, 20260918);
            client.ApplySnapshot(withKnobs);
            Assert.AreEqual(40f, client.Knobs.zoneHold0, 1e-4f);
            withoutKnobs.elapsed = 1f;
            withoutKnobs.tick = 60;
            client.ApplySnapshot(withoutKnobs);
            Assert.AreEqual(40f, client.Knobs.zoneHold0, 1e-4f);
            Assert.AreEqual(1f, client.Elapsed, 1e-4f);
        }

        [Test]
        public void ClientApplySnapshotReusesPickupSlots()
        {
            MatchKnobs knobs = CompetitiveMatch.WithDuration(Rules.DefaultKnobs());
            MatchController host = CreateMatch();
            host.Configure(MatchRunMode.Offline, MatchController.MaxPlayers, knobs);
            host.ResetMatch(MatchController.MaxPlayers, 20260918);
            MatchSnapshot snap = host.CaptureSnapshot();
            snap.pickups = new[]
            {
                new PickupSnapshot { id = 1, alive = true, kind = "heart", position = Vector3.one },
                new PickupSnapshot { id = 2, alive = true, kind = "shield", position = Vector3.zero }
            };

            MatchController client = CreateMatch();
            client.Configure(MatchRunMode.Client, MatchController.MaxPlayers, knobs);
            client.ResetMatch(MatchController.MaxPlayers, 20260918);
            client.ApplySnapshot(snap);
            var first = client.State.pickups[0];
            client.ApplySnapshot(snap);
            Assert.AreEqual(2, client.State.pickups.Count);
            Assert.AreSame(first, client.State.pickups[0]);
        }

        MatchController CreateMatch()
        {
            GameObject go = new GameObject("ZoneScheduleMatch");
            created.Add(go);
            return go.AddComponent<MatchController>();
        }
    }
}
