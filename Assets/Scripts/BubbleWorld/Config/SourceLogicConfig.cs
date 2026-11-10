using System;
using UnityEngine;

namespace BubbleWorld
{
    [Serializable]
    public sealed class WeatherConversionEntry
    {
        public WeatherType Weather;
        public int WaterAmount;
        public int SnowAmount;
        public int SunAmount;
    }

    [Serializable]
    public sealed class TileThresholdEntry
    {
        [Tooltip("Matching ItemId (e.g. 'grass', 'dirt', 'muddy', 'barren_dry')")]
        public string TileId;

        [Header("Water Limits")]
        public int WaterMax = 10;
        public int WaterMin = 3;

        [Header("Sun Limits")]
        public int SunMax = 10;
        public int SunMin = 3;

        [Header("Growth")]
        [Range(0f, 1f)]
        public float GrowthPotential = 0.5f;

        [Tooltip("If true, this tile can spawn adjacent plants when conditions are perfect")]
        public bool CanSpawnPlants;
    }

    [Serializable]
    public sealed class TileConversionRuleEntry
    {
        public string SourceTileType;
        public ExcessType ExcessType;
        public int RequiredDays = 3;
        public string TargetTileType;
    }

    [Serializable]
    public sealed class WaterPropagationEntry
    {
        [Tooltip("ItemId of the water source tile type")]
        public string SourceTileId;

        [Tooltip("How much water to propagate per distance step")]
        public int[] PropagationByDistance;
    }

    [Serializable]
    public sealed class PlantGrowthConfig
    {
        [Tooltip("Water accumulation required in 7-day window")]
        public int WaterThreshold = 7;

        [Tooltip("Sun accumulation required in 7-day window")]
        public int SunThreshold = 7;

        [Tooltip("Days between growth checks")]
        public int CheckIntervalDays = 7;

        [Tooltip("Radius to look for existing plants for clustering")]
        public int ClusterRadius = 3;

        [Tooltip("Base chance of new plant spawning per check")]
        [Range(0f, 1f)]
        public float BaseSpawnChance = 0.05f;

        [Tooltip("Additional chance per nearby plant")]
        [Range(0f, 1f)]
        public float PerPlantBonus = 0.03f;

        [Tooltip("Maximum spawn chance cap")]
        [Range(0f, 1f)]
        public float MaxSpawnChance = 0.5f;

        [Tooltip("Tiles that plants revert to in extreme conditions")]
        public string RevertTileId = "dirt";
    }

    [CreateAssetMenu(fileName = "SourceLogicConfig", menuName = "BubbleWorld/Config/Source Logic Config")]
    public sealed class SourceLogicConfig : ScriptableObject
    {
        [Header("Weather to Resource Conversion")]
        public WeatherConversionEntry[] WeatherConversion;

        [Header("Per-Tile Thresholds")]
        public TileThresholdEntry[] TileThresholds;

        [Header("Water Propagation")]
        public WaterPropagationEntry[] WaterPropagation;

        [Header("Tile Conversion Rules")]
        public TileConversionRuleEntry[] TileConversionRules;

        [Header("Plant Growth")]
        public PlantGrowthConfig PlantGrowth;

        [Header("Puddle Formation")]
        [Tooltip("Water accumulation multiplier above max to trigger puddle")]
        public float PuddleTriggerMultiplier = 2f;

        [Tooltip("Target tile for puddle formation")]
        public string PuddleTileId = "muddy";

        private void OnEnable()
        {
            if (WeatherConversion == null || WeatherConversion.Length == 0)
                WeatherConversion = DefaultWeatherConversion();
            if (TileThresholds == null || TileThresholds.Length == 0)
                TileThresholds = DefaultTileThresholds();
            if (WaterPropagation == null || WaterPropagation.Length == 0)
                WaterPropagation = DefaultWaterPropagation();
            if (TileConversionRules == null || TileConversionRules.Length == 0)
                TileConversionRules = DefaultTileConversionRules();
            if (PlantGrowth == null)
                PlantGrowth = DefaultPlantGrowth();
        }

        public TileThresholdEntry GetThreshold(string tileId)
        {
            foreach (var t in TileThresholds)
                if (t.TileId == tileId) return t;
            return null;
        }

        public WaterPropagationEntry GetPropagation(string tileId)
        {
            foreach (var p in WaterPropagation)
                if (p.SourceTileId == tileId) return p;
            return null;
        }

        private static WeatherConversionEntry[] DefaultWeatherConversion()
        {
            return new[]
            {
                new WeatherConversionEntry { Weather = WeatherType.Rain, WaterAmount = 3, SunAmount = 0 },
                new WeatherConversionEntry { Weather = WeatherType.Snow, SnowAmount = 2, SunAmount = 0 },
                new WeatherConversionEntry { Weather = WeatherType.Sunny, SunAmount = 2 },
                new WeatherConversionEntry { Weather = WeatherType.Cloudy, SunAmount = 1 },
                new WeatherConversionEntry { Weather = WeatherType.Clear, SunAmount = 0 },
                new WeatherConversionEntry { Weather = WeatherType.Windy, SunAmount = 0 },
                new WeatherConversionEntry { Weather = WeatherType.Storm1, WaterAmount = 5, SunAmount = 0 },
                new WeatherConversionEntry { Weather = WeatherType.Storm2, WaterAmount = 7, SunAmount = 0 },
                new WeatherConversionEntry { Weather = WeatherType.Storm3, WaterAmount = 10, SunAmount = 0 },
            };
        }

        private static TileThresholdEntry[] DefaultTileThresholds()
        {
            return new[]
            {
                new TileThresholdEntry { TileId = "grass", WaterMax = 8, WaterMin = 2, SunMax = 9, SunMin = 3, GrowthPotential = 0.6f, CanSpawnPlants = true },
                new TileThresholdEntry { TileId = "dirt", WaterMax = 7, WaterMin = 1, SunMax = 10, SunMin = 1, GrowthPotential = 0.3f },
                new TileThresholdEntry { TileId = "muddy", WaterMax = 12, WaterMin = 5, SunMax = 6, SunMin = 1, GrowthPotential = 0.2f },
                new TileThresholdEntry { TileId = "barren_dry", WaterMax = 3, WaterMin = 0, SunMax = 12, SunMin = 5, GrowthPotential = 0.05f },
                new TileThresholdEntry { TileId = "sand", WaterMax = 2, WaterMin = 0, SunMax = 12, SunMin = 6, GrowthPotential = 0.05f },
                new TileThresholdEntry { TileId = "stone", WaterMax = 1, WaterMin = 0, SunMax = 12, SunMin = 0, GrowthPotential = 0f },
                new TileThresholdEntry { TileId = "patchy_dirt", WaterMax = 6, WaterMin = 1, SunMax = 10, SunMin = 2, GrowthPotential = 0.25f, CanSpawnPlants = true },
                new TileThresholdEntry { TileId = "stream", WaterMax = 15, WaterMin = 8, SunMax = 7, SunMin = 1, GrowthPotential = 0.1f },
                new TileThresholdEntry { TileId = "fresh_water", WaterMax = 20, WaterMin = 10, SunMax = 6, SunMin = 0, GrowthPotential = 0f },
                new TileThresholdEntry { TileId = "shore", WaterMax = 10, WaterMin = 4, SunMax = 8, SunMin = 2, GrowthPotential = 0.4f, CanSpawnPlants = true },
                new TileThresholdEntry { TileId = "snow", WaterMax = 5, WaterMin = 0, SunMax = 4, SunMin = 0, GrowthPotential = 0f },
                new TileThresholdEntry { TileId = "plant", WaterMax = 10, WaterMin = 3, SunMax = 10, SunMin = 4, GrowthPotential = 0.8f, CanSpawnPlants = true },
                new TileThresholdEntry { TileId = "tree", WaterMax = 10, WaterMin = 3, SunMax = 10, SunMin = 4, GrowthPotential = 0.7f, CanSpawnPlants = true },
                new TileThresholdEntry { TileId = "bush", WaterMax = 10, WaterMin = 3, SunMax = 10, SunMin = 4, GrowthPotential = 0.6f, CanSpawnPlants = true },
                new TileThresholdEntry { TileId = "flowers", WaterMax = 8, WaterMin = 2, SunMax = 10, SunMin = 4, GrowthPotential = 0.5f, CanSpawnPlants = true },
                new TileThresholdEntry { TileId = "half_snow", WaterMax = 5, WaterMin = 0, SunMax = 4, SunMin = 0, GrowthPotential = 0f },
                new TileThresholdEntry { TileId = "half_ice", WaterMax = 5, WaterMin = 0, SunMax = 3, SunMin = 0, GrowthPotential = 0f },
            };
        }

        private static WaterPropagationEntry[] DefaultWaterPropagation()
        {
            return new[]
            {
                new WaterPropagationEntry { SourceTileId = "stream", PropagationByDistance = new[] { 2, 1 } },
                new WaterPropagationEntry { SourceTileId = "fresh_water", PropagationByDistance = new[] { 3, 2, 1 } },
                new WaterPropagationEntry { SourceTileId = "shore", PropagationByDistance = new[] { 1, 0 } },
            };
        }

        private static TileConversionRuleEntry[] DefaultTileConversionRules()
        {
            return new[]
            {
                new TileConversionRuleEntry { SourceTileType = "grass", ExcessType = ExcessType.Water, RequiredDays = 3, TargetTileType = "muddy" },
                new TileConversionRuleEntry { SourceTileType = "grass", ExcessType = ExcessType.Sun, RequiredDays = 5, TargetTileType = "barren_dry" },
                new TileConversionRuleEntry { SourceTileType = "grass", ExcessType = ExcessType.Earth, RequiredDays = 3, TargetTileType = "snow" },

                new TileConversionRuleEntry { SourceTileType = "dirt", ExcessType = ExcessType.Water, RequiredDays = 3, TargetTileType = "muddy" },
                new TileConversionRuleEntry { SourceTileType = "dirt", ExcessType = ExcessType.Sun, RequiredDays = 3, TargetTileType = "barren_dry" },

                new TileConversionRuleEntry { SourceTileType = "muddy", ExcessType = ExcessType.Water, RequiredDays = 3, TargetTileType = "stream" },
                new TileConversionRuleEntry { SourceTileType = "muddy", ExcessType = ExcessType.Sun, RequiredDays = 3, TargetTileType = "barren_dry" },

                new TileConversionRuleEntry { SourceTileType = "stream", ExcessType = ExcessType.Water, RequiredDays = 5, TargetTileType = "fresh_water" },
                new TileConversionRuleEntry { SourceTileType = "stream", ExcessType = ExcessType.Sun, RequiredDays = 5, TargetTileType = "barren_dry" },

                new TileConversionRuleEntry { SourceTileType = "barren_dry", ExcessType = ExcessType.Water, RequiredDays = 5, TargetTileType = "patchy_dirt" },
                new TileConversionRuleEntry { SourceTileType = "barren_dry", ExcessType = ExcessType.Perfect, RequiredDays = 7, TargetTileType = "grass" },

                new TileConversionRuleEntry { SourceTileType = "sand", ExcessType = ExcessType.Water, RequiredDays = 5, TargetTileType = "muddy" },
                new TileConversionRuleEntry { SourceTileType = "sand", ExcessType = ExcessType.Sun, RequiredDays = 3, TargetTileType = "barren_dry" },

                new TileConversionRuleEntry { SourceTileType = "stone", ExcessType = ExcessType.Water, RequiredDays = 7, TargetTileType = "sand" },

                new TileConversionRuleEntry { SourceTileType = "patchy_dirt", ExcessType = ExcessType.Water, RequiredDays = 3, TargetTileType = "muddy" },
                new TileConversionRuleEntry { SourceTileType = "patchy_dirt", ExcessType = ExcessType.Sun, RequiredDays = 3, TargetTileType = "barren_dry" },
                new TileConversionRuleEntry { SourceTileType = "patchy_dirt", ExcessType = ExcessType.Perfect, RequiredDays = 5, TargetTileType = "grass" },

                new TileConversionRuleEntry { SourceTileType = "plant", ExcessType = ExcessType.Water, RequiredDays = 2, TargetTileType = "grass" },
                new TileConversionRuleEntry { SourceTileType = "plant", ExcessType = ExcessType.Sun, RequiredDays = 2, TargetTileType = "dirt" },
                new TileConversionRuleEntry { SourceTileType = "plant", ExcessType = ExcessType.Earth, RequiredDays = 2, TargetTileType = "snow" },

                new TileConversionRuleEntry { SourceTileType = "tree", ExcessType = ExcessType.Water, RequiredDays = 3, TargetTileType = "grass" },
                new TileConversionRuleEntry { SourceTileType = "tree", ExcessType = ExcessType.Sun, RequiredDays = 3, TargetTileType = "dirt" },
                new TileConversionRuleEntry { SourceTileType = "tree", ExcessType = ExcessType.Earth, RequiredDays = 3, TargetTileType = "snow" },
            };
        }

        private static PlantGrowthConfig DefaultPlantGrowth()
        {
            return new PlantGrowthConfig
            {
                WaterThreshold = 7,
                SunThreshold = 7,
                CheckIntervalDays = 7,
                ClusterRadius = 3,
                BaseSpawnChance = 0.05f,
                PerPlantBonus = 0.03f,
                MaxSpawnChance = 0.5f,
                RevertTileId = "dirt"
            };
        }
    }
}
