"""Shared helpers for the Vanderbilt campus pipeline.

Every script imports this to find the config folder, the archive root
(data_root in config/local.json) and the manifest.
"""
import json
import os
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parent.parent          # Tools/VanderbiltCampus
CONFIG = TOOLS / "config"
REPO = TOOLS.parent.parent                               # repository root


def load_json(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def save_json(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2)
        f.write("\n")


def local_config():
    """Machine settings. local.json is git-ignored; local.example.json shows the keys."""
    path = CONFIG / "local.json"
    if not path.exists():
        sys.exit(f"Missing {path}. Copy local.example.json to local.json and set data_root.")
    return load_json(path)


def data_root():
    """The archive folder (outside git). VC_DATA_ROOT overrides it (used by tests)."""
    root = os.environ.get("VC_DATA_ROOT") or local_config()["data_root"]
    return Path(root)


def fail(message):
    """Print an error and exit non-zero, as every pipeline script must on failure."""
    print("ERROR: " + message, file=sys.stderr)
    sys.exit(1)
