# Prototype v2 — the plan (M10)

> The approved plan for Prototype v2, kept in the repo so any session can pick it up. Approved 2026-10-04.
> Order of authority: ARCHITECTURE.md (structure) > this file (what v2 is and how it's split) > TASKS.md (status).
> Design was done in chat with Yam; this file is the record of it. All numbers are placeholders (definitions / Inspector).

## What v2 is
Authored nights + the barn day phase + progression + a post-run screen + a night scoreboard, in a **new scene,
`TestNight.unity`** (Yam duplicated it from Night.unity; cloud sessions can't create scenes). **Night.unity and the current
build keep working unchanged**: new behaviour only activates when its components/data are present.

The loop: night ends (dawn / out of stones) → the sweep finishes → the **post-run** panels over the Night frame (stage 2,
PR E; was: at the Doors) → a button → the camera descends to the **Doors**, the doors open → **Barn room** (day loadout) → optional **Tower** (slice placement) → Start night → the
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

### 4. Post-run screen — PR E: replaced by Yam's final spec in "Stage 2 ▸ PR E" (2026-10-05)

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
| P | Progression: stone evolution list (look + refill per level, cap 25), peg copies up to 10 + same-type follow-ups (one per N copies), the 2-row pegboard | ✔ merged (#38, #39); follow-up chain in this PR |
| E | Post-run screen (Yam's final spec below) + the out-of-stones loss, records, wolves dropped, hour colours by progress | ✔ this PR (editor steps pending) |
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
- **Peg copies**: no fixed 4 — `PegDefinition.maxCopies` (default 10); `copyThresholds` has one entry per extra copy.
  **Follow-ups** (`PegDefinition.followUpEveryCopies`, default 3): a type earns one follow-up per that many copies
  (3 copies → 1, 6 → 2, 9 → 3). In an hour's round, placing one of its pegs then gives that many more throws, one after
  another, each the **same type**: with 10 copies, 4 in a row. The chain is given once per round (for the type thrown;
  it never stacks across types), each follow-up only if that type can still be placed (a chain that can't go on ends).
  Rules: `PegType.FollowUps`, `NightState.PegFollowUp` / `PegFollowUpsLeft`, `PegFollowUpGranted(id, remaining)`; CoreCheck.
  **Campaign only** (from owned copies); Night.unity's loadout has none. *(History: copy stages adding to a shared pool
  (#38), then one follow-up at 4 copies (#39) — both replaced after Yam played them.)*
- **Barn pegboard**: 8 slots per type, two rows of 4; row 2 only when the type owns more than 4 copies. Hole positions come
  from a holes file (a TextAsset: 24 "x,y" lines in pixels from the PNG's top-left, group order type 1 row 1, type 1 row 2,
  type 2 row 1, …), converted with the sprite's pivot and PPU. Locked types: ghosts in row 1 + the lock.
- **Rug**: one refill badge per refill amount (a list, entry 0 = +1); a missing one uses the last there is.
- Art from Yam: `stone_lv3`, `stone_lv4` (same size/pivot as lv1/lv2), `room_pegboard` v2 + its 24-hole file.
- Editor steps: Throwable_Stone's Levels list (4 entries: sprite, radius, trail, Stones Needed, Refill) and Max Stones;
  each peg's Max Copies, Copy Thresholds (one per extra copy) and Follow Up Every Copies; the pegboard sprite + Holes File (+ Hole
  Sprite, see question 1); the rug's Refill Badges list.

### PR E — post-run screen (Yam's final spec, 2026-10-05; replaces §4 and every earlier PR E note)
Mockup: `post_run_v5.png` → `Docs/Mockups/` (outside Assets, so Unity doesn't import it; PNGs are LFS, so cloud sessions
can't read it — the code fills Yam's frames, it doesn't depend on the layout).

**End of night (rule change)**
- Loss = **out of stones**: stones = 0 and no chain active → wait for the last chain to settle + a delay, then the night is
  a loss, banked at the last hour reached. No waiting for a wolf to breach.
- A breach on an empty pile can no longer happen first; the Caught path goes unless something still needs it.
- At that moment the wolf's chimney climb starts (hook only). The delay before the sweep and the post-run is an Inspector
  value (to match the wolf's sequence later). Dawn ends as before.

**Camera**
- The post-run appears over the **Night** frame (scene dimmed, Inspector alpha). The camera does NOT move at night end.
- It moves only on a button: To the barn → Doors → Barn room. Next night → down to the barn room for the next night's
  loadout. Retry night → same night (flow: see question 4).

**Buttons** — "To the barn" and "Next night" are never shown together. All disabled while the camera moves.
- LOSS: Retry night (primary) + To the barn (secondary).
- DAWN: Next night ▸ (primary) + Retry night (secondary). After the last night's dawn: To the barn instead of Next night.

**Left — two panels**
- **THE NIGHT**: "NIGHT n", the result (DAWN gold / OUT OF STONES), hour dots in the hour colours up to the hour reached,
  the score (loss: "kept X (hour n)"). BEST THROW EACH HOUR: one row per hour reached — "Hn" in the hour colour, a bar
  relative to the night's best throw, the value; the night's best in rainbow. Biggest chain (wolves · depth), avg per stone
  (score ÷ stones thrown), stones thrown · stolen by wolves.
- **ALL-TIME RECORDS**: best throw, longest chain (wolves), deepest chain, best night score. Broken tonight → NEW RECORD
  tag + gold value. Stored in the save (additive).

**Right — PROGRESS: only what moved tonight**
- Every bar: BEFORE tonight (dim) + TONIGHT's gain (bright), the gain filling in when the panel appears; a completed bar
  shows READY.
- **Stones** (always): "+N hits", bar to the next +1 stone, "a → b stones", the next evolution, an evolution track
  (each level's Stones Needed + stone sprite; reached ones full, others faded).
- **A peg type only if it triggered tonight or was unlocked tonight**: copies a → b, bar to the next copy, the next
  follow-up step ("6 copies → 3 in a row" — see question 7). Unlocked tonight: NEW tag + "unlocked by dawn on night n".
  Unused types: no row.
- **Wolves dropped** (always): "+N" tonight and the all-time total. Every wolf knocked loose, stored per robot type in the
  save (for later lineage progression).
- Footer: "Upgrades are waiting in the barn". The post-run does NOT play upgrades (PR F follow-up, below).

**Checks / done when**: CoreCheck covers out-of-stones loss timing, records update + NEW flags, "only what moved"
filtering, wolves-dropped counts; Night.unity keeps working; editor steps listed (panel frames, template rows, buttons,
the dim overlay); TASKS.md ticked.

**How it's built (plan)**
- *Rules*: `NightReferee` ends the night the moment the condition holds — Running, not dawn, `StonesLeft == 0`, no open
  chain, no pending round (`PendingPegRounds == 0`) — as `End(Lost, NightEndReason.OutOfStones)`, banked at
  `ScoreAtThreshold` like before. Checked whenever it can become true: a chain closes, a theft takes the last stone, a
  round ends. A breach on an empty pile still counts as a breach (`RobotsReachedTop`, the hour's breaches) but takes
  nothing and ends nothing. `NightEndReason.Caught` is **replaced** by `OutOfStones` (same slot; nothing else needs it).
  New per-night facts: `HourStats.BestThrow`, `NightState.StonesStolen`, `BiggestChainWolves` / `BiggestChainDepth` (one
  chain's wolves and its own depth — `LongestChain` / `DeepestChain` can come from two chains).
- *The delay is presentation*: the sweep happens as the night ends, as before (Yam: the robots are swept while the wolf
  climbs). `NightFlow` has **Wolf Seconds** (Inspector): on an out-of-stones end it raises `WolfClimbStarts(seconds)` (the
  hook), then waits for that long and for the sweep to land before the post-run shows. Dawn: no wolf wait.
- *Meta*: the save gets `"records": {bestThrow, longestChain, deepestChain, bestNightScore}` and
  `"robots".{type}.dropped` (both additive, v1). `Progression.RecordNight` updates the records and returns which broke.
  `MasteryTally` counts `Dropped[type]` on every `RobotLostGrip` (any cause) **and** every swept robot → `NightBanked(Lineage,
  type, Dropped)`. `PostRunProgress` (engine-free) builds the PROGRESS rows from the profile **before** tonight (a copy
  taken at load) and **after** it (banked): the stone row, a peg row per type whose saved triggers changed or that was
  unlocked tonight, the wolves row.
- *Hour colours by night progress*: `HourPaletteDefinition.ColourAt(hour, hours)` samples the palette first → last over
  the night's length (hour 1 = the first colour, the last hour = the last colour, dawn gold), blending between entries.
  `NightSession.HourColour` uses it, so the scoreboard and the HOURS card change with it.
- *Simulation*: `NightFlow` — Night → Settling (wolf delay + the wall settled) → **PostRun at the Night frame** → a button
  → Descending (Night → Doors, the doors open on arrival) → Leaving (save + reload). `PostRunButtons` (two slots, primary
  and secondary; what each does comes from `PostRunChoices`, engine-free + CoreCheck). `FlowButtons` keeps only the
  barn-room buttons.
- *Presentation*: `PostRunView` (dim overlay + fade, button labels), `PostRunNightPanel`, `PostRunRecordsPanel`,
  `PostRunProgressPanel`, `ProgressBarView` (before + gain + READY). Template rows like the scoreboard (`TemplateSlot`).

**Contradictions / questions** (found while planning — recommendations in bold)
1. **Out of stones vs a pending round.** A chain that crossed a threshold queues a round whose refill adds stones, so 0
   stones isn't the end yet. → **The condition includes `PendingPegRounds == 0`** (and not dawn). A round with no refill
   (Refill 0) that ends with an empty pile → the loss right after it.
2. **Caught could still pre-empt the new loss** if the Rules waited out the delay (wolves keep climbing; a breach on 0
   stones). → **Rules end at once; the delay is presentation only** (Rules can't keep time anyway). A breach on an empty
   pile while a chain is still in flight does **nothing** (no stone to take; the outcome waits for the chain). Caught has
   no user left → removed. CoreCheck cases that change: "last stone landed → not a loss" (now: the loss), "caught in hour
   3 banks 100" (now at the landing), "caught before the first threshold", "a threshold doesn't protect you" (now: the
   round, then the loss), "3 breaches take 3 stones — still not lost" (now: lost at the 3rd), "Caught at Breaching entry",
   the mastery and flow cases built on Caught.
3. **The wall during the wolf's delay.** *Decided (Yam):* the sweep happens as the delay starts — the robots are swept
   while the wolf climbs to the pig. So the sweep stays inside the end (unchanged); the delay only holds the post-run.
4. **Retry vs To the barn on a loss** both end in the same night's barn room — identical buttons. *Decided (Yam):*
   **Retry = fast: reload straight into the night** (boot at the Night frame, doors closed, begin at once — the post-run
   was at the Night frame, so the cut is invisible); To the barn = the full day phase. A one-shot "start in the night"
   flag in the save (`campaign.startInNight`, navigation like the night index), cleared and saved as the scene boots.
5. **Restart = reload, and the scene boots at the Doors.** With the post-run at the Night frame, To the barn / Next night
   first ease Night → Doors (doors open), then save + reload — else the cut shows. (Fast Retry, if chosen, needs no move.)
6. **Records are results, not causes** — against "the save stores causes only". They can't be derived (no per-night
   history is saved), so they're the exception, in their own section. A scoring retune doesn't rewrite them. Best night
   score = *decided (Yam):* **the live score** when the night ended (dawn: with the sweep). NEW = tonight beat the record from before tonight (strictly); the
   first night ever sets every non-zero record → all NEW.
7. **Peg row wording**: the old "6 = +1 throw per hour" is the replaced pool rule. → **The follow-up rule**:
   `PegStatus.NextFollowUpAt` → "6 copies → 3 in a row" (1 + its follow-ups); at the max: "max".
8. **Wolves dropped "any cause"** — *decided (Yam):* every robot knocked off the wall during the night, whatever the reason:
   direct hit, ball, bomb **and the end-of-night sweep**. Old saves have none: the all-time total starts from this build.
   *Follow-up (Yam):* the sweep's share is also saved on its own per robot type (`robots.{type}.swept`; knocked in play =
   dropped − swept), so later lineage progression can choose what counts. The screen shows only the total. `NightState.RobotsDropped` only
   counts knocks inside an open chain; the new per-type count counts them all.
9. **"kept X (hour n)"** → n = **the last hour finished** (the one whose threshold the score is kept at); "kept 0" before
   the first. The hour dots / BEST THROW rows go up to the hour being played (reached).
10. **Avg per stone** = the chains' score (without the dawn sweep) ÷ stones thrown — the sweep isn't the stones' work.
11. **Dropped from the old PR E notes** by this spec: BEST CHAINS (top 3), the "next locked peg" row (faded + lock), the
    copy dots. Kept out unless Yam wants them back.
12. **The placeholder post-run goes**: FlowButtons' Retry / Next and the Doors-frame post-run. The post-run log line stays
    (now at the Night frame). PlayAgain / NightEndView already stand down in campaign mode; NightEndView says OUT OF
    STONES (Night.unity has no wolf delay: no flow there).
13. **The stone bar when a stone was earned tonight**: before = progress from the last +1 (dim), gain to full → READY;
    two stones earned still show one full bar + "a → b".

**PR F follow-up — the barn room plays the upgrades** (not part of E): when the barn room opens after a night that moved
something, new stones drop onto the rug one by one, the stones evolve (sprite swap + burst), the +N refill badge changes,
new pegs fly to their holes on the pegboard. Reads the same before / after snapshot as the post-run (needs it to survive
the reload: the save keeps "what the barn has shown" — design in F).

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
4. **Follow-ups apply to the campaign only** (decision above). Night.unity unchanged.
5. **Stone asset vs the new defaults**: Throwable_Stone currently has start 5, max 28 and 3 levels (Balance). P's code
   defaults follow the spec (10 → … → 25, cap 25) but assets keep Yam's numbers; the removed fields' values are dropped
   and re-entered in the Levels list (editor step).
6. **H: choosing a night from the barn room** reloads the scene, which boots at the Doors: a visible cut from the barn
   room. H eases the camera to the Doors before reloading.
7. **I: there's no "left the danger zone" event**: a wolf counts as in danger from `RobotEnteredDangerZone` until it loses
   grip, breaches or is removed. Only the night's pig gets the face (the barn room's rig copy stays calm).
8. **Follow-ups + bigger refills** (P) both push against the "0 stones" losses; with Yam's retune, watch that night 3
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
