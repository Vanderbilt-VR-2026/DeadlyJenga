using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The left list: everything in the Set, grouped. Each row has a color dot, the take picker,
    // and mute (M), solo (S) and lock (L).
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

            var search = new UnityEditor.UIElements.ToolbarSearchField { tooltip = "Find a cast member, world track or camera by name." };
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
            row.Add(StudioUi.Dot(color));
            string status = performing && !PerformMode.HasPerformance(track) ? " · No take" : "";
            var label = new Label(name + status) { tooltip = name + status };
            label.AddToClassList("ts-item-name");
            row.Add(label);
            if (track == null) return row;

            if (!string.IsNullOrEmpty(track.badge))
            {
                var badge = new Label("!") { tooltip = track.badge };
                badge.AddToClassList("ts-badge");
                row.Add(badge);
            }
            var kept = track.Kept.ToList();
            if (kept.Count > 0)
            {
                int position = track.ActiveTake == null ? 0 : kept.IndexOf(track.ActiveTake) + 1;
                var takes = new Button { text = position + "/" + kept.Count, tooltip = "Pick the active take (number keys work too)" };
                takes.AddToClassList("ts-mini"); takes.AddToClassList("ts-take");
                takes.style.width = StyleKeyword.Auto;
                takes.RegisterCallback<ClickEvent>(e => e.StopPropagation());
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
                row.Add(takes);
            }
            row.Add(Mini("M", track.mode == TrackMode.Muted, "Mute: leave this out of playback", () => track.mode = track.mode == TrackMode.Muted ? TrackMode.Playback : TrackMode.Muted));
            row.Add(Mini("S", track.solo, "Solo: play only the soloed tracks", () => track.solo = !track.solo));
            row.Add(Mini("L", track.locked, "Lock: this track cannot be re-recorded", () => track.locked = !track.locked));
            return row;
        }

        Button Mini(string text, bool on, string tooltip, Action toggle)
        {
            var button = new Button(() => { toggle(); context.window.TapeChanged(); }) { text = text, tooltip = tooltip };
            button.AddToClassList("ts-mini");
            button.EnableInClassList("ts-mini--on", on);
            // Do not also select the row.
            button.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            return button;
        }
    }
}
