using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CharacterNetwork;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Brings Gameplay Theatre work into the Studio: a recipe becomes a Set, a v2-v4 motion tape becomes a v5 tape.
    public static class TheatreImport
    {
        // Cast become marks, cues become timeline cues, shots are kept for Phase 5.
        public static StudioSet RecipeToSet(TheatrePlan plan)
        {
            var shots = plan.shots ?? Array.Empty<TheatrePlan.TheatreShot>();
            var set = new StudioSet
            {
                title = string.IsNullOrWhiteSpace(plan.title) ? "Imported Theatre recipe" : plan.title,
                map = plan.map, fps = Mathf.Clamp(plan.fps, 12, 60), seed = plan.seed, warmup = plan.warmup,
                useMapAnchor = plan.useMapAnchor, mapAnchor = plan.mapAnchor, stageOffset = plan.stageOffset,
                length = Mathf.Max(1, shots.Length == 0 ? 6 : shots.Max(s => s.start + s.duration)),
                notes = "Imported from a Gameplay Theatre recipe."
            };
            foreach (var actor in plan.cast ?? Array.Empty<TheatrePlan.TheatreActor>())
            {
                if (actor == null) continue;
                set.marks.Add(new StudioMark
                {
                    id = set.UniqueId(actor.id), actor = actor.id, color = actor.color, position = actor.start,
                    // Theatre's scripted moves are not performances; a live bot is the closest stand-in until someone performs it.
                    role = actor.live ? StudioRole.Bot : StudioRole.Survivor,
                    customLook = actor.customLook, appearance = actor.appearance
                });
                if ((actor.moves?.Length ?? 0) + (actor.performance?.Length ?? 0) + (actor.stunt?.Length ?? 0) > 0)
                    set.shotNotes.Add($"{actor.id} had scripted motion in Theatre. Perform this actor in the Studio (Perform mode).");
            }
            foreach (var cue in plan.cues ?? Array.Empty<TheatrePlan.TheatreCue>())
                if (cue != null)
                    set.cues.Add(new StudioCue { at = cue.at, action = cue.action, actor = cue.actor ?? "", target = cue.target ?? "",
                        hazard = cue.hazard ?? "", weapon = cue.weapon ?? "", strength = cue.strength, heading = cue.heading,
                        height = cue.height, optional = cue.optional, lethal = cue.lethal });
            foreach (var shot in shots)
                if (shot != null)
                    set.shots.Add(new StudioShot { name = shot.name, subject = shot.subject ?? "", second = shot.second ?? "",
                        start = shot.start, duration = shot.duration, fov = shot.fov, roll = shot.roll, avoidWalls = shot.avoidWalls,
                        autoFocus = shot.autoFocus, firstPerson = shot.firstPerson, from = shot.from, to = shot.to, look = shot.look });
            return set;
        }

        // One click: pick a finished Theatre take folder (recipe.json + motion.djt). Returns the new Set.
        public static StudioSet ImportTakeFolder(string folder)
        {
            string recipe = Path.Combine(folder, "recipe.json"), motion = Path.Combine(folder, "motion.djt");
            if (!File.Exists(recipe)) throw new FileNotFoundException("This folder has no recipe.json.");
            var plan = JsonUtility.FromJson<TheatrePlan>(File.ReadAllText(recipe));
            if (plan == null) throw new InvalidDataException("The recipe could not be read.");
            var set = RecipeToSet(plan);
            StudioLibrary.Save(set);
            if (File.Exists(motion))
            {
                var tape = ConvertTape(motion, set);
                tape.Save(StudioLibrary.TapePath(set.id));
                StudioLibrary.Save(set);
            }
            return set;
        }

        const int FormatV2 = 0x324A5444, FormatV3 = 0x334A5444, FormatV4 = 0x344A5444;

        // Reads an old Theatre motion tape and writes the same motion as v5 tracks.
        // set (optional) gets its length, frame rate and any missing marks filled in from the tape.
        public static StudioTape ConvertTape(string file, StudioSet set = null)
        {
            var tape = new StudioTape();
            using (var r = new BinaryReader(File.OpenRead(file)))
            {
                int format = r.ReadInt32();
                if (format != FormatV2 && format != FormatV3 && format != FormatV4)
                    throw new InvalidDataException("Not a Gameplay Theatre motion tape.");
                bool effects = format != FormatV2, particles = format == FormatV4;
                string map = r.ReadString();
                int fps = r.ReadInt32(), length = r.ReadInt32(), blocks = r.ReadInt32(), cast = r.ReadInt32();
                if (blocks < 0 || blocks > 100000 || cast < 0 || cast > 64) throw new InvalidDataException("Damaged Theatre tape header.");
                Vector3 stage = NetWire.ReadVector(r);
                var ids = new string[cast];
                for (int i = 0; i < cast; i++) ids[i] = r.ReadString();
                var colors = new int[cast];
                var looks = new AvatarAppearance[cast];
                for (int i = 0; i < cast; i++)
                {
                    colors[i] = r.ReadInt32();
                    looks[i] = CandyPlayerAvatar.WithStyle(new AvatarAppearance { accessories = (AvatarAccessory)r.ReadUInt32() }, r.ReadUInt32());
                }

                tape.header = new TapeHeader { map = map, fps = fps, stage = stage, blocks = blocks,
                    created = DateTime.UtcNow.ToString("o"), notes = "Converted from Gameplay Theatre: " + Path.GetFileName(Path.GetDirectoryName(file)) };
                var world = tape.GetOrAdd("world", WorldTrack.Id, "World").AddTake(0, Mathf.Max(1, fps));
                var hazards = tape.GetOrAdd("hazards", HazardTrack.Id, "Hazards").AddTake(0, 1);
                var fx = effects ? tape.GetOrAdd("effects", EffectsTrack.Id, "Effects").AddTake(0, 1) : null;
                var actors = new StudioTake[cast];
                for (int i = 0; i < cast; i++)
                    actors[i] = tape.GetOrAdd(ids[i], SurvivorTrack.Id, ids[i]).AddTake(0, 1);
                var encoder = new WorldTrack.Encoder(blocks);
                world.meta = encoder.Meta;

                int frame = 0;
                Vector3[] firstFeet = new Vector3[cast];
                while (r.BaseStream.Position < r.BaseStream.Length && frame < length)
                {
                    hazards.frames.Add(HazardTrack.Pack(JsonUtility.FromJson<WaveFrame>(r.ReadString())));
                    for (int b = 0; b < blocks; b++)
                    {
                        if (!r.ReadBoolean()) { encoder.Set(b, 0, default, Quaternion.identity); continue; }
                        Vector3 position = NetWire.ReadVector(r);
                        encoder.Set(b, WorldTrack.Active, position, NetWire.ReadRotation(r));
                    }
                    world.frames.Add(encoder.Frame(frame % world.keyEvery == 0));   // take starts at tape frame 0
                    for (int i = 0; i < cast; i++)
                    {
                        CharacterWire.ReadSurvivor(r, out CandyAvatarFrame pose, out int color);
                        if (frame == 0) firstFeet[i] = pose.feet;
                        using (var memory = new MemoryStream())
                        using (var w = new BinaryWriter(memory))
                        {
                            // Write the pose again in the full form, which carries the outfit.
                            CharacterWire.WriteSurvivor(w, pose, color, looks[i]);
                            w.Flush();
                            actors[i].frames.Add(ActorTrack.Pack(true, memory.ToArray(), 100, ""));
                        }
                    }
                    if (effects)
                    {
                        string weapons = r.ReadString();
                        byte[] cloud = particles ? CopyParticles(r) : BitConverter.GetBytes(0);
                        fx.frames.Add(EffectsTrack.Pack(weapons, w => w.Write(cloud)));
                    }
                    frame++;
                }
                tape.header.frames = frame;

                if (set != null)
                {
                    set.fps = fps; set.length = frame / (float)fps; set.map = map;
                    tape.header.setId = set.id;
                    for (int i = 0; i < cast; i++)
                        if (set.Mark(ids[i]) == null)
                            set.marks.Add(new StudioMark { id = ids[i], actor = ids[i], role = StudioRole.Survivor, color = colors[i] & 0xFF,
                                position = firstFeet[i] - stage, customLook = true, appearance = looks[i] });
                }
            }
            return tape;
        }

        // Particle samples are stored the same way in v4 and v5; copy the bytes without changing them.
        static byte[] CopyParticles(BinaryReader r)
        {
            using (var memory = new MemoryStream())
            using (var w = new BinaryWriter(memory))
            {
                int systems = r.ReadInt32();
                if (systems < 0 || systems > 64) throw new InvalidDataException("Damaged particle data in Theatre tape.");
                w.Write(systems);
                for (int s = 0; s < systems; s++)
                {
                    w.Write(r.ReadInt32()); w.Write(r.ReadInt32());
                    w.Write(r.ReadString()); w.Write(r.ReadString()); w.Write(r.ReadString());
                    int count = r.ReadInt32();
                    if (count < 0 || count > 8192) throw new InvalidDataException("Damaged particle data in Theatre tape.");
                    w.Write(count);
                    w.Write(r.ReadBytes(count * 52));
                }
                w.Flush();
                return memory.ToArray();
            }
        }
    }
}
