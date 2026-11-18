using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The Polish tab: fix and improve a recorded performance without performing it again.
    //   Motion edits make a new take from the active one (the old take stays).
    //   Layers (look-at, reach, pose, reaction...) sit on top of the take and can be muted.
    //   In the viewport, drag a point of the selected actor's path to bend it.
    public sealed class PolishMode : IStudioMode, IStudioViewportTool
    {
        public string Title => "Polish";
        public int Order => 25;
        public bool Available => true;
        public bool TryGetRecord(StudioContext context, out string label, out Action start) { label = null; start = null; return false; }

        static float retime = .5f, slide = .25f;
        static int smooth = 3;
        static Vector3 nudge = new Vector3(0, 0, .5f);
        static readonly StudioTakes.CrowdOptions crowd = new StudioTakes.CrowdOptions();

        static StudioMark Chosen(StudioContext context) =>
            context.Selection as StudioMark ?? (context.Selection is StudioOverride layer ? context.Set?.Mark(layer.actor) : null);

        // ---- Viewport: drag the path ----
        int bendFrame = -1;
        Vector3 bendFrom;
        public static Vector3? BendPreview;   // where the dragged point is now (drawn by the overlay)

        public bool Down(PointerDownEvent e, StudioViewport viewport, StudioContext context)
        {
            var mark = Chosen(context); var stage = context.Stage;
            var track = mark == null ? null : context.Tape?.Find(mark.id);
            var take = track?.ActiveTake;
            if (take == null || stage == null) return false;
            // Is the pointer on the actor's path? Look for the nearest recorded point on screen.
            float best = 12; bendFrame = -1;
            for (int f = take.start; f < take.End; f += 2)
            {
                if (!ActorTrack.TryReadPosition(track.type, take.frames[f - take.start], out var point) || !viewport.ToPanel(point, out var screen)) continue;
                float distance = Vector2.Distance(screen, e.localPosition);
                if (distance < best) { best = distance; bendFrame = f; bendFrom = point; }
            }
            return bendFrame >= 0;
        }

        public void Move(PointerMoveEvent e, StudioViewport viewport, StudioContext context)
        {
            if (bendFrame < 0) return;
            // Slide the point along the ground plane at its own height.
            var plane = new Plane(Vector3.up, bendFrom);
            var ray = viewport.ToRay(e.localPosition);
            if (plane.Raycast(ray, out float distance)) BendPreview = ray.GetPoint(distance);
        }

        public void Up(PointerUpEvent e, StudioViewport viewport, StudioContext context)
        {
            var mark = Chosen(context);
            var track = mark == null ? null : context.Tape?.Find(mark.id);
            if (bendFrame >= 0 && BendPreview.HasValue && track != null && Vector3.Distance(BendPreview.Value, bendFrom) > .05f)
            {
                try
                {
                    StudioMotionEdit.Bend(track, bendFrame, BendPreview.Value - bendFrom, Mathf.RoundToInt(context.Set.fps * .75f));
                    context.window.TapeChanged();
                    context.Say($"Bent {mark.actor}'s path. It is a new take; the old one is still there.");
                }
                catch (Exception error) { context.Say(error.Message); }
            }
            bendFrame = -1; BendPreview = null;
        }

        // ---- Panel ----

        public void Build(VisualElement panel, StudioContext context)
        {
            panel.Add(StudioUi.Heading("POLISH"));
            var set = context.Set; var tape = context.Tape;
            if (set == null) { panel.Add(StudioUi.Hint("Choose or make a Set first.")); return; }
            var mark = Chosen(context);
            var track = mark == null ? null : tape?.Find(mark.id);
            if (mark == null || track?.ActiveTake == null)
            {
                panel.Add(StudioUi.Hint(mark == null ? "Click an actor in the list to polish their performance." : mark.actor + " has no take yet. Record one in the Perform tab."));
                Crowd(panel, context);
                return;
            }
            int fps = set.fps, from = context.window.InFrame, to = context.window.OutFrame + 1;
            bool ranged = context.window.InFrame > 0 || context.window.OutFrame < set.Frames - 1;
            panel.Add(StudioUi.Row(StudioUi.Dot(StudioColors.For(mark.color)), new Label(mark.actor + " · " + track.ActiveTake.name) { style = { unityFontStyleAndWeight = FontStyle.Bold } }));
            panel.Add(StudioUi.Hint(ranged ? $"Edits apply to {StudioUi.Time(from, fps)} to {StudioUi.Time(to, fps)}." : "Edits apply to the whole take. Press [ and ] on the timeline to pick a range."));

            void Edit(string what, Func<StudioTake> edit)
            {
                try
                {
                    var take = edit();
                    context.window.TapeChanged();
                    context.Say($"{what}: \"{take.name}\" is now {mark.actor}'s active take. The take it came from is untouched.");
                }
                catch (Exception error) { context.Say(error.Message); }
            }
            VisualElement Line(string label, VisualElement field, string button, Action click, string tooltip)
            {
                field.style.flexGrow = 1;
                var run = StudioUi.Small(button, click, tooltip);
                run.style.minWidth = 66;
                return StudioUi.Row(field, run);
            }
            var speed = new Slider("Speed", .25f, 2) { value = retime, showInputField = true };
            speed.RegisterValueChangedCallback(e => retime = e.newValue);
            panel.Add(Line("Speed", speed, "Retime", () => Edit("Retimed", () => StudioMotionEdit.Retime(track, from, to, retime, set.Frames)), "Play the range slower or faster. Everything after it moves along."));
            var later = new Slider("Later (s)", -2, 2) { value = slide, showInputField = true };
            later.RegisterValueChangedCallback(e => slide = e.newValue);
            panel.Add(Line("Later", later, "Slide", () => Edit("Slid in time", () => StudioMotionEdit.Slide(track, Mathf.RoundToInt(slide * fps), set.Frames)), "Move the whole take earlier or later."));
            var soft = new SliderInt("Smoothing", 1, 10) { value = smooth, showInputField = true };
            soft.RegisterValueChangedCallback(e => smooth = e.newValue);
            panel.Add(Line("Smoothing", soft, "Smooth", () => Edit("Smoothed", () => StudioMotionEdit.Smooth(track, from, to, smooth)), "Remove jitter from the range."));
            var move = new Vector3Field("Move by (m)") { value = nudge };
            move.RegisterValueChangedCallback(e => nudge = e.newValue);
            panel.Add(Line("Move", move, "Offset", () => Edit("Moved", () => StudioMotionEdit.Offset(track, from, to, nudge, Mathf.RoundToInt(fps * .4f))), "Shift the range in space, eased in and out."));
            var trim = StudioUi.Small("Trim to the range", () => Edit("Trimmed", () => StudioMotionEdit.Trim(track, from, to)));
            trim.SetEnabled(ranged);
            var feet = StudioUi.Small("Lock feet to the ground", () => Edit("Feet locked", () => StudioMotionEdit.LockFeet(context.Stage, track, from, to)),
                "After edits: put the feet back on the surface under them and hold planted feet still.");
            feet.SetEnabled(StudioRun.State == StudioState.Held && track.type == SurvivorTrack.Id);
            var others = track.Kept.Where(t => t != track.ActiveTake).ToList();
            var blend = StudioUi.Small("Blend into another take…", () =>
            {
                var menu = new GenericMenu();
                foreach (var take in others)
                {
                    var captured = take;
                    menu.AddItem(new GUIContent(take.name), false, () => Edit("Blended", () => StudioMotionEdit.BlendTakes(track, captured, from, to)));
                }
                menu.ShowAsContext();
            }, "The active take before the range, the other take after it, blended across the range.");
            blend.SetEnabled(others.Count > 0);
            panel.Add(StudioUi.Row(trim, feet));
            panel.Add(blend);
            panel.Add(StudioUi.Hint("In the viewport, drag a point of the path to bend it."));

            // Layers
            panel.Add(StudioUi.Heading("LAYERS ON TOP"));
            float now = context.Frame / (float)fps;
            panel.Add(StudioUi.Big("+  Add a layer at " + StudioUi.Time(context.Frame, fps), () =>
            {
                var menu = new GenericMenu();
                for (int i = 0; i < StudioOverrides.Kinds.Count; i++)
                {
                    string kind = StudioOverrides.Kinds[i];
                    menu.AddItem(new GUIContent(StudioOverrides.KindLabels[i]), false, () =>
                    {
                        StudioOverride made = null;
                        context.Edit("Add layer", () =>
                        {
                            made = new StudioOverride { actor = mark.id, kind = kind, from = now, to = Mathf.Min(set.length, now + (kind == "hitPause" ? .15f : 1.5f)) };
                            if (kind == "reaction") made.name = "knockback";
                            if (kind == "emote") made.name = Emotes.Survivor.FirstOrDefault() ?? "";
                            if (kind == "pose") made.crouch = .5f;
                            if (kind == "outfit") made.look = mark.appearance;
                            if (kind == "lookAt" || kind == "reach") made.point = mark.position + new Vector3(0, 1.4f, 2);
                            set.overrides.Add(made);
                        });
                        context.Select(made);
                    });
                }
                menu.ShowAsContext();
            }, "Look-at, reach, pose, reaction, emote, outfit, weapon or hit pause."));
            foreach (var layer in set.overrides.Where(o => o.actor == mark.id).OrderBy(o => o.from))
            {
                var captured = layer;
                int kind = Mathf.Max(0, StudioOverrides.Kinds.IndexOf(layer.kind));
                var name = new Label($"{StudioOverrides.KindLabels[kind]}  {layer.Start:0.00}–{layer.Stop:0.00}s"); name.AddToClassList("ts-item-name");
                var mute = StudioUi.Small(layer.muted ? "Muted" : "On", () => context.Edit("Mute layer", () => captured.muted = !captured.muted));
                mute.EnableInClassList("ts-small--on", !layer.muted);
                var row = StudioUi.Row(name, mute);
                row.AddToClassList("ts-item");
                row.EnableInClassList("ts-item--selected", ReferenceEquals(layer, context.Selection));
                row.RegisterCallback<ClickEvent>(_ => context.Select(captured));
                panel.Add(row);
            }
            if (set.overrides.Any(o => !string.IsNullOrEmpty(o.trigger)))
                panel.Add(StudioUi.Small("Find trigger times again", () =>
                {
                    List<string> notes = null;
                    context.Edit("Resolve triggers", () => notes = StudioOverrides.Resolve(set, tape, context.Stage?.anchor ?? Vector3.zero));
                    context.Say(notes.Count > 0 ? string.Join("  ", notes) : "Nothing to resolve.");
                }, "Reactions set off by a cue or by a block touching the actor read their start time from the tape."));
            Crowd(panel, context);
        }

        void Crowd(VisualElement panel, StudioContext context)
        {
            var set = context.Set; var tape = context.Tape;
            var performed = set.marks.Where(m => m.role != StudioRole.Hand && tape.Find(m.id)?.ActiveTake != null && !m.actor.StartsWith("Crowd ")).ToList();
            if (performed.Count == 0) return;
            var fold = new Foldout { text = "Make a crowd", value = false };
            fold.Add(StudioUi.Hint($"Turns your {performed.Count} performed take(s) into a group. Each person gets their own place, timing, mirroring and outfit."));
            var count = new SliderInt("People", 2, 40) { value = crowd.count, showInputField = true };
            count.RegisterValueChangedCallback(e => crowd.count = e.newValue);
            var radius = new Slider("Spread (m)", 1, 20) { value = crowd.radius, showInputField = true };
            radius.RegisterValueChangedCallback(e => crowd.radius = e.newValue);
            var spacing = new Slider("Apart (m)", .5f, 3) { value = crowd.spacing, showInputField = true };
            spacing.RegisterValueChangedCallback(e => crowd.spacing = e.newValue);
            var time = new Slider("Timing spread (s)", 0, 2) { value = crowd.timeSpread, showInputField = true };
            time.RegisterValueChangedCallback(e => crowd.timeSpread = e.newValue);
            var mirror = new Toggle("Mirror every other person") { value = crowd.mirror };
            mirror.RegisterValueChangedCallback(e => crowd.mirror = e.newValue);
            var outfits = new Toggle("Random outfits") { value = crowd.outfits };
            outfits.RegisterValueChangedCallback(e => crowd.outfits = e.newValue);
            fold.Add(count); fold.Add(radius); fold.Add(spacing); fold.Add(time); fold.Add(mirror); fold.Add(outfits);
            fold.Add(StudioUi.Small("Make the crowd", () =>
            {
                try
                {
                    List<string> notes = null; int made = 0;
                    crowd.seed = set.marks.Count * 17 + 3;
                    // With the stage open, people are only put where there is ground to stand on.
                    var open = StudioRun.State == StudioState.Held ? context.Stage : null;
                    crowd.ground = open == null ? (Func<Vector3, float?>)null : open.GroundAt;
                    context.Edit("Make crowd", () => made = StudioTakes.Crowd(set, tape, performed, crowd, out notes).Count, true);
                    context.window.TapeChanged();
                    context.Say($"Made a crowd of {made}. " + string.Join(" ", notes));
                }
                catch (Exception error) { context.Say(error.Message); }
            }));
            var crowdMarks = set.marks.Where(m => m.actor.StartsWith("Crowd ")).ToList();
            if (crowdMarks.Count > 0)
                fold.Add(StudioUi.Small($"Remove the crowd ({crowdMarks.Count})", () => context.Edit("Remove crowd", () => set.marks.RemoveAll(m => m.actor.StartsWith("Crowd ")), true),
                    "Removes the crowd's marks. Their takes stay on the tape."));
            panel.Add(fold);
        }
    }

    public sealed class OverrideInspector : IStudioInspector
    {
        public bool Handles(object selection, StudioContext context) => selection is StudioOverride;

        public void Build(VisualElement panel, object selection, StudioContext context)
        {
            var layer = (StudioOverride)selection;
            var set = context.Set;
            int kind = Mathf.Max(0, StudioOverrides.Kinds.IndexOf(layer.kind));
            panel.Add(new Label(StudioOverrides.KindLabels[kind] + " on " + (set.Mark(layer.actor)?.actor ?? "?")) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 14 } });
            panel.Add(StudioFields.Switch(context, "Muted", layer.muted, v => layer.muted = v));
            panel.Add(StudioFields.Slide(context, "From (s)", layer.from, 0, set.length, v => { float length = layer.Length; layer.from = v; layer.to = v + length; }));
            panel.Add(StudioFields.Slide(context, "Lasts (s)", layer.Length, .05f, Mathf.Max(.1f, set.length), v => layer.to = layer.from + v));
            if (layer.kind != "hitPause" && layer.kind != "emote" && layer.kind != "outfit" && layer.kind != "equipment" && layer.kind != "colorFlash")
            {
                panel.Add(StudioFields.Slide(context, "Ease in (s)", layer.blendIn, 0, 2, v => layer.blendIn = v));
                panel.Add(StudioFields.Slide(context, "Ease out (s)", layer.blendOut, 0, 2, v => layer.blendOut = v));
                panel.Add(StudioFields.Slide(context, "Amount", layer.strength, 0, layer.kind == "reaction" ? 3 : 1, v => layer.strength = v));
            }

            if (layer.kind == "lookAt" || layer.kind == "reach")
            {
                var ids = new List<string> { "", "hazard" }.Concat(set.marks.Where(m => m.id != layer.actor).Select(m => "actor:" + m.id)).Concat(set.cameras.Select(c => "camera:" + c.id)).ToList();
                var names = new List<string> { "A point", "The disaster" }.Concat(set.marks.Where(m => m.id != layer.actor).Select(m => m.actor)).Concat(set.cameras.Select(c => "Camera: " + c.name)).ToList();
                if (DirectMode.Picked.Count > 0) { ids.Add("block:" + DirectMode.Picked[0]); names.Add("The picked block (#" + DirectMode.Picked[0] + ")"); }
                else if (layer.target.StartsWith("block:")) { ids.Add(layer.target); names.Add("Block #" + layer.target.Substring(6)); }
                panel.Add(StudioFields.Choice(context, layer.kind == "lookAt" ? "Looks at" : "Reaches for", names, Mathf.Max(0, ids.IndexOf(layer.target)), i => layer.target = ids[Mathf.Max(0, i)]));
                if (layer.target == "")
                {
                    panel.Add(StudioFields.Point(context, "Point", layer.point, v => layer.point = v));
                    var here = StudioUi.Small("Point = what the viewport looks at", () => context.Edit("Move target", () => layer.point = context.Stage.ToStage(StudioCamera.Target)));
                    here.SetEnabled(context.StageOpen);
                    panel.Add(here);
                }
                if (layer.kind == "reach") panel.Add(StudioFields.Switch(context, "Left hand", layer.leftHand, v => layer.leftHand = v));
                if (layer.kind == "lookAt") panel.Add(StudioUi.Hint("The head turns as far as a neck can (85° either side of where the body faces)."));
            }
            if (layer.kind == "pose")
            {
                panel.Add(StudioFields.Slide(context, "Crouch", layer.crouch, 0, 1, v => layer.crouch = v));
                panel.Add(StudioFields.Slide(context, "Lean", layer.lean, -1, 1, v => layer.lean = v));
                panel.Add(StudioFields.Slide(context, "Flinch", layer.flinch, 0, 1, v => layer.flinch = v));
                panel.Add(StudioFields.Slide(context, "Arms up", layer.armRaise, 0, 1, v => layer.armRaise = v));
            }
            if (layer.kind == "reaction")
            {
                panel.Add(StudioFields.Choice(context, "Reaction", StudioOverrides.ReactionLabels, Mathf.Max(0, StudioOverrides.Reactions.IndexOf(layer.name)), i => layer.name = StudioOverrides.Reactions[Mathf.Max(0, i)]));
                var triggers = new List<string> { "", "contact" }.Concat(set.cues.Select(c => "cue:" + c.id)).ToList();
                var triggerNames = new List<string> { "Its own time", "When a recorded block touches them" }.Concat(set.cues.Select(c => $"Cue \"{StudioCues.Get(c.action)?.Label ?? c.action}\" at {c.at:0.00}s")).ToList();
                panel.Add(StudioFields.Choice(context, "Starts", triggerNames, Mathf.Max(0, triggers.IndexOf(layer.trigger)), i =>
                {
                    layer.trigger = triggers[Mathf.Max(0, i)];
                    StudioOverrides.Resolve(set, context.Tape, context.Stage?.anchor ?? Vector3.zero);
                }));
                if (!string.IsNullOrEmpty(layer.trigger))
                    panel.Add(StudioUi.Hint(layer.resolvedFrom >= 0 ? $"Found on the tape: starts at {layer.resolvedFrom:0.00} s." : "Not found on the tape yet, so it uses \"From\" above."));
                panel.Add(StudioUi.Hint("Reactions are built from the head, hands and feet, like a performance. There is no ragdoll physics."));
            }
            if (layer.kind == "emote")
            {
                var emotes = Emotes.Survivor.ToList();
                panel.Add(StudioFields.Choice(context, "Emote", emotes, Mathf.Max(0, emotes.IndexOf(layer.name)), i => layer.name = emotes[Mathf.Max(0, i)]));
            }
            if (layer.kind == "equipment")
                panel.Add(StudioFields.Choice(context, "Holds", SetLists.Equipment, Mathf.Max(0, SetLists.Equipment.IndexOf(layer.name)), i => layer.name = SetLists.Equipment[Mathf.Max(0, i)]));
            if (layer.kind == "colorFlash") panel.Add(StudioFields.Swatches(context, layer.color, v => layer.color = v));
            if (layer.kind == "outfit")
            {
                var look = layer.look;
                panel.Add(StudioFields.Enum(context, "Skin", look.skin, v => { look.skin = v; layer.look = look; }));
                panel.Add(StudioFields.Enum(context, "Hair", look.hair, v => { look.hair = v; layer.look = look; }));
                panel.Add(StudioFields.Enum(context, "Eyewear", look.eyewear, v => { look.eyewear = v; layer.look = look; }));
                panel.Add(StudioFields.Flags(context, "Clothing", look.clothing, v => { look.clothing = v; layer.look = look; }));
            }
            if (layer.kind == "hitPause") panel.Add(StudioUi.Hint("Playback holds the whole tape still for this long (a few frames sells a hit). Recording is not affected."));
            panel.Add(StudioUi.Small("Delete layer", () => { context.Edit("Delete layer", () => set.overrides.Remove(layer)); context.Select(set.Mark(layer.actor)); }));
        }
    }

    // The selected actor's path with a tick every half second, and the point being dragged.
    public sealed class PolishOverlay : IStudioOverlay
    {
        public void Draw(StudioOverlayPainter painter, StudioContext context)
        {
            if (!(context.window.Mode is PolishMode)) return;
            var set = context.Set; var tape = context.Tape;
            var mark = context.Selection as StudioMark ?? (context.Selection is StudioOverride layer ? set?.Mark(layer.actor) : null);
            var track = mark == null ? null : tape?.Find(mark.id);
            var take = track?.ActiveTake;
            if (take == null) return;
            var color = StudioColors.For(mark.color);
            int every = Mathf.Max(1, set.fps / 2);
            for (int f = take.start; f < take.End; f += every)
                if (ActorTrack.TryReadPosition(track.type, take.frames[f - take.start], out var point)) painter.Dot(point + Vector3.up * .05f, color, f % set.fps == 0 ? 5 : 3);
            if (PolishMode.BendPreview.HasValue) painter.Dot(PolishMode.BendPreview.Value, Color.white, 7);
            // Targets of point look-ats and reaches.
            if (context.Selection is StudioOverride selected && (selected.kind == "lookAt" || selected.kind == "reach") && context.Stage != null &&
                StudioOverrides.Target(set, tape, context.Stage.anchor, selected, context.Frame / (float)set.fps, out var target))
                painter.Dot(target, Color.white, 6);
        }
    }
}
