using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Desktop menu section and subtitles for AI players' voices, through ISocialControls (Core).
    // The social simulation in GameSession draws nothing itself.
    public static class SocialPanel
    {
        static Vector2 scroll;
        static string selected = "";

        public static void Draw(ISocialControls social)
        {
            if (social == null) { GUILayout.Label("AI players are getting ready..."); return; }
            GUILayout.Label("AI players · social & voice"); GUILayout.Label(social.IsHost ? "Host simulation" : "Joined host simulation");
            social.VoiceEnabled = GUILayout.Toggle(social.VoiceEnabled, "Enable bot voices");
            GUILayout.Label("Bot volume"); social.BotVolume = GUILayout.HorizontalSlider(social.BotVolume, 0, 1);
            GUILayout.Label("Human voice volume"); social.HumanVolume = GUILayout.HorizontalSlider(social.HumanVolume, 0, 1);
            if (GUILayout.Button(social.MicrophoneOn ? "Mute microphone" : "Enable microphone (permission required)")) social.SetMicrophone(!social.MicrophoneOn);
            var devices = Microphone.devices;
            if (!social.MicrophoneOn && devices.Length > 1)
            {
                int current = System.Array.IndexOf(devices, social.MicrophoneDevice);
                int chosen = GUILayout.SelectionGrid(Mathf.Max(0, current), devices, 1);
                social.MicrophoneDevice = devices[chosen];
            }
            GUILayout.Label(social.MicrophoneStatus); GUILayout.Label(social.SpeechStatus);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(210));
            foreach (var id in social.AiPlayerIds)
                if (GUILayout.Button(social.AiPlayerName(id) + " [AI]")) selected = id;
            GUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(selected))
            {
                bool muted = social.IsMuted(selected);
                bool value = GUILayout.Toggle(muted, "Mute selected AI player");
                if (value != muted) social.SetMuted(selected, value);
                GUILayout.Label("Selected AI volume");
                float volume = GUILayout.HorizontalSlider(social.Volume(selected), 0, 1);
                if (!Mathf.Approximately(volume, social.Volume(selected))) social.SetVolume(selected, volume);
            }
            GUILayout.Label(social.Caption);
        }

        // Subtitles at the bottom of the screen while playing.
        public static void DrawCaption(ISocialControls social)
        {
            GUI.Label(new Rect(20, Screen.height - 62, Mathf.Max(200, Screen.width - 40), 50), social.Caption);
        }
    }
}
