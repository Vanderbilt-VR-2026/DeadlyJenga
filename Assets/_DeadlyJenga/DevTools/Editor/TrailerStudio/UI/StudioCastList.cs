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
            row.EnableInClassList("ts-item--selected", select != null && ReferenceEquals(select, context.Selection));
            bool performing = select is StudioMark && context.window.Mode is PerformMode;
            if (select != null)
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    if (performing && select is StudioMark actor) PerformMode.ChooseActor(context, actor);
                    else context.Select(select);
                });
            var title = StudioUi.Row(StudioUi.Dot(color));
            title.style.minHeight = 26;
            row.Add(title);
            row.style.flexDirection = FlexDirection.Column;
            row.style.alignItems = Align.Stretch;
            row.style.height = StyleKeyword.Auto;
            string status = performing && !PerformMode.HasPerformance(track) ? " · No take" : "";
            var label = new Label(name + status) { tooltip = name + status };
            label.AddToClassList("ts-item-name");
            title.Add(label);
            if (track == null) return row;

            if (!string.IsNullOrEmpty(track.badge))
            {
                var badge = new Label("!") { tooltip = track.badge };
                badge.AddToClassList("ts-badge");
                title.Add(badge);
            }

            if (!ReferenceEquals(context.Selection, select))
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
            row.Add(actions);
            var kept = track.Kept.ToList();
            if (kept.Count > 0)
            {
                int position = track.ActiveTake == null ? 0 : kept.IndexOf(track.ActiveTake) + 1;
                string takeText = position == 0 ? "Choose take" : $"Take {position}/{kept.Count}";
                var takes = StateButton(takeText, false, $"Pick the active take for {name}. Number keys also work.");
                takes.clicked += () =>
                {
                    var menu = new GenericMenu();
                    foreach (var take in kept)
                    {
                        var captured = take;
                        menu.AddItem(new GUIContent((take.starred ? "* " : "") + take.name), take == track.ActiveTake,
                            () => context.window.SetActiveTake(track, track.takes.IndexOf(captured)));
                    }
                    menu.ShowAsContext();
                };
                actions.Add(takes);
            }
            bool muted = track.mode == TrackMode.Muted;
            actions.Add(StateButton(muted ? "Unmute" : "Mute", muted,
                muted ? $"Include {name} in playback, subject to solo." : $"Exclude {name} from playback.",
                () => track.mode = muted ? TrackMode.Playback : TrackMode.Muted));
            actions.Add(StateButton(track.solo ? "Unsolo" : "Solo", track.solo,
                track.solo ? $"Remove {name} from the solo group." : "Play only tracks marked Solo.",
                () => track.solo = !track.solo));
            actions.Add(StateButton(track.locked ? "Unlock" : "Lock", track.locked,
                track.locked ? $"Allow {name} to be recorded again." : $"Protect {name} from re-recording.",
                () => track.locked = !track.locked));
            return row;
        }

        Button StateButton(string text, bool on, string tooltip, Action toggle = null)
        {
            var button = new Button { text = text, tooltip = tooltip };
            if (toggle != null) button.clicked += () => { toggle(); context.window.TapeChanged(); };
            button.AddToClassList("ts-mini");
            button.EnableInClassList("ts-mini--on", on);
            button.style.width = StyleKeyword.Auto;
            button.style.height = 22;
            button.style.paddingLeft = 5;
            button.style.paddingRight = 5;
            // Do not also select the row.
            button.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            return button;
        }
    }
}
