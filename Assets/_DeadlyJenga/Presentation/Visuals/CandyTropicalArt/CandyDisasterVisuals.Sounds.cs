using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;

namespace DeadlyJenga.CandyArt
{
    // New disaster sound triggers that need what only the visuals know (fire masses, burning blocks, the tornado's
    // lightning, rain landings, the tower). Clips come from CandyDisasterSuite. Existing beds (rumble, acid rain,
    // tornado wind/pressure/debris/pass, fire crackle) stay where they were in CandyFeedback.
    public sealed partial class CandyDisasterVisuals
    {
        Transform rattleAnchor, roarAnchor, smoulderAnchor;
        float nextSoundScan, nextCrack, nextGroan, nextPop, nextAcidThunder, aftershockAt = -1, aftershock2At = -1, lastStormFlash, nextSplatSound;
        bool quakeWasActive;
        int soundBlockCursor;
        float stormFlashLevel;   // set by PrepareTornadoAt
        readonly HashSet<int> burningIds = new HashSet<int>();
        readonly List<int> stillBurning = new List<int>();

        Transform SoundAnchor(ref Transform anchor, string name)
        {
            if (!anchor) { anchor = new GameObject(name).transform; anchor.SetParent(transform, false); }
            return anchor;
        }

        void DisasterSounds()
        {
            var engine = CandyAudioEngine.Instance; var world = Services.World;
            if (!engine || !engine.Listener || world == null) return;
            Vector3 ear = engine.Listener.position;
            bool quake = false, fire = false, tornado = false, acid = false;
            foreach (var source in DisasterStates.Active)
            {
                var f = source.Capture(); if (f == null || f.warning > 0) continue;
                if (f.kind == "Earthquake") quake = true; else if (f.kind == "Fire") fire = true;
                else if (f.kind == "Tornado") tornado = true; else if (f.kind == "AcidRain") acid = true;
            }
            int id = GetInstanceID();

            // Earthquake: tower rattling loop, ground cracking and the tower groaning, aftershocks after it ends.
            if (quake)
            {
                var tower = world.Center + Vector3.up * world.TopHeight * .4f;
                engine.Loop(CandySound.EarthRattle, SoundAnchor(ref rattleAnchor, "Audio - quake rattle"), .8f);
                rattleAnchor.position = tower;
                if (Time.time >= nextCrack)
                {
                    nextCrack = Time.time + Random.Range(3f, 6f);
                    var p = ear + new Vector3(Random.Range(-12f, 12f), 4, Random.Range(-12f, 12f));
                    if (Physics.Raycast(p, Vector3.down, out var hit, 30, (1 << Layers.Surface) | (1 << Layers.Block))) p = hit.point;
                    engine.Play(CandySound.EarthCrack, p, 1, id);
                }
                if (Time.time >= nextGroan) { nextGroan = Time.time + Random.Range(4f, 8f); engine.Play(CandySound.EarthGroan, tower, 1, id); }
                aftershockAt = aftershock2At = -1;
            }
            else if (quakeWasActive) { aftershockAt = Time.time + 2.5f; aftershock2At = Time.time + 7f; }
            quakeWasActive = quake;
            if (aftershockAt > 0 && Time.time >= aftershockAt) { aftershockAt = -1; engine.Play(CandySound.EarthAftershock, world.Center + Random.insideUnitSphere * 40, 1, id); }
            if (aftershock2At > 0 && Time.time >= aftershock2At) { aftershock2At = -1; engine.Play(CandySound.EarthAftershock, world.Center + Random.insideUnitSphere * 60, .8f, id); }

            // Acid rain (all skins): toxic thunder now and then.
            if (acid && Time.time >= nextAcidThunder)
            {
                if (nextAcidThunder > 0) engine.Play(CandySound.AcidThunder, ear + new Vector3(Random.Range(-200f, 200f), 120, Random.Range(-200f, 200f)), .9f, id);
                nextAcidThunder = Time.time + Random.Range(8f, 20f);
            }
            if (!acid) nextAcidThunder = 0;

            // Tornado: thunder with each lightning flash (rising edge), blocks being ripped off nearby.
            if (tornado && stormFlashLevel > .3f && lastStormFlash <= .3f && lastTornado != null)
                engine.Play(CandySound.TornadoThunder, lastTornado.center + Vector3.up * (lastTornado.values != null && lastTornado.values.Length > 0 ? lastTornado.values[0] : 60), 1, id);
            lastStormFlash = tornado ? stormFlashLevel : 0;

            // Fire: roar at the nearest fire mass, ignitions, blistering, collapses; smoulder once the flames are gone.
            if (fire)
            {
                float best = 40 * 40; Vector3 nearest = default; bool any = false;
                foreach (var s in flameSubjects) { var c = s.bounds.center; float d = (c - ear).sqrMagnitude; if (d < best) { best = d; nearest = c; any = true; } }
                if (any) { var a = SoundAnchor(ref roarAnchor, "Audio - fire roar"); a.position = nearest; engine.Loop(CandySound.FireRoar, a, 1 - Mathf.Sqrt(best) / 40); }
            }

            if (Time.time < nextSoundScan) return;
            nextSoundScan = Time.time + .15f;
            if (fire)
            {
                stillBurning.Clear();
                foreach (var subject in flameSubjects)
                {
                    stillBurning.Add(subject.id);
                    float d = (subject.bounds.center - ear).sqrMagnitude;
                    if (burningIds.Add(subject.id) && d < 30 * 30) engine.Play(CandySound.FireIgnite, subject.bounds.center, 1, id);
                    if (subject.body && subject.body.linearVelocity.y < -4 && d < 60 * 60) engine.Play(CandySound.FireCollapse, subject.bounds.center, 1, id);
                }
                burningIds.IntersectWith(stillBurning);
                if (Time.time >= nextPop && flameSubjects.Count > 0)
                {
                    nextPop = Time.time + Random.Range(1.5f, 3f);
                    var s = flameSubjects[Random.Range(0, flameSubjects.Count)];
                    if ((s.bounds.center - ear).sqrMagnitude < 15 * 15) engine.Play(CandySound.FireSugarPop, s.bounds.center, 1, id);
                }
            }
            else
            {
                burningIds.Clear();
                float best = 20 * 20; Vector3 nearest = default; bool any = false;
                foreach (var pair in charred) if (pair.Key) { float d = (pair.Key.bounds.center - ear).sqrMagnitude; if (d < best) { best = d; nearest = pair.Key.bounds.center; any = true; } }
                if (any) { var a = SoundAnchor(ref smoulderAnchor, "Audio - smoulder"); a.position = nearest; engine.Loop(CandySound.FireSmoulder, a, 1 - Mathf.Sqrt(best) / 20); }
            }
            if (tornado && world.Blocks != null && world.Blocks.Count > 0)
            {
                var blocks = world.Blocks; int checks = Mathf.Min(48, blocks.Count);
                for (int i = 0; i < checks; i++)
                {
                    var body = blocks[soundBlockCursor++ % blocks.Count];
                    if (!body || body.isKinematic || body.linearVelocity.y < 6 || (body.position - ear).sqrMagnitude > 25 * 25) continue;
                    engine.Play(CandySound.TornadoBlockRip, body.position, 1, id); break;
                }
            }
        }

        // Called by RainLandings for each drop that lands: a splat sound near the listener (all rain skins).
        void RainSplatSound(Vector3 point)
        {
            var engine = CandyAudioEngine.Instance;
            if (!engine || !engine.Listener || Time.time < nextSplatSound || (point - engine.Listener.position).sqrMagnitude > 14 * 14) return;
            nextSplatSound = Time.time + .12f;
            engine.Play(CandySound.AcidSplat, point, 1, GetInstanceID());
        }
    }
}
