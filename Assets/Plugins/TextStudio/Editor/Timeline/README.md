# Composed Effects In Timeline

Select a saved Composed Effect in the Project window and choose **Assets > Create > Text Studio > Animation > Timeline From Composed Effect**. Save the Timeline, assign it to a Playable Director, and bind its Text Effects track to your Text Studio text component.

Select the effect clip in Timeline. **Open Composition** edits the shared animation asset, so every clip referencing it sees the changes. **Create Variant** saves an independent editable copy and assigns it to this clip. Its timing, ease curves, intensity curve and glyph range stay intact. You can use the variant in other clips or add it to the effect catalog for tag-based use.

The Composed Effect controls layered animation. Timeline controls when the clip plays, which glyphs it affects, and how it blends with other clips. Clip timing is not baked into the composition when creating a variant.
