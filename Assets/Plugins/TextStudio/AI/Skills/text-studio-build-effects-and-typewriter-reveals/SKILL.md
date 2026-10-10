---
name: "text-studio-build-effects-and-typewriter-reveals"
description: "Use this skill whenever the user asks to add Text Studio effect markup, combine reveal, active, and hide effects, configure typewriter timing, or create reusable effect and reveal behavior."
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

# Build Effects and Typewriter Reveals

Separate lifecycle phases, choose effects by semantic purpose, and make reveal timing independently testable.

## When to use this skill

- Adding or combining effect markup.
- Configuring character or word typewriter timing.
- Designing reveal, active, and hide phase behavior.

For finite event responses such as particles, audio cues, signals, or outgoing glyph motion, use `text-studio-author-reactions`. An always-on tag remains a Default Effect or text markup.

## Prerequisites

- The base animated text renders correctly without the new effect.
- The desired lifecycle phases and reduced-motion expectation are explicit.

## Quick start

1. Open Tools/Text Studio/Text Gallery to inspect supported tags and lifecycles.
2. Apply the narrowest effect markup to representative text.
3. Choose character, word, instant, or another supported RevealMode.
4. Configure typewriter timing or a preset.
5. Preview reveal, active, hide, interruption, and replay.

## Workflows

### Workflow: Compose lifecycle effects

1. Assign effects to Reveal, Active, or Hide according to when they should run.
2. Keep nested markup balanced and verify interactions on the same glyph range.
3. Use a Composed Effect when a reusable layered effect asset is appropriate.
4. Test the final pose of each phase before tuning duration.

### Workflow: Select a phrase or range

1. Use `range=0..5` on an effect tag to select its first five logical text units; the end is exclusive and the origin is the enclosing tag, so whitespace counts and markup does not.
2. Use the Composed Effect layer's Text Selection control for All, None, a range, a tag, or a scoped advanced selector. Its spatial and source-color controls still provide weighted influence within the selected units.
3. For repeated tagged phrases, use Each Match where a placement or paint mapping offers it. Use `^0` for the end of the active phrase and fix malformed range diagnostics rather than treating them as All.

### Workflow: Configure typewriter behavior

1. Choose the reveal unit and supported timing source.
2. Set speed, punctuation pauses, or custom timing through the intended preset or inline settings.
3. Test a short phrase, punctuation-heavy copy, multiline copy, and instant reveal.
4. Verify skip, pause, resume, and restart behavior.

### Workflow: Add Writing to Typewriter

1. Choose a Typewriter recipe or Inline settings, then enable Writing and assign a Writing Style. Behavior Profiles that own Typewriter also own these settings.
2. Choose Letters for shaped clusters, or Word/Line grouping. Sequence Direction orders units across text; Drawing Direction belongs to the style and acts inside each glyph.
3. Use Finish Current Unit for sequential handwriting or Cadence for overlap. All at Once draws together; Instant and skip-to-settled produce finished letters.
4. Enable Erase on Hide when coverage should reverse. Verify interrupted drawing does not flash a full letter.
5. Drive the canonical Typewriter timeline for scripting, Timeline, and preview. Do not create Writing clocks or use removed SetWritingProgress/SetWritingStyle APIs. Scoped Composed Writing retains selectors, influence, style, and a local curve without independent playback or timing.

## Verification

- Every opening effect tag has the intended scope and close.
- Reveal progress and effect time do not fight each other.
- Punctuation and word boundaries behave as intended.
- Hide behavior completes and repeated playback starts from a clean state.
- A reduced-motion or immediate route remains usable where required.

## API quick reference

- RevealMode selects the supported reveal strategy.
- TextStudioApi.ConfigureReveal sets mode, speed, loop, and loop delay.
- TextStudioTypewriterPreset stores reusable timing.
- CharacterTimings and WordTimings support their respective timing assets.
- TextStudioApi.ConfigureEffectFamilies enables Reveal, Active, and Hide families independently.

## Common issues

- **Effects restart unexpectedly:** Check whether text recompilation, ShowText, or RestartEffects is being called repeatedly.
- **Reveal pauses feel wrong:** Verify punctuation and timing preset settings with representative copy.
- **Nested effects produce surprising results:** Reduce to one tag, validate each effect, then restore nesting in a defined order.

## Boundaries

- Do not invent markup tags not present in the installed catalog.
- Do not drive reveal progress from a separate unsynchronized coroutine.
- Do not use intense motion without an immediate or reduced-motion alternative when the product requires it.
- Do not freeze incidental private effect implementation details into user code.
