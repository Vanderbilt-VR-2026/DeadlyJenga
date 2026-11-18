using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The Set tab: build the scene. Pick the map, place marks, add cues.
    public sealed class SetMode : IStudioMode
    {
        public string Title => "Set";
        public int Order => 10;
        public bool Available => true;
        public bool TryGetRecord(StudioContext context, out string label, out Action start) { label = null; start = null; return false; }

        public void Build(VisualElement panel, StudioContext context)
        {
            panel.Add(StudioUi.Heading("BUILD THE SET"));
            var set = context.Set;
            if (set == null) { panel.Add(StudioUi.Hint("Choose or make a Set in the top-left corner.")); return; }

            bool held = StudioRun.State == StudioState.Held;
            if (!context.StageOpen) panel.Add(StudioUi.Hint("Open the stage to place marks on the map. You can still change settings here."));
            else if (set.marks.Count == 0) panel.Add(StudioUi.Hint("Place a mark to add your first actor."));

            void Place(StudioRole role)
            {
                if (!held)
                {
                    // Stage closed: add the mark near the middle; it can be dragged into place later.
                    StudioMark made = null;
                    context.Edit("Add " + role.ToString().ToLowerInvariant(),
                        () => made = set.AddMark(role, new Vector3(set.marks.Count * 1.2f, role == StudioRole.Hand ? 6 : 0, 0)), true);
                    context.Select(made);
                    return;
                }
                context.window.Viewport.Placing = role;
                context.window.Viewport.Rebuild();
                context.Say("Click in the viewport where the new mark should stand.");
            }
            panel.Add(StudioUi.Big("+  Add survivor", () => Place(StudioRole.Survivor), "An actor you perform yourself (Perform mode). Until then a stand-in holds the mark."));
            panel.Add(StudioUi.Big("+  Add hand", () => Place(StudioRole.Hand), "The giant hand."));
            panel.Add(StudioUi.Big("+  Add bot", () => Place(StudioRole.Bot), "Background life: a survivor run by the game's normal bot brain."));
            panel.Add(StudioUi.Big("+  Add cue at " + StudioUi.Time(context.Frame, set.fps), () =>
            {
                var menu = new GenericMenu();
                foreach (var type in StudioCues.All)
                {
                    var captured = type;
                    menu.AddItem(new GUIContent(type.Label), false, () =>
                    {
                        StudioCue made = null;
                        context.Edit("Add " + captured.Label.ToLowerInvariant(), () =>
                        {
                            made = new StudioCue { action = captured.Action, at = Mathf.Min(context.Frame / (float)set.fps, set.length - .1f) };
                            if (captured.Action == "hazard") made.hazard = SetLists.Hazards.FirstOrDefault() ?? "";
                            set.cues.Add(made);
                        });
                        context.Select(made);
                    });
                }
                menu.ShowAsContext();
            }, "Something the world does at this time: a collapse, a disaster."));

            foreach (string problem in set.Problems()) panel.Add(StudioUi.Warning(problem));
        }
    }

    // Lists the inspectors offer. Loaded once per script reload.
    public static class SetLists
    {
        static List<string> mapIds, mapNames, hazards;

        static void LoadMaps()
        {
            if (mapIds != null) return;
            mapIds = new List<string>(); mapNames = new List<string>();
            // In Play Mode the game's own connection object can exist without a catalog, so read the asset directly.
            var source = Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");
            if (!source) return;
            var catalog = UnityEngine.Object.Instantiate(source);
            try
            {
                catalog.EnsureModeMaps();
                foreach (var map in catalog.maps.Where(SpectatorRecordingPlan.MapAvailable)) { mapIds.Add(map.id); mapNames.Add(map.title + "  (" + map.id + ")"); }
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }

        public static List<string> MapIds { get { LoadMaps(); return mapIds; } }
        public static List<string> MapNames { get { LoadMaps(); return mapNames; } }

        public static List<string> Hazards
        {
            get
            {
                if (hazards == null)
                {
                    try { hazards = DemoDisasters.Prefabs.Where(p => p).Select(p => p.name).OrderBy(n => n).ToList(); }
                    catch { hazards = new List<string>(); }
                }
                return hazards;
            }
        }

        public static readonly List<string> Equipment = new[] { "None" }.Concat(Enum.GetNames(typeof(EquipmentKind)).Where(n => n != "None")).ToList();
    }

    // Nothing selected: the Set's own settings.
    public sealed class SetInspector : IStudioInspector
    {
        public bool Handles(object selection, StudioContext context) => selection == null && context.Set != null;

        public void Build(VisualElement panel, object selection, StudioContext context)
        {
            var set = context.Set;
            bool closed = StudioRun.State == StudioState.Closed;
            panel.Add(StudioFields.Text(context, "Name", set.title, v => set.title = string.IsNullOrWhiteSpace(v) ? set.title : v));
            var map = StudioFields.Choice(context, "Map", SetLists.MapNames, SetLists.MapIds.IndexOf(set.map), i => { if (i >= 0 && i < SetLists.MapIds.Count) set.map = SetLists.MapIds[i]; });
            map.SetEnabled(closed);
            map.tooltip = closed ? "" : "Close the stage to change the map.";
            panel.Add(map);
            var length = StudioFields.Number(context, "Length (s)", set.length, v => set.length = Mathf.Clamp(v, .5f, 120));
            var rates = new List<string> { "24", "25", "30", "50", "60" };
            var rate = StudioFields.Choice(context, "Frame rate", rates, rates.IndexOf(set.fps.ToString()), i => { if (i >= 0) set.fps = int.Parse(rates[i]); });
            bool recorded = context.Tape != null && context.Tape.tracks.Any(t => t.takes.Count > 0);
            rate.SetEnabled(closed && !recorded);
            rate.tooltip = recorded ? "The tape was recorded at this rate." : closed ? "" : "Close the stage to change the frame rate.";
            length.SetEnabled(closed);
            length.tooltip = closed ? "" : "Close the stage to change the length.";
            panel.Add(length); panel.Add(rate);
            panel.Add(StudioFields.Text(context, "Notes", set.notes, v => set.notes = v, false, true));

            if (set.shotNotes.Count > 0)
            {
                var notes = StudioUi.Card(StudioUi.Hint("Shot list"));
                foreach (string note in set.shotNotes) notes.Add(StudioUi.Hint("• " + note));
                panel.Add(notes);
            }

            // Thumbnail
            string thumbnail = string.IsNullOrEmpty(set.thumbnail) ? "" : Path.Combine(StudioLibrary.ProjectRoot, set.thumbnail);
            if (File.Exists(thumbnail))
            {
                var picture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                picture.LoadImage(File.ReadAllBytes(thumbnail));
                var image = new Image { image = picture, scaleMode = ScaleMode.ScaleAndCrop };
                image.AddToClassList("ts-thumb");
                image.RegisterCallback<DetachFromPanelEvent>(_ => UnityEngine.Object.DestroyImmediate(picture));
                panel.Add(image);
            }
            var snap = StudioUi.Small("Use this view as the thumbnail", () =>
            {
                string relative = Path.Combine("Recordings", "TrailerStudio", set.id, "thumbnail.png");
                StudioCamera.SavePng(Path.Combine(StudioLibrary.ProjectRoot, relative));
                context.Edit("Set thumbnail", () => set.thumbnail = relative.Replace('\\', '/'));
            });
            snap.SetEnabled(context.StageOpen);
            panel.Add(snap);

            // Tape facts
            var tape = context.Tape;
            if (tape != null && recorded)
            {
                long bytes = File.Exists(tape.path) ? new FileInfo(tape.path).Length : tape.Bytes;
                panel.Add(StudioUi.Card(StudioUi.Hint($"Tape: {StudioTape.Size(bytes)} on disk, {tape.tracks.Count(t => t.takes.Count > 0)} recorded tracks, " +
                    $"{tape.tracks.Sum(t => t.Kept.Count())} takes kept.")));
            }

            // Starting world state
            var others = StudioLibrary.SetIds().Where(id => id != set.id).ToList();
            var startNames = new List<string> { "As the map loads" }.Concat(others.Select(id => "End of: " + (StudioLibrary.Load(id)?.title ?? id))).ToList();
            var start = StudioFields.Choice(context, "World starts", startNames, others.IndexOf(set.startFromSet) + 1,
                i => set.startFromSet = i <= 0 ? "" : others[i - 1]);
            start.SetEnabled(closed);
            var startFrame = StudioFields.Whole(context, "From frame", set.startFromFrame, v => set.startFromFrame = Mathf.Max(0, v));
            startFrame.tooltip = "Tape frame of the other Set to start from. Use a large number for its last frame.";
            startFrame.SetEnabled(closed && !string.IsNullOrEmpty(set.startFromSet));

            // Time of day and weather: the Studio moves the scene's own sun and fog while the stage is open.
            var ownTime = StudioFields.Switch(context, "Set time of day", set.timeOfDay >= 0, v => set.timeOfDay = v ? 12 : -1, true);
            var hour = StudioFields.Slide(context, "Hour", Mathf.Max(0, set.timeOfDay), 0, 24, v => { set.timeOfDay = v; if (context.StageOpen) StudioEnvironment.Apply(set); });
            hour.SetEnabled(set.timeOfDay >= 0);
            var weather = StudioFields.Choice(context, "Weather", StudioEnvironment.Weathers, Mathf.Max(0, StudioEnvironment.Weathers.IndexOf(set.weather)),
                i => set.weather = i <= 0 ? "" : StudioEnvironment.Weathers[i], true);
            var sky = StudioUi.Hint("The Studio moves the map's sun and fog itself. A sky that paints its own sun or clouds will not follow, and water level cannot be changed.");
            panel.Add(StudioUi.Advanced(
                start, startFrame,
                StudioFields.Whole(context, "Random seed", set.seed, v => set.seed = v),
                StudioFields.Number(context, "Settle time (s)", set.warmup, v => set.warmup = Mathf.Clamp(v, 0, 10)),
                StudioFields.Switch(context, "Fixed stage spot", set.useMapAnchor, v => set.useMapAnchor = v),
                StudioFields.Point(context, "Spot from map centre", set.mapAnchor, v => set.mapAnchor = v),
                StudioFields.Point(context, "Stage nudge", set.stageOffset, v => set.stageOffset = v),
                ownTime, hour, weather, sky));
        }
    }

    public sealed class MarkInspector : IStudioInspector
    {
        public bool Handles(object selection, StudioContext context) => selection is StudioMark;

        public void Build(VisualElement panel, object selection, StudioContext context)
        {
            var mark = (StudioMark)selection;
            var set = context.Set;
            panel.Add(StudioFields.Text(context, "Name", mark.actor, v => mark.actor = string.IsNullOrWhiteSpace(v) ? mark.actor : v, true));
            panel.Add(StudioFields.Enum(context, "Role", mark.role, v => mark.role = v, true));
            panel.Add(StudioFields.Swatches(context, mark.color, v => mark.color = v));
            string warning = context.Stage != null && StudioRun.State == StudioState.Held ? context.Stage.MarkWarning(mark) : "";
            if (warning.Length > 0) panel.Add(StudioUi.Warning(warning + ". Drag the mark onto a surface."));
            panel.Add(StudioFields.Slide(context, "Facing", mark.yaw, 0, 360, v => mark.yaw = v));
            if (mark.role != StudioRole.Hand)
            {
                panel.Add(StudioFields.Choice(context, "Starts with", SetLists.Equipment, Mathf.Max(0, SetLists.Equipment.IndexOf(string.IsNullOrEmpty(mark.equipment) ? "None" : mark.equipment)),
                    i => mark.equipment = i <= 0 ? "" : SetLists.Equipment[i]));
                panel.Add(StudioFields.Switch(context, "Choose outfit", mark.customLook, v => mark.customLook = v));
                if (mark.customLook)
                {
                    var look = mark.appearance;
                    void Wear(AvatarAppearance changed) => mark.appearance = changed;
                    panel.Add(StudioUi.Card(
                        StudioFields.Enum(context, "Skin", look.skin, v => { look.skin = v; Wear(look); }),
                        StudioFields.Enum(context, "Hair", look.hair, v => { look.hair = v; Wear(look); }),
                        StudioFields.Enum(context, "Hair shade", look.hairShade, v => { look.hairShade = v; Wear(look); }),
                        StudioFields.Enum(context, "Eyewear", look.eyewear, v => { look.eyewear = v; Wear(look); }),
                        StudioFields.Flags(context, "Clothing", look.clothing, v => { look.clothing = v; Wear(look); }),
                        StudioFields.Flags(context, "Accessories", look.accessories, v => { look.accessories = v; Wear(look); }),
                        StudioFields.Switch(context, "Powered armor", look.poweredArmor, v => { look.poweredArmor = v; Wear(look); })));
                }
                else panel.Add(StudioUi.Hint("Wears the game's usual random outfit."));
            }
            panel.Add(StudioUi.Advanced(StudioFields.Point(context, "Position", mark.position, v => mark.position = v),
                StudioUi.Hint("Metres from the stage spot. Drag the mark in the viewport instead of typing here.")));
            var track = context.Tape?.Find(mark.id);
            if (track != null && track.takes.Count > 0) TrackInspector.Takes(panel, track, context);
            else panel.Add(StudioUi.Hint(mark.role == StudioRole.Hand ? "The hand is recorded in the Perform mode." : "Not recorded yet."));
            panel.Add(StudioUi.Small("Delete mark", () =>
            {
                context.Edit("Delete " + mark.actor, () => set.marks.Remove(mark), true);
                context.Select(null);
            }, "The actor's takes stay on the tape."));
        }
    }

    public sealed class CueInspector : IStudioInspector
    {
        public bool Handles(object selection, StudioContext context) => selection is StudioCue;

        public void Build(VisualElement panel, object selection, StudioContext context)
        {
            var cue = (StudioCue)selection;
            var set = context.Set;
            var known = StudioCues.All.ToList();
            var names = known.Select(c => c.Label).ToList();
            int index = known.FindIndex(c => c.Action == cue.action);
            if (index < 0)
                panel.Add(StudioUi.Warning($"The Studio cannot run \"{cue.action}\" cues yet (imported from Theatre). It is kept but skipped."));
            else panel.Add(StudioFields.Choice(context, "Cue", names, index, i => cue.action = known[i].Action));
            panel.Add(StudioFields.Slide(context, "At (s)", cue.at, 0, Mathf.Max(.1f, set.length - .05f), v => cue.at = v));
            if (cue.action == "collapse")
            {
                panel.Add(StudioFields.Slide(context, "Strength", cue.strength, .5f, 30, v => cue.strength = v));
                panel.Add(StudioUi.Advanced(StudioFields.Point(context, "Where", cue.point, v => cue.point = v),
                    StudioUi.Hint("Metres from the stage spot. The ten nearest loose blocks get the shove.")));
            }
            if (cue.action == "hazard")
            {
                var hazards = SetLists.Hazards;
                panel.Add(StudioFields.Choice(context, "Disaster", hazards, hazards.IndexOf(cue.hazard), i => { if (i >= 0 && i < hazards.Count) cue.hazard = hazards[i]; }));
            }
            if (cue.action == "tsunami")
            {
                panel.Add(StudioFields.Slide(context, "Comes from", cue.heading, 0, 360, v => cue.heading = v));
                panel.Add(StudioFields.Slide(context, "Height (m)", cue.height, 0, 100, v => cue.height = v));
                panel.Add(StudioFields.Slide(context, "Speed", cue.strength, 1, 30, v => cue.strength = v));
                panel.Add(StudioUi.Hint("Height 0 lets the Studio pick a wave that reaches the stage."));
            }
            DirectCueFields.Add(panel, cue, context);
            panel.Add(StudioUi.Hint("Cues run when the world is recorded live. Record the world again, or branch, after changing one."));
            panel.Add(StudioUi.Small("Delete cue", () =>
            {
                context.Edit("Delete cue", () => set.cues.Remove(cue));
                context.Select(null);
            }));
        }
    }

    // A track that is not an actor (world, hazards, effects, sound events): its takes.
    public sealed class TrackInspector : IStudioInspector
    {
        public bool Handles(object selection, StudioContext context) => selection is StudioTrack;

        public void Build(VisualElement panel, object selection, StudioContext context)
        {
            var track = (StudioTrack)selection;
            panel.Add(new Label(track.name) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 14 } });
            if (!string.IsNullOrEmpty(track.badge))
            {
                panel.Add(StudioUi.Warning(track.badge));
                panel.Add(StudioUi.Small("Accept and clear this warning", () => { track.badge = ""; context.window.TapeChanged(); }));
            }
            if (StudioTrackTypes.Get(track.type) == null)
                panel.Add(StudioUi.Hint($"This build of the Studio does not know \"{track.type}\" tracks. The data is kept untouched."));
            if (track.takes.Count == 0) panel.Add(StudioUi.Hint("Nothing recorded yet. Press Record."));
            else Takes(panel, track, context);
        }

        // The take list: pick, rename, star, trash. Shared with the mark inspector.
        public static void Takes(VisualElement panel, StudioTrack track, StudioContext context)
        {
            panel.Add(StudioUi.Heading("TAKES"));
            foreach (var take in track.Kept.ToList())
            {
                var captured = take;
                bool active = take == track.ActiveTake;
                var use = StudioUi.Small(active ? "Active" : "Use", () => context.window.SetActiveTake(track, track.takes.IndexOf(captured)));
                use.EnableInClassList("ts-small--on", active);
                use.style.minWidth = 46;
                var name = new TextField { value = take.name, isDelayed = true };
                name.style.flexGrow = 1;
                name.RegisterValueChangedCallback(e => { captured.name = string.IsNullOrWhiteSpace(e.newValue) ? captured.name : e.newValue; context.window.TapeChanged(); });
                var star = StudioUi.Small("*", () => { captured.starred = !captured.starred; context.window.TapeChanged(); }, "Star the takes you like");
                star.EnableInClassList("ts-small--on", take.starred);
                var trash = StudioUi.Small("Trash", () =>
                {
                    captured.trashed = true;
                    if (track.ActiveTake == captured)
                    {
                        var next = track.Kept.LastOrDefault();
                        track.active = next == null ? -1 : track.takes.IndexOf(next);
                    }
                    context.window.TapeChanged();
                }, "Move to the trash list. Nothing is erased until you empty the trash.");
                panel.Add(StudioUi.Row(use, name, star, trash));
                panel.Add(StudioUi.Hint($"   {take.frames.Count} frames from {StudioUi.Time(take.start, context.Set.fps)}, {StudioTape.Size(take.Bytes)}"));
            }
            var trashed = track.takes.Where(t => t.trashed).ToList();
            if (trashed.Count == 0) return;
            var fold = new Foldout { text = "Trash (" + trashed.Count + ")", value = false };
            foreach (var take in trashed)
            {
                var captured = take;
                fold.Add(StudioUi.Row(new Label(take.name) { style = { flexGrow = 1 } },
                    StudioUi.Small("Restore", () => { captured.trashed = false; if (track.ActiveTake == null) track.active = track.takes.IndexOf(captured); context.window.TapeChanged(); })));
            }
            fold.Add(StudioUi.Small("Empty the trash", () =>
            {
                if (!EditorUtility.DisplayDialog("Empty the trash?", $"This erases {trashed.Count} trashed take(s) of {track.name} for good.", "Erase them", "Keep them")) return;
                var keep = track.ActiveTake;
                track.takes.RemoveAll(t => t.trashed);
                track.active = keep == null ? -1 : track.takes.IndexOf(keep);
                context.window.TapeChanged();
            }));
            panel.Add(fold);
        }
    }

    // Draws each recorded actor's path, the stage spot and where aimed cues point.
    public sealed class PathOverlay : IStudioOverlay
    {
        readonly Dictionary<string, List<Vector3>> cache = new Dictionary<string, List<Vector3>>();
        readonly Dictionary<string, string> cacheKeys = new Dictionary<string, string>();

        public void Draw(StudioOverlayPainter painter, StudioContext context)
        {
            var stage = context.Stage; var set = context.Set; var tape = context.Tape;
            if (stage == null || set == null || tape == null) return;
            foreach (var mark in set.marks)
            {
                var track = tape.Find(mark.id);
                var take = track?.ActiveTake;
                if (take == null || track.mode == TrackMode.Muted) continue;
                string key = take.id + ":" + take.frames.Count;
                if (!cacheKeys.TryGetValue(mark.id, out string old) || old != key)
                {
                    // One point every few frames is plenty for a line.
                    var points = new List<Vector3>();
                    for (int f = 0; f < take.frames.Count; f += 3)
                        if (ActorTrack.TryReadPosition(track.type, take.frames[f], out var p)) points.Add(p + Vector3.up * .05f);
                    cache[mark.id] = points; cacheKeys[mark.id] = key;
                }
                var color = StudioColors.For(mark.color);
                painter.Path(cache[mark.id], new Color(color.r, color.g, color.b, .75f));
            }
            foreach (var cue in set.cues)
                if (cue.action == "collapse") painter.Dot(stage.ToWorld(cue.point), ReferenceEquals(cue, context.Selection) ? Color.white : StudioColors.Hazard, 5);
        }
    }
}
