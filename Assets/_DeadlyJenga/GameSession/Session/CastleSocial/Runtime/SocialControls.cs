using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastleSocial
{
    // ISessionInfo.Social: AI voices and the microphone for the menus (Presentation), which draw them.
    public sealed class SocialControls : ISocialControls, IOnDeviceVoiceControls
    {
        public bool Requested => world && world.OnDevice && world.OnDevice.Requested;
        bool IOnDeviceVoiceControls.Ready => world && world.OnDevice && world.OnDevice.Ready;
        public string Status => world && world.OnDevice ? world.OnDevice.Status : "Unavailable";
        public string LastHeard => world && world.OnDevice ? world.OnDevice.LastHeard : "";
        public string LastReplyKind => world && world.OnDevice ? world.OnDevice.LastReplyKind : "";
        public int GeneratedReplies => world && world.OnDevice ? world.OnDevice.GeneratedReplies : 0;
        public int FallbackReplies => world && world.OnDevice ? world.OnDevice.FallbackReplies : 0;
        public float LastReplySeconds => world && world.OnDevice ? world.OnDevice.LastReplySeconds : 0;
        public void SetEnabled(bool enabled) { if(world && world.OnDevice) world.OnDevice.SetEnabled(enabled); }
        public void SubmitText(string text) { if(world && world.OnDevice) world.OnDevice.SubmitText(text); }
        readonly SocialWorld world;
        public SocialControls(SocialWorld owner) { world = owner; }
        bool Ready => world && world.Settings;

        public bool IsHost => world && world.IsAuthority;
        public bool VoiceEnabled { get => Ready && world.Config.enableVoice; set { if (Ready) world.Config.enableVoice = value; } }
        public float BotVolume { get => Ready ? world.Settings.botVolume : 0; set { if (Ready) world.Settings.botVolume = value; } }
        public float HumanVolume { get => Ready ? world.Settings.humanVolume : 0; set { if (Ready) world.Settings.humanVolume = value; } }
        public bool MicrophoneOn => Ready && world.MicrophoneInput.Capturing;
        public void SetMicrophone(bool on) { if (Ready) world.MicrophoneInput.EnableCapture(on); }
        public string MicrophoneDevice { get => Ready ? world.MicrophoneInput.Device : ""; set { if (Ready) world.MicrophoneInput.Device = value; } }
        public string MicrophoneStatus => Ready ? world.MicrophoneInput.Status : "";
        public string SpeechStatus => Ready ? world.Speech.Status : "";
        public string Caption => world ? world.Caption : "";
        public bool ShowCaption => Ready && world.Settings.showRuntimePanel && world.Config.enableVoice && world.Caption != "Social simulation ready";
        public IReadOnlyList<string> AiPlayerIds => world ? world.Participants.Values.OrderBy(n => n.name).Select(n => n.id).ToList() : new List<string>();
        public string AiPlayerName(string id) => world && world.Participants.TryGetValue(id, out var n) ? n.name : id;
        public bool IsMuted(string id) => Ready && world.Playback.IsMuted(id);
        public void SetMuted(string id, bool muted) { if (Ready) world.Mute(id, muted); }
        public float Volume(string id) => Ready ? world.Playback.Volume(id) : 0;
        public void SetVolume(string id, float volume) { if (Ready) world.Playback.SetVolume(id, volume); }
    }
}
