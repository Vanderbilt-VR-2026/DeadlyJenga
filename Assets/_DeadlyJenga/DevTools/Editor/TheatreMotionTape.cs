using System;
using System.IO;
using System.Linq;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CharacterNetwork;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    // A take stores observed poses. Playback never asks the physics solver to recreate a collapse.
    sealed class TheatreMotionTape : IDisposable, IWaveState
    {
        const int Format = 0x324A5444;
        readonly BinaryWriter writer;
        readonly BinaryReader reader;
        readonly Rigidbody[] blocks;
        readonly CandyPlayerAvatar[] actors;
        readonly int expected;
        int frames;
        public WaveFrame Wave { get; private set; }
        public Vector3 Stage { get; }

        public TheatreMotionTape(string file, bool replay, string map, int fps, int length, Vector3 stage,
            Rigidbody[] blocks, string[] ids, CandyPlayerAvatar[] actors)
        {
            this.blocks = blocks;
            this.actors = actors;
            expected = length;
            if (replay)
            {
                reader = new BinaryReader(File.OpenRead(file));
                try
                {
                    if (reader.ReadInt32() != Format || reader.ReadString() != map || reader.ReadInt32() != fps ||
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
                }
                catch { reader.Dispose(); throw; }
            }
            else
            {
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
                CharacterWire.WriteSurvivor(writer, actor.ReadFrame(), actor.ColorIndex);
        }

        public void ReadFrame()
        {
            if (frames++ >= expected) throw new InvalidDataException("Read beyond the requested take.");
            Wave = JsonUtility.FromJson<WaveFrame>(reader.ReadString());
            foreach (var block in blocks)
            {
                bool active = reader.ReadBoolean();
                if (!active)
                {
                    if (block && block.gameObject.activeSelf) block.gameObject.SetActive(false);
                    continue;
                }
                var position = NetWire.ReadVector(reader);
                var rotation = NetWire.ReadRotation(reader);
                if (!block) throw new InvalidDataException("A recorded block is missing from this map.");
                block.position = position; block.rotation = rotation;
                if (!block.gameObject.activeSelf) block.gameObject.SetActive(true);
            }
            foreach (var actor in actors)
            {
                CharacterWire.ReadSurvivor(reader, out var pose, out _);
                actor.transform.SetPositionAndRotation(pose.feet, pose.headRotation);
                actor.OverridePose(pose);
                actor.ApplyCosmeticFrame(pose);
            }
        }

        public void Dispose()
        {
            if (reader != null) Tracked.Waves.Remove(this);
            reader?.Dispose();
            writer?.Dispose();
        }
    }
}
