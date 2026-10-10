# Appearance Compositions

These assets style the saved Live Game HUD, Subtitles and Accessibility, and Whisperwood Canvas examples. Open those scenes to see the editable text, controls, and assigned Appearance assets.

## Edit the Looks

- Select Objective, Score, or Interaction Prompt in Live Game HUD. Relay HUD preserves the native colors of inline icons and adds a shared signal glow. Replace Prompt and Queue Objective retain the named icons while changing the text.
- Select Subtitle in Subtitles and Accessibility. Relay Dialogue adds a connected background and dashed underline to `<em>` ranges. Next Caption cycles English, French, and German; Text Size, Narrow Layout, and Reduced Motion exercise the same live text and style.
- Select Title in Whisperwood Canvas. Whisperwood Title combines gold paint, contour accents, outline, depth, and shadow. Appearance evaluates the top layer in front. The prepared title font contains capital Latin letters; prepare another font or extend its prepared glyph inventory for other alphabets.

Relay Signal Paint is shared by HUD glow and caption emphasis. Editing it changes both looks. Duplicate the paint and assign the copy to edit one independently. Duplicate an Appearance asset for a reusable variation, or use the component's Inline source to author a local look.

## Prepared Sources

Relay Icons Sprite contains Health, Energy, Relay, and Credits. Its native atlas keeps original color; Relay Icons Appearance supplies static silhouette fields, so these examples do not generate image fields while running. The texture is non-readable after import. If the artwork changes, prepare the image companion again before shipping.

Whisperwood Title SDF is a static native font with a companion containing 96 and 160 pixels-per-em tiers. Automatic field quality selects available detail for the displayed size. These resources belong to the base package and require no optional backend, integration, or Debug tooling at runtime.
