# Decisions

Newest first. One entry per non-obvious choice: what, why, alternatives rejected.

## Template

### YYYY-MM-DD — Title

- **Decision:**
- **Why:**
- **Alternatives:**

## 2026-10-04 — Prototype scenes share code with the main game

- **Decision:** Mechanics are tested in `Scenes/Prototypes/Proto_*` scenes using the real code and prefabs; only scopes and stand-ins (`_Core/_Prototype/`) are prototype-specific. Systems register through shared installers.
- **Why:** Test one mechanic in isolation without menu/save/day cycle, and plug it into World by adding an installer, not by porting code.
- **Alternatives:** Throwaway prototype project or copied prototype scripts (rework when promoting, drift between versions).

## 2026-10-04 — Stats with modifiers for all run-time growth

- **Decision:** Tunable player/weapon numbers are Stats (base + modifiers with source and optional expiry day). XP, perk cards and upgrades all add modifiers. Chance rolls go through `LuckService`.
- **Why:** The core growth pillar (weak office worker → survivor) and temporary perk cards touch recoil, jams, stamina, chopping, loot, crits and fishing. One system keeps that consistent and testable.
- **Alternatives:** Per-system fields mutated by perks (scattered, hard to expire or undo).

## 2026-10-04 — Navigation: A* Pathfinding Project

- **Decision:** Enemy navigation uses A* Pathfinding Project (`com.arongranberg.astar`).
- **Why:** Night waves need many agents at once; A* handles crowds and local avoidance well and is already in the project.
- **Alternatives:** Unity AI Navigation (weaker with many agents), Unity Behavior (a behaviour-graph tool, not a navigation stack; may still be used for AI logic).

## 2026-10-04 — VContainer for DI, no gameplay singletons

- **Decision:** VContainer with a root `ProjectLifetimeScope` prefab (via VContainerSettings) and one `GameLifetimeScope` per gameplay scene. No `X.Instance` singletons for game code.
- **Why:** Many systems read shared state (clock, luck, stats, inventory). Save, death loot loss and big-fight rewind need state owned by plain C# services. Root prefab lets Play start from any scene. Scene unload disposes game services cleanly.
- **Alternatives:** Singletons (hidden coupling, static state leaks across play sessions and rewinds), Zenject/Extenject (heavier, more framework to learn), hand-written composition root (viable fallback).

## Open questions

- Behaviour logic for enemy archetypes: plain C# state machines vs Unity Behavior graphs.
