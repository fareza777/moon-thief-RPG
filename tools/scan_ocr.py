"""Find bright text bands in a screenshot and OCR each one.

usage: python tools/scan_ocr.py <png> [thr]
"""
import sys
sys.path.insert(0, "tools")
import inspect_png as I
from ocr import load_font, decode

GLYPHS = load_font()


def band_rows(buf, bpp, w, h, thr=140):
    rows = []
    for y in range(h):
        n = 0
        for x in range(0, w, 2):
            if I.lum(buf, bpp, w, x, y) > thr:
                n += 1
        rows.append(n)
    bands = []
    inb = False
    for y, n in enumerate(rows):
        if n > 2 and not inb:
            s = y; inb = True
        if n <= 2 and inb:
            bands.append((s, y - 1)); inb = False
    if inb:
        bands.append((s, h - 1))
    return bands


def band_cols(buf, bpp, w, y0, y1, thr=140):
    cols = []
    for x in range(w):
        n = 0
        for y in range(y0, y1 + 1):
            if I.lum(buf, bpp, w, x, y) > thr:
                n += 1
        cols.append(n)
    x0 = next((x for x, n in enumerate(cols) if n > 0), 0)
    x1 = w - 1 - next((i for i, n in enumerate(reversed(cols)) if n > 0), w - 1)
    return x0, x1


def main():
    path = sys.argv[1]
    thr = int(sys.argv[2]) if len(sys.argv) > 2 else 140
    w, h, bpp, buf = I.read_png(path)
    bands = band_rows(buf, bpp, w, h, thr)
    print("==", path)
    for (y0, y1) in bands:
        if y1 - y0 < 5:
            continue
        x0, x1 = band_cols(buf, bpp, w, y0, y1, thr)
        text = decode(GLYPHS, buf, bpp, w, h, x0, y0, 2, max_cells=(x1 - x0) // 12 + 4)
        print(f"  y{y0}-{y1} x{x0}-{x1}: '{text}'")


if __name__ == "__main__":
    main()
