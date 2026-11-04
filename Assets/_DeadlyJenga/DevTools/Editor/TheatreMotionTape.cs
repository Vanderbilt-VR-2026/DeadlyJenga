using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CharacterNetwork;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using DeadlyJenga.CandyAudio;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    // A take stores observed poses. Playback never asks the physics solver to recreate a collapse.
    sealed class TheatreMotionTape : IDisposable, IWaveState
    {
        const int FormatV2 = 0x324A5444, FormatV3 = 0x334A5444, Format = 0x344A5444;
        [Serializable] sealed class Effects
        {
            public BlasterFrame blaster;
            public EquipmentProjectileFrame[] projectiles;
            public EquipmentActionFrame[] actions;
            public EquipmentState[] equipment;
            public Color[] palette;
            public string epoch;
            public int sequence;

            public Effects() { }
            public Effects(MatchFrame frame)
            {
                blaster = frame.blaster;
                projectiles = frame.weaponProjectiles;
                actions = frame.equipmentActions;
                equipment = frame.equipment;
                epoch = frame.equipmentEpoch;
                sequence = frame.equipmentSequence;
            }

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
        readonly BinaryWriter writer;
        readonly BinaryReader reader;
        readonly Rigidbody[] blocks;
        readonly CandyPlayerAvatar[] actors;
        readonly int expected;
        readonly string[] ids;
        readonly bool includesEffects;
        readonly TheatreParticleTape particles;
        readonly Dictionary<string, CandyWeaponVisual> weapons = new();
        readonly List<Action> replayViews = new();
        readonly List<long> offsets = new();
        readonly List<Behaviour> pausedViews = new();
        readonly CandyEquipmentActionCursor trailsCursor = new();
        GameObject replayRoot;
        CandyBulletTrails trails;
        int frames;
        public WaveFrame Wave { get; private set; }
        public Vector3 Stage { get; }
        public int PeakParticles => particles?.PeakSamples ?? 0;
        public bool ParticleBudgetReached => particles?.Truncated ?? false;
        public int PeakEquipped { get; private set; }

        public TheatreMotionTape(string file, bool replay, string map, int fps, int length, Vector3 stage,
            Rigidbody[] blocks, string[] ids, CandyPlayerAvatar[] actors)
        {
            this.blocks = blocks;
            this.actors = actors;
            this.ids = ids;
            expected = length;
            if (replay)
            {
                reader = new BinaryReader(File.OpenRead(file));
                try
                {
                    int format = reader.ReadInt32();
                    includesEffects = format == Format || format == FormatV3;
                    if (format == Format) particles = new TheatreParticleTape();
                    if (!includesEffects && format != FormatV2 || reader.ReadString() != map || reader.ReadInt32() != fps ||
                        reader.ReadInt32() < length || reader.ReadInt32() != blocks.Length || reader.ReadInt32() != ids.Length)
                        throw new InvalidDataException("Take does not match this map, frame rate, cast or timeline length.");
                    Stage = NetWire.ReadVector(reader);
                    foreach (string id in ids)
                        if (reader.ReadString() != id) throw new InvalidDataException("Take cast order or identity changed.");
                    for (int i = 0; i < actors.Length; i++)
                    {
                        var actor = actors[i];
                        actor.SetColor(reader.ReadInt32());
                        actor.ApplyAppearance(CandyPlayerAvatar.WithStyle(new AvatarAppearance
                        { accessories = (AvatarAccessory)reader.ReadUInt32() }, reader.ReadUInt32()));
                    }
                    foreach (var block in blocks) if (block) block.isKinematic = true;
                    Tracked.Waves.Add(this);
                    if (includesEffects) PrepareEffects();
                    offsets.Add(reader.BaseStream.Position);
                }
                catch { Dispose(); throw; }
            }
            else
            {
                includesEffects = true;
                particles = new TheatreParticleTape();
                writer = new BinaryWriter(File.Open(file, FileMode.CreateNew));
                try
                {
                    writer.Write(Format); writer.Write(map); writer.Write(fps);
                    writer.Write(length); writer.Write(blocks.Length); writer.Write(ids.Length);
                    Stage = stage;
                    NetWire.Write(writer, stage);
                    foreach (string id in ids) writer.Write(id);
                    foreach (var actor in actors)
                    {
                        writer.Write(actor.ColorIndex);
                        writer.Write((uint)actor.Appearance.accessories);
                        writer.Write(CandyPlayerAvatar.StyleBits(actor.Appearance));
                    }
                }
                catch { writer.Dispose(); throw; }
            }
        }

        public void WriteFrame()
        {
            if (frames++ >= expected) throw new InvalidDataException("Too many motion samples.");
            writer.Write(JsonUtility.ToJson(Tracked.Waves.FirstOrDefault(w => w.Wave.active)?.Wave ?? default));
            foreach (var block in blocks)
            {
                bool active = block && block.gameObject.activeSelf;
                writer.Write(active);
                if (!active) continue;
                NetWire.Write(writer, block.position);
                NetWire.Write(writer, block.rotation);
            }
            foreach (var actor in actors)
                CharacterWire.WriteSurvivor(writer, actor.LastFrame.scale > 0 ? actor.LastFrame : actor.ReadFrame(), actor.ColorIndex);
            var effects = new Effects(MatchRuntime.Current?.Frame ?? new MatchFrame())
            {
                equipment = new EquipmentState[actors.Length],
                palette = new Color[actors.Length]
            };
            int equipped = 0;
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                var state = actor.GetComponent<IEquipmentControls>()?.Equipment?.Copy() ?? new EquipmentState();
                state.actor = ids[i];
                effects.equipment[i] = state;
                effects.palette[i] = actor.Rig ? actor.Rig.BaseAppearanceColor : Color.white;
                if (state.kind >= EquipmentKind.Blaster) equipped++;
            }
            PeakEquipped = Math.Max(PeakEquipped, equipped);
            writer.Write(JsonUtility.ToJson(effects));
            particles.Write(writer);
        }

        public void ReadFrame()
        {
            ReadFrame(true);
        }

        public void Seek(int frame)
        {
            if (frame < 0 || frame >= expected) throw new ArgumentOutOfRangeException(nameof(frame));
            if (frame < offsets.Count)
            {
                reader.BaseStream.Position = offsets[frame];
                frames = frame;
            }
            while (frames < frame) ReadFrame(false);
            ReadFrame(true);
        }

        void ReadFrame(bool apply)
        {
            if (frames >= expected) throw new InvalidDataException("Read beyond the requested take.");
            frames++;
            Wave = JsonUtility.FromJson<WaveFrame>(reader.ReadString());
            foreach (var block in blocks)
            {
                bool active = reader.ReadBoolean();
                if (!active)
                {
                    if (apply && block && block.gameObject.activeSelf) block.gameObject.SetActive(false);
                    continue;
                }
                var position = NetWire.ReadVector(reader);
                var rotation = NetWire.ReadRotation(reader);
                if (!apply) continue;
                if (!block) throw new InvalidDataException("A recorded block is missing from this map.");
                block.position = position; block.rotation = rotation;
                if (!block.gameObject.activeSelf) block.gameObject.SetActive(true);
            }
            foreach (var actor in actors)
            {
                CharacterWire.ReadSurvivor(reader, out var pose, out int color);
                if (!apply) continue;
                if (actor.ColorIndex != color) actor.SetColor(color);
                actor.transform.SetPositionAndRotation(pose.feet, pose.headRotation);
                actor.OverridePose(pose);
                actor.ApplyCosmeticFrame(pose);
            }
            if (includesEffects)
            {
                string state = reader.ReadString();
                if (apply) ReplayEffects(JsonUtility.FromJson<Effects>(state));
            }
            particles?.Read(reader, apply);
            if (offsets.Count == frames) offsets.Add(reader.BaseStream.Position);
        }

        void PrepareEffects()
        {
            replayRoot = new GameObject("Theatre recorded weapon effects");
            trails = replayRoot.AddComponent<CandyBulletTrails>();
            foreach (var view in UnityEngine.Object.FindObjectsByType<CandyPlasmaView>(FindObjectsSortMode.None)) Bind(view);
            foreach (var view in UnityEngine.Object.FindObjectsByType<CandyArsenalEffects>(FindObjectsSortMode.None)) Bind(view);
            for (int i = 0; i < actors.Length; i++)
                if (actors[i].Rig)
                    weapons.Add(ids[i], new CandyWeaponVisual(replayRoot.transform, actors[i].Rig.style.material));
        }

        // Invoke only the existing visual updates against the recorded state.
        void Bind(Behaviour view)
        {
            var update = view.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            if (update == null) throw new MissingMethodException(view.GetType().Name, "Update");
            replayViews.Add((Action)update.CreateDelegate(typeof(Action), view));
            if (view.enabled) { pausedViews.Add(view); view.enabled = false; }
        }

        void ReplayEffects(Effects recorded)
        {
            var frame = MatchRuntime.Current?.Frame;
            if (frame == null || recorded == null) return;
            var previous = new Effects(frame);
            try
            {
                recorded.Apply(frame);
                foreach (var update in replayViews) update();
                if (frames == 1)
                {
                    trailsCursor.Baseline(new MatchFrame { equipmentEpoch = frame.equipmentEpoch,
                        equipmentSequence = Math.Max(0, (frame.equipmentActions.FirstOrDefault()?.id ?? 1) - 1) });
                }
                trailsCursor.Read(frame, item =>
                {
                    if (item.action != EquipmentAction.Shot || item.endpoints == null || item.endpoints.Length == 0) return;
                    if (item.kind == EquipmentKind.NeedleDrill) trails.FireNeedles(item.position, item.endpoints);
                    else if (item.kind == EquipmentKind.DualRifle || item.kind == EquipmentKind.SheriffHammer ||
                        item.kind > EquipmentKind.PoweredArmor && !EquipmentSpecifications.Get(item.kind).melee &&
                        item.kind != EquipmentKind.WristLaser) trails.Fire(item.position, item.endpoints, item.shotgun);
                });
                int equipped = 0;
                for (int i = 0; i < actors.Length; i++)
                {
                    var actor = actors[i];
                    if (recorded.palette?.Length == actors.Length && actor.Rig)
                        actor.Rig.SetTint(recorded.palette[i]);
                    if (!weapons.TryGetValue(ids[i], out var weapon)) continue;
                    var state = frame.equipment.FirstOrDefault(item => item.actor == ids[i]);
                    weapon.Present(state, actor.LastFrame, true);
                    if (state != null && state.kind >= EquipmentKind.Blaster) equipped++;
                }
                PeakEquipped = Math.Max(PeakEquipped, equipped);
            }
            finally { previous.Apply(frame); }
        }

        public void Dispose()
        {
            if (reader != null) Tracked.Waves.Remove(this);
            foreach (var view in pausedViews) if (view) view.enabled = true;
            if (replayRoot) UnityEngine.Object.Destroy(replayRoot);
            particles?.Dispose();
            reader?.Dispose();
            writer?.Dispose();
        }
    }
}
