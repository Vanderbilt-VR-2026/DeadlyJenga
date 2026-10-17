using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyAudio
{
    // Sound controls for menus and the lobby, offered through Core as Services.Audio.
    sealed class CandyAudioControls : IAudioControls
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => Services.Audio = new CandyAudioControls();

        static CandyAudioEngine Engine => CandyAudioEngine.Instance;

        public float MasterVolume
        {
            get { var e = Engine; return e && e.bank ? e.bank.master : 0; }
            set { var e = Engine; if (e && e.bank) e.bank.master = value; }
        }
        public float MusicVolume
        {
            get { var e = Engine; return e && e.bank ? e.bank.music : 0; }
            set { var e = Engine; if (e && e.bank) e.bank.music = value; }
        }
        public void PlayUiClick(Vector3 at) { var e = Engine; if (e) e.Play(CandySound.UiClick, at, local: true); }
        public void OpenStudio()
        {
            var e = Engine; if (!e) return;
            var studio = e.GetComponent<CandyAudioStudio>();
            if (!studio) studio = e.gameObject.AddComponent<CandyAudioStudio>();
            studio.Open();
        }
    }
}
