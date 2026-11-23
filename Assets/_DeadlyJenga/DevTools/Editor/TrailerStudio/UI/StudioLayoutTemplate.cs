using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The open Set supplies the destination map. The saved layout supplies local-space marks and cameras.
    internal sealed class StudioLayoutTemplate
    {
        internal static StudioSet Draft(StudioSet source, string map, Vector3? mapPoint, List<string> omitted)
        {
            var copy = source.Clone();
            var result = new StudioSet
            {
                title = source.title + " on " + map, map = map, length = source.length,
                fps = source.fps, warmup = source.warmup, seed = source.seed, alive = copy.alive,
                storyBeats = copy.storyBeats ?? new List<StudioStoryBeat>(),
                handBot = source.handBot, marks = copy.marks, cameras = copy.cameras,
                timeOfDay = source.timeOfDay, music = source.music, bpm = source.bpm, beatOffset = source.beatOffset,
                useMapAnchor = mapPoint.HasValue, mapAnchor = mapPoint.GetValueOrDefault()
            };
            bool MapTarget(string target) => target?.StartsWith("block:", StringComparison.OrdinalIgnoreCase) == true;
            foreach (var camera in result.cameras.ToArray())
            {
                if (MapTarget(camera.rideTarget) || camera.aims.Any(key => MapTarget(key.target)))
                {
                    result.cameras.Remove(camera);
                    omitted.Add(camera.name + " targets a specific map block");
                    continue;
                }
                camera.measure.RemoveAll(MapTarget);
            }
            foreach (var cue in copy.cues)
            {
                if (cue.blocks.Count != 0 || cue.fixedGrab || MapTarget(cue.target) || cue.action == "handPull" ||
                    (cue.action == "hazard" && !SetLists.Hazards.Contains(cue.hazard)))
                {
                    omitted.Add(cue.action + " needs its target or hazard picked on this map");
                    continue;
                }
                result.cues.Add(cue);
            }
            foreach (var drive in copy.drives)
            {
                if (drive.steps.Any(step => MapTarget(step.target) || MapTarget(step.shoot)))
                    omitted.Add((source.Mark(drive.actor)?.actor ?? drive.actor) + " needs a new map-specific movement target");
                else result.drives.Add(drive);
            }
            int removed;
            do
            {
                var available = new HashSet<string>(result.cues.Select(c => c.id));
                removed = result.cues.RemoveAll(cue => !string.IsNullOrEmpty(cue.link) && !available.Contains(cue.link));
                if (removed > 0) omitted.Add(removed + " dependent cue(s) lost their trigger");
            } while (removed > 0);
            foreach (var layer in copy.overrides)
            {
                if (MapTarget(layer.target) || (layer.trigger?.StartsWith("cue:", StringComparison.Ordinal) == true &&
                    result.cues.All(c => c.id != layer.trigger.Substring(4))))
                {
                    omitted.Add(layer.name + " needs its target or trigger rebound");
                    continue;
                }
                layer.resolvedFrom = -1;
                result.overrides.Add(layer);
            }
            return result;
        }

        static void Recast(StudioMark role, StudioMark donor)
        {
            role.actor = donor.actor;
            role.appearance = donor.appearance;
            role.customLook = donor.customLook;
            role.equipment = donor.equipment;
            role.color = donor.color;
        }

        string destinationKey, sourceId, draftTitle;
        Vector3? origin;
        readonly Dictionary<string, string> casting = new Dictionary<string, string>();

        public void Add(VisualElement panel, StudioContext context)
        {
            var destination = context.Set;
            if (destination == null) return;
            string key = destination.id + "/" + destination.map;
            if (destinationKey != key)
            {
                destinationKey = key;
                sourceId = destination.id;
                origin = null;
                casting.Clear();
                draftTitle = null;
            }
            var fold = new Foldout { text = "Reuse a scene layout", value = false };
            panel.Add(fold);
            bool built = false;
            fold.RegisterValueChangedCallback(change =>
            {
                if (change.target != fold || !change.newValue || built) return;
                built = true;
                var ids = StudioLibrary.SetIds();
                if (ids.Count == 0) return;
                var sources = ids.Select(StudioLibrary.Load).Where(set => set != null).ToList();
                if (sources.Count == 0) return;
                var names = StudioFields.Distinct(sources.Select(set => set.title).ToList());
                int first = Mathf.Max(0, sources.FindIndex(set => set.id == sourceId));
                var sourceField = new PopupField<string>("Layout from", names, first);
                fold.Add(sourceField);
                fold.Add(StudioUi.Hint("Choose the map in Set settings. This makes a new Set with fresh recordings."));
                var contents = new VisualElement();
                fold.Add(contents);
                void Preview()
                {
                    contents.Clear();
                    var source = sources[sourceField.index];
                    var omitted = new List<string>();
                    var draft = Draft(source, destination.map, origin, omitted);
                    var stage = context.Stage;
                    bool live = stage != null && StudioRun.State == StudioState.Held;
                    if (live && !origin.HasValue)
                    {
                        draft.useMapAnchor = true;
                        draft.mapAnchor = stage.anchor - stage.world.Center;
                    }
                    var problems = new List<string>();
                    var sightlines = new List<string>();
                    var bad = new HashSet<string>();
                    Vector3 anchor = live ? stage.world.Center + draft.mapAnchor : Vector3.zero;
                    if (live)
                    {
                        foreach (var mark in draft.marks.Where(mark => mark.role != StudioRole.Hand))
                        {
                            Vector3 at = anchor + mark.position;
                            if (StudioStage.SurfaceBelow(at, out var floor) && floor.y > stage.world.WaterHeight(floor) &&
                                Mathf.Abs(floor.y - at.y) < 2f) mark.position.y = floor.y - anchor.y;
                            else { bad.Add(mark.id); problems.Add(mark.actor + " has no nearby dry floor"); }
                        }
                    }
                    if (live)
                    {
                        int mask = (1 << Layers.Block) | (1 << Layers.Surface);
                        foreach (var camera in draft.cameras.Where(c => c.keys.Count > 0 && string.IsNullOrEmpty(c.rideTarget)))
                        {
                            var subject = draft.Mark(camera.subject);
                            if (subject != null && camera.behavior != "firstPerson" && Physics.Linecast(
                                anchor + camera.keys[0].position, anchor + subject.position + Vector3.up, mask, QueryTriggerInteraction.Ignore))
                                sightlines.Add(camera.name + " has a blocked opening sightline to " + subject.actor);
                        }
                    }
                    contents.Add(StudioUi.Hint(draft.marks.Count + " actors · " + draft.cameras.Count + " cameras · " +
                        draft.cues.Count + " cues on " + destination.map));
                    contents.Add(Plan(draft, bad));
                    contents.Add(StudioUi.Hint(live ? "Top view · circles are actors, rays show camera headings. Red crosses mark unsafe starts."
                        : "Top view · open the destination stage to check the floor and choose an origin."));
                    foreach (var problem in problems.Concat(sightlines).Concat(omitted).Take(5)) contents.Add(StudioUi.Warning(problem));
                    if (problems.Count + sightlines.Count + omitted.Count > 5) contents.Add(StudioUi.Hint((problems.Count + sightlines.Count + omitted.Count - 5) + " more warnings"));
                    if (live)
                    {
                        var pick = StudioUi.Small("Pick origin in viewport", () =>
                        {
                            context.window.Viewport.PickOrigin(world =>
                            {
                                if (contents.panel == null || context.Stage != stage) return;
                                origin = world - stage.world.Center;
                                Preview();
                            });
                            context.Say("Click dry ground for the layout origin. Esc cancels.");
                        });
                        contents.Add(pick);
                    }
                    var cast = new Foldout { text = "Cast roles and outfits", value = false };
                    contents.Add(cast);
                    foreach (var role in draft.marks)
                    {
                        var compatible = destination.marks.Where(actor => actor.role == role.role).ToList();
                        var options = StudioFields.Distinct(new[] { "Keep " + role.actor }.Concat(compatible.Select(actor => actor.actor)).ToList());
                        int choice = casting.TryGetValue(role.id, out var id) ? compatible.FindIndex(actor => actor.id == id) + 1 : 0;
                        var field = new PopupField<string>(role.actor, options, choice);
                        var original = source.Mark(role.id);
                        if (choice > 0) Recast(role, compatible[choice - 1]);
                        field.RegisterValueChangedCallback(_ =>
                        {
                            if (field.index == 0) casting.Remove(role.id);
                            else casting[role.id] = compatible[field.index - 1].id;
                            Recast(role, field.index == 0 ? original : compatible[field.index - 1]);
                        });
                        cast.Add(field);
                    }
                    var name = new TextField("New Set name") { value = draftTitle ?? draft.title };
                    name.RegisterValueChangedCallback(e => draftTitle = e.newValue);
                    contents.Add(name);
                    var save = StudioUi.Big("Save new Set", () =>
                    {
                        draft.title = string.IsNullOrWhiteSpace(name.value) ? draft.title : name.value.Trim();
                        draft.id = "";
                        StudioLibrary.Save(draft);
                        if (StudioRun.State == StudioState.Closed)
                        {
                            context.window.LoadSet(draft.id);
                            context.Rebuild();
                        }
                        context.Say("Created " + draft.title + (live ? ". Close the stage and choose it from the Set menu." : "."));
                    });
                    save.SetEnabled(problems.Count == 0 && (live || StudioRun.State == StudioState.Closed));
                    contents.Add(save);
                }
                sourceField.RegisterValueChangedCallback(_ => { sourceId = sources[sourceField.index].id; casting.Clear(); draftTitle = null; Preview(); });
                Preview();
            });
        }

        static VisualElement Plan(StudioSet set, HashSet<string> bad)
        {
            var view = new VisualElement { pickingMode = PickingMode.Ignore };
            view.style.height = 138;
            view.style.backgroundColor = new Color(.08f, .12f, .14f);
            var points = set.marks.Select(m => m.position).Concat(set.cameras.Where(c => c.keys.Count > 0 &&
                string.IsNullOrEmpty(c.rideTarget)).Select(c => c.keys[0].position)).ToList();
            if (points.Count == 0) return view;
            float x0 = points.Min(p => p.x), x1 = points.Max(p => p.x), z0 = points.Min(p => p.z), z1 = points.Max(p => p.z);
            view.generateVisualContent += ctx =>
            {
                var rect = view.contentRect;
                float scale = Mathf.Max(0, Mathf.Min((rect.width - 24) / Mathf.Max(2, x1 - x0), (rect.height - 24) / Mathf.Max(2, z1 - z0)));
                Vector2 Pixel(Vector3 pos) => rect.center + scale * new Vector2(pos.x - (x0 + x1) / 2, (z0 + z1) / 2 - pos.z);
                var pen = ctx.painter2D;
                void Line(Vector2 a, Vector2 b, Color color)
                {
                    pen.strokeColor = color; pen.lineWidth = 2; pen.BeginPath(); pen.MoveTo(a); pen.LineTo(b); pen.Stroke();
                }
                foreach (var mark in set.marks)
                {
                    var pt = Pixel(mark.position);
                    pen.fillColor = StudioColors.For(mark.color);
                    pen.BeginPath(); pen.Arc(pt, 4, 0, 360); pen.Fill();
                    if (bad.Contains(mark.id))
                    {
                        Line(pt - Vector2.one * 5, pt + Vector2.one * 5, Color.red);
                        Line(pt + new Vector2(-5, 5), pt + new Vector2(5, -5), Color.red);
                    }
                }
                foreach (var c in set.cameras.Where(cam => cam.keys.Count > 0 && string.IsNullOrEmpty(cam.rideTarget)))
                {
                    var key = c.keys[0]; var at = Pixel(key.position);
                    float vertical = StudioCameras.FieldOfView(key.focal, c.sensorHeight);
                    float aspect = set.render.width / (float)Mathf.Max(1, set.render.height);
                    float halfWidth = Mathf.Atan(Mathf.Tan(vertical * Mathf.Deg2Rad * .5f) * aspect) * Mathf.Rad2Deg;
                    foreach (float yaw in new[] { -halfWidth, halfWidth })
                    {
                        float angle = (key.euler.y + yaw) * Mathf.Deg2Rad;
                        Line(at, at + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * 14, StudioColors.Camera);
                    }
                }
            };
            return view;
        }
    }
}
