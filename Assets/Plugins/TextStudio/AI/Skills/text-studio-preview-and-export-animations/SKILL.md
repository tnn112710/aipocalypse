---
name: "text-studio-preview-and-export-animations"
description: "Use this skill whenever the user asks to preview a Text Studio effect, inspect lifecycle phases, export a supported animation to MP4 or GIF, or understand platform and transparency limits."
metadata:
  asset: "Text Studio - Text Animation for Unity | TextMeshPro & UI Toolkit"
  publisher: "Impossible Robert"
  asset-version: "1.3.1"
  skill-version: "1.0.0"
  unity: "2023.2"
  render-pipelines: "Built-in, URP, HDRP, custom SRP"
  category: "Tools/GUI"
  asset-store-url: "https://assetstore.unity.com/packages/tools/gui/text-studio-text-animation-for-unity-textmeshpro-ui-toolkit-394000"
  documentation-url: "https://www.wetzold.com/tools/text-studio/docs/"
  support-url: "https://discord.com/invite/uzeHzEMM4B"
  last-verified: "2026-08-29"
---

# Preview and Export Animations

Preview the same lifecycle that will be exported, then choose the supported format by platform and transparency needs.

## When to use this skill

- Previewing Code Effect or Composed Effect assets.
- Exporting a single supported Text Studio preview to MP4 or GIF.
- Diagnosing differences between inspector preview, export, and runtime presentation.

## Prerequisites

- A Code Effect or Composed Effect asset has a valid live preview.
- Lifecycle segments, speed, renderer, and authoring space are configured.
- The destination platform supports the requested format.

## Quick start

1. Select the effect asset and verify its live inspector preview.
2. Choose the lifecycle segments, playback speed, and representative sample text.
3. Choose MP4 for opaque 960 x 540 at 60 fps on Windows or macOS, or GIF for looping 960 x 540 at 50 fps on Windows, macOS, or Linux.
4. For Composed Effect, keep the renderer implied by Authoring Space.
5. Export, play the result externally, and compare phase timing and final state.

## Workflows

### Workflow: Export a preview

1. Confirm the inspector preview shows the intended lifecycle.
2. Choose MP4 or GIF according to platform and alpha requirements.
3. Select an explicit output destination.
4. Wait for the export to finish and verify dimensions, frame rate, duration, looping, and background.
5. Retain the source effect asset unchanged.

### Workflow: Diagnose preview and export differences

1. Compare lifecycle segments, playback speed, reveal blend, solo layer, and active-duration multiplier.
2. Remember that export is semantically equivalent, not pixel-identical to the UI Toolkit thumbnail.
3. Check whether Flat Text or an installed add-on renderer owns the output.
4. Repeat the same immutable snapshot to confirm deterministic rendering.

### Workflow: Seek an Installed Dynamics Preview

1. Use the component inspector's Seek control in Edit Mode and observe progress. A later request replaces the pending target; Cancel Seek stops further replay work.
2. For scripted tools, `RequestPreviewSeek` reports acceptance, not completion. Inspect `IsPreviewSeeking` or the base incremental preview provider contract before capturing or exporting the requested state.
3. Checkpoint storage grows lazily within a conservative 32 MiB reservation and at most 32 slots. The 4 ms work budget is checked between solver steps, so one expensive step can exceed it. Prefer baked playback when fast random access is required.

## Verification

- MP4 is offered only on supported Windows or macOS editors and contains no alpha.
- GIF loops infinitely and uses binary transparency with stable spatial coverage.
- Output is 960 x 540 at the documented frame rate.
- The exported lifecycle ends and restarts correctly.
- Temporary preview scenes, textures, and encoders are disposed after completion or cancellation.

## API quick reference

- TextStudioPreview drives inspector playback with Play, Pause, Restart, and Hide.
- Code Effect and Composed Effect inspectors export an immutable lifecycle snapshot.
- TextStudioPreviewExportCoordinator prevents concurrent media encoders.
- MP4 uses UnityEditor.Media on Windows and macOS at 60 fps.
- Managed GIF export runs at 50 fps with an infinite loop extension and binary transparency.

## Common issues

- **MP4 is unavailable on Linux:** Use GIF; UnityEditor.Media references are intentionally excluded there.
- **GIF fades look stippled:** GIF has one transparent palette index, so spatial coverage approximates partial alpha.
- **Export differs from the thumbnail:** Compare lifecycle semantics and renderer coordinates rather than expecting pixel identity.

## Boundaries

- Do not advertise MP4 export as royalty-free.
- Do not promise true partial alpha in GIF.
- Do not bundle FFmpeg, gifski, or libimagequant as an inferred dependency.
- Do not start concurrent exports or treat stale output as evidence of a successful run.
