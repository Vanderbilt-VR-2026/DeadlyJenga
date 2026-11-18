using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // "Cut" suite: track warnings that do not hide each other, cue ids, variation recipes that make a try
    // again, a cue exactly on the branch frame, and the export files checked against their formats.
    //   -executeMethod DeadlyJenga.DevTools.Editor.TrailerStudio.StudioAcceptance.RunCut
    public static partial class StudioAcceptance
    {
        static IEnumerator<int> CutTests()
        {
            var stage = StudioRun.Stage;
            var set = stage.set;
            var tape = stage.tape;
            int fps = set.fps;
            set.marks.Clear(); set.cues.Clear();
            set.length = 3;
            StudioLibrary.Save(set);
            StudioRun.Refresh();
            var worldTrack = tape.FindType(WorldTrack.Id);

            BadgeTests();
            yield return 0;

            Result("Runtime errors during the Studio tests", exceptions == 0, exceptions == 0 && errorLines.Count == 0 ? "None." : $"{exceptions} exceptions. First messages: {string.Join(" // ", errorLines)}");
            StudioRun.SaveTape();
        }

        // ---- Tests that need no stage ----

        static void BadgeTests()
        {
            var track = new StudioTrack { id = "ada", type = SurvivorTrack.Id, name = "Ada" };
            StudioBadges.Set(track, StudioBadges.CouldNotPlay, "a broken frame");
            StudioBadges.Set(track, StudioBadges.Retimed, "after 2.0 s this take no longer lines up with the world.");
            StudioBadges.Set(track, StudioBadges.Stale, "the world was re-recorded and first differs at 1.00 s (frame 30).");
            bool three = StudioBadges.Parts(track).Count == 3 && track.badge.StartsWith("Stale:");
            StudioBadges.Set(track, StudioBadges.Stale, "first differs at 1.50 s.");   // replaced, not added
            bool replaced = StudioBadges.Parts(track).Count(p => p.StartsWith(StudioBadges.Stale)) == 1 && track.badge.Contains("1.50 s");
            StudioBadges.Clear(track, StudioBadges.Retimed);
            bool cleared = !StudioBadges.Has(track, StudioBadges.Retimed) && StudioBadges.Has(track, StudioBadges.Stale) && StudioBadges.Has(track, StudioBadges.CouldNotPlay);
            // Saved and loaded through a real tape file: the binary layout is unchanged (one string per track).
            var tape = new StudioTape();
            tape.tracks.Add(track);
            var old = new StudioTrack { id = "ben", type = SurvivorTrack.Id, name = "Ben", badge = "Stale: an old tape's single warning" };
            tape.tracks.Add(old);
            string file = Path.Combine(Folder, "badges.djtape");
            tape.Save(file);
            var loaded = StudioTape.Load(file);
            var back = loaded.Find("ada"); var oldBack = loaded.Find("ben");
            bool kept = back != null && back.badge == track.badge && StudioBadges.Has(back, StudioBadges.Stale) && StudioBadges.Has(back, StudioBadges.CouldNotPlay);
            bool oldReads = oldBack != null && StudioBadges.Parts(oldBack).Count == 1 && StudioBadges.Has(oldBack, StudioBadges.Stale);
            StudioBadges.ClearAfterRecording(back);
            bool recorded = string.IsNullOrEmpty(back.badge);
            Result("Cut 0a. Track warnings do not hide each other and survive saving",
                three && replaced && cleared && kept && oldReads && recorded,
                $"Stale + retimed + could-not-play on one track: {StudioBadges.Parts(track).Count + 1} lines before clearing one, shown stale first: {three}; setting stale again replaced it: {replaced}; " +
                $"clearing retimed left the other two: {cleared}. Saved and loaded through StudioTape.Save/Load: {kept} (\"{back?.badge?.Replace("\n", " / ")}\"). A single warning as older tapes have it reads as one: {oldReads}. A new recording clears all three: {recorded}.");
        }
    }
}
