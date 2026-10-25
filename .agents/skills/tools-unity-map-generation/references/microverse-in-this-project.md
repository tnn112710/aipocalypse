# MicroVerse in this project

How MicroVerse is installed and used here. The library itself (stamps, filters, noise, tree/detail/clear mechanics, the MicroVerseKit, pitfalls) is the user-scope skill **unity-microverse**; read it first. This file only adds what is specific to aipocalypse. Recipes and numbers for the WorldMap's heights, water and surface are in heights.md, water.md and surface.md; build and rebuild order in `docs/technicals/ENVIRONMENT.md`. Checked against the code and assets on 2026-10-25.

## 1. What is installed

| Thing | Where | Notes |
| --- | --- | --- |
| MicroVerse 1.7.0: core + Splines, Vegetation, Objects, Ambiance, Demo | `Packages/com.jbooth.microverse*` | **Not installed:** MicroSplat, Masks, Roads (no proxy renderer, no mask stamps, no road tools) |
| Rowlan height packs: Alpine Mountains, Highlands, Jungle Rivers, Meadows, Swamp, Valleys | `Assets/_StoreAssets/Env/Rowlan/Terrain/Stamps/<Pack>/` | ~3.0 GB in LFS. `Brushes/` holds 140 extracted Terrain Tools brushes (`.brush` / `.asset`) |
| Genesis for MicroVerse 3.5.5 | `Assets/_StoreAssets/Env/Rowlan/Genesis/` | |
| BK Pure Nature 2 presets (14 biomes) | `Assets/_StoreAssets/Env/MicroVerse-Presets/BK/Data/BK_PureNature2_<Biome>/` | Only 6 BK art packs are installed (`Assets/_StoreAssets/Env/BK/`): **AsianValley, FantasyForest, Jungle, Meadows, Mountains, Swamp**. The other 8 biomes are broken (missing references); the kit's `Presets` hides them, the Content Browser still shows the Highlands ones |

All of it is vendor content: reference it, never edit it. Terrain-layer and art-pack inventory: art-packs.md.

## 2. Running the kit here

The kit is `.agents/skills/tools-unity-map-generation/scripts/MicroVerseKit.cs`, a copy of the user-scope `unity-microverse` kit kept in the repo so the map rebuilds on any clone and for any agent (`run_script` takes an absolute path). "Kit `Rebuild`" in ENVIRONMENT.md and in these references means that file. When the user-scope kit is improved, copy it back here.

```bash
K="$(cygpath -m "$PWD/.agents/skills/tools-unity-map-generation/scripts/MicroVerseKit.cs")"
P="$(cygpath -w "$PWD")"
unity command run_script --file "$K" --entry MicroVerseKit.Rebuild --args '[false]' --project-path "$P" --result-only
```

- In a `run_steps.sh` step file, write the kit as that `cygpath -m` path (`D:/.../tools-unity-map-generation/scripts/MicroVerseKit.cs|MicroVerseKit.Rebuild|[true]`).
- `Setup` data folder: `Assets/_Game/Maps/<Map>/Terrain` for a map, `Assets/_Game/Scenes/Prototypes/<Scene>/` for a `Proto_*` scene. `SaveScene` path for a new map: `Assets/_Game/Maps/<Map>/<Map>.unity`.
- `Look`: pass `"HUD,Player"` as `hideRoots`, or use `WorldMapLayout.Look(x, y, z, pitch, yaw, fov, aboveGround)`, which hides them already.

## 3. Where MicroVerse data goes

- Map-only: `Assets/_Game/Maps/<Map>/Terrain/` (TerrainData `Terrain_X_Z.asset`, `Heightmaps/` base map and masks); map-only ContentCollections and `CopyStamp` captures in `Maps/<Map>/Terrain/Stamps/`. Reusable: `Assets/_Game/Environment/` (`TerrainLayers/`, `Vegetation/DetailSettings/`; a shared heightmap collection would go in `Environment/Stamps/`, which doesn't exist yet).
- `.gitattributes` sends `Terrain*.asset`, `*Terrain.asset`, `**/Terrain/*.asset` and `**/Terrains/*.asset` to LFS: name TerrainData that way or keep it in a `Terrain/` folder. `Maps/WorldMap/Terrain/Terrain_0_0.asset` is 59 MB.
- Vendor tools write outside `_Game`: "Create MicroVerse With Terrain" → `Assets/New Terrain.asset`, the DetailStamp inspector → `Assets/MicroVerse/DetailSettings/`, the Genesis wizard → `Assets/Rowlan/Genesis Data`. Move them in the editor (keeps GUIDs) or don't use them. None exist today.
- URP terrain holes are on (`m_SupportsTerrainHoles: 1` in `Assets/_Game/Settings/PC_RPAsset.asset`), so HoleStamps work in builds.

## 4. Project rules on top of the library

- **Prompt first** (AGENTS.md): biome, size or features from `docs/gdd/` are proposed and confirmed before they are built.
- **No MicroVerse Ambiance** in game scenes: `AmbianceMgr` is a static singleton with its own AudioSource pool, it survives build stripping, and BroAudio owns audio. No Genesis **Gameplay** (legacy-Input rig) or **Visual Ambience** (built-in post-processing); don't reference `Rowlan.Genesis.Runtime` from game code (it ships in builds).
- **Sky, sun, fog and ambient belong to `EnvironmentDirector`** (`_Core/EnvironmentSystem/TimeOfDay/`, DAYNIGHT.md): never drop BK `Global` presets, `Skybox_*`, `TimeOfDay` or `PresetInstance` into a game scene. Keep the kit's `AddPreset` at `runDropActions = false`; BK's `TerrainMaterialReplacement` drop action would also replace the URP terrain material.
- BK `Objects` water planes are not Stylized Water 3: don't use them (the map's water is SW3 only).
- BK plant globals come from `BKFoliageGlobals` (art-packs.md), never from the BK Environment Manager.
- Don't touch the MicroVerse inspector's terrain-settings panel on the WorldMap: it rewrites every terrain field and would reset the saved draw distances and shadows (ENVIRONMENT.md → "Generator drift").

## 5. How the WorldMap generator drives MicroVerse

`AgentScripts/Maps/WorldMap/WorldMapLayout.cs` (WML) owns the MicroVerse stack of `Scenes/Prototypes/WorldMap.unity`. Each builder deletes and recreates its group:

| Group (path under the MicroVerse root) | Builder | Contents |
| --- | --- | --- |
| `Height/00 Base`, `Height/20 Region Detail`, `Height/90 Gameplay Pads` | `BuildHeights` (WML:937) | Override base heightmap, zero-mean Rowlan detail stamps, home pad (heights.md §3–5) |
| `Texture` | `BuildTextures` (WML:2598); `BlockoutTextures(grass, rock, shore)` (WML:998) is a temporary three-layer surface for review stop (a), replaced by `BuildTextures` | 20 texture stamps on terrain-space masks (surface.md §2) |
| `Vegetation/Trees`, `Bushes`, `Ground Cover`, `Clearing` (`Clear Trees`, `Clear Details`), `Trail Dressing` | `Vegetation.Build` (WML:4994; also deletes the `Vegetation Test` group of `Veg.Test`) | whole-terrain Tree/Detail stamps, then the clear stamps, then dressing that sits on cleared ground (surface.md §3) |

Not used on the WorldMap: BK presets, SplinePath / SplineArea, ObjectStamp, Hole / Occlusion / CopyPaste stamps, `FalloffOverride`, SW3 RiverModeler's MicroVerse section (water is carved in the height function, water.md §2; tools-unity-stylized-water).

**Helpers to copy into a new builder** (`run_script` compiles one file, so copy rather than reference):

| Helper | WML | Does |
| --- | --- | --- |
| `Stamp(parent, name, tex, mode, centre, size, y, height)` / `Mask(stamp, mask, centre, size)` | 968 / 986 | HeightStamp placed after `AddComponent`, `stampVersion = 1`; terrain-space Texture-falloff mask with `falloffRange.y = 1` |
| `Stamp(parent, name, layer)`, `Masked`, `Range`, `NoiseOn` | 2655–2692 | TextureStamp (`filterSet.version = 2`), whole-terrain mask, filter range, noise |
| `Veg.Trees`, `Veg.Detail`, `Veg.Mask`, `Veg.Slope`, `Veg.Height`, `Veg.Noise`, `Veg.Group`, `Veg.Clear` | 4558–4664 | Tree stamp with poisson disk and enabled `Randomization`s (`version = 1`), detail stamp, filters, group paths |
| `Vegetation.TexFilter`, `Vegetation.Clear` | 5189 / 5200 | texture filter (grass only on grass layers), ClearStamp |
| `WriteR16`, `ConfigureImporter` | 2827 / 2835 | R16 PNG writer and importer settings without `SaveAndReimport` |
| `CopyLayer`, `MaterialCopy`, `DetailPrefab` | 2585 / 2794 / 4929 | first-party layer copy (GUID kept), vendor material copy, LOD-free detail prefab |
| `Look`, `OpenScene` | 1048 / 927 | fixed camera views (hides HUD and Player), reload before screenshots |

Measuring tools in the same folder: `StampSheet.Make(folder, outPng, cell)` (Rowlan contact sheet with p10/p50/p90 and edge mean, for the zero-mean medians) and `DetailStats.Run()` (detail layers, prototypes, scatter mode, distances).

**Stamp-composition example in history.** `Prototype.unity` (deleted in `3653a1db4`, 2026-10-24) was a pure preset stack: one 1000×600×1000 terrain (`Scenes/Prototypes/Prototype/Prototype Terrain.asset`), Rowlan `Valleys 01` (Add, 1000×240×1000), BK Meadows "Green Grass Mixed" texture and "Flower Meadow Orange Blue" details, drawn by a `GPUIDetailManager` (terrain `detailObjectDistance` 0). Recover it with `git show 3653a1db4^:Assets/_Game/Scenes/Prototypes/Prototype.unity`.

## 6. After terrain changes here

The dependents and their order are in ENVIRONMENT.md ("How it is built", "Water-only rebuild") and water.md §8. MicroVerse-specific points:

- **A\***: the WorldMap's `Navigation` instance loads a cached recast graph (`Maps/WorldMap/Navigation/WorldMapNavmesh.bytes`), which doesn't follow the terrain: after heights, water or colliders change run `Navigation.BuildWaterVolume(0.5, true)` then `Navigation.Bake(0.3)` (ENVIRONMENT.md → Navigation). Other scenes use `_Core/EnemySystem/Prefabs/Navigation.prefab` as is: a small terrain-following grid scanned on startup, so it picks up the baked terrain by itself. Library detail: **unity-astar**; how this project's enemies use it: **aipocalypse-enemy-ai**.
- **GPUI**: no GPUI manager in any `_Game` scene; Unity's terrain renderer draws the WorldMap vegetation (surface.md §4).
- **Stylized Grass** colour map: a repeating 25 m tile of the BK lawn noise (`Environment/Grass Colormap`), independent of the terrain, so terrain changes don't need a re-bake. Never press Render on its `GrassColorMapRenderer`: that would replace the tile with a terrain render.
