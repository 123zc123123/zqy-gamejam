using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class BattleBoardWorldTests
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
            GameObject world = GameObject.Find(BattleBoardWorld.RootName);
            if (world != null) Object.DestroyImmediate(world);
            Rules.ResetArenaSize();
        }

        [Test]
        public void TableMapsToOpeningArenaAndStaysPut()
        {
            RectTransform board;
            RectTransform table;
            CreateBoard(1840f, 2740f, out board, out table);
            GameObject camGo = new GameObject("BattleCam");
            created.Add(camGo);
            camGo.transform.position = new Vector3(0f, 50f, 0f);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;

            const float opening = 2f;
            BattleBoardWorld.Place(board, cam, opening);

            AssertUniformScale(board);
            float halfW = Rules.DefaultArenaHalfWidth * opening;
            float halfD = Rules.DefaultArenaHalfDepth * opening;
            AssertTableMatchesArena(table, halfW, halfD);
            Vector2 span = BattleBoardWorld.PlanarSpan(table);
            Vector3 center = BattleBoardWorld.PlanarCenter(table);
            Assert.AreEqual(0f, center.x, 0.05f);
            Assert.AreEqual(0f, center.z, 0.05f);
            Assert.AreEqual(BattleBoardWorld.RootName, board.parent.name);
            Assert.IsFalse(board.IsChildOf(camGo.transform));

            Vector3 boardPos = board.position;
            Vector2 tableSpan = span;
            camGo.transform.position = new Vector3(18f, 50f, 11f);
            cam.orthographicSize = 12f;
            Assert.AreEqual(boardPos.x, board.position.x, 1e-4f);
            Assert.AreEqual(boardPos.z, board.position.z, 1e-4f);
            Vector2 after = BattleBoardWorld.PlanarSpan(table);
            Assert.AreEqual(tableSpan.x, after.x, 1e-4f);
            Assert.AreEqual(tableSpan.y, after.y, 1e-4f);
        }

        [Test]
        public void PlaceIsIdempotent()
        {
            RectTransform board;
            RectTransform table;
            CreateBoard(1840f, 2740f, out board, out table);
            BattleBoardWorld.Place(board, null, 2f);
            AssertUniformScale(board);
            Vector3 pos = board.position;
            Vector3 scale = board.localScale;
            Vector2 span = BattleBoardWorld.PlanarSpan(table);
            BattleBoardWorld.Place(board, null, 2f);
            AssertUniformScale(board);
            Assert.AreEqual(pos.x, board.position.x, 0.05f);
            Assert.AreEqual(pos.z, board.position.z, 0.05f);
            Assert.AreEqual(scale.x, board.localScale.x, 0.01f);
            Assert.AreEqual(scale.y, board.localScale.y, 0.01f);
            Vector2 again = BattleBoardWorld.PlanarSpan(table);
            Assert.AreEqual(span.x, again.x, 0.05f);
            Assert.AreEqual(span.y, again.y, 0.05f);
        }

        [Test]
        public void OddTableAspectFitsFieldThenWorldStaysUniform()
        {
            RectTransform board;
            RectTransform table;
            CreateBoard(1000f, 3000f, out board, out table);
            const float opening = 2f;
            BattleBoardWorld.Place(board, null, opening);
            AssertUniformScale(board);
            Assert.AreEqual(1f, table.localScale.x, 1e-4f);
            Assert.AreEqual(1f, table.localScale.y, 1e-4f);
            Assert.AreEqual(1f, table.localScale.z, 1e-4f);
            AssertTableMatchesArena(
                table,
                Rules.DefaultArenaHalfWidth * opening,
                Rules.DefaultArenaHalfDepth * opening);
            Assert.AreEqual(1000f, table.sizeDelta.x, 0.05f);
            Assert.AreEqual(1000f * Rules.DefaultArenaHalfDepth / Rules.DefaultArenaHalfWidth, table.sizeDelta.y, 0.05f);
        }

        [Test]
        public void TableFollowsFieldConvertedOpeningNotForcedTwoToThree()
        {
            MatchKnobs knobs = Rules.DefaultKnobs();
            Rules.ApplyFieldRects(
                knobs,
                new Vector2(1840f, 2740f),
                new Vector2(1291f, 1922f),
                new Vector2(1104f, 1644f),
                new Vector2(920f, 1370f));
            Vector2 open = Rules.ZoneHalfExtents(knobs, 0);

            RectTransform board;
            RectTransform table;
            CreateBoard(1840f, 2740f, out board, out table);
            BattleBoardWorld.Place(board, null, open.x, open.y);

            AssertUniformScale(board);
            AssertTableMatchesArena(table, open.x, open.y);
            Assert.AreEqual(1840f, table.sizeDelta.x, 0.05f);
            Assert.AreEqual(2740f, table.sizeDelta.y, 0.05f);
        }

        [Test]
        public void PlaceDoesNotRewriteBgRect()
        {
            RectTransform board;
            RectTransform table;
            CreateBoard(1840f, 2740f, out board, out table);
            GameObject bgGo = new GameObject("bg", typeof(RectTransform));
            RectTransform bg = bgGo.GetComponent<RectTransform>();
            bg.SetParent(board, false);
            bg.anchorMin = bg.anchorMax = bg.pivot = new Vector2(0.5f, 0.5f);
            bg.sizeDelta = new Vector2(7884f, 14016f);
            bg.anchoredPosition = new Vector2(0f, -409f);
            bg.localScale = Vector3.one;

            BattleBoardWorld.Place(board, null, 2f);
            Assert.AreEqual(7884f, bg.sizeDelta.x, 0.05f);
            Assert.AreEqual(14016f, bg.sizeDelta.y, 0.05f);
            Assert.AreEqual(1f, bg.localScale.x, 1e-4f);
            Assert.AreEqual(1f, bg.localScale.y, 1e-4f);
            Assert.AreEqual(-409f, bg.anchoredPosition.y, 0.05f);
            AssertUniformScale(board);
        }

        static void AssertUniformScale(RectTransform board)
        {
            Vector3 s = board.localScale;
            Assert.AreEqual(s.x, s.y, 1e-4f);
            Assert.AreEqual(s.x, s.z, 1e-4f);
        }

        static void AssertTableMatchesArena(RectTransform table, float halfW, float halfD)
        {
            Vector2 span = BattleBoardWorld.PlanarSpan(table);
            Assert.AreEqual(halfW * 2f, span.x, 0.05f);
            Assert.AreEqual(halfD * 2f, span.y, 0.05f);
            Assert.AreEqual(halfD / halfW, table.sizeDelta.y / table.sizeDelta.x, 1e-4f);
        }

        void CreateBoard(float tableW, float tableH, out RectTransform board, out RectTransform table)
        {
            GameObject boardGo = new GameObject("Board", typeof(RectTransform));
            created.Add(boardGo);
            board = boardGo.GetComponent<RectTransform>();
            board.anchorMin = board.anchorMax = board.pivot = new Vector2(0.5f, 0.5f);
            board.sizeDelta = new Vector2(2304f, 4096f);
            board.anchoredPosition = Vector2.zero;
            board.localScale = Vector3.one;
            board.localRotation = Quaternion.identity;

            GameObject tableGo = new GameObject("BattleTable", typeof(RectTransform));
            table = tableGo.GetComponent<RectTransform>();
            table.SetParent(board, false);
            table.anchorMin = table.anchorMax = table.pivot = new Vector2(0.5f, 0.5f);
            table.sizeDelta = new Vector2(tableW, tableH);
            table.anchoredPosition = Vector2.zero;
            table.localScale = Vector3.one;
            table.localRotation = Quaternion.identity;
        }
    }

    [TestFixture]
    public sealed class BattleViewTextureTests
    {
        [Test]
        public void RenderTextureKeepsViewAspectWhenCapping()
        {
            Vector2Int pixels = BattleHudBinder.FitRenderTextureSize(1440, 2560, 2048);
            Assert.AreEqual(2048f / 1152f, pixels.y / (float)pixels.x, 1e-3f);
            Assert.AreEqual(2560f / 1440f, pixels.y / (float)pixels.x, 1e-3f);
            Assert.LessOrEqual(Mathf.Max(pixels.x, pixels.y), 2048);
        }

        [Test]
        public void RenderTextureBelowCapStaysNative()
        {
            Vector2Int pixels = BattleHudBinder.FitRenderTextureSize(1440, 2560, 4096);
            Assert.AreEqual(1440, pixels.x);
            Assert.AreEqual(2560, pixels.y);
        }

        [Test]
        public void IndependentAxisCapWouldStretchAndIsRejected()
        {
            Vector2Int broken = new Vector2Int(Mathf.Min(1440, 2048), Mathf.Min(2560, 2048));
            Assert.Greater(Mathf.Abs(broken.y / (float)broken.x - 2560f / 1440f), 0.01f);
            Vector2Int fixedSize = BattleHudBinder.FitRenderTextureSize(1440, 2560, 2048);
            Assert.AreEqual(2560f / 1440f, fixedSize.y / (float)fixedSize.x, 1e-3f);
        }

        [Test]
        public void DesignSizeStaysPrefab1080By1920()
        {
            RectTransform parent;
            RectTransform design;
            CreateDesign(1080f, 2400f, out parent, out design);
            BattleHudBinder.FitDesignToParent(design);
            Assert.AreEqual(1080f, design.sizeDelta.x, 0.05f);
            Assert.AreEqual(1920f, design.sizeDelta.y, 0.05f);
            Assert.AreEqual(1f, design.localScale.x, 1e-4f);
            Object.DestroyImmediate(parent.gameObject);
        }

        static void CreateDesign(float parentW, float parentH, out RectTransform parent, out RectTransform design)
        {
            GameObject parentGo = new GameObject("CanvasRoot", typeof(RectTransform));
            parent = parentGo.GetComponent<RectTransform>();
            parent.anchorMin = parent.anchorMax = parent.pivot = new Vector2(0.5f, 0.5f);
            parent.sizeDelta = new Vector2(parentW, parentH);
            GameObject designGo = new GameObject("defaultDesignSize", typeof(RectTransform));
            design = designGo.GetComponent<RectTransform>();
            design.SetParent(parent, false);
        }
    }

    [TestFixture]
    public sealed class BattleCameraOpeningTests
    {
        GameObject camGo;

        [TearDown]
        public void TearDown()
        {
            if (camGo != null) Object.DestroyImmediate(camGo);
            camGo = null;
        }

        [Test]
        public void OpeningPanoramaFitsSkinnyScreenByHeight()
        {
            BattleCamera cam = CreateCam();
            float halfW;
            float halfD;
            BindPanorama(cam, 20f, 40f, out halfW, out halfD);
            cam.UseDesignFrame(1080f, 2400f);
            cam.FrameOpeningPanorama();
            Assert.AreEqual(halfD, cam.Cam.orthographicSize, 1e-3f);
        }

        [Test]
        public void PlayViewUsesDesignWidthNotField3()
        {
            BattleCamera cam = CreateCam();
            cam.UseDesignFrame(1080f, 1920f);
            cam.UsePlayDesign(1080f, 1920f);
            cam.FollowLocalPlayer(null, 0);
            Vector2 last = Rules.ZoneHalfExtents(null, Rules.LastZoneTier);
            Vector2 view = Rules.DesignViewHalfExtents(1080f, 1920f);
            Assert.Greater(view.x, last.x);
            Assert.AreEqual(last.x * 1080f / Rules.FieldRulerWidth, view.x, 1e-4f);
            float aspect = 1080f / 1920f;
            float expectedSize = Mathf.Max(view.y, view.x / aspect);
            Assert.AreEqual(expectedSize, cam.Cam.orthographicSize, 1e-3f);
        }

        [Test]
        public void OpeningPanoramaFitsFatScreenByWidth()
        {
            BattleCamera cam = CreateCam();
            float halfW;
            float halfD;
            BindPanorama(cam, 20f, 40f, out halfW, out halfD);
            cam.UseDesignFrame(1080f, 1440f);
            cam.FrameOpeningPanorama();
            float aspect = 1080f / 1440f;
            Assert.AreEqual(halfW / aspect, cam.Cam.orthographicSize, 1e-3f);
        }

        BattleCamera CreateCam()
        {
            camGo = new GameObject("BattleCam");
            Camera unityCam = camGo.AddComponent<Camera>();
            unityCam.orthographic = true;
            return camGo.AddComponent<BattleCamera>();
        }

        static void BindPanorama(BattleCamera cam, float halfW, float halfD, out float outW, out float outD)
        {
            float tableW = Rules.DefaultArenaHalfWidth * 2f;
            float tableH = Rules.DefaultArenaHalfDepth * 2f;
            cam.UsePanoramaArt(
                new Vector2(tableW, tableH),
                new Vector2(tableW * (halfW / Rules.DefaultArenaHalfWidth), tableH * (halfD / Rules.DefaultArenaHalfDepth)),
                Vector2.zero);
            outW = halfW;
            outD = halfD;
        }
    }
}
