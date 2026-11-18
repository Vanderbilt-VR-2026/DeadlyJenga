using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Puts live characters on their marks for a recording, and takes them away afterwards.
    // A Bot mark uses the normal bot brain. A Survivor mark nobody is performing gets a stand-in
    // that stands on its mark and obeys gravity. Hand marks wait for Phase 2.
    public static class StudioCast
    {
        static readonly List<CastleSurvivor> standIns = new List<CastleSurvivor>();

        // The local player is not part of the scene unless someone is performing: hide both local rigs.
        public static void Park(StudioStage stage)
        {
            var session = stage.session;
            if (NetworkActors.Alive(session.Player))
            {
                if (session.Player.ViewCamera) session.Player.ViewCamera.enabled = false;
                session.Player.gameObject.SetActive(false);
            }
            if (NetworkActors.Alive(session.Hand))
            {
                if (session.Hand.ViewCamera) session.Hand.ViewCamera.enabled = false;
                session.Hand.gameObject.SetActive(false);
            }
            session.SetMenu(false);
        }

        public static void SpawnLive(StudioStage stage, StudioPass pass)
        {
            foreach (var mark in stage.set.marks)
            {
                if (!pass.record.Contains(mark.id) || mark.role == StudioRole.Hand || stage.Live(mark.id)) continue;
                stage.live[mark.id] = SpawnBody(stage, mark, mark.role == StudioRole.Bot);
            }
        }

        public static GameObject SpawnBody(StudioStage stage, StudioMark mark, bool brain)
        {
            var bots = stage.session.SurvivorBots;
            var body = bots.SpawnOne();
            if (!body) throw new InvalidOperationException($"No room to add {mark.actor}. The bot limit is {bots.Capacity}.");
            var motor = body.GetComponent<CastleSurvivor>();
            var avatar = body.GetComponent<CandyPlayerAvatar>();
            if (!motor || !avatar) throw new InvalidOperationException(mark.actor + " is missing its normal survivor rig.");
            motor.Teleport(stage.ToWorld(mark.position), Quaternion.Euler(0, mark.yaw, 0));
            avatar.SetColor(mark.color);
            if (mark.customLook) avatar.ApplyAppearance(mark.appearance);
            if (!brain)
            {
                var mind = body.GetComponent<CastleBotBrain>();
                if (mind) mind.enabled = false;
                standIns.Add(motor);
            }
            GiveEquipment(stage, body, mark);
            return body;
        }

        static void GiveEquipment(StudioStage stage, GameObject body, StudioMark mark)
        {
            if (string.IsNullOrEmpty(mark.equipment) || !Enum.TryParse(mark.equipment, true, out EquipmentKind kind) || kind == EquipmentKind.None) return;
            var identity = body.GetComponent<IModeActor>();
            // The match runtime has no public "give this actor a weapon" yet; Theatre uses the same private method.
            var grant = typeof(MatchModeRuntime).GetMethod("GiveEquipmentTo", BindingFlags.Instance | BindingFlags.NonPublic);
            bool given = identity != null && grant != null &&
                (bool)grant.Invoke(stage.session.Modes, new object[] { identity.ActorId, kind, false, true });
            if (!given) Debug.LogWarning($"Trailer Studio: could not give {mark.equipment} to {mark.actor}.");
        }

        // Stand-ins have no brain to move them, so the Studio runs their normal movement with no input.
        public static void SimulateStandIns(StudioStage stage)
        {
            for (int i = standIns.Count - 1; i >= 0; i--)
            {
                if (!standIns[i]) { standIns.RemoveAt(i); continue; }
                if (standIns[i].isActiveAndEnabled && Time.deltaTime > 0) standIns[i].Simulate(Vector2.zero, false, Time.deltaTime);
            }
        }

        public static void ClearLive(StudioStage stage)
        {
            standIns.Clear();
            if (stage.live.Values.Any(body => body)) stage.session.SurvivorBots.Clear();
            stage.live.Clear();
        }
    }
}
