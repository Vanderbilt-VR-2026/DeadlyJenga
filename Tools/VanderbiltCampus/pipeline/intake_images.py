"""Reading capture images for intake (Stage 2).

Every reader returns the picture "upright": the EXIF Orientation tag is
applied, so detectors see people the right way up. Derived copies are saved
upright too, with Orientation reset to 1 (see intake_privacy.copy_exif).

Formats:
  .jpg .jpeg .png .tif .tiff  OpenCV (applies EXIF orientation itself)
  .heic .heif                 pillow-heif (iPhone default format)
  .dng                        rawpy (camera white balance, 8-bit sRGB)
"""
from pathlib import Path

import cv2
import numpy as np

PHOTO_EXT = {".jpg", ".jpeg", ".png", ".tif", ".tiff", ".heic", ".heif", ".dng"}
VIDEO_EXT = {".mp4", ".mov", ".insv", ".webm", ".mkv"}
# Files that are never captures (made by operating systems).
JUNK_NAMES = {".ds_store", "thumbs.db", "desktop.ini"}


def is_photo(path):
    return Path(path).suffix.lower() in PHOTO_EXT


def is_video(path):
    return Path(path).suffix.lower() in VIDEO_EXT


def is_junk(path):
    p = Path(path)
    return p.name.lower() in JUNK_NAMES or p.name.startswith("._") or "__MACOSX" in p.parts


def _read_heic(path):
    import pillow_heif
    from PIL import Image, ImageOps
    pillow_heif.register_heif_opener()
    with Image.open(path) as im:
        im = ImageOps.exif_transpose(im).convert("RGB")
        return cv2.cvtColor(np.asarray(im), cv2.COLOR_RGB2BGR)


def _read_dng(path):
    import rawpy
    with rawpy.imread(str(path)) as raw:
        rgb = raw.postprocess(use_camera_wb=True, output_bps=8)   # rawpy applies the flip
    return cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR)


def read_upright(path):
    """Load an image as an upright BGR uint8 array, or None if it cannot be read."""
    path = Path(path)
    ext = path.suffix.lower()
    try:
        if ext in (".heic", ".heif"):
            return _read_heic(path)
        if ext == ".dng":
            return _read_dng(path)
        # np.fromfile + imdecode handles non-ASCII Windows paths. We decode the
        # stored pixels and apply the EXIF orientation ourselves, so the result
        # does not depend on the OpenCV version.
        data = np.fromfile(str(path), dtype=np.uint8)
        img = cv2.imdecode(data, cv2.IMREAD_COLOR | cv2.IMREAD_IGNORE_ORIENTATION)
        if img is None:
            return None
        return apply_orientation(img, exif_orientation(path))
    except Exception:
        return None


def exif_orientation(path):
    """The EXIF Orientation tag (1..8); 1 when missing."""
    try:
        from PIL import Image
        with Image.open(path) as im:
            return int(im.getexif().get(0x0112, 1) or 1)
    except Exception:
        return 1


def apply_orientation(img, orientation):
    """Turn stored pixels upright for an EXIF Orientation value."""
    o = orientation
    if o == 2:
        return cv2.flip(img, 1)
    if o == 3:
        return cv2.rotate(img, cv2.ROTATE_180)
    if o == 4:
        return cv2.flip(img, 0)
    if o == 5:
        return cv2.flip(cv2.rotate(img, cv2.ROTATE_90_CLOCKWISE), 1)
    if o == 6:
        return cv2.rotate(img, cv2.ROTATE_90_CLOCKWISE)
    if o == 7:
        return cv2.flip(cv2.rotate(img, cv2.ROTATE_90_COUNTERCLOCKWISE), 1)
    if o == 8:
        return cv2.rotate(img, cv2.ROTATE_90_COUNTERCLOCKWISE)
    return img


def write_jpeg(path, img, quality=95):
    """Write a JPEG (works with non-ASCII paths). Writes to .part, then renames."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    ok, buf = cv2.imencode(".jpg", img, [cv2.IMWRITE_JPEG_QUALITY, quality])
    if not ok:
        raise IOError(f"could not encode {path}")
    part = path.with_name(path.name + ".part")
    buf.tofile(str(part))
    part.replace(path)


def write_png(path, img):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    ok, buf = cv2.imencode(".png", img)
    if not ok:
        raise IOError(f"could not encode {path}")
    part = path.with_name(path.name + ".part")
    buf.tofile(str(part))
    part.replace(path)
