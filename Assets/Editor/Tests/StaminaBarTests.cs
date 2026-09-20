using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class StaminaBarTests
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
        public void OwnedFillStaysOpaqueWhenGrowingFromEmpty()
        {
            StaminaBar bar = CreateBar();
            bar.ApplyFill(0f, 5);
            Image remain = RemainOf(bar);
            Transform clip = remain.transform.parent;
            Assert.IsTrue(clip.gameObject.activeSelf);
            Assert.IsFalse(remain.enabled);

            bar.ApplyFill(0.04f, 5);
            Assert.IsTrue(clip.gameObject.activeSelf);
            Assert.IsTrue(remain.enabled);
            Assert.AreEqual(1f, remain.color.a, 0.001f);
        }

        [Test]
        public void ShortOwnedFillSkipsSlicedCap()
        {
            StaminaBar bar = CreateBar();
            bar.ApplyFill(0.02f, 5);
            Image remain = RemainOf(bar);
            Assert.IsTrue(remain.enabled);
            Assert.AreEqual(1f, remain.color.a, 0.001f);
            Sprite sprite = remain.sprite;
            if (sprite == null || sprite.border.x < 1f) return;
            float clipW = ((RectTransform)remain.transform.parent).sizeDelta.x;
            if (clipW < sprite.border.x)
                Assert.Less(remain.rectTransform.anchoredPosition.x, -1f);
        }

        [Test]
        public void OwnedFillStaysOpaqueAtLowAndFull()
        {
            StaminaBar bar = CreateBar();
            bar.ApplyFill(0.08f, 5);
            Assert.AreEqual(1f, RemainOf(bar).color.a, 0.001f);

            bar.ApplyFill(1f, 5);
            Assert.AreEqual(1f, RemainOf(bar).color.a, 0.001f);
            Assert.AreEqual(0f, RemainOf(bar).rectTransform.anchoredPosition.x, 0.01f);
        }

        [Test]
        public void PendingFillIsTranslucentOwnedFillIsOpaque()
        {
            StaminaBar bar = CreateBar();
            bar.ApplyFill(0.6f, 5, 0.2f);
            Image remain = RemainOf(bar);
            Image pending = PendingOf(bar);
            Assert.IsTrue(remain.enabled);
            Assert.IsTrue(pending.enabled);
            Assert.AreEqual(1f, remain.color.a, 0.001f);
            Assert.Less(pending.color.a, 0.5f);
        }

        StaminaBar CreateBar()
        {
            GameObject prefab = Resources.Load<GameObject>("Battle/Entities/Prefabs/StaminaBar");
            GameObject go = prefab != null ? Object.Instantiate(prefab) : new GameObject("StaminaBar");
            created.Add(go);
            StaminaBar bar = go.GetComponent<StaminaBar>();
            if (bar == null) bar = go.AddComponent<StaminaBar>();
            bar.EnsureReady();
            return bar;
        }

        static Image RemainOf(StaminaBar bar)
        {
            Transform t = bar.transform.Find("Hud/FillArea/RemainClip/Remain");
            Assert.IsNotNull(t);
            return t.GetComponent<Image>();
        }

        static Image PendingOf(StaminaBar bar)
        {
            Transform t = bar.transform.Find("Hud/FillArea/PendingClip/Pending");
            Assert.IsNotNull(t);
            return t.GetComponent<Image>();
        }
    }
}
