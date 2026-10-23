# Placement, boundary and atmosphere

Scripted GameObjects (rocks, props, landmarks, mountains, effects), the enclosure around the playable area, and the atmosphere a map needs. From the WorldMap generator (WML = `AgentScripts/Maps/WorldMap/WorldMapLayout.cs`: boundary 1048–1865, `Landmarks`/`Dressing`/`Outcrops` 3087–4556, `Finishing` 5254+, `Atmosphere.cs` = ATM). Concrete WorldMap values are in ENVIRONMENT.md → Boundary / Rocks and dressing / Atmosphere.

## 1. Placement primitives

- **`Put`**: always `PrefabUtility.InstantiatePrefab(prefab, parent)`, then set the transform, so instances stay connected and overrides stay small. Throw when a path doesn't load. WML has seven near-identical copies (one without the null check): new code should share one `Put` and one `PutOnGround(xz, yaw, scale, sink, alignToNormal, tilt)`.
- **Measure prefabs; don't hand-type sizes.** `MassifInfo` (WML:1263) reads LOD0 vertices in prefab space: footprint radius from vertices ≥ 1 m above the lowest (ignores flat skirts), height, min Y, summit XZ; cached per path. Scale `sxz = r / R`, `sy = (crest − base) / H`, then place so the **summit** lands on the anchor (pivots are arbitrary). `Renderer.bounds` includes skirts and lower LODs: use LOD0 vertices. Tools: `MeasurePrefabs.Run/Lineup` (LOD0 bounds, tris, LODs, colliders, shaders), `PrefabInfo.Components` (lights, particles, audio, scripts to vet before placing).
  - Surprises found by measuring: BK mountain meshes are ~0.03 units (scale 40,000–45,000); HIGHLANDS `Mountain_1–4` are ~1050 m tiles, 210–350 m tall, each with a low side; ANGRY MESH mountains carry 14k-triangle MeshColliders; Komorebi waterfall modules ~7 m.
- **Sink below the lowest ground under the footprint**, never the centre height (it floats downhill): foot rocks take the lowest of 8 directions at 0.6 r; massifs the lowest of 16 directions × {0.35, 0.7, 1.0} r minus max(4, 0.08 H); peaks the lowest of a 9×9 grid minus 4 m.
- **Raycast onto meshes** for "on whatever is there" (pines on massif slopes, bushes on waterfall rocks): `Physics.SyncTransforms()` first; accept only hits on the intended colliders, `normal.y ≥ 0.7`, inside a height band. Strip heavy colliders only after the raycast passes.
- **Local frames**: author a set piece (yard, traced demo pond) in its own frame and transform it, so moving it re-maps every child.

## 2. Keep-outs, clusters, seeds

- **Two levels of keep-out**: static map zones derived from layout constants (home 38 m, viewpoint, pond, waterfall lip, bridge, water lines + 4 m, trails), and a per-pass occupancy list (`placed`, `Free(p, r)`) filled big-first (hero trees, driftwood, boulders, then small stones). Hand-typed keep-out coordinates went stale when the home moved.
- **Clusters with bare stretches** look natural; even sprinkling doesn't. Ratios that worked: foot rocks 2–4 per cluster, gaps 25–80 m, 25–60% of each side bare; lake shore stones in groups of 2–5 with about half the shore bare; meadow stones 3–7 per cluster ≥ 22 m apart; home-pond ring ~8 stones per 10 m with short gaps. Use run/gap state machines along lines.
- **Seeds**: every builder takes a seed; each independent sub-pass gets its own `System.Random` (e.g. seed + 7919). Inserting one draw shifts every later draw ("outcrop boulders and meadow stones land in different spots map-wide").
- **Group paths are an API.** Each builder deletes and recreates its own root (`Environment/Boundary/...`, `Environment/Dressing/<Group>/...`) with Undo. Later passes read these paths (clear masks treat `Near Range` as rocks; `Home Trees` sits outside `Landmarks/Home Base` so the clear pass doesn't mow around each canopy; `RiverLevel` keys shadow state by path). Keep them stable; share the strings as constants.

## 3. Walking shores and banks

**Shore walker** (lakes, ponds; `BuildWaterEdges`, WML:3463):
1. Step 1.5 m along the shore polygon; march −9…+9 m along the normal by 0.25 m until the ground rises above the level. That is the **real** waterline, not the design polygon. Skip river mouths.
2. Classify: land fraction of 12 probes at 15 m > 0.6 → cove, < 0.4 → point, else straight; sand weight > 0.4 → beach.
3. Dress per class: coves get most stones, reed beds and lilies; points 1–3 big rocks standing in 0.3–1.5 m of water, no reeds; beaches pebbles and driftwood only.
4. Stone offsets: 55% straddle the waterline (−0.5…+0.6 m), 30% in the water (+1…+4 m), 15% on land. Hero trees 2–5 m from the water leaning 4–9° out, ≥ 90 m apart. Lilies 0.9–2.2 m deep, y = level + 0.028. Reed beds 6–18 m runs with 10–35 m gaps.

**Bank walker** (rivers, streams; `BuildBanks`, WML:3711), 1 m steps on both sides:
1. Real edge by marching out until the ground is above the surface.
2. Bend: outer bank = cut (rock-lined, density ×1.25), inner = bar (few stones, ×0.5), from heading change plus a slow noise.
3. Stretch character from the surface slope over ±6 m: > 5% or in the highlands = rocky; < 1% = calm (lotus runs); else boulder-lined.
4. Skip near bridges, basin shores and trails.

**Islands**: archetype by waterline radius and height (wooded, pine knoll, flower islet, sand bar); leave the side facing the main viewpoint open.

## 4. Enclosing the playable area

The user's rules: mountains and rocks are **models**, the terrain is never raised for them; invisible walls only on the playable edge; nothing climbable.

| Layer (front to back) | Beyond the edge | Height above local ground | Notes |
| --- | --- | --- | --- |
| Foot rocks | 0–12 m | 2–7 m (≤ 3 m near the home and viewpoints) | One asset family per side; shadows and colliders kept |
| Near range (row A + fill knobs) | crests 36–100 m, no visible vertex within 6 m of the edge | 31–65 m | Pines raycast onto valley-facing slopes; shadows off, colliders removed |
| Back row | crests 150–200 m, none within 40 m | 66–121 m | Some snow-dusted |
| Tree belt | 4–175 m | trees 9–27 m | MicroVerse rim masks; short trees behind the home |
| Peaks | 550–800 m (≤ ~1.4 km from viewpoints: far clip 1500 m) | 260–450 m | Retinted material copies |
| Haze | toward the map from each peak | ground + 8–30 m | Komorebi `Fog_1` |
| Backdrop ground | border → 2.5 km | just under the border | Generated ring mesh |
| Invisible walls | on the edge | g − 10 … g + 130 | BoxColliders on `Ignore Raycast` |

- **Heights from an angle budget**, checked from hub views at eye height (1.7 m): near row ≤ ~14°, back row ≤ ~17°, every peak ≥ 3–5° above the layers in front. `BoundaryAngles(margin)` (WML:1801) reports per 10° bearing whether peaks clear the front layers. The first near-range pass "read too low behind the trees" (scaled r ×1.15, crest ×1.3).
- **Push out until clear**: sample up to ~4000 LOD0 vertices per mesh, ignore ones buried > 0.5 m, and push 3 m outward (up to 10×), then shrink ×0.95 (4×), until no visible vertex is within the margin of the playable edge (and away from outlets and waterfalls). Push along the edge's outward normal; WML pushes radially from the map centre, which is wrong on non-round maps.
- **Generate anchors from the edge** (v23 `NearRangeAnchors`): walk the playable polygon per row (spacing, distance-out range, r, H, phase), family from the outward normal ±20° jitter, yaw toward the map. Replaces hand tables.
- **Repetition breakers**: one family per side (corners pick by outward normal ±25° jitter so they blend), mirror every second use, never the same face twice in a row, ±15% XZ, yaw jitter. Orient HIGHLANDS mountain tiles so their low side faces the player.
- **What failed**: tall rock rows hid the peaks ("the alpine mountains are hiding behind those walls of big rocks"); low foot rocks with distant ridges read like a plain; a third ridge band 250–400 m out peeked 0–3° and read as a green band.
- **Backdrop ring**: border sampled every 25 m; inner height = **10th percentile** of border heights (an outlet channel crossing the border would drag the minimum down); rings (0 m, −0.6), (60, −2.5), (400, −6), (2500, −10); grass layer texture and tint.
- **Invisible walls**: one BoxCollider per edge segment (1 × 140 × length + 1.5 m overlap), `Ignore Raycast` so shots and interaction rays pass. If a gameplay ray mask ever includes Ignore Raycast, the walls block it.
- **Where a river leaves the map**: slabs on both banks and one across the end (outlet gorge) so it seems to run on between rocks; keep the near range ≥ 37 m off it.
- **Far clip**: the player camera clips at 1500 m (Scene view at 4 km hides the problem). A bigger map needs the Cinemachine lens and Camera far clip overridden in the scene (v23: 3000 m).

## 5. Vendor materials

- `MaterialCopy(vendor, dest, tune)` (WML:2793): `new Material(vendor)` the first time, then `CopyPropertiesFromMaterial(vendor)` + tune on every run (GUID stable, re-tuned each build). `MaterialVariant` makes a prefab **variant** with materials swapped on all LODs.
- Fog-ignoring shaders: HIGHLANDS (`Highlands/Background`) and Grasslands (`URP/Mountains`) mountains skip scene fog and haze with their own teal `_FogColor`; copies are retinted to the sky haze. ANGRY MESH takes scene fog. Keep scene fog thin (ExpSq 0.0011: ~5% haze at 200 m, 54% at 800 m) so near layers don't out-haze the peaks.
- ANGRY MESH rocks are beige and mountains mauve: copies are warm grey / desaturated. Their grass top layer needs the `ASPTopLayerGlobals` component in the scene.
- Never write to a vendor asset or reset a hand-tuned first-party one from a builder.

## 6. Render policy (part of the build, not a hand edit)

Apply in a final pass keyed by group path, so a rebuild reproduces it:
- **Shadows off**: everything beyond the walls (ranges, peaks, backdrop, their pines and feet), stones floating on or under water, lilies and lotus, bank lining, ground-hugging details, all particles, FX lights.
- **Shadows on**: foot rocks, outcrops, meadow stones, landmarks, hero trees (near the player and tall enough to cast a visible shadow).
- **Colliders**: removed beyond the walls; kept on walkable rocks.
- **Particles**: `cullingMode = Pause` on anything with a noise module or world-space simulation (Automatic kept simulating them off-screen), recorded with `PrefabUtility.RecordPrefabInstancePropertyModifications` or the next prefab reload reverts it.
- **Audio**: disable vendor AudioSources (BroAudio owns audio).
- Static flags are set but static batching is off project-wide (GPU Resident Drawer); don't turn it back on.

## 7. Atmosphere (map side)

- **Pick the sun from the hero views** (WorldMap: pitch 32°, yaw 300°, so the home → bridge view is side-lit and the meadow's east view back-lit), then align everything to it: god-ray prefabs `(SunPitch − 90, SunYaw)`, skybox rotation so its painted glow sits behind the light, wind yaw = sun yaw + 90.
- **Vendor demo grades are showcase-strength**: the Komorebi grade at contrast 50 / saturation +10 crushed cliffs and made the grass neon (→ 20 / +2); lens flare off in an FPS. The player prefab renders no post-processing: the scene enables it as an override.
- **Day/night**: the static light, fog and grade are the 09:00 seed; `EnvironmentDirector` owns them at runtime (DAYNIGHT.md).
- **Ambient particles**: place them from layout predicates (`Scatter(count, gap, predicate)`: inside the play area, forest weight, dry, off trails) and from placed objects (leaf fall from tree groups), not hand-typed coordinates. After the home moved, 8 of 9 hand-placed leaf-fall emitters were 5–38 m from any tree. Kroma dandelions: disable `KromaCameraFollow` and collision.
- Particles don't simulate in Scene-view captures: check them in play mode.
