using System;
using System.IO;
using System.Linq;
using DeadlyJenga.CandyPlayers;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    public static partial class StudioAcceptance
    {
        static void MomentTests()
        {
            var set = new StudioSet { id = "test-moment", fps = 30, length = 2 };
            var actor = set.AddMark(StudioRole.Survivor, Vector3.zero);
            var tape = new StudioTape { header = new TapeHeader { setId = set.id, map = set.map, fps = set.fps } };
            var world = tape.GetOrAdd("world", WorldTrack.Id, "World");
            var body = tape.GetOrAdd(actor.id, SurvivorTrack.Id, actor.actor);
            var worldTake = world.AddTake(0, 1);
            var bodyTake = body.AddTake(8, 1);
            worldTake.frames.AddRange(new byte[55][]);
            bodyTake.frames.AddRange(new byte[42][]);
            bool rejected = false;
            try { StudioMoments.Create(set, tape, 7, actor, new StudioCameraKey()); }
            catch (InvalidOperationException) { rejected = true; }
            Result("A moment outside its actor coverage makes no changes",
                rejected && set.moments.Count == 0 && set.cameras.Count == 0, "Actor coverage starts at frame 8.");

            var moment = StudioMoments.Create(set, tape, 10, actor, new StudioCameraKey { position = new Vector3(4, 2, 3) });
            moment.name = "The first crack";
            var restored = JsonUtility.FromJson<StudioSet>(JsonUtility.ToJson(set));
            var saved = restored.moments[0];
            var pov = restored.cameras.Find(c => c.id == saved.pov);
            var witness = restored.cameras.Find(c => c.id == saved.witness);
            Result("Named select persists its frame, cameras and exact active takes",
                saved.name == moment.name && saved.frame == 10 && saved.first == 8 && saved.end == 50 &&
                saved.source.Matches(tape) && pov?.subject == actor.id && witness?.keys[0].position == new Vector3(4, 2, 3),
                "The review range is clamped to the world and actor take coverage.");

            witness.speeds.Add(new StudioSpeedMark { time = 0, speed = .5f });
            witness.freezeAt = .3f;
            witness.freezeFor = .2f;
            var reference = StudioMoments.ReviewSamples(saved, restored.fps, false);
            var proxy = StudioMoments.ReviewSamples(saved, 12, true);
            Result("POV and witness reviews each sample the same exact tape frame",
                reference.Count == 1 && reference[0].tape == 10f / 30 && reference[0].camera == reference[0].tape &&
                proxy.Count == 17 && proxy[0].tape == 8f / 30 && proxy[proxy.Count - 1].tape < 50f / 30,
                "One shared camera-independent sample list feeds both angles and both modes.");

            witness.start = 0;
            bool added = StudioMoments.AddToCut(restored, saved, witness);
            var timeline = StudioCameras.Timeline(witness, restored, restored.render.fps);
            var cut = added ? restored.cuts[0] : null;
            int index = cut != null ? Mathf.RoundToInt(cut.from * restored.render.fps) : 0;
            Result("Sending a select to Cut translates tape handles through the chosen camera's retiming",
                cut != null && cut.camera == witness.id && index > 0 && timeline[index].tape >= saved.first / 30f - .0001f &&
                timeline[index - 1].tape < saved.first / 30f - .0001f, "Cut points are measured in rendered shot seconds.");

            world.AddTake(0, 1);
            Result("Changing the world take invalidates the saved select's source", !saved.source.Matches(tape),
                "The map and frame are the same but the source take differs.");

            string look = StudioMoments.SettingsKey(restored);
            restored.marks[0].equipment = "Changed wardrobe equipment";
            bool wardrobeChangesKey = look != StudioMoments.SettingsKey(restored);
            string updatedLook = StudioMoments.SettingsKey(restored);
            restored.render.width = 640;
            Result("Proxy cache changes with wardrobe and render settings",
                wardrobeChangesKey && updatedLook != StudioMoments.SettingsKey(restored), "Take IDs alone cannot validate a finished preview.");

            var obscured = new StudioFraming.Sample { onScreen = true, occluded = .9f, height = .35f, facing = 0 };
            var clear = new StudioFraming.Sample { onScreen = true, occluded = .05f, height = .35f, facing = 30 };
            var tinyAndBack = new StudioFraming.Sample { onScreen = true, occluded = .05f, height = .03f, facing = 150 };
            Result("Witness suggestion prefers a readable view of the actor",
                StudioMoments.AngleScore(obscured) == 0 && StudioMoments.AngleScore(clear) > StudioMoments.AngleScore(tinyAndBack),
                "Heavy obstruction is rejected, and size and facing decide between similarly clear views.");

            string bookmarkDir = Path.Combine(Folder, "moment-bookmarks");
            Directory.CreateDirectory(bookmarkDir);
            tape.header.frames = 55;
            string bookmarkFile = Path.Combine(bookmarkDir, GameplayTheatre.BookmarksFile);
            File.WriteAllText(bookmarkFile, JsonUtility.ToJson(new TheatreBookmarks
            {
                fps = tape.header.fps,
                marks = { new TheatreBookmark { frame = 54, name = "Last frame" }, new TheatreBookmark { frame = 3, name = "First beat" },
                    new TheatreBookmark { frame = 55, name = "Past the tape" } }
            }));
            var bookmarks = TheatreImport.ReadBookmarks(bookmarkDir, tape);
            tape.header.fps = 24;
            Result("Theatre imports bookmarks at exact valid frames and rejects a mismatched frame rate",
                bookmarks.Count == 2 && bookmarks[0].frame == 3 && bookmarks[1].frame == 54 &&
                TheatreImport.ReadBookmarks(bookmarkDir, tape).Count == 0,
                "A bookmark at the exclusive tape end is also excluded.");

            LookbackTests(bookmarkDir);
        }

        static void LookbackTests(string folder)
        {
            var bounded = new TheatreLookbackFrames(3, 5);
            bounded.Add(new byte[] { 1, 2, 3 });
            bounded.Add(new byte[] { 4, 5, 6 });
            bounded.Add(new byte[] { 7, 8 });
            bool rejectsOversize = !bounded.Add(new byte[6]);
            using (var memory = new MemoryStream())
            using (var output = new BinaryWriter(memory))
            {
                bounded.Write(output);
                Result("Look-back evicts whole oldest frames within its byte budget",
                    rejectsOversize && bounded.Count == 2 && memory.ToArray().SequenceEqual(new byte[] { 4, 5, 6, 7, 8 }),
                    "A sample larger than the entire budget leaves the current buffer intact.");
            }

            var fixture = new GameObject("Theatre look-back baseline");
            try
            {
                var block = fixture.AddComponent<Rigidbody>();
                block.isKinematic = true;
                var recent = new TheatreLookbackFrames(2, 1024 * 1024);
                var positions = new[] { Vector3.zero, Vector3.one, Vector3.one * 2 };
                using (var recorder = new TheatreMotionTape(Vector3.zero, new[] { block }, Array.Empty<string>(), Array.Empty<CandyPlayerAvatar>()))
                {
                    foreach (var position in positions)
                    {
                        block.position = position;
                        using (var memory = new MemoryStream())
                        using (var writer = new BinaryWriter(memory))
                        {
                            recorder.WriteSample(writer);
                            recent.Add(memory.ToArray());
                        }
                    }
                    string file = Path.Combine(folder, "lookback-baseline.djt");
                    using (var writer = new BinaryWriter(File.Create(file)))
                    {
                        recorder.WriteHeader(writer, "castle", TheatreLookback.Fps, recent.Count);
                        recent.Write(writer);
                    }
                    var imported = TheatreImport.ConvertTape(file);
                    var world = imported.FindType(WorldTrack.Id).ActiveTake;
                    var expected = new WorldTrack.Encoder(1);
                    expected.Set(0, WorldTrack.Active, positions[1], Quaternion.identity);
                    Result("A saved look-back starts with the first retained world baseline",
                        imported.header.frames == 2 && world.frames.Count == 2 && world.frames[0].SequenceEqual(expected.Frame(true)),
                        "The actual Theatre recorder wrote three observed rigidbody poses and imported the last two as a fresh tape.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(fixture); }
        }
    }
}
