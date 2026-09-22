"""Find every string key the code asks for and report the ones strings.txt does not define.

usage: python tools/strings_audit.py
"""
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STRINGS = os.path.join(ROOT, "MoonThief", "Assets", "Resources", "Text", "strings.txt")
SCRIPTS = os.path.join(ROOT, "MoonThief", "Assets", "Scripts")
EDITOR = os.path.join(ROOT, "MoonThief", "Assets", "Editor")

have = set()
with open(STRINGS, encoding="utf-8") as f:
    for line in f:
        line = line.strip()
        if not line or line.startswith("#") or "|" not in line:
            continue
        have.add(line.split("|", 1)[0])

# Keys show up three ways: Strings.Get("k"), table fields like TitleKey="q.mq1.title", and
# bare dotted literals such as "q.goal" in a dialog array. The third pattern is the loose one,
# which is why it is deliberately narrow: lowercase dotted words only.
DIRECT = re.compile(r'Strings\.Get\(\s*"([A-Za-z0-9_.]+)"')
FIELD = re.compile(r'(?:Key|TextKey|TitleKey|StepKey|OfferKey|DoneKey|Giver|NameKey|Gift|Fight|Name)\s*=\s*"([a-z][A-Za-z0-9_.]*)"')
BARE = re.compile(r'"([a-z][a-z0-9_]*(?:\.[a-z0-9_]+)+)"')
# prefixed families built by concatenation, e.g. "dl.home." + h + ".1"
PREFIX = re.compile(r'"([a-z][a-z0-9_]*(?:\.[a-z0-9_]+)*\.)"\s*\+')

want = {}
prefixes = set()
for d in (SCRIPTS, EDITOR):
    for name in sorted(os.listdir(d)):
        if not name.endswith(".cs"):
            continue
        path = os.path.join(d, name)
        with open(path, encoding="utf-8") as f:
            src = f.read()
        for m in DIRECT.finditer(src):
            want.setdefault(m.group(1), set()).add(name)
        for m in FIELD.finditer(src):
            want.setdefault(m.group(1), set()).add(name)
        for m in BARE.finditer(src):
            want.setdefault(m.group(1), set()).add(name)
        for m in PREFIX.finditer(src):
            prefixes.add(m.group(1))

# every key whose family is built by concatenation still has to be expanded by hand
FAMILIES = {
    "dl.home.": ["dl.home.%d.%d" % (h, i) for h in range(6) for i in (1, 2)],
    "npc.house.": ["npc.house.%d" % h for h in range(6)],
    "house.": ["house.%d" % h for h in range(6)],
    "zone.arrive.": ["zone.arrive.%d" % c for c in (1, 2, 3)],
    "ci.": ["ci.%d" % i for i in range(1, 10)],
    "ci.ch.": ["ci.ch.%d" % c for c in (1, 2, 3)],
}
for family, keys in FAMILIES.items():
    for k in keys:
        want.setdefault(k, set()).add("family " + family)

missing = sorted(k for k in want if k not in have)
print("strings defined : %d" % len(have))
print("keys referenced : %d" % len(want))
print("missing         : %d" % len(missing))
for k in missing:
    print("  %-22s %s" % (k, ",".join(sorted(want[k]))))

print("concatenated families seen by the scanner: %s" % ", ".join(sorted(prefixes)))
unused = sorted(k for k in have if k not in want)
print("defined but unreferenced (dialog lines reached by name patterns count as fine): %d" % len(unused))
for k in unused:
    print("  %s" % k)
