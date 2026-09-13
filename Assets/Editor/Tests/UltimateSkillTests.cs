using NUnit.Framework;
using UnityEngine;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class UltimateSkillTests
    {
        MatchKnobs knobs;

        [SetUp]
        public void SetUp()
        {
            knobs = Rules.DefaultKnobs();
            Rules.ResetArenaSize();
        }

        [TearDown]
        public void TearDown()
        {
            Rules.ResetArenaSize();
        }

        BugState Make(int quality, int temperament)
        {
            BugState bug = new BugState(0, Vector3.zero, knobs);
            bug.quality = quality;
            bug.temperament = temperament;
            CricketCatalog.ApplyCombatBias(bug, quality, temperament);
            Rules.RefreshBody(knobs, bug);
            return bug;
        }

        [Test]
        public void GuanYuRevivePullsBackOnce()
        {
            BugState bug = Make(4, (int)CricketTemperament.ChenWen);
            bug.guanYuReviveLeft = 1;
            bug.position = new Vector3(80f, 0f, 0f);
            Assert.IsFalse(Rules.InsideArena(bug.position));
            Assert.IsTrue(Rules.TryGuanYuRevive(knobs, bug));
            Assert.AreEqual(0, bug.guanYuReviveLeft);
            Assert.IsTrue(Rules.InsideArena(bug.position));
            Assert.IsFalse(Rules.TryGuanYuRevive(knobs, bug));
        }

        [Test]
        public void XianPinHasNoSkill()
        {
            BugState xian = Make(3, (int)CricketTemperament.ChenWen);
            Assert.IsFalse(Rules.IsGuanYu(xian));
            Assert.AreEqual(1f, Rules.ItemPowerMul(knobs, Make(3, (int)CricketTemperament.ZhiMou)));
            BugState diao = Make(3, (int)CricketTemperament.LingQiao);
            diao.diaochanStealArmed = true;
            BugState victim = Make(1, 1);
            victim.stamina = 2f;
            Assert.IsFalse(Rules.TryDiaoChanSteal(knobs, diao, victim));
            Assert.AreEqual(2f, victim.stamina, 1e-4f);
        }

        [Test]
        public void ZhuGeGrowsHarder()
        {
            BugState fan = Make(1, 1);
            fan.grow = 1;
            BugState zhu = Make(4, (int)CricketTemperament.ZhiMou);
            zhu.grow = 1;
            float fanRate = Rules.GrowRate(knobs, fan);
            float zhuRate = Rules.GrowRate(knobs, zhu);
            Assert.Greater(zhuRate, fanRate);
            Assert.AreEqual(1f + knobs.growPer * knobs.jiItemPower, zhuRate, 1e-4f);
        }

        [Test]
        public void LuBuArmorNeedsHalfStamina()
        {
            BugState lu = Make(4, (int)CricketTemperament.MengGong);
            lu.stamina = Rules.StaminaMaxOf(knobs, lu) * 0.69f;
            Assert.IsFalse(Rules.TryStartLuBuArmor(knobs, lu));
            lu.stamina = Rules.StaminaMaxOf(knobs, lu) * 0.7f;
            Assert.IsTrue(Rules.TryStartLuBuArmor(knobs, lu));
        }

        [Test]
        public void DiaoChanStealCapsVictimAndFillsSelf()
        {
            BugState diao = Make(4, (int)CricketTemperament.LingQiao);
            BugState victim = Make(1, 1);
            diao.diaochanStealArmed = true;
            diao.stamina = 0.2f;
            victim.stamina = 0.4f;
            knobs.diaochanStealStamina = 1.5f;
            Assert.IsTrue(Rules.TryDiaoChanSteal(knobs, diao, victim));
            Assert.AreEqual(0f, victim.stamina, 1e-4f);
            Assert.AreEqual(Mathf.Min(Rules.StaminaMaxOf(knobs, diao), 0.2f + 1.5f), diao.stamina, 1e-4f);
            Assert.IsFalse(diao.diaochanStealArmed);
            victim.stamina = 2f;
            Assert.IsFalse(Rules.TryDiaoChanSteal(knobs, diao, victim));
        }

        [Test]
        public void DiaoChanStealBreaksLuBuArmor()
        {
            BugState diao = Make(4, (int)CricketTemperament.LingQiao);
            BugState lu = Make(4, (int)CricketTemperament.MengGong);
            diao.diaochanStealArmed = true;
            lu.charging = true;
            lu.luBuArmorT = 3f;
            lu.stamina = Rules.StaminaMaxOf(knobs, lu) * 0.7f;
            knobs.diaochanStealStamina = 0.2f;
            Assert.IsTrue(Rules.ChargeLocked(lu));
            Rules.TryDiaoChanSteal(knobs, diao, lu);
            Assert.IsFalse(Rules.ChargeLocked(lu));
            Assert.IsFalse(Rules.Unstoppable(lu));
        }
    }
}
