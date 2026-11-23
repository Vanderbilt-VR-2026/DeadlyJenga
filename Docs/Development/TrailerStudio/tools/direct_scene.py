"""Turn a text prompt into an editable Theatre recipe and a directing report."""

import argparse
import json
from pathlib import Path
from tempfile import TemporaryDirectory

from direct_prompt import compile_brief
from stage_prompt import ROOT, stage


def direct(prompt, output):
    output = output.resolve()
    if output.is_relative_to(ROOT / "Assets") or output.exists():
        raise ValueError("Choose a new output directory outside Assets.")
    brief = compile_brief(prompt)
    recipe, report = stage(brief)
    output.parent.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(dir=output.parent) as temp:
        bundle = Path(temp) / "bundle"
        bundle.mkdir()
        for name, data in (("brief", brief), ("recipe", recipe), ("staging", report)):
            (bundle / f"{name}.json").write_text(json.dumps(data, indent=2, allow_nan=False) + "\n", encoding="utf-8")
        bundle.rename(output)
    return output


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("prompt_file", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    try:
        print(direct(args.prompt_file.read_text(encoding="utf-8-sig"), args.output))
    except (OSError, ValueError) as error:
        parser.exit(1, f"{error}\n")
