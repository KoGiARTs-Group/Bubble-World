using System.Collections.Generic;
using System.Linq;

namespace BubbleWorld
{
    public static class BubblePlacementCatalog
    {
        public static PlacementItem FindItemAsset(string id)
        {
            PlacementCatalog asset = BubbleWorldApp.ActiveCatalog;
            if (asset == null) return null;
            foreach (PlacementItem item in asset.Items)
                if (item != null && item.Id == id) return item;
            return null;
        }

        public static IReadOnlyList<BubblePlacementDefinition> All =>
            BubbleWorldApp.ActiveCatalog != null
                ? BubbleWorldApp.ActiveCatalog.Definitions
                : (IReadOnlyList<BubblePlacementDefinition>)HardcodedItems;

        public static BubblePlacementDefinition Find(string id)
        {
            if (BubbleWorldApp.ActiveCatalog != null)
            {
                var fromCatalog = BubbleWorldApp.ActiveCatalog.Find(id);
                if (fromCatalog != null) return fromCatalog;
                // Defensive fallback: items that exist only in the hardcoded list
                // (e.g. dead_deer) still resolve when a catalog asset is active.
            }
            return HardcodedItems.FirstOrDefault(item => item.Id == id);
        }

        public static List<BubblePlacementDefinition> ForTab(BuildTab tab)
        {
            if (BubbleWorldApp.ActiveCatalog != null)
                return BubbleWorldApp.ActiveCatalog.ForTab(tab);
            return HardcodedItems.Where(item => item.Tab == tab).ToList();
        }

        public static BubblePlacementDefinition DefaultFor(StarterEnvironment environment)
        {
            if (BubbleWorldApp.ActiveCatalog != null)
                return BubbleWorldApp.ActiveCatalog.DefaultFor(environment);
            switch (environment)
            {
                case StarterEnvironment.Forest: return Find("tree");
                case StarterEnvironment.BeachCoastal: return Find("sand");
                default: return Find("grass");
            }
        }

        public static readonly List<BubblePlacementDefinition> HardcodedItems = new List<BubblePlacementDefinition>
        {
            New("grass", "Grass", BuildTab.Terrain, LayerBand.GroundWaterBase, "#5DA64B", false, 1, 0, 0, 1, 0, 0, 1, 1),
            New("dirt", "Dirt", BuildTab.Terrain, LayerBand.GroundWaterBase, "#8B6239", false, 0, 0, 0, 0, 0, 0, 2, 1),
            New("sand", "Sand", BuildTab.Terrain, LayerBand.GroundWaterBase, "#D8C16D", false, 0, 0, 0, 1, 0, 1, 1, 1),
            New("stone", "Stone", BuildTab.Terrain, LayerBand.GroundWaterBase, "#858A8E", false, 0, 0, 1, 0, 2, 0, 3, 0),
            New("hill", "Small Hill", BuildTab.Terrain, LayerBand.ElevatedTerrain, "#7F9B55", false, 0, 0, 2, 1, 1, 0, 2, -1),
            New("barren_dry", "Barren Dry", BuildTab.Terrain, LayerBand.GroundWaterBase, "#A0895C", false, 0, 0, 0, 1, 0, 0, 1, 1, tileCategory: "dirt"),
            New("muddy", "Muddy", BuildTab.Terrain, LayerBand.GroundWaterBase, "#6B5430", false, 0, 2, 0, 0, 0, 0, 0, 0, tileCategory: "mud"),
            New("patchy_dirt", "Patchy Dirt", BuildTab.Terrain, LayerBand.GroundWaterBase, "#8B7A4A", false, 1, 0, 0, 1, 0, 0, 1, 1, tileCategory: "dirt"),
            New("snow", "Snow", BuildTab.Terrain, LayerBand.GroundWaterBase, "#E8E8F0", false, 0, 1, 0, 0, 0, 1, 1, 1, tileCategory: "snow"),
            New("half_snow", "Half Snow", BuildTab.Terrain, LayerBand.GroundWaterBase, "#D0D0E0", false, 0, 0, 0, 0, 0, 0, 1, 1, tileCategory: "snow"),
            New("half_ice", "Half Ice", BuildTab.Terrain, LayerBand.GroundWaterBase, "#B8D4E8", false, 0, 0, 0, 0, 0, 0, 1, 1, tileCategory: "ice"),
            New("stream", "Stream", BuildTab.Water, LayerBand.GroundWaterBase, "#6BB5FF", false, 0, 4, 0, 0, 0, 1, 1, 1, tileCategory: "water"),
            New("plant", "Plant", BuildTab.Plants, LayerBand.LivingEnvironment, "#4CAF50", true, 2, 0, 1, 0, 1, 0, 0, -1),
            New("half_vertical_front", "Half Vertical Front", BuildTab.Terrain, LayerBand.GroundWaterBase, "#B0B0B0", false, 0, 0, 0, 0, 0, 0, 0, 0, tileCategory: "half_vert", fixedVariant: TileVariant.HalfVerticalFront),
            New("half_vertical_back", "Half Vertical Back", BuildTab.Terrain, LayerBand.GroundWaterBase, "#A0A0A0", false, 0, 0, 0, 0, 0, 0, 0, 0, tileCategory: "half_vert", fixedVariant: TileVariant.HalfVerticalBack),
            New("half_vertical_left", "Half Vertical Left", BuildTab.Terrain, LayerBand.GroundWaterBase, "#909090", false, 0, 0, 0, 0, 0, 0, 0, 0, tileCategory: "half_vert", fixedVariant: TileVariant.HalfVerticalLeft),
            New("half_vertical_right", "Half Vertical Right", BuildTab.Terrain, LayerBand.GroundWaterBase, "#808080", false, 0, 0, 0, 0, 0, 0, 0, 0, tileCategory: "half_vert", fixedVariant: TileVariant.HalfVerticalRight),
            New("fresh_water", "Fresh Water", BuildTab.Water, LayerBand.GroundWaterBase, "#4DA6FF", false, 0, 5, 0, 0, 0, 1, 1, 0),
            New("shore", "Soft Shore", BuildTab.Water, LayerBand.GroundWaterBase, "#7DC7D4", false, 0, 3, 0, 1, 0, 1, 1, 1),
            New("tree", "Tree", BuildTab.Plants, LayerBand.LivingEnvironment, "#226E34", true, 3, 0, 3, -1, 1, 1, 2, -1),
            New("dead_deer", "Dead Deer", BuildTab.Resources, LayerBand.LivingEnvironment, "#7A5A44", true, 5, 0, 0, 0, 0, 0, 1, -1),
            New("bush", "Berry Bush", BuildTab.Plants, LayerBand.LivingEnvironment, "#3B8E3D", true, 4, 0, 2, 0, 1, 0, 1, -1),
            New("flowers", "Flowers", BuildTab.Plants, LayerBand.LivingEnvironment, "#E879B9", true, 2, 0, 0, 1, 1, 0, 0, 0),
            New("rocks", "Rocks", BuildTab.Resources, LayerBand.LivingEnvironment, "#6F7376", true, 0, 0, 2, 0, 2, 0, 3, -1),
            New("wood", "Wood Pile", BuildTab.Resources, LayerBand.LivingEnvironment, "#9A6338", true, 0, 0, 4, 0, 1, 0, 2, -1),
            New("sunlight", "Sunlight", BuildTab.Climate, LayerBand.SkyAtmosphere, "#FFD45A", true, 1, 0, 0, 5, 1, 2, 1, 0),
            New("gentle_rain", "Gentle Rain", BuildTab.Climate, LayerBand.SkyAtmosphere, "#8EC9FF", true, 2, 4, 0, -1, 0, 3, 1, 0),
            New("wind_break", "Wind Break", BuildTab.Interventions, LayerBand.LivingEnvironment, "#C39C60", true, 0, 0, 3, 0, 3, 3, 2, -1),
            New("simple_shelter", "Simple Shelter", BuildTab.Interventions, LayerBand.LivingEnvironment, "#A77B52", true, 0, 0, 6, 0, 3, 3, 2, -2, 2)
        };

        private static BubblePlacementDefinition New(
            string id, string displayName, BuildTab tab, LayerBand layer,
            string htmlColor, bool isObject,
            int food, int water, int shelter, int light, int safety, int weather, int stability, int space,
            int occupyRadius = 0, string tileCategory = null, TileVariant? fixedVariant = null)
        {
            UnityEngine.ColorUtility.TryParseHtmlString(htmlColor, out UnityEngine.Color color);
            return new BubblePlacementDefinition(id, displayName, tab, layer, color, isObject,
                food, water, shelter, light, safety, weather, stability, space, occupyRadius, tileCategory,
                fixedVariant: fixedVariant);
        }
    }
}
