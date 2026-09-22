"""Build a small contact sheet of world frames for a fast visual check.

usage: python tools/world_sheet.py [frame ...]
Writes preview/world.html — a self-contained page (frames embedded as data URIs) showing
each requested frame at 2x, so the preview webview never resamples a pixel.
"""
import base64
import os
import sys

SHOTS = "MoonThief/Screenshots"
RUNTIME = "MoonThief/Screenshots/Runtime"
DEFAULT = ["07-village", "09-plaza", "10-fields", "15-forest",
           "11-village", "12-explore-far", "17-after-boss"]


def find(name):
    """Editor frames and runtime frames live in different folders."""
    for base in (SHOTS, RUNTIME):
        for ext in (".png",):
            p = os.path.join(base, name + ext)
            if os.path.exists(p):
                return p
    return None


def data_uri(path):
    with open(path, "rb") as fh:
        return "data:image/png;base64," + base64.b64encode(fh.read()).decode("ascii")


def main():
    names = sys.argv[1:] or DEFAULT
    cells = []
    for n in names:
        png = find(n)
        if png is None:
            print("missing: %s" % n)
            continue
        cells.append(
            '<figure><img src="%s"><figcaption>%s</figcaption></figure>' % (data_uri(png), n)
        )
    html = (
        "<!doctype html><meta charset=\"utf-8\"><title>The Moon Thief - world sheet</title>"
        "<body style=\"margin:0;background:#0b0b12;color:#9aa0b5;"
        "font:12px ui-monospace,monospace\">"
        "<div style=\"display:flex;flex-wrap:wrap;gap:10px;padding:10px\">"
        + "".join(cells)
        + "</div><style>figure{margin:0}img{width:288px;height:512px;"
        "image-rendering:pixelated;display:block;border:1px solid #23233a}"
        "figcaption{padding-top:4px}</style></body>"
    )
    with open("preview/world.html", "w", encoding="utf-8") as fh:
        fh.write(html)
    print("wrote preview/world.html with %d frames" % len(cells))


if __name__ == "__main__":
    main()
