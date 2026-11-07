using UnityEngine;

namespace TrainTrain
{
    public static class PlayerProgress
    {
        private const string Prefix = "TrainTrain_ChaosRail_v2_";
        private const string LegacyPrefix = "TrainTrain_ChaosRail_";
        private const string HighestUnlockedKey = Prefix + "HighestUnlocked";
        private const string MutedKey = Prefix + "Muted";
        private const string SelectedTrainKey = Prefix + "SelectedTrain";
        private const string SchemaKey = Prefix + "Schema";

        public static int HighestUnlocked
        {
            get
            {
                EnsureMigrated();
                return Mathf.Clamp(PlayerPrefs.GetInt(HighestUnlockedKey, 0), 0, LevelCatalog.Levels.Count - 1);
            }
        }

        public static bool Muted
        {
            get
            {
                EnsureMigrated();
                return PlayerPrefs.GetInt(MutedKey, 0) == 1;
            }
            set
            {
                EnsureMigrated();
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static TrainStyle SelectedTrainStyle
        {
            get
            {
                EnsureMigrated();
                TrainStyle style = (TrainStyle)Mathf.Clamp(
                    PlayerPrefs.GetInt(SelectedTrainKey, 0),
                    0,
                    TrainStyleCatalog.All.Length - 1);
                return TrainStyleCatalog.IsUnlocked(style) ? style : TrainStyle.Steam;
            }
            set
            {
                EnsureMigrated();
                PlayerPrefs.SetInt(SelectedTrainKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static int GetStars(int levelIndex)
        {
            EnsureMigrated();
            return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Stars_" + levelIndex, 0), 0, 3);
        }

        public static int GetBestScore(int levelIndex)
        {
            EnsureMigrated();
            return Mathf.Max(0, PlayerPrefs.GetInt(Prefix + "Best_" + levelIndex, 0));
        }

        public static bool IsUnlocked(int levelIndex)
        {
            return levelIndex <= HighestUnlocked;
        }

        public static void RecordVictory(int levelIndex, int stars, int score)
        {
            EnsureMigrated();
            string starKey = Prefix + "Stars_" + levelIndex;
            string bestKey = Prefix + "Best_" + levelIndex;

            int previousStars = PlayerPrefs.GetInt(starKey, 0);
            int previousBest = PlayerPrefs.GetInt(bestKey, 0);
            PlayerPrefs.SetInt(starKey, Mathf.Max(previousStars, stars));
            PlayerPrefs.SetInt(bestKey, Mathf.Max(previousBest, score));

            int next = Mathf.Min(LevelCatalog.Levels.Count - 1, levelIndex + 1);
            PlayerPrefs.SetInt(HighestUnlockedKey, Mathf.Max(HighestUnlocked, next));
            PlayerPrefs.Save();
        }

        private static void EnsureMigrated()
        {
            if (PlayerPrefs.GetInt(SchemaKey, 0) >= 2)
            {
                return;
            }

            int legacyHighest = Mathf.Clamp(PlayerPrefs.GetInt(LegacyPrefix + "HighestUnlocked", 0), 0, 27);
            int legacyRegion = legacyHighest / 4;
            int legacyLocal = legacyHighest % 4;
            if (legacyRegion < LevelCatalog.Worlds.Count)
            {
                int mapped = LevelCatalog.FirstLevelIndex(legacyRegion) +
                             Mathf.Min(legacyLocal, LevelCatalog.Worlds[legacyRegion].Levels.Count - 1);
                PlayerPrefs.SetInt(HighestUnlockedKey, mapped);
            }

            for (int oldIndex = 0; oldIndex < 28; oldIndex++)
            {
                int region = oldIndex / 4;
                int local = oldIndex % 4;
                if (region >= LevelCatalog.Worlds.Count || local >= LevelCatalog.Worlds[region].Levels.Count)
                {
                    continue;
                }

                int mapped = LevelCatalog.FirstLevelIndex(region) + local;
                int stars = PlayerPrefs.GetInt(LegacyPrefix + "Stars_" + oldIndex, 0);
                int best = PlayerPrefs.GetInt(LegacyPrefix + "Best_" + oldIndex, 0);
                if (stars > PlayerPrefs.GetInt(Prefix + "Stars_" + mapped, 0))
                {
                    PlayerPrefs.SetInt(Prefix + "Stars_" + mapped, stars);
                }
                if (best > PlayerPrefs.GetInt(Prefix + "Best_" + mapped, 0))
                {
                    PlayerPrefs.SetInt(Prefix + "Best_" + mapped, best);
                }
            }

            if (PlayerPrefs.HasKey(LegacyPrefix + "Muted"))
            {
                PlayerPrefs.SetInt(MutedKey, PlayerPrefs.GetInt(LegacyPrefix + "Muted", 0));
            }

            PlayerPrefs.SetInt(SchemaKey, 2);
            PlayerPrefs.Save();
        }

        public static int TotalStars()
        {
            int total = 0;
            for (int i = 0; i < LevelCatalog.Levels.Count; i++)
            {
                total += GetStars(i);
            }

            return total;
        }

        public static void ResetAll()
        {
            EnsureMigrated();
            for (int i = 0; i < LevelCatalog.Levels.Count; i++)
            {
                PlayerPrefs.DeleteKey(Prefix + "Stars_" + i);
                PlayerPrefs.DeleteKey(Prefix + "Best_" + i);
            }

            PlayerPrefs.DeleteKey(HighestUnlockedKey);
            PlayerPrefs.DeleteKey(SelectedTrainKey);
            PlayerPrefs.Save();
        }
    }
}
