using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyAudio
{
    // Each giant-hand design sounds different: a swoosh when it moves fast and a signature
    // sound every few seconds while it is out. Reads Core's Tracked.HandLooks only.
    public sealed class HandLookAudio : MonoBehaviour
    {
        public float swooshSpeed = 14;     // m/s
        public float swooshGap = .7f;
        public float signatureGap = 4;

        // Per design: swoosh sound + pitch, signature sound + pitch. Index 0 (Classic) keeps the stock sounds.
        static readonly (CandySound sound, float pitch)[] swoosh =
        {
            (CandySound.HandFlight, 1), (CandySound.RubberTouch, .55f), (CandySound.BlockTap, 1.7f), (CandySound.BlockSmash, .5f),
            (CandySound.BlockSplash, 1.6f), (CandySound.RubberSlide, 1.3f), (CandySound.PlayerSplash, .7f), (CandySound.FallWind, 1.4f), (CandySound.UiClick, .6f),
        };
        static readonly (CandySound sound, float pitch)[] signature =
        {
            (CandySound.HandFlight, 1), (CandySound.GripWarning, .6f), (CandySound.BlockTap, 2.2f), (CandySound.StructureLean, .55f),
            (CandySound.BlockSplash, 2f), (CandySound.RubberTouch, 1.6f), (CandySound.BlockScrape, .6f), (CandySound.Respawn, .6f), (CandySound.Spawn, .8f),
        };

        sealed class State { public float nextSwoosh, nextSignature; }
        readonly Dictionary<IHandLook, State> states = new Dictionary<IHandLook, State>();
        static HandLookAudio instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance) return;
            var go = new GameObject("Hand look audio");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<HandLookAudio>();
        }

        void Update()
        {
            var engine = CandyAudioEngine.Instance;
            if (!engine) return;
            foreach (var hand in Tracked.HandLooks)
            {
                if (hand == null || !hand.Visible || hand.Look <= 0 || hand.Look >= swoosh.Length) continue;
                if (!states.TryGetValue(hand, out var s)) states[hand] = s = new State { nextSignature = Time.time + Random.Range(1f, signatureGap) };
                float speed = hand.Velocity.magnitude;
                if (speed > swooshSpeed && Time.time >= s.nextSwoosh)
                {
                    var (sound, pitch) = swoosh[hand.Look];
                    engine.Play(sound, hand.Position, Mathf.Clamp01(speed / (swooshSpeed * 2)), GetHashCode() ^ hand.GetHashCode(), pitch);
                    s.nextSwoosh = Time.time + swooshGap;
                }
                if (Time.time >= s.nextSignature)
                {
                    var (sound, pitch) = signature[hand.Look];
                    engine.Play(sound, hand.Position, .45f, GetHashCode() ^ hand.GetHashCode() ^ 7, pitch);
                    s.nextSignature = Time.time + signatureGap * Random.Range(.7f, 1.3f);
                }
            }
            if (states.Count > 64) states.Clear();
        }
    }
}
