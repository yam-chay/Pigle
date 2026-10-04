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
- **Stones:** start 10, +1 per stone-mastery threshold, **evolve at 15** (lv2 look + radius, hourly refill 1 → 2), keep
  adding up to **20** (cap). Hits are the saved cause.
- **New look everywhere:** depth-coloured, points-only popups and the debug-HUD toggle apply to Night.unity too.
- **UI:** Yam lays out each panel's frame once (Canvas, panel images, anchors, one template row/dot) in a local session;
  the components fill and repeat them from data.
- **Separate profiles:** `NightSession.profileName` → `piglings_<name>.json`: "dev" in Night.unity, "campaign" in TestNight.
- **Save stores causes only:** dawns per night id (unlocks are derived), the current night index (navigation), peg triggers
  per merged level (copies are derived), tower choices per night. Save stays v1 (additive sections).
- **One save rule for both scenes:** the stone's level = evolved (15 stones) everywhere.
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
- **Slice placement (MVP):** click a slot to cycle through the available slices; the tower rebuilds live (`TowerBuilder.Build`
  + `PegBoard.Rebuild`, before the night begins only); the choice is saved (`Progression.SetTower`).
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
| D | Night scoreboard + popups by depth + tutorial cards + HUD toggle + the Night frame from the tower | ✔ code (this PR); editor steps in the PR |
| E | Post-run screen | after C |
| F | Barn room day phase + slice placement | after C |

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

## Art map (as found in the repo)
All PPU 400; white sprites are tinted in code; code must still run with any sprite missing. Sprites go in through
Inspector fields, so folders don't matter — only names.
- `Art/Barn/Slices/`: `barn_slice` (the default "barn" slice — no rename), `barn_slice_wood/straw/brick`,
  `barn_bottom_closed/open`, `barn_top`.
- `Art/Barn/barn_open_doors/`: `room_pegboard` (+ `room_pegboard_holes.txt`), `room_materials`, `room_materials_glow`,
  `badge_refill_1/2`, `icon_lock`, `room_preview` (reference).
- `Art/UI/`: `board_frame` (9-slice L36 R36 T36 B52), `board_post` (Tiled), `ui_pill` (9-slice 12),
  `ui_button_primary/secondary` (9-slice L32 R32 T32 B40), `ui_round_rect` (9-slice 20), `ui_pip`, `ui_pip_ring`,
  `ui_pip_glow`, `tag_new`, `icon_wolf`, `icon_arrow_down`, `icon_moon`, `icon_sun`, `sky_moon`, `sky_moon_glow`.
- Existing: `Art/Pegs/peg_*` (+ `peg_bomb_spent`, `peg_level_2/3`, `peg_socket_highlight`, `peg_shelf`), `pile_rag`,
  `Art/Props/Weapons/Stone/stone_lv1/2`, `Art/FX/fx_*`.

## Debug tools (NightSession context menu, play mode)
- Mastery/Reset progress · Mastery/Add 10 hits
- Campaign/Go to Debug Night (the `Debug Night` field, 1-based; saves the index and reloads)
- Campaign/Reset campaign (night 1, no dawns, no tower choices — mastery kept; saves and reloads)
