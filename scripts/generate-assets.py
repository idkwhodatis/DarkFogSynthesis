#!/usr/bin/env python3
"""Render original SVG assets with Inkscape; validate checked-in PNGs with --check.

Requires Python 3.9+, Pillow, and (for rendering only) official Inkscape on PATH.
No network access, game resources, or SVG image editing is performed.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
NAMES = ("energy-analysis", "information-topology", "package-icon")
SIZE = (256, 256)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inspect(name: str) -> dict:
    source = ROOT / "assets/source" / f"{name}.svg"
    target = ROOT / "assets/generated" / f"{name}.png"
    with Image.open(target) as image:
        if image.format != "PNG" or image.size != SIZE or image.mode != "RGBA":
            raise ValueError(f"{target}: expected RGBA PNG, 256x256")
        pixels = list(image.get_flattened_data() if hasattr(image, "get_flattened_data") else image.getdata())
        alpha = [pixel[3] for pixel in pixels]
        transparent = sum(a == 0 for a in alpha)
        opaque = sum(a == 255 for a in alpha)
        if not transparent or not opaque:
            raise ValueError(f"{target}: needs both transparent and opaque pixels")
        if image.getpixel((0, 0))[3] != 0 or image.getpixel((255, 255))[3] != 0:
            raise ValueError(f"{target}: corners must be transparent")
        if name != "package-icon":
            if transparent < len(pixels) // 2:
                raise ValueError(f"{target}: tech emblem should have a transparent background")
            if any(rgb[:3] != (255, 255, 255) for rgb in pixels if rgb[3] > 0):
                raise ValueError(f"{target}: tech emblem foreground must be pure white")
        return {
            "source": source.relative_to(ROOT).as_posix(),
            "sourceSha256": sha256(source),
            "png": target.relative_to(ROOT).as_posix(),
            "pngSha256": sha256(target),
            "width": image.width,
            "height": image.height,
            "mode": image.mode,
            "transparentPixels": transparent,
            "opaquePixels": opaque,
            "antialiasedPixels": len(pixels) - transparent - opaque,
        }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="validate outputs and hashes without rendering")
    args = parser.parse_args()
    manifest_path = ROOT / "assets/generated/assets-manifest.json"
    if not args.check:
        inkscape = shutil.which("inkscape")
        if not inkscape:
            raise RuntimeError("Install official Inkscape or use --check with committed PNGs.")
        version = subprocess.check_output([inkscape, "--version"], text=True).strip()
        (ROOT / "assets/generated").mkdir(parents=True, exist_ok=True)
        for name in NAMES:
            subprocess.run([
                inkscape, str(ROOT / "assets/source" / f"{name}.svg"),
                "--export-type=png", "--export-width=256", "--export-height=256",
                "--export-background-opacity=0",
                f"--export-filename={ROOT / 'assets/generated' / (name + '.png')}",
            ], check=True)
        shutil.copyfile(ROOT / "assets/generated/package-icon.png", ROOT / "icon.png")
        report = {"schemaVersion": 1, "renderer": version, "assets": [inspect(n) for n in NAMES]}
        manifest_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    else:
        report = json.loads(manifest_path.read_text(encoding="utf-8"))
        if report.get("schemaVersion") != 1 or report.get("assets") != [inspect(n) for n in NAMES]:
            raise ValueError("Asset manifest does not match current SVG or PNG files; regenerate assets.")
    if sha256(ROOT / "icon.png") != sha256(ROOT / "assets/generated/package-icon.png"):
        raise ValueError("Root Thunderstore icon differs from the generated package icon.")
    print("PASS: 3 original 256x256 RGBA PNGs, transparency, white tech foregrounds, source/output hashes, package icon")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, RuntimeError, subprocess.CalledProcessError) as error:
        print(f"Asset validation failed: {error}", file=sys.stderr)
        raise SystemExit(1)
