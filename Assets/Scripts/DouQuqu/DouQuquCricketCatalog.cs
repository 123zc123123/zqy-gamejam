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
        ZhiKong = 3,
        WenZhong = 4
    }

    /// <summary>
    /// 品质、性格名称和战斗倾向。
    /// 品质决定整体强度，性格决定攻速、体重、蓄力和角度的配比。
    /// </summary>
    public static class DouQuquCricketCatalog
    {
        public static readonly string[] QualityNames = { "", "凡品", "灵品", "仙品", "极品" };
        public static readonly string[] TemperamentNames = { "", "猛攻", "灵巧", "智控", "稳重" };
        public static readonly string[] UltimateNames = { "", "吕布", "貂蝉", "诸葛亮", "关羽" };

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

        /// <summary>棋盘格短名：凡品·猛攻，极品则显示 极品·吕布。</summary>
        public static string ShortLabel(int quality, int temperament)
        {
            if (quality < 1) return "";
            string left = QualityName(quality);
            string right = quality >= 4 ? UltimateName(temperament) : TemperamentName(temperament);
            return left + "\n" + right;
        }

        public static string FullName(int quality, int temperament)
        {
            if (quality < 1) return "未成型";
            if (quality >= 4) return "极品·" + UltimateName(temperament);
            return QualityName(quality) + "·" + TemperamentName(temperament);
        }

        /// <summary>把品质强度和性格倾向叠到战斗参数上，供对局读取。</summary>
        public static void ApplyCombatBias(MatchKnobs knobs, int quality, int temperament)
        {
            if (knobs == null) return;
            quality = Mathf.Clamp(quality, 1, 4);
            temperament = Mathf.Clamp(temperament, 1, 4);
            float power = 0.92f + quality * 0.12f;
            knobs.vRate *= power;
            knobs.mass *= power;
            knobs.dMin *= power;

            switch ((CricketTemperament)temperament)
            {
                case CricketTemperament.MengGong:
                    knobs.vRate *= 1.28f;
                    knobs.tMax *= 0.88f;
                    knobs.mass *= 1.06f;
                    knobs.mu *= 0.92f;
                    break;
                case CricketTemperament.LingQiao:
                    knobs.vRate *= 1.08f;
                    knobs.mass *= 0.80f;
                    knobs.mu *= 1.16f;
                    knobs.theta *= 1.18f;
                    knobs.muCtrlScale *= 1.12f;
                    break;
                case CricketTemperament.ZhiKong:
                    knobs.vRate *= 0.94f;
                    knobs.tMax *= 1.22f;
                    knobs.theta *= 1.28f;
                    knobs.mu *= 1.08f;
                    break;
                case CricketTemperament.WenZhong:
                    knobs.vRate *= 0.88f;
                    knobs.mass *= 1.32f;
                    knobs.mu *= 1.22f;
                    knobs.dMin *= 1.10f;
                    knobs.theta *= 0.88f;
                    break;
            }
        }
    }
}
