"""End to end text verification.

The build logs one [LABEL] line per text block and per rendered frame:

    [LABEL] 08-dialog|Stage/WorldView/dialog/name|2|48|262|12.4|Center|7004|Mira

This script re-reads every one of those blocks straight out of the PNG with the same
glyph table the renderer used, so a frame is only proved good when every visible string
decodes back to the string the game asked for at the exact pixel it was placed on.

usage:  python tools/verify_labels.py <log>            (all frames)
        python tools/verify_labels.py <log> 08-dialog  (one frame)
"""
import os
import sys
import zlib

sys.path.insert(0, "tools")
import inspect_png as I
from ocr import load_font

SHOTS = "MoonThief/Screenshots"
GLYPHS = load_font()
GH = len(next(iter(GLYPHS.values())))
GW = 5


# ----------------------------------------------------------------- renderer mirrors

def advance(scale):
    return 6.0 * scale          # 6 font px per cell at 16 px per unit


def measure_width(text, scale):
    lines = text.split("\n")
    widest = max((len(l) for l in lines), default=0)
    if widest <= 0:
        return 0.0
    return (widest * 6 - 1) * scale


def wrap(text, max_width, scale):
    """Greedy word wrap, identical to PixelLabel.Wrap()."""
    if max_width <= 0 or not text:
        return text
    max_chars = max(1, int((max_width + (6 * scale - 5 * scale) / 16.0) / (6 * scale / 16.0)))
    out = []
    for raw in text.split("\n"):
        line_len = 0
        parts = []
        for w in raw.split(" "):
            extra = len(w) if line_len == 0 else len(w) + 1
            if line_len > 0 and line_len + extra > max_chars:
                parts.append("\n")
                line_len = 0
            if line_len > 0:
                parts.append(" ")
                line_len += 1
            parts.append(w)
            line_len += len(w)
        out.append("".join(parts))
    return "\n".join(out)


# ----------------------------------------------------------------- decoding

def decode_best(buf, bpp, w, h, x, y, scale, max_cells):
    """Try a few ink thresholds and keep the cleanest reading. A glyph over bright world art
    needs a higher cut than glyph over a panel; a fixed cut reported one of them as noise."""
    best = None
    for thr in (90, 110, 130, 150, 170, 190):
        got, err = decode_block(buf, bpp, w, h, x, y, scale, max_cells, thr)
        if best is None or err < best[0]:
            best = (err, got)
    return best[1], best[0]


def decode_block(buf, bpp, w, h, x, y, scale, max_cells, thr=90, max_empty=99):
    out, empty, total, glyphs = [], 0, 0, 0
    for cell in range(max_cells):
        if x + cell * GW * scale >= w:
            break
        pat = tuple(
            tuple(
                1 if (0 <= x + cell * 6 * scale + c * scale + scale // 2 < w
                      and 0 <= y + r * scale + scale // 2 < h
                      and I.lum(buf, bpp, w,
                                x + cell * 6 * scale + c * scale + scale // 2,
                                y + r * scale + scale // 2) > thr) else 0
                for c in range(GW))
            for r in range(GH)
        )
        if all(all(v == 0 for v in row) for row in pat):
            empty += 1
            if empty > max_empty:
                break
            out.append(" ")
            continue
        empty = 0
        best, bd = "?", 99
        for ch, g in GLYPHS.items():
            d = sum(1 for r in range(GH) for c in range(GW) if g[r][c] != pat[r][c])
            if d < bd:
                best, bd = ch, d
        total += bd
        glyphs += 1
        out.append(best)
    return "".join(out), (total / glyphs if glyphs else 0.0)


def read_log(path):
    labels = []
    for line in open(path, encoding="utf-8", errors="replace"):
        if "[LABEL] " not in line:
            continue
        body = line.split("[LABEL] ", 1)[1].strip()
        parts = body.split("|")
        if len(parts) < 9:
            continue
        frame, who, scale, px, py, maxw, align, order = parts[:8]
        text = "|".join(parts[8:]).replace("\\n", "\n")
        labels.append(dict(frame=frame.strip(), who=who, scale=int(scale), x=int(px), y=int(py),
                           maxw=float(maxw.replace(",", ".")), align=align, order=int(order), text=text))
    return labels


def main():
    log = sys.argv[1]
    only = sys.argv[2] if len(sys.argv) > 2 else None
    labels = read_log(log)
    if not labels:
        print("no [LABEL] lines in %s - rebuild with the label audit enabled" % log)
        return

    frames = {}
    for lb in labels:
        if only and lb["frame"] != only:
            continue
        frames.setdefault(lb["frame"], []).append(lb)

    bad = 0
    for frame in sorted(frames):
        png = os.path.join(SHOTS, frame + ".png")
        if not os.path.exists(png):
            print("%-16s no png" % frame)
            continue
        w, h, bpp, buf = I.read_png(png)
        print("== %s (%dx%d)" % (frame, w, h))
        for lb in frames[frame]:
            scale = lb["scale"]
            want = wrap(lb["text"], lb["maxw"], scale)
            lines = want.split("\n")
            line_h = 9 * scale              # atlas cell height in render pixels
            got_lines, errs = [], []
            if lb["x"] >= w or lb["y"] >= h or lb["x"] + max(len(l) for l in lines) * 6 * scale < 0:
                print("   %-30s s%-2d (%3d,%3d) offscreen" % (lb["who"].split("/")[-1], scale, lb["x"], lb["y"]))
                continue
            for i, line in enumerate(lines):
                # mirror PixelLabel.Rebuild: the anchor is the top of the first line and the
                # alignment decides where the ink starts, snapped to whole pixels
                line_px = (len(line) * 6 - 1) * scale if line else 0
                x0 = lb["x"]
                if lb["align"] == "Center":
                    x0 = lb["x"] - int(round(line_px * 0.5))
                elif lb["align"] == "Right":
                    x0 = lb["x"] - line_px
                if x0 < 0 or x0 + line_px > w:
                    got_lines.append("<clipped>")
                    errs.append(0.0)
                    continue
                # exactly the cells the string occupies: reading past the end picked up the
                # neighbouring moon icon and reported phantom glyphs
                got, err = decode_best(buf, bpp, w, h, x0, lb["y"] + i * line_h, scale, max(1, len(line)))
                got_lines.append(got.rstrip())
                errs.append(err)
            got_text = "\n".join(got_lines).rstrip()
            ok = got_text == want.rstrip()
            if not ok:
                bad += 1
            print("   %-30s s%-2d (%3d,%3d) %s" % (lb["who"].split("/")[-1], scale, lb["x"], lb["y"],
                                                   "ok" if ok else "MISMATCH"))
            if not ok:
                print("        want '%s'" % want.replace("\n", " / "))
                print("        got  '%s'   (err %s)"
                      % (got_text.replace("\n", " / "), " ".join("%.1f" % e for e in errs)))
    print()
    print("mismatched blocks: %d" % bad)


if __name__ == "__main__":
    main()
