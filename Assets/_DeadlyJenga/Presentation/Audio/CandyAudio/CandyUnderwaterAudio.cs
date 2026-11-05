using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    static class CandyUnderwaterAudio
    {
        public static readonly CandySound[] Sounds = { CandySound.WaveSubmerged, CandySound.WaveSurfaceBreath };
        static readonly AudioClip[] Clips = new AudioClip[2];

        public static CandyAudioCue DefaultCue(CandySound sound)
        {
            bool loop = sound == CandySound.WaveSubmerged;
            int index = loop ? 0 : 1;
            if (!Clips[index]) Clips[index] = Make(loop);
            return new CandyAudioCue
            {
                sound = sound, bus = CandyBus.Character, variants = new[] { Clips[index] },
                loop = loop, volume = loop ? .54f : .48f, priority = 39, maxVoices = 1,
                pitch = new Vector2(.96f, 1.04f), cooldown = .65f
            };
        }

        static AudioClip Make(bool loop)
        {
            const int rate = 22050;
            var samples = new float[Mathf.RoundToInt(rate * (loop ? 4 : 1.2f))];
            var random = new System.Random(loop ? 2611 : 3703);
            float low = 0, mid = 0, rumble = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float noise = (float)(random.NextDouble() * 2 - 1);
                rumble += .008f * (noise - rumble);
                low += .055f * (noise - low);
                mid += .32f * (noise - mid);
                if (loop)
                {
                    float current = .67f + .25f * Mathf.Sin(Mathf.PI * t);
                    float burst = Pulse(t, .32f, .82f) + Pulse(t, 1.56f, 2.16f) + Pulse(t, 2.83f, 3.56f);
                    float gurgle = Mathf.Sin(2 * Mathf.PI * (91 * t + 2 * Mathf.Sin(t * 17)));
                    float seam = Mathf.Min(1, Mathf.Min(t, 4 - t) * 32);
                    samples[i] = ((rumble * 4.5f + low * 1.4f) * current +
                        (gurgle * .16f + (mid - low) * .5f) * burst) * seam;
                }
                else
                {
                    float inhale = Pulse(t, .035f, .45f);
                    float exhale = Pulse(t, .52f, 1.13f);
                    float flutter = .78f + .22f * Mathf.Sin(t * 112);
                    samples[i] = ((mid - low) * 1.25f + low * .55f) * (inhale + exhale * .56f) * flutter;
                }
            }
            var clip = AudioClip.Create(loop ? "Wave pressure and trapped breath" : "Wave surface gasp", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static float Pulse(float time, float start, float end)
        {
            float progress = Mathf.Clamp01((time - start) / (end - start));
            return Mathf.Sin(Mathf.PI * progress) * Mathf.Sin(Mathf.PI * progress);
        }
    }
}
