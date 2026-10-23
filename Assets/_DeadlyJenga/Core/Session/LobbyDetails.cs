using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Core
{
    [Serializable]
    public sealed class LobbyParticipant
    {
        public string id, noun, displayName;
        public Color color=Color.white;
        public bool host;
    }
    [Serializable]
    public sealed class LobbyOption
    {
        public string id,title,description;
        public bool available=true;
    }
    [Serializable]
    public sealed class LobbyHazardSetup {public string id;public float[] values;}
    [Serializable]
    public sealed class LobbySetup
    {
        public ModeRules rules=new ModeRules();
        public MatchRules movement=new MatchRules();
        public int population=10,hands,survivorPreset,handPreset;
        public bool sameHumanTeam;
        public LobbyHazardSetup[] disasters=Array.Empty<LobbyHazardSetup>();
    }
    public interface ILobbyDetails
    {
        IReadOnlyList<LobbyParticipant> Participants { get; }
        IReadOnlyList<string> HostRequests { get; }
        IReadOnlyList<LobbyOption> Maps { get; }
        IReadOnlyList<LobbyOption> Modes { get; }
        string LocalParticipantId { get; }
        string SelectedMapId { get; }
        string SelectedModeId { get; }
        string Notification { get; }
        int ConnectedCount { get; }
        Transform ParticipantRoot(string id);
        void RequestHost();
        void ReviewHostRequest(string id,bool accept);
        LobbySetup Setup { get; }
        void ConfigureSetup(LobbySetup setup);
    }
}
