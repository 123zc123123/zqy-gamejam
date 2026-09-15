using NUnit.Framework;

namespace DouQuqu.Editor.Tests
{
    [TestFixture]
    public sealed class DialogueMarkdownTests
    {
        [Test]
        public void ParsesDeadZoneNarrativeFormat()
        {
            const string source = "---\nid: dlg.sample.box\ntitle: 样例\n---\n\n## 1. 旁白 · 开场\n> mood: calm\n\n旁白：第一句。\n\n## 2. 旁白 · 结束\n> mood: warm\n> auto_skip: 1.5\n\n旁白：第二句。\n";
            DialogueGroup group = DialogueMarkdown.Parse(source, "fallback");
            Assert.IsNotNull(group);
            Assert.AreEqual("dlg.sample.box", group.id);
            Assert.AreEqual(2, group.lines.Count);
            Assert.AreEqual("旁白", group.lines[0].speaker);
            Assert.AreEqual("第一句。", group.lines[0].text);
            Assert.AreEqual("calm", group.lines[0].mood);
            Assert.AreEqual("第二句。", group.lines[1].text);
            Assert.AreEqual(1.5f, group.lines[1].autoSkip, 0.001f);
        }

        [Test]
        public void AcceptsHalfwidthColon()
        {
            const string source = "## 1\nspeaker: 队长\n队长: 收到。\n";
            DialogueGroup group = DialogueMarkdown.Parse(source, "dlg.x");
            Assert.IsNotNull(group);
            Assert.AreEqual("队长", group.lines[0].speaker);
            Assert.AreEqual("收到。", group.lines[0].text);
        }
    }
}
