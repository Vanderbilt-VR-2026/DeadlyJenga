using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    public static partial class StudioAcceptance
    {
        static IEnumerator<int> CrowdVisualTests()
        {
            const int Count = 32;
            const string Group = "Viaduct extras";
            var stage = StudioRun.Stage;
            var set = stage.set;
            var originalTape = stage.tape;
            var originalMarks = set.marks;
            var originalCues = set.cues;
            float originalLength = set.length;
            int originalFrame = StudioRun.Frame;
            string originalActor = StudioPerform.Actor;
            var originalTarget = StudioCamera.Target;
            float originalPitch = StudioCamera.Pitch, originalYaw = StudioCamera.Yaw, originalDistance = StudioCamera.Distance;
            int originalWidth = StudioCamera.Image ? StudioCamera.Image.width : 640;
            int originalHeight = StudioCamera.Image ? StudioCamera.Image.height : 360;
            var tape = new StudioTape { header = new TapeHeader
            {
                setId = set.id, map = set.map, fps = set.fps, stage = stage.anchor, blocks = stage.blocks.Length
            } };
            string folder = Path.Combine(Folder, "crowd-32");
            Directory.CreateDirectory(folder);

            try
            {
                set.marks = new List<StudioMark>();
                set.cues = new List<StudioCue>();
                set.length = 2.6f;
                tape.header.frames = set.Frames;
                Vector3 center = CrowdFloor(stage);
                var source = set.AddMark(StudioRole.Survivor, center);
                source.actor = "Crowd movement reference";
                StudioRun.UseTape(tape);
                StudioRun.RecordWholeSet();
                foreach (int wait in UntilHeld()) yield return wait;

                var motor = stage.session.Survivor();
                var priorInput = motor.InputDriver;
                bool priorDriven = motor.externallyDriven;
                var performer = new ScriptedPerformer
                {
                    script = time => new CastleBotCommand { move = time > .3f && time < 1.1f ? Vector3.forward * .33f : Vector3.zero, look = Vector3.forward }
                };
                motor.externallyDriven = true;
                motor.InputDriver = performer;
                motor.RefreshMode();
                try
                {
                    var pass = new StudioPass { label = "Crowd movement reference", performer = source.id };
                    pass.record.Add(source.id);
                    StudioRun.Record(pass);
                    foreach (int wait in UntilHeld()) yield return wait;
                }
                finally
                {
                    motor.externallyDriven = priorDriven;
                    motor.InputDriver = priorInput;
                    motor.RefreshMode();
                }
                StudioRun.LeaveBody();
                StudioRun.Scrub(0);

                var options = new StudioTakes.CrowdOptions
                {
                    group = Group, count = Count, seed = 123, radius = 8, spacing = 1.05f, timeSpread = .45f,
                    ground = stage.GroundAt
                };
                var crowd = StudioTakes.Crowd(set, tape, new List<StudioMark> { source }, options, out var placementNotes);
                Result("32 performers placed on the live map", crowd.Count == Count,
                    $"{crowd.Count}/{Count} on {set.map}. {string.Join(" ", placementNotes)}");
                if (crowd.Count != Count) yield break;

                var plainPages = StudioWardrobeSheet.BuildPages(set, Group);
                bool defaultPaging = plainPages.Count == 4 && plainPages.All(page => page.Length == 8) &&
                    plainPages.SelectMany(page => page).Distinct().Count() == Count;
                var hero = crowd[Count / 2];
                hero.actor = "Protected hero";
                hero.heroLocked = true;
                hero.customLook = true;
                hero.appearance = new AvatarAppearance { hair = AvatarHair.Spikes, clothing = AvatarClothing.SailorShirt };
                var heroTrack = tape.Find(hero.id);
                heroTrack.locked = true;
                var approved = heroTrack.ActiveTake;
                var reference = tape.Find(source.id).ActiveTake;
                var world = tape.FindType(WorldTrack.Id).ActiveTake;
                int changed = StudioTakes.RetakeCrowd(set, tape, Group, .12f, 806);
                var pages = StudioWardrobeSheet.BuildPages(set, Group);
                bool heroPinned = pages.Count == 5 && pages.All(page => page[0] == hero) &&
                    pages.SelectMany(page => page.Skip(1)).Distinct().Count() == Count - 1;
                Result("Protected hero survives retakes and stays pinned on wardrobe pages", changed == Count - 1 &&
                    heroTrack.ActiveTake == approved && tape.Find(source.id).ActiveTake == reference && defaultPaging && heroPinned &&
                    crowd.Where(mark => mark != hero).All(mark => StudioTakes.WorldOf(tape.Find(mark.id).ActiveTake) == world.id),
                    $"{changed} background retakes on world {world.id}. Default pages {plainPages.Count}. Protected-hero pages {pages.Count}.");

                StudioRun.Refresh();
                StudioRun.ShowExact(set.fps);
                yield return 0;
                var poses = crowd.Where(mark => stage.playback.TryGetValue(mark.id, out var body) && body && body.activeInHierarchy)
                    .Select(mark => ActorFrame.Read(tape.Find(mark.id).type,
                        tape.Find(mark.id).ActiveTake.frames[set.fps - tape.Find(mark.id).ActiveTake.start]).Position).ToArray();
                var bounds = new Bounds(stage.ToWorld(center), Vector3.zero);
                foreach (var pose in poses) bounds.Encapsulate(pose);
                StudioCamera.Resize(1280, 720);
                StudioCamera.Target = bounds.center + Vector3.up * .5f;
                StudioCamera.Pitch = 61;
                StudioCamera.Yaw = 28;
                StudioCamera.Distance = Mathf.Max(18, Mathf.Max(bounds.size.x, bounds.size.z) * 1.4f);
                StudioCamera.Place();
                string wide = Path.Combine(folder, "01-stage-32-performers.png");
                StudioCamera.SavePng(wide);
                int framed = poses.Count(p =>
                {
                    Vector3 v = StudioCamera.Camera.WorldToViewportPoint(p + Vector3.up * .9f);
                    return v.z > 0 && v.x > .03f && v.x < .97f && v.y > .03f && v.y < .97f;
                });
                Result("32 performer bodies on stage and framed in the PNG", poses.Length == Count && framed == Count && File.Exists(wide),
                    $"{poses.Length} active replay bodies, {framed} in camera frame. crowd-32/{Path.GetFileName(wide)}");

                var rig = Resources.Load<CandyPlayerRig>("CandyPlayerVisual");
                var contactPages = new[]
                {
                    (name: "02-wardrobe-default-page-1", marks: plainPages[0]),
                    (name: "03-wardrobe-protected-page-1", marks: pages[0]),
                    (name: "04-wardrobe-protected-page-2", marks: pages[1])
                };
                var sheetNotes = new List<string>();
                bool pictures = rig != null;
                foreach (var contact in contactPages)
                {
                    string png = Path.Combine(folder, contact.name + ".png");
                    int valid = rig ? CaptureWardrobePage(png, contact.marks, tape, rig) : 0;
                    pictures &= valid == contact.marks.Length;
                    sheetNotes.Add($"{contact.name}: {valid}/{contact.marks.Length} visible portraits. " +
                        string.Join(", ", contact.marks.Select(mark => mark.actor)));
                    yield return 0;
                }
                File.WriteAllLines(Path.Combine(folder, "wardrobe-roster.txt"), sheetNotes);
                Result("Real wardrobe renderer draws every portrait on default and protected pages", pictures,
                    string.Join(" ", sheetNotes));
            }
            finally
            {
                set.marks = originalMarks;
                set.cues = originalCues;
                set.length = originalLength;
                StudioPerform.Actor = originalActor;
                if (StudioRun.State == StudioState.Held)
                {
                    if (!string.IsNullOrEmpty(stage.performer)) StudioRun.LeaveBody();
                    StudioRun.UseTape(originalTape);
                    StudioRun.Scrub(originalFrame);
                    StudioRun.SaveTape();
                    StudioCamera.Target = originalTarget;
                    StudioCamera.Pitch = originalPitch;
                    StudioCamera.Yaw = originalYaw;
                    StudioCamera.Distance = originalDistance;
                    StudioCamera.Resize(originalWidth, originalHeight);
                    StudioCamera.Place();
                }
            }
        }

        static Vector3 CrowdFloor(StudioStage stage)
        {
            Vector3 best = stage.anchor;
            int bestCount = -1;
            foreach (Vector3 origin in new[] { stage.anchor, stage.world.SafeSpawnPoint() })
                for (int z = -2; z <= 2; z++)
                    for (int x = -2; x <= 2; x++)
                    {
                        Vector3 test = origin + new Vector3(x * 4, 0, z * 4);
                        if (!StudioStage.SurfaceBelow(test, out var floor) || floor.y <= stage.world.WaterHeight(floor) + .1f) continue;
                        Vector3 center = stage.ToStage(floor);
                        int count = 0;
                        for (int iz = -4; iz <= 4; iz++)
                            for (int ix = -4; ix <= 4; ix++)
                            {
                                float? y = stage.GroundAt(center + new Vector3(ix * 1.5f, 0, iz * 1.5f));
                                if (y.HasValue && Mathf.Abs(y.Value - center.y) < .45f) count++;
                            }
                        if (count <= bestCount) continue;
                        bestCount = count;
                        best = floor;
                    }
            return stage.ToStage(best);
        }

        static int CaptureWardrobePage(string path, StudioMark[] marks, StudioTape tape, CandyPlayerRig rig)
        {
            const int Width = 150, Height = 160, Gap = 8, Columns = 4;
            int sheetWidth = Columns * Width + (Columns + 1) * Gap;
            int sheetHeight = 2 * Height + 3 * Gap;
            var canvas = new Texture2D(sheetWidth, sheetHeight, TextureFormat.RGB24, false);
            int visible = 0;
            try
            {
                var background = Enumerable.Repeat(new Color32(24, 28, 37, 255), sheetWidth * sheetHeight).ToArray();
                canvas.SetPixels32(background);
                for (int i = 0; i < marks.Length; i++)
                {
                    var wardrobe = StudioWardrobeSheet.Appearance(marks[i], tape);
                    var portrait = StudioWardrobeSheet.RenderPortrait(rig, wardrobe.look, wardrobe.color, null);
                    if (!portrait) continue;
                    try
                    {
                        Color32[] pixels = portrait.GetPixels32();
                        if (portrait.width != Width || portrait.height != Height) continue;
                        var edge = pixels[0];
                        int changed = pixels.Count(p => Mathf.Abs(p.r - edge.r) + Mathf.Abs(p.g - edge.g) + Mathf.Abs(p.b - edge.b) > 40);
                        if (changed > pixels.Length / 30 && changed < pixels.Length * .9f) visible++;
                        int left = Gap + i % Columns * (Width + Gap);
                        int bottom = sheetHeight - Gap - Height - i / Columns * (Height + Gap);
                        canvas.SetPixels32(left, bottom, Width, Height, pixels);
                    }
                    finally { Object.DestroyImmediate(portrait); }
                }
                canvas.Apply(false);
                File.WriteAllBytes(path, canvas.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(canvas); }
            return visible;
        }
    }
}
