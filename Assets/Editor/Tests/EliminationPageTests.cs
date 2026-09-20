using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class EliminationPageTests
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
        public void RankIconsLiveUnderCommonTextures()
        {
            for (int place = 1; place <= 4; place++)
            {
                Assert.IsNotNull(
                    Resources.Load<Sprite>("Common/Textures/rank" + place + "-icon"),
                    "missing Common/Textures/rank" + place + "-icon");
                Assert.IsNull(Resources.Load<Sprite>("Settlement/Textures/rank" + place + "-icon"));
            }
        }

        [Test]
        public void BindSwapsCenterMedalToActualPlace()
        {
            EliminationPage page = CreatePage();
            MatchController match = CreateMatch();
            match.State.place[0] = 3;
            match.State.matchScore[0] = 12;

            page.Bind(match, 0);

            Image medal = FindNamed(page.transform, "Rank").GetComponent<Image>();
            Assert.AreEqual(Resources.Load<Sprite>("Common/Textures/rank3-icon"), medal.sprite);
            Assert.AreEqual("10", FindNamed(page.transform, "PlaceScore").GetComponent<TMP_Text>().text);
            Assert.AreEqual("12", FindNamed(page.transform, "KillScore").GetComponent<TMP_Text>().text);
        }

        [Test]
        public void BindKeepsFourthMedalWhenPlayerIsFourth()
        {
            EliminationPage page = CreatePage();
            MatchController match = CreateMatch();
            match.State.place[0] = 4;

            page.Bind(match, 0);

            Image medal = FindNamed(page.transform, "Rank").GetComponent<Image>();
            Assert.AreEqual(Resources.Load<Sprite>("Common/Textures/rank4-icon"), medal.sprite);
            Assert.AreEqual("5", FindNamed(page.transform, "PlaceScore").GetComponent<TMP_Text>().text);
        }

        EliminationPage CreatePage()
        {
            GameObject prefab = Resources.Load<GameObject>("Settlement/Prefabs/Chuju");
            Assert.IsNotNull(prefab);
            GameObject go = Object.Instantiate(prefab);
            created.Add(go);
            EliminationPage page = go.GetComponent<EliminationPage>();
            if (page == null) page = go.AddComponent<EliminationPage>();
            return page;
        }

        MatchController CreateMatch()
        {
            GameObject go = new GameObject("EliminationMatch");
            created.Add(go);
            MatchController match = go.AddComponent<MatchController>();
            match.Configure(MatchRunMode.Offline, MatchController.MaxPlayers, Rules.DefaultKnobs());
            match.ResetMatch(MatchController.MaxPlayers, 20260920);
            return match;
        }

        static Transform FindNamed(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            Transform direct = root.Find(objectName);
            if (direct != null) return direct;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform nested = FindNamed(root.GetChild(i), objectName);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
