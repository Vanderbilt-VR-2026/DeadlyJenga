using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    // Sound controls for Presentation's own menus (the lobby's audio page).
    public static class CandyAudioControls
    {
        static CandyAudioEngine Engine => CandyAudioEngine.Instance;

        public static float MasterVolume
        {
            get { var e = Engine; return e && e.bank ? e.bank.master : 0; }
            set { var e = Engine; if (e && e.bank) e.bank.master = value; }
        }
        public static float MusicVolume
        {
            get { var e = Engine; return e && e.bank ? e.bank.music : 0; }
            set { var e = Engine; if (e && e.bank) e.bank.music = value; }
        }
        public static void PlayUiClick(Vector3 at) { var e = Engine; if (e) e.Play(CandySound.UiClick, at, local: true); }
        public static void OpenStudio()
        {
            var e = Engine; if (!e) return;
            var studio = e.GetComponent<CandyAudioStudio>();
            if (!studio) studio = e.gameObject.AddComponent<CandyAudioStudio>();
            studio.Open();
        }
    }
}
