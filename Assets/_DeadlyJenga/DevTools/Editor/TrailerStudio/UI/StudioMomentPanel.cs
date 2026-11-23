using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    static class StudioMomentPanel
    {
        public static void Build(VisualElement panel, StudioContext context, bool held)
        {
            var set = context.Set;
            var tape = context.Tape;
            var group = new Foldout { text = "Saved moments", value = true };
            panel.Add(group);
            var mark = StudioUi.Big("Mark this moment", () => Mark(context),
                "Save the current tape frame, a recorded player's POV, and the current viewport as the witness angle.");
            mark.SetEnabled(held && !StudioRender.Running && tape != null &&
                set.marks.Any(actor => actor.role != StudioRole.Hand && tape.Find(actor.id)?.ActiveTake != null));
            group.Add(mark);

            if (set.theatreBookmarks.Count > 0)
            {
                var bookmarks = new Foldout { text = "Current-match bookmarks", value = false };
                foreach (var bookmark in set.theatreBookmarks)
                {
                    var saved = bookmark;
                    var jump = StudioUi.Small($"{saved.name} · frame {saved.frame}", () => context.window.Scrub(saved.frame));
                    jump.SetEnabled(!StudioRender.Running && (held || !StudioRun.StageOpen));
                    bookmarks.Add(jump);
                }
                group.Add(bookmarks);
            }
            if (set.moments.Count == 0) return;
            string selectionKey = "DeadlyJenga.TrailerStudio.Moment." + set.id;
            var current = set.moments.FirstOrDefault(m => m.id == SessionState.GetString(selectionKey, "")) ?? set.moments[set.moments.Count - 1];
            var picker = new PopupField<StudioMoment>("Select", set.moments, set.moments.IndexOf(current),
                m => m.name, m => $"{m.name} · frame {m.frame}");
            picker.RegisterValueChangedCallback(e => { SessionState.SetString(selectionKey, e.newValue.id); context.Rebuild(); });
            group.Add(picker);
            BuildSelected(group, context, current, held);
        }

        static void BuildSelected(VisualElement group, StudioContext context, StudioMoment saved, bool held)
        {
            var set = context.Set;
            var tape = context.Tape;
            var pov = set.cameras.FirstOrDefault(camera => camera.id == saved.pov);
            var witness = set.cameras.FirstOrDefault(camera => camera.id == saved.witness);
            var card = StudioUi.Card();
            group.Add(card);
            card.Add(StudioFields.Text(context, "Name", saved.name, value =>
            {
                if (!string.IsNullOrWhiteSpace(value)) saved.name = value.Trim();
            }));
            bool sameSource = saved.source != null && saved.source.setId == set.id && saved.source.map == set.map &&
                saved.source.fps == set.fps && saved.source.Matches(tape);
            int fps = saved.source?.fps ?? set.fps;
            card.Add(StudioUi.Hint($"Frame {saved.frame} · {saved.frame / (float)fps:0.00} s · {saved.source?.map ?? set.map}"));
            var jump = StudioUi.Small("Go to frame", () => context.window.Scrub(saved.frame));
            jump.SetEnabled(sameSource && !StudioRender.Running);
            card.Add(StudioUi.Row(jump, StudioUi.Small("Remove select", () =>
                context.Edit("Remove saved moment", () => set.moments.Remove(saved)),
                "Remove the bookmark. Its cameras stay available to the cut.")));
            if (!sameSource) card.Add(StudioUi.Warning("This select belongs to a different take selection."));
            if (pov == null || witness == null) card.Add(StudioUi.Warning("One of this select's cameras was removed."));

            card.Add(StudioUi.Hint($"Reference frame {saved.ReferenceFrame} · review range {saved.first}–{saved.end - 1}"));
            var pinFrame = StudioUi.Small("Use playhead as reference", () =>
            {
                int frame = context.Frame;
                if (frame < saved.first || frame >= saved.end)
                {
                    context.Say($"Choose a frame from {saved.first} to {saved.end - 1}.");
                    return;
                }
                context.Edit("Set moment reference frame", () => saved.referenceFrame = frame);
            });
            pinFrame.SetEnabled(sameSource && held && !StudioRender.Running);
            card.Add(pinFrame);
            BuildAnglePicker(card, context, saved, pov, sameSource && held && !StudioRender.Running);

            if (pov != null && witness != null && saved.source != null)
            {
                bool covered = StudioMoments.CoversReference(set, saved, pov) && StudioMoments.CoversReference(set, saved, witness);
                var render = StudioUi.Small("Compare reference frame", () => Review(false));
                var motion = StudioUi.Small("Compare in motion", () => Review(true));
                void Review(bool moving)
                {
                    try
                    {
                        if (StudioMoments.ReadReview(set, saved, tape, pov, witness, moving) != null)
                        {
                            context.Rebuild();
                            context.Say("Showing the cached comparison.");
                            return;
                        }
                        StudioMoments.RenderReview(set, tape, saved, pov, witness, moving, context.Rebuild);
                        context.Rebuild();
                    }
                    catch (Exception error) { context.Say(error.Message); }
                }
                render.SetEnabled(covered && sameSource && held && !StudioRender.Running);
                motion.SetEnabled(render.enabledSelf);
                card.Add(StudioUi.Row(render, motion));
                if (!covered) card.Add(StudioUi.Warning("Both angles must cover the reference frame."));
                if (sameSource && !StudioRender.Running)
                {
                    BuildReview(card, set, tape, saved, pov, witness, false);
                    BuildReview(card, set, tape, saved, pov, witness, true);
                }
            }
            var cuts = StudioUi.Row();
            card.Add(cuts);
            AddAngle(cuts, context, saved, pov, "POV", held, sameSource);
            AddAngle(cuts, context, saved, witness, "Witness", held, sameSource);
        }

        static void BuildAnglePicker(VisualElement card, StudioContext context, StudioMoment moment, StudioCameraTrack pov, bool canScout)
        {
            if (moment.source == null || pov == null) return;
            var candidates = context.Set.cameras.Where(c => c.id != pov.id && c.behavior != "firstPerson" &&
                StudioMoments.CoversReference(context.Set, moment, c)).ToList();
            if (candidates.Count == 0) return;
            var chosen = candidates.Find(c => c.id == moment.witness);
            if (chosen == null) candidates.Insert(0, null);
            var pin = new PopupField<StudioCameraTrack>("Pinned witness", candidates, candidates.IndexOf(chosen),
                c => c?.name ?? "Choose an angle", c => c?.name ?? "Choose an angle");
            pin.RegisterValueChangedCallback(e =>
            {
                if (e.newValue != null) context.Edit("Pin witness angle", () => moment.witness = e.newValue.id);
            });
            card.Add(pin);
            var scout = StudioUi.Small("Suggest witness angle", () =>
            {
                int frame = context.Frame;
                var result = (camera: (StudioCameraTrack)null, reason: "");
                try
                {
                    context.window.Scrub(moment.ReferenceFrame);
                    Physics.SyncTransforms();
                    result = StudioMoments.Recommend(context.Stage, moment, pov);
                }
                finally { context.window.Scrub(frame); }
                if (result.camera == null) { context.Say(result.reason); return; }
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Pin " + result.camera.name), false, () =>
                {
                    context.Edit("Pin suggested witness angle", () => moment.witness = result.camera.id);
                    context.Say(result.reason);
                });
                menu.AddDisabledItem(new GUIContent(result.reason));
                menu.ShowAsContext();
            }, "Measure actor visibility, size and facing at the reference frame.");
            scout.SetEnabled(canScout);
            card.Add(scout);
        }

        static void Mark(StudioContext context)
        {
            var set = context.Set;
            var tape = context.Tape;
            int frame = context.Frame;
            var world = tape?.FindType(WorldTrack.Id)?.ActiveTake;
            var actors = set.marks.Where(actor => actor.role != StudioRole.Hand &&
                StudioMoments.Covers(tape?.Find(actor.id)?.ActiveTake, frame) &&
                StudioActorPoses.At(tape, actor.id, frame / (float)set.fps, set.fps).found).ToList();
            if (!StudioMoments.Covers(world, frame) || actors.Count == 0 || !StudioCamera.Camera)
            {
                context.Say("Pause on a recorded frame with a visible survivor and a world take.");
                return;
            }
            var key = StudioCameras.KeyFromView(context.Stage, new StudioCameraTrack(), frame / (float)set.fps);
            key.aperture = 16;
            var source = StudioMomentSource.Read(tape);
            var menu = new GenericMenu();
            foreach (var actor in actors)
                menu.AddItem(new GUIContent($"POV · {actor.actor} ({actor.id})"), false, () =>
                {
                    if (context.Set != set || !source.Matches(context.Tape) || StudioRun.State != StudioState.Held || StudioRender.Running)
                    {
                        context.Say("The active tape changed. Mark the moment again.");
                        return;
                    }
                    try
                    {
                        context.Edit("Mark this moment", () =>
                        {
                            var select = StudioMoments.Create(set, tape, frame, actor, key);
                            SessionState.SetString("DeadlyJenga.TrailerStudio.Moment." + set.id, select.id);
                        });
                    }
                    catch (Exception error) { context.Say(error.Message); }
                });
            menu.ShowAsContext();
        }

        static void AddAngle(VisualElement row, StudioContext context, StudioMoment moment,
            StudioCameraTrack camera, string label, bool held, bool sameSource)
        {
            if (camera == null) return;
            var section = new VisualElement { style = { flexGrow = 1, minWidth = 0 } };
            var preview = StudioUi.Small("Preview " + label, () =>
            {
                context.window.Scrub(moment.ReferenceFrame);
                ShootMode.LookThrough = camera.id;
                StudioCamera.Place();
                context.Select(camera);
            });
            preview.SetEnabled(held && sameSource && !StudioRender.Running && StudioMoments.CoversReference(context.Set, moment, camera));
            var cut = StudioUi.Small(label + " to Cut", () =>
            {
                bool added = false;
                context.Edit("Add selected moment angle", () => added = StudioMoments.AddToCut(context.Set, moment, camera));
                if (added) context.window.ShowMode("Cut");
                else context.Say("The camera's current timing does not cover this moment.");
            });
            cut.SetEnabled(sameSource && !StudioRender.Running);
            section.Add(preview);
            section.Add(cut);
            row.Add(section);
        }

        static void BuildReview(VisualElement card, StudioSet set, StudioTape tape, StudioMoment moment,
            StudioCameraTrack pov, StudioCameraTrack witness, bool moving)
        {
            try
            {
                var review = StudioMoments.ReadReview(set, moment, tape, pov, witness, moving);
                if (review == null) return;
                string folder = StudioMoments.Folder(set, moment);
                var comparison = StudioUi.Row(Angle("POV", out var povImage), Angle("Witness", out var witnessImage));
                Draw(0);
                card.Add(comparison);
                if (moving && review.frames > 1)
                {
                    var scrub = new SliderInt(0, review.frames - 1) { tooltip = "Scrub both angles together" };
                    scrub.RegisterValueChangedCallback(e => Draw(e.newValue));
                    card.Add(scrub);
                    IVisualElementScheduledItem playback = null;
                    var play = StudioUi.Small("Play comparison", () => { });
                    play.clicked += () =>
                    {
                        if (playback?.isActive == true) { playback.Pause(); play.text = "Play comparison"; return; }
                        playback = scrub.schedule.Execute(() => scrub.value = (scrub.value + 1) % review.frames)
                            .Every(Mathf.RoundToInt(1000f / review.fps));
                        play.text = "Pause comparison";
                    };
                    card.Add(play);
                }
                card.Add(StudioUi.Small(moving ? "Open proxy files" : "Open reference files",
                    () => EditorUtility.RevealInFinder(StudioMoments.ReviewFile(folder, moving))));

                void Draw(int frame)
                {
                    string filename = $"{frame:D05}.png";
                    Load(povImage, Path.Combine(folder, Path.GetDirectoryName(review.povImage), filename));
                    Load(witnessImage, Path.Combine(folder, Path.GetDirectoryName(review.witnessImage), filename));
                }
            }
            catch (IOException error) { card.Add(StudioUi.Warning(error.Message)); }
            catch (ArgumentException error) { card.Add(StudioUi.Warning(error.Message)); }
        }

        static VisualElement Angle(string title, out Image image)
        {
            var texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            image = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit, tooltip = title, style = { height = 90 } };
            image.RegisterCallback<DetachFromPanelEvent>(_ => UnityEngine.Object.DestroyImmediate(texture));
            var column = new VisualElement { style = { flexGrow = 1, flexBasis = 0, minWidth = 0 } };
            column.Add(new Label(title));
            column.Add(image);
            return column;
        }

        static void Load(Image image, string path)
        {
            if (!File.Exists(path) || !(image.image is Texture2D texture)) return;
            texture.LoadImage(File.ReadAllBytes(path));
            image.MarkDirtyRepaint();
        }
    }
}
