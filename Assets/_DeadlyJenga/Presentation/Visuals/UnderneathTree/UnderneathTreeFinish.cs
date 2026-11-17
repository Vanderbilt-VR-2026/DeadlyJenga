using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeadlyJenga.CandyArt
{
    [DefaultExecutionOrder(990)]
    public sealed partial class UnderneathTreeFinish : MonoBehaviour
    {
        const string MapId = "workshop-under-the-tree";
        const string ResourcePath = "UnderneathTreeFinish/";
        readonly List<GameObject> mapDressings = new List<GameObject>();
        readonly Dictionary<MeshRenderer, ShadowCastingMode> shadowCasting = new Dictionary<MeshRenderer, ShadowCastingMode>();
        readonly List<Material> materials = new List<Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly Dictionary<MeshFilter, Mesh> originalMeshes = new Dictionary<MeshFilter, Mesh>();
        readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
        readonly Dictionary<Light, bool> disabledSun = new Dictionary<Light, bool>();
        readonly Dictionary<CandyEnvironment, bool> environments = new Dictionary<CandyEnvironment, bool>();
        readonly Dictionary<UniversalAdditionalCameraData, bool> cameras = new Dictionary<UniversalAdditionalCameraData, bool>();
        readonly Dictionary<string, Material> finishes = new Dictionary<string, Material>();
        readonly List<Light> practicalLights = new List<Light>();
        ISyncedWorld world;
        Transform art;
        Material template;
        Material previousSky;
        AmbientMode previousMode;
        SphericalHarmonicsL2 previousProbe;
        Color previousAmbient, previousSkyColor, previousEquator, previousGround;
        bool previousFog, captured;
        float previousReflection;
        float refreshAt;
        int revision = -1;
        Light key, outageMoon;
        ReflectionProbe roomProbe;
        Material floorVarnish;
        bool roomReflected;
        Mesh roundedWrap;
        Volume volume;
        VolumeProfile grade;
        bool neutralView;
        readonly Dictionary<Material, Color> outageEmission = new Dictionary<Material, Color>();
        RenderPipelineAsset previousPipeline;
        UniversalRenderPipelineAsset roomPipeline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
            GameEvents.SessionStarted -= OnSessionStarted;
            GameEvents.SessionStarted += OnSessionStarted;
        }

        static void OnSessionStarted(GameObject session)
        {
            if (MatchChoices.MapId == MapId && session && !session.GetComponent<UnderneathTreeFinish>())
                session.AddComponent<UnderneathTreeFinish>();
        }

        void Start()
        {
            template = Resources.Load<Material>(ResourcePath + "Lit");
            if (!template)
            {
                enabled = false;
                return;
            }

            world = Services.World as ISyncedWorld;
            if (world == null || !world.Root)
            {
                enabled = false;
                return;
            }

            previousSky = RenderSettings.skybox;
            previousMode = RenderSettings.ambientMode;
            previousAmbient = RenderSettings.ambientLight;
            previousSkyColor = RenderSettings.ambientSkyColor;
            previousEquator = RenderSettings.ambientEquatorColor;
            previousGround = RenderSettings.ambientGroundColor;
            previousProbe = RenderSettings.ambientProbe;
            previousReflection = RenderSettings.reflectionIntensity;
            previousFog = RenderSettings.fog;
            captured = true;

            var host = new GameObject("Christmas Eve Art and Lighting");
            art = host.transform;
            art.SetParent(world.Root, false);
            BuildRoom();
            BuildFir();
            BuildRoomDetails();
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
            {
                previousPipeline = QualitySettings.renderPipeline;
                var roomProfile = Resources.Load<UniversalRenderPipelineAsset>(ResourcePath + "Room Rendering");
                roomPipeline = Instantiate(roomProfile ? roomProfile : pipeline);
                roomPipeline.name = "Christmas room rendering (runtime only)";
                roomPipeline.hideFlags = HideFlags.HideAndDontSave;
                roomPipeline.shadowDistance = 340f;
                roomPipeline.shadowCascadeCount = Application.platform == RuntimePlatform.Android ? 2 : 4;
                roomPipeline.mainLightShadowmapResolution = Application.platform == RuntimePlatform.Android ? 2048 : 4096;
                roomPipeline.supportsHDR = true;
                // Enough lamps per object for the floor to catch the ones nearest it.
                roomPipeline.maxAdditionalLightsCount = 8;
                QualitySettings.renderPipeline = roomPipeline;
            }
            BuildLighting();
            RestoreIndoorSettings();
            RefreshWorldArt();
        }

        void LateUpdate()
        {
            if (!captured)
                return;
            if (MatchChoices.MapId != MapId || !ReferenceEquals(Services.World, world) || !world.Root)
            {
                enabled = false;
                return;
            }
            if (world.Revision != revision)
                RefreshWorldArt();

            bool viewUnderFloor = Services.Session?.ActiveCamera is Camera view && view.transform.position.y < -1.7f;
            bool blackout = false;
            foreach (var state in DisasterStates.Active)
            {
                var frame = state.Capture();
                if (frame != null && frame.warning <= 0 && (frame.kind == "PowerOutage" || frame.kind == "Vampire"))
                {
                    blackout = true;
                    break;
                }
            }
            foreach (var material in materials)
            {
                if (!material || !material.HasProperty("_EmissionColor")) continue;
                if (blackout && !outageEmission.ContainsKey(material))
                {outageEmission[material] = material.GetColor("_EmissionColor");material.SetColor("_EmissionColor", Color.black);}
            }
            if (floorVarnish && floorVarnish.HasProperty("_GlowPower"))
                floorVarnish.SetFloat("_GlowPower", blackout ? 0f : 1f);
            if (!blackout && outageEmission.Count > 0)
            {
                foreach (var item in outageEmission) if (item.Key) item.Key.SetColor("_EmissionColor", item.Value);
                outageEmission.Clear();
            }
            if (blackout)
            {
                if (!outageMoon)
                {
                    var moon = new GameObject("Outage moon through window");moon.transform.SetParent(art, false);
                    moon.transform.rotation = Quaternion.Euler(38f, -65f, 0f);
                    outageMoon = moon.AddComponent<Light>();outageMoon.type = LightType.Directional;
                    outageMoon.color = new Color(.28f, .46f, 1f);outageMoon.intensity = .38f;
                    outageMoon.shadows = LightShadows.None;
                }
                RenderSettings.fog = false;RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Flat;
                var moonFill = new Color(.025f, .035f, .065f);RenderSettings.ambientLight = moonFill;
                var moonProbe = new SphericalHarmonicsL2();moonProbe.AddAmbientLight(moonFill);RenderSettings.ambientProbe = moonProbe;
                RenderSettings.reflectionIntensity = .12f;
            }
            if (outageMoon) outageMoon.enabled = blackout;
            bool externalLighting = viewUnderFloor || blackout;
            if (!externalLighting)
                RestoreIndoorSettings();
            if (neutralView != externalLighting)
            {
                neutralView = externalLighting;
                if (key)
                    key.enabled = !externalLighting;
                foreach (var light in practicalLights)
                    light.enabled = !externalLighting;
                if (volume)
                    volume.enabled = !externalLighting;
                if (roomProbe)
                    roomProbe.enabled = !externalLighting;
            }

            if (Time.unscaledTime < refreshAt)
                return;
            refreshAt = Time.unscaledTime + .7f;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional || light.transform.IsChildOf(art))
                    continue;
                if (!disabledSun.ContainsKey(light))
                    disabledSun.Add(light, light.enabled);
                light.enabled = false;
            }
            var environment = CandyEnvironment.ForMap(this);
            if (environment)
            {
                if (!environments.ContainsKey(environment))
                    environments.Add(environment, environment.applyLighting);
                environment.applyLighting = false;
                Hide(environment.sandRenderer);
                Hide(environment.waterRenderer);
            }
            if (roomProbe && !externalLighting && !roomReflected)
            {
                roomReflected = true;
                roomProbe.RenderProbe();
            }
            if (Services.Session?.ActiveCamera is Camera active &&
                active.TryGetComponent<UniversalAdditionalCameraData>(out var data))
            {
                if (!cameras.ContainsKey(data))
                    cameras.Add(data, data.renderPostProcessing);
                data.renderPostProcessing = true;
            }
        }

        void RestoreIndoorSettings()
        {
            // The middle of the night: only a little blue from the windows and red off the rug,
            // so the tree lights, the lamp and the moonlight do the lighting.
            var sky = new Color(.045f, .05f, .11f);
            var sides = new Color(.03f, .05f, .16f);
            var rug = new Color(.07f, .015f, .02f);
            if (!RenderSettings.skybox && !RenderSettings.fog && RenderSettings.ambientMode == AmbientMode.Trilight &&
                RenderSettings.ambientSkyColor == sky && Mathf.Approximately(RenderSettings.reflectionIntensity, .4f))
                return;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = sides;
            RenderSettings.ambientGroundColor = rug;
            RenderSettings.reflectionIntensity = .4f;
        }

        void Hide(Renderer renderer)
        {
            if (!renderer)
                return;
            if (!hidden.ContainsKey(renderer))
                hidden.Add(renderer, renderer.enabled);
            renderer.enabled = false;
        }

        void RefreshWorldArt()
        {
            revision = world.Revision;
            foreach (var renderer in world.Root.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.transform.IsChildOf(art))
                    continue;
                var name = renderer.name;
                if (name.StartsWith("Tree Tier", StringComparison.Ordinal) || name == "Tree Trunk" ||
                    name.StartsWith("Tree Light", StringComparison.Ordinal) ||
                    name.StartsWith("Tree Ornament", StringComparison.Ordinal) ||
                    name.StartsWith("Tree Wire", StringComparison.Ordinal) || name == "Fireplace Fire" ||
                    name.StartsWith("Wood Planks", StringComparison.Ordinal) || name == "Tree Skirt rim" ||
                    name.StartsWith("Sofa", StringComparison.Ordinal) || name == "Lamp Shade" || name == "Side Table" ||
                    name.StartsWith("Room Wall", StringComparison.Ordinal))
                {
                    Hide(renderer);
                    continue;
                }
                if (name.StartsWith("Room Wall", StringComparison.Ordinal) || name == "Room Ceiling")
                {
                    if (!shadowCasting.ContainsKey(renderer))
                        shadowCasting.Add(renderer, renderer.shadowCastingMode);
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                ApplyFinish(renderer);
                SoftenGiftEdges(renderer);
                MapRugTexture(renderer);
                AddGiftBow(renderer);
            }
        }

        void OnDisable()
        {
            if (!captured)
                return;
            captured = false;
            if (roomPipeline)
            {
                if (QualitySettings.renderPipeline == roomPipeline) QualitySettings.renderPipeline = previousPipeline;
                Destroy(roomPipeline);
            }
            foreach (var item in shadowCasting)
                if (item.Key)
                    item.Key.shadowCastingMode = item.Value;
            foreach (var dressing in mapDressings)
                if (dressing)
                    Destroy(dressing);
            foreach (var item in originalMeshes)
                if (item.Key)
                    item.Key.sharedMesh = item.Value;
            foreach (var item in originalMaterials)
                if (item.Key)
                    item.Key.sharedMaterials = item.Value;
            foreach (var item in hidden)
                if (item.Key)
                    item.Key.enabled = item.Value;
            foreach (var item in disabledSun)
                if (item.Key)
                    item.Key.enabled = item.Value;
            foreach (var item in environments)
                if (item.Key)
                    item.Key.applyLighting = item.Value;
            foreach (var item in cameras)
                if (item.Key)
                    item.Key.renderPostProcessing = item.Value;
            RenderSettings.skybox = previousSky;
            RenderSettings.ambientMode = previousMode;
            RenderSettings.ambientLight = previousAmbient;
            RenderSettings.ambientSkyColor = previousSkyColor;
            RenderSettings.ambientEquatorColor = previousEquator;
            RenderSettings.ambientGroundColor = previousGround;
            RenderSettings.ambientProbe = previousProbe;
            RenderSettings.reflectionIntensity = previousReflection;
            RenderSettings.fog = previousFog;
            if (art)
                Destroy(art.gameObject);
            if (grade)
                Destroy(grade);
            foreach (var material in materials)
                Destroy(material);
            foreach (var mesh in meshes)
                Destroy(mesh);
            if (firPalette)
                Destroy(firPalette);
        }

        void BuildLighting()
        {
            key = MakeLight("Amber lamplight and ceiling bounce", LightType.Directional,
                Vector3.zero, new Color(1f, .70f, .42f), .08f, 0f);
            // Shines from the fireplace side so the gift faces seen in the approved picture are lit.
            key.transform.localRotation = Quaternion.Euler(46f, 18f, 0f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = .78f;
            key.shadowBias = .08f;
            key.shadowNormalBias = .2f;

            var bounce = MakeLight("Soft blue room bounce", LightType.Directional, Vector3.zero,
                new Color(.18f, .38f, 1f), .12f, 0f);
            // Blue light from the side window gives the gifts a cool edge.
            bounce.transform.localRotation = Quaternion.Euler(30f, 262f, 0f);
            practicalLights.Add(bounce);

            // Moonlight falls in through each window onto the floor just inside it.
            MakeSpot("Moonlight through the front window", new Vector3(2.5f, 55f, 74f),
                new Vector3(2.5f, 0f, 10f), new Color(.16f, .36f, 1f), 9000f, 150f, 95f);
            MakeSpot("Moonlight through the side window", new Vector3(82f, 95f, -130f),
                new Vector3(20f, 0f, -130f), new Color(.12f, .30f, 1f), 12000f, 170f, 95f);
            MakeLight("Glow in the train tunnel", LightType.Point,
                new Vector3(-150, 16, -128), new Color(1f, .68f, .28f), 700f, 38f);
            // The lamp lights what is near it: the wall, the table and the end of the sofa.
            MakeLight("Floor lamp", LightType.Point,
                new Vector3(-88, 59, 54), new Color(1f, .70f, .39f), 9000f, 120f);

            // The tree is the room's main light. A warm heart, then a ring of coloured lights
            // low and just outside the branches, so they tint the gifts and rug rather than the tree.
            MakeLight("Golden light in the tree", LightType.Point,
                new Vector3(-37, 60, -50), new Color(1f, .66f, .36f), 600f, 150f);
            MakeSpot("Warm tree canopy bounce", new Vector3(-80f, 67f, -109f),
                new Vector3(-37f, 80f, -35.7f), new Color(1f, .86f, .64f), 1200f, 150f, 75f);
            // The same four colours as the bulbs: red, yellow, green, blue.
            var bulbGlow = new[] { new Color(1f, .08f, .05f), new Color(1f, .70f, .12f),
                new Color(.08f, 1f, .20f), new Color(.08f, .32f, 1f) };
            for (int i = 0; i < 12; i++)
            {
                float angle = (i * 30f + 10f) * Mathf.Deg2Rad;
                float reach = i % 2 == 0 ? 52f : 78f;
                var place = new Vector3(-37f + Mathf.Cos(angle) * reach, 10f + i * 37 % 24, -35.7f + Mathf.Sin(angle) * reach);
                MakeLight("Tree bulb glow " + i, LightType.Point, place, bulbGlow[i % bulbGlow.Length], 9000f, 170f);
            }

            var reflections = new GameObject("Christmas Room Reflections");
            reflections.transform.SetParent(art, false);
            reflections.transform.localPosition = new Vector3(-90f, 55f, -105f);
            roomProbe = reflections.AddComponent<ReflectionProbe>();
            roomProbe.mode = ReflectionProbeMode.Realtime;
            roomProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            roomProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            roomProbe.resolution = 256;
            roomProbe.hdr = true;
            roomProbe.size = new Vector3(340f, 180f, 340f);
            roomProbe.boxProjection = true;
            roomProbe.intensity = .85f;

            var post = new GameObject("Christmas Eve Grade");
            post.transform.SetParent(art, false);
            volume = post.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 18f;
            grade = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = grade;
            var tone = grade.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = grade.Add<Bloom>(true);
            bloom.threshold.Override(.9f);
            bloom.intensity.Override(1.35f);
            bloom.scatter.Override(.72f);
            var color = grade.Add<ColorAdjustments>(true);
            color.postExposure.Override(.1f);
            color.contrast.Override(30f);
            color.saturation.Override(38f);
            // Storybook grade: blue in the shadows, gold in the highlights.
            var split = grade.Add<SplitToning>(true);
            split.shadows.Override(new Color(.38f, .48f, 1f));
            split.highlights.Override(new Color(1f, .80f, .50f));
            split.balance.Override(-10f);
            var edges = grade.Add<Vignette>(true);
            edges.intensity.Override(.32f);
            edges.smoothness.Override(.5f);
        }

        Light MakeLight(string label, LightType type, Vector3 position, Color color, float power, float range)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(art, false);
            obj.transform.localPosition = position;
            var light = obj.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = power;
            light.range = range;
            if (type != LightType.Directional)
                practicalLights.Add(light);
            return light;
        }

        void MakeSpot(string label, Vector3 position, Vector3 target, Color color, float power, float range, float angle)
        {
            var lamp = MakeLight(label, LightType.Spot, position, color, power, range);
            lamp.transform.localRotation = Quaternion.LookRotation(target - position);
            lamp.spotAngle = angle;
            lamp.innerSpotAngle = angle * .67f;
        }
    }
}
