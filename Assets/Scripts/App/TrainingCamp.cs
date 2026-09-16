using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 训练营另外三人：使用和联机机器人相同的确定性 AI。
    /// 机器人1 凡品强攻、机器人2 凡品灵巧、机器人3 凡品智谋，各三槽相同。
    /// 时长、缩圈、镜头与随机匹配相同：单局 2 分钟，1:30 狂暴并收口；开场全景再落到己方角。
    /// 开局有效区是 field-0（档 0），不跳到末档 field-2。
    /// </summary>
    public static class TrainingCamp
    {
        public const int BotCount = 3;
        public const float RegTime = CompetitiveMatch.RegTime;
        public const float OtTime = CompetitiveMatch.OtTime;
        public static readonly string[] BotNames = { "机器人1", "机器人2", "机器人3" };

        public static MatchKnobs WithDuration(MatchKnobs source)
        {
            return CompetitiveMatch.WithDuration(source);
        }

        public static string BotName(int botIndex)
        {
            if (botIndex < 0 || botIndex >= BotNames.Length) return "机器人";
            return BotNames[botIndex];
        }

        public static CricketPick[] PicksForBot(int botIndex)
        {
            int temperament = Mathf.Clamp(botIndex, 0, BotCount - 1) + 1;
            CricketPick[] picks = new CricketPick[MatchController.LivesPerPlayer];
            int lives = TutorialDirector.OneLifeBattle ? 1 : picks.Length;
            for (int i = 0; i < lives; i++)
                picks[i] = MakePick(1, temperament);
            return picks;
        }

        public static CricketPick MakePick(int quality, int temperament)
        {
            quality = Mathf.Clamp(quality, 1, 4);
            temperament = Mathf.Clamp(temperament, 1, 4);
            return new CricketPick
            {
                catalogId = (quality - 1) * 4 + temperament,
                quality = quality,
                temperament = temperament
            };
        }
    }

    /// <summary>
    /// 随机匹配 / 好友房：单局 2 分钟，1:30 狂暴并收口，开局全景再落到己方角。
    /// Demo 场景 Inspector 可能留着训练营的 10 分钟和无缩圈，进局时必须盖掉。
    /// </summary>
    public static class CompetitiveMatch
    {
        public const float RegTime = 90f;
        public const float OtTime = 30f;

        public static MatchKnobs WithDuration(MatchKnobs source)
        {
            MatchKnobs knobs = source != null
                ? JsonUtility.FromJson<MatchKnobs>(JsonUtility.ToJson(source))
                : Rules.DefaultKnobs();
            knobs.regTime = RegTime;
            knobs.otTime = OtTime;
            knobs.zoneSchedule = true;
            return knobs;
        }
    }

    public enum MatchKind
    {
        None = 0,
        Random = 1,
        Friend = 2,
        Training = 3
    }
}
