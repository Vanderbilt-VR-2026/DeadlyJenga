using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    // Quiet room sounds, made in code so no audio files are needed: the fire crackling, the
    // grandfather clock ticking, wind past the windows and a faint music box playing
    // Silent Night (public domain) in the kitchen.
    public sealed partial class UnderneathTreeFinish
    {
        const int SoundRate = 22050;
        readonly List<AudioClip> clips = new List<AudioClip>();

        void BuildRoomSounds()
        {
            var fire = new Vector3(2f * FireplaceTurnX - (BreastFront + 20f), 25f, HearthZ);
            RoomSound("Fire crackle", FireCrackle(), fire, .55f, 40f, 520f);
            RoomSound("Clock tick", ClockTick(), new Vector3(RoomWest + 55f, 120f, RoomNorth - 20f), .28f, 25f, 320f);
            RoomSound("Wind at the north window", Wind(11), new Vector3((WindowWest + WindowEast) * .5f, 110f, RoomNorth), .22f, 60f, 420f);
            RoomSound("Wind at the south window", Wind(23), new Vector3((SouthWindowWest + SouthWindowEast) * .5f, 110f, RoomSouth), .22f, 60f, 420f);
            RoomSound("Music box in the kitchen", MusicBox(), new Vector3(RoomWest - 120f, 80f, (ArchSouth + ArchNorth) * .5f), .16f, 60f, 650f);
        }

        void RoomSound(string label, AudioClip clip, Vector3 position, float volume, float near, float far)
        {
            clips.Add(clip);
            var obj = new GameObject(label);
            obj.transform.SetParent(art, false);
            obj.transform.localPosition = position;
            var source = obj.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = near;
            source.maxDistance = far;
            source.dopplerLevel = 0f;
            source.time = Random.Range(0f, clip.length);
            source.Play();
        }

        static AudioClip Clip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SoundRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // A low rumble of burning wood with random pops and snaps.
        static AudioClip FireCrackle()
        {
            var rand = new System.Random(77);
            var s = new float[SoundRate * 8];
            float brown = 0f, low = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                brown = Mathf.Clamp(brown + ((float)rand.NextDouble() * 2f - 1f) * .02f, -1f, 1f);
                low += (brown - low) * .05f;
                s[i] = low * .5f;
            }
            for (int pop = 0; pop < 70; pop++)
            {
                int start = rand.Next(s.Length - 2000);
                float size = (float)rand.NextDouble();
                int length = 120 + (int)(size * 900);
                float decay = 1f;
                for (int k = 0; k < length; k++)
                {
                    s[start + k] += ((float)rand.NextDouble() * 2f - 1f) * decay * (.25f + size * .5f);
                    decay *= .992f;
                }
            }
            return Clip("Fire crackle", Seamless(s, 600));
        }

        // Tick ... tock, one per second.
        static AudioClip ClockTick()
        {
            var s = new float[SoundRate * 2];
            var rand = new System.Random(5);
            foreach (var (at, pitch) in new[] { (0, 3200f), (SoundRate, 2600f) })
                for (int k = 0; k < 500; k++)
                {
                    float t = k / (float)SoundRate, env = Mathf.Exp(-k / 70f);
                    s[at + k] += (Mathf.Sin(t * pitch * Mathf.PI * 2f) * .6f + ((float)rand.NextDouble() - .5f) * .4f) * env;
                }
            return Clip("Clock tick", s);
        }

        // Soft wind: filtered noise that swells and fades.
        static AudioClip Wind(int seed)
        {
            var rand = new System.Random(seed);
            var s = new float[SoundRate * 10];
            float a = 0f, b = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float noise = (float)rand.NextDouble() * 2f - 1f;
                a += (noise - a) * .02f;
                b += (a - b) * .03f;
                float t = i / (float)s.Length;
                float swell = .35f + .65f * Mathf.Pow(Mathf.Sin(t * Mathf.PI * 2f + seed) * .5f + .5f, 2f);
                s[i] = b * 3f * swell;
            }
            return Clip("Wind", Seamless(s, SoundRate / 2));
        }

        // Silent Night on a music box, then a pause before it plays again.
        static AudioClip MusicBox()
        {
            // (note, eighths) in C major, 6/8 time. 0 = rest.
            int G = 67, A = 69, B = 71, C = 72, D = 74, E = 76, F = 77, c = 60, d = 62, e = 64, f = 65, g = 55;
            var tune = new (int note, int length)[]
            {
                (G,3),(A,1),(G,2),(E,6), (G,3),(A,1),(G,2),(E,6), (D,4),(D,2),(B,6), (C,4),(C,2),(G,6),
                (A,4),(A,2),(C,3),(B,1),(A,2), (G,3),(A,1),(G,2),(E,6), (A,4),(A,2),(C,3),(B,1),(A,2), (G,3),(A,1),(G,2),(E,6),
                (D,4),(D,2),(F,3),(D,1),(B,2), (C,6),(E,6), (C,3),(G,1),(E,2),(G,3),(f,1),(d,2), (c,12), (0,12)
            };
            _ = g; _ = e;
            const float eighth = .34f;
            int total = 0;
            foreach (var n in tune) total += n.length;
            var s = new float[(int)(total * eighth * SoundRate) + SoundRate];
            int at = 0;
            foreach (var (note, length) in tune)
            {
                if (note > 0)
                {
                    // Two octaves up, like the comb of a music box; bright attack, long ring.
                    float freq = 440f * Mathf.Pow(2f, (note + 12 - 69) / 12f);
                    int ring = Mathf.Min(SoundRate * 2, s.Length - at);
                    for (int k = 0; k < ring; k++)
                    {
                        float t = k / (float)SoundRate;
                        float env = Mathf.Exp(-t * 3.2f);
                        s[at + k] += (Mathf.Sin(t * freq * Mathf.PI * 2f) + .3f * Mathf.Sin(t * freq * 4f * Mathf.PI)) * env * .35f;
                    }
                }
                at += (int)(length * eighth * SoundRate);
            }
            return Clip("Music box Silent Night", s);
        }

        // Fades the end into the start so the loop has no click.
        static float[] Seamless(float[] s, int blend)
        {
            for (int k = 0; k < blend; k++)
            {
                float t = k / (float)blend;
                s[k] = s[k] * t + s[s.Length - blend + k] * (1f - t);
            }
            System.Array.Resize(ref s, s.Length - blend);
            return s;
        }
    }
}
