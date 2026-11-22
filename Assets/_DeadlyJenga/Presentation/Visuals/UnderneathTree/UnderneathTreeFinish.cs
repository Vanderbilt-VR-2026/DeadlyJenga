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
            BuildCarpet();
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
            AnimateFire();
            AnimateCarpet();
            AnimateDog();

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
            // Night, but not black: deep blue from the windows, plum at the sides and warm red off the rug,
            // so shapes in shadow still read while the coloured lights make the pools.
            var sky = new Color(.065f, .085f, .19f);
            var sides = new Color(.045f, .065f, .145f);
            var rug = new Color(.055f, .037f, .06f);
            if (!RenderSettings.skybox && !RenderSettings.fog && RenderSettings.ambientMode == AmbientMode.Trilight &&
                RenderSettings.ambientSkyColor == sky && Mathf.Approximately(RenderSettings.reflectionIntensity, .3f))
                return;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = sides;
            RenderSettings.ambientGroundColor = rug;
            RenderSettings.reflectionIntensity = .3f;
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
                    name.StartsWith("Tree Wire", StringComparison.Ordinal) || name == "Fireplace Fire" || name == "Tree Star" ||
                    name.StartsWith("Wood Planks", StringComparison.Ordinal) || name == "Tree Skirt rim" ||
                    name.StartsWith("Sofa", StringComparison.Ordinal) || name == "Lamp Shade" || name == "Side Table" ||
                    name.StartsWith("Room Wall", StringComparison.Ordinal) || name == "Room Ceiling" ||
                    OldRoomDressing(renderer))
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
                if (RaiseToCeiling(renderer))
                    continue;
                ApplyFinish(renderer);
                SoftenGiftEdges(renderer);
                MapRugTexture(renderer);
                AddGiftBow(renderer);
            }
            LightToyBuildings();
        }

        // Big walk-in toy buildings get warm lights inside, like a dollhouse left on at night,
        // so their rooms can be seen and played in. Found by name and size only.
        readonly HashSet<int> litBuildings = new HashSet<int>();

        void LightToyBuildings()
        {
            foreach (Transform child in world.Root.GetComponentsInChildren<Transform>())
            {
                if (!child.name.StartsWith("Toy ", StringComparison.Ordinal) || litBuildings.Contains(child.GetInstanceID()))
                    continue;
                var renderers = child.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length < 40)
                    continue;
                var box = renderers[0].bounds;
                foreach (var r in renderers)
                    box.Encapsulate(r.bounds);
                if (box.size.x < 20f && box.size.z < 20f)
                    continue;
                litBuildings.Add(child.GetInstanceID());
                int floors = Mathf.Clamp(Mathf.RoundToInt(box.size.y / 9f), 1, 4);
                for (int i = 0; i < floors; i++)
                {
                    var glow = new GameObject(child.name + " room light");
                    glow.transform.position = new Vector3(box.center.x, box.min.y + 3f + i * (box.size.y - 6f) / Mathf.Max(1, floors - 1), box.center.z);
                    glow.transform.SetParent(art, true);
                    var light = glow.AddComponent<Light>();
                    light.type = LightType.Point;
                    // Soft and warm, like a night light: enough to see the rooms, not to wash them out.
                    light.color = new Color(1f, .72f, .45f);
                    light.intensity = 90f;
                    light.range = Mathf.Max(box.extents.x, box.extents.z) * 1.1f;
                    practicalLights.Add(light);
                }
            }
        }

        // The block-out's fireplace, garland, pictures and window sat on the old small room's walls.
        // The family room builds its own, so these would float in the middle of it.
        static bool OldRoomDressing(MeshRenderer renderer)
        {
            string name = renderer.name;
            // The old mantel's garland bulbs are plain "Sphere" pieces at mantel height.
            if (name.StartsWith("Sphere", StringComparison.Ordinal))
                return renderer.bounds.max.y < 100f && renderer.bounds.size.x < 6f;
            return name.StartsWith("Fireplace", StringComparison.Ordinal) || name.StartsWith("Garland", StringComparison.Ordinal) ||
            name.StartsWith("Stocking", StringComparison.Ordinal) || name.StartsWith("Picture", StringComparison.Ordinal) ||
            name.StartsWith("Room Baseboard", StringComparison.Ordinal) || name.StartsWith("Window Frame", StringComparison.Ordinal) ||
            name.StartsWith("Window Mullion", StringComparison.Ordinal);
        }

        // Decorations hung just under the map's own ceiling (the string of lights along the walls)
        // hung on the old small room's walls, which are gone, so they stay hidden.
        bool RaiseToCeiling(MeshRenderer renderer)
        {
            // Already copied on an earlier refresh.
            if (hidden.ContainsKey(renderer))
                return renderer.name.StartsWith("Sphere", StringComparison.Ordinal) || !renderer.enabled;
            var box = renderer.bounds;
            if (box.min.y < 150f || box.max.y > 177f || box.max.x > 86f || box.min.z < -266f || renderer.GetComponentInParent<Rigidbody>())
                return false;
            if (!renderer.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh)
                return false;
            Hide(renderer);
            return true;
        }

        void OnDisable()
        {
            if (!captured)
                return;
            captured = false;
            ClearCarpet();
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
            if (stoneTexture)
                Destroy(stoneTexture);
            foreach (var clip in clips)
                if (clip)
                    Destroy(clip);
        }

        void BuildLighting()
        {
            key = MakeLight("Faint night fill", LightType.Directional,
                Vector3.zero, new Color(.52f, .62f, 1f), .06f, 0f);
            // Leave enough fill to read edges between the coloured bulb pools.
            key.transform.localRotation = Quaternion.Euler(46f, 18f, 0f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = .78f;
            key.shadowBias = .08f;
            key.shadowNormalBias = .2f;

            var bounce = MakeLight("Soft blue room bounce", LightType.Directional, Vector3.zero,
                new Color(.18f, .38f, 1f), .055f, 0f);
            // Keep the unlit room cool between the local practicals.
            bounce.transform.localRotation = Quaternion.Euler(30f, 262f, 0f);
            practicalLights.Add(bounce);

            // Moonlight falls in through each window onto the floor just inside it.
            float windowX = (WindowWest + WindowEast) * .5f;
            MakeSpot("Moonlight through the window", new Vector3(windowX, WindowSill + 42f, 54f),
                new Vector3(windowX, 0f, 24f), new Color(.24f, .45f, 1f), 7000f, 108f, 62f);
            MakeSpot("Moonlight at the window edge", new Vector3(windowX + 40f, WindowSill + 32f, 54f),
                new Vector3(windowX + 52f, 0f, 30f), new Color(.18f, .38f, 1f), 4400f, 95f, 53f);
            // Keep the fireplace and windows as the main room accents.
            fireLight = MakeLight("Fireplace glow", LightType.Point, new Vector3(2f * FireplaceTurnX - (BreastFront - 18f), 34f, HearthZ),
                new Color(1f, .45f, .14f), 14000f, 138f);
            float southWindowX = (SouthWindowWest + SouthWindowEast) * .5f;
            MakeSpot("Moonlight through the south window", new Vector3(southWindowX, WindowSill + 50f, RoomSouth + 4f),
                new Vector3(southWindowX, 0f, RoomSouth + 35f), new Color(.20f, .40f, 1f), 8500f, 115f, 72f);
            MakeLight("Kitchen light", LightType.Point, new Vector3(RoomWest - 60f, 110f, (ArchSouth + ArchNorth) * .5f),
                new Color(.36f, .52f, 1f), 1400f, 140f);
            MakeLight("Bookcase lamp", LightType.Point, new Vector3(RoomEast - 60f, 120f, BookcaseZ),
                new Color(1f, .68f, .38f), 900f, 100f);
            // The lamp lights what is near it: the wall, the table and the end of the sofa.
            MakeLight("Floor lamp", LightType.Point,
                LampPlace + Vector3.up * 59f, new Color(1f, .70f, .39f), 750f, 80f);

            BuildCourseBulbPools();

            var reflections = new GameObject("Christmas Room Reflections");
            reflections.transform.SetParent(art, false);
            reflections.transform.localPosition = new Vector3((RoomWest + RoomEast) * .5f, 90f, (RoomSouth + RoomNorth) * .5f);
            roomProbe = reflections.AddComponent<ReflectionProbe>();
            roomProbe.mode = ReflectionProbeMode.Realtime;
            roomProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            roomProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            roomProbe.resolution = 256;
            roomProbe.hdr = true;
            roomProbe.size = new Vector3(RoomEast - RoomWest + 10f, RoomTop + 10f, RoomNorth - RoomSouth + 10f);
            roomProbe.boxProjection = true;
            roomProbe.intensity = .7f;

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
            bloom.intensity.Override(.22f);
            bloom.scatter.Override(.72f);
            var color = grade.Add<ColorAdjustments>(true);
            color.postExposure.Override(.1f);
            color.contrast.Override(12f);
            color.saturation.Override(9f);
            var edges = grade.Add<Vignette>(true);
            edges.intensity.Override(.32f);
            edges.smoothness.Override(.5f);
        }

        void BuildCourseBulbPools()
        {
            var red = new Color(1f, .20f, .16f);
            var blue = new Color(.085f, .26f, 1f);
            var green = new Color(.20f, 1f, .35f);
            var yellow = new Color(1f, .93f, .23f);

            // Oblique throws cross seams and open carpet. Each offset controls the shape on the surface.
            var pools = new (string label, Vector3 target, Vector3 offset, Color color, float power, float angle)[]
            {
                ("Left rail curve", new Vector3(-72f, 10f, -170f), new Vector3(15f, 24f, 5f), red, 4950f, 70f),
                ("Near-left rail", new Vector3(-29f, 11f, -151f), new Vector3(-5f, 21f, 14f), blue, 4600f, 63f),
                ("Near rail seam", new Vector3(-6f, 11f, -142f), new Vector3(18f, 23f, 2f), yellow, 4400f, 61f),
                ("Right gift steps", new Vector3(19f, 10f, -112f), new Vector3(-10f, 28f, 9f), red, 4250f, 69f),
                ("Right bend", new Vector3(-4f, 11f, -76f), new Vector3(14f, 27f, -9f), yellow, 4900f, 74f),
                ("Top stack ribbon", new Vector3(-23f, 44f, -89f), new Vector3(12f, 20f, -3f), red, 3500f, 64f),
                ("Bridge and inner rug", new Vector3(-47f, 18f, -89f), new Vector3(-8f, 24f, 14f), green, 4600f, 77f),
                ("Bridge approach", new Vector3(-92f, 21f, -119f), new Vector3(13f, 26f, -7f), blue, 5600f, 64f),
                ("Upper steps", new Vector3(-143f, 32f, -127f), new Vector3(-13f, 22f, -9f), yellow, 4400f, 65f),
                ("Far rail edge", new Vector3(-140f, 10f, -151f), new Vector3(17f, 20f, -2f), yellow, 3400f, 55f),
                ("Spawn wall", new Vector3(-181f, 18f, -101f), new Vector3(-6f, 26f, -10f), blue, 2200f, 53f),
                ("Bridge bow lip", new Vector3(-41f, 28f, -115f), new Vector3(13f, 20f, 3f), yellow, 2700f, 54f),
                ("Right stair ribbon", new Vector3(15f, 25f, -82f), new Vector3(7f, 18f, 8f), yellow, 2300f, 62f),
                ("Toy tower", new Vector3(-165f, 50f, -119f), new Vector3(-9f, 23f, 13f), blue, 4400f, 68f),
                ("Left stair corner", new Vector3(-81f, 20f, -177f), new Vector3(17f, 20f, -3f), yellow, 2150f, 52f),
                ("Rear curve", new Vector3(-124f, 10f, -60f), new Vector3(-16f, 22f, 3f), green, 3600f, 65f),
                ("Inside the near loop", new Vector3(-72f, 1f, -137f), new Vector3(17f, 16f, -8f), blue, 1600f, 54f),
                ("Carpet between low parcels", new Vector3(-106f, 1f, -121f), new Vector3(-5f, 18f, 11f), red, 2000f, 72f),
                ("Carpet beside the train", new Vector3(-31f, 1f, -59f), new Vector3(14f, 18f, -4f), yellow, 1750f, 57f),
                ("Side aisle", new Vector3(-153f, 4f, -84f), new Vector3(-10f, 19f, -12f), green, 2150f, 63f),
                ("Back of the tower", new Vector3(-153f, 7f, -45f), new Vector3(14f, 21f, 4f), blue, 2800f, 70f),
                ("Rear rug under the boughs", new Vector3(-44f, 1f, -9f), new Vector3(-12f, 13f, -6f), red, 1650f, 66f),
                ("Behind the trunk", new Vector3(-17f, 2f, -20f), new Vector3(-3f, 19f, -11f), blue, 1550f, 60f),
                ("Far side of the skirt", new Vector3(-78f, 2f, 17f), new Vector3(16f, 20f, -4f), yellow, 2400f, 78f),
                ("Tree-side gap", new Vector3(-102f, 2f, -6f), new Vector3(-11f, 15f, 3f), green, 1400f, 55f),
                ("Window-side gifts", new Vector3(-5f, 6f, -39f), new Vector3(-9f, 22f, -12f), red, 2350f, 60f),
                ("Far carpet fold", new Vector3(-121f, 1f, 4f), new Vector3(4f, 16f, -13f), blue, 1650f, 53f),
            };
            foreach (var (label, target, offset, color, power, angle) in pools)
                MakeSpot(label, target + offset, target, color, power, offset.magnitude * 1.8f, angle, .28f);
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

        void MakeSpot(string label, Vector3 position, Vector3 target, Color color, float power, float range,
            float angle, float innerFraction = .67f)
        {
            var lamp = MakeLight(label, LightType.Spot, position, color, power, range);
            lamp.transform.localRotation = Quaternion.LookRotation(target - position);
            lamp.spotAngle = angle;
            lamp.innerSpotAngle = angle * innerFraction;
        }
    }
}
