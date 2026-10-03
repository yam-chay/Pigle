# CLAUDE.md — working on Piglings

Piglings: 2D physics arcade/roguelike (Unity 6, URP 2D). A pig in the barn's roof hole throws stones at the wolf's
climbing robot-wolves; hit robots lose their grip, break into balls and knock others loose on the way down.
**Gravity is the weapon — chains are the game.**

Read first: `ARCHITECTURE.md` (structure, wins over this file), then `TASKS.md` (what's next). Design lives in the GDD (Claude Docs).

## Rules for all code
- Respect the layer references in ARCHITECTURE.md. If you need a forbidden reference, stop and explain why in the PR instead.
- No singletons, no static mutable state, no `Find*` lookups, no bootstrap. Scene objects get `NightSession` via `[SerializeField]`.
- One source of truth per thing. State machines: transitions only through `SetState` → `EnterState`.
- Events are immutable structs in `Piglings.Events`. Add a new event type rather than overloading an old one.
- Runtime ids are `GameId`, never `EntityId` (clashes with `UnityEngine.EntityId` in Unity 6.5).
- Every MonoBehaviour / ScriptableObject lives in its own file with the same name (Unity can't add it as a component otherwise — the old Zones.cs bug). Plain types (events, enums, structs) may share a file. CoreCheck enforces it.
- Engine-free assemblies: C# 9 max (Unity's compiler), no `UnityEngine`.
- Unity 6 API: `Rigidbody2D.linearVelocity` (not `velocity`); input via `UnityEngine.InputSystem`.
- Comments explain **why**, not what. Yam reads every change and wants to understand it — keep code plain over clever.
- Tune values in the Definition assets or Inspector, never by editing defaults in `.cs` files.

## Cloud sessions (claude.ai/code) — no Unity editor there
- ONLY touch: `*.cs`, `*.asmdef`, `*.md`, `Tools/**`.
- NEVER create or edit: `*.unity`, `*.prefab`, `*.asset`, `*.meta`, `*.anim`, `*.controller`, ProjectSettings/. Those need the editor.
- New `.cs` files are fine without `.meta` — Unity generates them when Yam pulls; he commits them afterward.
- Before opening a PR: `dotnet run --project Tools/CoreCheck` must pass. Add checks there for any Rules/Runtime/Events/Meta change (and the save file, `ProfileFile`). It restores Newtonsoft.Json from NuGet on first run.
- You can't compile Unity-side code (Simulation/Presentation/Definitions). Say so in the PR and list what Yam should verify in the editor.
- PR description: what changed, why, and what to test in play mode — in plain language.
- Tick the task in TASKS.md in the same PR.
- Before finishing a PR: merge the latest Production into the branch, then re-read in full every file that changed on both sides.

## Local sessions (VS Code + Claude Code + unity-mcp)
- Scene/prefab/import/layer work happens here.
- After merging a cloud PR: open Unity, let it import, commit the new `.meta` files.

## Reference
`Reference/CCTD/` holds read-only CCTD code to port from. Never edit it; never move it into Assets/.
