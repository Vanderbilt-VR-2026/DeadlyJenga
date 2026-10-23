using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Desktop menu section and subtitles for AI players' voices, through ISocialControls (Core).
    // The social simulation in GameSession draws nothing itself.
    public static class SocialPanel
    {
        static Vector2 scroll;
        static GUIStyle captionStyle;
        static string selected = "";
        static string test="Can you help me find a safe way up?";

        public static void Draw(ISocialControls social)
        {
            if (social == null) { GUILayout.Label("AI players are getting ready..."); return; }
            GUILayout.Label("AI players · social & voice"); GUILayout.Label(social.IsHost ? "Host simulation" : "Joined host simulation");
            var native=social as IOnDeviceVoiceControls;
            if(native!=null)
            {
                if(GUILayout.Button(native.Requested?"On-device conversation ON · turn off":"Enable on-device conversation"))native.SetEnabled(!native.Requested);
                GUILayout.Label(native.Status);
                GUILayout.Label("Microphone: "+(social.MicrophoneOn?"LISTENING":"OFF"));
                if(!string.IsNullOrEmpty(native.LastHeard))GUILayout.Label("Heard: "+native.LastHeard);
                GUILayout.Label(native.LastReplyKind+" · generated "+native.GeneratedReplies+" · fallback "+native.FallbackReplies);
                test=GUILayout.TextField(test??"");bool old=GUI.enabled;GUI.enabled=native.Ready;
                if(GUILayout.Button("Send text test · microphone not required"))native.SubmitText(test);GUI.enabled=old;
            }
            social.VoiceEnabled = GUILayout.Toggle(social.VoiceEnabled, "Enable bot voices");
            GUILayout.Label("Bot volume"); social.BotVolume = GUILayout.HorizontalSlider(social.BotVolume, 0, 1);
            GUILayout.Label("Human voice volume"); social.HumanVolume = GUILayout.HorizontalSlider(social.HumanVolume, 0, 1);
            if (GUILayout.Button(social.MicrophoneOn ? "Mute microphone" : "Enable microphone (permission required)")) social.SetMicrophone(!social.MicrophoneOn);
            var devices = Microphone.devices;
            if (!social.MicrophoneOn && devices.Length > 1)
            {
                int current = System.Array.IndexOf(devices, social.MicrophoneDevice);
                if(GUILayout.Button("Microphone: "+devices[Mathf.Max(0,current)]+" · next"))social.MicrophoneDevice=devices[(current+1)%devices.Length];
            }
            GUILayout.Label(social.MicrophoneStatus);if(native==null||!native.Requested)GUILayout.Label("External speech service: "+social.SpeechStatus);
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
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
            captionStyle??=new GUIStyle(GUI.skin.box){wordWrap=true,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white,background=CandyHudVisuals.Rounded}};
            captionStyle.fontSize=Mathf.Max(10,Mathf.RoundToInt(14*scale));
            var old=GUI.backgroundColor;GUI.backgroundColor=new Color(.08f,.025f,.14f,.86f);
            GUI.Box(new Rect(Screen.width*.5f-230*scale,Screen.height*.5f+356*scale,460*scale,36*scale),social.Caption,captionStyle);
            GUI.backgroundColor=old;
        }
    }
}
