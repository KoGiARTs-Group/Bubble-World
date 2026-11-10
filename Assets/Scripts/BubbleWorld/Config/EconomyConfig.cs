using UnityEngine;

namespace BubbleWorld
{
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "BubbleWorld/Config/Economy Config")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Header("Starting Balance")]
        public int StartingAP = 100;

        [Header("Costs")]
        public int InterventionCost = 30;

        [Tooltip("AP per token for player→Soulform (full rate)")]
        public float CommunicationFullRate = 1f;

        [Tooltip("AP per token for Soulform→Player (half rate)")]
        public float CommunicationHalfRate = 0.5f;

        [Header("Replenishment")]
        [Tooltip("AP earned every passive interval")]
        public int DailyPassiveAP = 1;

        [Tooltip("Hours between passive AP awards")]
        public float PassiveAwardIntervalHours = 24f;

        [Tooltip("NPC count threshold for population reward")]
        public int PopulationRewardThreshold = 100;

        [Tooltip("AP earned when population threshold met")]
        public int PopulationRewardAmount = 10;

        [Tooltip("AP earned for unlocking a new skill")]
        public int SkillUnlockReward = 10;

        [Tooltip("AP earned for leveling up a skill")]
        public int SkillLevelUpReward = 5;

        [Header("Messages")]
        public string InsufficientAPMessage = "The Soulform seems distant.";
    }
}
