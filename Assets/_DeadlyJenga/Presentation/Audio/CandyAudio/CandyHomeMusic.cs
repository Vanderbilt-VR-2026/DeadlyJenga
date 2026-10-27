using System.Collections;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    public sealed class CandyHomeMusic : MonoBehaviour
    {
        public static bool Active { get; private set; }
        AudioSource source;
        bool focused = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => GameEvents.HomeStarted += Attach;

        static void Attach(GameObject root)
        {
            if (root && !root.GetComponent<CandyHomeMusic>()) root.AddComponent<CandyHomeMusic>();
        }

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            source.priority = 16;
            source.clip = Resources.Load<AudioClip>("CandyMenuMusic/Shorebreak-Home");
        }

        void OnEnable()
        {
            Active = true;
            StartCoroutine(Play());
        }

        IEnumerator Play()
        {
            if (!source.clip) { Debug.LogWarning("Home music clip is missing."); yield break; }
            source.clip.LoadAudioData();
            while (source.clip.loadState == AudioDataLoadState.Loading) yield return null;
            if (source.clip.loadState == AudioDataLoadState.Failed) yield break;
            SetVolume();
            source.Play();
        }

        void SetVolume()
        {
            var audio = CandyAudioEngine.Instance;
            source.volume = focused ? (audio && audio.bank ? Mathf.Clamp01(audio.bank.master * audio.bank.music * 1.8f) : .32f) : 0;
        }

        void Update() => SetVolume();
        void OnApplicationFocus(bool value) { focused = value; SetVolume(); }
        void OnDisable()
        {
            Active = false;
            StopAllCoroutines();
            if (source) source.Stop();
        }
        void OnDestroy()
        {
            if (source) { source.Stop(); source.clip = null; }
        }
    }
}
