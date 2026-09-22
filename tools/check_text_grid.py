"""Catch text that is not sitting on the pixel grid.

Every line of the bitmap font is 7 rows tall, so a rendered band of text must be exactly 7*scale
pixels high (14 at scale 2, 21 at scale 3). A band of 13 or 15 means a label landed on a half
pixel and one glyph row got squeezed - the classic pixel art blur.

usage:  python tools/check_text_grid.py <png> [<png> ...]
        python tools/check_text_grid.py MoonThief/Screenshots/*.png
"""
import glob
import os
import sys

import inspect_png as I

INK = 170                 # text ink; dim coloured headings still count
VALID = {7, 14, 21}       # one font row at 1x, 2x and 3x


def bands(buf, bpp, w, h, x0, x1):
    rows = [sum(1 for x in range(x0, x1) if I.lum(buf, bpp, w, x, y) > INK) for y in range(h)]
    out, start = [], None
    for y in range(h):
        if rows[y] > 0 and start is None:
            start = y
        elif rows[y] == 0 and start is not None:
            out.append((start, y - 1))
            start = None
    if start is not None:
        out.append((start, h - 1))
    return out


def text_like(buf, bpp, w, x0, x1, top, bottom):
    """A line of bitmap text is made of thin strokes: no long horizontal run of ink.

    Artwork, panels and bars are skipped this way, so only real text is judged.
    """
    widest = 0
    for y in range(top, bottom + 1):
        run = 0
        for x in range(x0, x1):
            if I.lum(buf, bpp, w, x, y) > INK:
                run += 1
                widest = max(widest, run)
            else:
                run = 0
    return widest <= 4


def check(path):
    w, h, bpp, buf = I.read_png(path)
    problems = []
    for top, bottom in bands(buf, bpp, w, h, 4, w - 5):
        height = bottom - top + 1
        if height in VALID or height > 24:      # taller than one line = a panel, not text
            continue
        if height < 7:                          # a one or two pixel line is an edge, not text
            continue
        cols = [x for y in range(top, bottom + 1) for x in range(4, w - 4)
                if I.lum(buf, bpp, w, x, y) > INK]
        if max(cols) - min(cols) < 24:          # a text line is wide
            continue
        if len(cols) < 12 * height:              # a line of text is dense; art details are sparse
            continue
        if not text_like(buf, bpp, w, 4, w - 4, top, bottom):
            continue
        problems.append((top, bottom, height))
    name = os.path.basename(path)
    if problems:
        print("FAIL %-28s off-grid text bands: %s" % (
            name, ", ".join("y%d-%d h%d" % (a, b, c) for a, b, c in problems)))
        return False
    print("ok   %-28s every text band is 7/14/21 px tall" % name)
    return True


if __name__ == "__main__":
    files = sys.argv[1:]
    if not files:
        files = sorted(glob.glob("MoonThief/Screenshots/*.png")) + \
                sorted(glob.glob("MoonThief/Screenshots/Runtime/*.png"))
    results = [check(p) for p in files]
    print("\n%s" % ("ALL TEXT ON THE PIXEL GRID" if all(results) else "OFF-GRID TEXT FOUND"))
    sys.exit(0 if all(results) else 1)
