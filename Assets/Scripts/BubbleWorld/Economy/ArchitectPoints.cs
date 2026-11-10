using System;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class ArchitectPoints : IWorldTimeReceiver, IWorldSaveData
    {
        private readonly EconomyConfig config;
        private DateTime? lastPassiveRewardTime;
        private int lastPopulationCount;

        public int Balance { get; private set; }
        public string SaveKey => "ArchitectPoints";

        public event Action<int> OnBalanceChanged;
        public event Action OnInsufficientAP;

        public ArchitectPoints(EconomyConfig config)
        {
            this.config = config;
            Balance = config.StartingAP;
        }

        public void Initialize(DateTime? lastPassiveTime, int initialBalance)
        {
            Balance = initialBalance;
            lastPassiveRewardTime = lastPassiveTime;
        }

        public bool SpendAP(int amount)
        {
            if (Balance < amount)
            {
                OnInsufficientAP?.Invoke();
                return false;
            }
            Balance -= amount;
            OnBalanceChanged?.Invoke(Balance);
            return true;
        }

        public void EarnAP(int amount)
        {
            Balance += amount;
            OnBalanceChanged?.Invoke(Balance);
        }

        public bool CanAffordIntervention()
        {
            return Balance >= config.InterventionCost;
        }

        public bool SpendIntervention()
        {
            return SpendAP(config.InterventionCost);
        }

        public float CalculateCommunicationCost(int tokens, bool isPlayerToSoulform)
        {
            float rate = isPlayerToSoulform ? config.CommunicationFullRate : config.CommunicationHalfRate;
            return tokens * rate;
        }

        public bool CanAffordCommunication(int tokens, bool isPlayerToSoulform)
        {
            float cost = CalculateCommunicationCost(tokens, isPlayerToSoulform);
            return Balance >= cost;
        }

        public void OnPopulationChanged(int newPopulation)
        {
            if (newPopulation / config.PopulationRewardThreshold > lastPopulationCount / config.PopulationRewardThreshold)
            {
                int rewards = (newPopulation / config.PopulationRewardThreshold) -
                              (lastPopulationCount / config.PopulationRewardThreshold);
                EarnAP(rewards * config.PopulationRewardAmount);
            }
            lastPopulationCount = newPopulation;
        }

        public void OnSkillUnlocked()
        {
            EarnAP(config.SkillUnlockReward);
        }

        public void OnSkillLeveledUp()
        {
            EarnAP(config.SkillLevelUpReward);
        }

        public void OnGameTick(float deltaSeconds, WorldTimeContext context)
        {
            if (context.IsOffline) return;

            if (lastPassiveRewardTime.HasValue)
            {
                var elapsed = DateTime.UtcNow - lastPassiveRewardTime.Value;
                if (elapsed.TotalHours >= config.PassiveAwardIntervalHours)
                {
                    int hoursPassed = (int)(elapsed.TotalHours / config.PassiveAwardIntervalHours);
                    EarnAP(hoursPassed * config.DailyPassiveAP);
                    lastPassiveRewardTime = DateTime.UtcNow;
                }
            }
            else
            {
                lastPassiveRewardTime = DateTime.UtcNow;
            }
        }

        public void OnFastForward(float totalMissedSeconds, WorldTimeContext context)
        {
            double missedHours = totalMissedSeconds / 3600.0;
            int awards = Mathf.FloorToInt((float)(missedHours / config.PassiveAwardIntervalHours));
            if (awards > 0)
            {
                EarnAP(awards * config.DailyPassiveAP);
                lastPassiveRewardTime = DateTime.UtcNow;
            }
        }

        public string Serialize()
        {
            return JsonUtility.ToJson(new ArchitectPointsData
            {
                balance = Balance,
                lastPassiveRewardTime = lastPassiveRewardTime?.ToString("O"),
                lastPopulationCount = lastPopulationCount
            });
        }

        public void Deserialize(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            var d = JsonUtility.FromJson<ArchitectPointsData>(data);
            if (d == null) return;
            Balance = d.balance;
            if (!string.IsNullOrEmpty(d.lastPassiveRewardTime))
            {
                if (DateTime.TryParse(d.lastPassiveRewardTime, out var dt))
                    lastPassiveRewardTime = dt;
            }
            lastPopulationCount = d.lastPopulationCount;
        }

        [Serializable]
        private sealed class ArchitectPointsData
        {
            public int balance;
            public string lastPassiveRewardTime;
            public int lastPopulationCount;
        }
    }
}
