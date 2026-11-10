using System;
using UnityEngine;

namespace BubbleWorld
{
    [Serializable]
    public sealed class WeatherRollEntry
    {
        [Tooltip("Inclusive lower bound (1-100)")]
        public int RangeStart = 1;

        [Tooltip("Inclusive upper bound (1-100)")]
        public int RangeEnd = 100;

        public WeatherType Weather;
    }

    [Serializable]
    public sealed class SeasonWeatherTable
    {
        public SeasonType Season;
        public WeatherRollEntry[] Rolls;
    }

    [CreateAssetMenu(fileName = "WeatherConfig", menuName = "BubbleWorld/Config/Weather Config")]
    public sealed class WeatherConfig : ScriptableObject
    {
        [Header("Seasonal Tables")]
        public SeasonWeatherTable[] SeasonalTables;

        [Header("Random Pool")]
        [Tooltip("Weather types used when Random is rolled")]
        public WeatherType[] RandomWeatherPool = new[]
        {
            WeatherType.Clear, WeatherType.Cloudy, WeatherType.Rain,
            WeatherType.Windy, WeatherType.Sunny
        };

        private void OnEnable()
        {
            if (SeasonalTables == null || SeasonalTables.Length == 0)
                SeasonalTables = DefaultTables();
        }

        private static SeasonWeatherTable[] DefaultTables()
        {
            return new[]
            {
                new SeasonWeatherTable
                {
                    Season = SeasonType.Spring,
                    Rolls = new[]
                    {
                        new WeatherRollEntry { RangeStart = 1, RangeEnd = 50, Weather = WeatherType.Cloudy },
                        new WeatherRollEntry { RangeStart = 51, RangeEnd = 60, Weather = WeatherType.Sunny },
                        new WeatherRollEntry { RangeStart = 61, RangeEnd = 70, Weather = WeatherType.Rain },
                        new WeatherRollEntry { RangeStart = 71, RangeEnd = 80, Weather = WeatherType.Windy },
                        new WeatherRollEntry { RangeStart = 81, RangeEnd = 93, Weather = WeatherType.Random },
                        new WeatherRollEntry { RangeStart = 94, RangeEnd = 96, Weather = WeatherType.Storm1 },
                        new WeatherRollEntry { RangeStart = 97, RangeEnd = 99, Weather = WeatherType.Storm2 },
                        new WeatherRollEntry { RangeStart = 100, RangeEnd = 100, Weather = WeatherType.Storm3 }
                    }
                },
                new SeasonWeatherTable
                {
                    Season = SeasonType.Summer,
                    Rolls = new[]
                    {
                        new WeatherRollEntry { RangeStart = 1, RangeEnd = 50, Weather = WeatherType.Sunny },
                        new WeatherRollEntry { RangeStart = 51, RangeEnd = 60, Weather = WeatherType.Cloudy },
                        new WeatherRollEntry { RangeStart = 61, RangeEnd = 70, Weather = WeatherType.Rain },
                        new WeatherRollEntry { RangeStart = 71, RangeEnd = 80, Weather = WeatherType.Windy },
                        new WeatherRollEntry { RangeStart = 81, RangeEnd = 93, Weather = WeatherType.Random },
                        new WeatherRollEntry { RangeStart = 94, RangeEnd = 96, Weather = WeatherType.Storm1 },
                        new WeatherRollEntry { RangeStart = 97, RangeEnd = 99, Weather = WeatherType.Storm2 },
                        new WeatherRollEntry { RangeStart = 100, RangeEnd = 100, Weather = WeatherType.Storm3 }
                    }
                },
                new SeasonWeatherTable
                {
                    Season = SeasonType.Autumn,
                    Rolls = new[]
                    {
                        new WeatherRollEntry { RangeStart = 1, RangeEnd = 20, Weather = WeatherType.Sunny },
                        new WeatherRollEntry { RangeStart = 21, RangeEnd = 40, Weather = WeatherType.Cloudy },
                        new WeatherRollEntry { RangeStart = 41, RangeEnd = 60, Weather = WeatherType.Rain },
                        new WeatherRollEntry { RangeStart = 61, RangeEnd = 80, Weather = WeatherType.Windy },
                        new WeatherRollEntry { RangeStart = 81, RangeEnd = 93, Weather = WeatherType.Random },
                        new WeatherRollEntry { RangeStart = 94, RangeEnd = 96, Weather = WeatherType.Storm1 },
                        new WeatherRollEntry { RangeStart = 97, RangeEnd = 99, Weather = WeatherType.Storm2 },
                        new WeatherRollEntry { RangeStart = 100, RangeEnd = 100, Weather = WeatherType.Storm3 }
                    }
                },
                new SeasonWeatherTable
                {
                    Season = SeasonType.Winter,
                    Rolls = new[]
                    {
                        new WeatherRollEntry { RangeStart = 1, RangeEnd = 20, Weather = WeatherType.Sunny },
                        new WeatherRollEntry { RangeStart = 21, RangeEnd = 40, Weather = WeatherType.Cloudy },
                        new WeatherRollEntry { RangeStart = 41, RangeEnd = 45, Weather = WeatherType.Rain },
                        new WeatherRollEntry { RangeStart = 46, RangeEnd = 60, Weather = WeatherType.Snow },
                        new WeatherRollEntry { RangeStart = 61, RangeEnd = 80, Weather = WeatherType.Windy },
                        new WeatherRollEntry { RangeStart = 81, RangeEnd = 93, Weather = WeatherType.Random },
                        new WeatherRollEntry { RangeStart = 94, RangeEnd = 96, Weather = WeatherType.Storm1 },
                        new WeatherRollEntry { RangeStart = 97, RangeEnd = 99, Weather = WeatherType.Storm2 },
                        new WeatherRollEntry { RangeStart = 100, RangeEnd = 100, Weather = WeatherType.Storm3 }
                    }
                }
            };
        }

        public WeatherType RollForSeason(SeasonType season, int roll)
        {
            foreach (var table in SeasonalTables)
            {
                if (table.Season != season) continue;
                foreach (var entry in table.Rolls)
                {
                    if (roll >= entry.RangeStart && roll <= entry.RangeEnd)
                        return entry.Weather;
                }
            }
            return WeatherType.Clear;
        }
    }
}
