"""
Generate GeneIcon_WarriorHead.png from NecronWarriorHead_south.png.

Crops the skull to its non-transparent bounding box, adds uniform padding to
leave breathing room, then resamples the result to a 128×128 gene-icon canvas
(matching GeneIcon_OverlordHead, GeneIcon_LychguardHead, etc.).

Output: Textures/GW40K/NecronWarrior/Head/GeneIcon_WarriorHead.png

Requires: pip install Pillow

Run from repo root:
  python _tools/generate_warrior_head_gene_icon.py
"""
from __future__ import annotations

import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("Install Pillow: python -m pip install Pillow", file=sys.stderr)
    sys.exit(1)

ICON_SIZE   = 128        # target canvas (px)
PADDING_PCT = 0.10       # fraction of tight bbox to add as uniform border

REPO   = Path(__file__).resolve().parent.parent
SOURCE = REPO / "Textures" / "Things" / "Pawn" / "Mechanoid" / "Warrior" / "NecronWarriorHead_south.png"
OUT_DIR = REPO / "Textures" / "GW40K" / "NecronWarrior" / "Head"
OUTPUT  = OUT_DIR / "GeneIcon_WarriorHead.png"


def tight_bbox(img: Image.Image, alpha_threshold: int = 10):
    """Return (left, upper, right, lower) of non-transparent content."""
    r, g, b, a = img.split()
    mask = a.point(lambda v: 255 if v > alpha_threshold else 0)
    return mask.getbbox()


def generate() -> None:
    if not SOURCE.is_file():
        raise SystemExit(f"Source not found: {SOURCE}")

    src = Image.open(SOURCE).convert("RGBA")
    bbox = tight_bbox(src)
    if bbox is None:
        raise SystemExit("Source image is fully transparent — nothing to crop.")

    l, u, r, b = bbox
    content_w = r - l
    content_h = b - u

    pad = int(max(content_w, content_h) * PADDING_PCT)
    padded_size = max(content_w, content_h) + pad * 2

    # Crop tight, then place centered on a padded square canvas
    cropped = src.crop(bbox)
    square  = Image.new("RGBA", (padded_size, padded_size), (0, 0, 0, 0))
    paste_x = (padded_size - content_w) // 2
    paste_y = (padded_size - content_h) // 2
    square.paste(cropped, (paste_x, paste_y))

    icon = square.resize((ICON_SIZE, ICON_SIZE), Image.LANCZOS)

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    icon.save(OUTPUT, optimize=True)
    print(f"Saved {OUTPUT}  ({ICON_SIZE}×{ICON_SIZE} RGBA)")
    print(f"  Source content: {content_w}×{content_h}px at ({l},{u})")
    print(f"  Padding: {pad}px -> {padded_size}x{padded_size}px before resize")


if __name__ == "__main__":
    generate()
