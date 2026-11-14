"""Compare *-timeline.jsonl files written by MultiplayerTimeline (DevTools).
usage: python diff_timelines.py <run folder> [--settle 0.25] [--angle 10]
Pairs each guest line with the host line nearest in wall-clock time and reports
how many settled blocks disagree by more than the tolerance."""
import json, math, sys, glob, os

def load(path):
    rows = []
    for raw in open(path, encoding="utf-8"):
        try: rows.append(json.loads(raw))
        except ValueError: pass
    return rows

def angle(a, b):
    dot = abs(sum(x * y for x, y in zip(a, b)))
    return math.degrees(2 * math.acos(min(1.0, dot)))

def main():
    folder = sys.argv[1]
    pos_tol = float(sys.argv[sys.argv.index("--settle") + 1]) if "--settle" in sys.argv else 0.25
    ang_tol = float(sys.argv[sys.argv.index("--angle") + 1]) if "--angle" in sys.argv else 10.0
    files = {os.path.basename(p)[:-15]: load(p) for p in glob.glob(os.path.join(folder, "*-timeline.jsonl"))}
    host = files.pop("host", None)
    if not host:
        print("no host timeline"); return 1
    worst = 0
    for role, rows in sorted(files.items()):
        checked = bad_lines = 0; max_bad = 0; disconnected = 0
        for row in rows:
            if not row["connected"]: disconnected += 1
            if row["scene"] == "Lobby" or row["waiting"] or not row["blocks"]: continue
            ref = min(host, key=lambda h: abs(h["utc"] - row["utc"]))
            if abs(ref["utc"] - row["utc"]) > 600 or len(ref["blocks"]) != len(row["blocks"]) or ref["scene"] != row["scene"]: continue
            bad = 0
            for a, b in zip(ref["blocks"], row["blocks"]):
                if a is None or b is None:
                    bad += (a is None) != (b is None); continue
                if a[8] or b[8]: continue  # moving blocks are allowed to lag
                d = math.dist(a[:3], b[:3])
                if d > pos_tol or angle(a[3:7], b[3:7]) > ang_tol: bad += 1
            checked += 1; max_bad = max(max_bad, bad); bad_lines += bad > 0
        worst = max(worst, max_bad)
        print(f"{role}: compared {checked} seconds, seconds with desync {bad_lines}, worst settled-block mismatch {max_bad}, disconnected seconds {disconnected}")
    return 0 if worst == 0 else 2

if __name__ == "__main__":
    sys.exit(main())
