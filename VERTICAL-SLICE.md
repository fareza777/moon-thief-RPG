# The Moon Thief — vertical slice

Playable portrait JRPG slice built from the *Super Retro Collection* art pack.
This is story **S1** from `REKOMENDASI-GAME-PORTRAIT.md`: *the moon has been stolen, go get it back.*

## Play it

| | |
|---|---|
| **Android APK** | `MoonThief/Build/Android/TheMoonThief.apk` (22 MB, ARM64, minSdk 25, portrait) |
| **Windows preview** | `MoonThief/Build/Windows/TheMoonThief.exe` |
| **Screenshots** | `MoonThief/Screenshots/` (7 frames from the build camera) + `Screenshots/Runtime/` (7 frames played by the shipped player) |
| **Preview page** | `preview/slice.html` (opened in the Freebuff Preview tab) |

Install on a phone: copy the APK across and open it (enable "install unknown apps" if asked).
Package `com.fajargames.moontheif`, version 0.1.0. No internet permission, nothing to download.

## What the slice contains

- Title screen → one full battle → wave 2 boss → result card → chapter end → back to title.
- Turn based battle on a 288x512 render target (18x32 tiles at 16 px per unit), HUD anchored to the
  top and a thumb command menu to the bottom, so the arena keeps its shape on any portrait aspect.
- Four commands: **ATTACK** (tap a beast), **BOW** (30% crit), **BEFRIEND** (a weaker beast joins
  instead of dying), **MORSEL** (heals the hurtest friend).
- Real art from the pack: hero sheets (walk/run/attack/bow/throw/shield/hit/death), 3 map beasts,
  boss battler, dungeon and forest backdrops, 9-sliced panels from the pack's own UI atlas.
- All text is a 5x7 bitmap font built from `Assets/Resources/Text/font5x7.txt`; all strings live in
  `Assets/Resources/Text/strings.txt`, English as the default language. Nothing is baked into art.
- No prefabs and no scene wiring: the stage, HUD, menu and cards are constructed in code, so the
  whole slice is reviewable as C#.

## How it was verified

Every check below is a script in `tools/` and can be re-run:

```
python tools/check_text_grid.py MoonThief/Screenshots/*.png MoonThief/Screenshots/Runtime/*.png
python tools/check_overlay.py  MoonThief/Screenshots/05-chapter-end.png MoonThief/Screenshots/06-wave-clear.png MoonThief/Screenshots/07-defeat.png
python tools/make_preview.py
```

- **Runtime, not a mock-up.** `TheMoonThief.exe -selftest` plays the whole slice by itself and writes
  PNGs out of the live render target. Result: wave 1 → result card → wave 2 boss → chapter end →
  title, 368 orders, 0 exceptions.
- **Text on the pixel grid.** Every line of text must be exactly 14 px (2x) or 21 px (3x) tall. A
  13 px band means a label landed on a half pixel and one glyph row got squeezed. All 15 frames pass.
- **Result cards.** The checker finds the modal card, proves no text is drawn outside it, proves no
  two text bands overlap, and reads every line back through the font table:
  `CHAPTER END / WAVE 2 CLEAR / XP 165  GOLD 103 / BEFRIENDED 2/2 / THEY WANTED TO COME. /
  THE MOON IS STILL GONE. / [TRY AGAIN] [BACK TO TITLE]`.
- **APK contents.** `aapt dump badging`: portrait, ARM64, label *The Moon Thief*, activity
  `UnityPlayerGameActivity`; `strings.txt` and `font5x7.txt` confirmed inside the packed data.

Bugs the checks caught, all fixed: card text stacking on top of each other (fixed positions → a
measured layout), a heading wider than its own card (auto-fit to 3x, drop to 2x if it needs three
lines), labels landing off the pixel grid, and duplicated copy on the chapter end card.

## Rebuild

```
Unity.exe -batchmode -quit -projectPath <this folder>\MoonThief \
  -executeMethod MoonThief.EditorTools.VerticalSliceBuilder.BuildBatch
```
Or from the editor: `MoonThief > Build Vertical Slice`. `ScreenshotsOnly` renders the frames without
building players.

## Not in the slice yet

Sound (the art pack ships none), a world map, save/load, more than one chapter, and the befriending
roster screen. The foundation that matters — portrait tilemap battle, bitmap text, measured UI,
build pipeline — is done, and the same pieces carry over to the farm and dungeon concepts in the
main report.
