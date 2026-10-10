# Explore Text Studio

Choose a scene by name in the Project window. Its text, markup, inline recipes and shared assets are saved in the scene. Inspect the visible specimens before pressing Play; comparisons appear together without a recipe-selection menu.

## Appearance

Open `01_Appearance` to explore paint, layered lettering, materials, outlines, decorations and inline images. Inspect each text object's Appearance, Design or Writing settings to see what produces its result. A specimen may own an inline recipe or reference a reusable asset; shared references remain shared when the scene is opened.

**Paints and Layers**, **Outlines and Bevels**, **Writing and Erasing** and **Styles and Designs** each show four authored comparisons. Their [recipe prefabs](01_Appearance/Recipes/README.md) preserve the additional mapping, surface, writing and ownership configurations. Additional paints, styles, Writing phrases and complete designs remain available in the neighboring asset folders and the bundled presets. Apply these to a specimen in the Inspector to explore beyond the curated comparisons. Use normal editor Undo to return from an authoring experiment.

## Effects and interaction

Open `02_Effects` for reveals, motion, dialogue, captions, karaoke, spatial fields and game UI. **Reveal Timing** explores arrival and disappearance. **Composed Effects** shows Linear, Bounce and Step curves together, followed by separate Once, Loop, finite Ping-pong and Weighted playback specimens. The native preview repeats the comparison after eight seconds; Once holds its result while Loop continues. All one hundred curve, playback and channel combinations remain reusable assets. **Spatial Fields** shows four native field results around one draggable target. Use the target directly or focus it with the keyboard; Reset restores its position. The separate Text Magnifier production scene demonstrates optical resampling. **The Gate and the Lantern** presents a timed pause, one input pause and a marker-driven consequence in a short dialogue.

Visual previews use native Text Studio authoring playback. Enter Play Mode for meaningful interaction, dialogue and sound. Karaoke displays three styles together, all driven by one audio clock; its four transport actions play or pause, restart and seek. All six style assets remain reusable. Edit Mode stays silent; explicitly seeking with the sample component previews native word highlights without starting audio.

The reusable `02_Effects/Recipes/HUD signal inputs.prefab` keeps the HUD's value, sampled audio and authored beat examples together for Inspector experiments without adding another scene menu. Its three native signal drivers reference the same saved effect; the audio source starts silent.

## Sources and extensions

**Renderer Comparison** displays TMP, legacy UI and three UI Toolkit source routes together. Its one input changes the authored specimens. See [renderer notes](02_Effects/Renderers/README.md) for source ownership and compatibility limitations.

The guides in `Guides` explain [custom actions](Guides/CustomActions.md), [custom effects](Guides/CustomEffects.md), [audio profiles](Guides/AudioProfiles.md) and [spatial influence](Guides/SpatialInfluence.md). Their source code and assets are included alongside the scenes. The [coverage inventory](SampleCoverage.md) records the destinations and remaining verification requirements for older demonstrations.

See [Start Here](00_StartHere.md) for sample input requirements. For complete game patterns, open `03_ProductionExamples` or `04_CompleteDemo/Whisperwood`.

Prepared HUD icons are visible in Responsive HUD and remain attached through score, objective and prompt changes. Accessible Captions uses the shared Relay Dialogue style for inline `<em>` ranges. The six English, French and German cue recipes remain in `03_ProductionExamples/Shared/Captions/Assets/RelayRidgeSubtitleCues.asset`; use them to inspect localized wrapping with the same caption style and accessibility settings.
