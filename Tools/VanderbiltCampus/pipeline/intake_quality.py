"""Intake step 5: quality flags. Photos are flagged, never deleted.

Per image (measure):
- sharpness: variance of the Laplacian on a gray copy resized to a fixed
  long side (1024 px), so big and small images compare fairly;
- exposure: share of nearly white pixels (>= 250) in the lower 75% of the
  frame (the top is often sky) and share of nearly black pixels (<= 5),
  plus mean brightness;
- a 64-bit perceptual hash (DCT of a 32x32 gray thumbnail).

Per set (flag_set):
- blurry:      sharpness < BLUR_ABS, or < BLUR_REL x the set median
- overexposed: white share > 5%
- underexposed: black share > 15% or mean brightness < 40 (street scenes
               with dark cars and shadows often have 5-10% black and look fine)
- duplicate:   same content hash as an earlier image, or hash distance
               <= DUP_HAMMING and (time gap < 1 s or baseline < 0.15 m)
- tiny:        file < 50 KB or short side < 640 px
"""
import cv2
import numpy as np

LONG_SIDE = 1024
BLUR_ABS = 20.0
BLUR_REL = 0.35
CLIP_HI = 0.05
CLIP_LO = 0.15
MEAN_LO = 40.0
DUP_HAMMING = 8
DUP_SECONDS = 1.0
DUP_BASELINE_M = 0.15
TINY_BYTES = 50_000
TINY_SIDE = 640

PARAMS = {"long_side": LONG_SIDE, "blur_abs": BLUR_ABS, "blur_rel_to_median": BLUR_REL,
          "clip_hi_share": CLIP_HI, "clip_lo_share": CLIP_LO, "mean_lo": MEAN_LO,
          "dup_hamming": DUP_HAMMING, "dup_seconds": DUP_SECONDS, "dup_baseline_m": DUP_BASELINE_M,
          "tiny_bytes": TINY_BYTES, "tiny_side": TINY_SIDE}


def phash(gray):
    """64-bit perceptual hash as a 16-character hex string."""
    small = cv2.resize(gray, (32, 32), interpolation=cv2.INTER_AREA).astype(np.float32)
    d = cv2.dct(small)[:8, :8].flatten()
    bits = d > np.median(d[1:])
    return "%016x" % int("".join("1" if b else "0" for b in bits), 2)


def hamming(a, b):
    return bin(int(a, 16) ^ int(b, 16)).count("1")


def measure(img):
    """Quality numbers for one upright BGR image."""
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY) if img.ndim == 3 else img
    h, w = gray.shape
    s = LONG_SIDE / max(h, w)
    small = cv2.resize(gray, (max(1, round(w * s)), max(1, round(h * s))), interpolation=cv2.INTER_AREA)
    lap = float(cv2.Laplacian(small, cv2.CV_64F).var())
    lower = gray[h // 4:, :]
    return {
        "sharpness": round(lap, 2),
        "white_share": round(float((lower >= 250).mean()), 4),
        "black_share": round(float((gray <= 5).mean()), 4),
        "mean": round(float(gray.mean()), 1),
        "phash": phash(gray),
        "width": int(w), "height": int(h),
    }


def flag_set(items):
    """Set rec['quality']['flags'] for every item. Items need 'quality', 'name',
    and may have 't', 'campus', 'size', 'sha256', 'view' (360 views).

    Returns a summary dict with the set's sharpness percentiles."""
    measured = [r for r in items if r.get("quality") and "sharpness" in r["quality"]]
    sharp = np.array([r["quality"]["sharpness"] for r in measured]) if measured else np.array([0.0])
    median = float(np.median(sharp))
    blur_limit = max(BLUR_ABS, BLUR_REL * median)
    for r in items:
        q = r.setdefault("quality", {})
        flags = []
        if "sharpness" not in q:
            flags.append("unreadable")
        else:
            if q["sharpness"] < blur_limit:
                flags.append("blurry")
            if q["white_share"] > CLIP_HI:
                flags.append("overexposed")
            if q["black_share"] > CLIP_LO or q["mean"] < MEAN_LO:
                flags.append("underexposed")
            if (r.get("size") or 0) < TINY_BYTES or min(q["width"], q["height"]) < TINY_SIDE:
                flags.append("tiny")
        q["flags"] = flags
    _flag_duplicates(measured)
    return {"sharpness_p10": round(float(np.percentile(sharp, 10)), 2),
            "sharpness_median": round(median, 2), "blur_limit": round(blur_limit, 2)}


def _baseline(a, b):
    ca, cb = a.get("campus"), b.get("campus")
    if not ca or not cb:
        return None
    return float(np.hypot(ca["X"] - cb["X"], ca["Y"] - cb["Y"]))


def _same_group(a, b):
    """360 views only compare with views looking the same way from the same video."""
    va, vb = a.get("view"), b.get("view")
    if (va is None) != (vb is None):
        return False
    if va is None:
        return True
    return va["video"] == vb["video"] and va["yaw"] == vb["yaw"] and va["pitch"] == vb["pitch"]


def _flag_duplicates(items):
    """Mark the later of two near-identical images as 'duplicate' (the first is kept)."""
    order = sorted(items, key=lambda r: (r.get("t") is None, r.get("t") or 0.0, r["name"]))
    hashes = np.array([int(r["quality"]["phash"], 16) for r in order], dtype=np.uint64)
    seen_sha = {}
    for j, b in enumerate(order):
        sha = b.get("sha256")
        if sha and sha in seen_sha:
            b["quality"]["flags"].append("duplicate")
            b["quality"]["duplicate_of"] = seen_sha[sha]
            continue
        if sha:
            seen_sha[sha] = b["name"]
        if j == 0:
            continue
        x = np.bitwise_xor(hashes[:j], hashes[j])
        dist = np.unpackbits(x.view(np.uint8).reshape(-1, 8), axis=1).sum(axis=1)   # bit counts
        for i in np.nonzero(dist <= DUP_HAMMING)[0][::-1]:       # newest first
            a = order[int(i)]
            if not _same_group(a, b) or "duplicate" in a["quality"]["flags"]:
                continue
            close_time = a.get("t") is not None and b.get("t") is not None and abs(b["t"] - a["t"]) < DUP_SECONDS
            base = _baseline(a, b)
            close_space = base is not None and base < DUP_BASELINE_M
            if b.get("view") is not None:
                # 360 frames are spaced on purpose; only nearly identical ones
                # (camera standing still) count.
                close_time = close_time and dist[int(i)] <= 2
            if close_time or close_space:
                b["quality"]["flags"].append("duplicate")
                b["quality"]["duplicate_of"] = a["name"]
                break
