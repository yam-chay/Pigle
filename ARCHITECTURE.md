# Piglings — Architecture

> Wins on **structure**. The GDD (Claude Docs) wins on **design**. CLAUDE.md is how to work here. TASKS.md is what's next.

## Layers

```
Definitions   ScriptableObjects. Immutable design data (robots, throwables, wall materials, nights).
Runtime       Plain C# state (NightState). No behaviour, no engine types. Serializable.
Events        The backbone: EventBus + immutable event structs carrying ChainId + Attribution.
Rules         Plain C#. Subscribes to events, writes Runtime state, publishes results (ChainTracker).
Meta          Plain C#. Progression / currencies / grant pipeline. EMPTY until after the prototype.
Simulation    Unity. Physics bodies and their state machines (RobotController, Throwable, spawner, zones).
Presentation  Unity. Views, rigs, animation, HUD. Reads state and reacts to events. Never writes state.
```

### Dependency rules (enforced by .asmdef references)

| Assembly | May reference | Engine? |
|---|---|---|
| Piglings.Events | — | no (`noEngineReferences`) |
| Piglings.Runtime | Events | no |
| Piglings.Rules | Events, Runtime | no |
| Piglings.Meta | Events, Runtime | no |
| Piglings.Definitions | — | yes |
| Piglings.Simulation | Events, Runtime, Rules, Definitions | yes |
| Piglings.Presentation | Events, Runtime, Definitions, Simulation | yes |
| Piglings.Editor | — (editor-only tooling) | yes, Editor platform only |

- No game assembly may reference `Piglings.Editor`.

- Engine-free layers compile outside Unity (`Tools/CoreCheck`). That is what makes cloud sessions useful: they can build and verify everything below Simulation without an editor.
- Runtime state refers to things by `GameId` / string id — never to a ScriptableObject or GameObject. (This is also what lets saves store causes and derive results later.)
- Presentation never calls a method that changes gameplay state. If a view needs something to happen, that's a Simulation feature.

## Composition — no bootstrap

There is no bootstrap scene, no bootstrap assembly, no service locator, no singletons, no `DontDestroyOnLoad`.

Each playable scene has exactly one **scene-scoped owner**. For the prototype that's `NightSession` (Simulation):
- `Awake` (execution order -1000) creates `EventBus`, `NightState`, `IdAllocator` and the Rules (`ChainTracker`, then `NightReferee`).
- Every scene object that needs them gets the `NightSession` through a **serialized field** in the Inspector. No `FindObjectOfType`, no statics.
- When the scene unloads, everything goes with it.

> ⚠️ Decision to confirm (Yam): this owner is the thing that replaces the old AppServices/SceneBootstrap. It is scene-local and lives in Simulation, not a separate layer. When Day/Meta arrives, the persistent run data will need a home that survives scene changes — decide that then, not now.

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
- One state machine; `NightState.Phase` is the source of truth; transitions only via `SetPhase` → `EnterPhase`. Each change publishes `NightPhaseChanged(from, to)`. The hour isn't a phase: it's `NightState.ThresholdsReached + 1`.
- **Thresholds** (`NightDefinition.thresholds`, e.g. 5 → five hours; `Rules.NightGoal` keeps them ≥ 1 and strictly rising). Crossing one (not the last) → `HourReached(hour, multiplier, threshold)`. Throwing stops **and the wall freezes** at once (`NightState.WallMoving`: Running, no round pending, not dawn — climbers and the spawner stop; falling balls are physics and keep falling); the placement round starts when every open chain has settled, so a chain is never cut and never straddles two hours. One chain crossing several thresholds earns one round each, played back to back (`PegPlacement → PegPlacement`).
- **Hour multiplier**: each hour adds `ScoringDefinition.hourMultiplierStep` (0.5 → ×1, ×1.5, ×2…). It lives in one place, `ScoreCurve.RobotTotal` (with depth): received × depth multiplier × hour multiplier, rounded once. Fixed when the stone is thrown (`ChainTracker` records it per chain); applies to what a robot scores, never to what it carries on, so it never compounds. `RobotScored` / `ChainScored` carry the hour for views.
- **PegPlacement**: the wall pauses — `NightSession.WallMoving` is false, so robots stop climbing and the spawner stops (no `Time.timeScale`; breaching robots finish their sequence). The pile gets `NightDefinition.stonesPerThreshold` (`StonesChanged(Added)`), and the player gets `pegThrowsPerThreshold` pegs to place: `NightSession.PlacePeg(socket, pegId)` → `NightReferee.PlacePeg` → `PegPlaced` (empty socket, level 1) or `PegMerged` (same mergeable type, +1 level up to `PegDefinition.maxLevel`). A peg throw costs no stone, isn't a `ThrowReleased` and never starts a chain. The round ends when the throws are used, or — when nothing can be placed (`CanPlaceAnyPeg` false) — `NightSession` ends it after `NightDefinition.refillPauseSeconds` (Rules can't keep time). Unused throws are lost.
- **Dawn**: crossing the last threshold → `DawnReached`. The night can't be lost from there; it ends (`Ended`, `Dawn`) once every chain has settled. No placement round, so no refill, at dawn.
- **Caught**: a breach on an empty pile, in any hour before dawn (a threshold that isn't dawn protects nothing) → `Ended`, `Caught`.
- **Ended**: while handling `NightPhaseChanged(→Ended)` (the bus is synchronous), `RobotSpawner` sweeps the wall: every climbing robot calls `Sweep()` → `RobotSwept`. On both outcomes the robots fall. Won: each is scored flat by the referee with `ScoreCurve.RobotValue()` (the same function chains use), ×1. Lost: visual only. Then `NightBanked(Barn, BankedScore)` and `NightEnded` once. **BankedScore** = the live score at dawn (sweep included); the last threshold reached when caught (`NightGoal.ScoreAtThreshold`) — 0 before the first.
- Mastery is **only published** (`NightBanked`), never stored across nights: Meta subscribes later. Nothing banks to the weapon any more (leftover stones used to); weapon mastery will come from use + feats (Meta).
- *Replaced:* the M6.5 end-of-night choice (Stay / Leave, `ChoicePending`, `Overtime` and its ×2, the danger line ending overtime, forfeited stones, leftover stones → weapon mastery). See TASKS.md history.

**The pegs (Runtime, written only by `NightReferee`).** `NightState.Sockets` = one `PegSocket` per Hold (peg id + level, by string, never a ScriptableObject); its length is also the most pegs the wall can hold. `NightState.Shelf` = one `PegStack` (peg id + count) per type, in loadout order, at most `PegSetup.ShelfCapacity` (5) types. `PegThrowsLeft` = this round. Definitions: `PegDefinition` (id, maxLevel, mergeable); `NightDefinition.pegLoadout` is the shelf until the day phase exists. `NightSession` turns both into the Rules' plain `PegType` / `PegSetup`. Until the placement PR adds sockets, the wall has none, so every round is a refill pause.

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

**Coming:** mixed weapon types per night → the perch is the loadout, one container per weapon (needs a per-weapon count in Runtime; today there's one). The rag under the stones is the first container tier (rag → bindle → sack → dedicated tool); weapon mastery will show on it. Meta is still empty — don't build it.

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
