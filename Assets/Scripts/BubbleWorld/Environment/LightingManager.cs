using System;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class LightingManager : MonoBehaviour
    {
        [Serializable]
        public sealed class ProfileEntry
        {
            public StarterEnvironment environment;
            public EnvironmentProfile profile;
        }

        [SerializeField] private ProfileEntry[] profiles;
        [SerializeField] private Light sunLight;

        private void OnEnable()
        {
            if (sunLight == null)
                sunLight = FindObjectOfType<Light>();
        }

        public void ApplyProfile(StarterEnvironment environment)
        {
            if (profiles == null) return;
            foreach (ProfileEntry entry in profiles)
            {
                if (entry.environment != environment) continue;
                Apply(entry.profile);
                return;
            }
        }

        public void Apply(EnvironmentProfile profile)
        {
            if (profile == null) return;

            RenderSettings.skybox = profile.skyboxMaterial;
            RenderSettings.ambientMode = profile.ambientMode;
            RenderSettings.ambientLight = profile.ambientColor;
            RenderSettings.fogColor = profile.fogColor;
            RenderSettings.fogMode = profile.fogMode;
            RenderSettings.fogDensity = profile.fogDensity;

            Camera cam = Camera.main;
            if (profile.skyboxMaterial != null && profile.ambientMode == UnityEngine.Rendering.AmbientMode.Skybox)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                RenderSettings.ambientIntensity = 1f;
            }
            else
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = profile.cameraClearColor;
            }

            if (sunLight != null)
            {
                sunLight.color = profile.directionalColor;
                sunLight.intensity = profile.directionalIntensity;
                sunLight.transform.rotation = Quaternion.Euler(profile.directionalRotation);
            }

            DynamicGI.UpdateEnvironment();
        }
    }
}
