"""Read the text of every bright band in a frame, at whichever scale/offset fits best.

usage: python tools/row_read.py <png> [<png> ...]
"""
import sys
sys.path.insert(0, 'tools')
import inspect_png as I
from ocr import load_font, decode

G = load_font()

def score(s):
    if not s.strip():
        return -99
    good = sum(1 for c in s if c != '?' and c != ' ')
    return good - 3 * s.count('?')

def read(png):
    w, h, bpp, buf = I.read_png(png)
    print('==', png)
    bands, cur = [], None
    for y in range(h):
        n = sum(1 for x in range(6, w - 6) if I.lum(buf, bpp, w, x, y) > 110)
        if n > 5 and cur is None:
            cur = y
        elif n <= 5 and cur is not None:
            if y - cur >= 4:
                bands.append((cur, y - 1))
            cur = None
    for (a, b) in bands:
        best, bs = '', -99
        for scale in (2, 3):
            for y0 in range(a - 3, b + 1):
                for x0 in range(4, 70):
                    s = decode(G, buf, bpp, w, h, x0, y0, scale, max_cells=26)
                    if score(s) > bs:
                        best, bs = s, score(s)
        print('  y%-3d..%-3d score=%-4d %s' % (a, b, bs, best))

for p in sys.argv[1:]:
    read(p)
