using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class MergeDrawTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }

        [Test]
        public void FirstFinestOrPityForcesJiPin()
        {
            Assert.IsTrue(MergeBoard.ForcesJiPin(true, false));
            Assert.IsTrue(MergeBoard.ForcesJiPin(false, true));
            Assert.IsTrue(MergeBoard.ForcesJiPin(true, true));
            Assert.IsFalse(MergeBoard.ForcesJiPin(false, false));
        }

        [Test]
        public void NewAccountGetsFirstFinestGuarantee()
        {
            PlayerProfile fresh = new PlayerProfile();
            Assert.IsTrue(PlayerDataService.ShouldForceFirstFinest(fresh));

            fresh.firstFinestUsed = true;
            Assert.IsFalse(PlayerDataService.ShouldForceFirstFinest(fresh));
        }

        [Test]
        public void CollectionDoesNotSkipFirstFinestGuarantee()
        {
            PlayerProfile player = new PlayerProfile
            {
                crickets = new List<CricketCollectionEntry>
                {
                    new CricketCollectionEntry { drawA = 1, drawB = 1, count = 1 }
                }
            };
            Assert.IsTrue(PlayerDataService.ShouldForceFirstFinest(player));
        }

        [Test]
        public void FinestSkillDialogueParses()
        {
            TextAsset asset = Resources.Load<TextAsset>("Dialogue/dlg.tutorial.finest_skill");
            Assert.IsNotNull(asset);
            DialogueGroup group = DialogueMarkdown.Parse(asset.text, asset.name);
            Assert.IsNotNull(group);
            Assert.AreEqual(TutorialDirector.IdFinestSkill, group.id);
            Assert.AreEqual(4, group.lines.Count);
            Assert.IsTrue(group.lines[0].text.IndexOf("极品", System.StringComparison.Ordinal) >= 0);
            Assert.IsTrue(group.lines[1].text.IndexOf("技能", System.StringComparison.Ordinal) >= 0);
            Assert.IsTrue(group.lines[3].text.IndexOf("背包", System.StringComparison.Ordinal) >= 0);
        }

        [Test]
        public void AdultMergeWritesDrawResult()
        {
            MergeBoard board = CreateBoard();
            Assert.IsTrue(board.TrySpawn(0, 3));
            Assert.IsTrue(board.TrySpawn(1, 3));
            Assert.IsTrue(board.TryMerge(0, 1));
            Assert.AreEqual(1, board.Pieces.Count);
            MergePiece result = board.Pieces[0];
            Assert.AreEqual(4, result.level);
            Assert.IsTrue(result.isDrawResult);
            Assert.GreaterOrEqual(result.drawA, 1);
            Assert.LessOrEqual(result.drawA, 4);
            Assert.GreaterOrEqual(result.drawB, 1);
            Assert.LessOrEqual(result.drawB, 4);
        }

        MergeBoard CreateBoard()
        {
            GameObject go = new GameObject("MergeBoard");
            created.Add(go);
            MergeBoard board = go.AddComponent<MergeBoard>();
            board.ResetBoard(20260920);
            return board;
        }
    }
}
