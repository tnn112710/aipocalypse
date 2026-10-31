---
name: tools-unity-stylized-water
description: Stylized Water 3 in aipocalypse — WaterObject flat water, RiverModeler rivers built from code, underwater rendering, waterfalls, water materials, height queries and water perf. Use for any lake, pond, river, stream, waterfall, buoyancy, swimming / underwater code or water material work. NOT the map recipe for carving beds or rebuilding water after terrain changes (tools-unity-map-generation).
---

# Stylized Water 3 (aipocalypse)

Installed: **Stylized Water 3 3.3.3** (`Assets/_StoreAssets/Rendering/Staggart Creations/Stylized Water 3/`, "SW3/", full C# + shader source) with **River Modeler 1.0.1**, **Underwater Rendering 3.3.0**, **Dynamic Effects 3.3.1** (`Runtime/*/Extension.*.cs`). Namespaces `StylizedWater3`, `StylizedWater3.RiverModeler`, `StylizedWater3.UnderwaterRendering`. Vendor content: never edit it.

Read before acting:
- [references/api.md](references/api.md): classes, fields, enums, shader properties and keywords, checked against the 3.3.3 source with file:line.
- Map procedure (levels, carving, flood-filled sheets, ribbon trimming, junction design, dependents): [../tools-unity-map-generation/references/water.md](../tools-unity-map-generation/references/water.md). Don't duplicate it here.
- Project usage: `docs/technicals/ENVIRONMENT.md` (Water, Underwater, Waterfall), PLUGINS.md (Stylized Water row). Working code: `AgentScripts/Maps/WorldMap/WorldMapLayout.cs` ("WML") `BuildWaterMaterials` 1974, `FlatWaterMesh` 1866, `SaveMesh` 1930, `BuildFlatWater` 2123, `BuildUnderwater` 2176, `BuildRivers`/`BuildRiverGroup`/`RiverSpline` 2313/2358/2413, `MaterialCopy` 2794, `Landmarks.BuildWaterfall` 3217.
- Vendor: `SW3/CHANGELOG.md`, `RiverModeler/Changelog.md`, `Underwater/CHANGELOG.md`, `Quickstart.pdf` (`/mingw64/bin/pdftotext` works), online docs https://staggart.xyz/unity/stylized-water-3/sw3-docs/ (+ `sw3-river-modeler-docs/`, `sw3-underwater-rendering-docs/`). Source beats docs.

## 1. Modules here

| Module | Status | Why |
| --- | --- | --- |
| Water shader + `WaterObject` on meshes | **Used**: lake, ponds, swamp, waterfall source pool, river underside | |
| `RiverModeler` (splines → ribbons) | **Used**: `Environment/Water/Rivers` (River, Outlet, Streams) | |
| Underwater Rendering (`UnderwaterArea`) | **Used**: 5 areas, one per flat level | |
| River audio (`RiverAudioManager`, profiles) | **Off** (`settings.audio.enable = false`) | static singleton + own AudioSource pool; BroAudio owns audio |
| Dynamic Effects | **Not used** (feature flag on, idle) | ~160 MB buffers at our settings; passes early-out with no `DynamicEffect` (`RenderFeature.DynamicEffects.cs:100`) |
| `PlanarReflectionRenderer` | **Never** | a second full scene render (terrain, trees) per camera |
| RiverModeler MicroVerse section | **Not used** | overwrites an existing SplinePath into a flat-bed constant-depth canal (`RiverModeler.MicroVerse.cs:45-119`); beds are carved by the generator |
| `WaterGrid`, `OceanFollowBehaviour`, `AlignToWater`, `SetCustomWaterTime`, `SetWaterPositionOffset` | Not used | camera-following ocean tiles; per-instance FixedUpdate; global shader statics |

## 2. Mental model

- **Water = any mesh with an SW3 material** (`Shaders/StylizedWater3_Standard.watershader3`, ScriptedImporter template). `WaterObject` only *identifies* it (static `Instances`, `Find`, height queries, underwater auto-setup). Keep every water renderer on layer **Water (4)**: the height pre-pass renders only that layer (`Runtime/Rendering/HeightPrePass.cs:62`).
- **Flat water: mesh at local y 0, transform Y = water level.** No depth write (`_ZWrite 0`); shoreline, depth colour, foam and edge fade come from the opaque depth texture, so terrain above the level hides the mesh. A sheet may run under the banks; it must never end in open water.
- **River: `RiverModeler` + `SplineContainer` (same GameObject, identity transform).** Per spline one `SplineRiver` child, cut into `RiverSegment`s (≤ `maxSegmentLength`, min 2), each with MeshFilter, MeshRenderer, `WaterObject`, layer Water, shadows off, and a `new Mesh()` **embedded in the scene** (`RiverSegment.cs:123-142`). Knot Y = surface; flow runs knot 0 → last; full width = `baseWidth × RIVER_SCALE.x`. The job bakes bank gradient, transparency, wave flattening and foam into vertex colours RGBA and slope foam into uv0.z (`RiverGenerationJob.cs:425-433`); the `_RIVER` material reads them.
- **Render feature** (`Assets/_Game/Settings/PC_Renderer.asset:30-62`): SSR allowed, height pre-pass on (500 m, 4 cells/m), underwater on (RenderPass), Dynamic Effects on but idle. It skips overlay, preview and any camera with `hideFlags != None` (`StylizedWaterRenderFeature.cs:217-234`). Depth + opaque textures are on in `PC_RPAsset`.
- **Draw order**: still water queue 3000, river material 3002, waterfall copy 3001. Transparent sorting honours renderer `sortingOrder` before queue (observed with the waterfall FX).
- **Time**: shader time = `_TimeParameters.x × _Speed` (`Shaders/Libraries/Common.hlsl:16`, `Vertex.hlsl:93`); CPU waves mirror `Time.time` in play mode (`Runtime/Gerstner.cs:20-34`). A `TimeService` pause freezes water. `WaterObject.CustomTime` overrides it globally.

## 3. Project rules

- **Prompt first.** Current spec: no swimming, the player walks on the bed (map-generation water.md §1). Don't add swimming, drowning, buoyancy or new water bodies unless the prompt asks.
- **First-party copies only**, in the shared kit `Assets/_Game/Environment/Water/` (`Water_Still`, `Water_Pond`, `Water_Swamp`, `Water_River`, `Water_RiverUnderside`, `Water_Waterfall`). Never assign vendor materials: RiverModeler's inspector fix buttons write into the assigned material (`Editor/RiverModeler/RiverModelerInspector.cs:202-226`, `SettingsEditors.cs:255-262, 341-348`), and the Quickstart warns that updates overwrite vendor materials. Create or refresh with WML `MaterialCopy` (refresh from vendor, then tune). `CopyMaterial` (WML:2096) returns a stale copy and keeps old values.
- **Toggles from code need their keyword**: `SetFloat` never runs `[Toggle(KW)]` drawers. Use `SetFloat` + `CoreUtils.SetKeyword` (WML `WaterToggle` 2090). `_ShadingMode` → `_ADVANCED_SHADING` and `_FoamOn`/`_FoamDistanceOn` → `_SURFACE_FOAM_SINGLE`/`_DUAL` are set only by the private `MaterialUI.SetMaterialKeywords` (`Editor/Shader/MaterialUI.cs:667-682`). `[ToggleOff]` is inverted: property 0 = keyword **on** (`_ENVIRONMENTREFLECTIONS_OFF`). Table in api.md §8.
- **Audio**: every RiverModeler gets `settings.audio.enable = false; settings.audio.profile = null` (`Reset()` assigns a profile and the default is on, `Settings.cs:171`). Never add a `RiverAudioManager`. Leave `UnderwaterArea.environmentAudioMixer` empty (it drives a mixer low-pass from render code).
- **Gameplay code never touches SW3 statics directly** (`WaterObject.Instances/Find/CustomTime/PositionOffset`, `UnderwaterArea.Instances`, `HeightQuerySystem`, `RiverModeler.onPre/PostRebuildRiver`, `RiverSegment.onTrigger*`). If a prompt needs water queries, one Core service registered by its system's installer wraps them, with scene water refs injected (as `EnemyMover` gets the scene's `AstarPath` injected rather than using `AstarPath.active`), and game code sees an interface. No `AlignToWater` on crowd entities (per-instance FixedUpdate; `Start` turns off Rigidbody gravity, `AlignToWater.cs:74-83`).
- **Saving**: specific assets only (AGENTS.md editor gotchas). WML `BuildUnderwater` still calls `SaveAssets()` at 2188: fix it when you touch that code.
- **Editor control** via the Unity CLI (`script run`). These SW3 helpers open modal dialogs and stall the CLI: `StylizedWaterEditor.EnableDepthTexture/EnableOpaqueTexture` (`StylizedWaterEditor.cs:356, 384`), `PipelineUtilities.ValidateRenderFeatureSetup` (`PipelineUtilities.cs:254`), `RiverModelerEditor.CreateRiverObject(true)` (`:84`), the "Add River" context menu (`:139`).
- **Performance decisions stay** unless the user reopens them: environment reflections and SSR off in every water material (`WaterReflectionsOff`, WML:2083), lake probe deactivated, no planar reflections, no Dynamic Effects.

## 4. Recipes

**4.1 Flat body (lake, pond, pool in a building).** Mesh: generator flood fill (map-generation water.md §3), or for a rectangle `WaterMesh.Create(WaterMesh.Shape.Rectangle, size, vertexDistance)` (a square; scale the transform for a rectangle: UVs are world XZ when `_WorldSpaceUV` = 1). Save it as a mesh asset (prefabs and scenes need a persistent mesh). Write normals up, uv0 = local XZ and `Color` vertex colours with **G = −1** when the material has `_VertexColorTransparency 1` (all ours do).
```csharp
var wo = WaterObject.New(mat, meshAsset);                 // GO + MeshFilter/Renderer + WaterObject, layer Water, shadows off, Undo
wo.name = "Pool"; wo.transform.SetParent(parent, false);
wo.transform.position = new Vector3(cx, level, cz);       // Y = level: CPU queries and UnderwaterArea.Reset read it
foreach (var r in wo.GetComponentsInChildren<Renderer>()) r.sortingOrder = -10;   // ribbons and FX draw over it
```
Pool extras: the rim must be opaque geometry above the level; an `UnderwaterArea` (4.4) if the camera can go under; `_Cull 0` on its material copy.

**4.2 River or stream ribbon from code** (WML `BuildRiverGroup`/`RiverSpline`). Order matters:
```csharp
// using RM = StylizedWater3.RiverModeler;  (namespace and class share the name)
var go = new GameObject(name); go.transform.SetParent(parent, false);       // identity: knots = world positions
var container = go.AddComponent<SplineContainer>();
container.Splines = knotLists.Select(k => RiverSpline(k, baseWidth)).ToList();  // replaces the default EMPTY spline
var m = go.AddComponent<RM.RiverModeler>();         // Reset(): default audio profile + River Foam VFX
m.material = riverMat; m.foamVFX = vfx; m.rebuildOnStart = false;
var s = m.settings;                                 // class holding structs: edit fields in place
s.geometry.baseWidth = 30f; s.geometry.minEdgeDistance = 2f; s.geometry.widthEdgeDistance = 2.5f;
s.output.maxSegmentLength = 100f; s.audio.enable = false; s.audio.profile = null;
int seg = Mathf.Max(2, Mathf.CeilToInt(Mathf.Round(longest / strip) * strip / Mathf.Max(s.output.maxSegmentLength, strip)));  // strip = clamp(minEdge, 0.1, maxSeg)
s.transparency.startGradientFalloff = startMetres / seg; s.transparency.endGradientFalloff = endMetres / seg;
m.SetSplineContainer(container);                    // LAST: from here every Spline.Changed rebuilds that spline
m.Rebuild();
for (int i = 0; i < m.rivers.Count; i++) if (m.rivers[i]) m.rivers[i].SetProperties(riverMat, i);  // first build leaves sortingOrder 0
```
`RiverSpline`: `new Spline(points(x, surfaceY, z), TangentMode.AutoSmooth, false)` (AutoSmooth tilts knots on slopes, which drives cascade foam), then a `SplineData<float4>` with `PathIndexUnit.Distance`, one point per knot at `ConvertIndexUnit(i, Knot, Distance)`, value `(fullWidth / baseWidth, 1, 1, 1)` (y = turbulence multiplier), stored with `SetFloat4Data(RM.RiverModeler.SCALE_DATA_KEY, data)`. One modeler = one material + one settings block: group splines by kind. To remove a river, delete the whole GameObject.

**4.3 Draw order (water kinds and the waterfall).**

| Renderer | Queue | sortingOrder | Set by |
| --- | --- | --- | --- |
| Flat sheets (still, pond, swamp) | 3000 | −10 | WML:2164 |
| River ribbon segments | 3002 (`Water_River`) | spline index (higher draws on top) | `SplineRiver.SetProperties` |
| River foam VFX | n/a | `queue − 3000 + 2 + spline index` | `RiverModeler.cs:357` |
| Waterfall sheet (Komorebi `Waterfall_1`, copy `Water_Waterfall`) | 3001 (`_QueueOffset` 1) | 0 | `WaterfallMaterial` (WML:3316) |
| SW3 `Waterfall Impact Splashes`, BK `WaterSplash` mist | 3000 | **1** (to draw after the 3001 sheet) | `BuildWaterfall` |

Overlapping water double-tints (neither surface writes depth): cut outlines at mouths and fade one surface out over the other.

**4.4 Underwater area per flat level** (WML `BuildUnderwater` → `Area`):
```csharp
var go = new GameObject(name); go.layer = LayerMask.NameToLayer("Water");
go.transform.position = new Vector3(cx, level, cz);
var box = go.AddComponent<BoxCollider>();
var area = go.AddComponent<UnderwaterArea>();        // Reset(): box → 100×50×100, nearest WaterObject's material + level
box.isTrigger = true; box.size = new Vector3(w, 18f, d); box.center = new Vector3(0, -6f, 0);   // level −15 … level +3
area.boxCollider = box;
area.waterLevelSource = UnderwaterArea.WaterLevelSource.FixedValue; area.waterLevel = level;
area.waterMaterial = mat;                            // null → the area is ignored
area.shadingSettings ??= new UnderwaterArea.ShadingSettings(); area.shadingSettings.fogDensity = 0.25f;
area.FindShaders();                                  // editor-only lookup; serializes underwaterResources
```
One area = one flat level. River mode never renders underwater (shader `#undef`s it); under a flat-level river, add a flat sheet with a back-faces-only copy (`_Cull 1`, `Water_RiverUnderside`). Surfaces seen from below need `_Cull 0`. Sloped water gets no underwater (or a `Transform` level source moved per frame: not built). Author areas in the editor or in a prefab: at runtime `FindShaders()` returns null and `OnEnable` throws without resources.

**4.5 Water height for gameplay** (only if the prompt asks):

| Need | Use | Caveat |
| --- | --- | --- |
| Level of a flat body | its `WaterObject.transform.position.y` | **CPU `Gerstner.ComputeHeight` writes nothing when `_WAVES` is off** (`Gerstner.cs:43-46`): `AlignToWater`/`HeightQuerySystem.Interface` CPU mode return 0 on our wave-less water |
| Which body covers XZ | own body list + footprint test | `WaterObject.Find` = first AABB hit in registration order; includes river segments; AABBs cover dry land |
| River surface | `RiverModeler.FindNearestPointOnRiver(p, out point, …, sampleDistance)` | only inside a segment's mesh bounds; cost ≈ segment length / sampleDistance; width clamp samples near the spline start (bug, api.md §4) |
| Rendered surface incl. displacement | GPU `HeightQuerySystem.AsyncRequest` | ≤128 samples per request, ≥1 frame late, play mode main-camera-tagged camera only, 500 m range, −1000 on a miss |
| Camera underwater (post FX, UI) | `UnderwaterArea.GetFirstIntersecting(cam)` + `CameraSubmerged(cam)` | same test as the renderer |

River segment transforms sit at the curve centre: their Y is **not** a water level.

**4.6 Testing and captures.**
- Use the Scene view, the Game view or a plain visible camera. SW3 renders nothing for cameras with `hideFlags` set, preview or overlay cameras, so a hidden temp camera shows no underwater.
- Underwater shows only when the camera's near plane is inside an area box. The Scene view needs image effects on (`RenderFeature.UnderwaterRendering.cs:125`).
- Play mode: `Application.runInBackground = true` right after `editor.playmode start`; `capture game_view` without `--save_path` + `AgentScripts/Vision/shot.sh`. Never set runInBackground in edit mode.
- After scene open, ribbons log GRD "invalid Mesh" for a few seconds; deleted water keeps drawing until the scene reopens (`OpenScene()`).
- Numbers before pixels: `m.LastProcessingTime`, `rivers[i].Segments.Count`, material keyword lists (`m.shaderKeywords`).

## 5. Units and values that worked

Units, formulas and our values for the water shader and RiverModeler properties, with source lines: [references/values-and-gotchas.md](references/values-and-gotchas.md) §5. Check it before changing a water material value.

## 6. Gotchas (3.3.3 source)

G1–G16 from the 3.3.3 source (empty splines, container order, RIVER_SCALE units, linked knots, tip fade, sortingOrder, 16-bit indices, static flags, CPU height queries, UnderwaterArea, underwater fog on transparents, vertex colours, mesh refresh, inspector buttons, cascade angle, material dropdown): [references/values-and-gotchas.md](references/values-and-gotchas.md) §6. Read them before building or editing rivers, sheets or underwater areas.

## 7. Performance

- Shipped: environment reflections and SSR off in every water material (cost not measured). Planar reflections: never. Lake probe: off.
- **Asset state vs docs**: all five SW3 `_Game` water materials (not `Water_Waterfall`, which uses the Komorebi shader) also have `_SpecularReflectionsOn 0` + `_SPECULARHIGHLIGHTS_OFF` and `_ReceiveShadows 0` (since commit bb665145e, not set by the generator). That removes the sun highlight, although ENVIRONMENT.md/PLUGINS.md say it stays. Ask before changing either.
- Height pre-pass `enable: 1` renders the Water layer every frame into a 2000² RG16F target (500 m × 4 cells/m, ~16 MB) for the main camera; its only consumers are GPU height queries and the Shader Graph height sub-graph, and queries switch it on by themselves (`StylizedWaterRenderFeature.cs:83`). Turning it off is a candidate saving: unmeasured, the user decides.
- Dynamic Effects: free while no `DynamicEffect` exists; any SW3 `Prefabs/DynamicEffects/*` or `ShorelineWaveSpawner` allocates its buffers (~160 MB at renderRange 500 × 8 texels/m). Don't place them.
- Underwater: a sphere-then-box test over all areas per camera; when active, a depth pre-pass + mask + shading + waterline pass.
- RiverModeler: Mesh LODs (`lodLevels` 4); `maxSegmentLength` is also the culling unit (1000 m stream segments are never culled); every segment mesh is serialized into the scene (WorldMap.unity ~27.7 MB). Cascade particles add a `VisualEffect` per segment.
- Transparent overdraw: every overlapping sheet or ribbon is shaded twice. Keep sheets to the wet area (flood fill) and trim ribbons.
- Each distinct keyword set is a shader variant: keep palettes on one keyword set and change only floats and colours.

## 8. Re-verify on an SW3 upgrade

The 9-step checklist (versions, public API, RiverModeler order, job math, shader, CPU waves, vendor materials, importer, before/after captures): [references/upgrade-checklist.md](references/upgrade-checklist.md).
