# Water on a generated map

The procedure: design levels, carve beds into the height field, build Stylized Water 3 (SW3) surfaces on the carved shape, dress and verify. SW3 itself (components, material properties, RiverModeler and UnderwaterArea API, units, vendor gotchas) is in the `tools-unity-stylized-water` skill; read it before writing water code. WML = `AgentScripts/Maps/WorldMap/WorldMapLayout.cs`.

## 1. Decide the levels first

| Rule | Why |
| --- | --- |
| Decide which bodies the player can go **under**. Every connected body that needs underwater rendering must be **one flat level** (one `UnderwaterArea` per level). | SW3 underwater only works on flat water; river mode turns it off in the shader. The WorldMap pond, river and lake all sit at 19.5 m for this reason. |
| Sloped rivers and streams get no underwater, unless you terrace them into flat pools (one area each) or move a `Transform` level source with the camera (not built). | |
| Every dependent reads the level from **one constant** (`LakeLevel`), never a literal. | Mist heights in `Atmosphere.cs`, waterfall args and `RiverLevel.cs` kept their own copies and went stale. |
| Identify water bodies by polygon or id, never by level. | Once pond and lake shared 19.5 m, lake islands were counted as pond islands. |
| Line knots inside a basin carry the basin's level. | Otherwise the stream surface steps where it meets the pond. |
| Changing a level late is expensive. | Pond 23 → 19.5 m changed 4.4% of the terrain (max −4.6 m); objects 60+ m away dropped 2–3.8 m; bridge −2.7 m; 70 objects re-settled. |
| Leave ground under the lowest water. | The WorldMap water is at 19.5 m on a 300 m tall terrain so beds up to 10 m deep can be carved. |
| Depth vs the player: eye 1.7 m, no swimming (the player walks on the bed), slope limit 45°. | User spec: river 2.5–3 m at its deepest, crossable, banks walkable; streams 0.5–1.3 m; swamp 1–3.6 m; lake 8–10 m. |

## 2. Carve in the height function

Carve in the analytic field (references/heights.md), not with MicroVerse spline stamps: the bed, the surface and every mask then come from the same numbers. Each body adds a **levee** (combined with max) and a **cap** (combined with min):

```
h = max(h, levee)
h = max(SmoothMin(h, cap, 1.2), min(h, cap) - 0.03)   // clamp: smooth-min dips 0.3 m under both inputs
```

**Basin** (`WaterBasin`, WML:800). `d` = signed distance to the shore + 3.5 m fbm (λ 22 m), negative in water.
- Bed: `level + 0.05 − 0.9·S(0, 5, t) − (depth − 0.95)·S(3, deepDistance, t) + 0.35·fbm·S(2, 10, t)`, t = −d: a 0–5 m shelf dropping 0.9 m, then down to `depth` by `deepDistance`.
- Bank cone: `cap = level + 0.05 + slope·d` (slope = bankSlope × 0.75–1.35 noise), out to 120 m. **Fade it out before the cut-off** (v23: `+ 400·S(80, 118, d)`) or high ground gets a straight cliff where the cone stops.
- Lip: `levee = level + min(0.35, slope·d)` to 6 m, then falling 0.35 m per m (~19°), so a pond on a slope sits in a bowl instead of perching on a ridge.
- Values: lake depth 10 / deepDistance 90 / bankSlope 0.2; home pond 4.8 / 13 / 0.5; highland ponds 3 / 10 / 0.35.

**Line** (`WaterLine`, WML:823). `u = dist / w`, w = halfWidth × (1 + 0.15 fbm, λ 25 m).
- Bed: `s − 0.05 − (depth − 0.05)·taper·(1 − S(0.25, 1, u))`: flat deep middle, easing to a shallow shelf at the bank. A parabola is steepest at the waterline and showed as a white wall.
- Bank: `cap = s − 0.05 + slope·e` (river 0.3, streams 0.4–0.45), `levee = s + min(0.3, slope·e)` within 5 m, then dropping fast. Cap cut-off 150 m (fade it, as above).
- Variation along the line: Catmull-Rom ×6; meander `sin(π·arc/L) × noise` (zero at the ends); width `clamp(1 + env·(0.26·N(arc/85) + 0.1·N(arc/27)), 0.7, 1.38)`; depth `clamp(1 + env·(0.5·(1 − wv) + 0.15·N), 0.7, 1.35)` (deeper where narrower); `env = S(0, 35, min(arc, L − arc))` keeps the ends at design size so joins stay clean; optional pinch at a bridge (×0.85).
- Values: river half-width 12–24.5 m, depth 4.5 m; streams half-width 3.8–12 m, depth 1.0–1.3 m; outlet 2.7 m.

**Mouths** (`MouthTaper`, WML:859). A line end lying > 3 m inside a basin tapers its depth to 0 over `clamp(depth × 10, 8, 28)` m, so the bed meets the basin shelf (no pit, no trench through the shallows). Give the basin a blunt opening as wide as the flared channel: a pointed tip reads as a wedge of water.

**Islands**: `h = max(h, level + top − 9·max(0, d·n/r − 0.35)^1.6)`.

**Keep other relief off the water**: hills fade 25–55 m from water, hollows 6–25 m; detail-stamp masks × `S(3, 26, waterEdge)`.

**Bank cones are global.** A big, low basin caps everything within its cone: the 2.5× home pond at bankSlope 0.3 dropped the West Pond rim 6.5 m and a stream bank 5 m. Rule of thumb: for low water A at distance D from water B's rim that is Δ higher, `bankSlope_A × 0.75 ≥ (Δ + 0.5) / D`. Steeper banks then show the steep-rock texture: check the texture pass too.

**Perched channels.** A line whose surface is above the natural ground only gets a 0.3 m levee, so it runs in the air (v23's west river sat 11 m up until `FallCliff` steepened the pond cap behind the fall). `MacroCheck` catches it before baking.

## 3. Flat water from the carved shoreline

`FlatWaterMesh(name, outline, margin, cell, wet, feather)` (WML:1866):
1. Grid over the outline bounds + margin; a node is wet if `sd < margin && wet(x, z)`.
2. Seed with nodes well inside the shore, 4-connected flood fill through wet nodes: low ground beyond a rim is never reached, so no sheet floats over it.
3. Keep a quad when **any** corner is reached (no holes; edges tuck under the banks).
4. Vertices local to the outline centroid at y = 0; **transform Y = level**. Gameplay that needs the level (drowning, buoyancy) reads the transform: SW3's CPU height queries return 0 on wave-less water (tools-unity-stylized-water).
5. Vertex colour G = −1 everywhere (untouched), fading toward 1 under feeding channels.

`wet` for basins: CPU ground below `level − 0.02` (run kit `Rebuild false` first). Inside the shore polygon is **always** wet. Outside it, exclude cells within 3 m of a lower channel and cells up a channel that runs into this body.

Cell 2 m, margin 8 m; the waterfall source pool used cell 1, margin 0.5. Save with `SaveMesh` (Mesh API refill, keeps the GUID): `EditorUtility.CopySerialized` left the old GPU buffers on screen.

A flat sheet's edge must end under terrain or under an overlapping opaque surface with a vertex-colour fade, never in open water. A sheet cut on its 2 m grid inside a pond showed through the river as a staircase ("weird texture near the pond").

## 4. Rivers and streams (RiverModeler ribbons)

- Build each ribbon from the **same smoothed points that carved the bed** (`Line.pts`). A decimated copy drifts off the channel on curves.
- Width = carved half-width (same noise) **+ 0.8 m**, so the edges tuck under the banks.
- Where a line runs into a basin, trim the ribbon by **arc length past the shoreline crossing**, not by distance to the shore (a flared mouth runs along the shore and stayed "near the shore" 36 m into the lake). Kept: river 13 m into the pond and 16 m into the lake, streams 8 m.
- End fades in metres: SW3 multiplies the tip fade by the segment count, so set `fade = metres / segments`. Streams use `maxSegmentLength` 1000 so each has exactly 2 segments.
- One RiverModeler per kind (River, Outlet, Streams): one material and settings block for all its splines.
- Settings that worked: river baseWidth 30, maxSegment 100, displacement 0; streams baseWidth 12, displacement 0.15, cascade particles on; foam velocity 0.45; audio off (BroAudio owns audio). Build order and API: tools-unity-stylized-water.

## 5. Junctions

Design every junction and look at it from above and at eye level. The river-lake junction once had four separate faults at the same time.

| Junction | What worked |
| --- | --- |
| Line → lake / pond / swamp | Mouth taper; ribbon trimmed past the shore and fading out over the body; flat sheet fading out under the channel near the crossing (vertex G); shading matched so colours cross-fade. |
| Body → outflow (pond → river) | Inside the shore the sheet is always wet; the ribbon starts 13 m inside the pond and fades in over 8 m, covering the sheet's grid edge. |
| Stream → stream end to end | One spline (West + Margin): the junction stays opaque. |
| Branch into / off another stream | Separate splines in one container that start or end on the other's knot and fade over it. **Never link knots**: SW3 then fades a whole segment (the forest stream's first segment went invisible). |
| Flat body over a lower stream | Exclude sheet cells within 3 m of the lower channel; raise the wet floor near it (references/heights.md §2). |

## 6. Waterfall

`Landmarks.BuildWaterfall(px, pz, heading, top, pond)` (WML:3217), on the Komorebi kit:
- Sheet `Waterfall_1`: `sy = (top − pond) / 6.2`, width ×1.6; material copy at queue **3001** so it draws after SW3 water, flow ×0.42.
- Lip slabs 1 m under the source water, grass-topped notch walls, ripples, splash, SW3 impact splashes, a mist plume (shadows off), ring ripples, fog, a blue point light 0.4 m under the pool floor.
- Transparent effects at queue 3000 with no depth write need `sortingOrder` 1 to draw after the sheet.
- A small flat source pool at `top`.
- Pass pivot, top and pond level from the layout constants, not literals.
- Known gap: the sheet and other transparents draw after the underwater fog and stay unfogged.

## 7. Look

- **Connected water shares one palette**; vary it only by depth. Separate palettes per kind showed seams where bodies met. A disconnected, different body (swamp: olive, nearly opaque) may differ. (The shared palette was built for the 1500 m rescale; the WorldMap on develop still has four materials.)
- Warm/cream shallows over pebble beds read as a dry cobble path; directional caustics on a river read as white streaks.
- Vendor river materials run time ×16: use `_Speed` ~6 for calm water.
- Channel beds dark wet mud, not pale pebbles (a pale shelf read as a concrete curb through the water); channel textures stop at a lake or pond shore.
- Start generous: a main river 25–45 m wide on a 1 km map, ponds big enough to read from the bank, width ±30% along the line. First passes were too small and too uniform every time.

## 8. Order and dependents

Water steps run after `BuildHeights` + kit `Rebuild false` (they sample CPU heights): water materials → flat water → rivers → underwater → bridge / waterfall / water dressing → clear masks → `Rebuild true`.

After a water change, rebuild:

| Dependent | WorldMap entry |
| --- | --- |
| Heights | `Generate`, `BuildHeights`, kit `Rebuild false` |
| Bed, shore, sand, channel textures | `GenerateTextureMasks`, `BuildTextures` |
| Shore and reed vegetation | `Vegetation.GenerateMasks` |
| Sheets, ribbons, underside, underwater areas | `BuildFlatWater`, `BuildRivers`, `BuildUnderwater` |
| Bank, shore, pond dressing, islands | `Dressing.BuildBanks`, `BuildWaterEdges`, `BuildHomePond`, `Finishing.Islands` |
| Bridge (span = local river width; orientation follows the river tangent: anchor the knots around crossings) | `Landmarks.BuildBridge` |
| Waterfall (if the fall or the pond level moved) | `Landmarks.BuildWaterfall` |
| Boundary near the outlet (gorge, massif clearance) | `BuildBoundary` |
| Mist heights | `Atmosphere.Particles` or `RiverLevel.MoveMist` |
| Clear masks, A* graphs | `Vegetation.GenerateClearMasks`; `Navigation.BuildWaterVolume` + `Navigation.Bake` (unity-astar, aipocalypse-enemy-ai) |
| Placed objects outside rebuilt groups | `RiverLevel.Settle` / `Drowned` / `Floating` |

### Partial (water-only) rebuild

Use one when a full rebuild would wipe hand edits (SKILL.md §4). The WorldMap recipe is in ENVIRONMENT.md → "Water-only rebuild"; the tools are in `RiverLevel.cs`:

| Entry | Does |
| --- | --- |
| `Snapshot(path)` / `Apply(path)` | Records which subgroups of the rebuilt roots were mostly shadowless, then re-applies it after regeneration (majority vote). |
| `Settle(oldTerrain, within, minDrop, apply)` | Lowers placed prefab instances by the ground drop between the old and new TerrainData. **Not idempotent**: after a second iteration, revert to the committed state and run again. |
| `SettleBridge`, `MoveMist` | Bridge and mist follow the new ground and level. |
| `Drowned(level, within, apply)`, `Floating(within, above)` | Lists objects now under water or floating. |

- Old terrain: copy the TerrainData asset before regenerating, or recover it with `git show HEAD:<path> | git lfs smudge` (TerrainData is LFS).
- `RiverLevel.cs` keeps its own copy of the river knots and pond centre (run_script compiles one file): update it with the layout.
- A generic version should find the changed region by diffing old and new heights instead of copying knots, read water surfaces from the scene, and snapshot material instancing as well as shadows.
- Revert with git only after switching Unity to an empty scene, or a modal "modified externally" dialog blocks the CLI.

## 9. Verify

- Before baking: `MacroCheck` (v23; port it): banks 3 m and 10 m outside each channel above its surface, basin rims 8 m out.
- After `Rebuild`: `Verify(dir)`: no new leak cells in the 3–6 m rim ring, no negative bank height at 2 m, basin and channel depths as designed, `macroDriftInWater` small.
- Pixels: top-down and eye-level captures at every mouth, outlet and junction; underwater from below in play mode (with `runInBackground`, and a non-hidden camera: SW3 skips cameras with `hideFlags`).
