using UnityEngine;

namespace BubbleWorld
{
    public sealed class WeatherManager : IWorldTimeReceiver
    {
        private readonly WeatherConfig config;
        private readonly DeterministicRandom random;
        private int lastWeatherDay = -1;

        public WeatherType CurrentWeather { get; private set; } = WeatherType.Clear;
        public SeasonType CurrentSeason { get; private set; } = SeasonType.Spring;
        public float CurrentTemperature { get; private set; } = 32f;

        public string WeatherDisplayName => CurrentWeather.ToString();
        public string TemperatureDisplayName => CurrentTemperature > 30f ? "Hot" :
            CurrentTemperature > 20f ? "Warm" :
            CurrentTemperature > 10f ? "Cool" : "Cold";

        public event System.Action<WeatherType> OnWeatherChanged;

        public WeatherManager(WeatherConfig config, int seed)
        {
            this.config = config;
            random = new DeterministicRandom(seed);
        }

        public void RollWeather(SeasonType season)
        {
            CurrentSeason = season;
            int roll = random.RangeInclusive(1, 100);
            WeatherType rolled = config.RollForSeason(season, roll);

            if (rolled == WeatherType.Random)
            {
                var pool = config.RandomWeatherPool;
                if (pool != null && pool.Length > 0)
                    rolled = pool[random.Range(0, pool.Length)];
                else
                    rolled = WeatherType.Clear;
            }

            CurrentWeather = rolled;
            UpdateTemperature(season, rolled);
            OnWeatherChanged?.Invoke(rolled);
        }

        public void SetWeatherDirectly(WeatherType weather, SeasonType season)
        {
            CurrentWeather = weather;
            CurrentSeason = season;
            UpdateTemperature(season, weather);
            OnWeatherChanged?.Invoke(weather);
        }

        public void OnGameTick(float deltaSeconds, WorldTimeContext context)
        {
            if (context.DayCount != lastWeatherDay)
            {
                lastWeatherDay = context.DayCount;
                CurrentSeason = context.Season;
                RollWeather(context.Season);
            }
        }

        public void OnFastForward(float totalMissedSeconds, WorldTimeContext context)
        {
            CurrentSeason = context.Season;
            int daysMissed = Mathf.FloorToInt(totalMissedSeconds / 86400f);
            if (daysMissed > 0)
                RollWeather(context.Season);
        }

        private void UpdateTemperature(SeasonType season, WeatherType weather)
        {
            float baseTemp = season switch
            {
                SeasonType.Spring => 22f,
                SeasonType.Summer => 35f,
                SeasonType.Autumn => 18f,
                SeasonType.Winter => 5f,
                _ => 22f
            };

            float weatherMod = weather switch
            {
                WeatherType.Sunny => 5f,
                WeatherType.Rain => -5f,
                WeatherType.Snow => -10f,
                WeatherType.Storm1 => -3f,
                WeatherType.Storm2 => -5f,
                WeatherType.Storm3 => -8f,
                WeatherType.Cloudy => -2f,
                WeatherType.Windy => -3f,
                _ => 0f
            };

            CurrentTemperature = baseTemp + weatherMod + random.RangeInclusive(-3, 3);
        }

        public int GetWaterBonus()
        {
            return CurrentWeather switch
            {
                WeatherType.Rain => 3,
                WeatherType.Storm1 => 4,
                WeatherType.Storm2 => 5,
                WeatherType.Storm3 => 6,
                WeatherType.Snow => 2,
                _ => 0
            };
        }

        public int GetSunBonus()
        {
            return CurrentWeather switch
            {
                WeatherType.Sunny => 2,
                WeatherType.Clear => 1,
                WeatherType.Cloudy => 1,
                _ => 0
            };
        }

        public int GetSnowBonus()
        {
            if (CurrentSeason != SeasonType.Winter) return 0;
            return CurrentWeather == WeatherType.Snow ? 2 : 0;
        }
    }
}
