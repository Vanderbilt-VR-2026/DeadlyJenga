using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    public sealed class MarinaGullAmbience : MonoBehaviour
    {
        static readonly Vector3[] Calls = { new(-19, 7, -18), new(21, 8, -9), new(-13, 6, 27), new(16, 10, 23) };
        ISessionInfo session;
        AudioSource source;
        float nextCall;
        int call;
        bool focused = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => GameEvents.LobbyStarted += Attach;
        static void Attach(GameObject owner)
        {
            if (owner && owner.GetComponent<IWorldQuery>()?.Radius > 30 && !owner.GetComponent<MarinaGullAmbience>())
                owner.AddComponent<MarinaGullAmbience>();
        }

        void Awake()
        {
            session = GetComponent<ISessionInfo>();
            var clip = CandyDirectAudio.Clip(CandyDirectAudio.Gulls, Resources.Load<AudioClip>("CandyAudio/MarinaGullCalls"));
            if (!clip) { enabled = false; return; }
            var emitter = new GameObject("Marina gull calls");
            emitter.transform.SetParent(transform, false);
            source = emitter.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.spatialBlend = 1;
            source.dopplerLevel = 0;
            source.minDistance = 9;
            source.maxDistance = 52;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.priority = 140;
            nextCall = Time.unscaledTime + 5;
        }

        void Update()
        {
            if (!focused || !ReferenceEquals(Services.Session, session) ||
                !NetworkActors.Alive(session?.Player) || !session.Player.gameObject.activeInHierarchy)
            {
                if (source.isPlaying) source.Stop();
                return;
            }
            var audio = CandyAudioEngine.Instance;
            source.volume = CandyDirectAudio.Gain(CandyDirectAudio.Gulls) * (audio && audio.bank ? audio.bank.master * audio.bank.ambience * .55f : .14f);
            if (Time.unscaledTime < nextCall || source.isPlaying || source.volume <= .001f) return;
            source.transform.localPosition = Calls[call % Calls.Length];
            source.pitch = 1 + Mathf.Sin(call * 2.399963f) * .075f;
            source.Play();
            nextCall = Time.unscaledTime + 13 + (call % 4) * 2.8f;
            call++;
        }

        void OnApplicationFocus(bool value) => focused = value;
        void OnDisable() { if (source) source.Stop(); }
    }
}
