using UnityEditor;
using UnityEngine;

namespace BubbleWorld
{
    public static class CreatePlacementItems
    {
        [MenuItem("BubbleWorld/Create All Placement Items")]
        private static void CreateAll()
        {
            EnsureFolder("Assets", "PlacementItems");

            string folder = "Assets/PlacementItems";

            CreateItem(folder, "grass", "Grass", BuildTab.Terrain, LayerBand.GroundWaterBase,
                "#5DA64B", false, 1, 0, 0, 1, 0, 0, 1, 1, new Vector3(1, 1, 1), 0f);
            CreateItem(folder, "dirt", "Dirt", BuildTab.Terrain, LayerBand.GroundWaterBase,
                "#8B6239", false, 0, 0, 0, 0, 0, 0, 2, 1, new Vector3(1, 1, 1), 0f);
            CreateItem(folder, "sand", "Sand", BuildTab.Terrain, LayerBand.GroundWaterBase,
                "#D8C16D", false, 0, 0, 0, 1, 0, 1, 1, 1, new Vector3(1, 1, 1), 0f);
            CreateItem(folder, "stone", "Stone", BuildTab.Terrain, LayerBand.GroundWaterBase,
                "#858A8E", false, 0, 0, 1, 0, 2, 0, 3, 0, new Vector3(1, 0.8f, 1), 0f);
            CreateItem(folder, "hill", "Small Hill", BuildTab.Terrain, LayerBand.ElevatedTerrain,
                "#7F9B55", false, 0, 0, 2, 1, 1, 0, 2, -1, new Vector3(1.2f, 0.6f, 1.2f), 0f);
            CreateItem(folder, "barren_dry", "Barren Dry", BuildTab.Terrain, LayerBand.GroundWaterBase,
                "#A0895C", false, 0, 0, 0, 1, 0, 0, 1, 1, new Vector3(1, 1, 1), 0f, tileCategory: "dirt");
            CreateItem(folder, "muddy", "Muddy", BuildTab.Terrain, LayerBand.GroundWaterBase,
                "#6B5430", false, 0, 2, 0, 0, 0, 0, 0, 0, new Vector3(1, 1, 1), 0f, tileCategory: "mud");
            CreateItem(folder, "patchy_dirt", "Patchy Dirt", BuildTab.Terrain, LayerBand.GroundWaterBase,
                "#8B7A4A", false, 1, 0, 0, 1, 0, 0, 1, 1, new Vector3(1, 1, 1), 0f, tileCategory: "dirt");
            CreateItem(folder, "snow", "Snow", BuildTab.Terrain, LayerBand.GroundWaterBase,
                "#E8E8F0", false, 0, 1, 0, 0, 0, 1, 1, 1, new Vector3(1, 1, 1), 0f, tileCategory: "snow");
            CreateItem(folder, "fresh_water", "Fresh Water", BuildTab.Water, LayerBand.GroundWaterBase,
                "#4DA6FF", false, 0, 5, 0, 0, 0, 1, 1, 0, new Vector3(1, 0.2f, 1), 0f);
            CreateItem(folder, "shore", "Soft Shore", BuildTab.Water, LayerBand.GroundWaterBase,
                "#7DC7D4", false, 0, 3, 0, 1, 0, 1, 1, 1, new Vector3(1, 0.3f, 1), 0f);
            CreateItem(folder, "tree", "Tree", BuildTab.Plants, LayerBand.LivingEnvironment,
                "#226E34", true, 3, 0, 3, -1, 1, 1, 2, -1, new Vector3(1, 2, 1), 0.5f);
            CreateItem(folder, "bush", "Berry Bush", BuildTab.Plants, LayerBand.LivingEnvironment,
                "#3B8E3D", true, 4, 0, 2, 0, 1, 0, 1, -1, new Vector3(1, 0.8f, 1), 0.5f);
            CreateItem(folder, "flowers", "Flowers", BuildTab.Plants, LayerBand.LivingEnvironment,
                "#E879B9", true, 2, 0, 0, 1, 1, 0, 0, 0, new Vector3(0.8f, 0.5f, 0.8f), 0.5f);
            CreateItem(folder, "rocks", "Rocks", BuildTab.Resources, LayerBand.LivingEnvironment,
                "#6F7376", true, 0, 0, 2, 0, 2, 0, 3, -1, new Vector3(1, 0.7f, 1), 0.5f);
            CreateItem(folder, "wood", "Wood Pile", BuildTab.Resources, LayerBand.LivingEnvironment,
                "#9A6338", true, 0, 0, 4, 0, 1, 0, 2, -1, new Vector3(1.2f, 0.5f, 1.2f), 0.5f);
            CreateItem(folder, "sunlight", "Sunlight", BuildTab.Climate, LayerBand.SkyAtmosphere,
                "#FFD45A", true, 1, 0, 0, 5, 1, 2, 1, 0, new Vector3(1, 1, 1), 0f);
            CreateItem(folder, "gentle_rain", "Gentle Rain", BuildTab.Climate, LayerBand.SkyAtmosphere,
                "#8EC9FF", true, 2, 4, 0, -1, 0, 3, 1, 0, new Vector3(1, 1, 1), 0f);
            CreateItem(folder, "wind_break", "Wind Break", BuildTab.Interventions, LayerBand.LivingEnvironment,
                "#C39C60", true, 0, 0, 3, 0, 3, 3, 2, -1, new Vector3(1.5f, 1, 0.5f), 0.5f);
            CreateItem(folder, "simple_shelter", "Simple Shelter", BuildTab.Interventions, LayerBand.LivingEnvironment,
                "#A77B52", true, 0, 0, 6, 0, 3, 3, 2, -2, new Vector3(3, 2, 3), 0.5f, 2);

            PlacementCatalog catalog = ScriptableObject.CreateInstance<PlacementCatalog>();
            string[] guids = AssetDatabase.FindAssets("t:PlacementItem", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                PlacementItem item = AssetDatabase.LoadAssetAtPath<PlacementItem>(path);
                if (item != null) catalog.Items.Add(item);
            }
            catalog.InvalidateCache();
            AssetDatabase.CreateAsset(catalog, folder + "/BubbleWorldCatalog.asset");
            AssetDatabase.SaveAssets();

            Selection.activeObject = catalog;
            EditorUtility.FocusProjectWindow();
            EditorUtility.DisplayDialog("Done", "Created PlacementItems + BubbleWorldCatalog", "OK");
        }

        private static void CreateItem(
            string folder, string id, string displayName,
            BuildTab tab, LayerBand layer, string htmlColor, bool isObject,
            int food, int water, int shelter, int light, int safety, int weather, int stability, int space,
            Vector3 scale, float yOffset, int occupyRadius = 0, string tileCategory = null)
        {
            PlacementItem item = ScriptableObject.CreateInstance<PlacementItem>();
            item.Id = id;
            item.DisplayName = displayName;
            item.Tab = tab;
            item.Layer = layer;
            ColorUtility.TryParseHtmlString(htmlColor, out Color color);
            item.TintColor = color;
            item.IsObject = isObject;
            item.Food = food;
            item.Water = water;
            item.Shelter = shelter;
            item.Light = light;
            item.Safety = safety;
            item.Weather = weather;
            item.Stability = stability;
            item.Space = space;
            item.FallbackPrimitive = Mesh3DPrimitive.Cube;
            item.ModelScale3D = scale;
            item.YOffset3D = yOffset;
            item.OccupyRadius = occupyRadius;
            item.TileCategory = tileCategory;
            AssetDatabase.CreateAsset(item, folder + "/pl_" + id + ".asset");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
