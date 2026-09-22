"""Validate the bitmap font table and render sample text as ASCII.

usage: python tools/font_check.py                 -> structure + coverage report
       python tools/font_check.py "Some text"     -> ASCII proof of that string
"""
import sys

FONT = "MoonThief/Assets/Resources/Text/font5x8.txt"
CELL_W = 6          # glyph + 1 px gap


def load(path=FONT):
    glyphs = {}
    for n, line in enumerate(open(path, encoding="utf-8"), 1):
        line = line.rstrip("\n").rstrip("\r")
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        key, bar, rows = line.partition("|")
        if not bar:
            print("  ! line %d has no '|': %r" % (n, line))
            continue
        parts = rows.split("/")
        if len(set(len(r) for r in parts)) != 1:
            print("  ! line %d rows have different widths: %r" % (n, rows))
        if len(parts) != 8:
            print("  ! line %d has %d rows, expected 8" % (n, len(parts)))
        ch = " " if key == "space" else key[0]
        glyphs[ch] = parts
    return glyphs


def draw(glyphs, text, scale=1, x_gap=1):
    height = 8 * scale
    out = [[" "] * (CELL_W * scale * len(text) + x_gap) for _ in range(height)]
    for i, ch in enumerate(text):
        g = glyphs.get(ch.lower() if (ch not in glyphs and ch.lower() in glyphs) else ch)
        if g is None:
            g = glyphs["?"]
        for r, row in enumerate(g):
            for c, px in enumerate(row):
                if px != "#":
                    continue
                for dr in range(scale):
                    for dc in range(scale):
                        x = i * CELL_W * scale + c * scale + dc
                        y = r * scale + dr
                        if 0 <= x < len(out[0]) and 0 <= y < height:
                            out[y][x] = "#"
    return out


# ------------------------------------------------------------------ game strings

def strings():
    out = {}
    for line in open("MoonThief/Assets/Resources/Text/strings.txt", encoding="utf-8"):
        line = line.rstrip("\n")
        if not line.strip() or line.startswith("#"):
            continue
        key, bar, val = line.partition("|")
        if bar:
            out[key] = val
    return out


SAMPLES = [
    "THE MOON THIEF",
    "a tale in three nights",
    "Mira waits by the crystal",
    "Marguerite & Sons, 1234567890",
    "jumping quickly; g p q y",
    "TAP A COMMAND, THEN A FOE",
]


def main():
    glyphs = load()
    print("glyphs loaded: %d" % len(glyphs))
    letters = [c for c in glyphs if c.isalpha()]
    print("letters: %d  digits: %d  punct: %d"
          % (len(letters), len([c for c in glyphs if c.isdigit()]),
             len([c for c in glyphs if not c.isalnum() and c != " "])))

    missing = set()
    for key, val in strings().items():
        for ch in val.replace("\\n", "\n").format(*["x"] * 4):
            if ch not in glyphs and ch not in ("\n",):
                missing.add(ch)
    print("characters used by strings.txt but missing from the font: %s"
          % ("".join(sorted(missing)) if missing else "none"))

    if len(sys.argv) > 1:
        for text in sys.argv[1:]:
            print()
            for row in draw(glyphs, text):
                print("   " + "".join(r for r in row).rstrip())
        return

    for text in SAMPLES:
        print()
        for row in draw(glyphs, text):
            print("   " + "".join(row).rstrip())


if __name__ == "__main__":
    main()
