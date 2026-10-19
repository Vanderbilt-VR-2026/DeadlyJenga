using System.Collections.Generic;

namespace DeadlyJenga.Core
{
    public interface IDemoMapControls
    {
        IReadOnlyList<LobbyOption> Maps { get; }
        string CurrentMapId { get; }
        bool CanSelectMap { get; }
        string MapStatus { get; }
        void SelectMap(string id);
    }

    // What the in-game menus can ask the match to do. Clients' requests go to the host.
    public enum DemoCommand { Rebuild, AddSurvivor, FillSurvivors, NextSurvivor, ClearSurvivors, AddHand, NextHand, ClearHands, StopDisasters }

    // Bots and disasters for the menus (Presentation). GameSession provides it as ISessionInfo.Demo.
    public interface IDemoControls
    {
        bool DemoMode { get; }                 // the lobby's "demo" mode: disasters page and shared controls
        bool CanCommand { get; }               // host, or shared demo controls
        string Status { get; }                 // the current disaster's status, "" when none
        void Run(DemoCommand command);

        int SurvivorCount { get; }
        int SurvivorTarget { get; }
        string SurvivorPersonality { get; }
        string SurvivorStatus { get; }
        int HandCount { get; }
        string HandPersonality { get; }
        string HandStatus { get; }

        void ApplyStaminaRules(string json);   // ISurvivorSettings.SaveStaminaRules format; host shares with everyone

        int DisasterCount { get; }
        string DisasterLabel(int index);
        bool DisasterIsToggle(int index);
        IReadOnlyList<DisasterParameter> DisasterParameters(int index);
        float[] DisasterValues(int index);
        void SetDisasterValue(int index, int parameter, float value);   // applies to the next launch
        string LaunchDisaster(int index);      // returns a status line for the menu
    }

    // AI players' voices and the microphone. GameSession provides it as ISessionInfo.Social.
    public interface ISocialControls
    {
        bool IsHost { get; }
        bool VoiceEnabled { get; set; }
        float BotVolume { get; set; }
        float HumanVolume { get; set; }
        bool MicrophoneOn { get; }
        void SetMicrophone(bool on);
        string MicrophoneDevice { get; set; }
        string MicrophoneStatus { get; }
        string SpeechStatus { get; }
        string Caption { get; }                // the latest line spoken, for subtitles
        bool ShowCaption { get; }
        IReadOnlyList<string> AiPlayerIds { get; }
        string AiPlayerName(string id);
        bool IsMuted(string id);
        void SetMuted(string id, bool muted);
        float Volume(string id);
        void SetVolume(string id, float volume);
    }
}
