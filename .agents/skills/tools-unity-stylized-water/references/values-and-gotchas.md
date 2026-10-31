# Stylized Water 3: values that worked and source gotchas

Moved out of SKILL.md (same numbering) to keep it short.

## 5. Units and values that worked

| Property | Unit / formula | Ours | Source |
| --- | --- | --- | --- |
| `_EdgeFade` | **centimetres** of vertical depth: `alpha = saturate(depth / (_EdgeFade·0.01))` | 15 (generator sets swamp 20, asset has 15) | `ForwardPass.hlsl:825` |
| `_IntersectionLength` | band ≈ **ln(L) m**; L ≤ 1 kills the depth term | still 1.25, river 1.2 | FP:504 |
| `_IntersectionSource` | 0 depth, 1 vertex R, 2 depth + R | still 0, river 2 | FP:508-509 |
| `_IntersectionClipping` | sharp mode: everything above is solid white | still 0.45, river 0.6 (no bank curb) | TPL:81 |
| `_DepthVertical` ("View Depth") | `1 − exp(−viewDepth·v·0.1)` | still 4, pond 5.7, swamp 12, river 3 | FP:152 |
| `_DepthHorizontal` ("Vertical Height Depth") | `1 − exp(−verticalDepth·v)` | 0.35 / 0.5 / 2.5 / 0.3 | FP:153 |
| `_Speed` | multiplies all shader time | 1 still; river 6 (vendor river 16) | `Common.hlsl:16` |
| `_Cull` ("Render faces") | 0 Both, 1 Back, 2 Front (= CullMode value) | still/pond/swamp 0, underside 1, river 2 | TPL:17 |
| `_VertexColorTransparency` | `a = a·saturate(a − G)`: G −1 untouched, 0 squares, 1 hides | 1 on all except waterfall | FP:1012 |
| RiverModeler `baseWidth`, `widthGradientFalloff` | metres (full width; fade in from each edge) | river 30 / 2, streams 12 / 1 | `RiverGenerationJob.cs:249-251, 306` |
| `start/endGradientFalloff` | metres ÷ segment count | river 8/13 m, streams 5/5 m | §6 G5 |
| `cascadeAngleThreshold/Falloff` | degrees: weight 0 at `2T − F`, 1 at `2T` | 8/8 (8°→16°) | `RiverGenerationJob.Utilities.cs:21-29` |
| `foam.velocityStrength` | ×1000 = particle velocity | 0.45 | `RiverGenerationJob.cs:206` |
| `UnderwaterArea.shadingSettings.fogDensity` | 0.01–3 | 0.25, swamp 1.2 | `UnderwaterArea.cs:86` |

## 6. Gotchas (3.3.3 source)

- **G1 Empty spline throws.** A spline < 0.01 m or < 2 knots returns before `rivers.Add`; the next valid spline then indexes `rivers[i]` out of range (`RiverModeler.cs:161, 193-198`). A new `SplineContainer` holds one empty spline: assign `Splines =`.
- **G2 Container last.** After `SetSplineContainer`, every `Spline.Changed` and `SplineAdded` rebuilds (`RiverModeler.Splines.cs:44-81`). `OnDisable` only unsubscribes `Spline.Changed` (`:66-69`): a deleted modeler stays subscribed to `SplineAdded/Removed`. Delete container and modeler together.
- **G3 RIVER_SCALE on every spline, in metres.** `GetScaleData` forces `PathIndexUnit.Distance` without converting (`:83-95`); `SetupConnection` reads a linked spline's data with no null check (`RiverModeler.cs:435-436`).
- **G4 Don't link knots between ribbons.** A linked end loses its tip fade (`RiverGenerationJob.cs:211-212`) and gets a half-plane fade with no distance limit (distance check commented out, `Utilities.cs:41`) over the whole first/last *segment* (`RiverGenerationJob.cs:353-361`); cut 0.4 × radius toward the joining side, fade 0.15 × radius, radius = baseWidth × scale.x (`RiverModeler.cs:437-470`). A stream's whole first segment went invisible. Overlap and fade instead.
- **G5 Tip fade × segment count.** Fade uses spline-wide t × segment length (`RiverGenerationJob.cs:303-305`) and every spline has ≥ 2 segments (`RiverModeler.cs:177-185`). Settings are shared by all splines of a modeler, so equal fades need equal segment counts (streams: `maxSegmentLength` 1000 → 2 each). First and last rows are always transparent (`:365-366`).
- **G6 sortingOrder 0 after the first build**: `SetProperties` runs before `PrepareContainers` creates segments (`RiverModeler.cs:206, 212`). Re-apply after `Rebuild()`.
- **G7 16-bit indices per segment** (`RiverGenerationJob.cs:78, 678`): keep `(maxSegmentLength / max(minEdgeDistance, 0.5) + 2) × (ceil(baseWidth / widthEdgeDistance) + 1)` < 65,536 vertices (rows are sampled every 0.5 m, `:27`); width columns are capped at 100 (`:137-139`).
- **G8 Static flags** of the modeler GameObject are copied to segments at rebuild (`RiverModeler.cs:307-319`). Ribbons in a prefab need `rebuildOnStart` (meshes live in the scene).
- **G9 CPU height queries need `_WAVES`** (see 4.5). `HasMissingReferences` also requires a `WaveProfile` (`HeightQuerySystem.cs:145-148`).
- **G10 UnderwaterArea**: `Reset()` resizes the box on `AddComponent` (`UnderwaterArea.cs:139-178`); null `waterMaterial` → skipped (`:333`); overlapping boxes: the one whose nearest face is closest to the camera wins, so a nested pool box beats the lake box (`:323-361`, `UnderwaterUtilities.GetClosestPointOnSurface` :156); the enum is `FixedValue, Transform, BoxColliderTop, Ocean`, not the height-query enum.
- **G11 Underwater fog doesn't cover transparents.** The shading pass runs `BeforeRenderingTransparents` (`RenderFeature.UnderwaterRendering.cs:100`; the tooltip says "after"), so the waterfall sheet and particles stay unfogged. `SceneRenderer` mode with `renderingOrder` might fix it (unverified).
- **G12 Vertex colours on flat sheets.** With `_VertexColorTransparency 1`, write G = −1 as float `Color` (G = 0 squares alpha, sharpening the edge fade; a mesh without a colour stream: unverified).
- **G13 Mesh asset refresh**: refill through the Mesh API (`Clear`, `Set*`, `UploadMeshData`, WML `SaveMesh`). `EditorUtility.CopySerialized` left the old GPU buffers on screen.
- **G14 Inspector "fix" buttons set floats, not keywords** (`RiverModelerInspector.cs:207` sets `_RiverModeOn` without `_RIVER`). Check `shaderKeywords` after any inspector fix.
- **G15 `cascadeAngleThreshold`** carries both `[Min(0.1)]` and `[Range(0,1)]` (`Settings.cs:89-91`) though it is in degrees: set it from code; dragging the inspector slider may clamp it to 1° (unverified which drawer wins).
- **G16 The RiverModeler inspector's material dropdown** resolves folder GUID `d2a358901c7ad80418593cbcc0b06713` (`RiverModelerInspector.cs:101-104`), which matches no `.meta` in this project (the `Materials/RiverModeler` folder is `99fe3fb1…`), so it lists nothing. Assign our copy by code or drag.
