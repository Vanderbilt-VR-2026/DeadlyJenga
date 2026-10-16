"""Explicit opt-in CPU neural benchmark; downloads public model assets on first run.
Uses generated speech only. Never opens a microphone or records a human.
"""
import json
import os
from pathlib import Path
import time
import wave

ROOT = Path(__file__).resolve().parent
os.environ.setdefault("HF_HOME", str(ROOT / ".cache"))
OUT = ROOT.parents[1] / "ArtDirection/CastleSocial"
OUT.mkdir(parents=True, exist_ok=True)


def main():
    started = time.perf_counter()
    import torch
    from kokoro import KPipeline
    from server import resample_16k
    torch.set_num_threads(2)
    pipeline = KPipeline(lang_code="a", repo_id="hexgrad/Kokoro-82M", device="cpu")
    results = {"backend": "Kokoro-82M", "device": "cpu", "threads": 2, "recording": False, "samples": []}
    results["cold_start_seconds"] = round(time.perf_counter() - started, 3)
    for index, speaker in enumerate(("af_heart", "am_adam")):
        began = time.perf_counter()
        chunks = []
        first = None
        for _, _, audio in pipeline("Hey, watch that next ledge. We can make this!", voice=speaker, speed=1):
            if first is None:
                first = time.perf_counter() - began
            chunks.append(resample_16k(audio, 24000))
        pcm = b"".join(chunks)
        duration = len(pcm) / 32000
        if duration < 1:
            raise AssertionError("Neural provider did not produce an actual speech waveform")
        elapsed = time.perf_counter() - began
        path = OUT / ("neural-voice-" + str(index + 1) + ".wav")
        with wave.open(str(path), "wb") as output:
            output.setnchannels(1)
            output.setsampwidth(2)
            output.setframerate(16000)
            output.writeframes(pcm)
        results["samples"].append({"voice": speaker, "first_audio_seconds": round(first, 3), "audio_seconds": round(duration, 3), "synthesis_seconds": round(elapsed, 3), "realtime_factor": round(elapsed/duration, 3), "file": path.name})
    results["status"] = "PASS actual CPU neural speech generated; perceptual naturalness requires listening evaluation"
    (OUT / "NeuralBenchmark.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(json.dumps(results, indent=2), flush=True)


if __name__ == "__main__":
    main()
