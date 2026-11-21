"""The per-image work of intake, run in parallel worker processes.

process_image(task): read one image, measure its quality, detect faces,
plates, people and vehicles, write the blurred copy and the mask.
split_video_frame(task): cut one 360 frame into flat views.

Each worker loads the detector models once (init_worker). OpenCV's own
threads are limited to one per worker so --jobs N really uses N cores.
"""
import os
import time

os.environ.setdefault("OPENCV_LOG_LEVEL", "ERROR")   # hide OpenCV DNN info warnings
from pathlib import Path

import cv2

import intake_images
import intake_privacy
import intake_quality
import intake_video

_detectors = None


def init_worker(model_dir=None, load_models=True):
    global _detectors
    cv2.setNumThreads(1)
    if load_models and _detectors is None:
        _detectors = intake_privacy.Detectors(model_dir)


def process_image(task):
    t0 = time.time()
    out = {"name": task["name"], "error": None}
    img = intake_images.read_upright(task["input"])
    if img is None:
        out["error"] = "could not read image"
        out["quality"] = {}
        return out
    out["quality"] = intake_quality.measure(img)
    if not task.get("skip_blur"):
        h, w = img.shape[:2]
        dets = intake_privacy.detect(_detectors, img)
        regions = intake_privacy.blur_regions(dets, w, h)
        mask = intake_privacy.build_mask(dets, regions, w, h, task.get("view"))
        blurred = img.copy()
        for _, box in regions:
            intake_privacy.pixelate_blur(blurred, box)
        intake_images.write_jpeg(task["blurred"], blurred, 95)
        intake_images.write_png(task["mask"], mask)
        out["privacy"] = intake_privacy.summarize(dets, regions, mask)
    out["seconds"] = round(time.time() - t0, 2)
    return out


def split_video_frame(task):
    """Write the views of one equirectangular frame. Returns a list of view records."""
    eq = intake_images.read_upright(task["frame"])
    if eq is None:
        return []
    recs = []
    for yaw, pitch, img in intake_video.split_frame(eq, task["views"], task["fov"], task["size"]):
        name = f"{task['video_key']}/f{task['frame_index']:06d}_y{int(round(yaw)):03d}_p{int(round(pitch)):+03d}.jpg"
        dest = Path(task["out_dir"]) / name
        intake_images.write_jpeg(dest, img, 95)
        recs.append({"file": str(dest), "name": name, "yaw": yaw, "pitch": pitch, "fov": task["fov"],
                     "size": img.shape[1], "time_s": task["time_s"], "frame_index": task["frame_index"]})
    Path(task["frame"]).unlink(missing_ok=True)
    return recs
