"""Assemble a Pocket TTS conversation pack from a prepared conversation pack."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import tempfile

CAST = "pocket-tts-int8"


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


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-pack", type=Path, required=True)
    parser.add_argument("--pocket-model", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()

    out = args.out.resolve()
    if out.exists():
        raise FileExistsError(f"Destination already exists: {out}")
    manifest = json.loads((args.base_pack / "manifest.json").read_text(encoding="utf-8-sig"))
    if (manifest.get("version"), manifest.get("nativeApi")) not in (("dj-conversation-1", 1), ("dj-conversation-2", 2)):
        raise ValueError("Expected an existing Deadly Jenga conversation pack.")

    out.parent.mkdir(parents=True, exist_ok=True)
    stage = Path(tempfile.mkdtemp(prefix=f".{out.name}-", dir=out.parent))
    files = []
    for entry in manifest["files"]:
        name = safe_relative(entry["path"])
        if name.parts[0] == "voice" or (name.parts[0] == "licenses" and "kitten" in name.name.lower()):
            continue
        source = args.base_pack / name
        if source.stat().st_size != entry["bytes"] or fingerprint(source) != entry["sha256"]:
            raise ValueError(f"Base pack mismatch: {name}")
        link_or_copy(source, stage / name)
        files.append(entry)

    voice = args.pocket_model
    names = ["lm_flow.int8.onnx", "lm_main.int8.onnx", "encoder.onnx",
             "decoder.int8.onnx", "text_conditioner.onnx", "vocab.json", "token_scores.json"]
    names.extend(f"voice-{slot}.wav" for slot in range(8))
    new = {f"voice/{name}": voice / name for name in names}
    new["licenses/Pocket-TTS.txt"] = voice / "LICENSE"
    new["licenses/Pocket-Cast.txt"] = Path(__file__).resolve().parents[6] / "Tools/OnDeviceConversation/licenses/Pocket-Cast.txt"

    for name, source in sorted(new.items()):
        if not source.is_file() or not source.stat().st_size:
            raise ValueError(f"Voice pack input is missing or empty: {source}")
        link_or_copy(source, stage / name)
        files.append(dict(path=name, bytes=source.stat().st_size, sha256=fingerprint(source)))

    if len(files) > 2000 or sum(entry["bytes"] for entry in files) > 1024**3:
        raise ValueError("The prepared pack exceeds the runtime limits.")
    (stage / "manifest.json").write_text(json.dumps({
        "version": "dj-conversation-2",
        "nativeApi": 2,
        "voiceCast": CAST,
        "files": sorted(files, key=lambda entry: entry["path"]),
    }, indent=2) + "\n", encoding="utf-8")
    stage.rename(out)
    print(f"{CAST}: {len(files)} verified files in {out}")


if __name__ == "__main__":
    main()
