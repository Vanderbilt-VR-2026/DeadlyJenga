"""Intake step 1: put the original files into raw/capture/ without changing them.

Rules (STANDARDS.md section 8):
- raw/ is write-once. A file that is already there is never overwritten.
- A file whose content (sha256) is already in the destination folder is
  skipped, whatever its name.
- A different file with the same name is stored as name__<hash8>.ext.
- Copies are checked (hash after copy) and marked read-only.
- When the source folder is already inside raw/capture/ (the sample sets),
  the files are used where they are.
Zip files (some public sets come zipped) are unpacked into derived/, never
into raw/, so the zip in raw/ stays the original.
"""
import hashlib
import os
import shutil
import stat
import zipfile
from pathlib import Path

from intake_images import is_junk, is_photo

CHUNK = 1 << 20


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(CHUNK), b""):
            h.update(chunk)
    return h.hexdigest()


def is_inside(path, folder):
    try:
        Path(path).resolve().relative_to(Path(folder).resolve())
        return True
    except ValueError:
        return False


def scan(folder):
    """Every real file under folder (operating-system junk skipped), sorted."""
    folder = Path(folder)
    return sorted(p for p in folder.rglob("*") if p.is_file() and not is_junk(p.relative_to(folder))
                  and not p.name.endswith(".part"))


def known_hashes(dest_dir, db, root):
    """sha256 -> file for everything already in dest_dir (manifest first, then hashing)."""
    dest_dir = Path(dest_dir)
    out = {}
    if not dest_dir.exists():
        return out
    prefix = Path(dest_dir).resolve().relative_to(Path(root).resolve()).as_posix() + "/"
    recorded = {path: digest for path, digest in
                db.execute("SELECT path, sha256 FROM files WHERE path LIKE ?", (prefix + "%",))}
    for f in scan(dest_dir):
        r = f.resolve().relative_to(Path(root).resolve()).as_posix()
        out[recorded.get(r) or sha256(f)] = f
    return out


def plan_copy(files, dest_dir, have):
    """Decide what to do with each source file.

    Returns a list of dicts: {src, sha256, dest, action}, action is
    'copy', 'skip_same' (content already there) or 'copy_renamed'.
    `have` is the sha256 -> path map from known_hashes (it is updated).
    """
    dest_dir = Path(dest_dir)
    plan, taken = [], {p.name.lower() for p in have.values()}
    for src in files:
        digest = sha256(src)
        if digest in have:
            plan.append({"src": src, "sha256": digest, "dest": have[digest], "action": "skip_same"})
            continue
        dest = dest_dir / src.name
        action = "copy"
        if dest.name.lower() in taken or dest.exists():
            # Same name, different content: never overwrite; add the hash to the name.
            dest = dest_dir / f"{src.stem}__{digest[:8]}{src.suffix}"
            action = "copy_renamed"
        taken.add(dest.name.lower())
        have[digest] = dest
        plan.append({"src": src, "sha256": digest, "dest": dest, "action": action})
    return plan


def copy_verified(src, dest, digest):
    """Copy src to dest (which must not exist), check the hash, make it read-only."""
    dest = Path(dest)
    if dest.exists():
        raise FileExistsError(f"{dest} already exists; raw/ is write-once")
    dest.parent.mkdir(parents=True, exist_ok=True)
    part = dest.with_name(dest.name + ".part")
    shutil.copy2(src, part)                      # keeps the file times too
    if sha256(part) != digest:
        part.unlink()
        raise IOError(f"copy of {src} does not match its hash")
    part.replace(dest)
    os.chmod(dest, stat.S_IREAD | stat.S_IRGRP | stat.S_IROTH)


def unpack_zip(zip_path, out_dir):
    """Extract the photos inside a zip into out_dir/<zip stem>/ (skips ones already there).

    Returns a list of (member name, extracted path)."""
    out = []
    target = Path(out_dir) / Path(zip_path).stem
    with zipfile.ZipFile(zip_path) as z:
        for info in z.infolist():
            if info.is_dir() or is_junk(Path(info.filename)) or not is_photo(info.filename):
                continue
            dest = target / Path(info.filename).name
            if dest.exists() and dest.stat().st_size == info.file_size:
                out.append((info.filename, dest))
                continue
            dest.parent.mkdir(parents=True, exist_ok=True)
            part = dest.with_name(dest.name + ".part")
            with z.open(info) as fin, open(part, "wb") as fout:
                shutil.copyfileobj(fin, fout, CHUNK)
            part.replace(dest)
            out.append((info.filename, dest))
    return out
