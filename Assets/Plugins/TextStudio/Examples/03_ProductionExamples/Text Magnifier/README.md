# Text Magnifier

This production example turns the spatial influence workflow into a familiar text-inspection tool. The generated magnifying-glass texture draws only the physical lens and frame. Text Studio performs the visible magnification on the live TextMeshPro result beneath the lens.

## Try The Scene

The paper and lens are the complete visible example. Enter Play Mode and drag or touch the clear aperture; the lens stays clamped to the document. The saved scene starts at rest without a menu. The sample component retains `ResetMagnifier`, `ToggleAutoScan` and `SetScopedFallback` for scripted experiments; automatic travel respects reduced motion.

The saved home position already intersects the paragraph in Edit Mode, so the relationship remains inspectable without running the scene.

`TextStudioTextMagnifierExample` registers the asset before applying the effect wrapper. The scene therefore persists safe document markup, reloads without a missing-effect warning, and presents the exact magnified result immediately in Edit Mode.

## Copy The Pattern

1. Reuse `Assets/Effects/Text Magnifier.asset`, or add one `Lens Magnifier` layer to another Composed Effect. The sample uses a 123.5-unit radius, 2.1x center magnification, 1.75x edge magnification, 0.45 bulge, and a narrow 0.02 aperture feather.
2. Wrap the intended live text range in `<textMagnifier>...</textMagnifier>`.
3. Assign the lens-center Transform to `TextStudioText.InfluenceTarget`.
4. Move that Transform from pointer input, touch input, animation, gameplay state, or another system. The effect asset continues to own the glyph behavior.

The clear aperture is 247 units across, so the authored radius exactly matches the visible glass. The exact TMP route runs a terminal inverse radial resampling pass after the current 2D Appearance has been composed. This changes the apparent position, scale, bend, and rotation continuously across each glyph while preserving source order and spacing, so enlarged letters do not collide like independently scaled quads. Magnification already starts at 1.75x at the visible rim and rises smoothly toward the center.

Exact resampling is selected only for one full-range, persistent, unscoped lens layer on a compatible TMP output. Other renderers and unsupported combinations use the same built-in effect's renderer-neutral corner mapping. That fallback moves every glyph corner through the radial map, so it still produces useful expansion, bending, and apparent rotation without a sample-only mesh component, although only the Hero route can preserve continuous interior glyph detail and guarantee non-overlap at the raster level.

`Assets/Prefabs/TextMagnifier.prefab` keeps the transparent drag surface, shadow, and generated lens artwork together. `Assets/Art/InspectionPaper.png` is a text-free surface texture, so all readable content remains editable TMP text.

For isolated explanations of field shapes, coordinate spaces, inversion, and field stacks, open `../../02_Effects/Spatial Fields.unity`.
