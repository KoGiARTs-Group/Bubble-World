using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BubbleWorld
{
    public static class AssignTerrainTypePrefabs
    {
        private const string PrefabRoot = "Assets/Prefabs/Terrain";

        private static readonly Dictionary<string, string> IdToFolder = new Dictionary<string, string>
        {
            { "barren_dry", "Dirt" },
            { "dirt", "Dirt" },
            { "grass", "Grass" },
            { "muddy", "Muddy" },
            { "patchy_dirt", "Patchy Dirt" },
            { "sand", "Sand" },
            { "snow", "Snow" },
        };

        [MenuItem("BubbleWorld/Assign All TerrainType Prefabs")]
        private static void AssignAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:TerrainType");
            int assigned = 0;
            int skipped = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TerrainType tt = AssetDatabase.LoadAssetAtPath<TerrainType>(path);
                if (tt == null) continue;

                if (!IdToFolder.TryGetValue(tt.Id, out string folderName))
                {
                    Debug.Log($"Skipped {tt.name} (id={tt.Id}): no matching prefab folder");
                    skipped++;
                    continue;
                }

                string folder = $"{PrefabRoot}/{folderName}";

                tt.centerPrefab = LoadPrefab(folder, "Center");
                tt.edgePrefab = LoadPrefab(folder, "EdgeNorth");
                tt.outerCornerPrefab = LoadPrefab(folder, "OuterCornerNE");
                tt.innerCornerPrefab = LoadPrefab(folder, "InnerCornerNE");
                tt.isolatedPrefab = LoadPrefab(folder, "Isolated");

                var overrides = new List<TerrainVariantEntry>();

                AddOverride(overrides, TileVariant.EdgeSouth, LoadPrefab(folder, "EdgeSouth"));
                AddOverride(overrides, TileVariant.InnerCornerSE, LoadPrefab(folder, "InnerCornerSE_Half"));
                AddOverride(overrides, TileVariant.InnerCornerSW, LoadPrefab(folder, "InnerCornerSW_Half"));
                AddOverride(overrides, TileVariant.InnerCornerNW, LoadPrefab(folder, "InnerCornerNW"));
                AddOverride(overrides, TileVariant.WaterStraight, LoadPrefab(folder, "Water Straight"));
                AddOverride(overrides, TileVariant.WaterCorner, LoadPrefab(folder, "Water Corner"));
                AddOverride(overrides, TileVariant.WaterTee, LoadPrefab(folder, "Water T-Junction"));
                AddOverride(overrides, TileVariant.WaterCross, LoadPrefab(folder, "Water Cross"));

                tt.explicitOverrides = overrides.ToArray();

                EditorUtility.SetDirty(tt);
                assigned++;
                Debug.Log($"Assigned prefabs to {tt.name} from {folder}");
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("TerrainType Prefabs",
                $"Assigned: {assigned}, Skipped (no folder): {skipped}", "OK");
        }

        private static GameObject LoadPrefab(string folder, string name)
        {
            string path = $"{folder}/{name}.prefab";
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void AddOverride(List<TerrainVariantEntry> list, TileVariant variant, GameObject prefab)
        {
            if (prefab == null) return;
            list.Add(new TerrainVariantEntry
            {
                variant = variant,
                prefab = prefab,
                material = null
            });
        }
    }
}
