"""Regressions for plain-language directions in the offline brief."""

import unittest

from direct_prompt import compile_brief
from stage_prompt import stage


class HoldoutDirections(unittest.TestCase):
    def test_shot_and_event_times_do_not_replace_trailer_runtime(self):
        brief = compile_brief(
            "Make a 12-second widescreen clip on the Castle map. Start wide for 3 seconds. "
            "At 4 seconds a meteor hits. Cut to a close-up from 6 seconds until the end."
        )
        self.assertEqual(brief["duration_seconds"], 12)
        self.assertEqual(brief["constraints"]["hazards"][0]["at_seconds"], 4)
        close = next(shot for shot in brief["constraints"]["camera"] if shot["id"] == "close")
        self.assertEqual(close["at_seconds"], 6)
        recipe, _ = stage(brief)
        self.assertEqual(recipe["shots"][-1]["start"], 6)

    def test_portrait_framing_is_independent_of_delivery(self):
        brief = compile_brief(
            "A portrait shot of a survivor looking straight into camera. "
            "Shoot the teaser in landscape, 8 seconds."
        )
        self.assertEqual(brief["aspect"], "16:9")
        self.assertEqual([shot["id"] for shot in brief["constraints"]["camera"]], ["close"])

    def test_excluded_aspect_stays_an_exclusion(self):
        brief = compile_brief("A six-second wide shot on the Castle map, no vertical crop.")
        self.assertIsNone(brief["aspect"])
        aspect = brief["constraints"]["aspect"][0]
        self.assertEqual((aspect["id"], aspect["intent"]), ("9:16", "exclude"))
        recipe, _ = stage(brief)
        self.assertGreater(recipe["width"], recipe["height"])

    def test_camera_reaction_and_participants_survive_in_the_brief(self):
        brief = compile_brief(
            "Start first-person as the survivor looks up. "
            "At 4 seconds, a rival jumps onto the ledge and pursues them. "
            "Then cut close as the rival shoves the survivor off the ledge. "
            "Finish wide on the fall."
        )
        gameplay = {entry["id"]: entry for entry in brief["constraints"]["gameplay"]}
        self.assertEqual(gameplay["survivors_react"]["actor"], "survivor")
        self.assertEqual((gameplay["jump"]["actor"], gameplay["jump"]["at_seconds"]), ("rival", 4))
        self.assertEqual(gameplay["chase"]["actor"], "rival")
        for action in ("pvp_hit", "knock_off"):
            self.assertEqual((gameplay[action]["actor"], gameplay[action]["target"]), ("rival", "survivor"))
        self.assertEqual([shot["id"] for shot in brief["constraints"]["camera"]], ["first_person", "close", "wide"])
        recipe, _ = stage(brief)
        hit = next(cue for cue in recipe["cues"] if cue["action"] == "punch")
        self.assertEqual((hit["actor"], hit["target"]), ("rival", "survivor"))
        self.assertGreater(hit["at"], 4)
        rival = next(actor for actor in recipe["cast"] if actor["id"] == "rival")
        self.assertTrue(any(move["at"] == 4 and move["lift"] > 0 for move in rival["moves"]))

    def test_empty_silent_scene_repeats_requested_framing(self):
        recipe, _ = stage(compile_brief("An 8-second empty landscape on the Castle map. "
                                        "Start wide. Cut close. Finish wide. No survivors. Make it silent."))
        self.assertEqual(recipe["cast"], [])
        self.assertTrue(recipe["environmentOnly"] and recipe["muteDelivery"])
        self.assertEqual([shot["name"] for shot in recipe["shots"]], ["Wide scout", "Close scout", "Wide scout"])
        self.assertTrue(all("subject" not in shot for shot in recipe["shots"]))


if __name__ == "__main__":
    unittest.main()
