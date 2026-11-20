"""Intake step 6b: the coverage status file read by the coverage map page.

config/coverage_status.json holds one entry per shot-list entry id (or per
building id when there is no shot list yet):

  status        not_started | captured | aligned | needs_reshoot
  photo_count, usable_count, flagged_count   totals over all sets
  sets          {set name: {photos, usable, flagged, last_capture}}
  last_capture  newest capture date of any set

Each intake run replaces only its own set's numbers and keeps every other
set (merge, never clobber). "aligned" is written later by the alignment
stage and is kept here.

Status rule:
  no photos                                   -> not_started
  more than 40% of the photos flagged         -> needs_reshoot
  at least min_photos (default 12) usable     -> captured
  some photos, but too few usable             -> needs_reshoot (reason says so)
"""
import datetime
import json
import os
import time
from pathlib import Path

RESHOOT_SHARE = 0.40


def status_for(photos, usable, flagged, min_photos):
    if photos == 0:
        return "not_started", None
    if flagged / photos > RESHOOT_SHARE:
        return "needs_reshoot", f"{flagged} of {photos} photos flagged (> {int(RESHOOT_SHARE * 100)}%)"
    if usable >= min_photos:
        return "captured", None
    return "needs_reshoot", f"only {usable} usable photos (needs {min_photos})"


def _read(path):
    path = Path(path)
    if not path.exists():
        return {"version": 1, "entries": {}}
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def _write_atomic(path, data):
    path = Path(path)
    tmp = path.with_name(path.name + f".tmp{os.getpid()}")
    with open(tmp, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2)
        f.write("\n")
    os.replace(tmp, path)


class _Lock:
    """A tiny lock file so two intake runs do not write the file at the same time."""

    def __init__(self, path):
        self.path = Path(str(path) + ".lock")

    def __enter__(self):
        for _ in range(600):
            try:
                self.fd = os.open(self.path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
                return self
            except FileExistsError:
                try:
                    if time.time() - self.path.stat().st_mtime > 600:
                        self.path.unlink(missing_ok=True)   # left over from a crash
                except FileNotFoundError:
                    pass
                time.sleep(0.1)
        raise TimeoutError(f"{self.path} is locked")

    def __exit__(self, *exc):
        os.close(self.fd)
        self.path.unlink(missing_ok=True)


def update(path, set_name, counts, all_targets=None, source=None):
    """Merge one set's counts into the coverage file.

    counts: {target id: {"photos", "usable", "flagged", "last_capture", "info": {...}}}
    all_targets: shot-list targets (info dicts) to list as not_started when absent.
    Returns the merged data."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with _Lock(path):
        data = _read(path)
        entries = data.setdefault("entries", {})
        # Drop this set's old numbers everywhere (photos may have moved between areas).
        for e in entries.values():
            e.get("sets", {}).pop(set_name, None)
        for tid, c in counts.items():
            e = entries.setdefault(tid, {"status": "not_started", "sets": {}})
            info = c.get("info") or {}
            for k in ("kind", "shot_kind", "building", "facade", "priority", "name", "min_photos"):
                if info.get(k) is not None:
                    e[k] = info[k]
            e.setdefault("sets", {})[set_name] = {"photos": c["photos"], "usable": c["usable"],
                                                 "flagged": c["flagged"], "last_capture": c.get("last_capture")}
        for info in all_targets or []:
            e = entries.setdefault(info["id"], {"status": "not_started", "sets": {}})
            for k in ("kind", "shot_kind", "building", "facade", "priority", "name", "min_photos"):
                if info.get(k) is not None and k not in e:
                    e[k] = info[k]
        for tid, e in entries.items():
            sets = e.get("sets", {})
            photos = sum(s["photos"] for s in sets.values())
            usable = sum(s["usable"] for s in sets.values())
            flagged = sum(s["flagged"] for s in sets.values())
            dates = [s["last_capture"] for s in sets.values() if s.get("last_capture")]
            e["photo_count"], e["usable_count"], e["flagged_count"] = photos, usable, flagged
            e["last_capture"] = max(dates) if dates else None
            if e.get("status") == "aligned":
                continue                                # set by the alignment stage; keep it
            e["status"], reason = status_for(photos, usable, flagged, int(e.get("min_photos") or 12))
            if reason:
                e["reason"] = reason
            else:
                e.pop("reason", None)
        data["version"] = 1
        data["updated"] = datetime.datetime.now().isoformat(timespec="seconds")
        data["rule"] = ("captured: >= min_photos (default 12) usable photos; needs_reshoot: photos exist "
                        "but > 40% flagged, or too few usable; aligned: set by the alignment stage")
        if source:
            data.setdefault("target_sources", {})[set_name] = source
        data["entries"] = dict(sorted(entries.items()))
        _write_atomic(path, data)
    return data
