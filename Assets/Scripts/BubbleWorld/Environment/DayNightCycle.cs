using UnityEngine;

namespace BubbleWorld
{
    public sealed class DayNightCycle
    {
        private Light directionalLight;
        private float currentHour;
        private const float DawnHour = 6f;
        private const float DuskHour = 20f;

        public float CurrentHour => currentHour;
        public bool IsNight => currentHour < DawnHour || currentHour >= DuskHour;
        public bool IsDay => !IsNight;

        public void Initialize(Light sunLight)
        {
            directionalLight = sunLight;
        }

        public void Update(float hour)
        {
            currentHour = hour;

            if (directionalLight == null) return;

            float angle = Mathf.Lerp(0f, 180f, hour / 24f);
            directionalLight.transform.rotation = Quaternion.Euler(angle, 30f, 0f);

            float intensity;
            if (hour < DawnHour)
                intensity = 0.1f;
            else if (hour < DawnHour + 1f)
                intensity = Mathf.Lerp(0.1f, 1f, (hour - DawnHour));
            else if (hour < DuskHour - 1f)
                intensity = 1f;
            else if (hour < DuskHour)
                intensity = Mathf.Lerp(1f, 0.1f, (hour - (DuskHour - 1f)));
            else
                intensity = 0.1f;

            directionalLight.intensity = intensity;

            if (hour < DawnHour || hour >= DuskHour)
            {
                RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.15f);
            }
            else if (hour < DawnHour + 2f)
            {
                float t = (hour - DawnHour) / 2f;
                RenderSettings.ambientLight = Color.Lerp(
                    new Color(0.05f, 0.05f, 0.15f),
                    new Color(0.4f, 0.35f, 0.3f), t);
            }
            else if (hour > DuskHour - 2f)
            {
                float t = (DuskHour - hour) / 2f;
                RenderSettings.ambientLight = Color.Lerp(
                    new Color(0.05f, 0.05f, 0.15f),
                    new Color(0.4f, 0.35f, 0.3f), t);
            }
            else
            {
                RenderSettings.ambientLight = new Color(0.4f, 0.4f, 0.4f);
            }
        }

        public float GetSunContribution()
        {
            if (IsNight) return 0f;
            return Mathf.Clamp01((currentHour - DawnHour) / 2f)
                 * Mathf.Clamp01((DuskHour - currentHour) / 2f);
        }
    }
}
