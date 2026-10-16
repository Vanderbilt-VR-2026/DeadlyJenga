"""Local speech/HTTP checks. Optional --unity also queues actual Unity playback
checks in an open editor. The child speech server exists only for this test.
"""
import argparse
import base64
import json
from pathlib import Path
import struct
import subprocess
import sys
import time
from urllib.request import Request, urlopen
from urllib.error import HTTPError

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "ArtDirection/CastleSocial"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--unity", action="store_true")
    parser.add_argument("--asr-model", default="")
    parser.add_argument("--tts", choices=("windows", "kokoro"), default="windows")
    args = parser.parse_args()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    results = []
    def check(ok, message):
        if not ok:
            raise AssertionError(message)
        results.append("PASS " + message)
        print(results[-1], flush=True)
    command = [sys.executable, str(Path(__file__).with_name("server.py")), "--tts", args.tts]
    if args.asr_model:
        command += ["--asr-model", args.asr_model]
    server = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    def request(path, body=None):
        req = Request("http://127.0.0.1:8765" + path, data=json.dumps(body).encode() if body is not None else None, headers={"Content-Type": "application/json"})
        return urlopen(req, timeout=18)
    try:
        for _ in range(1200):
            if server.poll() is not None:
                raise RuntimeError("Speech bridge startup failed: " + server.stderr.read().decode(errors="replace")[-1500:])
            try:
                with request("/health") as response:
                    health = json.load(response)
                break
            except OSError:
                time.sleep(.1)
        else:
            raise RuntimeError("Speech bridge did not start")
        check(health["tts"] == args.tts and health["recording"] is False, "Local bridge uses no paid service or voice recording")
        with request("/voices") as response:
            voices = json.load(response)
        check(len(voices["actual"]) > 0, "At least one real installed speech voice is available")
        timings = []
        audio = bytearray()
        for index, voice in enumerate(("af_heart", "af_bella")):
            started = time.perf_counter()
            with request("/speak", {"text": "Hey, watch that next ledge. We can make this.", "speaker": voice, "rate": 1}) as response:
                for raw in response:
                    data = json.loads(raw)
                    check("error" not in data, "Speech chunk contains actual audio rather than a mock") if not audio else None
                    if "error" in data:
                        raise RuntimeError(data["error"])
                    if not timings or index == len(timings):
                        timings.append(round(time.perf_counter() - started, 3))
                    audio.extend(base64.b64decode(data["audio"], validate=True))
        samples = struct.unpack("<" + "h" * (len(audio) // 2), audio)
        check(len(samples) > 16000 and sum(abs(v) for v in samples) / len(samples) > 50, "End-to-end synthesis returns non-silent generated speech PCM")
        if args.tts == "kokoro":
            began = time.perf_counter()
            with request("/speak", {"text": "Wait.", "speaker": "af_heart", "rate": .94}) as response:
                quick = json.loads(response.readline())
            elapsed = time.perf_counter()-began
            check(len(base64.b64decode(quick["audio"]))>0,"Pre-generated neural acknowledgment returns real cached audio")
            results.append("MEASURE pre-generated reaction first audio seconds: " + str(round(elapsed,3)))
        if args.asr_model:
            # Generated speech fixture; no human microphone or retained recording.
            fixture = base64.b64encode(audio[:min(len(audio)//2,192000)]).decode()
            for final in (False, True):
                started = time.perf_counter()
                with request("/transcribe", {"audio": fixture, "final": final}) as response:
                    recognized = json.load(response)
                check("watch" in recognized["text"].lower() and "ledge" in recognized["text"].lower(), "Real CPU recognizer transcribes synthetic speech " + ("final" if final else "partial window"))
                results.append("MEASURE ASR " + ("final" if final else "cold partial") + " seconds: " + str(round(time.perf_counter()-started,3)))
        else:
            try:
                with request("/transcribe", {"audio": "", "final": True}) as response:
                    response.read()
                check(False, "Missing STT model must not pretend to transcribe")
            except HTTPError as error:
                check(error.code == 503, "Unavailable STT backend reports explicit fallback")
        with request("/speak", {"text": "Hello", "speaker": "unapproved"}) as response:
            rejected = json.loads(response.readline())
        check("error" in rejected, "Unapproved voice identity rejected")
        results.append("MEASURE actual " + args.tts + " voice bank: " + ", ".join(voices["actual"]))
        results.append("MEASURE first audio seconds: " + json.dumps(timings))
        if args.unity:
            if args.asr_model:
                (OUTPUT / "synthetic-speech.pcm").write_bytes(audio[:min(len(audio)//2,192000)])
            marker = OUTPUT / "voice-test.enabled"
            marker.write_text(args.tts, encoding="utf-8")
            request_file = OUTPUT / "validate.request"
            began = time.time()
            request_file.write_text("run", encoding="utf-8")
            report = OUTPUT / "UnityValidation.txt"
            deadline = time.monotonic() + 240
            while time.monotonic() < deadline:
                if report.exists() and report.stat().st_mtime >= began:
                    text = report.read_text(encoding="utf-8-sig")
                    if "FAIL " in text:
                        raise AssertionError(text)
                    if "Clearing regular players does not clear flying hands" in text:
                        check("Actual self-hosted TTS streams PCM into Unity" in text, "Real Unity speech provider/playback integration completed")
                        check("Invalid live capacity change restores" in text,"Unity loaded the current regression suite rather than stale scripts")
                        if args.asr_model:
                            check("Actual incoming audio reaches local ASR" in text,"Real Unity transcription and human-perception integration completed")
                        break
                time.sleep(1)
            else:
                raise TimeoutError("Unity did not finish; leave Play Mode stopped and refresh assets to run the queued checks")
        print("\n".join(results[-2:]), flush=True)
    except Exception as error:
        results.append("FAIL " + str(error))
        raise
    finally:
        (OUTPUT / "voice-test.enabled").unlink(missing_ok=True)
        (OUTPUT / "synthetic-speech.pcm").unlink(missing_ok=True)
        server.terminate()
        try:
            server.wait(5)
        except subprocess.TimeoutExpired:
            server.kill()
        (OUTPUT / "VoiceBridgeChecks.txt").write_text("\n".join(results) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
