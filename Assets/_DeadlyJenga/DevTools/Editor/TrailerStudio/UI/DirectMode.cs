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
    // The Direct tab: make the world do what the moment needs.
    // Pick blocks in the viewport and push them, add disasters, branch the physics, run variations.
    //   Click a block: select it.  Shift+click: add or remove.  Ctrl+drag: box select.  Alt+drag: brush select.
    //   With a push cue selected: drag the arrow's tip to aim it and set its force.
    public sealed class DirectMode : IStudioMode, IStudioViewportTool
    {
        public string Title => "Direct";
        public int Order => 30;
        public bool Available => true;
        public bool TryGetRecord(StudioContext context, out string label, out Action start)
        {
            label = "Record the world, take " + ((context.Tape?.FindType(WorldTrack.Id)?.takes.Count ?? 0) + 1);
            start = StudioRun.RecordWholeSet;
            return context.Set != null;
        }

        // The blocks picked in the viewport (indexes into the map's block list).
        public static readonly List<int> Picked = new List<int>();
        static string sortBy = "targetDistance";
        static string shownGroup = "";

        // ---- Viewport tool ----
        enum Drag { None, Arrow, Box, Brush, Click }
        Drag drag;
        Vector2 downAt, boxTo;
        public static Rect? Box;          // the box being dragged, in viewport pixels (drawn by the overlay)

        static Vector3 Centre(StudioStage stage, StudioCue cue)
        {
            var blocks = StudioDirector.Blocks(stage, cue);
            if (blocks.Count == 0) return stage.ToWorld(cue.point);
            Vector3 sum = Vector3.zero;
            foreach (var block in blocks) sum += block.worldCenterOfMass;
            return sum / blocks.Count;
        }

        // An arrow's length on screen stands for the push: one metre of arrow per 4 m/s.
        public static Vector3 ArrowTip(StudioStage stage, StudioCue cue) =>
            Centre(stage, cue) + (cue.direction.sqrMagnitude > .0001f ? cue.direction.normalized : Vector3.right) * Mathf.Max(.4f, cue.strength / 4f);

        public bool Down(PointerDownEvent e, StudioViewport viewport, StudioContext context)
        {
            var stage = context.Stage;
            if (stage == null) return false;
            downAt = boxTo = e.localPosition;
            if (context.Selection is StudioCue cue && cue.action == PushCue.Id &&
                viewport.ToPanel(ArrowTip(stage, cue), out var tip) && Vector2.Distance(tip, e.localPosition) < 14)
            {
                drag = Drag.Arrow;
                context.window.BeginEdit("Aim push");
                return true;
            }
            if (e.ctrlKey || e.commandKey) { drag = Drag.Box; return true; }
            if (e.altKey) { drag = Drag.Brush; Brush(viewport, e.localPosition); return true; }
            if (viewport.BlockAt(e.localPosition) >= 0) { drag = Drag.Click; return true; }
            return false;
        }

        void Brush(StudioViewport viewport, Vector2 at)
        {
            int block = viewport.BlockAt(at);
            if (block >= 0 && !Picked.Contains(block)) Picked.Add(block);
        }

        public void Move(PointerMoveEvent e, StudioViewport viewport, StudioContext context)
        {
            var stage = context.Stage;
            if (stage == null) return;
            if (drag == Drag.Arrow && context.Selection is StudioCue cue)
            {
                // Slide the tip on a plane through the blocks that faces the camera.
                Vector3 centre = Centre(stage, cue);
                var plane = new Plane(-StudioCamera.Camera.transform.forward, centre);
                var ray = viewport.ToRay(e.localPosition);
                if (!plane.Raycast(ray, out float distance)) return;
                Vector3 arrow = ray.GetPoint(distance) - centre;
                if (arrow.sqrMagnitude < .01f) return;
                cue.direction = arrow.normalized;
                cue.strength = Mathf.Clamp(arrow.magnitude * 4, .5f, 60);
            }
            else if (drag == Drag.Box) { boxTo = e.localPosition; Box = Rect.MinMaxRect(Mathf.Min(downAt.x, boxTo.x), Mathf.Min(downAt.y, boxTo.y), Mathf.Max(downAt.x, boxTo.x), Mathf.Max(downAt.y, boxTo.y)); }
            else if (drag == Drag.Brush) Brush(viewport, e.localPosition);
            else if (drag == Drag.Click && Vector2.Distance(downAt, e.localPosition) > 6) drag = Drag.None;   // it became a drag: not a click
        }

        public void Up(PointerUpEvent e, StudioViewport viewport, StudioContext context)
        {
            var stage = context.Stage;
            var was = drag; drag = Drag.None;
            if (stage == null) return;
            if (was == Drag.Arrow) { context.window.EndEdit(false); return; }
            if (was == Drag.Click)
            {
                int block = viewport.BlockAt(e.localPosition);
                if (block < 0) return;
                if (!e.shiftKey) { Picked.Clear(); Picked.Add(block); }
                else if (!Picked.Remove(block)) Picked.Add(block);
            }
            if (was == Drag.Box && Box.HasValue)
            {
                if (!e.shiftKey) Picked.Clear();
                for (int i = 0; i < stage.blocks.Length; i++)
                    if (stage.blocks[i] && stage.blocks[i].gameObject.activeSelf && viewport.ToPanel(stage.blocks[i].worldCenterOfMass, out var p) && Box.Value.Contains(p) && !Picked.Contains(i))
                        Picked.Add(i);
            }
            Box = null;
            context.Rebuild();
        }

        // ---- Panel ----

        public void Build(VisualElement panel, StudioContext context)
        {
            panel.Add(StudioUi.Heading("DIRECT THE WORLD"));
            var set = context.Set; var tape = context.Tape;
            if (set == null) { panel.Add(StudioUi.Hint("Choose or make a Set first.")); return; }
            bool held = StudioRun.State == StudioState.Held;

            void AddCue(string label, Action<StudioCue> setup)
            {
                // The playhead at the moment of the click (scrubbing does not rebuild this panel).
                float now = Mathf.Min(context.Frame / (float)set.fps, set.length - .1f);
                StudioCue made = null;
                context.Edit("Add " + label, () => { made = new StudioCue { at = now, strength = 6 }; setup(made); set.cues.Add(made); });
                context.Select(made);
            }

            // Aimed destruction
            Picked.RemoveAll(i => context.Stage == null || i < 0 || i >= context.Stage.blocks.Length);
            panel.Add(StudioUi.Hint(!context.StageOpen ? "Open the stage to pick blocks."
                : Picked.Count == 0 ? "Click a block in the viewport. Shift+click adds, Ctrl+drag boxes, Alt+drag brushes."
                : Picked.Count + (Picked.Count == 1 ? " block picked." : " blocks picked.")));
            var push = StudioUi.Big("+  Push the picked blocks", () => AddCue("push", c => { c.action = PushCue.Id; c.blocks = new List<int>(Picked); c.direction = Vector3.right; }),
                "Adds a push at the playhead. Drag its arrow in the viewport to aim it.");
            var ripple = StudioUi.Big("+  Ripple push (a wave)", () => AddCue("ripple push", c => { c.action = PushCue.Id; c.blocks = new List<int>(Picked); c.ripple = .08f; c.strength = 5; }),
                "Each block goes a little later than the last, by distance from the first one you picked.");
            var weak = StudioUi.Big("+  Weak point", () => AddCue("weak point", c => { c.action = WeakenCue.Id; c.blocks = new List<int>(Picked); }),
                "Loosen or remove the picked blocks at the playhead, as if pulled.");
            push.SetEnabled(Picked.Count > 0); ripple.SetEnabled(Picked.Count > 1); weak.SetEnabled(Picked.Count > 0);
            panel.Add(push); panel.Add(ripple); panel.Add(weak);
            if (Picked.Count > 0) panel.Add(StudioUi.Small("Clear the pick", () => { Picked.Clear(); context.Rebuild(); }));
            panel.Add(StudioUi.Big("+  Disaster", () =>
            {
                var menu = new GenericMenu();
                foreach (string hazard in SetLists.Hazards)
                {
                    string captured = hazard;
                    menu.AddItem(new GUIContent(hazard), false, () => AddCue(captured, c => { c.action = "hazard"; c.hazard = captured; c.values = DirectCueFields.Defaults(captured); }));
                }
                menu.AddItem(new GUIContent("Tsunami (aimed)"), false, () => AddCue("tsunami", c => c.action = "tsunami"));
                menu.AddItem(new GUIContent("Stop all disasters"), false, () => AddCue("stop disasters", c => c.action = "stopHazards"));
                menu.ShowAsContext();
            }, "Any of the game's disasters, with its own settings."));
            panel.Add(StudioUi.Big("+  Guide", () => AddCue("guide", c => { c.action = GuideCue.Id; c.duration = 2; c.strength = 8; c.point = context.Stage != null ? context.Stage.ToStage(StudioCamera.Target) : Vector3.zero; }),
                "An invisible pull that nudges falling debris toward a spot. Only the Studio shows it."));
            panel.Add(StudioUi.Big("+  Slow time", () => AddCue("slow time", c => { c.action = SlowTimeCue.Id; c.duration = 2; c.strength = .5f; }),
                "Slow the world over a range while you perform in real time."));

            Branches(panel, context, held);
            Variations(panel, context, held);
        }

        void Branches(VisualElement panel, StudioContext context, bool held)
        {
            var set = context.Set; var tape = context.Tape;
            var world = tape?.FindType(WorldTrack.Id);
            panel.Add(StudioUi.Heading("BRANCH THE PHYSICS"));
            if (world?.ActiveTake == null) { panel.Add(StudioUi.Hint("Record the world once. Then you can keep everything before a frame and re-simulate after it.")); return; }
            var branch = StudioUi.Big($"Branch at {StudioUi.Time(context.Frame, set.fps)}", () =>
            {
                try
                {
                    var pass = new StudioPass { label = "Branch", liveWorld = true, fixedClock = true, branchFrom = context.Frame };
                    foreach (var track in tape.tracks)
                        if (!track.locked && track.type != SurvivorTrack.Id && track.type != HandTrack.Id && StudioTrackTypes.Get(track.type) != null) pass.record.Add(track.id);
                    StudioRun.Record(pass);
                }
                catch (Exception error) { context.Say(error.Message); }
            }, "Everything before the playhead stays from the tape. After it, physics runs live with your new or changed cues and is saved as a new world take.");
            branch.SetEnabled(held && context.Frame > 0 && world.ActiveTake.Covers(context.Frame));
            panel.Add(branch);
            panel.Add(StudioUi.Hint("Branch before a disaster starts: a disaster that is already running on the tape does not carry over into the new physics."));
            TakeGrid(panel, context, world.Kept.Where(t => string.IsNullOrEmpty(StudioBranch.NoteOf(t).group)).ToList(), false);
        }

        void Variations(VisualElement panel, StudioContext context, bool held)
        {
            var set = context.Set; var tape = context.Tape;
            panel.Add(StudioUi.Heading("VARIATION RUNS"));
            var settings = StudioVariations.Current;
            void Save() => StudioVariations.Current = settings;
            var runs = new SliderInt("Tries", 2, 60) { value = settings.runs, showInputField = true };
            runs.RegisterValueChangedCallback(e => { settings.runs = e.newValue; Save(); });
            var from = new Toggle("Start from the playhead (branch)") { value = settings.fromFrame > 0, tooltip = "Off: every try starts from a fresh map." };
            from.RegisterValueChangedCallback(e => { settings.fromFrame = e.newValue ? Mathf.Max(1, context.Frame) : 0; Save(); context.Rebuild(); });
            var force = new Slider("Force change", 0, .5f) { value = settings.forceJitter, showInputField = true, tooltip = "Each push is up to this much stronger or weaker (0.15 = 15%)." };
            force.RegisterValueChangedCallback(e => { settings.forceJitter = e.newValue; Save(); });
            var timing = new Slider("Timing change (s)", 0, .5f) { value = settings.timeJitter, showInputField = true };
            timing.RegisterValueChangedCallback(e => { settings.timeJitter = e.newValue; Save(); });
            panel.Add(runs); panel.Add(from);
            if (settings.fromFrame > 0) panel.Add(StudioUi.Hint("Tries branch at " + StudioUi.Time(settings.fromFrame, set.fps) + "."));
            panel.Add(StudioUi.Advanced(force, timing));

            // What to score: a block and a spot.
            string target = settings.targetBlock >= 0 ? $"Block #{settings.targetBlock} should land within {settings.zoneRadius:0.#} m of the spot" : "No target block chosen (still scores what fell and how far).";
            panel.Add(StudioUi.Hint(target));
            var useBlock = StudioUi.Small("Target = picked block", () => { settings.targetBlock = Picked.Count > 0 ? Picked[0] : -1; Save(); context.Rebuild(); }, "Pick one block in the viewport first.");
            useBlock.SetEnabled(Picked.Count > 0);
            var useSpot = StudioUi.Small("Spot = selected mark", () =>
            {
                if (context.Selection is StudioMark mark) { settings.targetPoint = mark.position; Save(); context.Rebuild(); }
                else context.Say("Select a mark first; its position becomes the spot.");
            });
            panel.Add(StudioUi.Row(useBlock, useSpot));

            if (StudioVariations.Running)
            {
                panel.Add(StudioUi.Hint($"Running try {Mathf.Min(StudioVariations.Done + 1, StudioVariations.Total)} of {StudioVariations.Total}…"));
                panel.Add(StudioUi.Small("Stop after this try", StudioVariations.Cancel));
            }
            else
            {
                var go = StudioUi.Big("Run " + settings.runs + " variations", () =>
                {
                    try { StudioVariations.Start(settings); shownGroup = StudioVariations.LastGroup; }
                    catch (Exception error) { context.Say(error.Message); }
                }, "Runs one after another at the fixed clock. You can keep using the window; results appear below.");
                go.AddToClassList("ts-big--primary");
                go.SetEnabled(held);
                panel.Add(go);
            }

            var groups = StudioVariations.Groups(tape);
            if (groups.Count == 0) return;
            if (!groups.Contains(shownGroup)) shownGroup = groups[groups.Count - 1];
            var sorts = new List<string> { "Closest to the spot", "Most blocks fell", "Most movement", "In order" };
            var keys = new[] { "targetDistance", "fallen", "travelled", "order" };
            var sort = new PopupField<string>("Sort by", sorts, Mathf.Max(0, Array.IndexOf(keys, sortBy)));
            sort.RegisterValueChangedCallback(e => { sortBy = keys[Mathf.Max(0, sorts.IndexOf(e.newValue))]; context.Rebuild(); });
            panel.Add(sort);
            var results = StudioVariations.Results(tape, shownGroup);
            IEnumerable<StudioTake> ordered = results;
            if (sortBy == "fallen") ordered = results.OrderByDescending(t => StudioBranch.NoteOf(t).fallen);
            else if (sortBy == "travelled") ordered = results.OrderByDescending(t => StudioBranch.NoteOf(t).travelled);
            else if (sortBy == "targetDistance") ordered = results.OrderBy(t => { float d = StudioBranch.NoteOf(t).targetDistance; return d < 0 ? float.MaxValue : d; });
            TakeGrid(panel, context, ordered.ToList(), true);
        }

        // World takes as cards with a picture, scores and one button to make it the active world.
        void TakeGrid(VisualElement panel, StudioContext context, List<StudioTake> takes, bool scores)
        {
            var tape = context.Tape; var set = context.Set;
            var world = tape.FindType(WorldTrack.Id);
            foreach (var take in takes)
            {
                var captured = take;
                var note = StudioBranch.NoteOf(take);
                var card = StudioUi.Card();
                string picture = StudioBranch.Thumbnail(set.id, take);
                if (File.Exists(picture))
                {
                    var texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                    texture.LoadImage(File.ReadAllBytes(picture));
                    var image = new Image { image = texture, scaleMode = ScaleMode.ScaleAndCrop };
                    image.AddToClassList("ts-thumb"); image.style.height = 84;
                    image.RegisterCallback<DetachFromPanelEvent>(_ => UnityEngine.Object.DestroyImmediate(texture));
                    card.Add(image);
                }
                bool active = take == world.ActiveTake;
                var use = StudioUi.Small(active ? "Active" : "Use this", () =>
                {
                    StudioVariations.Activate(tape, captured);
                    StudioTakes.CheckStale(tape, set.fps);
                    context.window.TapeChanged();
                }, "Make this the world everyone plays against.");
                use.EnableInClassList("ts-small--on", active);
                var name = new Label(take.name); name.AddToClassList("ts-item-name");
                card.Add(StudioUi.Row(name, use));
                if (StudioVariations.RecipeOf(take) != null)
                    card.Add(StudioUi.Small("Put its cue timings in the Set", () =>
                    {
                        string said = "";
                        context.Edit("Use variation timings", () => said = StudioVariations.ApplyRecipe(set, captured));
                        context.Say(said);
                    }, "This try moved and strengthened the cues a little. Write those values (and, for a try from a fresh map, its random seed) into the Set, so recording the whole set makes this collapse again."));
                string detail = note.branchFrame >= 0 ? $"Branched at {StudioUi.Time(note.branchFrame, set.fps)}. " : "";
                if (scores || note.fallen > 0)
                    detail += $"{note.fallen} blocks fell, {note.travelled:0} m of movement" + (note.targetDistance >= 0 ? $", target block {note.targetDistance:0.00} m from the spot" + (note.inZone ? " (in the zone)" : "") : "") + ".";
                if (detail.Length > 0) card.Add(StudioUi.Hint(detail));
                panel.Add(card);
            }
        }
    }

    // The extra settings of Phase 3 cues, added to the cue inspector.
    public static class DirectCueFields
    {
        public static float[] Defaults(string hazard)
        {
            try
            {
                var prefab = DemoDisasters.Prefabs.FirstOrDefault(p => p && p.name == hazard);
                return prefab ? DemoDisasters.Parameters(prefab).Select(p => p.initial).ToArray() : new float[0];
            }
            catch { return new float[0]; }
        }

        // Does 'cue' wait for the cue 'id', directly or through a chain of links?
        public static bool WaitsFor(StudioSet set, StudioCue cue, string id)
        {
            var seen = new HashSet<string>();
            for (var step = cue; step != null && !string.IsNullOrEmpty(step.link) && seen.Add(step.id); step = set.cues.FirstOrDefault(c => c.id == step.link))
                if (step.link == id) return true;
            return false;
        }

        public static void Add(VisualElement panel, StudioCue cue, StudioContext context)
        {
            var set = context.Set;
            if (cue.action == PushCue.Id)
            {
                panel.Add(StudioUi.Hint(cue.blocks.Count + (cue.blocks.Count == 1 ? " block." : " blocks.") + " Drag the arrow's tip in the viewport to aim."));
                panel.Add(StudioFields.Slide(context, "Force (m/s)", cue.strength, .5f, 60, v => cue.strength = v));
                panel.Add(StudioFields.Slide(context, "Spin (turns/s)", cue.spin, -4, 4, v => cue.spin = v));
                panel.Add(StudioFields.Slide(context, "Ripple (s per m)", cue.ripple, 0, .5f, v => cue.ripple = v));
                panel.Add(StudioUi.Small("Use the picked blocks", () => context.Edit("Change blocks", () => cue.blocks = new List<int>(DirectMode.Picked)), "Replace this cue's blocks with the ones picked in the viewport."));
                panel.Add(StudioUi.Advanced(StudioFields.Point(context, "Direction", cue.direction, v => cue.direction = v.sqrMagnitude > .0001f ? v.normalized : Vector3.right)));
            }
            if (cue.action == WeakenCue.Id)
            {
                panel.Add(StudioUi.Hint(cue.blocks.Count + (cue.blocks.Count == 1 ? " block." : " blocks.")));
                panel.Add(StudioFields.Switch(context, "Remove completely", cue.lethal, v => cue.lethal = v));
                panel.Add(StudioUi.Hint(cue.lethal ? "The blocks vanish at this time." : "The blocks come loose at this time and fall by themselves."));
            }
            if (cue.action == GuideCue.Id)
            {
                panel.Add(StudioFields.Slide(context, "Lasts (s)", cue.duration, .2f, 10, v => cue.duration = v));
                panel.Add(StudioFields.Slide(context, "Reach (m)", cue.radius, .5f, 20, v => cue.radius = v));
                panel.Add(StudioFields.Slide(context, "Pull", cue.strength, 0, 40, v => cue.strength = v));
                panel.Add(StudioFields.Point(context, "Spot", cue.point, v => cue.point = v));
                var here = StudioUi.Small("Spot = what the viewport looks at", () => context.Edit("Move guide", () => cue.point = context.Stage.ToStage(StudioCamera.Target)));
                here.SetEnabled(context.StageOpen);
                panel.Add(here);
            }
            if (cue.action == SlowTimeCue.Id)
            {
                panel.Add(StudioFields.Slide(context, "Lasts (s)", cue.duration, .2f, 20, v => cue.duration = v));
                panel.Add(StudioFields.Slide(context, "World speed", cue.strength, .05f, 1, v => cue.strength = v));
                panel.Add(StudioUi.Hint("Applies while you perform in real time. Recordings at the fixed clock ignore it."));
            }
            if (cue.action == "hazard")
            {
                var prefab = DemoDisasters.Prefabs.FirstOrDefault(p => p && p.name == cue.hazard);
                var parameters = prefab ? DemoDisasters.Parameters(prefab) : null;
                if (parameters != null && parameters.Count > 0)
                {
                    if (cue.values == null || cue.values.Length != parameters.Count) cue.values = parameters.Select(p => p.initial).ToArray();
                    for (int i = 0; i < parameters.Count; i++)
                    {
                        int index = i; var parameter = parameters[i];
                        string label = parameter.label + (string.IsNullOrEmpty(parameter.unit) ? "" : " (" + parameter.unit + ")");
                        panel.Add(StudioFields.Slide(context, label, cue.values[i], parameter.min, parameter.max, v => cue.values[index] = parameter.Clamp(v)));
                    }
                }
                else panel.Add(StudioUi.Hint("This disaster has no settings of its own."));
                if (prefab && prefab.GetComponent<IDirectedHazard>() != null)
                {
                    panel.Add(StudioFields.Switch(context, "Choose where it comes from", cue.aimed, v => cue.aimed = v));
                    if (cue.aimed) panel.Add(StudioFields.Slide(context, "Comes from (degrees)", cue.heading, 0, 360, v => cue.heading = v));
                    else panel.Add(StudioUi.Hint("It arrives from a random side, like in the game."));
                }
                panel.Add(StudioFields.Slide(context, "Ends after (s)", cue.duration, 0, 30, v => cue.duration = v));
                panel.Add(StudioUi.Hint("0 lets the disaster end by itself."));
            }
            if (cue.action == "gear" || cue.action == "fire")
            {
                var actors = set.marks.Select(m => m.id).ToList();
                var names = set.marks.Select(m => m.actor).ToList();
                if (cue.action == "gear") { actors.Insert(0, ""); names.Insert(0, "Everyone live"); }
                panel.Add(StudioFields.Choice(context, cue.action == "fire" ? "Who fires" : "Who gets it", names, actors.IndexOf(cue.actor), i => { if (i >= 0) cue.actor = actors[i]; }));
                if (cue.action == "gear")
                    panel.Add(StudioFields.Choice(context, "Weapon", SetLists.Equipment, Mathf.Max(0, SetLists.Equipment.IndexOf(cue.weapon)), i => cue.weapon = i <= 0 ? "" : SetLists.Equipment[i]));
                else
                {
                    var targets = new List<string> { "" }.Concat(set.marks.Select(m => m.id)).ToList();
                    var targetNames = new List<string> { "A point" }.Concat(set.marks.Select(m => m.actor)).ToList();
                    panel.Add(StudioFields.Choice(context, "At", targetNames, Mathf.Max(0, targets.IndexOf(cue.target)), i => cue.target = i <= 0 ? "" : targets[i]));
                    if (string.IsNullOrEmpty(cue.target)) panel.Add(StudioFields.Point(context, "Point", cue.point, v => cue.point = v));
                    panel.Add(StudioFields.Switch(context, "Fine if gameplay refuses", cue.optional, v => cue.optional = v));
                }
                panel.Add(StudioUi.Hint("The actor must be live in the run (a bot, a stand-in or the performer). Recorded actors cannot be handed things or made to fire."));
            }

            // Chain reactions: any cue can wait for another cue instead of the clock.
            // A loop (A waits for B, B waits for C, C waits for A) would never start, so cues that already wait
            // for this one, directly or through others, are not offered.
            var others = set.cues.Where(c => c != cue && !WaitsFor(set, c, cue.id)).ToList();
            if (!string.IsNullOrEmpty(cue.link) && !set.cues.Any(c => c.id == cue.link && c != cue))
                panel.Add(StudioUi.Warning("This cue waits for a cue that is no longer in the Set, so it never runs. Pick \"Its own time\" or another cue under Chain reaction."));
            if (others.Count > 0 && cue.action != SlowTimeCue.Id)
            {
                var ids = new List<string> { "" }.Concat(others.Select(c => c.id)).ToList();
                var labels = new List<string> { "Its own time" }.Concat(others.Select(c => $"After \"{StudioCues.Get(c.action)?.Label ?? c.action}\" at {c.at:0.00}s")).ToList();
                var chain = StudioUi.Advanced(
                    StudioFields.Choice(context, "Starts at", labels, Mathf.Max(0, ids.IndexOf(cue.link)), i => cue.link = i <= 0 ? "" : ids[i]),
                    StudioFields.Switch(context, "…when its first block hits something", cue.linkOnHit, v => cue.linkOnHit = v),
                    StudioUi.Hint("Chain reaction: with a link, \"At\" above is the delay after the other cue (or after its block hits)."));
                chain.text = "Chain reaction";
                chain.value = !string.IsNullOrEmpty(cue.link);
                panel.Add(chain);
            }
        }
    }

    // Picked blocks, push arrows, guide rings and the box being dragged.
    public sealed class DirectOverlay : IStudioOverlay
    {
        public void Draw(StudioOverlayPainter painter, StudioContext context)
        {
            var stage = context.Stage; var set = context.Set;
            if (stage == null || set == null) return;
            bool direct = context.window.Mode is DirectMode;
            if (direct)
                foreach (int index in DirectMode.Picked)
                    if (index >= 0 && index < stage.blocks.Length && stage.blocks[index]) painter.Dot(stage.blocks[index].worldCenterOfMass, new Color(1, .9f, .3f), 5);
            foreach (var cue in set.cues)
            {
                bool selected = ReferenceEquals(cue, context.Selection);
                if (!direct && !selected) continue;
                Color color = selected ? Color.white : StudioColors.Hazard;
                if (cue.action == PushCue.Id)
                {
                    var blocks = StudioDirector.Blocks(stage, cue);
                    if (blocks.Count == 0) continue;
                    Vector3 centre = Vector3.zero;
                    foreach (var block in blocks) { centre += block.worldCenterOfMass; if (selected) painter.Dot(block.worldCenterOfMass, color, 3); }
                    centre /= blocks.Count;
                    Vector3 tip = DirectMode.ArrowTip(stage, cue);
                    painter.Line(centre, tip, color, selected ? 3 : 2);
                    painter.Dot(tip, color, selected ? 7 : 4);
                }
                if (cue.action == "hazard" && cue.aimed || cue.action == "tsunami")
                {
                    // An arrow from the side the disaster comes from, toward the middle of the map.
                    float angle = cue.heading * Mathf.Deg2Rad;
                    Vector3 from = stage.world.Center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (stage.world.Radius + 6);
                    from.y = stage.anchor.y;
                    Vector3 to = new Vector3(stage.world.Center.x, stage.anchor.y, stage.world.Center.z);
                    Vector3 head = Vector3.Lerp(from, to, .45f);
                    painter.Line(from, head, color, selected ? 3 : 2);
                    painter.Dot(head, color, selected ? 7 : 4);
                }
                if (cue.action == GuideCue.Id)
                {
                    Vector3 centre = stage.ToWorld(cue.point);
                    var ring = new List<Vector3>();
                    for (int i = 0; i <= 32; i++) ring.Add(centre + Quaternion.Euler(0, i * 11.25f, 0) * Vector3.forward * cue.radius);
                    painter.Path(ring, new Color(color.r, color.g, color.b, .7f), 1.5f);
                    painter.Dot(centre, color, 4);
                }
            }
            var settings = StudioVariations.Current;
            if (direct && settings.targetBlock >= 0)
            {
                Vector3 spot = stage.ToWorld(settings.targetPoint);
                var ring = new List<Vector3>();
                for (int i = 0; i <= 24; i++) ring.Add(spot + Quaternion.Euler(0, i * 15f, 0) * Vector3.forward * settings.zoneRadius);
                painter.Path(ring, new Color(.4f, 1, .6f, .8f), 1.5f);
            }
            if (DirectMode.Box.HasValue) painter.ScreenBox(DirectMode.Box.Value, new Color(1, .9f, .3f));
        }
    }
}
