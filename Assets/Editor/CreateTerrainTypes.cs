using UnityEditor;
using UnityEngine;

namespace BubbleWorld
{
    public static class CreateTerrainTypes
    {
        private const string Folder = "Assets/TerrainTypes";

        [MenuItem("BubbleWorld/Create All Terrain Types")]
        private static void CreateAll()
        {
            EnsureFolder("Assets", "TerrainTypes");

            string[] guids = AssetDatabase.FindAssets("t:PlacementItem", new[] { "Assets/PlacementItems" });
            int created = 0;
            int skipped = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                PlacementItem item = AssetDatabase.LoadAssetAtPath<PlacementItem>(path);
                if (item == null) continue;

                if (item.Tab != BuildTab.Terrain && item.Tab != BuildTab.Water)
                    continue;

                string assetPath = Folder + "/tt_" + item.Id + ".asset";
                TerrainType existing = AssetDatabase.LoadAssetAtPath<TerrainType>(assetPath);
                if (existing != null)
                {
                    existing.Id = item.Id;
                    existing.DisplayName = item.DisplayName;
                    EditorUtility.SetDirty(existing);
                    skipped++;
                    continue;
                }

                TerrainType tt = ScriptableObject.CreateInstance<TerrainType>();
                tt.Id = item.Id;
                tt.DisplayName = item.DisplayName;
                tt.name = item.DisplayName + " Terrain";

                AssetDatabase.CreateAsset(tt, assetPath);
                created++;
            }

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Terrain Types",
                $"Created: {created}, Skipped (already exist): {skipped}", "OK");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
