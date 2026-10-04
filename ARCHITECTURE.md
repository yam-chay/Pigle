# Piglings — Architecture

> Wins on **structure**. The GDD (Claude Docs) wins on **design**. CLAUDE.md is how to work here. TASKS.md is what's next.

## Layers

```
Definitions   ScriptableObjects. Immutable design data (robots, throwables, wall materials, nights).
Runtime       Plain C# state (NightState). No behaviour, no engine types. Serializable.
Events        The backbone: EventBus + immutable event structs carrying ChainId + Attribution.
Rules         Plain C#. Subscribes to events, writes Runtime state, publishes results (ChainTracker).
Meta          Plain C#. The player's progress across nights (PlayerProfile), the rules that apply a banked night to it, the save format.
Simulation    Unity. Physics bodies and their state machines (RobotController, Throwable, spawner, zones).
Presentation  Unity. Views, rigs, animation, HUD. Reads state and reacts to events. Never writes state.
```

### Dependency rules (enforced by .asmdef references)

| Assembly | May reference | Engine? |
|---|---|---|
| Piglings.Events | — | no (`noEngineReferences`) |
| Piglings.Runtime | Events | no |
| Piglings.Rules | Events, Runtime | no |
| Piglings.Meta | Events, Runtime (+ Newtonsoft.Json.dll, precompiled) | no |
| Piglings.Definitions | Events (shared enums only, e.g. `PegEffect`) | yes |
| Piglings.Simulation | Events, Runtime, Rules, Meta, Definitions | yes |
| Piglings.Presentation | Events, Runtime, Meta, Definitions, Simulation | yes |
| Piglings.Editor | — (editor-only tooling) | yes, Editor platform only |

- No game assembly may reference `Piglings.Editor`.

- Engine-free layers compile outside Unity (`Tools/CoreCheck`). That is what makes cloud sessions useful: they can build and verify everything below Simulation without an editor.
- Runtime state refers to things by `GameId` / string id — never to a ScriptableObject or GameObject. (This is also what lets saves store causes and derive results later.)
- Presentation never calls a method that changes gameplay state. If a view needs something to happen, that's a Simulation feature.

## Composition — no bootstrap

There is no bootstrap scene, no bootstrap assembly, no service locator, no singletons, no `DontDestroyOnLoad`.

Each playable scene has exactly one **scene-scoped owner**. For the prototype that's `NightSession` (Simulation):
- `Awake` (execution order -1000) creates `EventBus`, `NightState`, `IdAllocator`, the Rules (`ChainTracker`, then `NightReferee`, then `PegEffects` and `MasteryTally`), binds the `PegBoard` (every Hold learns its socket), loads the player's profile from disk and creates Meta's `Progression` around it.
- Every scene object that needs them gets the `NightSession` through a **serialized field** in the Inspector. No `FindObjectOfType`, no statics.
- When the scene unloads, everything goes with it.

**Persistence (decided, M9.1): the disk is the home of everything that outlives a scene.** No object survives a scene change. Each scene owner loads what it needs from disk when it starts and saves what it changed when its work is done: `NightSession` loads the profile in `Awake` and saves it once, at `NightEnded`. Play Again reloads the scene, so the next night reads the profile again from disk. Quitting mid-night saves nothing (that night's hits are lost — fine). A future Day scene follows the same rule with its own owner. See "Mastery and the save".

## The event backbone: chains

- A **chain** starts when a throw is released (`ThrowReleased`, new `ChainId`).
- The throwable knocks a climbing robot loose → `RobotLostGrip` with `Attribution.FromThrowable` (depth 0).
- A falling robot ball knocks another robot loose → `RobotLostGrip` with `Attribution.FromRobotBall(robot, depth)` (depth + 1), same `ChainId`.
- When the throwable and every robot of the chain are removed, `ChainTracker` publishes `ChainClosed(robotsDropped, maxDepth)`.

Outside chains, the wall publishes night-level facts:
- A climbing robot crosses the danger line → `RobotEnteredDangerZone(robot)`, once per robot (published by `DangerZone`). A warning only: no robot state, and it never ends or changes the night.
- A climbing robot reaches the roof → it enters `Breaching` and publishes `RobotBreached(robot)`: **that is the breach**, counted by `NightReferee` at that moment. With stones left it takes the top stone (`StonesChanged(Stolen, robot)`); with none left the pigs are caught — the only way to lose. When the breach sequence ends, `RobotRemoved(EnteredBarn)` is cleanup only and never counts again.
- Every change to the stone count → `StonesChanged(count, delta, cause, robot)`, published only by `NightReferee` (Thrown / Stolen / Added).
- A falling ball that never lands (resting on a hold) is removed after `RobotDefinition.MaxFallSeconds` → `RobotRemoved(TimedOut)`, so its chain can close.

## Night phases: hours until dawn (`NightReferee`, GDD "שעות הלילה")

```
Running(hour 1) ──threshold──▶ PegPlacement ──▶ Running(hour 2) ──▶ … ──last threshold──▶ Ended (dawn, won)
   └──a robot breaches with no stones left (caught, lost) ─────────────────────────────────▶ Ended
```
- One state machine; `NightState.Phase` is the source of truth; transitions only via `SetPhase` → `EnterPhase`. Each change publishes `NightPhaseChanged(from, to)`. The hour isn't a phase: it's `NightState.Hour`.
- **Thresholds** (`NightDefinition.thresholds`, e.g. 5 → five hours; `Rules.NightGoal` keeps them ≥ 1 and strictly rising). Crossing one (not the last) counts it (`ThresholdsReached`: banking, next threshold) and queues a placement round. **The new hour starts inside the freeze**, when that round starts: `NightState.Hour` +1 and `HourReached(hour, multiplier, threshold)` — the moment the hour's visuals and juice belong to. Play goes on: the wall keeps climbing and the pig keeps throwing, still at the current hour's multiplier. The placement round starts once the chains that were in play **at the crossing** have settled (`NightReferee` keeps one set per waiting round), so the chain that crossed is never cut. Chains thrown after the crossing don't hold the round off (steady throwing would postpone it forever): they keep falling and scoring through the pause. One chain crossing several thresholds earns one round each, played back to back (`PegPlacement → PegPlacement`).
- **Hour multiplier**: each hour adds `ScoringDefinition.hourMultiplierStep` (0.5 → ×1, ×1.5, ×2…). It lives in one place, `ScoreCurve.RobotTotal` (with depth): received × depth multiplier × hour multiplier, rounded once. Fixed when the stone is thrown (`ChainTracker` records it per chain), from `NightState.Hour`; applies to what a robot scores, never to what it carries on, so it never compounds. `RobotScored` / `ChainScored` carry the hour for views.
- **PegPlacement**: the wall pauses — `NightState.WallMoving` (= Running) is false, so robots stop climbing and the spawner stops (no `Time.timeScale`; breaching robots finish their sequence). The pile gets `NightDefinition.stonesPerThreshold` (`StonesChanged(Added)`), and the player gets `pegThrowsPerThreshold` pegs to place: `NightSession.PlacePeg(socket, pegId)` → `NightReferee.PlacePeg` → `PegPlaced` (empty socket, level 1) or `PegMerged` (same mergeable type, +1 level up to `PegDefinition.maxLevel`). A peg throw costs no stone, isn't a `ThrowReleased` and never starts a chain. The round ends when the throws are used, or — when nothing can be placed (`CanPlaceAnyPeg` false) — `NightSession` ends it after `NightDefinition.refillPauseSeconds` (Rules can't keep time). Unused throws are lost.
- **Dawn**: crossing the last threshold → `DawnReached`. The night can't be lost from there; throwing stops, and it ends (`Ended`, `Dawn`) once every chain has settled. No placement round, so no refill, at dawn.
- **Caught**: a breach on an empty pile, in any hour before dawn (a threshold that isn't dawn protects nothing) → `Ended`, `Caught`.
- **Ended**: while handling `NightPhaseChanged(→Ended)` (the bus is synchronous), `RobotSpawner` sweeps the wall: every climbing robot calls `Sweep()` → `RobotSwept`. On both outcomes the robots fall. Won: each is scored flat by the referee with `ScoreCurve.RobotValue()` (the same function chains use), ×1. Lost: visual only. Then `NightBanked(Barn, BankedScore)` and `NightEnded` once. **BankedScore** = the live score at dawn (sweep included); the last threshold reached when caught (`NightGoal.ScoreAtThreshold`) — 0 before the first.
- Banking: `NightBanked(destination, id, stat, amount, multiplier)` per mastery target, then `NightEnded`. Barn = the banked score; Weapon / Lineage = tonight's use (see "Mastery and the save"), banked on **both** outcomes. Meta's `Progression` applies them to the saved profile; the barn score isn't saved yet.
- *Replaced:* the M6.5 end-of-night choice (Stay / Leave, `ChoicePending`, `Overtime` and its ×2, the danger line ending overtime, forfeited stones, leftover stones → weapon mastery). See TASKS.md history.

**The pegs (Runtime, written only by `NightReferee`).** `NightState.Sockets` = one `PegSocket` per Hold (peg id + level, by string, never a ScriptableObject); its length is also the most pegs the wall can hold. `NightState.Shelf` = one `PegStack` (peg id + count) per type, in loadout order, at most `PegSetup.ShelfCapacity` (5) types. `PegThrowsLeft` = this round. Definitions: `PegDefinition` (id, maxLevel, mergeable); `NightDefinition.pegLoadout` is the shelf until the day phase exists. `NightSession` turns both into the Rules' plain `PegType` / `PegSetup`. 

**Peg placement (M8.3, Simulation + Presentation).**
- `PegBoard` (on Barn): every `Hold` under it, in hierarchy order = the sockets (socket i = the i-th Hold; `NightSession.board` gives the Rules the count). `NearestValid(point, pegId, radius)` asks the Rules which sockets the peg can go into.
- `PegShelf` (on peg_shelf): one pile per shelf type, mirroring `NightState.Shelf` with real peg objects — the shared pile pieces (`ItemPile`, `HopMover`, `PileLayout`). In a round with throws left the next peg hops to the hand (ThrowOrigin) by itself; `SwapNext` / `Pick` hop it back to its slot and bring another; after the round it hops back. The stone pile puts its held stone back during a round (the hand is the peg's).
- `PegThrower`: press-to-aim / release like the stone, same `ThrowSolver` arc, but the aim **snaps to the nearest valid socket** within `snapRadius` of the pointer and is solved to it; none = red line, release refused, nothing used. The peg is moved along the solved arc (no physics: can't hit robots, never a chain), then `NightSession.PlacePeg` — refused = it hops back. Right mouse = `SwapNext`; a press that starts on a shelf peg is a pick, never an aim.
- Presentation: `PegBoardView` (placed sprites on the Holds, level 2/3 overlays; in a round, pulsing highlight on valid sockets and fading of pegs the held one can't go on), `PegShelfView` (pulse + glow in a round, hover scale/glow + optional cursor; hover comes from `PegThrower.HoveredShelfPeg`, so Presentation needs no input), `PlacementFade` (sprites under an object fade in a round — the stone pile), `PlacementDim` (a dark overlay fades in), `RobotView.placementOpacity` (robots fade, Animator-safe). `TrajectoryView` / `PigView` take the peg thrower too. All sprites are Inspector fields; a missing one falls back (plain hold sprite tinted).

**Peg effects (M8.5)** — what a placed peg does when something hits it. Behaviour keys off `PegDefinition.effect` (`PegEffect`, in Events: Plain / Bouncy / Splitter / Bomb), never the id string. Strength comes from the peg's level through `PegDefinition.levels` (entry 0 = level 1; a level past the list uses the last entry), like the stone's levels.
- **The hit**: `Hold.OnCollisionEnter2D` (only for an occupied socket; a flying stone or a falling ball) → `NightSession.HitPeg(socket, hitter, id, chain)` → `Rules.PegEffects.Hit`, which reads the peg from `NightState.Sockets`, publishes `PegHit` (a fact, plain pegs too) and the effect's own event, and returns a `PegHitResult` for the physics part. A method call, not an event, because the Simulation needs the answer on the spot (the `PlacePeg` precedent). Empty sockets report nothing. Nothing after the night ends.
- **Bouncy**: a falling ball that hits it gets the peg's score multiplier (×2 at level 1, ×3 at level 2…) on what its victims **score** from then on — never on what they carry (like the hour multiplier, so it never compounds). Once per peg per ball. Several Bouncy pegs add on the extra part: each adds (multiplier − 1), so two ×2 pegs = ×3. Lives in `ChainTracker` with the chain (`AddPegExtra`, `RobotScored.PegMultiplier`) and goes when the chain closes. Stones bounce off physically but get no bonus. Physics: `PegBoard` gives the socket's collider a runtime `PhysicsMaterial2D` with the level's bounciness (same friction as the hold) on `PegPlaced` / `PegMerged`.
- **Views**: `PegBoardView` draws a placed peg on a child renderer (the Hold's own one hides under it), so the peg can squash-stretch on every Bouncy hit without scaling the Hold's collider; a ball that gets the bonus also gets a small gold ring (fx_burst_ring). The score popup tags it `B×2`.
- **Splitter** (the needle): a thrown stone or piece that hits it becomes `PiecesAt(level)` stones (2 at level 1, 3 at level 2…). The Rules decide (`StoneSplit`): once per needle per stone, cut short so one throw never has more than `MaxStonesPerThrow` stones flying (`ChainTracker.StonesInFlight`), and every stone — the original too — carries the stone's current value × `ValueShareAt(level)` (`ChainTracker.ShareStoneValue`). The Hold then calls `Throwable.Split`: the stone keeps flying as the middle of a fan (`FanAngleAt(level)`, same speed, around its velocity just after the bounce) and clones launch as the rest, smaller by `PieceScale`, with the parent's level look, trail and remaining lifetime. Each piece publishes `StonePieceLaunched`: `ChainTracker` adds it to the chain (the chain closes only once every piece is gone) with the parent's value, `PegEffects` marks it as already split at its needle, and `MasteryTally` credits its direct hits to the stone's weapon when the split's toggle (`countSplitHitsForMastery`) is on. Pieces aren't throws: no `ThrowReleased`, the stone count and ThrowsUsed don't change. Robot balls never split. Views: the needle flashes and sparkles burst out of it (`PegBoardView`).
- *Coming:* Bomb (M8.5c). Bombs do nothing during PegPlacement (a plain hold, no charge used) — chains thrown after a threshold's crossing can still be falling then (see "Night phases").

Events are facts, never commands. Nobody "asks" through the bus.

## Robot state machine (ported from CCTD SpiderController)

```
Spawned -> Climbing -> LosingGrip -> Falling -> Removed(HitGround | TimedOut)
              \-> Breaching -> Removed(EnteredBarn)
                   (ReachTop: the breach counts HERE)   (cleanup only)
```
- `RobotController.EnterState` is the ONLY place a state becomes physics (body type, layer).
- `LosingGrip` = the break clip (flail → crack → limbs collapse). Still kinematic. Duration from `RobotDefinition.BreakDuration`.
- `Falling` = dynamic ball on layer `RobotBall`, bounces through the Holds. Only a Falling robot passes a chain on. A ball that's nearly still (`StuckSpeed`) for `StuckSeconds` — wedged on a hold or between pegs — or still falling after `MaxFallSeconds`, is removed (`TimedOut`) so it can't hold a chain (or the next hour) open.
- `Breaching` (M7.3) = the breach sequence. `ReachTop` (a climbing robot touching `BarnTopZone`) enters it, then publishes `RobotBreached`. **The breach counts on entering `Breaching`**: `StonesChanged(Stolen)` or `Caught` happen at that moment, and the stolen stone moves onto the robot then. Why at the start: the outcome is fixed when the player sees the robot arrive; a chain that crosses a threshold during the sequence can't undo a theft, and a pile that's empty by the time the robot leaves must not count as a second breach. State before event: if the breach ends the night, the sweep (inside the publish) already sees the robot as not climbing.
  - `EnterState(Breaching)`: collider off, kinematic. It **never starts or joins a chain** (not `Falling`, no collider, touches no zone).
  - **The sequence**, all timed from `RobotDefinition.BreachSeconds` (the one tuning value, in the asset) by `BreachTiming` (Simulation, engine-free, checked by CoreCheck), as fractions of it:
    ```
    0 ──climbs onto the perch──▶ 0.35 ──holds──▶ 0.8 ──jumps off──▶ 1 → Removed(EnteredBarn)
    0 ──────stolen stone hops pile → robot──────▶ 0.6
    ```
    Where it stands: a **breach slot** on the perch. `BarnTopZone.slots` are fixed Transforms in the scene (4–5, beside the pile, above the throw line, so a breaching robot is clearly out of play and never sits where the player aims). A breaching robot `Claim`s the free slot nearest to where it arrived and `Release`s it in `Remove`, so thieves line up side by side, each with its stone. All slots full: it stands past the last one, continuing the row at 0.7 × the slot spacing (a small overlap) — rare, but defined. The robot moves there along a kinematic path in `FixedUpdate`, and jumps off away from the barn's middle (jump shape in ball radii). `StonePile` times the stone's hop with the same timeline, so the stone always lands before the jump, whatever `BreachSeconds` is.
  - The end at `BreachSeconds` is a hard limit — whatever the animation does, same class of bug as the stuck ball. That removal is **cleanup only**: `NightReferee` doesn't listen to it.
  - Not paused by PegPlacement (it isn't climbing — it finishes its sequence; `RobotView` doesn't freeze its Animator). Skipped by the Ended sweep (`Sweep` only takes climbing robots); its time limit removes it.
  - "Not a target" look: `RobotView` lowers the robot's opacity (`breachOpacity`, Inspector, ~0.7) from the state, re-applied in `LateUpdate` because the climb clips animate part colours. Its renderers are cached in `Awake`, so the stolen stone riding on it stays fully opaque. A future breach clip should move bones, not colours.

## Physics layers & matrix

Layers: `RobotClimbing`, `RobotBall`, `Throwable`, `Holds`, `BarnWalls`, `Ground`, `Zones`.

| collides with → | RobotClimbing | RobotBall | Throwable | Holds | BarnWalls | Ground | Zones |
|---|---|---|---|---|---|---|---|
| RobotClimbing | – | ✔ | ✔ | – | – | – | ✔ |
| RobotBall | | ✔ | ✔ | ✔ | ✔ | ✔ | – |
| Throwable | | | – | ✔ | ✔ | ✔ | – |

Everything else off. Holds are pins for falling balls only; climbing robots pass through them.

## Zones (triggers on layer `Zones`)

Only `RobotClimbing` touches `Zones`, so zones only ever see climbing (or just-hit) robots.

| Zone | Where | What it does |
|---|---|---|
| `BarnTopZone` | the breach line, just under the pig's hole (y ≈ 7.2) | A climbing robot touching it calls `ReachTop` → `Breaching` + `RobotBreached` = the breach, counted at that moment. `RobotRemoved(EnteredBarn)` follows after `BreachSeconds` as cleanup (not counted). Owns the perch's breach slots (claimed / released by breaching robots). Its other children are art only (perch pieces + the hanging "no wolves" sign): no colliders. |
| `DangerZone` | just above the wall's top beam (y ≈ 6.275) | Has `NightSession` (serialized). A climbing robot entering it → `RobotEnteredDangerZone`, once per robot, all night. `RobotView` sets the Animator bool `InDanger`. No robot state. A warning only: it never ends or changes the night. |
| `GroundZone` | below the barn | Marker. Falling balls and throwables touching it are removed. |

## The stones: one life, a real pile on the perch (M7.1)

**Loss rule.** The stones are the pig's life. There's no breach limit. A robot that breaches takes the top stone and leaves; a robot that breaches while the pile is empty catches the pigs (`NightEndReason.Caught`) — the only loss. Throwing your last stone isn't a loss: the night goes on, and a chain still in flight can cross a threshold (a refill comes with each hour) or reach dawn (after dawn the night can't be lost).

**One truth, one mirror.**
- **Runtime** `NightState.StonesLeft` is the count, written only by `NightReferee.ChangeStones`, which publishes `StonesChanged`. `NightReferee.AddStones(n)` is the seam for other mid-run refills. CoreCheck covers all of it.
- **Simulation** `StonePile` (on the right perch piece) **mirrors** the count with real stone GameObjects; it never decides it. It hands out the real throwables, which is why it's Simulation, not Presentation.
  - Stones sit in fixed pyramid slots, no physics, no collider (`Throwable.Park`). Top stone = last slot filled.
  - When the hand is empty and the pig may throw, the top stone hops to the hand (ThrowOrigin) and waits. `ThrowController` only aims with a stone in the hand, and launches *that* stone (`StonePile.ReleaseHeld` → `Throwable.Launch`). Hop < throw cooldown, so there's no input lag.
  - `Stolen` (at `RobotBreached`, when the breach starts): the top stone hops from the pile to the thief (same arc as the hop to the hand), landing at `BreachTiming.StoneLands` of its `BreachSeconds`; then it's parented to the thief and rides it off. Parked all the way (never a weapon, can't start a chain); destroyed with the robot. `Added`: new stones drop onto the next free slots. `Added` also comes with each hour's placement round (`stonesPerThreshold`), during the pause, where it can be watched.
  - After every change the pile reconciles to the count, so it can never disagree with the HUD.
- **Shared pile pieces (M8.2)** — what every "count mirrored with real objects" needs, so the peg shelf and future per-weapon ammo containers don't copy `StonePile`: `PileLayout` (engine-free pyramid slots; CoreCheck), `ItemPile<T>` (objects in their slots: add with a drop, take the top, put back, reconcile to a count), `HopMover` (the one hop arc to a moving target: hand, thief, slot; updated in `LateUpdate`). Plain classes owned by a component; the component keeps the serialized settings and the event wiring. `StonePile` = these + the hand + thefts.
- **No object pool yet**: stones are instantiated from the Stone prefab and destroyed (`StonePile.AddToPile` / `Destroy`). The "Weapons pool" commit (f810c2e) wired the pile into the scene and set the Stone prefab's sorting — it added no pooling code. A pool can slot in behind `StonePile` later.

**Coming:** mixed weapon types per night → the perch is the loadout, one container per weapon (needs a per-weapon count in Runtime; today there's one). The rag under the stones is the first container tier (rag → bindle → sack → dedicated tool); weapon mastery will show on it.

## Mastery and the save (M9)

Weapon mastery comes from **use**: a weapon levels up from the robots it knocks loose itself. The save stores **causes** (counts), never results (levels): a level is derived from the saved count and the weapon definition's thresholds, so thresholds can be retuned without breaking a save.

**Counting (Rules, `MasteryTally`)** into `NightState` (Runtime), keyed by definition id:
- `WeaponHits[weapon]`: a `RobotLostGrip` with `Attribution.FromThrowable`. One throw that knocks several robots counts each.
- `BallKnocks[type]` / `KnockedByBall[type]`: a `RobotLostGrip` with `FromRobotBall` — the knocker's type and the knocked robot's type. Never counts for the weapon. Recorded for future wolf-lineage mastery; nothing reads them yet.
- The ids come from the events: `ThrowReleased.Weapon` (`ThrowableDefinition.Id`) and `RobotSpawned.RobotType` (`RobotDefinition.Id`). **These ids are save keys — never rename one** once players have a save (it'd orphan their progress, or need a migration).
- The sweep (`RobotSwept`) never counts. Nothing counts once the night has ended (it's already banked).

**Banking (`NightReferee.FinishNight`)**, on both outcomes (mastery from use, not score): `NightBanked(Weapon, weapon, DirectHits, n, 1)` per weapon, `NightBanked(Lineage, type, BallKnocks | KnockedByBall, n, 1)` per robot type, only for non-zero counts. Then `NightEnded`.

**Applying (Meta, `Progression`)**: owns the `PlayerProfile`, applies each `NightBanked` (clamped add). By `NightEnded` the profile is complete; `NightSession` saves it then.

**Levels (Meta, `MasteryLevels`)**: thresholds are cumulative totals, strictly rising (`[50, 150]` = level 2 at 50 hits, level 3 at 150). `LevelFor(hits, thresholds)`, `NextThreshold`, `Problem` (for an Inspector warning). The level a night plays with is fixed when it starts; a level earned tonight shows at night end and applies from the next night.

**The level in play (M9.2, Simulation)**:
- `ThrowableDefinition.levels`: one entry per level `{hitsRequired, sprite, radiusScale, trailColour}`, entry 0 = level 1 (its `hitsRequired` ignored). `Thresholds()` feeds `MasteryLevels`. A level past the list uses the last entry; a level with no sprite uses the one before's; an empty list = one level that looks like the prefab.
- `NightSession.WeaponLevel`: `night.Throwable`'s level from the saved hits, fixed in `Awake` (logged, with the next threshold; a broken threshold list is a warning). `BankedWeaponLevel`: what the saved hits give now — after `NightEnded`, the next night's level.
- `Throwable.ApplyLevel(def, level)`: the level's sprite, and a uniform scale that makes **the sprite exactly as wide as the collider** (computed from the sprite's own bounds, so any pixels-per-unit works; the parent's scale divided out). `Launch` always applies `WeaponLevel`, so play never depends on what the pile is showing.
- `StonePile`: creates stones at the night's level and spreads its slots by the level's size (`spacing`/`rowHeight` are for level 1). At `NightEnded`, if the night earned a level, every stone on the pile (and in the hand) switches to it and the pile relayouts (`ItemPile.Relayout`) — the look only; nothing can be thrown after the end.

**Shown during the night (M9.3, Presentation — progress is shown, never applied; no progress bars)**:
- `NightSession.WeaponHitsSoFar` (saved at night start + tonight), `WeaponProgress` (0..1 from tonight's level toward the next, `MasteryLevels.ProgressFrom`, CoreCheck), `WeaponUpgradeReady` (tonight reached the next level). `StonePile.Stones` / `Held` and `Throwable.Look` / `InFlight` / `Definition` are read-only hooks for the views.
- `StoneHitFx`: on each direct hit (`RobotLostGrip` from the throwable — what MasteryTally counts), a hit star + a small rising sparkle at the robot (GameId → Transform map from `RobotSpawner.Spawned`, like the score popups). Ball knocks get none.
- `StonePileMasteryView` (on the pile): sparkles glint on random stones, more often as `WeaponProgress` rises; once `WeaponUpgradeReady`, the stones pulse gold (tint only — size is gameplay; a stone that leaves the pile mid-pulse gets its rest colour back); at `NightEnded` with a level earned, a burst ring over the pile while `StonePile` swaps the stones.
- `StoneTrail` (on the Stone prefab, with a `TrailRenderer` using fx_trail): emits only in flight, cleared at launch, coloured by the level's Trail Colour, as wide as the stone. This is also the stone-readability fix (M6.3).
- `FxSprites`: the shared one-shot effect sprite (spawn, grow, rise, spin, fade, destroy). Plain class owned by a view, like `ItemPile`. A missing sprite = no effect.

**The save format (Meta, `ProfileJson`)** — `piglings_profile.json` in `Application.persistentDataPath`:
```json
{
  "version": 1,
  "weapons": { "stone":         { "directHits": 137 } },
  "robots":  { "wolfbot_basic": { "ballKnocks": 412, "knockedByBall": 300 } }
}
```
- One object per id, so a new field (feats…) is an addition, not a new version. Unknown fields are ignored; missing ones read as 0.
- Read strictly: bad JSON, wrong types, negative / fractional / too-large counts, empty or repeated ids → **Corrupt**. No version, or one this build doesn't know (e.g. a newer build's save) → **UnknownVersion**. Both are handled the same way (below). Older versions would be migrated in `ProfileJson.Read`; there are none yet.
- Parsed with Newtonsoft's `JObject` (no reflection: IL2CPP-safe), shape checked by hand. Meta's asmdef references `Newtonsoft.Json.dll` (package `com.unity.nuget.newtonsoft-json`); CoreCheck uses the same version from NuGet.

**The file (`Simulation/ProfileFile`)** — engine-free (like `ThrowSolver`), so CoreCheck runs it on a real temp folder; `NightSession` just passes `persistentDataPath` and logs.
- **Save**: write `…json.tmp`, flush to disk, copy the current save to `piglings_profile.prev.json`, then `File.Replace` the .tmp over the save (one rename). Never a half-written save. A failed write is logged as an error; the old save is untouched and the game plays on.
- **Load**: the save → if bad, copy it to `piglings_profile.corrupt-<UTC time>.json` and try `.prev` (at most one night lost) → if that's bad too, copy it aside (then remove it) and start fresh. Whatever it ends with is written back at once, so the next launch doesn't trip on the same file. A bad file is never deleted or overwritten without its copy; if the copy fails, saving is switched off for the session. A leftover `.tmp` is ignored.
- **Logs**: one line on load (where from, the counts, the path; a warning with problems + backup paths if anything was wrong) and one on each save.
- **Playtest tools** (`NightSession` context menu, play mode): *Mastery ▸ Reset progress* (empty profile, saved now — the old save is in `.prev`), *Mastery ▸ Add 10 hits* (into tonight's tally, so they bank and save at night end like real hits).

## Background
`BackgroundNight` (`ScrollingBackground`: endless tiled scroll) with child `BackgroundDay` (`DayNightBackground`: fades in on `NightEnded`). Presentation only.

## Scale convention

**1 art pixel (design px) = 0.01 world units.**
| Asset | PNG resolution | Pixels Per Unit | Root scale |
|---|---|---|---|
| Barn pieces | 4× | 400 | 1 |
| Pig parts | 2× | 200 | 0.26 |
| Wolf-bot parts | 3× | 300 | 0.3 |
Barn = 6 units wide. Wolf-bot ball radius = 58 px × 0.3 × 0.01 = 0.174.
