using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The left list shows the Set's tracks, actors and cameras.
    public sealed class StudioCastList
    {
        readonly StudioContext context;
        readonly ScrollView host;
        string filter = "";
        static bool RunActive => StudioRun.Busy || StudioHandheld.Recording;

        public StudioCastList(StudioContext context, ScrollView host) { this.context = context; this.host = host; }

        public void Rebuild()
        {
            host.Clear();
            var set = context.Set; var tape = context.Tape;
            if (set == null)
            {
                host.Add(Empty("No Set yet", "A Set is one scene: a map, who is in it and what happens.", "New set",
                    () => { context.window.LoadSet(StudioLibrary.Create("Untitled set").id); context.Rebuild(); }));
                return;
            }

            var search = new UnityEditor.UIElements.ToolbarSearchField
            {
                placeholderText = "Search cast, tracks and cameras",
                tooltip = "Filter by name"
            };
            search.style.width = StyleKeyword.Auto;
            search.SetValueWithoutNotify(filter);
            host.Add(search);
            var items = new VisualElement();
            host.Add(items);
            search.RegisterValueChangedCallback(e =>
            {
                filter = e.newValue;
                Render();
            });
            Render();

            void Render()
            {
                items.Clear();
                string query = filter.Trim();
                bool Match(string name) => name?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                void AddGroup(string title, VisualElement[] rows, string empty = null)
                {
                    if (rows.Length == 0 && (query.Length != 0 || empty == null)) return;
                    var label = new Label(title.ToUpperInvariant());
                    label.AddToClassList("ts-group");
                    items.Add(label);
                    foreach (var row in rows) items.Add(row);
                    if (rows.Length == 0) items.Add(Quiet(empty));
                }
                VisualElement ActorRow(StudioMark mark) => Row(mark.actor + (mark.role == StudioRole.Bot ? "  (bot)" : ""),
                    StudioColors.For(mark.color), tape.Find(mark.id), mark);

                var worldTracks = tape.tracks.Where(t => t.type != SurvivorTrack.Id && t.type != HandTrack.Id && Match(t.name));
                AddGroup("World", worldTracks.Select(track =>
                    Row(track.name, StudioTrackTypes.Get(track.type)?.Color ?? Color.gray, track, track)).ToArray(), "No world tracks.");
                var actors = set.marks.Where(m => Match(m.actor));
                AddGroup("Hand", actors.Where(m => m.role == StudioRole.Hand).Select(ActorRow).ToArray(), "No hand yet.");
                AddGroup("Survivors", actors.Where(m => m.role != StudioRole.Hand).Select(ActorRow).ToArray(), "Place a mark to add your first actor.");
                var cameras = set.cameras.Where(c => Match(c.name)).Select(c => Row(c.name, StudioColors.For(c.color), null, c));
                var shots = set.shots.Where(s => Match(s.name)).Select(s => Row(s.name, StudioColors.Camera, null, null));
                AddGroup("Cameras", cameras.Concat(shots).ToArray(), "Cameras are added in Shoot.");
                if (items.childCount == 0) items.Add(Quiet("No cast, tracks or cameras match."));
            }
        }

        Label Quiet(string text)
        {
            var label = StudioUi.Hint(text);
            label.style.paddingLeft = 12;
            return label;
        }

        public static VisualElement Empty(string title, string text, string button, Action click)
        {
            var box = new VisualElement();
            box.AddToClassList("ts-empty");
            var heading = new Label(title); heading.AddToClassList("ts-empty-title");
            var body = new Label(text); body.AddToClassList("ts-empty-text");
            box.Add(heading); box.Add(body);
            if (button != null)
            {
                var action = StudioUi.Big(button, click);
                action.AddToClassList("ts-big--primary");
                box.Add(action);
            }
            return box;
        }

        VisualElement Row(string name, Color color, StudioTrack track, object select)
        {
            var row = new VisualElement();
            row.AddToClassList("ts-item");
            bool selected = select != null && ReferenceEquals(select, context.Selection);
            row.EnableInClassList("ts-item--selected", selected);
            bool performing = select is StudioMark && context.window.Mode is PerformMode;
            VisualElement title;
            if (select != null)
            {
                var pick = new Button();
                pick.AddToClassList("ts-row-picker");
                pick.AddToClassList("ts-row");
                pick.tooltip = (performing ? "Record as " : "Select ") + name;
                pick.SetEnabled(!performing || !RunActive || selected);
                pick.clicked += () =>
                {
                    if (performing && select is StudioMark actor)
                    {
                        if (RunActive || (selected && StudioPerform.Actor == actor.id)) return;
                        PerformMode.ChooseActor(context, actor);
                    }
                    else
                    {
                        if (selected) return;
                        context.Select(select);
                    }
                    host.Q(className: "ts-item--selected")?.Q<Button>(className: "ts-row-picker")?.Focus();
                };
                title = pick;
            }
            else title = StudioUi.Row();
            title.Add(StudioUi.Dot(color));
            title.style.minHeight = 26;
            row.Add(title);
            row.style.flexDirection = FlexDirection.Column;
            row.style.alignItems = Align.Stretch;
            row.style.height = StyleKeyword.Auto;
            string status = performing && !PerformMode.HasPerformance(track) ? " · No take" : "";
            var label = new Label(name + status) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("ts-item-name");
            title.Add(label);
            if (select == null) title.tooltip = "Imported Theatre shot. Convert it to an editable camera in Shoot.";
            if (track == null) return row;

            if (!string.IsNullOrEmpty(track.badge))
            {
                var badge = new Label("!") { tooltip = track.badge };
                badge.AddToClassList("ts-badge");
                title.Add(badge);
            }

            if (!selected)
            {
                string state = string.Join(" · ", new[] {
                    track.mode == TrackMode.Muted ? "Muted" : null, track.solo ? "Solo" : null,
                    track.locked ? "Locked" : null
                }.Where(item => item != null));
                if (state.Length > 0)
                {
                    var summary = new Label(state) { tooltip = state };
                    summary.AddToClassList("ts-row-meta");
                    title.Add(summary);
                }
                row.tooltip = "Select " + name + " to choose a take, mute, solo or lock.";
                return row;
            }

            var actions = StudioUi.Row();
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginBottom = 4;
            if (RunActive)
            {
                var wait = StudioUi.Hint("Track controls are available when the run stops.");
                row.Add(wait);
            }
            row.Add(actions);
            var kept = track.Kept.ToList();
            if (kept.Count > 0)
            {
                int position = track.ActiveTake == null ? 0 : kept.IndexOf(track.ActiveTake) + 1;
                string takeText = position == 0 ? "Choose take" : $"Take {position}/{kept.Count}";
                bool canPickTake = kept.Count > 1 || position == 0;
                var takes = StateButton(takeText, false, canPickTake ? $"Pick the active take for {name}. Number keys also work."
                    : $"{name} has one kept take.", canPickTake && !RunActive, () =>
                {
                    if (!CanEdit(track)) return;
                    var menu = new GenericMenu();
                    for (int i = 0; i < kept.Count; i++)
                    {
                        var captured = kept[i];
                        string takeName = (captured.name ?? "").Replace('/', '∕');
                        var entry = new GUIContent($"{i + 1}. {(captured.starred ? "* " : "")}{takeName}");
                        if (captured == track.ActiveTake) menu.AddDisabledItem(entry, true);
                        else menu.AddItem(entry, false, () =>
                        {
                            if (!CanEdit(track)) return;
                            int index = track.takes.IndexOf(captured);
                            if (index >= 0) context.window.SetActiveTake(track, index);
                        });
                    }
                    menu.ShowAsContext();
                });
                actions.Add(takes);
            }
            bool muted = track.mode == TrackMode.Muted;
            bool playable = PerformMode.HasPerformance(track);
            actions.Add(StateButton(muted ? "Unmute" : "Mute", muted,
                muted ? $"Include {name} in playback, subject to solo." : playable ? $"Exclude {name} from playback." : "Record a take before muting.",
                !RunActive && (playable || muted), () => ChangeTrack(track, () => track.mode = muted ? TrackMode.Playback : TrackMode.Muted)));
            actions.Add(StateButton(track.solo ? "Unsolo" : "Solo", track.solo,
                track.solo ? $"Remove {name} from the solo group." : playable ? "Play only tracks marked Solo." : "Record a take before soloing.",
                !RunActive && (playable || track.solo), () => ChangeTrack(track, () => track.solo = !track.solo)));
            actions.Add(StateButton(track.locked ? "Unlock" : "Lock", track.locked,
                track.locked ? $"Allow {name} to be recorded again." : $"Protect {name} from re-recording.",
                !RunActive, () => ChangeTrack(track, () => track.locked = !track.locked)));
            return row;
        }

        bool CanEdit(StudioTrack track)
        {
            if (RunActive) { context.Say("Stop the current run before changing tracks."); return false; }
            return ReferenceEquals(context.Tape?.Find(track.id), track);
        }

        void ChangeTrack(StudioTrack track, Action change)
        {
            if (!CanEdit(track)) return;
            change();
            context.window.TapeChanged();
        }

        Button StateButton(string text, bool on, string tooltip, bool enabled, Action click)
        {
            var button = new Button(click) { text = text, tooltip = tooltip };
            button.AddToClassList("ts-mini");
            button.EnableInClassList("ts-mini--on", on);
            button.style.width = StyleKeyword.Auto;
            button.style.height = 22;
            button.style.paddingLeft = 5;
            button.style.paddingRight = 5;
            button.SetEnabled(enabled);
            return button;
        }
    }
}
