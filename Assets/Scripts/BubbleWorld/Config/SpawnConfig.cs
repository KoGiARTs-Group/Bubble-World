using System;
using UnityEngine;

namespace BubbleWorld
{
    [Serializable]
    public sealed class SpawnRequirementsEntry
    {
        [Tooltip("Inclusive lower bound (0-100)")]
        public int RangeStart;

        [Tooltip("Inclusive upper bound (0-100)")]
        public int RangeEnd;

        public ScarcityResponse Response;
    }

    [CreateAssetMenu(fileName = "SpawnConfig", menuName = "BubbleWorld/Config/Spawn Config")]
    public sealed class SpawnConfig : ScriptableObject
    {
        [Header("NPC Spawn")]
        [Tooltip("Consecutive days of food required for NPC spawn")]
        public int NPCSpawnFoodDays = 7;

        [Tooltip("Consecutive days of water required for NPC spawn")]
        public int NPCSpawnWaterDays = 7;

        [Tooltip("Consecutive days of shelter required for NPC spawn")]
        public int NPCSpawnShelterDays = 7;

        [Header("Disgruntled NPCs")]
        [Tooltip("Days of inadequate resources before NPC becomes disgruntled")]
        public int DisgruntledTriggerDays = 3;

        [Tooltip("Scarcity response roll table")]
        public SpawnRequirementsEntry[] ScarcityResponseTable;

        [Header("Animal Spawn")]
        public bool AnimalSpawnRequiresFood = true;
        public bool AnimalSpawnRequiresWater = true;
        [Tooltip("Chance per day of animal spawn when conditions met (0-1)")]
        public float AnimalSpawnChance = 0.3f;

        [Header("NPC Spawn")]
        [Tooltip("Chance per day of NPC spawn when conditions met (0-1)")]
        public float NPCSpawnChance = 0.2f;
        [Tooltip("Maximum NPCs in a single bubble")]
        public int MaxNPCCount = 10;

        [Header("Cooldowns")]
        [Tooltip("Game days between animal spawn checks")]
        public int AnimalSpawnCooldownDays = 3;

        [Tooltip("Game days between NPC spawn checks")]
        public int NPCSpawnCooldownDays = 7;

        private void OnEnable()
        {
            if (ScarcityResponseTable == null || ScarcityResponseTable.Length == 0)
            {
                ScarcityResponseTable = new[]
                {
                    new SpawnRequirementsEntry { RangeStart = 1, RangeEnd = 20, Response = ScarcityResponse.Cooperate },
                    new SpawnRequirementsEntry { RangeStart = 21, RangeEnd = 40, Response = ScarcityResponse.Fight },
                    new SpawnRequirementsEntry { RangeStart = 41, RangeEnd = 60, Response = ScarcityResponse.Migrate },
                    new SpawnRequirementsEntry { RangeStart = 61, RangeEnd = 80, Response = ScarcityResponse.Theft },
                    new SpawnRequirementsEntry { RangeStart = 81, RangeEnd = 95, Response = ScarcityResponse.Starvation },
                    new SpawnRequirementsEntry { RangeStart = 96, RangeEnd = 100, Response = ScarcityResponse.Leadership },
                };
            }
        }
    }
}
