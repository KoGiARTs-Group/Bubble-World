using UnityEditor;
using UnityEngine;

namespace BubbleWorld
{
    public static class AutoTileAssetValidator
    {
        [MenuItem("BubbleWorld/Validate Visual Profiles")]
        private static void ValidateAll()
        {
            int totalMissing = 0;
            int totalProfiles = 0;

            string[] profileGuids = AssetDatabase.FindAssets("t:EnvironmentVisualProfile");
            foreach (string guid in profileGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EnvironmentVisualProfile profile = AssetDatabase.LoadAssetAtPath<EnvironmentVisualProfile>(path);
                if (profile == null) continue;
                totalProfiles++;

                totalMissing += CountMissing(profile.terrainOverrides, profile.name + " (terrain overrides)");
                totalMissing += CountMissing(profile.vegetationOverrides, profile.name + " (vegetation)");
                totalMissing += CountMissingTerrainTypes(profile.terrainTypes, profile.name + " (terrain types)");
            }

            string[] terrainTypeGuids = AssetDatabase.FindAssets("t:TerrainType");
            foreach (string guid in terrainTypeGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TerrainType tt = AssetDatabase.LoadAssetAtPath<TerrainType>(path);
                if (tt == null) continue;

                if (tt.centerPrefab == null)
                {
                    Debug.LogWarning($"[Validator] TerrainType '{tt.name}' (id={tt.Id}) has no centerPrefab");
                    totalMissing++;
                }
            }

            if (totalMissing == 0)
                EditorUtility.DisplayDialog("Validation",
                    string.Format("All {0} profiles validated. No missing references found.", totalProfiles), "OK");
            else
                Debug.LogWarningFormat("Asset Validation: Found {0} missing references across {1} profiles and TerrainTypes.",
                    totalMissing, totalProfiles);
        }

        private static int CountMissing(VisualOverride[] overrides, string label)
        {
            if (overrides == null) return 0;
            int count = 0;
            for (int i = 0; i < overrides.Length; i++)
            {
                if (overrides[i].prefab == null)
                {
                    Debug.LogWarningFormat("[Validator] {0}: entry {1} (item={2}, variant={3}) has null prefab",
                        label, i, overrides[i].itemId, overrides[i].variant);
                    count++;
                }
            }
            return count;
        }

        private static int CountMissingTerrainTypes(TerrainType[] types, string label)
        {
            if (types == null) return 0;
            int count = 0;
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i] == null)
                {
                    Debug.LogWarningFormat("[Validator] {0}: TerrainType at index {1} is null", label, i);
                    count++;
                }
            }
            return count;
        }

        [MenuItem("BubbleWorld/Create World Layer Config")]
        private static void CreateWorldLayerConfig()
        {
            string folder = "Assets/Scripts/BubbleWorld/Config";
            string path = folder + "/WorldLayerConfig.asset";

            WorldLayerConfig existing = AssetDatabase.LoadAssetAtPath<WorldLayerConfig>(path);
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Already Exists",
                    "WorldLayerConfig already exists at:\n" + path, "OK");
                Selection.activeObject = existing;
                return;
            }

            WorldLayerConfig cfg = WorldLayerConfig.CreateDefault();
            AssetDatabase.CreateAsset(cfg, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = cfg;
            EditorUtility.FocusProjectWindow();
            EditorUtility.DisplayDialog("Created",
                "WorldLayerConfig created at:\n" + path, "OK");
        }
    }
}
