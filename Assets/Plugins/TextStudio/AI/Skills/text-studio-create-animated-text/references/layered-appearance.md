# Layered Appearance

Use one persistent Appearance stack on a supported TMP host. Index zero is visually topmost. Typewriter controls visibility, Writing controls stroke reveal, and Appearance controls the persistent painted result.

## Paint And Selection

Use a `TextStudio2DPaintAsset` when multiple styles should follow the same paint. `TextStudio2DPaint.Reference(asset)` links to it; local overrides independently control mapping, tint, transform, spread, image fit and distance. `MakeLocal()` makes an independent copy. A Paint asset contains a concrete paint, without chains of other Paint assets.

Range mapping can restart for Each Match or span the Entire Selection. Repeated tag occurrences remain separate matches after wrapping. Select semantic units with `TextStudioApplicationSelector`; do not treat UTF-16 offsets as shaped glyph or cluster indices. A selection closes over the backend's published cluster. Match provenance and visual line fragments determine connected backgrounds and line phase.

Use `TextStudio2DDecorationSpec.WithTreatments(...)` to paint one background or line shape. A treatment stack can combine fill, stroke, shadow, glow and material response without adding another copy of the shape. Connected Range joins wrapped fragments of the same logical match. Connecting across explicit hard line breaks is a separate option.

## Color And Compositing

Color Adjustment modifies selected lower painted contributions before they blend. It supports saturation, hue, exposure, brightness, contrast, tint, grayscale, sepia, invert and an explicit matrix. It preserves the coverage of the underlying shape even when paint alpha changes. Exposure is in stops; color operations use linear light.

Composite Adjustment modifies the already composed lower image in an isolated compositor. Use it for spatially partial image changes or an adjustment that must include the combined shadows, glows, overlaps and neighborhood effects. An effect's later tint is not retroactively changed by a source Color Adjustment below it.

A shared surface belongs to a match. Whole Surface applies a selected source adjustment to that whole surface if any member is selected. Exclude Partial requires all members. Use Composite Adjustment for a spatially clipped recolor; do not simulate it by choosing one representative glyph.

Groups can order their children By Layer or By Character. A shared surface draws once before or after the characters according to its explicit surface order. Masks and nested groups retain their own scopes. Review overlapping letters when changing order because this deliberately changes the image.

## Lines, Outlines And Native Ink

`TextStudio2DDecorationLineSpec` describes Solid, Double, Dotted, Dashed and Wavy underline, strikethrough and overline. Keep line rhythm separate from its paint coordinates. Choose font metrics, automatic metrics or custom thickness/offset; choose em or output pixels for custom units. Match phase continues across wrapping, while Line phase restarts on each visual line. Follow Font Runs is an explicit choice when fallback fonts or sizes should change the line's metrics.

Skip Ink Off leaves the pattern intact. Auto uses source curves when available and a native glyph metric approximation otherwise. Precise requires the prepared source face and removes native ink intersections while keeping the same dash/wave phase. Read the readiness message when curves or a contour budget are unavailable. Native weight, italic shear, planar glyph rotation, reveal and directional Writing affect visible ink. Depth deformation of individual glyphs is a separate unqualified case; do not promise it from a planar test.

Round, bevel and bounded miter outlines follow the canonical glyph curves. Contour dashes run along the outline and its counters. Increasing miter limit changes acute-corner extent. Choose output-pixel units for a constant screen width or em for a font-relative width. Preview narrow strokes, holes, tiny sizes and the largest intended scale.

## Prepared Sources And Memory

Prepare font Appearance data from the original supported font file and the exact glyph IDs used by the native font. Assign the resulting `TextStudioFontAppearanceData` through `SetAppearanceFont`. Prepare fallback fonts independently. Existing gallery fonts carry companions for their shipped sample coverage; a different phrase may require more glyphs.

Detail and reach are independent. Higher pixels per em sharpen magnified fields. A wider signed-distance interval gives large outlines and soft effects enough reach. Adding a high-detail tier does not make a narrow tier suitable for a wide glow. Preparation accepts bounded tier requests, runs outside the main thread where supported, reports progress and supports cancellation. Missing source files, unsupported variation/CFF2 data, missing glyph coverage and budget rejection must remain explicit.

For static inline images, prepare `TextStudioColorAppearanceData` from the actual saved image geometry and assign it through `AddAppearanceImages`. Original color is preserved by default; Tint and Silhouette Fill are explicit choices. Image silhouettes use a scalar alpha field with their exact UVs and triangles, including holes and atlas rotation. Mutable sources use bounded runtime generation. Do not turn missing readback/generation support into a claim of zero cost or completed preparation.

Repeated consumers reuse immutable programs and shared sources. Do not compile or recreate assets every frame. Texture paints share a live Texture object and resource ID; identical names or import hashes do not establish shared lifetime. After GPU-side changes to a source texture, increment its Unity update counter so dependent paints and silhouettes observe the update.

The application-wide `TextStudioAppearanceMemory` budget admits owned resources and reports explicit payloads, pending work, reuse and borrowed textures. A budget diagnostic is not permission to silently remove an authored effect. The ledger's current scope is documented separately from total process memory; do not advertise the qualification envelopes as device recommendations.

## Preparation And Customer Examples

Use Runtime Preparation in the inspector or Tools/Text Studio/Runtime Preparation. Editing and player builds update known runtime data automatically after the project has made its font-embedding choice. Prepare now finishes actionable work; the shared Overview groups required, optional and ready items and explains manual steps. Source contours are prepared without textures when the appearance does not need field tiers. Expert font scope, inventory, density, distance reach and static-image controls are under Advanced settings in the same window. Include Font Inventory covers glyphs already in the native asset, not every glyph in the original font. Declare unknown runtime text explicitly and inspect any coverage or memory errors before building.

The shipped Live Game HUD, Subtitles and Accessibility, and Whisperwood Canvas scenes demonstrate these workflows with saved base-package assets. Try the HUD replacement controls, the caption language/width/text-size/reduced-motion controls, and Whisperwood's Credits screen. Relay assets share paints and prepared icons. Whisperwood's static title font and prepared fields cover capital titles; prepare additional glyphs when changing that coverage. Copy a style or paint before making a deliberately independent variation.

## Typed Authoring Example

This configures an existing editable style and a reusable paint. The caller owns those assets; changing either is intended to update their linked consumers.

```csharp
using TextStudio;
using UnityEngine;

public static class CaptionAppearance
{
    public static void Configure(TextStudioText text, TextStudio2DStyleAsset style, TextStudio2DPaintAsset paint)
    {
        paint.SetPaint(TextStudio2DPaint.Solid(new Color(0.15f, 0.8f, 1f)));
        TextStudio2DDecorationLineSpec line = TextStudio2DDecorationLineSpec.Default
            .WithPattern(TextStudio2DLinePattern.Dashed, 0.24f, 0.12f)
            .WithSkipInk(TextStudio2DSkipInk.Auto);
        style.SetLayers(new[]
        {
            TextStudio2DLayer.CreateFill("Face", TextStudio2DPaint.Solid(Color.white)),
            TextStudio2DLayer.CreateDecoration("Rule", TextStudio2DPaint.Reference(paint),
                new TextStudio2DDecorationSpec(TextStudio2DDecorationKind.Underline, 0.04f, -0.08f).WithLine(line))
        });
        text.SetAppearanceStyle(style);
    }
}
```

## Verify The Result

- Review actual rendered text in Edit Mode Preview, Play Mode and the intended export path. Raw authoring mode intentionally shows native text.
- Check short, wrapped, fallback-font and repeated-match content. Inspect the overlap order, line gaps, glyph counters and shared background boundary.
- Verify paint edits update all linked consumers while Inline and Make Local copies remain independent. Test Undo, reload and prefab overrides for persistent authoring changes.
- Test reveal, Writing, text replacement, disable and destruction without stale geometry or resources.
- Keep backend ownership explicit. Plain UniText components retain native output and preserve inactive Appearance settings. Folio 3.11+ explicitly converted `TextStudioUniTextCanvas` and `TextStudioUniTextWorld` components can render supported Appearance, with native fallback and a stated reason for unsupported combinations. Legacy Unity UI Text does not support this stack.
