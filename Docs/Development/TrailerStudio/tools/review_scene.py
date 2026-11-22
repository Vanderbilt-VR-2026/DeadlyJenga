"""Audit a staged Gameplay Theatre take. Technical passes still need creative review."""

import argparse
from fractions import Fraction
import json
import math
from pathlib import Path
import re
import subprocess

from direct_prompt import compile_brief


DEFAULTS = {
    "stageOffset": {"x": 0, "y": 0, "z": 0},
    "mapAnchor": {"x": 0, "y": 0, "z": 0},
    "useMapAnchor": False, "replaySource": "", "observedMatch": False,
    "environmentOnly": False, "muteDelivery": False, "fightSet": False, "rooftopBattleSet": False,
    "live": False, "customLook": False, "autoFocus": False, "avoidWalls": False,
    "firstPerson": False, "roll": 0, "lift": 0, "strength": 0, "heading": 0, "height": 0,
    "optional": False, "lethal": False, "requireTargetContact": False,
    "poses": [], "performance": [], "stunt": [], "moves": [],
    "appearance": {"accessories": 0, "skin": 0, "hair": 0, "eyewear": 0, "clothing": 0,
                   "poweredArmor": False, "hairShade": 0},
}
EMPTY_STRINGS = {"actor", "target", "hazard", "weapon", "second", "subject"}
EMPTY_ARRAYS = {"poses", "performance", "stunt", "moves", "cues"}
CUE_LINE = re.compile(r"^(\d+(?:[.,]\d+)?)\s+(\w+)(?:\s+(.*))?$")


def read_json(path):
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError(f"{path.name} must contain an object.")
    return value


def differences(source, rendered, path=""):
    """Ignore only Unity's known serializer defaults and the encoder path override."""
    if isinstance(source, dict) and isinstance(rendered, dict):
        for key in sorted(source.keys() | rendered.keys()):
            if key == "ffmpeg" and not path:
                continue
            default = "" if key in EMPTY_STRINGS else DEFAULTS.get(key)
            a, b = source.get(key, default), rendered.get(key, default)
            if key in EMPTY_STRINGS or key in EMPTY_ARRAYS:
                a, b = a or default, b or default
            yield from differences(a, b, f"{path}.{key}" if path else key)
    elif isinstance(source, list) and isinstance(rendered, list) and len(source) == len(rendered):
        for index, (a, b) in enumerate(zip(source, rendered)):
            yield from differences(a, b, f"{path}[{index}]")
    elif type(source) in (int, float) and type(rendered) in (int, float):
        if not math.isclose(source, rendered, rel_tol=1e-7, abs_tol=1e-7):
            yield path
    elif type(source) is not type(rendered) or source != rendered:
        yield path


def ceil_frame(seconds, fps):
    # The source decimals may round-trip through Unity single precision.
    frames = seconds * fps
    nearest = round(frames)
    return nearest if abs(frames - nearest) < 0.0001 else math.ceil(frames)


def edit_ranges(recipe):
    fps = recipe["fps"]
    if type(fps) is not int or fps <= 0 or not recipe["edit"]:
        raise ValueError("The recipe needs a positive integer fps and at least one cut.")
    for cut in recipe["edit"]:
        if type(cut["shot"]) is not int or not 0 <= cut["shot"] < len(recipe["shots"]):
            raise ValueError("The edit references an invalid shot.")
        shot = recipe["shots"][cut["shot"]]
        start = ceil_frame(cut["from"], fps)
        end = min(ceil_frame(cut["from"] + cut["duration"], fps), ceil_frame(shot["duration"], fps))
        if start < 0 or end <= start:
            raise ValueError("The edit has an empty or invalid cut.")
        offset = round(shot["start"] * fps)
        yield offset + start, offset + end


def command(executable, *args):
    run = subprocess.run([executable, *args], capture_output=True, timeout=120)
    if run.returncode or run.stderr.strip():
        raise ValueError(f"{Path(executable).name}: {run.stderr.decode(errors='replace').strip()}")
    return run.stdout


def probe_edit(video, recipe, brief, ranges, ffprobe, ffmpeg):
    if not video.is_file():
        raise ValueError("Missing edit.mp4.")
    streams = json.loads(command(ffprobe, "-v", "error", "-count_frames", "-show_streams",
                                 "-of", "json", str(video)))["streams"]
    pictures = [stream for stream in streams if stream["codec_type"] == "video"]
    if len(pictures) != 1:
        raise ValueError(f"Expected one video stream, found {len(pictures)}.")
    picture = pictures[0]
    width, height = picture["width"], picture["height"]
    aspect = Fraction(width, height)
    sar = picture.get("sample_aspect_ratio")
    display_aspect = aspect * Fraction(sar.replace(":", "/")) if sar not in (None, "N/A", "0:1") else aspect
    if any(int(item.get("rotation", 0)) % 180 == 90 for item in picture.get("side_data_list", [])):
        display_aspect = 1 / display_aspect
    requested = brief.get("aspect")
    expected_aspect = Fraction(requested.replace(":", "/")) if requested else Fraction(recipe["width"], recipe["height"])
    actual_frames = int(picture["nb_read_frames"])
    expected_frames = sum(end - start for start, end in ranges)
    evidence = [f"Encoded {width}×{height}, display aspect {display_aspect}, {actual_frames} decoded video frames."]
    errors = []
    if (width, height) != (recipe["width"], recipe["height"]) or display_aspect != expected_aspect:
        errors.append(f"Encoded dimensions or display aspect do not match the recipe and requested aspect {expected_aspect}.")
    if actual_frames != expected_frames:
        errors.append(f"Decoded {actual_frames} frames, expected {expected_frames} from the edit at {recipe['fps']} fps.")
    duration = brief.get("duration_seconds")
    if duration is not None and abs(actual_frames - duration * recipe["fps"]) > .50001:
        errors.append(f"Decoded frame count does not match the requested {duration}s at {recipe['fps']} fps.")
    # concat may introduce sub-frame timestamp offsets between cuts from audio padding.
    fps = Fraction(picture["r_frame_rate"])
    if fps != recipe["fps"]:
        errors.append(f"Encoded nominal frame rate is {fps}, expected {recipe['fps']}.")
    if abs(float(picture["duration"]) * recipe["fps"] - expected_frames) > 1:
        errors.append("Encoded video duration differs from the declared edit by more than one frame.")
    if recipe.get("muteDelivery") or any(
        entry["id"] == "audible_audio" and entry["intent"] == "exclude"
        for entry in brief.get("constraints", {}).get("audio_and_titles", [])
    ):
        audio = [stream for stream in streams if stream["codec_type"] == "audio"]
        for stream in audio:
            pcm = command(ffmpeg, "-nostdin", "-v", "error", "-xerror", "-i", str(video),
                          "-map", f"0:{stream['index']}", "-c:a", "pcm_f32le", "-f", "f32le", "-")
            if not pcm or len(pcm) % 4 or any(pcm[i:i + 4] not in (b"\0\0\0\0", b"\0\0\0\x80")
                                             for i in range(0, len(pcm), 4)):
                errors.append(f"Stream {stream['index']} did not decode to silence.")
        if not audio:
            evidence.append("The MP4 contains no audio stream.")
        elif not any("decode to silence" in error for error in errors):
            evidence.append(f"All {len(audio)} encoded audio stream(s) decoded to zero samples in amplitude.")
    return evidence, errors


def log_identity(cue):
    action = cue["action"]
    if action in {"punch", "fire"}:
        return f"{cue['actor']}->{cue['target']}"
    if action == "hazard":
        return cue["hazard"]
    if action == "gear":
        return cue["weapon"]
    return ""


def contact_proven(detail):
    fields = dict(re.findall(r"\b(\w+)=([^\s]*)", detail or ""))
    try:
        before, after, delta = (float(fields[key].replace(",", "."))
                                for key in ("healthBefore", "healthAfter", "healthDelta"))
    except (KeyError, ValueError):
        return False
    return (fields.get("gameplayDispatched") == fields.get("gameplayAccepted") == "True"
            and fields.get("targetPushed") == "True" and fields.get("otherPushed") == "False"
            and bool(fields.get("targetId")) and fields.get("predictedTargetId") == fields["targetId"]
            and bool(fields.get("attackerId")) and fields["attackerId"] != fields["targetId"]
            and all(math.isfinite(value) for value in (before, after, delta))
            and before > after and delta > 0 and abs(before - after - delta) < .02)


def review_cues(log, recipe, brief, ranges):
    rows = []
    errors = []
    evidence = []
    fps = recipe["fps"]
    for line in log.splitlines():
        match = CUE_LINE.fullmatch(line.strip())
        if match:
            time, action, detail = match.groups()
            rows.append((float(time.replace(",", ".")), action, detail or ""))
    punch_required = any(entry.get("id") == "pvp_hit" for entry in brief.get("must_show", []))
    cues = recipe.get("cues") or []
    if punch_required and not any(cue["action"] == "punch" for cue in cues):
        errors.append("The brief requires a gameplay punch but the recipe has no punch cue.")
    for cue in cues:
        action = cue["action"]
        identity = log_identity(cue)
        label = f"{action} {identity}".strip()
        frame = ceil_frame(cue["at"], fps)
        match = next((index for index, (time, event, detail) in enumerate(rows)
                      if event == action and abs(time - frame / fps) <= .00051
                      and (not identity or (detail.split() and detail.split()[0].casefold() == identity.casefold()))), None)
        if match is None:
            errors.append(f"Missing runtime cue for {label} at {frame / fps:.3f}s.")
            continue
        _, _, detail = rows.pop(match)
        if action == "punch" and (punch_required or cue.get("requireTargetContact")):
            if not contact_proven(detail):
                errors.append(f"Unverified required contact for {label}. Runtime log: {detail}")
            elif not any(start <= frame < end for start, end in ranges):
                errors.append(f"Verified contact for {label} falls outside the edited footage.")
            else:
                evidence.append(f"Confirmed intended-recipient contact in the edit for {label}. Runtime log: {detail}")
        else:
            rejected = "gameplayAccepted=False" in detail or "accepted=False" in detail
            if rejected and not cue.get("optional"):
                errors.append(f"Gameplay rejected {label}. Runtime log: {detail}")
            else:
                evidence.append(f"Runtime cue at {frame / fps:.3f}s: {label}{' (optional action rejected)' if rejected else ''}.")
    if rows:
        errors.append(f"Unexpected runtime cues: {rows}")
    return evidence, errors


def review(staged, take, ffprobe="ffprobe", ffmpeg="ffmpeg"):
    result = {"status": "failed", "staged_bundle": str(staged), "take_folder": str(take),
              "runtime_evidence": [], "failures": [], "creative_review": []}
    evidence, errors = result["runtime_evidence"], result["failures"]
    try:
        staging = read_json(staged / "staging.json")
        brief_path = staged / "brief.json"
        brief = read_json(brief_path) if brief_path.is_file() else staging.get("brief")
        if brief is None and staging.get("original_prompt"):
            brief = compile_brief(staging["original_prompt"])
        if not isinstance(brief, dict):
            raise ValueError("Missing directing brief or original prompt.")
        result["creative_review"] = list(dict.fromkeys([
            *staging.get("unresolved", []), *staging.get("pending_review", []),
            "Watch the edit for framing, choreography, pacing, event visibility, and prompt exclusions.",
        ]))
        if staging.get("status") != "recipe_staged_render_pending":
            raise ValueError("The directing report has no staged recipe available for audit.")
        recipe = read_json(staged / "recipe.json")
        ranges = list(edit_ranges(recipe))
    except (OSError, ValueError, TypeError, KeyError, IndexError, ArithmeticError) as error:
        errors.append(f"Staged bundle: {error}")
        return result

    def check(label, operation):
        try:
            return operation()
        except (OSError, ValueError, TypeError, KeyError, IndexError, ArithmeticError, subprocess.TimeoutExpired) as error:
            errors.append(f"{label}: {error}")
            return None

    receipt = check("Render result", lambda: (take / "result.txt").read_text(encoding="utf-8-sig").strip())
    if receipt is not None and not receipt.startswith("PASS "):
        errors.append(f"Theatre did not report a successful render. Runtime result: {receipt}")
    rendered = check("Rendered recipe", lambda: read_json(take / "recipe.json"))
    if rendered is not None:
        changed = list(differences(recipe, rendered))
        if changed:
            errors.append("Rendered recipe differs from the staged recipe at " + ", ".join(changed) + ".")
    take_info = check("Take info", lambda: (take / "take.txt").read_text(encoding="utf-8-sig").splitlines())
    if take_info is not None and (f"Map={recipe['map']}" not in take_info or f"FPS={recipe['fps']}" not in take_info):
        errors.append("Take info does not identify the staged map and frame rate.")
    log = check("Cue log", lambda: (take / "cues.txt").read_text(encoding="utf-8-sig"))
    if log is not None:
        if not log.startswith("Simulation seconds and executed cues"):
            errors.append("Cue log has no Gameplay Theatre runtime header.")
        reviewed = check("Cue evidence", lambda: review_cues(log, recipe, brief, ranges))
        if reviewed is not None:
            logged, rejected = reviewed
            evidence.extend(logged)
            errors.extend(rejected)
    measured = check("Edit probe", lambda: probe_edit(take / "edit.mp4", recipe, brief, ranges, ffprobe, ffmpeg))
    if measured is not None:
        measurements, rejected = measured
        evidence.extend(measurements)
        errors.extend(rejected)
    if not errors:
        result["status"] = "creative_review_required"
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__, epilog=(
        "Use the exact staged bundle and its native take, with FFmpeg and FFprobe on PATH. "
        "Map and cue logs depend on the take's provenance. This does not judge visuals or approve delivery."))
    parser.add_argument("staged_bundle", type=Path)
    parser.add_argument("take_folder", type=Path)
    parser.add_argument("--ffprobe", default="ffprobe")
    parser.add_argument("--ffmpeg", default="ffmpeg")
    parser.add_argument("--output", type=Path, help="Save a JSON report to a new file.")
    args = parser.parse_args()
    audited = review(args.staged_bundle, args.take_folder, args.ffprobe, args.ffmpeg)
    data = json.dumps(audited, indent=2, ensure_ascii=True) + "\n"
    if args.output:
        try:
            with args.output.open("x", encoding="utf-8") as output:
                output.write(data)
        except OSError as error:
            parser.exit(1, f"{error}\n")
    print(data, end="")
    return int(audited["status"] == "failed")


if __name__ == "__main__":
    raise SystemExit(main())
