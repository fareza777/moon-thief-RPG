"""Check every string against the box it has to live in.

Each element of the game gives its text a width (in world units) and a scale; PixelLabel wraps
at that width into as many lines as it needs. This tool mirrors that wrap exactly and reports
strings that take more lines than the design allows, plus the widest line in pixels.

usage: python tools/text_fit.py [--all]
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STRINGS = os.path.join(ROOT, "MoonThief", "Assets", "Resources", "Text", "strings.txt")

# font metrics, straight out of PixelFont: 6 px cell, 5 px glyph, 8 px tall, 16 px per unit
CELL, GLYPH, LINE = 6, 5, 9

# element -> (width in units, scale, max lines, exact key prefixes). Keys are matched by
# prefix, so "q.axe.done" belongs to exactly one element and the list stays honest.
ELEMENTS = [
    ("hud.goal",   11.4, 1, 2, ("quest.1", "quest.2", "quest.3", "quest.4", "quest.5")),
    ("hud.zone",   11.4, 1, 1, ("hud.explore",)),
    ("toast",      14.6, 1, 3, ("ev.", "onb.", "loot.found", "jr.newquest", "jr.questdone", "zone.shard")),
    ("banner.sub", 13.0, 1, 1, ("zone.arrive.1", "zone.arrive.2", "zone.arrive.3")),
    ("dialog",     14.4, 2, 5, ("dl.", "q.goal", "q.reward", "bt.")),
    ("quest.line", 14.4, 2, 5, ("q.axe.", "q.mush.", "q.doll.", "q.chicken.", "q.toll.",
                                 "q.kettle.", "q.rhyme.", "q.watch.", "q.grave.", "q.ledger.",
                                 "q.mq1.", "q.mq2.", "q.mq3.")),
    ("menu.row",   13.2, 2, 1, ("menu.new", "menu.continue", "menu.settings", "menu.credits",
                                "set.textspeed", "set.sound", "set.shake", "pause.resume",
                                "pause.journal", "pause.save", "pause.settings", "pause.totitle",
                                "btn.continue", "btn.retry", "btn.title", "menu.back")),
    ("menu.hint",  13.2, 2, 1, ("menu.hint",)),
    ("credits.body", 15.6, 1, 6, ("cred.body",)),
    ("credits.line", 15.6, 1, 1, ("cred.title", "cred.sub", "cred.thanks")),
    ("card.title", 14.0, 2, 1, ("card.wintitle", "card.bosstitle", "card.losstitle")),
    ("card.body",  13.2, 2, 4, ("card.bossline", "card.lossline", "card.joined", "card.moon", "card.lose")),
    ("card.stat",  12.0, 2, 1, ("card.xp", "card.gold", "card.befriended")),
    # a trailing "=" means the key must match exactly: "jr.equip" is a title, "jr.equip.sub" is
    # a sub line, and one prefix would have swallowed both
    ("page.title", 15.0, 2, 1, ("jr.title=", "jr.character=", "jr.items=", "jr.equip=", "jr.bestiary=", "jr.quests=")),
    ("page.sub",   15.0, 1, 1, ("jr.sub", "jr.items.sub", "jr.bestiary.sub", "jr.quests.sub",
                                "jr.equip.sub", "jr.page")),
    ("page.row",   11.6, 2, 1, ("jr.level", "jr.xp", "jr.shards", "jr.befriended", "jr.felled",
                                "jr.chests", "jr.gold", "jr.atk", "jr.maxhp", "jr.slot.", "jr.main",
                                "jr.empty")),
    ("page.val",    7.0, 2, 1, ("jr.worn", "jr.none", "jr.done", "jr.ready", "jr.active")),
    ("quest.title", 9.0, 2, 1, ("q.axe.title", "q.mush.title", "q.doll.title", "q.chicken.title",
                                "q.toll.title", "q.kettle.title", "q.rhyme.title", "q.watch.title",
                                "q.grave.title", "q.ledger.title", "q.mq1.title", "q.mq2.title", "q.mq3.title")),
    ("item.name",  12.0, 2, 1, ("item.",)),
    ("npc.name",    6.0, 1, 1, ("npc.",)),
    ("room.name",  14.0, 2, 1, ("house.",)),
    ("cinema",     15.5, 2, 4, ("ci.",)),
    ("ending",     15.5, 2, 14, ("end.",)),
]


def load_strings():
    out = {}
    with open(STRINGS, encoding="utf-8") as fh:
        for line in fh:
            line = line.rstrip("\n")
            if not line or line.startswith("#") or "|" not in line:
                continue
            k, v = line.split("|", 1)
            out[k] = v.replace("\\n", "\n")
    return out


def wrap(text, units, scale):
    advance = (CELL / 16.0) * scale
    glyph = (GLYPH / 16.0) * scale
    max_chars = max(1, int((units + (advance - glyph)) / advance))
    lines = []
    for raw in text.split("\n"):
        line, count = "", 0
        for word in raw.split(" "):
            extra = len(word) if count == 0 else len(word) + 1
            if count > 0 and count + extra > max_chars:
                lines.append(line)
                line, count = "", 0
            if count > 0:
                line += " "
                count += 1
            line += word
            count += len(word)
        lines.append(line)
    return lines, max_chars


def width_px(line, scale):
    advance = (CELL / 16.0) * scale
    glyph = (GLYPH / 16.0) * scale
    if not line:
        return 0
    return int(round((len(line) * advance - (advance - glyph)) * 16))


def main():
    strings = load_strings()
    show_all = "--all" in sys.argv
    seen = set()
    problems = 0
    for name, units, scale, max_lines, prefixes in ELEMENTS:
        for key, text in sorted(strings.items()):
            if not any(key == p[:-1] if p.endswith("=") else key.startswith(p) for p in prefixes):
                continue
            if (name, key) in seen:
                continue
            seen.add((name, key))
            if name == "banner.sub":
                # the banner is two labels: the night at scale 2, the place at scale 1
                text = text.split("\n")[1] if "\n" in text else text
            lines, max_chars = wrap(text, units, scale)
            widest = max(width_px(l, scale) for l in lines)
            bad_lines = len(lines) > max_lines
            bad_width = widest > int(units * 16)
            if bad_lines or bad_width:
                problems += 1
                print("%-11s %-20s %d lines (max %d), widest %d px of %d" %
                      (name, key, len(lines), max_lines, widest, int(units * 16)))
                for l in lines:
                    print("              | %s" % l)
            elif show_all:
                print("%-11s %-20s %d/%d lines, widest %d px" % (name, key, len(lines), max_lines, widest))
    print("\n%d strings over budget" % problems)


if __name__ == "__main__":
    main()
