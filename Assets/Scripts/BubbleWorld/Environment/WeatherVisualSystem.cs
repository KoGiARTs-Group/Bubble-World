using UnityEngine;

namespace BubbleWorld
{
    public sealed class WeatherVisualSystem
    {
        private ParticleSystem rainParticles;
        private ParticleSystem snowParticles;
        private ParticleSystem windParticles;
        private ParticleSystem stormParticles;
        private Light sunLight;
        private GameObject container;
        private WeatherType currentVisualWeather = WeatherType.Clear;
        private SeasonType currentVisualSeason = SeasonType.Spring;

        public void Initialize(Transform parent)
        {
            container = new GameObject("WeatherVisuals");
            container.transform.SetParent(parent);

            rainParticles = CreateParticleSystem("Rain", container.transform,
                new Color(0.5f, 0.6f, 0.8f, 0.6f), 2000, 0.5f, new Vector3(0, -2f, 0));
            snowParticles = CreateParticleSystem("Snow", container.transform,
                new Color(1f, 1f, 1f, 0.8f), 1500, 0.15f, new Vector3(0, -0.5f, 0));
            windParticles = CreateParticleSystem("Wind", container.transform,
                new Color(0.7f, 0.65f, 0.5f, 0.3f), 500, 1f, new Vector3(3f, 0, 0));
            stormParticles = CreateParticleSystem("Storm", container.transform,
                new Color(0.4f, 0.4f, 0.5f, 0.7f), 3000, 1.5f, new Vector3(0, -3f, 0));

            var sunObj = new GameObject("SunLight");
            sunObj.transform.SetParent(container.transform);
            sunLight = sunObj.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 1f;
            sunLight.color = new Color(1f, 0.95f, 0.85f);

            SetAllOff();
        }

        public void UpdateWeather(WeatherType weather, SeasonType season)
        {
            if (weather == currentVisualWeather && season == currentVisualSeason) return;

            SetAllOff();
            currentVisualWeather = weather;
            currentVisualSeason = season;

            ApplyWeatherVisuals(weather);
            ApplySeasonLighting(season, weather);
        }

        private void ApplyWeatherVisuals(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Rain:
                    if (rainParticles != null) rainParticles.Play();
                    break;
                case WeatherType.Snow:
                    if (snowParticles != null) snowParticles.Play();
                    break;
                case WeatherType.Windy:
                    if (windParticles != null) windParticles.Play();
                    break;
                case WeatherType.Storm1:
                    if (stormParticles != null) stormParticles.Play();
                    break;
                case WeatherType.Storm2:
                    if (stormParticles != null) stormParticles.Play();
                    if (rainParticles != null) rainParticles.Play();
                    break;
                case WeatherType.Storm3:
                    if (stormParticles != null) stormParticles.Play();
                    if (rainParticles != null) rainParticles.Play();
                    break;
                case WeatherType.Sunny:
                    if (sunLight != null) sunLight.intensity = 1.3f;
                    break;
                case WeatherType.Cloudy:
                    if (sunLight != null) sunLight.intensity = 0.7f;
                    break;
            }
        }

        private void ApplySeasonLighting(SeasonType season, WeatherType weather)
        {
            if (sunLight == null) return;

            float baseIntensity = sunLight.intensity;
            switch (season)
            {
                case SeasonType.Spring:
                    sunLight.color = new Color(0.9f, 0.95f, 0.9f);
                    break;
                case SeasonType.Summer:
                    sunLight.color = new Color(1f, 0.95f, 0.8f);
                    if (weather == WeatherType.Clear || weather == WeatherType.Sunny)
                        baseIntensity = 1.4f;
                    break;
                case SeasonType.Autumn:
                    sunLight.color = new Color(1f, 0.85f, 0.7f);
                    break;
                case SeasonType.Winter:
                    sunLight.color = new Color(0.8f, 0.85f, 1f);
                    if (weather == WeatherType.Clear || weather == WeatherType.Sunny)
                        baseIntensity = 0.8f;
                    break;
            }
            sunLight.intensity = baseIntensity;
        }

        private void SetAllOff()
        {
            if (rainParticles != null) rainParticles.Stop();
            if (snowParticles != null) snowParticles.Stop();
            if (windParticles != null) windParticles.Stop();
            if (stormParticles != null) stormParticles.Stop();
        }

        private ParticleSystem CreateParticleSystem(string name, Transform parent,
            Color color, int maxParticles, float startSpeed, Vector3 velocity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = maxParticles;
            main.startSpeed = startSpeed;
            main.startColor = color;
            main.startSize = 0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 2f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 10f, 30f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = velocity.x;
            vel.y = velocity.y;
            vel.z = velocity.z;

            var emission = ps.emission;
            emission.rateOverTime = maxParticles / 4;

            ps.Stop();
            return ps;
        }

        public void Destroy()
        {
            if (container != null)
                Object.Destroy(container);
        }
    }
}
