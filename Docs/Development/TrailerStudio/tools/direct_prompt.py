"""Turn a natural-language trailer request into a Stage 0 directing brief.

This offline compiler recognizes common phrasing. Read the original request and
the review items before using its suggestions to plan a scout or capture.
"""

import argparse
import json
import math
import re
import sys
from pathlib import Path


HAZARDS = {
    "planet_killer_ornament": r"planet[- ]killer ornament",
    "planet_killer_meteor": r"planet[- ]killer(?: meteor)?",
    "ornament_shower": r"ornament shower",
    "meteor_shower": r"meteor showers?|meteors?|asteroids?",
    "tsunami": r"tsunamis?|tidal waves?",
    "tornado": r"tornad(?:o|oes)|twisters?",
    "earthquake": r"earthquakes?|quakes?",
    "kraken": r"kraken",
    "acid_rain": r"acid rain",
    "fire": r"fire hazard|burning blocks|catch(?:es)? fire",
    "power_outage": r"power outage|blackout",
    "anti_gravity": r"anti[- ]gravity|zero[- ]gravity",
    "cat_attack": r"cat attack|giant cat",
    "evil_santa": r"evil santa",
    "vampire": r"vampire",
    "icey": r"icey|slippery blocks",
}

FEATURES = {
    "hand": (r"(?:giant |flying )+hands?", "Show the giant hand clearly interacting with a survivor or the structure."),
    "run": (r"sprint\w*|runn(?:ing|ers?)|runs?\s+(?:(?:off|along|across|through|toward|up|down|past)\b|the (?:route|ridge|path)\b)",
            "Show sustained survivor locomotion across readable terrain."),
    "jump": (r"jump(?:s|ed|ing)?|leap(?:s|ed|ing)?", "Show the takeoff, clearance, and landing."),
    "chase": (r"chas(?:e|es|ed|ing)|pursu(?:e|es|ed|ing|it)", "Show the runner and pursuer moving along a shared route."),
    "pvp_hit": (r"pvp|player[- ](?:vs\.?|versus)[- ]player|player versus player|punch\w*|shov\w*|boxing gloves?", "Keep the attacker, contact, and victim response readable together."),
    "knock_off": (r"(?:knock\w*|push\w*|launch\w*|throw\w*|toss\w*)[^.!?;]{0,40}\boff\b", "Show the attacker causing the survivor to leave their support."),
    "collapse": (r"collaps\w*|crumbl\w*|fall(?:s|ing)? apart", "Keep the failing support and the resulting structural movement visible."),
    "survivors_react": (r"survivor reactions?|(?:react|look up)(?:s|ing)?\b", "Show the threat and a readable change in the survivor's head, hands, or body."),
}

ASPECTS = {
    "9:16": r"9\s*:\s*16|vertical|portrait|tiktok|reels|shorts",
    "16:9": r"16\s*:\s*9|landscape|horizontal|widescreen",
    "1:1": r"1\s*:\s*1|square",
}
PLATFORMS = {
    "TikTok": r"tiktok",
    "Instagram Reels": r"(?:instagram )?reels",
    "YouTube Shorts": r"(?:youtube )?shorts",
    "YouTube": r"youtube(?!\s+shorts)",
    "Store page": r"store(?:front| page)|quest store|steam page",
    "Social feeds": r"social(?: media| feeds)?",
}
WORD_SECONDS = {
    "five": 5, "six": 6, "eight": 8, "ten": 10, "twelve": 12, "fifteen": 15,
    "eighteen": 18, "twenty": 20, "twenty-five": 25, "thirty": 30,
    "forty-five": 45, "sixty": 60,
}
SECONDS = r"\d+(?:\.\d+)?|" + "|".join(sorted(WORD_SECONDS, key=len, reverse=True))
DURATION = re.compile(
    rf"(?P<bound>at least |at most |up to |no more than |less than |under |over |about |around |roughly |approximately )?"
    rf"\b(?P<start>{SECONDS})(?:(?:\s*[-–]\s*|\s+to\s+)(?P<end>{SECONDS}))?"
    r"\s*[- ]?\s*(?:seconds?|secs?|s)\b(?!\s*(?:mark|in\b|into\b|from\b))",
    re.I,
)
AUDIO = {
    "audible_audio": r"silent|mute(?:d)?|no audio|no sound",
    "music": r"music|soundtrack|score",
    "dialogue": r"voice[- ]?over|dialogue|narration|spoken lines",
    "sound_effects": r"sound effects|sfx",
    "titles": r"title cards?|end cards?|captions?|on[- ]screen text",
}
CAMERAS = {
    "first_person": r"first[- ]person|pov|through (?:a |the |their )?survivor'?s? eyes",
    "third_person": r"third[- ]person|over[- ]the[- ]shoulder|ots",
    "close": r"close[- ]?ups?|up close|tight fram(?:e|ing)|stay close|(?:start|open|go|end|cut) (?:close|tight)",
    "wide": r"(?:wide|distant|long) shots?|establishing shots?|(?:start|open|go|end|cut) wide",
    "single_take": r"single[- ]take|one[- ]take|one continuous shot|unbroken shot|no cuts",
    "montage": r"montage|quick cuts|fast cuts",
}
MODES = {
    "Survival": r"survival mode",
    "Natural Disaster": r"natural disasters? mode",
    "Team Kill": r"team kill",
    "Team Tickets": r"team tickets",
    "King of the Hill": r"king of the hill|koth",
    "Capture the Flag": r"capture the flag|ctf",
    "Block Control": r"block control",
    "Infection": r"infection mode",
    "Murder Mystery": r"murder mystery",
    "Ninja": r"ninja mode|shadow lantern",
    "Demo": r"demo (?:mode|rules)",
}


def words(pattern):
    return rf"(?<![\w-])(?:{pattern})(?![\w-])"


def clauses(prompt):
    return [
        part.strip()
        for part in re.split(
            r"(?<=[.!?])\s+|[;\n]+|,\s*(?=(?:no\b|without\b|avoid\b|not\b|but\b|"
            r"then\b|finish\b|end\b|open\b|start\b))|\s+but\s+|\s+(?=then\s)",
            prompt,
            flags=re.I,
        )
        if part.strip()
    ]


def excluded(clause, match):
    """Use local scope so 'no hand, show a tornado' preserves the tornado."""
    prefix = re.split(r"[,;:]|\band\s+(?=(?:show|feature|include|use|open|start|finish|end|keep)\b)",
                      clause[:match.start()], flags=re.I)[-1]
    prefix = re.sub(r"\bno more than\b", "", prefix, flags=re.I)
    if re.search(r"\b(?:no|without|avoid|excluding|exclude|skip|omit|never|not|don['’]t|do not)\b"
                 r"(?!\s+(?:just|only)\b)", prefix, re.I):
        return True
    return bool(re.match(r"\s+(?:is |are )?(?:off[- ]limits|excluded|out of frame)\b",
                         clause[match.end():], re.I))


def find_mentions(catalog, parts):
    mentions = []
    for part in parts:
        claimed = []
        hits = []
        for key, pattern in catalog.items():
            for match in re.finditer(words(pattern), part, re.I):
                if any(match.start() < end and start < match.end() for start, end in claimed):
                    continue
                claimed.append(match.span())
                hits.append((match.start(), {
                    "id": key,
                    "intent": "exclude" if excluded(part, match) else "requested",
                    "source": part,
                    "phrase": match.group(),
                }))
        mentions.extend(entry for _, entry in sorted(hits, key=lambda hit: hit[0]))
    return mentions


def map_patterns():
    root = Path(__file__).resolve().parents[4]
    maps = root / "Assets/_DeadlyJenga/World/Maps/Resources/ModeMaps"
    names = sorted(
        (path.stem.removeprefix("Mode_") for path in maps.glob("Mode_*.prefab")),
        key=lambda name: (-len(name), name),
    )
    # Single-word map IDs need an explicit map marker to avoid matching ordinary prose.
    patterns = {}
    for name in names:
        label = name.replace("-", " ").title()
        phrase = "[- ]".join(re.escape(piece) for piece in name.split("-"))
        if "-" not in name:
            phrase = rf"(?:map\s+{phrase}|{phrase}\s+map)"
        patterns[label] = phrase
    patterns["Castle"] = r"castle map|map\s+castle|on (?:the )?castle(?![-\w])"
    return patterns


def unresolved_maps(parts, known):
    recognized = {entry["source"] for entry in known}
    explicit_map = re.compile(
        r"\b(?:map\s*[:=]\s*|(?:on|in|use|using|set it on)\s+(?:the\s+)?map\s+)"
        r"(?P<label>[^,.;!?]+)|\b(?P<named>[A-Z][\w'’-]+(?:\s+[A-Z][\w'’-]+)+)\s+map\b",
    )
    named_location = re.compile(
        r"\b(?:on|in|at|set on|set in)\s+(?:the\s+)?"
        r"(?P<label>[A-Z][\w'’-]+(?:\s+(?:(?:of|the|and)\s+)?[A-Z][\w'’-]+)+)"
    )
    results = []
    for part in parts:
        if part in recognized:
            continue
        match = explicit_map.search(part) or named_location.search(part)
        if match:
            label = match.group("label") or match.groupdict().get("named")
            results.append({"label": label.strip(), "intent": "exclude" if excluded(part, match) else "requested",
                            "source": part})
    return results


def one_choice(mentions, field, review):
    included = list(dict.fromkeys(entry["id"] for entry in mentions if entry["intent"] == "requested"))
    omitted = {entry["id"] for entry in mentions if entry["intent"] == "exclude"}
    if len(included) == 1 and included[0] not in omitted:
        return included[0]
    if included:
        review.append({"field": field, "reason": "Multiple or conflicting choices. Select the intended value.",
                       "choices": included})
    else:
        review.append({"field": field, "reason": "No supported positive choice was identified. Fill in if needed."})
    return None


def seconds(text):
    return WORD_SECONDS[text.lower()] if text.lower() in WORD_SECONDS else float(text)


def duration_request(parts, review):
    requests = []
    for part in parts:
        for match in DURATION.finditer(part):
            near = re.split(r"\b(?:with|when|while|then|and)\b|[,;]", part[:match.start()], flags=re.I)[-1]
            if re.search(r"\b(?:at|by|after|before|wait)\s+$", near, re.I):
                continue
            if re.search(r"\b(?:for|at|by|after|before|wait)\s+$", part[:match.start()], re.I):
                if not re.search(r"\b(?:trailer|cut|clip|teaser|spot|video)\b", part[:match.start()], re.I):
                    continue
            first = seconds(match["start"])
            last = seconds(match["end"]) if match["end"] else None
            if not math.isfinite(first) or (last is not None and not math.isfinite(last)):
                continue
            bound = match["bound"].strip().lower() if match["bound"] else None
            requests.append({"seconds": first, "end_seconds": last, "qualifier": bound, "source": part})
    unique = {(item["seconds"], item["end_seconds"], item["qualifier"]) for item in requests}
    if len(unique) == 1:
        first, last, bound = next(iter(unique))
        if first > 0 and last is None and bound in (None, "about", "around", "roughly", "approximately"):
            return first, requests
    review.append({"field": "duration_seconds",
                   "reason": "Choose the target duration within the requested constraints." if requests
                   else "No target duration was identified."})
    return None, requests


def craft_class(duration):
    if duration is None:
        return None
    if duration <= 10:
        return "moment"
    return min((15, 30, 60), key=lambda candidate: abs(duration - candidate))


def plausible_beats(parts):
    patterns = {
        "hook": r"open(?:ing)?|start|begin|cold[- ]open|first shot",
        "escalate": r"then|follow(?:ed)? by|build(?:s)?|escalat\w*|midway",
        "payoff": r"end|finish|final shot|payoff|climax|close out",
    }
    beats = []
    for part in parts:
        for role, pattern in patterns.items():
            if re.search(words(pattern), part, re.I):
                beats.append({"role": role, "direction": part})
                break
    return beats


def compile_brief(prompt: str) -> dict:
    """Return an editable brief. Extracted directions and editorial ideas stay separate."""
    if not prompt.strip():
        raise ValueError("Provide a trailer prompt.")
    parts = clauses(prompt)
    review = []
    hazards = find_mentions(HAZARDS, parts)
    features = find_mentions({key: value[0] for key, value in FEATURES.items()}, parts)
    camera = find_mentions(CAMERAS, parts)
    audio = find_mentions(AUDIO, parts)
    for entry in audio:
        if entry["id"] == "audible_audio":
            entry["intent"] = "exclude"
    duration, duration_options = duration_request(parts, review)

    # Explicit aspect instructions outrank formats inferred from a platform.
    aspect_mentions = find_mentions(ASPECTS, parts)
    preferred_aspect = find_mentions({
        "9:16": r"9\s*:\s*16|vertical|portrait",
        "16:9": ASPECTS["16:9"],
        "1:1": ASPECTS["1:1"],
    }, parts)
    aspect = one_choice(preferred_aspect or aspect_mentions, "aspect", review)
    platforms = find_mentions(PLATFORMS, parts)
    map_mentions = find_mentions(map_patterns(), parts)
    map_name = one_choice(map_mentions, "map", review)
    unknown_maps = unresolved_maps(parts, map_mentions)
    if unknown_maps:
        review.append({"field": "map", "reason": "Resolve the named location against the supported map catalog.",
                       "choices": [entry["label"] for entry in unknown_maps]})
        if any(entry["intent"] == "requested" for entry in unknown_maps):
            map_name = None
    modes = find_mentions(MODES, parts)
    mode = one_choice(modes, "mode_rules", review)
    active = {}
    for entry in [*hazards, *features]:
        active.setdefault(entry["id"], set()).add(entry["intent"])
    alternatives = set()
    for part in parts:
        options = {entry["id"] for entry in hazards if entry["source"] == part and entry["intent"] == "requested"}
        if len(options) > 1 and re.search(r"\bor\b", part, re.I):
            alternatives.update(options)
            review.append({"field": "must_show", "reason": "Choose among the requested hazard alternatives.",
                           "choices": sorted(options), "source": part})
    for key, intents in active.items():
        if len(intents) > 1:
            review.append({"field": "must_show", "reason": f"Resolve the requested and excluded mentions of {key}."})
    must_show = []
    for key, intents in active.items():
        if intents != {"requested"} or key in alternatives:
            continue
        if key in FEATURES:
            proof = FEATURES[key][1]
        else:
            proof = f"Show the {key.replace('_', ' ')} and its effect in a readable shot."
            review.append({
                "field": key,
                "reason": "Verify the hazard, its timing, and the requested interaction on the intended map in a scout.",
            })
        must_show.append({"id": key, "proof": proof})

    exclusions = [
        part for part in parts
        if any(entry["source"] == part and entry["intent"] == "exclude"
               for entry in [*hazards, *features, *camera, *audio, *map_mentions, *modes])
        or re.match(r"^(?:no|without|avoid|never|omit|skip|do not|don’t|don't)\b", part, re.I)
    ]
    recognized_sources = {entry["source"] for entry in [
        *hazards, *features, *camera, *audio, *map_mentions, *modes, *aspect_mentions, *platforms,
        *duration_options,
    ]}
    length_class = craft_class(duration)
    if length_class not in (None, "moment") and length_class != duration:
        review.append({"field": "length_class",
                       "reason": f"The draft uses the {length_class}-second craft class. Preserve the requested runtime in the plan."})
    if not must_show:
        review.append({"field": "must_show",
                       "reason": "Add the requested events and observable proof before planning shots."})

    return {
        "status": "draft",
        "goal": prompt.strip(),
        "owner_notes": parts,
        "audience": None,
        "duration_seconds": duration,
        "length_class": length_class,
        "aspect": aspect,
        "platform": ", ".join(dict.fromkeys(
            entry["id"] for entry in platforms if entry["intent"] == "requested"
        )) or None,
        "must_show": must_show,
        "repeat_sentence": None,
        "map": map_name,
        "mode_rules": mode,
        "performers": None,
        "budget": None,
        "not_featured": exclusions,
        "constraints": {
            "duration": duration_options,
            "aspect": aspect_mentions,
            "map": map_mentions,
            "unresolved_maps": unknown_maps,
            "mode": modes,
            "camera": camera,
            "audio_and_titles": audio,
            "hazards": hazards,
            "gameplay": features,
        },
        "requested_beats": plausible_beats(parts),
        "unclassified_notes": [part for part in parts if part not in recognized_sources],
        "review_items": review,
    }


def self_test():
    prompt = (
        "Make a twenty-second vertical trailer for TikTok. Open in first person on the castle. "
        "Show a survivor punch another off the tower. Then a twister hits and the tower crumbles. "
        "No giant flying hand and no background music. End on the survivor's reaction."
    )
    brief = compile_brief(prompt)
    assert brief == compile_brief(prompt)
    assert brief["duration_seconds"] == 20
    assert brief["length_class"] == 15
    assert brief["aspect"] == "9:16"
    assert brief["map"] == "Castle"
    assert brief["platform"] == "TikTok"
    assert {entry["id"] for entry in brief["must_show"]} >= {"pvp_hit", "tornado", "collapse"}
    assert all(entry["id"] != "hand" for entry in brief["must_show"])
    assert {"hook", "escalate", "payoff"} == {entry["role"] for entry in brief["requested_beats"]}
    assert brief["constraints"]["audio_and_titles"][0]["intent"] == "exclude"
    ambiguous = compile_brief("Under 15 seconds, square or vertical. Show the moon turning blue.")
    assert ambiguous["duration_seconds"] is None and ambiguous["aspect"] is None
    assert ambiguous["unclassified_notes"] == ["Show the moon turning blue."]
    conditional = compile_brief("No hand. Without meteors, show a tornado. 16:9 for TikTok.")
    assert conditional["aspect"] == "16:9"
    assert [entry["id"] for entry in conditional["must_show"]] == ["tornado"]
    range_brief = compile_brief("A 12-18 second teaser. Keep the planet-killer meteor in silhouette.")
    assert range_brief["duration_seconds"] is None
    assert range_brief["constraints"]["duration"][0]["end_seconds"] == 18
    assert [entry["id"] for entry in range_brief["must_show"]] == ["planet_killer_meteor"]
    alternatives = compile_brief("Never show the giant hand. Use a tornado or a tsunami.")
    assert alternatives["must_show"] == []
    assert any(item.get("choices") == ["tornado", "tsunami"] for item in alternatives["review_items"])
    maps = compile_brief("Start with a square shot on Castle Catwalks.")
    assert maps["map"] == "Castle Catwalks"
    bounded = compile_brief("No more than 15 seconds with a tornado. Make it silent.")
    assert [entry["id"] for entry in bounded["must_show"]] == ["tornado"]
    assert bounded["constraints"]["audio_and_titles"][0]["intent"] == "exclude"
    timing = compile_brief("A 15-second trailer with the first shot at 2 seconds.")
    assert timing["duration_seconds"] == 15
    for verb in ("Start", "Open", "Go", "End", "Cut"):
        for direction, expected in (("wide", "wide"), ("close", "close"), ("tight", "close")):
            result = compile_brief(f"{verb} {direction}.")
            assert [entry["id"] for entry in result["constraints"]["camera"]] == [expected]
    movement = compile_brief("Run the tool. The hero sprints and jumps while the rival chases.")
    assert [entry["id"] for entry in movement["must_show"]] == ["run", "jump", "chase"]
    assert movement["unclassified_notes"] == ["Run the tool."]
    unknown_map = compile_brief("Use map: Coastline Hotel. Then a tornado hits.")
    assert unknown_map["map"] is None
    assert unknown_map["constraints"]["unresolved_maps"][0]["label"] == "Coastline Hotel"


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("prompt", nargs="?", help="trailer request, quoted as one argument")
    parser.add_argument("--prompt-file", type=Path, help="UTF-8 prompt file (otherwise reads stdin)")
    parser.add_argument("-o", "--output", type=Path, help="editable brief JSON path (default: stdout)")
    parser.add_argument("--self-test", action="store_true", help="run deterministic compiler checks")
    args = parser.parse_args(argv)
    if args.self_test:
        self_test()
        print("Prompt compiler checks passed.")
        return
    if args.prompt is not None and args.prompt_file:
        parser.error("Use either a prompt argument or --prompt-file.")
    try:
        if args.prompt_file:
            prompt = args.prompt_file.read_text(encoding="utf-8-sig")
        elif args.prompt is not None:
            prompt = args.prompt
        else:
            prompt = sys.stdin.read()
        output = json.dumps(compile_brief(prompt), indent=2) + "\n"
        if args.output:
            args.output.write_text(output, encoding="utf-8")
        else:
            print(output, end="")
    except (OSError, ValueError) as error:
        parser.error(str(error))


if __name__ == "__main__":
    main()
