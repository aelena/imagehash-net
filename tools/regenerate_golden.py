"""Regenerate tests/NetImgHash.Tests/TestData/expected_hashes.json from the reference.

The manifest is the correctness anchor for this library: every value in it is what
the Python ``imagehash`` package produces for the image, and the .NET code must match
it bit for bit. Never edit the hashes by hand; run this instead.

    .venv/Scripts/python tools/regenerate_golden.py        # Windows
    .venv/bin/python tools/regenerate_golden.py            # Linux/macOS

Requires ``imagehash`` (which brings ``pillow``, ``numpy`` and ``scipy``).
"""

from __future__ import annotations

import json
from pathlib import Path

import numpy
import PIL
import imagehash
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "tests" / "NetImgHash.Tests" / "TestData"
MANIFEST = DATA / "expected_hashes.json"


def main() -> None:
    previous = json.loads(MANIFEST.read_text(encoding="utf-8"))
    notes = {e["file"]: e.get("notes") for e in previous["entries"]}

    entries = []
    for entry in previous["entries"]:
        name = entry["file"]
        with Image.open(DATA / "Images" / name) as image:
            entries.append(
                {
                    "file": name,
                    "averageHash": str(imagehash.average_hash(image)),
                    "differenceHash": str(imagehash.dhash(image)),
                    "perceptualHash": str(imagehash.phash(image)),
                    "notes": notes.get(name),
                }
            )

    manifest = {
        "generatedWith": {
            "imagehash": imagehash.__version__,
            "pillow": PIL.__version__,
            "numpy": numpy.__version__,
        },
        "entries": entries,
    }

    MANIFEST.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")

    for old, new in zip(previous["entries"], entries):
        for key in ("averageHash", "differenceHash", "perceptualHash"):
            if old.get(key) != new[key]:
                print(f"{new['file']:24} {key:15} {old.get(key)} -> {new[key]}")
    print(f"wrote {MANIFEST.relative_to(ROOT)} ({len(entries)} entries)")


if __name__ == "__main__":
    main()
