"""Build contact-review.mp4 in a contact review run folder with FFmpeg and FFprobe.

Usage: python contact_review.py <run_folder>
POV is on the left, Witness is on the right, and both use the run's tape audio.
When present, render-manifest.json must match the source clips and tape audio.
"""

import argparse
from fractions import Fraction
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
from tempfile import TemporaryDirectory


def probe(path, stream, *options):
    result = subprocess.run(
        ["ffprobe", "-v", "error", "-select_streams", stream, *options,
         "-show_streams", "-of", "json", path],
        check=True, capture_output=True, text=True,
    )
    streams = json.loads(result.stdout)["streams"]
    if not streams:
        raise ValueError(f"{path.name} has no {stream} stream")
    return streams[0]


def verify_hash(path, expected):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    if not isinstance(expected, str) or expected.lower() != digest.hexdigest():
        raise ValueError(f"SHA-256 mismatch for {path.name}")


def verify_manifest(path, videos, tape):
    # Older runs did not produce a manifest.
    if not os.path.lexists(path):
        return
    try:
        manifest = json.loads(path.read_text(encoding="utf-8-sig"))
        if type(manifest["version"]) is not int or manifest["version"] != 1:
            raise ValueError("Expected manifest version 1")
        entries = manifest["clips"]
        if not isinstance(entries, list):
            raise ValueError("Expected a manifest clips array")
        clips = {clip["id"]: clip for clip in entries}
        if len(clips) != len(entries) or clips.keys() != {shot.stem for shot in videos}:
            raise ValueError("Expected one POV and one Witness manifest clip")
        for shot, actual in videos.items():
            clip = clips[shot.stem]
            verify_hash(shot, clip["sha256"])
            for field, value in actual.items():
                if type(clip[field]) is not int or clip[field] != value:
                    raise ValueError(f"{shot.name} {field} is {value}, manifest declares {clip[field]!r}")
        verify_hash(tape, manifest["audioSha256"])
    except (KeyError, OSError, TypeError, ValueError) as error:
        raise ValueError(f"{path}: {error}") from error


def build_review(run):
    metadata, pov, witness, tape = (
        run / name for name in ("contact.txt", "shots/POV.mp4", "shots/Witness.mp4", "tape.wav")
    )
    for path in (metadata, pov, witness, tape):
        if not path.is_file():
            raise ValueError(f"Missing required run file {path}")

    lines = metadata.read_text(encoding="utf-8-sig").splitlines()
    rate = re.search(r"\(([1-9]\d*) fps\)\.$", lines[0]) if lines else None
    window = re.search(
        r"^Both cameras start at tape frame (\d+); review marker at output frame (\d+)\.$",
        "\n".join(lines), re.MULTILINE,
    )
    if not rate or not window:
        raise ValueError("contact.txt must specify the frame rate, tape start and output marker")
    fps = int(rate[1])
    start, marker = map(int, window.groups())

    videos = {}
    for shot in (pov, witness):
        video = probe(shot, "v:0", "-count_frames")
        if Fraction(video["avg_frame_rate"]) != fps:
            raise ValueError(f"{shot.name} has {video['avg_frame_rate']} fps, expected {fps}")
        videos[shot] = {
            "frames": int(video["nb_read_frames"]), "fps": fps,
            "width": video["width"], "height": video["height"],
        }
    counts = [video["frames"] for video in videos.values()]
    if counts[0] != counts[1] or marker >= counts[0]:
        raise ValueError(f"Cannot align POV ({counts[0]} frames) and Witness ({counts[1]} frames) at marker {marker}")
    verify_manifest(run / "render-manifest.json", videos, tape)

    audio = probe(tape, "a:0")
    sample_rate = int(audio["sample_rate"])
    audio_start = round(Fraction(start * sample_rate, fps))
    audio_end = round(Fraction((start + counts[0]) * sample_rate, fps))
    available = Fraction(audio["duration_ts"]) * Fraction(audio["time_base"]) * sample_rate
    if audio_end > available:
        raise ValueError(f"{tape.name} ends before the shot window finishes at {(start + counts[0]) / fps:.3f}s")

    panel = ("scale=640:360:force_original_aspect_ratio=decrease:force_divisible_by=2,"
             "pad=640:360:(ow-iw)/2:(oh-ih)/2,setsar=1,format=yuv420p")
    graph = (
        f"[0:v]settb=1/{fps},setpts=N,{panel}[pov];"
        f"[1:v]settb=1/{fps},setpts=N,{panel}[witness];"
        "[pov][witness]hstack[v];"
        f"[2:a]atrim=start_sample={audio_start}:end_sample={audio_end},asetpts=PTS-STARTPTS[a]"
    )
    output = run / "contact-review.mp4"
    if os.path.lexists(output):
        raise FileExistsError(f"Review already exists at {output}")
    with TemporaryDirectory(prefix=".contact-review-", dir=run) as scratch:
        encoded = Path(scratch) / output.name
        subprocess.run(
            ["ffmpeg", "-hide_banner", "-loglevel", "error", "-n", "-i", pov, "-i", witness, "-i", tape,
             "-filter_complex", graph, "-map", "[v]", "-map", "[a]", "-c:v", "libx264", "-crf", "18",
             "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", encoded],
            check=True,
        )
        # Publish the completed file without replacing another review.
        os.link(encoded, output)
    print(f"{output} | {counts[0]} frames at {fps} fps | marker {marker} ({marker / fps:.3f}s)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run_folder", type=Path)
    args = parser.parse_args()
    try:
        build_review(args.run_folder)
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        parser.exit(1, f"{error}\n")
