"""Read the animation clips straight out of the Unity importer metadata.

The art pack ships its sheets already sliced by an earlier pass, so the .png.meta files are the
authoritative record of "which frames exist in this sheet". This tool parses those rects, groups
them by sheet, orders the frames by their trailing index, and prints one line per sheet.

usage:  python tools/anim_scan.py [--quiet] [--dir <subdir>]
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "MoonThief", "Assets", "Resources", "Art")

SPRITE_RE = re.compile(
    r"name:\s*(\S+)\s*\n"
    r"\s+rect:\s*\n"
    r"\s+serializedVersion:\s*\d+\s*\n"
    r"\s+x:\s*(-?\d+)\s*\n"
    r"\s+y:\s*(-?\d+)\s*\n"
    r"\s+width:\s*(\d+)\s*\n"
    r"\s+height:\s*(\d+)"
)


def frames_of(meta_path):
    """[(name, x, y, w, h)] in file order, or [] when the sheet was not sliced."""
    with open(meta_path, "r", encoding="utf-8", errors="replace") as fh:
        text = fh.read()
    return [(m.group(1), int(m.group(2)), int(m.group(3)), int(m.group(4)), int(m.group(5)))
            for m in SPRITE_RE.finditer(text)]


def order_key(entry):
    """Frame order follows the trailing index, exactly like Bank.Frames does at runtime."""
    tail = entry[0].rsplit("_", 1)
    if len(tail) == 2 and tail[1].lstrip("-").isdigit():
        return int(tail[1])
    return 0


def clips(subdir=""):
    """{relative sheet path: [(name, x, y, w, h)]} with frames in runtime order."""
    base = os.path.join(ART, subdir) if subdir else ART
    out = {}
    for dirpath, _dirs, files in os.walk(base):
        for name in files:
            if not name.endswith(".png"):
                continue
            png = os.path.join(dirpath, name)
            meta = png + ".meta"
            if not os.path.exists(meta):
                continue
            frames = frames_of(meta)
            rel = os.path.relpath(png, ART).replace("\\", "/")
            out[rel] = sorted(frames, key=order_key)
    return out


def main():
    quiet = "--quiet" in sys.argv
    subdir = ""
    if "--dir" in sys.argv:
        subdir = sys.argv[sys.argv.index("--dir") + 1]

    all_clips = clips(subdir)
    single = [k for k, v in all_clips.items() if len(v) == 1]
    empty = [k for k, v in all_clips.items() if not v]

    if not quiet:
        for rel in sorted(all_clips):
            frames = all_clips[rel]
            if not frames:
                print(f"{rel:70s} UNSLICED")
                continue
            sizes = sorted({(f[3], f[4]) for f in frames})
            size = "x".join(str(s) for s in sizes[0]) if len(sizes) == 1 else f"MIXED {sizes}"
            print(f"{rel:70s} {len(frames):2d} frames  {size}")

    print()
    print(f"sheets: {len(all_clips)}   animated: {len(all_clips) - len(single) - len(empty)}"
          f"   one-frame: {len(single)}   unsliced: {len(empty)}")
    for rel in sorted(empty):
        print(f"  UNSLICED  {rel}")


if __name__ == "__main__":
    main()
