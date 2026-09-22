"""Decode text at exact pixel positions, so a known layout can be verified precisely.

The auto-band scanners fight with busy pixel art; this reads a named spot instead
(the HUD line, the dialog line, the battle menu labels, the story slide). Coordinates
are derived from the layout constants in Game.cs / Battle.cs / Frontend.cs.

usage:  python tools/read_at.py <png> <frame-name>
"""
import sys

sys.path.insert(0, "tools")
import inspect_png as I
from ocr import load_font

GLYPHS = load_font()

# frame -> list of (label, x, y, scale, cells, threshold)
SPOTS = {
    "01-splash": [
        ("studio", 45, 230, 3, 14, 90),
        ("made", 75, 265, 1, 26, 90),
        ("skip", 0, 0, 0, 0, 0),
    ],
    "02-menu": [
        ("row1", 54, 223, 2, 12, 90),
        ("row2", 54, 264, 2, 12, 12),
        ("row3", 54, 305, 2, 12, 90),
        ("row4", 54, 346, 2, 12, 90),
        ("hint", 87, 403, 1, 22, 90),
    ],
    "03-settings": [
        ("speed", 54, 207, 2, 14, 90),
        ("sound", 54, 248, 2, 14, 90),
        ("shake", 54, 289, 2, 14, 90),
        ("back", 54, 330, 2, 14, 90),
    ],
    "05-story": [("line1", 25, 221, 2, 22, 90)],
    "06-nightcard": [("night", 60, 230, 3, 14, 90)],
    "07-village": [("hud", 6, 8, 1, 22, 190), ("goal", 6, 25, 1, 30, 190)],
    "08-dialog": [
        ("name", 53, 424, 1, 6, 90),
        ("line1", 53, 434, 2, 20, 90),
        ("line2", 53, 448, 2, 20, 90),
        ("line3", 53, 462, 2, 20, 90),
    ],
    "10-fields": [("hud", 6, 8, 1, 22, 190), ("goal", 6, 25, 1, 30, 190)],
    "11-battle-command": [
        ("msg", 10, 379, 2, 24, 90),
        ("attack", 28, 434, 2, 12, 90),
        ("befriend", 166, 434, 2, 12, 90),
        ("morsel", 28, 479, 2, 12, 90),
        ("leto", 166, 479, 2, 12, 90),
        ("hint", 100, 393, 1, 22, 90),
    ],
    "12-battle-action": [("msg", 12, 379, 2, 24, 90)],
    "13-battle-card": [
        ("l1", 40, 292, 2, 22, 90),
        ("l2", 40, 315, 2, 22, 90),
        ("l3", 40, 338, 2, 22, 90),
    ],
    "16-ending": [
        ("l1", 40, 160, 2, 22, 210),
        ("l2", 40, 188, 2, 22, 210),
        ("l3", 40, 216, 2, 22, 210),
    ],
}


GH = len(next(iter(GLYPHS.values())))


def sample(buf, bpp, w, h, x, y, scale, cells, thr):
    out, empty = [], 0
    for cell in range(cells):
        pat = []
        for r in range(GH):
            row = []
            for c in range(5):
                px = x + cell * 6 * scale + c * scale + scale // 2
                py = y + r * scale + scale // 2
                row.append(1 if 0 <= px < w and 0 <= py < h and I.lum(buf, bpp, w, px, py) > thr else 0)
            pat.append(tuple(row))
        pat = tuple(pat)
        if all(all(v == 0 for v in r) for r in pat):
            empty += 1
            if empty > 2:
                break
            out.append(" ")
            continue
        empty = 0
        best, bd = "?", 99
        for ch, g in GLYPHS.items():
            d = sum(1 for r in range(GH) for c in range(5) if g[r][c] != pat[r][c])
            if d < bd:
                best, bd = ch, d
        out.append(best if bd <= 8 else "?")
    return "".join(out).rstrip()


def sample_scored(buf, bpp, w, h, x, y, scale, cells, thr):
    """Decode a run of cells and report the mean per-glyph hamming distance too.
    A band read at the wrong scale scores badly, which is how auto finds the scale."""
    out, empty, total, glyphs = [], 0, 0, 0
    for cell in range(cells):
        pat = []
        for r in range(GH):
            row = []
            for c in range(5):
                px = x + cell * 6 * scale + c * scale + scale // 2
                py = y + r * scale + scale // 2
                row.append(1 if 0 <= px < w and 0 <= py < h and I.lum(buf, bpp, w, px, py) > thr else 0)
            pat.append(tuple(row))
        pat = tuple(pat)
        if all(all(v == 0 for v in r) for r in pat):
            empty += 1
            if empty > 2:
                break
            out.append(" ") if empty == 1 else out.append(" ")
            continue
        empty = 0
        best, bd = "?", 99
        for ch, g in GLYPHS.items():
            d = sum(1 for r in range(GH) for c in range(5) if g[r][c] != pat[r][c])
            if d < bd:
                best, bd = ch, d
        total += bd
        glyphs += 1
        out.append(best)
    mean = total / glyphs if glyphs else 99.0
    return "".join(out).rstrip(), mean


def auto(buf, bpp, w, h, x0, x1, y0, y1, scale, thr):
    """Find text bands in a rectangle and read each one. `scale` is a hint: every band is
    also tried at 1, 2 and 3 and the reading with the lowest glyph error wins, because a
    page mixes sizes (a scale 3 title sits right above a scale 1 credit line)."""
    rows = []
    for y in range(y0, y1):
        ink = sum(1 for x in range(x0, x1) if I.lum(buf, bpp, w, x, y) > thr)
        rows.append(ink >= 3)
    bands, start = [], None
    for i, on in enumerate(rows):
        if on and start is None:
            start = i
        elif not on and start is not None:
            bands.append((y0 + start, i - start))
            start = None
    if start is not None:
        bands.append((y0 + start, len(rows) - start))
    for by, bh in bands:
        left = None
        for x in range(x0, x1):
            if any(I.lum(buf, bpp, w, x, by + k) > thr for k in range(bh)):
                left = x
                break
        if left is None:
            continue
        best = None
        for s in sorted({scale, 1, 2, 3}, reverse=True):
            if bh < GH * s - s - 2:
                continue
            cells = max(4, (x1 - left) // (6 * s))
            text, err = sample_scored(buf, bpp, w, h, left, by, s, cells, thr)
            if best is None or err < best[0]:
                best = (err, s, text)
        if best is None:
            continue
        print("  y%-4d h%-3d s%-2d x%-4d err%4.1f '%s'"
              % (by, bh, best[1], left, best[0], best[2]))


def main():
    png, name = sys.argv[1], sys.argv[2]
    w, h, bpp, buf = I.read_png(png)
    print("== %s (%dx%d)" % (png, w, h))
    if name == "auto":
        # usage: read_at.py <png> auto <x0> <x1> <y0> <y1> <scale> <thr>
        auto(buf, bpp, w, h, int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5]),
             int(sys.argv[6]), int(sys.argv[7]), int(sys.argv[8]))
        return
    for label, x, y, scale, cells, thr in SPOTS.get(name, []):
        if scale == 0:
            continue
        got = sample(buf, bpp, w, h, x, y, scale, cells, thr)
        flag = "   <-- QUESTION GLYPH" if "?" in got else ""
        print("  %-10s '%s'%s" % (label, got, flag))


if __name__ == "__main__":
    main()
