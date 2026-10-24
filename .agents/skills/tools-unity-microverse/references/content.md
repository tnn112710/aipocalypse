# Terrain content in this project

All of it is vendor content: reference it, never edit it. Make variants or copies under `Assets/_Game/Environment/` (reusable; keep the pack prefix `BK_`/`HL_`/`KN_`/`AM_`/`GL_`) or `Assets/_Game/Maps/<Map>/` (one map only). List live with the kit: `Library ""` (height stamps), `Presets <type> ""`, `Layers ""`. Map art packs (Raygeas, BK art, ANGRY MESH, Stylized Water…) and the shared first-party kit are in tools-unity-map-generation `references/art-packs.md`.

## Rowlan height stamp packs (`Assets/_StoreAssets/Env/Rowlan/Terrain/Stamps/<Pack>/`)

`Heightmaps/<Pack> NN.png`: 4096², 16-bit, imported as R16, linear, no mips, not readable, Clamp. Each pack has a MicroVerse `ContentCollection` in `<Pack>/MicroVerse/<Pack>.asset` (author Rowlan, Height tab). The six packs total about 3.0 GB (LFS), but they are only referenced by stripped HeightStamps. `Brushes/Brushes/` holds extracted Terrain Tools brushes over the same heightmaps (140 in all): opening the Paint Terrain brush list loads all their textures.

Looked at (contact sheets, 2026-10). **Most stamps don't fade to black at their borders**: use `Range` falloff or cover a whole tile, or a `Box` edge will leave a cliff. `AgentScripts/Maps/WorldMap/StampSheet.cs` renders a contact sheet with p10/p50/p90 and edge mean per heightmap (p50 = the median the zero-mean detail trick needs).

| Pack | Count | What the heightmaps look like | Typical use (starting points, not design) |
| --- | --- | --- | --- |
| Alpine Mountains | 25 | eroded ridge lines and valleys filling the tile; a crater ring (05); later numbers are isolated massifs on black (self-contained peaks) | mountain ranges and map-border backdrops: Max, 600–1500 m footprint, 200–400 m height. Isolated massifs drop in anywhere |
| Highlands | 15 (+1 prefab) | full-tile layered strata with a strong directional banding (sedimentary shelves), mid-to-bright everywhere | broad rugged ground over a whole area: Max/Blend at tile size, 40–150 m; rotate to orient the strata. WorldMap: 09 (560 m / 26 m) and 07 (300 m / 10 m) as zero-mean detail on the highlands |
| Jungle Rivers | 10 | full-tile eroded hill country cut by dendritic river channels (dark = low), mid-dark overall (p50 0.24–0.34), edges don't fade | forest/jungle river country: Add/Max at tile size, 20–80 m; carve water at the channel floors. 16-bit RGBA (R read) |
| Meadows | 30 | clusters of soft rounded mounds (bright blobs on dark) | gentle rolling hills: Add, 200–600 m, 5–30 m. WorldMap: 05, 16, 26 as 3.5–9 m zero-mean detail |
| Swamp | 30 | dendritic channel networks (dark = low) cut into flat ground; several are round islands fading to black | lowland with water channels: Add/Max on a low base at 3–15 m height; islands work as standalone landmasses. Water: a flat sheet over the whole area floods every channel below its level and floats over lower streams, so flood-fill the sheet from the real shoreline (map-generation `references/water.md`). 16-bit RGBA (R read) |
| Valleys | 30 | full-tile ridge massifs with branching valleys between them | hilly country with valleys: Add/Max at whole-tile scale (the old `Prototype.unity`, deleted 2026-10-24: Valleys 01, Add, 1000×240×1000); Subtract a low copy for basins |

Also: MicroVerse's own examples (`Packages/com.jbooth.microverse/Examples/HeightStamps/`: Rugged Rocks 02/14, Seaside Cliffs 02, Meadows 12). The 25 Rowlan ads in `Packages/com.jbooth.microverse/Data/Content/Ads/Rowlan/` are store links (21 for packs we don't own). Ignore them.

Import your own heightmaps (Gaea, World Machine, real DEM, or a generator's output): save as 16-bit PNG/TIF, Texture Type Default or Single Channel (Red), sRGB off, format R16 uncompressed, max size ≥ source, no mips (or mips for `mipBias` smoothing), Clamp. Put them under `Assets/_Game/Maps/<Map>/Terrain/Heightmaps/` (or `Assets/_Game/Environment/Heightmaps/` if several maps use them) and make a ContentCollection (api.md) if they should show in the browser and the kit's `Library`.

## BK Pure Nature 2 presets by Rowlan (`Assets/_StoreAssets/Env/MicroVerse-Presets/BK/Data/BK_PureNature2_<Biome>/`)

There are 14 biome folders, each with collections for `Biomes`, `Texture`, `Vegetation`, `Objects`, `Global` (+ `Paths` for some) and prefabs in `Prefabs/<Type>/`. **Only 6 BK art packs are installed** (`Assets/_StoreAssets/Env/BK/`): **AsianValley, FantasyForest, Jungle, Meadows, Mountains, Swamp**. The other 8 (AsianMountains, Glacier, Highlands, Islands, Mojave, OasisDesert, Savanna, Volcano) have hundreds of missing references each. The kit's `Presets` filters them out; the Content Browser still shows broken Highlands presets because of an id mismatch in its ad. Console "Problem detected while loading the contents of the Prefab file" refers to those; it's harmless.

What the preset types contain (Meadows census):
- `Texture`: TextureStamps under a `FalloffOverride` (e.g. Green Grass, Green Grass Mixed, Grass Wheat, Muddy Grass, Sandy Grass, Grassy Mud, Mud, Muddy Sand, Desert Sand).
- `Vegetation`: Detail/Tree stamps under a `FalloffOverride` (flower meadows, grass mixes, Birch/Oak/Elm/Cypress Cluster/Forest…).
- `Biomes`: a full stack of texture, trees, details, clear and object stamps. Fast to dress, but heavy on detail layers.
- `Objects`: real mesh props (rocks, and water planes with `BindHeightFilterRangeToTransform`: plain planes, not Stylized Water 3; don't mix water systems on a map).
- `Global`: `BK_EnvironmentManager` (sun, fog, ambient, clouds) and skybox presets. **Don't use them in game scenes**: `EnvironmentDirector` owns sun, ambient, fog and sky (DAYNIGHT.md), and BK plant globals come from `BKFoliageGlobals`.

Height stamps in BK presets are rare. Among installed biomes, only AsianValley's `Objects/Asian Hill 01–06` carry them (a hill mesh plus a matching height stamp). Terrain shape otherwise comes from Rowlan or our own stamps.

Shared preset scripts (`Shared/Scripts/MicroVerse Presets N/`), all drop actions (the kit's `AddPreset` skips them unless `runDropActions` is true):
- `PresetInstance` keeps one sky/fog/water preset per category.
- `TerrainMaterialReplacement` (in `TerrainMaterialSetup.cs`; the file and class names differ, so it may show as a missing script) replaces every terrain's material on drop.
- `TimeOfDay` and `Skybox_*` set `RenderSettings.skybox` and fog, which conflicts with `EnvironmentDirector`; don't use them in game scenes.

## Genesis for MicroVerse 3.5.5 (`Assets/_StoreAssets/Env/Rowlan/Genesis/`, `Window/Genesis for MicroVerse/...`)

Rowlan's editor wizards on top of MicroVerse. Its doc is `Documentation/Genesis.pdf`.

| Tool | Use here? |
| --- | --- |
| Converter → Heightmap | **Yes**: builds a height ContentCollection from a folder of heightmaps. Set the output under `_Game/Maps/<Map>/Terrain/Stamps` (or `_Game/Environment/Stamps`) and fill `author`/`packName`/`id` |
| Browser Tools → Placement ("Place all Height Content on Grid") | **Yes**: previews a whole stamp pack side by side in a scratch scene |
| Browser Tools → Validation | **Yes**: scans presets for missing heightmaps, layers and prefabs |
| Terraform (fit/center stamp, random heightmap, spline grow/subdivide/align) | Yes, interactive |
| ReGenesis (Seed & Noise, random stamp position/heightmap) | Yes, for variations |
| Distribution (bulk texture-filter edits) | Yes |
| Genesis wizard (creates MicroVerse + terrains + random stamps) | Throwaway blockouts only: it **replaces** existing MicroVerse setups and writes TerrainData to `Assets/Rowlan/Genesis Data` (outside `_Game`). Prefer the kit's `Setup` |
| Migration (clone collection, remap GUIDs) | Rarely; Remap edits project files without Undo |
| Roads, Spline Deformer | No (Roads module not installed; Spline Spawner covers spawning along splines) |
| Visual Ambience, Gameplay | **Never**: built-in post-processing, and a legacy-`Input` third-person rig that compiles into the runtime asmdef |

Genesis's runtime asmdef (`Rowlan.Genesis.Runtime`) ships in player builds. Don't reference it from game code.

## Terrain layers available

- **First-party, use first**: `Assets/_Game/Environment/TerrainLayers/Terrain_{Grass, GrassCool, GrassMeadow, Dirt, WetMud, Pebbles, Sand, Rock}` (Raygeas sources, tiles and tints in map-generation `references/surface.md` §2). Copy a vendor layer into this folder rather than referencing it from stamps.
- Raygeas: Grasslands `.../Grasslands - Stylized Nature/Assets/Terrain Layers/` (8: Grass, GrassMud, Grass_Blue, Grass_Yellow, Mud, MudStones, MudStonesGrass, Sand), Komorebi (Grass_1, Ground_1, Ground_1_Snow, Stones_1), HIGHLANDS (Grass_1/2, Mud_1/2), Lakefell (Dirt_1, Far_Snow_1, Grass_1, StonePath_1), AZURE (24 seasonal), Stylized Terrain Textures (50).
- BK packs: e.g. `Assets/_StoreAssets/Env/BK/PureNature_Meadows/Textures/Surfaces/TerrainLayers/{Grass01, Grass02, Mud01, Mud02, Sand01, GrassWheat}.terrainlayer`.
- ANGRY MESH Meadow: `.../Stylized Pack - Meadow Environment/Sources/Terrain Layers/ME_*_Terrain_Layer`.
- Animpic Polygon Mega Survival: `.../Textures/TerrainLayers/*`.
- MicroVerse examples: `Packages/com.jbooth.microverse/Examples/TerrainLayers/{CobbleStone, Grass A, Grass Soil, Mud, Rock, Rock (Small), Sand, Snow}`.

The project's art set is Raygeas (main) + BK Pure Nature (support) + ANGRY MESH (near boundary). Ask before bringing in another style (SICS toon, Polyart, AZURE, Animpic, Stylized Terrain Textures).

## Reference scenes

- `Assets/_Game/Scenes/Prototypes/WorldMap.unity`: the reference map (data in `Maps/WorldMap/`). One 1000×300×1000 terrain (`Maps/WorldMap/Terrain/Terrain_0_0.asset`, heightmap 2049, alphamap 2048, detail 1024, URP TerrainLit), generated by `AgentScripts/Maps/WorldMap/` (ENVIRONMENT.md): a whole-terrain Override base stamp, masked zero-mean Rowlan detail stamps, 20 texture stamps over 8 first-party layers, ~60 vegetation stamps on whole-terrain masks, two clear stamps. Unity's terrain renderer draws the vegetation.
- `Prototype.unity` (deleted 2026-10-24, in git history): the older preset stack. One 1000×600×1000 terrain (`Scenes/Prototypes/Prototype/Prototype Terrain.asset`); Rowlan Valleys 01 (Add), the BK Meadows "Green Grass Mixed" texture and "Flower Meadow Orange Blue" details; details drawn by a `GPUIDetailManager` (Unity `detailObjectDistance` 0).
- `Packages/com.jbooth.microverse.demo/Scenes/2x2 Demo/2x2 Demo.unity`: the full feature set (16 terrains, height/texture/tree/detail/occlusion/clear stamps, spline paths and areas). Its materials are authored for the built-in pipeline, so expect pink shaders in URP. Read it for stamp setups, not visuals.
- Module examples: `com.jbooth.microverse.splines/Examples/2x2 Demo.unity`, `com.jbooth.microverse.vegetation/Example/Scene/Demo.unity`, `com.jbooth.microverse.objects/Examples/example.unity`.
