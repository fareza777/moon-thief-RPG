#!/usr/bin/env python3
"""Moon Thief app icon: a hooded thief silhouette hanging off a crescent moon
above the village rooftops. Drawn as pixel art on a 96x96 grid, upscaled to
1024 for the store and down to the adaptive-icon sizes Unity wants."""
import math
from PIL import Image, ImageDraw

W = 96
img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

def px(x, y, c):
    if 0 <= x < W and 0 <= y < W:
        d.point((x, y), fill=c)

# --- night sky: dark navy to a lighter indigo band behind the moon
sky_top = (10, 12, 38)
sky_bot = (26, 32, 84)
for y in range(W):
    t = y / (W - 1)
    r = int(sky_top[0] + (sky_bot[0] - sky_top[0]) * t)
    g = int(sky_top[1] + (sky_bot[1] - sky_top[1]) * t)
    b = int(sky_top[2] + (sky_bot[2] - sky_top[2]) * t)
    d.line([(0, y), (W, y)], fill=(r, g, b, 255))

# --- stars: deterministic scatter, brighter ones get a plus sparkle
stars = [(7, 8), (18, 5), (31, 10), (44, 4), (58, 7), (72, 6), (86, 10),
         (13, 16), (26, 14), (80, 18), (90, 24), (6, 30), (15, 40), (84, 38),
         (8, 55), (90, 52), (12, 68), (89, 66)]
for i, (sx, sy) in enumerate(stars):
    bright = i % 4 == 0
    px(sx, sy, (255, 246, 200, 255 if bright else 150))
    if bright:
        px(sx - 1, sy, (255, 246, 200, 110)); px(sx + 1, sy, (255, 246, 200, 110))
        px(sx, sy - 1, (255, 246, 200, 110)); px(sx, sy + 1, (255, 246, 200, 110))

# --- crescent moon, center (48, 30), radius 17: full disc minus a dark disc
# offset up-right so the lit limb points down-left
mx, my, mr = 48, 30, 17
moon = (255, 224, 130)
moon_hi = (255, 242, 190)
moon_sh = (214, 168, 84)
ox, oy = 6, -5                     # dark disc offset
dr = mr - 3
for y in range(my - mr, my + mr + 1):
    for x in range(mx - mr, mx + mr + 1):
        infull = (x - mx) ** 2 + (y - my) ** 2 <= mr * mr
        inbite = (x - (mx + ox)) ** 2 + (y - (my + oy)) ** 2 <= dr * dr
        if infull and not inbite:
            # lit limb: highlight near the thin tip, shade near the dark bite
            edge = math.sqrt((x - mx) ** 2 + (y - my) ** 2)
            c = moon_hi if edge > mr - 2.5 else moon
            if x > mx + 4 and y < my - 4:
                c = moon_sh
            px(x, y, c)

# --- thief silhouette: hooded figure dangling from the moon's lower horn
sil = (12, 10, 24)
sil_hi = (26, 22, 46)
hx, hy = 43, 47                   # hanging point under the moon
hood = [(hx + 1, hy - 3), (hx, hy - 2), (hx + 1, hy - 2), (hx + 2, hy - 2),
        (hx - 1, hy - 1), (hx, hy - 1), (hx + 1, hy - 1), (hx + 2, hy - 1), (hx + 3, hy - 1),
        (hx - 1, hy), (hx, hy), (hx + 1, hy), (hx + 2, hy), (hx + 3, hy),
        (hx - 1, hy + 1), (hx, hy + 1), (hx + 1, hy + 1), (hx + 2, hy + 1)]
body = [(hx, hy + 2), (hx + 1, hy + 2), (hx + 2, hy + 2),
        (hx - 1, hy + 3), (hx, hy + 3), (hx + 1, hy + 3), (hx + 2, hy + 3),
        (hx - 1, hy + 4), (hx, hy + 4), (hx + 1, hy + 4), (hx + 2, hy + 4),
        (hx - 2, hy + 5), (hx - 1, hy + 5), (hx, hy + 5), (hx + 1, hy + 5),
        (hx - 2, hy + 6), (hx - 1, hy + 6), (hx, hy + 6), (hx + 1, hy + 6), (hx + 2, hy + 6)]
# arms: one hand gripping the horn, one arm trailing
arm_up = [(hx + 3, hy - 2), (hx + 4, hy - 3), (hx + 4, hy - 4)]
arm_lo = [(hx - 2, hy + 4), (hx - 3, hy + 5), (hx - 3, hy + 6)]
# legs dangling, knees bent
legs = [(hx, hy + 7), (hx + 1, hy + 7), (hx + 2, hy + 7), (hx + 3, hy + 7),
        (hx - 1, hy + 8), (hx, hy + 8), (hx + 2, hy + 8), (hx + 3, hy + 8), (hx + 4, hy + 8),
        (hx - 1, hy + 9), (hx + 3, hy + 9), (hx + 4, hy + 9)]
for c in hood + body + arm_up + arm_lo + legs:
    px(c[0], c[1], sil)
# a satchel (the stolen goods) on his back, and a lighter rim on the moon side
px(hx - 1, hy + 7, sil_hi); px(hx + 4, hy - 2, sil_hi); px(hx + 3, hy - 3, sil_hi)
satchel = (40, 30, 18)
px(hx - 3, hy + 3, satchel); px(hx - 3, hy + 4, satchel); px(hx - 4, hy + 3, satchel)

# --- rooftops silhouette across the bottom
roof = (8, 8, 20)
roof_hi = (18, 16, 36)
win = (255, 214, 120, 255)
houses = [(0, 74, 12, 96), (14, 78, 24, 96), (26, 71, 38, 96), (40, 76, 52, 96),
          (54, 72, 66, 96), (68, 78, 78, 96), (80, 74, 95, 96)]
for x0, y0, x1, y1 in houses:
    d.rectangle([x0, y0, x1, y1], fill=roof + (255,))
    # pitched roof
    for y in range(y0 - 6, y0):
        w = int((y - (y0 - 6)) * ((x1 - x0) / 2) / 6)
        d.line([(x0 + (x1 - x0) // 2 - w, y), (x0 + (x1 - x0) // 2 + w, y)], fill=roof_hi + (255,))
    # one warm window
    if (x1 - x0) > 8:
        wx = x0 + 3
        d.rectangle([wx, y0 + 4, wx + 2, y0 + 6], fill=win)

# --- upscale to 1024 (nearest) and save + a small adaptive set
out = img.resize((1024, 1024), Image.NEAREST)
out.save("/home/ubuntu/repos/moon-thief-RPG/MoonThief/Assets/Art/icon-1024.png")
img.resize((432, 432), Image.NEAREST).save("/home/ubuntu/repos/moon-thief-RPG/MoonThief/Assets/Art/icon-432.png")
img.resize((192, 192), Image.NEAREST).save("/home/ubuntu/repos/moon-thief-RPG/MoonThief/Assets/Art/icon-192.png")
img.resize((162, 162), Image.NEAREST).save("/home/ubuntu/repos/moon-thief-RPG/MoonThief/Assets/Art/icon-162.png")
img.resize((108, 108), Image.NEAREST).save("/home/ubuntu/repos/moon-thief-RPG/MoonThief/Assets/Art/icon-108.png")
img.resize((81, 81), Image.NEAREST).save("/home/ubuntu/repos/moon-thief-RPG/MoonThief/Assets/Art/icon-81.png")
print("icons written")
