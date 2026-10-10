r"""Builds the Steam Workshop preview and the README cover from the real Party Steward window.

    python tools\make_thumbnail.py            # both
    python tools\make_thumbnail.py --only thumbnail
    python tools\make_thumbnail.py --only cover

1. Crops Screenshots\01_suggestion_food.jpg (written by make_screenshots.py) to the part each image shows.
2. Renders tools\preview_thumbnail.html (1024 x 1024) and tools\cover.html (1600 x 900) with headless Edge (or Chrome).
3. Saves Screenshots\preview_thumbnail.jpg (the Workshop preview, square, under Steam's 1 MB cap) and
   Screenshots\cover.jpg (the README image), stepping JPEG quality down until each is under 1 MB.
Style = the sibling mods' thumbnails: gold frame, Palatino small caps title over a dark fade, one tagline.
Work files (cropped panel, page, PNG) go to %TEMP%\smart_steward_images. Needs Pillow and Edge or Chrome.
"""
from __future__ import annotations

import argparse
import io
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parent.parent
SHOTS = REPO / "Screenshots"
SOURCE = SHOTS / "01_suggestion_food.jpg"          # 2118 x 1292, the window alone
WORK = Path(tempfile.gettempdir()) / "smart_steward_images"
LIMIT = 1024 * 1024
BROWSERS = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
]

# name: template, output, page size, crop box in SOURCE pixels (same aspect as the HTML's .art)
IMAGES = {
    "thumbnail": ("preview_thumbnail.html", "preview_thumbnail.jpg", (1024, 1024),
                  (20, 14, 1630, 1055)),    # title, tabs, Denari, Troops, Food rows, Horses; the Denari column last
    "cover": ("cover.html", "cover.jpg", (1600, 900),
              (29, 14, 2089, 883)),         # the whole width, title down to the Horses line
}


def render(name: str, browser: str) -> None:
    template, out_name, (w, h), box = IMAGES[name]
    panel = WORK / f"{name}_art.png"
    Image.open(SOURCE).convert("RGB").crop(box).save(panel)
    page = WORK / f"{name}.html"
    page.write_text((REPO / "tools" / template).read_text(encoding="utf-8").replace("ART_IMG", panel.as_uri()),
                    encoding="utf-8")
    png = WORK / f"{name}.png"
    png.unlink(missing_ok=True)
    subprocess.run([browser, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                    "--allow-file-access-from-files", f"--user-data-dir={WORK / 'profile'}",
                    f"--window-size={w},{h}", f"--screenshot={png}", page.as_uri()],
                   check=True, capture_output=True, timeout=120)
    img = Image.open(png).convert("RGB").crop((0, 0, w, h))
    for q in (92, 90, 88, 85, 82, 78, 74, 70):
        buf = io.BytesIO()
        img.save(buf, "JPEG", quality=q, optimize=True, progressive=True)
        if buf.tell() < LIMIT:
            break
    else:
        sys.exit(f"{out_name} does not fit under 1 MB")
    (SHOTS / out_name).write_bytes(buf.getvalue())
    print(f"Screenshots\\{out_name}: {w}x{h}, {buf.tell():,} bytes, q{q}")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", choices=sorted(IMAGES))
    args = ap.parse_args()
    if not SOURCE.exists():
        sys.exit(f"{SOURCE} missing - run tools\\make_screenshots.py first")
    browser = next((b for b in BROWSERS if Path(b).exists()), None) or shutil.which("msedge") or shutil.which("chrome")
    if not browser:
        sys.exit("Edge or Chrome not found.")
    WORK.mkdir(parents=True, exist_ok=True)
    for name in ([args.only] if args.only else IMAGES):
        render(name, browser)
    return 0


if __name__ == "__main__":
    sys.exit(main())
