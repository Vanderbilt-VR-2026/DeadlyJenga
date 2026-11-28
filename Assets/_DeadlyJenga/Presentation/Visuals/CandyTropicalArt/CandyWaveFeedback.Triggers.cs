using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    // Tsunami soundscape triggers (recorded clips in CandyTsunamiSounds):
    //  drawback when the warning starts, crest roar + lip crashes while the crest is close, a slam at every splash
    //  column, spray rain near recent slams, swept blocks near the listener, and the barrel loop under the curl.
    public sealed partial class CandyWaveFeedback
    {
        readonly HashSet<IWaveState> drawbackPlayed = new HashSet<IWaveState>();
        readonly Dictionary<IWaveState, Transform> crestAnchors = new Dictionary<IWaveState, Transform>();
        Transform sprayAnchor, barrelAnchor;
        float nextLip, nextSlam, nextSwept, lastSlamTime = -10;
        Vector3 lastSlamPoint;
        int sweptCursor;

        void OnEnable() { CandyTsunamiVisuals.SplashContact += OnSplash; }

        void OnSplash(Vector3 point, float strength)
        {
            var audio = CandyAudioEngine.Instance; if (!audio || Time.time < nextSlam) return;
            nextSlam = Time.time + .35f; lastSlamTime = Time.time; lastSlamPoint = point;
            audio.Play(CandySound.WaveImpact, point, Mathf.Lerp(.55f, 1, strength), GetInstanceID());
        }

        void Drawback(IWaveState wave, CandyAudioEngine audio, IWorldQuery world)
        {
            if (drawbackPlayed.Add(wave)) audio.Play(CandySound.WaveDrawback, world.Center, 1, GetInstanceID());
        }

        Transform Anchor(ref Transform anchor, string name)
        {
            if (!anchor) { anchor = new GameObject(name).transform; anchor.SetParent(transform, false); }
            return anchor;
        }

        void Triggers(IWaveState wave, WaveFrame f, CandyAudioEngine audio, IWorldQuery world)
        {
            var listener = audio.Listener; if (!listener) return;
            Vector3 dir = Vector3.ProjectOnPlane(f.direction, Vector3.up).normalized; if (dir.sqrMagnitude < .5f) dir = Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 rel = listener.position - f.center;
            float along = Vector3.Dot(rel, dir), lateral = Vector3.Dot(rel, side), above = listener.position.y - f.waterHeight;

            // Crest roar + lip crashes: the crest sits about a third of the wave depth ahead of its centre.
            if (!crestAnchors.TryGetValue(wave, out var crest) || !crest)
            { crest = new GameObject("Audio - tsunami crest").transform; crest.SetParent(transform, false); crestAnchors[wave] = crest; }
            float across = Mathf.Clamp(lateral, -f.width * .5f, f.width * .5f);
            crest.position = new Vector3(f.center.x, f.waterHeight + f.height * .9f, f.center.z) + dir * f.depth * .34f + side * across;
            float crestNear = 1 - Mathf.Clamp01(Vector3.Distance(listener.position, crest.position) / (f.height * 3 + 30));
            if (crestNear > 0) audio.Loop(CandySound.WaveCrest, crest, .2f + .8f * crestNear);
            if (crestNear > .35f && Time.time >= nextLip)
            {
                nextLip = Time.time + Random.Range(1.2f, 2.6f);
                audio.Play(CandySound.WaveLipCrash, crest.position + side * Random.Range(-f.width * .2f, f.width * .2f), crestNear, GetInstanceID());
            }

            // Inside the barrel: under the curl (just ahead of the crest), above the water, within the wave's width.
            bool inBarrel = Mathf.Abs(lateral) < f.width * .45f && above > .3f && above < f.height * .8f && along > f.depth * .3f && along < f.depth * .58f;
            if (inBarrel) { var b = Anchor(ref barrelAnchor, "Audio - tsunami barrel"); b.position = listener.position; audio.Loop(CandySound.WaveBarrel, b, 1); }

            // Spray rain for a few seconds after a nearby slam.
            float sprayAge = Time.time - lastSlamTime, sprayDistance = Vector3.Distance(listener.position, lastSlamPoint);
            if (sprayAge < 3.5f && sprayDistance < 32)
            {
                var s = Anchor(ref sprayAnchor, "Audio - tsunami spray rain"); s.position = lastSlamPoint + Vector3.up * 3;
                audio.Loop(CandySound.WaveSprayRain, s, (1 - sprayDistance / 32) * (1 - sprayAge / 3.5f));
            }

            // Swept blocks: a block near the listener riding the wave (moving with it fast).
            if (Time.time >= nextSwept && world.Blocks != null && world.Blocks.Count > 0)
            {
                var blocks = world.Blocks; int checks = Mathf.Min(48, blocks.Count);
                for (int i = 0; i < checks; i++)
                {
                    var body = blocks[sweptCursor++ % blocks.Count]; if (!body || body.isKinematic) continue;
                    float speed = Vector3.Dot(body.linearVelocity, dir);
                    if (speed < 4 || (body.position - listener.position).sqrMagnitude > 25 * 25) continue;
                    audio.Play(CandySound.WaveSweptBlocks, body.position, Mathf.Clamp01(speed / 10), GetInstanceID());
                    nextSwept = Time.time + .5f; break;
                }
            }
        }

        void ClearTriggers()
        {
            foreach (var a in crestAnchors.Values) if (a) Destroy(a.gameObject);
            crestAnchors.Clear(); drawbackPlayed.Clear();
            if (sprayAnchor) Destroy(sprayAnchor.gameObject);
            if (barrelAnchor) Destroy(barrelAnchor.gameObject);
        }
    }
}
