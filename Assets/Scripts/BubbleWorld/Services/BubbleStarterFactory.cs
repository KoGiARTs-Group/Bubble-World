using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    public static class BubbleStarterFactory
    {
        public static BubbleModel Create(StarterEnvironment environment)
        {
            var bubble = new BubbleModel(environment, 60, 60, 8);

            switch (environment)
            {
                case StarterEnvironment.MeadowGrassland:
                    CreateMeadow(bubble);
                    break;
                case StarterEnvironment.Forest:
                    CreateForest(bubble);
                    break;
                case StarterEnvironment.BeachCoastal:
                    CreateBeach(bubble);
                    break;
                default:
                    CreateMeadow(bubble);
                    break;
            }

            for (int z = 0; z < bubble.Height; z++)
            {
                for (int x = 0; x < bubble.Width; x++)
                {
                    AutoTileSystem.ApplyToCell(bubble, x, 0, z);
                }
            }

            return bubble;
        }

        private static void CreateMeadow(BubbleModel bubble)
        {
            var dryDirt = BubblePlacementCatalog.Find("barren_dry");
            var grass = BubblePlacementCatalog.Find("grass");
            var freshWater = BubblePlacementCatalog.Find("fresh_water");

            for (int z = 0; z < 60; z++)
            {
                for (int x = 0; x < 60; x++)
                {
                    bubble.Place(x, 0, z, dryDirt);
                    var cell = bubble.Get(x, 0, z);
                    if (cell != null)
                    {
                        cell.Composition = new TileComposition
                        {
                            BaseMaterial = BaseMaterial.Earth,
                            SurfaceCover = SurfaceCover.BareDirt,
                            FluidLayer = FluidType.None,
                            FluidData = FluidLayerData.None
                        };
                        cell.State = TileState.Default(28f);
                        cell.State.CurrentMoisture = 0.15f;
                    }
                }
            }

            var streamPoints = GenerateWindingStream(15, 5, 55, 42);
            PlaceWaterTiles(bubble, streamPoints, freshWater);

            foreach (var pt in streamPoints)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        int nx = pt.x + dx, nz = pt.z + dz;
                        if (!bubble.IsInside(nx, 0, nz)) continue;
                        var cell = bubble.Get(nx, 0, nz);
                        if (cell != null && cell.Composition.BaseMaterial == BaseMaterial.Earth
                            && cell.Composition.SurfaceCover == SurfaceCover.BareDirt)
                        {
                            float dist = Mathf.Abs(dx) + Mathf.Abs(dz);
                            if (dist <= 3f)
                            {
                                cell.Composition.SurfaceCover = SurfaceCover.Grass;
                                cell.State.CurrentMoisture = Mathf.Lerp(0.8f, 0.2f, dist / 3f);
                                cell.State.CurrentFertility = Mathf.Lerp(0.6f, 0.3f, dist / 3f);
                            }
                        }
                    }
                }
            }

            int midIdx = streamPoints.Count / 2;
            int treeX = Mathf.Clamp(streamPoints[midIdx].x + 3, 1, 58);
            int treeZ = Mathf.Clamp(streamPoints[midIdx].z, 1, 58);
            PlaceIfObject(bubble, treeX, 0, treeZ, "tree");
            var treeCell = bubble.Get(treeX, 0, treeZ);
            if (treeCell != null) treeCell.State.ResourceRemaining = 1;

            // Opening condition: a dead deer carcass (meat, 5 units) near the stream.
            int deerX = Mathf.Clamp(streamPoints[midIdx].x - 6, 1, 58);
            int deerZ = Mathf.Clamp(streamPoints[midIdx].z + 5, 1, 58);
            for (int tries = 0; tries < 8; tries++)
            {
                var probe = bubble.Get(deerX, 0, deerZ);
                if (probe == null || probe.Composition.FluidLayer == FluidType.None) break;
                deerX = Mathf.Clamp(deerX + 2, 1, 58);
                deerZ = Mathf.Clamp(deerZ - 1, 1, 58);
            }
            PlaceIfObject(bubble, deerX, 0, deerZ, "dead_deer");
            var deerCell = bubble.Get(deerX, 0, deerZ);
            if (deerCell != null) deerCell.State.ResourceRemaining = 5;

            PlaceIfObject(bubble, treeX + 4, 0, treeZ - 2, "bush");
            PlaceIfObject(bubble, treeX - 3, 0, treeZ + 3, "bush");

            PlaceIfObject(bubble, 10, 0, 10, "sunlight");
        }

        private static void CreateForest(BubbleModel bubble)
        {
            var earth = BubblePlacementCatalog.Find("barren_dry");
            var grass = BubblePlacementCatalog.Find("grass");
            var freshWater = BubblePlacementCatalog.Find("fresh_water");
            var muddy = BubblePlacementCatalog.Find("muddy");

            for (int z = 0; z < 60; z++)
            {
                for (int x = 0; x < 60; x++)
                {
                    bubble.Place(x, 0, z, earth);
                    var cell = bubble.Get(x, 0, z);
                    if (cell != null)
                    {
                        cell.Composition = new TileComposition
                        {
                            BaseMaterial = BaseMaterial.Earth,
                            SurfaceCover = SurfaceCover.Grass,
                            FluidLayer = FluidType.None,
                            FluidData = FluidLayerData.None
                        };
                        cell.State = TileState.Default(24f);
                        cell.State.CurrentMoisture = 0.5f;
                        cell.State.CurrentFertility = 0.6f;
                    }
                }
            }

            var stream1 = GenerateWindingStream(10, 5, 55, 123);
            var stream2 = GenerateWindingStream(30, 5, 55, 456);
            PlaceWaterTiles(bubble, stream1, freshWater);
            PlaceWaterTiles(bubble, stream2, freshWater);

            var rng = new System.Random(789);
            for (int i = 0; i < 20; i++)
            {
                int tx = rng.Next(3, 57);
                int tz = rng.Next(3, 57);
                var existing = bubble.Get(tx, 0, tz);
                if (existing != null && !existing.IsObject && existing.ItemId != "fresh_water")
                {
                    PlaceIfObject(bubble, tx, 0, tz, "tree");
                    var tc = bubble.Get(tx, 0, tz);
                    if (tc != null) tc.State.ResourceRemaining = 1;
                }
            }

            for (int i = 0; i < 10; i++)
            {
                int bx = rng.Next(3, 57);
                int bz = rng.Next(3, 57);
                var existing = bubble.Get(bx, 0, bz);
                if (existing != null && !existing.IsObject)
                    PlaceIfObject(bubble, bx, 0, bz, "bush");
            }

            for (int i = 0; i < 5; i++)
            {
                int mx = rng.Next(3, 57);
                int mz = rng.Next(3, 57);
                var existing = bubble.Get(mx, 0, mz);
                if (existing != null && !existing.IsObject)
                    PlaceIfObject(bubble, mx, 0, mz, "plant");
            }

            PlaceIfObject(bubble, 10, 0, 10, "sunlight");
        }

        private static void CreateBeach(BubbleModel bubble)
        {
            var sand = BubblePlacementCatalog.Find("barren_dry");
            var freshWater = BubblePlacementCatalog.Find("fresh_water");

            for (int z = 0; z < 60; z++)
            {
                for (int x = 0; x < 60; x++)
                {
                    bubble.Place(x, 0, z, sand);
                    var cell = bubble.Get(x, 0, z);
                    if (cell != null)
                    {
                        bool nearEdge = x < 5 || x > 54 || z < 5 || z > 54;
                        cell.Composition = new TileComposition
                        {
                            BaseMaterial = nearEdge ? BaseMaterial.Sand : BaseMaterial.Earth,
                            SurfaceCover = nearEdge ? SurfaceCover.BareDirt : SurfaceCover.Grass,
                            FluidLayer = FluidType.None,
                            FluidData = FluidLayerData.None
                        };
                        cell.State = TileState.Default(30f);
                        cell.State.CurrentMoisture = nearEdge ? 0.3f : 0.4f;

                        if (nearEdge && (x < 4 || x > 55 || z < 4 || z > 55))
                        {
                            cell.Composition.FluidLayer = FluidType.ShallowWater;
                            cell.Composition.SurfaceCover = SurfaceCover.Stream;
                            cell.State.CurrentMoisture = 0.9f;
                            cell.State.SurfaceWaterAmount = 0.5f;
                        }
                    }
                }
            }

            var stream = GenerateWindingStream(25, 5, 55, 321);
            PlaceWaterTiles(bubble, stream, freshWater);

            var rng = new System.Random(654);
            for (int i = 0; i < 4; i++)
            {
                int px = rng.Next(8, 52);
                int pz = rng.Next(8, 52);
                PlaceIfObject(bubble, px, 0, pz, "tree");
            }

            PlaceIfObject(bubble, 10, 0, 10, "sunlight");
        }

        private static void PlaceWaterTiles(BubbleModel bubble, List<(int x, int z)> streamPoints, BubblePlacementDefinition waterDef)
        {
            foreach (var pt in streamPoints)
            {
                bubble.Place(pt.x, 0, pt.z, waterDef);
                var cell = bubble.Get(pt.x, 0, pt.z);
                if (cell != null)
                {
                    cell.Composition = new TileComposition
                    {
                        BaseMaterial = BaseMaterial.Earth,
                        SurfaceCover = SurfaceCover.Mud,
                        FluidLayer = FluidType.FlowingWater,
                        FluidData = FluidLayerData.Flowing(0.3f, 0.5f)
                    };
                    cell.State = TileState.Default(22f);
                    cell.State.CurrentMoisture = 0.8f;
                    cell.State.SurfaceWaterAmount = 1f;
                }
            }

            foreach (var pt in streamPoints)
            {
                var cell = bubble.Get(pt.x, 0, pt.z);
                if (cell == null) continue;

                cell.Composition.FluidData.NorthConnection = bubble.Get(pt.x, 0, pt.z - 1)?.ItemId == waterDef.Id;
                cell.Composition.FluidData.SouthConnection = bubble.Get(pt.x, 0, pt.z + 1)?.ItemId == waterDef.Id;
                cell.Composition.FluidData.EastConnection = bubble.Get(pt.x + 1, 0, pt.z)?.ItemId == waterDef.Id;
                cell.Composition.FluidData.WestConnection = bubble.Get(pt.x - 1, 0, pt.z)?.ItemId == waterDef.Id;

                WaterFlowVisualSystem.ApplyToCell(cell);
            }
        }

        private static List<(int x, int z)> GenerateWindingStream(int startX, int startZ, int endZ, int seed)
        {
            var points = new List<(int x, int z)>();
            int cx = startX, cz = startZ;
            var random = new System.Random(seed);

            while (cz < endZ)
            {
                points.Add((cx, cz));
                cz += random.Next(1, 3);
                cx += random.Next(-1, 2);
                cx = Mathf.Clamp(cx, 5, 55);
            }
            points.Add((cx, endZ));

            return points;
        }

        private static bool IsNearWater(BubbleModel bubble, int x, int z, string waterId, int distance)
        {
            for (int dz = -distance; dz <= distance; dz++)
            {
                for (int dx = -distance; dx <= distance; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    var cell = bubble.Get(nx, 0, nz);
                    if (cell != null && cell.ItemId == waterId)
                        return true;
                }
            }
            return false;
        }

        private static int DistanceToWater(BubbleModel bubble, int x, int z, string waterId)
        {
            int minDist = 10;
            for (int dz = -3; dz <= 3; dz++)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    var cell = bubble.Get(nx, 0, nz);
                    if (cell != null && cell.ItemId == waterId)
                    {
                        int dist = Mathf.Abs(dx) + Mathf.Abs(dz);
                        if (dist < minDist) minDist = dist;
                    }
                }
            }
            return minDist;
        }

        private static void PlaceIfObject(BubbleModel bubble, int x, int y, int z, string itemId)
        {
            var def = BubblePlacementCatalog.Find(itemId);
            if (def == null) return;
            bubble.Place(x, y, z, def);
        }
    }
}
