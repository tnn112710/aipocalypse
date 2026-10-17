# Terrain content in this project

All of it is vendor content: reference it, never edit it. Make variants or copies under `Assets/_Game/Data/Environment/`. List live with the kit: `Library ""` (height stamps), `Presets <type> ""`, `Layers ""`.

## Rowlan height stamp packs (`Assets/_StoreAssets/Env/Rowlan/Terrain/Stamps/<Pack>/`)

`Heightmaps/<Pack> NN.png`: 4096², 16-bit, imported as R16, linear, no mips, not readable, Clamp. Each pack has a MicroVerse `ContentCollection` in `<Pack>/MicroVerse/<Pack>.asset` (author Rowlan, Height tab). Leave `Brushes/Brushes.zip` **zipped**: unzipped Terrain Tools brushes are all loaded into memory when a terrain is selected. The packs total about 2.7 GB on disk, but they are only referenced by stripped HeightStamps.

Looked at (contact sheet of 8 per pack, 2026-10). **Most stamps don't fade to black at their borders**: use `Range` falloff or cover a whole tile, or a `Box` edge will leave a cliff.

| Pack | Count | What the heightmaps look like | Typical use (starting points, not design) |
| --- | --- | --- | --- |
| Alpine Mountains | 25 | eroded ridge lines and valleys filling the tile; a crater ring (05); later numbers are isolated massifs on black (self-contained peaks) | mountain ranges and map-border backdrops: Max, 600–1500 m footprint, 200–400 m height. Isolated massifs drop in anywhere |
| Highlands | 15 (+1 prefab) | full-tile layered strata with a strong directional banding (sedimentary shelves), mid-to-bright everywhere | broad rugged ground over a whole area: Max/Blend at tile size, 40–150 m; rotate to orient the strata |
| Meadows | 30 | clusters of soft rounded mounds (bright blobs on dark) | gentle rolling hills: Add, 200–600 m, 5–30 m |
| Swamp | 30 | dendritic channel networks (dark = low) cut into flat ground; several are round islands fading to black | lowland with water channels: Add/Max on a low base at 3–15 m height, water plane just above the channel floors; islands work as standalone landmasses. Stored as 16-bit RGBA (only R is read) |
| Valleys | 30 | full-tile ridge massifs with branching valleys between them | hilly country with valleys: Add/Max at whole-tile scale (Prototype.unity: Valleys 01, Add, 1000×240×1000); Subtract a low copy for basins |

Also: MicroVerse's own examples (`Packages/com.jbooth.microverse/Examples/HeightStamps/`: Rugged Rocks 02/14, Seaside Cliffs 02, Meadows 12). The 24 "Rowlan" height ads in `Packages/com.jbooth.microverse/Data/Content/Ads/Rowlan/` are marketing for packs we don't own. Ignore them.

Import your own heightmaps (Gaea, World Machine, real DEM): save as 16-bit PNG/TIF, Texture Type Default, sRGB off, format R16, max size ≥ source, no mips (or mips for `mipBias` smoothing), Clamp. Put them under `Assets/_Game/Data/Environment/Heightmaps/` and make a ContentCollection (api.md) so they show in the browser and the kit's `Library`.

## BK Pure Nature 2 presets by Rowlan (`Assets/_StoreAssets/Env/MicroVerse-Presets/BK/Data/BK_PureNature2_<Biome>/`)

There are 16 biome folders, each with collections for `Biomes`, `Texture`, `Vegetation`, `Objects`, `Global` (+ `Paths` for some) and prefabs in `Prefabs/<Type>/`. **Only 6 BK art packs are installed** (`Assets/_StoreAssets/Env/BK/`): **AsianValley, Jungle, Meadows, MesaDesert, Mountains, Redwood**. The other 10 (AsianMountains, FantasyForest, Glacier, Highlands, Islands, Mojave, OasisDesert, Savanna, Swamp, Volcano) have hundreds of missing references each. The kit's `Presets` filters them out; the Content Browser still shows broken Highlands presets because of an id mismatch in its ad. Console "Problem detected while loading the contents of the Prefab file" refers to those; it's harmless.

What the preset types contain (Meadows census):
- `Texture`: TextureStamps under a `FalloffOverride` (e.g. Green Grass, Green Grass Mixed, Grass Wheat, Muddy Grass, Sandy Grass, Grassy Mud, Mud, Muddy Sand, Desert Sand).
- `Vegetation`: Detail/Tree stamps under a `FalloffOverride` (flower meadows, grass mixes, Birch/Oak/Elm/Cypress Cluster/Forest…).
- `Biomes`: a full stack of texture, trees, details, clear and object stamps. Fast to dress, but heavy on detail layers.
- `Objects`: real mesh props (rocks, water planes with `BindHeightFilterRangeToTransform`).
- `Global`: `BK_EnvironmentManager` (sun, fog, ambient, clouds). **Don't use it in game scenes.** Sky and atmosphere ownership is an open PLUGINS.md decision (COZY vs others).

Height stamps in BK presets are rare. Among installed biomes, only AsianValley's `Objects/Asian Hill 01–06` carry them (a hill mesh plus a matching height stamp). Terrain shape otherwise comes from Rowlan or our own stamps.

Shared preset scripts (`Shared/Scripts/MicroVerse Presets N/`):
- `PresetInstance` is a drop action that keeps one sky/fog/water preset per category.
- `TerrainMaterialReplacement` (in `TerrainMaterialSetup.cs`; the file and class names differ, so it may show as a missing script) sets the terrain material on drop.
- `TimeOfDay` and `Skybox_*` overlap COZY; don't use them in game scenes.

## Genesis for MicroVerse 3.5 (`Assets/_StoreAssets/Env/Rowlan/Genesis/`, `Window/Genesis for MicroVerse/...`)

Rowlan's editor wizards on top of MicroVerse. Its doc is `Documentation/Genesis.pdf`.

| Tool | Use here? |
| --- | --- |
| Converter → Heightmap | **Yes**: builds a height ContentCollection from a folder of heightmaps. Set the output under `_Game/Data` and fill `author`/`packName`/`id` |
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

- BK packs: e.g. `Assets/_StoreAssets/Env/BK/PureNature_Meadows/Textures/Surfaces/TerrainLayers/{Grass01, Grass02, Mud01, Mud02, Sand01, GrassWheat}.terrainlayer`
- ANGRY MESH Meadow: `.../Stylized Pack - Meadow Environment/Sources/Terrain Layers/ME_*_Terrain_Layer`
- Animpic Polygon Mega Survival: `.../Textures/TerrainLayers/*`
- Raygeas
- MicroVerse examples: `Packages/com.jbooth.microverse/Examples/TerrainLayers/{CobbleStone, Grass A, Grass Soil, Mud, Rock, Rock (Small), Sand, Snow}`

Mixing art styles (stylized ANGRY MESH/Animpic vs realistic BK) is a visual decision. Ask before mixing.

## Reference scenes

- `Assets/_Game/Scenes/Prototype.unity`: our current MicroVerse use.
  - One 1000×600×1000 terrain (`Assets/_Game/Scenes/New Terrain.asset`, URP TerrainLit).
  - Stamps: Rowlan Valleys 01 (Add), the BK Meadows "Green Grass Mixed" texture and "Flower Meadow Orange Blue" details.
  - Details are drawn by a `GPUIDetailManager` (Unity `detailObjectDistance` 0).
- `Packages/com.jbooth.microverse.demo/Scenes/2x2 Demo/2x2 Demo.unity`: the full feature set (16 terrains, height/texture/tree/detail/occlusion/clear stamps, spline paths and areas). Its materials are authored for the built-in pipeline, so expect pink shaders in URP. Read it for stamp setups, not visuals.
- Module examples: `com.jbooth.microverse.splines/Examples/2x2 Demo.unity`, `com.jbooth.microverse.vegetation/Example/Scene/Demo.unity`, `com.jbooth.microverse.objects/Examples/example.unity`.
