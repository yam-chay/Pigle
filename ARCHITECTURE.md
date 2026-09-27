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

- Engine-free layers compile outside Unity (`Tools/CoreCheck`). That is what makes cloud sessions useful: they can build and verify everything below Simulation without an editor.
- Runtime state refers to things by `EntityId` / string id — never to a ScriptableObject or GameObject. (This is also what lets saves store causes and derive results later.)
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
- A climbing robot crosses the danger line → `RobotEnteredDangerZone(robot)`, once per robot (published by `DangerZone`). In Running a warning only; in Overtime it ends the night (see below). No robot state changes.
- A climbing robot reaches the roof → `RobotRemoved(EnteredBarn)`: a breach, counted by `NightReferee`.
- A falling ball that never lands (resting on a hold) is removed after `RobotDefinition.MaxFallSeconds` → `RobotRemoved(TimedOut)`, so its chain can close.

## Night phases (`NightReferee`, GDD "סוף הלילה: להמשיך או ללכת")

```
Running ──score ≥ target, no chain open──▶ ChoicePending ──Stay──▶ Overtime ──▶ Ended
   │                                            └──────Leave──────────────────▶ Ended
   └──out of stones / too many breaches (lost)────────────────────────────────▶ Ended
```
- One state machine; `NightState.Phase` is the source of truth; transitions only via `SetPhase` → `EnterPhase`. Each change publishes `NightPhaseChanged(from, to)`.
- Reaching the target ends the danger, not the night: from then on the night can't be lost (`NightTargetReached` marks the moment; throwing stops until the choice).
- **ChoicePending**: the wall pauses — `NightSession.WallMoving` is false, so robots stop climbing and the spawner stops. No `Time.timeScale`. `NightChoice` (Simulation) calls `NightSession.ChooseStay/ChooseLeave` → `NightChoiceMade`.
- **Overtime**: wall and throwing resume. **Every chain point is doubled as it's scored** — the ×2 lives in one place, `ScoreCurve.RobotTotal` (the same function that applies depth), with `ScoringDefinition.overtimeMultiplier`; `ChainTracker` passes it only while `Phase == Overtime`. It's applied after rounding (exactly 2× the Running value) and not to what a robot carries on (never compounds). HUD score and "+N" popups (tagged `OT×2`) show the doubled points; barn mastery banks them as scored, with no further multiplier. The weapon earns nothing. Ends when the stones run out (after the last chain) or at the danger line — but only for robots that **spawned during overtime** (entering the line, or reaching the top). Robots already on the wall at Stay can't end it (they'd end it instantly, because the wall resumes exactly as it froze); they still pay double if knocked off, and reaching the top costs nothing. Unthrown stones are lost. `ChainScored.InOvertime` lets views style overtime chains (rainbow chain popup).
- **Ended**: while handling `NightPhaseChanged(→Ended)` (the bus is synchronous), `RobotSpawner` sweeps the wall: every climbing robot calls `Sweep()` → `RobotSwept`, scored flat by the referee with `ScoreCurve.RobotValue()` — the same robot-value function chains use — and ×1 on both paths (not earned by throwing). Then the referee publishes `NightBanked(Barn|Weapon, amount, multiplier)` per destination and `NightEnded` once.
- Mastery is **only published** (`NightBanked`), never stored across nights: Meta subscribes later.

Events are facts, never commands. Nobody "asks" through the bus.

## Robot state machine (ported from CCTD SpiderController)

```
Spawned -> Climbing -> LosingGrip -> Falling -> Removed(HitGround | TimedOut)
              \-> ReachedTop -> Breaching -> Removed(EnteredBarn)        ← Breaching is PLANNED (M7.3), not in code yet
```
- `RobotController.EnterState` is the ONLY place a state becomes physics (body type, layer).
- `LosingGrip` = the break clip (flail → crack → limbs collapse). Still kinematic. Duration from `RobotDefinition.BreakDuration`.
- `Falling` = dynamic ball on layer `RobotBall`, bounces through the Holds. Only a Falling robot passes a chain on. A ball still falling after `RobotDefinition.MaxFallSeconds` is removed (`TimedOut`) so it can't hold a chain open.
- `Breaching` *(planned, M7.3)* = the breach sequence after `ReachedTop`: collider off, the robot stays alive for its animation, then jumps off the porch and falls. It **never starts or joins a chain** (it isn't `Falling` and has no collider) and **never holds a chain or the night open**: a hard time limit ends it in `Removed(EnteredBarn)` whatever the animation does — same class of bug as the stuck ball. Today `ReachTop` removes the robot at once (it vanishes). The breach is counted when it's removed, as now; 5 breaches still lose the night.

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
| `BarnTopZone` | the breach line, just under the pig's hole (y ≈ 7.2) | Marker. A climbing robot touching it calls `ReachTop` → `RobotRemoved(EnteredBarn)` = breach. Its children are art only (perch pieces + the hanging "no wolves" sign): no colliders. |
| `DangerZone` | just above the wall's top beam (y ≈ 6.275) | Has `NightSession` (serialized). A climbing robot entering it → `RobotEnteredDangerZone`, once per robot, all night. `RobotView` sets the Animator bool `InDanger`. No robot state. In Overtime it's the line that ends the night; `DangerZoneView` shows it differently then. |
| `GroundZone` | below the barn | Marker. Falling balls and throwables touching it are removed. |

## Ammo on the perch (PLANNED — M7.1 / M7.2)

Ammo is the pig's life, shown as a physical container on the perch (stones: a pile) that visibly depletes: each throw, one item hops from the container to the pig's hand.
- **One reading rule for every weapon's container:** many left = how full the container is; ≤ 5 left = individual items + a small pulsing number; 0 = empty.
- **Structure:** an ammo view in **Presentation** reads the count from **Runtime** (today `NightState.StonesLeft`) and its look (container sprite states, item sprite) from the throwable's **Definition** (`ThrowableDefinition` — these visual fields don't exist yet). Nothing is hard-coded to stones: a new weapon is new art, not new code. Reads only.
- **Coming:** mixed weapon types per night → the perch is the loadout, one container per weapon; that needs a per-weapon count in Runtime (today there's one number). In the day phase the perch shows only owned weapons and the player picks a limited number (size TBD). Weapon mastery will show on its container — Meta is still empty, don't build it.

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

Piglings.Editor | (none) | editor-only tooling; no game assembly may reference it
