using UnityEngine;

namespace DeadlyJenga.World.Hazards
{
    // The breaking-wave shape, exported from Blender into Resources/TsunamiProfile.json.
    // All values are normalised: along = metres / wave depth (-0.5 back .. +0.5 front),
    // lateral = metres / wave width (-0.5 .. +0.5), height = fraction of the wave height (max 1).
    // Gameplay uses the envelope (highest water at each point, no overhang). Presentation reads the
    // same file for the full curled cross-section, so physics and visuals always agree.
    // If the file is missing the hazard falls back to the original analytic hump.
    public static class TsunamiProfile
    {
        [System.Serializable]
        sealed class Data
        {
            public int version, lateralSamples, pathSamples, alongSamples;
            public float crestAlong;
            public float[] path, envelope;
        }

        static Data data;
        static bool loaded;

        public static bool Available { get { Load(); return data != null; } }

        // Where the crest sits along the wave (fraction of depth from the centre).
        public static float CrestAlong { get { Load(); return data != null ? data.crestAlong : .22f; } }

        // Height fraction (0..1) of the water surface at this normalised point.
        public static float Envelope(float along, float lateral)
        {
            Load();
            if (data == null) return 0;
            float a = Mathf.Clamp01(along + .5f) * (data.alongSamples - 1);
            float l = Mathf.Clamp01(lateral + .5f) * (data.lateralSamples - 1);
            int a0 = Mathf.Min((int)a, data.alongSamples - 2), l0 = Mathf.Min((int)l, data.lateralSamples - 2);
            float fa = a - a0, fl = l - l0;
            float h00 = Sample(l0, a0), h10 = Sample(l0, a0 + 1), h01 = Sample(l0 + 1, a0), h11 = Sample(l0 + 1, a0 + 1);
            return Mathf.Lerp(Mathf.Lerp(h00, h10, fa), Mathf.Lerp(h01, h11, fa), fl);
        }

        static float Sample(int lateral, int along) => data.envelope[lateral * data.alongSamples + along];

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            var asset = Resources.Load<TextAsset>("TsunamiProfile");
            if (!asset) return;
            var parsed = JsonUtility.FromJson<Data>(asset.text);
            bool valid = parsed != null && parsed.lateralSamples >= 2 && parsed.alongSamples >= 2 && parsed.pathSamples >= 2 &&
                parsed.envelope != null && parsed.envelope.Length == parsed.lateralSamples * parsed.alongSamples &&
                parsed.path != null && parsed.path.Length == parsed.lateralSamples * parsed.pathSamples * 2;
            if (valid) data = parsed;
            else Debug.LogError("TsunamiProfile.json is malformed; using the analytic wave shape.");
        }
    }
}
