# SETUP — from empty folder to first chain

Do these in order. ☁ = can go to a cloud session later; everything here is **local** (needs the Unity editor).

## 1. Repo + project
1. Create an **empty private GitHub repo** `piglings` (no README, no .gitignore — this kit brings its own).
2. Unity Hub → New project → **Unity 6** → template **Universal 2D** → create it in a folder, e.g. `…/Piglings`.
3. Copy this kit's contents into that project folder (so `Assets/_Piglings`, `CLAUDE.md`, `.gitignore`… sit next to `Assets/`, `Packages/`, `ProjectSettings/`).
4. Unity: *Edit → Project Settings → Editor*: Version Control = **Visible Meta Files**, Asset Serialization = **Force Text** (default in Unity 6 — just confirm).
5. Terminal in the project folder:
   ```
   git init -b main
   git add .
   git commit -m "Piglings: project + architecture kit"
   git remote add origin https://github.com/<you>/piglings.git
   git push -u origin main
   ```

## 2. Packages (Window → Package Manager)
- **Input System** (the template usually has it; accept "use new input backend").
- **2D Animation** and **2D PSD Importer** (rigging the PSDs).
- **Test Framework** (for `Tests/EditMode`).

## 3. Layers & physics
1. *Project Settings → Tags and Layers*: add user layers **RobotClimbing, RobotBall, Throwable, Holds, BarnWalls, Ground, Zones** (exact names — `PhysicsLayers.cs` uses them).
2. *Project Settings → Physics 2D → Layer Collision Matrix*: set exactly as the table in ARCHITECTURE.md (uncheck everything else among these layers).

## 4. Art import  (1 art px = 0.01 units)
The art is already in the kit — just set the import settings:
| File | Settings |
|---|---|
| `Art/Barn/*.png` (4× resolution) | Sprite (Single), **PPU 400**, pivot **Bottom** |
| `Art/Pig/pig_rig.psb` | PSD Importer, **Character Rig** on, PPU 200 |
| `Art/WolfBot/wolfbot_rig.psb` | PSD Importer, **Character Rig** on, PPU 300 |
The `*_parts.json` next to each rig lists every part's pivot and parent (for bones).

Hidden layers in the PSDs (red/X eyes, extra mouths, sparks) come in hidden — keep them; they're swaps.

## 5. Definitions (right-click in `Assets/_Piglings/Definitions` → Create → Piglings)
- `Robot_WolfBot` (defaults are tuned: ballRadius 0.174, breakDuration 0.6)
- `Throwable_Stone`
- `Wall_Wood` (holdMaterial: a PhysicsMaterial2D, bounciness ~0.35, friction ~0.2 — make it in `PhysicsMaterials/`)
- `Night_01` → assign the three above; spawnInterval 1.6, throws 30.
Also create `Ball.physicsMaterial2D` (bounciness ~0.3) and assign it to Robot_WolfBot.

## 6. Prefabs
**WolfBot.prefab** — root scale 0.3
- Rigidbody2D (Kinematic, gravity 1), CircleCollider2D (radius set by code), `RobotController` (wire body + collider)
- child: the imported wolf-bot rig → `RobotView` on root (wire animator, eye_on / eye_red / eye_x, detachables = arm/leg uppers, antenna, tail_1)

**Stone.prefab** — Rigidbody2D (Dynamic), CircleCollider2D, `Throwable`, a simple stone sprite (placeholder circle is fine).

## 7. Night scene — `Assets/_Piglings/Scenes/Night.unity`
```
NightSession            NightSession (night = Night_01)
Main Camera             Orthographic, size ≈ 5.3, position (0, 4.8, -10)
Barn
  Barn_Bottom           y 0      (sprite: bottom closed)
  Slice_01              y 3.0    (sprite: slice)
    Holds               7 children, CircleCollider2D r 0.1, layer Holds, component Hold (positions below)
  Slice_02              y 4.6    (same, + Holds)
  Barn_Top              y 6.2    (sprite: top)
    HoleMask            SpriteMask (circle sprite), local (0, 1.6), scale → diameter 0.76
    Pig                 the pig rig, scale 0.26, local ≈ (0, 1.4); body/face sprites Mask Interaction = Visible Inside Mask;
                        throwing arm + stone: no mask, higher Order in Layer than the rim; PigView (wire ThrowController)
  SideWalls             two BoxCollider2D at x ±3.05, layer BarnWalls, tall enough (0 → 6.2)
  TopZone               BoxCollider2D trigger across the top of Slice_02 (y ≈ 6.2), layer Zones, BarnTopZone,
                        Rigidbody2D Kinematic (same trick as CCTD's DangerZone)
ThrowOrigin             empty at the pig's hand (≈ 0.25, 7.9)
ThrowController         ThrowController (session, Stone prefab, origin, container=Throwables, camera)
RobotSpawner            y 0.2, minX -2.6, maxX 2.6; RobotSpawner (session, WolfBot prefab, container=Robots)
GroundZone              BoxCollider2D trigger, wide (12 × 1) at y -0.6, layer Ground, GroundZone, Rigidbody2D Kinematic
Robots / Throwables     empty containers
Debug                   ChainDebugHUD (session)
```
**Hold local positions** (per slice, pivot bottom-center): row 1 y 1.12 → x −1.1, 0, 1.1 · row 2 y 0.54 → x −1.7, −0.55, 0.55, 1.7

## 8. First playtest = Milestone 1
Press Play, click to throw. **Done when:** one stone knocks one robot, its ball knocks a second robot on the way down,
and the HUD shows `Last chain: 2 robots, depth 1`.

## 9. Claude Code cloud sessions
- claude.ai/code → claim the credit → connect GitHub → pick the `piglings` repo.
- Give one task at a time from TASKS.md marked ☁, e.g. *"Do TASKS.md M2.1. Follow CLAUDE.md."*
- Review the PR → merge → `git pull` locally → open Unity (generates `.meta`) → commit the metas.
