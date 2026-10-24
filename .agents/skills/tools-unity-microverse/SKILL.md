---
name: tools-unity-microverse
description: MicroVerse 1.7 (Jason Booth) terrain authoring for aipocalypse — creating, sculpting and modifying Unity terrains non-destructively with stamps (height, height effects, texture, tree/detail/object, clear/occlusion, spline paths/areas), filters, noise and masks as they really behave in the 1.7 source, Rowlan's height stamp packs (Alpine Mountains, Highlands, Jungle Rivers, Meadows, Swamp, Valleys), the BK Pure Nature 2 presets, driving it all from the Unity CLI through the bundled MicroVerseKit run_script, baking to TerrainData, and the A* / Stylized Water / GPUI / Spline Spawner interplay. Use for any terrain, landscape, level-blockout or environment-sculpting work. For building or changing a whole map with a generator (the WorldMap way) also read tools-unity-map-generation. NOT Unity Terrain Tools brush painting (erased on the next MicroVerse rebuild) and NOT a runtime system (MicroVerse is stripped from builds and play mode).
---

# MicroVerse terrain authoring (aipocalypse)

Installed: **MicroVerse 1.7.0** core + Splines, Vegetation, Objects, Ambiance, Demo (`Packages/com.jbooth.microverse*`, full C# source, namespace `JBooth.MicroVerseCore`). **Not installed:** MicroSplat, Masks, Roads (no proxy renderer, no mask stamps, no road tools; ignore docs/Genesis features that need them). Content: Rowlan height packs and Genesis in `Assets/_StoreAssets/Env/Rowlan/`, Rowlan's BK Pure Nature 2 presets in `Assets/_StoreAssets/Env/MicroVerse-Presets/`. All of it is vendor content: reference it, never edit it.

The worked example is the WorldMap (`Assets/_Game/Scenes/Prototypes/WorldMap.unity`, data in `Maps/WorldMap/`), built by a generator (`AgentScripts/Maps/WorldMap/WorldMapLayout.cs`) that writes an analytic base heightmap and terrain-space masks and drives MicroVerse with them. Whole-map work (layouts, water, boundary, dressing, rebuilds) follows **tools-unity-map-generation**; this skill is the MicroVerse layer underneath.

Read before acting:
- [references/api.md](references/api.md): stamp classes, fields, enums and the JSON field names `Edit` needs (checked against the 1.7.0 source).
- [references/content.md](references/content.md): Rowlan packs, BK presets, Genesis tools, terrain layers, reference scenes. Map art packs (Raygeas, BK art, ANGRY MESH…) are in tools-unity-map-generation `references/art-packs.md`.
- `docs/technicals/ENVIRONMENT.md` before touching the WorldMap.
- Vendor docs, as text: `Packages/com.jbooth.microverse/Documentation/*.pdf` (`pdftotext` from Git's mingw64). Source beats the PDFs where they differ (api.md lists the differences).

## 1. Mental model

- **One `MicroVerse` component per scene** (`MicroVerse.instance`). Terrains are its children (default search) or `explicitTerrains`. All terrains at **Y = 0**, `allowAutoConnect` on, same size and resolutions.
- **Stamps are components under the MicroVerse GameObject. Hierarchy order = evaluation order.** Group them under plain GameObjects ("Height", "Texture", "Vegetation"…). Stamps outside the hierarchy do nothing (and aren't stripped from builds: never leave one there).
- **Every rebuild starts from height 0 and re-applies the whole stack** (heights → textures → holes → trees → details → objects). Nothing is painted into the terrain, so any stamp can be moved, re-ordered, disabled or deleted later. That's the whole point; keep it that way.
- Height stamp transform: **X/Z position = centre, X/Z scale = footprint in metres, Y rotation only. Texture black = the stamp's Y, white = Y + scale.y metres.** Only the red channel is read. No texture = a flat pad at the stamp's Y.
- **Texture stamps: lower in the hierarchy wins.** Stamps are evaluated bottom-up; each takes `min(its filter weight, 1 − weight already claimed)`, and the pixel keeps the **top 4 by weight**. The top stamp (base) fills what's left. A partial stamp (weight 0.3) claims 30% and leaves 70% for the stamps above it.
- MicroVerse **replaces** `terrainData.terrainLayers` with the layers its stamps use, **including disabled stamps** (they reserve their slot). Never add layers by hand; every distinct layer (even a tint variant) costs a slot, and each 4 layers is another splat pass.
- Edits render on the GPU first. The CPU copy (colliders, `SampleHeight`, raycasts, A* scans) and the TerrainData asset only update on a sync: kit `Rebuild`, the inspector's "Sync to Terrain", or a scene save.
- **Builds/play mode:** `SceneBuildStripping` removes MicroVerse and every stamp component, and the editor disables `MicroVerse.instance` when entering play mode; only the baked TerrainData (plus ObjectStamp-spawned GameObjects) ships. There is no runtime MicroVerse API. Never write game code against it.

## 2. Project rules

- **Prompt first** (AGENTS.md): build the terrain the prompt describes. Biome choice, dimensions or features taken from `docs/gdd/` must be proposed and confirmed first. Sizes and numbers in this skill are starting points for a requested shape, not design.
- **Where things go:** everything only one map uses goes in `Assets/_Game/Maps/<Map>/`: the scene (`<Map>.unity`), TerrainData and own heightmaps (`Terrain/`, `Terrain/Heightmaps/`), map-only meshes, ContentCollections and `CopyStamp` captures. Anything a second map could reuse (terrain layers, `DetailPrototypeSettings`, vegetation/rock/water copies) goes in the shared kit `Assets/_Game/Environment/`. Prototype scenes go in `Assets/_Game/Scenes/Prototypes/` (`Proto_*`), their terrain data in `Scenes/Prototypes/<Scene>/`. Name TerrainData `Terrain_*.asset` or keep it in a `Terrain/` folder: `.gitattributes` sends only those to Git LFS (a 1000 m tile at 2049 is ~59 MB). MicroVerse's "Create MicroVerse With Terrain" writes `Assets/New Terrain.asset` and the DetailStamp inspector writes `Assets/MicroVerse/DetailSettings/`: move those in the editor (keeps GUIDs) or use the kit's `Setup`.
- **Never `AssetDatabase.SaveAssets()`** in scripts. It writes *every* dirty asset, including vendor materials Unity upgraded in memory. Save the specific assets (`AssetDatabase.SaveAssetIfDirty(terrainData)`), as the kit does. (The WorldMap generator still breaks this in `BuildUnderwater`; fix it when you touch it.)
- Editor control is the Unity CLI (AGENTS.md). Run `unity status` and `set_autotick --enable true` first. MicroVerse processes `Invalidate()` on `EditorApplication.update`, so without autotick nothing rebuilds. One agent drives the editor at a time.
- Don't put MicroVerse **Ambiance** (`AmbientArea`, `AmbianceMgr` static singleton, own AudioSource pool) in game scenes. It survives stripping and breaks the no-singletons / BroAudio rules. Genesis **Gameplay** (legacy Input third-person rig) and **Visual Ambience** (built-in post-processing) are off-limits too.
- Sky, sun, fog and ambient belong to `EnvironmentDirector` (DAYNIGHT.md): never drop BK `Global` presets, `Skybox_*` or `TimeOfDay` into a game scene.
- Don't touch a scene the user has open with unsaved changes. Check `isDirty` (kit `Status`) and ask first.

## 3. The kit (MicroVerseKit run_script)

`scripts/MicroVerseKit.cs` wraps the verified API so no agent re-derives it. It lives outside `Assets/`, so running it triggers no import or domain reload. Every mutating entry registers Undo and calls `Invalidate()`. Paths are **relative to the MicroVerse root** (`"Height/Valleys 03 (Height Stamp)"`), as `Status` prints them.

```bash
K=.agents/skills/tools-unity-microverse/scripts/MicroVerseKit.cs
P="$(cygpath -w "$PWD")"     # Git Bash: the CLI needs a Windows path
unity command run_script --file $K --entry MicroVerseKit.Status --project-path "$P" --result-only
unity command run_script --file $K --entry MicroVerseKit.AddHeight --args '["Highlands 05","Height",300,0,250,500,150,500,30,"Max","Range",0.5]' --project-path "$P" --result-only
```

| Entry | Args (in order; trailing optional) | Does |
| --- | --- | --- |
| `Status` | – | Scene, dirty flag, MicroVerse state, terrains (size, resolutions, layers, data path), stamp stack in evaluation order |
| `Library` | filter | Height stamps from every Height ContentCollection (Rowlan packs + MicroVerse examples) |
| `Presets` | contentType, filter | Prefab presets of a type (`Texture`, `Vegetation`, `Biomes`, `Objects`, `Global`, `Paths`), installed packs only |
| `Layers` | filter | TerrainLayer asset paths |
| `Setup` | dataFolder, tilesX, tilesZ, tileSize, height, heightmapRes, alphamapRes, detailRes = 1024 | New MicroVerse root + terrain grid, TerrainData in `dataFolder` (under `Assets/_Game/Maps/` or `Assets/_Game/Scenes/Prototypes/`), URP terrain material, coverage scatter mode. Refuses if a MicroVerse exists |
| `AddHeight` | stamp, group, x, y, z, sizeX, height, sizeZ, rotY, mode, falloff, edge | HeightStamp. `stamp` = name or path from `Library`, `""` = flat pad at `y`. `mode` = CombineMode name. `falloff` = `Box`/`Range`/`Global` |
| `AddHeightEffect` | effect, group, x, y, z, sizeX, sizeZ, falloff, edge | `Terrace`/`Beach`/`RemapCurve`/`Noise` acting on the heights of stamps **above** it |
| `AddTexture` | layer, group, x, z, sizeX, sizeZ, falloff, edge | TextureStamp. `Global` falloff = whole terrain (base layer) |
| `AddPreset` | prefabPath, group, x, z, sizeX, sizeZ, rotY, runDropActions = false | Drops a Content Browser preset like the browser: unpacked, on the ground, **poisson disk assigned** (§6). Drop actions are skipped unless asked (BK ones change sky, fog and every terrain's material); the result lists the skipped ones. size 0 = keep prefab scale |
| `Place` | path, x, y, z, sizeX, sizeY, sizeZ, rotY | Move/scale/rotate any object under the root (invalidates old + new bounds) |
| `Edit` | path, json | Overwrite stamp fields with JSON, e.g. `{"mode":1,"erosion":120,"falloff":{"filterType":2}}`. Enums are ints (api.md) |
| `Inspect` | path | Dump a stamp's serialized fields (read before `Edit`) |
| `SetEnabled` / `Reorder` / `Remove` | path, enabled / path, siblingIndex / path | Toggle, re-order (changes the result), delete |
| `Rebuild` | bake | Synchronous regenerate + CPU sync (no second async pass); `true` also syncs splats/holes/vegetation into TerrainData and saves those assets. Returns min/max height, layers, tree count per terrain |
| `Sample` | x0, z0, x1, z1, steps | Height grid (north up) to check shapes numerically |
| `Look` / `ViewAll` | x, y, z, pitch, yaw, fov = 60, aboveGround = true / pitch, yaw | Scene-view camera exactly at (x, y, z) (y above ground by default; 1.7 = player eye), gizmos off, HUD/Player hidden / frame all terrains |
| `SaveScene` | path (`""` = current) | Save scene (MicroVerse syncs on save) + terrain assets; refuses while MicroVerse is still rebuilding, throws if the save fails |

`edge`: for `Box` it is the edge hardness (`falloffRange.y`, 1 = hard cut, 0.6–0.8 soft); for `Range` it is the radius (0–1) where the radial fade starts (0.3–0.6 looks natural). Kit stamps get `stampVersion = 1` (no surprise noise rescale) and texture filter sets `version = 2` (no legacy slope migration; slope values are still not degrees, §9).

Anything the kit doesn't cover (masks, vegetation stamps, filters, layer copies, spline stamps): write a builder in `AgentScripts/Maps/<Map>/`. `run_script` compiles **one file**, so copy helpers in rather than referencing the kit; start from the WorldMap generator's: `Stamp`/`Mask` (height + terrain-space mask), `Tex`/`Masked`/`Range`/`NoiseOn` (texture stamps), `Veg.Trees/Detail/Mask/Slope/Height/Noise`, `Vegetation.TexFilter/Clear`, `WriteR16`/`ConfigureImporter`, `CopyLayer`, `MaterialCopy`, `DetailPrefab`, `Look`/`OpenScene`. Each builder owns one top-level group and rebuilds it (destroy + recreate). Heavy entries: `async Task<object>` + `Task.Run`, and pass both `--timeout_ms` and the CLI's `--timeout`.

**Verify every change; never guess:**
1. `Rebuild false` (or `true` before saving) and read the min/max heights.
2. `Sample` across the feature. Heights should match the intent (a 60 m hill reads ~ground + 60).
3. `Look`/`ViewAll`, then `unity command screenshot --view scene` (saves to the ignored `Temp/pipeline-screenshots/`; Read the returned path). Don't use `capture_scene_view --save_path`: it saves under `Assets/`.
4. `unity command console --level error` for MicroVerse errors.

## 4. Building a terrain (order of operations)

Two proven ways:
- **Stamp composition** (blockouts, small areas; the old `Prototype.unity`, deleted 2026-10-24, is in git history): Rowlan stamps and BK presets placed with the kit, steps below.
- **Generated base** (a designed map that must match a layout, carry water and be verified; the WorldMap): an analytic height field written as an R16 PNG in one whole-terrain Override stamp, Rowlan detail stamps on top as zero-mean `Add` with terrain-space masks, then texture/vegetation stamps on whole-terrain masks. See tools-unity-map-generation.

Stamp composition:
1. **Scene and terrains.** New area: `Setup`. Resolution: 1 m per heightmap texel is the minimum for FPS play spaces (500 m tiles at 513, 1000 m at 1025); where water or paths are carved use ≤ 0.5 m (the WorldMap: 2049 on 1000 m, alphamap 2048, detail 1024; 1500 m: 4097). Set terrain `height` (size.y) with headroom over the tallest planned peak; it can't be exceeded. Raising resolution later is supported (stamps regenerate). Existing scene: `Status` first and work with what is there.
2. **Base elevation.** Heights start at 0, so `Subtract`/`Min` can't dig below the floor. Start with a **flat pad in `Override`** covering everything at a base height (e.g. 40 m), or a large low-relief stamp in Override. Rivers, valleys and swamps then cut into it.
3. **Big landforms.** Large stamps first (300–1000 m), from the matching Rowlan pack (content.md):
   - Ranges and peaks: `Max` (overlaps keep the higher), Alpine Mountains / Highlands.
   - Hills on top of the base: `Add` (adds the stamp's Y too; keep Y = 0 for Add/Subtract unless you want an offset; a negative offset is lost, §9).
   - Valleys, basins, swamps: `Subtract` with Valleys / Swamp / Jungle Rivers at low scale.y, or `Min` with a raised stamp.
   - Smooth hand-off between two shapes: `Blend` (+ `blend` field) or `Average`.
   - Use **`Range` falloff for natural shapes**. `Box` leaves visible rectangular rims on Add/Subtract stamps. Most Rowlan stamps don't fade to black at their borders.
4. **Detail and character.** Smaller stamps (50–300 m) on top. `twist`, `erosion` (0–600, slope-based), `power`, `invert`, `tiltX/Z`, `mipBias` (smoothing, needs mips) via `Edit`. `HeightAreaEffect` Terrace for plateaus or steps, Beach to flatten around a water level (its Y). Don't stretch a detail stamp to cover a bigger area: its micro-relief stretches too; tile more stamps.
5. **Gameplay spaces.** Flatten where buildings, arenas or roads go: flat pad (`""` stamp) in `Max` or `Override`, **last in the height group**, `Range` falloff with Smoothstep easing (flat to ~0.275 × footprint at edge 0.55) or Box ~0.6, Y = the floor height. Re-check with `Sample`.
6. **Texturing.** First texture stamp = base layer with `Global` falloff (otherwise black where weights < 1). Then slope and height layers (rock on slopes, mud in hollows) via filter-set `Edit`, or BK `Texture` presets (`Presets Texture meadows`). Order matters (§1). A generated map uses whole-terrain masks instead (map-generation `references/surface.md`).
7. **Vegetation and objects** (§6), then **splines** (§7).
8. **Bake and save:** `Rebuild true`, then `SaveScene`. Then refresh what depends on terrain (§8).

Hand-painting: Terrain Tools brushes on a MicroVerse terrain are erased by the next rebuild. To keep painted work: disable MicroVerse, paint, capture with a `CopyPasteStamp` (`CopyPasteStampEditor.Capture` via reflection, api.md), re-enable. Rowlan's packs ship extracted Terrain Tools brushes (`<Pack>/Brushes/Brushes/`, 140 over 4K R16 maps): opening the brush list loads them all.

## 5. Combining Rowlan heights with BK presets

The two content sets are made to stack: Rowlan packs give the **shape** (height stamps only), BK presets give the **surface** (texture/vegetation/object stamps under a `FalloffOverride` root, no heights in most biomes). The old `Prototype.unity` (deleted 2026-10-24, in git history) did this: Rowlan `Valleys 01` (Add, 1000×240×1000) + BK Meadows "Green Grass Mixed" texture + "Flower Meadow Orange Blue" details. The WorldMap uses no BK presets: its surface comes from first-party layers on generated masks.

- Natural pairings (suggestions, the user picks the look): Rowlan Meadows or Valleys + BK Meadows/AsianValley surface, Alpine Mountains + BK Mountains, Swamp or Jungle Rivers + BK Swamp/Jungle, FantasyForest for mossy forest floors. BK Highlands art isn't installed.
- Only the **6 installed BK biomes** resolve: AsianValley, FantasyForest, Jungle, Meadows, Mountains, Swamp. `Presets` filters out the rest.
- Presets carry height/slope filters tuned to their demo terrain. A preset that seems to do nothing is usually filtered out by height or slope (remember slope values aren't degrees, §9). `Inspect` the child stamps and widen `filterSet.heightFilter` / `slopeFilter` with `Edit`.
- Preset footprint = the `FalloffOverride` root's scale. `AddPreset` sizeX/Z sets it; the Content Browser default is 300×300.
- BK `Biomes` presets are whole stacks (texture + trees + details + clear + objects): quick dressing, but many detail layers. Check the layer count and detail triangles (`AgentScripts/Maps/WorldMap/DetailStats.cs`).

## 6. Vegetation and objects

- `TreeStamp` → Unity terrain trees (`SetTreeInstances`; Y-rotation only, **primitive colliders only**: audit tree prefabs, MeshCollider trees are walk-through). `DetailStamp` → terrain detail layers. `ObjectStamp` → **real GameObjects** in a `"<stamp> (Container)"` under MicroVerse; they survive stripping, get recycled on every rebuild (manual edits are lost) and are much heavier, so keep them for props needing scripts, colliders or full rotation. Scripted placement from a generator is usually better (map-generation `references/placement.md`).
- **TreeStamp mechanics** (`TreeStamp.cs`, `TreeFilter.shader`):
  - `poissonDisk == null` places nothing. Only the inspector auto-assigns `Packages/com.jbooth.microverse/Data/microverse_default_poissondisk.poissondisk`; `AddPreset` assigns it; set it yourself from code.
  - Candidates are a jittered grid over the **whole terrain**, `512 · density² · (size/1000)²`, whatever the stamp's footprint (spacing ≈ 44 / density m).
  - Keep test: a candidate is rejected when `random > weight`; with `densityByWeight` (**default on**) weights ≤ 0.5 spawn nothing and the rest is remapped `(w − 0.5) · 2`. Low noise balance is what makes groves dense.
  - A `Randomization` built in code is **disabled** (inverted flag bit) and has zero scale ranges: set `disabled = false`, scale ranges, `scaleMultiplierAtBoundaries = 1`, `weightRange = (0, 99999)` (api.md; the WorldMap's `Veg.Trees` does all of it).
  - Earlier tree stamps occupy space that later `occludedByOthers` stamps skip: order is priority.
  - Seeds: if a builder increments the seed per stamp, inserting a stamp re-rolls every later one.
- **DetailStamp mechanics:**
  - One terrain detail layer per **distinct prototype value** (`DetailPrototypeSerializable.Equals` compares every field, `noiseSeed` included). Stamps share a layer only through identical settings or one `DetailPrototypeSettings` asset (`settings` overrides `prototype`); keep shared ones in `Assets/_Game/Environment/Vegetation/DetailSettings/`.
  - Stamps sharing a layer combine with **max**, not a sum.
  - Unity rejects detail prefabs with a LODGroup on the root; MicroVerse substitutes child 0 of a LODGroup only when the renderer is on a child. Use LOD-free copies.
  - Coverage scatter mode (what the WorldMap uses): density isn't linear (1.6 gave 3× the instances of 1.0).
- `ClearStamp` removes trees/details/objects from spawn stamps **above** it (paths, clearings): put clear stamps last and together, and any dressing that must sit on cleared ground after them. `OcclusionStamp` blocks stamps **after** it. `SplinePath.clearTrees/clearDetails` clears along a path.
- **Rendering:** on the WorldMap, Unity's terrain renderer draws trees and details (detail distance 100, tree distance 1000 in the saved scene; ask before changing them). Details are most of the triangles: keep the detail distance ≤ ~120 m without GPUI. BK plants fade out beyond ~50 m unless `BKFoliageGlobals` (`_Core/EnvironmentSystem/`, render distance ≈ 2× detail distance) is in the scene.
- **GPU Instancer Pro** is on no scene now (the deleted `Prototype.unity` had a `GPUIDetailManager`, terrain `detailObjectDistance` 0). The Detail Manager is cheap; the Tree Manager costs CPU per prototype × LOD × material × camera every frame (it was slower than Unity's trees on the WorldMap). Shader Graphs need GPUI's setup node (Raygeas graphs don't have it: magenta). Attaching or removing a manager rewrites the terrain's draw distances. GPUI never removes prototypes: `ResetPrototypesFromTerrains()` after removing a stamp; `GPUITerrainAPI.RequireUpdate(manager, true, true)` if details look stale after a rebuild.
- **MicroVerse's terrain-settings panel** (MicroVerse inspector) writes **all** of its fields to every terrain when any one changes: material, resolutions, basemap distance, detail scatter mode/resolution/density/distance, tree distance, pixel error. Don't touch it without restoring them.

## 7. Splines (paths, roads, rivers, areas)

- `SplinePath` (on a GameObject with a Unity `SplineContainer`, under MicroVerse): carves (`trench`, `width`, `smoothness`, `heightBlendMode` Override/Max/Min/Blend), paints (`layer`, `embankmentLayer`), clears vegetation. Inside the per-point width the distance is 0, so the trench curve gives a flat bed across the width; it's sampled at `distance / (width + smoothness)`. There's no menu item: add the component, or use the Content Browser's spline paint mode. Per-knot widths (`splineWidths`): the docs say first spline only, but `SplineRenderer.cs` reads them per spline (unverified in the editor).
- `SplineArea` + stamps with `falloff.filterType = SplineArea` (4) and `falloff.splineArea` = that area confines any stamp group to a drawn shape (biome borders). Put a `FalloffOverride` on the group root to apply it to all children.
- **Rivers:** the WorldMap carves beds into its generated base heightmap and draws water with Stylized Water 3 on the same knots (map-generation `references/water.md`). SW3's RiverModeler MicroVerse section (`microVersePathSettings`) is **not used**: it overwrites an existing SplinePath with a flat-bed, constant-depth canal (a near-vertical bank step by default), does nothing for lakes or ponds, and gives no depth variation or mouth taper. Acceptable only for a hand-drawn, canal-like river; ask first and never mix both on one map.
- **Staggart Spline Spawner** (fences, rocks along a spline) doesn't listen for height changes. Respawn it after reshaping terrain under it.

## 8. After terrain changes (dependents)

- **Scripted object groups** (rocks, dressing, landmarks, water meshes and ribbons) sampled the old heights: rebuild them, then regenerate clear masks (ENVIRONMENT.md order). Objects outside rebuilt groups: settle them (`RiverLevel.Settle/Drowned/Floating`).
- **Water** (flat meshes, ribbons, underwater areas) doesn't follow the terrain: rebuild after `Rebuild false` (tools-unity-stylized-water, map-generation `references/water.md`).
- **A\***: no automatic rescan. `_Core/EnemySystem/Prefabs/Navigation.prefab` scans a small grid on startup; cached graphs must be rescanned (tools-unity-astar). The WorldMap has no A* setup yet.
- **Stylized Grass** colour map (`GrassColorMapRenderer`), only in scenes that use it: re-render after texture changes.
- **GPUI** terrain managers, where present: §6.
- **Lighting/occlusion bakes**, if a scene has them, are stale.
- Commit the TerrainData `.asset` files and their `.meta`s with the scene.

## 9. Pitfalls checklist

- Nothing rebuilds → `set_autotick` off, MicroVerse component disabled, or the stamp isn't under the MicroVerse root.
- Changed fields from code, re-ordered the hierarchy or re-enabled MicroVerse, and nothing happened → only transform moves and enable/disable auto-invalidate. Call `Invalidate()` (the kit does).
- `Invalidate()` + `Modify(true, true)` in your own code rebuilds twice: the sync `Modify` doesn't clear the pending flag (the kit's `Rebuild` clears it).
- `AddComponent<HeightStamp>()` resets the GameObject's Y to 0 (version migration in `OnEnable`). Set the position **after** adding.
- A stamp created in code defaults to `falloff.filterType = Global`, not the Box the inspector shows. On a HeightStamp that means full weight inside its rectangle (hard edge); on texture/tree/detail/clear stamps it means **the whole terrain**, whatever the stamp's size. Set it.
- New stamps with `stampVersion = 0` get their noise frequencies rescaled once on non-1000 m terrains. Set `stampVersion = 1`.
- **Slope filter values are not degrees.** The shader compares `(π/2)·acos(n·up)` with `range · Deg2Rad`, so on the 1000 × 300 × 1000 m WorldMap a value ≈ true degrees × 1.57 (measured: 32 → 18–20°, 45 → 27°, 54 → 34–35°, 57 → 36–38°; the api.md example 30–90 is ~19°–57°). The normal comes from normalised heights, so the mapping also shifts with terrain width vs height: the same real slope read ~1.5× steeper on a 1500 m × 300 m terrain (v23 converted with `64.4 · atan(size/1000 · tan(v/64.4))`, empirical). Measure real slopes after changing the terrain size. `filterSet.version = 2` only stops the legacy ×1.579 migration.
- **Noise frequency units depend on the stamp type.** TextureStamp noise is cycles per **terrain tile**; Tree/Detail/Clear/Object/Hole/Occlusion stamps scale theirs to cycles per **1000 m**. Copying texture noise values to a bigger terrain makes bigger patches: scale them by size/1000.
- Noise: MicroVerse FBM spans about ±0.47 (p10/p90 ±0.14); weight = saturate(1 + (n − balance) × amplitude). Amplitude 3 is a soft ramp that sprinkles everywhere; 8–24 gives hard-edged drifts. `offset` is in **noise cells**: stamps with identical frequency/amplitude/balance need offsets that differ by ≥ 1 (0, 0.15 and 0.45 gave near-identical drifts).
- **Add can't lower.** The shader `saturate`s `Y + tex · scale.y` to [0, 2·size.y] before combining, so a negative Y offset is lost. For zero-mean detail, keep Y = 0 and lower the base by `median · amplitude · weight` instead (map-generation `references/heights.md` §4).
- **Region-limited stamps (Texture falloff).** Height stamps always map the mask through `textureRotationScale`, so one terrain-space mask works for any unrotated square height stamp with `(0, size/terrainSize, centreX/terrainSize − 0.5, centreZ/terrainSize − 0.5)` and `falloffRange.y = 1`; then fade the mask to 0 before the stamp's square edge yourself, or it ends in a step. Filter-set stamps (texture/tree/detail/clear/occlusion) with `clampTexture` on **ignore** `textureRotationScale` (and with it off the mask repeats): make them terrain-sized with an identity transform, as the WorldMap does. Under a `FalloffOverride` the clamp flag still comes from the child stamp.
- `SampleHeight`, raycasts or colliders return old heights → the CPU copy is stale. `Rebuild` (it syncs) or save.
- Seams between tiles → a terrain has `allowAutoConnect` off, Y ≠ 0, or mismatched resolution or size.
- Black terrain patches → no global base texture stamp.
- Stair-stepped slopes → an 8-bit heightmap. Use 16-bit sources imported as Default or Single Channel (Red), sRGB off, R16 uncompressed, no mips (unless for `mipBias`), not readable, Clamp, max size ≥ source.
- Heights clamp flat at the top → the stamp exceeds terrain size.y.
- Heavy `run_script` entries fail with "Main thread operation timed out after 30000ms" → make them `async Task<object>`, run the math in `Task.Run`, and pass `--timeout_ms` and `--timeout`. The timed-out call keeps running: wait before retrying.
- After importing generated textures, use `AssetDatabase.WriteImportSettingsIfDirty` + `ImportAsset`, not `TextureImporter.SaveAndReimport()`: the latter wrote unrelated vendor materials that were dirty in memory.
- Only one MicroVerse per scene. "Create MicroVerse For Existing Terrains" opens a modal dialog, so don't run it from the CLI (it stalls).
- Screenshots still show objects a script deleted or disabled → the GPU Resident Drawer keeps their instances until the scene reloads. Save and reopen the scene. **Don't** force `EditorUtility.RequestScriptReload()` right after MicroVerse work: on the world map that left the TerrainData's managed wrapper pointing at another object (`terrain.terrainData` threw InvalidCastException), and the next scene save crashed the editor.
- `options.settings.useSceneCulling` (off by default) resets every Scene-view camera's far clip each tick.
- Unity 6000.6: `GetInstanceID()` is obsolete (fails `eval` compiles); `FindFirstObjectByType` and `FindObjectsSortMode` overloads are deprecated (use `FindAnyObjectByType`, `FindObjectsByType<T>(FindObjectsInactive)`). MicroVerse itself still calls the old ones (warnings only).
