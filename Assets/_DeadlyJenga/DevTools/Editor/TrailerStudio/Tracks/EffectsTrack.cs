using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Weapons, projectiles and particles. Same data Gameplay Theatre's v4 tape stored:
    // the match's equipment state each frame, plus a sample of every visible particle.
    public sealed class EffectsTrack : IStudioTrackType
    {
        public const string Id = "effects";
        public string TypeId => Id;
        public string Label => "Effects";
        public Color Color => StudioColors.Effects;
        public int Order => 15;   // before the actors, so they draw their weapons from the recorded state

        public void EnsureTracks(StudioSet set, StudioTape tape) => tape.GetOrAdd("effects", Id, "Effects");
        public StudioRecorder CreateRecorder(StudioStage stage, StudioTrack track) => new Recorder(stage);
        public StudioPlayer CreatePlayer(StudioStage stage, StudioTrack track, StudioTake take) => new Player(stage, take);

        // The weapon part of a match frame.
        [Serializable] public sealed class Weapons
        {
            public BlasterFrame blaster;
            public EquipmentProjectileFrame[] projectiles;
            public EquipmentActionFrame[] actions;
            public EquipmentState[] equipment;
            public string epoch;
            public int sequence;

            public static Weapons From(MatchFrame frame) => new Weapons
            {
                blaster = frame.blaster, projectiles = frame.weaponProjectiles, actions = frame.equipmentActions,
                equipment = frame.equipment, epoch = frame.equipmentEpoch, sequence = frame.equipmentSequence
            };

            public void Apply(MatchFrame frame)
            {
                frame.blaster = blaster ?? new BlasterFrame();
                frame.weaponProjectiles = projectiles ?? Array.Empty<EquipmentProjectileFrame>();
                frame.equipmentActions = actions ?? Array.Empty<EquipmentActionFrame>();
                frame.equipment = equipment ?? Array.Empty<EquipmentState>();
                frame.equipmentEpoch = epoch;
                frame.equipmentSequence = sequence;
            }
        }

        public static byte[] Pack(string weaponsJson, Action<BinaryWriter> particles)
        {
            using (var memory = new MemoryStream())
            using (var w = new BinaryWriter(memory))
            {
                w.Write(weaponsJson ?? "");
                particles(w);
                w.Flush();
                return memory.ToArray();
            }
        }

        sealed class Recorder : StudioRecorder
        {
            readonly StudioStage stage;
            readonly TheatreParticleTape particles = new TheatreParticleTape();

            public Recorder(StudioStage stage) { this.stage = stage; }

            public override byte[] Capture(int frame, bool key)
            {
                var weapons = Weapons.From(MatchRuntime.Current?.Frame ?? new MatchFrame());
                // Store each live actor's equipment under its mark id, so playback can hand it to the right copy.
                var equipment = new List<EquipmentState>();
                foreach (var pair in stage.live)
                {
                    var state = pair.Value ? pair.Value.GetComponent<IEquipmentControls>()?.Equipment?.Copy() : null;
                    if (state == null) continue;
                    state.actor = pair.Key;
                    equipment.Add(state);
                }
                weapons.equipment = equipment.ToArray();
                return Pack(JsonUtility.ToJson(weapons), particles.Write);
            }

            public override void Dispose() => particles.Dispose();
        }

        sealed class Player : StudioPlayer
        {
            readonly StudioStage stage;
            readonly StudioTake take;
            readonly TheatreParticleTape particles = new TheatreParticleTape();
            readonly List<Action> weaponViews = new List<Action>();
            readonly List<Behaviour> pausedViews = new List<Behaviour>();
            readonly CandyEquipmentActionCursor shots = new CandyEquipmentActionCursor();
            GameObject trailRoot;
            CandyBulletTrails trails;
            bool firstFrame = true;
            Weapons held;       // the live match's own state, put back after the actors have drawn

            public Player(StudioStage stage, StudioTake take) { this.stage = stage; this.take = take; }

            // With nobody live in the scene, the game's own projectile and weapon-effect views are paused
            // and run by hand against the recorded state (the same trick Gameplay Theatre uses).
            void TakeOverWeaponViews()
            {
                if (trailRoot) return;
                trailRoot = new GameObject("Studio recorded weapon effects");
                trails = trailRoot.AddComponent<CandyBulletTrails>();
                foreach (var view in UnityEngine.Object.FindObjectsByType<CandyPlasmaView>(FindObjectsSortMode.None)) Bind(view);
                foreach (var view in UnityEngine.Object.FindObjectsByType<CandyArsenalEffects>(FindObjectsSortMode.None)) Bind(view);
            }

            void Bind(Behaviour view)
            {
                var update = view.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                if (update == null) return;
                weaponViews.Add((Action)update.CreateDelegate(typeof(Action), view));
                if (view.enabled) { pausedViews.Add(view); view.enabled = false; }
            }

            public override void Show(int frame, bool running)
            {
                if (!take.Covers(frame) || take.frames[frame - take.start].Length == 0) return;
                using (var r = new BinaryReader(new MemoryStream(take.frames[frame - take.start])))
                {
                    string json = r.ReadString();
                    var match = MatchRuntime.Current?.Frame;
                    var recorded = match != null && json.Length > 0 ? JsonUtility.FromJson<Weapons>(json) : null;
                    if (recorded != null)
                    {
                        held = Weapons.From(match);
                        if (stage.live.Count == 0) ShowWeapons(match, recorded);
                        else
                        {
                            // Someone is live: keep their equipment and add the recorded actors' equipment.
                            var merged = (match.equipment ?? Array.Empty<EquipmentState>()).ToList();
                            foreach (var state in recorded.equipment ?? Array.Empty<EquipmentState>())
                                if (state != null && merged.All(m => m.actor != state.actor)) merged.Add(state);
                            match.equipment = merged.ToArray();
                        }
                    }
                    particles.Read(r, true);
                }
            }

            void ShowWeapons(MatchFrame match, Weapons recorded)
            {
                TakeOverWeaponViews();
                recorded.Apply(match);
                foreach (var update in weaponViews) update();
                if (firstFrame)
                {
                    firstFrame = false;
                    shots.Baseline(new MatchFrame { equipmentEpoch = match.equipmentEpoch,
                        equipmentSequence = Math.Max(0, (match.equipmentActions.FirstOrDefault()?.id ?? 1) - 1) });
                }
                shots.Read(match, item =>
                {
                    if (item.action != EquipmentAction.Shot || item.endpoints == null || item.endpoints.Length == 0) return;
                    if (item.kind == EquipmentKind.NeedleDrill) trails.FireNeedles(item.position, item.endpoints);
                    else if (item.kind == EquipmentKind.DualRifle || item.kind == EquipmentKind.SheriffHammer ||
                        item.kind > EquipmentKind.PoweredArmor && !EquipmentSpecifications.Get(item.kind).melee &&
                        item.kind != EquipmentKind.WristLaser) trails.Fire(item.position, item.endpoints, item.shotgun);
                });
            }

            public override void AfterShow()
            {
                var match = MatchRuntime.Current?.Frame;
                if (held != null && match != null) held.Apply(match);
                held = null;
            }

            public override void Dispose()
            {
                foreach (var view in pausedViews) if (view) view.enabled = true;
                if (trailRoot) UnityEngine.Object.Destroy(trailRoot);
                particles.Dispose();
            }
        }
    }
}
