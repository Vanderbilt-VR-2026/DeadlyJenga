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
    //   Bot mark: the normal bot brain.
    //   Survivor mark nobody performs: a stand-in that stands on its mark and obeys gravity.
    //   Hand mark: the game's hand bot when the Set says so; otherwise it waits to be performed.
    //   The performer: the local player (survivor or hand rig) placed on the chosen mark.
    public static class StudioCast
    {
        static readonly List<CastleSurvivor> standIns = new List<CastleSurvivor>();
        static readonly List<(CastleSurvivor motor, StudioPerformer performer)> driven = new List<(CastleSurvivor, StudioPerformer)>();
        static bool spawnedBots, spawnedHands;
        static readonly MethodInfo grantEquipment = typeof(MatchModeRuntime).GetMethod("GiveEquipmentTo", BindingFlags.Instance | BindingFlags.NonPublic);

        // The local player is not part of the scene unless someone is performing: hide both local rigs.
        public static void Park(StudioStage stage)
        {
            var session = stage.session;
            if (stage.performer.Length > 0) return;   // someone is in a body; leave them there
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
            StudioCamera.Listen(true);
        }

        public static void SpawnLive(StudioStage stage, StudioPass pass)
        {
            foreach (var mark in stage.set.marks)
            {
                if (!pass.record.Contains(mark.id) || mark.id == pass.performer || stage.Live(mark.id)) continue;
                if (mark.role == StudioRole.Hand)
                {
                    if (stage.set.handBot) stage.live[mark.id] = SpawnHandBot(stage, mark);
                    continue;
                }
                stage.live[mark.id] = SpawnBody(stage, mark, mark.role == StudioRole.Bot);
            }
        }

        public static GameObject SpawnBody(StudioStage stage, StudioMark mark, bool brain)
        {
            var bots = stage.session.SurvivorBots;
            var body = bots.SpawnOne();
            if (!body) throw new InvalidOperationException($"No room to add {mark.actor}. The bot limit is {bots.Capacity}.");
            spawnedBots = true;
            var motor = body.GetComponent<CastleSurvivor>();
            var avatar = body.GetComponent<CandyPlayerAvatar>();
            if (!motor || !avatar) throw new InvalidOperationException(mark.actor + " is missing its normal survivor rig.");
            motor.Teleport(stage.ToWorld(mark.position), Quaternion.Euler(0, mark.yaw, 0));
            avatar.SetColor(mark.color);
            if (mark.customLook) avatar.ApplyAppearance(mark.appearance);
            if (!brain)
            {
                // No brain and no input: the survivor's own movement code still runs, so it stands and obeys gravity.
                var mind = body.GetComponent<CastleBotBrain>();
                if (mind) mind.enabled = false;
                // A driven mark: its script works the controls where the bot brain would (StudioPerformer).
                var drive = StudioPerformer.For(stage.set, mark.id);
                if (drive != null) { var performer = StudioPerformer.Attach(stage, drive, motor); motor.InputDriver = performer; motor.InputSuspended = false; driven.Add((motor, performer)); }
                else
                {
                    motor.InputDriver = null;
                    standIns.Add(motor);
                }
            }
            GiveEquipment(stage, body, mark);
            return body;
        }

        static GameObject SpawnHandBot(StudioStage stage, StudioMark mark)
        {
            var hands = stage.session.HandBots;
            var hand = hands?.SpawnOne();
            if (!hand) throw new InvalidOperationException("The hand bot could not be added.");
            spawnedHands = true;
            (hand.GetComponent<ITeleportable>() ?? hand.GetComponentInChildren<ITeleportable>())
                ?.Teleport(stage.ToWorld(mark.position), Quaternion.Euler(0, mark.yaw, 0));
            return hand;
        }

        internal static EquipmentKind StartingEquipment(StudioMark mark)
        {
            if (Enum.TryParse(mark.equipment, true, out EquipmentKind kind) && Enum.IsDefined(typeof(EquipmentKind), kind))
                return kind;
            return mark.customLook && mark.appearance.poweredArmor ? EquipmentKind.PoweredArmor : EquipmentKind.None;
        }

        static void GiveEquipment(StudioStage stage, GameObject body, StudioMark mark, bool clearEmpty = false)
        {
            var kind = StartingEquipment(mark);
            if (kind == EquipmentKind.None && !clearEmpty) return;
            var identity = body.GetComponent<IModeActor>();
            // The match runtime has no public "give this actor a weapon" yet; Theatre uses the same private method.
            bool given = identity != null && grantEquipment != null &&
                (bool)grantEquipment.Invoke(stage.session.Modes, new object[] { identity.ActorId, kind, false, true });
            if (!given) Debug.LogWarning($"Trailer Studio: could not set starting gear {kind} for {mark.actor}.");
        }

        // Keep stand-ins brainless even if the bot system hands their body a driver again.
        public static void SimulateStandIns(StudioStage stage)
        {
            for (int i = standIns.Count - 1; i >= 0; i--)
            {
                if (!standIns[i]) { standIns.RemoveAt(i); continue; }
                if (standIns[i].InputDriver != null) standIns[i].InputDriver = null;
            }
            // Driven marks keep their script, for the same reason.
            foreach (var (motor, performer) in driven)
                if (motor && !ReferenceEquals(motor.InputDriver, performer)) motor.InputDriver = performer;
        }

        // Remove bots and stand-ins. The performer stays in their body until LeaveBody.
        public static void ClearLive(StudioStage stage)
        {
            standIns.Clear();
            driven.Clear();
            StudioPerformer.Clear();
            if (spawnedBots) stage.session.SurvivorBots.Clear();
            if (spawnedHands) stage.session.HandBots?.Clear();
            spawnedBots = spawnedHands = false;
            var performerBody = stage.performer.Length > 0 ? stage.Live(stage.performer) : null;
            stage.live.Clear();
            if (performerBody) stage.live[stage.performer] = performerBody;
        }

        // ---- The performer ----

        static IPlayableCharacter Rig(StudioStage stage, StudioMark mark) =>
            mark.role == StudioRole.Hand ? stage.session.Hand : stage.session.Player;

        // Put the local player into this actor: the survivor rig or the flying hand, dressed like the mark.
        public static GameObject Perform(StudioStage stage, StudioMark mark)
        {
            var session = stage.session;
            bool hand = mark.role == StudioRole.Hand;
            if (stage.performer != mark.id)
            {
                if (stage.performer.Length > 0) stage.live.Remove(stage.performer);
                stage.performer = mark.id;
                session.SetRole(hand);              // the session's own switch: activates the rig and its camera
                StudioCamera.Listen(false);         // the player's own ears are used while performing
            }
            var rig = Rig(stage, mark);
            if (!rig.gameObject.activeSelf)
            {
                rig.gameObject.SetActive(true);
                if (rig.ViewCamera) rig.ViewCamera.enabled = true;
            }
            if (!hand)
            {
                var avatar = rig.gameObject.GetComponent<CandyPlayerAvatar>();
                if (avatar)
                {
                    avatar.SetColor(mark.color);
                    stage.localWardrobe ??= avatar.Appearance;
                    ApplyLocalAppearance(avatar, mark.customLook ? mark.appearance : stage.localWardrobe.Value);
                }
                GiveEquipment(stage, rig.gameObject, mark, clearEmpty: true);
            }
            stage.live[mark.id] = rig.gameObject;
            session.SetMenu(false);
            return rig.gameObject;
        }

        static void ApplyLocalAppearance(CandyPlayerAvatar avatar, AvatarAppearance look)
        {
            if (!avatar.TryGetComponent<CastleSurvivor>(out var motor)) return;
            bool driven = motor.externallyDriven;
            try
            {
                // The studio costume is take-local. Do not save it as the player's lobby outfit.
                motor.externallyDriven = true;
                avatar.ApplyAppearance(look);
            }
            finally { motor.externallyDriven = driven; }
        }

        internal static void RestoreLocalAppearance(StudioStage stage)
        {
            if (stage?.localWardrobe == null || !NetworkActors.Alive(stage.session.Player)) return;
            var avatar = stage.session.Player.gameObject.GetComponent<CandyPlayerAvatar>();
            if (avatar) ApplyLocalAppearance(avatar, stage.localWardrobe.Value);
            stage.localWardrobe = null;
        }

        public static void PlaceOnMark(StudioStage stage, StudioMark mark) =>
            Rig(stage, mark).Place(stage.ToWorld(mark.position), Quaternion.Euler(0, mark.yaw, 0));

        // Step out of the body: back to the director's view.
        public static void LeaveBody(StudioStage stage)
        {
            if (stage.performer.Length == 0) return;
            var mark = stage.set.Mark(stage.performer);
            if (mark != null) Rig(stage, mark).InputSuspended = false;
            RestoreLocalAppearance(stage);
            stage.live.Remove(stage.performer);
            stage.performer = "";
            Park(stage);
        }

        public static void SuspendInput(StudioStage stage, bool suspended)
        {
            var mark = stage.set.Mark(stage.performer);
            if (mark != null) Rig(stage, mark).InputSuspended = suspended;
        }

        // Before a punch-in the performer rides along their old take: same place, same facing.
        public static void Follow(StudioStage stage, StudioMark mark, StudioTake take, int frame)
        {
            if (!take.Covers(frame)) return;
            var track = stage.tape.Find(mark.id);
            var pose = ActorFrame.Read(track.type, take.frames[frame - take.start]);
            if (!pose.visible) return;
            float yaw = (pose.isHand ? pose.target.rotation : pose.body.headRotation).eulerAngles.y;
            var rig = Rig(stage, mark);
            rig.InputSuspended = true;
            rig.Place(pose.Position, Quaternion.Euler(0, yaw, 0));
        }
    }
}
