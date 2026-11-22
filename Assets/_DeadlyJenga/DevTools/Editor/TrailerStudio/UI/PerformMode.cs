using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The Perform tab: choose who to play, how the others look to you, then press record.
    public sealed class PerformMode : IStudioMode
    {
        public string Title => "Perform";
        public int Order => 20;
        public bool Available => true;

        static readonly StudioTakes.CloneOptions clone = new StudioTakes.CloneOptions();
        static int cloneCount = 1;
        bool showRecordedCast;

        static bool IsActor(StudioTrack track) => track.type == SurvivorTrack.Id || track.type == HandTrack.Id;
        internal static bool HasPerformance(StudioTrack track) => track?.ActiveTake?.frames.Count > 0;

        StudioMark Chosen(StudioContext context)
        {
            // Clicking an actor in the list or viewport chooses who you play.
            if (context.Selection is StudioMark selected && StudioPerform.Actor != selected.id) StudioPerform.Actor = selected.id;
            return StudioPerform.ActorMark(context.Set);
        }

        public bool TryGetRecord(StudioContext context, out string label, out Action start)
        {
            label = null; start = null;
            var mark = context.Set == null ? null : Chosen(context);
            if (mark == null) { label = "Pick an actor to play"; start = () => context.Say("Click an actor in the list, then press record."); return true; }
            bool punch = StudioPerform.PunchIn && !StudioPerform.LivePhysics && StudioPerform.CanPunch(context.Tape, mark, context.Frame);
            label = punch ? $"Punch in {mark.actor} at {StudioUi.Time(context.Frame, context.Set.fps)}"
                : $"Record {mark.actor}, take {StudioPerform.NextTake(context.Tape, mark)}";
            start = () => StudioPerform.Record(context.Frame);
            return true;
        }

        public void Build(VisualElement panel, StudioContext context)
        {
            panel.Add(StudioUi.Heading("PERFORM"));
            var set = context.Set; var tape = context.Tape;
            if (set == null) { panel.Add(StudioUi.Hint("Choose or make a Set first.")); return; }
            if (set.marks.Count == 0) { panel.Add(StudioUi.Hint("This Set has no actors yet. Add marks in the Set tab.")); return; }
            var mark = Chosen(context);
            ActorPicker(panel, context, mark);
            if (mark == null) panel.Add(StudioUi.Hint("Click an actor in the list to choose who you play."));
            else
            {
                var who = StudioUi.Row(StudioUi.Dot(StudioColors.For(mark.color)), new Label("You play " + mark.actor) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 14 } });
                panel.Add(who);
                panel.Add(StudioUi.Hint(mark.role == StudioRole.Hand ? "You fly the hand with the normal hand controls. Its pulls are real."
                    : "You spawn on the mark and play with the normal game controls, on desktop or in the headset."));
            }
            if (!context.StageOpen) panel.Add(StudioUi.Hint("Open the stage, then press record. Click the Game view to take control."));
            if (context.Stage != null && context.Stage.performer.Length > 0 && StudioRun.State == StudioState.Held)
            {
                var occupied = set.Mark(context.Stage.performer);
                if (occupied != null && occupied != mark)
                    panel.Add(StudioUi.Hint($"The stage camera is still with {occupied.actor}. Record to take control of {mark?.actor ?? "the next actor"}."));
                panel.Add(StudioUi.Small("Step out of the body", () => { StudioRun.LeaveBody(); context.Rebuild(); }, "Go back to the director's view."));
            }

            Others(panel, context, mark);

            // ---- How to record ----
            void Flag(string label, bool value, Action<bool> apply, string tooltip, bool enabled = true)
            {
                var toggle = new Toggle(label) { value = value, tooltip = tooltip };
                toggle.AddToClassList("ts-field");
                toggle.SetEnabled(enabled);
                toggle.RegisterValueChangedCallback(e => { apply(e.newValue); context.Rebuild(); });
                panel.Add(toggle);
            }
            void Pick(string label, List<string> names, int index, Action<int> apply, string tooltip)
            {
                var field = new PopupField<string>(label, names, Mathf.Clamp(index, 0, names.Count - 1)) { tooltip = tooltip };
                field.AddToClassList("ts-field");
                field.RegisterValueChangedCallback(e => { apply(names.IndexOf(e.newValue)); context.Rebuild(); });
                panel.Add(field);
            }
            Flag("3-2-1 countdown", StudioPerform.Countdown, v => StudioPerform.Countdown = v, "Three seconds to get ready before the tape rolls.");
            var rolls = new List<string> { "None", "1 second", "2 seconds", "3 seconds", "5 seconds" };
            float[] rollValues = { 0, 1, 2, 3, 5 };
            Pick("Pre-roll", rolls, Array.IndexOf(rollValues, StudioPerform.PreRoll), i => StudioPerform.PreRoll = rollValues[Mathf.Max(0, i)],
                "Play the tape for a moment before recording starts, so you can get moving.");
            bool canPunch = mark != null && !StudioPerform.LivePhysics && StudioPerform.CanPunch(tape, mark, context.Frame);
            Flag("Punch in at the playhead", StudioPerform.PunchIn, v => StudioPerform.PunchIn = v,
                "Keep your active take up to the playhead, ride along it, then take over and redo the rest.", mark != null && !StudioPerform.LivePhysics);
            if (StudioPerform.PunchIn && mark != null && !canPunch && !StudioPerform.LivePhysics)
                panel.Add(StudioUi.Hint("Move the playhead inside this actor's active take to punch in."));
            var speeds = new List<string> { "Real speed", "Half speed (½×)", "Quarter speed (¼×)" };
            Pick("World speed", speeds, StudioPerform.Speed > .75f ? 0 : StudioPerform.Speed > .4f ? 1 : 2, i => StudioPerform.Speed = i == 1 ? .5f : i == 2 ? .25f : 1,
                "Perform in slow motion. The take is stored at real speed.");

            var more = StudioUi.Advanced();
            void MoreFlag(string label, bool value, Action<bool> apply, string tooltip)
            {
                var toggle = new Toggle(label) { value = value, tooltip = tooltip };
                toggle.RegisterValueChangedCallback(e => { apply(e.newValue); context.Rebuild(); });
                more.Add(toggle);
            }
            MoreFlag("Loop record", StudioPerform.Loop, v => StudioPerform.Loop = v, "Record again and again, a new take each time, until you press stop.");
            MoreFlag("Live physics", StudioPerform.LivePhysics, v => StudioPerform.LivePhysics = v,
                "Use this when this actor breaks things (hand pulls, punches, explosions). The world is simulated again and saved as a new world take.");
            MoreFlag("Path lines on the floor", StudioPerform.Paths, v => StudioPerform.Paths = v, "Show where the others go, and where your last take went.");
            if (set.marks.Any(m => m.role == StudioRole.Hand))
            {
                var hands = new List<string> { "Performed by you", "Hand bot (the game's own hand AI)" };
                var source = StudioFields.Choice(context, "The hand is", hands, set.handBot ? 1 : 0, i => set.handBot = i == 1);
                source.tooltip = "A hand bot is recorded with \"Record whole set\" in the Set tab.";
                more.Add(source);
            }
            panel.Add(more);
            if (StudioPerform.LivePhysics) panel.Add(StudioUi.Hint("Live physics: other actors' takes may go stale if the world changes under them. The Studio will tell you where."));

            Stale(panel, context);
            if (mark != null) TakeTools(panel, context, mark);
        }

        void ActorPicker(VisualElement panel, StudioContext context, StudioMark performer)
        {
            var set = context.Set;
            var tape = context.Tape;
            string ActorLabel(StudioMark actor)
            {
                if (actor == null) return "Choose an actor";
                var track = tape.Find(actor.id);
                var state = HasPerformance(track) ? "Recorded" : "No take";
                if (track?.locked == true) state = "Locked";
                if (actor.role == StudioRole.Hand && set.handBot) state = "Hand bot";
                return $"{actor.actor} ({actor.role}) · {state}";
            }
            var choices = set.marks.ToList();
            if (performer == null) choices.Insert(0, null);
            var picker = new PopupField<StudioMark>("Record as", choices, choices.IndexOf(performer), ActorLabel, ActorLabel);
            picker.AddToClassList("ts-field");
            picker.RegisterValueChangedCallback(e =>
            {
                if (e.newValue != null) ChooseActor(context, e.newValue);
            });
            panel.Add(picker);
            bool canSwitch = StudioRun.State != StudioState.Running && StudioRun.State != StudioState.Opening;
            picker.SetEnabled(canSwitch);

            var next = choices.FirstOrDefault(actor => actor != null && actor != performer
                && (actor.role != StudioRole.Hand || !set.handBot)
                && tape.Find(actor.id)?.locked != true && !HasPerformance(tape.Find(actor.id)));
            if (next != null)
            {
                var shortcut = StudioUi.Small($"Choose unrecorded actor: {next.actor}", () => ChooseActor(context, next));
                shortcut.SetEnabled(canSwitch);
                panel.Add(shortcut);
            }
            var current = performer == null ? null : tape.Find(performer.id);
            if (current?.locked == true) panel.Add(StudioUi.Warning("Unlock this actor in the cast list to record."));
            if (performer?.role == StudioRole.Hand && set.handBot)
                panel.Add(StudioUi.Warning("Set the hand to Performed by you in Advanced to record it yourself."));
        }

        internal static void ChooseActor(StudioContext context, StudioMark actor)
        {
            StudioPerform.Actor = actor.id;
            context.Select(actor);
        }

        // Honest status: takes performed against a world that has since been re-recorded.
        void Stale(VisualElement panel, StudioContext context)
        {
            var tape = context.Tape; var set = context.Set;
            foreach (var track in tape.tracks.Where(IsActor))
            {
                int frame = StudioTrackNotes.Of(track).staleFrame;
                if (frame < 0) continue;
                var captured = track;
                var card = StudioUi.Card(StudioUi.Warning($"{track.name} is stale: the world first differs at {StudioUi.Time(frame, set.fps)}."));
                card.Add(StudioUi.Row(
                    StudioUi.Small("Keep", () => { StudioTakes.AcceptStale(tape, captured); context.window.TapeChanged(); }, "The old performance still works: clear the warning."),
                    StudioUi.Small("Punch in there", () =>
                    {
                        StudioPerform.Actor = captured.id; StudioPerform.PunchIn = true; StudioPerform.LivePhysics = false;
                        context.window.Scrub(Mathf.Max(1, frame));
                        context.Select(set.Mark(captured.id));
                    }, "Put the playhead where the world first differs and get ready to redo the rest."),
                    StudioUi.Small("Re-record", () => { StudioPerform.Actor = captured.id; StudioPerform.PunchIn = false; context.Select(set.Mark(captured.id)); }, "Choose this actor, then press record.")));
                panel.Add(card);
            }
        }

        // How each already recorded actor looks to you while you perform.
        void Others(VisualElement panel, StudioContext context, StudioMark performer)
        {
            var tape = context.Tape; var set = context.Set;
            bool anySolo = tape.tracks.Any(t => t.solo);
            var others = set.marks.Where(m => m != performer)
                .Select(mark => (mark, track: tape.Find(mark.id)))
                .Where(actor => HasPerformance(actor.track)).ToList();
            int playing = others.Count(actor => actor.track.mode != TrackMode.Muted && (!anySolo || actor.track.solo));
            panel.Add(StudioUi.Heading("RECORDED CAST"));
            if (others.Count == 0)
            {
                panel.Add(StudioUi.Hint("Record one actor, then choose another to perform against the first actor’s take."));
                return;
            }
            panel.Add(StudioUi.Hint($"{playing} of {others.Count} other recorded actors play back during this pass."));
            var fold = new Foldout { text = "Playback appearance and collision", value = showRecordedCast };
            fold.RegisterValueChangedCallback(e => showRecordedCast = e.newValue);
            var list = new ScrollView { viewDataKey = "perform-recorded-cast-scroll" };
            list.style.maxHeight = 246;
            list.SetEnabled(StudioRun.State != StudioState.Running && StudioRun.State != StudioState.Opening);
            fold.Add(list);
            panel.Add(fold);
            var looks = new List<string> { "Character", "Ghost", "Hidden" };
            var collisions = new List<string> { "Solid", "Walk through" };
            foreach (var (other, track) in others)
            {
                var notes = StudioTrackNotes.Of(track);
                var state = track.mode == TrackMode.Muted ? "Muted" : anySolo && !track.solo ? "Excluded by solo" : track.ActiveTake.name;
                var name = new Label(other.actor + " · " + state) { tooltip = other.actor + " · " + state };
                name.AddToClassList("ts-item-name");
                var look = new PopupField<string>(looks, Mathf.Clamp(notes.ghost, 0, 2))
                {
                    tooltip = "Show this replay as a character, a see-through ghost, or hide it during your performance."
                };
                look.RegisterValueChangedCallback(e =>
                {
                    StudioTrackNotes.Update(track, n => n.ghost = looks.IndexOf(e.newValue));
                    context.window.TapeChanged();
                });
                var solid = new PopupField<string>(collisions, notes.passThrough ? 1 : 0)
                {
                    tooltip = "Solid replays can block movement. The actor follows the recorded take."
                };
                solid.RegisterValueChangedCallback(e =>
                {
                    StudioTrackNotes.Update(track, n => n.passThrough = e.newValue == collisions[1]);
                    context.window.TapeChanged();
                });
                list.Add(StudioUi.Card(
                    StudioUi.Row(StudioUi.Dot(StudioColors.For(other.color)), name),
                    StudioUi.Row(look, solid)));
            }
        }

        // Comps and clones for the chosen actor.
        void TakeTools(VisualElement panel, StudioContext context, StudioMark mark)
        {
            var tape = context.Tape; var set = context.Set;
            var track = tape.Find(mark.id);
            if (track?.ActiveTake == null) return;
            panel.Add(StudioUi.Heading("TAKE TOOLS"));
            var kept = track.Kept.Where(t => t != track.ActiveTake && t.Covers(context.Frame)).ToList();
            var comp = StudioUi.Small($"From {StudioUi.Time(context.Frame, set.fps)} on, use another take…", () =>
            {
                var menu = new GenericMenu();
                foreach (var take in kept)
                {
                    var captured = take;
                    menu.AddItem(new GUIContent(take.name), false, () =>
                    {
                        try
                        {
                            StudioTakes.Comp(track, captured, context.Frame, Mathf.RoundToInt(.2f * set.fps));
                            context.window.TapeChanged();
                            context.Say($"Made a comp for {mark.actor}. The takes it came from are untouched.");
                        }
                        catch (Exception error) { context.Say(error.Message); }
                    });
                }
                menu.ShowAsContext();
            }, "Comp: build a new take from the active take up to the playhead and another take after it.");
            comp.SetEnabled(kept.Count > 0 && context.Frame > track.ActiveTake.start);
            panel.Add(comp);
            if (kept.Count == 0) panel.Add(StudioUi.Hint("Record a second take to combine parts of takes."));

            var fold = new Foldout { text = "Clone this performance", value = false };
            fold.Add(StudioUi.Hint("One performance becomes several people: a copy on a new actor, moved, turned, delayed or mirrored."));
            Vector3Field offset = new Vector3Field("Move by (m)") { value = clone.offset };
            offset.RegisterValueChangedCallback(e => clone.offset = e.newValue);
            var turn = new Slider("Turn", -180, 180) { value = clone.turn, showInputField = true };
            turn.RegisterValueChangedCallback(e => clone.turn = e.newValue);
            var delay = new Slider("Start later (s)", -2, 2) { value = clone.delay, showInputField = true };
            delay.RegisterValueChangedCallback(e => clone.delay = e.newValue);
            var mirror = new Toggle("Mirror left and right") { value = clone.mirror };
            mirror.RegisterValueChangedCallback(e => clone.mirror = e.newValue);
            var jitter = new Slider("Variation", 0, 1) { value = clone.jitter, showInputField = true, tooltip = "Small random differences in place and timing, so clones do not move as one." };
            jitter.RegisterValueChangedCallback(e => clone.jitter = e.newValue);
            var count = new SliderInt("How many", 1, 8) { value = cloneCount, showInputField = true };
            count.RegisterValueChangedCallback(e => cloneCount = e.newValue);
            fold.Add(offset); fold.Add(turn); fold.Add(delay); fold.Add(mirror); fold.Add(jitter); fold.Add(count);
            fold.Add(StudioUi.Small("Make clones", () =>
            {
                try
                {
                    context.Edit("Clone " + mark.actor, () =>
                    {
                        for (int i = 0; i < cloneCount; i++)
                        {
                            var options = new StudioTakes.CloneOptions { offset = clone.offset * (i + 1), turn = clone.turn, delay = clone.delay * (i + 1),
                                mirror = clone.mirror, jitter = clone.jitter, seed = set.marks.Count * 31 + i };
                            StudioTakes.Clone(set, tape, mark, options);
                        }
                    }, true);
                    context.window.TapeChanged();
                    context.Say($"Made {cloneCount} clone(s) of {mark.actor}. Each is its own actor with its own take.");
                }
                catch (Exception error) { context.Say(error.Message); }
            }));
            panel.Add(fold);
        }
    }
}
