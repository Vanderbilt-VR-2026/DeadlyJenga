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
        Light key;
        Mesh roundedWrap;
        Volume volume;
        VolumeProfile grade;
        bool neutralView;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
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
            if (Application.platform != RuntimePlatform.Android && Services.Session?.ActiveCamera is Camera active &&
                active.TryGetComponent<UniversalAdditionalCameraData>(out var data))
            {
                if (!cameras.ContainsKey(data))
                    cameras.Add(data, data.renderPostProcessing);
                data.renderPostProcessing = true;
            }
        }

        void RestoreIndoorSettings()
        {
            var fill = new Color(.30f, .265f, .29f);
            if (!RenderSettings.skybox && !RenderSettings.fog && RenderSettings.ambientMode == AmbientMode.Flat &&
                RenderSettings.ambientLight == fill && Mathf.Approximately(RenderSettings.reflectionIntensity, .38f))
                return;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = fill;
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(fill);
            RenderSettings.ambientProbe = probe;
            RenderSettings.reflectionIntensity = .38f;
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
                    name.StartsWith("Wood Planks", StringComparison.Ordinal) || name == "Tree Skirt rim")
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
                AddGiftBow(renderer);
            }
        }

        void OnDisable()
        {
            if (!captured)
                return;
            captured = false;
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
        }

        void BuildLighting()
        {
            key = MakeLight("Warm ceiling bounce", LightType.Directional,
                Vector3.zero, new Color(1f, .82f, .66f), 1.28f, 0f);
            key.transform.localRotation = Quaternion.Euler(51f, 34f, 0f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = .72f;

            MakeSpot("Moonlight from the front window", new Vector3(10, 65, 70),
                new Vector3(-26, 2, -44), new Color(.43f, .63f, 1f), 1220f, 177f, 105f);
            MakeSpot("Hearth bounce", new Vector3(-113, 37, -148),
                new Vector3(-45, 7, -54), new Color(1f, .45f, .13f), 1150f, 220f, 115f);
            MakeLight("Golden light under the tree", LightType.Point,
                new Vector3(-9, 18, -30), new Color(1f, .58f, .28f), 155f, 64f);
            MakeSpot("Warm fill across the junction", new Vector3(18, 30, -5),
                new Vector3(1, 1, -23), new Color(1f, .73f, .43f), 290f, 77f, 91f);
            MakeLight("Floor lamp", LightType.Point,
                new Vector3(-203, 63, -12), new Color(1f, .67f, .31f), 430f, 112f);

            if (Application.platform == RuntimePlatform.Android)
                return;
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
            bloom.threshold.Override(.85f);
            bloom.intensity.Override(.39f);
            bloom.scatter.Override(.62f);
            var color = grade.Add<ColorAdjustments>(true);
            color.postExposure.Override(.24f);
            color.contrast.Override(10f);
            color.saturation.Override(10f);
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
