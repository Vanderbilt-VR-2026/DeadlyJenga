using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;
using P = DeadlyJenga.NinjaArt.NinjaPalette;

namespace DeadlyJenga.NinjaArt
{
    // Shadow Lantern presentation on any map: the spirit lantern, its light, smoke bombs, steals and the Blood Moon gong.
    public sealed class NinjaLanternView : MonoBehaviour
    {
        Transform root, spin; Light glowLight; ParticleSystem smoke, sparks, embers; AudioSource hum;
        Material body; Renderer beam;
        int lastEvent = -1; bool built, seen; Vector3 shown, flightFrom; float pulse, carriedBlend, flightStart = -10;
        readonly Dictionary<string, bool> wasDying = new Dictionary<string, bool>();
        static readonly int FogParams = Shader.PropertyToID("_NinjaFogParams");

        void Build()
        {
            built = true;
            root = new GameObject("Visual - spirit lantern").transform; spin = new GameObject("Lantern body").transform; spin.SetParent(root, false);
            body = new Material(NinjaFx.Find("NinjaLacquer")) { name = "Spirit lantern", enableInstancing = true };
            body.SetFloat("_AlphaEmission", 4f); body.SetFloat("_Warm", 0); body.SetFloat("_Rim", .4f); body.SetFloat("_Gloss", .8f);
            var m = new NinjaMesh(); NinjaTempleScenery.Lantern(m, Vector3.zero, .62f, P.Vermilion);
            m.Lathe(new Vector3(0, .36f, 0), new[] { new Vector2(0, 0), new Vector2(.3f, 0), new Vector2(.3f, .07f), new Vector2(.12f, .12f), new Vector2(0, .14f) }, 12, new[] { P.Gold, P.Gold, P.Gold, P.Gold, P.Gold });
            m.Lathe(new Vector3(0, -.45f, 0), new[] { new Vector2(0, 0), new Vector2(.3f, 0), new Vector2(.3f, .07f), new Vector2(0, .07f) }, 12, new[] { P.Gold, P.Gold, P.Gold, P.Gold });
            m.Cylinder(new Vector3(0, -.45f, 0), new Vector3(0, -.95f, 0), .05f, .11f, 6, P.Vermilion, P.Gold);
            m.Cylinder(new Vector3(0, .5f, 0), new Vector3(0, .85f, 0), .025f, .025f, 4, P.Lacquer, P.Lacquer);
            var go = spin.gameObject; go.AddComponent<MeshFilter>().sharedMesh = m.Build("Spirit lantern"); var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = body; r.shadowCastingMode = ShadowCastingMode.Off;
            var g = new NinjaGlowMesh(); g.Halo(Vector3.zero, 3.4f, new Color(1, .5f, .2f, .8f), 2.1f); g.Halo(Vector3.zero, 1.2f, new Color(1, .85f, .6f, .9f), 5.3f); g.Halo(Vector3.down * 1.4f, 5.5f, new Color(.9f, .35f, .12f, .25f), 0, 1);
            var halo = new GameObject("Lantern halo"); halo.transform.SetParent(root, false); halo.AddComponent<MeshFilter>().sharedMesh = g.Build("Spirit lantern halo", 6);
            var hr = halo.AddComponent<MeshRenderer>(); hr.sharedMaterial = NinjaFx.Glow; hr.shadowCastingMode = ShadowCastingMode.Off;
            var pillar = new NinjaGlowMesh(); for (int k = 0; k < 2; k++) { var x = (k == 0 ? Vector3.right : Vector3.forward) * .7f; pillar.Card(-x + Vector3.up * .6f, -x + Vector3.up * 16, x + Vector3.up * 16, x + Vector3.up * .6f, new Color(1, .55f, .25f, .35f), 2); }
            var pg = new GameObject("Carrier beacon"); pg.transform.SetParent(root, false); pg.AddComponent<MeshFilter>().sharedMesh = pillar.Build("Carrier beacon", 2);
            beam = pg.AddComponent<MeshRenderer>(); beam.sharedMaterial = NinjaFx.Glow; beam.shadowCastingMode = ShadowCastingMode.Off;
            glowLight = new GameObject("Light - spirit lantern").AddComponent<Light>(); glowLight.transform.SetParent(root, false); glowLight.type = LightType.Point;
            glowLight.range = 11; glowLight.intensity = 2.4f; glowLight.color = new Color(1, .58f, .3f); glowLight.shadows = LightShadows.None; glowLight.renderMode = LightRenderMode.ForcePixel;
            var fx = new GameObject("Visual - shadow lantern effects").transform; fx.SetParent(transform, false);
            smoke = NinjaFx.Smoke(fx); sparks = NinjaFx.Sparks(fx);
            embers = NinjaFx.Sparks(root); var main = embers.main; main.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .7f); main.gravityModifier = -.12f; main.startLifetime = new ParticleSystem.MinMaxCurve(.8f, 1.6f);
            var e = embers.emission; e.rateOverTime = 14;
            hum = NinjaSounds.Loop(root, NinjaSounds.Hum, .22f, true, 1.5f, 14);
        }
        void LateUpdate()
        {
            var match = MatchRuntime.Current; var f = match?.Frame; var l = f?.lantern;
            bool active = f != null && f.rules.mode == GameModeKind.Ninja && l != null && l.active && (f.phase == RoundPhase.Playing || f.phase == RoundPhase.Victory);
            if (!active) { if (root) root.gameObject.SetActive(false); lastEvent = l?.eventSequence ?? -1; seen = false; wasDying.Clear(); return; }
            if (!built) Build();
            if (NinjaTempleArt.Active == null) Shader.SetGlobalVector(FogParams, new Vector4(400, 4000, 0, -1000));
            root.gameObject.SetActive(true);
            Vector3 target = l.position; bool carried = !string.IsNullOrEmpty(l.carrier);
            if (carried)
            {
                var carrier = match.FindParticipantRoot(l.carrier);
                if (carrier) target = carrier.position + Vector3.up * 2.2f - carrier.forward * .3f;
            }
            float flight = (Time.time - flightStart) / 1.1f;
            if (flight < 1 && !carried) { float k = flight * flight * (3 - 2 * flight); shown = Vector3.Lerp(flightFrom, target, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * Mathf.Min(12, Vector3.Distance(flightFrom, target) * .35f); }
            else if (!seen || (shown - target).sqrMagnitude > 64) shown = target; else shown = Vector3.Lerp(shown, target, 1 - Mathf.Exp(-Time.deltaTime * (carried ? 16 : 6)));
            seen = true; carriedBlend = Mathf.MoveTowards(carriedBlend, carried ? 1 : 0, Time.deltaTime * 3);
            pulse = Mathf.MoveTowards(pulse, 0, Time.deltaTime * 1.5f);
            float blood = NinjaTempleArt.Active ? NinjaTempleArt.Active.BloodMoon : l.gongRemaining > 0 ? 1 : 0;
            root.position = shown + Vector3.up * (Mathf.Sin(Time.time * 1.7f) * .12f * (1 - carriedBlend));
            spin.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 2.1f) * 6 * carriedBlend, Time.time * 25, Mathf.Cos(Time.time * 1.6f) * 5 * carriedBlend);
            glowLight.intensity = (2.3f + pulse * 3) * (1 + .1f * Mathf.Sin(Time.time * 9) + .05f * Mathf.Sin(Time.time * 23));
            glowLight.color = Color.Lerp(new Color(1, .58f, .3f), new Color(1, .3f, .22f), blood);
            glowLight.range = 11 + pulse * 6; beam.enabled = carriedBlend > .05f; beam.transform.localScale = new Vector3(1, carriedBlend * (1 + blood * .4f), 1);
            if (l.eventSequence != lastEvent) { if (lastEvent >= 0) Play(l, match); lastEvent = l.eventSequence; }
            foreach (var p in f.players)
            {
                wasDying.TryGetValue(p.id, out bool dying);
                if (dying && !p.dying && p.alive) { NinjaFx.Burst(smoke, p.position + Vector3.up * 1, 14); NinjaSounds.Play(NinjaSounds.Poof, p.position, .25f, 1.25f); }
                wasDying[p.id] = p.dying;
            }
        }
        void Play(LanternFrame l, IMatchRuntime match)
        {
            var at = l.eventPosition; bool local = l.eventActor == match.LocalActorId || l.eventVictim == match.LocalActorId;
            switch (l.eventKind)
            {
                case "grab": NinjaFx.Burst(sparks, at, 28); NinjaSounds.Play(NinjaSounds.Chime, at, local ? .55f : .4f); pulse = 1; break;
                case "steal": NinjaFx.Burst(smoke, at, 16); NinjaFx.Burst(sparks, at, 45); NinjaSounds.Play(NinjaSounds.Steal, at, .6f); NinjaSounds.Play(NinjaSounds.Chime, at, .3f, 1.12f); pulse = 1; break;
                case "knock": NinjaFx.Burst(smoke, at, 26); NinjaFx.Burst(sparks, at, 18); NinjaSounds.Play(NinjaSounds.Poof, at, .6f); pulse = .7f; break;
                case "drop": NinjaFx.Burst(smoke, at, 14); NinjaSounds.Play(NinjaSounds.Poof, at, .4f, .9f); break;
                case "flee": NinjaFx.Burst(smoke, at, 20); NinjaSounds.Play(NinjaSounds.Whoosh, at, .5f); flightFrom = shown; flightStart = Time.time; NinjaFx.Burst(sparks, l.position, 30); NinjaSounds.Play(NinjaSounds.Chime, l.position, .35f, .8f); pulse = 1; break;
                case "gong": var view = Services.Session?.ActiveCamera; NinjaSounds.Play(NinjaSounds.Gong, view ? view.transform.position + Vector3.up * 6 : at, .75f, 1, true); pulse = 1; break;
            }
        }
        void OnDestroy()
        {
            if (root) Destroy(root.gameObject); if (body) Destroy(body);
        }
    }
}
