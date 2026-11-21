"""Check that every tool the campus pipeline needs is installed.

It imports each Python package, runs each outside tool's version command
(paths come from config/local.json) and prints a table.
Exits with code 1 if anything required is missing or broken.

Usage:
    .venv\\Scripts\\python Tools/VanderbiltCampus/pipeline/check_tools.py
"""
import argparse
import importlib
import importlib.metadata
import os
import re
import shlex
import shutil
import subprocess
import sys

import common

# (import name, package name on PyPI)
PYTHON_PACKAGES = [
    ("numpy", "numpy"),
    ("scipy", "scipy"),
    ("laspy", "laspy"),
    ("lazrs", "lazrs"),
    ("pyproj", "pyproj"),
    ("shapely", "shapely"),
    ("rasterio", "rasterio"),
    ("open3d", "open3d"),
    ("trimesh", "trimesh"),
    ("cv2", "opencv-python-headless"),
    ("pycolmap", "pycolmap"),
    ("PIL", "pillow"),
    ("requests", "requests"),
    ("pytest", "pytest"),
    ("matplotlib", "matplotlib"),
    ("skimage", "scikit-image"),
    ("sklearn", "scikit-learn"),
    ("networkx", "networkx"),
    ("pygltflib", "pygltflib"),
]

# (name, key in local.json or None for PATH, arguments, regex that finds the version)
EXTERNAL_TOOLS = [
    ("python", "python", ["--version"], r"Python (\S+)"),
    ("pdal", "pdal", ["--version"], r"pdal (\S+)"),
    ("colmap", "colmap", ["version"], r"COLMAP (\S+)"),
    ("exiftool", "exiftool", ["-ver"], r"(\d+\.\d+)"),
    ("blender", "blender", ["-b", "--version"], r"Blender (\S+)"),
    ("ffmpeg", None, ["-version"], r"ffmpeg version (\S+)"),
]


def check_package(import_name, dist_name):
    """Import one package. Returns (ok, version or error text)."""
    try:
        importlib.import_module(import_name)
    except Exception as e:  # any import failure counts as missing
        return False, f"import failed: {e}"
    try:
        return True, importlib.metadata.version(dist_name)
    except importlib.metadata.PackageNotFoundError:
        return True, "unknown"


def command_from_config(value):
    """Turn a local.json value into a command list.

    A value that is a path to a file is used as is (it may contain spaces).
    Anything else is split into words, e.g. the micromamba run form for PDAL.
    """
    if isinstance(value, list):
        return value
    if os.path.isfile(value):
        return [value]
    return [part.strip('"') for part in shlex.split(value, posix=False)]


def check_tool(name, key, args, pattern, config):
    """Run one tool's version command. Returns (ok, version or error text)."""
    if key is None:
        exe = shutil.which(name)
        if not exe:
            return False, "not found on PATH"
        command = [exe]
    else:
        value = config.get(key)
        if not value:
            return False, f"'{key}' missing in config/local.json"
        command = command_from_config(value)
        if not os.path.isfile(command[0]):
            return False, f"not found: {command[0]}"
    try:
        result = subprocess.run(command + args, capture_output=True, text=True,
                                timeout=180, errors="replace")
    except (OSError, subprocess.TimeoutExpired) as e:
        return False, f"could not run: {e}"
    output = result.stdout + result.stderr
    match = re.search(pattern, output)
    if result.returncode != 0 or not match:
        return False, f"exit code {result.returncode}: {output.strip()[:200]}"
    return True, match.group(1)


def main():
    parser = argparse.ArgumentParser(
        description="Check that the campus pipeline's Python packages and tools are installed.")
    parser.parse_args()

    config = common.local_config()
    rows = []  # (kind, name, ok, detail)

    for import_name, dist_name in PYTHON_PACKAGES:
        ok, detail = check_package(import_name, dist_name)
        rows.append(("python pkg", dist_name, ok, detail))

    # laspy needs the lazrs backend to read .laz files.
    try:
        import laspy
        ok = laspy.LazBackend.Lazrs.is_available()
    except Exception:
        ok = False
    rows.append(("python pkg", "laspy LAZ backend", ok, "lazrs" if ok else "not available"))

    for name, key, args, pattern in EXTERNAL_TOOLS:
        ok, detail = check_tool(name, key, args, pattern, config)
        rows.append(("tool", name, ok, detail))

    width = max(len(r[1]) for r in rows)
    print(f"{'KIND':<11} {'NAME':<{width}} {'OK':<4} VERSION / PROBLEM")
    for kind, name, ok, detail in rows:
        print(f"{kind:<11} {name:<{width}} {'yes' if ok else 'NO':<4} {detail}")

    missing = [r[1] for r in rows if not r[2]]
    if missing:
        print("\nMissing or broken: " + ", ".join(missing), file=sys.stderr)
        return 1
    print("\nAll required tools found.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
