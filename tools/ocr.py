"""Read text back out of a rendered PNG using the game's own font table.

samples each font pixel at its centre, matches it against font5x7.txt with a hamming
distance and prints the decoded string. This is how the vertical slice text is verified:
the same file drives the renderer and the checker.

usage:  python tools/ocr.py <png> <x0> <y0> <scale> [maxcells]
"""
import sys

import inspect_png as I

FONT = "MoonThief/Assets/Resources/Text/font5x8.txt"


def load_font():
    glyphs = {}
    for line in open(FONT, encoding="utf-8"):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        key, _, rows = line.partition("|")
        parts = rows.split("/")
        if len(parts) < 6 or len(set(len(r) for r in parts)) != 1:
            continue
        ch = " " if key == "space" else key[0]
        glyphs[ch] = tuple(tuple(1 if c == "#" else 0 for c in r) for r in parts)
    return glyphs


def decode(glyphs, buf, bpp, w, h, x0, y0, scale, max_cells=60, max_empty=2):
    """Read a run of characters.

    max_empty is how many blank cells in a row are tolerated before the read is assumed to be
    past the end of the text - raise it for strings that contain runs of spaces.
    """
    gh = len(next(iter(glyphs.values())))
    out = []
    empty_run = 0
    for cell in range(max_cells):
        pattern = []
        for r in range(gh):
            row = []
            for c in range(5):
                px = x0 + cell * 6 * scale + c * scale + scale // 2
                py = y0 + r * scale + scale // 2
                if px < 0 or px >= w or py < 0 or py >= h:
                    row.append(0)
                    continue
                row.append(1 if I.lum(buf, bpp, w, px, py) > 60 else 0)
            pattern.append(tuple(row))
        pattern = tuple(pattern)

        if all(all(v == 0 for v in row) for row in pattern):
            empty_run += 1
            if empty_run > max_empty:
                break
            out.append(" ")
            continue
        empty_run = 0

        best, best_d = "?", 99
        for ch, g in glyphs.items():
            d = sum(1 for r in range(gh) for c in range(5) if g[r][c] != pattern[r][c])
            if d < best_d:
                best, best_d = ch, d
        out.append(best if best_d <= 6 else "?")
    return "".join(out).rstrip()


if __name__ == "__main__":
    path = sys.argv[1]
    x0, y0, scale = int(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4])
    max_cells = int(sys.argv[5]) if len(sys.argv) > 5 else 60
    glyphs = load_font()
    w, h, bpp, buf = I.read_png(path)
    print("%s  x=%d y=%d scale=%d" % (path, x0, y0, scale))
    print("  '%s'" % decode(glyphs, buf, bpp, w, h, x0, y0, scale, max_cells))
