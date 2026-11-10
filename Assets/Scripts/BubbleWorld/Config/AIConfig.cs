using System;
using UnityEngine;

namespace BubbleWorld
{
    [Serializable]
    public sealed class DietConfig
    {
        [Range(1, 100)]
        [Tooltip("Hunger level (%) below which the animal becomes hungry")]
        public int HungerThreshold = 30;

        [Header("Food Priority")]
        [Tooltip("Roll range for first food action")]
        public RollRangeEntry[] FoodRolls;

        [Header("Social Priority")]
        public RollRangeEntry[] SocialRolls;

        [Header("Safety Priority")]
        public RollRangeEntry[] SafetyRolls;
    }

    [Serializable]
    public sealed class RollRangeEntry
    {
        [Tooltip("Inclusive lower bound (0-100)")]
        public int RangeStart;

        [Tooltip("Inclusive upper bound (0-100)")]
        public int RangeEnd;

        public string Action;
    }

    [Serializable]
    public sealed class NPCBaselineTable
    {
        [Tooltip("1-X% → Busy")]
        public int BusyEnd = 33;

        [Tooltip("X+1% to Y% → Socializing")]
        public int SocializingEnd = 66;

        [Tooltip("Y+1% to 100% → Working")]
        public int WorkingEnd = 100;
    }

    [Serializable]
    public sealed class NPCStateEntry
    {
        [Tooltip("Inclusive lower bound (0-100)")]
        public int RangeStart;

        [Tooltip("Inclusive upper bound (0-100)")]
        public int RangeEnd;

        public string StateName;
        public string TriggerCondition;
    }

    [Serializable]
    public sealed class NPCTriggerResponseEntry
    {
        public string StateName;
        public RollRangeEntry[] Responses;
    }

    [CreateAssetMenu(fileName = "AIConfig", menuName = "BubbleWorld/Config/AI Config")]
    public sealed class AIConfig : ScriptableObject
    {
        [Header("Animal AI")]
        public DietConfig Carnivore;
        public DietConfig Herbivore;
        public DietConfig Omnivore;

        [Header("NPC AI")]
        public NPCBaselineTable NPCBaseline;
        public NPCStateEntry[] NPCStates;
        public NPCTriggerResponseEntry[] NPCTriggerResponses;

        private void OnEnable()
        {
            if (Carnivore == null)
            {
                Carnivore = new DietConfig
                {
                    HungerThreshold = 30,
                    FoodRolls = new[] { new RollRangeEntry { RangeStart = 1, RangeEnd = 100, Action = "Hunt" } },
                    SocialRolls = new[] { new RollRangeEntry { RangeStart = 1, RangeEnd = 100, Action = "Socialize" } },
                    SafetyRolls = new[]
                    {
                        new RollRangeEntry { RangeStart = 1, RangeEnd = 50, Action = "Attack" },
                        new RollRangeEntry { RangeStart = 51, RangeEnd = 100, Action = "Flee" }
                    }
                };
            }

            if (Herbivore == null)
            {
                Herbivore = new DietConfig
                {
                    HungerThreshold = 30,
                    FoodRolls = new[] { new RollRangeEntry { RangeStart = 1, RangeEnd = 100, Action = "Forage" } },
                    SocialRolls = new[] { new RollRangeEntry { RangeStart = 1, RangeEnd = 100, Action = "Socialize" } },
                    SafetyRolls = new[]
                    {
                        new RollRangeEntry { RangeStart = 1, RangeEnd = 90, Action = "Flee" },
                        new RollRangeEntry { RangeStart = 91, RangeEnd = 100, Action = "Attack" }
                    }
                };
            }

            if (Omnivore == null)
            {
                Omnivore = new DietConfig
                {
                    HungerThreshold = 30,
                    FoodRolls = new[]
                    {
                        new RollRangeEntry { RangeStart = 1, RangeEnd = 85, Action = "Forage" },
                        new RollRangeEntry { RangeStart = 86, RangeEnd = 100, Action = "Hunt" }
                    },
                    SocialRolls = new[] { new RollRangeEntry { RangeStart = 1, RangeEnd = 100, Action = "Socialize" } },
                    SafetyRolls = new[]
                    {
                        new RollRangeEntry { RangeStart = 1, RangeEnd = 50, Action = "Attack" },
                        new RollRangeEntry { RangeStart = 51, RangeEnd = 100, Action = "Flee" }
                    }
                };
            }

            if (NPCBaseline == null)
            {
                NPCBaseline = new NPCBaselineTable
                {
                    BusyEnd = 33,
                    SocializingEnd = 66,
                    WorkingEnd = 100
                };
            }

            if (NPCStates == null || NPCStates.Length == 0)
            {
                NPCStates = new[]
                {
                    new NPCStateEntry { RangeStart = 1, RangeEnd = 30, StateName = "SafeHungry" },
                    new NPCStateEntry { RangeStart = 31, RangeEnd = 40, StateName = "SafeStarving" },
                    new NPCStateEntry { RangeStart = 41, RangeEnd = 50, StateName = "SafeLonely", TriggerCondition = "loneliness<=12" },
                    new NPCStateEntry { RangeStart = 51, RangeEnd = 60, StateName = "SafeBored", TriggerCondition = "boredom<=10" },
                    new NPCStateEntry { RangeStart = 61, RangeEnd = 80, StateName = "HasFoodShelter" },
                    new NPCStateEntry { RangeStart = 81, RangeEnd = 85, StateName = "Threatened" },
                    new NPCStateEntry { RangeStart = 86, RangeEnd = 100, StateName = "NeedsShelter" }
                };
            }

            if (NPCTriggerResponses == null || NPCTriggerResponses.Length == 0)
            {
                NPCTriggerResponses = new[]
                {
                    new NPCTriggerResponseEntry
                    {
                        StateName = "SafeHungry",
                        Responses = new[]
                        {
                            new RollRangeEntry { RangeStart = 1, RangeEnd = 20, Action = "Forage" },
                            new RollRangeEntry { RangeStart = 21, RangeEnd = 40, Action = "Wait" },
                            new RollRangeEntry { RangeStart = 41, RangeEnd = 60, Action = "Steal" },
                            new RollRangeEntry { RangeStart = 61, RangeEnd = 80, Action = "Fight" },
                            new RollRangeEntry { RangeStart = 81, RangeEnd = 100, Action = "Ask" }
                        }
                    },
                    new NPCTriggerResponseEntry
                    {
                        StateName = "SafeStarving",
                        Responses = new[]
                        {
                            new RollRangeEntry { RangeStart = 1, RangeEnd = 20, Action = "Wait" },
                            new RollRangeEntry { RangeStart = 21, RangeEnd = 40, Action = "Steal" },
                            new RollRangeEntry { RangeStart = 41, RangeEnd = 60, Action = "Fight" },
                            new RollRangeEntry { RangeStart = 61, RangeEnd = 80, Action = "Hunt" },
                            new RollRangeEntry { RangeStart = 81, RangeEnd = 100, Action = "None" }
                        }
                    },
                    new NPCTriggerResponseEntry
                    {
                        StateName = "SafeLonely",
                        Responses = new[]
                        {
                            new RollRangeEntry { RangeStart = 1, RangeEnd = 60, Action = "Socialize" },
                            new RollRangeEntry { RangeStart = 61, RangeEnd = 100, Action = "LookForRelationship" }
                        }
                    },
                    new NPCTriggerResponseEntry
                    {
                        StateName = "SafeBored",
                        Responses = new[]
                        {
                            new RollRangeEntry { RangeStart = 1, RangeEnd = 20, Action = "PlayGames" },
                            new RollRangeEntry { RangeStart = 21, RangeEnd = 40, Action = "Read" },
                            new RollRangeEntry { RangeStart = 41, RangeEnd = 60, Action = "Sing" },
                            new RollRangeEntry { RangeStart = 61, RangeEnd = 80, Action = "PlayInstrument" },
                            new RollRangeEntry { RangeStart = 81, RangeEnd = 100, Action = "Socialize" }
                        }
                    },
                    new NPCTriggerResponseEntry
                    {
                        StateName = "HasFoodShelter",
                        Responses = new[]
                        {
                            new RollRangeEntry { RangeStart = 1, RangeEnd = 80, Action = "Share" },
                            new RollRangeEntry { RangeStart = 81, RangeEnd = 100, Action = "Hide" }
                        }
                    },
                    new NPCTriggerResponseEntry
                    {
                        StateName = "Threatened",
                        Responses = new[]
                        {
                            new RollRangeEntry { RangeStart = 1, RangeEnd = 60, Action = "Flee" },
                            new RollRangeEntry { RangeStart = 61, RangeEnd = 80, Action = "FleeForHelp" },
                            new RollRangeEntry { RangeStart = 81, RangeEnd = 100, Action = "Fight" }
                        }
                    },
                    new NPCTriggerResponseEntry
                    {
                        StateName = "NeedsShelter",
                        Responses = new[]
                        {
                            new RollRangeEntry { RangeStart = 1, RangeEnd = 20, Action = "Wait" },
                            new RollRangeEntry { RangeStart = 21, RangeEnd = 40, Action = "Ask" },
                            new RollRangeEntry { RangeStart = 41, RangeEnd = 60, Action = "Take" },
                            new RollRangeEntry { RangeStart = 61, RangeEnd = 100, Action = "Build" }
                        }
                    }
                };
            }
        }

        public DietConfig GetDietConfig(AnimalDiet diet)
        {
            return diet switch
            {
                AnimalDiet.Carnivore => Carnivore,
                AnimalDiet.Herbivore => Herbivore,
                AnimalDiet.Omnivore => Omnivore,
                _ => Herbivore
            };
        }
    }
}
