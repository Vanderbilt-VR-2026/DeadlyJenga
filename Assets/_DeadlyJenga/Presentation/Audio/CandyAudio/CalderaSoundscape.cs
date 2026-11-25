using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    // Twin Caldera's volcanic soundscape: a molten lava bed everywhere, bubbling lava along
    // the shores, and distant volcanoes that erupt, rumble and thunder on their own timers.
    // Presentation only: it reads positions and plays sounds, nothing else.
    public sealed class CalderaSoundscape : MonoBehaviour
    {
        public static bool Active { get; private set; }

        // Where lava meets the two cones (metres from the map centre, at lava height).
        static readonly Vector3[] Shores =
        {
            new(-45, 0, 4), new(-28, 0, -29), new(0, 0, -13), new(0, 0, 15),
            new(23, 0, -22), new(43, 0, 8), new(24, 0, 31), new(-24, 0, 32),
        };
        // Smoking background volcanoes: compass bearing (degrees) and distance (metres).
        static readonly Vector2[] Volcanoes = { new(6, 268), new(-37, 202), new(88, 222), new(-148, 239), new(46, 356), new(-90, 381) };

        AudioClip bed, near;
        AudioClip[] eruptions, farEruptions, tremors, thunders;
        AudioSource bedSource, rumble, eruptionSource, thunderSource;
        AudioSource[] shoreSources;
        float nextEruption, nextThunder, tremorAt = -1;
        bool focused = true;

        static AudioClip Load(string name) => Resources.Load<AudioClip>("CandyAudio/Caldera/" + name);
        static AudioClip[] LoadSet(string name, int count)
        {
            var set = new AudioClip[count];
            for (int i = 0; i < count; i++) set[i] = Load(name + (i + 1));
            return set;
        }

        AudioSource Source(string label, Vector3 local, bool spatial, float min, float max, AudioRolloffMode rolloff)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false; s.dopplerLevel = 0;
            s.spatialBlend = spatial ? 1 : 0;
            s.minDistance = min; s.maxDistance = max; s.rolloffMode = rolloff;
            s.priority = spatial ? 150 : 60;
            return s;
        }

        void Awake()
        {
            bed = Load("CalderaLavaBed"); near = Load("CalderaLavaNear");
            eruptions = LoadSet("CalderaEruption", 3); farEruptions = LoadSet("CalderaEruptionFar", 3);
            tremors = LoadSet("CalderaTremor", 2); thunders = LoadSet("CalderaThunder", 2);
            if (!bed) { enabled = false; return; }

            bedSource = Source("Lava bed (everywhere)", Vector3.zero, false, 1, 2, AudioRolloffMode.Linear);
            bedSource.clip = bed; bedSource.loop = true;
            shoreSources = new AudioSource[Shores.Length];
            for (int i = 0; i < Shores.Length; i++)
            {
                var s = Source("Shore lava " + i, Shores[i], true, 4, 30, AudioRolloffMode.Linear);
                s.clip = near; s.loop = true; s.spread = 90;
                s.pitch = 1 + Mathf.Sin(i * 2.399963f) * .08f;
                shoreSources[i] = s;
            }
            eruptionSource = Source("Volcano eruption", Vector3.zero, true, 120, 1400, AudioRolloffMode.Logarithmic);
            eruptionSource.spread = 70; eruptionSource.priority = 40;
            thunderSource = Source("Volcanic thunder", Vector3.zero, true, 120, 1400, AudioRolloffMode.Logarithmic);
            thunderSource.spread = 120;
            rumble = Source("Ground tremor", Vector3.zero, false, 1, 2, AudioRolloffMode.Linear);
            nextEruption = Time.unscaledTime + Random.Range(8f, 16f);
            nextThunder = Time.unscaledTime + Random.Range(20f, 35f);
        }

        void OnEnable() { if (bed) Active = true; }

        void OnDisable()
        {
            Active = false;
            foreach (var s in GetComponentsInChildren<AudioSource>()) s.Stop();
        }

        void OnApplicationFocus(bool value) => focused = value;

        static Vector3 VolcanoPosition(Vector2 v) =>
            Quaternion.Euler(0, v.x, 0) * Vector3.forward * v.y + Vector3.up * 40;

        void Update()
        {
            var engine = CandyAudioEngine.Instance;
            float gain = engine && engine.bank ? engine.bank.master * engine.bank.ambience : .3f;
            if (!focused) gain = 0;
            var listener = engine && engine.Listener ? engine.Listener.position : transform.position;
            float height = Mathf.Max(0, listener.y - transform.position.y);

            // The bed is loudest down by the lava and thins out as players climb.
            bedSource.volume = gain * Mathf.Lerp(.9f, .45f, Mathf.Clamp01(height / 40));
            if (!bedSource.isPlaying && gain > 0) bedSource.Play();
            foreach (var s in shoreSources)
            {
                s.volume = gain * .85f;
                if (!s.isPlaying && gain > 0 && near) { s.time = Random.Range(0, near.length); s.Play(); }
            }

            float now = Time.unscaledTime;
            if (now >= nextEruption)
            {
                // Sound travels about 343 m/s, so a distant burst arrives a little late.
                var v = Volcanoes[Random.Range(0, Volcanoes.Length)];
                var at = transform.position + VolcanoPosition(v);
                bool far = v.y > 300 || Random.value < .4f;
                var set = far ? farEruptions : eruptions;
                var clip = set[Random.Range(0, set.Length)];
                if (clip)
                {
                    eruptionSource.transform.position = at;
                    eruptionSource.clip = clip;
                    eruptionSource.volume = gain * (far ? .9f : 1f);
                    eruptionSource.pitch = Random.Range(.9f, 1.05f);
                    eruptionSource.PlayDelayed(Vector3.Distance(listener, at) / 343f);
                    if (Random.value < .6f) tremorAt = now + Vector3.Distance(listener, at) / 343f + .3f;
                }
                nextEruption = now + Random.Range(22f, 55f);
            }
            if (tremorAt > 0 && now >= tremorAt)
            {
                var clip = tremors[Random.Range(0, tremors.Length)];
                if (clip) { rumble.clip = clip; rumble.volume = gain * .8f; rumble.Play(); }
                tremorAt = -1;
            }
            if (now >= nextThunder)
            {
                var clip = thunders[Random.Range(0, thunders.Length)];
                var at = transform.position + VolcanoPosition(Volcanoes[Random.Range(0, Volcanoes.Length)]) + Vector3.up * 150;
                if (clip)
                {
                    thunderSource.transform.position = at;
                    thunderSource.clip = clip; thunderSource.volume = gain * .7f;
                    thunderSource.PlayDelayed(Vector3.Distance(listener, at) / 343f);
                }
                nextThunder = now + Random.Range(35f, 80f);
            }
        }
    }
}
