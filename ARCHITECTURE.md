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
- `Awake` (execution order -1000) creates `EventBus`, `NightState`, `IdAllocator`, the Rules (`ChainTracker`, then `NightReferee`, then `PegEffects`, `MasteryTally` and `SettleTracker`), binds the `PegBoard` (every Hold learns its socket), loads the player's profile from disk and creates Meta's `Progression` around it.
- Every scene object that needs them gets the `NightSession` through a **serialized field** in the Inspector. No `FindObjectOfType`, no statics.
- When the scene unloads, everything goes with it.

**Persistence (decided, M9.1): the disk is the home of everything that outlives a scene.** No object survives a scene change. Each scene owner loads what it needs from disk when it starts and saves what it changed when its work is done: `NightSession` loads the profile in `Awake` and saves it at `NightEnded` (and, in the campaign scene, again when a post-run button writes where to go — `GoToNight` / `RetryNight` — and when a fast Retry's one-shot `startInNight` is used up at load). Play Again / Retry / Next reload the scene, so the next night reads the profile again from disk. Quitting mid-night saves nothing (that night's hits are lost — fine). A future Day scene follows the same rule with its own owner. See "Mastery and the save".

## The event backbone: chains

- A **chain** starts when a throw is released (`ThrowReleased`, new `ChainId`).
- The throwable knocks a climbing robot loose → `RobotLostGrip` with `Attribution.FromThrowable` (depth 0).
- A falling robot ball knocks another robot loose → `RobotLostGrip` with `Attribution.FromRobotBall(robot, depth)` (depth + 1), same `ChainId`.
- When the throwable and every robot of the chain are removed, `ChainTracker` publishes `ChainClosed(robotsDropped, maxDepth)`.

**Chain scoring (M10.S, Balatro-style; `ScoreCurve` + `ChainTracker`)** — each chain keeps two running numbers, both additive:
- **SCORE** (width): + the stone's base at the throw (by its evolution level: `ThrowableLevel.baseScore`, 10 / 20 / 40 / 80 — fixed
  for the night with the level); + the wolf value for each robot knocked loose, whatever knocked it (stone, ball, bomb, a split
  piece); + the plain-peg score (1) for EVERY contact with a plain hold (an empty socket or a Plain peg), the same stone / ball
  on the same hold at most once per `plainHoldCooldown` (0.2 s; `ChainTracker.TouchPlain` gets the caller's clock — Rules
  can't keep time).
- **MULT** (depth): starts at 1; + `multPerNewDepth` each time the chain reaches a NEW depth (once per level, never per robot —
  wide chains don't gain mult); + a special peg's `multBonus` when it triggers (`ChainTracker.AddPegMult`: Bouncy by stones and
  balls, once per peg per hitter; Splitter / Bomb 0 by default).
- **Result** at the close = SCORE × MULT × the hour multiplier of the hour it was thrown in, rounded once (`ScoreCurve.Result`).
- **Raw, then the remainder**: every SCORE gain goes into `NightState.Score` at once (the bar moves as the chain falls) and is
  published as `ChainGained(cause Stone / Wolf / PlainPeg / Peg, source, socket, scoreAdded, multAdded, running score, mult,
  hour)`; at the close the remainder (result − raw) is added, then `ChainClosed` and `ChainScored` (total, score, mult, hour
  multiplier, remainder). `NightReferee` checks thresholds on both (raw on `ChainGained`, the remainder on `ChainScored`); per-hour
  score = raw + remainder by the chain's hour. Mastery, records, best throws and the post-run use the result.
- **Every peg has a SCORE value per level** (`PegLevel.scoreValue`, `PegType.ScoreValueAt`): a Plain peg adds it on every contact (the plain-hold cooldown applies), a special peg when it triggers (`ChainTracker.AddPegGain`, with its mult bonus). -1 = the default: the Scoring asset's Plain Peg Score for a Plain peg, nothing for a special one. Empty sockets always use Plain Peg Score.
- *Replaced (M4.5–M10.S):* the per-robot carried value (`stoneValue`, `growthPerHit`, `multiplierPerDepth`, `carryScoredTotal`),
  `RobotScored`, Bouncy's score-only multiplier, the Splitter's value share.

Outside chains, the wall publishes night-level facts:
- A climbing robot crosses the danger line → `RobotEnteredDangerZone(robot)`, once per robot (published by `DangerZone`). A warning only: no robot state, and it never ends or changes the night.
- A climbing robot reaches the roof → it enters `Breaching` and publishes `RobotBreached(robot)`: **that is the breach**, counted by `NightReferee` at that moment. With stones left it takes the top stone (`StonesChanged(Stolen, robot)`); on an empty pile it takes nothing (M10.E — the loss is running out of stones, not a breach). When the breach sequence ends, `RobotRemoved(EnteredBarn)` is cleanup only and never counts again.
- Every change to the stone count → `StonesChanged(count, delta, cause, robot)`, published only by `NightReferee` (Thrown / Stolen / Added).
- A falling ball that never lands (resting on a hold) is removed after `RobotDefinition.MaxFallSeconds` → `RobotRemoved(TimedOut)`, so its chain can close.

## Night phases: hours until dawn (`NightReferee`, GDD "שעות הלילה")

```
[Dusk ──Begin()──▶] Running(hour 1) ──threshold──▶ PegPlacement ──▶ Running(hour 2) ──▶ … ──last threshold──▶ Ended (dawn, won)
                       └──out of stones: none left, nothing in flight, no round to refill (lost) ───────────────▶ Ended
```
- **Dusk** (M10, campaign scene only): before the night begins — the day phase, the camera rising. Nothing climbs, nothing can be thrown (`WallMoving` / `CanThrow` are Running-only). `NightSession.BeginNight()` → `NightReferee.Begin()` → Running (`NightPhaseChanged(Dusk → Running)`). `NightSession.startImmediately` (on in Night.unity) skips it: Running from the start, as before.
- One state machine; `NightState.Phase` is the source of truth; transitions only via `SetPhase` → `EnterPhase`. Each change publishes `NightPhaseChanged(from, to)`. The hour isn't a phase: it's `NightState.Hour`.
- **Thresholds** (`NightDefinition.thresholds`, e.g. 5 → five hours; `Rules.NightGoal` keeps them ≥ 1 and strictly rising). Crossing one (not the last) counts it (`ThresholdsReached`: banking, next threshold) and queues a placement round. **The new hour starts inside the freeze**, when that round starts: `NightState.Hour` +1 and `HourReached(hour, multiplier, threshold)` — the moment the hour's visuals and juice belong to. Play goes on: the wall keeps climbing and the pig keeps throwing, still at the current hour's multiplier. The placement round starts once the chains that were in play **at the crossing** have settled (`NightReferee` keeps one set per waiting round), so the chain that crossed is never cut. Chains thrown after the crossing don't hold the round off (steady throwing would postpone it forever): they keep falling and scoring through the pause. One chain crossing several thresholds earns one round each, played back to back (`PegPlacement → PegPlacement`).
- **Hour multiplier**: each hour adds `ScoringDefinition.hourMultiplierStep` (0.5 → ×1, ×1.5, ×2…). Fixed when the stone is thrown (`ChainTracker` records it per chain, from `NightState.Hour`), applied once at the chain's close (`ScoreCurve.Result`: score × mult × hour). `ChainGained` / `ChainScored` carry the hour for views.
- **PegPlacement**: the wall pauses — `NightState.WallMoving` (= Running) is false, so robots stop climbing and the spawner stops (no `Time.timeScale`; breaching robots finish their sequence). The pile gets `NightDefinition.stonesPerThreshold` (`StonesChanged(Added)`), and the player gets `pegThrowsPerThreshold` pegs to place: `NightSession.PlacePeg(socket, pegId)` → `NightReferee.PlacePeg` → `PegPlaced` (empty socket, level 1) or `PegMerged` (same mergeable type, +1 level up to `PegDefinition.maxLevel`). A peg throw costs no stone, isn't a `ThrowReleased` and never starts a chain. The round ends when the throws are used, or — when nothing can be placed (`CanPlaceAnyPeg` false) — `NightSession` ends it after `NightDefinition.refillPauseSeconds` (Rules can't keep time). Unused throws are lost.
- **Dawn**: crossing the last threshold → `DawnReached`. The night can't be lost from there; throwing stops, and it ends (`Ended`, `Dawn`) once every chain has settled. No placement round, so no refill, at dawn.
- **Out of stones** (M10.E, replaced Caught): `NightReferee.OutOfStones` — Running, not dawn, `StonesLeft == 0`, no chain open, no round waiting (`PendingPegRounds == 0`: its refill would add stones) → `Ended`, `Lost`, `NightEndReason.OutOfStones` (the old `Caught` slot), in any hour before dawn. Checked whenever it can become true: a chain closes, a theft takes the last stone, a round ends. The Rules end the night **at once** (they can't keep time); the wolf's climb before the post-run is a delay in `NightFlow`. A breach on an empty pile while a chain is still in flight counts as a breach but takes nothing and ends nothing — the chain decides.
- **Per hour** (`NightState.Hours[]`, written by the referee): score and the best throw (`BestThrow`, M10.E) by the hour each chain was thrown in, breaches by the hour they happened. `BestThrowPoints` / `BestThrowHour` = tonight's best closed chain. Also for the post-run: `StonesStolen` (the referee) and `BiggestChainRobots` / `BiggestChainDepth` (`ChainTracker`: the chain with the most robots and its own depth — `LongestChain` / `DeepestChain` can come from two chains). **Throw quality** = points ÷ the gap of the hour it was thrown in (`NightGoal.HourGap(n)` = T(n) − T(n−1), T(0) = 0; `NightGoal.ThrowQuality`); the colour bands are a view choice. The `[Night]` log prints time / score / breaches per hour (time counted from when the night begins).
- **Ended**: while handling `NightPhaseChanged(→Ended)` (the bus is synchronous), `RobotSpawner` sweeps the wall: every climbing robot calls `Sweep()` → `RobotSwept`. On both outcomes the robots fall — on a loss in the campaign, only later (M11.T1): `RobotSpawner.HoldSweepOnLoss` (set by NightFlow) keeps the swept robots holding on (counted now, `CanLoseGrip` false) until `LetGoSwept()`, after the beat and the wolf. The Rules see the same sweep either way. Won: each is scored flat by the referee with `ScoreCurve.RobotValue()` (the same function chains use), ×1. Lost: no score (they still count as dropped). Then `NightBanked(Barn, BankedScore)` and `NightEnded` once. **BankedScore** = the live score at dawn (sweep included); the last threshold reached when out of stones (`NightGoal.ScoreAtThreshold`) — 0 before the first.
- Banking: `NightBanked(destination, id, stat, amount, multiplier)` per mastery target, then `NightEnded`. Barn = the banked score; Weapon / Lineage = tonight's use (see "Mastery and the save"), banked on **both** outcomes. Meta's `Progression` applies them to the saved profile; the barn score isn't saved yet.
- *Replaced:* the M6.5 end-of-night choice (Stay / Leave, `ChoicePending`, `Overtime` and its ×2, the danger line ending overtime, forfeited stones, leftover stones → weapon mastery). See TASKS.md history.

**The pegs (Runtime, written only by `NightReferee`).** `NightState.Sockets` = one `PegSocket` per Hold (peg id + level, by string, never a ScriptableObject); its length is also the most pegs the wall can hold. `NightState.Shelf` = one `PegStack` (peg id + count) per type, in loadout order, at most `PegSetup.ShelfCapacity` (5) types. `PegThrowsLeft` = this round. Definitions: `PegDefinition` (id, maxLevel, mergeable); `NightDefinition.pegLoadout` is the shelf until the day phase exists. `NightSession` turns both into the Rules' plain `PegType` / `PegSetup`. 

**Peg placement (M8.3, Simulation + Presentation).**
- `PegBoard` (on Barn): every `Hold` under it, in hierarchy order = the sockets (socket i = the i-th Hold; `NightSession.board` gives the Rules the count). `NearestValid(point, pegId, radius)` asks the Rules which sockets the peg can go into.
- `PegShelf` (on peg_shelf): one pile per shelf type, mirroring `NightState.Shelf` with real peg objects — the shared pile pieces (`ItemPile`, `HopMover`, `PileLayout`). In a round with throws left the next peg hops to the hand (ThrowOrigin) by itself; `SwapNext` / `Pick` hop it back to its slot and bring another; after the round it hops back. The stone pile puts its held stone back during a round (the hand is the peg's).
- `PegThrower`: press-to-aim / release like the stone, same `ThrowSolver` arc, but the aim **snaps to the nearest valid socket** within `snapRadius` of the pointer and is solved to it; none = red line, release refused, nothing used. The peg is moved along the solved arc (no physics: can't hit robots, never a chain), then `NightSession.PlacePeg` — refused = it hops back. Right mouse = `SwapNext`; a press that starts on a shelf peg is a pick, never an aim.
- Presentation: `PegBoardView` (placed sprites on the Holds, level 2/3 overlays; in a round, pulsing highlight on valid sockets and fading of pegs the held one can't go on), `PegShelfView` (pulse + glow in a round, hover scale/glow + optional cursor; hover comes from `PegThrower.HoveredShelfPeg`, so Presentation needs no input), `PlacementFade` (sprites under an object fade in a round — the stone pile), `PlacementDim` (a dark overlay fades in), `RobotView.placementOpacity` (robots fade, Animator-safe). `TrajectoryView` / `PigView` take the peg thrower too. All sprites are Inspector fields; a missing one falls back (plain hold sprite tinted).

**Peg effects (M8.5)** — what a placed peg does when something hits it. Behaviour keys off `PegDefinition.effect` (`PegEffect`, in Events: Plain / Bouncy / Splitter / Bomb), never the id string. Strength comes from the peg's level through `PegDefinition.levels` (entry 0 = level 1; a level past the list uses the last entry), like the stone's levels.
- **The hit**: `Hold.OnCollisionEnter2D` (every hold — a flying stone or a falling ball) → `NightSession.HitPeg(socket, hitter, id, chain)` → `Rules.PegEffects.Hit`, which reads the peg from `NightState.Sockets`, publishes `PegHit` (a fact, plain pegs too) and the effect's own event, and returns a `PegHitResult` for the physics part. A method call, not an event, because the Simulation needs the answer on the spot (the `PlacePeg` precedent). An empty socket (a plain hold) or a Plain peg adds the plain-peg score to the chain (M10.S, every contact, a short cooldown per hold per hitter); empty sockets publish no `PegHit`. Nothing after the night ends.
- **Bouncy** (M10.S): a stone or a falling ball bouncing off it adds the peg's `multBonus` (+1 at level 1, +2 at level 2…) to its chain's MULT, once per peg per stone / ball (`PegBounced(socket, hitter, chain, bonus, chain mult)`). Every special peg has a `multBonus` per level (Splitter: on a split; Bomb: on an explosion; 0 by default). Physics: `PegBoard` gives the socket's collider a runtime `PhysicsMaterial2D` with the level's bounciness (same friction as the hold) on `PegPlaced` / `PegMerged`.
- **Views**: `PegBoardView` draws a placed peg on a child renderer (the Hold's own one hides under it), so the peg can squash-stretch on every Bouncy hit without scaling the Hold's collider; a stone or ball that gives its chain the bonus gets a small gold ring (fx_burst_ring). (The popup's `B×2` tag went with the M10.D points-only popups; a peg tutorial card will explain it.)
- **Splitter** (the needle): a thrown stone or piece that hits it becomes `PiecesAt(level)` stones (2 at level 1, 3 at level 2…). The Rules decide (`StoneSplit`): once per needle per stone, cut short so one throw never has more than `MaxStonesPerThrow` stones flying (`ChainTracker.StonesInFlight`). A piece adds no stone base (it isn't a throw); its hits add like any. The Hold then calls `Throwable.Split`: the stone keeps flying as the middle of a fan (`FanAngleAt(level)`, same speed, around its velocity just after the bounce) and clones launch as the rest, smaller by `PieceScale`, with the parent's level look, trail and remaining lifetime. Each piece publishes `StonePieceLaunched`: `ChainTracker` adds it to the chain (the chain closes only once every piece is gone), `PegEffects` marks it as already split at its needle, and `MasteryTally` credits its direct hits to the stone's weapon when the split's toggle (`countSplitHitsForMastery`) is on. Pieces aren't throws: no `ThrowReleased`, the stone count and ThrowsUsed don't change. Robot balls never split. Views: the needle flashes and sparkles burst out of it (`PegBoardView`).
- **Bomb**: a stone (or piece) or a falling ball that hits it sets it off — only when it's charged, the night is **Running** and the trigger is in an open chain. During PegPlacement it's a plain hold and keeps its charge (chains thrown after a threshold's crossing can still be falling then — see "Night phases"). The Rules (`BombExploded`): the **explosion is its own hitter** (a new GameId from the night's `IdAllocator`); its victims add the wolf value like any knock. Victims are 1 deep from a stone, the ball's depth + 1 from a ball (`ChainTracker.DepthOf`), attributed `Attribution.FromPeg(explosion, depth)` (`CauseKind.Peg`) and scored by the chain rules. Then it's **spent** (`PegSocket.Recharge` = `CooldownAt(level)`), a plain hold until `PegEffects.Advance` — fed every frame by NightSession, ignored unless Running, so it pauses with the wall and stops at the end — counts it down (`PegRecharged`). The Hold does the physics: `OverlapCircle` on `RobotClimbing` within `BombRadiusAt(level)` → `LoseGrip`, so they fall as normal balls; plus an optional push (`ImpulseAt`) to falling balls and flying stones in range, strongest at the bomb.
  - Mastery: the victims count for the peg (`NightState.PegKnocks` → `NightBanked(Peg, pegId, PegKnocks)` → the save's `pegs` section), whatever set it off. They count as **stone hits** only when the stone itself — or a Splitter piece whose split counts — set it off by hitting it; a ball-triggered explosion gives the stone nothing.
  - Views: a star and an orange ring that grows exactly to the radius; the spent sprite (or the peg greyed) until it recharges, then a fizz of sparkles on the fuse (`PegBoardView`); `CameraShake` adds `bombTrauma`.

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
- `Breaching` (M7.3) = the breach sequence. `ReachTop` (a climbing robot touching `BarnTopZone`) enters it, then publishes `RobotBreached`. **The breach counts on entering `Breaching`**: `StonesChanged(Stolen)` happens at that moment (nothing on an empty pile), and the stolen stone moves onto the robot then. Why at the start: the outcome is fixed when the player sees the robot arrive; a chain that crosses a threshold during the sequence can't undo a theft, and a pile that's empty by the time the robot leaves must not count as a second breach. State before event: if the breach ends the night, the sweep (inside the publish) already sees the robot as not climbing.
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

**Loss rule (M10.E).** The stones are the pig's life. There's no breach limit. A robot that breaches takes the top stone and leaves (on an empty pile, nothing). The only loss is **out of stones** (`NightEndReason.OutOfStones`): no stone left, nothing in flight and no placement round waiting. Throwing your last stone isn't the loss yet: a chain still in flight can cross a threshold (its round refills) or reach dawn (after dawn the night can't be lost). *Was (M7.1): a breach on an empty pile caught the pigs — it left the player watching robots climb with nothing to do; replaced.*

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
- `Dropped[type]` (M10.E): every robot knocked off the wall, whatever did it — a `RobotLostGrip` of any cause, and the end-of-night sweep (`RobotSwept` while the night ends, before it's banked). The post-run's "wolves dropped"; later lineage progression. `Swept[type]`: the sweep's share of it, kept apart (knocked in play = `Dropped − Swept`, `RobotRecord.KnockedInPlay`) so a later rule can choose what counts; the post-run shows only the total.
- The ids come from the events: `ThrowReleased.Weapon` (`ThrowableDefinition.Id`) and `RobotSpawned.RobotType` (`RobotDefinition.Id`). **These ids are save keys — never rename one** once players have a save (it'd orphan their progress, or need a migration).
- The sweep (`RobotSwept`) never counts for mastery (only for `Dropped`). Nothing counts once the night is banked.

**Banking (`NightReferee.FinishNight`)**, on both outcomes (mastery from use, not score): `NightBanked(Weapon, weapon, DirectHits, n, 1)` per weapon, `NightBanked(Lineage, type, BallKnocks | KnockedByBall | Dropped | Swept, n, 1)` per robot type, only for non-zero counts. Then `NightEnded`.

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
  "robots":  { "wolfbot_basic": { "ballKnocks": 412, "knockedByBall": 300, "dropped": 950, "swept": 210 } },
  "pegs":    { "peg_bomb":      { "knocks": 12, "triggers": [9, 2] } },
  "campaign": { "night": 1, "dawns": { "night_01": 3 }, "startInNight": false },
  "towers":  { "night_02": ["slice_barn", "slice_wood"] },
  "records": { "bestThrow": 620, "longestChain": 9, "deepestChain": 3, "bestNightScore": 4100 }
}
```
Everything after `weapons` / `robots` was added inside version 1 (`pegs` M8.5c; `triggers`, `campaign`, `towers` M10; `dropped`, `swept`, `startInNight`, `records` M10.E): older builds read the file and ignore it — but drop it if they then save.
- **Records are the one kind of result the save keeps** (M10.E): the all-time bests can't be derived from anything saved (no per-night history), so they're stored, in their own section; a scoring retune doesn't rewrite them. `Progression.RecordNight(bestThrow, longestChain, deepestChain, nightScore)` — called by `NightSession` at `NightEnded`, before the save — keeps each only if beaten and returns which broke (`RecordsBroken`). Best night = the live score the night ended with.
- `ProfileJson.Copy` copies a profile through the format itself (the post-run's "before tonight", taken at load).
- One object per id, so a new field (feats…) is an addition, not a new version. Unknown fields are ignored; missing ones read as 0.
- Read strictly: bad JSON, wrong types, negative / fractional / too-large counts, empty or repeated ids → **Corrupt**. No version, or one this build doesn't know (e.g. a newer build's save) → **UnknownVersion**. Both are handled the same way (below). Older versions would be migrated in `ProfileJson.Read`; there are none yet.
- Parsed with Newtonsoft's `JObject` (no reflection: IL2CPP-safe), shape checked by hand. Meta's asmdef references `Newtonsoft.Json.dll` (package `com.unity.nuget.newtonsoft-json`); CoreCheck uses the same version from NuGet.

**The file (`Simulation/ProfileFile`)** — engine-free (like `ThrowSolver`), so CoreCheck runs it on a real temp folder; `NightSession` just passes `persistentDataPath` and logs.
- **Save**: write `…json.tmp`, flush to disk, copy the current save to `piglings_profile.prev.json`, then `File.Replace` the .tmp over the save (one rename). Never a half-written save. A failed write is logged as an error; the old save is untouched and the game plays on.
- **Load**: the save → if bad, copy it to `piglings_profile.corrupt-<UTC time>.json` and try `.prev` (at most one night lost) → if that's bad too, copy it aside (then remove it) and start fresh. Whatever it ends with is written back at once, so the next launch doesn't trip on the same file. A bad file is never deleted or overwritten without its copy; if the copy fails, saving is switched off for the session. A leftover `.tmp` is ignored.
- **Logs**: one line on load (where from, the counts, the path; a warning with problems + backup paths if anything was wrong) and one on each save.
- **Playtest tools** (`NightSession` context menu, play mode): *Mastery ▸ Reset progress* (empty profile, saved now — the old save is in `.prev`), *Mastery ▸ Add 10 hits* (into tonight's tally, so they bank and save at night end like real hits).

## Campaign progression (M10, derived from causes)

The campaign scene's progression; every rule is engine-free in Meta and checked by CoreCheck. The save stores causes only; all of these are derived.
- **Stones** (`StoneProgression.For(hits)`): start 10; each threshold of the stone's total direct hits reached gives +1, up to the cap (25). **Evolutions** (stage 2): `ThrowableDefinition.levels` in order — each entry's Stones Needed, look (sprite, radius, trail) and hourly Refill; the level = how many entries the stone count reaches (at least 1), the refill that entry's (default 10 → lv1 +1 · 15 → lv2 +2 · 20 → lv3 +3 · 25 → lv4 +4). `StoneStatus` also gives the next evolution (stones, level). `StoneProgression.Problem` warns when Stones Needed doesn't rise.
- **Peg copies** (`PegProgression.For(triggersPerLevel)`): peg mastery = Σ triggers × the level's weight (default weight = the level). An unlocked type owns 1 copy; each mastery threshold adds one, up to `PegDefinition.maxCopies` (10). **Follow-ups**: one per `followUpEveryCopies` (3) copies (`PegStatus.FollowUps`, `NextFollowUpAt`) → `PegType.FollowUps` (campaign only). In a round, placing a peg of such a type starts its chain: one more throw at a time, each the same type (`NightState.PegFollowUp` + `PegFollowUpsLeft`; `IsValidTarget` refuses other types; `PegFollowUpGranted(id, remaining)`), as many as it has — once per round, each only if that type can still be placed. `PegShelf` brings that type to the hand and won't pick or swap to another. Triggers (`MasteryTally`, per peg id and merged level) = each time its effect fires: a Bouncy bonus granted (already once per peg per ball), a Splitter split, a Bomb explosion. Banked as `NightBanked(Peg, id, PegTriggers, n, level)`.
- **Unlocks** (`CampaignPlan`): starting types (Bouncy) + the type a dawn on each night unlocks (N1 → Bomb, N2 → Splitter), from the profile's **dawns per night id**. `TryNextLocked` gives the next locked type and its "dawn on night n". `CanGoNext` = a dawn on this night and a night after it. `TowerFor` = the player's saved slices for a night, or the night's own (a saved tower with a different slice count is ignored).
- **The profile** adds `CurrentNight` (where the player is — navigation, not a result), `StartInNight` (a fast Retry's one-shot, navigation too), `Dawns`, peg `Triggers` per level, `Towers` (slices chosen per night), robots' `Dropped` / `Swept`, the all-time `Records` — written by `Progression` (`RecordNightResult`, `RecordNight`, `SetCurrentNight`, `SetStartInNight`, `SetTower`, and banking). In the save (still v1, additive): `"campaign": {"night", "dawns", "startInNight"}`, `"pegs".{id}.triggers: [l1, l2, …]`, `"towers": {nightId: [sliceIds]}`, `"robots".{type}.dropped` / `.swept`, `"records"`.
- **Separate profiles**: `NightSession.profileName` → `piglings_<name>.json` ("dev" in Night.unity, "campaign" in the v2 scene), so testing in Night.unity never advances the campaign.
- **Campaign mode** (M10.B — `NightSession.campaign` set; Night.unity has none): the profile is loaded first and decides tonight's night (`CampaignDefinition.NightAt(saved index)`, clamped by `CampaignPlan`), its tower (`SlicesForTonight`: the player's saved slices, or the night's own), the stones and refill (`StoneProgression` from the stone's hits) and the shelf (every unlocked type × `CopiesOwned`, from `PegDefinition.copyThresholds` + `masteryWeight` per level). A dawn is recorded at NightEnded (`RecordNightResult`), before the save. Without a campaign the night, pile (`throwsAvailable`), refill (`stonesPerThreshold`) and shelf (`pegLoadout`) come from the Night field, as before.
- **The stone's level** comes from `StoneProgression` in both scenes (the evolution its stone count reaches); `ThrowableDefinition.stoneThresholds` replaced the old "level 2 at N hits". `NightSession.StoneAtStart` / `StoneNow` / `WeaponProgress` (toward the next +1 stone) / `WeaponUpgradeReady` (a stone earned tonight) feed the pile's glints and gold pulse.
- **The built tower** (`TowerBuilder`, Simulation): slices (`WallSliceDefinition`: id, sprite, 7 hold points — the original layout by default —, hold material, an effect slot) stack every `sliceHeight` (1.6) above `firstSliceY` (3.0) under `container` (Barn), a Hold per point; then `towerTop` (one parent for Barn_Top + pig, TopZone + perch + breach slots + stone pile, DangerZone, the peg shelf, ChainPopupAnchor) goes to `firstSliceY + slices × sliceHeight + topOffset`, the side walls' colliders stretch from `wallBottom` to that top + `wallAboveTop`, and `PegBoard.Rebuild()` re-reads the holds (bottom slice first). All positions are absolute, so a rebuild or an edit-mode preview (context menu "Preview tower", objects `DontSave`) always lands the same. The spawn line and GroundZone don't move: the tower grows upward. Built in `NightSession.Awake` before anything counts sockets; the socket count is fixed per night.
- **Night data** (`NightDefinition`): `id` (save key), `slices` (bottom → top), an optional Night camera frame override (`cameraY`, `cameraSize`; 0 = auto, fitted to the tower), thresholds (any length; length = hours). `CampaignDefinition`: nights in order with the peg type a dawn unlocks, the starting types, every slice available. `HourPaletteDefinition`: the night's colours first → last (the last = dawn), sampled over each night's hours (`ColourAt`, `NightSession.HourColour` — see "Hour colours" above); `NightSession` warns (load + `OnValidate`) when a night's hours + dawn need more colours than it has (8 = 7 hours + dawn).
- **Hour colours** (M10.E): `HourPaletteDefinition.ColourAt(hour, hours)` spreads the palette over the night's hours **and its dawn** — hour 1 = the first colour, **dawn = the last** (`DawnColour`, the gold), the hours between blending along the list (`PaletteSampling`, engine-free, CoreCheck; post_run_v5: 6 hours on 7 colours = one each + gold for dawn). `NightSession.HourColour(hour)` / `DawnColour` use tonight's hour count, so the scoreboard (its DAWN too), the HOURS card and the post-run all follow it.

## Campaign flow and camera (M10.C, campaign scene only)

```
Boot (Doors → Barn room) → BarnRoom ⇄ Tower → Rising (→ Night frame) → Night → Settling → PostRun (over the Night frame)
  → To the barn / Next night: Descending (→ Doors, they open) → Leaving (save + reload → boots at the Doors)
  → Retry: Leaving (save + reload → boots at the Night frame, straight into the night)
```
- **`NightFlow`** (Simulation) owns the flow state (`FlowState`); one machine, transitions only via `SetState` → `EnterState`. Restart = reload: every post-run button saves where the player goes and reloads by build index. **To the barn / Next night** (`NightSession.GoToNight(index)`: `Progression.SetCurrentNight` + save + reload) first ease the camera Night → Doors (the doors open on arrival): the reloaded scene boots at the Doors frame — the same view, so the cut is invisible — and eases into the barn room; the night waits in **Dusk** (`NightSession` Start Immediately **off**) until the camera reaches the Night frame, then `BeginNight()`. **Retry** is fast (M10.E, Yam): `NightSession.RetryNight` saves a one-shot `startInNight` flag; the reloaded `NightSession` clears it (and saves) at load and says `StartsInNight`, and `NightFlow` snaps to the Night frame — where the post-run was — and begins the night at once, no barn room. A scene whose night is already running (Start Immediately on) skips straight to Night, with a warning.
- **One continuous move (M10.S)**: To the barn / Next night is one move across the reload — the descent eases **in** (speeding up into the Doors, `CameraEase.In`) and the reloaded boot eases **out** from the Doors into the barn room (`CameraEase.Out`), both timed by `transitSpeed` (`CameraFraming.SecondsAtPeakSpeed`: T = 2·distance / speed), so they meet at the same speed; the doors open on arrival. A load hitch of a frame or two can show at the seam. **Start night**: the doors close first (`doorsCloseSeconds`), then the rise. `NightFlow.Daylight` (0..1) drives the background in the campaign (`DayNightBackground` with a flow): day in the barn, night from the rise; the rise crossfades day → night, the descent night → day; hooks `MoonRises` / `MoonSets`. Builds on `BarnDoorsView` (two SpriteRenderers on BarnBottom, closed / open, from `DoorsOpen`).
- **After the night**: `NightEnded` → Settling. The sweep was counted inside the end. Out of stones (M11.T1): the wall holds still for **Loss Beat Seconds**, then `WolfClimbStarts(wolfSeconds)` (`WolfChimneyPlaceholder` moves any sprite to the chimney meanwhile), then `spawner.LetGoSwept()` — the robots fall (the wolf switched the remote off). Dawn: they fell at once. Both outcomes then wait until `NightSession.WallSettled` (Rules `SettleTracker`: no robot off the wall — knocked loose, swept or breaching — and no chain open) has held for a beat (a safety timeout logs and goes anyway) → **PostRun, over the Night frame: the camera doesn't move** until a button.
- **Input**: the camera moves only between phases and on a post-run button. Gameplay input is already off then (Dusk / Ended can't throw); the flow refuses its own actions while the camera moves (`CanStartNight`, `CanChoose`… false). `NightFlow.NextEarned` = `CampaignPlan.CanGoNext` (a dawn on this night and a night after it); `Primary` / `Secondary` = `PostRunChoices` (engine-free, CoreCheck): a loss → Retry + To the barn; a dawn → Next night + Retry, or To the barn + Retry after the last night — To the barn and Next night are never shown together.
- **`CameraDirector`** (Simulation, on the camera, order -800): named frames (`CameraFrame`): **Night** fitted to the built tower (fixed bottom `nightBottomY`, top = `TowerBuilder.TopY` + the roof `towerAboveTop`; tonight's `cameraY` / `cameraSize` above 0 override, each on its own), **Doors** and **Barn room** (Inspector), **Tower** fitted to the built tower (`TowerBuilder.TopY`: ground → top + Barn_Top, never narrower than the barn). `SnapTo` / `MoveTo(frame, seconds)` with an ease in-out from wherever it is; `IsMoving`, `Target`, `Progress`. x never changes. Alone (no NightFlow) it starts on the night's frame. Maths engine-free in `CameraFraming` (CoreCheck). Order with `CameraShake`: shake restores (-900) → director sets the pose (-800) → gameplay reads the camera (0) → shake on top (LateUpdate).
- **Hooks for views**: `NightFlow.DoorsOpen` (open from the camera's arrival at the doors until the night begins; `BarnDoorsView` swaps `barn_bottom_closed` / `barn_bottom_open`), `NightFlow.MoonRises(seconds)` (the moon-rise hook — no moon art yet), `NightFlow.WolfClimbStarts(seconds)` (`WolfChimneyPlaceholder`), `NightFlow.State`.
- **Buttons**: `FlowButtons` (Simulation — input) shows the barn room's Start night / Tower / Back only in their state, interactable when the flow allows; `PostRunButtons` (Simulation) is the post-run's primary and secondary slot — each press does `NightFlow.Choose(Primary / Secondary)`; the labels are `PostRunView`'s. Clicks wired in code.
- **Post-run log**: entering PostRun logs the night, its result and reason, the dawns saved on it, the two buttons and why Next night is or isn't offered.

## The post-run screen (M10.E, campaign scene)

Over the Night frame, after the sweep has landed (and on a loss, the wolf's time). Screen-space UI laid out once by Yam; the code fills it.
- **`PostRunView`** (Presentation): fades the whole screen in on PostRun (a CanvasGroup) with a dim overlay (`dimAlpha`), dims the live night HUD (`nightHud` CanvasGroups to `hudAlpha`, M10.S), fills the panels once — tonight is banked by then — sets the two buttons' labels from `NightFlow.Primary` / `Secondary`, and fades out when a button sends the camera down.
- Mockup: `Docs/Mockups/post_run_v5.png`. Numbers with thousands separators (`Numbers.Thousands`, invariant).
- **THE NIGHT** (`PostRunNightPanel`): "NIGHT n", DAWN (`DawnColour`) / OUT OF STONES, exactly one hour dot per hour + one for dawn (`HourDots`, engine-free, CoreCheck: hours in their colour, dawn in `DawnColour` only on a dawn, the rest dimmed; old clones cleared before a fill; Hour Dot and Hour Row must be different templates), "6 / 6" (hours reached / hours), the score ("kept X (hour n)" on a loss — n = the last hour finished), BEST THROW EACH HOUR (`HourStats.BestThrow`: "Hn" in the hour colour, a bar against the night's best, the night's best in animated rainbow — `ScoreStyles.Rainbow`), the biggest chain, avg per stone (the chains' score without the dawn sweep ÷ stones thrown), stones thrown · stolen ("26 · 2").
- **ALL-TIME RECORDS** (`PostRunRecordsPanel`): best throw, longest chain, deepest chain, best night score; each row label · bar · value (M10.S): the bar = how close tonight came (`RecordsReport.Tonight` ÷ the record, `RecordsReport.Closeness`); one broken tonight → bar full + gold, value gold, its small NEW tag.
- **PROGRESS** (`PostRunProgressPanel`) — only what moved, from **`NightSession.BuildPostRunReport()`** → Meta's **`PostRunProgress.Build(before, after, …)`** (engine-free, CoreCheck): `before` = `NightSession.ProfileBeforeTonight` (a `ProfileJson.Copy` taken at load), `after` = the banked profile. The stone (always): its icon at its level, +hits, the bar to the next +1 stone, a → b stones, the evolution it was heading for when the night began ("next: 15 · evolve"), the evolution track (`ThrowableDefinition.Levels`: Stones Needed + sprite + the line into it; reached full, others faded) with an optional bar along it (`evolutionBar`: `StoneProgression.TrackPosition` — slots evenly spaced, stones between them — before tonight dim, now bright, READY when tonight reached a new evolution). A peg row only for a type whose saved triggers changed tonight or that tonight unlocked (`CampaignPlan.UnlockNightOf` → "unlocked by dawn on night n", NEW badge, no copies, no bar): copies "5 → 6", the bar to the next copy, the next follow-up step (`PegStatus.NextFollowUpAt` → "6 = 3 in a row"). Wolves dropped (always): "+141", "1,204 all time" (`PlayerProfile.TotalDropped`).
- **Bars** (`ProgressBarView`): a dim fill for before tonight + a bright gain that eases in; READY when tonight completed it. `ProgressBar` (Meta) is the span the player was working on when the night began: before = its progress then, after = its progress now (full, READY, when tonight earned the stone / copy); it never runs backwards.
- **Rows**: `TemplateSlot` (label, detail, note, marker, bar, badge) cloned by `TemplateList`, like the scoreboard.
- The post-run plays no upgrades: the barn room will (PROTOTYPE_V2 ▸ PR F follow-up).
- The `[Night]` tuning line is `NightLog`, a plain class owned by `NightSession`, so every scene logs it (it used to live in `NightEndView`, which the campaign scene doesn't have). Night.unity keeps `NightEndView` (OnGUI end screen) and `PlayAgain`; both **stand down in a campaign scene** (`NightSession.IsCampaign`) — left in TestNight, PlayAgain reloaded on the *press* of Next night, before the button's click (on release) could save the next night. Retry / Next log when pressed (or why they were refused).

## The day phase: the barn room and slice placement (M10.F, campaign scene)

- **Slice placement** (the Tower frame), by hand — `SlicePicker` (Simulation: the input, and it moves the slices): the hovered slice **jiggles**; **drag** a tower slice onto another slot → they trade places (`NightSession.SwapSlices`); drag a slice from the **tray** beside the tower (`NightSession.SlicesToChoose()`: the campaign's slices with tonight's hold count — the night's sockets were counted at load) onto a slot → it replaces that slot (`NightSession.SetSlice`); a drop anywhere else slides back. The tower is **always whole** (`SliceDrag.Resolve`, engine-free, CoreCheck) — no gap to fill. Only in **Dusk**, never through a UI button, never while the camera moves. A change rebuilds the tower (`TowerBuilder.Build` → `PegBoard.Rebuild` re-binds the Holds → `PegBoard.Rebuilt` → `PegBoardView` re-creates its overlays) and saves the night's tower (`Progression.SetTower`). The jiggle and the drag move the real slice looks (`TowerBuilder.SliceLook`, holds included — nothing physical runs before the night); a dragged slice is lifted with a SortingGroup; leaving placement puts every look back exactly (`TowerBuilder.ResetSlice`).
- **The barn room** (the BarnRoom frame): `BarnInteraction` (Simulation — input; the hit area is the materials pile's sprite bounds, no colliders) — hovering the pile leans the camera toward it (`CameraDirector.Nudge`: a small eased pan + zoom on top of the resting frame, dropped by any move) and raises `HoverStarted` (the sound hook); a click opens the Tower (`NightFlow.OpenTower`). Views: `MaterialsPileView` (idle faint glow; hover: lift, pop, full glow, sparkles), `BarnPegboardView` (a group of slots per type of `NightSession.CampaignPegTypes()`; v2: 8 slots in two rows of 4, row 2 only past 4 copies; holes from a holes file — "x,y" pixels from the PNG's top-left — converted with the sprite's pivot and PPU; an optional hole sprite drawn per visible slot; owned copies hang on their slots, a locked type shows its first row as ghosts + icon_lock), `RugStonesView` (tonight's stones in the pile's pyramid at the stone's level and size + one stone apart with its refill badge — a list, entry 0 = +1), `NightSignView` ("NIGHT n · H HOURS", day states only). No text on world objects in the barn.

## The night's look: scoreboard, popups, tutorial cards (M10.D, both scenes)

- **Score colours** (`ScoreColoursDefinition`, Definitions; one asset on `NightSession` ▸ Score Colours, built-in defaults when empty — `NightSession.ScoreColours`): a colour per **depth** (0 cream, 1 amber, 2 orange, 3 red, 4+ magenta) and **throw-quality bands** (quality = points ÷ the gap of the hour the throw was thrown in, `NightGoal.ThrowQuality`, exposed as `NightSession.ThrowQuality`): < 5% cream, 5–15% amber, 15–30% orange, 30–50% red, 50–100% magenta pulsing, ≥ 100% animated rainbow. The bands are a view choice; the quality is the Rules'. Every view that colours a score reads this one asset.
- Presentation turns it into popup styles with `ScoreStyles` (plain class, cached `PopupColor`s: solid, Cycle = pulse, PerLetter = rainbow); `TextColouring.Apply` paints a TMP text with a style (whole text or per letter) — shared by `ScorePopup` and the board.
- **Score colours (M10.S)**: every score look — robot popups, the chain-close popup, the board's throw rows and BEST — is `ScoreStyles.ForQuality`: the Score Colours asset's quality bands by the throw's quality (its worth ÷ its hour's gap; a live chain by its worth so far, so its popups warm up as it grows). *(An hour-palette base was tried in S2 and dropped: it flattened every solid band into one colour.)*
- **Popups** (`ScorePopupSpawner`, M10.S): a robot knocked loose shows ONE number, the chain so far — `789 ×5` (its SCORE × MULT at that moment; no hour, no result). A special peg triggering: a small `+1 mult` at the peg; a plain hold: a small `+1` at the hold, coloured by its hitter's depth with the quality bands' looks (`ScoreStyles.ForDepthBand`: band i for depth i, so the deepest pulse and the last is the rainbow; `ChainGained.Depth`). The stone's base: none. At the close, above the pig (`ChainPopupAnchor`, on by default): `120 ×3 ×H1 = 360` — the only place the result shows on the board (with the camera shake); it fits the spawner's Chain Area box (the font shrinks, `ScorePopup.FitInto`; the box is drawn as a gizmo). "Preview popups" shows every band.
- **UI texts (M11)**: every text the code writes into the UI (scoreboard, score popups, post-run, night sign) is a template on one `UiTextsDefinition` asset (`NightSession` ▸ Ui Texts, read as `session.Texts`; unassigned = built-in defaults, the old texts). `{name}` tokens are filled by `UiText.Fill` (Presentation, stateless; numbers arrive formatted — `Numbers.Thousands` / `Numbers.Mult`); TMP rich text works inside. The post-run's button labels stay on `PostRunView`.
- **The board's live throw rows (M10.S)**: `NightScoreboard`'s LAST THROWS are the chains as they happen — one row per chain, cloned from the throw row and placed by code (newest on top; older rows step down, shrink and dim; Row Brightness's length show). Live: `SCORE × MULT × H2` ticking with each `ChainGained`; at `ChainScored` the label stays and the result appears next to it (`+360`); a miss's row goes. Rows glide in `rowGlideSeconds`. BEST TONIGHT: the best throw's breakdown `84 × 3 × H2 =` and its result. The score NUMBER, the hour BAR and the gap line change only when a chain closes (or the night ends — the dawn sweep), together with the chain popup (M11.T6): all three read one board score (the real score at the last close), the number climbing (`scoreClimbSeconds`), the bar gliding (`barEaseSeconds`). DAWN on the board likewise waits for the close. The Rules' raw live score (thresholds, the log) is unchanged.
- **Scoreboard** (`NightScoreboard`, a world-space Canvas on a post, left of the tower): `HOUR n · ×m` in the hour colour (`DAWN` after the last threshold); score / next threshold and a fill bar through the hour the score is climbing (the one after the last threshold crossed — `State.Hour` only moves on at the round), in that hour's colour; `hour n gap: a → b`; LAST THROWS (the last 3 closed chains with wolves, newest on top and brightest via a CanvasGroup, `9 wolves · depth 2  +620`, depth only when ≥ 1, misses not listed; points + marker quality-coloured); BEST TONIGHT (same rule as `NightState.BestThrowPoints`). Texts rebuild only on change; animated colours repaint every frame. The bar glides (`barEaseSeconds`, SmoothDamp); at a threshold it fills up first, then starts the new hour from empty in its colour — never runs backwards.
- **Tutorial cards** (shown always for now): `DepthCardView` (a wolf row per depth with an arrow between, `×m` from `NightSession.DepthMultiplier` — since M10.S the chain's mult at that depth, 1 + depth — in the depth's colour) and `HoursCardView` (moon → one dot per hour of tonight in the palette colour with `×m` from `NightSession.HourMultiplierAt`, a peg marker between hours → sun). Numbers come from the scoring / night, never typed in.
- **Templates**: Yam lays out one row / dot (`TemplateSlot`: label, detail, note, marker, bar, badge) and the separators; `TemplateList.Build` clones them in order where the template sits (positions from a Layout Group on the parent), hiding the templates; `TemplateList.Clear` destroys earlier clones (a view that fills twice). Every field optional. One template must never serve two lists (the post-run's hour-dots bug: the dots were cloned again as rows).
- **Debug HUD** (`ChainDebugHUD`): hidden by default, F1 toggles (Inspector key). Presentation reads that key directly: it only changes what this view draws.

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

## Debug panel (M11.T2, developer only)
- `DebugPanel` (Simulation, IMGUI, F1): off unless `Debug.isDebugBuild` (editor, Development builds). While open it sets `NightSession.GameplayInputBlocked` (ThrowController, PegThrower and SlicePicker stand down) and, optionally, pauses with `Time.timeScale` (restored on close / disable).
- Save edits go through `NightSession.DebugEditSave(edit, startInNight, why)` → save → reload; the edits are `Meta/ProfileEdits` (engine-free, CoreCheck): a result is written as its smallest cause (stones → hits, copies → level-1 triggers, nights won → dawns), so nothing derived is ever stored.
- Board edit: `NightReferee.SetSocket(socket, pegId, level)` (any phase but Ended; only tonight's types; no shelf / throw / follow-up) publishes `PegSocketSet`; `PegBoardView` redraws and `PegBoard` re-applies the hold's material. Not saved.
- Scenarios: `DebugScenarioDefinition` assets; `NightSession.DebugLoadScenario` resets the profile, applies the scenario, saves, reloads.

## Balance log (M11.T3)
- `Rules/BalanceTally` (engine-free, the caller's clock): gathers each closed chain (`ChainRecord`: score, mult, hour mult, total, wolves, depth, wall density at the throw, stones left) and per-hour sums (`HourRecord`, incl. miss share); CoreCheck.
- `Simulation/BalanceCsv` (engine-free, in CoreCheck): one fixed column set for `chain` / `hour` / `night` rows; invariant numbers; quoting.
- `Simulation/BalanceLog` (owned by NightSession, `Write Balance Log`): appends a night's rows on `NightEnded` to `persistentDataPath/BalanceLogs/balance_<profile>.csv`; never throws.
- `PlayerProfile.Origin` (save: `"debug": { "origin" }`, written only when set): the label every row carries — set by the debug panel (scenario / edit), "" = normal flow. Not a cause; nothing in the game reads it.
- `BalanceKnobsDefinition` (NightSession ▸ Balance Knobs): `ClimbSpeedMultiplier`, `SpawnRateMultiplier`, applied by `RobotSpawner` on top of the night and its wall.
