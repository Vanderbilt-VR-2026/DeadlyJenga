using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastleSocial
{
    // A fact belongs to an observer. A reply gets its own fact and the utterance it heard.
    internal sealed class ObservedSpeech
    {
        internal string Fact;
        internal SocialEventKind Kind;
        internal string HeardFrom;
        internal string HeardText;
        internal string PartnerId;
        internal string PartnerFact;
        internal bool HeardOnly;
        internal bool PartnerHeardOnly;
        internal float Expires;
    }

    public sealed partial class SocialWorld
    {
        const float VoiceHearingRange = 12;
        const float EventHearingRange = 17;
        const float SightRange = 24;
        const int SightMask = ~(1 << Layers.Survivor);
        readonly Dictionary<SocialEventKind, float> nextObservation = new();
        readonly Dictionary<string, (string fact, float at)> recentObservations = new();
        int roundSpeechEpoch;
        System.Random dialogueChance;

        bool ContextualSpeech => OnDevice && OnDevice.Requested;
        internal int RoundSpeechEpoch => roundSpeechEpoch;

        void SubscribeToObservations()
        {
            GameEvents.BlockBroken += ObserveBreak;
            GameEvents.Damaged += ObserveDamage;
            GameEvents.Pushed += ObservePush;
            GameEvents.Eliminated += ObserveElimination;
            GameEvents.HazardStarted += ObserveHazardStart;
            GameEvents.HazardEnded += ObserveHazardEnd;
            GameEvents.RoundStarted += ResetObservations;
            GameEvents.RoundEnded += ResetObservations;
            GameEvents.RewindStarted += RewindObservations;
        }

        void UnsubscribeFromObservations()
        {
            GameEvents.BlockBroken -= ObserveBreak;
            GameEvents.Damaged -= ObserveDamage;
            GameEvents.Pushed -= ObservePush;
            GameEvents.Eliminated -= ObserveElimination;
            GameEvents.HazardStarted -= ObserveHazardStart;
            GameEvents.HazardEnded -= ObserveHazardEnd;
            GameEvents.RoundStarted -= ResetObservations;
            GameEvents.RoundEnded -= ResetObservations;
            GameEvents.RewindStarted -= RewindObservations;
        }

        void ResetObservations()
        {
            roundSpeechEpoch++;
            recentObservations.Clear();
            nextObservation.Clear();
            objectiveTotals.Clear();
            if (ContextualSpeech) OnDevice.CancelAll();
        }

        void RewindObservations(float seconds) => ResetObservations();

        void ObserveBreak(GameObject block, Vector3 at, Vector3 velocity) =>
            ObserveNearby(SocialEventKind.Collapse, at, "a tower block broke apart", "a crash", block, 3);

        void ObserveDamage(GameObject actor, float damage)
        {
            if (actor && damage > 0) ObserveNearby(SocialEventKind.Damage, actor.transform.position + Vector3.up * .7f,
                "someone took a hit", null, actor, 2);
        }

        void ObservePush(GameObject actor, Vector3 force)
        {
            if (actor && force.sqrMagnitude > 4) ObserveNearby(SocialEventKind.Collision, actor.transform.position + Vector3.up * .7f,
                "someone got knocked back", null, actor, 2);
        }

        void ObserveElimination(GameObject actor, GameObject source)
        {
            if (actor) ObserveNearby(SocialEventKind.Elimination, actor.transform.position + Vector3.up * .7f,
                "someone was eliminated", null, actor, 2);
        }

        void ObserveHazardStart(HazardType hazard, Vector3 at) =>
            ObserveNearby(SocialEventKind.Hazard, at, "a hazard started", null, null, 4);

        void ObserveHazardEnd(HazardType hazard, Vector3 at) =>
            ObserveNearby(SocialEventKind.Hazard, at, "a hazard ended", null, null, 4);

        void ObserveNearby(SocialEventKind kind, Vector3 at, string seen, string sound, GameObject subject, float cooldown)
        {
            if (!isActiveAndEnabled || !ContextualSpeech || !OnDevice.Ready || !IsAuthority || !Config.enableBots || !Config.enableVoice) return;
            float now = Time.unscaledTime;
            if (nextObservation.TryGetValue(kind, out float next) && now < next) return;
            nextObservation[kind] = now + cooldown;

            SocialBotAgent speaker = null;
            string fact = null;
            bool sawEvent = false;
            float bestDistance = float.PositiveInfinity;
            foreach (var agent in Agents)
            {
                if (!CanTalk(agent)) continue;
                string observed = Observation(agent, at, seen, sound, subject, out bool saw, out float distance);
                if (observed == null) continue;
                recentObservations[agent.State.identity.id] = (observed, now);
                if (agent.State.nextSpeech > now) continue;
                if (speaker && (sawEvent && !saw || sawEvent == saw && distance >= bestDistance)) continue;
                speaker = agent;
                fact = observed;
                sawEvent = saw;
                bestDistance = distance;
            }
            if (!speaker) return;

            var observation = new ObservedSpeech { Fact = fact, Kind = kind, HeardOnly = !sawEvent, Expires = now + 12 };
            var partner = PickPartner(speaker, observation);
            if (partner)
            {
                observation.PartnerFact = Observation(partner, at, seen, sound, subject, out bool partnerSaw, out _);
                observation.PartnerHeardOnly = !partnerSaw;
            }
            OnDevice.QueueObserved(speaker.State.identity, observation);
        }

        SocialBotAgent PickPartner(SocialBotAgent speaker, ObservedSpeech turn)
        {
            dialogueChance ??= new System.Random(Config.seed ^ 0x2b180b);
            if (dialogueChance.NextDouble() >= Config.respondBot) return null;
            SocialBotAgent partner = null;
            float nearest = VoiceHearingRange * VoiceHearingRange;
            foreach (var agent in Agents)
            {
                if (!CanTalk(agent) || agent == speaker || agent.State.nextSpeech > Time.unscaledTime) continue;
                float distance = (agent.transform.position - speaker.transform.position).sqrMagnitude;
                if (distance >= nearest) continue;
                partner = agent;
                nearest = distance;
            }
            if (partner) turn.PartnerId = partner.State.identity.id;
            return partner;
        }

        static bool CanTalk(SocialBotAgent agent) => agent && agent.isActiveAndEnabled && agent.State != null
            && agent.State.identity.voice && !agent.State.muted && NetworkActors.Alive(agent.Bot) && !agent.Bot.Respawning;

        static string Observation(SocialBotAgent agent, Vector3 at, string seen, string sound,
            GameObject subject, out bool saw, out float distance)
        {
            Vector3 eyes = agent.transform.position + Vector3.up * .7f;
            Vector3 delta = at - eyes;
            distance = delta.sqrMagnitude;
            bool self = subject == agent.gameObject;
            float facing = Vector3.Dot(agent.transform.forward, delta.normalized);
            saw = self || distance <= SightRange * SightRange && (facing >= -.1f || distance <= 4)
                && (!Physics.Linecast(eyes, at, out var hit, SightMask, QueryTriggerInteraction.Ignore)
                    || IsObservedTarget(hit.collider, subject));
            if (saw)
            {
                string place = delta.y > 1.5f ? "above me" : delta.y < -1.5f ? "below me" :
                    Vector3.Dot(agent.transform.right, delta) > .7f ? "on my right" :
                    Vector3.Dot(agent.transform.right, delta) < -.7f ? "on my left" : "in front of me";
                return self ? "This happened to me: " + seen + "." : char.ToUpperInvariant(seen[0]) + seen.Substring(1) + " " + place + ". I saw it happen.";
            }
            return sound != null && distance <= EventHearingRange * EventHearingRange
                ? "I heard " + sound + " nearby. I could not see what happened." : null;
        }

        static bool IsObservedTarget(Collider collider, GameObject subject)
        {
            if (!subject) return false;
            if (collider.transform.IsChildOf(subject.transform)) return true;
            // Climbable surfaces can use a separate collider from the source block.
            return Climbables.TryGet(collider, out var climbable) && climbable.Body &&
                climbable.Body.gameObject == subject;
        }

        internal string RecentObservation(string id)
        {
            return recentObservations.TryGetValue(id, out var recent) && Time.unscaledTime - recent.at < 12
                ? recent.fact : "";
        }

        internal bool CanDeliverObserved(string id) =>
            Agents.Exists(agent => CanTalk(agent) && agent.State.identity.id == id);

        internal void ReplyToObserved(string speakerId, string text, ObservedSpeech turn)
        {
            if (turn.PartnerId == null || !ContextualSpeech || !Config.enableBots || !Config.enableVoice || !IsAuthority) return;
            var partner = Agents.Find(a => CanTalk(a) && a.State.identity.id == turn.PartnerId);
            var speaker = Agents.Find(a => CanTalk(a) && a.State.identity.id == speakerId);
            if (!partner || !speaker || (partner.transform.position - speaker.transform.position).sqrMagnitude >
                VoiceHearingRange * VoiceHearingRange || partner.State.nextSpeech > Time.unscaledTime) return;
            OnDevice.QueueObserved(partner.State.identity, new ObservedSpeech {
                Kind = turn.Kind,
                Fact = turn.PartnerFact ?? "I did not witness the event.",
                HeardFrom = speaker.State.identity.name,
                HeardOnly = turn.PartnerHeardOnly || turn.PartnerFact == null,
                HeardText = text,
                Expires = Time.unscaledTime + 12
            });
        }
    }
}
