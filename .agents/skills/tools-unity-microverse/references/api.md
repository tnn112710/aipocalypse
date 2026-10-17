# MicroVerse 1.7.0 API (checked against source)

Namespace `JBooth.MicroVerseCore` everywhere (add-ons compile into the same `JBooth.MicroVerseCore` assembly via `.asmref`; editor code is in `JBooth.MicroVerseCore.Editor`, the browser in `JBooth.MicroVerseCore.Browser`). Source: `Packages/com.jbooth.microverse/Scripts/` (+ `Packages/com.jbooth.microverse.<module>/Scripts/`). Enum values below are what `Edit` JSON needs.

## MicroVerse (`Scripts/MicroVerse.cs`, `[ExecuteAlways]`)

| Member | Notes |
| --- | --- |
| `static MicroVerse instance` | cached, else `FindFirstObjectByType` |
| `Terrain[] explicitTerrains`, `Terrain[] terrains` (get), `SyncTerrainList()` | search: explicit → `options.settings.terrainSearchMethod` (`Hierarchy` children, default / `AllInScene`). Forces `drawInstanced` |
| `Invalidate(Bounds? bounds = null, InvalidateType type = All)` | deferred to `EditorApplication.update`. `enum InvalidateType { All, Splats, Tree }` |
| `Modify(bool writeToCPU = false, bool noAsync = false, bool boundsCull = false)` | the rebuild; no-op while the component is disabled. `Modify(true, true)` = synchronous + CPU heights |
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

Result height = `lerp(old, combine(old, stampY + tex * scale.y), falloff * (1 - occlusion))`, clamped to `[0, terrain size.y]`. Add/Subtract also add the stamp's Y offset.

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
    t.GetMethod("Capture", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { stamp, "Assets/_Game/Data/Environment/<Area>/Stamps/Base.asset" });
    ```
  - Size the stamp to the source area first (scale = terrain size for a whole tile). It captures at the current resolution.
- **TextureStamp**: `layer` (TerrainLayer), `filterSet`, `ignoreOcclusion`.
- **HoleStamp**: `filterSet` (URP terrain holes are enabled in `PC_RPAsset`).
- **OcclusionStamp**: `occludeHeightWeight/TextureWeight/TreeWeight/DetailWeight/ObjectWeight` (0..1, all 0 by default), `filterSet`. Blocks **later** stamps.
- **PaintFalloffArea**: `paintMask`, `clampOutsideOfBounds`. Referenced via `falloff.paintArea`; multiplies with the stamp's falloff.
- **FalloffOverride** (MonoBehaviour on a parent): `filter`. All stamps below it use this falloff instead of their own. BK presets use it at their root.

## FalloffFilter (`Scripts/FalloffFilter.cs`)

- `filterType`: `Global=0, Box=1, Range=2, Texture=3, SplineArea=4, PaintMask=5`. Default Global.
- `falloffRange` Vector2 (0.8, 1):
  - Box uses `.y` only: 1 = hard edge, lower = softer.
  - Range is radial: fades from radius `x` to `y` (fractions of the half-footprint).
- Texture falloff: `texture`, `textureChannel` (`R=0, G=1, B=2, A=3`), `textureParams` (amplitude, balance), `textureRotationScale` (rot, scale, offset xy), `clampTexture`.
- `easing.blend`: `Linear=0, Smoothstep=1, EaseIn=2, EaseOut=3, EaseInOut=4`.
- `noise` (edge noise; ignored for Global).
- `paintArea`, `paintMask`.
- `splineArea`, `splineAreaFalloff`, `splineAreaFalloffBoost` ("Width Boost": pushes the area outside thin open splines).

## Noise (`Scripts/Noise.cs`)

- `noiseType`: `None=0, Simple=1, FBM=2, Worley=3, Worm=4, WormFBM=5, Texture=6`
- `noiseSpace`: `World=0, Stamp=1`
- `frequency` (=10; normalised to 1000 m terrains), `amplitude` (=1, negative inverts), `offset`, `balance` (-0.5..0.5)
- `texture`, `textureST`, `channel`

## FilterSet (`Scripts/FilterSet.cs`) on Texture/Hole/Occlusion/Tree/Detail/Object/Clear stamps

- `falloffFilter` (FalloffFilter)
- `weight` (0..1)
- `weightNoise`, `weight2Noise`, `weight3Noise`, plus `weight2NoiseOp`, `weight3NoiseOp` (`Add=0, Subtract=1, Multiply=2, Overlay=3, Min=4, Max=5`)
- Filters, each `{ enabled, filterType (Simple=0, Curve=1), weight, range, smoothness, noise, mipBias, curve }`:

| Filter | Default range | Default smoothness | Units |
| --- | --- | --- | --- |
| `heightFilter` | (0, 500) | (20, 20) | world metres |
| `slopeFilter` | (0, 18) | (4, 4) | degrees |
| `angleFilter` | (0, 90) | (12, 12) | facing degrees |
| `curvatureFilter` | (0.6, 1) | (0.1, 0.1) | 0..0.5 concave, 0.5..1 convex |
| `flowFilter` | (0.6, 1) | (0.1, 0.1) | 0..1 |

- Texture filter: `textureFilterEnabled`, `otherTextureWeight`, `textureFilters` (list of `{ layer, weight, amplitude, balance }`)
- `version`: **set `version = 2` when authoring slope values from code**. Version 0/1 data is migrated by ×1.579 on deserialise.

Example `Edit` JSON for a rock-on-slopes texture stamp:
```json
{"filterSet":{"version":2,"falloffFilter":{"filterType":0},"slopeFilter":{"enabled":true,"range":{"x":30,"y":90},"smoothness":{"x":6,"y":6}}}}
```

## Vegetation / Objects / Splines (add-ons)

- **TreeStamp**: tree placement.
  - `prototypes` (`List<TreePrototypeSerializable>` with `prefab`, `bendFactor`, `navMeshLod`) paired with `randomizations` (same length).
  - `seed`, **`poissonDisk`** (null = nothing spawns), `poissonDiskStrength`, `density` (0.1..20).
  - `occludeOthers`, `occludedByOthers`, `min/maxDistanceFromTree/Object/Parent`, `sdfClamp`, `minHeight`, `heightModAmount/Width`, `layer/layerWeight/layerWidth`, `filterSet`.
  - Output: `terrainData.treeInstances`.
- **DetailStamp**: detail placement.
  - `prototype` (`DetailPrototypeSerializable`: `prototype` GameObject or `prototypeTexture`, `renderMode`, `usePrototypeMesh`, `useInstancing`, `density`, `min/maxWidth`, `min/maxHeight`, `alignToGround`, colours, `noiseSpread`, `positionJitter`…).
  - `settings` (a `DetailPrototypeSettings` asset overrides `prototype`), `filterSet`, `occludedByOthers`, distance filters, `weightRange`.
  - Output: one terrain detail layer per stamp.
- **ClearStamp**: `clearTrees`, `clearDetails`, `clearObjects`, `filterSet`. Clears **earlier** stamps' spawns. Max 256 per terrain.
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
  - Per-knot widths: `splineWidths` (first spline only).
  - After changing a curve from code: `ClearCachedSpline{Texture,Tree,Detail,Trench}Curve()`. Moving the stamp: `OnMoved()`.
- **SplineArea** (`SplineContainer spline`): `sdfRes`, `maxSDF` (=128, max falloff distance), `positionNoise`, `closedMode` (`Area=0, Path=1`). Stamps reference it through `falloff.filterType = SplineArea` + `falloff.splineArea`.

## Content browser data (editor assembly)

- `ContentCollection : BrowserContent` (`Scripts/Editor/ContentBrowser/ContentCollection.cs`, Create > MicroVerse > ContentPack).
  - Base fields: `contentType` (`Height=0, Texture=1, Vegetation=2, Objects=3, Audio=4, Biomes=5, Roads=6, Caves=7, Global=8, Paths=9`), `author`, `packName`, `id`.
  - Own fields: `previewGradient`, `contents[]` of `ContentData { prefab, childPrefab (unused in 1.7), previewImage, previewAsset, string stamp /* heightmap GUID */, previewGradient, description }`.
- `ContentAd : BrowserContent`: `requireInstalledObject` + `installedObject`. The browser hides collections whose **id** matches an ad with a missing object.
- Browser spawn of a height entry: `HeightStamp` with `mode = Add`, Box falloff (0.8, 1), scale (300, 120, 300), Y = 0. Shift-drag fits it to the terrain bounds. Prefab entries are instantiated and fully unpacked.
- Make a collection from your own heightmap folder: Genesis `Window/Genesis for MicroVerse/Converter` → Heightmap → "Create Height Stamp Collection" (then set `author`/`packName`/`id` by hand), or create the asset in code with `contents[i].stamp = AssetDatabase.AssetPathToGUID(png)`. Put it under `Assets/_Game/Data/Environment/Stamps/`.

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
