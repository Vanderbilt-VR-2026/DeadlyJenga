using System;
using UnityEngine;

namespace DeadlyJenga.Core
{
    // A character a person plays: the survivor or the flying hand. The session switches between them.
    public interface IPlayableCharacter
    {
        GameObject gameObject { get; }
        Transform transform { get; }
        Camera ViewCamera { get; }
        bool IsVR { get; }
        bool InputSuspended { get; set; }
        bool HasHandTracking { get; }
        bool MenuPressed { get; }       // hand-tracking menu gesture this frame
        bool SelectPressed { get; }     // hand-tracking select (right pinch) this frame
        string StatusText { get; }      // one line for the HUD
        void PrepareForSession();       // cameras clear to the sky; hands leave the desktop cursor alone
        void Place(Vector3 position, Quaternion rotation);   // put here, ready to play (survivors are revived)
    }

    // Survivor settings and rules that menus, the lobby and multiplayer change.
    public interface ISurvivorSettings
    {
        string MovementLabel { get; }
        string TurnLabel { get; }
        float SnapAngle { get; }
        float SmoothTurnSpeed { get; }
        void CycleMovement();
        void CycleTurn();
        void CycleSnapAngle();
        void CycleSmoothTurnSpeed();
        void UseStationaryPreset();
        void UseFluidPreset();
        MatchRules CurrentRules { get; }                      // current VR and stamina values; change a copy, then ApplyMatchRules
        void FitJump(float stepHeight, float shortfall);      // jump just short of one map level
        string SaveStaminaRules();                            // host's rules, sent to joining players
        void LoadStaminaRules(string json);
        void ApplyMatchRules(MatchRules rules);               // choices made in the lobby
        bool TryStandOn(Rigidbody block, float sideways, out Vector3 spot);   // a safe spot on top of a block
    }

    // Lobby choices for the local survivor. Option names come from MatchOptions.
    [Serializable]
    public sealed class MatchRules
    {
        public string movement = "HeadRelative", turn = "Snap", drainCurve = "Linear", recoveryCurve = "Linear";
        public float snapAngle = 30, smoothDegrees = 60, gripSeconds = 4;
        public float drainRate = 1, recoveryRate = .65f, recoveryDelay = .45f, drainDegree = 2, recoveryDegree = 2;
    }

    // Option lists the lobby cycles through. Characters fills them when the game loads.
    public static class MatchOptions
    {
        public static string[] Movements = { "HeadRelative", "BodyRelative" };
        public static string[] Turns = { "Snap", "Smooth" };
        public static string[] Curves = { "Linear" };
        public static Func<string[]> SurvivorPersonalities = () => new[] { "Default" };
        public static Func<string[]> HandPersonalities = () => new[] { "Playful" };

        public static string Next(string[] options, string current)
        {
            if (options == null || options.Length == 0) return current;
            return options[(Array.IndexOf(options, current) + 1) % options.Length];
        }
    }

    // Lobby (before the castle game): the local rig's hand input for Photon.
    public interface ILocalPlayerInput
    {
        void MoveToSpawn(Vector3 position, Quaternion rotation);
        bool TryWriteInput(object networkInput);   // a Fusion NetworkInput; the character fills it in
    }

    public static class LocalPlayerInput
    {
        public static Func<ILocalPlayerInput> FindOrCreate = () => null;   // Characters sets this on load
    }
}
