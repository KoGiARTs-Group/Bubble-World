using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BubbleWorld
{
    public static class CreateEnvironmentProfiles
    {
        private const string ProfileFolder = "Assets/EnvironmentProfiles";
        private const string TerrainTypeFolder = "Assets/TerrainTypes";

        [MenuItem("BubbleWorld/Create or Update Environment Visual Profiles")]
        private static void CreateOrUpdateAll()
        {
            EnsureFolder("Assets", "EnvironmentProfiles");
            EnsureFolder("Assets", "TerrainTypes");

            TerrainType[] allTerrainTypes = FindAllTerrainTypes();
            StarterEnvironment[] environments = (StarterEnvironment[])System.Enum.GetValues(typeof(StarterEnvironment));
            List<string> created = new List<string>();
            List<string> updated = new List<string>();

            foreach (StarterEnvironment env in environments)
            {
                string fileName = "env_visual_" + env.ToString().ToLower() + ".asset";
                string assetPath = ProfileFolder + "/" + fileName;
                EnvironmentVisualProfile profile = AssetDatabase.LoadAssetAtPath<EnvironmentVisualProfile>(assetPath);

                bool isNew = profile == null;
                if (isNew)
                {
                    profile = ScriptableObject.CreateInstance<EnvironmentVisualProfile>();
                    profile.environment = env;
                    profile.name = env.ToString() + " Visual Profile";
                }

                profile.terrainTypes = allTerrainTypes;

                if (profile.vegetationOverrides == null || profile.vegetationOverrides.Length == 0)
                {
                    List<VisualOverride> vegOverrides = new List<VisualOverride>();
                    foreach (PlacementItem item in FindAllPlacementItems())
                    {
                        if (item == null) continue;
                        if (item.Tab == BuildTab.Terrain || item.Tab == BuildTab.Water) continue;

                        vegOverrides.Add(new VisualOverride
                        {
                            itemId = item.Id,
                            variant = TileVariant.Center,
                            prefab = item.Prefab3D,
                            material = null
                        });
                    }
                    profile.vegetationOverrides = vegOverrides.ToArray();
                }

                if (profile.defaultTerrainMaterial == null)
                    profile.defaultTerrainMaterial = null;

                if (isNew)
                {
                    AssetDatabase.CreateAsset(profile, assetPath);
                    created.Add(env.ToString());
                }
                else
                {
                    EditorUtility.SetDirty(profile);
                    updated.Add(env.ToString());
                }
            }

            AssetDatabase.SaveAssets();

            string msg = "";
            if (created.Count > 0)
                msg += "Created: " + string.Join(", ", created) + "\n";
            if (updated.Count > 0)
                msg += "Updated: " + string.Join(", ", updated) + "\n";
            if (created.Count == 0 && updated.Count == 0)
                msg = "No changes.";

            EditorUtility.DisplayDialog("Environment Visual Profiles", msg, "OK");
        }

        [MenuItem("BubbleWorld/Recreate All Environment Visual Profiles (reset to defaults)")]
        private static void RecreateAll()
        {
            if (!EditorUtility.DisplayDialog("Recreate All Profiles?",
                "This will DELETE all existing EnvironmentVisualProfile assets and recreate them from scratch.\n\nManual overrides will be lost. Are you sure?",
                "Recreate", "Cancel"))
                return;

            EnsureFolder("Assets", "EnvironmentProfiles");
            EnsureFolder("Assets", "TerrainTypes");

            string[] guids = AssetDatabase.FindAssets("t:EnvironmentVisualProfile", new[] { ProfileFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.SaveAssets();

            TerrainType[] allTerrainTypes = FindAllTerrainTypes();
            StarterEnvironment[] environments = (StarterEnvironment[])System.Enum.GetValues(typeof(StarterEnvironment));

            foreach (StarterEnvironment env in environments)
            {
                string fileName = "env_visual_" + env.ToString().ToLower() + ".asset";
                string assetPath = ProfileFolder + "/" + fileName;

                EnvironmentVisualProfile profile = ScriptableObject.CreateInstance<EnvironmentVisualProfile>();
                profile.environment = env;
                profile.name = env.ToString() + " Visual Profile";
                profile.terrainTypes = allTerrainTypes;
                profile.terrainOverrides = null;
                profile.vegetationOverrides = null;
                profile.defaultTerrainMaterial = null;

                AssetDatabase.CreateAsset(profile, assetPath);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Done", $"Recreated {environments.Length} profiles.", "OK");
        }

        private static TerrainType[] FindAllTerrainTypes()
        {
            string[] guids = AssetDatabase.FindAssets("t:TerrainType", new[] { TerrainTypeFolder });
            List<TerrainType> list = new List<TerrainType>();
            foreach (string guid in guids)
            {
                TerrainType tt = AssetDatabase.LoadAssetAtPath<TerrainType>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (tt != null) list.Add(tt);
            }
            return list.ToArray();
        }

        private static List<PlacementItem> FindAllPlacementItems()
        {
            string[] guids = AssetDatabase.FindAssets("t:PlacementItem");
            List<PlacementItem> items = new List<PlacementItem>();
            foreach (string guid in guids)
            {
                PlacementItem item = AssetDatabase.LoadAssetAtPath<PlacementItem>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (item != null) items.Add(item);
            }
            return items;
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
