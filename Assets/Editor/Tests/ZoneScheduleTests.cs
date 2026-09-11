using NUnit.Framework;
using UnityEngine;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class ZoneScheduleTests
    {
        [TearDown]
        public void TearDown()
        {
            Rules.ResetArenaSize();
        }

        [Test]
        public void Z1_DefaultSnapsAt60_90()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            float[] snaps = Rules.ZoneSnapTimes(knobs);
            Assert.AreEqual(2, snaps.Length);
            Assert.AreEqual(60f, snaps[0], 1e-4f);
            Assert.AreEqual(90f, snaps[1], 1e-4f);
        }

        [Test]
        public void Z2_SnapIsInstantNoLerp()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            Assert.AreEqual(0, Rules.ZoneTierAt(knobs, 59.9f));
            Assert.AreEqual(2f, Rules.ZoneScaleOf(knobs, Rules.ZoneTierAt(knobs, 59.9f)), 1e-4f);
            Assert.AreEqual(1, Rules.ZoneTierAt(knobs, 60f));
            Assert.AreEqual(1.2f, Rules.ZoneScaleOf(knobs, Rules.ZoneTierAt(knobs, 60f)), 1e-4f);
        }

        [Test]
        public void Z3_WarnKeepsCurrentSdf()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            Assert.IsTrue(Rules.IsZoneWarn(knobs, 55f));
            Assert.IsTrue(Rules.IsZoneWarn(knobs, 59.9f));
            Assert.IsFalse(Rules.IsZoneWarn(knobs, 54.9f));
            Assert.AreEqual(0, Rules.ZoneTierAt(knobs, 57f));
            Rules.ApplyZoneAt(knobs, 57f);
            Vector3 inCurrentOutNext = new Vector3(35f, 0f, 0f);
            Assert.IsTrue(Rules.InsideArena(inCurrentOutNext));
            Rules.SetArenaScale(Rules.ZoneScaleOf(knobs, 1));
            Assert.IsFalse(Rules.InsideArena(inCurrentOutNext));
        }

        [Test]
        public void Z4_SecondSnapIsRageAndNoThird()
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
            float aabbCorner = Mathf.Sqrt(
                (Rules.ArenaHalfWidth - knobs.spawnEdge) * (Rules.ArenaHalfWidth - knobs.spawnEdge)
                + (Rules.ArenaHalfDepth - knobs.spawnEdge) * (Rules.ArenaHalfDepth - knobs.spawnEdge));
            float fromOrigin = new Vector2(p1.x, p1.z).magnitude;
            Assert.Less(fromOrigin, aabbCorner - 0.5f);
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
        public void Z16_OldFourTierMigratesToTwoSnaps()
        {
            MatchKnobs knobs = new MatchKnobs();
            knobs.zoneScale1 = 1.5f;
            knobs.zoneScale2 = 1.2f;
            knobs.zoneScale3 = 1f;
            knobs.zoneHold0 = 35f;
            knobs.zoneHold1 = 25f;
            knobs.zoneHold2 = 15f;
            knobs.zoneSnapSchema = 0;
            knobs.OnAfterDeserialize();
            Assert.AreEqual(2, knobs.zoneSnapSchema);
            Assert.AreEqual(1.2f, knobs.zoneScale1, 1e-4f);
            Assert.AreEqual(1f, knobs.zoneScale2, 1e-4f);
            Assert.AreEqual(55f, knobs.zoneHold0, 1e-4f);
            Assert.AreEqual(25f, knobs.zoneHold1, 1e-4f);
            Assert.AreEqual(0, Rules.ZoneTierAt(knobs, 40f));
            float[] snaps = Rules.ZoneSnapTimes(knobs);
            Assert.AreEqual(2, snaps.Length);
            Assert.AreEqual(60f, snaps[0], 1e-4f);
            Assert.AreEqual(90f, snaps[1], 1e-4f);
        }
    }
}
