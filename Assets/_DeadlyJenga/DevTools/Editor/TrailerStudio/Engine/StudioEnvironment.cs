using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Time of day and weather for a Set. The game has no control for these, so the Studio moves the
    // scene's own sun light and fog while the stage is open, and puts them back when it closes.
    // Skies that paint their own sun or clouds do not follow; that would need an art-side hook.
    public static class StudioEnvironment
    {
        public static readonly List<string> Weathers = new List<string> { "Map's own", "Clear", "Overcast", "Fog", "Storm" };

        static Light sun;
        static bool saved;
        static Quaternion sunRotation;
        static float sunIntensity, ambientIntensity, fogDensity;
        static Color sunColor, fogColor;
        static bool fog;
        static FogMode fogMode;

        static Light FindSun() => RenderSettings.sun ? RenderSettings.sun :
            Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional && l.enabled).OrderByDescending(l => l.intensity).FirstOrDefault();

        public static void Apply(StudioSet set)
        {
            bool wantsTime = set.timeOfDay >= 0, wantsWeather = !string.IsNullOrEmpty(set.weather) && set.weather != Weathers[0];
            if (!wantsTime && !wantsWeather) { Restore(); return; }
            if (!saved)
            {
                sun = FindSun();
                if (sun) { sunRotation = sun.transform.rotation; sunIntensity = sun.intensity; sunColor = sun.color; }
                ambientIntensity = RenderSettings.ambientIntensity;
                fog = RenderSettings.fog; fogDensity = RenderSettings.fogDensity; fogColor = RenderSettings.fogColor; fogMode = RenderSettings.fogMode;
                saved = true;
            }
            // Start from the map's own values each time, so turning one of the two off (weather back to
            // "Map's own" while the hour stays set, or the reverse) really takes it away.
            if (sun) sun.transform.rotation = sunRotation;
            RenderSettings.fog = fog; RenderSettings.fogDensity = fogDensity; RenderSettings.fogColor = fogColor; RenderSettings.fogMode = fogMode;
            float light = 1, haze = 0;
            Color tint = sunColor, mist = fog ? fogColor : new Color(.72f, .78f, .84f);
            if (sun && wantsTime)
            {
                // 6:00 the sun is on the horizon, 12:00 overhead, 18:00 on the other horizon.
                float hour = Mathf.Repeat(set.timeOfDay, 24);
                float height = Mathf.Sin((hour - 6) / 12 * Mathf.PI);
                float yaw = sunRotation.eulerAngles.y;
                // Morning light comes from one side, afternoon light from the other.
                sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(4, 90, Mathf.Clamp01(height)), yaw + (hour > 12 ? 180 : 0), 0);
                bool night = height <= 0;
                light = night ? .12f : Mathf.Lerp(.35f, 1, height);
                tint = night ? new Color(.45f, .55f, .9f) : Color.Lerp(new Color(1, .62f, .38f), Color.white, Mathf.Clamp01(height * 1.6f));
            }
            if (wantsWeather)
            {
                switch (set.weather)
                {
                    case "Clear": haze = 0; break;
                    case "Overcast": light *= .5f; haze = .006f; tint = Color.Lerp(tint, new Color(.8f, .84f, .9f), .7f); break;
                    case "Fog": light *= .6f; haze = .03f; break;
                    case "Storm": light *= .3f; haze = .014f; mist = new Color(.28f, .31f, .36f); tint = Color.Lerp(tint, new Color(.6f, .68f, .8f), .8f); break;
                }
                RenderSettings.fog = haze > 0;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = haze;
                RenderSettings.fogColor = mist;
            }
            if (sun) { sun.intensity = sunIntensity * light; sun.color = tint; }
            RenderSettings.ambientIntensity = ambientIntensity * Mathf.Lerp(.35f, 1, light);
        }

        public static void Restore()
        {
            if (!saved) return;
            if (sun) { sun.transform.rotation = sunRotation; sun.intensity = sunIntensity; sun.color = sunColor; }
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.fog = fog; RenderSettings.fogDensity = fogDensity; RenderSettings.fogColor = fogColor; RenderSettings.fogMode = fogMode;
            saved = false; sun = null;
        }
    }
}
