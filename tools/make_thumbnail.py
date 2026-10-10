r"""Builds the Steam Workshop preview and the README cover from the real Party Steward window.

    python tools\make_thumbnail.py                  # both, v1 -> preview_thumbnail.jpg + cover.jpg
    python tools\make_thumbnail.py --variant v2     # v1 + a before -> after line -> preview_thumbnail_v2.jpg + cover_v2.jpg
    python tools\make_thumbnail.py --only thumbnail
    python tools\make_thumbnail.py --only cover

1. Takes the WHOLE window from Screenshots\01_suggestion_food.jpg (written by make_screenshots.py); the HTML dims it and
   shows its real "Deal all" button (DEAL_BOX) again undimmed, enlarged, in a gold glow - placed by this script.
   The v2 numbers (5 days of food -> 40 days, 12 prisoners -> +3,400 denari) are EXAMPLE figures, not from a shot.
2. Renders tools\preview_thumbnail.html (1024 x 1024) and tools\cover.html (1600 x 900) with headless Edge (or Chrome).
3. Saves Screenshots\preview_thumbnail.jpg (the Workshop preview, square, under Steam's 1 MB cap) and
   Screenshots\cover.jpg (the README image), stepping JPEG quality down until each is under 1 MB.
Style = the sibling mods' thumbnails: gold frame, Palatino small caps title over a dark fade, one tagline, the verbs strip.
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

# name: template, output, page size, the .art box in the page (left, top, width, height - keep in sync with the HTML)
IMAGES = {
    "thumbnail": ("preview_thumbnail.html", "preview_thumbnail.jpg", (1024, 1024), (34, 40, 956, 583)),
    "cover": ("cover.html", "cover.jpg", (1600, 900), (50, 145, 1000, 610)),
}
ART_BOX = (0, 0, 2118, 1292)        # the whole window
DEAL_BOX = (1815, 1200, 2063, 1252)  # the "Deal all" button in SOURCE pixels (bottom right of the footer)


def render(name: str, browser: str, variant: str) -> None:
    template, out_name, (w, h), (ax, ay, aw, ah) = IMAGES[name]
    if variant != "v1":
        out_name = out_name.replace(".jpg", f"_{variant}.jpg")
    panel = WORK / f"{name}_art.png"
    Image.open(SOURCE).convert("RGB").crop(ART_BOX).save(panel)
    sx, sy = aw / (ART_BOX[2] - ART_BOX[0]), ah / (ART_BOX[3] - ART_BOX[1])
    bx, by = (DEAL_BOX[0] - ART_BOX[0]) * sx, (DEAL_BOX[1] - ART_BOX[1]) * sy
    values = {"DEAL_LEFT": ax + bx, "DEAL_TOP": ay + by, "DEAL_W": (DEAL_BOX[2] - DEAL_BOX[0]) * sx,
              "DEAL_H": (DEAL_BOX[3] - DEAL_BOX[1]) * sy, "DEAL_BX": bx, "DEAL_BY": by}
    html = (REPO / "tools" / template).read_text(encoding="utf-8").replace("ART_IMG", panel.as_uri())
    html = html.replace("VARIANT", variant)
    for key, v in values.items():
        html = html.replace(key + "px", f"{v:.1f}px")
    page = WORK / f"{name}_{variant}.html"
    page.write_text(html, encoding="utf-8")
    png = WORK / f"{name}_{variant}.png"
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
    ap.add_argument("--variant", choices=["v1", "v2"], default="v1")
    args = ap.parse_args()
    if not SOURCE.exists():
        sys.exit(f"{SOURCE} missing - run tools\\make_screenshots.py first")
    browser = next((b for b in BROWSERS if Path(b).exists()), None) or shutil.which("msedge") or shutil.which("chrome")
    if not browser:
        sys.exit("Edge or Chrome not found.")
    WORK.mkdir(parents=True, exist_ok=True)
    for name in ([args.only] if args.only else IMAGES):
        render(name, browser, args.variant)
    return 0


if __name__ == "__main__":
    sys.exit(main())
