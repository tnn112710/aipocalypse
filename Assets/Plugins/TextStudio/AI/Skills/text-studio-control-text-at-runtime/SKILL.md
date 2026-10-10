---
name: "text-studio-control-text-at-runtime"
description: "Use this skill whenever the user asks to set, reveal, hide, pause, resume, restart, format, or inspect Text Studio text from C# with TextStudioApi or TextStudioText."
metadata:
  asset: "Text Studio - Text Animation for Unity | TextMeshPro & UI Toolkit"
  publisher: "Impossible Robert"
  asset-version: "1.2.0"
  skill-version: "1.0.0"
  unity: "2023.2"
  render-pipelines: "Built-in, URP, HDRP, custom SRP"
  category: "Tools/GUI"
  asset-store-url: "https://assetstore.unity.com/packages/tools/gui/text-studio-text-animation-for-unity-textmeshpro-ui-toolkit-394000"
  documentation-url: "https://www.wetzold.com/tools/text-studio/docs/"
  support-url: "https://discord.com/invite/uzeHzEMM4B"
  last-verified: "2026-08-29"
---

# Control Text at Runtime

For finite responses to text lifecycle events or named signals, load `text-studio-author-reactions`; keep text control, Typewriter progression, and Reactions in their separate owners.

Use the public convenience API for common operations and preserve one owner for text content and lifecycle commands.

## When to use this skill

- Showing or replacing animated text at runtime.
- Skipping, pausing, resuming, hiding, or restarting lifecycle behavior.
- Formatting numbers or controlling glyph and word visibility.

## Prerequisites

- The target TextStudioText exists and owns the intended text source.
- Command ordering and cancellation behavior are defined.

## Quick start

1. Hold a direct TextStudioText reference instead of searching each frame.
2. Use TextStudioApi.ShowText or SetTextInstant for replacement.
3. Configure reveal and effect families through the public API.
4. Use visibility overrides only for the requested glyph or word behavior.
5. Test repeated commands, interruption, disable, and scene reload.

## Workflows

### Workflow: Show dialogue or status text

1. Call TextStudioApi.ShowText with explicit autoReveal behavior.
2. Use ConfigureReveal once when settings change, not every frame.
3. Respond to lifecycle completion through supported events or state.
4. Define what a new request does to an active reveal.

### Workflow: Control visibility and effects

1. Use SetGlyphVisibility, SetWordVisibility, or SetTextVisibility for temporary overrides.
2. Clear overrides when normal reveal ownership should resume.
3. Use RestartEffects without replacing text when only effect time should restart.
4. Call RefreshEffectBindings only after runtime effect registration changes.

### Workflow: Assign persistent 2D Appearance

1. For a reusable style, call `text.SetAppearanceStyle(asset)`; this selects Style, including when the assigned reference is null. `AppearanceStyle` exposes only the active linked asset, while `CompiledAppearanceStyle` covers every source.
2. For local appearance, copy `text.InlineAppearanceSettings` or start with `TextStudio2DStyleSettings.Default`, edit the value with `SetLayers`, `SetInheritance`, or `SetRenderingOptions`, then call `text.ApplyAppearanceSettings(settings)`. It defensively copies the data and selects Inline. Reapply only when settings change.
3. Use `text.ApplyAppearanceSettings(TextStudio2DStyleSettings.Empty)` to clear the default appearance and return to native styling. Switch `AppearanceSource` between Inline and Style to retain local settings and the linked asset independently. `SetAppearanceStyle(asset)` assigns either built-in or custom reusable assets by reference; it never searches for an asset at runtime.
4. `asset.Settings` copies local authored settings, including a live BaseStyle reference. Use the inspector's Make Inline when an independent flattened copy of an inherited preset is required. Textures remain shared references in all snapshots.
5. Check the intended backend: unsupported backends preserve the settings but do not execute Text Studio 2D Appearance. Writing and lifecycle effects remain separate.

## Verification

- For a formatted counter, choose `TextChangeScope = TextStudioTextChangeScope.NumericValue` and set `NumericDecimalSeparator` to the formatter's decimal separator. Equal integer and fractional digits retain their decimal positions. Multiple-number text uses the ordinary diff fallback. Opt into position interpolation with `TextChangeLayoutDuration`, for example 0.18 seconds; rapid updates continue from the current positions. `ShowTextInstant` and No Motion settle immediately.
- Reuse a `TextStudioCapabilityReport` and call `CollectCapabilities` after changing backends, effects, or add-ons. Inspect output readiness and actionable diagnostics; unknown capability metadata is not a claim of unsupported rendering.
- Use `TextStudioSignalDriver` for normalized values, audio RMS, or beat phase driving an Effect or Composed Effect during Play Mode. Share DSP start time for synchronized beat signals. Pure `TextStudioSignal` math can feed the external-effect facet of other hosts.

- Explicit `TextStudioText.ShowText` and `TextStudioBinding.ShowText` restart reveal when the supplied text is unchanged. Use `SetText` for idempotent content updates.
- Semantic glyph indices and counts identify complete Unicode 17 text units, including grapheme clusters and atomic inline objects. They are not UTF-16 character offsets or renderer mesh-glyph indices. Use `Plan.GetTextUnit`, `GetUnitText`, or `GetPrimaryCodePoint` as appropriate.
- Set `WordBoundaryProvider` only when language-specific word tailoring is required; the default uses Unicode word boundaries. Preserve complete text units when implementing selectors or integration mappings.

- Runtime code holds or receives the correct TextStudioText reference.
- Repeated ShowText calls have deterministic interruption behavior.
- Pause, resume, skip, hide, and restart return to valid states.
- Visibility overrides are cleared when no longer needed.
- Runtime code contains no UnityEditor dependency or per-frame scene search.
- Inline appearance edits leave source assets unchanged; source switching restores local settings and native appearance without restarting text ownership.

## API quick reference

- TextStudioApi.ShowText sets text and optionally reveals it.
- SetTextInstant shows text without typewriter animation.
- ConfigureReveal and ConfigureEffectFamilies adjust supported behavior.
- SetGlyphVisibility, SetWordVisibility, SetTextVisibility, and clear methods manage overrides.
- GetRevealSnapshot returns a debug snapshot; RestartEffects and RefreshEffectBindings handle focused resets.

## Common issues

- **Text restarts every frame:** Move ShowText out of Update and call it only when content changes.
- **A hidden glyph never returns:** Clear its visibility override so reveal ownership resumes.
- **Effects do not reflect a new registration:** Call RefreshEffectBindings after the registration boundary, not continuously.

## Boundaries

- Do not poke internal reveal or evaluator state.
- Do not find TextStudioText every frame.
- Do not combine multiple systems that each set the same text.
- Do not use visibility overrides as a replacement for normal reveal progression.
