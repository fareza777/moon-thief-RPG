"""Final text verifier: brute-forces every x/dy/scale per band so no alignment guess fails.

usage: python tools/verify_text.py <png> [thr]
"""
import sys

sys.path.insert(0, "tools")
import inspect_png as I
from ocr import load_font, decode


def band_cols(buf, bpp, w, y0, y1, thr):
    cols = []
    for x in range(w):
        for y in range(y0, y1):
            if I.lum(buf, bpp, w, x, y) > thr:
                cols.append(x)
                break
    return (min(cols), max(cols)) if cols else (0, 0)


def best_read(G, buf, bpp, w, h, y0, y1, thr):
    x0, x1 = band_cols(buf, bpp, w, y0, y1, thr)
    span = x1 - x0
    if span < 6:
        return 0.0, "", 0
    best = (0.0, "", 0)
    for adv in (6, 12, 18):
        scale = adv // 6
        cells = min(40, span // adv + 2)
        if cells < 2:
            continue
        for x in range(max(0, x0 - 12), x0 + 13):
            for dy in range(-3, 4):
                yy = y0 + dy
                if yy < 0 or yy + 7 * scale > h:
                    continue
                t = decode(G, buf, bpp, w, h, x, yy, scale, max_cells=cells)
                n = sum(1 for c in t if c not in "? ")
                s = n / max(1, len(t))
                if s > best[0]:
                    best = (s, t, scale)
    return best


def row_counts(buf, bpp, w, h, thr):
    prof = []
    for y in range(h):
        n = sum(1 for x in range(0, w, 2) if I.lum(buf, bpp, w, x, y) > thr)
        prof.append(n)
    return prof


def main():
    png = sys.argv[1]
    thr = int(sys.argv[2]) if len(sys.argv) > 2 else 96
    w, h, bpp, buf = I.read_png(png)
    G = load_font()
    prof = row_counts(buf, bpp, w, h, thr)
    bands, start = [], None
    for y, n in enumerate(prof):
        if n > 2 and start is None:
            start = y
        elif n <= 2 and start is not None:
            if y - start >= 5:
                bands.append((start, y))
            start = None
    if start is not None:
        bands.append((start, h))
    print("==", png)
    fail = 0
    for (y0, y1) in bands:
        s, t, sc = best_read(G, buf, bpp, w, h, y0, y1, thr)
        ok = s >= 0.6 and "?" not in t
        if not ok:
            fail += 1
        print(f"  y{y0}-{y1} s{sc} {s:.2f}: '{t}'{'' if ok else '   <-- CHECK'}")
    print("  FAIL" if fail else "  ALL BANDS READ")


if __name__ == "__main__":
    main()
