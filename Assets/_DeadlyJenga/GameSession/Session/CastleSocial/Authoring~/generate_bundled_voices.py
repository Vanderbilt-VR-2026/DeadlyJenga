"""Bake the game's authored reactions into eight fixed KittenTTS voices."""
import argparse
import json
from pathlib import Path
import re
import struct

import numpy as np
import sherpa_onnx


VOICE_COUNT = 8
RATE = 16000


def authored_lines(source):
    # The Reaction method keeps the base lines as C# string literals.
    # Prefix/suffix decoration is skipped by the bundled provider.
    text = source.read_text(encoding="utf-8")
    reactions = text.split("string line;", 1)[1].split("if(c.verbosity<", 1)[0]
    backchannels = text.split("Backchannels=", 1)[1].split(";", 1)[0]
    lines = set()
    for literal in re.findall(r'"(?:[^"\\]|\\.)*"', reactions + backchannels):
        line = json.loads(literal)
        if line.endswith((".", "!", "?")):
            lines.add(line)
    return sorted(lines)


def pcm(audio):
    samples = np.asarray(audio.samples, dtype=np.float32)
    if not len(samples) or not np.isfinite(samples).all():
        raise ValueError("Empty or non-finite synthesized speech")
    active = np.flatnonzero(np.abs(samples) >= 0.008)
    if not len(active):
        raise ValueError("Silent synthesized speech")
    samples = samples[max(0, active[0]-audio.sample_rate//25):active[-1]+audio.sample_rate//12]
    new_length = round(len(samples) * RATE / audio.sample_rate)
    if not 1 <= new_length <= RATE * 6:
        raise ValueError(f"Unexpected clip duration: {new_length / RATE:.2f}s")
    samples = np.interp(np.arange(new_length) * audio.sample_rate / RATE, np.arange(len(samples)), samples)
    gain = min(.92 / np.max(np.abs(samples)), .12 / np.sqrt(np.mean(samples**2)), 2.5)
    return np.round(samples * gain * 32767).astype("<i2").tobytes()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-dir", type=Path, required=True)
    parser.add_argument("--model-file", default="model.fp16.onnx")
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--staging", type=Path, required=True,
                        help="Temporary file outside Unity's Assets directory")
    parser.add_argument("--line-list", type=Path, help="Optional UTF-8 inventory with one exact line per row")
    parser.add_argument("--threads", type=int, choices=(1, 2), default=1)
    args = parser.parse_args()
    model = args.model_dir
    kitten = sherpa_onnx.OfflineTtsKittenModelConfig(
        model=str(model / args.model_file), voices=str(model / "voices.bin"),
        tokens=str(model / "tokens.txt"), data_dir=str(model / "espeak-ng-data"))
    config = sherpa_onnx.OfflineTtsConfig(
        model=sherpa_onnx.OfflineTtsModelConfig(kitten=kitten, num_threads=args.threads, provider="cpu"),
        max_num_sentences=1, silence_scale=.15)
    tts = sherpa_onnx.OfflineTts(config)
    if tts.num_speakers < VOICE_COUNT:
        raise ValueError("The Kitten voice pack does not contain eight speakers")
    if args.line_list:
        lines = [line.strip() for line in args.line_list.read_text(encoding="utf-8-sig").splitlines() if line.strip()]
        if not 1 <= len(lines) <= 128 or len(set(lines)) != len(lines) or any(len(line.encode()) > 960 for line in lines):
            raise ValueError("The custom line inventory must contain 1–128 distinct short lines")
    else:
        lines = authored_lines(Path(__file__).resolve().parents[1] / "Core/SocialSimulation.cs")
        if not 40 <= len(lines) <= 128:
            raise ValueError(f"Unexpected authored reaction inventory: {len(lines)}")
    partial = args.staging
    header = struct.pack("<4sHH", b"DJB1", VOICE_COUNT, len(lines))
    completed = 0
    if partial.exists():
        with partial.open("r+b") as previous:
            data = previous.read()
            if not data.startswith(header):
                raise ValueError("The saved bake has a different header")
            cursor = len(header)
            for text in lines:
                start = cursor
                try:
                    length, = struct.unpack_from("<H", data, cursor)
                    cursor += 2
                    if data[cursor:cursor+length] != text.encode("utf-8"):
                        break
                    cursor += length
                    for _ in range(VOICE_COUNT):
                        samples, = struct.unpack_from("<I", data, cursor)
                        if not 1 <= samples <= RATE * 6:
                            raise ValueError("The saved bake has an invalid clip")
                        cursor += 4 + samples * 2
                    if cursor > len(data):
                        break
                except struct.error:
                    break
                completed += 1
                start = cursor
            else:
                start = cursor
            previous.truncate(start)
        print(f"Resuming after {completed} complete lines", flush=True)
    else:
        partial.write_bytes(header)
    with partial.open("ab") as out:
        for index, text in enumerate(lines[completed:], completed + 1):
            encoded = text.encode("utf-8")
            out.write(struct.pack("<H", len(encoded)))
            out.write(encoded)
            for voice in range(VOICE_COUNT):
                speech = pcm(tts.generate(text[0].upper() + text[1:], sid=voice, speed=1.0))
                out.write(struct.pack("<I", len(speech) // 2))
                out.write(speech)
            if index % 10 == 0:
                print(f"Baked {index}/{len(lines)} lines", flush=True)
    partial.replace(args.out)
    print(f"{VOICE_COUNT} voices × {len(lines)} lines: {args.out.stat().st_size:,} bytes")


if __name__ == "__main__":
    main()
