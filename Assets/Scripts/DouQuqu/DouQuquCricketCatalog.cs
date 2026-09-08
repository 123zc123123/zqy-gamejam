using UnityEngine;

namespace DouQuqu
{
    /// <summary>精品虫品质：成虫合成时按权重抽取。</summary>
    public enum CricketQuality
    {
        Fan = 1,
        Ling = 2,
        Xian = 3,
        Ji = 4
    }

    /// <summary>性格。每个品质都会随机带一种；极品仍用三国名做短名。</summary>
    public enum CricketTemperament
    {
        MengGong = 1,
        LingQiao = 2,
        ZhiMou = 3,
        ChenWen = 4
    }

    /// <summary>
    /// 品质、性格名称和战斗倾向。
    /// 品质决定整体强度，性格决定攻速、体重、蓄力和角度的配比。
    /// </summary>
    public static class DouQuquCricketCatalog
    {
        public static readonly string[] QualityNames = { "", "凡品", "灵品", "仙品", "极品" };
        public static readonly string[] TemperamentNames = { "", "强攻", "灵巧", "智谋", "沉稳" };
        public static readonly string[] UltimateNames = { "", "吕布", "貂蝉", "诸葛亮", "关羽" };
        public static readonly string[] Idioms = { "", "天下无双", "闭月羞花", "神机妙算", "义薄云天" };
        /// <summary>16 只精品虫的名字：行=品质，列=性格。极品沿用三国名。</summary>
        public static readonly string[][] CricketNames =
        {
            null,
            new[] { "", "草莽", "蹦蹦", "细须", "土狗" },
            new[] { "", "青项", "麻翅", "黄头", "铜头" },
            new[] { "", "紫牙青", "金翅", "白牙青", "墨牙" },
            new[] { "", "吕布", "貂蝉", "诸葛亮", "关羽" }
        };
        public static readonly string[] TemperamentBlurbs =
        {
            "",
            "体沉、气长，适合硬碰硬。",
            "蓄得快、抓地稳，适合走位抢先。",
            "蓄得久，适合算准时机。",
            "回气快，适合连续出手。"
        };

        public static readonly Color[] QualityColors =
        {
            Color.white,
            new Color(0.72f, 0.72f, 0.72f, 1f),
            new Color(0.45f, 0.82f, 1f, 1f),
            new Color(0.78f, 0.52f, 1f, 1f),
            new Color(1f, 0.78f, 0.22f, 1f)
        };

        public static readonly Color[] TemperamentColors =
        {
            Color.white,
            new Color(0.92f, 0.28f, 0.22f, 1f),
            new Color(0.95f, 0.48f, 0.78f, 1f),
            new Color(0.28f, 0.72f, 0.58f, 1f),
            new Color(0.22f, 0.55f, 0.32f, 1f)
        };

        public static string QualityName(int quality)
        {
            quality = Mathf.Clamp(quality, 1, 4);
            return QualityNames[quality];
        }

        /// <summary>详情第三行：凡/灵/仙只写品质；极品写 极品·成语。</summary>
        public static string RankLabel(int quality, int temperament)
        {
            if (quality >= 4) return QualityName(quality) + "·" + Idiom(temperament);
            return QualityName(quality);
        }

        public static string TemperamentName(int temperament)
        {
            temperament = Mathf.Clamp(temperament, 1, 4);
            return TemperamentNames[temperament];
        }

        public static string UltimateName(int temperament)
        {
            temperament = Mathf.Clamp(temperament, 1, 4);
            return UltimateNames[temperament];
        }

        public static string CricketName(int quality, int temperament)
        {
            quality = Mathf.Clamp(quality, 1, 4);
            temperament = Mathf.Clamp(temperament, 1, 4);
            return CricketNames[quality][temperament];
        }

        public static string Idiom(int temperament)
        {
            temperament = Mathf.Clamp(temperament, 1, 4);
            return Idioms[temperament];
        }

        public static string Blurb(int temperament)
        {
            temperament = Mathf.Clamp(temperament, 1, 4);
            return TemperamentBlurbs[temperament];
        }

        /// <summary>棋盘格短名：名字 + 性格。</summary>
        public static string ShortLabel(int quality, int temperament)
        {
            if (quality < 1) return "";
            return CricketName(quality, temperament) + "\n" + TemperamentName(temperament);
        }

        public static string FullName(int quality, int temperament)
        {
            if (quality < 1) return "未成型";
            return CricketName(quality, temperament) + " · " + QualityName(quality) + " · " + TemperamentName(temperament);
        }

        public const int PanelStatCount = 6;

        public enum PanelStat
        {
            Mass = 0,
            Grip = 1,
            ChargeSpeed = 2,
            ChargeTime = 3,
            StaminaRegen = 4,
            StaminaMax = 5
        }

        /// <summary>品质常数：凡/灵/仙/极各一个，六个维度共用。局内 knobs × 品质 × 性格。</summary>
        static readonly float[] QualityMul = { 0f, 1.04f, 1.16f, 1.28f, 1.40f };

        /// <summary>双维强势 1.24，单维强势 1.44。非强势维为 1。列=强攻/灵巧/智谋/沉稳。</summary>
        const float TemperBoostDual = 1.24f;
        const float TemperBoostSingle = 1.44f;
        static readonly float[][] TemperMul =
        {
            new[] { 0f, TemperBoostDual, 1f, 1f, 1f },
            new[] { 0f, 1f, TemperBoostDual, 1f, 1f },
            new[] { 0f, 1f, TemperBoostDual, 1f, 1f },
            new[] { 0f, 1f, 1f, TemperBoostSingle, 1f },
            new[] { 0f, 1f, 1f, 1f, TemperBoostSingle },
            new[] { 0f, TemperBoostDual, 1f, 1f, 1f }
        };

        public static float QualityFactor(int quality)
        {
            quality = Mathf.Clamp(quality, 1, 4);
            return QualityMul[quality];
        }

        public static float TemperFactor(int temperament, PanelStat stat)
        {
            temperament = Mathf.Clamp(temperament, 1, 4);
            return TemperMul[(int)stat][temperament];
        }

        /// <summary>详情显示值 = 统一品质常数 × 该维性格常数。</summary>
        public static float StatFactor(int quality, int temperament, PanelStat stat)
        {
            return QualityFactor(quality) * TemperFactor(temperament, stat);
        }

        public static string StatDisplay(int quality, int temperament, PanelStat stat)
        {
            return StatFactor(quality, temperament, stat).ToString("0.00");
        }

        public static string[] PanelStatDisplays(int quality, int temperament)
        {
            string[] values = new string[PanelStatCount];
            for (int i = 0; i < PanelStatCount; i++)
                values[i] = StatDisplay(quality, temperament, (PanelStat)i);
            return values;
        }

        public static bool IsStrongStat(int temperament, PanelStat stat)
        {
            return TemperFactor(temperament, stat) > 1.001f;
        }

        public static bool[] PanelStatStrongFlags(int temperament)
        {
            bool[] flags = new bool[PanelStatCount];
            for (int i = 0; i < PanelStatCount; i++)
                flags[i] = IsStrongStat(temperament, (PanelStat)i);
            return flags;
        }

        /// <summary>把品质强度和性格倾向叠到战斗参数上：基础 knobs × 合成常数。</summary>
        public static void ApplyCombatBias(MatchKnobs knobs, int quality, int temperament)
        {
            if (knobs == null) return;
            quality = Mathf.Clamp(quality, 1, 4);
            temperament = Mathf.Clamp(temperament, 1, 4);
            knobs.mass *= StatFactor(quality, temperament, PanelStat.Mass);
            knobs.mu *= StatFactor(quality, temperament, PanelStat.Grip);
            knobs.vRate *= StatFactor(quality, temperament, PanelStat.ChargeSpeed);
            knobs.tChargeMax *= StatFactor(quality, temperament, PanelStat.ChargeTime);
            knobs.staminaRegen *= StatFactor(quality, temperament, PanelStat.StaminaRegen);
            knobs.staminaMax *= StatFactor(quality, temperament, PanelStat.StaminaMax);
            knobs.tFloor *= QualityFactor(quality);
        }
    }
}
