"""Bake the game's authored reactions into eight fixed Pocket TTS voices."""
import argparse
import json
from pathlib import Path
import re
import struct
import wave

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
        if line.endswith((".", "!", "?")) and any(char.isalpha() for char in line):
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


def open_pocket(model, threads):
    pocket = sherpa_onnx.OfflineTtsPocketModelConfig(
        lm_flow=str(model / "lm_flow.int8.onnx"),
        lm_main=str(model / "lm_main.int8.onnx"),
        encoder=str(model / "encoder.onnx"),
        decoder=str(model / "decoder.int8.onnx"),
        text_conditioner=str(model / "text_conditioner.onnx"),
        vocab_json=str(model / "vocab.json"),
        token_scores_json=str(model / "token_scores.json"),
        voice_embedding_cache_capacity=VOICE_COUNT,
    )
    config = sherpa_onnx.OfflineTtsConfig(
        model=sherpa_onnx.OfflineTtsModelConfig(pocket=pocket, num_threads=threads, provider="cpu"),
        max_num_sentences=1, silence_scale=.15)
    return sherpa_onnx.OfflineTts(config)


def voice_settings(path):
    with wave.open(str(path), "rb") as voice:
        if voice.getnchannels() != 1 or voice.getsampwidth() != 2:
            raise ValueError(f"Expected a mono PCM16 reference: {path}")
        config = sherpa_onnx.GenerationConfig()
        config.reference_audio = (np.frombuffer(voice.readframes(voice.getnframes()), dtype="<i2")
                                  .astype(np.float32) / 32768).tolist()
        config.reference_sample_rate = voice.getframerate()
    config.num_steps = 2
    config.speed = 1.0
    config.silence_scale = .15
    config.extra = {"temperature": "0.3", "max_frames": "120", "chunk_size": "5"}
    return config


def render(tts, text, voice, seed_offset=0):
    repeated = re.fullmatch(r"(\w+)(?:,\s+\1)+!", text, re.IGNORECASE)
    if repeated:
        word = repeated.group(1) + "!"
        return (b"\0" * (RATE // 10)).join(
            render(tts, word, voice, index) for index in range(text.count(",") + 1))

    # Some samples run past the intended line. Keep a complete, bounded take.
    spoken = text[0].upper() + text[1:]
    for seed, end, reference_seconds in ((42, "", "10"), (101, "!", "10"), (202, ".", "5"), (303, "!", "5")):
        voice.extra = dict(voice.extra, seed=str(seed + seed_offset), max_reference_audio_len=reference_seconds)
        candidate = spoken.rstrip(".?!") + end if end else spoken
        try:
            return pcm(tts.generate(candidate, config=voice))
        except ValueError:
            if seed == 303:
                raise ValueError(f"No bounded Pocket take for {text!r}") from None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-dir", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--staging", type=Path, required=True,
                        help="Temporary file outside Unity's Assets directory")
    parser.add_argument("--line-list", type=Path, help="Optional UTF-8 inventory with one exact line per row")
    parser.add_argument("--threads", type=int, choices=(1, 2), default=1)
    args = parser.parse_args()
    tts = open_pocket(args.model_dir, args.threads)
    cast = [voice_settings(args.model_dir / f"voice-{i}.wav") for i in range(VOICE_COUNT)]
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
            for voice in cast:
                speech = render(tts, text, voice)
                out.write(struct.pack("<I", len(speech) // 2))
                out.write(speech)
            if index % 10 == 0:
                print(f"Baked {index}/{len(lines)} lines", flush=True)
    partial.replace(args.out)
    print(f"{VOICE_COUNT} voices × {len(lines)} lines: {args.out.stat().st_size:,} bytes")


if __name__ == "__main__":
    main()
