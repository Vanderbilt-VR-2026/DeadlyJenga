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
            AnimateFire();

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
            float windowX = (WindowWest + WindowEast) * .5f;
            MakeSpot("Moonlight through the window", new Vector3(windowX, WindowHead, 54f),
                new Vector3(windowX, 0f, -80f), new Color(.16f, .36f, 1f), 24000f, 300f, 70f);
            MakeSpot("Moonlight across the room", new Vector3(windowX + 40f, WindowHead - 10f, 54f),
                new Vector3(windowX + 70f, 0f, -50f), new Color(.12f, .30f, 1f), 14000f, 280f, 60f);
            // Fire on the east wall, then lit doorways and a lamp up on the loft.
            fireLight = MakeLight("Fireplace glow", LightType.Point, new Vector3(BreastFront - 18f, 34f, HearthZ),
                new Color(1f, .45f, .14f), 14000f, 230f);
            MakeSpot("Moonlight through the south window", new Vector3((SouthWindowWest + SouthWindowEast) * .5f, WindowHead, RoomSouth + 4f),
                new Vector3((SouthWindowWest + SouthWindowEast) * .5f, 0f, RoomSouth + 150f), new Color(.16f, .36f, 1f), 26000f, 300f, 80f);
            MakeLight("Kitchen light", LightType.Point, new Vector3(RoomWest - 60f, 110f, (ArchSouth + ArchNorth) * .5f),
                new Color(1f, .70f, .40f), 9000f, 200f);
            MakeLight("Bookcase lamp", LightType.Point, new Vector3(RoomWest + 60f, 120f, -330f),
                new Color(1f, .68f, .38f), 9000f, 220f);
            MakeLight("Glow in the train tunnel", LightType.Point,
                new Vector3(-150, 16, -128), new Color(1f, .68f, .28f), 700f, 38f);
            // The lamp lights what is near it: the wall, the table and the end of the sofa.
            MakeLight("Floor lamp", LightType.Point,
                LampPlace + Vector3.up * 59f, new Color(1f, .70f, .39f), 9000f, 120f);

            // The tree is the room's main light. A warm heart, then a ring of coloured lights
            // low and just outside the branches. Short reach, so each tints only the gifts beside it
            // instead of all of them adding up to one orange pool on the rug.
            MakeLight("Golden light in the tree", LightType.Point,
                new Vector3(-37, 75, -50), new Color(1f, .66f, .36f), 600f, 170f);
            // The same four colours as the bulbs: red, yellow, green, blue.
            var bulbGlow = new[] { new Color(1f, .08f, .05f), new Color(1f, .70f, .12f),
                new Color(.08f, 1f, .20f), new Color(.08f, .32f, 1f) };
            // Twelve lights spiral up the outside of the tree from the gifts to the top, so the
            // colour lands on the presents below and on the branches all the way up.
            for (int i = 0; i < 12; i++)
            {
                float height = (12f + i * 15f) * FirHeight / FirScale;
                float spread = Mathf.Lerp(78f, 18f, height / (200f * FirHeight / FirScale));
                float angle = (i * 137.5f + 10f) * Mathf.Deg2Rad;
                var place = new Vector3(-37f + Mathf.Cos(angle) * spread, height, -35.7f + Mathf.Sin(angle) * spread);
                MakeLight("Tree bulb glow " + i, LightType.Point, place, bulbGlow[i % bulbGlow.Length],
                    height < 60f ? 6000f : 3000f, height < 60f ? 95f : 55f);
            }

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
