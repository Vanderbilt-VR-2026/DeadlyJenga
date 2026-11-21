"""Small download helpers shared by the fetch_*.py scripts.

- A polite, generic User-Agent (no personal details).
- Retries with growing waits when a server is busy.
- download_once(): stream a URL into raw/, record it in the manifest, and skip it
  next time if the file is already there with the recorded hash (idempotent).
"""
import datetime
import sys
import time
from pathlib import Path

import requests

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import data_root, fail  # noqa: E402
import manifest  # noqa: E402

USER_AGENT = "campus-map-pipeline/0.1 (student game project; python-requests)"
HEADERS = {"User-Agent": USER_AGENT}


def today():
    return datetime.date.today().isoformat()


def get(url, params=None, data=None, tries=5, wait=10, timeout=300, stream=False):
    """GET (or POST when data is given) with retries. Returns the response or exits."""
    last = None
    attempt, busy = 0, 0
    while attempt < tries:
        try:
            if data is not None:
                r = requests.post(url, data=data, headers=HEADERS, timeout=timeout, stream=stream)
            else:
                r = requests.get(url, params=params, headers=HEADERS, timeout=timeout, stream=stream)
            if r.status_code == 200:
                return r
            last = f"HTTP {r.status_code}"
            if r.status_code in (400, 401, 403, 404):
                last += f": {r.text[:200]!r}"
                break                                   # retrying will not help
            if r.status_code == 429 and busy < 30:
                # "Too many requests": the server wants us to slow down. Wait and
                # try again without using up a normal retry (up to 30 times).
                busy += 1
                pause = int(r.headers.get("Retry-After", "0") or 0) or min(60 * busy, 300)
                print(f"  server busy (429) for {Path(url).name}; waiting {pause}s ({busy}/30)")
                time.sleep(pause)
                continue
        except requests.RequestException as e:
            last = str(e)
        attempt += 1
        print(f"  retry {attempt}/{tries} for {url} ({last}); waiting {wait}s")
        time.sleep(wait)
        wait *= 2
    fail(f"could not fetch {url}: {last}")


def recorded_hash(db, path, root):
    row = db.execute("SELECT sha256 FROM files WHERE path=?", (manifest.rel(path, root),)).fetchone()
    return row[0] if row else None


def already_have(path, db, root):
    """True when the file exists and matches the hash in the manifest."""
    path = Path(path)
    if not path.exists():
        return False
    old = recorded_hash(db, path, root)
    return old is not None and old == manifest.sha256(path)


def download_once(url, dest, source_license, db, root=None, stage=0, params=None, check=None):
    """Download url to dest unless it is already there. Records it in the manifest.

    Returns (sha256, was_downloaded). A file that exists but is not in the
    manifest (for example an interrupted earlier run) is re-downloaded only if it
    is not yet recorded; a recorded raw/ file is never overwritten.
    check(path) may inspect the finished download and return an error message;
    a bad download is deleted and never recorded.
    """
    root = Path(root or data_root())
    dest = Path(dest)
    if already_have(dest, db, root):
        return recorded_hash(db, dest, root), False
    if dest.exists() and recorded_hash(db, dest, root):
        fail(f"{dest} is recorded with a different hash; raw/ is write-once. Investigate before re-running.")
    dest.parent.mkdir(parents=True, exist_ok=True)
    part = dest.with_name(dest.name + ".part")
    r = get(url, params=params, stream=True, timeout=600)
    with open(part, "wb") as f:
        for chunk in r.iter_content(1 << 20):
            f.write(chunk)
    problem = check(part) if check else None
    if problem:
        part.unlink()
        fail(f"{url}: {problem}")
    part.replace(dest)
    digest = manifest.add_file(dest, source=r.url, license=source_license, stage=stage,
                               date=today(), root=root, db=db)
    db.commit()
    return digest, True


def save_bytes_once(content, dest, source, source_license, db, root=None, stage=0):
    """Write bytes we fetched ourselves (for example an API answer) into raw/ once."""
    root = Path(root or data_root())
    dest = Path(dest)
    if dest.exists():
        if already_have(dest, db, root):
            return recorded_hash(db, dest, root), False
        if recorded_hash(db, dest, root):
            fail(f"{dest} is recorded with a different hash; raw/ is write-once.")
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(content)
    digest = manifest.add_file(dest, source=source, license=source_license, stage=stage,
                               date=today(), root=root, db=db)
    db.commit()
    return digest, True
