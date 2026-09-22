import sys
sys.path.insert(0, "tools")
import inspect_png as I

def dump(png, x0, y0, x1, y1, thr=128, step=1):
    w, h, bpp, buf = I.read_png(png)
    print(f"== {png} x{x0}-{x1} y{y0}-{y1}")
    for y in range(y0, y1):
        row = "".join("#" if I.lum(buf, bpp, w, x, y) > thr else "." for x in range(x0, x1, step))
        print(f"{y:4d} {row}")

if __name__ == "__main__":
    a = sys.argv
    dump(a[1], int(a[2]), int(a[3]), int(a[4]), int(a[5]), int(a[6]) if len(a) > 6 else 128)
