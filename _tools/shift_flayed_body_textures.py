"""
Shift Flayed One body (trophy-flesh) PNGs down within the same canvas.

Before modifying each file, copies the current file into:
  Textures/GW40K/FlayedOne/Body/_backup_before_shift/<same filename>

Re-running stacks another shift (backup then reflects pre-run state). Restore from
_backup_before_shift/ or git checkout the three PNGs to reset.

Requires: pip install Pillow

Run from repo root:
  python _tools/shift_flayed_body_textures.py
"""
from __future__ import annotations

import shutil
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("Install Pillow: python -m pip install Pillow", file=sys.stderr)
    sys.exit(1)

# Pixels to move art toward feet (positive = shift raster down in image space).
SHIFT_DOWN_PX = 10

REPO = Path(__file__).resolve().parent.parent
BODY_DIR = REPO / "Textures" / "GW40K" / "FlayedOne" / "Body"
BACKUP_DIR = BODY_DIR / "_backup_before_shift"

FILES = [
    "Naked_Male_south.png",
    "Naked_Male_north.png",
    "Naked_Male_east.png",
]


def backup_file(src: Path, backup_root: Path) -> Path:
    backup_root.mkdir(parents=True, exist_ok=True)
    dst = backup_root / src.name
    shutil.copy2(src, dst)
    return dst


def shift_down(path: Path, dy: int) -> None:
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    if dy <= 0 or dy >= h:
        raise ValueError(f"bad dy={dy} for {path} size={w}x{h}")
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    # Rows 0..h-dy-1 of source pasted at y=dy; top dy rows transparent (loses bottom dy rows of source).
    src = im.crop((0, 0, w, h - dy))
    out.paste(src, (0, dy))
    out.save(path, optimize=True)
    print(f"OK {path.name}: shifted down {dy}px (backup in {BACKUP_DIR.name}/)")


def main() -> None:
    for name in FILES:
        p = BODY_DIR / name
        if not p.is_file():
            raise SystemExit(f"missing {p}")
        backed = backup_file(p, BACKUP_DIR)
        print(f"Backup: {backed}")
        shift_down(p, SHIFT_DOWN_PX)


if __name__ == "__main__":
    main()
