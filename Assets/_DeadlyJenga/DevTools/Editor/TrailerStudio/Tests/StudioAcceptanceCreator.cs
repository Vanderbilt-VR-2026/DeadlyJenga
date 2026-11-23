using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    public static partial class StudioAcceptance
    {
        public static void RunCreatorWorkflows() => Begin("CreatorWorkflows");
        public static void RunCreatorVisuals() => Begin("CreatorVisuals");

        static IEnumerator<int> CreatorVisualTests()
        {
            using (var moments = MomentVisualTests())
                while (moments.MoveNext()) yield return moments.Current;
            using (var crowd = CrowdVisualTests())
                while (crowd.MoveNext()) yield return crowd.Current;
        }

        static IEnumerator<int> CreatorWorkflowTests()
        {
            MomentTests();
            RetimedSoundTests(Path.Combine(Folder, "audio"));
            var stage = StudioRun.Stage;
            var set = stage.set;
            var source = new StudioSet
            {
                title = "Template source", map = "other-map", startFromSet = "old-world",
                useMapAnchor = true, mapAnchor = new Vector3(99, 99, 99),
                storyBeats = DirectMode.InitialBeats(6)
            };
            var hero = source.AddMark(StudioRole.Survivor, Vector3.zero);
            hero.actor = "Hero";
            source.cameras.Add(new StudioCameraTrack { name = "Witness", subject = hero.id,
                keys = { new StudioCameraKey { position = new Vector3(2, 2, -3) } } });
            source.cameras.Add(new StudioCameraTrack { name = "Old block camera", rideTarget = "block:8" });
            var push = new StudioCue { blocks = { 8 } };
            source.cues.Add(push);
            source.cues.Add(new StudioCue { action = "collapse", link = push.id });
            source.cuts.Add(new StudioCut { camera = source.cameras[0].id });
            StudioLibrary.Save(source);
            var before = JsonUtility.ToJson(source);
            var warnings = new List<string>();
            var origin = stage.anchor - stage.world.Center;
            var preview = StudioLayoutTemplate.Draft(source, set.map, origin, warnings);
            Result("New-map layout retains role bindings and discards incompatible world references",
                preview.map == set.map && preview.mapAnchor == origin && preview.useMapAnchor &&
                preview.startFromSet == "" && preview.cuts.Count == 0 && preview.cues.Count == 0 &&
                preview.cameras.Count == 1 && preview.cameras[0].subject == hero.id &&
                preview.storyBeats.Count == 3 && warnings.Count == 3, string.Join(" / ", warnings));
            preview.marks[0].actor = "Recast";
            Result("Template source is independent", JsonUtility.ToJson(source) == before && source.marks[0].actor == "Hero",
                "Changing the draft leaves the source actor and camera untouched.");

            var window = TourWindow();
            foreach (int wait in Settle(window, 15)) yield return wait;
            window.LoadSet(set.id);
            window.ShowMode("Set");
            var root = window.rootVisualElement;
            var fold = root.Query<Foldout>().ToList().First(f => f.text == "Reuse a scene layout");
            fold.value = true;
            var picker = fold.Q<PopupField<string>>();
            picker.value = source.title;
            foreach (int wait in Settle(window, 3)) yield return wait;
            var save = fold.Query<Button>().ToList().First(b => b.text == "Save new Set");
            Result("Destination map and live ground enable template save", save.enabledInHierarchy,
                "A single hero role uses the viaduct stage anchor.");
            if (save.enabledInHierarchy)
            {
                var oldIds = StudioLibrary.SetIds();
                SubmitButton(save);
                string newId = StudioLibrary.SetIds().Except(oldIds).SingleOrDefault();
                var saved = newId == null ? null : StudioLibrary.Load(newId);
                Result("Template UI writes its own Set without importing a world tape", saved != null &&
                    saved.id != source.id && saved.map == set.map && saved.cameras[0].subject == hero.id &&
                    !File.Exists(StudioLibrary.TapePath(saved.id)), saved?.title ?? "No new set");
            }
            Shoot(window, "01-layout-review");
            window.ShowMode("Direct");
            var beats = root.Query<Foldout>().ToList().First(f => f.text == "Story beats");
            beats.value = true;
            SubmitButton(beats.Query<Button>().ToList().First(b => b.text == "Add three beats"));
            Result("Direct adds and persists three named beats", set.storyBeats.Count == 3 &&
                StudioLibrary.Load(set.id).storyBeats.Count == 3, "Anticipation, Action and Aftermath");
            foreach (int wait in Settle(window, 3)) yield return wait;
            Shoot(window, "02-story-beats");
        }
    }
}
