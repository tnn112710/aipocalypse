# Third-party content packs

Every art, animation, UI, VFX and audio pack in the project, grouped by kind, one row per package. Code plugins and tools are in [PLUGINS.md](PLUGINS.md). A pack that is both (Feel, Damage Numbers Pro, Stylized Water 3, MicroVerse) lives there and is only linked here. **Check this before building or dressing anything**, and **update it in the same change** whenever a pack is added, removed, or starts being used for something. Name each pack folder exactly (`Folder` column): `.claude/hooks/check-undeclared-packs.sh` flags any folder under `_StoreAssets/<Kind>/`, `Synty/` or `Plugins/` that neither file names.

Vendor folders (`Assets/_StoreAssets/`, `Assets/Synty/`) are never modified. Make variants, material copies or prefabs under `Assets/_Game/`. Paths below are relative to `Assets/_StoreAssets/` unless they start with `Assets/`.

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
| Malbers Animations (Animal Controller, Animals Packs Forest and Pets, Common) | Could | `Characters/Malbers Animations` | In use: `Common/Audio` clips as placeholder SFX (dry fire, jam, kill confirm, melee swing/hits, jump, land, body fall, repair accents). Animals: ask first; the Animal Controller is a heavy per-instance controller, fine for a few unique animals (dog, cat), never crowds, and its own sounds stay off (BroAudio owns audio). Fishing add-on: PLUGINS.md. |
| Synty Sidekick Characters | Undecided | `Assets/Synty/SidekickCharacters` | Humans, enemies. No animated characters yet. |
| Synty Animation Base Locomotion, Goblin Locomotion | Undecided | `Assets/Synty/AnimationBaseLocomotion`, `Assets/Synty/AnimationGoblinLocomotion` | Locomotion animations. No animated characters yet. |
| KINEMATION FPS Animation Pack | Avoid | `Animation/KINEMATION/FPSAnimationPack` | Our first-person weapon animation is first-party ([HANDS.md](HANDS.md), [WEAPONS.md](WEAPONS.md)). The other KINEMATION tools: PLUGINS.md. |

## Weapons

| Pack | Tag | Folder | Notes |
| --- | --- | --- | --- |
| Low Poly Weapon Bundle Packs 1–3 | Should | `Weapons/Low Poly Weapon Bundle Pack 1`, `Low Poly Weapon Bundle Pack 2`, `Low Poly Weapon Bundle Pack 3` | Gun models. Each gun is a `Gun_Base` variant holding an unpacked copy of the store model (store prefabs untouched), shadows off, no colliders ([WEAPONS.md](WEAPONS.md#gun-clips-and-constraints)). In use (Pack 1): AR_B (AK47), SMG_B (MP5), Pistol_E (Pistol), ShotGun_C (PumpShotgun), ShotGun_E (DoubleBarrel). |
| Animpic Poly-Survival Melee Weapons | Could | `Env/Animpic` | Melee models, used like the gun models. In use: fire axe. |
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
| Gabriel Aguiar (Unique Projectiles Vol 1–5, Toon Projectiles, Magic Abilities Vol 2, Markers & Pointers, Sci-Fi) | Should | `VFX/GabrielAguiarProductions` | Weapon, ability and marker effects. In use (Projectiles Vol 1): our muzzle flash, tracer and impact prefabs in `Content/Weapons/_Shared/Effects/` use its materials, pooled through `EffectPool`, never instantiated per shot. |
| Hovl Studio (Muzzle flashes, AAA Projectiles, 3D Lasers, Auras, Background VFX, Fireworks, Glowing orbs, HQ Portals, Snow VFX, Toon projectiles) | Could | `VFX/Hovl Studio` | Not used. |
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

All sound plays through BroAudio entities in `Assets/_Game/Data/Audio/` (`tools-unity-broaudio`). No final audio is chosen yet. Clips in use today come from `Assets/_Placeholder/Audio` (below), Malbers `Common/Audio` and the Raygeas packs (their rows above).

| Pack | Tag | Folder | Notes |
| --- | --- | --- | --- |
| ithappy Nature Sound FX | Should | `Audio/ithappy` | Nature ambience. Not used yet. |
| PlaceHolder Inc (Environment Sounds, Gun Sounds Vol 1, Interface & Item Sounds Lite, Survival Game Sounds Lite) | Should | `Audio/PlaceHolder Inc` | Environment, gun, UI and survival sounds. Not used yet. |
| Gamemaster Audio Pro Sound Collection | Could | `Audio/Gamemaster Audio - Pro Sound Collection` | General library (guns, UI, zombies, doors, foley, …). Not used. |

## Placeholder content

| Folder | Tag | Notes |
| --- | --- | --- |
| `Assets/_Placeholder/` | Avoid | Temporary storage for placeholder and AI-generated assets. Nothing ships from here and nothing new should point at it; replace these uses with real assets: `Audio/Weapons` (gun fire, equip, reload and bolt sounds, repair QTE mechanism/miss), `Audio/Music` (death music), `Audio/Voice` (death groans). |
