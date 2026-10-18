# Meadow Home terrain

Scene: `Assets/_Game/Scenes/Prototype/Prototype_Environment.unity`.
Reference: `docs/concepts/meadow-home-top-down-v4.png`, with a 1,000 × 1,000 m footprint and an approximately 150 m deep home/meadow region along the southern edge. The sculpt uses Rowlan's Valleys, Highlands and Meadows stamps for ridges, eroded folds and rolling ground. This pass contains terrain, authoring markers and Komorebi lighting/grading. Water surfaces, buildings, bridge geometry, vegetation and rock meshes are not placed.

## Terrain and stamp stack

One Unity terrain at `(0, 0, 0)`, size `(1000, 500, 1000)`, with a **2049 heightmap** (approximately 0.49 m spacing) and **2048 alphamap**. The 500 m vertical range is storage headroom; the authored surface currently ranges from approximately **11 to 123 m**. This aspect ratio also matches MicroVerse's normal generator, which derives normals from UV/packed-height differences without terrain aspect correction. TerrainData, first-party terrain layers, material and grading profile live in `Assets/_Game/Data/Environment/MeadowHome/`. The original placeholder TerrainData is left untouched.

Under `MicroVerse`, hierarchy order controls evaluation:

1. `01 Sculpt - landforms`: a composed macro heightmap for broad uplands and saddles, followed by independently movable **Valleys 08**, **Highlands 05**, **Valleys 01**, **Highlands 10**, and **Meadows 12** height stamps. Valley stamps contribute up to 100 m before footprint falloff, giving the uplands distinct ridges and gullies rather than smooth domes.
2. `02 Sculpt - watercourse and shore`: a descending, variable-width river/pond valley with gentler meadow-side banks, masked lake shelves and coves, and a rounded, feathered home clearing.
3. `03 Surface - Komorebi landcover`: continuous meadow grass, broken forest moss, exposed earth, river sediment, low shore earth, steep-face stone and restrained upland outcrops. Four first-party TerrainLayers reuse Komorebi's grass/ground/stone textures with per-scene tint and normal-strength adjustments. Slope, height, curvature and noise filters respond to height changes.
4. `90 Archive - initial blockout (disabled)`: the first-pass stamp stack, retained for comparison. Do not enable it alongside the sculpt stack.

Active custom maps are native linear **R16 Texture2D** assets under `Assets/_Game/Data/Environment/Heightmaps/MeadowHome/Sculpt/`: macro relief, river height/influence/sediment, lake shelf height/influence, home-clearing influence and forest cover. They are 1025 × 1025, bilinear and clamped, without mipmaps; Rowlan's source maps retain their original 4K resolution. River/lake maps encode elevation divided by 100, with their stamps at Y = 0 and scale Y = 100. Lake shelves use `Min` to preserve the lower river outlet. The macro map is scaled to 100 m and raised by 13 m. Move/scale the stamps to adjust placement; edit their Y/scale Y to adjust elevation/depth.

The first-party terrain material uses splat-weight blending rather than height blending, avoiding the source stone mask overpowering grass at low painted weights. Basemap distance is 450 m to reduce distant texture aliasing. The installed MicroVerse slope shader multiplies its normal angle by PI/2, so the stone slope filter compensates when translating actual slope angles into serialized values. These adjustments avoid modifying vendor shaders.

`Lighting - Komorebi colour grade` holds a global URP Volume referencing a first-party copy of `KomorebiNature_Demo_Post-Processing.asset`. It retains the demo's colour/tone treatment; motion blur and screen-space lens flares are disabled. The scene references Komorebi's skybox and matches its sun direction/intensity. Scene view image effects must be enabled to see the grading; gameplay cameras need their own post-processing flag enabled when introduced.

## Layout targets (concept v13)

The current sculpt follows the v4 concept. The latest concept, `docs/concepts/meadow-home-top-down-v13.png`, keeps the 1000 × 1000 m terrain but fits the whole map into the central **700 × 700 m** (about X/Z 150–850), with unwalkable boundary mountains in the outer ~150 m. The southern mountains extend north over the south half of the meadow, leaving a meadow of about 200 × 70 m. It also adds targets that the scene does not meet yet: a **fenced base of about 30 × 30 m** (the current home clearing is about 70 × 60 m) and **80–100 m from the home base to the broken bridge**. Today's markers put the base about 178 m from the bridge. Move the home clearing or bridge markers and the home-clearing stamp when the terrain is updated to v13.

## Coordinates and markers

North is +Z; east is +X. Region markers are layout guides, not gameplay boundaries.

| Marker | Centre X/Z (m) | Intent |
| --- | --- | --- |
| Home base | 125 / 65 | Approximately 70 × 60 m level clearing at Y = 36 |
| Flower meadow | 425 / 65 | Low rolling southern strip |
| Home region | Southern Z = 0–150 strip | Approximate depth to the river's south bank |
| Pine forest | 475 / 620 | Ridges, saddles and valleys, without trees |
| Waterfall source | 76 / 545 | Upper ravine at approximately Y = 61 |
| Pond | 113 / 355 | Wider basin below the approximately 21 m headwater drop |
| Broken bridge | 251 / 191 | Crossing location only |
| River | 500 / 158 | Channel from the northwest headwaters to the eastern lake |
| Great lake | 940 / 440 | Eastern basin |

`Region Markers - editor only` and its children are tagged `EditorOnly`, so they are stripped from builds. `TerrainRegionMarker` draws labels and outlines when Scene view **Gizmos** are enabled. It samples the terrain while drawing, so the guides follow later terrain changes without gameplay updates. Toggle the marker root to hide all guides.

## Editing and verification

Keep MicroVerse enabled and edit its stamps; direct Terrain brush edits are overwritten on the next rebuild. Use the project's `tools-unity-microverse` skill and `MicroVerseKit` to rebuild, sync heights to CPU/collider, and save the scene and TerrainData. Save only the specific assets rather than calling `AssetDatabase.SaveAssets()`.

`AgentScripts/MeadowHomeTerrain.cs` is the initial blank-scene builder and inspection helper. `AgentScripts/MeadowHomeSculpt.cs` composes the refined terrain, preserves the initial stack in the disabled archive and provides `Inspect`, `Overview`, `GroundView`, `TopDown` and `Validate`. Both live outside `Assets/`, so they are editor commands rather than shipped game scripts. Initial construction commands refuse to replace existing stacks. Standard artist edits/rebuilds use `MicroVerseKit.Rebuild(true)` and `MicroVerseKit.SaveScene("")`.

`Validate` checks the level 70 × 60 m home area, continuous riverbed, terrain/collider identity, nine editor-only markers, normalized surface weights and first-party grading profile. It temporarily increases a Rowlan stamp's height, verifies a terrain response, then restores it and verifies an identical rebuild. The measured response is approximately 0.53 m for a 5% height increase at its probe; restoring the stamp changes the probe height by 0 m. Stone dominates approximately 4% of the sampled surface.

The current river is a terrain-only height stamp. When authoring actual water, use Stylized Water 3's RiverModeler/MicroVerse integration and reconcile its bed with this stamp. Future vegetation/props should use MicroVerse Tree/Detail/Object stamps to regenerate with terrain edits; independently placed objects do not automatically follow terrain changes. Refresh dependent GPUI data, grass colour maps, navigation scans and lighting bakes when those systems are introduced.
