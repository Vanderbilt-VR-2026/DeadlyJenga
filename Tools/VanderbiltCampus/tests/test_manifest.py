"""Manifest: add, verify, and the raw/ write-once rule, on a temporary archive."""
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "pipeline"))
import manifest  # noqa: E402


def test_add_and_verify(tmp_path):
    f = tmp_path / "raw" / "osm" / "a.json"
    f.parent.mkdir(parents=True)
    f.write_text("{}")
    manifest.add_file(f, "test", "CC0", 0, root=tmp_path)
    ok, problems = manifest.verify(root=tmp_path)
    assert ok == 1 and problems == []


def test_verify_finds_changed_and_missing(tmp_path):
    a = tmp_path / "derived" / "a.txt"
    b = tmp_path / "derived" / "b.txt"
    a.parent.mkdir(parents=True)
    a.write_text("one")
    b.write_text("two")
    manifest.add_file(a, root=tmp_path)
    manifest.add_file(b, root=tmp_path)
    a.write_text("changed")
    b.unlink()
    ok, problems = manifest.verify(root=tmp_path)
    assert ok == 0
    assert sorted(problems) == [("changed", "derived/a.txt"), ("missing", "derived/b.txt")]


def test_raw_is_write_once(tmp_path):
    f = tmp_path / "raw" / "x.bin"
    f.parent.mkdir(parents=True)
    f.write_bytes(b"1")
    manifest.add_file(f, root=tmp_path)
    f.write_bytes(b"2")
    with pytest.raises(SystemExit):
        manifest.add_file(f, root=tmp_path)
