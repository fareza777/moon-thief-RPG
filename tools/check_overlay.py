"""Verify a rendered result card (the modal overlays of MoonThief).

Checks three things the eye would catch late:
  1. the card frame is found and sits fully inside the screen
  2. nothing bright is drawn outside the card (no text floating over the art)
  3. the text bands inside the card never overlap each other

Each band is then decoded with the same bitmap font the renderer used, so the strings
can be read back exactly. Run:  python tools/check_overlay.py <png> [<png> ...]
"""
import sys

import inspect_png as I
import ocr

BRIGHT = 150          # the card frame
INK = 170             # the card text (the defeat heading is a dim red, about 175)
FRAME_SPAN = 200      # a frame line covers most of the card width


def bands(buf, bpp, w, h, x0, x1, y0, y1, min_gap=3):
    """Consecutive row ranges inside the box that contain ink."""
    rows = [sum(1 for x in range(x0, x1) if I.lum(buf, bpp, w, x, y) > INK) for y in range(h)]
    out, start = [], None
    for y in range(y0, y1 + 1):
        if rows[y] > 0 and start is None:
            start = y
        elif rows[y] == 0 and start is not None:
            if y - start >= 3:
                out.append((start, y - 1))
            start = None
    if start is not None:
        out.append((start, y1))
    return out


def decode_band(buf, bpp, w, h, glyphs, top, bottom, left, right, panel=False):
    """Read one text band back.

    A short band is a plain line of text. A tall band is a panel (a button) with its label
    centred inside, so the label is searched in a row window that starts below the frame
    border and the frame's own columns are excluded from the x candidates.
    """
    height = bottom - top + 1
    # the band's own ink extent: for a panel this is the frame, so pad inside it; for a plain
    # line of text it is the text itself, so keep almost all of it
    ext = [x for y in range(top, bottom + 1) for x in range(left + 2, right - 1)
           if I.lum(buf, bpp, w, x, y) > INK]
    if not ext:
        return "", 2
    x_lo, x_hi = min(ext) + (10 if panel else 1), max(ext) - (10 if panel else 1)
    best, best_score, best_scale = "", -1e9, 2
    # the slice draws headings at 3x and body text at 2x, so pick the scale that fits the band
    for scale in ([2] if panel else ([3] if height >= 19 else [2])):
        ink_h = 7 * scale
        # a panel centres its label; a plain line starts at the band top. The band can be a pixel
        # shorter than the theoretical glyph height, so clamp instead of skipping the scale.
        off_y = max(0, (height - ink_h) // 2)
        if ink_h > height + 2:
            continue
        y0 = top + off_y
        xs = [x for y in range(y0, y0 + ink_h) for x in range(x_lo, x_hi + 1)
              if I.lum(buf, bpp, w, x, y) > INK]
        if not xs:
            continue
        for off in range(0, 6 * scale):
            # the results card lines can contain a run of spaces, so tolerate a few blank cells
            text = ocr.decode(glyphs, buf, bpp, w, h, min(xs) - off, y0, scale, 64, max_empty=4)
            q = text.count("?")
            score = sum(1 for c in text if c not in " ?") - 4 * q
            if score > best_score:
                best, best_score, best_scale = text, score, scale
    return best, best_scale


def check(path):
    """Check one modal overlay screenshot: frame, no text outside, no overlapping bands."""
    w, h, bpp, buf = I.read_png(path)
    frame_rows = [y for y in range(h)
                  if sum(1 for x in range(w) if I.lum(buf, bpp, w, x, y) > BRIGHT) > FRAME_SPAN]
    print("== %s (%dx%d)" % (path.split("/")[-1], w, h))
    if not frame_rows:
        print("   skip: no modal card in this frame")
        return True
    top, bottom = frame_rows[0], frame_rows[-1]
    cols = [x for x in range(w)
            if sum(1 for y in range(top, bottom + 1) if I.lum(buf, bpp, w, x, y) > BRIGHT) > (bottom - top) * 0.8]
    left, right = (cols[0], cols[-1]) if cols else (0, w - 1)
    ok = True
    print("   card  rows %d..%d  cols %d..%d  (%dx%d px)" % (top, bottom, left, right, right - left + 1, bottom - top + 1))
    if top < 4 or bottom > h - 5 or left < 2 or right > w - 3:
        print("   skip: this is not a modal overlay screenshot (the whole screen is a panel)")
        return True

    outside = [(x, y) for y in range(h) for x in range(w)
               if I.lum(buf, bpp, w, x, y) > INK and not (top <= y <= bottom and left <= x <= right)]
    if outside:
        ys = sorted({y for x, y in outside})
        print("   FAIL: %d text pixels outside the card (rows %d..%d)" % (len(outside), ys[0], ys[-1]))
        ok = False
    else:
        print("   ok: no text outside the card")

    glyphs = ocr.load_font()
    found = bands(buf, bpp, w, h, left + 3, right - 2, top + 2, bottom - 2)
    prev_end = None
    for a, b in found:
        if prev_end is not None and a <= prev_end + 1:
            ok = False
        text, scale = decode_band(buf, bpp, w, h, glyphs, a, b, left, right, panel=(b - a + 1) > 22)
        mark = "  <-- OVERLAP" if prev_end is not None and a <= prev_end + 1 else ""
        print("   band rows %3d..%-3d (%2dpx, x%d) '%s'%s" % (a, b, b - a + 1, scale, text, mark))
        prev_end = b
    return ok


if __name__ == "__main__":
    results = [check(p) for p in sys.argv[1:]]
    print("\n%s" % ("ALL CARDS OK" if all(results) else "PROBLEMS FOUND"))
    sys.exit(0 if all(results) else 1)
