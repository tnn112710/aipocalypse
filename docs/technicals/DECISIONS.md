# Decisions

Newest first. One entry per non-obvious choice: what, why, alternatives rejected.

## Template

### YYYY-MM-DD — Title

- **Decision:**
- **Why:**
- **Alternatives:**

## 2026-10-04 — Core / Features split, enforced by assemblies

- **Decision:** Stable mechanics (movement, combat, enemies, inventory counts, clock) are Core; design-dependent rules (day/night, progression, persistence, activities, economy) are Features, each a folder + installer. Trial mechanics go in `_Experimental/` behind `DevStartConfig` toggles. Asmdefs `Aipocalypse.Core` ← `Features` ← `Experimental` enforce the direction.
- **Why:** The GDD is still changing. GDD changes should touch only features; cutting a feature = removing its installer.
- **Alternatives:** One assembly with a folder convention (direction not enforced, drifts). Cost: VContainer's source generator stays off (multi-assembly issue #822); reflection injection is fine on PC.

## 2026-10-04 — MessagePipe for gameplay events

- **Decision:** Core publishes small event structs (`ShotFired`, `DamageDealt`, `EntityDied`, `ItemPicked`, `TimePhaseChanged`…) through MessagePipe; features subscribe. C# events only within one system.
- **Why:** New rules (karma, perks, drone quips, AI patch notes) attach as subscribers without editing Core — the main lever for an unfinished design.
- **Alternatives:** Direct calls / C# events across systems (every new rule edits existing code), hand-rolled static event bus (statics, no DI).

## 2026-10-04 — Defer save until the core loop is fun

- **Decision:** No save system during prototyping; `DevStartConfig` presets set up test situations. When added: versioned JSON, no old-save compatibility during development.
- **Why:** Save formats lock in data shapes while the design is still changing.
- **Alternatives:** Daily snapshots from the start (planned for rewind; reconsider when the death/rewind design is final).

## 2026-10-04 — Self-contained entity prefabs; prototypes share code with the game

- **Decision:** Entities (player, enemies, companions, stateful interactables) carry their own `EntityScope`: parent = the scene's `GameLifetimeScope` or the root; each outside need falls back to an entity-owned default if no parent provides it (error in World's strict mode). Mechanics are tested in `Scenes/Prototypes/Proto_*` with the real prefabs and code; scenes add a `GameLifetimeScope` with selected installers only when they need real systems.
- **Why:** Any entity prefab can be dropped into any scene and behave the same, so a working prototype plugs into World with no porting, and scene setup can't silently leave an entity un-injected.
- **Alternatives:** Scene scope registers entities via `RegisterComponentInHierarchy` (breaks when a scope forgets it, wrong instance with several copies); separate prototype scopes with stand-ins (drift between scenes); throwaway prototype project (rework).

## 2026-10-04 — Stats with modifiers for all run-time growth

- **Decision:** Tunable player/weapon numbers are read through `IStatProvider` (base + modifiers); XP, perk cards and upgrades add modifiers. Chance rolls go through `IChanceRoll` (Core default: plain random; Luck feature replaces it). Start minimal; add expiry/stacking when perks are designed.
- **Why:** The core growth pillar (weak office worker → survivor) and temporary perk cards touch recoil, jams, stamina, chopping, loot, crits and fishing. One system keeps that consistent and testable.
- **Alternatives:** Per-system fields mutated by perks (scattered, hard to expire or undo).

## 2026-10-04 — Navigation: A* Pathfinding Project

- **Decision:** Enemy navigation uses A* Pathfinding Project (`com.arongranberg.astar`).
- **Why:** Night waves need many agents at once; A* handles crowds and local avoidance well and is already in the project.
- **Alternatives:** Unity AI Navigation (weaker with many agents), Unity Behavior (a behaviour-graph tool, not a navigation stack; may still be used for AI logic).

## 2026-10-04 — VContainer for DI, no gameplay singletons

- **Decision:** VContainer with a root `ProjectLifetimeScope` prefab (via VContainerSettings) and one `GameLifetimeScope` per gameplay scene. No `X.Instance` singletons for game code.
- **Why:** Many systems read shared state (clock, luck, stats, inventory). Run state (and later save, death loot loss, rewind) is owned by plain C# services. Root prefab lets Play start from any scene. Scene unload disposes game services cleanly.
- **Alternatives:** Singletons (hidden coupling, static state leaks across play sessions and rewinds), Zenject/Extenject (heavier, more framework to learn), hand-written composition root (viable fallback).

## Open questions

- Behaviour logic for enemy archetypes: plain C# state machines vs Unity Behavior graphs.
