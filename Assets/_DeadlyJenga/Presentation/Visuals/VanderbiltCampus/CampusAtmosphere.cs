using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // Campus weather: hides the tropical sand and ocean, keeps a clear candy day sky and one sun,
    // and adds distance fog so the 700 m camera far clip is never visible.
    // Everything is put back in OnDisable. Power outage and vampire disasters keep control of the lighting.
    // Same approach as ChristmasTownAtmosphere.
    [DefaultExecutionOrder(950)]
    public sealed class CampusAtmosphere : MonoBehaviour
    {
        public Material daySky;            // e.g. Tropical Sky; empty keeps the current sky
        public float fogStart = 250, fogEnd = 620;
        public Color fogColor = new Color(.48f, .86f, 1f);  // used when the sky has no _Horizon color
        public Color sunColor = new Color(1f, .94f, .83f);
        public float sunIntensity = 1.65f;
        public Vector3 sunEuler = new Vector3(48, -32, 0);

        struct HiddenRenderer { public Renderer renderer; public bool enabled; }
        sealed class EnvironmentState
        {
            public CandyEnvironment environment; public bool apply;
            public Light sun; public Color color; public float intensity; public Quaternion rotation;
        }
        readonly List<HiddenRenderer> hidden = new List<HiddenRenderer>();
        readonly List<EnvironmentState> environments = new List<EnvironmentState>();
        Material previousSky;
        bool previousFog, captured;
        Color previousFogColor, previousSkyColor, previousEquator, previousGround;
        float previousStart, previousEnd, nextFind;
        FogMode previousFogMode; AmbientMode previousAmbientMode;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            previousSky = RenderSettings.skybox; previousFog = RenderSettings.fog; previousFogColor = RenderSettings.fogColor;
            previousStart = RenderSettings.fogStartDistance; previousEnd = RenderSettings.fogEndDistance; previousFogMode = RenderSettings.fogMode;
            previousAmbientMode = RenderSettings.ambientMode; previousSkyColor = RenderSettings.ambientSkyColor;
            previousEquator = RenderSettings.ambientEquatorColor; previousGround = RenderSettings.ambientGroundColor;
            captured = true; nextFind = 0;
        }

        void LateUpdate()
        {
            if (!captured) return;
            if (!LightingHazardActive())
            {
                ApplyDay();
                foreach (var item in environments) SetSun(item.sun);
            }
            if (Time.unscaledTime >= nextFind)
            {
                nextFind = Time.unscaledTime + .5f;
                if (CandyEnvironment.ForMap(this) is CandyEnvironment environment && environment)
                {
                    Hide(environment.sandRenderer); Hide(environment.waterRenderer);
                    if (!environments.Exists(s => s.environment == environment))
                    {
                        var sun = environment.sun;
                        environments.Add(new EnvironmentState
                        {
                            environment = environment, apply = environment.applyLighting, sun = sun,
                            color = sun ? sun.color : Color.white, intensity = sun ? sun.intensity : 0,
                            rotation = sun ? sun.transform.rotation : Quaternion.identity
                        });
                    }
                    environment.applyLighting = false; // we set the sky and sun now
                }
            }
            foreach (var item in hidden) if (item.renderer) item.renderer.enabled = false;
        }

        static bool LightingHazardActive()
        {
            foreach (var state in DisasterStates.Active)
            {
                var frame = state.Capture();
                if (frame != null && frame.warning <= 0 && (frame.kind == "PowerOutage" || frame.kind == "Vampire")) return true;
            }
            return false;
        }

        void ApplyDay()
        {
            if (daySky && RenderSettings.skybox != daySky) RenderSettings.skybox = daySky;
            var sky = RenderSettings.skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.fogColor = sky && sky.HasProperty("_Horizon") ? sky.GetColor("_Horizon") : fogColor;
            // Same daytime ambient as the tropical island.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.55f, .72f, .9f);
            RenderSettings.ambientEquatorColor = new Color(.43f, .57f, .64f);
            RenderSettings.ambientGroundColor = new Color(.3f, .3f, .36f);
        }

        void SetSun(Light sun)
        {
            if (!sun) return;
            sun.color = sunColor; sun.intensity = sunIntensity; sun.transform.rotation = Quaternion.Euler(sunEuler);
        }

        void Hide(Renderer renderer)
        {
            if (!renderer) return;
            foreach (var item in hidden) if (item.renderer == renderer) return;
            hidden.Add(new HiddenRenderer { renderer = renderer, enabled = renderer.enabled });
            renderer.enabled = false;
        }

        void OnDisable()
        {
            if (!captured) return;
            foreach (var item in hidden) if (item.renderer) item.renderer.enabled = item.enabled;
            hidden.Clear();
            foreach (var item in environments)
            {
                if (item.environment) item.environment.applyLighting = item.apply;
                if (item.sun) { item.sun.color = item.color; item.sun.intensity = item.intensity; item.sun.transform.rotation = item.rotation; }
            }
            environments.Clear();
            RenderSettings.skybox = previousSky; RenderSettings.fog = previousFog; RenderSettings.fogColor = previousFogColor;
            RenderSettings.fogStartDistance = previousStart; RenderSettings.fogEndDistance = previousEnd; RenderSettings.fogMode = previousFogMode;
            RenderSettings.ambientMode = previousAmbientMode; RenderSettings.ambientSkyColor = previousSkyColor;
            RenderSettings.ambientEquatorColor = previousEquator; RenderSettings.ambientGroundColor = previousGround;
            captured = false;
        }
    }
}
