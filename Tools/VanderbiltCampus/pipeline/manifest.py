"""The archive manifest: one row per file in data_root (STANDARDS.md section 8).

Commands:
  add PATH... --source S --license L --stage N [--date D]   record files (or folders)
  list [--prefix raw/lidar]                                   show recorded files
  verify [--prefix raw/]                                      re-hash; report missing or changed

Files under raw/ are write-once: adding a raw/ path whose recorded hash differs
is refused. Other scripts call add_file() directly.
"""
import argparse
import datetime
import hashlib
import sqlite3
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import data_root, fail  # noqa: E402

SCHEMA = """
CREATE TABLE IF NOT EXISTS files (
  path TEXT PRIMARY KEY,      -- relative to data_root, forward slashes
  sha256 TEXT NOT NULL,
  size INTEGER NOT NULL,
  source TEXT,                -- where it came from (URL, device, script)
  date TEXT,                  -- when it was made or downloaded
  stage INTEGER,              -- pipeline stage that wrote it
  license TEXT,
  added TEXT NOT NULL
)"""


def connect(root=None):
    root = Path(root or data_root())
    root.mkdir(parents=True, exist_ok=True)
    db = sqlite3.connect(root / "manifest.sqlite", timeout=120)  # other scripts may be writing
    db.execute(SCHEMA)
    return db


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def rel(path, root):
    return Path(path).resolve().relative_to(Path(root).resolve()).as_posix()


def add_file(path, source="", license="", stage=None, date=None, root=None, db=None):
    """Record one file. Returns its sha256. Refuses to change a recorded raw/ file."""
    root = Path(root or data_root())
    own = db is None
    db = db or connect(root)
    r = rel(path, root)
    digest = sha256(path)
    old = db.execute("SELECT sha256 FROM files WHERE path=?", (r,)).fetchone()
    if old and old[0] != digest and r.startswith("raw/"):
        fail(f"{r} is in raw/ and its content changed. raw/ is write-once; add a new file instead.")
    now = datetime.datetime.now().isoformat(timespec="seconds")
    db.execute("INSERT OR REPLACE INTO files VALUES (?,?,?,?,?,?,?,?)",
               (r, digest, Path(path).stat().st_size, source, date or now[:10], stage, license, now))
    if own:
        db.commit()
        db.close()
    return digest


def verify(prefix="", root=None):
    """Re-hash every recorded file. Returns (ok_count, problems)."""
    root = Path(root or data_root())
    db = connect(root)
    problems, ok = [], 0
    for path, digest, size in db.execute(
            "SELECT path, sha256, size FROM files WHERE path LIKE ? ORDER BY path", (prefix + "%",)):
        full = root / path
        if not full.exists():
            problems.append(("missing", path))
        elif full.stat().st_size != size or sha256(full) != digest:
            problems.append(("changed", path))
        else:
            ok += 1
    db.close()
    return ok, problems


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)
    a = sub.add_parser("add", help="record files or folders")
    a.add_argument("paths", nargs="+")
    a.add_argument("--source", default="")
    a.add_argument("--license", default="")
    a.add_argument("--stage", type=int)
    a.add_argument("--date")
    ls = sub.add_parser("list", help="list recorded files")
    ls.add_argument("--prefix", default="")
    v = sub.add_parser("verify", help="re-hash and report missing/changed files")
    v.add_argument("--prefix", default="")
    args = p.parse_args(argv)
    root = data_root()

    if args.cmd == "add":
        db = connect(root)
        n = 0
        for given in args.paths:
            given = Path(given)
            files = [f for f in given.rglob("*") if f.is_file()] if given.is_dir() else [given]
            for f in files:
                add_file(f, args.source, args.license, args.stage, args.date, root, db)
                n += 1
        db.commit()
        db.close()
        print(f"added {n} file(s)")
    elif args.cmd == "list":
        db = connect(root)
        for row in db.execute("SELECT path, size, source, license FROM files WHERE path LIKE ? ORDER BY path",
                              (args.prefix + "%",)):
            print("\t".join(str(x) for x in row))
        db.close()
    elif args.cmd == "verify":
        ok, problems = verify(args.prefix, root)
        for kind, path in problems:
            print(f"{kind}: {path}")
        print(f"verify: {ok} ok, {len(problems)} problem(s)")
        if problems:
            sys.exit(1)


if __name__ == "__main__":
    main()
