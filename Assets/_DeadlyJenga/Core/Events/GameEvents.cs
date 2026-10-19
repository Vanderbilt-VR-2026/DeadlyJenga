using System;
using UnityEngine;

namespace DeadlyJenga.Core
{
    // Announcements. Whoever causes something raises it; whoever cares listens.
    public static class GameEvents
    {
        public static event Action<Vector3, float> BlockHit;                  // position, strength
        public static event Action<GameObject, Vector3> Pushed;               // who, force
        public static event Action<GameObject, float> Damaged;                // who, amount
        public static event Action<GameObject, GameObject> Eliminated;        // who, source
        public static event Action<GameObject, EffectType> EffectStarted;     // who, effect
        public static event Action<GameObject, EffectType> EffectEnded;
        public static event Action<HazardType, Vector3> HazardStarted;        // hazard, position
        public static event Action<HazardType, Vector3> HazardEnded;
        public static event Action<GameObject, string> Emoted;              // who, emote name
        public static event Action RoundStarted;
        public static event Action RoundEnded;
        public static event Action<float> DepartureStarted;
        public static event Action<GameObject> SessionStarted;                // the session object; menus and HUDs attach here
        public static event Action<GameObject> LobbyStarted;                  // the lobby object (also ILobbyControls); its terminals attach here

        public static void RaiseBlockHit(Vector3 position, float strength) => BlockHit?.Invoke(position, strength);
        public static void RaisePushed(GameObject who, Vector3 force) => Pushed?.Invoke(who, force);
        public static void RaiseDamaged(GameObject who, float amount) => Damaged?.Invoke(who, amount);
        public static void RaiseEliminated(GameObject who, GameObject source) => Eliminated?.Invoke(who, source);
        public static void RaiseEffectStarted(GameObject who, EffectType type) => EffectStarted?.Invoke(who, type);
        public static void RaiseEffectEnded(GameObject who, EffectType type) => EffectEnded?.Invoke(who, type);
        public static void RaiseHazardStarted(HazardType type, Vector3 position) => HazardStarted?.Invoke(type, position);
        public static void RaiseHazardEnded(HazardType type, Vector3 position) => HazardEnded?.Invoke(type, position);
        public static void RaiseEmoted(GameObject who, string emote) => Emoted?.Invoke(who, emote);
        public static void RaiseRoundStarted() => RoundStarted?.Invoke();
        public static void RaiseRoundEnded() => RoundEnded?.Invoke();
        public static void RaiseDepartureStarted(float seconds) => DepartureStarted?.Invoke(seconds);
        public static void RaiseSessionStarted(GameObject session) => SessionStarted?.Invoke(session);
        public static void RaiseLobbyStarted(GameObject lobby) => LobbyStarted?.Invoke(lobby);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            BlockHit = null; Pushed = null; Damaged = null; Eliminated = null;
            EffectStarted = null; EffectEnded = null; HazardStarted = null; HazardEnded = null;
            RoundStarted = null; RoundEnded = null; Emoted = null; SessionStarted = null; LobbyStarted = null;
            DepartureStarted=null;
        }
    }
}
