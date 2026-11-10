using UnityEditor;
using UnityEngine;

namespace BubbleWorld
{
    public static class CreateGameBalanceConfigs
    {
        private const string Folder = "Assets/Config";

        [MenuItem("BubbleWorld/Create All Game Balance Configs")]
        private static void CreateAll()
        {
            EnsureFolder("Assets", "Config");

            Create<GameTimeConfig>(Folder, "GameTimeConfig.asset");
            Create<WeatherConfig>(Folder, "WeatherConfig.asset");
            Create<SourceLogicConfig>(Folder, "SourceLogicConfig.asset");
            Create<AIConfig>(Folder, "AIConfig.asset");
            Create<SpawnConfig>(Folder, "SpawnConfig.asset");
            Create<SkillConfig>(Folder, "SkillConfig.asset");
            Create<EconomyConfig>(Folder, "EconomyConfig.asset");
            Create<MemoryConfig>(Folder, "MemoryConfig.asset");

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Game Balance Configs",
                "Created all 8 config assets in Assets/Config/", "OK");
        }

        private static void Create<T>(string folder, string fileName) where T : ScriptableObject
        {
            string path = folder + "/" + fileName;
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                Debug.Log("Skipped (already exists): " + path);
                return;
            }

            T instance = ScriptableObject.CreateInstance<T>();
            instance.name = fileName.Replace(".asset", "");
            AssetDatabase.CreateAsset(instance, path);
            Debug.Log("Created: " + path);
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
