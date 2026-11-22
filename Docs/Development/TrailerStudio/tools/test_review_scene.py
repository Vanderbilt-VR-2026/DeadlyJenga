import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

from review_scene import differences, edit_ranges, review, review_cues


BRIEF = {"aspect": "16:9", "duration_seconds": 1, "must_show": [{"id": "pvp_hit"}]}
RECIPE = {
    "map": "castle", "width": 320, "height": 180, "fps": 15, "muteDelivery": True,
    "cast": [{"id": "survivor"}, {"id": "rival"}],
    "shots": [{"start": 0, "duration": 1}],
    "edit": [{"shot": 0, "from": 0, "duration": 1}],
    "cues": [{"action": "punch", "at": .2, "actor": "survivor", "target": "rival"}],
}
HEADER = "Simulation seconds and executed cues\n"
REJECTED = HEADER + "0.200 punch survivor->rival gameplayAccepted=False healthDelta=0.0\n"
CONTACT = HEADER + (
    "0.200 punch survivor->rival attackerId=a targetId=b predictedTargetId=b "
    "gameplayDispatched=True gameplayAccepted=True targetPushed=True otherPushed=False "
    "healthBefore=100.00 healthAfter=90.00 healthDelta=10.00\n"
)


class CueReviewTest(unittest.TestCase):
    def test_unity_serialization_and_wrong_take(self):
        rendered = json.loads(json.dumps(RECIPE))
        rendered["environmentOnly"] = False
        rendered["ffmpeg"] = "C:/ffmpeg.exe"
        rendered["cast"][0].update(live=False, moves=[], poses=None)
        rendered["cues"][0].update(at=.20000000298023224, hazard="", requireTargetContact=False)
        self.assertEqual(list(differences(RECIPE, rendered)), [])
        rendered["cues"][0]["target"] = "other"
        rendered["map"] = "marina"
        self.assertEqual(list(differences(RECIPE, rendered)), ["cues[0].target", "map"])

    def test_contact_requires_positive_and_specific_runtime_proof(self):
        ranges = list(edit_ranges(RECIPE))
        for log in (REJECTED, CONTACT.replace("predictedTargetId=b", "predictedTargetId=c"),
                    CONTACT.replace("otherPushed=False", "otherPushed=True"),
                    CONTACT.replace("survivor->rival", "survivor->other")):
            with self.subTest(log=log):
                self.assertTrue(review_cues(log, RECIPE, BRIEF, ranges)[1])
        self.assertEqual(review_cues(CONTACT, RECIPE, BRIEF, ranges)[1], [])
        self.assertIn("outside", review_cues(CONTACT, RECIPE, BRIEF, [(4, 15)])[1][0])


class TakeReviewTest(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        root = Path(self.directory.name)
        self.staged, self.take = root / "draft", root / "take"
        self.staged.mkdir()
        self.take.mkdir()
        (self.staged / "recipe.json").write_text(json.dumps(RECIPE), encoding="utf-8")
        (self.take / "recipe.json").write_text(json.dumps(RECIPE), encoding="utf-8")
        (self.staged / "staging.json").write_text(json.dumps({
            "status": "recipe_staged_render_pending", "brief": BRIEF,
            "unresolved": ["Review the camera angle."],
        }), encoding="utf-8")
        (self.take / "result.txt").write_text("PASS Gallery ready", encoding="utf-8")
        (self.take / "take.txt").write_text("Map=castle\nFPS=15\n", encoding="utf-8")
        (self.take / "cues.txt").write_text(CONTACT, encoding="utf-8")

    def video(self, audio, size="320x180", duration="1"):
        subprocess.run([
            "ffmpeg", "-nostdin", "-v", "error", "-y",
            "-f", "lavfi", "-i", f"color=black:s={size}:r=15",
            "-f", "lavfi", "-i", audio, "-t", duration,
            "-c:v", "mpeg4", "-c:a", "aac", str(self.take / "edit.mp4"),
        ], check=True, capture_output=True, timeout=30)

    def test_missing_artifacts_keep_the_take_failed(self):
        (self.take / "cues.txt").unlink()
        (self.take / "result.txt").write_text("FAIL interrupted", encoding="utf-8")
        result = review(self.staged, self.take)
        self.assertEqual(result["status"], "failed", result)
        self.assertEqual(len(result["failures"]), 3, result)

    @unittest.skipUnless(shutil.which("ffmpeg") and shutil.which("ffprobe"), "Requires FFmpeg and FFprobe")
    def test_decoded_audio_aspect_and_frame_count(self):
        self.video("anullsrc")
        result = review(self.staged, self.take)
        self.assertEqual(result["status"], "creative_review_required", result)
        self.assertIn("Review the camera angle.", result["creative_review"])
        self.video("sine=frequency=400", size="320x320", duration="0.8")
        result = review(self.staged, self.take)
        self.assertEqual(result["status"], "failed", result)
        for message in ("display aspect", "expected 15", "decode to silence"):
            self.assertTrue(any(message in error for error in result["failures"]), result)


if __name__ == "__main__":
    unittest.main()
