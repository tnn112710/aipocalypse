# Texturing and vegetation

Recipe and numbers from the WorldMap (1000 m, `AgentScripts/Maps/WorldMap/WorldMapLayout.cs` = WML: textures 2449–2825, `Veg`/`Vegetation` 4557–5253). How the MicroVerse stamps behave (claim order, filters, noise units, tree/detail/clear mechanics) is in unity-microverse §1, §6 and §9: read those first; the WorldMap stamp groups and generator helpers are in [microverse-in-this-project.md](microverse-in-this-project.md) §5.

## 1. Masks vs filters

- **Analytic terrain-space masks** (C#, R16, one per stamp) for everything the layout decides: biome regions (polygon distance + fbm edge), water bands (from the same height and waterline functions that carved the terrain, so bands follow islands and coves), trails, yard, sight lines. WML:2481 (`GenerateTextureMasks`), 4695 (`Vegetation.GenerateMasks`).
- **MicroVerse filters** for what the terrain decides: slope, height, weight-noise breakup, texture filter (grass only on grass layers).
- Mask stamps are **whole-terrain**: centre (size/2, size/2), scale (size, 1, size), Texture falloff, channel R, params (1, 0), `clampTexture = true`. Region-sized texture/vegetation stamps with a terrain-space mask don't work (unity-microverse §9).
- ~1 m per mask pixel (1024 on 1000 m) was enough even for 0.9–1.7 m trail half-widths. 1500 m used 2048 texture masks and 1536 vegetation masks.
- Build masks **after** the heights are final and **before** the stamps. A missing mask makes the vegetation builder throw half-way and leave a partial group (on v23 the Clearing group stayed empty and ~2,400 trees grew in the yard and on the trails): write empty placeholder masks on a first build.

## 2. Texturing

**Order** (top of the group = base; bottom stamps claim first). Generic template:

1. Base grass, Global falloff (no base = black patches).
2. Broad tone noise (cool patches).
3. Biome regions, each with a masked "reset" stamp so the region starts clean (meadow base grass sits below forest dirt so inside the meadow it claims everything).
4. Region tones (warm/cool meadow, scree, swamp grass/mud).
5. Water bands: beds, shore pebbles, beach sand, channel beds, channel banks.
6. Steep rock (slope only), highland shelves (mask + slope).
7. Paths last: verge (weight 0.55 + fine noise) → core → centre wear (weight 0.3).

WorldMap: 20 texture stamps, **8 terrain layers** (Grass, GrassCool, GrassMeadow, Dirt, WetMud, Pebbles, Sand, Rock) = two 4-layer splat passes. Every layer costs; a tint variant of the same texture still takes a slot, and a disabled test stamp still adds its layer.

**Layers are first-party copies** (`CopyLayer`, WML:2585) in `Assets/_Game/Environment/TerrainLayers/`: copy once (GUID stable), then re-tune `tileSize` and `diffuseRemapMax` (tint) every run. Tiles: grass/dirt/mud 3 m, pebbles 2 m, sand 4 m, rock 10 m. Tint the meadow layer to the grass details' mean colour so there is no tone step where the details end. WetMud = Mud × (0.44, 0.47, 0.37). Pebbles use Komorebi `Stones_1` (grey, matching the rocks; Grasslands MudStones read brown).

**Noise values that worked** (texture stamps on a 1000 m tile; frequency, amplitude, balance):

| Use | Values | Reads as |
| --- | --- | --- |
| cool grass patches | 22, 3, 0.2 | ~45 m soft patches, ~50% cover |
| forest dirt (weight 0.6) | 45, 3, 0.42 | sparse 22 m spots |
| meadow warm | 14, 3, 0.0 | 70 m blend |
| meadow cool patches | 16, 8, 0.14 | hard-edged, 10–20% |
| scree / swamp grass / highland shelves | 35, 2.5, 0.35 / 30, 2.5, −0.2 / 25, 2, 0.2 | |
| channel banks / steep rock | 60, 1.2, 0 / 60, 0.5, 0 | mostly full with dips / ±25% |
| trail verge (weight 0.55) | 450, 6, 0.05 | ~2 m trampled patches |

On a bigger tile, scale texture-stamp frequencies by size/1000 to keep these patch sizes.

**Slope ranges that worked** (MicroVerse filter units, ≈ degrees × 1.57 on this terrain): steep rock 54–95 (s 5, 0) ≈ 34°–60°; highland shelves 48–95; scree 40–56; meadow tones ≤ 32; forest dirt ≤ 45 and height ≤ 62 m.

**Water and path bands** (WML:2497–2538):
- Shore band by **height relative to the water**, not distance, so islands and coves follow the real waterline: `band = (1 − S(0.15 + 0.25·fbm, 0.6, rel))·S(−2.4, −1.2, rel)`, bed below −1.1 m.
- Pebbles only in patches: grass reaches the water on most of the shore (vendor demos). Sand coves are whole discs with ragged rims, none at river mouths.
- Channel beds dark wet mud; channel textures × "outside lake/pond" so they stop at the shore.
- Trails are textured, not carved: frayed core `hw = max(0.5, 1.3 + 0.45·fbm(x/10) + 0.9·fbm(x/3))`, verge hw + 0.3 … hw + 3, wear in the middle, all faded within 1.5 m of water.

## 3. Vegetation

**One mask per stamp.** Every Tree/Detail/Clear stamp is whole-terrain with its own R16 mask plus filters. WorldMap: 26 vegetation masks + 2 clear masks + 12 texture masks. Masks multiply shared factors (inside playable, ragged water reach, tree fade 5–12 m from water, region weights, view lanes, trail fades). Vegetation region weights = texture region masks without the water fade, so grass and ground agree.

**Which system:**
- Terrain trees (TreeStamp): trees and bushes, anything with a LODGroup that must collide or show far. ~3,800 on 1000 m, ~11,000 on 1500 m.
- Details (DetailStamp): grass, flowers, ferns, pebbles; drawn only to the detail distance.
- Scripted GameObjects: hero trees, reed beds that must read past the detail distance, rocks, logs, island sets (references/placement.md). Clear masks are generated from them.

**Tree densities** (MicroVerse density; spacing ≈ 44 / density m): pines 3.6, broadleaf groves 3.0, highland firs 4.6, young pines 2.2, dead trees 0.55–0.7, meadow edge 2.2, rim belt 1.8–2.4, shore trees 2.8, swamp 1.7–2.4. Bushes: understory/rim 3.0, meadow flowering 4.5 (noise 35, 6, 0.2 → clumps of 2–4), hedgerows 8 (mask band only), shore bushes 14 (noise 16, 8, 0.16 → clumps of 3–5). Scale 0.75–1.25 (young 0.45–0.7). Order is priority: groves came out sparse until they moved before the pines.

**Detail densities** (coverage mode, density per prototype): meadow lawn 1.8–2.0 (width 1.2–2.0, height 1.6–2.8 → tips 0.4–0.8 m, so blades hide the ground at 20 m from a 1.7 m eye), yard lawn 1.8–2.2 (0.3–0.45 m), knee-high clumps 2.0, sorrel 2.5, flower heads 1.0–1.5, flower fields 0.8–1.0, lavender 2.0, wild grass 1.3, ferns 0.8–1.0, clovers 1.4, reeds 1.8 (noise 70, 3.5, 0.38 → clumps), shore grass 2.0, scree 0.35, trail pebbles 0.1–0.15. Coverage density is non-linear: 1.6 gave 3× the instances of 1.0.
- `alignToGround` 1 for lawns, 0 for upright flowers and spikes; `positionJitter` 0.3 lawns, 0.7–0.8 flowers.
- Hard-edged flower drifts: amplitude 8–12, balance 0.12–0.2, and a **different noise offset per stamp ≥ 1** (offsets are in noise cells: 0 / 0.15 / 0.45 give near-identical drifts).
- SDF filters: flowers ≥ 4–5 m from trees; ferns, needle litter and tall grass ≤ 5.5–7 m from trees.

**Shared layers.** Point near-identical stamps at one `DetailPrototypeSettings` asset (`Assets/_Game/Environment/Vegetation/DetailSettings/`) so they share a terrain detail layer (38 → 34 layers on WorldMap). Shared layers combine with max. The generator doesn't do this yet (drift, SKILL.md §4).

**Prefab rules:**
- Detail prefabs with a LODGroup *and* a mesh on the root are rejected by Unity: make plain LOD0 mesh copies (`DetailPrefab`, WML:4928; Komorebi `Grass_1_Large`, HIGHLANDS grass/flowers/clovers/stones).
- Check triangles before adding a detail: ≤ ~200 for dense cover. Ferns 360–760, Farfugium 509, clovers 228; `Salvia_Group_1` (960) was swapped for `Salvia_1` (199).
- Shadows off on anything under ~0.5 m (sorrel, clovers, stones): detail shadows follow the prefab's MeshRenderer.
- Terrain trees only use primitive colliders: audit each tree prefab (HIGHLANDS `Fir_Tall_*` had MeshColliders → capsule variants in `Environment/Vegetation/Prefabs/`).
- BK plants need `BK Foliage Globals` (`_Core/EnvironmentSystem/BKFoliageGlobals`, render distance ≈ 2× the detail distance) or they fade out from ~50 m with no micro wind.

**Clearing** (`GenerateClearMasks`, WML:4840): from the scene, every LODGroup ≥ 1.2 m tall under the rock/dressing roots with r = 0.42 × max(size.x, size.z): trees cleared to r + 1.5 … r + 3.5, details 0.7–0.85 r. Plus analytic shapes: yard (trees 7–13 m off the fence), spawn → bridge sight line, bridge box, trail cores. Two ClearStamps, last in the group; dressing that must sit on cleared ground (trail pebbles) goes after them.

Order: clear masks → `Rebuild true` (terrain trees exist) → dressing that avoids terrain trees (`BuildMeadow` reads `treeInstances`) → clear masks again → `Rebuild true`. Re-run clear masks after every rock or dressing pass.

**Wind:** one driver per shader family (Komorebi `Wind_Controller`, HIGHLANDS `Wind Settings`, `Raygeas.WindSettings` for Grasslands/Lakefell) at yaw = sun yaw + 90, plus BK Foliage Globals. `Vegetation.Wind()` deletes the Wind root that `Atmosphere.SharedWind()` fills: run Wind first.

## 4. Draw distances and cost

- Unity's terrain renderer draws the WorldMap vegetation (no GPUI manager in the scene; a leftover `GPUITerrainBuiltin` sits on the terrain). Saved scene: detail distance 100, tree distance 1000, terrain shadows off. Docs said 120 / 5000; `DrawDistances(120, 240)` would reset detail to 120 and never sets the tree distance.
- Details are most of the triangles: detail distance 250 m drew 20M triangles at the spawn, 120 m about 9.5M; 120 → 80 m saved 1.6–1.9 ms CPU and 1 ms GPU. Don't go past ~120 m without GPUI.
- The forest thins when seen from beyond ~350 m (LOD cull); billboards or impostors would fix it.
- GPUI: the Detail Manager helped, the Tree Manager cost CPU (references/performance.md). It needs the "GPU Instancer Pro Setup" node in every Shader Graph it draws (Raygeas graphs don't have it: make `_Game` copies).

## 5. Look

- **"Grass disappears too soon"** was not culling: details drew to 80 m, but a short, uniform lawn the same green as the terrain layer read as bare ground past 10–15 m. Raising the distance to 250 m barely changed the view. Measure first, then fix with taller tips, a sunlit/cool colour spread in first-party material copies (`TintedDetail`), knee-high clumps, spikes across sight lines, cool terrain patches.
- **Meadow** (BK Pure Nature Meadows recipe): short bright lawn, flowers in hard-edged drifts of one to three species with plain lawn between, barley tufts, tall grass only at tree and fence feet, upright spikes in drifts across the spawn's sight lines, hedgerows on the rim, a few lone hero trees, rolling ground (knolls +5 m, hollows −2.5 m).
- **Shores** (Raygeas demos): lush grass and clover 0.3–3 m from the water, bush clumps of 3–5 and flowers from 3 m, trees from 7 m; most in coves, fewest on points, none on beaches.
- **Mixing packs**: HIGHLANDS firs and ground plants read cyan next to Komorebi pines, so firs stay in the highlands; HIGHLANDS grass-topped rocks render teal (copies use the Grasslands lawn texture). Check a pack's palette before scattering it (SKILL.md §7).
