# Art packs for map building

Vendor content: reference it, never edit it. Copies and variants go in `Assets/_Game/Environment/` (reusable; keep the pack prefix `BK_`/`HL_`/`KN_`/`AM_`/`GL_`) or `Assets/_Game/Maps/<Map>/` (one map only). Before placing a prop, vet it: size, LOD0 triangles, LODGroup on the root, collider types, shaders, scripts, lights, audio (`MeasurePrefabs.Run`, `PrefabInfo.Components` in `AgentScripts/Maps/WorldMap/`). Rowlan height packs, BK MicroVerse presets and Genesis: what is installed here in [microverse-in-this-project.md](microverse-in-this-project.md) §1, how they work in unity-microverse `references/content.md`.

## 1. Shared first-party kit (`Assets/_Game/Environment/`): reuse first

| Folder | Assets | Notes |
| --- | --- | --- |
| `TerrainLayers/` | `Terrain_Grass` (GL Grass), `Terrain_GrassCool` (GL Grass_Blue), `Terrain_GrassMeadow` (GL Grass_Yellow tinted to the BK lawn), `Terrain_Dirt` (GL Mud), `Terrain_WetMud` (GL Mud, dark tint), `Terrain_Pebbles` (GL MudStones with Komorebi `Stones_1` textures), `Terrain_Sand` (GL Sand, tile 4), `Terrain_Rock` (Komorebi rock detail, tile 10) | Made by `BuildTerrainLayers()`; `CopyLayer` keeps GUIDs |
| `Water/` | `Water_Still` (SW3 Smooth), `Water_Pond` (Komorebi turquoise), `Water_Swamp`, `Water_River` (SW3 SplineRiver Default, river mode), `Water_RiverUnderside` (back faces), `Water_Waterfall` (Komorebi, queue 3001) | tools-unity-stylized-water |
| `Vegetation/Materials/` | `BK_Grass1/2_Meadow`, `BK_Grass3_Tall`, `BK_Sorrel_Dark` | Colour-spread copies of BK grass |
| `Vegetation/DetailS/` (capital S in git; the generator and docs write `Details/`, which only works on Windows: rename in the editor) | LOD-free detail prefabs: `BK_Grass1/2_Meadow_Detail`, `BK_Grass3_Tall_Detail`, `BK_Sorrel_Dark_Detail`, `HL_Clovers/Flowers_1/Grass_1/Grass_2/Stones_Detail`, `KN_Clovers_1_Detail`, `KN_Grass_1_Large_Detail` | Unity rejects detail prefabs with a LODGroup on the root |
| `Vegetation/DetailSettings/` | `Clovers`, `Fern_1`, `Fern_3`, `FlowerMeadow1` | Shared `DetailPrototypeSettings`: stamps pointing at one asset share a detail layer |
| `Vegetation/Prefabs/` | `Fir_Tall_1/2/3_Capsule` | HIGHLANDS variants with a CapsuleCollider (terrain trees ignore MeshColliders) |
| `Rocks/` | `HL_Rock_1/2_Grass`, `HL_Stones_Rocks_Grass.mat` | Grass-topped HIGHLANDS rocks using the Grasslands lawn (the pack's own coverage reads teal) |
| `Mountains/` | `AM_M_Rock_01-05_Summer`, `AM_M_Mountain_01/03/04_Summer` (grey ANGRY MESH copies), `HL_Mountain_1-4`, `GL_Mountain_1/2` (haze retinted) | AM rocks keep the grass top layer (needs `ASPTopLayerGlobals`) |

Runtime helpers: `_Core/EnvironmentSystem/BKFoliageGlobals` (BK plant globals; without it BK plants fade beyond ~50 m) and `ASPTopLayerGlobals` (ANGRY MESH top-layer globals). Time of day: `Assets/_Game/Data/Environment/TimeOfDay/`. Map-only (don't reuse): `Maps/WorldMap/Terrain/` (TerrainData, `Heightmaps/` base + masks), `Water/*.asset` meshes, `Backdrop/`.

## 2. Packs

### Raygeas (`Assets/_StoreAssets/Env/Raygeas/`): the main style

For every Raygeas pack:
- Wind comes only from the drivers in the scene: Komorebi `Wind_Controller.prefab`, HIGHLANDS `Wind Settings.prefab`, a `Raygeas.WindSettings` component for Grasslands/Lakefell. Keep them aligned (yaw = sun yaw + 90).
- `Shared Assets/Scripts/WindController.cs` and `WindSettings.cs` have an unguarded `using UnityEditor;` (player builds will fail until fixed).
- `Tree_Foliage` leaves glow through an emission-based fake SSS: pines, bushes and sakura stay bright green at night (DAYNIGHT.md).
- Never place the demo rigs (`Player_Controller`, `Controller`: legacy Input, lock the cursor), `PhysicsObject`/`Interactive` demo props (use the non-interactable versions), or `AudioZoneControl` (owns an AudioSource; BroAudio owns audio).
- GPUI draws the Shader Graphs magenta until `_Game` copies get GPUI's setup node.

| Pack | Path | Use it for | Quirks |
| --- | --- | --- | --- |
| Komorebi Nature | `Komorebi Nature/Assets/` | Pines `PineTree_1-6` (+`_Snow`), `Sakura_1-5`, bushes `Bush_1-4` (+`_Flowers`); details `Grass_1`, `Grass_1_Large`, `Clovers_1/2`, `Fern_1-3`, `Farfugium_1-3`, `Salvia_1-3`; rocks `Rocks Grass/` (grass-topped) and `Rocks/*_NoGrass` (same meshes, other material), `SmallStone_1-6`, `Rocks Snow/`; water lilies, `Waterfall_1` (+ ripples, splash); FX `Fog_1`, `GodRays_1`, `Cloud_1-4`, birds; layers `Grass_1, Ground_1, Stones_1`. Demo `Demo/KomorebiNature_Demo.unity` (pond and waterfall setup) | Nature only: no torii, shrines, lanterns, bamboo (user rule). Sakura only around the home pond. Fish are koi. Heavy details: ferns 360–760 tris, Farfugium 509, clovers 228, `Salvia_Group_1` 960 (use `Salvia_1`, 199). `Grass_1_Large`/`Clovers_1` need LOD-free copies. `SmallStone_5/6` work as detail prefabs. `GodRays.mat` loses `_FADING_ON` when instanced (revert the diff) |
| HIGHLANDS | `HIGHLANDS - Stylized Environment/Assets/` | Firs `Fir_1-3`, `Fir_Tall_1-3`, dead firs, bushes, grass, flowers, clovers, `Rock_1/2`, `Stone_1-4`; `Mountains/Mountain_1-4` (~1050 m range tiles, 210–350 m tall, low side M1 −Z, M2 +Z, M3 +X, M4 −Z); `Camp/Bonfire`; layers `Grass_1/2, Mud_1/2`. Demo `Demo/Demo.unity` | **Teal palette**: firs, ground plants and rock coverage read cyan next to Komorebi/Grasslands (user rejected it twice): keep firs to highland zones, use `HL_Rock_*_Grass`. Mountain shader ignores scene fog (teal `_FogColor`): use `Environment/Mountains/HL_*`. `Fir_Tall_*` have MeshColliders. `Fire.mat` loses `_FADING_ON` when instanced |
| Grasslands | `Grasslands - Stylized Nature/Assets/` | `Broadleaf_Large/Medium/Small`, `Birch_*`, bushes, `Grass_1-3`, flowers, `Lotus` (+ groups), ivy; rocks `Clif_1-3` (grass / `_(no_grass)` / `_(sand)`), `Stone_1-9`, `Small_Stone_1-8`; `Other/Mountain_1/2` (cones), `Waterfall_Small`; `Environment/Bridge`; FX birds, clouds, god rays, leaf fall; 8 layers (source of our `Terrain_*`). Demo `Demo/Grasslands Demo.unity` | Mountain shader ignores fog: use `GL_Mountain_*`. Never place `Other/Controller.prefab` |
| Lakefell Village | `Lakefell Village/Assets/` | `Buildings/House_1-10`, `Modules/` (walls, doors, `Extension_1_Fence`, `FenceEnd_1`), props, broadleafs, sunflowers, rocks; layers incl. `StonePath_1`. Demo `Demo/Demo_Scene.unity` | `House_1` door at local +Z. Avoid `Particle Systems/` (smoke, flame) and the combined-mesh buildings: built-in particle shader, magenta in URP |
| AZURE Nature | `AZURE Nature/Assets/` | `AN_*` firs, birches, broadleafs, cliffs, rocks (+ coverage variants), reeds, water lilies; 24 seasonal layers; own URP shaders | Not used yet |
| Stylized Terrain Textures | `Stylized Terrain Textures/Terrain Layers/` | 50 layers (grass, mud, rock, sand, snow, ice; autumn/dark/desert/winter sets) | Not used yet |

### BK Pure Nature (`Assets/_StoreAssets/Env/BK/`): support style

Six packs: AsianValley, FantasyForest, Jungle, Meadows, Mountains, Swamp (+ `Pure_Common` shaders and scripts). BK plants read globals `GrassRenderDist`, `MicroPower/Speed/Frequency`: without `BKFoliageGlobals` they fade beyond ~50 m. Don't place `Prefabs/Environment Manager` (it owns sun, fog and clouds; `EnvironmentDirector` does that here). BK plants have no LODGroup, so they work as detail prefabs directly. `Mountains/Mountain1/2` meshes are ~0.03 m at scale 1 (scale 20,000–45,000).

| Pack | Highlights |
| --- | --- |
| Meadows | Lawn and meadow plants (`Grass1-4`, `GrassMeadows1-4`, barley, wheat, sorrel, daisy, `CommonBlue/Pink/Yellow`, `FlowerMeadow1-4`, lavender, lupin, gorse, carrot, reeds); birch, oak, elm, willow, cypress, bushes; `Stone1-5`; `Mountain1/2`. The world map's lawn, flowers, meadow stones and reeds |
| Mountains | Fir, pine, spruce; mountain grass, ferns, lupin; boulders, cliffs, stones, pebbles; `Mountain1/2` (world map far range) |
| AsianValley | Lotus, astilbe (world map's calm streams), cyrtomium, bamboo, osmanthus, rocks, `AsianHill_0-5` (+ MicroVerse height stamps), `Fx/WaterSplash` (waterfall mist) |
| Jungle | Banana, kapok, tropical trees, big ferns, jungle boulders/cliffs, waterfall, dense fog, rain |
| Swamp | Swamp trees, cattails, lotus, wet grass, moss, usnea, lianas, mossy rocks |
| FantasyForest | Big trees and roots, big mushrooms, ivy, large ferns, dead leaves, mossy rocks |

### ANGRY MESH Stylized Pack – Meadow (`Assets/_StoreAssets/Env/ANGRY MESH/Stylized Pack - Meadow Environment/`)

Summer/Autumn/Winter sets: trees, `P_Rock_01-12`, rock groups, `Background/P_Hill_01/02`, `P_Mountain_01-04`, grass, flowers, props, VFX. `Prefabs Terrain Details/P_TD_*` are LOD-free detail versions. Quirks: vendor rocks are beige and mountains mauve (use `Environment/Mountains/AM_*`); the rock top layer needs `ASPTopLayerGlobals` (not the vendor global scripts, which set it every frame); mountains carry 14k-triangle MeshColliders (remove beyond the walls); the shader takes scene fog. Rocks scaled ×14–25 make granite domes for a near range.

### Water, grass and effects

- **Stylized Water 3** (3.3.3, `Assets/_StoreAssets/Rendering/Staggart Creations/Stylized Water 3/`): tools-unity-stylized-water.
- **Stylized Grass** (same folder): terrain grass/flower prefabs, benders, `GrassColorMapRenderer`. Not used on the world map.
- **SICS** particles used on the world map: `TFF_Fireflies_01A`, `TEM_Dust_01A` (gold motes). **Kroma** `Kroma_Particle_Dandelions_Prefab` (meadow fluff): disable `KromaCameraFollow` and collision.

### Other packs (not used; ask first, styles clash)

SICS Toon Fantasy Nature / Enchanted Meadow / Farm Pack / ToonScapes Spring Isles (toon shaders); PolyartStudio Dreamscape Meadows / Campsite / Castle and Farmlands (URP Shader Graphs; ignore their built-in folders and the Post Processing v2 / `PP` layer instructions; not in PLUGINS.md yet); Animpic Polygon Mega Survival (low-poly; its `Standard Assets/Environment/Water*` are built-in); NewLua farm crops.

## 3. The user's standing preferences (2026-10-18 → 23)

- **Raygeas is the main style**, BK is support (lawn, flowers, stones, reeds, far range), ANGRY MESH for the near boundary. Ask before bringing in another style.
- Komorebi **nature only** (pines, bushes, ferns, rocks, water, waterfall); no cultural props. Sakura only at the home pond.
- **Boundary**: mountains and rocks as models; the terrain is never raised for them; invisible walls on the playable edge only; not climbable; a different asset family per side.
- **Water**: Stylized Water 3; waterfall and pond as beautiful as the Komorebi demo; rivers crossable (no walls along banks, deepest point ~3 m, gentle banks; drowning comes later); calm flow.
- **Home base**: Lakefell house, simple fence with an open gate, no crops; a complete (not broken) bridge.
- **No COZY**; post-processing from a cloned, toned-down vendor grade.
- Declined: volumetric haze. Removed by hand: the sky-bounce fill light, massif E-B9.
- Pending, never done: the highlands rework ("the highlands seems boring, the current blueish tree on it seems off… raise the high ground a bit more, make the zone bigger by pushing the boundary mountains further, keep it climbable", 2026-10-19). Raise it when the highlands are next touched.
