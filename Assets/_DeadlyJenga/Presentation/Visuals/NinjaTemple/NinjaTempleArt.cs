using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;
using P = DeadlyJenga.NinjaArt.NinjaPalette;

namespace DeadlyJenga.NinjaArt
{
    // Owns the night: sky, fog, moonlight, scenery, weather and ambience while this map exists.
    [DefaultExecutionOrder(985)]
    public sealed class NinjaTempleArt : MonoBehaviour
    {
        public float waterLevel = .08f;
        static readonly int FogColor = Shader.PropertyToID("_NinjaFogColor"), FogFar = Shader.PropertyToID("_NinjaFogFar"), FogParams = Shader.PropertyToID("_NinjaFogParams"),
            SkyAmb = Shader.PropertyToID("_NinjaSkyAmb"), GroundAmb = Shader.PropertyToID("_NinjaGroundAmb"), Warm = Shader.PropertyToID("_NinjaWarm"), Wind = Shader.PropertyToID("_NinjaWind"), Blood = Shader.PropertyToID("_NinjaBlood");
        public static readonly Vector3 MoonDirection = new Vector3(.402f, .309f, .862f).normalized;
        static readonly Color Horizon = new Color(.16f, .15f, .26f), Fog = new Color(.07f, .085f, .14f), FarFog = new Color(.12f, .12f, .21f);
        public static NinjaTempleArt Active { get; private set; }
        public float BloodMoon { get; private set; }

        Material sky, ground, props, foliage, far, water, previousSky;
        Light moon; readonly List<Light> practicals = new List<Light>();
        readonly Dictionary<Light, bool> sunStates = new Dictionary<Light, bool>();
        readonly Dictionary<Renderer, bool> hiddenRenderers = new Dictionary<Renderer, bool>();
        readonly Dictionary<CandyArt.CandyEnvironment, bool> environments = new Dictionary<CandyArt.CandyEnvironment, bool>();
        readonly List<Object> owned = new List<Object>();
        Transform scenery, weather; ParticleSystem rain, leaves, fireflies;
        AudioSource rainAudio, windAudio, waterAudio, taikoAudio;
        IWorldQuery world; ISyncedWorld synced; int decoratedRevision = -1, decoratedBlocks = -1;
        bool captured, previousFog; Color previousFogColor, previousAmbientSky, previousAmbientEquator, previousAmbientGround; FogMode previousFogMode; float previousFogStart, previousFogEnd; AmbientMode previousAmbient; SphericalHarmonicsL2 previousProbe;
        float nextScan, nextLights, nextClack, nextApply;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            Active = this; world = GetComponent<IWorldQuery>(); synced = world as ISyncedWorld;
            previousSky = RenderSettings.skybox; previousFog = RenderSettings.fog; previousFogColor = RenderSettings.fogColor; previousFogMode = RenderSettings.fogMode;
            previousFogStart = RenderSettings.fogStartDistance; previousFogEnd = RenderSettings.fogEndDistance; previousAmbient = RenderSettings.ambientMode;
            previousAmbientSky = RenderSettings.ambientSkyColor; previousAmbientEquator = RenderSettings.ambientEquatorColor; previousAmbientGround = RenderSettings.ambientGroundColor; previousProbe = RenderSettings.ambientProbe;
            captured = true; Build(); Apply(); nextScan = 0;
        }

        Material Make(string shader, string name)
        {
            var m = new Material(NinjaFx.Find(shader)) { name = name, enableInstancing = true }; owned.Add(m); return m;
        }
        Renderer Add(string name, Mesh mesh, Material material, Transform parent, bool shadows = false)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); owned.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = true; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return r;
        }
        static void Pool(NinjaGlowMesh glow, Vector3 at, float radius, float strength)
        {
            glow.Card(at + new Vector3(-radius, 0, -radius), at + new Vector3(-radius, 0, radius), at + new Vector3(radius, 0, radius), at + new Vector3(radius, 0, -radius), new Color(1, .5f, .2f, strength), 1);
        }
        void Blocker(Vector3 local, Vector3 size)
        {
            var go = new GameObject("Collision - shrine prop"); go.transform.SetParent(scenery, false); go.transform.localPosition = local;
            go.AddComponent<BoxCollider>().size = size;
        }
        void Build()
        {
            sky = Make("NinjaSky", "Ninja moon sky"); sky.SetVector("_MoonDir", MoonDirection);
            ground = Make("NinjaLacquer", "Ninja courtyard"); ground.SetFloat("_Gloss", .2f); ground.SetFloat("_Spec", .1f); ground.SetFloat("_Rim", .05f); ground.SetFloat("_Warm", 1.25f);
            props = Make("NinjaLacquer", "Ninja props"); props.SetFloat("_Gloss", .7f); props.SetFloat("_Spec", .35f); props.SetFloat("_Rim", .45f); props.SetFloat("_AlphaEmission", 4f);
            foliage = Make("NinjaLacquer", "Ninja foliage"); foliage.SetFloat("_Wind", 1); foliage.SetFloat("_Cull", 0); foliage.SetFloat("_Gloss", .35f); foliage.SetFloat("_Spec", .15f); foliage.SetFloat("_Rim", 1.1f); foliage.SetFloat("_Warm", .6f);
            far = Make("NinjaUnlit", "Ninja far silhouettes"); far.SetFloat("_UseFog", 0);
            water = Make("NinjaWater", "Ninja lake"); water.SetVector("_MoonDir", MoonDirection);
            scenery = new GameObject("Visual - moonlit scenery").transform; scenery.SetParent(transform, false);
            float wy = transform.InverseTransformPoint(new Vector3(0, waterLevel, 0)).y;
            var glow = new NinjaGlowMesh(); var mist = new NinjaGlowMesh();

            var groundMesh = new NinjaMesh(); NinjaTempleScenery.Ground(groundMesh, wy); Add("Visual - courtyard and walls", groundMesh.Build("Ninja courtyard"), ground, scenery);
            var lake = new NinjaMesh(); lake.Quad(new Vector3(-900, wy, -900), new Vector3(-900, wy, 900), new Vector3(900, wy, 900), new Vector3(900, wy, -900), Color.white); Add("Visual - lake", lake.Build("Ninja lake"), water, scenery);
            var mountains = new NinjaMesh(); NinjaTempleScenery.Mountains(mountains, Horizon * 1.05f, new Color(.018f, .02f, .04f));
            NinjaTempleScenery.FarCastle(mountains, glow, -NinjaTempleScenery.MoonFlat * 240 + Vector3.right * 60 + Vector3.up * 18, 1.3f, new Color(.03f, .032f, .055f));
            Add("Visual - layered ridges", mountains.Build("Ninja ridges"), far, scenery);

            var p = new NinjaMesh(); var f = new NinjaMesh(); var rng = new System.Random(5);
            var moonFlat = NinjaTempleScenery.MoonFlat; var facing = Quaternion.LookRotation(moonFlat);
            NinjaTempleScenery.Torii(p, moonFlat * 82 + Vector3.up * wy, facing, 20, 15, true, glow);
            for (int i = 0; i < 4; i++)
            {
                var axis = Quaternion.Euler(0, i * 90, 0);
                NinjaTempleScenery.Torii(p, axis * new Vector3(0, 0, 15.5f), axis, 7.5f, 5.4f, true, glow);
                NinjaTempleScenery.Maple(f, axis * new Vector3(31.5f, 0, 31.5f), 1.6f, i * 4 + 20);
                for (int s = -1; s <= 1; s += 2)
                {
                    NinjaTempleScenery.StoneLantern(p, axis * new Vector3(s * 3.4f, 0, 20.5f), 1.15f, glow, i * 10 + s + 7);
                    Blocker(axis * new Vector3(s * 3.4f, 1.1f, 20.5f), new Vector3(1, 2.4f, 1));
                    Blocker(axis * new Vector3(s * 2.7f, 3.5f, 15.5f), new Vector3(.85f, 7, .85f));
                }
            }
            float[] islet = { 48, 52, 47, 55 };
            for (int i = 0; i < 4; i++)
            {
                var dirc = Quaternion.Euler(0, 45 + i * 90, 0) * Vector3.forward; var at = dirc * (islet[i] + 6);
                NinjaTempleScenery.Rock(p, at, new Vector3(9, 3.2f, 7.5f), 30 + i, wy);
                NinjaTempleScenery.Bamboo(f, at, 6.5f, 26, 40 + i, wy + 1.2f);
            }
            for (int i = 0; i < 14; i++)
            {
                float a = i * 2.39996f + .3f, r = 75 + (i * 37 % 70); var at = new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r);
                if (Vector3.Dot(at.normalized, moonFlat) > .93f) continue;
                var size = new Vector3(6 + i % 4 * 3, 2.5f + i % 3 * 1.8f, 5 + i % 5 * 2.5f); NinjaTempleScenery.Rock(p, at, size, 60 + i, wy);
                if (i % 3 == 0) NinjaTempleScenery.Pine(f, at + Vector3.up * (wy + size.y * .55f), 1.6f + i % 2 * .5f, 70 + i);
                else if (i % 3 == 1) NinjaTempleScenery.Bamboo(f, at, size.x * .55f, 12, 80 + i, wy + size.y * .45f);
                else { NinjaTempleScenery.StoneLantern(p, at + Vector3.up * (wy + size.y * .5f), 1.6f, glow, 90 + i); NinjaTempleScenery.Maple(f, at + Vector3.up * (wy + size.y * .5f) + Vector3.right * 2, 1.4f, 95 + i); }
            }
            for (int i = 0; i < 46; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, r = 40 + (float)rng.NextDouble() * 75; var at = new Vector3(Mathf.Sin(a) * r, wy + .12f, Mathf.Cos(a) * r);
                var col = i % 3 == 0 ? new Color(.95f, .85f, .65f) : P.Vermilion;
                p.Box(at, new Vector3(.55f, .08f, .55f), Quaternion.Euler(0, i * 37, 0), P.Wood, P.Wood);
                p.Box(at + Vector3.up * .32f, new Vector3(.42f, .5f, .42f), Quaternion.Euler(0, i * 37, 0), P.Paper(Color.Lerp(col, new Color(1, .7f, .35f), .5f), .85f), P.Paper(col, .7f));
                glow.Halo(at + Vector3.up * .35f, .8f, new Color(1, .5f, .2f, .55f), i * 1.3f + 2);
                glow.Halo(at + Vector3.down * .1f, 2.2f, new Color(.8f, .32f, .12f, .25f), 0, 1);
            }
            Add("Visual - shrines, gates and stones", p.Build("Ninja props"), props, scenery, true);
            Add("Visual - bamboo, pines and maples", f.Build("Ninja foliage"), foliage, scenery, true);

            for (int i = 0; i < 26; i++)
            {
                float a = i * 2.39996f, r = 42 + (i * 53 % 80); float size = 16 + (i * 7 % 14);
                mist.Halo(new Vector3(Mathf.Sin(a) * r, wy + 1.5f + (i % 3), Mathf.Cos(a) * r), size, new Color(.42f, .5f, .7f, .14f), 0, 1);
            }
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2 / 12; var at = new Vector3(Mathf.Sin(a) * 30, .8f, Mathf.Cos(a) * 30);
                mist.Halo(at, 9, new Color(.4f, .45f, .62f, .2f), 0, 1);
            }
            for (int ridge = 0; ridge < 3; ridge++) for (int i = 0; i < 24; i++)
            {
                float rr = new[] { 225f, 350f, 545f }[ridge]; float a = i * Mathf.PI * 2 / 24 + ridge;
                mist.Halo(new Vector3(Mathf.Sin(a) * rr, wy + 6 + ridge * 8, Mathf.Cos(a) * rr), 60 + ridge * 40, new Color(.32f, .34f, .5f, .22f), 0, 1);
            }
            for (int i = 0; i < 4; i++) for (int s = -1; s <= 1; s += 2)
            {
                var axis = Quaternion.Euler(0, i * 90, 0);
                Pool(glow, axis * new Vector3(s * 3.4f, .04f, 20.5f), 3.2f, .5f);
                Pool(glow, axis * new Vector3(s * 2.7f, .04f, 15.5f), 2.6f, .35f);
            }
            for (int i = 0; i < 4; i++) Pool(glow, Quaternion.Euler(0, i * 90 + 45, 0) * new Vector3(0, .04f, 11), 6.5f, .28f);
            var beamBase = moonFlat * 160 + Vector3.up * 70;
            for (int i = 0; i < 5; i++)
            {
                var side = Vector3.Cross(Vector3.up, moonFlat) * (i - 2) * 16; var top = beamBase + side; var bottom = moonFlat * 40 + side * 2.2f + Vector3.up * wy;
                var across = Vector3.Cross(Vector3.up, moonFlat) * (3 + i % 2 * 2);
                glow.Card(bottom - across * 1.6f, top - across, top + across, bottom + across * 1.6f, new Color(.35f, .42f, .6f, .1f), 2);
            }
            Add("Visual - lantern glow", glow.Build("Ninja glows", 30), NinjaFx.Glow, scenery);
            Add("Visual - mist banks", mist.Build("Ninja mist", 70), NinjaFx.Mist, scenery);

            for (int i = 0; i < 4; i++)
            {
                var light = new GameObject("Light - stone lantern pool").AddComponent<Light>(); light.transform.SetParent(scenery, false);
                light.transform.localPosition = Quaternion.Euler(0, i * 90 + 45, 0) * new Vector3(0, 2.4f, 13); light.type = LightType.Point; light.range = 13; light.intensity = 1.6f;
                light.color = new Color(1, .55f, .26f); light.shadows = LightShadows.None; light.renderMode = LightRenderMode.ForcePixel; practicals.Add(light);
            }
            moon = new GameObject("Light - moon").AddComponent<Light>(); moon.transform.SetParent(transform, false); moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.LookRotation(-new Vector3(MoonDirection.x, .62f, MoonDirection.z).normalized);
            moon.color = new Color(.62f, .72f, 1); moon.intensity = .9f; moon.shadows = LightShadows.Hard; moon.shadowStrength = .8f;

            weather = new GameObject("Visual - weather").transform; weather.SetParent(transform, false);
            rain = NinjaFx.Rain(weather); leaves = NinjaFx.Leaves(weather); fireflies = NinjaFx.Fireflies(weather);
            rainAudio = NinjaSounds.Loop(weather, NinjaSounds.Rain, .16f, false); windAudio = NinjaSounds.Loop(weather, NinjaSounds.Wind, .2f, false);
            waterAudio = NinjaSounds.Loop(weather, NinjaSounds.Water, .1f, false); taikoAudio = NinjaSounds.Loop(weather, NinjaSounds.Taiko, 0, false);
        }

        void Apply()
        {
            RenderSettings.skybox = sky; RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 26; RenderSettings.fogEndDistance = 230;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            var probe = new SphericalHarmonicsL2(); probe.AddAmbientLight(new Color(.15f, .16f, .26f)); RenderSettings.ambientProbe = probe;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.gameObject.scene==gameObject.scene && light.type == LightType.Directional && light != moon && !light.transform.IsChildOf(transform)) { if (!sunStates.ContainsKey(light)) sunStates.Add(light, light.enabled); light.enabled = false; }
            if(CandyArt.CandyEnvironment.ForMap(this) is CandyArt.CandyEnvironment env && env)
            {
                if (!environments.ContainsKey(env)) environments.Add(env, env.applyLighting); env.applyLighting = false;
                foreach (var r in env.GetComponentsInChildren<Renderer>(true)) { if (!hiddenRenderers.ContainsKey(r)) hiddenRenderers.Add(r, r.enabled); r.enabled = false; }
            }
        }

        void LateUpdate()
        {
            if (!captured) return;
            float target = 0;
            BloodMoon = Mathf.MoveTowards(BloodMoon, target, Time.deltaTime * (target > BloodMoon ? 1.4f : .5f));
            float b = BloodMoon * BloodMoon * (3 - 2 * BloodMoon);
            var fog = Color.Lerp(Fog, new Color(.13f, .045f, .065f), b * .65f); var farFog = Color.Lerp(FarFog, new Color(.2f, .06f, .09f), b * .6f);
            Shader.SetGlobalColor(FogColor, fog); Shader.SetGlobalColor(FogFar, farFog);
            Shader.SetGlobalVector(FogParams, new Vector4(22, 260, .07f, waterLevel));
            Shader.SetGlobalColor(SkyAmb, Color.Lerp(new Color(.11f, .13f, .22f), new Color(.2f, .07f, .09f), b));
            Shader.SetGlobalColor(GroundAmb, new Color(.045f, .035f, .05f));
            Shader.SetGlobalVector(Warm, new Vector4(.42f, .16f, .05f, 6.5f));
            Shader.SetGlobalVector(Wind, new Vector4(.22f, 1.25f, .8f, .6f));
            Shader.SetGlobalFloat(Blood, b);
            RenderSettings.fogColor = fog;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(.2f, .23f, .36f), new Color(.34f, .12f, .14f), b);
            RenderSettings.ambientEquatorColor = new Color(.13f, .12f, .18f); RenderSettings.ambientGroundColor = new Color(.09f, .06f, .06f);
            if (moon) moon.color = Color.Lerp(new Color(.62f, .72f, 1), new Color(1, .45f, .4f), b * .85f);
            if (taikoAudio) taikoAudio.volume = Mathf.Lerp(0, .32f, b) * Master * NinjaSounds.Gain(NinjaSounds.Taiko);
            var view = Services.Session?.ActiveCamera;
            if (view && weather)
            {
                var at = view.transform.position; rain.transform.position = at; leaves.transform.position = at; fireflies.transform.position = new Vector3(at.x, transform.position.y, at.z);
                bool sheltered = world != null && world.IsSheltered(at);
                if (rainAudio) rainAudio.volume = Mathf.MoveTowards(rainAudio.volume, (sheltered ? .1f : .17f) * Master * NinjaSounds.Gain(NinjaSounds.Rain), Time.deltaTime * .3f);
            }
            if (Time.unscaledTime >= nextClack)
            {
                nextClack = Time.unscaledTime + Random.Range(14f, 32f);
                if (view) NinjaSounds.Play(NinjaSounds.Clack, view.transform.position + Random.onUnitSphere * 25, .12f, Random.Range(.9f, 1.1f));
            }
            if (Time.unscaledTime >= nextLights)
            {
                nextLights = Time.unscaledTime + .4f; if (Time.unscaledTime >= nextApply) { nextApply = Time.unscaledTime + 2; Apply(); }
                var p = view ? view.transform.position : transform.position; bool vr = Services.Session?.IsVR == true;
                for (int i = 0; i < practicals.Count; i++)
                {
                    int closer = 0; float d = (practicals[i].transform.position - p).sqrMagnitude;
                    for (int j = 0; j < practicals.Count; j++) if (j != i && (practicals[j].transform.position - p).sqrMagnitude < d) closer++;
                    practicals[i].enabled = closer < (vr ? 1 : 2);
                }
            }
            if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + .5f; Decorate(); }
        }
        static float Master { get { var engine = CandyAudio.CandyAudioEngine.Instance; return engine && engine.bank ? engine.bank.master : .8f; } }

        void Decorate()
        {
            if (world == null) return; var blocks = world.Blocks; int revision = synced?.Revision ?? 0;
            if (revision == decoratedRevision && blocks.Count == decoratedBlocks) return;
            decoratedRevision = revision; decoratedBlocks = blocks.Count;
            foreach (var body in blocks)
            {
                if (!body || body.transform.Find("Visual - ninja decor")) continue;
                var size = body.transform.lossyScale; var kind = NinjaTempleDecor.Classify(size); if (kind == NinjaTempleDecor.Kind.None) continue;
                NinjaTempleDecor.Meshes(kind, size, out var mesh, out var glowMesh);
                var root = new GameObject("Visual - ninja decor").transform; root.SetParent(body.transform, false);
                var ls = body.transform.localScale; root.localScale = new Vector3(1 / ls.x, 1 / ls.y, 1 / ls.z);
                var r = root.gameObject.AddComponent<MeshRenderer>(); root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh; r.sharedMaterial = props;
                r.shadowCastingMode = kind == NinjaTempleDecor.Kind.Deck ? ShadowCastingMode.On : ShadowCastingMode.Off; r.lightProbeUsage = LightProbeUsage.Off;
                var g = new GameObject("Visual - ninja decor glow"); g.transform.SetParent(root, false);
                g.AddComponent<MeshFilter>().sharedMesh = glowMesh; var gr = g.AddComponent<MeshRenderer>(); gr.sharedMaterial = NinjaFx.Glow; gr.shadowCastingMode = ShadowCastingMode.Off; gr.lightProbeUsage = LightProbeUsage.Off;
            }
        }

        void OnDisable()
        {
            if (!captured) return; captured = false; if (Active == this) Active = null;
            foreach (var pair in sunStates) if (pair.Key) pair.Key.enabled = pair.Value; sunStates.Clear();
            foreach (var pair in environments) if (pair.Key) pair.Key.applyLighting = pair.Value; environments.Clear();
            foreach (var pair in hiddenRenderers) if (pair.Key) pair.Key.enabled = pair.Value; hiddenRenderers.Clear();
            RenderSettings.skybox = previousSky; RenderSettings.fog = previousFog; RenderSettings.fogColor = previousFogColor; RenderSettings.fogMode = previousFogMode;
            RenderSettings.fogStartDistance = previousFogStart; RenderSettings.fogEndDistance = previousFogEnd; RenderSettings.ambientMode = previousAmbient;
            RenderSettings.ambientSkyColor = previousAmbientSky; RenderSettings.ambientEquatorColor = previousAmbientEquator; RenderSettings.ambientGroundColor = previousAmbientGround; RenderSettings.ambientProbe = previousProbe;
            Shader.SetGlobalFloat(Blood, 0);
            if (scenery) Destroy(scenery.gameObject); if (weather) Destroy(weather.gameObject); if (moon) Destroy(moon.gameObject);
            foreach (var o in owned) if (o) Destroy(o); owned.Clear(); practicals.Clear();
        }
    }
}
