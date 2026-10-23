# Stylized Water 3 3.3.3: checked API

Read from the installed source on 2026-10-23. `SW3/` = `Assets/_StoreAssets/Rendering/Staggart Creations/Stylized Water 3/`, `RM/` = `SW3/Runtime/RiverModeler/`, `TPL` = `SW3/Shaders/StylizedWater3_Standard.watershader3`, `FP` = `SW3/Shaders/Passes/ForwardPass.hlsl`. Line numbers are 3.3.3 (River Modeler 1.0.1, Underwater 3.3.0). Members not listed were not checked: read the source before using them. After an upgrade: `grep -n "public " <file>` on each file below and diff.

Assemblies: `sc.stylizedwater3.runtime` (`SW3/Runtime/`), `sc.stylizedwater3.river.runtime` (`RM/`, references `JBooth.MicroVerseCore`), editor `sc.stylizedwater3.editor`, `sc.stylizedwater3.river.editor`. Version defines active here: `URP`, `SPLINES`, `VFX_GRAPH`, `BURST`, `MICROVERSE`. Compiled shader name: `Stylized Water 3/Standard` (`Editor/Shader/TemplateParser.cs:131`).

## 1. WaterObject (`SW3/Runtime/Components/WaterObject.cs`, namespace `StylizedWater3`)

`[ExecuteInEditMode] [DisallowMultipleComponent]`.

| Member | Line | Notes |
| --- | --- | --- |
| `const int WaterLayer = 4` | 26 | |
| `static readonly List<WaterObject> Instances` | 32 | add/remove in `OnEnable`/`OnDisable` (104-112). River segments are in it too |
| `Material material`, `MeshFilter meshFilter`, `MeshRenderer meshRenderer` | 34-36 | `OnValidate` re-fetches `material` from the renderer (114-119) |
| `static Vector3 PositionOffset { set; internal get; }` | 46-54 | sets global `_WaterPositionOffset` (floating origin). Global state |
| `static float CustomTime { get; set; }` | 66-74 | sets global `_CustomTime`; < 0 reverts to normal time. Global state |
| `MaterialPropertyBlock props`, `ApplyInstancedProperties()` | 77-89, 138-141 | |
| `Material FetchWaterMaterial()` | 124-133 | |
| `void AssignMesh(Mesh)`, `void AssignMaterial(Material)` | 143-152 | |
| `static WaterObject New(Material waterMaterial = null, Mesh mesh = null)` | 159-179 | new GO "Water Object" + MeshFilter + MeshRenderer + WaterObject; layer `Water`; shadows off; Undo registered in the editor |
| `static WaterObject Find(Vector3 position, bool rotationSupport)` | 187-207 | vertical ray from +1000 m against each instance's bounds; returns the **first** hit in list order, not the nearest. `rotationSupport = true` re-transforms the same ray origin cumulatively per object (bug) |

## 2. WaterMesh (`SW3/Runtime/WaterMesh.cs`) and shipped meshes

- `enum Shape { Rectangle, Disk }` (20-24). Fields: `shape`, `scale = 100` (`[Range(10,1000)]`), `vertexDistance = 1` (`[Range(0.15,10)]`), `UVTiling = 1`, `noise`, `boundsPadding = 4`, `mesh` (25-47).
- `Mesh Rebuild()` (52); `static Mesh Create(Shape shape, float size, float vertexDistance, float uvTiling = 1f, float noise = 0f)` (65). Rectangle = square `size × size` centred on the origin.
- Output: normals up, tangent `(-1, 0, 0, -1)` (50), all vertex colours 0 (171, 254), `UInt32` indices at ≥ 65,536 vertices (165, 244), bounds `(scale, boundsPadding, scale)` (176, 255).
- Assets: `SW3/Models/{HighPoly_Plane, HighPoly_Plane_wNoise, HighPoly_Circle, LowPoly_Plane}.watermesh` (ScriptedImporter). Menu `Assets/Create/Water/Mesh` (`Editor/StylizedWaterEditor.cs:145`), `GameObject/3D Object/Water/Single Object` (:23).
- The WorldMap's generated sheets (WML `FlatWaterMesh`) write positions, normals, uv0 and `Color` (G = −1 or a feather), no tangents; they render fine.

## 3. Height queries

**`HeightQuerySystem`** (`SW3/Runtime/HeightQueries/HeightQuerySystem.cs`, static partial class)

| Member | Line | Notes |
| --- | --- | --- |
| `static bool DISABLE_IN_EDIT_MODE` | 20-28 | EditorPrefs |
| `static bool IsSupported()` | 34-41 | compute shaders |
| `static bool EqualsVoid(float)` | 51-54 | `≤ HeightPrePass.VOID_THRESHOLD` (−1000, `Rendering/HeightPrePass.cs:28`) |
| `static Vector3 DeriveNormal(float left, float right, float down, float up, float strength = 1)` | 65-71 | |
| `class Interface` | 77-149 | `Method { GPU, CPU }` (default GPU); `WaterObject waterObject`; `bool autoFind = true`; `WaveProfile waveProfile`; `enum WaterLevelSource { FixedValue, WaterObject, Transform, Ocean }` (default `WaterObject`); `Transform waterLevelTransform`; `float waterLevel` |
| `Interface.GetWaterLevel()` | 119-131 | WaterObject source = `waterObject.transform.position.y` |
| `Interface.GetWaterObject(Vector3)` | 138-143 | `autoFind` → `WaterObject.Find(pos, false)` |
| `Interface.HasMissingReferences()` | 145-148 | true unless waterObject, its material **and** a waveProfile are set |
| `static readonly List<Query> queries`, `static int QueryCount` | 155-158 | |
| adding a request with a used hashCode | 172-179 | throws |

**`Sampler`** (`HeightQuerySystem.Sampler.cs:19`): `NativeArray<float3> positions` (24), `NativeArray<float> heightValues` (28, Persistent, zero-initialised); `SetSampleCount(int sampleCount, bool cpu = false)` (49-77; > `Query.MAX_SIZE` = 128 throws unless `cpu`); `SetSamplePosition(int, float3)` (85-93); `Dispose()` (95-101). Always Dispose.

**`AsyncRequest`** (`HeightQuerySystem.Request.cs:16`): ctor `(int hashCode, Sampler sampler, string label = "")` (45); `event onCompleted` (22); `bool invalidateMisses = true` (35); `Issue()` (52-60, throws `WarningException` without compute support); `Withdraw()` (62); `Dispose()` (67-72, also disposes the sampler). Read back with `AsyncGPUReadback` (`HeightQuerySystem.Query.cs:164`), so results arrive at least one frame later. In play mode the pass runs only for a camera tagged `MainCamera` (`StylizedWaterRenderFeature.cs:274`) and only while `QueryCount > 0` (:283).

**CPU waves** (`SW3/Runtime/Gerstner.cs`): `ComputeHeight(Sampler, Interface)` (36-39), `ComputeHeight(Sampler, WaveProfile, float waterLevel, Material)` (41-59): **returns without writing anything when the material's `_WAVES` keyword is off** (43-46), so `heightValues` stay 0. Time = `WaterObject.CustomTime > 0 ? CustomTime : Time.time` in play, `realtimeSinceStartup` in edit mode (20-34).

**`AlignToWater`** (`SW3/Runtime/Components/AlignToWater.cs`): `heightInterface`, `surfaceSize`, `followTarget`, `heightOffset`, `rollAmount`, `rotation`, `smoothing`, `HeightValue { Average, Maximum } heightValue`, `rigidbody` (23-60). `Start` sets `rigidbody.useGravity = false` (74-83). GPU mode issues one `AsyncRequest` of 4 samples per component (97-118). Work runs in its own `FixedUpdate` (227-257). Not for crowds.

## 4. RiverModeler (`RM/RiverModeler.cs`, `RM/RiverModeler.Splines.cs`; namespace `StylizedWater3.RiverModeler`)

`[ExecuteInEditMode] [SelectionBase]` (34-37).

| Member | Line | Notes |
| --- | --- | --- |
| `Material material` | 39 | must be a river-mode material (§8) |
| `VisualEffectAsset foamVFX` | 41 | default `RM/VFX/River Foam VFX.vfx` (GUID `f91270c6f4288ea4f98d1de4a62067fa`) |
| `Settings settings` | 44 | §5 |
| `List<SplineRiver> rivers` | 46 | index = spline index |
| `bool rebuildOnStart` | 48-49 | needed in prefabs (meshes aren't saved in assets) |
| `const string SCALE_DATA_KEY = "RIVER_SCALE"` | 51 | `float4`: x width multiplier, y turbulence multiplier (y − 1 adds displacement), z/w 1 |
| `float LastProcessingTime` | 56 | ms |
| `static event OnRebuildRiver onPreRebuildRiver, onPostRebuildRiver`; `RebuildEvent onPreRebuild, onPostRebuild` | 59-72 | static events are global state |
| `[Flags] enum ChangeFlags { None, Spline, Geometry, Scale, Foam, Transparency, Audio, MicroVerse, All }` | 79-90 | |
| `void Reset()` | 98-110 | container from parent; **assigns the default audio profile**; foam VFX by GUID |
| `void Rebuild(ChangeFlags changeFlags = All, int splineIndex = -1)` | 127-150 | silent no-op without container or material (130); splines in index order |
| `static void SetupConnection(…)` | 417-471 | junction data for linked end knots (G4) |
| `bool IsAllowedToRebuild(bool suppressError = true)` | 478-494 | false for prefab assets |
| `SplineContainer SplineContainer { get; private set; }`, `void SetSplineContainer(SplineContainer)` | Splines 32-42 | assign last |
| subscribe / unsubscribe | Splines 44-49 / 66-69 | `OnDisable` drops only `Spline.Changed` |
| `SplineData<float4> GetScaleData(int splineIndex)` | Splines 83-95 | creates data (1) if missing; forces `PathIndexUnit.Distance` |
| `float SampleWidth(int splineIndex, float distance)` | Splines 97-105 | full width (m) at a distance (m) |
| `void ResetScaleData()` | Splines 107-116 | |
| `bool FindNearestSplinePoint(float3 worldPosition, out float3 point, out float3 tangent, out float3 up, out float t, float sampleDistance = 1)` | Splines 128-154 | segments whose mesh bounds contain the point; `t` normalised |
| `bool FindNearestPointOnRiver(float3 worldPosition, out float3 point, out float3 direction, out float3 normal, out float t, float sampleDistance = 1)` | Splines 166-191 | point on the surface, laterally clamped (bug below); no displacement |
| `bool IsOnOrUnderRiverSurface(float splineT, int splineIndex, float3 target, float3 splinePosition, float3 forward, float3 up)` | Splines 203-216 | compares the lateral offset with the **full** width (212): 2× generous |

Segment count (`RiverModeler.cs:177-185`): `strip = clamp(minEdgeDistance, 0.1, maxSegmentLength)`, `segments = max(2, ceil(round(L / strip) · strip / max(maxSegmentLength, strip)))`, L = world spline length. Strips are spread evenly over segments (266-276); `CurveRange` = metres along the spline.

Build order traps: invalid spline (< 0.01 m or < 2 knots) returns before `rivers.Add` and the next spline throws (161, 193-198); `SetProperties` before `PrepareContainers` (206, 212) leaves new segments at sortingOrder 0; static editor flags copied to segments (307-319); VFX sortingOrder `material.renderQueue − 3000 + 2 + SplineIndex` (357); `UpdateMicroVerseSplines` after Scale/MicroVerse rebuilds (384-387).

## 5. Settings (`RM/Settings.cs`; a class holding structs, edit `m.settings.geometry.x = …` in place)

| Field | Line | Default | Ours (River / Streams) |
| --- | --- | --- | --- |
| `geometry.baseWidth` (full width, m) | 24 | 10 | 30 / 12 |
| `geometry.minEdgeDistance` (row spacing, m) | 28 | 1 | 2 / 1 |
| `geometry.widthEdgeDistance` (column spacing, m) | 33 | 1 | 2.5 / 1.5 |
| `geometry.simplifyStraight` (skip rows below `value × 0.01°` curvature) | 37 | 25 | 25 |
| `geometry.roundedEnds` | 38 | false | false |
| `geometry.displacementStrength` `[Range(0,3)]` | 42 | 0 | 0 / 0.15 |
| `geometry.displacementSlopeInfluence`, `displacementScale` | 44, 47 | 0, 0.1 | 0, 0.1 |
| `geometry.lodLevels` `[1,8]`, `lodReduction` %, `lodSelectionSlope` | 51, 54, 57 | 4, 50, 0.5 | same |
| `geometry.twistCorrection` | 62 | true | true |
| `foam.displacementFoam` | 87 | 1 | 1 |
| `foam.cascadeAngleThreshold` (degrees, but `[Range(0,1)]`) | 89-91 | 10 | 8 |
| `foam.cascadeAngleFalloff` (degrees) | 93 | 15 | 8 |
| `foam.enableCascadeParticles`, `cascadeParticleSize` | 96, 98 | true, 1 | false / true, 1 |
| `foam.splashThreshold`, `enableSplashParticles`, `splashParticleCount`, `splashParticleSize` | 102-107 | 0.25, false, 3, 1 | off |
| `foam.velocityStrength` (× 1000 particle velocity) | 109 | 1 | 0.45 |
| `transparency.startGradientFalloff`, `endGradientFalloff` | 139, 141 | 3, 3 | metres ÷ segments |
| `transparency.widthGradientFalloff` (m from each edge) | 144 | 1 | 2 / 1 |
| `audio.enable`, `audio.profile` | 162, 165 | **true**, null (Reset assigns one) | false, null |
| `output.maxSegmentLength` `[Min(10)]` | 186 | 100 | 100 / 1000 |
| `output.boxTriggers`, `output.collider` | 191, 192 | false | false |
| `microVersePathSettings` (`MicroVerseSettings[1]`: splinePath, bankHeight 0.5, bankWidth 5, bankFalloff 2, bankSplatWidth, bankSplatSmoothness 3, bedDepth 2, bedSmoothness 0, offset) | 205-229 | one null entry | null (unused) |

## 6. SplineRiver, RiverSegment, Structs

**`SplineRiver`** (`RM/SplineRiver.cs`): `RiverModeler Owner` (25-26), `int SplineIndex` (29-30), `List<RiverSegment> Segments` (32-33), `static Create(RiverModeler)` (35-51, child "Spline River", transform `hideFlags = NotEditable`), `PrepareContainers(int count)` (53-91), `SetProperties(Material, int index)` (93-102: material + `sortingOrder = index` on every segment).

**`RiverSegment`** (`RM/RiverSegment.cs`):

| Member | Line | Notes |
| --- | --- | --- |
| `SplineRiver River`, `Vector2 CurveRange` | 34-44 | range in metres |
| `Mesh mesh`; `meshBounds` (serialized) | 46-47 | `new Mesh()` (135): stored in the scene |
| `static RiverSegment Create(Transform parent)` | 123-142 | MeshFilter, MeshRenderer (shadows off), layer Water, `WaterObject` |
| `OnEnable`/`OnDisable` | 97-114 | play mode: `RiverAudioManager.Register/Unregister` (static list only) |
| `UpdateTrigger(bool)`, `UpdateCollider(bool)` | 144, 157 | BoxCollider trigger / MeshCollider |
| `FindNearestSplinePoint(…)` | 181-249 | throws when orphaned (183-192); samples `curveLength / sampleDistance` points |
| `FindNearestPointOnRiver(…)` | 251-278 | **bug**: passes normalised `t` to `SampleWidth`, which reads it as metres (255), so the clamp uses the width near the spline start |
| `MeshBoundsContains(Vector3)`, `SetMaterial`, `SetSortingOrder`, `SetLODBias` | 394-414 | |
| `static event TriggerAction onTriggerEnter, onTriggerStay, onTriggerExit` | 491-495 | need `output.boxTriggers` |

**`Structs`** (`RM/Structs.cs`): `AudioEmitter { Type {Stream, Rapids, Cascade}, position, minRadius, maxRadius }`, `ConnectionInfo { isValid, t, radius, position, normal }`, `ParticleEmitter { position, velocity, scale }`.

## 7. Ribbon generation math (`RM/Jobs/RiverGenerationJob.cs`, `…Utilities.cs`)

- Sampling every `SAMPLE_DISTANCE = 0.5` m (27, 142). A vertex row is made at both ends, or when ≥ `minEdgeDistance` from the last row **and** curvature ≥ `simplifyStraight × 0.01°` (214, 232-257). Columns: `widthSegments = clamp(ceil(baseWidth / max(0.1, widthEdgeDistance)), 1, 100)` (137-139), spanning `baseWidth × scale.x` (249, 283, 349).
- Orientation from the knot's up vector; `twistCorrection` locks roll (236-237). Cross-section level across, tilted along.
- Tip fade: `CalculateDistanceWeight(t × curveLength, …, startFade, …, endFade)` with spline-wide t (303-305) → visible metres = setting × segments. A linked end gets no tip fade (211-212). Width fade: `EdgeDistanceMask` in metres (306; `Utilities.cs:109-117`). `alpha = saturate(tip + width)` (316); junction fade (353-361); first/last rows of the spline forced to alpha 1 (365-366).
- Junction (`Utilities.cs:31-62`): half-plane `dot(v − position, −normal)`, fade over `0.15 × radius`, noise added; the distance limit is commented out (41); the joining spline's Y is blended to the receiver.
- Cascade weight: `saturate((angle − (2·threshold − falloff)) / falloff)` (`Utilities.cs:21-29`).
- Vertex data (402-433): uv0 = `(−xt × width, −t × splineLength, slopeFoam, alpha)` in metres; colour = `(widthGradient, alpha, waveFlattening, foam)`; uv1 = lightmap-style 0-1.
- Indices `ushort` (78, 678): ≤ 65,536 vertices per segment. Mesh LODs via `mesh.SetLod` on Unity 6.2+ (685-699).

## 8. Shader properties and keywords (`TPL` properties 12-192; pass 238-245)

| Property | TPL | Type / keyword | Notes |
| --- | --- | --- | --- |
| `_ZWrite` | 15 | Toggle (no keyword), default 0 | must stay 0 for rivers; inspector hides it unless "Disable depth texture" (3.3.2) |
| `_Cull` | 17 | Enum Both 0, Back 1, Front 2 (default 2) | "Render faces"; value goes to `Cull [_Cull]` |
| `_DepthOffset` | 18 | −3..3 | z-fight fix (`Offset [_DepthOffset], 0`) |
| `_ShadingMode` | 19 | 0 Performance, 1 Appearance → `_ADVANCED_SHADING` (MaterialUI only) | |
| `_WorldSpaceUV` | 21 | 0 mesh UV, 1 world XZ | |
| `_Direction`, `_Speed` | 22-23 | | `TIME = _TimeParameters.x × _Speed` |
| `_SlopeSpeed`, `_SlopeFoam`, `_RiverModeLegacy` | 26-30 | legacy default 1 | RiverModeler needs legacy 0 |
| `_BaseColor`, `_ShallowColor`, `_ColorAbsorption`, `_HorizonColor`, `_HorizonDistance` | 35-40 | | colour = `lerp(shallow, base, density)` (FP:467) |
| `_VertexColorTransparency` | 41 | Toggle | `a = a · saturate(a − G)` (FP:1012) |
| `_FogSource` | 42 | 0 depth texture, 1 vertex G | |
| `_DepthVertical`, `_DepthHorizontal` | 43-44 | | FP:152-153 (names swapped vs meaning, §5 of SKILL) |
| `_EdgeFade` | 45 | float | centimetres (FP:825) |
| `_Caustics*` | 58-64 | | |
| `_RefractionStrength` | 70 | | with `_REFRACTION`: rgb lerps over the opaque texture, alpha = edge fade (FP:980-982) |
| `_IntersectionSource`, `_IntersectionLength`, `_IntersectionFalloff`, `_IntersectionSharp`, `_IntersectionClipping`, `_IntersectionTiling`, `_IntersectionSpeed`, `_IntersectionRipple*` | 74-87 | | FP:504-509 |
| `_Foam*`, `_VertexColorFoam` | 90-118 | | |
| `_NormalTiling`, `_NormalStrength`, `_NormalSpeed`, `_NormalSubSpeed` | 123-127 | | |
| `_SparkleIntensity`, `_SunReflection*`, `_PointSpotLightReflection*` | 133-145 | | |
| `_ReflectionStrength`, `_ReflectionDistortion`, `_ReflectionFresnel`, `_ScreenSpaceReflectionsEnabled` (Toggle, no keyword) | 148-155 | | |
| `_WaveSpeed`, `_WaveFrequency`, `_WaveHeight`, `_WaveMaxLayers` … | 158-170 | | |

Keyword toggles (TPL:173-192; `shader_feature_local` list 261-277). Script code must set both:

| Property | Keyword | Note |
| --- | --- | --- |
| `_LightingOn` | `_UNLIT` | ToggleOff: 0 → keyword on |
| `_ReceiveShadows` | `_RECEIVE_SHADOWS_OFF` | ToggleOff |
| `_SpecularReflectionsOn` | `_SPECULARHIGHLIGHTS_OFF` | ToggleOff; off removes sun/point specular (FP:689) |
| `_EnvironmentReflectionsOn` | `_ENVIRONMENTREFLECTIONS_OFF` | ToggleOff |
| `_FlatShadingOn`, `_TranslucencyOn`, `_RefractionOn`, `_RiverModeOn`, `_CausticsOn` | `_FLAT_SHADING`, `_TRANSLUCENCY`, `_REFRACTION`, `_RIVER`, `_CAUSTICS` | Toggle |
| `_NormalMapOn`, `_IntersectionFoamOn`, `_DistanceNormalsOn`, `_DisableDepthTexture`, `_WavesOn` | `_NORMALMAP`, `_INTERSECTION_FOAM`, `_DISTANCE_NORMALS`, `_DISABLE_DEPTH_TEX`, `_WAVES` | Toggle |
| `_FoamOn`, `_FoamDistanceOn` | `_SURFACE_FOAM_SINGLE`, `_SURFACE_FOAM_DUAL` | set only by `MaterialUI.SetMaterialKeywords` (`Editor/Shader/MaterialUI.cs:667-682`) |
| `_ShadingMode` | `_ADVANCED_SHADING` | same |

Global keywords: `UNDERWATER_ENABLED`, `DYNAMIC_EFFECTS_ENABLED`, `WATER_HEIGHT_PASS` (`Runtime/ShaderParams.cs:45-55`). `#if UNDERWATER_ENABLED && _RIVER` → underwater off (TPL:283-286). Queue tag `Transparent+%render_queue_offset%` (TPL:217).

River-mode material (RiverModeler inspector checks, `Editor/RiverModeler/RiverModelerInspector.cs:202-226, 310-316`; `SettingsEditors.cs:255-262, 341-348`): `_RiverModeOn 1` + `_RIVER`, `_ZWrite 0`, `_RiverModeLegacy 0`, `_VertexColorFoam 1`, `_VertexColorTransparency 1`; ours also `_IntersectionSource 2`.

Vendor bases: `SW3/Materials/StylizedWater3_Smooth.mat` (queue default, `_Speed 1`, `_EdgeFade 10`, waves on); `SW3/Materials/RiverModeler/StylizedWater3_SplineRiver (Default).mat` (queue 3002, `_Speed 16`, `_EdgeFade 21.89`, `_IntersectionSource 0`, waves on); `StylizedWater3_Waterfall.mat` (legacy river, mesh UV, used by `SW3/Prefabs/Waterfalls/Waterfall_{5x20,10x20,20x30}.prefab`, not used here).

## 9. UnderwaterArea (`SW3/Runtime/Underwater/UnderwaterArea.cs`, namespace `StylizedWater3.UnderwaterRendering`)

`[ExecuteAlways] [SelectionBase]`; needs a trigger BoxCollider.

| Member | Line | Notes |
| --- | --- | --- |
| `Material waterMaterial` | 21 | null → ignored by `GetFirstIntersecting` (333). Its properties are copied to the underwater materials every frame (`RenderFeature.UnderwaterRendering.cs:54-60`) |
| `BoxCollider boxCollider` | 23 | |
| `enum WaterLevelSource { FixedValue, Transform, BoxColliderTop, Ocean }`, `waterLevelSource` | 25-33 | default FixedValue |
| `float waterLevel`, `Transform waterLevelTransform` | 35-37 | |
| `int renderingOrder [-5,5]`, `waterlineOffset 0.1`, `waterlineThickness 0.05`, `visualizeWaterLevel` | 40-46 | `SceneRenderer` queue = material queue + renderingOrder + 1 |
| `WATER_LEVEL_PADDING = 3` | 49 | |
| `static readonly List<UnderwaterArea> Instances` | 53 | global state |
| `UnderwaterResources underwaterResources` | 56 | `OnEnable` throws without it (180-212) |
| `AudioMixer environmentAudioMixer`, `lowPassCutoffFrequency` | 58-63 | leave empty (BroAudio) |
| `float CurrentWaterLevel` | 65-80 | |
| `class ShadingSettings` | 83-110 | `fogDensity 0.25 [0.01,3]`, `fogStartDistance 1`, `fogBrightness 1`, `colorAbsorption 0.01`, `heightFogDensity 4`, `heightFogStart 1`, `heightFogEnd 20`, `heightFogBrightness 0.5`, `causticsBrightness 1`, `translucencyStrength 0.5`, `translucencyExponent 2` |
| `ShadingSettings shadingSettings` | 112 | no initializer: guard with `??=` |
| `List<ParticleEffect> particleEffects` | 115-137 | scene particle systems clamped below the level |
| `void Reset()` | 139-178 | layer Water; adds/resizes box 100 × 50 × 100, centre y −22; nearest `WaterObject` → material + level; `FindShaders()` |
| `void FindShaders()` | 249-252 | `UnderwaterResources.Find()`: editor only, null in builds (`UnderwaterResources.cs:22-38`) |
| `static UnderwaterArea GetFirstIntersecting(Camera)` | 323-362 | camera near-plane corners in the box; nearest face wins on overlap |
| `bool CameraIntersectsFast(Camera)`, `CameraIntersects(Camera, out Vector3)` | 368-410 | |
| `bool CameraSubmerged(Camera)` | 412-415 | |
| `float CameraSubmersionAmount(Camera)` | 423-433 | 0..1 |
| `GetColliderPlaneHeight()`, `SetColliderPlaneHeight(float)`, `SetColliderPlaneDepth(float)` | 435-456 | |

## 10. Render feature (`SW3/Runtime/Rendering/StylizedWaterRenderFeature.cs` + partials) and our values

| Setting | Line | PC_Renderer (`Assets/_Game/Settings/PC_Renderer.asset:30-62`) |
| --- | --- | --- |
| `screenSpaceReflectionSettings.allow / reflectEverything` | 32-44 | 1 / 0 (materials have SSR off) |
| `allowDirectionalCaustics`, `enableDepthPrePass` | 49, 52 | 0, 0 |
| `heightPrePassSettings` (`HeightPrePass.Settings`: `enable`, `range 500`, `cellsPerUnit 4 [1,8]`, `maxResolution 4096`, `disableInSceneView`) | 54; `HeightPrePass.cs:31-43` | 1, 500, 4, 4096, 1 → 2000² `R16G16_SFloat` (`HeightPrePass.cs:133`) |
| `dynamicEffectsSettings` (`enabled`, `renderRange`, `texelsPerUnit`, `maxResolution`, …) | `RenderFeature.DynamicEffects.cs:18-` | enabled 1, 500, 8, 4096 (idle: no instances) |
| `underwaterRenderingSettings` (`enable`, `renderMethod { RenderPass, SceneRenderer }`, `ignoreSceneView`) | `RenderFeature.UnderwaterRendering.cs:20-36` | 1, RenderPass, 0 |
| `static bool RequireHeightPrePass`, `RequireDepthPrePass` | 82, 86 | |
| `WillExecuteHeightPrePass = RequireHeightPrePass ‖ settings.enable ‖ QueryCount > 0` | 83 | |
| camera skips: preview or `hideFlags != None` (except Scene view) / overlay | 222 / 228 | |
| height pre-pass in play: Scene view skipped, non-`MainCamera` skipped | 271-274 | |
| underwater passes: setup `BeforeRendering`, mask + shading `BeforeRenderingTransparents`, waterline `AfterRenderingTransparents` | `RenderFeature.UnderwaterRendering.cs:93-103` | |

Pipeline needs (Quickstart; `SW3/Editor/Installer.cs`): depth texture and opaque texture on (PC_RPAsset `m_RequireDepthTexture 1`, `m_RequireOpaqueTexture 1`, downsampling none), depth copy not "after transparents". Read-only checks: `PipelineUtilities.GetRenderFeature<StylizedWaterRenderFeature>()`, `RenderFeatureMissing<T>(out renderers)`, `IsDepthAfterTransparents()` (`SW3/Runtime/PipelineUtilities.cs`; WML `WaterRendering` 1957).

## 11. Global and static state (wrap in one service if gameplay needs it)

`WaterObject.Instances / PositionOffset / CustomTime`, `HeightQuerySystem.queries / QueryCount`, `StylizedWaterRenderFeature.RequireHeightPrePass / RequireDepthPrePass`, `UnderwaterArea.Instances`, `RiverModeler.onPreRebuildRiver / onPostRebuildRiver`, `RiverSegment.onTriggerEnter/Stay/Exit`, `RiverAudioManager.Instance` (+ static segment list, `DontDestroyOnLoad`, `RM/Audio/RiverAudioManager.cs:27-56, 308-317`), `OceanFollowBehaviour.Instance`, `PlanarReflectionRenderer.Instances / AllowReflections`. Domain reload stays on in this project (`EditorSettings.asset` enter-play-mode options 0), so these reset on play.

## 12. Editor helpers

- Menus: `GameObject/3D Object/Water/{Single Object, Grid, Ocean, River, Planar Reflections Renderer}`, `GameObject/Spline/River`, `GameObject/Audio/River Audio Manager` (don't), `Window/Stylized Water 3/Set up render feature`, `CONTEXT/SplineContainer/Add River` (modal), `CONTEXT/RiverModeler/Clear Width Data`.
- `RiverModelerEditor.GetDefaultMaterial()` (`Editor/RiverModeler/RiverModelerEditor.cs:36`) = vendor SplineRiver (Default): don't use it for scene rivers.
- `StylizedWaterEditor.DisableCullingForMaterial(Material)` (`Editor/StylizedWaterEditor.cs:398-403`): sets `_Cull` 0.
- Modal (stall the CLI): `StylizedWaterEditor.cs:356, 384`, `PipelineUtilities.cs:254`, `RiverModelerEditor.cs:84, 139`.

## 13. Not used here (facts for the decision)

- **MicroVerse section** (`RM/RiverModeler.MicroVerse.cs:38-124`, editor only): for each `microVersePathSettings[i].splinePath` that already exists, overwrites `trench 0`, `useTrenchCurve`, a 3-key curve `(0, bedDepth) → (max(0.02, bedSmoothness / baseWidth), −bankHeight) → (1, 0)`, `width = bankWidth + bedSmoothness`, `smoothness = 1 + bankFalloff`, splat width/smoothness, and per-spline `splineWidths = scale.x × (baseWidth/2 − 0.25) + offset`; then `OnMoved()`. Never creates the stamp. An entry with both modify flags off `return`s from the whole loop (56-59). All three WorldMap modelers serialize `splinePath: {fileID: 0}`.
- **Dynamic Effects**: `DynamicEffect` components and `ShorelineWaveSpawner` (`SW3/Runtime/DynamicEffects/`); passes skip while `DynamicEffect.InstanceCount <= 0` (`RenderFeature.DynamicEffects.cs:100`).
- **PlanarReflectionRenderer** (`SW3/Runtime/Rendering/PlanarReflectionRenderer.cs`): renders the scene again through `RenderPipeline.SubmitRenderRequest` (309).
- **River audio**: `RiverAudioManager` pools `AudioSource`s in `FixedUpdate` around the `AudioListener`; `Settings.audio`; `RiverAudioProfile`.
- **Underwater audio**: `environmentAudioMixer` drives the exposed `UnderwaterCutoffFrequency` parameter from `UpdateWithCamera` (`UnderwaterArea.cs:224-246`).
