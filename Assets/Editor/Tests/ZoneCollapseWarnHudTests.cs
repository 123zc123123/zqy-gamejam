using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class ZoneCollapseWarnHudTests
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
        public void EnterStartsTransparentBelowRest()
        {
            float alpha, y;
            ZoneCollapseWarnHud.Sample(true, 0f, out alpha, out y);
            Assert.AreEqual(0f, alpha, 1e-4f);
            Assert.AreEqual(-ZoneCollapseWarnHud.FloatPx, y, 1e-4f);
        }

        [Test]
        public void EnterEndsOpaqueAtRest()
        {
            float alpha, y;
            ZoneCollapseWarnHud.Sample(true, 1f, out alpha, out y);
            Assert.AreEqual(1f, alpha, 1e-4f);
            Assert.AreEqual(0f, y, 1e-4f);
        }

        [Test]
        public void ExitEndsTransparentAboveRest()
        {
            float alpha, y;
            ZoneCollapseWarnHud.Sample(false, 1f, out alpha, out y);
            Assert.AreEqual(0f, alpha, 1e-4f);
            Assert.AreEqual(ZoneCollapseWarnHud.FloatPx, y, 1e-4f);
        }

        [Test]
        public void ExitStartsOpaqueAtRest()
        {
            float alpha, y;
            ZoneCollapseWarnHud.Sample(false, 0f, out alpha, out y);
            Assert.AreEqual(1f, alpha, 1e-4f);
            Assert.AreEqual(0f, y, 1e-4f);
        }

        [Test]
        public void BreathPeaksAtPeriodEndsAndDipsMidway()
        {
            Assert.AreEqual(1f, ZoneCollapseWarnHud.Breath(0f), 1e-4f);
            Assert.AreEqual(ZoneCollapseWarnHud.BreathMin, ZoneCollapseWarnHud.Breath(0.5f), 1e-4f);
            Assert.AreEqual(1f, ZoneCollapseWarnHud.Breath(1f), 1e-4f);
            Assert.AreEqual(1f, ZoneCollapseWarnHud.Breath(2f), 1e-4f);
        }

        [Test]
        public void LabelIs72RedBaibian()
        {
            GameObject root = new GameObject("HudRoot", typeof(RectTransform));
            created.Add(root);
            ZoneCollapseWarnHud hud = root.AddComponent<ZoneCollapseWarnHud>();
            hud.Bind(null, root.GetComponent<RectTransform>());
            hud.Show();

            Transform node = root.transform.Find("ZoneCollapseWarn");
            Assert.IsNotNull(node);
            TextMeshProUGUI label = node.GetComponent<TextMeshProUGUI>();
            Assert.IsNotNull(label);
            Assert.AreEqual(ZoneCollapseWarnHud.Message, label.text);
            Assert.AreEqual(ZoneCollapseWarnHud.FontSize, label.fontSize, 1e-4f);
            Assert.AreEqual(FontStyles.Normal, label.fontStyle);
            Assert.AreEqual(Color.red, label.color);
            Material baibian = Resources.Load<Material>(ZoneCollapseWarnHud.BaibianMat);
            if (baibian != null)
                Assert.AreEqual(baibian, label.fontSharedMaterial);
        }
    }
}
