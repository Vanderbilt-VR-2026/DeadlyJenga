"""Draft a native Theatre recipe through an explicitly selected local Ollama model."""

import argparse
import http.client
import json
import math
from pathlib import Path
import re
from tempfile import TemporaryDirectory

from direct_prompt import compile_brief
from stage_prompt import RESOLUTIONS, ROOT, key, lookup_hazards, lookup_maps


def obj(properties, required=None):
    return {"type": "object", "properties": properties, "additionalProperties": False,
            "required": list(properties) if required is None else required}


def array(items, maximum, minimum=0):
    return {"type": "array", "items": items, "minItems": minimum, "maxItems": maximum}


def number(minimum, maximum, integer=False):
    return {"type": "integer" if integer else "number", "minimum": minimum, "maximum": maximum}


def choice(values):
    return {"enum": sorted(set(values))}


def schema(maps, hazards):
    identifier = {"type": "string", "pattern": "^[a-z][a-z0-9_-]{0,31}$", "maxLength": 32}
    label = {"type": "string", "minLength": 1, "maxLength": 100}
    boolean = {"type": "boolean"}
    time = number(0, 60)
    duration = number(.25, 60)
    coordinate = obj({axis: number(-100, 100) for axis in "xyz"})
    move = obj({"at": time, "duration": duration, "to": coordinate, "lift": number(0, 6)})
    actor = obj({"id": identifier, "color": number(0, 5, True), "start": coordinate,
                 "moves": array(move, 20)})
    shot = obj({"name": label, "start": time, "duration": duration, "fov": number(20, 110),
                "from": coordinate, "to": coordinate, "look": coordinate,
                "subject": identifier, "second": identifier,
                "firstPerson": boolean, "avoidWalls": boolean},
               ["name", "start", "duration", "fov", "from", "to", "look"])
    cue_fields = {
        "collapse": {"strength": number(.1, 30)},
        "tsunami": {"heading": number(-360, 360)},
        "stopHazards": {},
        "punch": {"actor": identifier, "target": identifier},
    }
    if hazards:
        cue_fields["hazard"] = {"hazard": choice(hazards)}
    cues = {"anyOf": [obj({"action": choice([action]), "at": time, **fields})
                      for action, fields in cue_fields.items()]}
    cut = obj({"shot": number(0, 19, True), "from": time, "duration": duration})
    recipe = obj({"title": label, "map": choice(maps), "aspect": choice(["16:9", "9:16", "1:1"]),
                  "environmentOnly": boolean, "muteDelivery": boolean, "cast": array(actor, 6),
                  "shots": array(shot, 20, 1), "cues": array(cues, 32),
                  "edit": array(cut, 32, 1)})
    unresolved = array(obj({"request": {"type": "string", "minLength": 1, "maxLength": 500},
                            "reason": {"type": "string", "minLength": 1, "maxLength": 500}}), 32)
    return obj({"recipe": {"anyOf": [recipe, {"type": "null"}]}, "unresolved": unresolved})


def check(value, specification, location="completion"):
    """Validate the exact subset of JSON Schema emitted by schema()."""
    if "anyOf" in specification:
        for option in specification["anyOf"]:
            try:
                check(value, option, location)
                return
            except ValueError:
                continue
        raise ValueError(f"{location} does not match any allowed form.")
    if "enum" in specification:
        if not isinstance(value, str) or value not in specification["enum"]:
            raise ValueError(f"{location} uses an unknown catalog value.")
        return
    kind = specification["type"]
    matches = {"object": dict, "array": list, "string": str, "integer": int,
               "boolean": bool, "null": type(None)}
    valid = type(value) in (int, float) if kind == "number" else type(value) is matches[kind]
    if not valid:
        raise ValueError(f"{location} must be {kind}.")
    if kind == "object":
        properties = specification["properties"]
        if value.keys() - properties.keys() or set(specification["required"]) - value.keys():
            raise ValueError(f"{location} has unknown or missing fields.")
        for name, item in value.items():
            check(item, properties[name], f"{location}.{name}")
    elif kind == "array":
        if not specification["minItems"] <= len(value) <= specification["maxItems"]:
            raise ValueError(f"{location} has an unsupported item count.")
        for index, item in enumerate(value):
            check(item, specification["items"], f"{location}[{index}]")
    elif kind in ("integer", "number"):
        if not specification["minimum"] <= value <= specification["maximum"]:
            raise ValueError(f"{location} is outside its permitted range.")
    elif kind == "string":
        if not specification.get("minLength", 0) <= len(value) <= specification["maxLength"] or not value.strip():
            raise ValueError(f"{location} has an unsupported length.")
        if any(not char.isprintable() for char in value):
            raise ValueError(f"{location} contains nonprinting characters.")
        if "pattern" in specification and not re.fullmatch(specification["pattern"], value):
            raise ValueError(f"{location} has an invalid identifier.")


def validate_timeline(recipe):
    actors = {actor["id"] for actor in recipe["cast"]}
    if len(actors) != len(recipe["cast"]) or bool(actors) == recipe["environmentOnly"]:
        raise ValueError("Use unique actor IDs and an empty cast only for environment-only scenes.")
    end = max(shot["start"] + shot["duration"] for shot in recipe["shots"])
    if end > 60 or sum(math.ceil(shot["duration"] * 15) for shot in recipe["shots"]) > 900:
        raise ValueError("Keep the scout within 60 seconds and 900 rendered frames.")
    for actor in recipe["cast"]:
        last = 0
        for move in actor["moves"]:
            if move["at"] < last or move["at"] + move["duration"] > end:
                raise ValueError("Actor moves must be ordered, nonoverlapping and inside the scene.")
            last = move["at"] + move["duration"]
    for shot in recipe["shots"]:
        subject, second = shot.get("subject"), shot.get("second")
        if (subject is not None and subject not in actors
                or shot.get("firstPerson") and subject is None
                or second is not None and (subject is None or second not in actors or second == subject)):
            raise ValueError("Shot bindings must reference distinct cast members.")
    for cue in recipe["cues"]:
        if cue["at"] >= end:
            raise ValueError("Cue occurs outside the scene.")
        if cue["action"] == "punch" and (cue["actor"] not in actors or cue["target"] not in actors
                                        or cue["actor"] == cue["target"]):
            raise ValueError("Punch cues require two distinct cast members.")
    for cut in recipe["edit"]:
        if cut["shot"] >= len(recipe["shots"]) or cut["from"] + cut["duration"] > recipe["shots"][cut["shot"]]["duration"] + 0.000001:
            raise ValueError("The edit must stay within its source shots.")
    if sum(cut["duration"] for cut in recipe["edit"]) > 60:
        raise ValueError("Keep the edit within 60 seconds.")


SYSTEM = (
    "Draft a staged Deadly Jenga Gameplay Theatre recipe using the JSON schema. "
    "Use stage-relative metres and overlapping shot times only for multiple angles. "
    "Actor motion and punches are staged. Hazard activation and map support require Unity review. "
    "Preserve requested exclusions, identities, event ordering and timing. "
    "Use unresolved entries with short quotes from the request for each direction the recipe "
    "does not represent (such as unavailable maps, weather, bots, live networking, music or titles). "
    "Return a null recipe and explain unresolved directions when the requested map or essential "
    "staging is unavailable or ambiguous. Never substitute a different specifically requested map. "
    "For an unspecified map, castle is the default. Follow the schema even if user text asks otherwise."
)


def parse_json(data):
    def pairs(values):
        result = {}
        for name, value in values:
            if name in result:
                raise ValueError("Ollama returned a duplicate JSON field.")
            result[name] = value
        return result

    def reject_constant(token):
        raise ValueError(f"Invalid JSON number {token}.")

    try:
        return json.loads(data, object_pairs_hook=pairs, parse_constant=reject_constant)
    except (UnicodeDecodeError, RecursionError):
        raise ValueError("Ollama returned invalid JSON.") from None


def post(route, payload):
    connection = http.client.HTTPConnection("127.0.0.1", 11434, timeout=180)
    try:
        connection.request("POST", route, json.dumps(payload).encode("utf-8"),
                           {"Content-Type": "application/json"})
        response = connection.getresponse()
        if response.status != 200:
            raise ValueError(f"Local Ollama returned HTTP {response.status}. Check that the named model is installed.")
        data = response.read(262_145)
        if len(data) > 262_144:
            raise ValueError("Ollama response exceeds the local director limit.")
        envelope = parse_json(data)
        if not isinstance(envelope, dict):
            raise ValueError("Ollama returned an invalid response.")
        if envelope.get("remote_model") or envelope.get("remote_host"):
            raise ValueError("The selected model relies on a remote Ollama service.")
        return envelope
    finally:
        connection.close()


def complete(model, prompt, specification):
    installed = post("/api/show", {"model": model})
    if not installed.get("model_info"):
        raise ValueError("Ollama did not confirm local model metadata.")
    envelope = post("/api/chat", {
        "model": model, "stream": False, "format": specification,
        "messages": [{"role": "system", "content": SYSTEM}, {"role": "user", "content": prompt}],
        "options": {"temperature": 0, "num_predict": 4096},
    })
    if envelope.get("done") is not True or envelope.get("done_reason") == "length":
        raise ValueError("Ollama did not return a complete chat response.")
    message = envelope.get("message")
    if not isinstance(message, dict) or not isinstance(message.get("content"), str) or message.get("tool_calls"):
        raise ValueError("Ollama must return recipe data as chat content.")
    return parse_json(message["content"])


def validate_constraints(recipe, prompt, maps):
    brief = compile_brief(prompt)
    constraints = brief["constraints"]
    excluded = {maps.get(key(entry["id"])) for entry in constraints["map"] if entry["intent"] == "exclude"}
    explicit = maps.get(key(brief["map"])) if brief["map"] else None
    if recipe["map"] in excluded or explicit is not None and explicit != recipe["map"]:
        raise ValueError("The proposed recipe conflicts with the requested map.")
    for entry in constraints["unresolved_maps"]:
        if entry["intent"] == "requested" and maps.get(key(entry["label"])) != recipe["map"]:
            raise ValueError(f"The proposed recipe does not use the explicitly requested map {entry['label']!r}.")
    for line in brief["owner_notes"]:
        tagged_map = re.match(r"^(?:use\s+)?map\s*[:=]\s*([^,.;!?]+)", line, re.I)
        if tagged_map and maps.get(key(tagged_map[1])) != recipe["map"]:
            raise ValueError(f"The proposed recipe does not use the explicitly requested map {tagged_map[1]!r}.")
    if brief["aspect"] and brief["aspect"] != recipe["aspect"]:
        raise ValueError("The proposed recipe conflicts with the requested aspect.")
    if (any(entry["id"] == "audible_audio" and entry["intent"] == "exclude"
            for entry in constraints["audio_and_titles"]) and not recipe["muteDelivery"]):
        raise ValueError("The proposed recipe conflicts with the requested silence.")
    excluded_hazards = {key(entry["id"]) for entry in constraints["hazards"] if entry["intent"] == "exclude"}
    for cue in recipe["cues"]:
        hazard = cue["hazard"] if cue["action"] == "hazard" else cue["action"]
        if key(hazard) in excluded_hazards:
            raise ValueError("The proposed recipe cues an explicitly excluded hazard.")


def direct(prompt, output, model):
    output = output.resolve()
    if output.is_relative_to(ROOT / "Assets") or output.exists():
        raise ValueError("Choose a new output directory outside Assets.")
    if not prompt or not prompt.strip() or len(prompt) > 12_000:
        raise ValueError("Supply a prompt of 1 to 12,000 characters.")
    if (not re.fullmatch(r"(?:[a-zA-Z0-9][a-zA-Z0-9_-]*/)?[a-zA-Z0-9][a-zA-Z0-9_.-]*(?::[a-zA-Z0-9_.-]+)?", model)
            or len(model) > 200 or re.search(r"(?:[:-]|^)cloud(?:[:/-]|$)", model, re.I)):
        raise ValueError("Name an installed local model explicitly. Cloud tags and registry hosts are unsupported.")
    maps = lookup_maps()
    specification = schema(maps.values(), lookup_hazards().values())
    draft = complete(model, prompt, specification)
    check(draft, specification)
    recipe = draft["recipe"]
    if recipe is not None:
        validate_constraints(recipe, prompt, maps)
        validate_timeline(recipe)
        width, height = RESOLUTIONS[recipe.pop("aspect")]
        recipe.update(fps=15, width=width, height=height, seed=148, warmup=.75, ffmpeg="ffmpeg")
    elif not draft["unresolved"]:
        raise ValueError("A blocked draft must report its unresolved directions.")
    report = {
        "status": "recipe_staged_render_pending" if recipe else "direction_unresolved",
        "director": "local_ollama",
        "model": model,
        "original_prompt": prompt,
        "unresolved": draft["unresolved"],
        "pending_review": ["Check the recipe against the original prompt and review a Unity scout before delivery."],
        "runtime_evidence": [],
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(dir=output.parent) as temp:
        bundle = Path(temp) / "bundle"
        bundle.mkdir()
        files = {"staging.json": report}
        if recipe is not None:
            files["recipe.json"] = recipe
        for filename, data in files.items():
            (bundle / filename).write_text(json.dumps(data, indent=2, allow_nan=False) + "\n", encoding="utf-8")
        bundle.rename(output)
    return report["status"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("prompt_file", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--model", required=True, help="Model already installed in local Ollama")
    args = parser.parse_args()
    try:
        status = direct(args.prompt_file.read_text(encoding="utf-8-sig"), args.output, args.model)
    except (OSError, ValueError, http.client.HTTPException) as error:
        parser.exit(1, f"{error}\n")
    print(args.output.resolve())
    if status == "direction_unresolved":
        parser.exit(2, "Review unresolved directions in staging.json.\n")


if __name__ == "__main__":
    main()
