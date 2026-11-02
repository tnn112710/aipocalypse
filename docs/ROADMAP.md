# Roadmap

## Current milestone

Core feel prototypes — the mechanics are currently tested together in `Scenes/Prototypes/WorldMap.unity`; each mechanic is planned to get its own `Proto_*` scene under `Assets/_Game/Scenes/Prototypes/` (see [ARCHITECTURE.md](technicals/ARCHITECTURE.md#prototypes--main-game)).

## Next

- [ ] Foundation
  - [ ] Asmdefs (Core / Features / Experimental / Tests) — Core + EditMode tests done
  - [ ] Root `ProjectLifetimeScope` prefab, `GameLifetimeScope` (installers, strict mode), `DevStartConfig` — root prefab + VContainerSettings, MessagePipe, `InputService` done; `GameLifetimeScope` done (strict flag, installs the `SceneInstaller` components on its GameObject and reports installer problems and, when strict, missing installers), with `WorldTimeInstaller`, `EnvironmentInstaller`, `EnemyInstaller` and `RaidInstaller`. Left: `DevStartConfig`
  - [ ] `EntityScope`, `EntityInjector`, `EntityRegistry` + spatial grid, `_Common` Combat / Events / Stats / `IChanceRoll` — `EntityScope`, `Health`/`IDamageable`/`DamageInfo`, `DamageDealt`/`PlayerDied`, `IStatProvider`, `ITargetable` done
  - [ ] HUD — standalone `_Core/UI/Prefabs/HUD.prefab` with its own `HudScope` (canvas 1920×1080, corner regions; placed in the scene, not in the Player prefab), widgets + style assets ([UI.md](technicals/UI.md)); done: health and stamina bars, hotbar, crosshair (with spread ticks), interact prompt, hit feedback, hurt feedback (directional edge flash), QTE, clock, death screen
  - [x] Dev console (backquote key): commands and cheats registered per system (`ConsoleCommands`), Unity logs, on-screen log (`ScreenLog`); editor and dev builds ([CONSOLE.md](technicals/CONSOLE.md), [DEBUG.md](technicals/DEBUG.md)). Later: key binds, live watches
  - [x] PlayMode smoke test — `Tests/PlayMode/WorldMapSmokeTests` (WorldMap scene: injection, enemy system, pick up, fire, reload, raiders spawn at the raid's start hour, no errors)
- [ ] `Proto_Movement` — walk, sprint, crouch, jump, look. Done in `Scenes/Prototypes/WorldMap.unity`: walk, sprint + stamina/exhaustion, jump (coyote, buffer), look, steep-slope slide, fall damage, head bob + landing dip, interact (look cast + HUD prompt, used by pickups), footstep / jump / landing sounds (BroAudio). Left: crouch
- [ ] `Proto_Shooting` — fire, recoil, hit feedback, reload, gun jammed, (entity defaults: limited/infinite ammo, base stats, gun feel). Done in `Scenes/Prototypes/WorldMap.unity` (shooting range ahead of the table): AK-47 + pistol + MP5 + double-barrel and pump shotguns, fire modes, pellets, pump action, travelling pooled bullets (muzzle or camera origin), damage, impacts + bullet holes, magazine + shared ammo by type (limited or infinite), magazine / per-round reload, jams with auto clear and a QTE repair (clear early), clip-driven reload and repair animations on all five guns (cycle clips on MP5, pistol and both shotguns; AK-47 doesn't cycle), recoil, hip / aim / sprint poses, ammo on the hotbar, gun sounds (BroAudio, placeholder clips), aim zoom, hit marker + hit / kill sounds + damage numbers (through `DamageService` / `DamageDealt`), crosshair dot with spread ticks ([WEAPONS.md](technicals/WEAPONS.md)). Left: crosshair polish, damageable targets in the scene, ammo pickups
- [ ] `Proto_Equipment` — hotbar, switch among guns, melee, fishing rod, axe, farm tools (Previous / Next, number keys); equip / unequip animation, primary / secondary use per item. Done in `Scenes/Prototypes/WorldMap.unity`: 3-slot hotbar (any weapon in any slot; up to 9) with HUD, weapon pickup (E, first free slot) / swap with the held weapon when full / drop (G), keys 1–9 + mouse wheel, timed holster → equip with a temporary procedural animation, five guns (AK-47, pistol, MP5, double-barrel and pump shotguns) + fire axe (melee swings: hold to repeat, sphere-cast hit, knockback), gun animations partly done (clip-driven equip, reload, repair and cycle clips on the guns; holster and melee still procedural). Left: real animations (the rest), fishing rod, tools, use actions
- [ ] `Proto_Enemies` — A* `FollowerEntity`, one archetype as a BD Subtree (Manual mode, ticked by `EnemyManager`), `EnemyBrain` (injected, self-registering in `EnemyRegistry`), attack, health, pooling. **Gate:** 300 tree-driven enemies within budget (`Proto_Stress`) before more archetypes. Done in `Scenes/Prototypes/WorldMap.unity`: `FollowerEntity` movement (`EnemyMover`), the shared Wander BD subtree, Manual trees ticked by `EnemyManager`, `EnemyBrain`, `EnemyRegistry`. Health, knockback, hit reactions and death done (`EnemyStats`, `EnemyBody`; goblins have 100 HP). Goblins hunt their target (`TargetRegistry` / `TargetSelector`: the player for now), close in at most three at a time (`AttackSlots`), swing a sword and hurt (`AttackDamage` 15 through `DamageService`); code spawning and pooling through the generic SpawnSystem (`EnemySpawnCard`, `SpawnService`, `PrefabPool` with warm prewarm, formations, air / water placement, open-world `SpawnArea`s with a first goblin camp by the lake, fish in rivers, pool trimming, per-card caps; [SPAWNING.md](technicals/SPAWNING.md)), enemy sounds (`EnemySoundSet`, central footsteps), raid nights ([RAIDS.md](technicals/RAIDS.md)). Left: the 300-enemy gate (`enemy.spawn goblin 300`)
- [ ] `Proto_Fishing` — rod: cast, bite, reel, catch into Inventory; gun fishing: shoot jumping fish (weapon on-hit "catch fish"); fish as crowd entities
- [ ] `Proto_Crafting` — workbench, recipe data, materials → ammo (crafting rules are still [TBD] in the GDD; this tests only the interaction)

- [x] Day/night cycle (Core) — world clock (45 s per game hour, master + per-phase speed, jumps, skips, freeze, N / M cheats), time-of-day look (sun / moon, ambient, fog, painted sky + stars and moon phases, grades), reactors (window glow, door light, particles, tints), ambience (dawn chorus, wind, owls), Duckov-style HUD clock. world map only ([DAYNIGHT.md](technicals/DAYNIGHT.md))
- [x] Raid nights (Core) — every night at 20:00 a fixed raid of 18 goblins in three waves from 60–80 m in front of the home base, at most 12 alive; ends Cleared, at Dawn (06:00) or in Defeat; a death during a raid wakes the player at the next 07:00, any other death costs an hour; arrival horn, hurt feedback (edge flash, grunt, flinch), test ammo crate, `cheat.*` / `raid.*` / `enemy.*` commands, `RaidSoak` autopilot run. World map only ([RAIDS.md](technicals/RAIDS.md)). Later: quiet nights, the difficulty ladder and stat scaling, Blood Moon and eclipse, the midnight retreat, losses on death, closing the fence and fence targeting, crops and animals as targets, spear throwers, raid HUD and morning report, enemy ammo drops
- [x] Fences and gates (Core) — the home fence is 52 breakable segments (about 2 m, too tall to jump) and a double gate, shut by default; E opens / closes the gate, E (hold on a gate) repairs with a first-person hammer, free for now; raiders with no path break the weakest of their 5 nearest segments; once a gap opens most go through it while a third (and anyone the gap is far from) break a second one; breach alert, wood sounds and effects; upgrade tiers as data (spiked fence with thorns) ([DEFENSE.md](technicals/DEFENSE.md)). Later: repair costs with currency, own looks for upgrade tiers, damage while away on a raid night, the paddock fence.

## Later (Features — after the core feels good)

- Enemy liveliness backlog (user, 2026-11-01; design each before building): **wounded goblins** limp (slower, hunched walk) or sometimes flee below ~30% health; **group reactions**: nearby goblins flinch or hesitate when one dies, duck at a headshot kill, taunt when the player retreats (Synty Emotes taunts are imported).
- DayNight rules (dusk warning, sleeping with `SkipTo`, night enemies), Weather (an atmosphere layer in `EnvironmentDirector`, see DAYNIGHT.md), Progression, Persistence (save, death, rewind), Activities, Economy — scope follows the GDD

## Polish levels

How much effort and polish each area gets (user, 2026-10-31). Scale work to the level: don't polish a Low area past working and readable, and ask before raising an area's level.

| Level | Meaning | Done looks like |
| --- | --- | --- |
| **Very High** | The reason to buy the game | Tuned by feel over many passes; custom feedback on every action (sound, VFX, animation, camera); playtested again and again |
| **High** | Used every day in the loop | Complete, clear feedback, polished main paths; store assets adapted to fit; a couple of tuning passes |
| **Medium** | Supports the loop | Works and reads clearly; store assets with light setup; one tuning pass; edge cases handled simply |
| **Low** | Needed but rarely noticed | Works without bugs; defaults and placeholders that read fine; no tuning pass |

| Area | Covers | Level |
| --- | --- | --- |
| Shooting | Guns, recoil, hit feedback, first-person hands, gun audio, muzzle and impact VFX | Very High |
| Inner zone | The yard and meadow (the home side of the river): layout, dressing, house and farm look, the cozy feel | Very High |
| Day/night | Clock, sky, time-of-day looks, the night look | High |
| Enemies & bosses | AI, behaviours, theft, roster and looks, raid spawns, Blood Moon minibosses, the eclipse boss | High |
| Fishing | Rod, gun fishing, fish variety | High |
| Animals | Roaming, drops, the bell, theft, sleep | High |
| Environment | The whole map: terrain, water, vegetation, boundary, atmosphere | High |
| Outer-zone content | Points of interest, loot spots, landmarks, secrets | High |
| Movement & camera | Walk, sprint, jump, look, head bob | High |
| Level balancing | Raid difficulty and schedule tuning, economy numbers, progression pacing | High |
| Tutorial | Wake-up intro, teaching the loop, the bridge repair, the first raid | High |
| Performance | 60 fps at 1080p on an RTX 2060 (here the level means reliability) | High |
| Save/load | Progress across days, never lost (here the level means reliability) | High |
| Crafting | Recipes, workbench | Medium |
| Trading | The trading robot, buy and sell, daily stock, the robots' look and personality | Medium |
| Audio | Everything outside shooting: ambience, music, farm, enemy and UI sounds | Medium |
| VFX | Everything outside shooting: atmosphere, farm, enemy and UI effects | Medium |
| Melee | Axe and fallback weapons | Medium |
| Defense | Fences, barricades, repair | Medium |
| Player upgrades | All player and home growth | Medium |
| Sanity & the Dark | Effects, drain, warnings | Medium |
| Inventory | Hotbar, backpack, carrying | Medium |
| Sleep | Sleeping, time skip, sanity recovery | Medium |
| Crops & plants | Planting, growth, harvest | Medium |
| Daily box | The pick, rarity, the opening moment | Medium |
| Interaction | Pickups, prompts | Medium |
| Looting | Searching, containers, loot tables | Medium |
| HUD | Bars, clock, prompts, the progress bar | Medium |
| Menus | Main and pause menus, settings (FOV, sensitivity, key rebinding, graphics) | Medium |
| Writing | Drone, robots, AI Lord, item names, humor | Medium |
| Swimming | River crossing, drowning | Low |
| Tree chopping | Felling, regrowth | Low |
| Cooking | Recipes, eating | Low |
| Deposit | The safe stash | Low |

Not set yet (suggestions until the user decides): weapon roster & mods Medium, survival meters (HP, hunger, stamina) Medium, death, knockout & losses Medium, dog Medium, guide drone Medium, weather Low, Steam features (achievements, cloud saves) Low, accessibility Low, controller / Steam Deck Low, localization Later (English only at launch).

## Done

- Project setup, URP, store assets imported
