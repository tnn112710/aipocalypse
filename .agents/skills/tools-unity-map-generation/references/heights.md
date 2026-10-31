# Layout, heights and masks

How the WorldMap generator (`AgentScripts/Maps/WorldMap/WorldMapLayout.cs`, "WML") turns a layout into a terrain, written as a recipe for any map. Line numbers are from the 2026-10-23 file; search by symbol if they moved. An earlier 1500 m rescale of the generator ("v23", not on develop) had several improvements the current file lacks (marked **v23**).

## 1. Layout as data

All authored shapes are plain tables in metres at the top of the generator (x east, z north). Everything downstream (heights, masks, water meshes, ribbons, dressing, checks) derives from them.

| Table | Shape | WML |
| --- | --- | --- |
| Playable edge, biome regions | closed polygons, smoothed by closed Catmull-Rom ×4 | `Playable` 34, `HighlandsArea` 44, `MeadowArea` 51, `SwampArea` 57 |
| Basins (lake, ponds) | shore polygon + level, depth, deepDistance, bankSlope | `Basins` 160–184 |
| Lines (rivers, streams) | knots `(x, z, surfaceY, halfWidth)` + depth, bankSlope, meander, seed; open Catmull-Rom ×6 | `Lines` 234–276 |
| Anchors | `(x, z, h, r)` Gaussian height targets | `Anchors` 283–314 |
| Bumps, hills | additive Gaussians (knolls, meadow hills/hollows) | 317, 325 |
| Islands, landmarks | points, frames (`HomeBase`, `HomeYaw`, `Bridge`) | 92–130, 339 |
| Detail stamps | Rowlan texture, centre, size, amplitude, median, mask | `Details` 361 |

- **Anchors on a grid (v23).** v13 had ~110 hand-placed anchors; v23 replaced them with a 100 m `AnchorGrid[z,x]` (r 70). A grid reads straight off a gridded concept, rescales by adding rows, and is easy to review. Prefer it.
- **Frames for traced sub-layouts.** A vendor demo layout (the Komorebi pond) is traced once in its own frame and mapped with a transform that has an exact inverse (`PondXf`/`PondXfInverse`, radii scaled by `sqrt(stretch)`, WML:66–91). Moving or stretching the feature then re-maps everything authored in that frame.
- **One input per landmark.** The home is `HomeBase` + `HomeYaw` + `FenceHalf`; yard, gate, spawn, masks and trail starts derive from them (WML:92–130).

## 2. The height function

One pure `HeightAt(x, z, out waterSurface)` returns ground and water surface (−∞ when dry). Order (WML:718–798):

1. **Region weights** from polygon signed distance: `w = 1 − smoothstep(−blend, blend, sd)`; blends 18–30 m with ~6 m edge noise. The background biome is `1 − others`.
2. **Regional field**: Gaussian-weighted average of anchors, `h = Σ w·a.h / Σ w`, `w = exp(−d²/r²)`, plus a tiny prior weight so empty areas fall back to a default height.
3. **Bumps**: knolls +2…+9 m (r 16–32), meadow hills/hollows ±2…5 m (r 22–50). Fade hills 25–55 m and hollows 6–25 m from water: raised ground near water lets the bank caps draw long straight planes.
4. **Rolling noise, domain-warped**, all frequencies in world metres (size-independent): warp 40 m at λ 260 m; roll = fbm(λ 150, 5 oct); ridges = 1 − |fbm(λ 110)|; amplitude per biome (meadow 1.2, forest 3.5, highlands 2.0 + ridges 10).
5. **Wet flat biome** (swamp): floor = water + 0.35, pools −2.6 m where fbm > 0, hummocks; floor forced above its own water within 5–16 m of any *lower* stream so the swamp sheet never floats over it (WML:757–773).
6. **Water** (references/water.md §2), then islands, then clamp to `[0, size.y − 1]`.

Values that worked on the 1000 × 300 × 1000 m map: meadow 23–32 m, forest 24–48, highlands 40–83, water at 19.5 m (keep ~20 m of ground under the lowest water so channels can be carved).

Performance: cache region weights per sample (Generate went 129 s → 55 s), early-out by bounds for every basin/line, and don't compute polygon SDFs you don't use (`HeightAt` computes `dPlay` and never reads it, WML:721).

## 3. Base heightmap → MicroVerse

- `Generate(res, maskRes, preview)` (WML:409): `await Task.Run(() => Parallel.For(...))` over texel centres `(i + 0.5) / res · size`, write `Terrain/Heightmaps/Base.png` as **R16** (value / size.y · 65535), one mask per detail stamp, then `AssetDatabase.Refresh()` and configure each importer. Delete stale outputs of older layouts.
- Importer (`ConfigureImporter`, WML:2835): Single Channel/Red, sRGB off, no mips, Clamp, Bilinear, not readable, NPOT none, R16 uncompressed (default + Standalone override), max size ≥ res. Apply with `WriteImportSettingsIfDirty` + `ImportAsset`, never `SaveAndReimport` (it wrote unrelated dirty vendor materials).
- `BuildHeights()` (WML:937) deletes and rebuilds the `Height` group: `00 Base` = HeightStamp **Override**, Global falloff, centre (size/2, 0, size/2), scale (size, size.y, size) → texture 1.0 = size.y. Then `20 Region Detail`, `90 Gameplay Pads`. Number prefixes keep the evaluation order readable.
- Then kit `Rebuild false` before anything samples the terrain (CPU heights are stale until a sync).
- Resolutions: heightmap ≤ 0.5 m per texel where water is carved (1000 m → 2049, base PNG 2048; 1500 m → 4097, base 4096). Height masks ~1 m (1024 on 1000 m). Unity's heightmap max is 4097, so beyond ~2 km at 0.5 m you need tiles. Generate at 4096 takes ~5 min: pass a long `--timeout`.

## 4. Rowlan detail stamps as zero-mean relief

MicroVerse `Add` can only raise: the shader saturates `Y + tex · scale.y` to `[0, 2·size.y]` before combining (`HeightmapStamp.shader:212`), so a negative Y offset is lost.

Recipe (WML:358–404, 986–995):
- Detail stamps at **Y = 0, Add**, footprint and amplitude from the table, each with its own terrain-space mask.
- Pre-lower the analytic base by `median · amplitude · mask` per stamp, so the result is `H + Σ mask · amp · (tex − median)`: relief with zero median, macro shape kept. Medians come from `StampSheet.Make` (GPU blit, p50; the textures aren't readable). Used: Highlands 09 0.64, 07 0.68; Meadows 05 0.45, 16 0.39, 26 0.43; Swamp 12 0.40.
- Mask hookup: falloff `Texture`, channel R, params (1, 0), `textureRotationScale = (0, size/terrainSize, cx/terrainSize − 0.5, cz/terrainSize − 0.5)`, **`falloffRange.y = 1`**. With y = 1 MicroVerse applies no edge fade, so the mask itself must fade to 0 over the outer ~15% of the footprint (`SquareFade`) or the stamp ends in a step.
- Mask = region weight × water fade `smoothstep(3, 26, waterDist)` × pad fade × inside-playable × square fade. The water fade matters: a stamp dip near water drops the bank below the surface.
- Sizes/amplitudes (1000 m): Highlands 09 560 m/26 m, Highlands 07 300/10, Meadows 05 640/9 and 300/6, Meadows 16 300/4 and 240/4, Meadows 26 380/3.5, Swamp 12 380/3.5.
- **Don't enlarge detail stamps to cover a bigger region** (v23's swamp 380 → 600 m stretched the micro-relief into a bare mud flat): tile more stamps instead.
- `Delta()` (WML:639) reports per region what the stamps changed; p50 ≈ 0 confirms zero-mean.

The terrain-space mask trick works on HeightStamps only. On Texture/Tree/Detail/Clear stamps `clampTexture = true` ignores `textureRotationScale`, and `false` uses a repeating sampler, so for those use whole-terrain stamps with terrain-sized masks (references/surface.md).

## 5. Gameplay pads

Flat Override HeightStamp, no texture (flat at its Y), last in the height group, `Range` falloff (0.55, 1) + Smoothstep easing: flat to 0.275 × footprint, zero at 0.5 × footprint (WML:956). Pre-shape the analytic field so the pad blends (an anchor + a rise under it) and fade detail masks 22–45 m from the pad. `Verify` reports the pad's min/max.

## 6. Checks and previews

| Tool | Reads | Use |
| --- | --- | --- |
| `Preview(res, png)` (WML:459) | analytic field only | Shaded map with water tint, 5 m contours, outlines, points. Iterate the layout tables and get approval before touching assets. `res` must be a multiple of 1024 (`step = res / 1024`); colour ramp is hard-coded to 18/32/70 m. |
| `MacroCheck()`, `Profile()` (**v23** only) | analytic | Lowest bank 3 m and 10 m outside each channel vs its surface (negative = perched or spilling), basin rims 8 m out, waterfall lip, pad. Cheap pre-bake gate: port it before the next water change. |
| `Verify(dir)` (WML:489) | synced CPU heights (`Rebuild` first) | Basin depth and leak cells in a 3–6 m ring, line stations every 20 m (depth, banks at +2/+8/+20 m, slope), region height/slope percentiles, `macroDriftInWater`, pad, a 5 m CSV grid. |
| `Delta()` (WML:639) | terrain − analytic | What detail stamps and pads changed per region. |
| `Hillshade(...)` (WML:663) | terrain or analytic | Exaggerated hillshade PNG; diff macro vs final to find stamp seams, creases and cut-off cliffs. |
| `StampSheet.Make` | vendor heightmaps | Contact sheet + p10/p50/p90 and edge mean (does the stamp fade to black?). |
| kit `Sample` | CPU heights | Small spot grids. |

## 7. Gotchas

- **Smooth-min overshoot.** Polynomial smooth-min dips up to k/4 below both inputs; clamp to `min(h, cap) − 0.03` or banks sink under the surface (WML:782).
- **Hard cut-offs leave cliffs.** Basin caps stop at 120 m, line caps at 150 m; where a cap still binds there, the ground jumps. v23 fades the cap out first (`+ 400 · smoothstep(80, 118, d)`); the current file doesn't (latent on low ground).
- **One canonical waterline.** The carve uses a noisy width/shoreline; masks, dressing and ribbons must use the same function (`TrueLineEdge`, `WaterEdgeDistancePublic`), not the plain one. WML has five near-duplicates; new code should use one.
- **Hard-coded size literals** (`1000`, `998`, `995`, `500`, `200` columns, island fallback 19.5, `farClip 4000`) all had to change for v23. Write `size` everywhere.
- **Seeds and order.** Every builder takes a seed; a new random consumer inserted mid-pass reshuffles everything after it. Give new passes their own `System.Random`.
- **Lazy shared data inside `Parallel.For`** needs locking (`Line.pts` uses volatile + lock). Build derived data before the loop instead.
- `AddComponent<HeightStamp>()` resets Y to 0 and new stamps default to Global falloff: set position and falloff after adding.
- Ellipse helper ignores its `n` argument (always 24 points, WML:2924).

## 8. Changing the map size

What v23 (1000 → 1500 m) taught:
- **Retrace, don't scale.** Feature sizes stay in metres (noise wavelengths, bank slopes, widths, depths, island shapes). Only the layout moves.
- **Parameterise the frame**: size, size.y, resolutions, far clip; derive `hmRes ≈ nextPOT(size / 0.5) + 1` (max 4097), masks ≈ 1 m per pixel, Preview res = 1024 · ceil(size / 1000).
- **MicroVerse UV-space parameters must be converted**: slope filter values (the same real slope reads ~1.5× steeper on 1500 m than on 1000 m at 300 m height; v23 `SlopeUnits(v) = 64.4 · atan(size/1000 · tan(v / 64.4))`, empirical) and **texture-stamp** noise frequency (× size/1000; vegetation stamps scale themselves). See unity-microverse §9.
- **Far clip ∝ map size**: the player far clip is 1500 m; v23 overrode it to 3000 m on the Cinemachine lens and camera as scene overrides.
- **Steepen bank caps where water is raised** (v23 `FallCliff`: a river feeding a waterfall sat 11 m above the ground until the pond's cap was steepened behind the lip).
- **Swap TerrainData on the same Terrain object** to keep GPUI/MicroVerse/collider references (v23:1019).
- Settle the size with a traced preview and a coordinate table before porting anything: ~7 hours went into rescale variants (1200 m, three 1500 m restarts) that were thrown away.
