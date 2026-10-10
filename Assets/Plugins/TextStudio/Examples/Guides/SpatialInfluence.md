# Text Studio Spatial Influence Sample

Open `../02_Effects/Spatial Fields.unity`. Before entering Play Mode, the saved hierarchy shows four separate `TextStudioText` objects and one `Shared target`. Each row names the Composed Effect asset that drives it, so the complete setup can be inspected without tracing a controller script.

Enter Play Mode to move the shared target across all four rows. Drag the + target to take manual control, or focus it with the keyboard. Reset restores its authored position. Automatic travel remains available on the sample component and respects reduced motion. The optional `TextStudioSpatialTargetInteraction` component only moves the target . It does not implement, select, or modify any text effect.

The four asset-authored rows demonstrate that spatial influence is a reusable mask for effect layers, not a separate renderer feature:

- `Scale Focus` applies one Target Distance field to scale and color layers. Nearby glyphs grow and take on the focus color.
- `Motion Field` applies the same type of falloff to wave and rotation layers, so motion follows the target instead of affecting the whole line.
- `Inverse Spotlight` inverts Target Distance. Glyphs near the target remain readable while those outside the radius are dimmed.
- `Combined Fields` multiplies a Placed Text Target Distance field by Noise in Source Text Normalized space. This creates a textured local response and shows that a layer can stack fields from different coordinate spaces.

Select any row and inspect `Placement > Shared Spatial Target`. Every row references the same saved Transform directly. Text Studio observes its runtime position and reevaluates the affected glyphs.

Select the field recipes in `../02_Effects/Fields` or their reusable source assets in `../Shared/Spatial Influence` to inspect their field stacks. Spatial fields can scope any channel or built-in effect layer. Available shapes include Linear, Sphere, Box, Cylinder, Torus, Target Distance, and Noise. Coordinate spaces include Source Layout, Source Text Normalized, Placed Text, and World. Multiple fields can combine with Multiply, Add, Maximum, or Minimum.

To copy a recipe into your own scene:

1. Duplicate the relevant Composed Effect asset.
2. Use its inspector to add it to the Text Studio Effect Catalog.
3. Assign a Transform under `Placement > Shared Spatial Target` on your `TextStudioText` component.
4. Wrap the desired content with the asset's tag, which its inspector can copy for you.

The tutorial root registers the four packaged assets locally so the example can run without changing your project's Effect Catalog. It does not choose or implement their behavior. No sample controller is required after you add your duplicated asset to your own catalog. Copy the interaction helper only when you also want this particular draggable, automatically moving demonstration target.

Use the same asset pattern when text should react to a player, cursor, world object, spotlight, interaction point, damage source, or other moving Transform. Use a fixed field center when the influence belongs to the text layout itself.
