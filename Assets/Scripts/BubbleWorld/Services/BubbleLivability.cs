using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    public enum LivabilityRating
    {
        Poor = 0,
        Mild = 1,
        Fair = 2,
        Good = 3
    }

    public sealed class WorldCondition
    {
        public string Name;
        public int Value;
        public LivabilityRating Rating;
        public string Description;

        public WorldCondition(string name, int value)
        {
            Name = name;
            Value = value;
            Rating = RateFromValue(value);
            Description = BuildDescription(name, Rating);
        }

        public static LivabilityRating RateFromValue(int value)
        {
            if (value >= 75) return LivabilityRating.Good;
            if (value >= 50) return LivabilityRating.Fair;
            if (value >= 25) return LivabilityRating.Mild;
            return LivabilityRating.Poor;
        }

        private static string BuildDescription(string name, LivabilityRating rating)
        {
            switch (rating)
            {
                case LivabilityRating.Good: return name + " is in excellent condition.";
                case LivabilityRating.Fair: return name + " is adequate for life.";
                case LivabilityRating.Mild: return name + " is below comfortable levels.";
                default: return name + " is critically lacking.";
            }
        }
    }

    public sealed class LifeCondition
    {
        public string Name;
        public bool Met;
        public string Reason;

        public LifeCondition(string name, bool met, string reason)
        {
            Name = name;
            Met = met;
            Reason = reason;
        }
    }

    public sealed class LivabilityReport
    {
        public readonly Dictionary<string, int> Metrics = new Dictionary<string, int>();
        public readonly List<WorldCondition> WorldConditions = new List<WorldCondition>();
        public readonly List<LifeCondition> LifeConditions = new List<LifeCondition>();
        public int Percentage;
        public LivabilityRating Rating;
        public bool AnimaReady;

        public static readonly string[] ConditionNames =
        {
            "Light",
            "Water Access",
            "Food Potential",
            "Shelter",
            "Safety",
            "Temperature",
            "Stability",
            "Space"
        };
    }

    public static class BubbleLivability
    {
        private static readonly int[] Targets = { 15, 18, 24, 22, 18, 15, 24, 45 };

        public static LivabilityReport Calculate(BubbleModel bubble)
        {
            LivabilityReport report = new LivabilityReport();
            int food = 0;
            int water = 0;
            int shelter = 0;
            int light = 0;
            int safety = 0;
            int weather = 0;
            int stability = 0;
            int openSpace = Mathf.Max(0, bubble.Cells.Count / 24);
            int temperature = 0;

            foreach (BubbleCell cell in bubble.Cells.Values)
            {
                BubblePlacementDefinition item = BubblePlacementCatalog.Find(cell.ItemId);
                if (item == null) continue;

                food += item.Food;
                water += item.Water;
                shelter += item.Shelter;
                light += item.Light;
                safety += item.Safety;
                weather += item.Weather;
                stability += item.Stability;
                openSpace += item.Space;
                temperature += item.Weather;
            }

            int[] values = { light, water, food, shelter, safety, temperature, stability, openSpace };
            for (int i = 0; i < values.Length; i++)
            {
                int pct = Mathf.Clamp(Mathf.RoundToInt(values[i] / (float)Targets[i] * 100f), 0, 100);
                report.Metrics[LivabilityReport.ConditionNames[i]] = pct;
                report.WorldConditions.Add(new WorldCondition(LivabilityReport.ConditionNames[i], pct));
            }

            int total = 0;
            foreach (int value in report.Metrics.Values) total += value;
            report.Percentage = Mathf.Clamp(Mathf.RoundToInt(total / (float)report.Metrics.Count), 0, 100);
            report.Rating = report.Percentage >= 80 ? LivabilityRating.Good
                : report.Percentage >= 60 ? LivabilityRating.Fair
                : report.Percentage >= 40 ? LivabilityRating.Mild
                : LivabilityRating.Poor;

            BuildLifeConditions(report);
            report.AnimaReady = AllLifeConditionsMet(report) && report.Percentage >= 80;

            return report;
        }

        private static void BuildLifeConditions(LivabilityReport report)
        {
            report.LifeConditions.Clear();
            report.LifeConditions.Add(MakeCondition("Light \u2265 50%",
                report.Metrics["Light"] >= 50,
                report.Metrics["Light"] >= 50
                    ? "Sufficient light for life."
                    : "Add Sunlight or reduce shade."));
            report.LifeConditions.Add(MakeCondition("Water Access \u2265 40%",
                report.Metrics["Water Access"] >= 40,
                report.Metrics["Water Access"] >= 40
                    ? "Fresh water available."
                    : "Add Fresh Water tiles."));
            report.LifeConditions.Add(MakeCondition("Food Potential \u2265 40%",
                report.Metrics["Food Potential"] >= 40,
                report.Metrics["Food Potential"] >= 40
                    ? "Food sources present."
                    : "Plant trees or berry bushes."));
            report.LifeConditions.Add(MakeCondition("Shelter \u2265 40%",
                report.Metrics["Shelter"] >= 40,
                report.Metrics["Shelter"] >= 40
                    ? "Shelter structures present."
                    : "Build Simple Shelter or Wind Break."));
            report.LifeConditions.Add(MakeCondition("Safety \u2265 40%",
                report.Metrics["Safety"] >= 40,
                report.Metrics["Safety"] >= 40
                    ? "Safe from hazards."
                    : "Add stone or wind breaks for protection."));
            report.LifeConditions.Add(MakeCondition("Temperature \u2265 40%",
                report.Metrics["Temperature"] >= 40,
                report.Metrics["Temperature"] >= 40
                    ? "Climate is temperate."
                    : "Add Gentle Rain or other climate items."));
            report.LifeConditions.Add(MakeCondition("Stability \u2265 40%",
                report.Metrics["Stability"] >= 40,
                report.Metrics["Stability"] >= 40
                    ? "Terrain is stable."
                    : "Add stone or hill tiles."));
            report.LifeConditions.Add(MakeCondition("Space \u2265 30%",
                report.Metrics["Space"] >= 30,
                report.Metrics["Space"] >= 30
                    ? "Adequate open space."
                    : "Leave more open tiles for movement."));
        }

        private static LifeCondition MakeCondition(string name, bool met, string reason)
        {
            return new LifeCondition(name, met, reason);
        }

        private static bool AllLifeConditionsMet(LivabilityReport report)
        {
            foreach (LifeCondition c in report.LifeConditions)
            {
                if (!c.Met) return false;
            }
            return true;
        }
    }
}
