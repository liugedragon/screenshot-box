#!/usr/bin/env python3
"""Render ScreenshotBox's SVG as a multi-size Windows icon and review PNGs.

Development tool only; no dependencies are included in the application.
On Debian/Ubuntu: apt install python3-gi python3-cairo gir1.2-rsvg-2.0 python3-pil
Libraries: librsvg (LGPL-2.0-or-later), PyGObject (LGPL-2.1-or-later),
Pycairo (LGPL-2.1-or-later or MPL-1.1), Pillow (HPND).
Sources: https://gitlab.gnome.org/GNOME/librsvg
         https://pygobject.gnome.org/ https://pycairo.readthedocs.io/
         https://python-pillow.org/

Run: python3 scripts/build-icons.py
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import struct
from pathlib import Path

import cairo
import gi

gi.require_version("Rsvg", "2.0")
from gi.repository import Rsvg
from PIL import Image, ImageDraw, ImageFont, __version__ as pillow_version

SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)
RESAMPLE = getattr(Image, "Resampling", Image).LANCZOS


def render_svg(source: Path, size: int) -> Image.Image:
    # Rasterize each size separately at 4x, then filter alpha together with color.
    handle = Rsvg.Handle.new_from_file(str(source.resolve()))
    dimensions = handle.get_dimensions()
    surface = cairo.ImageSurface(cairo.FORMAT_ARGB32, size * 4, size * 4)
    context = cairo.Context(surface)
    context.set_antialias(cairo.ANTIALIAS_BEST)
    context.scale(size * 4 / dimensions.width, size * 4 / dimensions.height)
    if not handle.render_cairo(context):
        raise RuntimeError("SVG rendering failed")
    output = io.BytesIO()
    surface.write_to_png(output)
    image = Image.open(io.BytesIO(output.getvalue())).convert("RGBA")
    return image.resize((size, size), RESAMPLE)


def write_ico(images: dict[int, Image.Image], destination: Path) -> None:
    # 32-bit DIB frames preserve compatibility with Windows tray/GDI loaders.
    # Export every requested size explicitly, including 256 pixels.
    payloads = []
    for size in SIZES:
        rgba = images[size].tobytes()
        xor = bytearray()
        mask_stride = ((size + 31) // 32) * 4
        mask = bytearray(mask_stride * size)
        for row in range(size):
            original_row = size - 1 - row
            for column in range(size):
                i = (original_row * size + column) * 4
                red, green, blue, alpha = rgba[i:i + 4]
                xor.extend((blue, green, red, alpha))
                if alpha == 0:
                    mask[row * mask_stride + column // 8] |= 1 << (7 - column % 8)
        header = struct.pack("<IiiHHIIiiII", 40, size, size * 2, 1, 32, 0,
                             len(xor), 0, 0, 0, 0)
        payloads.append(header + bytes(xor) + bytes(mask))
    directory = bytearray(struct.pack("<HHH", 0, 1, len(SIZES)))
    offset = 6 + 16 * len(SIZES)
    for size, payload in zip(SIZES, payloads):
        dimension = size if size < 256 else 0
        directory.extend(struct.pack("<BBBBHHII", dimension, dimension, 0, 0,
                                     1, 32, len(payload), offset))
        offset += len(payload)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(bytes(directory) + b"".join(payloads))
    with Image.open(destination) as icon:
        actual = icon.ico.sizes()
        if actual != {(size, size) for size in SIZES}:
            raise RuntimeError("ICO size directory is incomplete")
        for size in SIZES:
            frame = icon.ico.getimage((size, size)).convert("RGBA")
            if frame.getchannel("A").getextrema() != (0, 255):
                raise RuntimeError(f"ICO frame {size} lost transparency")


def font(size: int) -> ImageFont.ImageFont:
    choices = (Path("/mnt/c/Windows/Fonts/segoeui.ttf"),
               Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"))
    for candidate in choices:
        if candidate.is_file():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


def contact_sheet(images: dict[int, Image.Image], destination: Path) -> None:
    cell_widths = [max(size + 36, 64) for size in SIZES]
    width = sum(cell_widths) + 48
    sheet = Image.new("RGB", (width, 730), "#F6F6F6")
    draw = ImageDraw.Draw(sheet)
    draw.rectangle((0, 375, width, 730), fill="#202020")
    for row, foreground, background in ((0, "#202020", "#FFFFFF"),
                                        (1, "#F1F1F1", "#202020")):
        y = 20 + row * 365
        draw.text((24, y), "ScreenshotBox / native pixel sizes", fill=foreground,
                  font=font(18))
        left = 24
        for size, cell in zip(SIZES, cell_widths):
            x = left + (cell - size) // 2
            top = y + 55 + (256 - size) // 2
            draw.rectangle((x - 8, top - 8, x + size + 8, top + size + 8),
                           fill=background)
            sheet.paste(images[size], (x, top), images[size])
            draw.text((left + 10, y + 323), f"{size}px", fill=foreground,
                      font=font(14))
            left += cell
    sheet.save(destination)


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--svg", type=Path, default=root / "assets/screenshotbox.svg")
    parser.add_argument("--ico", type=Path, default=root / "assets/screenshotbox.ico")
    parser.add_argument("--preview-dir", type=Path,
                        default=root / "artifacts/icon-design")
    args = parser.parse_args()
    args.preview_dir.mkdir(parents=True, exist_ok=True)
    images = {size: render_svg(args.svg, size) for size in SIZES}
    write_ico(images, args.ico)
    for size, image in images.items():
        image.save(args.preview_dir / f"icon-{size}.png")
    contact_sheet(images, args.preview_dir / "contact-sheet.png")
    report = {"source": args.svg.name, "sizes": list(SIZES), "alpha": "RGBA",
              "renderer": f"librsvg {Rsvg.MAJOR_VERSION}.{Rsvg.MINOR_VERSION}.{Rsvg.MICRO_VERSION}",
              "cairo": cairo.cairo_version_string(), "pillow": pillow_version,
              "svgSha256": hashlib.sha256(args.svg.read_bytes()).hexdigest(),
              "icoSha256": hashlib.sha256(args.ico.read_bytes()).hexdigest()}
    (args.preview_dir / "report.json").write_text(json.dumps(report, indent=2) + "\n")
    print(f"Created {args.ico.name}: {len(SIZES)} RGBA frames")


if __name__ == "__main__":
    main()
