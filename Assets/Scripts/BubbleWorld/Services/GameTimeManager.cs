using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class GameTimeManager
    {
        private readonly GameTimeConfig config;
        private readonly List<IWorldTimeReceiver> receivers = new List<IWorldTimeReceiver>();

        public float ElapsedGameTime { get; private set; }
        public int DayCount { get; private set; } = 1;
        public SeasonType Season { get; private set; } = SeasonType.Spring;
        public float TimeMultiplier { get; set; } = 1f;
        public bool IsPaused { get; set; }
        public int TutorialDaysRemaining { get; set; } = 7;

        public float DayLength => config.DayLength;
        public int Hour => Mathf.FloorToInt(ElapsedGameTime / 3600f) % 24;
        public int Minute => Mathf.FloorToInt((ElapsedGameTime % 3600f) / 60f);

        public event Action<int> OnDayChanged;
        public event Action<SeasonType> OnSeasonChanged;

        public GameTimeManager(GameTimeConfig config)
        {
            this.config = config;
        }

        public void AddReceiver(IWorldTimeReceiver receiver)
        {
            if (!receivers.Contains(receiver))
                receivers.Add(receiver);
        }

        public void RemoveReceiver(IWorldTimeReceiver receiver)
        {
            receivers.Remove(receiver);
        }

        public void AdvanceTime(float deltaSeconds)
        {
            if (IsPaused) return;

            float scaledDelta = deltaSeconds * TimeMultiplier;
            ElapsedGameTime += scaledDelta;

            int previousDay = DayCount;
            while (ElapsedGameTime >= config.DayLength)
            {
                ElapsedGameTime -= config.DayLength;
                DayCount++;
                if (TutorialDaysRemaining > 0)
                    TutorialDaysRemaining--;

                SeasonType newSeason = (SeasonType)((DayCount / 30) % 4);
                if (newSeason != Season)
                {
                    Season = newSeason;
                    OnSeasonChanged?.Invoke(Season);
                }

                OnDayChanged?.Invoke(DayCount);
            }

            var context = new WorldTimeContext
            {
                ElapsedGameTime = ElapsedGameTime,
                DayCount = DayCount,
                Hour = Hour,
                Minute = Minute,
                Season = Season,
                IsOffline = false,
                IsPaused = IsPaused
            };

            foreach (var receiver in receivers)
                receiver.OnGameTick(scaledDelta, context);
        }

        public void ProcessOfflineCatchUp(double missedSeconds)
        {
            if (TutorialDaysRemaining > 0)
            {
                Debug.Log("[GameTimeManager] Skipping offline catch-up: tutorial active");
                return;
            }

            float capped = Mathf.Min((float)missedSeconds, config.OfflineCapHours * 3600f);
            float chunkSeconds = config.ChunkIntervalHours * 3600f;
            float processed = 0f;

            var context = new WorldTimeContext
            {
                IsOffline = true,
                IsPaused = false
            };

            while (processed < capped)
            {
                float chunk = Mathf.Min(chunkSeconds, capped - processed);
                ElapsedGameTime += chunk;
                processed += chunk;

                int previousDay = DayCount;
                while (ElapsedGameTime >= config.DayLength)
                {
                    ElapsedGameTime -= config.DayLength;
                    DayCount++;
                }

                context.ElapsedGameTime = ElapsedGameTime;
                context.DayCount = DayCount;
                context.Hour = Hour;
                context.Minute = Minute;
                context.Season = Season;

                foreach (var receiver in receivers)
                    receiver.OnFastForward(chunk, context);
            }

            Debug.Log($"[GameTimeManager] Offline catch-up: processed {processed / 3600f:F1} game hours");
        }

        public void SetTimeDirectly(float elapsed, int dayCount, SeasonType season)
        {
            ElapsedGameTime = elapsed;
            DayCount = dayCount;
            Season = season;
        }

        public void Save(ISaveWriter writer)
        {
            writer.Write("elapsedGameTime", ElapsedGameTime);
            writer.Write("dayCount", DayCount);
            writer.Write("season", (int)Season);
            writer.Write("tutorialDaysRemaining", TutorialDaysRemaining);
        }

        public void Load(ISaveReader reader)
        {
            ElapsedGameTime = reader.ReadFloat("elapsedGameTime", 0f);
            DayCount = reader.ReadInt("dayCount", 1);
            Season = (SeasonType)reader.ReadInt("season", 0);
            TutorialDaysRemaining = reader.ReadInt("tutorialDaysRemaining", 7);
        }
    }

    public interface ISaveWriter
    {
        void Write(string key, float value);
        void Write(string key, int value);
        void Write(string key, string value);
    }

    public interface ISaveReader
    {
        float ReadFloat(string key, float defaultValue);
        int ReadInt(string key, int defaultValue);
        string ReadString(string key, string defaultValue);
    }
}
