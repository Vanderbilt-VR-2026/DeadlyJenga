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
                // While the tape runs (or the stage is still opening) the cast is in use: wait for it to hold still.
                if (StudioRun.Busy) { context.Say("Wait until the stage holds still, then add the mark."); return; }
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
            bool crowdReady = set.marks.Any(mark => mark.role != StudioRole.Hand &&
                !mark.actor.StartsWith("Crowd ") && context.Tape?.Find(mark.id)?.ActiveTake != null);
            panel.Add(StudioUi.Card(
                StudioUi.Hint(crowdReady ? "Build a crowd from your recorded performances in Polish."
                    : "Keep a survivor take to build a crowd from that performance."),
                StudioUi.Small(crowdReady ? "Open Polish · Make a crowd" : "Open Perform",
                    () => context.window.ShowMode(crowdReady ? "Polish" : "Perform"))));
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
        static List<string> hazards;
        internal static IslandLobbyCatalog Catalog =>
            IslandLobbyCatalog.Runtime(Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog"));

        internal static string SceneFor(IslandLobbyCatalog.Map map, string gameScene)
        {
            if (!SpectatorRecordingPlan.MapAvailable(map)) return null;
            return map.mapPrefab ? gameScene : map.scenePath;
        }

        internal static IslandLobbyCatalog.Map[] AvailableMaps()
        {
            var catalog = Catalog;
            if (!catalog) return Array.Empty<IslandLobbyCatalog.Map>();
            string gameScene = ProjectPath.Find("Game.unity");
            return catalog.maps.Where(map => !string.IsNullOrWhiteSpace(map?.id) &&
                    !string.IsNullOrEmpty(SceneFor(map, gameScene)))
                .GroupBy(map => map.id).Select(group => group.First())
                .OrderBy(map => map.title, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static List<string> MapIds => AvailableMaps().Select(map => map.id).ToList();
        public static List<string> MapNames => AvailableMaps().Select(MapName).ToList();
        internal static string MapName(IslandLobbyCatalog.Map map) => $"{map.title} ({map.id})";

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
            bool recorded = context.Tape != null && context.Tape.tracks.Any(t => t.takes.Count > 0);
            var maps = SetLists.AvailableMaps();
            var mapIds = maps.Select(entry => entry.id).ToList();
            var mapNames = maps.Select(SetLists.MapName).ToList();
            int mapIndex = mapIds.IndexOf(set.map);
            if (mapIndex < 0)
            {
                mapIds.Insert(0, set.map);
                mapNames.Insert(0, string.IsNullOrEmpty(set.map) ? "Choose a map" : $"Unavailable map ({set.map})");
                panel.Add(StudioUi.Warning(maps.Length == 0 ? "No playable maps were found in the lobby catalog."
                    : "This Set's saved map is unavailable in this project."));
                mapIndex = 0;
            }
            var map = StudioFields.Choice(context, "Map", mapNames, mapIndex, i => set.map = mapIds[i]);
            map.SetEnabled(closed && !recorded && maps.Length > 0);
            map.tooltip = recorded ? "Create a new Set for a different map. The recorded world belongs to this map."
                : closed ? "Choose from the game's lobby catalog." : "Close the stage to change the map.";
            panel.Add(map);
            var length = StudioFields.Number(context, "Length (s)", set.length, v => set.length = Mathf.Clamp(v, .5f, 120));
            var rates = new List<string> { "24", "25", "30", "50", "60" };
            // An imported Set can have another rate (Theatre allowed 12 to 60); show it rather than a wrong one.
            if (!rates.Contains(set.fps.ToString())) rates.Insert(0, set.fps.ToString());
            var rate = StudioFields.Choice(context, "Frame rate", rates, rates.IndexOf(set.fps.ToString()), i => { if (i >= 0) set.fps = int.Parse(rates[i]); });
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
        [Serializable]
        sealed class WardrobeCatalog
        {
            public AvatarAppearance[] skins, hair, headgear, eyewear, outfits, back, extras;
        }

        static WardrobeCatalog wardrobe;

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
                var lookControls = new VisualElement();
                if (StudioRun.Busy || StudioHandheld.Recording)
                {
                    panel.Add(StudioUi.Hint("Stop the run to change the actor's look or loadout."));
                    lookControls.SetEnabled(false);
                }
                AddEquipment(lookControls, mark, context);
                lookControls.Add(StudioFields.Switch(context, "Custom outfit", mark.customLook, v => mark.customLook = v));
                if (mark.customLook) AddWardrobe(lookControls, mark, context);
                else lookControls.Add(StudioUi.Hint(mark.role == StudioRole.Bot ? "Uses the game's random bot outfit."
                    : "Uses the local player's outfit when performed."));
                panel.Add(lookControls);
            }
            panel.Add(StudioUi.Advanced(StudioFields.Point(context, "Position", mark.position, v => mark.position = v),
                StudioUi.Hint("Metres from the stage spot. Drag the mark in the viewport instead of typing here.")));
            var track = context.Tape?.Find(mark.id);
            if (track != null && track.takes.Count > 0) TrackInspector.Takes(panel, track, context);
            else panel.Add(StudioUi.Hint(mark.role == StudioRole.Hand ? "The hand is recorded in the Perform mode." : "Not recorded yet."));
            // Same as the Delete key: also takes away the actor's layers and the cameras' references to it.
            panel.Add(StudioUi.Small("Delete mark", context.window.DeleteSelection, "The actor's takes stay on the tape."));
        }

        static void AddEquipment(VisualElement panel, StudioMark mark, StudioContext context)
        {
            var kinds = ((EquipmentKind[])Enum.GetValues(typeof(EquipmentKind)))
                .Where(kind => kind != EquipmentKind.FlyingHand &&
                    (int)kind < EquipmentSpecifications.All.Length).ToList();
            var labels = kinds.Select(kind => kind == EquipmentKind.None ? "None" : EquipmentSpecifications.Get(kind).name).ToList();
            EquipmentKind starting = StudioCast.StartingEquipment(mark);
            int current = kinds.IndexOf(starting);
            if (current < 0)
            {
                labels.Insert(0, $"Saved loadout ({mark.equipment})");
                kinds.Insert(0, starting);
                current = 0;
            }
            panel.Add(StudioFields.Choice(context, "Starting gear", labels, current, i =>
            {
                var kind = kinds[i];
                mark.equipment = kind == EquipmentKind.None ? "" : kind.ToString();
                var look = mark.appearance;
                look.poweredArmor = false;
                mark.appearance = look;
            }));
        }

        static void AddWardrobe(VisualElement panel, StudioMark mark, StudioContext context)
        {
            if (wardrobe == null)
            {
                var asset = Resources.Load<TextAsset>("WardrobeCatalog");
                wardrobe = (asset ? JsonUtility.FromJson<WardrobeCatalog>(asset.text) : null) ?? new WardrobeCatalog();
            }
            var look = mark.appearance;
            var card = StudioUi.Card();
            panel.Add(card);
            void Wear(AvatarAppearance value) => mark.appearance = value;
            void Pick<T>(string label, AvatarAppearance[] items, Func<AvatarAppearance, T> get, Func<T, AvatarAppearance> change, T current)
                where T : struct
            {
                var values = new[] { default(T) }.Concat((items ?? Array.Empty<AvatarAppearance>()).Select(get)).Distinct().ToList();
                var labels = values.Select(value => ObjectNames.NicifyVariableName(value.ToString())).ToList();
                int index = values.IndexOf(current);
                if (index < 0)
                {
                    index = values.Count;
                    values.Add(current);
                    labels.Add($"Saved {ObjectNames.NicifyVariableName(current.ToString())}");
                }
                card.Add(StudioFields.Choice(context, label, labels, index, i => Wear(change(values[i]))));
            }
            void Slot(string label, AvatarAppearance[] items, bool hair = false, bool eyewear = false)
            {
                items ??= Array.Empty<AvatarAppearance>();
                var accessories = items.Aggregate(AvatarAccessory.None, (mask, item) => mask | item.accessories);
                var clothing = items.Aggregate(AvatarClothing.None, (mask, item) => mask | item.clothing);
                var options = new[] { default(AvatarAppearance) }.Concat(items).ToArray();
                var names = options.Select(item => item.accessories != 0 ? ObjectNames.NicifyVariableName(item.accessories.ToString())
                    : item.clothing != 0 ? ObjectNames.NicifyVariableName(item.clothing.ToString())
                    : item.hair != AvatarHair.None ? ObjectNames.NicifyVariableName(item.hair.ToString())
                    : item.eyewear != AvatarEyewear.None ? ObjectNames.NicifyVariableName(item.eyewear.ToString()) : "None").ToList();
                var activeAccessories = look.accessories & accessories;
                var activeClothing = look.clothing & clothing;
                int index = Array.FindIndex(options, item => item.accessories == activeAccessories && item.clothing == activeClothing &&
                    (!hair || item.hair == look.hair) && (!eyewear || item.eyewear == look.eyewear));
                if (index < 0)
                {
                    index = names.Count;
                    names.Add("Saved combination");
                }
                card.Add(StudioFields.Choice(context, label, names, index, i =>
                {
                    if (i >= options.Length) return;
                    var changed = look;
                    changed.accessories = (look.accessories & ~accessories) | options[i].accessories;
                    changed.clothing = (look.clothing & ~clothing) | options[i].clothing;
                    if (hair) changed.hair = options[i].hair;
                    if (eyewear) changed.eyewear = options[i].eyewear;
                    Wear(changed);
                }));
            }
            Pick("Skin", wardrobe.skins, item => item.skin, value => { var changed = look; changed.skin = value; return changed; }, look.skin);
            if (look.skin == AvatarSkin.Default && StudioCast.StartingEquipment(mark) != EquipmentKind.PoweredArmor)
            {
                Slot("Hair", wardrobe.hair, hair: true);
                card.Add(StudioFields.Enum(context, "Hair shade", look.hairShade, value => { var changed = look; changed.hairShade = value; Wear(changed); }));
                Slot("Eyewear", wardrobe.eyewear, eyewear: true);
                Slot("Headwear", wardrobe.headgear);
                Slot("Outfit", wardrobe.outfits);
                Slot("Back", wardrobe.back);
                Slot("Extras", wardrobe.extras);
            }
            else card.Add(StudioUi.Hint("The selected skin or armor covers the saved wardrobe items."));

            var prefab = SetLists.Catalog?.survivorPrefab;
            var preview = prefab ? prefab.GetComponentInChildren<IAvatarWardrobePreview>(true) : null;
            if (preview != null && look.skin == AvatarSkin.Default && StudioCast.StartingEquipment(mark) != EquipmentKind.PoweredArmor)
            {
                var fit = preview.EvaluateAppearance(look);
                if (!string.IsNullOrEmpty(fit.reason)) card.Add(StudioUi.Warning(fit.reason));
            }
            card.Add(StudioUi.Small("Reset outfit", () => context.Edit("Reset outfit", () => mark.appearance = default)));
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
            // Same as the Delete key: cues that waited for this one go back to their own time.
            panel.Add(StudioUi.Small("Delete cue", context.window.DeleteSelection));
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
            // Each warning on its own, with its own clear button (a track can be stale and retimed at once).
            foreach (string part in StudioBadges.Parts(track))
            {
                string kind = StudioBadges.KindOf(part), captured = part;
                panel.Add(StudioUi.Warning(part));
                panel.Add(StudioUi.Small("Accept and clear this warning", () =>
                {
                    if (kind == StudioBadges.Stale) StudioTakes.AcceptStale(context.Tape, track);
                    else track.badge = string.Join("\n", StudioBadges.Parts(track).Where(p => p != captured));
                    context.window.TapeChanged();
                }));
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
                use.SetEnabled(!active && !StudioRun.Busy && !StudioHandheld.Recording);
                use.style.minWidth = 46;
                var name = new TextField { value = take.name, isDelayed = true };
                name.style.flexGrow = 1;
                name.RegisterValueChangedCallback(e => { captured.name = string.IsNullOrWhiteSpace(e.newValue) ? captured.name : e.newValue; context.window.TapeChanged(); });
                var star = StudioUi.Small(take.starred ? "Starred" : "Star", () => { captured.starred = !captured.starred; context.window.TapeChanged(); }, "Star the takes you like");
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
