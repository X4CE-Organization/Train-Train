using UnityEngine;

namespace TrainTrain
{
    public enum TrainStyle
    {
        Steam = 0,
        Express = 1,
        Metro = 2,
        Freight = 3
    }

    public sealed class TrainStyleInfo
    {
        public readonly TrainStyle Style;
        public readonly string NameCn;
        public readonly string NameEn;
        public readonly string Description;
        public readonly int UnlockStars;
        public readonly int Capacity;
        public readonly float SpeedMultiplier;
        public readonly Color Accent;

        public TrainStyleInfo(
            TrainStyle style,
            string nameCn,
            string nameEn,
            string description,
            int unlockStars,
            int capacity,
            float speedMultiplier,
            Color accent)
        {
            Style = style;
            NameCn = nameCn;
            NameEn = nameEn;
            Description = description;
            UnlockStars = unlockStars;
            Capacity = capacity;
            SpeedMultiplier = speedMultiplier;
            Accent = accent;
        }
    }

    public static class TrainStyleCatalog
    {
        public static readonly TrainStyleInfo[] All =
        {
            new TrainStyleInfo(TrainStyle.Steam, "经典蒸汽", "CLASSIC STEAM", "平衡的经典列车。", 0, 6, 1f, RailPalette.Amber),
            new TrainStyleInfo(TrainStyle.Express, "流线快车", "STREAMLINE", "车身更快，但载客更少。", 4, 5, 1.18f, RailPalette.Azure),
            new TrainStyleInfo(TrainStyle.Metro, "城市地铁", "CITY METRO", "车身紧凑，载客量更高。", 12, 7, 1f, RailPalette.Crimson),
            new TrainStyleInfo(TrainStyle.Freight, "重载货运", "HEAVY FREIGHT", "速度稍慢，但能装下更多乘客。", 20, 8, 0.88f, RailPalette.Violet)
        };

        public static TrainStyleInfo Get(TrainStyle style)
        {
            return All[Mathf.Clamp((int)style, 0, All.Length - 1)];
        }

        public static bool IsUnlocked(TrainStyle style)
        {
            return PlayerProgress.TotalStars() >= Get(style).UnlockStars;
        }
    }
}
