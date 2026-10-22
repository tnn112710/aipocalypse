---
name: tools-unity-microverse
description: MicroVerse 1.7 (Jason Booth) terrain authoring for aipocalypse — creating, sculpting and modifying Unity terrains non-destructively with stamps (height, height effects, texture, tree/detail/object, clear/occlusion, spline paths/areas), combining Rowlan's height stamp packs (Alpine Mountains, Highlands, Meadows, Swamp, Valleys) with the BK Pure Nature 2 biome presets, driving it all from the Unity CLI through the bundled MicroVerseKit run_script, baking to TerrainData, and the GPUI / A* / Stylized Water / Spline Spawner interplay. Use for any terrain, landscape, level-blockout or environment-sculpting work. NOT Unity Terrain Tools brush painting (erased on the next MicroVerse rebuild) and NOT a runtime system (MicroVerse is stripped from builds).
---

# MicroVerse terrain authoring (aipocalypse)

Installed: **MicroVerse 1.7.0** core + Splines, Vegetation, Objects, Ambiance, Demo (`Packages/com.jbooth.microverse*`, full C# source, namespace `JBooth.MicroVerseCore`). **Not installed:** MicroSplat, Masks, Roads (no proxy renderer, no mask stamps, no road tools; ignore docs/Genesis features that need them). Content: Rowlan height packs and Genesis in `Assets/_StoreAssets/Env/Rowlan/`, Rowlan's BK Pure Nature 2 presets in `Assets/_StoreAssets/Env/MicroVerse-Presets/`. All of it is vendor content: reference it, never edit it.

Read before acting:
- [references/api.md](references/api.md): stamp classes, fields, enums and the JSON field names `Edit` needs (checked against the 1.7.0 source).
- [references/content.md](references/content.md): what each Rowlan pack / BK biome / Genesis tool is good for, sizes, import settings, which presets resolve.
- Vendor docs, as text: `Packages/com.jbooth.microverse/Documentation/*.pdf` (the Read tool can't render PDFs here; `pdftotext` from Git's mingw64 works). Source beats the PDFs where they differ (api.md lists the differences).

## 1. Mental model

- **One `MicroVerse` component per scene** (`MicroVerse.instance`). Terrains are its children (default search) or `explicitTerrains`. All terrains at **Y = 0**, `allowAutoConnect` on, same size and resolutions.
- **Stamps are components under the MicroVerse GameObject. Hierarchy order = evaluation order.** Group them under plain GameObjects ("Height", "Texture", "Vegetation"…). Stamps outside the hierarchy do nothing.
- **Every rebuild starts from height 0 and re-applies the whole stack** (heights → textures → holes → trees → details → objects). Nothing is painted into the terrain, so any stamp can be moved, re-ordered, disabled or deleted later. That's the whole point; keep it that way.
- Height stamp transform: **X/Z position = centre, X/Z scale = footprint in metres, Y rotation only. Texture black = the stamp's Y, white = Y + scale.y metres.** Only the red channel is read. No texture = a flat pad at the stamp's Y.
- **Texture stamps: lower in the hierarchy wins** (like Photoshop layers); max 4 layers blend per pixel, 32 per terrain. MicroVerse **replaces** `terrainData.terrainLayers` with what the stamps use, so never add layers by hand.
- Edits render on the GPU first. The CPU copy (colliders, `SampleHeight`, raycasts, NavMesh/A* scans) and the TerrainData asset only update on a sync: `Rebuild(true)` in the kit, the inspector's "Sync to Terrain", or a scene save.
- **Builds/play mode:** `SceneBuildStripping` removes MicroVerse and every stamp component; only the baked TerrainData (plus ObjectStamp-spawned GameObjects) ships. There is no runtime MicroVerse API. Never write game code against it.

## 2. Project rules

- **Prompt first** (AGENTS.md): build the terrain the prompt describes. Biome choice, dimensions or features taken from `docs/gdd/` must be proposed and confirmed first. Sizes and numbers in this skill are starting points for a requested shape, not design.
- **Where things go:** everything only one map uses goes in `Assets/_Game/Maps/<Map>/`: the scene (`<Map>.unity`), TerrainData and own heightmaps (`Terrain/`, `Terrain/Heightmaps/`), map-only meshes, ContentCollections and `CopyStamp` captures. Anything a second map could reuse (terrain layers, `DetailPrototypeSettings`, vegetation/rock/water copies) goes in the shared kit `Assets/_Game/Environment/`. Prototype scenes go in `Assets/_Game/Scenes/Prototypes/`, named `Proto_*`. MicroVerse's menu "Create MicroVerse With Terrain" writes `Assets/New Terrain.asset` and the DetailStamp inspector writes `Assets/MicroVerse/DetailSettings/`: move those into `_Game/Maps` or `_Game/Environment` in the editor (keeps GUIDs) or use the kit's `Setup` instead.
- **Never `AssetDatabase.SaveAssets()`** in scripts. It writes *every* dirty asset, including vendor materials Unity upgraded in memory (that happened while this skill was tested). Save the specific assets (`AssetDatabase.SaveAssetIfDirty(terrainData)`), as the kit does.
- Editor control is the Unity CLI (AGENTS.md). Run `unity status` and `set_autotick --enable true` first. MicroVerse processes `Invalidate()` on `EditorApplication.update`, so without autotick nothing rebuilds. One agent drives the editor at a time.
- Don't put MicroVerse **Ambiance** (`AmbientArea`, `AmbianceMgr` static singleton, own AudioSource pool) in game scenes. It survives stripping and breaks the no-singletons / BroAudio rules. Genesis **Gameplay** (legacy Input third-person rig) and **Visual Ambience** (built-in post-processing) are off-limits too.
- Don't touch a scene the user has open with unsaved changes. Check `scene.isDirty` (kit `Status`) and ask first.

## 3. The kit (MicroVerseKit run_script)

`scripts/MicroVerseKit.cs` wraps the verified API so no agent re-derives it. It lives outside `Assets/`, so running it triggers no import or domain reload. Every mutating entry registers Undo and calls `Invalidate()`. Paths are **relative to the MicroVerse root** (`"Height/Valleys 03 (Height Stamp)"`), as `Status` prints them.

```bash
K=.agents/skills/tools-unity-microverse/scripts/MicroVerseKit.cs
P="$(cygpath -w "$PWD")"     # Git Bash: the CLI needs a Windows path
unity command run_script --file $K --entry MicroVerseKit.Status --project-path "$P" --result-only
unity command run_script --file $K --entry MicroVerseKit.AddHeight --args '["Highlands 05","Height",300,0,250,500,150,500,30,"Max","Range",0.5]' --project-path "$P" --result-only
```

| Entry | Args (in order) | Does |
| --- | --- | --- |
| `Status` | – | Scene, dirty flag, MicroVerse state, terrains (size, resolutions, layers, data path), stamp stack in evaluation order |
| `Library` | filter | Height stamps from every Height ContentCollection (Rowlan packs + MicroVerse examples) |
| `Presets` | contentType, filter | Prefab presets of a type (`Texture`, `Vegetation`, `Biomes`, `Objects`, `Global`, `Paths`), installed packs only |
| `Layers` | filter | TerrainLayer asset paths |
| `Setup` | dataFolder, tilesX, tilesZ, tileSize, height, heightmapRes, alphamapRes | New MicroVerse root + terrain grid, TerrainData in `dataFolder` (must be under `Assets/_Game/Maps/`), URP terrain material. Refuses if a MicroVerse exists |
| `AddHeight` | stamp, group, x, y, z, sizeX, height, sizeZ, rotY, mode, falloff, edge | HeightStamp. `stamp` = name or path from `Library`, `""` = flat pad at `y`. `mode` = CombineMode name. `falloff` = `Box`/`Range`/`Global` |
| `AddHeightEffect` | effect, group, x, y, z, sizeX, sizeZ, falloff, edge | `Terrace`/`Beach`/`RemapCurve`/`Noise` acting on the heights of stamps **above** it |
| `AddTexture` | layer, group, x, z, sizeX, sizeZ, falloff, edge | TextureStamp. `Global` falloff = whole terrain (base layer) |
| `AddPreset` | prefabPath, group, x, z, sizeX, sizeZ, rotY | Drops a Content Browser preset like the browser: unpacked, on the ground, drop actions run, **poisson disk assigned** (see §6). size 0 = keep prefab scale |
| `Place` | path, x, y, z, sizeX, sizeY, sizeZ, rotY | Move/scale/rotate any object under the root (invalidates old + new bounds) |
| `Edit` | path, json | Overwrite stamp fields with JSON, e.g. `{"mode":1,"erosion":120,"falloff":{"filterType":2}}`. Enums are ints (api.md) |
| `Inspect` | path | Dump a stamp's serialized fields (read before `Edit`) |
| `SetEnabled` / `Reorder` / `Remove` | path, enabled / path, siblingIndex / path | Toggle, re-order (changes the result), delete |
| `Rebuild` | bake | Synchronous regenerate + CPU sync; `true` also syncs splats/holes/vegetation into TerrainData and saves those assets. Returns min/max height, layers, tree count per terrain |
| `Sample` | x0, z0, x1, z1, steps | Height grid (north up) to check shapes numerically |
| `View` / `ViewAll` | x, y, z, pitch, yaw, size / pitch, yaw | Frame the Scene view for a screenshot |
| `SaveScene` | path (`""` = current) | Save scene (MicroVerse syncs on save) + terrain assets |

`edge`: for `Box` it is the edge hardness (`falloffRange.y`, 1 = hard cut, 0.6–0.8 soft); for `Range` it is the radius (0–1) where the radial fade starts (0.3–0.6 looks natural). Kit stamps get `stampVersion = 1` (no surprise noise rescale) and texture filter sets `version = 2` (slope values literal).

Anything the kit doesn't cover (spline stamps, filters on presets, custom ContentCollections): write a builder in `AgentScripts/` per the unity-pipeline guide, reusing the kit's patterns. api.md has the API.

**Verify every change; never guess:**
1. `Rebuild false` (or `true` before saving) and read the min/max heights.
2. `Sample` across the feature. Heights should match the intent (a 60 m hill reads ~ground + 60).
3. `View`/`ViewAll`, then `unity command screenshot --view scene`. It saves to the project's ignored `Temp/pipeline-screenshots/`, so Read the returned path. Don't use `capture_scene_view --save_path`: it saves under `Assets/` (creates an asset) and rejects paths outside.
4. `unity command console --level error` for MicroVerse errors.

## 4. Building a terrain (order of operations)

1. **Scene and terrains.** New area: `Setup`. For FPS play spaces, 1 m per heightmap texel is a good default (500 m tiles at 513, 1000 m at 1025). Set terrain `height` (size.y) with headroom over the tallest planned peak; it can't be exceeded. Authoring at lower resolution and raising it later is supported (stamps regenerate at the new resolution). Existing scene (e.g. `Assets/_Game/Scenes/Prototype.unity`): `Status` first and work with what is there.
2. **Base elevation.** Heights start at 0, so `Subtract`/`Min` can't dig below the floor. Start with a **flat pad in `Override`** covering everything at a base height (e.g. 40 m), or a large low-relief stamp in Override. Rivers, valleys and swamps then cut into it.
3. **Big landforms.** Large stamps first (300–1000 m), from the matching Rowlan pack (content.md):
   - Ranges and peaks: `Max` (overlaps keep the higher), Alpine Mountains / Highlands.
   - Hills on top of the base: `Add` (adds the stamp's Y too; keep Y = 0 for Add/Subtract unless you want an offset).
   - Valleys, basins, swamps: `Subtract` with Valleys / Swamp at low scale.y, or `Min` with a raised stamp.
   - Smooth hand-off between two shapes: `Blend` (+ `blend` field) or `Average`.
   - Use **`Range` falloff for natural shapes**. `Box` leaves visible rectangular rims on Add/Subtract stamps.
4. **Detail and character.** Smaller stamps (50–300 m) on top. `twist`, `erosion` (0–600, slope-based), `power`, `invert`, `tiltX/Z`, `mipBias` (smoothing, needs mips) via `Edit`. `HeightAreaEffect` Terrace for plateaus or steps, Beach to flatten around a water level (its Y).
5. **Gameplay spaces.** Flatten where buildings, arenas or roads go: flat pad (`""` stamp) in `Max` or `Override`, **last in the height group**, Box falloff with edge ~0.6 for a soft blend, Y = the floor height. Re-check with `Sample`.
6. **Texturing.** First texture stamp = base layer with `Global` falloff (otherwise black where weights < 1). Then slope and height layers (rock on slopes, mud in hollows) via filter-set `Edit`, or drop BK `Texture` presets (`Presets Texture meadows`). Order matters: later wins.
7. **Vegetation and objects** (§6), then **splines** (§7).
8. **Bake and save:** `Rebuild true`, then `SaveScene`. Then refresh what depends on terrain (§8).

Hand-painting: Terrain Tools brushes on a MicroVerse terrain are erased by the next rebuild. To keep painted work: disable MicroVerse, paint, capture with a `CopyPasteStamp` (`CopyPasteStampEditor.Capture` via reflection, api.md), re-enable. Leave Rowlan's `Brushes.zip` zipped: Terrain Tools loads every brush heightmap into memory.

## 5. Combining Rowlan heights with BK presets

The two content sets are made to stack: Rowlan packs give the **shape** (height stamps only), BK presets give the **surface** (texture/vegetation/object stamps under a `FalloffOverride` root, no heights in most biomes). `Prototype.unity` already does this: Rowlan `Valleys 01` (Add, 1000×240×1000) + BK Meadows "Green Grass Mixed" texture + "Flower Meadow Orange Blue" details.

- Natural pairings (suggestions, the user picks the look): Rowlan Meadows or Valleys + BK Meadows/AsianValley surface, Alpine Mountains + BK Mountains, Swamp + BK Jungle. BK Highlands art isn't installed, so pair Rowlan Highlands with another installed biome.
- Only the **4 installed BK biomes** resolve: AsianValley, Jungle, Meadows, Mountains. `Presets` already filters out the rest.
- Presets carry height/slope filters tuned to their demo terrain. A preset that seems to do nothing is usually filtered out by height or slope. `Inspect` the child stamps and widen `filterSet.heightFilter` / `slopeFilter` with `Edit`.
- Preset footprint = the `FalloffOverride` root's scale. `AddPreset` sizeX/Z sets it; the Content Browser default is 300×300.
- BK `Biomes` presets are whole stacks (texture + trees + details + clear + objects). They're good for quick dressing, but they add many detail layers. Check GPUI cost (§6).

## 6. Vegetation and objects

- `TreeStamp` → Unity terrain trees (`SetTreeInstances`; Y-rotation only, primitive colliders only). `DetailStamp` → terrain detail layers, **one prototype per stamp** (share a `DetailPrototypeSettings` asset so near-identical stamps don't become separate layers). `ObjectStamp` → **real GameObjects** in a `"<stamp> (Container)"` under MicroVerse; they survive stripping, get recycled on every rebuild (manual edits are lost) and are much heavier, so keep them for props needing scripts, colliders or full rotation.
- **`TreeStamp`/`ObjectStamp` with `poissonDisk == null` place nothing.** Only their inspector auto-assigns `Packages/com.jbooth.microverse/Data/microverse_default_poissondisk.poissondisk`. `AddPreset` assigns it; set it yourself when creating these stamps from code.
- `ClearStamp` removes trees/details/objects from stamps **before** it (paths, clearings). `OcclusionStamp` blocks stamps **after** it. `SplinePath.clearTrees/clearDetails` clears along a path.
- **GPU Instancer Pro renders the vegetation** (PLUGINS.md): keep the terrain's Unity `detailObjectDistance` at 0 and let the `GPUIDetailManager` draw details. MicroVerse's **terrain-settings panel on the MicroVerse inspector rewrites `detailObjectDistance`, `treeDistance` and `drawTreesAndFoliage` on every terrain**, so don't apply it without restoring those. GPUI auto-adds prototypes but never removes them: after removing a vegetation stamp, call `ResetPrototypesFromTerrains()` on the GPUI manager (Prototype.unity has a stale `FlowerMeadow2`). If GPUI details look stale after a rebuild, bake (`Rebuild true`) or `GPUITerrainAPI.RequireUpdate(manager, true, true)`.

## 7. Splines (paths, roads, rivers, areas)

- `SplinePath` (on a GameObject with a Unity `SplineContainer`, under MicroVerse): carves (`trench`, `width`, `smoothness`, `heightBlendMode` Override/Max/Min/Blend), paints (`layer`, `embankmentLayer`), clears vegetation. There's no menu item: add the component, or use the Content Browser's spline paint mode. Per-knot widths only work on the first spline of a container.
- `SplineArea` + stamps with `falloff.filterType = SplineArea` (4) and `falloff.splineArea` = that area confines any stamp group to a drawn shape (biome borders). Put a `FalloffOverride` on the group root to apply it to all children.
- **Rivers:** Stylized Water 3's `RiverModeler` has a MicroVerse section (`microVersePathSettings`) that drives a `SplinePath`'s trench and width from the river spline. Use it instead of hand-tuning a riverbed.
- **Staggart Spline Spawner** (fences, rocks along a spline) doesn't listen for height changes. Respawn it after reshaping terrain under it.

## 8. After terrain changes (dependents)

- **A\***: no automatic rescan. `Navigation.prefab` scans its grid on startup (fine). Any cached graph must be rescanned and its cache regenerated (tools-unity-astar).
- **Stylized Grass** colour map (`GrassColorMapRenderer`): re-render after texture changes.
- **GPUI** terrain managers: see §6.
- **Lighting/occlusion bakes**, if a scene has them, are stale.
- Commit the TerrainData `.asset` files and their `.meta`s with the scene. They are binary (several MB each).

## 9. Pitfalls checklist

- Nothing rebuilds → `set_autotick` off, MicroVerse component disabled, or the stamp isn't under the MicroVerse root.
- Changed fields from code, re-ordered the hierarchy or re-enabled MicroVerse, and nothing happened → only transform moves and enable/disable auto-invalidate. Call `Invalidate()` (the kit does).
- `AddComponent<HeightStamp>()` resets the GameObject's Y to 0 (version migration in `OnEnable`). Set the position **after** adding.
- A stamp created in code defaults to `falloff.filterType = Global` (hard rectangle), not the Box the inspector shows. Set it.
- New stamps with `stampVersion = 0` get their noise frequencies rescaled once on non-1000 m terrains. Set `stampVersion = 1`.
- `SampleHeight`, raycasts or colliders return old heights → the CPU copy is stale. `Rebuild` (it syncs) or save.
- Seams between tiles → a terrain has `allowAutoConnect` off, Y ≠ 0, or mismatched resolution or size.
- Black terrain patches → no global base texture stamp.
- Stair-stepped slopes → an 8-bit heightmap. Use 16-bit sources imported as Default type, sRGB off, R16, not readable, Clamp, max size ≥ source (all Rowlan packs already are).
- Heights clamp flat at the top → the stamp exceeds terrain size.y.
- A detail stamp in `Add` with a negative Y offset only ever raises → the shader `saturate`s the stamp height before combining, so `Y + tex * scale.y` below 0 becomes 0. For zero-mean detail, keep Y = 0 and lower the base by `median * amplitude * weight` instead (the world map does this in its generator, `AgentScripts/Maps/WorldMap/WorldMapLayout.cs`).
- Region-limited stamps: `Texture` falloff samples in the stamp's own UV, but one terrain-space mask works for any unrotated square stamp with `textureRotationScale = (0, size / terrainSize, centreX / terrainSize - 0.5, centreZ / terrainSize - 0.5)`. Fade the mask to 0 before the stamp's square edge, or the stamp ends in a step.
- Heavy `run_script` entries fail with "Main thread operation timed out after 30000ms" → make them `async Task<object>`, run the math in `Task.Run`, and pass `--timeout_ms`.
- After importing generated textures, use `AssetDatabase.WriteImportSettingsIfDirty` + `ImportAsset`, not `TextureImporter.SaveAndReimport()` → in this project, the first generated-map import also wrote unrelated vendor materials that were dirty in memory (Unity's `m_AllowLocking` upgrade).
- Only one MicroVerse per scene. "Create MicroVerse For Existing Terrains" opens a modal dialog, so don't run it from the CLI (it stalls).
- Noise filters: MicroVerse FBM spans only about ±0.47 (p10/p90 ±0.14), and weight = saturate(1 + (n − balance) × amplitude). Amplitude 3 is a soft ramp that sprinkles everywhere; 8–24 gives hard-edged drifts. Stamps with identical frequency/amplitude/balance/`offset` get identical patterns, so vary `offset`. Tree stamps keep each candidate with probability = weight.
- Screenshots still show objects a script deleted or disabled → URP's GPU Resident Drawer keeps their instances until the scene reloads. Save and reopen the scene. **Don't** force `EditorUtility.RequestScriptReload()` right after MicroVerse work: on the world map that left the TerrainData's managed wrapper pointing at another object (`terrain.terrainData` threw InvalidCastException), and the next scene save crashed the editor.
