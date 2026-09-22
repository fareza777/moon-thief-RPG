"""Read the layout lines a build printed and report every defect that a compiler cannot see.

usage: python tools/layout_audit.py [log ...]      (default: the newest build*.log / shots*.log)

The game writes four families of lines in frame pixels (288x512, x right, y down):

  [RECT]  frame|name|order|x0|y0|x1|y1|text      a text block's measured box
  [LABEL] frame|name|scale|px|py|maxw|align|order|text
  [PANEL] frame|kind|name|order|x0|y0|x1|y1|alpha   kind is "card" (framed panel art) or "plate"
  [ACTOR] frame|name|order|x0|y0|x1|y1           a character sprite

Rules, in order of how badly they read on a phone:

  OFF      text runs off the frame
  EDGE     text touches the outer 2 px -- one more character and it would be off
  CLASH    two text blocks intersect, whatever layer each is on
  CLIP     a plate drawn after the text cuts through it (text sheared off mid-word)
  PLATE    two cards overlap without one nesting inside the other (toast over dialog)
  ONACTOR  a name or hint lands on a character sprite
  PILE     two scenery sprites of the same family cover each other (two crowns on one tile)
  LOOT     anything at all sits on a chest (a boulder growing out of the lid)

A plate is any bar, chip, icon or framed card; the C# pass marks a sprite as a card when it is
sliced and as a plate when it belongs to the menu, battle or HUD layer. The battle screen used
to be measured as "0 plates" because its panels sit at sorting 46..60 -- below the world-cast
threshold that used to decide what a plate is. Reports on that screen were worthless until that
was fixed, so treat a clean battle frame from before then as unmeasured.
"""
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
W, H = 288, 512
RECT = re.compile(r"\[RECT\] ([^|]+)\|([^|]+)\|(-?\d+)\|(-?\d+)\|(-?\d+)\|(-?\d+)\|(-?\d+)\|(.+)$")
PANEL = re.compile(r"\[PANEL\] ([^|]+)\|([^|]+)\|([^|]+)\|(-?\d+)\|(-?\d+)\|(-?\d+)\|(-?\d+)\|(-?\d+)\|([\d.]+)$")
ACTOR = re.compile(r"\[ACTOR\] ([^|]+)\|([^|]+)\|(-?\d+)\|(-?\d+)\|(-?\d+)\|(-?\d+)\|(-?\d+)$")
PROP = re.compile(r"\[PROP\] ([^|]+)\|([^|]+)\|(-?\d+)\|(-?\d+)\|(-?\d+)\|(-?\d+)\|(-?\d+)$")

# How much of a smaller scenery box may sit under a neighbour on the SAME row before it reads as
# a pile. Two crowns on cells two apart overlap by a third of their width and sort one behind the
# other, which is depth; the same two on one row have no depth to explain the overlap.
PILE_RATIO = 0.3
# A chest is the one prop a player taps, so nothing may sit on it at all.
LOOT_RATIO = 0.05

# Art families: the object is named after its art ("tree_07#12", "rock_03#5", "Bed#2"), so the
# family is that name with the numbers taken out. Scenery of different families (a rock beside a
# tree) may touch; two of the same family is what the eye reads as stacking.
def family(name):
    leaf = name.split("/")[-1].split("#")[0]
    return re.sub(r"_+", "_", re.sub(r"\d+", "", leaf)).strip("_")

# labels that are supposed to float over a fighter or a chest: not a layout defect
FLOATERS = ("dmg", "Float", "pop", "spark")

# every glyph of every label is its own sprite, so a sprite walk sees thousands of them. They are
# the text, not the cast: a name plate landing on a letter of another label is already a CLASH.
GLYPH = "/glyphs/"


def newest_log():
    cands = []
    for pat in ("build*.log", "shots*.log", "win*.log"):
        cands += glob.glob(os.path.join(ROOT, pat))
    cands = [c for c in cands if os.path.getsize(c) > 20000]
    if not cands:
        return None
    return max(cands, key=os.path.getmtime)


def norm(x0, y0, x1, y1):
    """RECT/LABEL y0 is the box bottom, y1 the top; PANEL/ACTOR y0 is the top."""
    return min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)


def overlaps(a, b):
    return a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]


def contains(outer, inner):
    return outer[0] <= inner[0] and outer[1] <= inner[1] and outer[2] >= inner[2] and outer[3] >= inner[3]


def area(r):
    return max(0, r[2] - r[0]) * max(0, r[3] - r[1])


def read(log):
    frames = {}
    with open(log, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            m = RECT.search(line)
            if m:
                f, name, order, x0, y0, x1, y1, text = m.groups()
                frames.setdefault(f, {"text": [], "panels": [], "actors": [], "props": []})
                frames[f]["text"].append({
                    "name": name, "order": int(order), "box": norm(int(x0), int(y0), int(x1), int(y1)),
                    "text": text,
                })
                continue
            m = PANEL.search(line)
            if m:
                f, kind, name, order, x0, y0, x1, y1, alpha = m.groups()
                frames.setdefault(f, {"text": [], "panels": [], "actors": [], "props": []})
                frames[f]["panels"].append({
                    "kind": kind, "name": name, "order": int(order),
                    "box": norm(int(x0), int(y0), int(x1), int(y1)), "alpha": float(alpha),
                })
                continue
            m = ACTOR.search(line)
            if m:
                f, name, order, x0, y0, x1, y1 = m.groups()
                frames.setdefault(f, {"text": [], "panels": [], "actors": [], "props": []})
                frames[f]["actors"].append({
                    "name": name, "order": int(order), "box": norm(int(x0), int(y0), int(x1), int(y1)),
                })
                continue
            m = PROP.search(line)
            if m:
                f, name, order, x0, y0, x1, y1 = m.groups()
                frames.setdefault(f, {"text": [], "panels": [], "actors": [], "props": []})
                frames[f]["props"].append({
                    "name": name, "order": int(order), "box": norm(int(x0), int(y0), int(x1), int(y1)),
                })
    return frames


def audit_frame(f, rows):
    texts, panels, actors = rows["text"], rows["panels"], rows["actors"]
    props = rows.get("props", [])
    out = []
    for t in texts:
        b = t["box"]
        if b[0] < 0 or b[1] < 0 or b[2] > W or b[3] > H:
            out.append(("OFF", t["name"], "%d,%d-%d,%d %s" % (b[0], b[1], b[2], b[3], t["text"][:34])))
        elif b[0] <= 2 or b[1] <= 2 or b[2] >= W - 2 or b[3] >= H - 2:
            out.append(("EDGE", t["name"], "%d,%d-%d,%d %s" % (b[0], b[1], b[2], b[3], t["text"][:34])))

    for i in range(len(texts)):
        for j in range(i + 1, len(texts)):
            if overlaps(texts[i]["box"], texts[j]["box"]):
                out.append(("CLASH", texts[i]["name"], "<> " + texts[j]["name"] + " [" + texts[i]["text"][:20] + "]"))

    # a plate drawn AFTER the text cuts through it: the text is half-covered by a box that was
    # not there when the string was measured -- this is what "textunya terpotong" looks like
    for t in texts:
        for p in panels:
            if p["order"] <= t["order"] or p["alpha"] < 0.5:
                continue
            if overlaps(p["box"], t["box"]) and not contains(t["box"], p["box"]):
                out.append(("CLIP", t["name"], "cut by " + p["name"] + " [" + t["text"][:24] + "]"))
                break

    # two framed cards that overlap without nesting: the notification over the narration box.
    # A plate the size of the whole frame is a dim, and a card may legally sit on one.
    cards = [p for p in panels if p["kind"] == "card" and 1500 < area(p["box"]) < 0.95 * W * H]
    for i in range(len(cards)):
        for j in range(i + 1, len(cards)):
            a, b = cards[i], cards[j]
            if not overlaps(a["box"], b["box"]):
                continue
            if contains(a["box"], b["box"]) or contains(b["box"], a["box"]):
                continue
            out.append(("PLATE", a["name"], "<> " + b["name"]))

    for t in texts:
        if any(k.lower() in t["name"].lower() for k in FLOATERS):
            continue
        for a in actors:
            if GLYPH in a["name"]:
                continue
            if not overlaps(a["box"], t["box"]):
                continue
            # A label that sits on an opaque plate drawn over the sprite is read against the
            # plate: the part of the character it covers is already hidden, so it cannot be a
            # collision. This is what lets a party name tag ride its own name chip in battle.
            if behind_plate(t, panels, a):
                continue
            out.append(("ONACTOR", t["name"], "on " + a["name"] + " [" + t["text"][:24] + "]"))
            break

    # Scenery: two props of the same family overlapping is a stand drawn as a pile. Everything in
    # the world sorts by the row it stands on, so the order alone can no longer tell a tree from a
    # villager -- these boxes come from the [PROP] dump the C# pass writes next to [ACTOR].
    for i in range(len(props)):
        for j in range(i + 1, len(props)):
            a, b = props[i], props[j]
            if not overlaps(a["box"], b["box"]):
                continue
            small = min(area(a["box"]), area(b["box"]))
            if small <= 0:
                continue
            ov = (min(a["box"][2], b["box"][2]) - max(a["box"][0], b["box"][0])) * \
                 (min(a["box"][3], b["box"][3]) - max(a["box"][1], b["box"][1]))
            chest, other = (a, b) if "chest" in family(a["name"]) else (b, a)
            if "chest" in family(a["name"]) or "chest" in family(b["name"]):
                # Only a prop drawn OVER the chest counts: everything in the world sorts by its
                # own row, so a barrel one row behind the chest is a barrel behind a chest, which
                # is depth. A crown on the row below covers the lid, which is the defect.
                if other["order"] > chest["order"] and ov / small > LOOT_RATIO:
                    out.append(("LOOT", chest["name"], "<covered by> " + other["name"]))
                continue
            if family(a["name"]) != family(b["name"]):
                continue
            # One sprite hidden inside another's box is a wasted prop whatever the depth is: the
            # slab drawn over the pebble means the pebble was never drawn. Two sprites that overlap
            # at the SAME row are a pile - there is no depth between them to explain the overlap.
            ratio = ov / small
            if ratio > 0.9 or (a["order"] == b["order"] and ratio > PILE_RATIO):
                out.append(("PILE", a["name"], "<> " + b["name"] + " (%.0f%%, rows %d/%d)"
                            % (100 * ratio, a["order"], b["order"])))
    return out


def behind_plate(t, panels, actor):
    """True when an opaque plate is drawn between this actor and the label, so the sprite the
    label covers is already hidden. A plate drawn *over* the label is the CLIP rule's job.

    The old version compared against a fixed "world top" sorting order, which stopped being true
    the moment the battle screen drew its panels at 46..60: it reads the actor's own order now."""
    for p in panels:
        if p["alpha"] < 0.8 or p["order"] <= actor["order"] or p["order"] > t["order"]:
            continue
        if contains(p["box"], t["box"]):
            return True
    return False


def main():
    logs = sys.argv[1:] or [newest_log()]
    for log in logs:
        if not log:
            print("no log found with layout lines (run ScreenshotsOnly / the self-test first)")
            return
        frames = read(log)
        print("== %s (%d frames measured)" % (os.path.basename(log), len(frames)))
        total = 0
        for f in sorted(frames):
            rows = frames[f]
            bad = audit_frame(f, rows)
            head = "   %-22s %2d text %2d plates %2d actors %3d props" % (
                f, len(rows["text"]), len(rows["panels"]), len(rows["actors"]), len(rows.get("props", [])))
            if not bad:
                print(head + "  clean")
                continue
            kinds = {}
            for kind, _, _ in bad:
                kinds[kind] = kinds.get(kind, 0) + 1
            print(head + "  " + " ".join("%s=%d" % (k, n) for k, n in sorted(kinds.items())))
            total += len(bad)
            for kind, name, detail in bad:
                print("      %-8s %-34s %s" % (kind, name, detail))
        print("   total problems: %d" % total)


if __name__ == "__main__":
    main()
