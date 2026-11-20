"""Plan lints for a Deadly Jenga trailer plan (deadlyjenga-trailer-craft, revision 3).

Usage:
  python lint.py <plan.yaml>           one line per lint, then the count of hard failures
  python lint.py --help

Reads the plan schema in references/plan-schema-and-lints.md. Prints PASS, FAIL, WAIVED (a soft lint
with a `waive` entry), WAIVED-HARD (a hard lint with a waiver: report it to the human), N/A, or notes
DEFERRED (planning-only mode) and CHECK (the tool cannot judge it; a person or critic must).
Exit code 1 when an unwaived hard lint fails.

What it cannot check: whether a `shows` label, `learns` line, brief `proof` or `fit` line is TRUE of
the shot (L18 and L38 only check that the field is there), and every scout/render lint (R01-R07).
Started from hand-reveal-2's tools/lint.py (2026-11-20) and generalised: core verb by concept type,
the `moment` class, single-take moments, and the mayhem lints L40-L46. Needs PyYAML.
"""
import re
import statistics
import sys
from collections import Counter

if len(sys.argv) < 2 or sys.argv[1] in ("-h", "--help"):
    print(__doc__)
    sys.exit(0)

import yaml  # PyYAML

plan = yaml.safe_load(open(sys.argv[1], encoding="utf-8"))
shots = plan["shots"]
fps = plan.get("fps", 30)
L = plan["length_frames"]
cls = str(plan.get("class", "15"))
moment = cls == "moment"
ctype = plan.get("concept_type", "mayhem")
mode = plan.get("mode", "production")
shape = plan.get("shape", "montage")
hero = plan.get("hero")
cast = set(plan.get("cast", []))
threats = plan.get("threats", [])
waived = {}
for w in plan.get("waive", []) or []:
    waived.setdefault(w["lint"], w.get("reason", ""))
music = plan.get("music", {}) or {}
hits = {h["name"]: h for h in music.get("hits", []) or []}
out, hard_fail, hard_waived = [], [], []


def S(f):
    """Frame counts in the skill are written for 30 fps."""
    return f * fps / 30


def report(lint, ok, detail, hard=True, state=None):
    if state is None:
        if ok:
            state = "PASS"
        elif lint in waived:
            state = "WAIVED-HARD" if hard else "WAIVED"
        else:
            state = "FAIL"
    if state == "FAIL" and hard:
        hard_fail.append(lint)
    if state == "WAIVED-HARD":
        hard_waived.append(lint)
    extra = f"  [waiver: {waived[lint]}]" if state.startswith("WAIVED") else ""
    out.append(f"{lint} {'hard' if hard else 'soft'} {state}: {detail}{extra}")


def length(s):
    return s["cut_out"] - s["cut_in"]


def frame_of(s):
    ids = set(s.get("also_in_frame", []) or [])
    sub = s.get("subject", {}) or {}
    if sub.get("id"):
        ids.add(sub["id"])
    return ids


def gameplay(s):
    return s.get("kind", "gameplay") == "gameplay"


# ---------- derived terms ----------
closing = next((t for t in plan.get("titles", []) or [] if t.get("closing")), None)
body_end = closing["frame_in"] if closing else L
body = [s for s in shots if s["cut_in"] < body_end and gameplay(s)]
lengths = [min(s["cut_out"], body_end) - s["cut_in"] for s in body]
cuts = [s["cut_in"] for s in shots[1:]]
body_cuts = [c for c in cuts if 0 < c < body_end]

# Beats: shots, or the moments of a single take (a moment's `at` is a shot frame).
beats = []
for i, s in enumerate(shots):
    ms = s.get("moments") or []
    if ms:
        for j, m in enumerate(ms):
            nxt = ms[j + 1]["at"] if j + 1 < len(ms) else length(s)
            b = dict(m)
            b.update(shot=s, index=i, start=s["cut_in"] + m["at"], key=s["cut_in"] + m["at"], span=nxt - m["at"],
                     inframe=frame_of(s), moment=True)
            b.setdefault("shows", [])
            beats.append(b)
    else:
        b = dict(s)
        b.update(shot=s, index=i, start=s["cut_in"], key=s["cut_in"] + s.get("key_frame", 0), span=length(s),
                 inframe=frame_of(s), moment=False)
        b.setdefault("shows", [])
        beats.append(b)


def tag(b):
    return b["shot"]["id"] + (f"@{b['at']}" if b["moment"] else "")


VERBS = {"hand": {"pull"}, "two_role": {"pull"}, "disaster": {"hazard_hit"}, "pvp": {"pvp_hit"},
         "mayhem": {"hazard_hit", "pvp_hit"}}[ctype]
CAUSES = {"pull", "grip", "hazard_hit", "pvp_hit", "knock_off", "weapon"}
deadline = {"moment": 5.0, "15": 5.0, "30": 8.0, "60": 12.0}[cls]
SCALE = ("tower", "block", "castle", "crown", "stump", "wall")


def has_scale(b, verb):
    inf = b["inframe"]
    if verb == "pull":
        return (b["shot"].get("subject", {}).get("kind") == "block") or bool(inf & cast) or any(str(x).startswith("block") for x in inf)
    if verb == "hazard_hit":
        return bool(inf & cast) or any(str(x).startswith(SCALE) for x in inf)
    if verb == "pvp_hit":
        return b.get("attacker") in inf and b.get("victim") in inf
    return False


def threat_of(b):
    return b.get("threat") or (threats[0] if len(threats) == 1 else None)


def near_hit(f):
    return any(abs(f - h["frame"]) <= 2 for h in hits.values())


# ---------- hard plan lints ----------
ok = shots[0]["cut_in"] == 0 and shots[-1]["cut_out"] == L and all(a["cut_out"] == b["cut_in"] for a, b in zip(shots, shots[1:]))
report("L01", ok, " -> ".join(str(s["cut_in"]) for s in shots) + f" -> {shots[-1]['cut_out']} (length {L})")


def too_long(text):
    return text is None or len(str(text).split()) > 12 or " and " in f" {text} " or ";" in str(text)


bad = [s["id"] + ":subject" for s in shots if not (s.get("subject") or {}).get("kind")]
bad += [s["id"] for s in shots if too_long(s.get("learns"))]
bad += [tag(b) for b in beats if b["moment"] and too_long(b.get("what"))]
report("L02", not bad, f"missing subject, or learns/what over 12 words or with 'and': {bad or 'none'}")

seq = [b.get("section") for b in beats]
text = " ".join(seq) + " "
if moment:
    pattern = r"(hook )+(teach )*((escalate|breath) )*(payoff )+((aftermath|breath) )*(title )*"
else:
    pattern = r"(hook )+(teach )+((escalate|breath) )+(payoff )+((aftermath|breath) )*(title )+"
ok3 = re.fullmatch(pattern, text) is not None
if not moment and "payoff" in seq:
    ok3 = ok3 and "escalate" in seq[:seq.index("payoff")]
ok3 = ok3 and all(b.get("raises") for b in beats if b.get("section") == "escalate")
report("L03", ok3, f"sections: {' '.join(seq)}" + (" (moment: teach, escalate, title optional)" if moment else ""))

first_gp = min((s["cut_in"] for s in shots if gameplay(s)), default=10 ** 9)
report("L04", gameplay(shots[0]) and first_gp <= S(60), f"first shot {shots[0].get('kind', 'gameplay')}; first gameplay at frame {first_gp}")

verb_beats = [b for b in beats if VERBS & set(b["shows"])]
fv = verb_beats[0] if verb_beats else None
verb = sorted(VERBS & set(fv["shows"]))[0] if fv else None
if fv:
    t5 = fv["key"] / fps
    report("L05", t5 <= deadline + 1e-6 and has_scale(fv, verb),
           f"core verb {sorted(VERBS)}: first is {verb} at {tag(fv)}, key {t5:.2f} s (deadline {deadline} s); scale or both parties in frame: {has_scale(fv, verb)}")
else:
    report("L05", False, f"no shot shows the core verb {sorted(VERBS)}")

med = statistics.median(lengths) if lengths else 0
if shape == "single_take":
    take = max(lengths) if lengths else 0
    ok6 = take >= 0.7 * body_end and len(body_cuts) <= 2
    report("L06", ok6, f"single take: longest body shot {take} of body {body_end} = {take / max(body_end, 1):.0%}; cuts in body {body_cuts}")
elif moment:
    ok6 = S(24) <= med <= S(75) and max(lengths) >= S(45)
    report("L06", ok6, f"moment montage: lengths {lengths}, median {med}, longest {max(lengths)} (no barrage needed)")
else:
    floor = {"15": 60, "30": 90, "60": 99}[cls]
    window = max([sum(1 for c in body_cuts if a <= c < a + S(150)) for a in range(0, max(1, body_end))] or [0])
    ok6 = S(30) <= med <= S(66) and min(lengths) <= S(30) and max(lengths) >= S(floor) and window >= 4
    report("L06", ok6, f"montage: lengths {lengths}, median {med}, shortest {min(lengths)}, longest {max(lengths)} (floor {floor}), most cuts in a 5 s window {window}")

payoff_beat = next((b for b in beats if b.get("section") == "payoff"), None)
if moment:
    if closing:
        report("L07", payoff_beat is not None and closing["frame_in"] >= payoff_beat["key"], f"moment: optional title at {closing['frame_in']}, after the payoff key")
    else:
        report("L07", True, "moment: no closing title needed", state="N/A")
else:
    win = {"15": (330, 360, 90, 120), "30": (720, 780, 120, 180), "60": (1590, 1650, 150, 210)}[cls]
    if closing:
        fi, card = closing["frame_in"], L - closing["frame_in"]
        report("L07", S(win[0]) <= fi <= S(win[1]) and S(win[2]) <= card <= S(win[3]),
               f"closing title at {fi} (window {win[0]}-{win[1]}), title plus card {card} (window {win[2]}-{win[3]})")
    else:
        report("L07", False, "no closing title")

problems8 = []
for T in threats:
    R = next((b for b in beats if b.get("reveal") in ("whole", "payoff") and threat_of(b) == T), None)
    if R is None:
        continue
    if R["start"] == 0:
        if not (plan.get("opening") == "cold_open" and plan.get("cold_open_threat") == T):
            problems8.append(f"{T} fully shown at frame 0 without opening: cold_open")
        if length(R["shot"]) < S(30):
            problems8.append(f"{T}: cold-open shot under 30 frames")
        cons = {b.get("raises") for b in beats if b.get("section") == "escalate" and b.get("reveal") == "consequence"} - {None}
        if len(cons) < 2:
            problems8.append(f"{T}: cold open needs 2 consequence escalates with different raises, has {sorted(cons)}")
    else:
        antic = sum(b["span"] for b in beats if b["start"] < R["start"] and b.get("reveal") in ("knows", "effect", "partial", "reaction") and threat_of(b) == T)
        if antic < S(21):
            problems8.append(f"{T}: {antic} frames of anticipation before its first full view at {R['start']}")
for i, b in enumerate(beats):
    if b.get("reveal") in ("consequence", "payoff"):
        c = b.get("cause_in")
        if c == "prev":
            p = beats[i - 1] if i else None
            if not p or not (CAUSES & set(p["shows"]) or p.get("reveal") in ("whole", "payoff")):
                problems8.append(f"{tag(b)}: cause_in prev, but the previous beat shows no cause")
        elif c != "self":
            problems8.append(f"{tag(b)}: consequence or payoff without cause_in")
report("L08", not problems8, "; ".join(problems8) or f"threats {threats}: anticipation or declared cold open ok; every effect has its cause")

if fv:
    s = fv["shot"]
    if fv["moment"]:
        a = fv["key"] - s["cut_in"]
        ok9 = a >= S(12) and length(s) - a >= S(12)
        d9 = f"{tag(fv)}: {a} frames into a {length(s)}-frame take"
    else:
        n, k = length(s), s.get("key_frame", 0)
        ok9 = n >= S(45) and k >= S(12) and n - k >= S(12)
        d9 = f"{s['id']}: {n} frames, key {k}/{n - k} from the ends"
    report("L09", ok9 and has_scale(fv, verb), d9 + f"; scale or both parties in frame: {has_scale(fv, verb)}")
else:
    report("L09", False, "no core-verb shot")

bad10 = []
for s in shots:
    if s.get("moments") or not gameplay(s):
        continue
    k, n = s.get("key_frame", 0), length(s)
    if not ((k >= S(6) and n - k >= S(6)) or (k <= S(4) and near_hit(s["cut_in"]))):
        bad10.append(f"{s['id']} {k}/{n - k}")
report("L10", not bad10, f"keys too near a cut: {bad10 or 'none'}")

unmeasured = [h["name"] for h in hits.values() if h.get("measured_onset_frame") in (None, "unmeasured")]
meas_ok = not unmeasured and all(abs(float(h["measured_onset_frame"]) - h["frame"]) <= 2 for h in hits.values())
pay_ok = payoff_beat is not None and "main" in hits and abs(payoff_beat["key"] - hits["main"]["frame"]) <= 2
title_ok = (closing is None and moment) or (closing is not None and "title" in hits and abs(closing["frame_in"] - hits["title"]["frame"]) <= 2)
sync_bad = []
for s in shots:
    for snd in s.get("sound", []) or []:
        if snd.get("sync"):
            h = hits.get(snd["sync"])
            if not h or abs(s["cut_in"] + snd["at"] - h["frame"]) > 2:
                sync_bad.append(f"{s['id']}:{snd['sync']}")
d11 = (f"payoff key {payoff_beat['key'] if payoff_beat else None} vs main {hits.get('main', {}).get('frame')}; "
       f"title clause {'n/a (moment without a title)' if closing is None and moment else title_ok}; sync sounds off their hit: {sync_bad or 'none'}")
if mode == "planning_only" and unmeasured:
    report("L11", pay_ok and title_ok and not sync_bad, d11 + f"; measured-onset clause DEFERRED ({len(unmeasured)} hits unmeasured)")
else:
    report("L11", meas_ok and pay_ok and title_ok and not sync_bad, d11 + f"; every hit measured within 2 frames: {meas_ok}")

bad12 = []
for b in beats:
    s = b["shot"]
    if not gameplay(s) and not s.get("sound"):
        continue
    if b["moment"]:
        sa = b.get("sound_at")
        if sa is None or not (b["at"] - 1 <= sa <= b["at"] + 3):
            bad12.append(tag(b))
    else:
        k = s.get("key_frame", 0)
        if not any(k - 1 <= snd["at"] <= k + 3 for snd in s.get("sound", []) or [] if not snd.get("lead_in")):
            bad12.append(s["id"])
report("L12", not bad12, f"keys without a sound in [key-1, key+3]: {bad12 or 'none'}")

ramp_bad = [s["id"] for s in shots if any(sp.get("speed", 1) != 1 for sp in s.get("speed", []) or [])
            and any(snd.get("source") == "take" for snd in s.get("sound", []) or [])]
report("L13", (plan.get("studio", {}) or {}).get("ramp_sound") is True or not ramp_bad, f"ramped shots with take sound: {ramp_bad or 'none'}")

close = [s["id"] for s in shots if s.get("size") in ("MCU", "CU", "ECU", "INSERT")]


def face_ok(s):
    side = s.get("side") or {}
    yaw = side.get("yaw_deg")
    return ((s.get("subject") or {}).get("id") == hero and side.get("face") in ("front", "3q_front") and yaw is not None
            and abs(yaw) <= 60 and s.get("size") in ("MS", "MCU", "CU", "ECU"))


faces = [s["id"] for s in shots if face_ok(s)]
reacting = [s["id"] for s in shots if face_ok(s) and s.get("reaction")]
report("L14", bool(close) and bool(reacting), f"close shots {close}; hero head-on shots {faces}; of those with a `reaction` (layer, emote or performed pose) {reacting}")

LIB = {"static", "push_in", "pull_out", "pan", "tilt_up", "tilt_down", "truck", "crane_up", "crane_down", "orbit", "follow",
       "ride", "handheld", "whip", "crash_zoom", "dolly_zoom", "pov", "fall_with"}
NO_EASE = {"static", "whip", "crash_zoom", "handheld", "pov"}
bad15 = []
for s in shots:
    cam = s.get("camera") or {}
    if not cam and not gameplay(s):
        continue
    mv = cam.get("move")
    ease = cam.get("ease", "none" if mv in NO_EASE else None)
    if mv not in LIB or not cam.get("purpose") or ease is None or (ease == "none" and mv not in NO_EASE):
        bad15.append(s["id"])
report("L15", not bad15, f"bad camera declarations: {bad15 or 'none'}")

bad16 = []
for a, b in zip(shots, shots[1:]):
    ca, cb = (a.get("camera") or {}).get("move"), (b.get("camera") or {}).get("move")
    ya, yb = (a.get("side") or {}).get("yaw_deg"), (b.get("side") or {}).get("yaw_deg")
    if "pov" in (ca, cb) or ya is None or yb is None or b.get("jump_cut") == "deliberate":
        continue
    if (a.get("subject") or {}).get("id") == (b.get("subject") or {}).get("id") and a.get("size") == b.get("size") and a.get("angle") == b.get("angle") and abs(ya - yb) < 30:
        bad16.append(a["id"] + ">" + b["id"])
report("L16", not bad16, f"jump cuts: {bad16 or 'none'}")


def smooth_ok(s):
    p = (s.get("camera") or {}).get("pov_smoothing") or {}
    return 0.2 <= p.get("pos_s", 0) <= 0.4 and 0.3 <= p.get("rot_s", 0) <= 0.6


bad17 = [s["id"] for s in shots if (s.get("camera") or {}).get("move") == "pov" and not smooth_ok(s)] + [s["id"] for s in shots if s.get("raw_hmd")]
report("L17", not bad17, f"unsmoothed POV: {bad17 or 'none'}")

covered = {m: sorted({tag(b) for b in beats if m in b["shows"]}) for m in plan.get("must_show", [])}
report("L18", all(covered.values()), f"must-show coverage {covered} (whether each proof is true of its shot: CHECK)")

CANNOT = {"sea_drawback", "drawn_face", "live_action_headset", "studio_sound_cue", "mode_objective_in_studio", "punch_cue"}
needs = [(s["id"], n) for s in shots for n in s.get("needs", []) or []]
bad19 = [f"{sid}:{n['feature']}" for sid, n in needs if n.get("status") == "missing" or (n["feature"] in CANNOT and n.get("status") == "available")]
verify = sorted({n["feature"] for _, n in needs if n.get("status") == "verify"})
report("L19", not bad19, f"missing or impossible: {bad19 or 'none'}; verify at scout: {verify or 'none'}")

if ctype == "two_role":
    half = body_end / 2
    ok36 = any(b.get("role_view") == "hand" and b["start"] < half for b in beats) and any(b.get("role_view") == "survivor" and b["start"] < half for b in beats)
    report("L36", ok36, f"both roles before frame {half:.0f}")
else:
    report("L36", True, "two_role only", state="N/A")

no_fit = [s["id"] for s in shots if gameplay(s) and "Hf" not in str(s.get("fit", ""))]
report("L38", not no_fit, f"gameplay shots without an Hf fit line: {no_fit or 'none'} (whether each fit is right: CHECK)")

RESERVED_IDS = {"mint", "rook", "coral", "finn", "ivo", "plum", "gus", "tam", "rin", "jolt", "brick", "pip", "dune",
                "vela", "kestrel", "okra", "tansy", "sprout"}
RESERVED_TEXT = {"don't be the one who falls.", "the tower was the easy part.", "one friend pulls. everyone else climbs.",
                 "the hand is another player", "stay on the tower", "your friend plays the hand",
                 "shove friends. dodge disasters.", "your friends are a disaster too.",
                 "weapons. disasters. one tower.", "survive the sky. then survive your friends."}
if plan.get("example"):
    report("L39", True, "a worked example itself", state="N/A")
else:
    texts = [t["text"].strip().lower() for t in plan.get("titles", []) or []]
    clash = sorted(({hero} | cast) & RESERVED_IDS) + [t for t in texts if t in RESERVED_TEXT]
    report("L39", not clash, f"reserved example ids or text used: {clash or 'none'}")

if ctype == "mayhem":
    lim = 0.6 * body_end
    first = {v: next((b["key"] for b in beats if v in b["shows"]), None) for v in ("hazard_hit", "pvp_hit")}
    report("L40", all(f is not None and f <= lim for f in first.values()),
           f"first hazard_hit at {first['hazard_hit']}, first pvp_hit at {first['pvp_hit']}; both needed by frame {lim:.0f} (60 % of the body)")
else:
    report("L40", True, "mayhem only", state="N/A")

bad41 = []
for i, b in enumerate(beats):
    if {"pvp_hit", "knock_off", "elimination"} & set(b["shows"]):
        att, vic = b.get("attacker"), b.get("victim")
        if not att or not vic:
            bad41.append(tag(b) + ": no attacker or victim")
            continue
        inf = b["inframe"]
        prev_inf = beats[i - 1]["inframe"] if i else set()
        att_ok = att in inf or (b.get("cause_in") == "prev" and att in prev_inf)
        if str(att).startswith("hazard:") and att in threats and b.get("cause_in") == "self":
            att_ok = True
        if vic not in inf or not att_ok:
            bad41.append(f"{tag(b)}: {att} -> {vic} not both readable (attacker in frame, or in the shot before)")
report("L41", not bad41, f"who did what to whom: {bad41 or 'every hit names its attacker and victim, both readable'}")


def hero_in(sections):
    return any(b.get("section") in sections and hero in b["inframe"] for b in beats)


need = [("hook/teach", {"hook", "teach"}), ("payoff/aftermath", {"payoff", "aftermath"})]
if any(b.get("section") == "escalate" for b in beats):
    need.insert(1, ("escalate", {"escalate"}))
miss42 = [n for n, secs in need if not hero_in(secs)]
report("L42", not miss42, f"hero {hero} in frame through the cut; missing from: {miss42 or 'none'}")

# ---------- soft lints ----------
hazards = [t for t in threats if str(t).startswith("hazard:")]
if len(hazards) >= 2:
    firsts = [(T, next((b for b in beats if threat_of(b) == T), None)) for T in hazards]
    starts = [b["start"] for _, b in firsts if b]
    ok43 = (all(b is not None for _, b in firsts) and starts == sorted(starts) and len(set(starts)) == len(starts)
            and all(b.get("section") in ("escalate", "payoff") for _, b in firsts[1:] if b))
    report("L43", ok43, "hazards by first appearance: " + ", ".join(f"{T}@{b['start'] if b else None} ({b.get('section') if b else '-'})" for T, b in firsts), hard=False)
else:
    report("L43", True, "fewer than two hazards", state="N/A", hard=False)

est = [s["id"] for s in shots if s["cut_in"] < body_end / 3 and s.get("size") in ("EWS", "WS")
       and (any(str(x).startswith(SCALE) for x in frame_of(s)) or (s.get("subject") or {}).get("kind") == "tower") and hero in frame_of(s)]
no_level = [s["id"] for s in shots if gameplay(s) and not s.get("tower_level")]
report("L44", bool(est) and not no_level, f"establishing wides with structure and hero in the first third: {est or 'none'}; shots without tower_level: {no_level or 'none'}", hard=False)

bad45 = []
for s in shots:
    shows = set(s.get("shows", []) or []) | {x for m in s.get("moments") or [] for x in m.get("shows", []) or []}
    if {"multiplayer", "several_survivors"} & shows and (s.get("readable_survivors") or 0) < 3:
        bad45.append(s["id"])
report("L45", not bad45, f"crowd shots with fewer than 3 survivors at h >= 0.08: {bad45 or 'none'}", hard=False)

bad46 = []
for i, b in enumerate(beats):
    if {"knock_off", "elimination"} & set(b["shows"]):
        after = b["span"] if b["moment"] else length(b["shot"]) - b["shot"].get("key_frame", 0)
        nxt = beats[i + 1] if i + 1 < len(beats) else None
        shown_next = nxt is not None and nxt.get("cause_in") == "prev" and b.get("victim") in nxt["inframe"]
        if after < S(18) and not shown_next:
            bad46.append(f"{tag(b)}: {after} frames after the hit")
report("L46", not bad46, f"knock-offs held 0.6 s or picked up by the next shot: {bad46 or 'all'}", hard=False)

montage = shape == "montage" and not moment
bad20 = [t["text"] for t in plan.get("titles", []) or [] if t["frame_out"] - t["frame_in"] < fps * (0.3 * len(t["text"].split()) + 1.0)]
report("L20", not bad20, f"text too short on screen: {bad20 or 'none'}", hard=False)

if montage:
    esc = [length(s) for s in body if s["section"] == "escalate"]
    ht = [length(s) for s in body if s["section"] in ("hook", "teach")]
    report("L21", bool(esc) and bool(ht) and statistics.median(esc) <= statistics.median(ht),
           f"escalate median {statistics.median(esc) if esc else None} vs hook+teach median {statistics.median(ht) if ht else None}", hard=False)
    cv = statistics.pstdev(lengths) / statistics.mean(lengths)
    report("L22", cv >= .35, f"CV {cv:.2f}", hard=False)
    run, best = 1, 1
    for a, b in zip(lengths, lengths[1:]):
        run = run + 1 if abs(a - b) <= 2 else 1
        best = max(best, run)
    report("L23", best <= 3, f"longest even run {best}", hard=False)
    if music.get("bpm"):
        beat = fps * 60 / music["bpm"]
        down = music.get("downbeat_frame", 0)
        on = [c for c in cuts if abs((c - down) / beat - round((c - down) / beat)) * beat <= 1]
        report("L24", len(on) <= .8 * len(cuts), f"cuts within 1 frame of a beat: {len(on)} of {len(cuts)} {on}", hard=False)
    else:
        report("L24", True, "no bpm", state="N/A", hard=False)
else:
    for lint in ("L21", "L22", "L23", "L24"):
        report(lint, True, "montage (15/30/60) only", state="N/A", hard=False)

hc = [b.get("hook_count") for b in beats if b.get("hook_count")]
if moment:
    report("L25", True, "moment class", state="N/A", hard=False)
else:
    report("L25", bool(hc) and max(hc) in (2, 3), f"hook counts {hc}", hard=False)

bad26 = []
for a, b in zip(shots, shots[1:]):
    ca, cb = a.get("connects_from_prev") or {}, b.get("connects_from_prev") or {}
    da, db = ca.get("screen_direction"), cb.get("screen_direction")
    if cb.get("type") in ("cause_effect", "match_motion") and {da, db} <= {"L>R", "R>L"} and da != db:
        bad26.append(a["id"] + ">" + b["id"])
report("L26", not bad26, f"direction flips across cause-effect cuts: {bad26 or 'none'}", hard=False)

CELLS = {c: (i // 3, i % 3) for i, c in enumerate(["TL", "TC", "TR", "ML", "MC", "MR", "BL", "BC", "BR"])}
bad27 = []
for a, b in zip(shots, shots[1:]):
    ex, en = (a.get("connects_from_prev") or {}).get("eye_exit"), (b.get("connects_from_prev") or {}).get("eye_enter")
    if (b.get("connects_from_prev") or {}).get("type") == "new_beat" or b.get("transition_in") == "smash_cut" or ex not in CELLS or en not in CELLS:
        continue
    if max(abs(CELLS[ex][0] - CELLS[en][0]), abs(CELLS[ex][1] - CELLS[en][1])) > 1:
        bad27.append(f"{a['id']}>{b['id']} {ex}>{en}")
report("L27", not bad27, f"eye-trace jumps: {bad27 or 'none'}", hard=False)

if "main" in hits:
    frac = 0.5 if moment else 0.6
    report("L28", hits["main"]["frame"] >= frac * body_end, f"main hit {hits['main']['frame']} vs {frac} x {body_end} = {frac * body_end:.0f}", hard=False)
else:
    report("L28", False, "no main hit", hard=False)

COARSE = {"EWS": "EW", "WS": "W", "FS": "W", "MS": "M", "MCU": "M", "CU": "C", "ECU": "C", "INSERT": "C"}
if body:
    w = sum(1 for s in body if COARSE.get(s.get("size")) in ("W", "EW")) / len(body)
    c = sum(1 for s in body if COARSE.get(s.get("size")) == "C") / len(body)
    climit = .25 if cls in ("moment", "15") else .15
    report("L29", w >= .4 and c <= climit, f"wide share {w:.0%}, close share {c:.0%} (limit {climit:.0%})", hard=False)

DESIGNED = {"push_in", "pull_out", "pan", "tilt_up", "tilt_down", "truck", "crane_up", "crane_down", "orbit", "follow", "dolly_zoom"}
if montage:
    dm = sum(1 for s in shots if (s.get("camera") or {}).get("move") in DESIGNED) / len(shots)
    report("L30", .1 <= dm <= .3, f"designed moves {dm:.0%} of shots", hard=False)
else:
    report("L30", True, "montage (15/30/60) only", state="N/A", hard=False)

if cls in ("30", "60"):
    sd = [x["to"] - x["from"] for x in music.get("stopdowns", []) or []]
    report("L31", any(d >= S(9) for d in sd), f"stop-downs {sd}", hard=False)
else:
    report("L31", True, "class 30 and 60 only", state="N/A", hard=False)

SUPER = {"epic", "insane", "ultimate", "best", "greatest", "ever"}
bad32 = []
for t in plan.get("titles", []) or []:
    n = len(t["text"].split())
    lim = {"caption": 6, "tagline": 8, "cta": 6}.get(t.get("role"))
    if lim and n > lim:
        bad32.append(f"{t['role']} {n} words")
    if SUPER & {w.strip(".,!?").lower() for w in t["text"].split()}:
        bad32.append(f"superlative in '{t['text']}'")
caps = sum(1 for t in plan.get("titles", []) or [] if t.get("role") == "caption")
report("L32", not bad32, f"captions {caps}; text problems {bad32 or 'none'} (rule-of-play wording: CHECK)", hard=False)

if montage:
    bad33 = [a["id"] for a, b in zip(body, body[1:]) if length(a) < S(24)
             and not (any(0 <= snd["at"] <= 2 for snd in b.get("sound", []) or []) or near_hit(b["cut_in"]))]
    report("L33", not bad33, f"short shots whose next cut has no sound or hit: {bad33 or 'none'}", hard=False)
else:
    report("L33", True, "montage (15/30/60) only", state="N/A", hard=False)

cnt = Counter(x for b in beats for x in set(b["shows"]) if x not in VERBS)
report("L34", all(v <= 3 for v in cnt.values()), f"shows counts, core verb excluded: {dict(cnt)}", hard=False)

MIN = {"EWS": 15, "WS": 15, "FS": 12, "MS": 12, "MCU": 12, "CU": 9, "ECU": 9, "INSERT": 9}
bad35, pov_held = [], False
for s in body:
    pov = (s.get("camera") or {}).get("move") == "pov"
    need_len = S(30) if pov and not pov_held else S(MIN.get(s.get("size"), 12))
    if length(s) < need_len:
        bad35.append(s["id"])
    if pov and length(s) >= S(30):
        pov_held = True
report("L35", not bad35, f"too short to read: {bad35 or 'none'}", hard=False)

if ctype == "two_role":
    ev = Counter(b.get("same_event") for b in beats if b.get("same_event"))
    report("L37", any(v >= 2 for v in ev.values()), f"same_event tags {dict(ev)}", hard=False)
else:
    report("L37", True, "two_role only", state="N/A", hard=False)

print("\n".join(sorted(out, key=lambda line: int(line[1:3]))))
print(f"\nHard failures: {len(hard_fail)} {hard_fail}; hard lints waived (report to the human): {hard_waived or 'none'}")
print("Not checked here: R01-R07 (scout and render), and whether labels, proofs and fit lines are true.")
sys.exit(1 if hard_fail else 0)
