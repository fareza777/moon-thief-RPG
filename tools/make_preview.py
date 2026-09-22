"""Build a self-contained preview page for THE MOON THIEF (full game).

The PNGs are inlined as data URIs, so the page renders from any location without a web server
and without depending on relative asset paths.

usage:  python tools/make_preview.py
writes: preview/slice.html
"""
import base64
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "preview", "slice.html")

# The animated clip sheet lives in tools/anim_gallery.py and is embedded here as the last section:
# the Preview tab serves ONE file, so a link to a second page would simply be a 404. One page that
# carries both the run's frames and every animation in the pack is also the point - a walk cycle
# cannot be judged from a still, and a still cannot be judged from a clip.
sys.path.insert(0, os.path.join(ROOT, "tools"))
import anim_gallery

SHOTS = [
    ("MoonThief/Screenshots/01-splash.png", "01 - Studio card", "The boot screen: who made the night tale."),
    ("MoonThief/Screenshots/02-menu.png", "02 - Main menu", "NEW NIGHT / CONTINUE / SETTINGS / CREDITS over the title art."),
    ("MoonThief/Screenshots/03-settings.png", "03 - Settings", "Text speed, sound and screen shake, kept between runs."),
    ("MoonThief/Screenshots/04-credits.png", "04 - Credits", "The thank-you card: moon, title, measured credit block, BACK."),
    ("MoonThief/Screenshots/05-story.png", "05 - Story slide", "The intro cinematic: short lines over a night backdrop."),
    ("MoonThief/Screenshots/06-nightcard.png", "06 - Night card", "The card that opens each of the three nights."),
    ("MoonThief/Screenshots/07-village.png", "07 - Village", "Quiet Hollow: houses from the pack, lamps, critters, the crystal. Compact HUD, and a name plate only for the villager you are standing beside."),
    ("MoonThief/Screenshots/08-dialog.png", "08 - Dialog", "Mira sets the quest, with the goal line already on the HUD."),
    ("MoonThief/Screenshots/09-plaza.png", "09 - Plaza", "Market clutter: pots, barrels, crates, torches, a cat."),
    ("MoonThief/Screenshots/10-fields.png", "10 - Fields", "Crop rows, roadside torches, wild monsters out in the open."),
    ("MoonThief/Screenshots/11-battle-command.png", "11 - Battle: command", "The 2x2 command menu; the control hint now sits on its own line under NIGHT."),
    ("MoonThief/Screenshots/12-battle-action.png", "12 - Battle: action", "Damage numbers, a hurt hero, the befriend call."),
    ("MoonThief/Screenshots/13-battle-card.png", "13 - Battle: result", "The measured result card on top of a dimmed arena."),
    ("MoonThief/Screenshots/14-pause.png", "14 - Pause", "Pause, save, settings and leave, on top of the frozen world."),
    ("MoonThief/Screenshots/15-forest.png", "15 - Forest (night 3)", "The dark wood, with the Pale Guard blocking the road north."),
    ("MoonThief/Screenshots/16-ending.png", "16 - Ending", "The moon returns. End of the run."),
    ("MoonThief/Screenshots/17-interior-shrine.png", "17 - Inside: shrine", "A room of its own: masonry, lamp, statue, rug, shrine shelf, doormat."),
    ("MoonThief/Screenshots/18-interior-kitchen.png", "18 - Inside: kitchen", "The sixth house: bed, table, pots, barrels, sacks - and the way out."),
    ("MoonThief/Screenshots/19-journal-hub.png", "19 - Journal hub", "CHARACTER / BAG / EQUIPMENT / BESTIARY / QUESTS, opened from pause."),
    ("MoonThief/Screenshots/20-journal-character.png", "20 - Character sheet", "Every row labelled from the string table: level, XP, shards, kills, gold, and the three worn slots - tap one to change it."),
    ("MoonThief/Screenshots/21-journal-quests.png", "21 - Quest log", "Main story and side errands, each with its own state word."),
    ("MoonThief/Screenshots/22-bestiary.png", "22 - Bestiary", "Every species, with HP and attack once the run has met it."),
    ("MoonThief/Screenshots/23-dialog-toast.png", "23 - Talk & toast", "A quest is accepted: the notification now sits in its own slot above the dialog box instead of on the narration."),
    ("MoonThief/Screenshots/24-loot-toast.png", "24 - Loot toast", "A chest gives an item: the toast is measured and clears the HUD, the banner and the name plates."),
    ("MoonThief/Screenshots/25-zone-banner.png", "25 - Night banner", "Two-line zone card: the night in the big face, the place in the small one - 106 px instead of running the full 288."),
    ("MoonThief/Screenshots/26-chest.png", "26 - Treasure chest", "A chest standing on its tile: four 18x32 frames per sheet, sliced on the art instead of the file name."),
    ("MoonThief/Screenshots/Runtime/09-splash.png", "R1 - Splash (runtime)", "Captured from the shipped player, not the editor."),
    ("MoonThief/Screenshots/Runtime/10-menu.png", "R2 - Menu (runtime)", "The real main menu, after the splash."),
    ("MoonThief/Screenshots/Runtime/11-village.png", "R3 - Explore (runtime)", "The self-test walking the village at night."),
    ("MoonThief/Screenshots/Runtime/13-battle-1.png", "R4 - Battle (runtime)", "The same battle, played by the self-test."),
    ("MoonThief/Screenshots/Runtime/14-card.png", "R5 - Result card (runtime)", "The result card as the player sees it."),
    ("MoonThief/Screenshots/Runtime/16-bosscard.png", "R6 - Boss card (runtime)", "Victory over the Pale Guard, from the real player."),
    ("MoonThief/Screenshots/Runtime/19-pause.png", "R7 - Pause (runtime)", "RESUME / SAVE PROGRESS / SETTINGS / LEAVE TO TITLE, captured in play."),
    ("MoonThief/Screenshots/Runtime/20-settings.png", "R8 - Settings (runtime)", "The settings page open from the pause card, out in the world."),
    ("MoonThief/Screenshots/Runtime/18-ending.png", "R9 - Ending (runtime)", "The loop closes: splash to ending."),
    ("MoonThief/Screenshots/Runtime/27-journal.png", "R10 - Journal (runtime)", "The journal hub built and drawn by the player itself."),
    ("MoonThief/Screenshots/Runtime/28-quests.png", "R11 - Quest log (runtime)", "The quest page as the shipped build draws it."),
    ("MoonThief/Screenshots/Runtime/12-explore-far.png", "R12 - Explore, far north (runtime)", "The fields beyond Quiet Hollow, walked by the shipped player."),
    ("MoonThief/Screenshots/Runtime/13-battle-2.png", "R13 - Battle, round 2 (runtime)", "A second round of the wild fight."),
    ("MoonThief/Screenshots/Runtime/13-battle-3.png", "R14 - Battle, round 3 (runtime)", "Damage numbers mid swing."),
    ("MoonThief/Screenshots/Runtime/13-battle-4.png", "R15 - Battle, round 4 (runtime)", "The enemy down to its last hit points."),
    ("MoonThief/Screenshots/Runtime/13-battle-5.png", "R16 - Battle, round 5 (runtime)", "The befriend call landing on a weakened beast."),
    ("MoonThief/Screenshots/Runtime/13-battle-6.png", "R17 - Battle, round 6 (runtime)", "The last exchange before the arena clears."),
    ("MoonThief/Screenshots/Runtime/14-card-2.png", "R18 - Result card, second fight (runtime)", "The card again after the next encounter, drawn by the player."),
    ("MoonThief/Screenshots/Runtime/15-bosszone.png", "R19 - Boss zone (runtime)", "The dark road where the Pale Guard waits."),
    ("MoonThief/Screenshots/Runtime/17-after-boss.png", "R20 - After the boss (runtime)", "The night clears once the guard falls."),
]

PAGE = """<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<title>The Moon Thief - game preview</title>
<style>
  :root { color-scheme: dark; }
  * { box-sizing: border-box; }
  body { margin: 0; padding: 32px 24px 80px; background: #0b0a14; color: #e8e6f2;
         font: 15px/1.5 "Segoe UI", system-ui, sans-serif; }
  header { max-width: 1180px; margin: 0 auto 28px; }
  h1 { margin: 0 0 6px; font-size: 30px; letter-spacing: .5px; }
  h1 span { color: #ffd98a; }
  .sub { color: #9a95b8; max-width: 60ch; }
  .nav { margin: 10px 0 0; font-size: 13.5px; color: #9a95b8; }
  .nav a { color: #ffd98a; }
  .bar { position: sticky; top: 0; z-index: 5; display: flex; gap: 10px; align-items: center;
         flex-wrap: wrap; padding: 10px 0 12px; max-width: 1180px; margin: 0 auto;
         background: linear-gradient(#0b0a14 78%, transparent); }
  .bar button { font: inherit; font-size: 13px; padding: 5px 12px; border-radius: 6px; cursor: pointer;
                background: #241f3d; color: #e8e6f2; border: 1px solid #3b3462; }
  .bar button[aria-pressed="true"] { background: #4a3f86; border-color: #6f61c0; }
  .plan { max-width: 1180px; margin: 10px auto 0; display: flex; flex-wrap: wrap;
          gap: 14px; align-items: flex-start; }
  .plan figure { padding: 6px; }
ANIM_CSS_PLACEHOLDER
  .meta { margin-top: 12px; display: flex; flex-wrap: wrap; gap: 8px; }
  .chip { background: #1b1830; border: 1px solid #2c2748; border-radius: 999px;
          padding: 3px 12px; font-size: 12.5px; color: #bdb7d8; }
  /* Every frame is shown at a whole multiple of its native 288x512 so no pixel is ever
     resampled - fractional scaling turns a crisp bitmap font into uneven, muddy strokes. */
  :root { --shot-w: 576px; }
  @media (max-width: 700px) { :root { --shot-w: 288px; } }
  .grid { margin: 0 auto; display: grid; gap: 22px; justify-content: center;
          grid-template-columns: repeat(auto-fill, var(--shot-w)); }
  figure { margin: 0; background: #14122a; border: 1px solid #262042; border-radius: 14px;
           padding: 12px; }
  figure img { width: var(--shot-w); height: auto; display: block; border-radius: 8px;
               image-rendering: pixelated; background: #05040c; }
  figcaption { margin-top: 10px; font-size: 13px; color: #a8a3c6; }
  figcaption b { display: block; color: #f0edff; font-size: 14px; margin-bottom: 2px; }
  /* The contact sheet: every frame of the run at thumbnail size, so the whole game can be
     judged in one screen before scrolling into the full-size frames below. */
  .sheet { max-width: 1180px; margin: 0 auto 32px; display: grid; gap: 10px;
           grid-template-columns: repeat(auto-fill, 84px); justify-content: center; }
  .sheet a { text-decoration: none; color: #9a95b8; }
  .sheet img { width: 84px; height: 149px; display: block; border-radius: 5px; border: 1px solid #2c2748;
               image-rendering: pixelated; background: #05040c; }
  .sheet span { display: block; font-size: 10px; margin-top: 3px; letter-spacing: .2px; }
  .sheet a:hover img { border-color: #ffd98a; }
  h2 { max-width: 1180px; margin: 26px auto 14px; font-size: 17px; color: #ffd98a;
       font-weight: 600; letter-spacing: .4px; }
  footer { max-width: 1180px; margin: 34px auto 0; color: #8b86a8; font-size: 13px; }
  code { background: #1b1830; padding: 1px 6px; border-radius: 5px; color: #cdc7ea; }
</style></head><body>
<header>
  <h1>The Moon Thief <span>&mdash; game preview</span></h1>
  <p class="sub">Portrait monster-taming JRPG built from the Super Retro Collection art pack:
  explore a moonlit world, talk to villagers, fight turn based battles with a befriending system,
  chase the thief across three chapters to the ending.</p>
  <div class="meta"><span class="chip">288&times;512 render target</span>
    <span class="chip">5&times;8 bitmap font</span><span class="chip">shown at 2&times;</span>
    <span class="chip">Android ARM64</span>
    <span class="chip">no prefabs, stage built from code</span></div>
  <p class="nav">At the foot of this page: <a href="#animations">every animation in the pack</a>,
  playing at its in-game rate &mdash; a still frame cannot show a walk cycle.</p>
</header>
<h2>All frames at a glance</h2>
<div class="sheet">
<!--SHEET-->
</div>
<h2>Every frame, 1:1</h2>
<div class="grid">
<!--CARDS-->
</div>
<h2 id="animations">Every animation in the pack</h2>
<p class="sub">Every clip below is sliced out of the same sheet, with the same cell size and in the
same frame order the game uses, and plays at the rate the game plays it. A still frame cannot show a
walk cycle; this page can.</p>
<p class="nav">{anim_note}</p>
<!--CONTROLS-->
<!--ANIM-->

<footer>Rendered by the game itself: the editor frames come from the build camera, the
<code>R</code> frames from <code>-selftest</code> running the shipped player end to end.</footer>
<script>
<!--ANIMJS-->
</script>
</body></html>
"""

CARD = """  <figure id="f{index}">
    <img src="data:image/png;base64,{data}" width="288" height="512" alt="{alt}">
    <figcaption><b>{title}</b>{caption}</figcaption>
  </figure>"""

SHEET_CARD = """  <a href="#f{index}" title="{title}">
    <img src="data:image/png;base64,{data}" width="288" height="512" alt="{alt}"><span>{short}</span>
  </a>"""


def write_contact_sheet(frames):
    """One PNG holding every frame, numbered, so the whole run can be looked at without
    scrolling a page or opening 46 files.
    """
    from PIL import Image, ImageDraw, ImageFont

    cols, cell_w, cell_h, pad, label_h = 8, 288, 512, 10, 22
    rows = (len(frames) + cols - 1) // cols
    w = cols * cell_w + (cols + 1) * pad
    h = rows * (cell_h + label_h) + (rows + 1) * pad
    sheet = Image.new("RGB", (w, h), (11, 10, 20))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 16)
    except Exception:
        font = ImageFont.load_default()
    for i, (path, title, _caption) in enumerate(frames):
        full = os.path.join(ROOT, path.replace("/", os.sep))
        img = Image.open(full).convert("RGB")
        cx, cy = i % cols, i // cols
        x = pad + cx * (cell_w + pad)
        y = pad + cy * (cell_h + label_h + pad)
        sheet.paste(img, (x, y))
        draw.text((x + 2, y + cell_h + 3), title.split(" -")[0][:34], (240, 237, 255), font=font)
    out = os.path.join(ROOT, "MoonThief", "Screenshots", "_all-frames.png")
    sheet.save(out)
    print("wrote %s (%dx%d, %d frames)" % (os.path.relpath(out, ROOT), w, h, len(frames)))


def main():
    cards, sheet, missing = [], [], []
    for path, title, caption in SHOTS:
        full = os.path.join(ROOT, path.replace("/", os.sep))
        if not os.path.exists(full):
            missing.append(path)
            continue
        with open(full, "rb") as fh:
            data = base64.b64encode(fh.read()).decode("ascii")
        index = len(cards)
        # the thumbnail label is just the frame's number: "01" .. "26", then the runtime takes
        # ("R1" .. "R11") - the title itself is on the full-size caption below
        head = title.split(" ")[0]
        short = head if head.startswith("R") else head.lstrip("0") or "0"
        cards.append(CARD.format(data=data, title=title, caption=caption, alt=title, index=index))
        sheet.append(SHEET_CARD.format(data=data, title=title, alt=title, index=index, short=short))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    # The animated clips are built by the same code the standalone gallery uses, then folded in.
    anim_body, clips, clip_sheets = anim_gallery.build_sections()
    anim_note = (f"{clips} frames across {clip_sheets} sheets, cut by <code>tools/anim_gallery.py</code>: "
                 "pre-sliced sheets are read from their importer metadata, the rest are sliced with "
                 "the cell sizes in <code>GRID_RULES</code> (the same constants as "
                 "<code>TexArt.Cell/Grid</code>).")
    # plain replace: the stylesheet is full of braces, so str.format would choke on it
    page = (PAGE.replace("<!--CARDS-->", "\n".join(cards))
                .replace("<!--SHEET-->", "\n".join(sheet))
                .replace("ANIM_CSS_PLACEHOLDER", anim_gallery.ANIM_CSS)
                .replace("<!--CONTROLS-->", anim_gallery.ANIM_CONTROLS)
                .replace("<!--ANIMJS-->", anim_gallery.ANIM_JS)
                .replace("<!--ANIM-->", anim_body)
                .replace("{anim_note}", anim_note))
    with open(OUT, "w", encoding="utf-8") as fh:
        fh.write(page)
    print("wrote %s with %d frames and %d animated clip sheets (%d frames)"
          % (os.path.relpath(OUT, ROOT), len(cards), clip_sheets, clips))
    present = [s for s in SHOTS if os.path.exists(os.path.join(ROOT, s[0].replace("/", os.sep)))]
    write_contact_sheet(present)
    for path in missing:
        print("   missing:", path)


if __name__ == "__main__":
    main()
