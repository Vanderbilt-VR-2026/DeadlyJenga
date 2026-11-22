"""Cut synchronized coverage while keeping continuous production sound.

Example: python cut_take.py TAKE NEW_DIRECTORY Witness@0 POV@24 Witness@51 --end 90
Frame numbers are relative to the shot window. Install opentimelineio in a local
Python environment. FFmpeg and ffprobe must be on PATH.
"""

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
from tempfile import TemporaryDirectory

import opentimelineio as otio

from contact_review import probe, read_synchronized_take


def frame_range(start, duration, fps):
    return otio.opentime.TimeRange(
        otio.opentime.RationalTime(start, fps), otio.opentime.RationalTime(duration, fps))


def clip(path, start, duration, fps):
    return otio.schema.Clip(name=path.stem,
                            media_reference=otio.schema.ExternalReference(target_url="media/" + path.name),
                            source_range=frame_range(start, duration, fps))


def cut(run, out, switches, end):
    pov, witness, tape, fps, tape_start, frames, _, _, _ = read_synchronized_take(run)
    sources = {"POV": pov, "Witness": witness}
    edits = []
    for switch in switches:
        angle, separator, number = switch.partition("@")
        if angle not in sources or not separator or not number.isascii() or not number.isdigit():
            raise ValueError(f"Expected POV@frame or Witness@frame, received {switch!r}")
        edits.append((angle, int(number)))
    end = frames if end is None else end
    starts = [frame for _, frame in edits]
    if not starts or starts != sorted(set(starts)) or not starts[-1] < end <= frames:
        raise ValueError(f"Expected increasing cut positions inside the {frames}-frame source window")
    if out.exists() or out.is_symlink():
        raise ValueError(f"Choose a new output path: {out}")
    out.parent.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(dir=out.parent) as temp:
        package = Path(temp) / "edit"
        media = package / "media"
        media.mkdir(parents=True)
        timeline = otio.schema.Timeline(name="Trailer Studio camera edit")
        picture = otio.schema.Track(name="Camera", kind=otio.schema.TrackKind.Video)
        sound = otio.schema.Track(name="Production sound", kind=otio.schema.TrackKind.Audio)
        timeline.tracks.extend([picture, sound])
        graph = []
        for index, ((angle, start), stop) in enumerate(zip(edits, starts[1:] + [end])):
            picture.append(clip(sources[angle], start, stop-start, fps))
            stream = 0 if angle == "POV" else 1
            graph.append(f"[{stream}:v]trim=start_frame={start}:end_frame={stop},setpts=PTS-STARTPTS[v{index}]")
        duration = end - starts[0]
        sound.append(clip(tape, tape_start + starts[0], duration, fps))
        samples = int(probe(tape, "a:0")["sample_rate"])
        audio_in, audio_out = (round((tape_start + frame) * samples / fps) for frame in [starts[0], end])
        graph.append("".join(f"[v{i}]" for i in range(len(edits))) + f"concat=n={len(edits)}:v=1:a=0[v]")
        graph.append(f"[2:a]atrim=start_sample={audio_in}:end_sample={audio_out},asetpts=PTS-STARTPTS[a]")
        hashes = {}
        for source in (pov, witness, tape):
            dest = media / source.name
            shutil.copyfile(source, dest)
            with dest.open("rb") as stream:
                hashes[dest.name] = hashlib.file_digest(stream, "sha256").hexdigest()
        timeline.metadata["TrailerStudio"] = {"cameraCuts": switches, "sourceHashes": hashes,
                                             "sourceEndExclusive": end, "sourceTapeStart": tape_start}
        edit_file = package / "edit.otio"
        otio.adapters.write_to_file(timeline, str(edit_file))
        loaded = otio.adapters.read_from_file(str(edit_file))
        if loaded.duration().value != duration or any(
            not (package / item.media_reference.target_url).is_file() for item in loaded.find_clips()
        ):
            raise ValueError("Exported timeline could not be verified")
        movie = package / "cut.mp4"
        subprocess.run(["ffmpeg", "-v", "error", "-nostdin", "-i", media / pov.name,
                        "-i", media / witness.name, "-i", media / tape.name, "-filter_complex", ";".join(graph),
                        "-map", "[v]", "-map", "[a]", "-c:v", "libx264", "-crf", "18", "-c:a", "aac",
                        "-b:a", "192k", "-movflags", "+faststart", movie], check=True)
        if int(probe(movie, "v:0", "-count_frames")["nb_read_frames"]) != duration:
            raise ValueError("Exported movie duration differs from the edit")
        probe(movie, "a:0")
        package.rename(out)
    print(json.dumps({"frames": duration, "fps": fps, "package": str(out)}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", type=Path)
    parser.add_argument("out", type=Path)
    parser.add_argument("switches", nargs="+")
    parser.add_argument("--end", type=int, help="Exclusive last frame in shot-window coordinates")
    args = parser.parse_args()
    try:
        cut(args.run, args.out, args.switches, args.end)
    except (KeyError, OSError, ValueError, subprocess.CalledProcessError) as error:
        parser.exit(1, f"{error}\n")
