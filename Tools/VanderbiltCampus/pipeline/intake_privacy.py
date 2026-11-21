"""Intake step 4: blur faces and license plates, and mask people and vehicles.

Everything runs locally on the CPU with OpenCV's DNN module. Nothing is
uploaded. Models come from the OpenCV Zoo (licenses in ATTRIBUTION.md) and are
stored in <tools>/models/ (VanderbiltCampusTools\\models by default):

- YuNet face detector (MIT), run on overlapping tiles so small faces count;
- YOLOX-S object detector (Apache-2.0, COCO classes) for people and vehicles,
  run on the whole image and on 2x2 tiles;
- LPD-YuNet license plate detector (Apache-2.0; trained on Chinese plates,
  so it is backed up by the vehicle rule below).

What is blurred (recall over precision; these regions are masked for
alignment anyway, so extra blur costs nothing):
- every face box, enlarged 1.6x;
- the head band (top 22%) of every person box, so faces turned away, too
  small or missed by YuNet are still covered;
- every plate box, enlarged 1.5x (plates must be small, wide and on a detected
  vehicle unless the score is >= 0.85; huge or tall boxes are dropped);
- the plate band (lower 55%) of every car, truck, bus and motorcycle box.

Masks (white = usable for alignment): black over people, vehicles, animals,
every blurred box, and for 360 views the operator region (rays more than
OPERATOR_PITCH degrees below the horizon).
"""
import hashlib
import os
import subprocess
import tempfile
from pathlib import Path

import cv2
import numpy as np

FACE_MODEL = "face_detection_yunet_2026may.onnx"
YOLOX_MODEL = "object_detection_yolox_2022nov.onnx"
PLATE_MODEL = "license_plate_detection_lpd_yunet_2023mar.onnx"

FACE_SCORE = 0.6
YOLOX_SCORE = 0.30
YOLOX_NMS = 0.45
PLATE_SCORE = 0.6
PLATE_SCORE_ALONE = 0.85   # a plate not on a detected vehicle needs this score
FACE_MAX_SHARE = 0.4       # a face box wider than this share of the short side is a false hit
FACE_TILE = 960            # tile size (px, at working scale) for the face detector
FACE_MIN_SCALE_SIDE = 1800 # small images are enlarged to at least this long side for faces
OPERATOR_PITCH = -35.0     # 360 views: mask rays below this pitch (degrees)

COCO = ('person', 'bicycle', 'car', 'motorcycle', 'airplane', 'bus', 'train', 'truck', 'boat',
        'traffic light', 'fire hydrant', 'stop sign', 'parking meter', 'bench', 'bird', 'cat', 'dog',
        'horse', 'sheep', 'cow', 'elephant', 'bear', 'zebra', 'giraffe', 'backpack', 'umbrella',
        'handbag', 'tie', 'suitcase', 'frisbee', 'skis', 'snowboard', 'sports ball', 'kite',
        'baseball bat', 'baseball glove', 'skateboard', 'surfboard', 'tennis racket', 'bottle',
        'wine glass', 'cup', 'fork', 'knife', 'spoon', 'bowl', 'banana', 'apple', 'sandwich', 'orange',
        'broccoli', 'carrot', 'hot dog', 'pizza', 'donut', 'cake', 'chair', 'couch', 'potted plant',
        'bed', 'dining table', 'toilet', 'tv', 'laptop', 'mouse', 'remote', 'keyboard', 'cell phone',
        'microwave', 'oven', 'toaster', 'sink', 'refrigerator', 'book', 'clock', 'vase', 'scissors',
        'teddy bear', 'hair drier', 'toothbrush')
PERSON = {"person"}
VEHICLES = {"car", "truck", "bus", "motorcycle"}
MOVERS = PERSON | VEHICLES | {"bicycle", "train", "bird", "cat", "dog", "horse"}

VERSION = 3   # bump when the blur/mask rules change, so re-runs redo the images


def default_model_dir():
    return Path(os.environ.get("VC_MODELS") or Path.home() / "VanderbiltCampusTools" / "models")


def file_sha(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def detector_info(model_dir=None):
    """Names, files, hashes and thresholds of the detectors (logged in the outputs)."""
    d = Path(model_dir or default_model_dir())
    info = {"opencv": cv2.__version__, "rules_version": VERSION, "models": {}}
    for key, name, lic, thr in [("face", FACE_MODEL, "MIT", {"score": FACE_SCORE, "tile": FACE_TILE, "max_share_of_short_side": FACE_MAX_SHARE, "aspect_h_over_w": [0.7, 2.5]}),
                                ("objects", YOLOX_MODEL, "Apache-2.0", {"score": YOLOX_SCORE, "nms": YOLOX_NMS}),
                                ("plates", PLATE_MODEL, "Apache-2.0", {"score": PLATE_SCORE, "score_if_not_on_vehicle": PLATE_SCORE_ALONE, "max_w_share": 0.25, "max_h_share": 0.15, "aspect_w_over_h": [1.2, 6.5]})]:
        p = d / name
        info["models"][key] = {"file": name, "sha256": file_sha(p) if p.exists() else None,
                               "license": lic, "source": "https://github.com/opencv/opencv_zoo", **thr}
    info["rules"] = {"face_enlarge": 1.6, "head_band_of_person": 0.22, "plate_enlarge": 1.5,
                     "vehicle_plate_band": 0.55, "operator_pitch_deg": OPERATOR_PITCH,
                     "masked_classes": sorted(MOVERS)}
    return info


def check_models(model_dir=None):
    d = Path(model_dir or default_model_dir())
    return [n for n in (FACE_MODEL, YOLOX_MODEL, PLATE_MODEL) if not (d / n).exists()]


# ---- detectors -----------------------------------------------------------

def _nms(boxes, scores, thr):
    if not boxes:
        return []
    keep = cv2.dnn.NMSBoxes([list(map(float, b)) for b in boxes], [float(s) for s in scores], 0.0, thr)
    return [int(i) for i in np.array(keep).flatten()]


def _tiles(w, h, size, overlap=0.2):
    """Top-left corners of overlapping size x size tiles covering a w x h image."""
    def starts(n):
        if n <= size:
            return [0]
        step = int(size * (1 - overlap))
        s = list(range(0, n - size, step)) + [n - size]
        return sorted(set(s))
    return [(x, y) for y in starts(h) for x in starts(w)]


class Detectors:
    """Loads the three models once (one copy per worker process)."""

    def __init__(self, model_dir=None):
        d = Path(model_dir or default_model_dir())
        self.face = cv2.FaceDetectorYN.create(str(d / FACE_MODEL), "", (320, 320), FACE_SCORE, 0.3, 5000)
        self.yolox = cv2.dnn.readNet(str(d / YOLOX_MODEL))
        self.plate = cv2.dnn.readNet(str(d / PLATE_MODEL))
        self._grid = self._yolox_grid(640)
        self._priors = {}

    # YOLOX-S (decode as in the OpenCV Zoo demo, Apache-2.0)
    @staticmethod
    def _yolox_grid(size):
        grids, strides = [], []
        for s in (8, 16, 32):
            n = size // s
            xv, yv = np.meshgrid(np.arange(n), np.arange(n))
            grids.append(np.stack((xv, yv), 2).reshape(-1, 2))
            strides.append(np.full((n * n, 1), s))
        return np.concatenate(grids).astype(np.float32), np.concatenate(strides).astype(np.float32)

    def _yolox_once(self, img):
        h, w = img.shape[:2]
        r = min(640 / h, 640 / w)
        pad = np.full((640, 640, 3), 114, np.float32)
        rs = cv2.resize(cv2.cvtColor(img, cv2.COLOR_BGR2RGB), (int(w * r), int(h * r)))
        pad[:rs.shape[0], :rs.shape[1]] = rs
        self.yolox.setInput(np.transpose(pad, (2, 0, 1))[None])
        out = self.yolox.forward(self.yolox.getUnconnectedOutLayersNames())[0][0].copy()
        grid, stride = self._grid
        out[:, :2] = (out[:, :2] + grid) * stride
        out[:, 2:4] = np.exp(out[:, 2:4]) * stride
        scores = out[:, 4:5] * out[:, 5:]
        cls = scores.argmax(1)
        best = scores.max(1)
        sel = best >= YOLOX_SCORE
        res = []
        for (cx, cy, bw, bh), s, c in zip(out[sel, :4], best[sel], cls[sel]):
            name = COCO[int(c)]
            if name in MOVERS:
                res.append((name, float(s), [(cx - bw / 2) / r, (cy - bh / 2) / r, bw / r, bh / r]))
        return res

    def objects(self, img):
        """People, vehicles and animals: list of (class, score, [x, y, w, h])."""
        h, w = img.shape[:2]
        found = self._yolox_once(img)
        if max(h, w) > 1000:
            # 2x2 tiles with overlap find small, distant people.
            th, tw = int(h * 0.6), int(w * 0.6)
            for y0 in (0, h - th):
                for x0 in (0, w - tw):
                    for name, s, (x, y, bw, bh) in self._yolox_once(img[y0:y0 + th, x0:x0 + tw]):
                        found.append((name, s, [x + x0, y + y0, bw, bh]))
        out = []
        for name in {f[0] for f in found}:
            group = [f for f in found if f[0] == name]
            for i in _nms([g[2] for g in group], [g[1] for g in group], YOLOX_NMS):
                out.append(group[i])
        return out

    def faces(self, img):
        """Face boxes: list of (score, [x, y, w, h]) in image pixels."""
        h, w = img.shape[:2]
        scale = max(1.0, FACE_MIN_SCALE_SIDE / max(h, w))
        work = cv2.resize(img, (round(w * scale), round(h * scale)), interpolation=cv2.INTER_CUBIC) \
            if scale > 1 else img
        wh, ww = work.shape[:2]
        boxes, scores = [], []
        # Pass 1: whole image (big, close faces). Pass 2: tiles (small faces).
        passes = [(0, 0, ww, wh, min(1.0, 1280 / max(ww, wh)))]
        passes += [(x, y, min(FACE_TILE, ww), min(FACE_TILE, wh), 1.0) for x, y in _tiles(ww, wh, FACE_TILE)]
        for x0, y0, tw, th, s in passes:
            crop = work[y0:y0 + th, x0:x0 + tw]
            if s != 1.0:
                crop = cv2.resize(crop, (round(tw * s), round(th * s)), interpolation=cv2.INTER_AREA)
            self.face.setInputSize((crop.shape[1], crop.shape[0]))
            _, det = self.face.detect(crop)
            if det is None:
                continue
            for row in det:
                x, y, bw, bh = row[:4] / s
                boxes.append([(x + x0) / scale, (y + y0) / scale, bw / scale, bh / scale])
                scores.append(float(row[14]))
        limit = FACE_MAX_SHARE * min(h, w)
        return [(scores[i], boxes[i]) for i in _nms(boxes, scores, 0.3)
                if boxes[i][2] <= limit and 0.7 <= boxes[i][3] / max(boxes[i][2], 1e-6) <= 2.5]

    def _plate_priors(self, w, h):
        key = (w, h)
        if key not in self._priors:
            # Prior boxes in the same order as the OpenCV Zoo demo:
            # level, then row i, then column j, then anchor size.
            min_sizes = [[10, 16, 24], [32, 48], [64, 96], [128, 192, 256]]
            steps = [8, 16, 32, 64]
            f2 = [int(int((h + 1) / 2) / 2), int(int((w + 1) / 2) / 2)]
            f3 = [f2[0] // 2, f2[1] // 2]
            f4 = [f3[0] // 2, f3[1] // 2]
            f5 = [f4[0] // 2, f4[1] // 2]
            f6 = [f5[0] // 2, f5[1] // 2]
            parts = []
            for k, (fh, fw) in enumerate([f3, f4, f5, f6]):
                i, j, m = np.meshgrid(np.arange(fh), np.arange(fw), np.array(min_sizes[k]), indexing="ij")
                parts.append(np.stack([(j + 0.5) * steps[k] / w, (i + 0.5) * steps[k] / h,
                                       m / w, m / h], -1).reshape(-1, 4))
            self._priors[key] = np.concatenate(parts).astype(np.float32)
        return self._priors[key]

    def _plates_once(self, img):
        h, w = img.shape[:2]
        w32, h32 = (w // 32) * 32, (h // 32) * 32
        if w32 < 64 or h32 < 64:
            return []
        img = cv2.resize(img, (w32, h32))
        self.plate.setInput(cv2.dnn.blobFromImage(img))
        loc, conf, iou = self.plate.forward(["loc", "conf", "iou"])
        pri = self._plate_priors(w32, h32)
        score = np.sqrt(conf[:, 1] * np.clip(iou[:, 0], 0, 1))
        sel = score >= PLATE_SCORE
        if not sel.any():
            return []
        loc, pri, score = loc[sel], pri[sel], score[sel]
        corners = [(pri[:, 0:2] + loc[:, k:k + 2] * 0.1 * pri[:, 2:4]) * (w32, h32) for k in (4, 6, 10, 12)]
        pts = np.stack(corners, 1)                               # n x 4 x 2
        x0, y0 = pts[:, :, 0].min(1), pts[:, :, 1].min(1)
        x1, y1 = pts[:, :, 0].max(1), pts[:, :, 1].max(1)
        sx, sy = w / w32, h / h32
        return [(float(s), [a * sx, b * sy, (c - a) * sx, (d - b) * sy])
                for s, a, b, c, d in zip(score, x0, y0, x1, y1)]

    def _plates_scaled(self, crop, x0, y0, long_side):
        h, w = crop.shape[:2]
        s = long_side / max(h, w)
        work = cv2.resize(crop, (max(1, round(w * s)), max(1, round(h * s))),
                          interpolation=cv2.INTER_AREA if s < 1 else cv2.INTER_CUBIC)
        return [(sc, [x / s + x0, y / s + y0, bw / s, bh / s]) for sc, (x, y, bw, bh) in self._plates_once(work)]

    def plates(self, img, vehicles=()):
        """Plate boxes: list of (score, [x, y, w, h]).

        One pass over the whole image (long side 1280), then one pass on a
        crop around each of the 12 largest vehicles (plates are on vehicles,
        and a crop makes a distant plate big enough to find)."""
        h, w = img.shape[:2]
        found = self._plates_scaled(img, 0, 0, min(1280, max(h, w)))
        for x, y, bw, bh in sorted(vehicles, key=lambda b: -b[2] * b[3])[:12]:
            x0, y0, cw, ch = grow([x, y, bw, bh], 1.2, 1.2, w, h)
            if cw < 24 or ch < 16:
                continue
            found += self._plates_scaled(img[y0:y0 + ch, x0:x0 + cw], x0, y0, min(640, max(320, max(cw, ch))))
        # Sanity filter: the detector sometimes returns huge boxes over sky or
        # walls. A real plate is small, wider than tall, and sits on a vehicle
        # (or is a very confident detection).
        near_vehicle = [grow(b, 1.3, 1.3) for b in vehicles]
        ok = []
        for sc, (x, y, bw, bh) in found:
            if not (8 <= bw <= 0.25 * w and 4 <= bh <= 0.15 * h and 1.2 <= bw / max(bh, 1e-6) <= 6.5):
                continue
            cx, cy = x + bw / 2, y + bh / 2
            on_vehicle = any(vx <= cx <= vx + vw and vy <= cy <= vy + vh for vx, vy, vw, vh in near_vehicle)
            if on_vehicle or sc >= PLATE_SCORE_ALONE:
                ok.append((sc, [x, y, bw, bh]))
        keep = _nms([f[1] for f in ok], [f[0] for f in ok], 0.3)
        return [ok[i] for i in keep]


# ---- blur and mask -------------------------------------------------------

def grow(box, fx, fy=None, w=None, h=None):
    """Enlarge [x, y, w, h] around its center by fx (and fy), clipped to the image."""
    fy = fx if fy is None else fy
    x, y, bw, bh = box
    cx, cy = x + bw / 2, y + bh / 2
    nw, nh = bw * fx, bh * fy
    x0, y0, x1, y1 = cx - nw / 2, cy - nh / 2, cx + nw / 2, cy + nh / 2
    if w is not None:
        x0, x1 = max(0, x0), min(w, x1)
    if h is not None:
        y0, y1 = max(0, y0), min(h, y1)
    return [int(np.floor(x0)), int(np.floor(y0)), int(np.ceil(x1 - x0)), int(np.ceil(y1 - y0))]


def blur_regions(dets, w, h):
    """The boxes to blur, as (reason, [x, y, w, h]) in pixels."""
    out = []
    for score, box in dets["faces"]:
        out.append(("face", grow(box, 1.6, 1.7, w, h)))
    for name, score, (x, y, bw, bh) in dets["objects"]:
        if name in PERSON and bh >= 24:
            out.append(("head", grow([x, y, bw, bh * 0.22], 1.15, 1.2, w, h)))
        if name in VEHICLES and bh >= 16:
            out.append(("plate_band", grow([x, y + bh * 0.45, bw, bh * 0.55], 1.05, 1.05, w, h)))
    for score, box in dets["plates"]:
        out.append(("plate", grow(box, 1.5, 1.8, w, h)))
    return [(r, b) for r, b in out if b[2] > 1 and b[3] > 1]


def pixelate_blur(img, box):
    """Strong anonymizing blur in place: coarse pixelation, then a Gaussian blur."""
    x, y, bw, bh = box
    roi = img[y:y + bh, x:x + bw]
    if roi.size == 0:
        return
    block = max(4, min(bw, bh) // 6)
    small = cv2.resize(roi, (max(1, bw // block), max(1, bh // block)), interpolation=cv2.INTER_AREA)
    roi[:] = cv2.resize(small, (bw, bh), interpolation=cv2.INTER_NEAREST)
    k = max(3, (min(bw, bh) // 4) | 1)
    roi[:] = cv2.GaussianBlur(roi, (k, k), 0)


def operator_mask(view, size):
    """For a 360 view: True where the ray points more than OPERATOR_PITCH below the horizon."""
    import intake_video
    _, pitch_map = intake_video.ray_angles(size, view["pitch"], view["fov"])
    return pitch_map < np.radians(OPERATOR_PITCH)


def build_mask(dets, regions, w, h, view=None):
    mask = np.full((h, w), 255, np.uint8)
    for name, score, box in dets["objects"]:
        x, y, bw, bh = grow(box, 1.1, 1.08, w, h)
        mask[y:y + bh, x:x + bw] = 0
    for _, (x, y, bw, bh) in regions:
        mask[y:y + bh, x:x + bw] = 0
    if view is not None and w == h:
        mask[operator_mask(view, w)] = 0
    return mask


def detect(det, img):
    objects = det.objects(img)
    vehicles = [box for name, _, box in objects if name in VEHICLES]
    return {"faces": det.faces(img), "objects": objects, "plates": det.plates(img, vehicles)}


def summarize(dets, regions, mask):
    count = {}
    for name, _, _ in dets["objects"]:
        count[name] = count.get(name, 0) + 1
    return {
        "faces": len(dets["faces"]), "plates": len(dets["plates"]),
        "persons": count.get("person", 0), "vehicles": sum(count.get(c, 0) for c in VEHICLES),
        "objects": count,
        "blur_boxes": [{"reason": r, "box": b} for r, b in regions],
        "usable_share": round(float((mask > 0).mean()), 4),
    }


# ---- EXIF on the blurred copies -----------------------------------------

def copy_exif(pairs, exe):
    """Copy metadata from each original to its blurred JPEG (GPS kept; it is needed
    for alignment). Embedded thumbnails/previews and maker notes are NOT copied,
    because they can hold an unblurred small copy of the photo. Orientation is
    set to 1 because the blurred pixels are already upright.

    pairs: list of (original path, blurred path). Returns number of failures."""
    if not pairs:
        return 0
    with tempfile.NamedTemporaryFile("w", suffix=".args", delete=False, encoding="utf-8") as f:
        for src, dst in pairs:
            for a in ["-TagsFromFile", str(src), "-all:all", "--ThumbnailImage", "--PreviewImage",
                      "--JpgFromRaw", "--OtherImage", "--MakerNotes",
                      "-Orientation#=1", "-overwrite_original", "-charset", "filename=utf8", str(dst),
                      "-execute"]:
                f.write(a + "\n")
        argfile = f.name
    try:
        res = subprocess.run([exe, "-q", "-q", "-@", argfile], capture_output=True, timeout=3600)
    finally:
        Path(argfile).unlink(missing_ok=True)
    err = res.stderr.decode("utf-8", errors="replace")
    return sum(1 for line in err.splitlines() if line.lower().startswith("error"))
