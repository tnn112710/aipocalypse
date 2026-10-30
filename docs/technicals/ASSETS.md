# Third-party content packs

Every art, animation, UI, VFX and audio pack in the project, grouped by kind, one row per package. Code plugins and tools are in [PLUGINS.md](PLUGINS.md). A pack that is both (Feel, Damage Numbers Pro, Stylized Water 3, MicroVerse) lives there and is only linked here. **Check this before building or dressing anything**, and **update it in the same change** whenever a pack is added, removed, or starts being used for something. Name each pack folder exactly (`Folder` column): `.claude/hooks/check-undeclared-packs.sh` flags any folder under `_StoreAssets/<Kind>/`, `Synty/`, `Synty/SidekickCharacters/Characters/` or `Plugins/` that neither file names.

Vendor folders (`Assets/_StoreAssets/`, `Assets/Synty/`) are never modified. Make variants, material copies or prefabs under `Assets/_Game/`. Paths below are relative to `Assets/_StoreAssets/` unless they start with `Assets/`.

**Not installed yet:** the project has a SyntyPass (every Synty pack). [SYNTY_CATALOG.md](SYNTY_CATALOG.md) lists the store's packs with their purpose and fit; when no installed pack covers a need, check it and suggest one (importing still needs the user's OK, then a row here).

**Before using assets for a task**, check `Assets/_StoreAssets/` for packs this file doesn't list yet (`bash .claude/hooks/check-undeclared-packs.sh`) and the Undecided rows. If one could be useful, ask the user for a decision (suggest a tag), add or update its row, then pick by tag.

## Priority tags

Each package is declared once, with one tag. **Notes** say what it's used for, what's in use, and any part that's off-limits (e.g. "avoid its particles").

| Tag | Meaning |
| --- | --- |
| **Must** | The only choice for what its row says it's used for. Don't use alternatives for that. |
| **Should** | The default pick. Using another pack instead needs a reason, written in its row. |
| **Could** | Fallback, only for what the Must/Should packs don't have. When you use one, add what you took to its row. |
| **Maybe** | Last resort, only when no Must/Should/Could pack has it, and ask first: expect a style mismatch. When you use one, add what you took to its row. |
| **Avoid** | Reference only. Nothing new under `Assets/_Game/` may point at it; existing uses are listed so they can be replaced. |
| **Undecided** | Not ranked yet. Ask before using it. The row may suggest a tag. |

Art style is the main reason for a ranking: **Raygeas is the main look**, BK supports it, and toon or very low-poly sets clash. Ask before bringing in a style not already used.

## Environment (3D)

Deep map detail (wind drivers, shader globals, material copies, prefab names) is in [ENVIRONMENT.md](ENVIRONMENT.md) and the `tools-unity-map-generation` skill (`references/art-packs.md`).

### Nature: trees, plants, rocks, mountains, terrain textures

| Pack | Tag | Folder | Notes |
| --- | --- | --- | --- |
| Raygeas Komorebi Nature, HIGHLANDS, Grasslands | Should | `Env/Raygeas` | Main art set of the world map. Komorebi: nature only (no torii, shrines, lanterns or bamboo); sakura only around the home pond plus a few lake accents. Keep the three wind drivers aligned. Never place Grasslands `Other/Controller.prefab` (a demo player rig that locks the cursor). `Shared Assets/Scripts/WindController.cs` and `WindSettings.cs` start with an unguarded `using UnityEditor;`, which likely breaks player builds (vendor edit needed, not done). Their audio is also in use: footsteps and owls (HIGHLANDS), wind (Grasslands). |
| Raygeas AZURE Nature, Stylized Terrain Textures | Could | `Env/Raygeas` | Same author and style as the main set. Not used. |
| BK Pure Nature Meadows, Asian Valley | Could | `Env/BK` | In use: meadow lawn and flowers (through first-party material copies), meadow stones, reeds, butterflies; Asian Valley `Fx/WaterSplash` is the waterfall mist (shadows off, sortingOrder 1). BK plants need `BKFoliageGlobals`; never place `BK_EnvironmentManager` (it draws clouds). |
| BK Pure Nature Mountains | Could | `Env/BK` | In use: flattened hills and the far range of the world map boundary, at scale 20,000–45,000 (the meshes are ~0.03 m at scale 1). |
| BK Pure Nature Jungle, Fantasy Forest, Swamp | Could | `Env/BK` | Same family as the BK support set. Not used. |
| SICS Toon Fantasy Nature, Toon Series, ToonScapes | Maybe | `Env/SICS` | Toon style clashes. In use: two particle prefabs on URP Particles shaders, `TEM_Dust_01A` (gold motes) and `TFF_Fireflies_01A`, tweaked per instance in `Atmosphere.Particles` (culling Pause). |
| ANGRY MESH Stylized Pack – Meadow | Avoid | `Env/ANGRY MESH` | **Still in use** on the WorldMap near boundary range until it is replaced: don't add new uses. Current use: rocks scaled ×14–25 as granite domes, small mountains behind them, through the grey copies `Environment/Mountains/AM_*.mat` and `ASPTopLayerGlobals` (not the vendor global scripts). Remove the mountains' 14k-triangle mesh colliders beyond the walls. |
| PolyartStudio Dreamscape (Campsite, Castle, Meadows), Farmlands | Avoid | `Env/PolyartStudio` | Style clashes. Not used. |

### Buildings, village, home base

| Pack | Tag | Folder | Notes |
| --- | --- | --- | --- |
| Raygeas Lakefell Village | Maybe | `Env/Raygeas` | In use: home base house, fence, bridge; its audio (birds, kneel). Avoid its particles and `Buildings (Combined Meshes)`: they use the built-in `Particles/Standard Unlit` shader (magenta in URP). |
| Animpic Polygon-Mega Survival Construction | Maybe | `Env/Animpic` | Low-poly structures. Not used. |

### Props, pickups, tools

| Pack | Tag | Folder | Notes |
| --- | --- | --- | --- |
| Animpic Polygon-Mega Survival, Survival Kit, Survival Tools, Medical Kit, POLY-SurvivalWorkshop, Poly-Survival Food | Should | `Env/Animpic` | Item and prop models. In use: the kerosene lamp (belt lantern) and a few workshop/kit models. Avoid its `Standard Assets/Environment/Water*` (built-in). |
| NewLua Stylized Farm Crops | Could | `Env/NewLua Studios` | Crops. Not used; the home base has no crops (user). |
| Synty Polygon Generic | Avoid | `Assets/Synty/PolygonGeneric` | Still in use: one FX texture (`Textures/FX/Generic_Circle_Soft_01`). |
| Synty Polygon Prototype | Avoid | `Assets/Synty/PolygonPrototype` | Not used. |

### Terrain heights (editor content)

| Pack | Tag | Folder | Notes |
| --- | --- | --- | --- |
| Rowlan height stamps (Alpine Mountains, Highlands, Jungle Rivers, Meadows, Swamp, Valleys) | Should | `Env/Rowlan/Terrain/Stamps` | 4K R16 heightmaps for MicroVerse stamps, used by the WorldMap generator (Highlands and Meadows today). Rowlan's tools are in PLUGINS.md. |
| MicroVerse presets (BK Pure Nature 2) | Could | `Env/MicroVerse-Presets` | Only the 6 installed BK biomes resolve. |

## Characters & animation

| Pack | Tag | Folder | Notes |
| --- | --- | --- | --- |
| VR Low Poly Hand (Left & Right) | Should | `Characters/VR Low Poly Hand (Left & Right)` | First-person hands: mesh of `_Core/HandSystem/Prefabs/FPHand_Left/Right` ([HANDS.md](HANDS.md)). |
| Malbers Animations (Animal Controller, Animals Packs Forest and Pets, Common) | Could | `Characters/Malbers Animations` | In use: `Common/Audio` clips as placeholder SFX (dry fire, jam, kill confirm, melee swing/hits, jump, land, body fall, repair accents); `Common/Human Anims` humanoid hit reactions (`Hit/Hit_All.fbx`, light and heavy × four sides) and deaths (`Deaths/H_Death1`, `H_Death3`) in `Goblin.controller`, because the Synty locomotion packs have none (`H_Death2.anim` throws the body off its origin: unused). Animals: ask first; the Animal Controller is a heavy per-instance controller, fine for a few unique animals (dog, cat), never crowds, and its own sounds stay off (BroAudio owns audio). Fishing add-on: PLUGINS.md. |
| Synty Sidekick Characters | Undecided | `Assets/Synty/SidekickCharacters` | Humans, enemies. **Test use (user-approved):** the two Goblin enemies (`Content/Enemies/Goblin/`), built from `.sk` files by `SidekickKit` into first-party prefabs (no vendor prefab or script referenced). Its `Resources/` (~180 MB of parts) ships in every player build while the pack is in the project: decide before a release build. Skill: `tools-unity-synty-characters`. |
| Synty Sidekick character packs (Goblin Fighters, Goblin Species) | Undecided | `Assets/Synty/SidekickCharacters/Characters/GoblinFighters`, `Assets/Synty/SidekickCharacters/Characters/GoblinSpecies` | Ready-made Sidekick characters, 5 each, built on the Sidekick tool above. Use their `.sk` files, not their prefabs (old-tool output: bloated bone arrays, readable 5–15 MB meshes). Test use: Goblin looks 01–05 come from `GoblinFighter_01…05.sk`. More packs land in `Characters/` as they are imported. Not used. |
| Synty Sidekick Apocalypse Zombies | Could | `Assets/Synty/SidekickCharacters/Characters/ApocalypseZombies` | 5 ready-made Sidekick zombies (`ApocalypseZombie_01…05`, species Zombie), for zombie enemies built like the Goblin. Its 86 parts are all in the Sidekick tool's `Resources/` and DB 1.0.2 knows the species, so `SidekickKit.Build` works from the `.sk` files. Use the `.sk` files, not the prefabs (old-tool output, as above). Clips: Kevin Iglesias Zombie (Could) or the humanoid packs. Not used. |
| Synty Animation Base Locomotion, Goblin Locomotion | Undecided | `Assets/Synty/AnimationBaseLocomotion`, `Assets/Synty/AnimationGoblinLocomotion` | Humanoid locomotion clips in Sidekick (`A_MOD_*`) and Polygon formats. **Test use (user-approved):** `Goblin.controller` uses Goblin Locomotion's Sidekick idle, walk, run, fidgets and the additive look and lean clips (non-root-motion). Base Locomotion: not used. Their sample scripts and prefabs never go in `_Game` (own input actions, `Update`-driven controller). |
| Synty Animation Bow Combat | Could | `Assets/Synty/AnimationBowCombat` | Archery clips for goblin archers or any bow user, 499 per format (Sidekick `A_MOD_BOW_*`, Polygon `A_POLY_BOW_*`). **Character (190):** five stances (Standing, Crouching, Kneeling, LeanBack, Sliding), each with enter / exit, aim `Undrawn` / `Drawn` loops, `ToDrawn`, `Shoot_ToAiming`, `Shoot_Reload`. Also dive and leap shots, crouch rolls, a bow block / parry / club swing, equip / stow, and directional hits (`React`, `Stagger`) and deaths in Masc / Femn. Five two-frame aim additives (`Stand_Aiming_Undrawn_Additive_N/L90/R90/U90/D90`) aim up / down / sideways. **Bow (309):** every clip with a bow has a twin of the same name and length per bow type in `Cmp/`, `Lng/`, `Rcv/`, played on the bow's own Animator in sync (the string draws). **Models:** 14 bow prefabs (each with an Animator), unrigged copies of the 3 bows in `Models/Bows/Unrigged/` (no Animator, cheaper for crowds, the string stays still), 8 quivers, 23 arrows, a target. Bow, arrow and sample materials are Built-in Standard (pink in URP): make URP copies under `_Game`. The `_FPV_` clips pose the bow for a head-mounted camera; our player has first-party hands ([HANDS.md](HANDS.md)), so they don't apply. Gallery scenes and timelines stay out of `_Game`. Not used. |
| Synty Animation Sword Combat | Could | `Assets/Synty/AnimationSwordCombat` | Sword combat set, 118 clips per format (Sidekick `A_MOD_SWD_*`; Polygon keeps the old names `A_<Type>_…_Sword`, with `_RootMotion` for `_RM`). Light (one-hand) and heavy (two-hand) attacks: two 3-hit combos (`A`→`B`→`C`, each step ends ready for the next wind-up or its `ReturnToIdle` clip), fencing, stab, flourish, leaping attack. A looping block with left / front / right parries, parry break and counters (shove, pommel strike), 16 dodges, hits (`React`, `Stagger`, `KnockDown`, `Stun`), 4 directional deaths plus `_Pose` end holds, combat and menacing idles, draw / sheathe. **Every attack FBX also holds `_WindUp`, `_Hit` and `_FollowThrough` sub-clips**: the `_Hit` range is the damage window to time an enemy swing. Clips start and end on the pack's own neutral combat idle (`Idle/Base/…Idle_Base_Neut`); draw / sheathe link it to the Synty Masc / Femn base idle. Overlaps Kevin Iglesias Melee (Should, in use on the goblins): take what that lacks (combos with timing sub-clips, parries, counters). Not used. |
| Synty Animation Idles | Could | `Assets/Synty/AnimationIdles` | 330 idle clips per format (`A_MOD_IDL_*`, `A_POLY_IDL_*`), Masculine and Feminine, no root motion. Categories: arms folded, bored, check watch, drink, eat, hands on hips, head nod / shake, inspect, lean (on walls), look around, pick nose, play with hair, plead, point, posture (aggressive, slumped, shy, dramatic), pray, stretch, sway, thoughtful, use phone, wave, weight shift, yawn. `Actions/` are one-offs that start and end on their category's pose; `Stances/` are `Enter` / `Loop` / `Exit` sets from the base idle (`Base/Stances/`). Prop clips (cup, food, phone, comb) animate `prop_r`. For NPCs, base dwellers and idle fidgets on humanoid enemies; overlaps the Kevin Iglesias Basic Motions idles (Should). Sample props, gallery scenes and Built-in materials stay out of `_Game`. Not used. |
| Synty Animation Emotes and Taunts | Could | `Assets/Synty/AnimationEmotesAndTaunts` | 140 clips per format (`A_MOD_EMOT_*`, `A_POLY_EMOT_*`), Masculine and Feminine: affection, aggressive, angry, celebrate, dance, greet, happy, reproach, sad, sporty (a backflip and a martial-arts flourish, its only root-motion clips) and taunt. They start and end on the Synty Masc / Femn base idle. For enemy taunts and NPC reactions; Kevin Iglesias Basic Motions (Should) has the everyday emotes. Not used. |
| Agor2012 (Modular Robot, Robot X-26-2, Tracked Robot, Zombie) | Undecided | `Characters/Agor2012` | Sci-fi robots that fit the AI theme, plus a zombie; URP shader graph materials (`Robot_URP_Shader`), HDRP `.unitypackage`s not imported. Modular Robot: separate parts and weapons on Generic rigs (41 prefabs). Tracked Robot: one model with 20 Generic clips and a demo `Demo/Scripts/RobotMovement.cs` (stays out of `_Game`). The zombie is a Generic mesh without clips, so humanoid clips don't apply. Suggested: Could for robot enemy kinds, once their style is checked next to Raygeas. Not used. |
| DeskDragons (Spider 1–3) | Undecided | `Characters/DeskDragons` | Three animated spiders, Generic rig, ~19 clips each in the FBX (alert, attacks, death, dodge, …), URP Lit. Suggested: Could for a creature enemy kind. Not used. |
| Dungeon Mason Monster Swarm Survivor Polyart | Undecided | `Characters/Dungeon Mason` | Ten low-poly swarm monsters (`Swarm01…10`), Generic rigs, each with idle, 4-direction move, attack, get-hit and die clips and a demo controller; URP Lit. Made for swarms, so cheap in crowds. Suggested: Could for crowd enemies, once the polyart style is checked next to Raygeas. Not used. |
| Synty Polygon Goblin War Camp | Undecided | `Assets/Synty/PolygonGoblinWarCamp` | Fantasy goblin camp: characters, buildings, props, vehicles, weapons, FX (~600 prefabs, URP Synty shader). Off-style for the apocalypse setting; goblin characters pair with Goblin Locomotion. Its demo scripts (`SimpleRotateGoblin`, `SyntyWaterBobGoblin`) stay out of `_Game`. Not used. |
| Kevin Iglesias Human Animations: Melee 2.1, Basic Motions 2.5, Soldier 2.0, Crafting 2.2, Throwing 2.0 | Should | `Animation/Kevin Iglesias` | Humanoid clips that retarget onto any humanoid model, the default body animation for humans, companions and humanoid enemies. All the Human packs share one tree, `Human Animations/Animations/{Male,Female}/{Combat,Idles,Misc,Movement,Social,Work}/…` plus `Masked Poses/`; a clip several packs ship sits there once. Each pack's clip list is its PDF in `Human Animations/`. **Melee:** 1H, 2H, dual wield, polearm, shield, unarmed, kicks, parries, combat idles and deaths. **Basic Motions:** 8-direction walk / run / sprint / strafe / crouch, turns, jump, roll, slide, swim, idles, emotes and talk, sit / sleep, eat / drink, loot / open, knockdown. **Soldier:** pistol, dual pistols, rifle, assault rifle and bazooka (aim, precise aim, shoot, reload), grenade throws, prone and crawl, sheathe / unsheathe, Damage01–05 and Death01–05. **Crafting:** chop, mine, gather, farm (plow, seed, water), fish, hammer, skin, carry, pick up / drop. **Throwing:** rock, big rock, spear, ball, boomerang and weapon throws. Naming: `HumanM` / `HumanF` masculine / feminine rig, `Human@` shared by both; `_L` / `_R` hand; `[RM]` is the root-motion copy, so take the in-place clip (the mover moves our bodies); `… - Begin` / `… - Loop` / `… - Stop` split one action; `A-B` clips blend idle A into B. Avatar masks in `Models/Avatar Masks/` (full and upper body, head, each arm and hand). **In use:** `Goblin.controller`'s sword swings (`Male/Combat/1H/HumanM@Attack1H02_R`, `03_R`, `04_R`, the right-hand set the Melee sword demo uses), combat idle between swings (`HumanM@CombatIdle1H01`) and right-hand grip (`Masked Poses/Human@ObjectGripHands01` with `Models/Avatar Masks/Hands/Human Hand Right Mask.mask`). Demo scenes, dummy models and materials, shaders and scripts (`Scripts/SpineProxy.cs`, `HumanSoldierController`, `HumanThrowingAnimationsController`, `HumanBasicMotionsDemo`…) stay out of `_Game`. Hands off by an arm-length mismatch on two-handed holds: Kevin Iglesias IK Helper Tool (PLUGINS.md). |
| Kevin Iglesias Human Spellcasting Animations 2.0 | Could | `Animation/Kevin Iglesias` | Same tree and naming as the row above (`Combat/Spellcasting/`, PDF in `Human Animations/`): casting idles, one- and two-hand magic attacks, ground and omni attacks, block, dodge, book holds. Magic is off-theme; a fallback for something like a goblin shaman. Not used. |
| Kevin Iglesias Zombie Animations, Humanoid Giant | Could | `Animation/Kevin Iglesias/Zombie Animations`, `Animation/Kevin Iglesias/Characters/Humanoid Giant` | Humanoid clips on their own dummy models (grey placeholders, not game art: clips only). **Zombie** (`Animations/Zombie@*`): Attack01–03, Damage01–03, Death01_A, Idle01 with four idle actions, Walk01 (no run), Scream01, EatingCorpse01, Spawn01 / 02 (rise from the ground, drop from mid-air), Stun01, Jump01, Victory01, Lose01. **Giant** (`Animations/Giant@*`, plus `Combat/` and `Movement/`): 8-direction walk and run (`[RM]` copies), idle actions, unarmed attacks, object / rock throw, stun, damage, deaths, spawn, win and lose sequences. For undead or shambler enemies and a brute or boss. Giant clips are authored for giant proportions: check them on the target model with `CharacterKit.Snapshot` before using them on a normal-sized one. Demo scenes, controllers, `ThrowRockScript.cs` and materials stay out of `_Game`. Not used. |
| KINEMATION FPS Animation Pack | Avoid | `Animation/KINEMATION/FPSAnimationPack` | Our first-person weapon animation is first-party ([HANDS.md](HANDS.md), [WEAPONS.md](WEAPONS.md)). The other KINEMATION tools: PLUGINS.md. |

## Weapons

| Pack | Tag | Folder | Notes |
| --- | --- | --- | --- |
| Castle Bravo Low Poly Weapon Bundle Packs 1–3, 5 | Should | `Weapons/Castle Bravo` (`Low Poly Weapon Bundle Pack 1`, `2`, `3`, `5`) | Gun models. Pack 5 adds 37 guns in `Prefabs/Low Poly Series V 1…7/Weapons/` (modern, Cold War `CW_*` and WWII `WWII_*`: ARs, SMGs, pistols, shotguns, DMRs, recon and battle rifles, an LMG, a launcher; `Weapon_PreSet` holds the fully attached versions), plus attachments, bullets, grenades, mines, a combat knife and shovel, small items (radio, tablet, armor plate, stim shot, key cards, dogtags, charms), gun walls, tables and cases, and 222 icon sprites in `UI/`; its `Previous/` folder repeats earlier series' attachments and bullets. URP Lit materials, same atlas style as Packs 1–3. Each gun is a `Gun_Base` variant holding an unpacked copy of the store model (store prefabs untouched), shadows off, no colliders ([WEAPONS.md](WEAPONS.md#gun-clips-and-constraints)). In use (Pack 1): AR_B (AK47), SMG_B (MP5), Pistol_E (Pistol), ShotGun_C (PumpShotgun), ShotGun_E (DoubleBarrel). |
| Animpic Poly-Survival Melee Weapons | Could | `Env/Animpic` | Melee models, used like the gun models. In use: fire axe; the goblins' sword (`SM_Handcrafted_Sword` copied into `Content/Enemies/Goblin/Goblin_Sword.prefab`, no collider). |
| Animpic Polygon-Mega Weapons Kit | Could | `Env/Animpic` | For guns the bundles lack. |
| ArmoryCore | Undecided | `Weapons/ArmoryCore` | Low-poly weapon models plus its own weapon system (`Scripts/`, `Definitions/`). Never use its scripts or definitions: ours is `_Core/WeaponSystem` ([WEAPONS.md](WEAPONS.md)). For the models, check the style against the bundles first. |

## UI

| Pack | Tag | Folder | Notes |
| --- | --- | --- | --- |
| TextMesh Pro samples | Must | `Assets/Samples/TextMesh Pro` | TMP SDF shaders and fonts. LiberationSans SDF is the dev console's font (`Data/Debug/ConsoleStyle.asset`, drawn monospaced with `<mspace>`). |
| Synty Interface Apocalypse HUD | Should | `Assets/Synty/InterfaceApocalypseHUD` | HUD art and item icons. **Sprites and the SairaCondensed TMP fonts only**, never its prefabs or sample scripts. Widgets reference the sprites through style assets ([UI.md](UI.md)); the repair QTE uses its ring/bar/panel sprites; all current item icons come from `Sprites/Icons_Weapons`. |
| Synty Interface Core | Should | `Assets/Synty/InterfaceCore` | Input prompts: the hotbar key cap uses `Sprites/Icons_Input`. |
| Crosshairs | Should | `UI/Crosshairs` | Crosshair sprites. Not used yet: the crosshair today is a 6×6 dot from the built-in Knob sprite ([UI.md](UI.md)). |
| Survival Flat Icons (Kat Grabowska), Survival flat icons, Flat resource and craft icons, Flat skill and ability icons | Should | `UI/Survival Flat Icons by Kat Grabowska`, `UI/Survival_flat_icons`, `UI/Flat_resource_and_craft_icons`, `UI/Flat_Skill_and_Ability_icons` | Flat-style icons (items, resources, crafting, skills). Not used yet. Don't mix sets within one screen. |
| Layer Lab GUI Pro – Survival Clean | Maybe | `UI/Layer Lab` | Full UI kit for menus and inventory screens; overlaps the Synty HUD style. Not used. |

## VFX

| Pack | Tag | Folder | Notes |
| --- | --- | --- | --- |
| Gabriel Aguiar (Unique Projectiles Vol 1–5, Toon Projectiles, Magic Abilities Vol 2, Markers & Pointers, Sci-Fi) | Should | `VFX/GabrielAguiarProductions` | Weapon, ability and marker effects. In use (Projectiles Vol 1): our muzzle flash, tracer and impact prefabs in `Content/Weapons/_Shared/Effects/` use its materials (the tracer line uses its `Line001` texture; the old `MuzzleFlash` prefab is kept but the guns use Hovl's flash), pooled through `EffectPool`, never instantiated per shot. |
| Hovl Studio (Muzzle flashes, AAA Projectiles, 3D Lasers, Auras, Background VFX, Fireworks, Glowing orbs, HQ Portals, Snow VFX, Toon projectiles) | Could | `VFX/Hovl Studio` | In use: `Muzzle flashes/Prefabs/For bloom/Muzzle flash 3` is the guns' flash (`MuzzleFlash_Hovl3` in `Content/Weapons/_Shared/Effects/`), because the Gabriel Aguiar packs have no gun muzzle flash, only magic and projectile ones. Its materials use the built-in `Particles/Standard Unlit` shader, so the prefab uses URP Particles/Unlit copies (`MuzzleFlash_*.mat`). |
| Kroma | Maybe | `GameFeel/Kroma` | In use: `Kroma_Particle_Dandelions_Prefab` as meadow seed fluff, with `KromaCameraFollow` and `KromaParticleController` disabled and collision off. Avoid `Dust_Glow` (emission 191) unless through a material copy, and Kroma's zone, lighting and LUT tools. |
| Unluck Software (Insect Particles, Stylized Weather, Simple Particle Scaler) | Maybe | `VFX/Unluck Software` | Not used. Weather will be ours ([DAYNIGHT.md](DAYNIGHT.md)): its particles at most, never its weather controller. |

## Sky

Not vendor packs: source images converted into `Assets/_Game/Data/Environment/TimeOfDay/StarMap/` by the scripts in `AgentScripts/DayNight/StarMap/` (README there). How the sky uses them: [DAYNIGHT.md](DAYNIGHT.md).

| Source | Tag | File | Notes |
| --- | --- | --- | --- |
| NASA SVS Deep Star Maps 2020 (`starmap_2020_8k.exr`, real sky from Hipparcos, Tycho-2 and Gaia DR2) | Should | `StarMap_NASA2020.png` | The photographic real sky. Converted by `convert_exr_starmap.py`. NASA imagery is free to use; credit it, e.g. "Star map: NASA/Goddard Space Flight Center Scientific Visualization Studio; Gaia DR2: ESA/Gaia/DPAC". Check the exact credit line on the SVS page before shipping. |
| NASA SVS Deep Star Maps 2020, stylized | Should | `StarMap_NASA2020_Stylized.png` | The same real stars at the same pixels, with a smoothed violet-pink Milky Way and clean tiered stars, made from `StarMap_NASA2020.png` by `stylize_starmap.py`. Same credit as the row above. In use on `SkyboxPlus_Sky.mat`; set Star Map back to `StarMap_NASA2020` for the photographic sky. |
| NASA SVS Deep Star Maps 2020, stylized 2 and 3 | Should | `StarMap_NASA2020_Stylized2.png`, `StarMap_NASA2020_Stylized3.png` | Like the stylized version, plus the source's own faint stars added back (`--pepper 1.6`; without them the sky reads empty). 2: violet palette; 3: teal palette with a gold core. Same credit. |
| Procedural (`generate_starmap.py`) | Could | `StarMap.png` | Our generated sky (styles natural, vivid, subtle, rift). |
| AI-generated panoramas | Avoid | `StarMap2…6.png`, `StarMap2…6_8K.png` (upscaled by `upscale_starmap.py`) | Trial images; AI-generated content belongs in `Assets/_Placeholder/` and does not ship. |

## Audio

All sound plays through BroAudio entities in `Assets/_Game/Data/Audio/` (`tools-unity-broaudio`). No final audio is chosen yet. Clips in use today come from `Assets/_Placeholder/Audio` (below), Malbers `Common/Audio`, the Raygeas packs (their rows above) and PlaceHolder Inc (below).

| Pack | Tag | Folder | Notes |
| --- | --- | --- | --- |
| ithappy Nature Sound FX | Should | `Audio/ithappy` | Nature ambience. Not used yet. |
| PlaceHolder Inc (Environment Sounds, Gun Sounds Vol 1, Interface & Item Sounds Lite, Survival Game Sounds Lite) | Should | `Audio/PlaceHolder Inc` | Environment, gun, UI and survival sounds. In use: hit marker (`Interface Click 6-1`). |
| Gamemaster Audio Pro Sound Collection | Could | `Audio/Gamemaster Audio - Pro Sound Collection` | General library (guns, UI, zombies, doors, foley, …). Not used. |

## Placeholder content

| Folder | Tag | Notes |
| --- | --- | --- |
| `Assets/_Placeholder/` | Avoid | Temporary storage for placeholder and AI-generated assets. Nothing ships from here and nothing new should point at it; replace these uses with real assets: `Audio/Weapons` (gun fire, equip, reload and bolt sounds, repair QTE mechanism/miss), `Audio/Music` (death music), `Audio/Voice` (death groans). |
