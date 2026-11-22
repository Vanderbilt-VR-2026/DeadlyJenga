"""Stage an editable directing brief as a local Gameplay Theatre scout recipe."""

import argparse
import json
import math
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[4]
GAME = ROOT / "Assets/_DeadlyJenga"
RUN_METHOD = "DeadlyJenga.DevTools.Editor.GameplayTheatre.RunBatch"
RESOLUTIONS = {"16:9": (640, 360), "9:16": (360, 640), "1:1": (480, 480)}
CAMERAS = {
    "wide": ((11, 4, -14), (9, 3, -12), (0, -.6, 0), 55),
    "third_person": ((-1, 2, -4), (-.75, 1.8, -3.4), (0, .8, 0), 55),
    "close": ((-1.6, 1.55, -2.4), (-1.2, 1.45, -2), (0, 1, 0), 43),
    "first_person": ((0, 0, .14), (0, 0, .14), (0, 0, 0), 63),
}


def vector(x, y, z):
    return {"x": x, "y": y, "z": z}


def key(text):
    return re.sub(r"[^a-z0-9]", "", text.lower())


def lookup_maps():
    catalog = GAME / "GameSession/Session/Resources/IslandLobby/Catalog.asset"
    maps = {}
    yaml = catalog.read_text(encoding="utf-8-sig").split("\n  modes:", 1)[0]
    for map_id, title in re.findall(r"^  - id: (.+)\n    title: (.+)$", yaml, re.M):
        maps[key(map_id)] = map_id
        maps.setdefault(key(title.strip("\"'")), map_id)
    source = GAME / "GameSession/Session/Scripts/IslandLobbyCatalog.cs"
    families = source.read_text(encoding="utf-8-sig")
    prefabs = GAME / "World/Maps/Resources/ModeMaps"
    for family, title in re.findall(r'AddMap\(list,"([^"]+)","([^"]+)"', families):
        if (prefabs / f"Mode_{family}.prefab").is_file():
            maps.setdefault(key(family), f"mode-{family}")
            maps.setdefault(key(title), f"mode-{family}")
            maps.setdefault(key(f"mode-{family}"), f"mode-{family}")
    return maps


def lookup_hazards():
    hazards = GAME / "World/Hazards"
    catalog = (hazards / "Resources/DisasterCatalog.asset").read_text(encoding="utf-8-sig")
    guids = set(re.findall(r"guid: ([0-9a-f]{32})", catalog))
    prefabs = {}
    for meta in sorted(hazards.rglob("*.prefab.meta")):
        guid = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(encoding="utf-8-sig"), re.M)
        if guid and guid[1] in guids and meta.with_suffix("").is_file():
            prefab = meta.name.removesuffix(".prefab.meta")
            prefabs[key(prefab)] = prefab
    return prefabs


def requested(entries):
    blocked = {entry["id"] for entry in entries if entry["intent"] == "exclude"}
    selected = {}
    for entry in entries:
        if entry["intent"] == "requested" and entry["id"] not in blocked:
            selected.setdefault(entry["id"], entry)
    return selected


def make_shot(style, start, duration, pair):
    origin, end, look, fov = CAMERAS[style]
    if pair and style == "wide":
        origin, end, look, fov = (4.3, 2.6, -2.1), (3.9, 2.4, -1.8), (0, .8, 0), 49
    elif pair and style == "close":
        origin, end = (-2.6, 1.55, -.9), (-2.35, 1.45, -.8)
    shot = {
        "name": f"{style.replace('_', ' ').title()} scout",
        "start": start, "duration": duration, "fov": fov,
        "from": vector(*origin), "to": vector(*end), "look": vector(*look),
        "avoidWalls": style in ("close", "third_person") or pair and style == "wide",
        "firstPerson": style == "first_person",
    }
    if style != "wide" or pair:
        shot["subject"] = "survivor"
    if style in ("wide", "close") and pair:
        shot["second"] = "rival"
    return shot


def move_at(actor, at, duration, end, lift=0):
    actor.setdefault("moves", []).append(
        {"at": at, "duration": duration, "to": vector(*end), "lift": lift})


def block_performers(cast, features, cues, fps, duration, choices, pending):
    punch = next((cue for cue in cues if cue["action"] == "punch"), None)
    travel = bool({"run", "jump", "chase"} & features.keys())
    if not cast:
        return
    jump = features.get("jump", {})
    lead_id = (punch or {}).get("actor") or jump.get("actor") or features.get("run", {}).get("actor") or "survivor"
    lead = next(actor for actor in cast if actor["id"] == lead_id)
    if punch:
        approach = min(1.2, punch["at"] - 1 / fps)
        if approach >= 1 / fps:
            # The scripted performer faces the direction of its last movement.
            rival = next(actor for actor in cast if actor["id"] == punch["target"])
            lead["start"], rival["start"] = vector(-.15, 0, -2.7), vector(.15, 0, 1.25)
            move_at(lead, 0, approach, (-.15, 0, -.5))
            move_at(rival, 0, approach, (.15, 0, .55))
            if jump:
                jumper = next(actor for actor in cast if actor["id"] == jump.get("actor", lead_id))
                when = jump.get("at_seconds")
                if when is None:
                    jumper["moves"][0]["lift"] = .55
                elif when >= approach and when + .25 <= duration:
                    mark = jumper["moves"][0]["to"]
                    move_at(jumper, when, min(.65, duration - when), (mark["x"], mark["y"], mark["z"]), .65)
                else:
                    pending.append("Explicit jump timing conflicts with the approach path. Rehearse a custom path.")
            choices.append("Survivors approach from opposing directions and stop about 1.1 metres apart before the punch cue.")
            pending.append("The contact distance and facing are scripted. Verify health change and visible response in Unity.")
        else:
            pending.append("Runtime leaves too little lead-in to stage the punch approach.")
        if "chase" in features:
            pending.append("The punch uses opposing approaches. A separate pursuit needs a custom performance.")
        if travel and any(re.search(r"\b(?:after|finish|end|escape|flee)\b", features[event]["source"], re.I)
                          for event in {"run", "jump"} & features.keys()):
            pending.append("The default punch blocking uses movement before contact. Check any requested later movement.")
        return
    if not travel:
        choices.append("Uses production survivor rigs with fixed marks for the scout.")
        return

    start = jump.get("at_seconds", features.get("run", {}).get("at_seconds", 0))
    run_time = min(2.25, duration * .65, duration - start)
    if run_time <= 0:
        raise ValueError("Actor movement starts after the scene ends.")
    move_at(lead, start, run_time, (0, 0, 3.8), .75 if jump else 0)
    if "chase" in features:
        other = next(actor for actor in cast if actor != lead)
        other["start"] = vector(.35, 0, -1.65)
        move_at(other, start, run_time, (.35, 0, 2.25))
    choices.append("Authored a short stage-relative movement path with the requested jump or pursuit where applicable.")
    pending.append("Movement uses a relative path. Verify footing, route, camera tracking and any requested destination on the actual map.")


def stage(brief):
    if not isinstance(brief, dict) or not isinstance(brief.get("goal"), str) or not brief["goal"].strip():
        raise ValueError("Expected a directing brief with an original prompt.")
    constraints = brief.get("constraints", {})
    notes = brief.get("owner_notes", [])
    phases = {beat["direction"]: beat["role"] for beat in brief.get("requested_beats", [])}

    def order(entry):
        source = entry["source"]
        phase = {"hook": 0, "payoff": 2}.get(phases.get(source), 1)
        return phase, notes.index(source) if source in notes else len(notes)

    report = {
        "status": "recipe_staged_render_pending",
        "brief": brief,
        "choices": [],
        "cue_attempts": [],
        "movement_requests": [],
        "pending_review": [],
        "runtime_evidence": [],
    }
    choices = report["choices"]
    pending = report["pending_review"]
    maps = lookup_maps()
    map_label = brief.get("map")
    if any(entry["intent"] == "requested" for entry in constraints.get("unresolved_maps", [])):
        raise ValueError("Resolve the unknown map name in the brief before staging.")
    if map_label:
        map_id = maps.get(key(map_label))
        if not map_id:
            raise ValueError(f"Map {map_label!r} did not resolve to this checkout's lobby catalog.")
    elif constraints.get("map") or any(entry["intent"] == "requested"
                                      for entry in constraints.get("unresolved_maps", [])):
        raise ValueError("Resolve the map constraint by setting the brief's map to an allowed catalog name or ID.")
    else:
        map_id = "castle"
        choices.append("No map selected. Scout defaults to Candy Castle.")
    if any(entry["intent"] == "exclude" and maps.get(key(entry["id"])) == map_id
           for entry in constraints.get("map", [])):
        raise ValueError("The selected map is explicitly excluded by the brief.")

    duration = brief.get("duration_seconds")
    if duration is None and constraints.get("duration"):
        raise ValueError("Set duration_seconds in the brief to resolve the requested time range.")
    if duration is None:
        duration = 6
        choices.append("No runtime selected. Scout defaults to six seconds.")
    if isinstance(duration, bool) or not isinstance(duration, (int, float)) or not .25 <= duration <= 60:
        raise ValueError("Gameplay Theatre takes must be between 0.25 and 60 seconds.")
    fps = 15
    exact = duration
    duration = max(4, round(duration * fps)) / fps
    if not math.isclose(exact, duration):
        choices.append(f"Rounded runtime from {exact:g} to {duration:.3f} seconds at {fps} fps.")

    aspect = brief.get("aspect")
    formats = constraints.get("aspect", [])
    if not aspect and any(entry["intent"] == "requested" for entry in formats):
        raise ValueError("Set aspect in the brief to resolve the requested formats.")
    if not aspect:
        excluded = {entry["id"] for entry in formats if entry["intent"] == "exclude"}
        aspect = next((fmt for fmt in RESOLUTIONS if fmt not in excluded), None)
        choices.append(f"Using an available scout aspect of {aspect}.")
    if aspect not in RESOLUTIONS:
        raise ValueError(f"Unsupported delivery aspect {aspect!r}.")
    width, height = RESOLUTIONS[aspect]

    features = requested(constraints.get("gameplay", []))
    hazards = requested(constraints.get("hazards", []))
    must_show = {item["id"] for item in brief.get("must_show", [])}
    for event, entry in {**features, **hazards}.items():
        if event not in must_show:
            pending.append(f"Resolve the staging intent for {event}: {entry['source']}")
    features = {event: entry for event, entry in features.items() if event in must_show}
    hazards = {event: entry for event, entry in hazards.items() if event in must_show}
    cameras = constraints.get("camera", [])
    camera_intents = requested(cameras)
    excluded_cameras = {entry["id"] for entry in cameras if entry["intent"] == "exclude"}
    angles = [entry for entry in cameras if entry["intent"] == "requested" and entry["id"] in CAMERAS
              and entry["id"] not in excluded_cameras]
    styles = [entry["id"] for entry in angles]
    if not styles:
        preferred = "third_person" if {"run", "jump", "chase"} & features.keys() else "wide"
        styles = [style for style in (preferred, *CAMERAS) if style not in excluded_cameras][:1]
        choices.append("Using the first available scout framing preset.")
    if not styles:
        raise ValueError("The brief excludes every supported camera preset.")
    if "single_take" in camera_intents and len(styles) > 1:
        pending.append("One camera used for the single take. Other requested framings need a custom rail.")
        styles = styles[:1]
    choices.append("Camera presets use stage-relative or actor-relative positions; confirm composition in the scout.")

    environment = bool(re.search(r"\b(?:no (?:survivors|actors|characters|players|people)|without (?:any )?(?:survivors|actors|characters|players|people)|empty (?:landscape|castle|map)|environment.only)\b", brief["goal"], re.I))
    if environment and ({"run", "jump", "chase", "pvp_hit"} & features.keys() or "first_person" in styles):
        raise ValueError("An empty cast conflicts with the requested actor action or POV. Edit the brief.")
    if any(e.get("actor") == "hand" or e.get("target") == "hand" for e in features.values()):
        raise ValueError("The flying hand needs a Trailer Studio performer track for this action.")
    pair = bool({"pvp_hit", "chase"} & features.keys() or any(e.get("actor") == "rival" for e in features.values()))
    cast = [{"id": "survivor", "color": 0, "start": vector(0, 0, -.55 if pair else 0)}]
    if pair:
        cast.append({"id": "rival", "color": 2, "start": vector(0, 0, .55)})
    if environment:
        cast = []

    render_frames = round(duration * fps)
    shot_count = min(len(styles), render_frames // 4)
    if shot_count < len(styles):
        pending.append("The brief's runtime is too short to fit all requested camera presets.")
    boundaries = [index * render_frames // shot_count for index in range(shot_count + 1)]
    for index, style in enumerate(styles[:shot_count]):
        when = angles[index].get("at_seconds") if angles else None
        if when is not None:
            if index == 0 and when > 0:
                raise ValueError("Specify the camera framing before the timed camera change.")
            boundaries[index] = round(when * fps)
    if any(b - a < 4 for a, b in zip(boundaries, boundaries[1:])):
        raise ValueError("The requested camera timing leaves less than four frames in a shot.")
    shots = [make_shot(style, boundaries[index] / fps, (boundaries[index + 1] - boundaries[index]) / fps,
                       pair or "chase" in features)
             for index, style in enumerate(styles[:shot_count])]
    if environment:
        for shot in shots:
            shot.pop("subject", None)
            shot.pop("second", None)

    prefabs = lookup_hazards()
    cue_specs = []
    for event, entry in sorted({**features, **hazards}.items(), key=lambda item: order(item[1])):
        if event in ("run", "jump", "chase"):
            continue
        if event == "pvp_hit":
            actor = entry.get("actor", "rival" if entry.get("target") == "survivor" else "survivor")
            cue = {"action": "punch", "actor": actor, "target": entry.get("target", "rival" if actor == "survivor" else "survivor"),
                   "requireTargetContact": True}
            if cue["actor"] == cue["target"]:
                raise ValueError("Assign distinct performers to the staged contact.")
        elif event == "collapse":
            cause = re.search(r"\b(?:caus\w+|makes?|triggers?|because|from|knocks? down|destroys?)\b",
                              entry["source"], re.I)
            if cause and any(hazard["source"] == entry["source"] for hazard in hazards.values()):
                pending.append("The prompt connects a hazard and structural failure. Observe that relationship "
                               "in a scout before adding any independent collapse impulse.")
                continue
            cue = {"action": "collapse", "strength": 9}
        elif event == "tsunami" and key(event) in prefabs:
            cue = {"action": "tsunami", "heading": 0}
            pending.append("The staged tsunami moves blocks with player effects disabled. Verify any requested survivor outcome separately.")
        elif event in hazards and key(event) in prefabs:
            cue = {"action": "hazard", "hazard": prefabs[key(event)]}
        else:
            pending.append(f"No automatic Theatre staging for {event}: {entry['source']}")
            continue
        cue_specs.append((event, entry, cue))

    cues = []
    shared_frames = {}
    for index, (event, entry, cue) in enumerate(cue_specs, 1):
        # Spread cues across the early and middle frames to leave aftermath coverage.
        fraction = .15 + .45 * (index - .5) / len(cue_specs)
        if phases.get(entry["source"]) == "payoff":
            fraction = .75 + .15 * index / len(cue_specs)
        frame = min(render_frames - 1, max(2, round(render_frames * fraction)))
        if entry["source"].lower().startswith(("then ", "afterward", "after that")):
            prior_sources = notes[:notes.index(entry["source"])] if entry["source"] in notes else []
            earlier_times = [other["at_seconds"] for other in (*features.values(), *hazards.values())
                             if other["source"] in prior_sources and "at_seconds" in other]
            if earlier_times:
                frame = max(frame, round((max(earlier_times) + .65) * fps))
                if frame >= render_frames:
                    raise ValueError("Insufficient time after the preceding action. Extend the scene or edit the timing.")
        if re.search(r"\b(?:while|simultaneously|at the same time)\b", entry["source"], re.I):
            frame = shared_frames.setdefault(entry["source"], frame)
        if "at_seconds" in entry:
            frame = round(entry["at_seconds"] * fps)
            if not 0 <= frame < render_frames:
                raise ValueError("Requested cue falls outside the scene duration.")
        cue["at"] = frame / fps
        cues.append(cue)
        report["cue_attempts"].append({"id": event, "source": entry["source"], "cue": cue})
    if cue_specs:
        pending.append("Cue timing is suggested. Review the cue log and footage for accepted hits, map-filtered hazards, event timing and visible consequences.")
    if any(entry["intent"] == "exclude" for group in ("gameplay", "hazards")
           for entry in constraints.get(group, [])):
        pending.append("Excluded events have no explicit cues. Check the resulting simulation and footage for prohibited content.")
    if any(entry["intent"] == "exclude" and entry["id"] == "Demo" for entry in constraints.get("mode", [])):
        raise ValueError("The brief excludes Demo rules, which the Gameplay Theatre staged route requires.")
    if brief.get("mode_rules") and brief["mode_rules"] != "Demo":
        pending.append("Gameplay Theatre stages with Demo rules. The requested match mode needs a separate capture.")
    silent = any(entry["id"] == "audible_audio" and entry["intent"] == "exclude"
                 for entry in constraints.get("audio_and_titles", []))
    if silent:
        choices.append("Mute the delivered shot audio and final edit while retaining the captured source WAV.")
    if any(entry["id"] != "audible_audio" for entry in constraints.get("audio_and_titles", [])):
        pending.append("Scout uses the native game mix. Review audio exclusions, added sound, dialogue and titles in finishing.")
    if "montage" in camera_intents or "single_take" in excluded_cameras:
        pending.append("Confirm cut rhythm and shot count in editorial.")
    for note in brief.get("unclassified_notes", []):
        if environment and re.fullmatch(r"\s*no (?:survivors|actors|characters|players|people)[.! ]*", note, re.I):
            choices.append("Use an empty cast and hide the local player's geometry for this environment take.")
            continue
        pending.append(f"No automatic staging for this direction: {note}")
    if brief.get("requested_beats"):
        pending.append("Review the original directing clauses for exact choreography, event order, performer actions and visual beats.")

    block_performers(cast, features, cues, fps, duration, choices, pending)
    report["movement_requests"] = [entry for event, entry in features.items() if event in ("run", "jump", "chase")]
    recipe = {
        "title": "Prompt scout", "map": map_id, "fps": fps, "width": width, "height": height,
        "seed": 148, "warmup": .75, "ffmpeg": "ffmpeg", "cast": cast, "shots": shots, "cues": cues,
        "edit": [{"shot": index, "from": 0, "duration": shot["duration"]} for index, shot in enumerate(shots)],
    }
    if environment:
        recipe["environmentOnly"] = True
    if silent:
        recipe["muteDelivery"] = True
    return recipe, report


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("brief", type=Path, help="JSON from direct_prompt.py")
    parser.add_argument("--output", required=True, type=Path, help="new folder outside Unity Assets")
    args = parser.parse_args(argv)
    try:
        output = args.output.resolve()
        if output.is_relative_to(ROOT / "Assets"):
            raise ValueError("Choose an output folder outside Unity Assets.")
        brief = json.loads(args.brief.read_text(encoding="utf-8-sig"))
        recipe, report = stage(brief)
        report["unity_execute_method"] = RUN_METHOD
        report["unity_argument_working_directory"] = "folder containing staging.json"
        report["unity_arguments"] = ["-theatrePlan", "recipe.json", "-theatreOutput", "renders"]
        output.mkdir(parents=True, exist_ok=False)
        for name, value in (("recipe.json", recipe), ("staging.json", report)):
            with (output / name).open("x", encoding="utf-8") as stream:
                json.dump(value, stream, indent=2, allow_nan=False)
                stream.write("\n")
    except (KeyError, TypeError, OSError, ValueError) as error:
        parser.error(str(error))
    print(f"Staged {output / 'recipe.json'} for Unity. Render and review pending.")


if __name__ == "__main__":
    main()
