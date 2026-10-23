# MicroVerse 1.7.0 API (checked against source)

Namespace `JBooth.MicroVerseCore` everywhere (add-ons compile into the same `JBooth.MicroVerseCore` assembly via `.asmref`; editor code is in `JBooth.MicroVerseCore.Editor`, the browser in `JBooth.MicroVerseCore.Browser`). Source: `Packages/com.jbooth.microverse/Scripts/` (+ `Packages/com.jbooth.microverse.<module>/Scripts/`). Enum values below are what `Edit` JSON needs.

## MicroVerse (`Scripts/MicroVerse.cs`, `[ExecuteAlways]`)

| Member | Notes |
| --- | --- |
| `static MicroVerse instance` | cached, else `FindFirstObjectByType` |
| `Terrain[] explicitTerrains`, `Terrain[] terrains` (get), `SyncTerrainList()` | search: explicit → `options.settings.terrainSearchMethod` (`Hierarchy` children, default / `AllInScene`). Forces `drawInstanced` |
| `Invalidate(Bounds? bounds = null, InvalidateType type = All)` | deferred to `EditorApplication.update`. `enum InvalidateType { All, Splats, Tree }` |
| `Modify(bool writeToCPU = false, bool noAsync = false, bool boundsCull = false)` | the rebuild; no-op while the component is disabled. `Modify(true, true)` = synchronous + CPU heights. `boundsCull` is ignored (culling uses the bounds of pending `Invalidate` calls). It doesn't clear a pending `Invalidate` (private `needUpdate`), so `Invalidate(); Modify(true, true)` runs a second async rebuild on the next editor tick unless you clear the flag (the kit's `Rebuild` does, by reflection) |
| `SaveBackToTerrain(bool forceFinishSpawnProcssing = false, bool saveHoles = true)` | syncs heights/alphamaps/holes (+ flushes vegetation) into TerrainData. Doesn't `SetDirty` or save the asset |
| `CancelModify(bool cancelRoads = true)`, `IsModifyingTerrain`, `IsHeightSyncd` | state |
| `static UnityEvent OnBeginUpdating / OnFinishedUpdating / OnCancelUpdating`, `static event OnTerrainLayersChanged` | editor hooks |
| `options.settings`: `keepLayersInSync`, `maxHeightSaveBackPerFrame = 1`, `useSceneCulling`, `controllFoliageDraw`, culling distances, `SyncHolesOnSave` | `options.gizmoVisibility` (`GizmosOff, Culled, AlwaysVisible`) |

Auto-invalidation only happens on stamp transform moves (`Stamp.Update`, editor) and stamp `OnEnable`/`OnDisable`. Field changes, re-ordering and re-enabling MicroVerse need an explicit `Invalidate()`.

Inspector "Sync to Terrain (Save)" = `Modify(true, true, false); SaveBackToTerrain();`. Scene save calls `SaveBackToTerrain(saveHoles: false)`.

Bake to plain terrain: sync + save, then `mv.enabled = false`. Re-enabling regenerates from the stamps and overwrites manual edits.

## Stamp base (`Scripts/Stamps/Stamp.cs`)

`int stampVersion` (0 → noise/density get rescaled once by `1000 / terrainSize.x` in `RevisionAllStamps`; set 1 from code), `GetBounds()` (square of `max(scale.x, scale.z)`, rotation-expanded), `IsEnabled()` (`activeInHierarchy && enabled`), `StripInBuild()`.

## HeightStamp (`Stamps/HeightStamp.cs`) : IHeightModifier

| Field | Type / default |
| --- | --- |
| `stamp` | `Texture2D` (red channel; null = flat pad at the stamp's Y) |
| `mode` | `CombineMode`, default `Max`: `Override=0, Max=1, Min=2, Add=3, Subtract=4, Multiply=5, Average=6, Difference=7, SqrtMultiply=8, Blend=9` |
| `falloff` | `FalloffFilter`; **default filterType Global (hard rectangle)** |
| `twist` | -90..90 |
| `erosion`, `erosionSize` | 0..600, 1..90 (=4); slope-based shader erosion |
| `power` | 0.1..8 (=1) |
| `invert` | bool |
| `useHeightRemap`, `remapCurve` | call `ClearRemapCurve()` after changing the curve |
| `blend` | 0..1, for `Blend` mode |
| `scaleOffset` | Vector4 tiling/offset (1,1,0,0); non-default switches wrap to Repeat |
| `tiltX`, `tiltZ`, `tiltScaleX`, `tiltScaleZ` | -1..1 |
| `mipBias` | 0..6, smoothing; the texture needs mips |

Result height = `lerp(old, combine(old, saturate(stampY + tex * scale.y)), falloff * (1 - occlusion))`, clamped to `[0, terrain size.y]`; the `saturate` works in units of 2·size.y (`HeightmapStamp.shader:212`), so a negative stamp height becomes 0 before combining and `Add` can never lower. Outside the stamp's rectangle the old height passes through. Add/Subtract also add the stamp's Y offset.

**`OnEnable` migration**: a new stamp (`version 0`, mode Max) has `transform.position.y` set to 0. Position the GameObject after `AddComponent`.

## HeightAreaEffectStamp (`Stamps/HeightAreaEffectStamp.cs`)

Modifies the heights accumulated by the stamps **before** it, inside its falloff.
- `effectType`: `Terrace=0, Beach=1, RemapCurve=2, Noise=3`
- `falloff`
- Terrace: `terraceSize` (0.05..20, =1), `terraceStrength` (0..1)
- Beach: `beachDistance` (0.05..100, =5), `beachPower` (0.25..4); pulls heights toward the stamp's world Y
- `remapCurve`: after changing it, `DestroyImmediate(remapCurveTex)` and set it to null
- Noise mode: `noise`, `combineMode` (HeightStamp.CombineMode, default Add), `combineBlend`

## Other core stamps

- **MeshStamp**: depth-renders `targetObject` into heights. Fields: `hideRenderers` (also destroys the target in builds), `offset`, `heightScale`, `heightClamp`, `resolution` (`k32…k2048`, default k256), `falloff`, `blur` (0..24), `blendMode` (`Add=0, Subtract=1, Fillaround=2, Connect=3`), `connectHeight`. Fits rocks or buildings into terrain. No menu item.
- **CopyPasteStamp**: replays a captured `CopyStamp` asset.
  - Flags `copyHeights/Texturing/Trees/Details/Holes` and `applyHeights/Texturing` (true), `applyTrees/Details/Holes` (false).
  - `pixelQuantization` (turn it off to rotate or scale).
  - Hidden child `heightStamp` holds the paste mode and falloff.
  - Capture with the public static `CopyPasteStampEditor.Capture(CopyPasteStamp, string assetPath)`. The class is internal, so call it via reflection:
    ```csharp
    var t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("JBooth.MicroVerseCore.CopyPasteStampEditor")).First(x => x != null);
    t.GetMethod("Capture", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { stamp, "Assets/_Game/Maps/<Map>/Terrain/Stamps/Base.asset" });
    ```
  - Size the stamp to the source area first (scale = terrain size for a whole tile). It captures at the current resolution.
- **TextureStamp**: `layer` (TerrainLayer), `filterSet`, `ignoreOcclusion`.
- **HoleStamp**: `filterSet` (URP terrain holes are enabled in `PC_RPAsset`).
- **OcclusionStamp**: `occludeHeightWeight/TextureWeight/TreeWeight/DetailWeight/ObjectWeight` (0..1, all 0 by default), `filterSet`. Blocks **later** stamps.
- **PaintFalloffArea**: `paintMask`, `clampOutsideOfBounds`. Referenced via `falloff.paintArea`; multiplies with the stamp's falloff.
- **FalloffOverride** (MonoBehaviour on a parent): `filter`. All stamps below it use this falloff instead of their own. BK presets use it at their root.

## FalloffFilter (`Scripts/FalloffFilter.cs`)

- `filterType`: `Global=0, Box=1, Range=2, Texture=3, SplineArea=4, PaintMask=5`. Default Global. Global on a HeightStamp = full weight inside its rectangle; on filter-set stamps (texture/tree/detail/clear/occlusion) = the whole terrain.
- `falloffRange` Vector2 (0.8, 1):
  - Box uses `.y` only: 1 = hard edge, lower = softer.
  - Range is radial: fades from radius `x` to `y` (fractions of the half-footprint).
- Texture falloff: `texture`, `textureChannel` (`R=0, G=1, B=2, A=3`), `textureParams` (amplitude, balance), `textureRotationScale` (rot, scale, offset xy), `clampTexture`.
  - Height stamps always map the texture through `textureRotationScale` (`HeightStampFiltering.cginc`): `uv' = (stampUV − 0.5) · scale + 0.5 + offset`. `clampTexture` is a no-op there.
  - Filter-set stamps with `clampTexture` sample the stamp's own UV and **ignore** `textureRotationScale` (`Filtering.cginc:409-412`); without clamp they apply it with a repeating sampler. Under a `FalloffOverride` the clamp flag is read from the child stamp.
  - The texture term is multiplied by the Box term of `falloffRange.y`: 1 = no edge fade (the mask must fade itself).
- `easing.blend`: `Linear=0, Smoothstep=1, EaseIn=2, EaseOut=3, EaseInOut=4`.
- `noise` (edge noise; ignored for Global).
- `paintArea`, `paintMask`.
- `splineArea`, `splineAreaFalloff`, `splineAreaFalloffBoost` ("Width Boost": pushes the area outside thin open splines).

## Noise (`Scripts/Noise.cs`)

- `noiseType`: `None=0, Simple=1, FBM=2, Worley=3, Worm=4, WormFBM=5, Texture=6`
- `noiseSpace`: `World=0, Stamp=1`
- `frequency` (=10), `amplitude` (=1, negative inverts), `offset`, `balance` (-0.5..0.5)
- Frequency units depend on the stamp: TextureStamp passes no scale (`TextureStamp.cs:121`), so it is cycles per **terrain tile**; Tree/Detail/Clear/Object/Hole/Occlusion stamps pass `size.x / 1000` (`Stamp.cs:136-141`), so cycles per **1000 m**. First-octave cell ≈ tile / frequency (texture) or 1000 / frequency m (vegetation).
- `offset` is a scalar added to both axes in **noise cells** (`Noise.cginc:141-144`): offsets closer than ~1 give near-identical patterns.
- Weight noise: `w = saturate(1 + (FBM − balance) · amplitude) · weight` (`Filtering.cginc:206-224`); FBM spans about ±0.47.
- `texture`, `textureST`, `channel`

## FilterSet (`Scripts/FilterSet.cs`) on Texture/Hole/Occlusion/Tree/Detail/Object/Clear stamps

- `falloffFilter` (FalloffFilter)
- `weight` (0..1)
- `weightNoise`, `weight2Noise`, `weight3Noise`, plus `weight2NoiseOp`, `weight3NoiseOp` (`Add=0, Subtract=1, Multiply=2, Overlay=3, Min=4, Max=5`)
- Filters, each `{ enabled, filterType (Simple=0, Curve=1), weight, range, smoothness, noise, mipBias, curve }`:

| Filter | Default range | Default smoothness | Units |
| --- | --- | --- | --- |
| `heightFilter` | (0, 500) | (20, 20) | world metres above the terrain origin |
| `slopeFilter` | (0, 18) | (4, 4) | **not degrees**: compared with `(π/2)·acos(n·up)` (`Filtering.cginc:338`), so ≈ degrees × 1.57 on the 1000 × 300 m WorldMap, and it shifts with terrain width vs height (SKILL.md §9) |
| `angleFilter` | (0, 90) | (12, 12) | facing degrees |
| `curvatureFilter` | (0.6, 1) | (0.1, 0.1) | 0..0.5 concave, 0.5..1 convex |
| `flowFilter` | (0.6, 1) | (0.1, 0.1) | 0..1 |

- Texture filter: `textureFilterEnabled`, `otherTextureWeight`, `textureFilters` (list of `{ layer, weight, amplitude, balance }`)
- Range semantics: full weight inside `[range.x, range.y]`, fading to 0 over `smoothness` **outside** the range (`FilterRangeSmoothstep`, `Filtering.cginc:176-186`).
- `version`: **set `version = 2` when authoring slope values from code**. Version 0/1 data is migrated by ×1.579 on deserialise (and clamped to 90). Version 2 doesn't make the values degrees.

Example `Edit` JSON for a rock-on-slopes texture stamp (54–95 ≈ 34°–60° on the WorldMap terrain):
```json
{"filterSet":{"version":2,"falloffFilter":{"filterType":0},"slopeFilter":{"enabled":true,"range":{"x":54,"y":95},"smoothness":{"x":5,"y":0}}}}
```

## Vegetation / Objects / Splines (add-ons)

- **TreeStamp**: tree placement.
  - `prototypes` (`List<TreePrototypeSerializable>` with `prefab`, `bendFactor`, `navMeshLod`) paired with `randomizations` (same length).
  - `seed`, **`poissonDisk`** (null = nothing spawns), `poissonDiskStrength`, `density` (0.1..20).
  - `occludeOthers`, `occludedByOthers`, `min/maxDistanceFromTree/Object/Parent`, `sdfClamp`, `minHeight`, `heightModAmount/Width`, `layer/layerWeight/layerWidth`, `filterSet`.
  - Output: `terrainData.treeInstances`.
  - Candidates: jittered grid over the whole terrain, `512 · density² · (size/1000)²` per terrain, independent of the stamp's footprint (`TreeStamp.cs:606`).
  - Keep test (`TreeFilter.shader:234-241`): rejected if `random > w` (w not saturated); then with `densityByWeight`, `w ≤ 0.5` → rejected, else `w = (w − 0.5) · 2`.
  - `Randomization` (struct, one per prototype): flag bits are inverted for `disabled`, `densityByWeight`, `randomRotation`, `randomScale` (bit clear = true), so a struct built in code (`flags = 0`) is **disabled** with `densityByWeight`/`randomRotation`/`randomScale` on. Its scale ranges and `scaleMultiplierAtBoundaries` default to 0 (zero-size trees) and `weightRange` (0, 0) = no range test. From code set `disabled = false`, scale ranges, `scaleMultiplierAtBoundaries = 1`, `weightRange = (0, 99999)`, `lockScaleWidthHeight`. `OnEnable` migrates a version-0 stamp by enabling the randomizations that exist then: set `version = 1` if you add them after `AddComponent`. Prototype pick weight is `1 + weight`.
- **DetailStamp**: detail placement.
  - `prototype` (`DetailPrototypeSerializable`: `prototype` GameObject or `prototypeTexture`, `renderMode`, `usePrototypeMesh`, `useInstancing`, `density`, `min/maxWidth`, `min/maxHeight`, `alignToGround`, colours, `noiseSpread`, `positionJitter`…).
  - `settings` (a `DetailPrototypeSettings` asset overrides `prototype`), `filterSet`, `occludedByOthers`, distance filters, `weightRange` (default (0, 999999)). The prototype also has `targetCoverage`, `useDensityScaling`, `holeEdgePadding`, `noiseSeed`.
  - Output: one terrain detail layer per **distinct prototype value** (`DetailPrototypeSerializable.Equals` compares every field incl. `noiseSeed`, `DetailPrototypeSerializable.cs:55-85`). Stamps on one layer combine with **max** (`CombineDetailBuffers.shader:43-49`).
  - Coverage scatter mode writes `saturate(w)` per cell and Unity spawns by prototype density; instance-count mode writes `w · density / 128` (`DetailStamp.cs:309-336`).
  - Prefab with a LODGroup: MicroVerse substitutes child 0 when that child has the renderer; a LODGroup with the mesh on the root is rejected by Unity (`DetailPrototypeSerializable.cs:193-206`).
- **ClearStamp**: `clearTrees`, `clearDetails`, `clearObjects`, `filterSet`. Clears **earlier** stamps' spawns. Max 256 per terrain. The clear map keeps only the last clear stamp's index per pixel and accumulates all clear weights, so a spawn stamp sitting between two clear stamps is also hit by the earlier clear where the later one touches: keep clear stamps together at the end (`ClearFilter.shader:89-95`).
- **ObjectStamp**: GameObject spawning.
  - `prototypes` (GameObjects) + `randomizations` (rotation/scale ranges, locks `None/XY/XZ/YZ/XYZ`, `slopeAlignment`, `sink`…).
  - `spawnAsPrefab`, `parentObject`, `hideInHierarchy`, `seed`, **`poissonDisk`**, `density` (0.1..8), distance filters, `minHeight`, `filterSet`.
  - Output: GameObjects under `"<name> (Container)"` (`ObjectStampHolder`). These are not stripped from builds.
- **SplinePath** (`SplineContainer spline`): a spline that carves terrain and paints texture.
  - Blend and resolution: `heightBlendMode` (`Override=0, Max=1, Min=2, Blend=9`), `blend`, `treatAsSplineArea`, `sdfRes` (`k256…k2048`), `searchQuality` (64…2048).
  - Height: `modifyHeightMap`, `width`, `smoothness`, `trench` (+ = down), `useTrenchCurve`/`trenchCurve`, `heightNoise`, `embankmentEasing`/`embankmentNoise`.
  - Texture: `modifySplatMap`, `layer`, `splatWeight/Width/Smoothness`, `embankmentLayer`.
  - Clearing: `clearTrees/Details/Objects` + widths.
  - Occlusion: `occludeHeightMod/occludeTextureMod` + widths.
  - Per-knot widths: `splineWidths` (the docs say first spline only; `SplineRenderer.cs:164-214` reads them per spline: unverified in the editor).
  - Carve shape: the SDF distance is 0 inside the per-point width (`SplineSDFFill.shader:350-351`), and the trench curve is sampled at `distance / (width + smoothness)`, value = metres down (`SplinePathHeight.shader:156-173`); so the bed is flat across the width. Default `heightBlendMode` Override sets the band to the spline's Y before blending out.
  - After changing a curve from code: `ClearCachedSpline{Texture,Tree,Detail,Trench}Curve()`. Moving the stamp: `OnMoved()`.
- **SplineArea** (`SplineContainer spline`): `sdfRes`, `maxSDF` (=128, max falloff distance), `positionNoise`, `closedMode` (`Area=0, Path=1`). Stamps reference it through `falloff.filterType = SplineArea` + `falloff.splineArea`.

## Content browser data (editor assembly)

- `ContentCollection : BrowserContent` (`Scripts/Editor/ContentBrowser/ContentCollection.cs`, Create > MicroVerse > ContentPack).
  - Base fields: `contentType` (`Height=0, Texture=1, Vegetation=2, Objects=3, Audio=4, Biomes=5, Roads=6, Caves=7, Global=8, Paths=9`), `author`, `packName`, `id`.
  - Own fields: `previewGradient`, `contents[]` of `ContentData { prefab, childPrefab (unused in 1.7), previewImage, previewAsset, string stamp /* heightmap GUID */, previewGradient, description }`.
- `ContentAd : BrowserContent`: `requireInstalledObject` + `installedObject`. The browser hides collections whose **id** matches an ad with a missing object.
- Browser spawn of a height entry: `HeightStamp` with `mode = Add`, Box falloff (0.8, 1), scale (300, 120, 300), Y = 0. Shift-drag fits it to the terrain bounds. Prefab entries are instantiated and fully unpacked.
- Make a collection from your own heightmap folder: Genesis `Window/Genesis for MicroVerse/Converter` → Heightmap → "Create Height Stamp Collection" (then set `author`/`packName`/`id` by hand), or create the asset in code with `contents[i].stamp = AssetDatabase.AssetPathToGUID(png)`. Put a map-only collection in `Assets/_Game/Maps/<Map>/Terrain/Stamps/`, a reusable one in `Assets/_Game/Environment/Stamps/`. `contents` isn't initialised: an empty collection is null.

## Menu items (internal `MenuItems`, `GameObject/MicroVerse/...`)

"Create MicroVerse", "Create MicroVerse With Terrain" (writes `Assets/New Terrain.asset`), "Create MicroVerse For Existing Terrains" (**modal**), "Create Height Stamp", "…Height Area Effect", "…Hole", "…Occlusion", "…Paint Falloff Area", "…Texture", "…CopyPaste", "…Clear", "…Detail", "…Tree", "…Object", "Create Ambiant Area" (don't use). Created stamps get scale 100 and no Undo. `Window/MicroVerse/Content Browser`, `Window/MicroVerse/PoissonDiskGenerator`.

## Docs vs source

- The docs list 6 height modes. The source also has Average, Difference and SqrtMultiply.
- The docs say HeightAreaEffect applies "below it". In code it modifies the heights from stamps before it in the hierarchy.
- Content-browser height stamps are spawned in Add mode, not the Max default.
- The documented `childPrefab` behaviour is not implemented.
- The ObjectStamp spawn throttle is 1500 per frame, not the documented 1000.
- The documented example stamp path `Examples/Common/HeightStamps` is really `Packages/com.jbooth.microverse/Examples/HeightStamps`.
- The inspector's 256 alphamap option sets 258 (vendor bug).
- The docs and inspector call slope filter values degrees; they aren't (FilterSet table above).
- The MicroVerse inspector's terrain-settings panel writes every field to every terrain when any one changes; its Heightmap Scale field has no effect (it restores the old size).
- The editor disables `MicroVerse.instance` when entering play mode.
