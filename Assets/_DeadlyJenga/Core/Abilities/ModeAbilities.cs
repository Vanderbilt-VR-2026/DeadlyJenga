using UnityEngine;
namespace DeadlyJenga.Core
{
    public interface IBlockOwnership
    {
        int OwnerTeam { get; }
        void SetOwner(int team, Color color);
    }
    public static class SeaSpiritSystems
    {
        public static System.Func<GameObject, ISeaSpiritControls> Create;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Create = null;
    }
    public interface ISeaSpiritView
    {
        Transform BustAnchor { get; }
        Transform LeftShoulder { get; }
        Transform RightShoulder { get; }
        Transform LeftHandAnchor { get; }
        Transform RightHandAnchor { get; }
        bool Active { get; }
    }
    public interface ISeaSpiritControls
    {
        bool SeaSpiritActive { get; }
        float SeaSpiritReach { get; }
        float SeaSpiritOrbit { get; }
        void SetSeaSpirit(bool active);
        void SetSeaSpiritReach(float meters);
        void RecenterSeaSpirit();
    }
    public interface IOnDeviceVoiceControls
    {
        bool Requested { get; }
        bool Ready { get; }
        string Status { get; }
        string LastHeard { get; }
        string LastReplyKind { get; }
        int GeneratedReplies { get; }
        int FallbackReplies { get; }
        float LastReplySeconds { get; }
        void SetEnabled(bool enabled);
        void SubmitText(string text);
    }
}
