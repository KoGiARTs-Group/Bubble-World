using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class SourceLogicSystem : IWorldTimeReceiver
    {
        private readonly SourceLogicConfig config;
        private readonly WeatherManager weather;
        private readonly BubbleModel bubble;
        private readonly DeterministicRandom random;
        private readonly Dictionary<(int, int, int), int> daysSincePlantCheck = new Dictionary<(int, int, int), int>();
        private int dayCount;
        private SeasonType currentSeason;

        public SourceLogicSystem(SourceLogicConfig config, WeatherManager weather, BubbleModel bubble, int seed)
        {
            this.config = config;
            this.weather = weather;
            this.bubble = bubble;
            random = new DeterministicRandom(seed);
        }

        public void OnGameTick(float deltaSeconds, WorldTimeContext context)
        {
            if (context.DayCount == dayCount) return;
            dayCount = context.DayCount;
            currentSeason = context.Season;

            var weatherType = weather.CurrentWeather;
            var conversion = GetConversion(weatherType);

            ApplyWeatherBonuses(conversion);

            foreach (var kv in bubble.Cells)
            {
                var cell = kv.Value;
                if (cell == null || !cell.HasTileData) continue;
                var key = kv.Key;

                AccumulateResources(cell, conversion);
                CheckExcess(cell, key);
                CheckPuddleFormation(cell, key);
                CheckOverflow(cell, key);
            }

            PropagateWater();
            CheckPlantGrowth();
        }

        public void OnFastForward(float totalMissedSeconds, WorldTimeContext context)
        {
            int daysMissed = Mathf.FloorToInt(totalMissedSeconds / 86400f);
            if (daysMissed <= 0) return;

            for (int i = 0; i < daysMissed; i++)
            {
                dayCount++;
                currentSeason = context.Season;

                var weatherType = weather.CurrentWeather;
                var conversion = GetConversion(weatherType);

                foreach (var kv in bubble.Cells)
                {
                    var cell = kv.Value;
                    if (cell == null || !cell.HasTileData) continue;
                    var key = kv.Key;

                    AccumulateResources(cell, conversion);
                    CheckExcess(cell, key);
                    CheckPuddleFormation(cell, key);
                }

                PropagateWater();
                CheckPlantGrowth();
            }
        }

        private void AccumulateResources(BubbleCell cell, WeatherConversionEntry conversion)
        {
            var threshold = config.GetThreshold(cell.ItemId);
            if (threshold == null) return;

            bool isSnowSeason = currentSeason == SeasonType.Winter;

            if (conversion.SnowAmount > 0 && isSnowSeason)
            {
                cell.State.EarthAccumulation += conversion.SnowAmount;
            }
            else if (conversion.SnowAmount > 0 && !isSnowSeason)
            {
                cell.State.WaterAccumulation += conversion.SnowAmount / 2 + 1;
            }

            cell.State.WaterAccumulation += conversion.WaterAmount;
            cell.State.SunAccumulation += conversion.SunAmount;

            cell.State.CurrentMoisture = Mathf.Clamp01(
                cell.State.CurrentMoisture + conversion.WaterAmount * 0.05f);

            if (cell.State.WaterAccumulation > threshold.WaterMax)
            {
                cell.State.SurfaceWaterAmount = Mathf.Min(
                    cell.State.SurfaceWaterAmount + conversion.WaterAmount * 0.1f, 5f);
            }

            cell.State.DaysSinceLastCheck++;
        }

        private void CheckExcess(BubbleCell cell, (int x, int y, int z) key)
        {
            var threshold = config.GetThreshold(cell.ItemId);
            if (threshold == null) return;

            bool waterExcess = cell.State.WaterAccumulation >= threshold.WaterMax * 1.5f;
            bool sunExcess = cell.State.SunAccumulation >= threshold.SunMax * 1.5f;
            bool earthExcess = cell.State.EarthAccumulation >= 10;

            bool isPerfect = !waterExcess && !sunExcess && !earthExcess
                && cell.State.WaterAccumulation >= threshold.WaterMin
                && cell.State.SunAccumulation >= threshold.SunMin;

            ExcessType? activeExcess = null;
            if (waterExcess) activeExcess = ExcessType.Water;
            else if (sunExcess) activeExcess = ExcessType.Sun;
            else if (earthExcess) activeExcess = ExcessType.Earth;
            else if (isPerfect) activeExcess = ExcessType.Perfect;

            if (activeExcess == null)
            {
                cell.State.ConsecutiveExcessDays = 0;
                return;
            }

            cell.State.ConsecutiveExcessDays++;

            var rule = FindConversionRule(cell.ItemId, activeExcess.Value);
            if (rule != null && cell.State.ConsecutiveExcessDays >= rule.RequiredDays)
            {
                TryConvertTile(cell, key, rule.TargetTileType);
            }
        }

        private TileConversionRuleEntry FindConversionRule(string sourceTileId, ExcessType excessType)
        {
            foreach (var rule in config.TileConversionRules)
            {
                if (rule.SourceTileType == sourceTileId && rule.ExcessType == excessType)
                    return rule;
            }
            return null;
        }

        private void CheckPuddleFormation(BubbleCell cell, (int x, int y, int z) key)
        {
            var threshold = config.GetThreshold(cell.ItemId);
            if (threshold == null) return;

            if (cell.State.WaterAccumulation >= threshold.WaterMax * config.PuddleTriggerMultiplier
                && cell.ItemId != "stream" && cell.ItemId != "fresh_water")
            {
                bool hasWaterSource = HasConnectedWaterSource(key.x, key.y, key.z, cell.ItemId);
                if (!hasWaterSource)
                {
                    var targetDef = BubblePlacementCatalog.Find(config.PuddleTileId);
                    if (targetDef != null)
                    {
                        cell.State.WaterAccumulation = threshold.WaterMax;
                        cell.State.SurfaceWaterAmount = Mathf.Min(
                            cell.State.SurfaceWaterAmount + 1f, 3f);
                    }
                }
            }
        }

        private bool HasConnectedWaterSource(int x, int y, int z, string tileId)
        {
            var visited = new HashSet<(int, int, int)>();
            var queue = new Queue<(int, int, int)>();
            queue.Enqueue((x, y, z));
            visited.Add((x, y, z));

            var waterIds = new HashSet<string> { "fresh_water", "stream", "shore" };

            int searchRadius = 15;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var cell = bubble.Get(current.Item1, current.Item2, current.Item3);
                if (cell != null && waterIds.Contains(cell.ItemId))
                {
                    if (cell.ItemId == "fresh_water" || cell.ItemId == "shore")
                        return true;
                }

                if (System.Math.Abs(current.Item1 - x) > searchRadius ||
                    System.Math.Abs(current.Item3 - z) > searchRadius)
                    continue;

                var neighbors = new (int, int, int)[]
                {
                    (current.Item1 + 1, y, current.Item3),
                    (current.Item1 - 1, y, current.Item3),
                    (current.Item1, y, current.Item3 + 1),
                    (current.Item1, y, current.Item3 - 1),
                };

                foreach (var n in neighbors)
                {
                    if (!visited.Contains(n) && bubble.IsInside(n.Item1, y, n.Item3))
                    {
                        var nCell = bubble.Get(n.Item1, y, n.Item3);
                        if (nCell != null && nCell.HasTileData)
                        {
                            visited.Add(n);
                            queue.Enqueue(n);
                        }
                    }
                }
            }

            return false;
        }

        private void TryConvertTile(BubbleCell cell, (int x, int y, int z) key, string targetTileType)
        {
            if (targetTileType == "snow" && currentSeason != SeasonType.Winter)
                return;

            bool needsWaterSource = (targetTileType == "stream" || targetTileType == "fresh_water");
            if (needsWaterSource && !HasConnectedWaterSource(key.x, key.y, key.z, cell.ItemId))
                return;

            var targetDef = BubblePlacementCatalog.Find(targetTileType);
            if (targetDef == null) return;

            bool isPlantOrTree = cell.IsObject && (cell.ItemId == "plant" || cell.ItemId == "tree"
                || cell.ItemId == "bush" || cell.ItemId == "flowers");

            if (isPlantOrTree && (targetTileType == "dirt" || targetTileType == "grass" || targetTileType == "snow"))
            {
                if (cell.GroundItemId != null)
                {
                    var groundDef = BubblePlacementCatalog.Find(cell.GroundItemId);
                    if (groundDef != null)
                    {
                        bubble.Remove(key.x, key.y, key.z);
                        bubble.Place(key.x, key.y, key.z, groundDef);
                    }
                    else
                    {
                        var revertDef = BubblePlacementCatalog.Find(targetTileType);
                        if (revertDef != null)
                        {
                            bubble.Remove(key.x, key.y, key.z);
                            bubble.Place(key.x, key.y, key.z, revertDef);
                        }
                    }
                }
                else
                {
                    bubble.Remove(key.x, key.y, key.z);
                    var revertDef = BubblePlacementCatalog.Find(targetTileType);
                    if (revertDef != null)
                        bubble.Place(key.x, key.y, key.z, revertDef);
                }

                ResetAccumulation(cell);
                return;
            }

            bubble.Remove(key.x, key.y, key.z);
            bubble.Place(key.x, key.y, key.z, targetDef);

            var newCell = bubble.Get(key.x, key.y, key.z);
            if (newCell != null)
            {
                ResetAccumulation(newCell);
                InitializeNewTileState(newCell);
            }
        }

        private static void InitializeNewTileState(BubbleCell cell)
        {
            cell.State.CurrentMoisture = 0.3f;
            cell.State.CurrentFertility = 0.3f;

            if (cell.ItemId == "stream" || cell.ItemId == "fresh_water")
            {
                cell.State.CurrentMoisture = 0.9f;
                cell.State.SurfaceWaterAmount = 1f;
            }
            else if (cell.ItemId == "muddy")
            {
                cell.State.CurrentMoisture = 0.7f;
                cell.State.SurfaceWaterAmount = 0.2f;
            }
            else if (cell.ItemId == "snow")
            {
                cell.State.CurrentMoisture = 0.5f;
            }
        }

        private static void ResetAccumulation(BubbleCell cell)
        {
            cell.State.WaterAccumulation = 0;
            cell.State.SunAccumulation = 0;
            cell.State.EarthAccumulation = 0;
            cell.State.ConsecutiveExcessDays = 0;
        }

        private void PropagateWater()
        {
            var toProcess = new Dictionary<(int, int, int), float>();

            foreach (var kv in bubble.Cells)
            {
                var cell = kv.Value;
                if (cell == null) continue;
                if (cell.ItemId != "stream" && cell.ItemId != "fresh_water" && cell.ItemId != "shore")
                    continue;

                var propConfig = config.GetPropagation(cell.ItemId);
                if (propConfig == null) continue;

                bool isHalfTile = cell.TileHeight == HeightMode.Half;
                float moistureMultiplier = isHalfTile ? 2f : 1f;

                for (int dist = 0; dist < propConfig.PropagationByDistance.Length; dist++)
                {
                    int waterAmount = propConfig.PropagationByDistance[dist];
                    if (waterAmount <= 0) continue;

                    PropagateToDistance(kv.Key.x, kv.Key.y, kv.Key.z, dist + 1, waterAmount * moistureMultiplier, toProcess);
                }
            }

            foreach (var kv in toProcess)
            {
                var cell = bubble.Get(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3);
                if (cell == null) continue;

                float moistureDelta = kv.Value * 0.05f;
                cell.State.CurrentMoisture = Mathf.Clamp01(cell.State.CurrentMoisture + moistureDelta);
                cell.State.WaterAccumulation += Mathf.RoundToInt(kv.Value);

                if (cell.State.WaterAccumulation > 10)
                {
                    cell.State.SurfaceWaterAmount = Mathf.Min(
                        cell.State.SurfaceWaterAmount + moistureDelta, 3f);
                }
            }
        }

        private static void PropagateToDistance(int cx, int cy, int cz, int distance, float amount,
            Dictionary<(int, int, int), float> accumulator)
        {
            for (int dz = -distance; dz <= distance; dz++)
            {
                for (int dx = -distance; dx <= distance; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    if (System.Math.Abs(dx) + System.Math.Abs(dz) != distance) continue;

                    var key = (cx + dx, cy, cz + dz);
                    if (accumulator.ContainsKey(key))
                        accumulator[key] += amount;
                    else
                        accumulator[key] = amount;
                }
            }
        }

        private void CheckPlantGrowth()
        {
            var growth = config.PlantGrowth;
            if (growth == null) return;

            var spawnCandidates = new List<(int x, int y, int z)>();
            var revertCandidates = new List<(int x, int y, int z)>();

            foreach (var kv in bubble.Cells)
            {
                var cell = kv.Value;
                if (cell == null) continue;
                if (cell.State.DaysSinceLastCheck < growth.CheckIntervalDays) continue;

                cell.State.DaysSinceLastCheck = 0;

                bool meetsWater = cell.State.WaterAccumulation >= growth.WaterThreshold;
                bool meetsSun = cell.State.SunAccumulation >= growth.SunThreshold;

                var threshold = config.GetThreshold(cell.ItemId);

                bool inRange = threshold != null
                    && cell.State.WaterAccumulation >= threshold.WaterMin
                    && cell.State.WaterAccumulation <= threshold.WaterMax
                    && cell.State.SunAccumulation >= threshold.SunMin
                    && cell.State.SunAccumulation <= threshold.SunMax;
                bool isExtreme = threshold != null
                    && (cell.State.WaterAccumulation > threshold.WaterMax * 1.8f
                        || cell.State.SunAccumulation > threshold.SunMax * 1.8f);

                if (inRange && meetsWater && meetsSun)
                {
                    UpdateGrowthPotential(cell, kv.Key);
                }

                if (isExtreme && (cell.ItemId == "plant" || cell.ItemId == "tree"
                    || cell.ItemId == "bush" || cell.ItemId == "flowers"))
                {
                    revertCandidates.Add(kv.Key);
                }

                if (cell.IsObject && (cell.ItemId == "tree" || cell.ItemId == "bush"
                    || cell.ItemId == "flowers" || cell.ItemId == "plant"))
                {
                    continue;
                }

                bool canSpawn = threshold != null && threshold.CanSpawnPlants;
                if (!canSpawn) continue;
                if (!inRange) continue;

                float spawnChance = growth.BaseSpawnChance;
                int nearbyCount = CountNearbyPlants(kv.Key.x, kv.Key.y, kv.Key.z, growth.ClusterRadius);
                spawnChance += nearbyCount * growth.PerPlantBonus;
                spawnChance = Mathf.Min(spawnChance, growth.MaxSpawnChance);

                if (random.Value01() < spawnChance)
                {
                    spawnCandidates.Add(kv.Key);
                }

                cell.State.WaterAccumulation = Mathf.Max(0, cell.State.WaterAccumulation - growth.WaterThreshold);
                cell.State.SunAccumulation = Mathf.Max(0, cell.State.SunAccumulation - growth.SunThreshold);
                cell.State.GrowthPotential = Mathf.Clamp01(spawnChance);
            }

            foreach (var key in revertCandidates)
            {
                var cell = bubble.Get(key.x, key.y, key.z);
                if (cell == null) continue;

                string revertId = growth.RevertTileId;
                if (cell.State.EarthAccumulation > 10 && currentSeason == SeasonType.Winter)
                    revertId = "snow";

                var revertDef = BubblePlacementCatalog.Find(revertId);
                if (revertDef != null)
                {
                    bubble.Remove(key.x, key.y, key.z);
                    bubble.Place(key.x, key.y, key.z, revertDef);
                }
            }

            foreach (var key in spawnCandidates)
            {
                TrySpawnPlant(key.x, key.y, key.z, growth);
            }
        }

        private void UpdateGrowthPotential(BubbleCell cell, (int x, int y, int z) key)
        {
            var growth = config.PlantGrowth;
            int nearby = CountNearbyPlants(key.x, key.y, key.z, growth.ClusterRadius);
            cell.State.GrowthPotential = Mathf.Clamp01(
                config.PlantGrowth.BaseSpawnChance + nearby * config.PlantGrowth.PerPlantBonus);
        }

        private int CountNearbyPlants(int x, int y, int z, int radius)
        {
            int count = 0;
            var plantIds = new HashSet<string> { "plant", "tree", "bush", "flowers" };

            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var cell = bubble.Get(x + dx, y, z + dz);
                    if (cell != null && (plantIds.Contains(cell.ItemId) || (cell.IsObject && cell.ItemId == "plant")))
                        count++;
                }
            }

            return count;
        }

        private void TrySpawnPlant(int x, int y, int z, PlantGrowthConfig growth)
        {
            var targetDef = BubblePlacementCatalog.Find("plant");
            if (targetDef == null) return;

            var neighbors = new (int dx, int dz)[]
            {
                (1, 0), (-1, 0), (0, 1), (0, -1)
            };

            var candidates = new List<(int nx, int nz)>();

            foreach (var n in neighbors)
            {
                int nx = x + n.dx;
                int nz = z + n.dz;
                if (!bubble.IsInside(nx, y, nz)) continue;
                var nCell = bubble.Get(nx, y, nz);
                if (nCell == null || !nCell.HasTileData) continue;
                if (nCell.IsObject) continue;

                var threshold = config.GetThreshold(nCell.ItemId);
                if (threshold == null || !threshold.CanSpawnPlants) continue;
                candidates.Add((nx, nz));
            }

            if (candidates.Count == 0) return;

            var chosen = candidates[random.RangeInclusive(0, candidates.Count - 1)];
            bubble.Place(chosen.nx, y, chosen.nz, targetDef);
        }

        private WeatherConversionEntry GetConversion(WeatherType weatherType)
        {
            foreach (var entry in config.WeatherConversion)
            {
                if (entry.Weather == weatherType)
                    return entry;
            }
            return new WeatherConversionEntry { WaterAmount = 0, SunAmount = 0 };
        }

        private void ApplyWeatherBonuses(WeatherConversionEntry conversion)
        {
            if (weather == null) return;

            int waterBonus = weather.GetWaterBonus();
            int sunBonus = weather.GetSunBonus();
            int snowBonus = weather.GetSnowBonus();

            if (waterBonus <= 0 && sunBonus <= 0 && snowBonus <= 0) return;

            foreach (var kv in bubble.Cells)
            {
                var cell = kv.Value;
                if (cell == null || !cell.HasTileData) continue;

                if (waterBonus > 0)
                {
                    cell.State.WaterAccumulation += waterBonus;
                    cell.State.CurrentMoisture = Mathf.Clamp01(cell.State.CurrentMoisture + waterBonus * 0.02f);
                }

                if (sunBonus > 0)
                    cell.State.SunAccumulation += sunBonus;

                if (snowBonus > 0 && currentSeason == SeasonType.Winter)
                    cell.State.EarthAccumulation += snowBonus;
            }
        }

        private void CheckOverflow(BubbleCell cell, (int x, int y, int z) key)
        {
            var threshold = config.GetThreshold(cell.ItemId);
            if (threshold == null) return;

            if (cell.State.WaterAccumulation >= threshold.WaterMax * 2f
                && cell.Composition.FluidLayer == FluidType.None)
            {
                bool hasWaterSource = HasConnectedWaterSource(key.x, key.y, key.z, cell.ItemId);
                if (hasWaterSource)
                {
                    cell.Composition.FluidLayer = FluidType.StandingWater;
                    cell.State.SurfaceWaterAmount = Mathf.Min(cell.State.SurfaceWaterAmount + 0.5f, 2f);
                }
            }
        }
    }
}
