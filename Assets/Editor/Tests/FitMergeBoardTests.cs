using NUnit.Framework;
using UnityEngine;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class FitMergeBoardTests
    {
        static readonly Vector2 Design = new Vector2(952f, 1193f);

        [Test]
        public void SidePaddingMatchesFrameInsetOnReferenceCanvas()
        {
            Assert.AreEqual(64f, FitMergeBoard.SidePadding(1080f, 952f), 1e-4f);
        }

        [Test]
        public void SidePaddingIsZeroWhenFrameFillsWidth()
        {
            Assert.AreEqual(0f, FitMergeBoard.SidePadding(952f, 952f), 1e-4f);
        }

        [Test]
        public void DesignPhoneKeepsNearFullWidth()
        {
            Vector2 pos;
            float scale;
            float side = FitMergeBoard.SidePadding(1080f, Design.x);
            Assert.IsTrue(FitMergeBoard.TryLayout(
                new Vector2(1080f, 1920f), Design,
                320f, 240f, side, side, out pos, out scale));
            Assert.AreEqual(1f, scale, 1e-4f);
            Assert.AreEqual(0f, pos.x, 1e-3f);
            Assert.Less(pos.y, 0f);
        }

        [Test]
        public void LongPhoneShrinksToWidthAndKeepsAspect()
        {
            Vector2 pos;
            float scale;
            float side = FitMergeBoard.SidePadding(1080f, Design.x);
            Assert.IsTrue(FitMergeBoard.TryLayout(
                new Vector2(886f, 1920f), Design,
                320f, 240f, side, side, out pos, out scale));
            float expected = (886f - side * 2f) / Design.x;
            Assert.AreEqual(expected, scale, 1e-4f);
            Assert.Less(scale, 1f);
            Assert.AreEqual(0f, pos.x, 1e-3f);
            float visualW = Design.x * scale;
            float visualH = Design.y * scale;
            Assert.LessOrEqual(visualW, 886f - side * 2f + 0.1f);
            Assert.LessOrEqual(visualH, 1920f - 560f + 0.1f);
        }

        [Test]
        public void WidePhoneStopsAtHeight()
        {
            Vector2 pos;
            float scale;
            float side = FitMergeBoard.SidePadding(1080f, Design.x);
            Assert.IsTrue(FitMergeBoard.TryLayout(
                new Vector2(1440f, 1920f), Design,
                320f, 240f, side, side, out pos, out scale));
            float heightFit = (1920f - 560f) / Design.y;
            Assert.AreEqual(heightFit, scale, 1e-4f);
            Assert.AreEqual(0f, pos.x, 1e-3f);
        }

        [Test]
        public void ExtraBottomPaddingShiftsBoardUp()
        {
            Vector2 equal;
            Vector2 heavierBottom;
            float scale;
            Assert.IsTrue(FitMergeBoard.TryLayout(
                new Vector2(1080f, 1920f), Design,
                200f, 200f, 64f, 64f, out equal, out scale));
            Assert.IsTrue(FitMergeBoard.TryLayout(
                new Vector2(1080f, 1920f), Design,
                200f, 400f, 64f, 64f, out heavierBottom, out scale));
            Assert.AreEqual(0f, equal.y, 1e-3f);
            Assert.Greater(heavierBottom.y, equal.y);
        }

        [Test]
        public void TinyParentFails()
        {
            Vector2 pos;
            float scale;
            Assert.IsFalse(FitMergeBoard.TryLayout(
                new Vector2(8f, 8f), Design,
                320f, 240f, 64f, 64f, out pos, out scale));
        }
    }
}
