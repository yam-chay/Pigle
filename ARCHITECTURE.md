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
- A climbing robot crosses the danger line → `RobotEnteredDangerZone(robot)`, once per robot (published by `DangerZone`). In Running a warning only; in Overtime it ends the night (see below). No robot state changes.
- A climbing robot reaches the roof → it enters `Breaching` and publishes `RobotBreached(robot)`: **that is the breach**, counted by `NightReferee` at that moment. With stones left it takes the top stone (`StonesChanged(Stolen, robot)`); with none left the pigs are caught — the only way to lose. When the breach sequence ends, `RobotRemoved(EnteredBarn)` is cleanup only and never counts again.
- Every change to the stone count → `StonesChanged(count, delta, cause, robot)`, published only by `NightReferee` (Thrown / Stolen / Added / Forfeited).
- A falling ball that never lands (resting on a hold) is removed after `RobotDefinition.MaxFallSeconds` → `RobotRemoved(TimedOut)`, so its chain can close.

## Night phases (`NightReferee`, GDD "סוף הלילה: להמשיך או ללכת")

```
Running ──score ≥ target, no chain open──▶ ChoicePending ──Stay──▶ Overtime ──▶ Ended
   │                                            └──────Leave──────────────────▶ Ended
   └──a robot breaches with no stones left (Caught, lost)───────────────────────▶ Ended
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
              \-> Breaching -> Removed(EnteredBarn)
                   (ReachTop: the breach counts HERE)   (cleanup only)
```
- `RobotController.EnterState` is the ONLY place a state becomes physics (body type, layer).
- `LosingGrip` = the break clip (flail → crack → limbs collapse). Still kinematic. Duration from `RobotDefinition.BreakDuration`.
- `Falling` = dynamic ball on layer `RobotBall`, bounces through the Holds. Only a Falling robot passes a chain on. A ball still falling after `RobotDefinition.MaxFallSeconds` is removed (`TimedOut`) so it can't hold a chain open.
- `Breaching` (M7.3) = the breach sequence. `ReachTop` (a climbing robot touching `BarnTopZone`) enters it, then publishes `RobotBreached`. **The breach counts on entering `Breaching`**: `StonesChanged(Stolen)` or `Caught` happen at that moment, and the stolen stone moves onto the robot then. Why at the start: the outcome is fixed when the player sees the robot arrive; a chain that reaches the target during the sequence can't undo a theft, and a pile that's empty by the time the robot leaves must not count as a second breach. State before event: if the breach ends the night, the sweep (inside the publish) already sees the robot as not climbing.
  - `EnterState(Breaching)`: collider off, kinematic, still. It **never starts or joins a chain** (not `Falling`, no collider, touches no zone).
  - Hard time limit `RobotDefinition.BreachSeconds` (tune in the asset) → `Removed(EnteredBarn)`, whatever the animation does — same class of bug as the stuck ball. That removal is **cleanup only**: `NightReferee` doesn't listen to it.
  - Not paused by ChoicePending (it isn't climbing — it finishes its sequence; `RobotView` doesn't freeze its Animator). Skipped by the Ended sweep (`Sweep` only takes climbing robots); its time limit removes it.
  - The jump off the porch and the fall are animation (🖥), not physics, for now.

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
| `BarnTopZone` | the breach line, just under the pig's hole (y ≈ 7.2) | Marker. A climbing robot touching it calls `ReachTop` → `Breaching` + `RobotBreached` = the breach, counted at that moment. `RobotRemoved(EnteredBarn)` follows after `BreachSeconds` as cleanup (not counted). Its children are art only (perch pieces + the hanging "no wolves" sign): no colliders. |
| `DangerZone` | just above the wall's top beam (y ≈ 6.275) | Has `NightSession` (serialized). A climbing robot entering it → `RobotEnteredDangerZone`, once per robot, all night. `RobotView` sets the Animator bool `InDanger`. No robot state. In Overtime it's the line that ends the night; `DangerZoneView` shows it differently then. |
| `GroundZone` | below the barn | Marker. Falling balls and throwables touching it are removed. |

## The stones: one life, a real pile on the perch (M7.1)

**Loss rule.** The stones are the pig's life. There's no breach limit. A robot that breaches takes the top stone and leaves; a robot that breaches while the pile is empty catches the pigs (`NightEndReason.Caught`) — the only loss. Throwing your last stone isn't a loss: the night goes on, and a chain still in flight can reach the target before the next breach (after the target the night can't be lost, as before).

**One truth, one mirror.**
- **Runtime** `NightState.StonesLeft` is the count, written only by `NightReferee.ChangeStones`, which publishes `StonesChanged`. `NightReferee.AddStones(n)` is the seam for mid-run refills. CoreCheck covers all of it.
- **Simulation** `StonePile` (on the right perch piece) **mirrors** the count with real stone GameObjects; it never decides it. It hands out the real throwables, which is why it's Simulation, not Presentation.
  - Stones sit in fixed pyramid slots, no physics, no collider (`Throwable.Park`). Top stone = last slot filled.
  - When the hand is empty and the pig may throw, the top stone hops to the hand (ThrowOrigin) and waits. `ThrowController` only aims with a stone in the hand, and launches *that* stone (`StonePile.ReleaseHeld` → `Throwable.Launch`). Hop < throw cooldown, so there's no input lag.
  - `Stolen` (at `RobotBreached`, when the breach starts): the top stone is parented to the thief, stays parked (never a weapon, can't start a chain), rides on it through the breach sequence and is destroyed with it. `Added`: new stones drop onto the next free slots. `Forfeited`: overtime ended at the danger line, the unthrown stones go.
  - After every change the pile reconciles to the count, so it can never disagree with the HUD.
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
