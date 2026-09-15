using System;
using System.Collections.Generic;

namespace DouQuqu
{
    /// <summary>
    /// 对白组。一份 markdown / json 就是一组，用 <see cref="id"/> 播放。
    /// 对齐 DeadZone narrative dialogue：id 用 dlg.phase.scope.slug。
    /// </summary>
    [Serializable]
    public sealed class DialogueGroup
    {
        public string id;
        public string title;
        public List<DialogueLine> lines = new List<DialogueLine>();
    }

    /// <summary>组内一句。顺序播放；不写 next 就按列表往下。</summary>
    [Serializable]
    public sealed class DialogueLine
    {
        public string id;
        public string speaker;
        public string text;
        public string mood = "neutral";
        public string avatar;
        public float autoSkip;
        public float cps;
    }
}
