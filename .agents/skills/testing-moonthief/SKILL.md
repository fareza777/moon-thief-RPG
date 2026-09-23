---
name: testing-moonthief
description: How to run and end-to-end test The Moon Thief Unity Linux player on a GUI desktop (launch flags, input model quirks, map coordinates, save/log locations, known spec gaps)
---

# Testing The Moon Thief (Unity Linux player)

## Devin Secrets Needed
None — the game has no logins or network dependencies. Audio will NOT work on VMs without an audio device (FMOD falls back to emulated software output; log lines like `FMOD failed to initialize the output device` are environmental, not game bugs).

## Launch
- Binary: `MoonThief/Build/Linux/TheMoonThief.x86_64` (built via the repo's Unity batch build; see environment blueprint for editor path).
- Run windowed portrait on display :0:
  `./TheMoonThief.x86_64 -screen-width 432 -screen-height 768 &`
- Do NOT pass `-selftest` (scripted harness) or `-screenshotdir`.
- The window is a small 432x768 portrait view; it can appear behind other windows — raise it with `wmctrl -a "The Moon Thief"`.

## PlayerPrefs / save / log
- Config dir: `~/.config/unity3d/F7 Developer Games/The Moon Thief/`
  - `prefs` — PlayerPrefs (first-run flag `mt.onb`; delete whole dir for onboarding).
  - `moonthief-save.json` — save file (contains `heroX`/`heroY`, `gold`, `night`; grep to confirm position when screen is ambiguous).
  - `Player.log` — Unity log; at end of run `grep -n "Exception\|NullReference\|InvalidOperation" Player.log` (ALSA/FMOD errors expected on headless VMs).

## Input model quirks (verified on Linux build)
- **NPC talk / chest / dialogue-advance is CLICK-ONLY.** `TryInteract()` fires only on a left-click near the hero (within ~1.4 tiles via `World.NearestNpc`). Walking adjacent + Z/Enter/Space does NOTHING in Explore or to advance dialogue — keyboard confirm works only inside menus and the battle command grid. If clicking does nothing, the hero is >1.4 tiles away — walk it right next to the sprite first.
- WASD/arrows move in Explore (`Input.GetAxisRaw`); arrows+Enter/Space/Z navigate menus and the battle 2x2 command grid; Esc opens PAUSED; Tab cycles battle targets.
- Mouse joystick needs RMB-drag or Ctrl+LMB-drag — a plain LMB-drag does NOT move the hero.
- Click/tap also acts as the "advance" input for cinema pages, dialogue lines, and result cards.
- The Devin automation Chrome window sits on the right half of the screen and can overlap the game; it also STEALS keyboard focus if you click outside the game window or it raises itself — keyboard movement then silently dies and looks like a wedge. Refocus with `wmctrl -a "The Moon Thief"`; do NOT pkill it (it's the automation browser). Before calling any movement problem a bug, confirm the game window is focused.

## Identifying the hero (sprites are tiny + camera follows it)
- The hero is a small figure among similar-looking wandering villagers/critters. To find it: the camera centers on the hero, so after any move the figure nearest screen-center is the hero. NPCs and critters wander on their own — do NOT mistake their idle drift for your input working.
- For ground-truth position, don't trust the pixels: Esc → SAVE PROGRESS → read `heroX`/`heroY` from `moonthief-save.json` (save writes live `World.HeroPos`). Bit-identical heroX/heroY across inputs = hero did NOT move.

## Blocked hero drops — FIXED (snap behavior to verify)
- `PlaceHero` used to drop the hero inside blockers (house door-return on a boulder = permanent wedge, only NEW GAME escaped). Now it checks `CanStand` and snaps to the nearest standable cell (`NearestStandable`, ring search half-tile steps, 3-tile radius).
- Verify: land a door-return on a blocked cell (boundary-edge entry, e.g. house(15,9) → boulder cell(16,6)) → hero must land BESIDE the blocker at a half-step offset (~0.3-0.5 from the blocked cell) and move freely. A snap landing on a half-step neighbor is the signature vs a raw landing.
- The door auto-enter overshoots ~0.25 tiles, so boundary-edge reproductions are finicky — expect several enter/exit cycles before a return lands on the blocker.
- Hero can still wedge (non-fatally) in concave terrain pockets — one direction stays open; try all four.

## Map layout (GameDefs.cs, 60x86 tiles)
- Village ~y8 (safe zone), fields y15–59 (slimes, night chests), forest y59–83 (shades), BossPos (30,81), crystal (11.5,7.5).
- NPCs: MIRA/elder quest giver near crystal; MARN shopkeeper ~(24.5,9.5) opens shop card (not dialogue); TOBBE kid, HARL smith, FINN, PIP, PRUNE.
- Houses: walk onto a door tile → interior; interior chest = +20G; step on mat to exit.
- Dead enemies respawn only after 30–75s AND when hero is >7 cells away — walking back over a fresh kill spot must NOT spawn instantly.
- Battle: party AMBER/SEA/MOSS; 2x2 grid ATTACK/BEFRIEND/MORSEL/LET GO; AUTO chip top-right of HUD toggles auto-battle and persists between fights.
- Beating the Pale Guard boss drops the last shard and transitions to "NIGHT TWO" chapter (respawns in village).
- The hero's lantern glow is intentionally subtle — `hglow` is a warm amber SpriteRenderer at only 0.30 alpha (`WorldView.CreateHero`). Don't expect a dramatic light pool; look for a faint halo around the hero, most readable on dark field grass away from torches.

## Known spec gaps (as of the tested build — verify before re-flagging)
- Settings rows: TEXT SPEED / MUSIC / SOUND / SCREEN SHAKE / BACK. MUSIC and SOUND are 4-step volume rows (100%/75%/50%/25%/OFF); there is no EN/ID language toggle (strings.txt is English-only).
- SHARE → clipboard toast (no crash); RATE US → opens a browser to a Play Store URL that 404s (app unpublished) — the browser steals focus; return with `wmctrl -a "The Moon Thief"`. Both are expected stubs.
