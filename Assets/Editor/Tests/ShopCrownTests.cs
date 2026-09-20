using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class ShopCrownTests
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
        public void TopLeftCardIsHighestThenLeftmost()
        {
            GameObject root = new GameObject("Shop", typeof(RectTransform));
            created.Add(root);
            CreateCard(root.transform, -214f, -374f);
            CreateCard(root.transform, 248f, 180f);
            CreateCard(root.transform, -214f, 180f);
            CreateCard(root.transform, 248f, -374f);
            Transform hit = ShopController.FindTopLeftParchment(root.transform);
            Assert.IsNotNull(hit);
            Assert.AreEqual(new Vector2(-214f, 180f), ((RectTransform)hit).anchoredPosition);
        }

        [Test]
        public void ApplyCrownIsSkippedByBindHierarchy()
        {
            CricketUnit unit = CreateUnit();
            CricketVisual visual = unit.Body.GetComponent<CricketVisual>();
            Assert.IsNotNull(visual);
            visual.BindHierarchy();
            visual.ApplyCrown(true);

            Transform crown = FindNamed(visual.transform, CricketVisual.CrownObjectName);
            Assert.IsNotNull(crown);
            Assert.IsNotNull(crown.GetComponent<CricketAccessory>());
            SpriteRenderer renderer = crown.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer);
            Assert.IsTrue(renderer.enabled);

            visual.BindHierarchy();
            visual.ApplyOutline();
            Assert.IsTrue(renderer.enabled);
            Assert.AreSame(crown.GetComponent<CricketAccessory>(), renderer.GetComponent<CricketAccessory>());
        }

        [Test]
        public void ApplyCrownOffHidesAccessory()
        {
            CricketUnit unit = CreateUnit();
            CricketVisual visual = unit.Body.GetComponent<CricketVisual>();
            visual.BindHierarchy();
            visual.ApplyCrown(true);
            visual.ApplyCrown(false);
            Transform crown = FindNamed(visual.transform, CricketVisual.CrownObjectName);
            Assert.IsNotNull(crown);
            Assert.IsFalse(crown.GetComponent<SpriteRenderer>().enabled);
        }

        CricketUnit CreateUnit()
        {
            GameObject prefab = Resources.Load<GameObject>("Battle/Entities/Prefabs/CricketUnit");
            Assert.IsNotNull(prefab);
            GameObject go = Object.Instantiate(prefab);
            created.Add(go);
            CricketUnit unit = go.GetComponent<CricketUnit>();
            Assert.IsNotNull(unit);
            unit.Bind();
            unit.CaptureAuthored();
            return unit;
        }

        static void CreateCard(Transform parent, float x, float y)
        {
            GameObject go = new GameObject("parchment-scroll-card", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(x, y);
        }

        static Transform FindNamed(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamed(root.GetChild(i), objectName);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
