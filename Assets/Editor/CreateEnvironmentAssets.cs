using UnityEditor;
using UnityEngine;

namespace BubbleWorld
{
    public static class CreateEnvironmentAssets
    {
        [MenuItem("BubbleWorld/Create Environment & Audio Assets")]
        private static void CreateAll()
        {
            EnsureFolder("Assets", "EnvironmentProfiles");
            EnsureFolder("Assets", "AudioCues");
            EnsureFolder("Assets", "SoundBanks");

            CreateMeadowProfile("Assets/EnvironmentProfiles");
            CreateForestProfile("Assets/EnvironmentProfiles");
            CreateBeachProfile("Assets/EnvironmentProfiles");

            AudioCue bgmMeadow = CreateCue("Assets/AudioCues", "bgm_meadow", null, 0.7f, 1f, true);
            AudioCue bgmForest = CreateCue("Assets/AudioCues", "bgm_forest", null, 0.7f, 1f, true);
            AudioCue bgmBeach = CreateCue("Assets/AudioCues", "bgm_beach", null, 0.7f, 1f, true);

            AudioCue hover = CreateCue("Assets/AudioCues", "sfx_cursor_hover", null, 0.5f, 1.2f, false);
            AudioCue select = CreateCue("Assets/AudioCues", "sfx_cursor_select", null, 0.6f, 1f, false);
            AudioCue place = CreateCue("Assets/AudioCues", "sfx_brush_place", null, 0.6f, 0.9f, false);
            AudioCue remove = CreateCue("Assets/AudioCues", "sfx_brush_remove", null, 0.5f, 0.8f, false);
            AudioCue back = CreateCue("Assets/AudioCues", "sfx_ui_back", null, 0.5f, 1f, false);
            AudioCue alert = CreateCue("Assets/AudioCues", "sfx_livability_alert", null, 0.7f, 1f, false);

            SoundBank bank = ScriptableObject.CreateInstance<SoundBank>();

            bank.bgmPerWorld = new SoundBank.BgmEntry[]
            {
                new SoundBank.BgmEntry { environment = StarterEnvironment.MeadowGrassland, cue = bgmMeadow },
                new SoundBank.BgmEntry { environment = StarterEnvironment.Forest, cue = bgmForest },
                new SoundBank.BgmEntry { environment = StarterEnvironment.BeachCoastal, cue = bgmBeach },
            };

            bank.cursorHover = hover;
            bank.cursorSelect = select;
            bank.cursorSwitch = select;
            bank.brushPlace = place;
            bank.brushRemove = remove;
            bank.uiBack = back;
            bank.livabilityAlert = alert;

            AssetDatabase.CreateAsset(bank, "Assets/SoundBanks/DefaultSoundBank.asset");
            AssetDatabase.SaveAssets();

            Selection.activeObject = bank;
            EditorUtility.FocusProjectWindow();
            EditorUtility.DisplayDialog("Done",
                "Created 3 EnvironmentProfiles, 9 AudioCues, 1 SoundBank", "OK");
        }

        private static void CreateMeadowProfile(string folder)
        {
            EnvironmentProfile p = ScriptableObject.CreateInstance<EnvironmentProfile>();
            p.name = "Meadow Grassland";
            p.ambientColor = new Color(0.25f, 0.28f, 0.22f);
            p.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            p.directionalColor = new Color(1f, 0.95f, 0.85f);
            p.directionalIntensity = 1.1f;
            p.directionalRotation = new Vector3(50f, -30f, 0f);
            p.fogColor = new Color(0.7f, 0.75f, 0.8f);
            p.fogMode = UnityEngine.FogMode.Exponential;
            p.fogDensity = 0.008f;
            p.cameraClearColor = new Color(0.5f, 0.7f, 0.9f);
            AssetDatabase.CreateAsset(p, folder + "/env_meadow.asset");
        }

        private static void CreateForestProfile(string folder)
        {
            EnvironmentProfile p = ScriptableObject.CreateInstance<EnvironmentProfile>();
            p.name = "Forest";
            p.ambientColor = new Color(0.15f, 0.2f, 0.12f);
            p.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            p.directionalColor = new Color(0.9f, 0.85f, 0.6f);
            p.directionalIntensity = 0.8f;
            p.directionalRotation = new Vector3(40f, -45f, 0f);
            p.fogColor = new Color(0.3f, 0.4f, 0.3f);
            p.fogMode = UnityEngine.FogMode.Exponential;
            p.fogDensity = 0.015f;
            p.cameraClearColor = new Color(0.2f, 0.3f, 0.2f);
            AssetDatabase.CreateAsset(p, folder + "/env_forest.asset");
        }

        private static void CreateBeachProfile(string folder)
        {
            EnvironmentProfile p = ScriptableObject.CreateInstance<EnvironmentProfile>();
            p.name = "Beach Coastal";
            p.ambientColor = new Color(0.3f, 0.28f, 0.22f);
            p.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            p.directionalColor = new Color(1f, 0.97f, 0.9f);
            p.directionalIntensity = 1.2f;
            p.directionalRotation = new Vector3(60f, -20f, 0f);
            p.fogColor = new Color(0.8f, 0.8f, 0.85f);
            p.fogMode = UnityEngine.FogMode.Exponential;
            p.fogDensity = 0.006f;
            p.cameraClearColor = new Color(0.6f, 0.8f, 0.95f);
            AssetDatabase.CreateAsset(p, folder + "/env_beach.asset");
        }

        private static AudioCue CreateCue(string folder, string fileName, AudioClip clip, float volume, float pitch, bool loop)
        {
            AudioCue cue = ScriptableObject.CreateInstance<AudioCue>();
            cue.clip = clip;
            cue.volume = volume;
            cue.pitch = pitch;
            cue.loop = loop;
            AssetDatabase.CreateAsset(cue, folder + "/" + fileName + ".asset");
            return cue;
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
