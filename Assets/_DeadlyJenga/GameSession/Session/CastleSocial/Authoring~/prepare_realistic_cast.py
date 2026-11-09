"""Assemble a matching Mini 0.8 cast from a prepared conversation pack."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import struct
import tempfile

CAST = "kitten-mini-0.8"


def fingerprint(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def safe_relative(name):
    path = PurePosixPath(name)
    if not name or path.is_absolute() or ".." in path.parts or ":" in name or "\\" in name:
        raise ValueError(f"Invalid manifest path: {name}")
    return path


def link_or_copy(source, target):
    target.parent.mkdir(parents=True, exist_ok=True)
    try:
        os.link(source.resolve(), target)
    except OSError:
        shutil.copy2(source, target)


def validate_bank(path):
    data = path.read_bytes()
    if len(data) > 32 * 1024 * 1024:
        raise ValueError("Voice phrase bank exceeds the runtime limit.")
    inventory = Path(__file__).with_name("fast-banter-lines.txt").read_text(encoding="utf-8-sig").splitlines()
    magic, speakers, count = struct.unpack_from("<4sHH", data)
    if (magic, speakers) != (b"DJB1", 8) or not 1 <= count <= len(inventory):
        raise ValueError("The phrase bank does not match the eight-voice inventory.")
    offset = 8
    seen = set()
    for _ in range(count):
        length, = struct.unpack_from("<H", data, offset)
        offset += 2
        phrase = data[offset:offset+length].decode()
        if phrase not in inventory or phrase in seen:
            raise ValueError("The phrase bank contains an unknown or duplicate phrase.")
        seen.add(phrase)
        offset += length
        takes = set()
        for _ in range(speakers):
            samples, = struct.unpack_from("<I", data, offset)
            offset += 4
            take = data[offset:offset + samples * 2]
            if not 1 <= samples <= 96000 or len(take) != samples * 2 or not any(take):
                raise ValueError("A phrase has an invalid or silent recording.")
            takes.add(hashlib.sha256(take).digest())
            offset += samples * 2
        if len(takes) != speakers:
            raise ValueError("A phrase has identical speaker recordings.")
    if offset != len(data):
        raise ValueError("The phrase bank contains unexpected trailing data.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-pack", type=Path, required=True)
    parser.add_argument("--mini-model", type=Path, required=True)
    parser.add_argument("--phrase-bank", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()

    out = args.out.resolve()
    if out.exists():
        raise FileExistsError(f"Destination already exists: {out}")
    manifest = json.loads((args.base_pack / "manifest.json").read_text(encoding="utf-8-sig"))
    if manifest.get("nativeApi") != 1 or manifest.get("version") != "dj-conversation-1":
        raise ValueError("Expected an existing conversation pack with native API 1.")
    validate_bank(args.phrase_bank)

    out.parent.mkdir(parents=True, exist_ok=True)
    stage = Path(tempfile.mkdtemp(prefix=f".{out.name}-", dir=out.parent))
    files = []
    for entry in manifest["files"]:
        name = safe_relative(entry["path"])
        if name.parts[0] == "voice":
            continue
        source = args.base_pack / name
        if source.stat().st_size != entry["bytes"] or fingerprint(source) != entry["sha256"]:
            raise ValueError(f"Base pack mismatch: {name}")
        link_or_copy(source, stage / name)
        files.append(entry)

    voice = args.mini_model
    new = {
        "voice/model.fp16.onnx": voice / "model.onnx",
        "voice/voices.bin": voice / "voices.bin",
        "voice/tokens.txt": voice / "tokens.txt",
        "voice/fast-banter.bytes": args.phrase_bank,
        "licenses/Kitten-Mini-0.8.txt": voice / "LICENSE",
    }
    for source in (voice / "espeak-ng-data").rglob("*"):
        if source.is_file():
            new[f"voice/{source.relative_to(voice).as_posix()}"] = source

    for name, source in sorted(new.items()):
        if not source.is_file() or not source.stat().st_size:
            raise ValueError(f"Voice pack input is missing or empty: {source}")
        link_or_copy(source, stage / name)
        files.append(dict(path=name, bytes=source.stat().st_size, sha256=fingerprint(source)))

    if len(files) > 2000 or sum(entry["bytes"] for entry in files) > 1024**3:
        raise ValueError("The prepared pack exceeds the runtime limits.")
    (stage / "manifest.json").write_text(json.dumps({
        "version": manifest["version"],
        "nativeApi": manifest["nativeApi"],
        "voiceCast": CAST,
        "files": sorted(files, key=lambda entry: entry["path"]),
    }, indent=2) + "\n", encoding="utf-8")
    stage.rename(out)
    print(f"{CAST}: {len(files)} verified files in {out}")


if __name__ == "__main__":
    main()
