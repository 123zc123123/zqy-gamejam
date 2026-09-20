using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class SettlementPageTests
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
        public void BindKeepsSelfRowBackgroundOpaqueAndFadesOthers()
        {
            SettlementPage page = CreatePage();
            MatchController match = CreateMatch();
            match.State.place[0] = 2;
            match.State.place[1] = 1;
            match.State.place[2] = 3;
            match.State.place[3] = 4;

            page.Bind(match, MatchKind.Training, 0);

            Image selfRow = page.transform.Find("PlayerRow2").GetComponent<Image>();
            Image firstOther = page.transform.Find("PlayerRow1").GetComponent<Image>();
            Image thirdOther = page.transform.Find("PlayerRow3").GetComponent<Image>();
            Assert.AreEqual(1f, selfRow.color.a);
            Assert.Less(firstOther.color.a, selfRow.color.a);
            Assert.AreEqual(firstOther.color.a, thirdOther.color.a);
        }

        SettlementPage CreatePage()
        {
            GameObject prefab = Resources.Load<GameObject>("Settlement/Prefabs/Jiesuan");
            Assert.IsNotNull(prefab);
            GameObject go = Object.Instantiate(prefab);
            created.Add(go);
            SettlementPage page = go.GetComponent<SettlementPage>();
            if (page == null) page = go.AddComponent<SettlementPage>();
            return page;
        }

        MatchController CreateMatch()
        {
            GameObject go = new GameObject("SettlementMatch");
            created.Add(go);
            MatchController match = go.AddComponent<MatchController>();
            match.Configure(MatchRunMode.Offline, MatchController.MaxPlayers, Rules.DefaultKnobs());
            match.ResetMatch(MatchController.MaxPlayers, 20260920);
            return match;
        }
    }
}
