# Third-party content packs

Every art, animation, UI, VFX and audio pack in the project, grouped by kind, with which one to use for each purpose. Code plugins and tools are in [PLUGINS.md](PLUGINS.md). A pack that is both (Feel, Damage Numbers Pro, Stylized Water 3, MicroVerse) lives there and is only linked here. **Check this before building or dressing anything**, and **update it in the same change** whenever a pack is added, removed, or starts being used for something.

Vendor folders (`Assets/_StoreAssets/`, `Assets/Synty/`) are never modified. Make variants, material copies or prefabs under `Assets/_Game/`. Paths below are relative to `Assets/_StoreAssets/` unless they start with `Assets/`.

## Priority tags

Tags rank a pack **for one purpose**, not the whole pack: the same pack can be Must in one group and Avoid in another.

| Tag | Meaning |
| --- | --- |
| **Must** | The only choice for this purpose. Don't use alternatives for it. At most one Must per purpose. |
| **Should** | The default pick. Using another pack needs a reason, written in its row. |
| **Could** | Fallback, only for what the Must/Should packs don't have. When you use one, add what you took to its row. |
| **Maybe** | Last resort, only when no Must/Should/Could pack has it, and ask first: expect a style mismatch. When you use one, add what you took to its row. |
| **Avoid** | Reference only. Nothing under `Assets/_Game/` may point at it. |
| **Undecided** | Not ranked yet. Ask before using it. The row may suggest a tag. |

Art style is the main reason for a ranking: **Raygeas is the main look**, BK supports it, and toon or very low-poly sets clash. Ask before bringing in a style not already used.

## Environment (3D)

Deep map detail (wind drivers, shader globals, material copies, prefab names) is in [ENVIRONMENT.md](ENVIRONMENT.md) and the `tools-unity-map-generation` skill (`references/art-packs.md`).

### Nature: trees, plants, rocks, terrain textures

| Pack | Tag | Folder | Notes |
| --- | --- | --- | --- |
| Raygeas Komorebi Nature, HIGHLANDS, Grasslands | Should | `Env/Raygeas` | Main art set of the world map. Komorebi: nature only (no torii, shrines, lanterns or bamboo); sakura only around the home pond plus a few lake accents. Keep the three wind drivers aligned. Never place Grasslands `Other/Controller.prefab` (a demo player rig that locks the cursor). `Shared Assets/Scripts/WindController.cs` and `WindSettings.cs` start with an unguarded `using UnityEditor;`, which likely breaks player builds (vendor edit needed, not done). |
| BK Pure Nature Meadows, Asian Valley | Could | `Env/BK` | Support set: meadow lawn and flowers (through first-party material copies), meadow stones, reeds, butterflies. BK plants need `BKFoliageGlobals`; never place `BK_EnvironmentManager` (it draws clouds). |
| Raygeas AZURE Nature, Stylized Terrain Textures | Could | `Env/Raygeas` | Same author and style as the main set. Not used. |
| BK Pure Nature Jungle, Fantasy Forest, Swamp | Could | `Env/BK` | Same family as the BK support set. Not used. |
| SICS Toon Fantasy Nature, Toon Series, ToonScapes | Maybe | `Env/SICS` | Toon style clashes. Only two particle prefabs are used (see VFX). |
| PolyartStudio Dreamscape (Campsite, Castle, Meadows), Farmlands | Maybe | `Env/PolyartStudio` | Style clashes. Ignore its built-in folders and Post Processing v2 instructions. |

### Boundary

| Pack | Tag | Folder | Notes |
| --- | --- | --- | --- |
| ANGRY MESH Stylized Pack – Meadow | Avoid | `Env/ANGRY MESH` | **Still in use** on the WorldMap near boundary range until it is replaced: don't add new uses. Current use: rocks scaled ×14–25 as granite domes, small mountains behind them. Use the grey copies `Environment/Mountains/AM_*.mat` and `ASPTopLayerGlobals` (not the vendor global scripts). Remove the mountains' 14k-triangle mesh colliders beyond the walls. |
| BK Pure Nature Mountains | Could | `Env/BK` | Flattened hills and far range at scale 20,000–45,000 (the meshes are ~0.03 m at scale 1). |

### Buildings, village, home base

| Pack | Tag | Folder | Notes |
| --- | --- | --- | --- |
| Raygeas Lakefell Village | Must | `Env/Raygeas` | Home base house, fence, bridge. Avoid its particles and `Buildings (Combined Meshes)`: they use the built-in `Particles/Standard Unlit` shader (magenta in URP). |
| Animpic Polygon-Mega Survival Construction | Undecided | `Env/Animpic` | Low-poly. Suggest Could, for structures Lakefell lacks. |

### Props, pickups, tools

| Pack | Tag | Folder | Notes |
| --- | --- | --- | --- |
| Animpic Polygon-Mega Survival, Survival Kit, Survival Tools, Medical Kit, POLY-SurvivalWorkshop, Poly-Survival Food | Should | `Env/Animpic` | Item and prop models. In use: the kerosene lamp (belt lantern) and a few workshop/kit models. Its `Standard Assets/Environment/Water*` are built-in: Avoid. |
| Synty Polygon Generic | Could | `Assets/Synty/PolygonGeneric` | One FX texture in use (`Textures/FX/Generic_Circle_Soft_01`). |
| Synty Polygon Prototype | Could | `Assets/Synty/PolygonPrototype` | Blockout and prototype stand-ins only. |
| NewLua Stylized Farm Crops | Undecided | `Env/NewLua Studios` | Not used; the home base has no crops (user). Suggest Avoid until farming exists. |

### Terrain heights (editor content)

| Pack | Tag | Folder | Notes |
| --- | --- | --- | --- |
| Rowlan height stamps (Alpine Mountains, Highlands, Jungle Rivers, Meadows, Swamp, Valleys) | Must | `Env/Rowlan/Terrain/Stamps` | 4K R16 heightmaps for MicroVerse stamps, used by the WorldMap generator (Highlands and Meadows today). Rowlan's tools are in PLUGINS.md. |
| MicroVerse presets (BK Pure Nature 2) | Could | `Env/MicroVerse-Presets` | Only the 6 installed BK biomes resolve. |

## Characters & animation

| Purpose | Pack | Tag | Folder | Notes |
| --- | --- | --- | --- | --- |
| First-person hands | VR Low Poly Hand (Left & Right) | Must | `Characters/VR Low Poly Hand (Left & Right)` | Mesh of `_Core/HandSystem/Prefabs/FPHand_Left/Right` ([HANDS.md](HANDS.md)). |
| Humans, enemies | Synty Sidekick Characters | Undecided | `Assets/Synty/SidekickCharacters` | No animated characters yet. |
| Locomotion animations | Synty Animation Base Locomotion, Goblin Locomotion | Undecided | `Assets/Synty/Animation*` | No animated characters yet. |
| Animals | Malbers Animals Packs (Forest, Pets) | Undecided | `Characters/Malbers Animations/Animals Packs` | Models and animations for Malbers' Animal Controller (PLUGINS.md): a few unique animals, never crowds. |
| First-person weapon animations | KINEMATION FPS Animation Pack | Avoid | `Animation/KINEMATION` | Our weapon animation is first-party ([HANDS.md](HANDS.md), [WEAPONS.md](WEAPONS.md)). |

## Weapons

| Purpose | Pack | Tag | Folder | Notes |
| --- | --- | --- | --- | --- |
| Gun models | Low Poly Weapon Bundle Packs 1–3 | Must | `Weapons/Low Poly Weapon Bundle Pack 1–3` | Each gun is a `Gun_Base` variant holding an unpacked copy of the store model (store prefabs untouched), shadows off, no colliders ([WEAPONS.md](WEAPONS.md#gun-clips-and-constraints)). In use (Pack 1): AR_B (AK47), SMG_B (MP5), Pistol_E (Pistol), ShotGun_C (PumpShotgun), ShotGun_E (DoubleBarrel). New guns come from Packs 1–3 first. |
| Gun models | Animpic Polygon-Mega Weapons Kit | Could | `Env/Animpic` | For guns the bundles lack. |
| Gun models | ArmoryCore models | Undecided | `Weapons/ArmoryCore/Models` | Check the style against the bundles first. Its weapon scripts are Avoid (PLUGINS.md). |
| Melee models | Animpic Poly-Survival Melee Weapons | Must | `Env/Animpic` | Used like the gun models. In use: fire axe. |

## UI

| Purpose | Pack | Tag | Folder | Notes |
| --- | --- | --- | --- | --- |
| HUD art (frames, bars, panels, fonts) | Synty Interface Apocalypse HUD | Must | `Assets/Synty/InterfaceApocalypseHUD` | **Sprites and the SairaCondensed TMP fonts only**, never its prefabs or sample scripts. Widgets reference the sprites through style assets ([UI.md](UI.md)); the repair QTE uses its ring/bar/panel sprites. |
| Input prompts | Synty Interface Core | Must | `Assets/Synty/InterfaceCore` | The hotbar key cap uses `Sprites/Icons_Input`. |
| Item icons | Synty Interface Apocalypse HUD `Icons_Weapons` | Must | `Assets/Synty/InterfaceApocalypseHUD/Sprites` | All current item icons. |
| Item icons | Survival Flat Icons (Kat Grabowska), Survival_flat_icons, Flat resource and craft icons, Flat skill and ability icons | Undecided | `UI/` | Flat style. Suggest Could for icons Synty lacks, and pick one set so styles don't mix. |
| Menus, inventory screens | Layer Lab GUI Pro – Survival Clean | Undecided | `UI/Layer Lab` | Full UI kit; overlaps the Synty HUD style. Decide before the first menu. |
| Crosshair | Crosshairs | Could | `UI/Crosshairs` | The crosshair today is a 6×6 dot from the built-in Knob sprite ([UI.md](UI.md)). |
| Text shaders | TextMesh Pro samples | Must | `Assets/Samples/TextMesh Pro` | TMP SDF shaders and fonts. |

## VFX

| Purpose | Pack | Tag | Folder | Notes |
| --- | --- | --- | --- | --- |
| Weapon effects (muzzle flash, tracer, impact) | Gabriel Aguiar Unique Projectiles Vol 1 | Must | `VFX/GabrielAguiarProductions` | Our prefabs in `Content/Weapons/_Shared/Effects/` use its materials, pooled through `EffectPool`, never instantiated per shot. |
| Weapon, ability and marker effects | Gabriel Aguiar Unique Projectiles Vol 2–5, Toon Projectiles, Magic Abilities Vol 2, Markers & Pointers, Sci-Fi | Should | `VFX/GabrielAguiarProductions` | Same author and material setup as Vol 1. Not used yet. |
| Weapon effects | Hovl Studio Muzzle flashes, AAA Projectiles Vol 1 | Could | `VFX/Hovl Studio` | Not used. |
| Other effects | Hovl Studio 3D Lasers, Auras, Background VFX, Fireworks, Glowing orbs, HQ Portals, Snow VFX, Toon projectiles | Undecided | `VFX/Hovl Studio` | Not used. |
| Ambient particles | SICS `TEM_Dust_01A` (gold motes), `TFF_Fireflies_01A` | Maybe | `Env/SICS` | In use on the world map (URP Particles shaders). Tweaks per instance in `Atmosphere.Particles` (culling Pause). |
| Ambient particles | Kroma `Kroma_Particle_Dandelions_Prefab` | Should | `GameFeel/Kroma` | In use as meadow seed fluff, with `KromaCameraFollow` and `KromaParticleController` disabled and collision off. Avoid `Dust_Glow` (emission 191) unless through a material copy, and Kroma's zone, lighting and LUT tools. |
| Ambient particles | BK Asian Valley `Fx/WaterSplash` | Could | `Env/BK` | Waterfall mist (shadows off, sortingOrder 1). Stylized Water 3's own waterfall particles: PLUGINS.md. |
| Ambient particles | Unluck Insect Particles | Could | `VFX/Unluck Software` | Not used. |
| Ambient particles | Raygeas Lakefell particles | Avoid | `Env/Raygeas` | Built-in shader, magenta in URP. |
| Weather | Unluck Stylized Weather | Undecided | `VFX/Unluck Software` | Weather will be ours ([DAYNIGHT.md](DAYNIGHT.md)). Suggest Could as a particle source only, never its controller. |

## Audio

All sound plays through BroAudio entities in `Assets/_Game/Data/Audio/` (`tools-unity-broaudio`). No final audio is chosen yet: current clips are borrowed from the packs below and from `Assets/_Placeholder/Audio`.

| Purpose | Pack | Tag | Folder | Notes |
| --- | --- | --- | --- | --- |
| Gun sounds | `Assets/_Placeholder/Audio/Weapons` | Should (temporary) | | In use: fire, equip, reload and bolt sounds, repair QTE mechanism/miss. To be replaced. |
| Hits, impacts, body foley | Malbers `Common/Audio` | Should | `Characters/Malbers Animations/Common/Audio` | In use: dry fire, jam, kill confirm, melee swing/hits, jump, land, body fall, repair accents. |
| Footsteps, nature ambience | Raygeas audio (HIGHLANDS, Lakefell, Grasslands) | Should | `Env/Raygeas/*/Assets/Audio` | In use: footsteps and owls (HIGHLANDS), birds and kneel (Lakefell), wind (Grasslands). |
| Nature ambience | ithappy Nature Sound FX, PlaceHolder Inc Environment Sounds | Could | `Audio/` | Not used. |
| General library (guns, UI, zombies, doors, foley, …) | Gamemaster Audio Pro Sound Collection | Undecided | `Audio/Gamemaster Audio - Pro Sound Collection` | Not used. Suggest Should as the main source for final SFX. |
| Gun, UI and survival sounds | PlaceHolder Inc Gun Sounds Vol 1, Interface & Item Sounds Lite, Survival Game Sounds Lite | Undecided | `Audio/PlaceHolder Inc` | Not used. Suggest Could. |
| Music, voice | `Assets/_Placeholder/Audio/Music`, `Voice` | Should (temporary) | | In use: death music, death groans. To be replaced. |

## Placeholder content

`Assets/_Placeholder/` holds borrowed assets waiting for final ones (only audio today). Fine to reference from `_Game`, but nothing there ships: every use is listed above so it can be replaced.
