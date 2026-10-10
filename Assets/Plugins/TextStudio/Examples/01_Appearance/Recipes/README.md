# Appearance and writing recipes

The focused lessons show their comparisons immediately. These prefabs preserve the deeper native configurations for inspection and reuse without a recipe selector in the lesson UI.

| Prefab | Native configurations |
| --- | --- |
| Paint mapping recipes | Shared paints, per-letter and range mapping, palettes, tint, transforms, spread and local copies |
| Connected surface recipes | Hard breaks, word matches, bevels, material responses, opacity, masks, painter order and blending |
| Outline and join recipes | Width, alignment, miter, bevel and round joins, nested contours, dash caps and phase |
| Bevel and surface recipes | Face, edge and connected-surface relief, light direction and material responses |
| Shared style and fallback recipes | Shared styles at different sizes, scale, clipping, real font fallback and reduced motion |
| Appearance ownership recipes | Native text color, linked styles, local settings and complete design assignment |
| Writing direction and timing recipes | Ink reveal, units, layout, directional coverage, hide and reversed erasure |
| Authored stroke and pressure recipes | Prepared phrase paths, authored pressure, stroke ordering, brush styles, width and live text input |

These are complete authored example rigs, including their camera and responsive canvases. Open a prefab to inspect it, or place one in an empty scene. The text hosts and referenced styles, paints, writing sources and designs remain editable through their native Inspectors. The small lesson components retain the original configuration methods for use from a script; their source documents those operations. Shared assets are not mutated by Play Mode session settings.

For a single configuration, reuse its referenced asset or copy its native inline settings onto your own text host. A full recipe rig is unnecessary when you only need one style or prepared writing phrase.

## Surface Materials

The `Surface Materials` folder contains native comparison groups for an existing Canvas. Drag a prefab onto the Canvas; its root stretches to the available area. Each text host stores its own inline appearance settings, without a controller rewriting them on enable.

- Halftone, Fine grain, Weathered, Carbon and Frost each show textured and clean lettering together.
- Mapping compares whole text, glyph, each matched range and the entire selection using repeated words.
- Texture fit compares Stretch, Contain, Cover and Tile using the same source; the tile specimen uses two repeats to make repetition visible.
- Tint and offset and Pattern scale retain texture-paint variants. Printed tint and offset and Printed pattern scale demonstrate the corresponding procedural graphic settings.

The lesson shows Halftone, Weathered and Carbon directly. Inspect their linked A06 styles or the referenced paint assets to locate the source texture. To experiment independently, copy a specimen or apply its inline settings to your own text. Revert the prefab instance to restore its saved configuration.

## Inline Images

The `Inline Images` comparison groups also stretch inside an existing Canvas. Nine groups combine Preserve original, Tint or Silhouette fill with Text only, Text and image or Image only effects. Each group shows no effects, soft shadow, glow and both effects together, preserving all thirty-six native configurations without selectors. Each text host retains its own inline layers and the same live TMP crystal sprite.

`Image preparation` compares assigned saved appearance data with native runtime preparation. Runtime preparation may reuse a field already in the shared cache; it does not replace the original color texture. `Contour limits` requests exact contour dashes beside a soft silhouette. The image has scalar silhouette data rather than font vector contours, so native capability diagnostics explain which contour treatment cannot be supplied. Inspect the text host, its inline layers and the assigned Crystal Appearance asset for these settings and diagnostics.

The main lesson keeps the reward and three image treatments visible. The twelve A13 shared styles remain available independently of the prefabs. Revert a prefab instance to restore its authored configuration.

## Selective Recolouring

Each named adjustment in `Selective Recolouring` shows four native configurations together: Shadows only, Each layer, Finished image and Masked half. The first two adjust source contributions before blending; the latter two adjust the composed image, with the optional mask restricted to the first half of the word. The gold overline stays outside the adjusted group or image region. A linear transform can look identical when applied to every source layer or to the finished image; the native ownership differs, and composite adjustment also supports the illustrated spatial mask.

The seven adjustment groups retain hue shift, low saturation, exposure, cool tint, grayscale, warm tint and original colors. `Strength` shows 0%, 50% and 100% together. All values, groups and masks are native inline settings that remain editable in the text Inspector. The five A12 shared styles also remain available.

## Animated Surfaces

Each of the twelve named treatment groups shows the saved finish with animation removed, then 55% and 100% animation strength. The middle specimen stores inline native settings; the full-strength and clean specimens reference the existing style assets. Their GUIDs are preserved.

Use these prefabs inside an existing Canvas with a dark background, as in Animated Surfaces. The small shared playback component starts only the native Active segment and pauses it on disable. It never replaces markup, styles or inline settings. Inspect a text host to pause, scrub or adjust its native appearance; revert the prefab instance to restore the saved configuration. Echo Spectrum and Velocity Smear use native history, so their appearance develops over successive frames. Reduced Motion and No Motion continue to use the product's global policy.

## Text Decorations

Five pattern groups compare underline, strikethrough and overline. The other native groups cover skip ink, thickness metrics, mixed font runs, wrapped-line phase, text layouts, long-label diagnostics, thickness, pattern spacing and phase, output-pixel versus em sizing at different zooms, and live writing beside its completed result. All sixteen groups stretch inside an existing Canvas and retain the prepared Kalam font.

The main lesson demonstrates a patterned quest label and gaps around descenders. No controller replaces its native markup or settings. Adjust a text host in its Inspector to combine the retained options; the live-writing group uses only the shared native playback sequencer. The Long label group deliberately retains the native contour-budget diagnostic, which is visible in the text Inspector. It does not substitute a different treatment when preparation reaches its limit.

## Styles and Designs

The Appearance ownership rig shows native text color, a linked shared style and two independent inline colors together. Edit the native appearance source in the Inspector to link a style or copy its settings; enabling the object does not reset those choices. The `Styles and Designs/Design playback` prefab stretches inside an existing Canvas and shows the same bundled Editorial Ink design revealing beside its completed appearance. Only the shared native playback sequencer runs these specimens.

## Shared Styles

The shared-style rig uses one linked appearance at title, button and status sizes, beside a fallback accent. Twelve Canvas-ready groups retain the thirty-two saved combinations of recipe, light/dark contrast, em/pixel keyline units and full/simple rendering. Font fallback compares primary letters with a combining accent resolved by the real TMP fallback font. Clipping compares the same text with and without a native RectMask2D; Scale stores 65%, 100% and 130% transforms. Motion shows Full, Reduced and None policies on the same native wave effect. Inspect each text host and its capability diagnostics directly. The four complete design assets remain reusable, and prefab reversion restores the authored configuration.

Simple keyline styles use native Automatic execution so the compiler selects the supported path for em or pixel widths. Full compositions request Hero rendering. A forced rendering path that cannot preserve the authored layer produces a native compiler diagnostic; it is not a usable sample configuration.

## Paint Mapping

Eight paint groups show Whole text, Each letter, Each match and Entire selection together for both palettes and all four paint types. The two range specimens select both BONUS occurrences: Each match restarts the gradient, while Entire selection spans the two matches and their gap. Solid paint deliberately remains uniform across mapping choices. The main rig shows the linear gold comparison immediately.

Tint, Transform and Spread preserve native tint, rotation, scale, offset and Clamp/Repeat/Mirror settings. Shared and local stores a linked paint beside an independent local copy. Edit the shared Paint asset to see its users update; the local copy retains its own colors. No sample controller clones or resets the authored paints, and prefab reversion restores the saved settings.

## Connected Surfaces

The main rig shows soft wrapping, separate hard breaks, connected hard breaks and word matches together. Seven Canvas-ready groups preserve wrapping, selection, flat/beveled/metal surfaces, normal/multiply/screen overlays, a face between layers, group opacity, masking, painter order and three light directions. All twelve original surface styles and the two overlap styles remain reusable assets. Lighting specimens store native inline settings; enabling them preserves those settings. Inspect the text box, layer order, selector and surface treatments directly.

## Outlines

The `Outlines` folder contains seven native Canvas comparison groups: Width, Joins, Alignment, Dash caps, Dash phase, Scale and Font preparation. The twelve original A03 styles remain shared assets. Joins uses the same Audiowide letter for all three corner treatments; Scale holds the style fixed at title and game sizes. Font preparation pairs assigned contour data with a host lacking that data, whose native capability report explains the unavailable exact contour treatment. Inspect that host for the diagnostic. No selector or preparation toggle is needed.

## Bevels

The `Bevels` folder keeps five material groups, each comparing outline, face and connected-surface treatment. Outline finishes adds titanium beside matte and gold. Lighting shows three fixed light directions; Relief shows flat, standard and strong settings together. Edge close-up pairs a whole letter with a native clipped enlargement. All sixteen existing styles remain available, while lighting and relief use native inline copies retaining their layer and mask identities. No runtime controller rewrites these settings.

## Writing

The main writing rig shows start, halfway and complete states beside native write-and-erase playback. The `Writing` folder groups five lessons by recipe, then by logical, horizontal or vertical sequence direction. Every prefab compares one line, two lines and a combining accent using native typewriter settings and a saved halfway preview. This retains all seventy-five combinations without selectors. Word cascade and cluster lettering use native reveal motion; ink reveal, line wipe and reverse erase retain the fifteen saved writing styles. The small sample playback component stores the intended snapshot position and restores it with native scrubbing when the group is enabled. Inspect a host to play, pause or scrub its native preview.

## Strokes

The main stroke rig plays Signature, Marker, Chalk and Stroke to fill together. The `Strokes` folder groups six brushes by authored, even or bold pressure and original or reversed order. Each group compares 50%, 100% and 150% brush widths using saved native styles at the same reveal position. The six original styles and six prepared phrase assets remain available; comparison styles live in `Writing/Stroke comparisons`.

`Phrase input` is a focused typing lesson. Luma matches the authored paths; Mira uses the native fallback. Formatting that preserves the shaped phrase still matches. The path graphic shows authored knots only when the phrase matches. Its small controller connects input to native playback and displays readiness; it does not substitute rendering or clone temporary styles.

## Layered Lettering

The main scene compares offset shadow, inner light, flat depth and stepped depth. View edge turns all four specimens together to reveal their flat output; View face restores them. Five reusable groups retain Shadows, Light, Depth, Packaged styles and title/game Scale comparisons. The eight original A04 styles and four bundled presets remain shared native assets, with no recipe selector or temporary style cloning.
