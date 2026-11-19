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

            Group("World");
            foreach (var track in tape.tracks.Where(t => t.type != SurvivorTrack.Id && t.type != HandTrack.Id))
            {
                var type = StudioTrackTypes.Get(track.type);
                host.Add(Row(track.name, type?.Color ?? Color.gray, track, track));
            }

            Group("Hand");
            foreach (var mark in set.marks.Where(m => m.role == StudioRole.Hand))
                host.Add(Row(mark.actor, StudioColors.For(mark.color), tape.Find(mark.id), mark));
            if (!set.marks.Any(m => m.role == StudioRole.Hand)) host.Add(Quiet("No hand yet."));

            Group("Survivors");
            foreach (var mark in set.marks.Where(m => m.role != StudioRole.Hand))
                host.Add(Row(mark.actor + (mark.role == StudioRole.Bot ? "  (bot)" : ""), StudioColors.For(mark.color), tape.Find(mark.id), mark));
            if (!set.marks.Any(m => m.role != StudioRole.Hand)) host.Add(Quiet("Place a mark to add your first actor."));

            Group("Cameras");
            foreach (var camera in set.cameras) host.Add(Row(camera.name, StudioColors.For(camera.color), null, camera));
            foreach (var shot in set.shots) host.Add(Row(shot.name, StudioColors.Camera, null, null));
            if (set.cameras.Count == 0) host.Add(Quiet(set.shots.Count == 0 ? "Cameras are added last, in the Shoot mode." : "Kept from Theatre for the Shoot mode."));
        }

        void Group(string title)
        {
            var label = new Label(title.ToUpperInvariant());
            label.AddToClassList("ts-group");
            host.Add(label);
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
            if (select != null) row.RegisterCallback<ClickEvent>(_ => context.Select(select));
            row.Add(StudioUi.Dot(color));
            var label = new Label(name); label.AddToClassList("ts-item-name");
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
