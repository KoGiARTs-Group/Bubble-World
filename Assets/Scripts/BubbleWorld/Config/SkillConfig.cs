using System;
using UnityEngine;

namespace BubbleWorld
{
    [Serializable]
    public sealed class StarLevelEntry
    {
        public int Star;
        [Range(0, 100)]
        public int SuccessPercentage;
    }

    [Serializable]
    public sealed class SkillSetEntry
    {
        public string Name;
        public string[] Skills;
    }

    [CreateAssetMenu(fileName = "SkillConfig", menuName = "BubbleWorld/Config/Skill Config")]
    public sealed class SkillConfig : ScriptableObject
    {
        [Header("Progression")]
        [Tooltip("Success percentages per star level (0★ to 5★)")]
        public StarLevelEntry[] StarLevels;

        [Tooltip("Successful executions required to fill one star")]
        public int ExecutionsPerStar = 5;

        [Header("Skill Sets (Vertical Slice)")]
        public SkillSetEntry[] SkillSets;

        private void OnEnable()
        {
            if (StarLevels == null || StarLevels.Length == 0)
            {
                StarLevels = new[]
                {
                    new StarLevelEntry { Star = 0, SuccessPercentage = 15 },
                    new StarLevelEntry { Star = 1, SuccessPercentage = 25 },
                    new StarLevelEntry { Star = 2, SuccessPercentage = 45 },
                    new StarLevelEntry { Star = 3, SuccessPercentage = 65 },
                    new StarLevelEntry { Star = 4, SuccessPercentage = 85 },
                    new StarLevelEntry { Star = 5, SuccessPercentage = 99 }
                };
            }

            if (SkillSets == null || SkillSets.Length == 0)
            {
                SkillSets = new[]
                {
                    new SkillSetEntry
                    {
                        Name = "Survival",
                        Skills = new[] { "Tracking", "Shelter Building", "Hunting", "Trapping", "Self-Defense" }
                    },
                    new SkillSetEntry
                    {
                        Name = "Firecraft",
                        Skills = new[] { "Fuel Sourcing", "Spark Craft", "Fire Making", "Tinder Crafting", "Fire Maintenance" }
                    },
                    new SkillSetEntry
                    {
                        Name = "Garden Craft",
                        Skills = new[] { "Foraging", "Plant Knowledge", "Gardening", "Seed Selection", "Herbalism" }
                    },
                    new SkillSetEntry
                    {
                        Name = "Shelter Craft",
                        Skills = new[] { "Material Gathering", "Site Choosing", "Shelter Building", "Weather Proofing", "Shelter Maintenance" }
                    },
                    new SkillSetEntry
                    {
                        Name = "Stone Craft",
                        Skills = new[] { "Stone Identification", "Spark Craft", "Stone Shaping", "Stone Tooling", "Mason" }
                    }
                };
            }
        }

        public int GetSuccessPercentage(int starLevel)
        {
            foreach (var entry in StarLevels)
            {
                if (entry.Star == starLevel)
                    return entry.SuccessPercentage;
            }
            return 15;
        }
    }
}
