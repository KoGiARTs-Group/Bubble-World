using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace BubbleWorld.Tests
{
    public sealed class BubbleWorldMilestone1Tests
    {
        private PlacementCatalog testCatalog;

        [SetUp]
        public void Setup()
        {
            testCatalog = ScriptableObject.CreateInstance<PlacementCatalog>();
            testCatalog.Items = new List<PlacementItem>();
            foreach (var def in BubblePlacementCatalog.HardcodedItems)
            {
                var item = ScriptableObject.CreateInstance<PlacementItem>();
                item.Id = def.Id;
                item.DisplayName = def.DisplayName;
                item.Tab = def.Tab;
                item.Layer = def.Layer;
                item.IsObject = def.IsObject;
                item.TintColor = def.Color;
                item.OccupyRadius = def.OccupyRadius;
                item.Food = def.Food;
                item.Water = def.Water;
                item.Shelter = def.Shelter;
                item.Light = def.Light;
                item.Safety = def.Safety;
                item.Weather = def.Weather;
                item.Stability = def.Stability;
                item.Space = def.Space;
                testCatalog.Items.Add(item);
            }
            testCatalog.InvalidateCache();
            BubbleWorldApp.ActiveCatalog = testCatalog;
        }

        [TearDown]
        public void TearDown()
        {
            BubbleWorldApp.ActiveCatalog = null;
            if (testCatalog != null)
                Object.DestroyImmediate(testCatalog);
        }
        [Test]
        public void StarterBubblesUseDistinctEnvironmentData()
        {
            BubbleModel meadow = BubbleStarterFactory.Create(StarterEnvironment.MeadowGrassland);
            BubbleModel forest = BubbleStarterFactory.Create(StarterEnvironment.Forest);
            BubbleModel beach = BubbleStarterFactory.Create(StarterEnvironment.BeachCoastal);

            Assert.AreEqual(StarterEnvironment.MeadowGrassland, meadow.Environment);
            Assert.AreEqual(StarterEnvironment.Forest, forest.Environment);
            Assert.AreEqual(StarterEnvironment.BeachCoastal, beach.Environment);
            Assert.IsNotNull(meadow.Get(6, 0, 6));
            Assert.AreEqual("dirt", forest.Get(6, 0, 6).ItemId);
            Assert.AreEqual("sand", beach.Get(6, 0, 6).ItemId);
        }

        [Test]
        public void BrushSizesPlaceExpectedCellCounts()
        {
            BubbleModel bubble = new BubbleModel(StarterEnvironment.MeadowGrassland, 10, 10);
            BubblePlacementDefinition grass = BubblePlacementCatalog.Find("grass");

            Assert.AreEqual(1, bubble.ApplyBrush(5, 0, 5, BrushSize.One, grass, false));
            Assert.AreEqual(1, bubble.Cells.Count);
            Assert.AreEqual(9, bubble.ApplyBrush(2, 0, 2, BrushSize.Three, grass, false));
            Assert.AreEqual(10, bubble.Cells.Count);
            Assert.AreEqual(25, bubble.ApplyBrush(7, 0, 7, BrushSize.Five, grass, false));
            Assert.AreEqual(34, bubble.Cells.Count);
        }

        [Test]
        public void TerrainAndObjectsCanShareACell()
        {
            BubbleModel bubble = new BubbleModel(StarterEnvironment.MeadowGrassland, 10, 10);

            bubble.Place(4, 0, 4, BubblePlacementCatalog.Find("grass"));
            bubble.Place(4, 1, 4, BubblePlacementCatalog.Find("tree"));

            Assert.AreEqual("grass", bubble.Get(4, 0, 4).ItemId);
            Assert.AreEqual("tree", bubble.Get(4, 1, 4).ItemId);
            Assert.AreEqual(2, bubble.Cells.Count);
        }

        [Test]
        public void BrushClampsAtGridBounds()
        {
            BubbleModel bubble = new BubbleModel(StarterEnvironment.MeadowGrassland, 5, 5);
            BubblePlacementDefinition grass = BubblePlacementCatalog.Find("grass");

            Assert.AreEqual(9, bubble.ApplyBrush(0, 0, 0, BrushSize.Five, grass, false));
            Assert.AreEqual(9, bubble.Cells.Count);
        }

        [Test]
        public void PlacementRemovalChangesLivability()
        {
            BubbleModel bubble = new BubbleModel(StarterEnvironment.MeadowGrassland, 12, 12);
            BubblePlacementDefinition water = BubblePlacementCatalog.Find("fresh_water");
            BubblePlacementDefinition tree = BubblePlacementCatalog.Find("tree");
            int start = BubbleLivability.Calculate(bubble).Percentage;

            bubble.ApplyBrush(5, 0, 5, BrushSize.Three, water, false);
            bubble.ApplyBrush(8, 0, 8, BrushSize.Three, tree, false);
            int improved = BubbleLivability.Calculate(bubble).Percentage;
            bubble.ApplyBrush(5, 0, 5, BrushSize.Three, water, true);
            int reduced = BubbleLivability.Calculate(bubble).Percentage;

            Assert.Greater(improved, start);
            Assert.Less(reduced, improved);
        }

        [Test]
        public void SaveRoundTripsBubbleLayout()
        {
            BubbleModel bubble = BubbleStarterFactory.Create(StarterEnvironment.Forest);
            bubble.ApplyBrush(10, 0, 10, BrushSize.One, BubblePlacementCatalog.Find("simple_shelter"), false);
            string path = Path.Combine(Path.GetTempPath(), "bubbleworld_milestone1_test_save.json");

            try
            {
                BubbleSaveService.Save(bubble, BubbleLivability.Calculate(bubble), path);
                BubbleModel loaded = BubbleSaveService.Load(path);

                Assert.AreEqual(bubble.Environment, loaded.Environment);
                Assert.AreEqual(bubble.Cells.Count, loaded.Cells.Count);
                Assert.AreEqual("dirt", loaded.Get(10, 0, 10).ItemId);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public void GridConfigDefaultsAreValid()
        {
            GridConfig config = GridConfig.CreateDefault();
            Assert.AreEqual(24, config.Width);
            Assert.AreEqual(24, config.Height);
            Assert.AreEqual(1f, config.TileSize);
            Assert.AreEqual(14f, config.DefaultZoom);
            Assert.IsTrue(config.UsePerspectiveCamera);
            Assert.AreEqual(Mesh3DPrimitive.Cube, config.Default3DPrimitive);
            Assert.AreEqual(60f, config.CameraFOV);
            Assert.AreEqual(75f, config.CameraFOV45);
            Assert.AreEqual(45f, config.CameraTilt45X);
            Assert.AreEqual(89.9f, config.CameraTiltTopX);
            Assert.AreEqual(Camera3DView.Angle45, config.Default3DView);
        }

        [Test]
        public void Camera3DViewEnumIsDefined()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(Camera3DView), Camera3DView.Top));
            Assert.IsTrue(System.Enum.IsDefined(typeof(Camera3DView), Camera3DView.Angle45));
        }

        [Test]
        public void PrimitiveMeshUtilityProvidesAllShapes()
        {
            Assert.IsNotNull(PrimitiveMeshUtility.Cube);
            Assert.IsNotNull(PrimitiveMeshUtility.Sphere);
            Assert.IsNotNull(PrimitiveMeshUtility.Cylinder);
            Assert.IsNotNull(PrimitiveMeshUtility.Capsule);
            Assert.IsNotNull(PrimitiveMeshUtility.Plane);
            Assert.IsNotNull(PrimitiveMeshUtility.Quad);
        }

        [Test]
        public void WorldToCellMapsGridCentersCorrectly()
        {
            for (int cx = 0; cx < 24; cx++)
            {
                for (int cz = 0; cz < 24; cz++)
                {
                    float tileSize = 1f;
                    float tileHeight = 0.6f;
                    float offsetX = 24 * 0.5f * tileSize - tileSize * 0.5f;
                    float offsetZ = 24 * 0.5f * tileSize - tileSize * 0.5f;
                    float worldX = cx * tileSize - offsetX;
                    float worldGrid = cz * tileSize - offsetZ;
                    Vector3 worldPos = new Vector3(worldX, 0f, worldGrid);

                    bool ok = BubbleWorldGrid.WorldToCellStatic(
                        worldPos, 24, 24, 8, tileSize, tileHeight, out int rx, out int ry, out int rz);
                    Assert.IsTrue(ok, "Cell ({0},{1}) should be inside the grid", cx, cz);
                    Assert.AreEqual(cx, rx, "X mismatch for cell ({0},{1})", cx, cz);
                    Assert.AreEqual(0, ry, "Y mismatch for cell ({0},{1})", cx, cz);
                    Assert.AreEqual(cz, rz, "Z mismatch for cell ({0},{1})", cx, cz);
                }
            }
        }

        [Test]
        public void WorldToCellReturnsOutOfBoundsForOutsidePoints()
        {
            Vector3 outside = new Vector3(-100f, 0f, -100f);
            Assert.IsFalse(BubbleWorldGrid.WorldToCellStatic(outside, 24, 24, 8, 1f, 0.6f, out int x, out int y, out int z));
        }

        [Test]
        public void CameraRightIsPositiveXForTopView()
        {
            Vector3 cameraPos = new Vector3(0f, 10f, 0.001f);
            Vector3 target = Vector3.zero;
            Vector3 up = Vector3.back;
            Vector3 forward = (target - cameraPos).normalized;
            Vector3 right = Vector3.Cross(forward, up).normalized;
            Assert.Greater(right.x, 0.5f, "Top view camera right should be +X (got " + right + ")");
        }

        [Test]
        public void CameraRightIsPositiveXFor45View()
        {
            Vector3 cameraPos = new Vector3(0f, 10f, 10f);
            Vector3 target = Vector3.zero;
            Vector3 up = Vector3.up;
            Vector3 forward = (target - cameraPos).normalized;
            Vector3 right = Vector3.Cross(forward, up).normalized;
            Assert.Greater(right.x, 0.5f, "45 view camera right should be +X (got " + right + ")");
        }

        [Test]
        public void UIThemeDefaultsAreValid()
        {
            UITheme theme = UITheme.CreateDefault();
            Assert.AreEqual(54, theme.TitleFontSize);
            Assert.AreEqual(36, theme.HeaderFontSize);
            Assert.AreEqual(18, theme.BodyFontSize);
            Assert.AreEqual(14, theme.SmallFontSize);
        }

        [Test]
        public void PlacementItemToDefinitionPreservesData()
        {
            PlacementItem item = ScriptableObject.CreateInstance<PlacementItem>();
            item.Id = "test_grass";
            item.DisplayName = "Test Grass";
            item.Tab = BuildTab.Terrain;
            item.Layer = LayerBand.GroundWaterBase;
            item.TintColor = Color.green;
            item.IsObject = false;
            item.Food = 3;
            item.Water = 1;
            item.Shelter = 0;
            item.Light = 2;
            item.Safety = 0;
            item.Weather = 0;
            item.Stability = 1;
            item.Space = 1;

            BubblePlacementDefinition def = item.ToDefinition();
            Assert.AreEqual("test_grass", def.Id);
            Assert.AreEqual("Test Grass", def.DisplayName);
            Assert.AreEqual(BuildTab.Terrain, def.Tab);
            Assert.AreEqual(LayerBand.GroundWaterBase, def.Layer);
            Assert.AreEqual(3, def.Food);
            Assert.AreEqual(1, def.Water);
        }

        [Test]
        public void LivabilityReportHasAllMetrics()
        {
            BubbleModel bubble = BubbleStarterFactory.Create(StarterEnvironment.MeadowGrassland);
            LivabilityReport report = BubbleLivability.Calculate(bubble);

            Assert.AreEqual(8, report.Metrics.Count);
            Assert.IsTrue(report.Metrics.ContainsKey("Light"));
            Assert.IsTrue(report.Metrics.ContainsKey("Water Access"));
            Assert.IsTrue(report.Metrics.ContainsKey("Food Potential"));
            Assert.IsTrue(report.Metrics.ContainsKey("Shelter"));
            Assert.IsTrue(report.Metrics.ContainsKey("Safety"));
            Assert.IsTrue(report.Metrics.ContainsKey("Temperature"));
            Assert.IsTrue(report.Metrics.ContainsKey("Stability"));
            Assert.IsTrue(report.Metrics.ContainsKey("Space"));
            Assert.GreaterOrEqual(report.Percentage, 0);
            Assert.LessOrEqual(report.Percentage, 100);
        }

        [Test]
        public void WorldConditionsHaveRatings()
        {
            BubbleModel bubble = BubbleStarterFactory.Create(StarterEnvironment.MeadowGrassland);
            LivabilityReport report = BubbleLivability.Calculate(bubble);

            Assert.AreEqual(8, report.WorldConditions.Count);
            foreach (WorldCondition c in report.WorldConditions)
            {
                Assert.IsNotNull(c.Name);
                Assert.GreaterOrEqual(c.Value, 0);
                Assert.LessOrEqual(c.Value, 100);
            }
        }

        [Test]
        public void RatingThresholdsAreCorrect()
        {
            Assert.AreEqual(LivabilityRating.Good, WorldCondition.RateFromValue(100));
            Assert.AreEqual(LivabilityRating.Good, WorldCondition.RateFromValue(75));
            Assert.AreEqual(LivabilityRating.Fair, WorldCondition.RateFromValue(74));
            Assert.AreEqual(LivabilityRating.Fair, WorldCondition.RateFromValue(50));
            Assert.AreEqual(LivabilityRating.Mild, WorldCondition.RateFromValue(49));
            Assert.AreEqual(LivabilityRating.Mild, WorldCondition.RateFromValue(25));
            Assert.AreEqual(LivabilityRating.Poor, WorldCondition.RateFromValue(24));
            Assert.AreEqual(LivabilityRating.Poor, WorldCondition.RateFromValue(0));
        }

        [Test]
        public void LifeConditionsListHasEightItems()
        {
            BubbleModel bubble = BubbleStarterFactory.Create(StarterEnvironment.MeadowGrassland);
            LivabilityReport report = BubbleLivability.Calculate(bubble);

            Assert.AreEqual(8, report.LifeConditions.Count);
            foreach (LifeCondition c in report.LifeConditions)
            {
                Assert.IsNotNull(c.Name);
                Assert.IsNotNull(c.Reason);
            }
        }

        [Test]
        public void AnimaReadyRequiresHighLivability()
        {
            BubbleModel bubble = new BubbleModel(StarterEnvironment.MeadowGrassland, 8, 8);
            LivabilityReport empty = BubbleLivability.Calculate(bubble);
            Assert.IsFalse(empty.AnimaReady);
            Assert.Less(empty.Percentage, 80);
        }

        [Test]
        public void CatalogForTabReturnsCorrectItems()
        {
            var terrainItems = BubblePlacementCatalog.ForTab(BuildTab.Terrain);
            Assert.Greater(terrainItems.Count, 0);
            foreach (var item in terrainItems)
            {
                Assert.AreEqual(BuildTab.Terrain, item.Tab);
            }

            var waterItems = BubblePlacementCatalog.ForTab(BuildTab.Water);
            Assert.Greater(waterItems.Count, 0);
            foreach (var item in waterItems)
            {
                Assert.AreEqual(BuildTab.Water, item.Tab);
            }
        }
    }
}
