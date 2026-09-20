using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class TutorialNestWatchTests
    {
        [Test]
        public void WatchPointPicksNearestLiveEgg()
        {
            List<EggState> eggs = new List<EggState>
            {
                new EggState { alive = true, position = new Vector3(8f, 0f, 0f) },
                new EggState { alive = false, position = new Vector3(0.1f, 0f, 0f) },
                new EggState { alive = true, position = new Vector3(2f, 0f, 1f) }
            };
            Vector3 point = TutorialBattleDirector.WatchPoint(eggs, null, Vector3.zero);
            Assert.AreEqual(2f, point.x, 1e-4f);
            Assert.AreEqual(1f, point.z, 1e-4f);
        }

        [Test]
        public void WatchPointUsesBabyWhenNoEgg()
        {
            List<BabyState> babies = new List<BabyState>
            {
                new BabyState { alive = true, position = new Vector3(4f, 0f, 3f) }
            };
            Vector3 point = TutorialBattleDirector.WatchPoint(null, babies, Vector3.zero);
            Assert.AreEqual(4f, point.x, 1e-4f);
            Assert.AreEqual(3f, point.z, 1e-4f);
        }

        [Test]
        public void WatchPointFallsBackWhenNothingAlive()
        {
            Vector3 fallback = new Vector3(3f, 0f, 4f);
            Vector3 point = TutorialBattleDirector.WatchPoint(null, new List<BabyState>(), fallback);
            Assert.AreEqual(fallback, point);
        }

        [Test]
        public void ZoneCollapseDialogueParses()
        {
            TextAsset asset = Resources.Load<TextAsset>("Dialogue/dlg.tutorial.battle_zone");
            Assert.IsNotNull(asset);
            DialogueGroup group = DialogueMarkdown.Parse(asset.text, asset.name);
            Assert.IsNotNull(group);
            Assert.AreEqual(TutorialDirector.IdBattleZone, group.id);
            Assert.AreEqual(2, group.lines.Count);
            Assert.IsTrue(group.lines[0].text.IndexOf("崩塌", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(group.lines[1].text.IndexOf("出局", StringComparison.Ordinal) >= 0);
        }

        [Test]
        public void JumpHintFollowsChosenDirection()
        {
            bool previous = InputDirectionSettings.ReverseDrag;
            try
            {
                InputDirectionSettings.Set(true);
                Vector2 reverse = TutorialFingerHint.JumpDragDelta();
                Assert.AreEqual(Vector3.one, TutorialFingerHint.JumpHintScale());
                InputDirectionSettings.Set(false);
                Vector2 same = TutorialFingerHint.JumpDragDelta();
                Assert.AreEqual(-reverse, same);
                Assert.AreEqual(new Vector3(-1f, -1f, 1f), TutorialFingerHint.JumpHintScale());
            }
            finally
            {
                InputDirectionSettings.Set(previous);
            }
        }

        [Test]
        public void JumpSameDialogueParses()
        {
            TextAsset asset = Resources.Load<TextAsset>("Dialogue/dlg.tutorial.battle_jump_same");
            Assert.IsNotNull(asset);
            DialogueGroup group = DialogueMarkdown.Parse(asset.text, asset.name);
            Assert.IsNotNull(group);
            Assert.AreEqual(TutorialDirector.IdBattleJumpSame, group.id);
            Assert.IsTrue(group.lines[0].text.IndexOf("往哪拖", StringComparison.Ordinal) >= 0);
        }

        [Test]
        public void ShopSkinDialogueParses()
        {
            TextAsset asset = Resources.Load<TextAsset>("Dialogue/dlg.tutorial.shop_skin");
            Assert.IsNotNull(asset);
            DialogueGroup group = DialogueMarkdown.Parse(asset.text, asset.name);
            Assert.IsNotNull(group);
            Assert.AreEqual(TutorialDirector.IdShopSkin, group.id);
            Assert.AreEqual(2, group.lines.Count);
            Assert.IsTrue(group.lines[0].text.IndexOf("皮肤", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(group.lines[1].text.IndexOf("幼虫", StringComparison.Ordinal) >= 0);
        }

        [Test]
        public void CloseupBarDrainsWithUnscaledWaitEvenIfElapsedFrozen()
        {
            EggState egg = new EggState { alive = true };
            const float elapsed = 24.5f;
            TutorialBattleDirector.DriveCloseupHatch(egg, elapsed, 0f, TutorialBattleDirector.CloseupHatchSeconds);
            Assert.AreEqual(1f, HatchBar.RemainRatio(elapsed, egg.hatchAt, egg.hatchDuration), 1e-4f);
            TutorialBattleDirector.DriveCloseupHatch(egg, elapsed, 1.2f, TutorialBattleDirector.CloseupHatchSeconds);
            Assert.AreEqual(0.5f, HatchBar.RemainRatio(elapsed, egg.hatchAt, egg.hatchDuration), 1e-4f);
            TutorialBattleDirector.DriveCloseupHatch(egg, elapsed, TutorialBattleDirector.CloseupHatchSeconds, TutorialBattleDirector.CloseupHatchSeconds);
            Assert.AreEqual(0f, HatchBar.RemainRatio(elapsed, egg.hatchAt, egg.hatchDuration), 1e-4f);
            Assert.IsTrue(TutorialBattleDirector.CloseupHatchDue(TutorialBattleDirector.CloseupHatchSeconds, TutorialBattleDirector.CloseupHatchSeconds));
        }

        [Test]
        public void HatchEggTurnsFocusedEggIntoBaby()
        {
            MatchState state = new MatchState
            {
                knobs = Rules.DefaultKnobs(),
                elapsed = 8f
            };
            EggState egg = new EggState
            {
                alive = true,
                position = new Vector3(2f, 0f, 1f),
                ownerId = 0,
                hatchAt = 99f,
                hatchDuration = 5f
            };
            state.eggs.Add(egg);
            NestSystem nest = new NestSystem();
            Assert.IsTrue(nest.HatchEgg(state, egg, null));
            Assert.IsFalse(egg.alive);
            Assert.AreEqual(1, state.babies.Count);
            Assert.IsTrue(state.babies[0].alive);
            Assert.AreEqual(2f, state.babies[0].position.x, 1e-4f);
            Assert.AreEqual(1f, state.babies[0].position.z, 1e-4f);
        }

        [Test]
        public void NestHatchDialogueParses()
        {
            TextAsset asset = Resources.Load<TextAsset>("Dialogue/dlg.tutorial.battle_nest_hatch");
            Assert.IsNotNull(asset);
            DialogueGroup group = DialogueMarkdown.Parse(asset.text, asset.name);
            Assert.IsNotNull(group);
            Assert.AreEqual(TutorialDirector.IdBattleNestHatch, group.id);
            Assert.AreEqual(2, group.lines.Count);
            Assert.IsTrue(group.lines[0].text.IndexOf("宝宝", System.StringComparison.Ordinal) >= 0);
            Assert.IsTrue(group.lines[1].text.IndexOf("饲主", System.StringComparison.Ordinal) >= 0);
        }
    }
}
