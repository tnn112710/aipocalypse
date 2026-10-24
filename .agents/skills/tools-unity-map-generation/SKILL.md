---
name: tools-unity-map-generation
description: Building or changing a whole map in aipocalypse the way the WorldMap was built — an editor generator in AgentScripts/Maps/<Map>/ that turns layout tables (polygons, water basins and lines, anchors) into an analytic height field with carved water, an R16 base heightmap plus MicroVerse stamps, terrain-space texture and vegetation masks, Stylized Water 3 surfaces, a mountain/rock boundary, scripted dressing and atmosphere. Covers working from concept images and annotated screenshots, interpreting the user's size/depth/multiplier requests, build order and timings, verification (layout preview, analytic checks, Verify, fixed views), rebuild discipline (full vs partial rebuilds, folding hand edits into code), surviving long editor runs, map art direction and map performance. Use for any map, level layout, region, lake/river/pond, boundary, map-wide dressing, map rescale, or WorldMap generator change. NOT the MicroVerse API itself (tools-unity-microverse), NOT Stylized Water 3 internals (tools-unity-stylized-water), NOT runtime gameplay systems.
---

# Map generation (aipocalypse)

Maps are produced by an editor **generator**, not by hand. The reference implementation is the WorldMap: `AgentScripts/Maps/WorldMap/WorldMapLayout.cs` (classes `WorldMapLayout`, `Landmarks`, `Dressing`, `Outcrops`, `Veg`, `Vegetation`, `Finishing`) + `Atmosphere.cs` + `RiverLevel.cs`, documented in `docs/technicals/ENVIRONMENT.md` (layout tables, rebuild order, water-only rebuild, generator drift). Read ENVIRONMENT.md before touching the WorldMap. The 1500 m variant (v23) lives on `origin/terrain-4` (`AgentScripts/MeadowHomeV14/`) and has improvements the current file lacks.

Read before acting:
- tools-unity-microverse: stamps, filters, kit, MicroVerse pitfalls. Every terrain step goes through it.
- tools-unity-stylized-water: before any water code.
- [references/heights.md](references/heights.md): layout tables, the height function, base heightmap + zero-mean detail stamps, pads, checks, changing the map size.
- [references/water.md](references/water.md): levels, carving, flat water, ribbons, junctions, waterfall, dependents, partial rebuilds.
- [references/surface.md](references/surface.md): texturing and vegetation recipe and numbers.
- [references/placement.md](references/placement.md): placing rocks/props, shore and bank walkers, boundary, render policy, atmosphere.
- [references/performance.md](references/performance.md): what moved the frame time, measuring scripts.
- [references/art-packs.md](references/art-packs.md): which packs to use for what, their quirks, the user's standing preferences.

## 1. Working with the user

- **Prompt first** (AGENTS.md). The user's map prompts are spec sheets in metres tied to the player (eye 1.7 m, slope limit 45°, step 0.35 m, jump 1.1 m, no swimming). Turn them into a table (terrain size, playable extents, water levels and depths, region heights), propose missing numbers, record the answers.
- **Answer "any concerns / questions?" within minutes** from a direct read of the concept and this skill, with defaults ("reply 'go with defaults'"). Run deep surveys afterwards, in the background. Two sessions that surveyed first were stopped by the user ("are you still running?", "what are we waiting?").
- **Concept images are AI paintings.** Rulers and captions are not measurements (v10's ruler said 2000 m; v23's ruler was offset ~70 m and its caption contradicted the drawing → "follow the drawing"). Find the map frame, trace on a 50/100 m grid into coordinate tables, then show concept and `Preview` side by side before building ("the cheap moment to move things").
- **Annotated screenshots are perspective views.** Don't eyeball strokes: the home box was missed by 14 m and 30° that way. Read the Scene view pose before moving it (the user's view is the reference), fit the camera, raycast the stroke corners onto the TerrainCollider, and render the outline back from the same pose to confirm.
- **Multipliers**: "N× bigger" = area, "wider/deeper" = linear. State your reading in one line, compute before/after, and check what the growth hits (bridge span, trails, home pad, a neighbour's bank cone). Growing one region shrinks another.
- **Quote measured extents, not concept numbers** (the user's "700 × 700 playable" was really 810 × 877 m).
- **Review stops**: after (a) the height and water blockout and (b) texturing, unless told otherwise. End each round with a screenshot sheet, decisions for the user, known issues, and what is not committed.
- **Reproduce a complaint from the user's camera before fixing.** The named cause is often wrong: "grass LOD appears too soon" was contrast; "weird texture near the pond" was a water-mesh staircase.
- **Deliver the visible goal** or say what's missing (flattening the river wasn't "underwater rendering"; the areas were missing).
- **Log user overrides of the concept** in ENVIRONMENT.md or the concept doc: the next concept revision silently undoes them.
- Ask with AskUserQuestion and a recommended default. For "make it more beautiful", give a ranked menu with the ms cost of each item and "needs your OK" on project-wide settings.

## 2. Generator architecture

- One generator per map in `AgentScripts/Maps/<Map>/`, outside `Assets/` (no import, no domain reload), run with `unity command run_script`. Map-only output in `Assets/_Game/Maps/<Map>/` (the WorldMap scene itself is `Scenes/Prototypes/WorldMap.unity` while it is a prototype); reusable first-party copies in `Assets/_Game/Environment/`.
- **Layout is data**: polygons, basins, lines, anchors (100 m grid), bumps, islands, landmark frames and the detail-stamp table at the top of the file, in metres (x east, z north). Everything derives from them.
- **One height function** and **one canonical waterline**: masks, water meshes, ribbons and dressing call the same functions that carved the terrain.
- **Terrain size is a parameter.** No `1000`/`500`/`998` literals; noise in world metres; MicroVerse UV-space values converted (heights.md §8).
- **Builders are idempotent**: each deletes and recreates its own root group with Undo, takes a seed, uses its own `System.Random`, returns counts and throws when inputs are missing (no silent partial groups).
- **Group paths are an API**: clear masks, the render-policy pass and partial rebuilds read them. Keep them stable, as shared constants.
- **`run_script` compiles one file**, unoptimised, and loads a new assembly per run. Helper classes must live in the same file (why WorldMapLayout.cs is 5.5k lines; `RiverLevel.cs` keeps copies of layout data). Hot loops: `async Task<object>` + `await Task.Run(() => Parallel.For(...))`, asset calls after the await, `--timeout_ms` and CLI `--timeout`. A timed-out call keeps running: wait for `editor_status` before retrying.
- **Save specific assets only** (`SaveAssetIfDirty`; importers via `WriteImportSettingsIfDirty` + `ImportAsset`). Known violations to fix when touched: `BuildUnderwater` (WML:2188), `Atmosphere.RenderQuality` (also loads a moved settings path: don't run it), `DayNightSetup.cs:71`.
- **Vendor content is read-only**: material copies (refresh then tune), prefab variants, first-party copies in `Environment/`. A builder must never reset a hand-tuned shared asset (`BuildBanks` once turned instancing off on a material the user had enabled).

## 3. Build order

Generic pipeline (WorldMap entry names; full list in ENVIRONMENT.md → "How it is built"):

| # | Step | WorldMap |
| --- | --- | --- |
| 1 | Layout tables → preview PNG → user approval | `Preview(1024, png)` |
| 2 | Analytic checks: perched channels, rim leaks, pad | `MacroCheck` (v23; port it) |
| 3 | Base heightmap + detail masks → height stamps → CPU sync → checks | `Generate`, `BuildHeights`, kit `Rebuild false`, `Verify` — **review stop (a)** |
| 4 | Texture masks → terrain layers → texture stamps | `GenerateTextureMasks`, `BuildTerrainLayers`, `BuildTextures` — **review stop (b)** |
| 5 | Vegetation masks → prefab variants → vegetation stamps | `Vegetation.GenerateMasks`, `MakeVariants`, `Build` |
| 6 | Boundary | `BuildBoundary(seed)`, `BoundaryAngles(3)` |
| 7 | Water | `BuildWaterMaterials`, `BuildFlatWater`, `BuildRivers`, `BuildUnderwater` |
| 8 | Landmarks, waterfall, outcrops, dressing, islands | `Landmarks.Build`, `BuildWaterfall`, `Outcrops.Build`, `Dressing.*`, `Finishing.Islands` |
| 9 | Atmosphere, wind (before shared wind), spawn | `Atmosphere.*`, `Vegetation.Wind`, `Finishing.PlayerSpawn`, `DayNightSetup.Scene` |
| 10 | Clear masks → bake → tree-aware dressing → clear masks → bake | `GenerateClearMasks`, kit `Rebuild true`, `BuildMeadow`, `BuildHomeTrees`, … |
| 11 | Render policy, distances, save, reopen, stats | (policy pass: placement.md §6), kit `SaveScene`, `OpenScene`, `DetailStats` |
| 12 | Enemy navmesh: water volume, recast bake + startup cache (after anything that adds or moves colliders) | `Navigation.BuildWaterVolume(0.5, true)`, `Navigation.Bake(0.3)` (ENVIRONMENT.md → Navigation) |

Timings: the 1000 m map's full rebuild is ~8.3 min over 36 steps (`Generate` 185 s at 2048, `Verify` 33 s, texture masks 48 s, vegetation masks 39 s, clear masks 2 × 20 s, the rest 1–15 s). The 1500 m map took ~35 min (`Generate` ~5 min at 4096). Run step lists in the background with [scripts/run_steps.sh](scripts/run_steps.sh) (`file|Class.Method|json args` per line, LF endings, stops at the first failure, one JSON log per step).

## 4. Rebuild discipline

- A full rebuild is what keeps a map reproducible, and it is only safe when **the generator matches the scene**.
- **Hand edits are wiped by the next rebuild.** Fold every tweak (yours or the user's) into the generator the same day, preferably into one final render-policy pass keyed by group path (shadows, culling, static flags, colliders, deletions). If you can't, list it in ENVIRONMENT.md → "Generator drift".
- Before a full rebuild of an existing map, read the drift list and ask the user. Never full-rebuild a hand-edited scene silently. **The WorldMap has drift today** (3k+ shadow overrides, particle culling, perf trims, terrain settings, deleted objects; ENVIRONMENT.md → "Generator drift"): use the water-only recipe or port the drift first.
- **Partial rebuilds** re-run only the dependents of what changed (water.md §8), snapshot render state first, settle placed objects by the old → new ground drop, check drowned/floating objects. `Settle` is not idempotent.
- **Check before baking.** Run the analytic checks on the layout first: one pond change was rebuilt three times because `Verify` only ran after bake and settle.
- **Reverting with git**: switch Unity to an empty scene first, or a modal "modified externally" dialog blocks the CLI (one session sat 7 h on it). TerrainData is in LFS: `git show HEAD:<path> | git lfs smudge > <tmp>.asset`.
- **Commit generator, data and scene together**, and at session start check that the generator exists and matches the scene (it was deleted once in `bb665145e` and restored three times).
- Warn the user before they commit: regenerated dressing changes the `.unity` by 100k+ lines, TerrainData assets are tens of MB, RiverModeler meshes are embedded in the scene (WorldMap.unity ~28 MB).

## 5. Verification loop

| Stage | Tool | Look for |
| --- | --- | --- |
| Layout | `Preview` PNG; concept side by side | shapes, sizes, distances before any asset changes |
| Analytic | `MacroCheck`, `Profile` | banks below a channel's surface, rim leaks, pad flatness |
| Terrain | kit `Rebuild` → `Verify(dir)`, `Delta`, `Hillshade` | leak cells, bank heights, depths, region slopes, drift in water, stamp seams |
| Vegetation | `DetailStats`, tree counts after `Rebuild true` | layer count, invalid prototypes, coverage |
| Boundary | `BoundaryAngles(3)` | bearings where peaks don't clear the front layers |
| Pixels | `OpenScene()`, then `Look(x, y, z, pitch, yaw, fov)` at named views (eye 1.7 m) + aerials; before/after from the user's camera | the user's complaint, every water junction, the spawn view |

Capture traps: the GPU Resident Drawer keeps drawing deleted objects until the scene reloads (`OpenScene`); never force a domain reload after MicroVerse work (it corrupted the TerrainData wrapper and the next save crashed). The Scene view doesn't repaint while Unity is unfocused, and the user moves it between calls. SW3 skips cameras with `hideFlags`. The Scene view clips at 4 km, the player at 1500 m. Particles don't simulate in the Scene view (use play mode with `runInBackground`). Screenshots: `unity command screenshot --view scene` (lands in ignored `Temp/`); not `capture_scene_view --save_path` (writes under `Assets/`). Keep the named view list as coordinates in ENVIRONMENT.md.

## 6. Long editor runs

- `unity status` and `set_autotick --enable true` first; one agent drives the editor. Don't touch a dirty scene or the user's play mode without asking.
- Crashes happen (four in three days). Save at milestones. After a crash, wait for the user to reopen Unity, then check `isDirty`, stamp counts and the terrain data before continuing.
- Modal dialogs block every CLI command: "modified externally" (git under an open scene), "save modified scenes" before `run_tests`, MicroVerse "Create For Existing Terrains". Memory note `unity-cli-playmode-gotchas` has the user32 trick to find them.
- Revert vendor churn after each pass (`git status Assets/_StoreAssets`): Unity rewrites some vendor materials when their prefabs are instanced.
- Write scripts and step files with the Write tool (LF), parse JSON with node (no python/jq).
- Persist as you go: pending tasks and decisions to a file or memory at each milestone, research agents writing reports to files. Context exhaustion has killed background research and lost a user request (the highlands rework).

## 7. Art direction

- **Check pack palettes before mixing.** HIGHLANDS reads teal next to Komorebi/Grasslands (firs, ground plants, grass-topped rocks, mountain fog colour); the user rejected it twice. Line assets up under the target light and grade; tint copies or confine a pack to one region.
- **Judge open ground from the spawn at eye height.** Ground cover must contrast with the terrain layer: a short lawn the same green reads as bare ground (surface.md §5).
- **Boundary**: no single-asset wall; low foot rocks, a near range, peaks visible over it; a different family per side (placement.md §4).
- **Water**: start generous, vary width and depth, design every junction, one palette for connected water (water.md §7).
- **"Like the X demo"** means trace the demo's layout into a local frame and transform it, not approximate it.
- Vendor demo grades and speeds are showcase-strength: re-tune at 1:1 (grade contrast 50 → 20, water speed ×16 → 6).
- Place effects from the layout and placed objects, never hand-typed coordinates.
- The user's standing preferences are in art-packs.md §3.
