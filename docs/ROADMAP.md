# Roadmap

## Current milestone

Core feel prototypes — each mechanic in its own scene under `Assets/_Game/Scenes/Prototypes/` (see [ARCHITECTURE.md](technicals/ARCHITECTURE.md#prototypes--main-game)).

## Next

- [ ] Foundation
  - [ ] Asmdefs (Core / Features / Experimental / Tests), add MessagePipe package
  - [ ] Root `ProjectLifetimeScope` prefab, `GameLifetimeScope` (installers, strict mode), `DevStartConfig`
  - [ ] `EntityScope`, `EntityInjector`, `EntityRegistry` + spatial grid, `_Common` Combat / Events / Stats / `IChanceRoll`
  - [ ] Debug panel with `IDebugCommands`
  - [ ] PlayMode smoke test
- [ ] `Proto_Movement` — walk, sprint, crouch, jump, look
- [ ] `Proto_Shooting` — fire, switch, recoil, hit feedback, dummies (entity defaults: infinite ammo, base stats)
- [ ] `Proto_Enemies` — A* `FollowerEntity`, one archetype, attack, health, pooling, `EnemyManager` central tick
- [ ] `Proto_Stress` — 100 / 300 / 500 enemies, profiler markers, sets the performance budget
- [ ] `Proto_Ammo` — limited ammo, melee fallback, jams (real Inventory + Stats)

## Later (Features — after the core feels good)

- DayNight, Progression, Persistence (save, death, rewind), Activities, Economy — scope follows the GDD

## Done

- Project setup, URP, store assets imported
