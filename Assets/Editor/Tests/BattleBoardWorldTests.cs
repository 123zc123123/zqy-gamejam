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

            float halfW = Rules.DefaultArenaHalfWidth * opening;
            float halfD = Rules.DefaultArenaHalfDepth * opening;
            Vector2 span = BattleBoardWorld.PlanarSpan(table);
            Vector3 center = BattleBoardWorld.PlanarCenter(table);
            Assert.AreEqual(halfW * 2f, span.x, 0.05f);
            Assert.AreEqual(halfD * 2f, span.y, 0.05f);
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
            Vector3 pos = board.position;
            Vector3 scale = board.localScale;
            Vector2 span = BattleBoardWorld.PlanarSpan(table);
            BattleBoardWorld.Place(board, null, 2f);
            Assert.AreEqual(pos.x, board.position.x, 0.05f);
            Assert.AreEqual(pos.z, board.position.z, 0.05f);
            Assert.AreEqual(scale.x, board.localScale.x, 0.01f);
            Assert.AreEqual(scale.y, board.localScale.y, 0.01f);
            Vector2 again = BattleBoardWorld.PlanarSpan(table);
            Assert.AreEqual(span.x, again.x, 0.05f);
            Assert.AreEqual(span.y, again.y, 0.05f);
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
}
