"""Prepare two local, counterbalanced review folders from comparable MP4s."""

import argparse
import json
import shutil
import subprocess
from fractions import Fraction
from pathlib import Path
from tempfile import TemporaryDirectory


QUESTIONS = """Trailer review

Watch clip-1.mp4 and clip-2.mp4 in that order. Answer questions 1–4
after each clip, before watching the next. Answer questions 5–6 at the end.
If something was unclear, say so.

1. Cause. What set the main action in motion? What did you see or hear
   that gave you that impression?
2. Decision. What did the main character choose or try to do?
3. Reaction. How did the other characters or the environment respond?
4. Recovery. What happened after the biggest impact? How could you tell?
5. Preference. Which clip worked better for you, clip 1, clip 2, or a tie?
   What made the difference?
6. Timestamp. If anything was hard to follow, give the clip number and
   approximate time (mm:ss), then describe what you expected to understand.
"""


def run(args):
    result = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if result.returncode or result.stderr.strip():
        raise ValueError(result.stderr.strip() or f"{args[0]} failed")
    return result.stdout


def media_signature(path):
    if path.suffix.lower() != ".mp4" or not path.is_file():
        raise ValueError(f"Expected an existing MP4: {path}")
    data = json.loads(run(["ffprobe", "-v", "error", "-show_streams", "-show_format", "-of", "json", str(path)]))
    video = [stream for stream in data["streams"]
             if stream["codec_type"] == "video" and not stream.get("disposition", {}).get("attached_pic")]
    audio = [stream for stream in data["streams"] if stream["codec_type"] == "audio"]
    if len(video) != 1 or len(audio) != 1:
        raise ValueError(f"Expected one video stream and one audio stream: {path}")
    stream = video[0]
    result = {
        "fps": Fraction(stream["avg_frame_rate"]),
        "resolution": (stream["width"], stream["height"]),
        "picture duration": int(stream["duration_ts"]) * Fraction(stream["time_base"]),
        "file duration": Fraction(data["format"]["duration"]),
    }
    if any(value <= 0 for value in (result["fps"], *result["resolution"],
                                    result["picture duration"], result["file duration"])):
        raise ValueError(f"Missing or nonpositive media metadata: {path}")
    return result


def match_media(expected, actual):
    for field, value in expected.items():
        if value != actual[field]:
            raise ValueError(f"Candidate {field} differs: {value} vs {actual[field]}")


def prepare_packet(first, second, out):
    if out.exists() or out.is_symlink():
        raise ValueError(f"Choose a new output directory: {out}")
    if first == second:
        raise ValueError("Choose two separate candidate files")
    signature = media_signature(first)
    match_media(signature, media_signature(second))

    out.parent.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(prefix=".trailer-review-", dir=out.parent) as staging:
        scratch = Path(staging)
        normalized = [scratch / "first.mp4", scratch / "second.mp4"]
        for source, target in zip((first, second), normalized):
            run(["ffmpeg", "-v", "error", "-nostdin", "-i", str(source), "-map", "0:V:0", "-map", "0:a:0",
                 "-map_metadata", "-1", "-map_metadata:s", "-1", "-map_chapters", "-1",
                 "-c", "copy", "-movflags", "+faststart", str(target)])
        normalized_signatures = [media_signature(clip) for clip in normalized]
        match_media(*normalized_signatures)
        picture_signature = {field: signature[field] for field in ("fps", "resolution", "picture duration")}
        for actual in normalized_signatures:
            match_media(picture_signature, actual)

        packet = scratch / "packet"
        assignments = {}
        for group, order in (("group-1", (0, 1)), ("group-2", (1, 0))):
            public = packet / "public" / group
            public.mkdir(parents=True)
            assignments[group] = {}
            for number, source_index in enumerate(order, 1):
                name = f"clip-{number}.mp4"
                shutil.copyfile(normalized[source_index], public / name)
                assignments[group][name] = str((first, second)[source_index])
            (public / "questions.txt").write_text(QUESTIONS, encoding="utf-8")

        private = packet / "private"
        private.mkdir()
        (private / "assignment-key.json").write_text(json.dumps(assignments, indent=2) + "\n", encoding="utf-8")
        packet.rename(out)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("candidate_a", type=Path)
    parser.add_argument("candidate_b", type=Path)
    parser.add_argument("out", type=Path, help="New directory for the packet")
    args = parser.parse_args()
    try:
        prepare_packet(args.candidate_a.resolve(), args.candidate_b.resolve(), args.out.absolute())
    except (KeyError, OSError, TypeError, ValueError, ZeroDivisionError) as error:
        parser.exit(2, f"Cannot prepare review packet: {error}\n")
    print(f"Review folders: {args.out / 'public'}")
    print(f"Private assignment key: {args.out / 'private' / 'assignment-key.json'}")


if __name__ == "__main__":
    main()
