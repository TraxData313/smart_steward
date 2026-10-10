r"""Copies the chosen F12 shots of the Party Steward window into Screenshots\ - the Workshop gallery and README images.

    python tools\make_screenshots.py                  # from Steam's screenshot folder (the defaults below)
    python tools\make_screenshots.py --src D:\shots   # or wherever the originals are

Each shot (3440x1440, taken 2026-10-10 in Onira) is cropped to the window alone - the town menu, the message log
(it showed a Windows user path) and other mods' widgets stay out - and saved as JPEG under 1 MB (Steam's cap per
image), stepping the quality down if needed. The numbered names are the gallery order, most telling first.
Not used: the five battle shots of that evening (another mod) and 20261010205912 (the Horses part, already in 02).
Needs Pillow.
"""
from __future__ import annotations

import argparse
import io
import sys
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parent.parent
OUT = REPO / "Screenshots"
STEAM_SHOTS = Path(r"C:\Program Files (x86)\Steam\userdata\258577504\760\remote\261550\screenshots")
LIMIT = 1024 * 1024
WINDOW = (660, 74, 2778, 1366)          # the Party Steward window in a 3440x1440 shot, its gold-grey border included

SHOTS = [  # (original, published name)
    ("20261010205908_1.jpg", "01_suggestion_food.jpg"),
    ("20261010205832_1.jpg", "02_suggestion_troops_horses.jpg"),
    ("20261010205841_1.jpg", "03_instructions_general_money.jpg"),
    ("20261010205846_1.jpg", "04_instructions_goals_food.jpg"),
    ("20261010205850_1.jpg", "05_instructions_prices_pack_animals.jpg"),
    ("20261010205853_1.jpg", "06_instructions_mounts.jpg"),
    ("20261010205856_1.jpg", "07_instructions_prisoners_loot_tavern.jpg"),
]


def save_under_limit(img: Image.Image, path: Path, quality: int = 90) -> tuple[int, int]:
    while True:
        buf = io.BytesIO()
        img.save(buf, "JPEG", quality=quality, optimize=True, progressive=True)
        if buf.tell() < LIMIT or quality <= 60:
            break
        quality -= 4
    if buf.tell() >= LIMIT:
        sys.exit(f"{path.name} is still {buf.tell()} bytes at quality {quality}")
    path.write_bytes(buf.getvalue())
    return buf.tell(), quality


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--src", type=Path, default=STEAM_SHOTS)
    args = ap.parse_args()
    OUT.mkdir(exist_ok=True)
    for src_name, out_name in SHOTS:
        img = Image.open(args.src / src_name).convert("RGB")
        if img.size != (3440, 1440):
            sys.exit(f"{src_name} is {img.size}, expected 3440x1440 (WINDOW is in those pixels)")
        size, q = save_under_limit(img.crop(WINDOW), OUT / out_name)
        print(f"{out_name}: {WINDOW[2] - WINDOW[0]}x{WINDOW[3] - WINDOW[1]}, {size:,} bytes, q{q}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
