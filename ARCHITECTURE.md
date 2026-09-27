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
- **Overtime**: wall and throwing resume. Overtime score banks ×2 to the barn; the weapon earns nothing. Ends when the stones run out (after the last chain) or at the danger line (a robot entering it, or one already past it reaching the top). Unthrown stones are lost.
- **Ended**: while handling `NightPhaseChanged(→Ended)` (the bus is synchronous), `RobotSpawner` sweeps the wall: every climbing robot calls `Sweep()` → `RobotSwept`, scored flat by the referee with `ScoreCurve.RobotValue()` — the same robot-value function chains use. Then the referee publishes `NightBanked(Barn|Weapon, amount, multiplier)` per destination and `NightEnded` once.
- Mastery is **only published** (`NightBanked`), never stored across nights: Meta subscribes later.

Events are facts, never commands. Nobody "asks" through the bus.

## Robot state machine (ported from CCTD SpiderController)

```
Spawned -> Climbing -> LosingGrip -> Falling -> Removed(HitGround)
              \-> ReachedTop -> Removed(EnteredBarn)
```
- `RobotController.EnterState` is the ONLY place a state becomes physics (body type, layer).
- `LosingGrip` = the break clip (flail → crack → limbs collapse). Still kinematic. Duration from `RobotDefinition.BreakDuration`.
- `Falling` = dynamic ball on layer `RobotBall`, bounces through the Holds. Only a Falling robot passes a chain on.

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
| `BarnTopZone` | roof line | Marker. A climbing robot touching it calls `ReachTop` → `RobotRemoved(EnteredBarn)` = breach. |
| `DangerZone` | a little below `BarnTopZone` | Has `NightSession` (serialized). A climbing robot entering it → `RobotEnteredDangerZone`, once per robot, all night. `RobotView` sets the Animator bool `InDanger`. No robot state. In Overtime it's the line that ends the night; `DangerZoneView` shows it differently then. |
| `GroundZone` | below the barn | Marker. Falling balls and throwables touching it are removed. |

## Scale convention

**1 art pixel (design px) = 0.01 world units.**
| Asset | PNG resolution | Pixels Per Unit | Root scale |
|---|---|---|---|
| Barn pieces | 4× | 400 | 1 |
| Pig parts | 2× | 200 | 0.26 |
| Wolf-bot parts | 3× | 300 | 0.3 |
Barn = 6 units wide. Wolf-bot ball radius = 58 px × 0.3 × 0.01 = 0.174.

Piglings.Editor | (none) | editor-only tooling; no game assembly may reference it
