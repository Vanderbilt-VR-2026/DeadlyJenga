import copy
import json
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import patch

import local_director


def completion():
    point = {"x": 0, "y": 0, "z": 0}
    camera = {"x": 5, "y": 2, "z": -8}
    return {"recipe": {
        "title": "An empty court", "map": "castle", "aspect": "9:16",
        "environmentOnly": True, "muteDelivery": False, "cast": [],
        "shots": [{"name": "Wide", "start": 0, "duration": 5, "fov": 50,
                   "from": camera, "to": camera, "look": point}],
        "cues": [], "edit": [{"shot": 0, "from": 0, "duration": 5}],
    }, "unresolved": [{"request": "silver rain", "reason": "No matching supported cue."}]}


class LocalDirectorTests(unittest.TestCase):
    def setUp(self):
        self.temp = TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.output = Path(self.temp.name) / "draft"

    def direct(self, draft):
        with patch.object(local_director, "complete", return_value=draft):
            return local_director.direct("Empty castle with silver rain.", self.output, "localmodel")

    def test_valid_staged_completion(self):
        self.assertEqual(self.direct(completion()), "recipe_staged_render_pending")
        recipe = json.loads((self.output / "recipe.json").read_text(encoding="utf-8"))
        self.assertEqual((recipe["width"], recipe["height"]), (360, 640))
        self.assertEqual(recipe["ffmpeg"], "ffmpeg")
        report = json.loads((self.output / "staging.json").read_text(encoding="utf-8"))
        self.assertEqual(report["unresolved"][0]["request"], "silver rain")
        self.assertEqual(report["runtime_evidence"], [])

    def test_blocked_direction_only_writes_report(self):
        draft = completion()
        draft["recipe"] = None
        self.assertEqual(self.direct(draft), "direction_unresolved")
        self.assertEqual([p.name for p in self.output.iterdir()], ["staging.json"])

    def test_refuses_a_substitute_for_an_explicit_unknown_map(self):
        with patch.object(local_director, "complete", return_value=completion()):
            with self.assertRaisesRegex(ValueError, "explicitly requested map"):
                local_director.direct("Map: Painted Moon. Show silver rain.", self.output, "localmodel")
        self.assertFalse(self.output.exists())

    def test_cloud_model_is_rejected_before_chat(self):
        with patch.object(local_director, "complete") as complete:
            with self.assertRaisesRegex(ValueError, "installed local model"):
                local_director.direct("An empty court.", self.output, "model:120b-cloud")
        complete.assert_not_called()

    def test_rejects_unknown_fields_and_invalid_values_before_writing(self):
        variants = [
            {"ffmpeg": "bad.exe"},
            {"replaySource": "spoof.json"},
            {"map": "unknown"},
            {"cues": [{"action": "hazard", "at": 1, "hazard": "fake"}]},
            {"cues": [{"action": "punch", "at": 1, "actor": "missing", "target": "other"}]},
            {"edit": [{"shot": 0, "from": 0, "duration": 10}]},
            {"edit": [{"shot": False, "from": 0, "duration": 4}]},
            {"shots": [dict(completion()["recipe"]["shots"][0], executable="bad")]},
        ]
        example = completion()
        for changes in variants:
            with self.subTest(changes=changes):
                draft = copy.deepcopy(example)
                draft["recipe"].update(changes)
                with self.assertRaises(ValueError):
                    self.direct(draft)
                self.assertFalse(self.output.exists())

    @patch.object(local_director.http.client, "HTTPConnection")
    def test_fixed_endpoint_and_structured_completion(self, connect):
        response = connect.return_value.getresponse.return_value
        response.status = 200
        result = json.dumps({"done": True, "message": {
            "content": json.dumps(completion()),
        }}).encode()
        metadata = b'{"model_info": {"general.architecture": "test"}}'
        response.read.side_effect = [metadata, result]
        specification = local_director.schema(["castle"], [])
        self.assertEqual(local_director.complete("my-model", "Film the castle.", specification), completion())
        self.assertEqual(connect.call_count, 2)
        for call in connect.call_args_list:
            self.assertEqual(call.args, ("127.0.0.1", 11434))
            self.assertEqual(call.kwargs, {"timeout": 180})
        show, chat = connect.return_value.request.call_args_list
        self.assertEqual(show.args[:2], ("POST", "/api/show"))
        self.assertEqual(json.loads(show.args[2]), {"model": "my-model"})
        method, route, data, _ = chat.args
        self.assertEqual((method, route), ("POST", "/api/chat"))
        self.assertEqual(json.loads(data)["format"], specification)
        response.read.side_effect = [metadata, b'{"done": true, "done": false}']
        with self.assertRaisesRegex(ValueError, "duplicate"):
            local_director.complete("my-model", "Film the castle.", specification)
        connect.reset_mock()
        response.read.side_effect = [b'{"remote_model": "alias", "remote_host": "remote"}']
        with self.assertRaisesRegex(ValueError, "remote Ollama"):
            local_director.complete("innocent-alias", "Private prompt.", specification)
        request = connect.return_value.request
        self.assertEqual(request.call_count, 1)
        self.assertEqual(json.loads(request.call_args.args[2]), {"model": "innocent-alias"})

    def test_rejects_conflicts_with_explicit_constraints(self):
        for prompt, error in [("Make it 16:9.", "requested aspect"),
                              ("Make it silent.", "requested silence"),
                              ("Set it in Painted Moon.", "explicitly requested map"),
                              ("No tsunami.", "excluded hazard")]:
            with self.subTest(prompt=prompt):
                draft = completion()
                draft["recipe"]["cues"] = [{"action": "tsunami", "at": 1, "heading": 0}]
                with patch.object(local_director, "complete", return_value=draft):
                    with self.assertRaisesRegex(ValueError, error):
                        local_director.direct(prompt, self.output, "localmodel")
                self.assertFalse(self.output.exists())

    def test_refinement_changes_lens_and_camera_end_only(self):
        prompt = "A four-second silent 16:9 Castle scout. No actors. Open wide. A tsunami at 2 seconds."
        specification = local_director.schema(["castle"], ["Tsunami"])
        seed = json.loads(local_director.grounded_request(prompt, specification))["seed_recipe"]
        adjustment = {"shot": 0, "fov": 45, "toOffset": {"x": 0, "y": 1, "z": 0}}
        with patch.object(local_director, "chat",
                          return_value={"adjustments": [adjustment], "unresolved": []}):
            self.assertEqual(local_director.direct(prompt, self.output, "localmodel", refine_camera=True),
                             "recipe_staged_render_pending")
        recipe = json.loads((self.output / "recipe.json").read_text(encoding="utf-8"))
        self.assertEqual(recipe["cues"], seed["cues"])
        self.assertEqual(recipe["edit"], seed["edit"])
        self.assertEqual(recipe["cast"], [])
        expected = copy.deepcopy(seed["shots"])
        expected[0]["fov"] = 45
        expected[0]["to"]["y"] += 1
        self.assertEqual(recipe["shots"], expected)


if __name__ == "__main__":
    unittest.main()
