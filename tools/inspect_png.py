"""Read a PNG without any dependency and print it as text.

Used to verify the vertical slice renders: a coarse luminance map shows the composition,
and a 1:1 crop dump shows whether the bitmap font is legible.
"""
import struct
import sys
import zlib

CHARS = " .:-=+*#%@"


def read_png(path):
    data = open(path, "rb").read()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("not a png: " + path)
    pos, idat = 8, b""
    w = h = ct = None
    while pos < len(data):
        ln = struct.unpack(">I", data[pos:pos + 4])[0]
        typ = data[pos + 4:pos + 8]
        chunk = data[pos + 8:pos + 8 + ln]
        pos += 12 + ln
        if typ == b"IHDR":
            w, h, _bd, ct = struct.unpack(">IIBB", chunk[:10])
        elif typ == b"IDAT":
            idat += chunk
        elif typ == b"IEND":
            break
    raw = zlib.decompress(idat)
    bpp = {0: 1, 2: 3, 4: 2, 6: 4}[ct]
    stride = w * bpp
    out = bytearray(w * h * bpp)
    prev = bytearray(stride)
    p = 0
    for y in range(h):
        f = raw[p]; p += 1
        line = bytearray(raw[p:p + stride]); p += stride
        if f == 1:
            for i in range(bpp, stride):
                line[i] = (line[i] + line[i - bpp]) & 255
        elif f == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 255
        elif f == 3:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + ((a + prev[i]) >> 1)) & 255
        elif f == 4:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                b = prev[i]
                c = prev[i - bpp] if i >= bpp else 0
                pp = a + b - c
                pa, pb, pc = abs(pp - a), abs(pp - b), abs(pp - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 255
        out[y * stride:(y + 1) * stride] = line
        prev = line
    return w, h, bpp, out


def lum(buf, bpp, w, x, y):
    i = (y * w + x) * bpp
    return (buf[i] * 30 + buf[i + 1] * 59 + buf[i + 2] * 11) // 100


def composition(path, block=6, ink=26):
    w, h, bpp, buf = read_png(path)
    print("== %s  (%dx%d)  composition, %dpx blocks ==" % (path.split("/")[-1], w, h, block))
    for by in range(0, h, block):
        row = []
        for bx in range(0, w, block):
            total = 0
            n = 0
            for y in range(by, min(by + block, h)):
                for x in range(bx, min(bx + block, w)):
                    total += lum(buf, bpp, w, x, y)
                    n += 1
            avg = total // max(1, n)
            row.append(" " if avg < ink else CHARS[min(len(CHARS) - 1, 1 + avg * (len(CHARS) - 2) // 255)])
        print("".join(row))
    print()


def crop(path, x0, y0, x1, y1, threshold=120, scale=1):
    w, h, bpp, buf = read_png(path)
    x0, x1 = max(0, x0), min(w, x1)
    y0, y1 = max(0, y0), min(h, y1)
    print("== %s crop x%d..%d y%d..%d ==" % (path.split("/")[-1], x0, x1, y0, y1))
    for y in range(y0, y1, scale):
        row = []
        for x in range(x0, x1, scale):
            row.append("#" if lum(buf, bpp, w, x, y) > threshold else ".")
        print("".join(row))
    print()


if __name__ == "__main__":
    base = "MoonThief/Screenshots/"
    if len(sys.argv) > 1 and sys.argv[1] == "crops":
        # title text block (anchor y=Top-6.2, 3x font) and one menu row (MORSEL, 2x font)
        crop(base + "01-title.png", 16, 92, 272, 132)
        crop(base + "02-battle-command.png", 20, 480, 268, 502)
        crop(base + "02-battle-command.png", 60, 322, 240, 350)
    else:
        for name in ["01-title.png", "02-battle-command.png", "03-battle-capture.png",
                     "04-boss-wave.png", "05-chapter-end.png"]:
            composition(base + name)
