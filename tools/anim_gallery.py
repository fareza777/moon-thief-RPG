"""Build an animated contact sheet of every clip in the art pack.

A still frame can never show whether an animation is right, and reading the code cannot show
which frames exist. This tool slices the same sheets the game slices, in the same order, and
writes every clip as a strip that plays at the rate the game plays it - so a walk cycle can be
judged by eye, and a missing or mis-sliced frame is obvious instead of invisible.

Two sources, because the project uses both:
  * sheets the Unity importer already sliced -> the .png.meta is authoritative (Art/Hero/...)
  * sheets sliced at runtime by TexArt.Cell/Grid -> the cell size lives in code, so GRID_RULES
    below repeats the constants from WorldView / GameDefs.

usage:  python tools/anim_gallery.py
writes: preview/anim.html   (self-contained: every strip is a data URI)
"""
import base64
import io
import os
import re
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "MoonThief", "Assets", "Resources")

SPRITE_RE = re.compile(
    r"name:\s*(\S+)\s*\n"
    r"\s+rect:\s*\n"
    r"\s+serializedVersion:\s*\d+\s*\n"
    r"\s+x:\s*(-?\d+)\s*\n"
    r"\s+y:\s*(-?\d+)\s*\n"
    r"\s+width:\s*(\d+)\s*\n"
    r"\s+height:\s*(\d+)"
)

# The cell size the game slices each family with. Where the file name carries it ("_16x32"),
# the name wins - the pack is consistent about that and the code reads it the same way.
GRID_RULES = [
    ("Art/Char/chara_", 16, 20),            # 3 x 4: villagers, front row first
    ("Pack/Chara/chara_", 16, 20),          # the wider cast, same shape
    ("Art/Mon/Monsters_", 48, 48),          # 3 x 4: wild monsters
    ("Pack/Monsters/", 48, 48),
    ("Pack/Animals/", 16, 20),              # 3 x 4: cats, birds, foxes, mice, rabbits
    ("Pack/Props/Fires/", 16, 32),          # 3 or 6 frames in a row
    ("Pack/Props/Torches/", 16, 32),        # 3 frames
    ("Pack/Anim/Chest/", 18, 32),           # 4 frames: three closed designs + lid up
    ("Pack/Anim/Cristal/", 16, 32),
    ("Pack/Anim/Door/", 16, 16),
    ("Pack/Anim/Water/", 16, 16),
    ("Art/Obj/chest_", 18, 32),
    ("Art/Obj/cristal_", 16, 32),
    ("Art/Obj/fire_", 16, 32),
    ("Art/Obj/torch_", 16, 32),
    ("Art/Obj/door_", 16, 16),
    ("Art/Obj/house_autotile", 48, 48),
]

# Playback rates, taken from the Play() calls in the game, so the page shows what the player sees.
FPS = [
    ("Art/Hero/hero/", "walk", 8.0),
    ("Art/Hero/hero/", "breath_idle", 4.0),
    ("Art/Hero/hero/", "attack", 15.0),
    ("Art/Hero/hero/", "hit", 14.0),
    ("Art/Hero/hero/", "bow", 12.0),
    ("Art/Hero/hero/", "spin", 10.0),
    ("Art/Char/", "", 3.0),
    ("Pack/Chara/", "", 4.5),
    ("Art/Mon/", "", 5.0),
    ("Pack/Animals/", "", 5.5),
    ("Pack/Anim/Chest/", "", 7.0),
    ("Art/Obj/chest", "", 7.0),
    ("Pack/Anim/Cristal/", "", 6.0),
    ("Art/Obj/cristal", "", 6.0),
    ("Pack/Anim/Water/", "", 5.0),
    ("Pack/Props/Fires/", "", 9.0),
    ("Pack/Props/Torches/", "", 9.0),
    ("Pack/Anim/Door/", "", 1.0),
    ("Art/Obj/door", "", 1.0),
]

SECTIONS = [
    ("Hero", "Art/Hero/",
     "The only sheet the importer pre-sliced. Its clips are the battle rigs too: a 7 frame swing, "
     "a 3 frame stagger, a 5 frame idle breath. The game swaps the strips per <b>Dir</b>, and the "
     "attack frames are wide on purpose - the last ones are the arc of the blade."),
    ("Villagers", "Art/Char/",
     "3 frames per row, four rows, front row first (row 0 = DOWN, 1 = LEFT, 2 = RIGHT, 3 = UP). "
     "The six in <code>Art/Char</code> are the classic cast."),
    ("The pack's wider cast", "Pack/Chara/",
     "The same 3 x 4 shape, straight out of the pack: the newcomers (FINN, PIP, ODA, NAIL and the "
     "rest of the roster) are picked by path, so adding a villager is a name and two lines."),
    ("Wild monsters", "Art/Mon/",
     "3 frames x 4 rows of 48 px. All twelve frames shipped; only the front strip used to be drawn, "
     "so a beast walking north was still looking at the camera. The row now follows the walk."),
    ("Animals", "Pack/Animals/",
     "Cats, birds, foxes, mice and rabbits are the same 3 x 4 shape as the villagers - the "
     "critters hop through one row at a time."),
    ("Chests", "Pack/Anim/Chest/",
     "72 x 32 = four 18 x 32 frames: three closed designs and the same chest with its lid up. "
     "Frame 3 is what a chest flips to when it is opened."),
    ("Crystal", "Pack/Anim/Cristal/",
     "The shard the night hangs on: three frames of glow, played at 6 fps."),
    ("Fire and light", "Pack/Props/",
     "Roadside torches and campfires: 3 to 6 frames in a row, played at 9 fps with the light pool "
     "flickering on its own curve underneath."),
    ("Water", "Pack/Anim/Water/",
     "One ripple sprite per pond tile, each started at a different phase so the pond is never in step."),
]

CACHE = {}


def sheet(path):
    """RGB(A) sheet by pack path, e.g. Pack/Props/Torches/Torch_01."""
    if path in CACHE:
        return CACHE[path]
    full = os.path.join(ART, path.replace("/", os.sep) + ".png")
    im = Image.open(full).convert("RGBA") if os.path.exists(full) else None
    CACHE[path] = im
    return im


def meta_frames(path):
    """[(name, x, y, w, h)] from the importer metadata, in frame order."""
    full = os.path.join(ART, path.replace("/", os.sep) + ".png.meta")
    if not os.path.exists(full):
        return []
    with open(full, "r", encoding="utf-8", errors="replace") as fh:
        text = fh.read()
    out = [(m.group(1), int(m.group(2)), int(m.group(3)), int(m.group(4)), int(m.group(5)))
           for m in SPRITE_RE.finditer(text)]

    def key(entry):
        tail = entry[0].rsplit("_", 1)
        return int(tail[1]) if len(tail) == 2 and tail[1].isdigit() else 0

    return sorted(out, key=key)


def grid_cell(path):
    """Cell size the game slices this sheet with, or None when the sheet is a single image."""
    base = os.path.basename(path)
    m = re.search(r"_(\d{2,3})x(\d{2,3})(?:_|$)", base)
    if m:
        return int(m.group(1)), int(m.group(2))
    for prefix, w, h in GRID_RULES:
        if path.startswith(prefix):
            return w, h
    return None


def frames_of(path):
    """Every frame of one sheet as PIL images, in the order the game reads them."""
    im = sheet(path)
    if im is None:
        return []
    meta = meta_frames(path)
    if meta:
        # The importer's rects are in texture space: origin bottom-left. Cropping them with a
        # top-left origin shifts every frame up by its own height and reads as a half-empty
        # sheet - which is exactly how the hero's swing looked before this line existed.
        return [im.crop((x, im.height - (y + h), x + w, im.height - y))
                for (_n, x, y, w, h) in meta]
    cell = grid_cell(path)
    if cell is None:
        return [im]
    cw, ch = cell
    cols, rows = im.width // cw, im.height // ch
    if cols * rows <= 1:
        return [im]
    out = []
    for r in range(rows):                       # r = 0 is the bottom strip, like TexArt.Grid
        for c in range(cols):
            y = im.height - (r + 1) * ch
            out.append(im.crop((c * cw, y, c * cw + cw, y + ch)))
    return out


def fps_of(path):
    for prefix, needle, fps in FPS:
        if path.startswith(prefix):
            if not needle or needle in path:
                return fps
    return 4.0


def strip_uri(frames, zoom):
    """One PNG holding every frame side by side, scaled by a whole number of pixels.

    Frames of one clip are NOT always the same size: the hero's attack is seven frames of 18x22
    up to 54x52, because the last of them carry the arc of the blade. Every frame is padded into
    a cell the size of the biggest one and centred in it, which is how the engine draws them -
    each sprite hangs off its own centre, not off a shared corner."""
    w = max(f.width for f in frames)
    h = max(f.height for f in frames)
    sheet_img = Image.new("RGBA", (w * len(frames), h))
    for i, f in enumerate(frames):
        sheet_img.paste(f, (i * w + (w - f.width) // 2, (h - f.height) // 2))
    if zoom != 1:
        sheet_img = sheet_img.resize((sheet_img.width * zoom, sheet_img.height * zoom), Image.NEAREST)
    buf = io.BytesIO()
    sheet_img.save(buf, "PNG", optimize=True)
    return base64.b64encode(buf.getvalue()).decode("ascii"), w * zoom, h * zoom


def zoom_for(w, h):
    if w >= 48:
        return 2
    if w >= 24:
        return 3
    return 4


def walk_sheets(root):
    """Every .png under a folder that is worth animating, sorted."""
    full = os.path.join(ART, root.replace("/", os.sep))
    out = []
    for dirpath, _dirs, files in os.walk(full):
        for name in sorted(files):
            if not name.endswith(".png"):
                continue
            rel = os.path.relpath(os.path.join(dirpath, name), ART).replace("\\", "/")
            out.append(rel[:-4])
    return sorted(out)


def clip_card(path, frames):
    if not frames:
        return None, 0
    zoom = zoom_for(*frames[0].size)
    uri, w, h = strip_uri(frames, zoom)
    n = len(frames)
    fps = fps_of(path)
    dur = max(0.12, n / fps)
    name = os.path.basename(path)
    cell = grid_cell(path)
    cell_txt = f"{cell[0]}x{cell[1]}" if cell else f"{frames[0].width}x{frames[0].height} (single)"
    div = f"""      <figure class="clip">
        <div class="anim" style="--sw:{w * n}px; --dur:{dur:.3f}s; --n:{n}; width:{w}px; height:{h}px;
             background-image:url(data:image/png;base64,{uri})"></div>
        <figcaption><b>{name}</b><span>{n} frame{'s' if n != 1 else ''} &middot; cell {cell_txt} &middot; {fps:g} fps</span></figcaption>
      </figure>"""
    return div, n


# The rules that make a strip play. Kept in one place because two pages use them: the standalone
# gallery, and the game preview, which shows the same clips as its last section.
ANIM_CSS = """
  .agrid { display: flex; flex-wrap: wrap; gap: 14px; }
  figure.clip { margin: 0; padding: 6px; border: 1px solid #241f3d; border-radius: 8px;
                background: #100e1c; }
  .anim { image-rendering: pixelated; background-repeat: no-repeat; background-color: #171528;
          background-size: var(--sw) auto; background-position: 0 0;
          animation: play var(--dur) steps(var(--n, 1)) infinite; }
  figure.clip figcaption { display: flex; flex-direction: column; margin-top: 5px; font-size: 11.5px;
                           color: #8d89a6; max-width: 190px; }
  figure.clip figcaption b { color: #d8d4ee; font-weight: 600; word-break: break-all; }
  body.slow .anim { animation-duration: calc(var(--dur) * 6); }
  body.frozen .anim { animation-play-state: paused; }
  @keyframes play { from { background-position-x: 0; }
                    to   { background-position-x: calc(-1 * var(--sw)); } }
"""

ANIM_JS = """
  // The step count is written per clip, but confirm it from the rendered strip: a browser that
  // rounds the width would otherwise skip or repeat a frame at the seam.
  document.querySelectorAll('.anim').forEach(function (el) {
    var sw = parseFloat(getComputedStyle(el).getPropertyValue('--sw'));
    var w = el.clientWidth;
    if (sw > 0 && w > 0) el.style.setProperty('--n', Math.max(1, Math.round(sw / w)));
  });
  var pb = document.getElementById('btn-pause');
  if (pb) pb.onclick = function () {
    document.body.classList.toggle('frozen');
    this.setAttribute('aria-pressed', document.body.classList.contains('frozen'));
    this.textContent = document.body.classList.contains('frozen') ? 'Play' : 'Pause';
  };
  var sb = document.getElementById('btn-slow');
  if (sb) sb.onclick = function () {
    document.body.classList.toggle('slow');
    this.setAttribute('aria-pressed', document.body.classList.contains('slow'));
  };
"""


ANIM_CONTROLS = """<div class="bar">
  <button id="btn-pause" aria-pressed="false">Pause</button>
  <button id="btn-slow" aria-pressed="false">Slow motion (&times;6)</button>
  <span class="legend">Each clip plays at its own frame count and fps, exactly as the game runs it.</span>
</div>"""


def build_sections():
    """The animated contact sheet as HTML sections plus its totals, for any page to embed."""
    body = []
    total = 0
    sheets = 0
    for title, root, note in SECTIONS:
        if not os.path.isdir(os.path.join(ART, root.replace("/", os.sep))):
            continue
        cards = []
        for path in walk_sheets(root):
            frames = frames_of(path)
            if len(frames) < 2:
                continue
            card, n = clip_card(path, frames)
            if card:
                cards.append(card)
                total += n
                sheets += 1
        if not cards:
            continue
        body.append(f"<h2>{title}</h2>\n<p class='note'>{note}</p>\n<div class='agrid'>\n"
                    + "\n".join(cards) + "\n</div>")
    return "\n".join(body), total, sheets


PAGE = """<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<title>The Moon Thief - every animation in the pack</title>
<style>
  :root {{ color-scheme: dark; }}
  * {{ box-sizing: border-box; }}
  body {{ margin: 0; padding: 28px 22px 90px; background: #0b0a14; color: #e8e6f2;
         font: 14px/1.55 ui-sans-serif, system-ui, "Segoe UI", sans-serif; }}
  h1 {{ font-size: 22px; margin: 0 0 6px; letter-spacing: .3px; }}
  h2 {{ font-size: 15px; margin: 34px 0 4px; color: #ffd98a; letter-spacing: .4px; }}
  p.lede {{ margin: 0 0 6px; color: #a9a5c0; max-width: 78ch; }}
  p.note {{ margin: 0 0 14px; color: #8d89a6; max-width: 88ch; }}
  code {{ background: #1b1830; padding: 1px 4px; border-radius: 3px; font-size: 12px; }}
  .bar {{ position: sticky; top: 0; z-index: 5; display: flex; gap: 10px; align-items: center;
          flex-wrap: wrap; padding: 10px 0 12px; margin-bottom: 4px;
          background: linear-gradient(#0b0a14 78%, transparent); }}
  button {{ font: inherit; font-size: 13px; padding: 5px 12px; border-radius: 6px; cursor: pointer;
            background: #241f3d; color: #e8e6f2; border: 1px solid #3b3462; }}
  button[aria-pressed="true"] {{ background: #4a3f86; border-color: #6f61c0; }}
  .legend {{ color: #8d89a6; font-size: 12px; }}
  a {{ color: #ffd98a; }}
<!--ANIMCSS-->
</style></head>
<body class="">
<h1>The Moon Thief &mdash; every animation in the pack</h1>
<p class="lede">Every clip below is sliced out of the same sheet, with the same cell size and in the
same frame order the game uses, and plays at the rate the game plays it. A still frame cannot show a
walk cycle; this page can.</p>
<p class="lede">The same clips are the last section of the game page in the Preview tab, next to
the frames of the run.</p>
<p class="note">{count} clips from {sheets} sheets. The strips are cut by
<code>tools/anim_gallery.py</code>: pre-sliced sheets are read from their importer metadata, the rest
are sliced with the cell sizes in <code>GRID_RULES</code> (the same constants as
<code>TexArt.Cell/Grid</code>).</p>

<!--CONTROLS-->

{body}

<script>
<!--ANIMJS-->
</script>
</body></html>
"""


def main():
    body, total, sheets = build_sections()
    # The CSS and JS blobs are replaced rather than formatted: they are full of braces, and a
    # brace that survives one too many escapes silently kills every rule after it.
    html = (PAGE.format(count=total, sheets=sheets, body=body)
                .replace("<!--ANIMCSS-->", ANIM_CSS)
                .replace("<!--ANIMJS-->", ANIM_JS)
                .replace("<!--CONTROLS-->", ANIM_CONTROLS))
    out = os.path.join(ROOT, "preview", "anim.html")
    with open(out, "w", encoding="utf-8") as fh:
        fh.write(html)
    print(f"wrote {out}")
    print(f"  {total} frames across {sheets} animated sheets, {os.path.getsize(out) / 1e6:.2f} MB")


if __name__ == "__main__":
    main()
