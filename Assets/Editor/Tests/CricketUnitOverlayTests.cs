using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class CricketUnitOverlayTests
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
        public void ApplyMotionLaysBarFacingTopDownCamera()
        {
            CricketUnit unit = CreateUnit();
            unit.ApplyMotion(0f, 1f);
            Transform bar = unit.Bar.transform;
            Assert.Greater(Vector3.Dot(bar.TransformDirection(Vector3.up), Vector3.forward), 0.9f);
            Assert.Greater(Vector3.Dot(bar.TransformDirection(Vector3.forward), Vector3.down), 0.9f);
        }

        [Test]
        public void StandaloneBarApplyFacesTopDownCamera()
        {
            GameObject go = new GameObject("StaminaBar");
            created.Add(go);
            StaminaBar bar = go.AddComponent<StaminaBar>();
            bar.Apply(1f, 5, Vector3.zero, 1.8f);
            Assert.Greater(Vector3.Dot(bar.transform.TransformDirection(Vector3.up), Vector3.forward), 0.9f);
            Assert.Greater(Vector3.Dot(bar.transform.TransformDirection(Vector3.forward), Vector3.down), 0.9f);
        }

        [Test]
        public void ApplyOutlineWritesWidthToBodyNotAntenna()
        {
            CricketUnit unit = CreateUnit();
            CricketVisual visual = unit.Body.GetComponent<CricketVisual>();
            Assert.IsNotNull(visual);
            visual.BindHierarchy();
            visual.ApplyOutline();

            float width = new SerializedObject(visual).FindProperty("outlineWidth").floatValue;
            Assert.Greater(width, 0f);

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            SpriteRenderer body = visual.BodyRenderer;
            Assert.IsNotNull(body);
            body.GetPropertyBlock(block);
            Assert.AreEqual(width, block.GetFloat("_OutlineWidth"), 0.01f);

            SpriteRenderer antenna = visual.AntennaRenderer;
            Assert.IsNotNull(antenna);
            antenna.GetPropertyBlock(block);
            Assert.AreEqual(0f, block.GetFloat("_OutlineWidth"), 0.01f);
        }

        [Test]
        public void DividersKeepAuthoredHeight()
        {
            CricketUnit unit = CreateUnit();
            RectTransform divider = unit.Bar.transform.Find("Hud/Dividers/Divider_0") as RectTransform;
            Assert.IsNotNull(divider);
            float authored = divider.sizeDelta.y;
            unit.Bar.ShowAuthored();
            unit.ApplyMotion(0f, 1f);
            Assert.AreEqual(authored, divider.sizeDelta.y, 0.5f);
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
    }
}
