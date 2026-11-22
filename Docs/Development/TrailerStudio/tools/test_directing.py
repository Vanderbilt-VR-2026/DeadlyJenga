"""Offline prompt benchmark. Run with python -B test_directing.py."""

import unittest

from direct_prompt import compile_brief
from stage_prompt import stage


def must_show(brief):
    return {entry["id"] for entry in brief["must_show"]}


def attempted(report):
    return {entry["id"] for entry in report["cue_attempts"]}


class DirectingBenchmark(unittest.TestCase):
    def test_negation_is_local_and_reaches_staging(self):
        brief = compile_brief(
            "No tornado, but show a tsunami. Never show the giant flying hand."
        )
        self.assertEqual(must_show(brief), {"tsunami"})
        _, report = stage(brief)
        self.assertEqual(attempted(report), {"tsunami"})

    def test_alternate_map_needs_a_choice(self):
        brief = compile_brief(
            "Show a wide shot on Castle Catwalks or Breakwater Village."
        )
        self.assertIsNone(brief["map"])
        with self.assertRaisesRegex(ValueError, "[Mm]ap"):
            stage(brief)
        brief["map"] = "Breakwater Village"
        recipe, _ = stage(brief)
        self.assertIn(recipe["map"], {"breakwater-village", "mode-breakwater-village"})

    def test_alternate_hazards_are_not_both_staged(self):
        brief = compile_brief("Use either a tornado or a tsunami.")
        self.assertFalse(must_show(brief))
        _, report = stage(brief)
        self.assertFalse(attempted(report))

    def test_cause_and_coincidence_preserve_distinct_intent(self):
        cause = "Show a tornado causing the tower to collapse."
        coincidence = "Show a tornado while the tower collapses."
        for prompt in (cause, coincidence):
            with self.subTest(prompt=prompt):
                brief = compile_brief(prompt)
                self.assertIn("tornado", must_show(brief))
                _, report = stage(brief)
                self.assertEqual(report["brief"]["goal"], prompt)
                self.assertFalse(report["runtime_evidence"])
                self.assertTrue(report["pending_review"])
                if prompt == cause:
                    self.assertNotIn("collapse", attempted(report))
                else:
                    self.assertEqual(attempted(report), {"collapse", "tornado"})
                    self.assertEqual(len({attempt["cue"]["at"] for attempt in report["cue_attempts"]}), 1)

    def test_unknown_direction_survives_for_review(self):
        prompt = "Show three moons turning into stained glass."
        brief = compile_brief(prompt)
        self.assertIn(prompt, brief["unclassified_notes"])
        _, report = stage(brief)
        self.assertFalse(attempted(report))
        self.assertTrue(report["pending_review"])

    def test_silent_cut_keeps_audible_audio_out_of_delivery(self):
        brief = compile_brief("Make it silent. Use on-screen captions.")
        audio = {
            (entry["id"], entry["intent"])
            for entry in brief["constraints"]["audio_and_titles"]
        }
        self.assertIn(("audible_audio", "exclude"), audio)
        self.assertIn(("titles", "requested"), audio)
        _, report = stage(brief)
        self.assertTrue(any("audio" in note.lower() for note in report["pending_review"]))

    def test_duration_and_aspect_drive_recipe(self):
        brief = compile_brief("Make a 17-second vertical trailer.")
        recipe, _ = stage(brief)
        self.assertEqual(brief["duration_seconds"], 17)
        self.assertEqual(sum(shot["duration"] for shot in recipe["edit"]), 17)
        self.assertLess(recipe["width"], recipe["height"])

    def test_duration_bound_does_not_exclude_subject(self):
        brief = compile_brief("No more than 15 seconds with a tornado.")
        self.assertIsNone(brief["duration_seconds"])
        self.assertEqual(must_show(brief), {"tornado"})
        with self.assertRaisesRegex(ValueError, "duration_seconds"):
            stage(brief)


if __name__ == "__main__":
    unittest.main()
