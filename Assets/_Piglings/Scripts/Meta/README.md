# Meta
The player's progress across nights: what's saved, and the rules that turn a banked night into it.
Engine-free (no UnityEngine). Reads Events; never touches physics, files or the scene.

- `PlayerProfile` — the save's contents: causes only (per weapon: direct hits; per robot type: ball knocks / knocked by a ball).
- `Progression` — owns the profile; applies `NightBanked` to it.
- `MasteryLevels` — the level rule: a level is derived from total hits + the weapon's thresholds, never stored.
- `ProfileJson` — the save format (versioned JSON) and its strict reader. The file itself is `Simulation/ProfileFile`.
