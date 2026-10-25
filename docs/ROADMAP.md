# Roadmap

## Current milestone

Core feel prototypes — the mechanics are currently tested together in `Scenes/Prototypes/WorldMap.unity`; each mechanic is planned to get its own `Proto_*` scene under `Assets/_Game/Scenes/Prototypes/` (see [ARCHITECTURE.md](technicals/ARCHITECTURE.md#prototypes--main-game)).

## Next

- [ ] Foundation
  - [ ] Asmdefs (Core / Features / Experimental / Tests) — Core + EditMode tests done
  - [ ] Root `ProjectLifetimeScope` prefab, `GameLifetimeScope` (installers, strict mode), `DevStartConfig` — root prefab + VContainerSettings, MessagePipe, `InputService` done; `GameLifetimeScope` done (strict flag, installs the `SceneInstaller` components on its GameObject and reports installer problems and, when strict, missing installers), with `WorldTimeInstaller`, `EnvironmentInstaller` and `EnemyInstaller`. Left: `DevStartConfig`
  - [ ] `EntityScope`, `EntityInjector`, `EntityRegistry` + spatial grid, `_Common` Combat / Events / Stats / `IChanceRoll` — `EntityScope`, `Health`/`IDamageable`/`DamageInfo`, `DamageDealt`/`PlayerDied`, `IStatProvider` done
  - [ ] HUD — standalone `_Core/UI/Prefabs/HUD.prefab` with its own `HudScope` (canvas 1920×1080, corner regions; placed in the scene, not in the Player prefab), widgets + style assets ([UI.md](technicals/UI.md)); done: health and stamina bars, hotbar, crosshair (with spread ticks), interact prompt, hit feedback, QTE, clock, death screen
  - [ ] Debug panel with `IDebugCommands`
  - [x] PlayMode smoke test — `Tests/PlayMode/WorldMapSmokeTests` (WorldMap scene: injection, enemy registration, pick up, fire, reload, no errors)
- [ ] `Proto_Movement` — walk, sprint, crouch, jump, look. Done in `Scenes/Prototypes/WorldMap.unity`: walk, sprint + stamina/exhaustion, jump (coyote, buffer), look, steep-slope slide, fall damage, head bob + landing dip, interact (look cast + HUD prompt, used by pickups), footstep / jump / landing sounds (BroAudio). Left: crouch
- [ ] `Proto_Shooting` — fire, recoil, hit feedback, reload, gun jammed, (entity defaults: limited/infinite ammo, base stats, gun feel). Done in `Scenes/Prototypes/WorldMap.unity` (shooting range ahead of the table): AK-47 + pistol + MP5 + double-barrel and pump shotguns, fire modes, pellets, pump action, travelling pooled bullets (muzzle or camera origin), damage, impacts + bullet holes, magazine + shared ammo by type (limited or infinite), magazine / per-round reload, jams with auto clear and a QTE repair (clear early), clip-driven reload and repair animations on all five guns (cycle clips on MP5, pistol and both shotguns; AK-47 doesn't cycle), recoil, hip / aim / sprint poses, ammo on the hotbar, gun sounds (BroAudio, placeholder clips), aim zoom, hit marker + hit / kill sounds + damage numbers (through `DamageService` / `DamageDealt`), crosshair dot with spread ticks ([WEAPONS.md](technicals/WEAPONS.md)). Left: crosshair polish, damageable targets in the scene, ammo pickups
- [ ] `Proto_Equipment` — hotbar, switch among guns, melee, fishing rod, axe, farm tools (Previous / Next, number keys); equip / unequip animation, primary / secondary use per item. Done in `Scenes/Prototypes/WorldMap.unity`: 3-slot hotbar (any weapon in any slot; up to 9) with HUD, weapon pickup (E, first free slot) / swap with the held weapon when full / drop (G), keys 1–9 + mouse wheel, timed holster → equip with a temporary procedural animation, five guns (AK-47, pistol, MP5, double-barrel and pump shotguns) + fire axe (melee swings: hold to repeat, sphere-cast hit, knockback), gun animations partly done (clip-driven equip, reload, repair and cycle clips on the guns; holster and melee still procedural). Left: real animations (the rest), fishing rod, tools, use actions
- [ ] `Proto_Enemies` — A* `FollowerEntity`, one archetype as a BD Subtree (Manual mode, ticked by `EnemyManager`), `EnemyBrain` (injected, self-registering in `EnemyRegistry`), attack, health, pooling. **Gate:** 300 tree-driven enemies within budget (`Proto_Stress`) before more archetypes. Done in `Scenes/Prototypes/WorldMap.unity` (two `WanderCapsule` enemies): `FollowerEntity` movement (`EnemyMover`), the shared Wander BD subtree, Manual trees ticked by `EnemyManager`, `EnemyBrain`, `EnemyRegistry`. Left: attack, health, code spawning, pooling, the 300-enemy gate
- [ ] `Proto_Fishing` — rod: cast, bite, reel, catch into Inventory; gun fishing: shoot jumping fish (weapon on-hit "catch fish"); fish as crowd entities
- [ ] `Proto_Crafting` — workbench, recipe data, materials → ammo (crafting rules are still [TBD] in the GDD; this tests only the interaction)

- [x] Day/night cycle (Core) — world clock (30 s per game hour, master + per-phase speed, jumps, skips, freeze, N / M cheats), time-of-day look (sun / moon, ambient, fog, painted sky + stars and moon phases, grades), reactors (window glow, door light, particles, tints), ambience (dawn chorus, wind, owls), Duckov-style HUD clock. world map only ([DAYNIGHT.md](technicals/DAYNIGHT.md))

## Later (Features — after the core feels good)

- DayNight rules (dusk warning, sleeping with `SkipTo`, night enemies), Weather (an atmosphere layer in `EnvironmentDirector`, see DAYNIGHT.md), Progression, Persistence (save, death, rewind), Activities, Economy — scope follows the GDD

## Done

- Project setup, URP, store assets imported
