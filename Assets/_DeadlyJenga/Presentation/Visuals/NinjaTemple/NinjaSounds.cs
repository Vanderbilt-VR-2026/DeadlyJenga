using UnityEngine;

namespace DeadlyJenga.NinjaArt
{
    // Procedural sound design: no imported clips. Generated once, shared, cheap mono 22 kHz.
    public static class NinjaSounds
    {
        const int Rate = 22050;
        static AudioClip rain, wind, water, gong, poof, chime, steal, whoosh, hum, taiko, clack;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { rain = wind = water = gong = poof = chime = steal = whoosh = hum = taiko = clack = null; }
        delegate float Sample(float t, System.Random rng);
        static AudioClip Make(string name, float seconds, Sample f, bool loop)
        {
            int n = Mathf.CeilToInt(seconds * Rate); var data = new float[n]; var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++) data[i] = f(i / (float)Rate, rng);
            if (loop) { int fade = Rate / 4; for (int i = 0; i < fade; i++) { float k = i / (float)fade; data[i] = data[i] * k + data[n - fade + i] * (1 - k); } System.Array.Resize(ref data, n - fade); }
            float peak = .0001f; foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < data.Length; i++) data[i] = data[i] / peak * .9f;
            var clip = AudioClip.Create("Ninja " + name, data.Length, 1, Rate, false); clip.SetData(data, 0); return clip;
        }
        sealed class Filter { public float a, b; public float Low(float x, float k) { a += (x - a) * k; return a; } public float Low2(float x, float k) { a += (x - a) * k; b += (a - b) * k; return b; } }
        static float Noise(System.Random r) => (float)r.NextDouble() * 2 - 1;

        public static AudioClip Rain => rain ? rain : rain = RainClip();
        static AudioClip RainClip()
        {
            var lo = new Filter(); var hi = new Filter(); float drop = 0, dropF = 0;
            return Make("rain", 8, (t, r) =>
            {
                float n = Noise(r); float body = n - hi.Low(n, .25f); float bed = lo.Low2(body, .5f) * .55f;
                if (r.NextDouble() < .0035) { drop = .8f + (float)r.NextDouble() * .6f; dropF = 1800 + (float)r.NextDouble() * 2600; }
                drop *= .9965f; float tick = Mathf.Sin(t * dropF * Mathf.PI * 2) * drop * drop * .35f;
                return bed * (.85f + .15f * Mathf.Sin(t * .9f)) + tick;
            }, true);
        }
        public static AudioClip Wind => wind ? wind : wind = WindClip();
        static AudioClip WindClip()
        {
            var lo = new Filter(); var band = new Filter(); float knock = 0, knockF = 0;
            return Make("bamboo wind", 12, (t, r) =>
            {
                float n = Noise(r); float gust = .45f + .35f * Mathf.Sin(t * .41f) + .2f * Mathf.Sin(t * 1.13f + 1);
                float w = lo.Low2(n, .02f + gust * .03f) * 3f * gust; float rustle = (n - band.Low(n, .3f)) * .05f * gust * gust;
                if (r.NextDouble() < .00012) { knock = 1; knockF = 380 + (float)r.NextDouble() * 260; }
                knock *= .9988f; float clack = Mathf.Sin(t * knockF * Mathf.PI * 2) * knock * knock * knock * .5f;
                return w + rustle + clack;
            }, true);
        }
        public static AudioClip Water => water ? water : water = Make("lake", 9, (t, r) =>
        {
            float n = Noise(r); float swell = .5f + .5f * Mathf.Sin(t * .7f) * Mathf.Sin(t * .23f + 2);
            return n * .06f * swell + Mathf.Sin(t * 50 + Mathf.Sin(t * 3) * 4) * .02f * swell;
        }, true);
        public static AudioClip Gong => gong ? gong : gong = Make("gong", 6, (t, r) =>
        {
            float[] f = { 92, 139, 187, 251, 318, 427, 561 }; float[] d = { .55f, .7f, .9f, 1.3f, 1.7f, 2.4f, 3.2f }; float s = 0;
            for (int i = 0; i < f.Length; i++) s += Mathf.Sin(t * f[i] * Mathf.PI * 2 * (1 + .004f * Mathf.Sin(t * (2 + i)))) * Mathf.Exp(-t * d[i]) / (1 + i * .45f);
            float strike = Noise(r) * Mathf.Exp(-t * 40) * .5f; float swell = Mathf.Min(1, t * 30);
            return (s * swell + strike) * (1 + .3f * Mathf.Sin(t * 5.3f) * Mathf.Exp(-t * .5f));
        }, false);
        public static AudioClip Poof => poof ? poof : poof = PoofClip();
        static AudioClip PoofClip()
        {
            var lo = new Filter();
            return Make("smoke bomb", 1.1f, (t, r) =>
            {
                float n = Noise(r); float env = Mathf.Exp(-t * 5) * Mathf.Min(1, t * 120); float crack = Noise(r) * Mathf.Exp(-t * 90) * .9f;
                return lo.Low2(n, .08f + .4f * Mathf.Exp(-t * 6)) * env * 2.4f + crack + Mathf.Sin(t * 70 * Mathf.PI * 2) * Mathf.Exp(-t * 14) * .35f;
            }, false);
        }
        public static AudioClip Chime => chime ? chime : chime = Make("lantern chime", 2.2f, (t, r) =>
        {
            float a = Mathf.Sin(t * 1318.5f * Mathf.PI * 2) * Mathf.Exp(-t * 2.4f) + Mathf.Sin(t * 1975.5f * Mathf.PI * 2) * Mathf.Exp(-t * 3.4f) * .5f;
            float b = t > .12f ? Mathf.Sin((t - .12f) * 1760 * Mathf.PI * 2) * Mathf.Exp(-(t - .12f) * 2.2f) * .8f + Mathf.Sin((t - .12f) * 5280 * Mathf.PI * 2) * Mathf.Exp(-(t - .12f) * 8) * .15f : 0;
            return (a + b) * Mathf.Min(1, t * 400);
        }, false);
        public static AudioClip Steal => steal ? steal : steal = Make("koto steal", 1.4f, (t, r) =>
        {
            float s = 0; float[] notes = { 659.3f, 493.9f, 392 }; for (int k = 0; k < 3; k++) { float t0 = t - k * .09f; if (t0 <= 0) continue; float f = notes[k];
                s += (Mathf.Sin(t0 * f * Mathf.PI * 2) + .45f * Mathf.Sin(t0 * f * 2 * Mathf.PI * 2) + .2f * Mathf.Sin(t0 * f * 3 * Mathf.PI * 2)) * Mathf.Exp(-t0 * 4.5f) * Mathf.Min(1, t0 * 600); }
            return s + Noise(r) * Mathf.Exp(-t * 60) * .3f;
        }, false);
        public static AudioClip Whoosh => whoosh ? whoosh : whoosh = WhooshClip();
        static AudioClip WhooshClip()
        {
            var lo = new Filter();
            return Make("spirit whoosh", 1.3f, (t, r) => { float env = Mathf.Sin(Mathf.Clamp01(t / 1.3f) * Mathf.PI); return lo.Low2(Noise(r), .03f + .25f * env) * env * env * 3; }, false);
        }
        public static AudioClip Hum => hum ? hum : hum = Make("lantern hum", 4, (t, r) =>
        {
            float flick = 1 + .15f * Mathf.Sin(t * 7.1f) + .1f * Mathf.Sin(t * 13.3f);
            return (Mathf.Sin(t * 220 * Mathf.PI * 2) * .4f + Mathf.Sin(t * 330 * Mathf.PI * 2) * .25f + Mathf.Sin(t * 440.7f * Mathf.PI * 2) * .12f + Noise(r) * .05f) * flick;
        }, true);
        public static AudioClip Taiko => taiko ? taiko : taiko = Make("taiko", 2, (t, r) =>
        {
            float s = 0; float[] hits = { 0, .5f, .75f, 1.25f }; float[] amp = { 1, .55f, .45f, .8f };
            for (int k = 0; k < hits.Length; k++) { float t0 = t - hits[k]; if (t0 < 0) continue; float f = 62 + 50 * Mathf.Exp(-t0 * 18); s += (Mathf.Sin(t0 * f * Mathf.PI * 2) * Mathf.Exp(-t0 * 7) + Noise(r) * Mathf.Exp(-t0 * 50) * .25f) * amp[k]; }
            return s;
        }, true);
        public static AudioClip Clack => clack ? clack : clack = Make("hyoshigi", .5f, (t, r) =>
            (Mathf.Sin(t * 2350 * Mathf.PI * 2) + Mathf.Sin(t * 3520 * Mathf.PI * 2) * .6f) * Mathf.Exp(-t * 28) + Noise(r) * Mathf.Exp(-t * 120) * .4f, false);

        static float Master
        {
            get { var engine = CandyAudio.CandyAudioEngine.Instance; return engine && engine.bank ? engine.bank.master : .8f; }
        }
        public static AudioSource Loop(Transform parent, AudioClip clip, float volume, bool spatial, float near = 3, float far = 40)
        {
            var go = new GameObject("Audio - " + clip.name); go.transform.SetParent(parent, false);
            var s = go.AddComponent<AudioSource>(); s.clip = clip; s.loop = true; s.playOnAwake = false; s.volume = volume * Master; s.dopplerLevel = 0;
            s.spatialBlend = spatial ? 1 : 0; s.minDistance = near; s.maxDistance = far; s.rolloffMode = AudioRolloffMode.Linear; s.time = Random.Range(0, clip.length * .9f); s.Play(); return s;
        }
        public static void Play(AudioClip clip, Vector3 at, float volume, float pitch = 1, bool global = false)
        {
            var engine = CandyAudio.CandyAudioEngine.Instance;
            if (engine && engine.bank)
            {
                var cue = new CandyAudio.CandyAudioCue { sound = (CandyAudio.CandySound)(900 + (clip.GetInstanceID() & 63)), variants = new[] { clip }, bus = CandyAudio.CandyBus.World, volume = volume, pitch = new Vector2(pitch * .97f, pitch * 1.03f), cooldown = .05f, maxVoices = 3, priority = 40, minDistance = global ? 40 : 4, maxDistance = global ? 600 : 70 };
                if (engine.PlayCue(cue, at, 1, 0, 1, global)) return;
            }
            AudioSource.PlayClipAtPoint(clip, at, volume * Master);
        }
    }
}
