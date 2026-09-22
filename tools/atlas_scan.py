"""Classify the legacy 16x16 atlas so we can pick ground / transition / deco tiles.

usage: python tools/atlas_scan.py [mode]
  modes: dims | grass | near <tile> | halftile | list
Read-only: parses the PNG with tools/inspect_png.py (no PIL needed).
"""
import sys
sys.path.insert(0, "tools")
import inspect_png as I

ATLAS = "MoonThief/Assets/Resources/Art/Env/legacy_atlas.png"


def tiles():
    w, h, bpp, buf = I.read_png(ATLAS)
    cols, rows = w // 16, h // 16
    out = []
    for ty in range(rows):
        for tx in range(cols):
            px = []
            for y in range(16):
                row = []
                for x in range(16):
                    i = ((ty * 16 + y) * w + tx * 16 + x) * bpp
                    row.append((buf[i], buf[i + 1], buf[i + 2], buf[i + 3] if bpp == 4 else 255))
                px.append(row)
            out.append((ty * cols + tx, px))
    return cols, rows, out


def avg(px):
    n = 0
    r = g = b = a = 0
    for row in px:
        for p in row:
            r += p[0]; g += p[1]; b += p[2]; a += p[3]; n += 1
    return (r // n, g // n, b // n, a // n)


def is_green_grass(p):
    r, g, b, a = p
    return a > 200 and g > 90 and g > r + 18 and g > b + 40


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "dims"
    cols, rows, tl = tiles()
    print("atlas %d tiles  (%d cols x %d rows)" % (len(tl), cols, rows))

    if mode == "dims":
        return

    if mode == "list":
        for idx, px in tl:
            r, g, b, a = avg(px)
            if a < 20:
                continue
            print("%5d  avg=(%3d,%3d,%3d) a=%3d" % (idx, r, g, b, a))
        return

    if mode == "grass":
        # tiles whose average is grass green
        n = 0
        for idx, px in tl:
            if is_green_grass(avg(px)):
                r, g, b, a = avg(px)
                bright = sum(1 for row in px for p in row if p[1] > 150)
                print("%5d avg=(%3d,%3d,%3d) bright_green_px=%3d" % (idx, r, g, b, bright))
                n += 1
        print("-- %d grass-ish tiles" % n)
        return

    if mode == "near":
        want = int(sys.argv[2])
        base = dict(tl)[want]
        br, bg, bb, _ = avg(base)
        scored = []
        for idx, px in tl:
            if idx == want:
                continue
            r, g, b, a = avg(px)
            if a < 200:
                continue
            d = abs(r - br) + abs(g - bg) + abs(b - bb)
            scored.append((d, idx, r, g, b))
        scored.sort()
        for d, idx, r, g, b in scored[:25]:
            print("%5d  d=%3d avg=(%3d,%3d,%3d)" % (idx, d, r, g, b))
        return

    if mode == "halftile":
        # transition tiles: one half dirt-ish, the other half grass
        for idx, px in tl:
            top = avg(px[:8])
            bot = avg(px[8:])
            left = avg([row[:8] for row in px])
            right = avg([row[8:] for row in px])
            pairs = [("TB", top, bot), ("LR", left, right)]
            for tag, a1, b1 in pairs:
                g1, g2 = is_green_grass(a1), is_green_grass(b1)
                if g1 != g2 and min(a1[3], b1[3]) > 200:
                    print("%5d %s  %s | %s" % (idx, tag,
                                               "grass" if g1 else "other",
                                               "grass" if g2 else "other"))
        return


if __name__ == "__main__":
    main()
