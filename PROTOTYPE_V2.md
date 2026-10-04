# Prototype v2 — the plan (M10)

> The approved plan for Prototype v2, kept in the repo so any session can pick it up. Approved 2026-10-04.
> Order of authority: ARCHITECTURE.md (structure) > this file (what v2 is and how it's split) > TASKS.md (status).
> Design was done in chat with Yam; this file is the record of it. All numbers are placeholders (definitions / Inspector).

## What v2 is
Authored nights + the barn day phase + progression + a post-run screen + a night scoreboard, in a **new scene,
`TestNight.unity`** (Yam duplicated it from Night.unity; cloud sessions can't create scenes). **Night.unity and the current
build keep working unchanged**: new behaviour only activates when its components/data are present.

The loop: night ends (dawn / caught) → the sweep finishes → the camera descends to the **Doors**, the doors open → the
**post-run** panels → Retry / Next → **Barn room** (day loadout) → optional **Tower** (slice placement) → Start night → the
camera rises to the **Night** frame as the moon rises.

## Decisions (with Yam)
- **Restart = reload.** Retry / Next save the choice and reload the scene. The campaign scene boots at the **Doors** frame
  (the same view as before the reload, so the cut is invisible), eases into the **Barn room**, and the night waits in
  **Dusk** until Start night. Every night still starts clean; no reset code.
- **Stones:** start 10, +1 per stone-mastery threshold, up to a cap. *Superseded by stage 2 (PR P):* evolutions are a
  list — each entry: stones needed, look, refill per hour (10 → lv1 +1 · 15 → lv2 +2 · 20 → lv3 +3 · 25 → lv4 +4, cap 25).
  Hits are the saved cause.
- **New look everywhere:** depth-coloured, points-only popups and the debug-HUD toggle apply to Night.unity too.
- **UI:** Yam lays out each panel's frame once (Canvas, panel images, anchors, one template row/dot) in a local session;
  the components fill and repeat them from data.
- **The UI kit (Art/UI) is the look of every button and panel.** Its 9-slice borders are set by the editor menu
  Piglings ▸ UI ▸ 1 (import settings); selected Images are styled with Piglings ▸ UI ▸ 2 (primary / secondary / panel).
- **Separate profiles:** `NightSession.profileName` → `piglings_<name>.json`: "dev" in Night.unity, "campaign" in TestNight.
- **Save stores causes only:** dawns per night id (unlocks are derived), the current night index (navigation), peg triggers
  per merged level (copies are derived), tower choices per night. Save stays v1 (additive sections).
- **One save rule for both scenes:** the stone's level comes from its stone count (the evolution list) everywhere.
- **The Bouncy number tag goes** with the new popups; a peg tutorial card covers it later.
- **Bombs do nothing during PegPlacement** (a plain hold, charge kept). Chains thrown after a threshold's crossing can
  still be falling then (by design since M8.2c).

## Design spec (per area)

### 1. Authored nights + campaign — done in M10.B
- Campaign asset: ordered nights, each with the peg type a dawn on it unlocks (N1 → Bomb, N2 → Splitter); starting types (Bouncy); all slices.
- Test nights: N1 = 2 slices, 5 hours. N2 = 3 slices, 6 hours. N3 = 4 slices, 7 hours. Hour multiplier +step per hour (hour 7 = ×4).
- `WallSliceDefinition`: id, sprite, 7 holds, hold material, effect slot (None for the red barn; wood/straw/brick later).
- The tower is built from the night's slices: every 1.6 above the bottom piece; the tower top (Barn_Top + pig, perch +
  TopZone + breach slots + stone pile, DangerZone, peg shelf, ChainPopupAnchor) follows the top slice; spawn line and ground
  stay put (the tower grows upward). "Preview tower" in edit mode.
- `[Night]` log per hour: time, score, breaches.

### 2. Camera frames — PR C
Camera moves **only between phases**; input locked while moving; ease in-out. CameraShake keeps working as an offset
(it already re-reads the pose every frame); popups stay inside the Night frame.
- **Night:** computed from the built tower, like the Tower frame (M10.D): a **fixed bottom** (N1's frame bottom, y 2.5 —
  `CameraDirector` Night Bottom Y) up to the tower's top + the roof (Barn_Top + the pig: the same Above Top as the Tower
  frame, 3.3). A taller tower zooms out upward. `NightDefinition` `cameraY` / `cameraSize` stay as an **optional override,
  0 = auto** (each on its own). The formula reproduces the hand-tuned frames: N1 y 6 / 3.5, N2 6.8 / 4.3, N3 7.6 / 5.1.
- **Doors** (post-run): y 2, size 2.
- **Barn room** (day loadout): ~y 1.16, size ~1.15 (inside the open doorway).
- **Tower** (slice placement): the whole tower, wide — computed from the built tower (`TowerBuilder.TopY`).
- Doors: `barn_bottom_closed` → `barn_bottom_open` when the camera reaches the Doors frame. Moon rise: a hook only
  (`Art/UI/sky_moon`, `sky_moon_glow`).

### 3. Progression — rules done in M10.A, data in M10.B
- Peg types unlock by dawn (Bouncy from the start; N1 dawn → Bomb; N2 dawn → Splitter). A new type starts with 1 copy.
- Peg copies: up to 4 per type, earned by that peg's mastery = its effect triggers (Bouncy bonus granted, split,
  explosion) × the merged level's weight (`PegDefinition` levels `masteryWeight`; 0 = the level). Thresholds per type (`copyThresholds`).
- Copies owned = how many of that type can be placed in a night (still limited by pegThrowsPerThreshold per hour).
- Stones: see Decisions.

### 4. Post-run screen — PR E (screen-space UI, in the Doors frame)
- **Left — NIGHT SUMMARY:** "NIGHT n"; DAWN (gold) or CAUGHT; hour dots in palette colours (filled up to the hour reached);
  score (on a loss: "kept: X (hour n)"); best throw (quality-coloured, rainbow if ≥ 100% of its hour's gap); deepest depth
  (depth colour); wolves dropped; stones lost (= stolen by breaches).
- **Right — PROGRESS:** Stones "a → b" with dots from 10 to 14 and the evolved stone icon at 15 ("15 · evolve"), the new dot
  glowing gold, a mastery bar + next threshold. Each unlocked peg: "Bouncy 1 → 2", 4 copy dots (new one glowing), "max 4",
  trigger bar. A newly unlocked peg: NEW tag. The next locked peg: faded + lock + "dawn on night n".
- **Buttons in the doorway:** Retry night (secondary), Next night ▸ (primary, only after a dawn; hidden after the last
  night). On a loss Retry is primary. Later "Next night" becomes "To the barn ▸": a label / flow-target change, not a rewrite.
- Before/after values: snapshot the profile before banking.

### 5. Day phase — the barn room — PR F (Barn room frame)
- Inside the open barn: the pig centre-left (the existing pig rig, hole mask off, Idle).
- **Wall pegboard** (`room_pegboard.png`, pivot Bottom): 3 groups × 4 holes; hole centres in `room_pegboard_holes.txt`
  (px from the PNG's top-left, PPU 400; 12 points in one row, evenly spaced — the groups are separated by cream battens
  in the art between holes 4→5 and 8→9). Holes 1–4 = the first type, 5–8 = the second, 9–12 = the third. Owned copies hang
  on holes; empty holes are the empty slots; a locked group is dimmed with `icon_lock.png` over it.
- **The rug** with exactly the current stone count (real stones, the pile mechanic) + ONE stone set apart with a
  badge sprite (`badge_refill_1` / `badge_refill_2` and the lv2 sprite after evolving). No text on world objects in the barn.
- **Materials pile** (`room_materials.png`, pivot Bottom, `room_materials_glow.png` white glow behind it) in the right
  corner. Interactables idle with a faint outline; hover on the pile: lift, glow, solid outline, sparkles, a small camera
  nudge (~5% pan/zoom), a material sound (hook only). Click → Tower frame.
- **Slice placement:** the hovered slice jiggles; drag a slice out of the tower onto another slot to swap them, or drag one
  from the tray beside the tower onto a slot to replace it; anywhere else it slides back (the tower is always whole). The
  tower rebuilds live (`TowerBuilder.Build` + `PegBoard.Rebuild`, before the night begins only); the choice is saved
  (`Progression.SetTower`). *(Was "click a slot to cycle"; changed after the first playtest of it.)*
- Hover/click input lives in Simulation (`BarnInteraction`); views only read it.
- **Screen UI:** night sign top-left ("NIGHT n · H HOURS"), Start night ☾ top-right.

### 6. Night scoreboard + popups + tutorial cards — PR D (Night frame)
- **Left: a world-space chalkboard on a post** (`board_frame` 9-slice L36 R36 T36 B52, `board_post` tiled, `ui_pill`
  9-slice 12). Contents: "HOUR n · ×m" in the hour's palette colour; score / next threshold + fill (pill) in the hour
  colour; "hour n gap: a → b"; LAST THROWS (the last 3 closed chains, newest on top and brighter, marker pill;
  "9 wolves · depth 2   +620"; depth text only when depth ≥ 1; misses not listed); BEST TONIGHT.
- **Throw quality colour** = points ÷ the gap of the hour it was thrown in (`NightGoal.ThrowQuality`): < 5% cream,
  5–15% amber, 15–30% orange, 30–50% red, 50–100% magenta (pulsing), ≥ 100% animated rainbow. Inspector values.
- **Popups:** points only ("+60"), coloured by DEPTH (0 cream, 1 amber, 2 orange, 3 red, 4+ magenta…). The chain-close
  popup keeps the total, quality-coloured. No depth/multiplier tags. The display term is "depth" everywhere.
- **Right: two stacked tutorial cards** — DEPTH (a column of wolves with arrows and per-depth multipliers) and HOURS (a
  moon → sun track with hour colours and multipliers, a peg marker between hours). Values read from the scoring / night
  definitions, never hard-coded. Shown always for now (just-in-time later). Art: `Art/UI/icon_wolf`, `icon_arrow_down`,
  `icon_moon`, `icon_sun` (+ fx_hit_star, stone, peg sprites).
- The current debug HUD behind a toggle key, off by default.

### Not now
Wall-slice effects, the chimney loss scene, onboarding triggers, a skill tree, slice unlocks, moon art (Yam makes it).

## PR order and status
| PR | What | Status |
|---|---|---|
| A | Rules + Meta core: Dusk + Begin, per-hour stats, quality, peg triggers, Stone/Peg progression, CampaignPlan, save sections, profiles | ✔ merged (#30) |
| B | Authored nights, campaign mode, the built tower | ✔ merged (#31) |
| — | Debug: "Campaign/Go to Debug Night", "Campaign/Reset campaign", copies in the campaign log | this file's PR |
| C | Phase flow + camera (CameraDirector, NightFlow, doors, reload on Retry/Next, moon hook) | ✔ merged (#33) |
| D | Night scoreboard + popups by depth + tutorial cards + HUD toggle + the Night frame from the tower | ✔ merged (#34, #35) |
| F | Barn room day phase + slice placement (+ F2: slices by hand, UI kit tool) | ✔ merged (#36, #37) |
| E | Post-run screen | moved to stage 2 (below) |

Every PR: updates ARCHITECTURE.md / TASKS.md, lists its editor steps, passes `dotnet run --project Tools/CoreCheck`,
merges the latest Production first (CLAUDE.md).

### PR C — what it needs to do
- `CameraDirector` (Simulation, on the camera): named frames (Night from `session.Night`, Doors, Barn room, Tower from
  `TowerBuilder.TopY`), `MoveTo(frame)` with ease in-out, `IsMoving`; sets position + orthographic size in `Update`
  (CameraShake restores in its early Update and shakes on top in LateUpdate — order already right).
- `NightFlow` (Simulation, campaign scene): owns the flow state (Boot → BarnRoom → [Tower] → Rising → Night → Settling →
  Doors/PostRun). Boot at Doors → ease to Barn room; Start night → rise to Night → `session.BeginNight()` on arrival;
  NightEnded → wait for the sweep to settle (no robot falling, every chain closed) → Doors + doors open → post-run.
  Retry / Next(index) → `Progression.SetCurrentNight` + save → reload. Buttons are non-interactive while the camera moves.
- Remove `PlayAgain` and the OnGUI end screen from TestNight; move the `[Night]` log out of `NightEndView` into a small
  non-OnGUI logger so TestNight keeps it.
- TestNight: NightSession **Start Immediately off** (the flow begins the night); the scene must be in Build Settings.
- Placeholder Start / Retry / Next buttons are fine (the real post-run screen is PR E, the barn is PR F).

## Stage 2 — progression, post-run, HUD, night select, pig face (planned 2026-10-04)

### Playtest findings that drive it (Yam's logs)
- Night length is right: hours 21–53 s, ~4 min for a 7-hour night.
- Night 3 losses all end with **0 stones**: ammo decides them, not breaches. → P raises the refill with each evolution and
  adds peg throws per hour; the empty-pile "dead time" question (TASKS ▸ Open) is still open.
- Progression maxed out within ~3 nights with fast-iteration numbers — Yam retunes the thresholds (data, not code).
- Campaign data fixed: start Bouncy, night 1 dawn → Bomb, night 2 dawn → Splitter.

### PR order and status
| PR | What | Status |
|---|---|---|
| P | Progression: stone evolution list (look + refill per level, cap 25), peg copies up to 8 with copy stages → +1 peg throw per hour per stage, the 2-row pegboard | ✔ code (this PR); editor steps in the PR |
| E | Post-run screen (§4) + avg per stone, thrown vs lost, BEST CHAINS, stage progress | next |
| G | HUD reshape: screen-space night track + two screen-space chalkboards (record, tips); removes the world scoreboard and the HOURS card | after E (needs Yam's mockup + peg tip art) |
| H | Night selection overlay (replaces Next night) | after G |
| I | Pig face (Presentation only) | after H (any time) |

Every PR: CoreCheck passes, Night.unity keeps working, editor steps listed in the PR, TASKS.md ticked.

### PR P — progression changes
- **Stone evolutions** = the `levels` list on `ThrowableDefinition` (kept, so each level's sprite / radius / trail stay):
  each entry adds **Stones Needed** and **Refill** (per hour). Level = how many entries' Stones Needed the night's stone
  count reaches (entry 0 = level 1, always at least 1); refill = that entry's. `evolveAtStones`, `refill` and
  `evolvedRefill` are removed. Default list 10 → lv1 +1 · 15 → lv2 +2 · 20 → lv3 +3 · 25 → lv4 +4; max stones 25. The
  saved cause stays the stone's hits; stones, level and refill are derived (`StoneProgression`, Meta, CoreCheck).
  `StoneStatus` gains the next evolution (stones needed, its level) for the post-run's "dots up to the next evolution".
- **Peg copies**: no fixed 4 — `PegDefinition.maxCopies` (default 8); `copyThresholds` has one entry per extra copy.
  **Copy stages** (`PegDefinition.copyStages`, default 4, 6, 8 copies): each stage a type has reached adds +1 to the
  hour's peg-throw pool. Throws per hour = the night's `pegThrowsPerThreshold` (base 1) + Σ stages reached over the owned
  types (`PegProgression`, CoreCheck: 3 copies → 1 · 4 → 2 · 6 → 3 · 8 → 4 · Bouncy 6 + Bomb 4 → 4). The pool is shared
  across types and still limited by the copies on the shelf. Fixed when the night starts. **Campaign only** — Night.unity's
  shelf is its loadout, not owned copies, so it keeps its night's own throws.
- **Barn pegboard**: 8 slots per type, two rows of 4; row 2 only when the type owns more than 4 copies. Hole positions come
  from a holes file (a TextAsset: 24 "x,y" lines in pixels from the PNG's top-left, group order type 1 row 1, type 1 row 2,
  type 2 row 1, …), converted with the sprite's pivot and PPU. Locked types: ghosts in row 1 + the lock.
- **Rug**: one refill badge per refill amount (a list, entry 0 = +1); a missing one uses the last there is.
- Art from Yam: `stone_lv3`, `stone_lv4` (same size/pivot as lv1/lv2), `room_pegboard` v2 + its 24-hole file.
- Editor steps: Throwable_Stone's Levels list (4 entries: sprite, radius, trail, Stones Needed, Refill) and Max Stones;
  each peg's Max Copies, Copy Thresholds (one per extra copy) and Copy Stages; the pegboard sprite + Holes File (+ Hole
  Sprite, see question 1); the rug's Refill Badges list.

### PR E — post-run screen (§4), plus
- Night summary adds **avg per stone** (score ÷ stones thrown), and **stones thrown** and **stones lost to breaches** as
  two lines, so a loss explains itself.
- **BEST CHAINS**: the top 3 closed chains of the night (Inspector, up to 5), the scoreboard's row format
  ("9 wolves · depth 2  +620"), quality-coloured. Needs a Rules record of the top chains (`NightState` only keeps the
  best one today) — CoreCheck.
- **PROGRESS**: stones a → b with dots up to the NEXT evolution and that evolution's icon (its level sprite); each peg type:
  copies, the next copy stage ("4/6 → +1 throw per hour"), a NEW tag when unlocked tonight (before/after snapshot of the
  profile taken before banking).
- **Hour colours sampled by night progress** (see question 3) land here: the post-run's hour dots are the first new user.
- Editor steps: the two panels in the Doors frame (UI kit), template rows / dots, the Retry / Next buttons moved in.

### PR G — HUD reshape (replaces the world-space chalkboard and the HOURS card)
- **Night track** (screen space, across the top): a thin line, one circle per hour (equal segments, not proportional to
  score), the moon riding it. Inside a segment the moon moves by progress through that hour's gap. Reaching a circle = the
  hour moment (when the scoreboard's hour changes today: the round's start): the circle fills with the hour colour, its ×m
  pops; the peg round starts there. A small peg icon between circles. At dawn the moon becomes the sun.
- The moon rise during the camera's rise uses `sky_moon` / `sky_moon_glow` (`NightFlow.MoonRises` already fires).
- **Two screen-space chalkboards** (`board_frame`, no post) — LEFT = the record: total score, LAST THROWS, BEST TONIGHT.
  RIGHT = tips, just in time: DEPTH on the first chain with depth ≥ 1; a peg type's tip the first time it's in the pig's
  hand during placement. **Tips seen are saved** (a new additive save section, Meta + `ProfileJson` + CoreCheck) so they
  don't repeat; learned tips collapse to small icons, expandable on hover.
- Removed: `NightScoreboard` (only TestNight uses it) and `HoursCardView` (in no scene); DEPTH becomes the first tip.
- Before it starts: Yam's mockup + peg tip art.

### PR H — night selection overlay
- Replaces the single "Next night" button. From the barn room and the post-run, an overlay lists the campaign's nights.
  Unlocked = a dawn on the previous night (night 1 always). Each entry: number, hours, slice count, a dawn badge if won;
  locked: greyed with `icon_lock`. Choosing one saves the index and reloads (like Next night); replaying earlier nights
  earns mastery.

### PR I — pig face
- Presentation only, from events / state: calm by default; worried at stones left ≤ A; gritting at ≤ B; `mouth_o` while any
  wolf is in the danger zone; a flinch on a breach. The rig's swap layers (mouth_smile / grin / grit / o,
  eye_*_closed); A and B in the Inspector. The PR lists the Animator parameters for Yam to wire.

### Contradictions and open questions (found while planning)
1. **Pegboard row 2 "only shows" past 4 copies** — if room_pegboard v2 paints all 24 holes, code can't hide row 2's holes.
   P supports both: with a **Hole Sprite** set, the art has no holes and code draws one per visible slot (row 2 appears at
   5 copies); without it, the art's holes are always visible and only the pegs follow the copies. *Yam: which art?*
2. **Refill badges go to +4**, but only `badge_refill_1/2` exist (and Balance swapped which one shows for refill ≥ 2 —
   the list in P makes the order explicit). *Needs +3 / +4 badge art* (no text on world objects); until then the last
   badge repeats.
3. **Hour colours by night progress don't exist yet**: `HourPaletteDefinition.ColourFor` picks by index and clamps, so a
   5-hour night never reaches the palette's last (dawn gold) colour. Moved to E (first PR showing hour colours after P);
   the scoreboard / track get it for free.
4. **Peg throws per hour from copy stages apply to the campaign only** (decision above). Night.unity unchanged.
5. **Stone asset vs the new defaults**: Throwable_Stone currently has start 5, max 28 and 3 levels (Balance). P's code
   defaults follow the spec (10 → … → 25, cap 25) but assets keep Yam's numbers; the removed fields' values are dropped
   and re-entered in the Levels list (editor step).
6. **H: choosing a night from the barn room** reloads the scene, which boots at the Doors: a visible cut from the barn
   room. H eases the camera to the Doors before reloading.
7. **I: there's no "left the danger zone" event**: a wolf counts as in danger from `RobotEnteredDangerZone` until it loses
   grip, breaches or is removed. Only the night's pig gets the face (the barn room's rig copy stays calm).
8. **More peg throws + bigger refills** (P) both push against the "0 stones" losses; with Yam's retune, watch that night 3
   doesn't flip to never-lost. The dead-time decision (TASKS ▸ Open) is still separate.

## Art map (as found in the repo)
All PPU 400; white sprites are tinted in code; code must still run with any sprite missing. Sprites go in through
Inspector fields, so folders don't matter — only names.
- `Art/Barn/Slices/`: `barn_slice` (the default "barn" slice — no rename), `barn_slice_wood/straw/brick`,
  `barn_bottom_closed/open`, `barn_top`.
- `Art/Barn/barn_open_doors/`: `room_pegboard` (+ `room_pegboard_holes.txt`), `room_materials`, `room_materials_glow`,
  `badge_refill_1/2` (+3/+4 wanted, stage 2), `icon_lock`, `room_preview` (reference). Stage 2: `room_pegboard` v2
  + a 24-hole file.
- `Art/UI/`: `board_frame` (9-slice L36 R36 T36 B52), `board_post` (Tiled), `ui_pill` (9-slice 12),
  `ui_button_primary/secondary` (9-slice L32 R32 T32 B40), `ui_round_rect` (9-slice 20), `ui_pip`, `ui_pip_ring`,
  `ui_pip_glow`, `tag_new`, `icon_wolf`, `icon_arrow_down`, `icon_moon`, `icon_sun`, `sky_moon`, `sky_moon_glow`.
- Existing: `Art/Pegs/peg_*` (+ `peg_bomb_spent`, `peg_level_2/3`, `peg_socket_highlight`, `peg_shelf`), `pile_rag`,
  `Art/Props/Weapons/Stone/stone_lv1/2` (+ lv3/lv4, stage 2), `Art/FX/fx_*`.

## Debug tools (NightSession context menu, play mode)
- Mastery/Reset progress · Mastery/Add 10 hits
- Campaign/Go to Debug Night (the `Debug Night` field, 1-based; saves the index and reloads)
- Campaign/Reset campaign (night 1, no dawns, no tower choices — mastery kept; saves and reloads)
