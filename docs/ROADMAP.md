# Roadmap

## Current milestone

Core feel prototypes — each mechanic in its own scene under `Assets/_Game/Scenes/Prototypes/` (see [ARCHITECTURE.md](technicals/ARCHITECTURE.md#prototypes--main-game)).

## Next

- [ ] Foundation
  - [ ] Asmdefs (Core / Features / Experimental / Tests) — Core + EditMode tests done
  - [ ] Root `ProjectLifetimeScope` prefab, `GameLifetimeScope` (installers, strict mode), `DevStartConfig` — root prefab + VContainerSettings, MessagePipe, `InputService` done; `GameLifetimeScope` is a stub with the strict flag
  - [ ] `EntityScope`, `EntityInjector`, `EntityRegistry` + spatial grid, `_Common` Combat / Events / Stats / `IChanceRoll` — `EntityScope`, `Health`/`IDamageable`/`DamageInfo`, `DamageDealt`/`PlayerDied`, `IStatProvider` done
  - [ ] HUD — canvas (1920×1080, corner regions) in the Player prefab, `StatBar` widget + style assets; health and stamina bars done
  - [ ] Debug panel with `IDebugCommands`
  - [ ] PlayMode smoke test
- [ ] `Proto_Movement` — walk, sprint, crouch, jump, look. Done in `Scenes/Prototype.unity`: walk, sprint + stamina/exhaustion, jump (coyote, buffer), look, steep-slope slide, fall damage, head bob + landing dip, interact (look cast + HUD prompt, used by pickups). Left: crouch
- [ ] `Proto_Shooting` — fire, recoil, hit feedback, reload, gun jammed, (entity defaults: limited/infinite ammo, base stats, gun feel). Done in `Scenes/Prototype.unity` (shooting range ahead of the table): AK-47 + pistol, fire modes, travelling pooled bullets (muzzle or camera origin), damage, impacts + bullet holes, magazine + shared ammo by type (limited or infinite), magazine / per-round reload, jams with auto clear, recoil, hip / aim / sprint poses, ammo on the hotbar, gun sounds (plain AudioSource), placeholder crosshair dot ([WEAPONS.md](technicals/WEAPONS.md)). Left: sounds through BroAudio, crosshair polish, hit marker / damage numbers, real reload / jam animations, ammo pickups
- [ ] `Proto_Equipment` — hotbar, switch among guns, melee, fishing rod, axe, farm tools (Previous / Next, number keys); equip / unequip animation, primary / secondary use per item. Done in `Scenes/Prototype.unity`: 3-slot hotbar (any weapon in any slot; up to 9) with HUD, weapon pickup (E, first free slot) / swap with the held weapon when full / drop (G), keys 1–9 + mouse wheel, timed holster → equip with a temporary procedural animation, AK-47 / pistol / fire axe (melee swings: hold to repeat, sphere-cast hit, knockback). Left: real animations, fishing rod, tools, use actions
- [ ] `Proto_Enemies` — A* `FollowerEntity`, one archetype as a BD Subtree (Manual mode, ticked by `EnemyManager`), `EnemyBrain`, attack, health, pooling. **Gate:** 300 tree-driven enemies within budget (`Proto_Stress`) before more archetypes
- [ ] `Proto_Fishing` — rod: cast, bite, reel, catch into Inventory; gun fishing: shoot jumping fish (weapon on-hit "catch fish"); fish as crowd entities
- [ ] `Proto_Crafting` — workbench, recipe data, materials → ammo (crafting rules are still [TBD] in the GDD; this tests only the interaction)

## Later (Features — after the core feels good)

- DayNight, Progression, Persistence (save, death, rewind), Activities, Economy — scope follows the GDD

## Done

- Project setup, URP, store assets imported
