"""Package a reviewed gameplay recording for Candy Sound Studio. Requires FFmpeg."""
import argparse
import hashlib
import json
import math
import shutil
import subprocess
import tempfile
from pathlib import Path

CONTEXT_FIELDS = ("sourceCommit", "scene", "map", "mode", "captureKind", "source",
                  "action", "target", "listenerRole", "triggerEvidence", "semanticReview", "reviewer")


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def validate_context(context):
    missing = [key for key in CONTEXT_FIELDS if not isinstance(context.get(key), str) or not context[key].strip()]
    if missing:
        raise ValueError("Missing reviewed capture context: " + ", ".join(missing))
    if context["captureKind"] not in ("gameplay", "staged-gameplay"):
        raise ValueError("Use an actual gameplay capture, labelled staged-gameplay when staged.")
    if context["semanticReview"] != "verified":
        raise ValueError("Inspect the clip and verify its source/action/target before packaging.")
    if context.get("viewerUrl") and not context["viewerUrl"].startswith("https://"):
        raise ValueError("viewerUrl must be an HTTPS private viewer link.")


def build(args):
    source, output = args.source.resolve(), args.output.resolve()
    context = json.loads(args.context.read_text(encoding="utf-8-sig"))
    validate_context(context)
    if not source.is_file():
        raise ValueError("Source recording does not exist.")
    if not all(math.isfinite(v) for v in (args.start, args.duration, args.event)):
        raise ValueError("Times must be finite numbers.")
    if not 1 <= args.duration <= 5 or not 0 <= args.event < args.duration or args.start < 0:
        raise ValueError("Use 1–5 seconds and an event marker inside that excerpt; start must be nonnegative.")
    if args.cue_id < 0:
        raise ValueError("Use the stable cue ID exported by the studio.")
    if output.exists() and (not output.is_dir() or any(output.iterdir())):
        raise ValueError("Choose a new or empty output directory; existing packages are preserved.")
    ffmpeg = args.ffmpeg or shutil.which("ffmpeg")
    if not ffmpeg:
        raise ValueError("FFmpeg is required. Supply --ffmpeg with your existing executable path.")

    def run(*parameters):
        result = subprocess.run([str(ffmpeg), "-hide_banner", "-loglevel", "error", "-nostdin", "-y",
                                 "-threads", "2", "-filter_threads", "1", "-filter_complex_threads", "1",
                                 *map(str, parameters)], capture_output=True, text=True, timeout=60)
        if result.returncode:
            raise ValueError("FFmpeg failed: " + result.stderr[-3000:])

    with tempfile.TemporaryDirectory(prefix="candy-trigger-") as temporary:
        work = Path(temporary)
        frames = work / "frame-%03d.png"
        run("-ss", args.start, "-i", source, "-t", args.duration, "-an", "-vf",
            "fps=12,scale=512:288:force_original_aspect_ratio=decrease:flags=lanczos,"
            "pad=512:288:(ow-iw)/2:(oh-ih)/2:color=0x24172d,setsar=1", "-frames:v", "60", frames)
        count = len(list(work.glob("frame-*.png")))
        if count < 12 or args.event >= count / 12:
            raise ValueError("The recording ended before a valid excerpt/event could be extracted.")
        columns, rows = 8, math.ceil(count / 8)
        atlas, gif = work / "atlas.png", work / "preview.gif"
        run("-framerate", "12", "-i", frames, "-vf", f"tile={columns}x{rows}:padding=0:margin=0:color=0x24172d",
            "-frames:v", "1", atlas)
        run("-framerate", "12", "-i", frames, "-filter_complex",
            "[0:v]split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=sierra2_4a",
            "-loop", "0", gif)
        packet = {key: context[key] for key in CONTEXT_FIELDS}
        packet.update(schemaVersion=1, cueId=args.cue_id, title=context.get("title", ""),
                      frameCount=count, columns=columns, rows=rows, frameWidth=512, frameHeight=288,
                      fps=12, eventTimeSeconds=args.event, atlasFile="atlas.png", atlasSha256=digest(atlas),
                      gifFile="preview.gif", gifSha256=digest(gif), sourceSha256=digest(source),
                      viewerUrl=context.get("viewerUrl", ""))
        output.mkdir(parents=True, exist_ok=True)
        shutil.copy2(atlas, output / atlas.name)
        shutil.copy2(gif, output / gif.name)
        (output / "preview.json").write_text(json.dumps(packet, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"package": str(output), "cueId": args.cue_id, "seconds": count / 12,
                      "frames": count, "atlas": f"{columns * 512}x{rows * 288}"}, indent=2))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True, help="Actual recorded MP4 or GIF, retained outside Assets.")
    parser.add_argument("--cue-id", type=int, required=True)
    parser.add_argument("--context", type=Path, required=True, help="Reviewed context JSON; see SOUND_PREVIEW_CAPTURE_GUIDE.md.")
    parser.add_argument("--output", type=Path, required=True, help="New/empty external package folder.")
    parser.add_argument("--start", type=float, default=0)
    parser.add_argument("--duration", type=float, required=True)
    parser.add_argument("--event", type=float, required=True, help="Audible event time relative to the excerpt, not the raw recording.")
    parser.add_argument("--ffmpeg", help="Existing FFmpeg executable if not on PATH.")
    try:
        build(parser.parse_args())
    except (ValueError, OSError, subprocess.TimeoutExpired) as error:
        parser.exit(1, str(error) + "\n")


if __name__ == "__main__":
    main()
