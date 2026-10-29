# Synty store catalogue

What we *could* import from [syntystore.com](https://syntystore.com/). The project has a **SyntyPass** (full access to every pack), but packs are imported only when a task needs them. This file is the shopping list; [ASSETS.md](ASSETS.md) is what's actually in the project.

- **Importing a pack** follows the usual rule (AGENTS.md → "Ask the user first"): ask, import into `Assets/Synty/<PackFolder>` (Sidekick character packs into `Assets/Synty/SidekickCharacters/Characters/<Pack>`), add its ASSETS.md row with a tag, and mark it **Installed** here.
- **Style:** POLYGON packs and Sidekick characters only. The SIMPLE (cube cartoon) and POLYGON MINI (chibi) series are left out: wrong style, not needed.
- **Fit** is for aipocalypse (an AI-made meadow sandbox with references everywhere, guns first, fantasy goblins already in): **High** = clear use now, **Med** = plausible later, **Low** = unlikely.


## Installed

| Pack | Folder | ASSETS.md tag |
| --- | --- | --- |
| ANIMATION - Base Locomotion | `Assets/Synty/AnimationBaseLocomotion` | Undecided |
| ANIMATION - Goblin Locomotion | `Assets/Synty/AnimationGoblinLocomotion` | Undecided |
| INTERFACE - Apocalypse HUD | `Assets/Synty/InterfaceApocalypseHUD` | Should |
| INTERFACE - Core (comes with the Interface packs) | `Assets/Synty/InterfaceCore` | Should |
| POLYGON - Generic (shared Synty content) | `Assets/Synty/PolygonGeneric` | Avoid |
| POLYGON - Goblin War Camp | `Assets/Synty/PolygonGoblinWarCamp` | Undecided |
| POLYGON - Prototype Pack | `Assets/Synty/PolygonPrototype` | Avoid |
| Sidekick Characters (tool) + Goblin Fighters, Goblin Species | `Assets/Synty/SidekickCharacters` | Undecided |
| Synty Package Helper (importer tool) | `Assets/Synty/SyntyPackageHelper` | Avoid (PLUGINS.md) |

## Animation

Humanoid clips that retarget onto Sidekick and POLYGON characters (and any humanoid we use).

| Pack | Fit | Purpose |
| --- | --- | --- |
| ANIMATION - Bow Combat | High | Bow draw, aim, fire, reload, locomotion with a bow. **The missing clips for goblin archers** (the Malbers bow clips don't pose standalone). |
| ANIMATION - Sword Combat | Low | Sword attacks, blocks, hits, combat locomotion. Kevin Iglesias Melee (installed, in use on the goblins) already covers it; only for extra variety. |
| ANIMATION - Idles | Low | Many idle variations: fidgets for enemies and NPCs, the drone's people, base dwellers. Kevin Iglesias Basic Motions and Soldier (installed) have idles, idle breaks and military idles. |
| ANIMATION - Emotes and Taunts | Med | Salutes, taunts, celebrations: enemy taunts, NPC reactions, "everything is a game" jokes. Kevin Iglesias Basic Motions (installed) covers everyday emotes (cheer, angry, wave, clap, talk) and Soldier the salutes; this pack is for taunts. |
| ANIMATION - Base Locomotion | Installed | Walk, run, crouch, jump, strafe (human). |
| ANIMATION - Goblin Locomotion | Installed | Goblin-styled locomotion (in use). |

## Sidekick modular characters

Modular characters built with the Sidekick tool we already use (`SidekickKit`, tools-unity-synty-characters). Parts mix across packs.

| Pack | Fit | Purpose |
| --- | --- | --- |
| Apocalypse Survivors | High | Scavenger survivors: NPCs, traders, rival raiders, player-model references. |
| Apocalypse Outlaws | High | Raider/bandit faction: human enemies. |
| Apocalypse Zombies | High | Zombie horde: night-defense enemies. |
| Goblin Fighters | Installed | Goblin warriors (current enemies). |
| Modern Civilians | Med | Everyday people: office workers (the MC's former life), town NPCs. |
| Modern Police | Med | Police NPCs or enemies. |
| Modern Thugs | Med | Street gang enemies. |
| Sci-Fi Robots | Med | Robots: AI Lord's minions, drones' bigger cousins. |
| Sci-Fi Soldiers | Med | Armoured soldiers: AI enforcer faction. |
| Fantasy Skeletons | Med | Undead enemies for night waves or dungeon areas. |
| Fantasy Knights | Low | Armoured knights (reference zones). |
| Fantasy Villagers | Low | Medieval villagers. |
| Fantasy Sorcerers | Low | Mages: caster enemies. |
| Elven Warriors | Low | Elves. |
| Viking Warriors | Low | Vikings. |
| Samurai Warriors | Low | Samurai. |
| Pirate Captains | Low | Pirates. |
| Sci-Fi Civilians | Low | Futuristic civilians. |
| Sidekick FREE Starter Pack | Low | Sample parts across genres. |

## Interface

UI sprites, fonts and prefabs (we use sprites and fonts only, never their prefabs or scripts: ASSETS.md → UI).

| Pack | Fit | Purpose |
| --- | --- | --- |
| INTERFACE - Apocalypse HUD | Installed | HUD art, item and weapon icons (in use). |
| INTERFACE - Modern Menus | High | Main menu, settings, pause, inventory screens in a modern style. |
| INTERFACE - Military Combat HUD | Med | Extra shooter HUD pieces: ammo, compass, hit markers, killfeed. |
| INTERFACE - Sci-Fi Menus / Sci-Fi Soldier HUD | Med | AI Lord / drone UI flavour (screens the AI shows). |
| INTERFACE - Fantasy Menus / Fantasy Warrior HUD / Fantasy Screens Add-On | Low | Fantasy RPG UI. |
| INTERFACE - Dark Fantasy HUD / Dark Fantasy Menus | Low | Gothic UI. |

## POLYGON: apocalypse and survival

| Pack | Fit | Purpose |
| --- | --- | --- |
| POLYGON - Apocalypse Pack | High | Ruined town, survivors, zombies, weapons, vehicles, barricades: loot sites and base-defense props. |
| POLYGON - Apocalypse Wasteland | High | Post-disaster wasteland: scrap buildings, makeshift camps, vehicles, survivors. Base building and raid areas. |
| POLYGON - Woodland Apocalypse Map | Med | Map add-on (needs Apocalypse Pack + Alpine Mountain): a forest demo map to borrow layouts from. |
| POLYGON - City Zombies Pack | Med | Zombie characters (POLYGON, non-modular). |
| POLYGON - Boss Zombies | Med | Zombie bosses with weapons: night-wave bosses. |
| POLYGON - Bubblegum Killstick Kit (free) | Low | Kawaii melee weapons: joke loot. |

## POLYGON: nature biomes

The world map is a meadow; these match the Synty style if we ever move off the current environment packs (ENVIRONMENT.md).

| Pack | Fit | Purpose |
| --- | --- | --- |
| POLYGON - Meadow Forest - Nature Biome | High | Meadow, flowers, trees: the dream meadow and home area. |
| POLYGON - Nature Pack | Med | Generic trees, rocks, plants, terrain bits. |
| POLYGON - Alpine Mountain - Nature Biome | Med | Mountains, pines, snow: map edges, cold region. |
| POLYGON - Swamp Marshland - Nature Biome | Med | Rotting swamp: goblin territory. |
| POLYGON - Enchanted Forest - Nature Biome | Med | Magical forest: "AI glitch" zones. |
| POLYGON - Arid Desert - Nature Biome | Low | Desert region. |
| POLYGON - Tropical Jungle - Nature Biome | Low | Jungle region. |

## POLYGON: modern world (loot sites, reference zones)

| Pack | Fit | Purpose |
| --- | --- | --- |
| POLYGON - Farm Pack | High | Farm buildings, crops, animals, tractors: the farmer dream, home base. |
| POLYGON - Town Pack | High | Small-town buildings, cars, props: raid towns. |
| POLYGON - Office Pack | High | Offices and desks: the MC's old job, opening scene, joke loot sites. |
| POLYGON - Shops Pack (+ Shopping Plaza Map) | Med | Modular shops and interiors: supermarket and mall raids. |
| POLYGON - City Pack / City Characters Pack | Med | City buildings and people. |
| POLYGON - Construction Pack | Med | Scaffolding, cranes, materials: base building props, a construction-site raid. |
| POLYGON - Police Station (+ Prison Map) | Med | Police station interiors, armoury loot. |
| POLYGON - Gang Warfare Pack | Med | Gang hideouts, weapons, vehicles. |
| POLYGON - Heist Pack | Med | Bank, vault, heist props: loot vault references. |
| POLYGON - Dog Pack | Med | Rigged dogs: companion or enemy dogs. |
| POLYGON - Kids Pack | Low | Playgrounds, toys: "everything is a game" props. |
| POLYGON - Casino / Nightclubs / Palm City | Low | Themed reference zones. |
| POLYGON - Street Racer / Pro Racer | Low | Cars. |
| POLYGON - Snow Kit / Spy Kit / Explorer Kit | Low | Small themed kits. |
| POLYGON - Coffee Shop / Quad Bike / Stunt Plane / Water Guns (free) | Low | Small props and vehicles. |

## POLYGON: military and war

| Pack | Fit | Purpose |
| --- | --- | --- |
| POLYGON - Military Pack (+ Military Warehouse Map) | High | Bunkers, crates, vehicles, soldiers, weapons: high-value loot sites, base defenses, ammo crates. |
| POLYGON - Battle Royale Pack | Med | Loot crates, airdrops, weapons, characters: extraction-shooter props. |
| POLYGON - War Pack (+ War Map - WWI) | Low | WW2/WWI trenches and weapons. |
| POLYGON - Mech Pack | Low | Mechs: an AI boss idea. |

## POLYGON: fantasy

Goblins are already in, and the AI sandbox can mix genres, so these are reference zones and enemy sources.

| Pack | Fit | Purpose |
| --- | --- | --- |
| POLYGON - Goblin War Camp | Installed | Goblin camp, weapons (bows, arrows, quiver), props. |
| POLYGON - Bow and Crossbow (free) | Med | Bows, crossbows, arrows (props for archers or player loot). |
| POLYGON - Dungeon Pack / Dungeon Realms (+ map add-ons) | Med | Dungeons: a "game level" raid site. |
| POLYGON - Dark Fantasy / Dark Fortress (+ Manor Map) | Low | Gothic fantasy. |
| POLYGON - Fantasy Kingdom / Fantasy Village / Knights / Fantasy Characters / Fantasy Rivals / Modular Fantasy Hero | Low | Medieval fantasy. |
| POLYGON - Elven Realm / Vikings / Viking Realm / Samurai / Samurai Empire / Pirate Pack / Pirate Realm (+ Pirate Isles Map) / Adventure Pack | Low | Themed fantasy and historical worlds. |
| POLYGON - Ancient Egypt / Ancient Empire / Western Pack / Western Frontier | Low | Historical worlds. |
| POLYGON - Frog Shrine / Legendary Chest | Low | Single set pieces (a legendary chest could be a loot gag). |

## POLYGON: horror and sci-fi

| Pack | Fit | Purpose |
| --- | --- | --- |
| POLYGON - Sci-Fi Worlds / Sci-Fi Cyber City / Sci-Fi City / Sci-Fi Space (+ Sci-Fi Outpost Map) | Med | AI Lord tech: servers, terminals, robots for AI-themed areas. |
| POLYGON - Sci-Fi Horror | Med | Dark corridors and creatures: the AI's lair. |
| POLYGON - Horror Mansion / Horror Asylum / Horror Carnival / Hearse Kit | Low | Scary reference zones for nights. |
| POLYGON - Werewolf | Med | A rigged werewolf: night monster. |
| POLYGON - Kaiju Pack | Low | Giant monsters: boss joke. |
| POLYGON - Halloween Horror Masks / Pumpkins (free) | Low | Seasonal props. |

## POLYGON: effects and utility

| Pack | Fit | Purpose |
| --- | --- | --- |
| POLYGON - Particle FX Pack | Med | Fire, smoke, blood, muzzle, magic in the Synty style (check against our VFX packs in ASSETS.md first). |
| POLYGON - Icons Pack | Med | 520 3D icons: map markers, world-space pickups. |
| POLYGON - Adult Face Plates (free) | Low | Facial expression plates for POLYGON characters. |
| POLYGON - Prototype Pack | Installed | Grey-box prototyping. |
| Seasonal and free kits (Easter, Gingerbread, Xmas, Santa, Holiday Gnomes, Holiday 2025, Valentine's, Pride sets, Wheelchair, Starter Pack) | Low | Event props and samples. |

## Bundles

Bundles (Animation Collection, Nature Biomes Seasons One and Two, map bundles, Humble bundles) are groupings of the packs above.
