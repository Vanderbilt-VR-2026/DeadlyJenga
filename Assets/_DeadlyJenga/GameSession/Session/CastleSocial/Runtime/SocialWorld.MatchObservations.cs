using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastleSocial
{
    public sealed partial class SocialWorld
    {
        readonly Dictionary<string, (int captures, int returns, int claims, int revives, int infections, int kills, float hill, float survival)> objectiveTotals = new();
        GameModeKind observedMode;
        RoundPhase observedPhase;
        bool haveMatchFrame;
        string observedWarning;
        string observedEquipmentEpoch;
        int observedEquipmentSequence;

        // Use explicit public announcements or the bot's own confirmed counters.
        // Never serialize the match frame, roles, equipment inventory or opponent positions.
        void ObserveMatchFrame()
        {
            if (!ContextualSpeechReady || !Config.enableBots || !Config.enableVoice || !Session || !Session.Modes) return;
            var frame = Session.Modes.Frame;
            if (frame?.rules == null) return;
            var mode = frame.rules.mode;
            bool initial = !haveMatchFrame || mode != observedMode;
            if (initial) objectiveTotals.Clear();

            if (!initial && frame.phase != observedPhase)
            {
                if (frame.phase == RoundPhase.Playing)
                    ObservePublic(SocialEventKind.Round, "The public match display says the round has started.");
                else if (frame.phase == RoundPhase.Victory)
                    ObservePublic(SocialEventKind.Round, "The public match display says the round is over.");
            }

            if (!initial && !string.IsNullOrWhiteSpace(frame.disasterWarning) &&
                !string.Equals(frame.disasterWarning, observedWarning, StringComparison.Ordinal))
                ObservePublic(SocialEventKind.Hazard,
                    "The public disaster warning reads: " + SocialText.Clean(frame.disasterWarning, 72) + ".");

            haveMatchFrame = true;
            observedMode = mode;
            observedPhase = frame.phase;
            observedWarning = frame.disasterWarning;

            if (mode == GameModeKind.MurderMystery) return;
            if (frame.phase != RoundPhase.Playing && mode != GameModeKind.Demo)
            {
                objectiveTotals.Clear();
                return;
            }

            if (!initial && observedEquipmentEpoch == frame.equipmentEpoch && observedEquipmentSequence < frame.equipmentSequence)
            {
                foreach (var action in frame.equipmentActions)
                {
                    if (action == null || action.id <= observedEquipmentSequence) continue;
                    switch (action.action)
                    {
                        case EquipmentAction.Shot:
                            ObserveNearby(SocialEventKind.Combat, action.position, "a shot went off", "a weapon go off", null, 4);
                            break;
                        case EquipmentAction.Pickup:
                            ObserveNearby(SocialEventKind.Equipment, action.position, "someone picked up equipment", null, null, 4);
                            break;
                        case EquipmentAction.Drop:
                            ObserveNearby(SocialEventKind.Equipment, action.position, "someone dropped equipment", null, null, 4);
                            break;
                        case EquipmentAction.Bash:
                            ObserveNearby(SocialEventKind.Combat, action.position, "someone swung a weapon", null, null, 4);
                            break;
                    }
                }
            }
            observedEquipmentEpoch = frame.equipmentEpoch;
            observedEquipmentSequence = frame.equipmentSequence;

            foreach (var player in frame.players)
            {
                if (player == null || !player.bot || string.IsNullOrEmpty(player.id)) continue;
                var agent = Agents.Find(a => CanTalk(a) && a.NetworkId == player.id);
                if (!agent) continue;
                bool hadTotal = objectiveTotals.TryGetValue(player.id, out var previous);
                objectiveTotals[player.id] = (player.captures, player.flagReturns, player.blocksClaimed,
                    player.revives, player.infections, player.kills, player.hillSeconds, player.survivalSeconds);
                if (!hadTotal) continue;
                string fact = mode == GameModeKind.CaptureFlag && player.captures > previous.captures ? "I scored a confirmed flag capture." :
                    mode == GameModeKind.CaptureFlag && player.flagReturns > previous.returns ? "I returned a flag to its base." :
                    mode == GameModeKind.BlockControl && player.blocksClaimed > previous.claims ? "I claimed a block for my team." :
                    player.revives > previous.revives ? "I revived a player." :
                    mode == GameModeKind.Infection && player.infections > previous.infections ? "I infected a player." :
                    player.kills > previous.kills ? "I got a confirmed elimination." :
                    mode == GameModeKind.KingOfHill && previous.hill < 5 && player.hillSeconds >= 5 ? "I put five seconds on the hill." :
                    mode == GameModeKind.Survival && previous.survival < 60 && player.survivalSeconds >= 60 ? "I made it to a minute." : null;
                if (fact != null) SpeakOwnEvent(agent, SocialEventKind.Objective, fact);
            }
        }

        internal string PublicModeName => haveMatchFrame ? observedMode.ToString() : "";

        void ObservePublic(SocialEventKind kind, string fact)
        {
            foreach (var agent in Agents)
            {
                if (!CanTalk(agent)) continue;
                if (agent.State.nextSpeech > Time.unscaledTime) continue;
                SpeakOwnEvent(agent, kind, fact);
                return;
            }
        }

        void SpeakOwnEvent(SocialBotAgent agent, SocialEventKind kind, string fact)
        {
            float now = Time.unscaledTime;
            recentObservations[agent.State.identity.id] = (fact, now);
            if (agent.State.nextSpeech > now) return;
            var turn = new ObservedSpeech { Kind = kind, Fact = fact, Expires = now + 12 };
            PickPartner(agent, turn);
            SpeakObservation(agent.State.identity, turn);
        }
    }
}
