# TASKS — Piglings prototype

☁ = cloud session OK (code/docs only) · 🖥 = local, needs the Unity editor (use Claude Code + unity-mcp)

## M0 — Setup 🖥
- [x] SETUP.md steps 1–7

## M1 — First chain 🖥
- [x] Playtest: stone → robot → its ball → second robot. HUD shows depth 1.
- [x] Note what feels off (fall speed, bounciness, spawn rate) in the Playtest log below.

## M2 — Throw feel
- [x] ☁ M2.1 Port CCTD `ThrowSolver` (Reference/CCTD) into Simulation: aim by target point with an arc, not a straight line. Keep `ThrowController`'s public surface (`Thrown` event, `Launch` call).
- [x] ☁ M2.2 Port `TrajectoryView` into Presentation (reads aim from ThrowController, draws the arc). Presentation must not change state.
- [x] 🖥 M2.3 Wire both in Night.unity; tune throw speed so the whole wall is reachable from the hole.

## M3 — Robots feel right 🖥
- [x] Animator for WolfBot: Climb (loop), Break (≈0.6 s: flail → X eyes → limbs collapse). Rebuild from the Wolf-Bot Rig Tester keyframes.
- [x] Animator for Pig: Idle (loop), Throw. From the Pig Rig Tester.
- [ ] Tune `BreakDuration` — if chains feel slow, shorten the flail first.

## M4 — Chains readable
- [x] ☁ M4.1 Rules: combo multiplier by depth; publish a `ChainScored` event with the final value. Add CoreCheck cases.
- [x] ☁ M4.2 Presentation: floating "+N" / depth popup on `RobotLostGrip`, bigger on `ChainClosed`. (Unity-side: list editor steps in the PR.)
- [x] ☁ M4.3 Screen shake on depth ≥ 2: `CameraShake` (trauma² shake from deep hits and big chains; restores the camera before gameplay reads it, so aiming never shakes).
- [x] 🖥 M4.3b Add `CameraShake` to the Main Camera and tune it in play.
- [x] ☁ M4.6 Popup juice: every popup colour is a `PopupColor` style (Solid by key / Over lifetime / Cycle / Per-letter), overtime = animated per-letter rainbow (danger line too); big hits (depth, chain size) shake and stay longer.
- [x] ☁ M4.4 *(superseded by M4.5)* Rules: rework scoring. Robot points = basePoints × order in chain; per-robot multiplier = 1 + multiplierPerDepth × own depth, applied immediately. No chain-end multiplier; `ChainScored` = sum. Popup "30 ×2" → "+60".
- [x] ☁ M4.5 Rules: value flows down the chain. Stone carries `stoneValue` (+`growthPerHit` per extra hit); a knocked robot scores received × (1 + `multiplierPerDepth` × depth) and carries `wolfValue` + what it received. Low defaults (10/10/10/×0.5); compounding (`carryScoredTotal`) kept as an off switch for future upgrades.

## M5 — Walls matter
- [ ] 🖥 Wall_Straw / Wall_Brick definitions with different hold bounciness; A/B them in play.
- [ ] 🖥 M5.2 Tune the night: `targetScore`, `throwsAvailable`, `stonesLostPerBreach`, `maxBreaches`. Log whether losing a stone per breach creates tension or snowballs into hopeless nights (if it snowballs: set it to 0 and let `maxBreaches` defend).
- [x] ☁ Night end condition in Rules (`NightReferee`): reach `targetScore` = win (once everything in flight settles); out of stones = lose; a robot reaching the top costs `stonesLostPerBreach` stones; `maxBreaches` = lose. Event: `NightEnded`. (Changed from "survive X seconds" — a timer rewards stalling; a score target rewards chains.)

## M6 — Night loop (a night that starts, ends and restarts)
- [ ] ☁+🖥 M6.2 Real (uGUI) end panel and choice buttons, replacing the M6.5 OnGUI placeholders: result + reason, score / target, score per stone, mastery gained, best chain. (Restarting is M6.6.)
- [ ] 🖥 M6.3 Stone readability (playtest TODO): colour/outline/trail so the stone is easy to follow.
- [x] ☁ M6.4 Danger zone: `DangerZone` trigger (layer Zones) a little below the roof publishes `RobotEnteredDangerZone` once per robot; `RobotView` sets Animator bool `InDanger`. No robot state. Breach warning now; later the line that ends overtime.
- [x] 🖥 M6.4b Wire the danger zone: place the trigger, add `InDanger`, bake `WolfBot_ClimbDanger` (Piglings ▸ Animation ▸ Bake WolfBot Clips — Climb + red eyes + antenna wiggle, generated), Climb → ClimbDanger on `InDanger`, Break reachable from both. Steps in PR #7 and the play-again PR.
- [ ] M6.5 End-of-night choice + overtime — GDD section **"סוף הלילה: להמשיך או ללכת"** (source of truth). Phases `Running → ChoicePending → Overtime → Ended`, `ChoicePending → Ended` (Leave). Reaching the target ends the danger, not the night.
  - [x] ☁ M6.5a Rules/Events/Runtime: `NightPhase` state machine in `NightReferee` (SetPhase → EnterPhase); target reached + no open chain → ChoicePending; Stay → Overtime (overtime score, no weapon mastery), Leave → Ended; overtime ends on stones out or the danger line; the sweep scores every robot on the wall flat with the chain robot-value function; `NightBanked` events (Barn/Weapon) for Meta to consume later. CoreCheck for every transition, the no-weapon-mastery rule and the sweep.
  - [x] ☁ M6.5b Simulation/Presentation code: wall pauses in ChoicePending (no `Time.timeScale`), Stay/Leave buttons, sweep on Ended, falling-robot timeout (a ball resting on a hold can't keep a chain open forever), danger line looks different in Overtime, sweep counter, end screen (result, score vs target, barn/weapon mastery, play again), one tuning log line at Ended.
  - [x] 🖥 M6.5c Unity wiring: add `NightChoice`, `NightEndView`, `DangerZoneView` (+ a line sprite) to Night.unity; check `RobotDefinition.Max Fall Seconds`. Steps in the PR.
  - Waits for Meta: turning `NightBanked` into barn / weapon mastery.
- [x] ☁ M6.7 Overtime doubles points in play: ×2 at scoring time in `ScoreCurve.RobotTotal` (`ScoringDefinition.overtimeMultiplier`), popups tagged `OT×2`; barn mastery banks points as scored (no second ×2); the sweep stays ×1 on both paths.
- [x] ☁ M6.8 Overtime fairness + look: only robots spawned during overtime can end it at the danger line (no instant end from where robots froze); overtime chain results use their own style (animated rainbow); "Preview popups" context menu.
- [x] ☁ M6.6 Play again: after `NightEnded` the HUD shows "Click to play again"; the next click (after a short grace) reloads the active scene (`PlayAgain`, Simulation).

## M7 — Ammo on the perch + breaches that play out
Context (story frame, GDD): the villain is the Big Bad Wolf; the robots are his tools, driven by his remote, clearing the way so he can climb up and get the pigs. On a loss he goes down the chimney after them (off-screen / cartoon — the pigs are never shown eaten); the end-of-night sweep happens with it or just after. Exact sequence still being designed (see open questions). Structure: ARCHITECTURE.md → "Ammo on the perch" and the robot state machine.
- [x] 🖥 M7.0 Already in the scene: breach line (TopZone) moved up under the pig's hole — (0, 7.2), 4.4 × 0.2; danger line (DangerZone) just above the wall's top beam — (0, 6.275), 4.4 × 0.2; perch pieces + hanging "no wolves" sign as art-only children of TopZone; night background that scrolls, with a day version fading in when the night ends (`ScrollingBackground` + `DayNightBackground`). See SETUP.md §7.
- [ ] ☁ M7.1 Ammo view seam: a Presentation ammo view that reads the count from Runtime and its look (container sprite states, item sprite) from the throwable's Definition (new fields). One reading rule: many = fullness, ≤ 5 = individual items + pulsing number, 0 = empty. Nothing stone-specific in code. CoreCheck for any engine-free part.
- [ ] 🖥 M7.2 Stone pile on the perch: container art for full → ≤ 5 discrete → empty; one stone hops from the pile to the pig's hand on each throw.
- [ ] ☁+🖥 M7.3 Robot `Breaching` state: `ReachedTop → Breaching → Removed(EnteredBarn)`. Collider off, alive for the breach animation, then jumps off the porch and falls. Never starts or joins a chain; never holds a chain or the night open (hard time limit). Physics changes only in `EnterState`. Code ☁, animation 🖥. 5 breaches still lose the night.

## Open design questions (from the GDD — don't implement until decided)
- One robot line per run vs. mixed swarm.
- House modification mid-run vs. between runs.
- Upgrade families and their levers (from M4.5): stone upgrades → `stoneValue` / `growthPerHit`; wolf upgrades → `wolfValue`; pegs / slices → `multiplierPerDepth`; compounding (`carryScoredTotal`) as a rare late upgrade. When the first real upgrade exists, move these values onto the stone/robot definitions.
- Boss nights: each `NightDefinition` is a "blind" (target, stones, spawn rate, wall); later, one rule twisted per boss night.
- **PROPOSAL** — What a breach does to stones: (A) the robots steal stones — disarming the pig is part of clearing the way; or (B) the pig spends a stone swatting the robot off the perch. (Today a breach silently removes `stonesLostPerBreach` stones — 1 in Night_01.)
- **PROPOSAL** — The breach counter as the wolf's route being built: each breach installs one piece (hook, rope, ladder rungs toward the chimney); breach 5 completes it and the wolf appears.
- **PROPOSAL** — The sweep's cause is the remote: on a loss the wolf switches it off (robots go limp); on a win, at dawn, he smashes it in frustration.
- **PROPOSAL** — Loss sequence order: wolf climbs + chimney → barn shakes → remote off, sweep → payout (progress still fills) → "next night locked".
- **PROPOSAL** — The pig's face as a warning / breach meter using the rig's hidden mouths (mouth_o when a robot is in the danger zone, escalating per breach).
- **PROPOSAL** — Loadout size in the day phase (how many weapons you can take onto the perch).

## Playtest log
<!-- date · what you tried · what you felt · what to change -->

2026-09-26 · first playtest · chains work: best 5 robots / depth 2, 2 reached top in ~40s ·
TODO: stone hard to see (color) → M6.3 · ~~last-chain line shows empty chains from misses~~ fixed in PR #2
