# Roadmap

## Current milestone: Vertical slice

Goal (user, 2026-11-09): stop prototyping and build a **vertical slice**. It is 3 in-game days, about an hour of play in a build. The whole loop (farm → explore → prepare → defend → sleep) is at final quality, in a small area. It is used for the Steam page, a trailer and outside playtests.

**Estimates:**

- Hours include +15% for making the systems work together.
- Weeks assume about 45 h of work a week: 4–6 h on weekdays, 8–12 h on weekend days.
- Counting from 2026-11-09, the slice was about **585–875 h (about 730)**, or **13–19 weeks**: early to mid-March 2027.
- Re-estimated 2026-11-22, with Phase 1 postponed (below): about **375–580 h left**, or **9–13 weeks**: late January to late February 2027. The new map comes on top of that when it is picked up again.
- Early Access follows in about late June 2027.

**Status (2026-11-22):** the first two weeks went to shooting (gun parts and optics, 11 guns, arms with retargeted reloads), inventory and loot, save and menus, sleep, animals, ranged goblins, localization, weather rules and the UI look. Phase 1 has not started. What the loop still lacks is anything that spends Tokens (trading, repair costs, upgrades) and a cost for losing a night.

**Decided (user, 2026-11-22):**

- **Keep the current map.** The slice work happens on `WorldMap`; the map redesign (Phase 1) waits until all the basic mechanics are done.
- **Keep the current guns:** all 11 stay in the game and the slice.

**Done means:**

1. A new player plays days 1–3 in a build, from waking up to the morning after the Blood Moon, with no help and no blocking bugs.
2. Shooting and the inner zone are at final quality. Every other area in the slice is at its polish level (below).
3. Nothing seen in the slice comes from `_Placeholder`.
4. It runs at 60 fps at 1080p on an RTX 3060, in a build.
5. Five outside players have played it, and there is footage for a trailer and the Steam page.

**Open decisions (user):**

- **Map size**, for when Phase 1 comes back: 2 × 2 km is assumed. The layout and a travel-time check decide it: a game hour is 45 s, so daylight is about 10 min. Until then the slice uses `WorldMap` (700 × 700 m playable).
- **The farm animal:** decided 2026-11-12: cows and sheep first (built, below); chickens or goats later if wanted.
- **Crops:** in the slice, or stretch only.
- **Blood Moon on night 3.** This would be a slice-only setting; the GDD has it every 10 days.
- **Skill tree and upgrades design** (user, 2026-11-09: both in the slice). Before building, design them in `docs/gdd/IDEAS.md`. The current suggestion has two parts:
  - **Skills** are earned by doing: XP from kills, farming and exploring gives 1 point per level. There are about 10 nodes in 3 branches (Combat, Farming, Survival) and no respec.
  - **Upgrades** are bought with Tokens at a bench, for gear and home.
  - To decide: the XP sources, the node list, the upgrade list, and keeping skills and upgrades from overlapping.
  - This differs from the GDD: `ONE_PAGE.md` lists XP skills under "Not this version".
- **Gun animation scope.** The estimate was made for floating hands and five re-keyed guns. Since 2026-11-16 the hands are Sidekick arms, reloads are retargeted from the KINEMATION pack and jam clearing is generated or a procedural shake (HANDS.md), on 11 guns. Phase 6 is re-estimated below for that route.

**Building rules for this milestone:**

- Items taken from the GDD (thieves, Tokens, debt bar, drone, Blood Moon…) are still confirmed with the user before each is built (AGENTS.md → "Prompt first, GDD second").
- Core feel leftovers not listed in a phase get built when a phase needs them.

### Phase 0: Close Core feel (2–3 h, to about Nov 10)

- [x] Crouch (2–3 h)

### Phase 1: New world map (114–175 h) — postponed

**Postponed (user, 2026-11-22):** the current `WorldMap` stays the game map until all the basic mechanics are done; this phase is planned again after that. Phases 2–7 build on `WorldMap`.

A bigger map replaces the prototype WorldMap as the game scene. WorldMap stays as the test scene.

The whole map gets generator quality. Only the home side and one corner of the Pine Forest get final art in the slice; the other regions stay rough until their updates.

- [ ] Layout for the whole game (home, 3 regions, final zone, rivers) and a travel-time check: how far you can go and come back before dark (12–17 h)
- [ ] Generator for a bigger, tiled map (the current one assumes a single 1 km terrain) (29–46 h)
- [ ] Build heights, water, textures, vegetation and boundary (35–52 h)
- [ ] Home base, fence, raid spawn zone, A* navmesh at the new size (12–17 h)
- [ ] Move the gameplay setups over: spawn areas, goblin camp, fish, bridge, landmarks (9–14 h)
- [ ] Performance on the bigger map: terrain tiles, draw distances, navmesh memory (17–29 h)

### Phase 2: From prototype to game (about 15–23 h left)

- [ ] Start-up flow into the game map, `DevStartConfig` (6–9 h). The main menu already loads `WorldMap`
- [ ] Main menu, pause menu, basic settings: sensitivity, FOV, volume (12–17 h). Main and pause menus built 2026-11-11 with the save system ([SAVE.md](technicals/SAVE.md)). Left: the settings menu (5–8 h; the pause menu's Settings button waits for it)
- [ ] Sleep in bed → skip to morning → morning report (10–15 h). Built 2026-11-11: the bed in the WorldMap house, a wake-up clock (whole hours, never past a raid that hasn't run), lie down / time skip / get up ([SLEEP.md](technicals/SLEEP.md)). Left: the morning report, what sleeping gives

### Phase 3: The day, farm, economy and progression (about 100–150 h left)

- [x] Inventory (built 2026-11-10, ~55–80 h with the loot below; uncommitted): Duckov-style slots, 2 active guns + melee (V) + quick slots 3–9, backpack sized by the worn bag, gear slots, item weights (no slowdown yet), Tokens and ammo as items, the Token magnet, pickups with a feed, throwing, a paused drag-and-drop inventory screen ([EQUIPMENT.md](technicals/EQUIPMENT.md), [UI.md](technicals/UI.md#inventory-screen)). Produce items come with the animals
- [ ] One animal species: roams, drops produce, comes home at the bell (17–23 h). Built 2026-11-12 with two species: cows and a sheep flock in a pen in the WorldMap yard, released by the pen gate to a pasture (double rate), called home by the bell, hurried with E, asleep at night; E sends one home and lets it out, they earn Tokens the player collects with C on the animal, they look up at the player and scatter from gunshots, saved; chickens and pigs added the same day ([ANIMALS.md](technicals/ANIMALS.md)). Left: produce items instead of Tokens, the paddock on the new map, tuning by hand
- [ ] Tokens + trading robot: sell produce, buy ammo and fence repairs (23–32 h). Robots pass 1 built 2026-11-22 (uncommitted, [ROBOTS.md](technicals/ROBOTS.md)): two service robots at home (placeholder Shop and Upgrades), idle and strolls, E greeting, the Q whistle, reactions, night standby, beeps; no menus yet. Polish above Medium approved by the user
- [ ] Debt bar + guide drone with a few billing jokes (18–28 h)
- [ ] Fence repair costs Tokens (3–5 h)
- [ ] Upgrades: an upgrade bench, upgrade data, a purchase screen, 4–6 upgrades paid in Tokens (14–21 h)
- [ ] Simple skill tree (39–59 h):
  - XP and levels
  - about 10 nodes in 3 branches, with unlock rules
  - a skill tree screen
  - skill effects through a shared stat-modifier layer, which upgrades use too
- [ ] *Stretch:* 1–2 crops (12–17 h), rod fishing (`Proto_Fishing`, 23–35 h)

### Phase 4: Explore (about 32–49 h left)

- [ ] Bridge repair (9–14 h)
- [ ] Points of interest in one corner of the Pine Forest (17–26 h)
- [x] Loot containers and loot tables: searched crates (`LootContainer`), lootable goblin bodies (`LootTable`, 20 at most, gone at dawn), Take all (built with the inventory above). Left: loot tables for crates and points of interest
- [ ] Wandering goblins with caps (6–9 h)

### Phase 5: Defend (about 51–80 h left)

- [ ] Thieves steal animals (14–21 h)
- [ ] Raids get harder over the 3 nights; losing a raid has a cost (13–21 h)
- [x] Spear thrower, a second goblin unit (9–14 h). Built 2026-11-11 with an archer as well: stand-off distances, straight throws and lobs over the fence, a wind-up, trails, landing rings and a fly-by whistle ([ARCHITECTURE.md → AI](technicals/ARCHITECTURE.md#ai)). Left: tuning by hand, checking the weapons in the hands up close
- [ ] Raid HUD (7–9 h)
- [ ] Blood Moon on night 3, light version: red look, more goblins, one elite instead of a full miniboss (17–29 h)

### Phase 6: Gun animation rework (about 30–50 h left)

This comes before the sound and VFX pass, because impact sounds are timed by markers inside the clips.

- [ ] Clips on every gun (the 11 current guns, user 2026-11-22): equip, idle, fire, reload, empty reload, jam clearing, cycle
  - [x] Trial on the AK47 (2026-11-16): arms, retargeted reload and empty reload, shake for jam clearing
  - [x] The other guns (2026-11-16 to 11-21): reloads retargeted and Reseat / Rack repairs generated for MP5, pistol, M4, SCAR, Glock 17, Desert Eagle (AK-74M shares the AK47's); block-outs animated; sprint poses and grip turns on all 11 ([HANDS.md](technicals/HANDS.md))
  - [ ] Left: Rack repair wrists on M4 / SCAR / Desert Eagle, hand-keyed shotgun reloads, the double barrel's ADS and reload hands, underbarrel grips moving the support hand, the Glock slide, SCAR on the Weapons Test tables
- [ ] Replace the procedural holster, axe swing and hammer knocks with clips (12–17 h)

### Phase 7: Quality pass (144–223 h, to about Mar 3)

- [ ] Shooting leftovers: crosshair polish, damageable targets in the scene (12–17 h; ammo pickups came with the inventory)
- [ ] Shooting to Very High: real gun sounds instead of the `_Placeholder` clips, muzzle and impact VFX, feel passes (15–28 h). First feel pass 2026-11-11 ([WEAPONS.md → Feel pass](technicals/WEAPONS.md#feel-pass)): view punch, shells, shot tails, last-rounds click, surfaces, enemy hit flash, kill and melee feedback, Token burst, low-health warning, sprint view. Not tuned by hand yet
- [ ] Inner zone to Very High: yard, house and farm dressing, lighting, the cozy feel (35–52 h)
- [ ] Goblins easy to read at night; hit reactions (12–17 h)
- [ ] Final HUD style; audio and music pass (21–31 h)
- [ ] Short wake-up intro: the drone and a few lines, not the full tutorial (9–14 h)
- [ ] Performance in a build (17–29 h)
- [ ] Two rounds of outside playtests, with fixes (23–35 h)

**If a phase slips by more than a week, cut rather than add hours.** Cuts in order:

1. Crops and other stretch items
2. ~~A 1.5 × 1.5 km map~~ (no longer needed: the slice keeps `WorldMap`, user 2026-11-22)
3. Polish only 3 guns' animation in the slice (AK-47, pistol, pump), the rest after it; all 11 stay in the game (about 0.5 week)
4. A smaller skill tree: 1 branch of 4 nodes, the rest after the slice (about 0.5 week)
5. A shorter intro

## After the vertical slice

Milestones to Early Access (about late June 2027): **Ten days** (save/load, more upgrades and skills, losses, the rest of the Pine Forest and its boss, the second enemy type, hunger and sanity) → **Thirty days** (eclipse, tutorial, balance, the Steam build) → **Early Access**.

Left out of the slice on purpose:

- Systems: save/load (first version built early, 2026-11-10: five slots, morning autosave, pause and main menus, [SAVE.md](technicals/SAVE.md); a settings menu still to come), hunger and sanity, crafting (`Proto_Crafting`), the daily box, deposit, rescued robots.
- Enemies: grudge and Threat level.
- World: region bosses, other regions, eclipse.
- Story: endings, the dog.

## Core feel (prototype milestone, closing)

The mechanics are tested together in `Scenes/Prototypes/WorldMap.unity`; each mechanic is planned to get its own `Proto_*` scene under `Assets/_Game/Scenes/Prototypes/` (see [ARCHITECTURE.md](technicals/ARCHITECTURE.md#prototypes--main-game)). What is built so far:

- [ ] Foundation
  - [ ] Asmdefs (Core / Features / Experimental / Tests) — Core + EditMode tests done
  - [ ] Root `ProjectLifetimeScope` prefab, `GameLifetimeScope` (installers, strict mode), `DevStartConfig` — root prefab + VContainerSettings, MessagePipe, `InputService` done; `GameLifetimeScope` done (strict flag, installs the `SceneInstaller` components on its GameObject and reports installer problems and, when strict, missing installers), with `WorldTimeInstaller`, `EnvironmentInstaller`, `EnemyInstaller` and `RaidInstaller`. Left: `DevStartConfig`
  - [ ] `EntityScope`, `EntityInjector`, `EntityRegistry` + spatial grid, `_Common` Combat / Events / Stats / `IChanceRoll` — `EntityScope`, `Health`/`IDamageable`/`DamageInfo`, `DamageDealt`/`PlayerDied`, `IStatProvider`, `ITargetable` done
  - [ ] HUD — standalone `_Core/UI/Prefabs/HUD.prefab` with its own `HudScope` (canvas 1920×1080, corner regions; placed in the scene, not in the Player prefab), widgets + style assets ([UI.md](technicals/UI.md)); done: health and stamina bars, hotbar, crosshair (with spread ticks), interact prompt, hit feedback, hurt feedback (directional edge flash), QTE, clock, death screen
  - [x] Dev console (backquote key): commands and cheats registered per system (`ConsoleCommands`), Unity logs, on-screen log (`ScreenLog`); editor and dev builds ([CONSOLE.md](technicals/CONSOLE.md), [DEBUG.md](technicals/DEBUG.md)). Later: key binds, live watches
  - [x] PlayMode smoke test — `Tests/PlayMode/WorldMapSmokeTests` (WorldMap scene: injection, enemy system, pick up, fire, reload, raiders spawn at the raid's start hour, no errors)
- [x] `Proto_Movement` — walk, sprint, crouch, jump, look. Done in `Scenes/Prototypes/WorldMap.unity`: walk, sprint + stamina/exhaustion, jump (coyote, buffer), look, steep-slope slide, fall damage, head bob + landing dip, interact (look cast + HUD prompt, used by pickups), footstep / jump / landing sounds (BroAudio), crouch (toggle; works with sprint, jump, low ceilings, aiming and respawn)
- [ ] `Proto_Shooting` — fire, recoil, hit feedback, reload, gun jammed, (entity defaults: limited/infinite ammo, base stats, gun feel). Done in `Scenes/Prototypes/WorldMap.unity` (shooting range ahead of the table): AK-47 + pistol + MP5 + double-barrel and pump shotguns, fire modes, pellets, pump action, travelling pooled bullets (muzzle or camera origin), damage, impacts + bullet holes, magazine + shared ammo by type (limited or infinite), magazine / per-round reload, jams with auto clear and a QTE repair (clear early), clip-driven reload and repair animations on all five guns (cycle clips on MP5, pistol and both shotguns; AK-47 doesn't cycle), recoil, hip / aim / sprint poses, ammo on the hotbar, gun sounds (BroAudio, placeholder clips), aim zoom, hit marker + hit / kill sounds + damage numbers (through `DamageService` / `DamageDealt`), crosshair dot with spread ticks ([WEAPONS.md](technicals/WEAPONS.md)). Left: crosshair polish, damageable targets in the scene, ammo pickups
- [ ] `Proto_Equipment` — hotbar, switch among guns, melee, fishing rod, axe, farm tools (Previous / Next, number keys); equip / unequip animation, primary / secondary use per item. Done in `Scenes/Prototypes/WorldMap.unity`: 3-slot hotbar (any weapon in any slot; up to 9) with HUD, weapon pickup (E, first free slot) / swap with the held weapon when full / drop (G), keys 1–9 + mouse wheel, timed holster → equip with a temporary procedural animation, five guns (AK-47, pistol, MP5, double-barrel and pump shotguns) + fire axe (melee swings: hold to repeat, sphere-cast hit, knockback), gun animations partly done (clip-driven equip, reload, repair and cycle clips on the guns; holster and melee still procedural). Left: real animations (the rest), fishing rod, tools, use actions
- [ ] `Proto_Enemies` — A* `FollowerEntity`, one archetype as a BD Subtree (Manual mode, ticked by `EnemyManager`), `EnemyBrain` (injected, self-registering in `EnemyRegistry`), attack, health, pooling. **Enemy count (user, 2026-11-09):** a raid has at most 50 raiders alive at once, usually 20–30. This is not a cap on the whole map. Enemies out of sight or far from the player can be hidden (parked or asleep), and non-raid enemies can be hidden during a raid. Done in `Scenes/Prototypes/WorldMap.unity`: `FollowerEntity` movement (`EnemyMover`), the shared Wander BD subtree, Manual trees ticked by `EnemyManager`, `EnemyBrain`, `EnemyRegistry`. Health, knockback, hit reactions and death done (`EnemyStats`, `EnemyBody`; goblins have 100 HP). Goblins hunt their target (`TargetRegistry` / `TargetSelector`: the player for now), close in at most three at a time (`AttackSlots`), swing a sword and hurt (`AttackDamage` 15 through `DamageService`); code spawning and pooling through the generic SpawnSystem (`EnemySpawnCard`, `SpawnService`, `PrefabPool` with warm prewarm, formations, air / water placement, open-world `SpawnArea`s with a first goblin camp by the lake, fish in rivers, pool trimming, per-card caps; [SPAWNING.md](technicals/SPAWNING.md)), enemy sounds (`EnemySoundSet`, central footsteps), raid nights ([RAIDS.md](technicals/RAIDS.md)). 300 already ran in the editor, at a 13.7 ms PlayerLoop median ([SPAWNING.md](technicals/SPAWNING.md))
- [ ] `Proto_Fishing` — rod: cast, bite, reel, catch into Inventory; gun fishing: shoot jumping fish (weapon on-hit "catch fish"); fish as crowd entities
- [ ] `Proto_Crafting` — workbench, recipe data, materials → ammo (crafting rules are still [TBD] in the GDD; this tests only the interaction)

- [x] Day/night cycle (Core) — world clock (45 s per game hour, master + per-phase speed, jumps, skips, freeze, N / M cheats), time-of-day look (sun / moon, ambient, fog, painted sky + stars and moon phases, grades), reactors (window glow, door light, particles, tints), ambience (dawn chorus, wind, owls), Duckov-style HUD clock. world map only ([DAYNIGHT.md](technicals/DAYNIGHT.md))
- [x] Raid nights (Core) — every night at 20:00 a fixed raid of 18 goblins in three waves from 60–80 m in front of the home base, at most 12 alive; ends Cleared, at Dawn (06:00) or in Defeat; a death during a raid wakes the player at the next 07:00, any other death costs an hour; arrival horn, hurt feedback (edge flash, grunt, flinch), test ammo crate, `cheat.*` / `raid.*` / `enemy.*` commands, `RaidSoak` autopilot run. World map only ([RAIDS.md](technicals/RAIDS.md)). Later: quiet nights, the difficulty ladder and stat scaling, Blood Moon and eclipse, the midnight retreat, losses on death, closing the fence and fence targeting, crops and animals as targets, spear throwers, raid HUD and morning report, enemy ammo drops
- [x] Fences and gates (Core) — the home fence is 52 breakable segments (about 2 m, too tall to jump) and a double gate, shut by default; E opens / closes the gate, with the hammer (a hotbar item, on the yard table) R repairs a segment and E repairs all of them, damaged segments glow through walls while it's held, free for now; raiders with no path break the weakest of their 5 nearest segments; once a gap opens most go through it while a third (and anyone the gap is far from) break a second one; breach alert, wood sounds and effects; upgrade tiers as data (spiked fence with thorns) ([DEFENSE.md](technicals/DEFENSE.md)). Later: repair costs with currency, own looks for upgrade tiers, damage while away on a raid night, the paddock fence.

## Later (Features — after the core feels good)

- Enemy liveliness backlog (user, 2026-11-01; design each before building): **wounded goblins** limp (slower, hunched walk) or sometimes flee below ~30% health; **group reactions**: nearby goblins flinch or hesitate when one dies, duck at a headshot kill, taunt when the player retreats (Synty Emotes taunts are imported).
- DayNight rules (dusk warning, sleeping with `SkipTo`, night enemies), Weather effects on play (the weather and its rules are built: phases A and B in WEATHER.md), Progression, Persistence (save, death, rewind), Activities, Economy — scope follows the GDD

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
| Dog | Loki: following, home life, fighting, herding, reactions (user, 2026-11-23) | High |
| Environment | The whole map: terrain, water, vegetation, boundary, atmosphere | High |
| Outer-zone content | Points of interest, loot spots, landmarks, secrets | High |
| Movement & camera | Walk, sprint, jump, look, head bob | High |
| Level balancing | Raid difficulty and schedule tuning, economy numbers, progression pacing | High |
| Tutorial | Wake-up intro, teaching the loop, the bridge repair, the first raid | High |
| Performance | 60 fps at 1080p on an RTX 3060 (here the level means reliability) | High |
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

Not set yet (suggestions until the user decides): weapon roster & mods Medium, survival meters (HP, hunger, stamina) Medium, death, knockout & losses Medium, guide drone Medium, weather Low, Steam features (achievements, cloud saves) Low, accessibility Low, controller / Steam Deck Low, localization Later (English only at launch; the system is built since 2026-11-13 with Vietnamese and French to test it, LOCALIZATION.md; which languages ship is still open).

## Done

- Project setup, URP, store assets imported
