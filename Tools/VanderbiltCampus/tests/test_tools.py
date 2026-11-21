"""Checks that every pipeline tool is installed (runs check_tools.py)."""
import subprocess
import sys
from pathlib import Path

CHECK_TOOLS = Path(__file__).resolve().parent.parent / "pipeline" / "check_tools.py"


def test_all_tools_installed():
    result = subprocess.run([sys.executable, str(CHECK_TOOLS)],
                            capture_output=True, text=True, timeout=600)
    print(result.stdout)
    assert result.returncode == 0, result.stdout + result.stderr
