# Text Studio examples

Open `00_Entry/Text Studio Showcase.unity` for a short introduction, or choose a subject directly in the Project window. There is no required order.

## Find an example

- `01_Appearance` explores painting, outlines, layered materials, Writing and reusable styles.
- `02_Effects` explores reveals, motion, dialogue, captions, karaoke, spatial fields and game UI.
- `03_ProductionExamples` contains reusable patterns for combat feedback, live HUDs, subtitles, karaoke and text magnification.
- `04_CompleteDemo/Whisperwood` combines the tools in a small game flow, with Canvas and UI Toolkit versions.

The named lessons include UI Toolkit bindings, custom actions, custom effects and live input. The older numbered lessons are retired; the [coverage map](SampleCoverage.md) records their destinations and preserved assets. Optional integrations and Text Studio 3D keep their examples in their own packages.

## Try a scene

The working hierarchy and asset references are saved in each scene. Select a text object to inspect its settings, then follow its referenced styles, effects or timing assets. The examples use the same public components and APIs available in your own scenes.

Visual previews animate in Edit Mode where appropriate. Enter Play Mode for on-screen controls and game flows. Karaoke stays still and silent in Edit Mode; its word highlights follow the audio during playback.

## Input setup

Interactive samples require Unity's Input System package, version 1.4 or newer, enabled in Player settings. Their Canvas controls use the standard Input System UI module and Unity's UI actions. The base samples also register a sample input provider for wait-for-input markup while they are enabled.

This requirement belongs to the interactive examples. The Text Studio runtime can still use UI callbacks or a custom input provider without an Input System dependency.
