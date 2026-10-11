# Roadmap

## Current milestone

Core feel prototypes — each mechanic in its own scene under `Assets/_Game/Scenes/Prototypes/` (see [ARCHITECTURE.md](technicals/ARCHITECTURE.md#prototypes--main-game)).

## Next

- [ ] Foundation
  - [ ] Asmdefs (Core / Features / Experimental / Tests)
  - [ ] Root `ProjectLifetimeScope` prefab, `GameLifetimeScope` (installers, strict mode), `DevStartConfig`
  - [ ] `EntityScope`, `EntityInjector`, `EntityRegistry` + spatial grid, `_Common` Combat / Events / Stats / `IChanceRoll`
  - [ ] Debug panel with `IDebugCommands`
  - [ ] PlayMode smoke test
- [ ] `Proto_Movement` — walk, sprint, crouch, jump, look
- [ ] `Proto_Shooting` — fire, recoil, hit feedback, reload, gun jammed, (entity defaults: limited/infinite ammo, base stats, gun feel)
- [ ] `Proto_Equipment` — hotbar, switch among guns, melee, fishing rod, axe, farm tools (Previous / Next, number keys); equip / unequip animation, primary / secondary use per item
- [ ] `Proto_Enemies` — A* `FollowerEntity`, one archetype as a BD Subtree (Manual mode, ticked by `EnemyManager`), `EnemyBrain`, attack, health, pooling. **Gate:** 300 tree-driven enemies within budget (`Proto_Stress`) before more archetypes
- [ ] `Proto_Fishing` — rod: cast, bite, reel, catch into Inventory; gun fishing: shoot jumping fish (weapon on-hit "catch fish"); fish as crowd entities
- [ ] `Proto_Crafting` — workbench, recipe data, materials → ammo (crafting rules are still [TBD] in the GDD; this tests only the interaction)

## Later (Features — after the core feels good)

- DayNight, Progression, Persistence (save, death, rewind), Activities, Economy — scope follows the GDD

## Done

- Project setup, URP, store assets imported
