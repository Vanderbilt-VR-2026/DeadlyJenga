using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Core
{
    public enum MysteryRole { Random, Innocent, Sheriff, Murderer }
    [Serializable] public sealed class MysterySettings
    {
        public float attackGraceSeconds=10, reviveSeconds=3, interactDistance=2;
        public float reportDelaySeconds=5, suspicionSeconds=30, trailRadius=3.05f;
        public float firstDisasterSeconds=45, vetoSeconds=10, chooseSeconds=20;
        public float disasterMinSeconds=60, disasterMaxSeconds=120;
        public int sheriffMistakes=2;
    }
    // Sent only to its owner. Never include this in a public match frame.
    [Serializable] public sealed class MysteryPrivateFrame
    {
        public MysteryRole role;
        public bool specialAttack;
        public int mistakes;
        public string[] disasterChoices=Array.Empty<string>();
        public bool veto;
        public float choiceRemaining, attackReadyIn;
        public string notice;
        public MysteryReportLock[] reportLocks=Array.Empty<MysteryReportLock>();
    }
    [Serializable] public sealed class MysteryReportLock {public string body;public float remaining;}
    [Serializable] public sealed class MysteryBodyFrame
    {
        public string id, name, finder;
        public Color color, finderColor;
        public Vector3 position;
        public int supportBlock=-1;
        public Vector3 supportPoint;
        public bool sheriff, reported, lost;
        public float reportedAge;
        public Color[] suspected=Array.Empty<Color>();
    }
    [Serializable] public sealed class MysteryTrailFrame
    {
        public int sequence, block;
        public Color color;
        public Vector3 point, normal;
        public float yaw;
    }
    [Serializable] public sealed class MysteryPublicFrame
    {
        public int revision;
        public bool sheriffFallen;
        public MysteryBodyFrame[] bodies=Array.Empty<MysteryBodyFrame>();
        public string[] downed=Array.Empty<string>();
        public MysteryStrikeFrame[] strikes=Array.Empty<MysteryStrikeFrame>();
    }
    [Serializable] public sealed class MysteryStrikeFrame {public string actor;public MysteryRole role;public float remaining;}
    public interface IMysteryControls
    {
        bool Active { get; }
        MysteryPrivateFrame Personal { get; }
        MysteryPublicFrame Clues { get; }
        IReadOnlyList<MysteryTrailFrame> Trails { get; }
        void SetSpecialAttack(bool enabled);
        void ReportBody(string bodyId);
        void Revive(string playerId,bool held);
        void Suspect(string bodyId,int trailSequence);
        void ChooseDisaster(string hazardId);
        void SetDemoRole(MysteryRole role);
        void ConfigureMystery(MysterySettings settings);
    }
    public interface IMysteryBodySource
    {
        IMysteryBody CreateMysteryBody();
    }
    public interface IMysteryBody
    {
        GameObject gameObject { get; }
        Vector3 Position { get; }
        void Follow(Rigidbody support,Vector3 position);
    }
    public static class MysteryChoices
    {
        public static MysteryRole RequestedRole;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>RequestedRole=MysteryRole.Random;
    }
}
