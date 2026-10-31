# Stylized Water 3: re-verify on an upgrade

Moved out of SKILL.md (same numbering) to keep it short.

## 8. Re-verify on an SW3 upgrade

1. `package.json` version, `CHANGELOG.md`, the extension changelogs and `Extension.*.cs` versions; update the version in PLUGINS.md.
2. `grep -n "public " Runtime/RiverModeler/*.cs Runtime/Underwater/UnderwaterArea.cs Runtime/Components/WaterObject.cs`; diff against api.md.
3. RiverModeler: segment count (`RiverModeler.cs:177-185`), SetProperties/PrepareContainers order (`:206, 212`), VFX sortingOrder (`:357`), invalid-spline early return (`:161`), Splines unsubscribe (`.Splines.cs:66-69`), `Reset()` audio, `Settings` defaults, MicroVerse section (`.MicroVerse.cs:56-59` returns from the whole loop).
4. Job math: tip fade (`RiverGenerationJob.cs:211-212, 303-305`), forced end rows (`:365-366`), `FadeIntersection` distance check (`Utilities.cs:41`), slope mask (`:21-29`), ushort indices (`:78, 678`), `FindNearestPointOnRiver` width (`RiverSegment.cs:255`).
5. Shader: `#if UNDERWATER_ENABLED && _RIVER` (TPL:283-286), `_Cull` enum (TPL:17), keyword list (TPL:261-277), `_EdgeFade` (FP:825), intersection (FP:504-509), VC transparency (FP:1012), `TIME` (`Common.hlsl:16`).
6. CPU waves early return (`Gerstner.cs:43-46`); camera skips (`StylizedWaterRenderFeature.cs:222, 228, 274`); underwater pass events (`RenderFeature.UnderwaterRendering.cs:93-103`); `UnderwaterArea.Reset`/`GetFirstIntersecting`.
7. Vendor materials the copies come from: `StylizedWater3_Smooth.mat`, `RiverModeler/StylizedWater3_SplineRiver (Default).mat` (queue 3002, `_Speed` 16). Re-run `BuildWaterMaterials` through `MaterialCopy` and diff the `_Game` copies: a renamed property is silently ignored by `SetFloat`.
8. Shader importer: `autoIntegration: 1` in `StylizedWater3_Standard.watershader3.meta` and COZY's fog include is installed, so a reimport can switch the compiled fog integration. Check the material inspector's "Active fog integration" line (unverified what it currently compiles to).
9. Capture each water kind (flat, ribbon, junction, waterfall, from below) before and after.
