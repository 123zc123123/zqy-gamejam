using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class CollectFeelTests
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
        public void BounceCurveStartsAndEndsAtRest()
        {
            Assert.AreEqual(1f, UiButtonBounce.ScaleAt(0f), 1e-4f);
            Assert.AreEqual(1f, UiButtonBounce.ScaleAt(1f), 1e-4f);
            Assert.AreEqual(1.22f, UiButtonBounce.ScaleAt(0.22f), 1e-4f);
            Assert.AreEqual(0.9f, UiButtonBounce.ScaleAt(0.48f), 1e-4f);
            Assert.AreEqual(1.06f, UiButtonBounce.ScaleAt(0.74f), 1e-4f);
            Assert.Greater(UiButtonBounce.ScaleAt(0.11f), 1f);
            Assert.Less(UiButtonBounce.ScaleAt(0.35f), 1.22f);
        }

        [Test]
        public void FlyPathStartsAtPortraitAndEndsAtBackpack()
        {
            Vector3 start = new Vector3(540f, 960f, 0f);
            Vector3 end = new Vector3(980f, 320f, 0f);
            Vector3 control = FinestRevealFx.ArcControl(start, end);
            Assert.AreEqual(start, FinestRevealFx.QuadBezier(start, control, end, 0f));
            Assert.AreEqual(end, FinestRevealFx.QuadBezier(start, control, end, 1f));
            Vector3 mid = FinestRevealFx.QuadBezier(start, control, end, 0.5f);
            Assert.Greater(mid.y, (start.y + end.y) * 0.5f);
        }

        [Test]
        public void FlyProgressHitsBagBeforeBounce()
        {
            Assert.AreEqual(0f, FinestRevealFx.FlyProgress(0f), 1e-4f);
            Assert.AreEqual(1f, FinestRevealFx.FlyProgress(FinestRevealFx.CollectFlySeconds), 1e-4f);
            Assert.AreEqual(1f, FinestRevealFx.FlyProgress(2f), 1e-4f);
            Assert.Less(FinestRevealFx.FlyProgress(FinestRevealFx.CollectFlySeconds * 0.8f), FinestRevealFx.CollectBounceAt);
            Assert.GreaterOrEqual(FinestRevealFx.FlyProgress(FinestRevealFx.CollectFlySeconds * 0.97f), FinestRevealFx.CollectBounceAt);
        }

        [Test]
        public void WorldCenterRejectsMissingOrEmptyTarget()
        {
            Vector3 world;
            Assert.IsFalse(FinestRevealFx.TryWorldCenter(null, out world));

            GameObject go = new GameObject("EmptyBag", typeof(RectTransform));
            created.Add(go);
            Assert.IsFalse(FinestRevealFx.TryWorldCenter(go.GetComponent<RectTransform>(), out world));
        }
    }
}
