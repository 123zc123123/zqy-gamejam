using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 训练营另外三人：固定木桩，不蓄力、不跳。
    /// 机器人1 凡品强攻、机器人2 凡品灵巧、机器人3 凡品智谋，各三槽相同。
    /// 正赛 10 分钟，到点硬截止，不加时。
    /// </summary>
    public static class TrainingCamp
    {
        public const int BotCount = 3;
        public const float RegTime = 600f;
        public const float OtTime = 0f;
        public static readonly string[] BotNames = { "机器人1", "机器人2", "机器人3" };

        public static MatchKnobs WithDuration(MatchKnobs source)
        {
            MatchKnobs knobs = source != null
                ? JsonUtility.FromJson<MatchKnobs>(JsonUtility.ToJson(source))
                : Rules.DefaultKnobs();
            knobs.regTime = RegTime;
            knobs.otTime = OtTime;
            return knobs;
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
            for (int i = 0; i < picks.Length; i++)
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

    public enum MatchKind
    {
        None = 0,
        Random = 1,
        Friend = 2,
        Training = 3
    }
}
