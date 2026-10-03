# TASKS — Piglings prototype

> **Next up (2026-10-03):** Hours until dawn — M8.1 ✔ → M8.2 pile refactor (☁ done, 🖥 check) → M8.3 peg placement → M8.4 hour palettes → M8.5 Bouncy. Then M6.3 stone readability → decide the empty-pile "dead time" → M5.2.
> Why: the hours change what a night is (no more Stay/Leave), so M5.2 tunes the new loop, not the old one. M5.2 still needs the theft (M7.3) and the stone (M6.3) readable.

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
- [ ] 🖥 M5.2 Tune the night: `thresholds`, `stonesPerThreshold`, `throwsAvailable` (the pile = the pig's life), spawn rate. Log whether the pile drains too fast to breaches.
- [x] ☁ Night end condition in Rules (`NightReferee`): reach `targetScore` = win (once everything in flight settles); *(loss rule since replaced by M7.1: one life, the pile)* Event: `NightEnded`. (Changed from "survive X seconds" — a timer rewards stalling; a score target rewards chains.)

## M6 — Night loop (a night that starts, ends and restarts)
- [ ] ☁+🖥 M6.2 Real (uGUI) end panel, replacing the OnGUI placeholder: result (dawn / caught), hours reached, score, banked, score per stone, mastery gained, best chain. (Restarting is M6.6.)
- [ ] 🖥 M6.3 Stone readability (playtest TODO): colour/outline/trail so the stone is easy to follow.
- [x] ☁ M6.4 Danger zone: `DangerZone` trigger (layer Zones) a little below the roof publishes `RobotEnteredDangerZone` once per robot; `RobotView` sets Animator bool `InDanger`. No robot state. Breach warning now; later the line that ends overtime.
- [x] 🖥 M6.4b Wire the danger zone: place the trigger, add `InDanger`, bake `WolfBot_ClimbDanger` (Piglings ▸ Animation ▸ Bake WolfBot Clips — Climb + red eyes + antenna wiggle, generated), Climb → ClimbDanger on `InDanger`, Break reachable from both. Steps in PR #7 and the play-again PR.
- [x] ~~M6.5 End-of-night choice + overtime~~ — **replaced by Hours until dawn (M8)**; kept for history. Was: GDD section "סוף הלילה: להמשיך או ללכת". Phases `Running → ChoicePending → Overtime → Ended`, `ChoicePending → Ended` (Leave). Reaching the target ends the danger, not the night.
  - [x] ☁ M6.5a Rules/Events/Runtime: `NightPhase` state machine in `NightReferee` (SetPhase → EnterPhase); target reached + no open chain → ChoicePending; Stay → Overtime (overtime score, no weapon mastery), Leave → Ended; overtime ends on stones out or the danger line; the sweep scores every robot on the wall flat with the chain robot-value function; `NightBanked` events (Barn/Weapon) for Meta to consume later. CoreCheck for every transition, the no-weapon-mastery rule and the sweep.
  - [x] ☁ M6.5b Simulation/Presentation code: wall pauses in ChoicePending (no `Time.timeScale`), Stay/Leave buttons, sweep on Ended, falling-robot timeout (a ball resting on a hold can't keep a chain open forever), danger line looks different in Overtime, sweep counter, end screen (result, score vs target, barn/weapon mastery, play again), one tuning log line at Ended.
  - [x] 🖥 M6.5c Unity wiring: add `NightChoice`, `NightEndView`, `DangerZoneView` (+ a line sprite) to Night.unity; check `RobotDefinition.Max Fall Seconds`. Steps in the PR.
  - Waits for Meta: turning `NightBanked` into barn / weapon mastery.
- [x] ~~☁ M6.7~~ *(replaced by M8: hour multiplier)* Overtime doubles points in play: ×2 at scoring time in `ScoreCurve.RobotTotal` (`ScoringDefinition.overtimeMultiplier`), popups tagged `OT×2`; barn mastery banks points as scored (no second ×2); the sweep stays ×1 on both paths.
- [x] ~~☁ M6.8~~ *(replaced by M8)* Overtime fairness + look: only robots spawned during overtime can end it at the danger line (no instant end from where robots froze); overtime chain results use their own style (animated rainbow); "Preview popups" context menu.
- [x] ☁ M6.6 Play again: after `NightEnded` the HUD shows "Click to play again"; the next click (after a short grace) reloads the active scene (`PlayAgain`, Simulation).

## M7 — Ammo on the perch + breaches that play out
Context (story frame, GDD): the villain is the Big Bad Wolf; the robots are his tools, driven by his remote, clearing the way so he can climb up and get the pigs. On a loss he goes down the chimney after them (off-screen / cartoon — the pigs are never shown eaten); the end-of-night sweep happens with it or just after. Exact sequence still being designed (see open questions). Structure: ARCHITECTURE.md → "Ammo on the perch" and the robot state machine.
- [x] 🖥 M7.0 Already in the scene: breach line (TopZone) moved up under the pig's hole — (0, 7.2), 4.4 × 0.2; danger line (DangerZone) just above the wall's top beam — (0, 6.275), 4.4 × 0.2; perch pieces + hanging "no wolves" sign as art-only children of TopZone; night background that scrolls, with a day version fading in when the night ends (`ScrollingBackground` + `DayNightBackground`). See SETUP.md §7.
- [x] ☁ M7.1 One life + a real pile (replaces the sprite-state ammo view): no breach limit; a breach takes the top stone, a breach on an empty pile = caught (the only loss); last stone thrown ≠ loss. `StonesChanged` (Thrown/Stolen/Added/Forfeited) from `NightReferee`; `AddStones(n)`. `StonePile` (Simulation) mirrors the count with real stones in pyramid slots (parked: no physics/collider); the top stone hops to the hand when the pig may throw; a thief carries its stone off (parked, never a weapon). CoreCheck for the rule and every count change.
- [x] 🖥 M7.2 Pile wired: `StonePile` on the right perch piece (session, Stone prefab, hand = ThrowOrigin, spawner), `ThrowController.pile` set; ThrowOrigin moved under the pig's throwing-hand bone and the held stone follows it without parenting (PR #17); Stone prefab on Sorting Layer **Pig**, order 20, so it draws over the arm.
  - [ ] 🖥 `pile_rag.png` under the pile (not in the repo/scene yet).
- M7.3 Robot `Breaching` state: `Climbing → Breaching → Removed(EnteredBarn)`. Lean: no animation polish.
  - [x] ☁ M7.3a Code: `ReachTop` enters `Breaching` and publishes `RobotBreached` — **the breach counts there** (Stolen / Caught at that moment; the stolen stone moves onto the robot then). `Removed(EnteredBarn)` at the end is cleanup, never counted again. `EnterState(Breaching)`: collider off, kinematic, still. Hard limit `RobotDefinition.BreachSeconds`. Not paused by ChoicePending; skipped by the Ended sweep. CoreCheck: Stolen once at entry, Caught at entry, a chain reaching the target during another robot's breach = win, overtime breach ends at entry.
  - [x] ☁ M7.3c Breach sequence (code): the robot climbs to the free breach slot nearest to where it arrived (fixed slots on the perch, above the throw line; overflow stands past the last slot), the stolen stone hops to it (lands at 60 %), it holds it, jumps off, is removed at `BreachSeconds` and frees its slot. All timed from `BreachSeconds` (`BreachTiming`, CoreCheck). `RobotView` lowers a breaching robot's opacity ("not a target"); its stone stays opaque.
  - [ ] 🖥 M7.3b Editor: add 4–5 breach-slot Transforms in a row beside the pile (above the throw line) and assign them, in order, to `BarnTopZone.Slots`; raise `Breach Seconds` (0.5 is too short for the sequence — try 1.5); check `Breach Opacity` (~0.7) on the robot prefab's `RobotView`. Play-check steps in the PR. A real breach clip (bones only, not colours) comes later.

## M8 — Hours until dawn (GDD section **"שעות הלילה"** = source of truth)
Replaces M6.5's Stay/Leave + overtime. A night = N score thresholds ("hours"); each one raises the score multiplier (+`hourMultiplierStep`), refills stones and pauses the wall for a peg-placement round; the last one is dawn (won). Loss unchanged (breach on an empty pile); a loss banks the last threshold reached. Structure: ARCHITECTURE.md → "Night phases: hours until dawn".
- [x] ☁ M8.1 Rules/Runtime/Events: phases `Running → PegPlacement → Running … → Ended(Dawn)`, `Caught` from Running; hour multiplier in `ScoreCurve.RobotTotal` (fixed at throw, rounded once, never carried); `HourReached` / `DawnReached` / `PegPlaced` / `PegMerged`; refill per threshold; board (sockets) + shelf state; `PlacePeg` (place, merge, max level); loss banks the last threshold; sweep on both outcomes (scored only on a win). Overtime / choice removed. `PegDefinition` (id, maxLevel, mergeable), `NightDefinition` thresholds / refill / loadout / throws, `refillPauseSeconds` clock in `NightSession`. CoreCheck for all of it.
  - [ ] 🖥 M8.1b Editor: set `Night_01` thresholds (and `stonesPerThreshold`), check `Scoring` hour step; remove the old NightChoice component; play to dawn and to caught. Steps in the PR.
- [x] ☁ M8.2 Pile refactor, no behaviour change: `PileLayout` (slots, CoreCheck), `ItemPile<T>` (mirror-and-reconcile, drops, put back), `HopMover` (the hop to a moving target); `StonePile` sits on them, same serialized fields.
  - [x] 🖥 M8.2b Play-check the stone pile is unchanged (hop to hand, refill drop, theft hop).
- [x] ☁ M8.2c Playtest fixes: after a threshold, play goes on (wall + throwing) until the chains in play at the crossing land — then the round (the only freeze); later chains don't delay it and keep scoring through it. A ball stuck still (`StuckSpeed` for `StuckSeconds`, RobotDefinition) is removed so it can't hold the chain open.
- [ ] ☁+🖥 M8.3 Peg placement: sockets on the Holds, peg shelf (one pile per type, above the pig), next peg hops to the hand, right-click swaps / left-click picks, peg throw on the normal arc, snap to the nearest valid socket to the aimed point (radius; none = red line, refused), highlights, transparency, dim, shelf pulse / hover / cursor. Sprites on `PegDefinition` (missing = still runs).
- [ ] ☁+🖥 M8.4 Hour palettes: one asset per hour (popup colours + background tint, night → dawn); day background on `DawnReached`.
- [ ] ☁+🖥 M8.5 Bouncy peg: a ball that hits it gets +bonus (× level) on its depth multiplier for everything it scores after; once per peg per ball.

## Open design questions (from the GDD — don't implement until decided)
- One robot line per run vs. mixed swarm.
- House modification mid-run vs. between runs.
- Upgrade families and their levers (from M4.5): stone upgrades → `stoneValue` / `growthPerHit`; wolf upgrades → `wolfValue`; pegs / slices → `multiplierPerDepth`; compounding (`carryScoredTotal`) as a rare late upgrade. When the first real upgrade exists, move these values onto the stone/robot definitions.
- Boss nights: each `NightDefinition` is a "blind" (target, stones, spawn rate, wall); later, one rule twisted per boss night.
- **PROPOSAL** — The wolf's route to the chimney (hook, rope, ladder) as part of the loss scene: built in one go after the fatal breach on an empty pile. (No longer a per-breach counter: there's no breach limit.)
- **PROPOSAL** — The sweep's cause is the remote: on a loss the wolf switches it off (robots go limp); on a win, at dawn, he smashes it in frustration.
- **PROPOSAL** — Loss sequence order: wolf climbs + chimney → barn shakes → remote off, sweep → payout (progress still fills) → "next night locked".
- **PROPOSAL** — The pig's face as a warning meter using the rig's hidden mouths, escalating by **stones left** (the thing that actually kills you): worried at 5, gritting at 2, panicking at an empty rag; mouth_o when a robot is in the danger zone.
- **PROPOSAL** — Loadout size in the day phase (how many weapons you can take onto the perch).
- **IDEA** — Hidden "Pacifist" achievement: lose every stone to the wolves without throwing once (possible weapon unlock).
- *(Decided: breaches steal stones — option A; the 5-breach limit is gone. See M7.1.)*
- **OPEN** — Empty-pile dead time: with 0 stones, nothing falling and dawn not reached, the player can only watch robots climb until one breaches (~10–15 s). Fast-forward the wall once the player can't act, or make the wait the start of the loss scene (the wolf begins his climb)?
  - **PROPOSAL C — "the wolf cranks the remote"**: when the player has **no possible action** (no stones, nothing in flight, below dawn), the robots climb at a high multiplier, so the breach lands in ~2 s and the loss scene starts. The condition is "no possible action", not `StonesLeft == 0`: `AddStones` can refill the pile, and a falling chain can still win. Don't build it yet.
- **WATCH in M5.2** — Death spiral: every breach takes a stone, fewer stones → fewer throws → more breaches. Fine if it's readable (the pile shows it); if a bad first minute always decides the night, tune spawn rate before stone count.

## Playtest log
<!-- date · what you tried · what you felt · what to change -->

2026-09-26 · first playtest · chains work: best 5 robots / depth 2, 2 reached top in ~40s ·
TODO: stone hard to see (color) → M6.3 · ~~last-chain line shows empty chains from misses~~ fixed in PR #2
