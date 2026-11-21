"""Launch N Deadly Jenga player processes (1 host + N-1 guests) and collect their reports.
usage: python run_gate.py <player exe> <players> <hold seconds> [--out <folder>] [--windowed] [--match --bots N]
Each process writes <out>/<role>.txt and <out>/<role>-timeline.jsonl (MultiplayerTimeline).
Prints one line per process and exits non-zero if any process failed."""
import os, subprocess, sys, time, datetime

def main():
    exe, players, hold = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
    out = sys.argv[sys.argv.index("--out") + 1] if "--out" in sys.argv else os.path.join(
        os.path.dirname(os.path.abspath(__file__)), "..", "..", "Logs", "MPGate",
        datetime.datetime.now().strftime("%Y%m%d-%H%M%S"))
    out = os.path.abspath(out)
    os.makedirs(out, exist_ok=True)
    extra = ["--dj-match", f"--dj-match-bots={sys.argv[sys.argv.index('--bots') + 1]}"] if "--match" in sys.argv else []
    roles = ["host"] + [f"guest{i}" for i in range(1, players)]
    procs = {}
    for role in roles:
        args = [exe, "-screen-width", "640", "-screen-height", "360", "-window-mode", "windowed",
                f"--dj-smoke={role}", f"--dj-smoke-dir={out}", f"--dj-timeline={out}",
                "--dj-scale", f"--dj-scale-players={players}", f"--dj-scale-seconds={hold}"] + extra
        procs[role] = subprocess.Popen(args)
        time.sleep(2)  # stagger starts so the host can publish its code first
    failed = []
    for role, proc in procs.items():
        try:
            code = proc.wait(timeout=hold + 900)
        except subprocess.TimeoutExpired:
            proc.kill(); code = "killed"
        report = os.path.join(out, role + ".txt")
        lines = open(report, encoding="utf-8", errors="replace").read().splitlines() if os.path.exists(report) else []
        summary = [l for l in lines if l.startswith(("SCALE", "COMPLETE", "FAILED", "FAIL "))]
        print(f"{role}: exit={code} " + " | ".join(summary[-3:]))
        if code != 0: failed.append(role)
    print(f"RESULT players={players} failed={len(failed)} out={out}")
    return 1 if failed else 0

if __name__ == "__main__":
    sys.exit(main())
