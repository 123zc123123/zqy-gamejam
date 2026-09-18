using NUnit.Framework;
using UnityEngine;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class CoverFitTests
    {
        const float SpriteAspect = 1080f / 1920f;

        [Test]
        public void DesignPhoneKeepsExactSize()
        {
            Vector2 sizeDelta;
            Assert.IsTrue(CoverFit.TryLayout(new Vector2(1080f, 1920f), SpriteAspect, out sizeDelta));
            Assert.AreEqual(0f, sizeDelta.x, 1e-3f);
            Assert.AreEqual(0f, sizeDelta.y, 1e-3f);
        }

        [Test]
        public void LongPhoneCropsSides()
        {
            Vector2 sizeDelta;
            Assert.IsTrue(CoverFit.TryLayout(new Vector2(886f, 1920f), SpriteAspect, out sizeDelta));
            Assert.AreEqual(1080f - 886f, sizeDelta.x, 1e-3f);
            Assert.AreEqual(0f, sizeDelta.y, 1e-3f);
            Assert.GreaterOrEqual(886f + sizeDelta.x, 1080f - 0.1f);
        }

        [Test]
        public void EditorNarrowCropsSides()
        {
            Vector2 sizeDelta;
            Assert.IsTrue(CoverFit.TryLayout(new Vector2(864f, 1920f), SpriteAspect, out sizeDelta));
            Assert.AreEqual(1080f - 864f, sizeDelta.x, 1e-3f);
            Assert.AreEqual(0f, sizeDelta.y, 1e-3f);
        }

        [Test]
        public void WidePhoneCropsTopAndBottom()
        {
            Vector2 sizeDelta;
            Assert.IsTrue(CoverFit.TryLayout(new Vector2(1440f, 1920f), SpriteAspect, out sizeDelta));
            Assert.AreEqual(0f, sizeDelta.x, 1e-3f);
            Assert.AreEqual(1440f / SpriteAspect - 1920f, sizeDelta.y, 1e-3f);
            Assert.Greater(sizeDelta.y, 0f);
        }

        [Test]
        public void TinyParentFails()
        {
            Vector2 sizeDelta;
            Assert.IsFalse(CoverFit.TryLayout(new Vector2(1f, 1f), SpriteAspect, out sizeDelta));
        }
    }
}
