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
- **Talk / chest / door / interact** = `TryInteract()` → `World.NearestNpc(HeroPos)` within **1.4 tiles**. Triggered by a **short left-tap** (`_tapPending`, press+release without drag) OR by **Enter/Space/Z** (`KeyConfirm`). Confirmed on build a5c2327: pressing Enter next to an NPC opens its dialogue and the MARN shop. (An earlier note claimed click-only — that was wrong; the hero was likely out of range or focus-stolen.) The NPC is chosen by proximity to the HERO, not the click location.
- **NPCs wander**, so the in-range window is a moving target — a "!" indicator appears over the nearest in-range NPC; interact then. If talk does nothing, the NPC drifted >1.4 tiles — press Enter again as it wanders back.
- WASD/arrows move in Explore (`Input.GetAxisRaw`: `w`=+y north/up-screen, `s`=−y south, `a`=−x west, `d`=+x east; +y = north toward boss). Arrows+Enter/Space/Z navigate menus and the battle 2x2 command grid; Esc opens PAUSED; Tab cycles battle targets.
- Mouse joystick needs RMB-drag or Ctrl+LMB-drag — a plain LMB-drag does NOT move the hero. A plain LMB *click* is a tap = TryInteract.
- Click/tap also acts as the "advance" input for cinema pages, dialogue lines, and result cards.
- The Devin automation Chrome window sits on the right half of the screen and can overlap the game; it also STEALS keyboard focus if you click outside the game window or it raises itself — keyboard movement then silently dies and looks like a wedge. Refocus with `wmctrl -a "The Moon Thief"`; do NOT pkill it (it's the automation browser). Before calling any movement problem a bug, confirm the game window is focused.
- **Two game processes can coexist** (a leftover from a previous session isn't auto-killed). Their windows overlap at the same position, so `wmctrl -a` may raise a STALE binary and show old behavior. Check `wmctrl -lp` (gives PID per window) + `ps aux|grep TheMoonThief` for start times; `kill` the old PID, then `wmctrl -i -a 0x<newWindowId>`.

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
- **The lantern glow now scales with moon shards** (bb8365f, `SetMoonFill`): `localScale` (4.4,3.2)→(6.4,4.6), `_glowAmp` 0.22→0.40, color warms — so shards=4 gives ~45% wider + brighter pool vs shards=0. To verify objectively: save-edit `"shards":0` vs `4`, put the hero on uniform dark grass, screenshot, and count warm pixels (r>g>b, r>90) in a crop around the hero — shards=4 ≈ 3x the warm-pixel count of shards=0. The glow flickers (~1.9Hz) so single shots vary; the SIZE (localScale) is the reliable signal.
- **Zone-followed music** (1acf8cf/3fccf99): `Sfx.Mus.Play` runs every frame — "village" inside the wall (heroY<=26 or in a house), "explore" fields (26<y<=58), "wood" the dark wood (y>58). No audio device on this VM → can't hear it; verify instead that crossing boundaries throws no exceptions and grep Player.log for Mus/Compose/Play errors (expect none — Mus.Play no-ops gracefully). `NoteZone` records zone.village/fields/wood to save.zones (silent tracking).
- **First-visit zone banners** (76e21ff): `NoteZone` now returns bool (true only on first visit); the Update loop fires `ShowBanner("zone.name.X\n"+"NIGHT n")` when you FIRST cross into a zone — "THE LONG FIELDS" (y>26), "THE DARK WOOD" (y>58), "QUIET HOLLOW" (village). Once per save, persisted via `save.zones`. `BuildChapterNow` seeds the standing zone silently (so the arrive card isn't repeated). Re-entering a visited zone → no banner. Verify: clear `zones`, spawn in village → cross into fields → "THE LONG FIELDS/NIGHT 1" once; back out & in → none; save+continue → none.
- Entering a house fires the `InteriorNameKey` banner (e.g. "ILSE'S SHRINE"); the chapter-spawn `zone.arrive.N` fires on NEW GAME/CONTINUE/chapter transition.

## Known spec gaps (as of the tested build — verify before re-flagging)
- Settings rows: TEXT SPEED / MUSIC / SOUND / SCREEN SHAKE / BACK. MUSIC and SOUND are 4-step volume rows (100%/75%/50%/25%/OFF); there is no EN/ID language toggle (strings.txt is English-only).
- SHARE → clipboard toast (no crash); RATE US → opens a browser to a Play Store URL that 404s (app unpublished) — the browser steals focus; return with `wmctrl -a "The Moon Thief"`. Both are expected stubs.

## Quest journal + quest givers (as of build a5c2327)
- Journal QUESTS page lists only MAIN quests (mq.1-3: LAST LANTERN / FOUR SHARDS / THE PALE GUARD) + side quests actually accepted — side quests stay hidden until taken. Pause→JOURNAL→QUESTS.
- Quest-giver names (ch1): MIRA/elder (also MAIN), TOBBE/kid→RED DOLL, SABLE/house.3→LENT AXE, JUNO/house.5; npc.house.X = ILSE/BRAM/MARA/SABLE/TAVI/JUNO. Chapter-gated spawns: ch≥2 adds WREN/ODA; ch≥3 adds OSK/GRANNY ASH(MUSHROOMS)/NAIL — so e.g. GRANNY ASH's MUSHROOMS quest can't be tested in night 1.
- Accepting: talk → an OFFER card with a ✓/YES row → tap to accept → quest moves to ACTIVE in the journal + quest counter (JOURNAL x/10) ticks.

## Chest economy + moon shards
- Outdoor field chests give **moon shards** only for the first 3 chests opened (ChestsOpened<3), no gold; interior house chests give +20 gold (no shard); outdoor chest #4+ gives 12 gold + a random item.
- Field chest spots (ch1): cells (12,22) and (46,48). Roads: spine lane x29-31 y13-58, west fork y22→chest(12,22), east fork y48→chest(46,48).
- SHARD counter = `shards/4` (ShardsNeeded=4). Top-right moon icon dims at 0/4 and brightens toward warm as shards collect (`SetMoonFill` alpha lerp 0.30→1.0).

## Efficient state setup via save-editing
- `moonthief-save.json` keys: version, chapter, shards, befriended, gold, xp, morsels, items, chestsOpened, defeats, heroX, heroY, stamp, bag, worn, zones, seen, quests. Edit it to set up a test state (e.g. `"shards":3` for moon-fill, `"gold":999`, `"heroX"/"heroY"` to teleport).
- **LeaveToTitle does NOT save** — safe to edit the file after leaving to title, then CONTINUE reloads it. Resume places via PlaceHero (snaps to standable). Use `python3 -c "import json;d=json.load(open('.../moonthief-save.json'));print(d)"` to inspect.
- Reading hero pos live: Esc→SAVE PROGRESS writes the file with live heroX/heroY.
- **`"chapter":N` save-edit jumps the whole world** — set it, relaunch, CONTINUE loads that chapter's map (ch1 fields/forest, ch3 dark wood) + monster pool + backdrop. Useful to reach ch3 content (DungeonA battle backdrop, ch3 monsters) without playing through. `Battle.SetBackdrop`: ch1→ForestA, ch2→PlainA, ch≥3→DungeonA.

## Capturing brief/probabilistic moments
- The `*-edited.mp4` screen recording is TIME-COMPRESSED (~47x on fast segments) — a 0.85s battle message can appear in <1 frame there. **Use the `*-raw-NNN.mkv` files instead** (same dir, uncompressed ~15fps): extract frames with `ffmpeg -y -i FILE -vf fps=15 -q:v 4 out_%04d.png`. This is how to catch the boss slam line "The PALE GUARD rears up and SLAMS down!" (~35% of boss turns, bt.slam) and the "A wild X blocks the way!" intro — both are too brief to screenshot reliably live.
- Boss intro uses `bt.boss` "The {0} bars your way!" (any spec.Boss in the fight); wild battles use `bt.one`/`bt.two` "A wild {0} blocks the way!". Boss = `MonsterSpec.Boss` (Boss=true, struct copy — safe); BossFight() = {Boss, wisp}. Slam = `e.Boss && Random<0.35` in EnemyTurn → 1.6x dmg + `Fx.Shake(View.Stage)` + redder damage float.
- To force a loss for defeat-card testing: BEFRIEND all 3 party members every round (always fails with a full party = no damage dealt) → boss grinds the party down. MOSS periodically auto-mends which prolongs it.

## Known issues / quirks observed (verify before re-flagging)
- **World HUD missing on CONTINUE — FIXED in 6b19f1f.** Previously CONTINUE rendered the village without the world HUD (NIGHT/SHARD/NEXT + name plates + moon) until a card was opened/closed. Now `LeaveToTitle` + `BuildChapterNow` force `SetTextVisible(true)`, so the HUD shows immediately on continue. Verified fixed on 6b19f1f (cold-launch continue + pause→leave→continue + NEW GAME all render HUD at once).
- **Transition-fired zone banner flash — FIXED in 3a1b81d.** Previously `ShowBanner` fired inside `DoTransition` while the HUD text layer was hidden (no `SetTextVisible(true)` first) → ~1-frame flash. Now all battle-exit paths restore the text layer first: `OnRunLost` (line ~1454 `SetTextVisible(true)` → 1455 `ShowBanner(zone.retreat)`), `OnBossDefeated` (~1420 → `zone.bossdown`/`chapdone`), `SpawnIntoChapter` (~783 → `zone.arrive`). Verified: "You fall back to the village." now holds ~2.2s real (visible ~2s after flee, gone ~3.5s). Note: the edited screen recording is TIME-COMPRESSED (~47x on fast segments) — a 2.2s banner can appear in only ~1-2 video frames there, so measure holds from real-time screenshots, not video frame counts.
- **Zone banner text overflow — FIXED in 3a1b81d.** `ShowBanner` now drops to the small face when the top line exceeds the frame. Verified: "You fall back to the village." (30 chars) and chest-loot "You found BENT SPOON and 12 gold." (33 chars) both render fully inside the 432px width, no clipping. Short text (zone.arrive "NIGHT ONE") still uses the big face.
- **FLEE HOME persistence — FIXED in 3a1b81d.** `OnRunLost` now calls `SaveRun`, so after FLEE HOME the save records the village pos (~30,8) and CONTINUE wakes the hero in the village (not the pre-flee boss approach). Verified end-to-end. `LeaveToTitle` still does NOT save (intended — menu quit without SAVE loses position).
- **Arrive banner getting stuck — FIXED in 76e21ff.** Previously a `World.SetActive(false)` (house/battle/title) during a banner's ~2.8s fade killed the `BannerFade` coroutine → frozen banner on re-show. Now `WorldView.OnEnable` restarts `BannerFade` when `ZoneChip.enabled` → the banner picks its fade back up and clears ~3s. Verified: teleport onto a door (16.5,9.2), auto-enter during arrive banner → exit → banner fades away cleanly, no >15s freeze.
- Movement is slow (~1.3-3.6 tiles/s effective) on VNC/low-fps; reads save pos to confirm motion rather than eyeballing pixels.

## Round-12 polish (0ab5c67) — verified
- **LEAVE TO TITLE now SaveRuns first** — hero resumes at the exact tile walked to (previously dropped at the last autosave). Verify: walk to a distinct spot → pause → LEAVE TO TITLE → save's heroX/Y = that spot → CONTINUE resumes there.
- **CONTINUE row stamps `NIGHT {n}`** (hud.nightshort) not "N2" — from save's `chapter`.
- **Credits (ABOUT)** has a 6th body line `version     1.0.0` (strings `version {0}`). Body fits the card — lines are tightly packed but legible, no clipping.
- **Bestiary tap** → flavor toast: seen beast → `mon.X.d` line (e.g. slime2 "Softer than it looks. Meaner too."), unseen `?????` → "Nobody has met this one yet." (jr.unseen). Toast ~2.6s — capture fast.
- **Hero-down plays a faint sting** (PlayerDowned:707) — audio, untestable on the no-audio VM.
- All four behave; regression clean.

## Round-13 (18c6f81) — party trail + AUTO BATTLE — verified
- **Party trail**: Sea(color_2)+Moss(color_3) follow the hero as `_friends` (WorldView). Crumbs drop every ~0.25 tiles (`_crumbs.Insert(0)`); each friend targets `_crumbs[(i+1)*6]` → ~1.5u & ~3u behind, `MoveTowards` at 5.4/s + `StepBob` hop. >3u off → snaps to crumb (corner/door catch-up). On `PlaceHero` they gather at hero's heels. Each WorldView (incl house interiors) has its OWN friends → they follow into and out of houses, no strays. Friends have `f.Name=null` → no name plates, no collision with NPC plates.
  - Verify: enter a house — the ONLY actors inside are hero + 2 friends (isolation from wandering NPCs). Trail visibly forms a line behind when walking; bends on corners (friends on the path leg, not a beeline). Hard to isolate in the crowded village — use house interiors or the east-village edge (x~44) for clean shots.
- **AUTO BATTLE setting** (1656e52): `Prefs.Auto`=PlayerPrefs `mt.auto`. Settings card now has 6 rows incl `AUTO BATTLE ON/OFF`. `Battle.ToggleAuto` (the in-battle AUTO chip) also writes `Prefs.Auto`+Store → persists. Battle init reads `Auto=Prefs.Auto` → chip reflects it at battle start. Verify: settings→ON→battle auto-plays+chip lit; in-battle chip toggle→mt.auto flips→next battle + relaunch keep it. Check `strings prefs|grep mt.auto` (1/0).

## Round-14 (745d626) — chest dupe fix + persistence — verified
- **Chest loot rules** (WorldView.OpenChest): field chest `shard = !Interior && ChestsOpened<3` → first 3 field chests give a MOON SHARD (SHARD x/4 ticks); opened≥3 → 12 gold + rolled item; interior room chest → always 20 gold, never a shard. Field chests persist (`MarkChest`→`ChestsDone`→save `chests`); interior chests do NOT persist (re-lootable each visit). Field chestSpots: (12,22),(46,48),(9,40),(52,36),(18,66),(44,78); `chests=Clamp(1+chapter,2,4)` → ch1 spawns only (12,22)+(46,48), ch2 adds (9,40), etc. Each house interior has ONE cache at cell (x1-1,y1-1) "the back" (plus decorative chest sprites that are NOT lootable — hard to distinguish visually).
- **DUPE FIX verified**: `OpenChest(int i)` sets `_chests[i].Opened=true` on the ARRAY element (was a struct copy). Open once → lid opens (frame 3), halo dies, wood darkens `Sr.color=(0.76,0.78,0.86)`; re-tap → `NearestChest` skips `Opened` → nothing (no 2nd loot/shard/gold). Verified on ch1 (12,22),(46,48) + ch2 (12,22).
- **PERSISTENCE verified**: opened chests save as `chests:["{chapter}:{x},{y}"]` e.g. "1:12,22"; `ChestsOpened` counter also saved. On CONTINUE, chest spawn checks `State.HasChest(MapChapter,cell)` → renders already-open. Leave→continue→re-visit → lid up/dark/no-glow, tap→nothing. Ch1 chests persisted correctly across a chapter→2 save-edit.
- **HUD top line** reads `NIGHT n  SHARD x/4  GOLD g` — GOLD ticks live on chest loot (+12 → 106→118 seen) and battle reward (+33 → 73→106).
- Verify tip: to get a GOLD field chest without clearing ch1, save-edit `chestsOpened:3` (+`chapter:2` for an unopened chest) → next open gives 12g, watch GOLD tick. Player.log stayed 0 exceptions throughout.

## Round-15/16/17 — speaker facing, 'v' cue, '!' gate, dust, chest burst, boss taunt — verified
- **Speaker facing**: `TalkTo` calls `World.FaceAt(talker,HeroPos)` + `World.FaceHeroAt(talker pos)` before the shop/quest/dialog branch. Hero-side turn persists (frozen by `_dlgOpen`). **NPC-side turn is transient for wanderers** — NPCs keep wandering DURING open dialogs (no dialog gate in the wander loop) so their turned-facing is overwritten ~1 frame later; only a paused NPC (`n.Pause>0`) holds it. Verify the hero's turn, not the NPC's.
- **Dialog 'v' advance cue**: a PixelLabel "v" at the dialog's bottom-right, `enabled=!IsRevealing`, blinks alpha 0.55+0.45*sin(t*6). Shows ONLY after the line finishes typing (not mid-reveal). Reveal ~55 chars/s. Small + blinky — catch a bright phase or screenshot twice.
- **Quest '!' bubble gate** (`Npcs[i].Alert`, questAlert, +2.95y, sorting 2100): `wants=(elder && Step(mq.1)==0) || (ForGiver!=null && (Step==0||ready))`. Quests AUTO-accept on talk (Step==0→1) so the giver's "!" clears — verify: find a giver with "!" (e.g. TOBBE/kid at 33.5,5.5 for sq.doll), talk → accept → "!" gone. MIRA shows "!" only while mq.1 Step==0. Monster aggro "!" is a separate `m.Alert` at +1.05y.
- **Footstep dust**: `SpawnDust(heroPos - input*0.35)` every 0.24s while a direction is held (`_stepSfxT` beat) — `TexArt.Glow()` tinted (0.9,0.88,0.76) @0.38 alpha, scale 0.5, drifts up 0.5, life 0.42s. **Deliberately VERY faint** — masked by the hero's lantern glow; could not isolate a clean puff in any still despite screenshots/imports/x11grab/diffs. Treat as code-verified ambient FX; don't flag as missing.
- **Chest loot burst**: `OpenChest`→`BurstLoot(pos)` = 9× `Tex.Spark()` flecks Color(1,0.9,0.45,0.85) scale 0.34 fanned (i*40°±rand, vy up-biased), life 0.62s, sorted 1955. Fast transient — catch via fast window captures (`import -window`) or raw-mkv frames; **verify objectively by pixel-diff**: closed-frame vs open+burst-frame, count new bright-warm px (peaked ~128 px). Gold star burst visible on the open lid at high zoom.
- **Boss taunt**: `TryInteract`→`if(NearBoss && !_bossDown)` builds `NpcDef{Sheet=BossMapSheet(),NameKey=mon.minotaur="PALE GUARD",Monster=true,Lines=boss.taunt.1/2}`→OpenDialog; `OpenDialog` for `Monster` uses `TexArt.MapMonster(sheet,1)` portrait @1.15 (vs Chara @3). `_dlgThen=StartBattle(BossFight())` on close. `NearBoss`=dist(HeroPos,BossPos(30,81))<1.6, **interact-only** (proximity alone does nothing). Boss prop always renders at BossPos (scale 0.85, Down, red glow +0.6y) — but cells (28..32,81) are Blocked so hero stops ~y80.7; interact fires fine. Taunt: "A hundred nights I stood this watch..." then "Take it if you can. Or kneel, little thief...". Boss fight is HARD (wiped an AUTO LV5-6 party over ~14 rounds).

## Capture / technique additions
- **`xdotool keydown` for held movement**: `DISPLAY=:0 xdotool keydown d` holds a direction → Unity GetAxisRaw reads it → continuous walk during a screenshot; `keyup` releases. Fields are monster-dense → walks trigger battles; use the village plaza or short bursts.
- **Fast in-window captures**: `import -window <winid>` (ImageMagick) grabs the game window ~150-200ms/frame in a shell loop — good for ~0.6s transients (chest burst). Get winid via `xdotool search --name "The Moon Thief"`. `ffmpeg -f x11grab -video_size 576x1024 -i :0.0+0,29 -r 20` records a few seconds of just the window.
- **Pixel-diff for subtle FX**: when a transient is too faint to eyeball, diff a before-frame vs the event frame over the event region and count new bright-warm px (flecks/dust are warm-tinted). Reliable for chest burst (gold) and glow changes.
- **Title/menu pitfalls**: the title cursor defaults to CONTINUE — pressing `Down` moves it to STORY (a LOOPING 9-slide reel that restarts instead of loading the world). Select CONTINUE with plain `Return`. NEW GAME prompts "START OVER?" — pick "KEEP MY SAVE". SHARE/RATE US buttons call OpenURL → brings Chrome to front (steals keyboard focus; `wmctrl -a "The Moon Thief"` to refocus).
- **Environmental crash seen once**: SIGSEGV inside `swrast_dri.so` (Mesa software GL, no GPU) during a malloc — the game process died mid-session. This is a VM/driver issue, NOT a game-logic fault; the managed C# threw no exception. If it recurs, suspect memory pressure from the recorder + Chrome, not the build.
