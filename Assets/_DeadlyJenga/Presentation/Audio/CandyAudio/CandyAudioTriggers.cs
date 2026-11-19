using System;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    public enum CandyAudioTrigger
    {
        Manual, RoundStarted, RoundEnded, HazardStarted, HazardEnded, PlayerDamaged,
        PlayerEliminated, Emote, BlockHit, BlockBroken, RewindStarted, RewindEnded,
        DepartureStarted, MatchFeedback, EffectStarted, EffectEnded
    }

    [Serializable]
    public sealed class CandyAudioBinding
    {
        public CandySound sound;
        public CandyAudioTrigger trigger;
        public bool enabled = true;
        public bool local = true;
        [Tooltip("For damage, death, emotes and effects, ignore actors other than the local player.")]
        public bool localPlayerOnly = true;
        [Tooltip("Optional exact hazard/effect asset name, emote name, or MatchSoundCue name. Empty matches all.")]
        public string filter = "";
        [Min(0)] public float minimumStrength;
        [Range(0,1)] public float intensity = 1;
    }

    // Event subscriptions only: no polling, scene searches or new gameplay events.
    public sealed class CandyAudioTriggers : MonoBehaviour
    {
        CandyAudioEngine engine;
        void Awake() => engine = GetComponent<CandyAudioEngine>();
        void OnEnable()
        {
            GameEvents.RoundStarted += RoundStart; GameEvents.RoundEnded += RoundEnd;
            GameEvents.HazardStarted += HazardStart; GameEvents.HazardEnded += HazardEnd;
            GameEvents.Damaged += Damage; GameEvents.Eliminated += Death; GameEvents.Emoted += Emote;
            GameEvents.BlockHit += Hit; GameEvents.BlockBroken += Broken;
            GameEvents.RewindStarted += RewindStart; GameEvents.RewindEnded += RewindEnd;
            GameEvents.DepartureStarted += Departure; GameEvents.MatchSound += Feedback;
            GameEvents.EffectStarted += EffectStart; GameEvents.EffectEnded += EffectEnd;
        }
        void OnDisable()
        {
            GameEvents.RoundStarted -= RoundStart; GameEvents.RoundEnded -= RoundEnd;
            GameEvents.HazardStarted -= HazardStart; GameEvents.HazardEnded -= HazardEnd;
            GameEvents.Damaged -= Damage; GameEvents.Eliminated -= Death; GameEvents.Emoted -= Emote;
            GameEvents.BlockHit -= Hit; GameEvents.BlockBroken -= Broken;
            GameEvents.RewindStarted -= RewindStart; GameEvents.RewindEnded -= RewindEnd;
            GameEvents.DepartureStarted -= Departure; GameEvents.MatchSound -= Feedback;
            GameEvents.EffectStarted -= EffectStart; GameEvents.EffectEnded -= EffectEnd;
        }
        public static bool Matches(CandyAudioBinding binding, CandyAudioTrigger trigger, string filter, float strength, bool actorIsLocal)
            => binding != null && binding.enabled && binding.intensity > 0 && binding.trigger == trigger && strength >= binding.minimumStrength &&
                (!binding.localPlayerOnly || actorIsLocal) &&
                (string.IsNullOrWhiteSpace(binding.filter) || string.Equals(binding.filter.Trim(), filter, StringComparison.OrdinalIgnoreCase));

        void Emit(CandyAudioTrigger trigger, Vector3 position, string filter = "", float strength = 1, GameObject actor = null)
        {
            if (!engine || !engine.bank || engine.bank.bindings == null) return;
            var player = Services.Session?.Player;
            bool isLocal = !actor || player != null && player.gameObject &&
                (actor == player.gameObject || actor.transform.IsChildOf(player.gameObject.transform));
            foreach (var binding in engine.bank.bindings)
                if (Matches(binding, trigger, filter, strength, isLocal))
                    engine.Play(binding.sound, position, binding.intensity, GetInstanceID(), local: binding.local);
        }
        Vector3 At => engine && engine.Listener ? engine.Listener.position : transform.position;
        void RoundStart() => Emit(CandyAudioTrigger.RoundStarted, At);
        void RoundEnd() => Emit(CandyAudioTrigger.RoundEnded, At);
        void HazardStart(HazardType type, Vector3 at) => Emit(CandyAudioTrigger.HazardStarted, at, type ? type.name : "");
        void HazardEnd(HazardType type, Vector3 at) => Emit(CandyAudioTrigger.HazardEnded, at, type ? type.name : "");
        void Damage(GameObject actor, float amount) => Emit(CandyAudioTrigger.PlayerDamaged, actor ? actor.transform.position : At, strength: amount, actor: actor);
        void Death(GameObject actor, GameObject source) => Emit(CandyAudioTrigger.PlayerEliminated, actor ? actor.transform.position : At, actor: actor);
        void Emote(GameObject actor, string name) => Emit(CandyAudioTrigger.Emote, actor ? actor.transform.position : At, name, actor: actor);
        void Hit(Vector3 at, float strength) => Emit(CandyAudioTrigger.BlockHit, at, strength: strength);
        void Broken(GameObject block, Vector3 at, Vector3 velocity) => Emit(CandyAudioTrigger.BlockBroken, at, strength: velocity.magnitude);
        void RewindStart(float seconds) => Emit(CandyAudioTrigger.RewindStarted, At, strength: seconds);
        void RewindEnd() => Emit(CandyAudioTrigger.RewindEnded, At);
        void Departure(float seconds) => Emit(CandyAudioTrigger.DepartureStarted, At, strength: seconds);
        void Feedback(MatchSoundCue cue, Vector3 at) => Emit(CandyAudioTrigger.MatchFeedback, at, cue.ToString());
        void EffectStart(GameObject actor, EffectType type) => Emit(CandyAudioTrigger.EffectStarted, actor ? actor.transform.position : At, type ? type.name : "", actor: actor);
        void EffectEnd(GameObject actor, EffectType type) => Emit(CandyAudioTrigger.EffectEnded, actor ? actor.transform.position : At, type ? type.name : "", actor: actor);
    }
}
