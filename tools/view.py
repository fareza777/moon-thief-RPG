"""Colour-coded ASCII view of a PNG region, plus a sprite locator.

usage:
  python tools/view.py <png> [x0 x1 y0 y1]      # region in *pixels of the file*
  python tools/view.py <png> --find <sheet> <cellW> <artH> <col>

The viewer maps every pixel to one character: hue letter for saturated pixels
(r red, a amber, y yellow, l leaf, g green, c cyan, b blue, p purple, m magenta)
in lower case when dark and UPPER case when bright, with `.`/`o`/`#` for the
greys. That is enough to read pixel art on a busy background as text.

`--find` searches the frame for a frame of a sheet (masked, allowing one global
brightness factor, because the game dims opened chests and night scenes), and
prints the best match so a sprite can be judged where it actually landed.
"""
import sys
import numpy as np
from PIL import Image


def classify(r, g, b):
    mx = max(r, g, b)
    mn = min(r, g, b)
    v = mx / 255.0
    if v < 0.13:
        return ' '
    sat = 0.0 if mx == 0 else (mx - mn) / mx
    if sat < 0.18:
        return '.' if v < 0.45 else ('o' if v < 0.75 else '#')
    if mx == r:
        h = (60 * (g - b) / (mx - mn)) % 360
    elif mx == g:
        h = 60 * (b - r) / (mx - mn) + 120
    else:
        h = 60 * (r - g) / (mx - mn) + 240
    if h < 20 or h >= 340:
        c = 'r'
    elif h < 45:
        c = 'a'
    elif h < 70:
        c = 'y'
    elif h < 100:
        c = 'l'
    elif h < 165:
        c = 'g'
    elif h < 200:
        c = 'c'
    elif h < 250:
        c = 'b'
    elif h < 300:
        c = 'p'
    else:
        c = 'm'
    return c.upper() if v > 0.55 else c


def to_array(im):
    """PIL -> numpy via getdata(): this Pillow build's tobytes() is broken for some PNGs."""
    w, h = im.size
    return np.array(list(im.getdata()), dtype=np.uint8).reshape(h, w, 4)


def view(path, box=None, alpha_on_white=True):
    im = Image.open(path).convert('RGBA')
    if box:
        im = im.crop(box)
    w, h = im.size
    arr = to_array(im)
    print("== %s %dx%d%s" % (path, w, h, (" box=%s" % (box,)) if box else ""))
    out = []
    for y in range(h):
        row = ''
        for x in range(w):
            r, g, b, a = arr[y, x]
            if a < 8:
                row += ' '
            else:
                row += classify(int(r), int(g), int(b))
        out.append("%4d %s" % (y + (box[1] if box else 0), row))
    print("\n".join(out))


def load_rgba(path):
    im = Image.open(path).convert('RGBA')
    return to_array(im).astype(np.float32)


def find(frame_path, sheet_path, cell_w, art_h, col, kmin=0.4, kmax=1.15, kstep=0.05):
    """Locate one sheet frame inside a frame, allowing a global brightness factor."""
    sheet = load_rgba(sheet_path)
    tile = sheet[sheet.shape[0] - art_h:, col * cell_w:(col + 1) * cell_w]
    mask = tile[:, :, 3] > 40
    rgb = tile[:, :, :3]
    th, tw = mask.shape

    frame = load_rgba(frame_path)
    H, W = frame.shape[:2]
    best = None
    for scale in (1, 2):
        if tw * scale >= W or th * scale >= H:
            continue
        up = Image.fromarray(tile.astype(np.uint8), 'RGBA').resize(
            (tw * scale, th * scale), Image.NEAREST)
        big = to_array(up).astype(np.float32)
        m = big[:, :, 3] > 40
        t = big[:, :, :3]
        for k in np.arange(kmin, kmax, kstep):
            ref = t * k
            for y in range(0, H - th * scale + 1, 1):
                band = frame[y:y + th * scale, :, :3]
                for x in range(0, W - tw * scale + 1, 1):
                    win = band[:, x:x + tw * scale]
                    d = np.abs(win - ref).max(axis=2)
                    frac = float((d[m] <= 20).mean())
                    if best is None or frac > best[0]:
                        best = (frac, k, scale, x, y)
    print("best match: frac=%.3f k=%.2f scale=%d at x=%d y=%d (size %dx%d)"
          % (best[0], best[1], best[2], best[3], best[4], tw * best[2], th * best[2]))
    return best


if __name__ == '__main__':
    a = sys.argv
    if len(a) > 2 and a[2] == '--find':
        find(a[1], a[3], int(a[4]), int(a[5]), int(a[6]))
    elif len(a) >= 6:
        view(a[1], (int(a[2]), int(a[4]), int(a[3]), int(a[5])))
    else:
        view(a[1])
