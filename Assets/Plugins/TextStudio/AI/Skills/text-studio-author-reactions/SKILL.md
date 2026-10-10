---
name: "text-studio-author-reactions"
description: "Use this skill whenever the user asks for a finite response to text reveal, hide, replacement, a marker, or a named signal, such as particles, audio cues, gameplay feedback, or outgoing glyph motion."
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
  last-verified: "2026-09-25"
---

# Author Finite Text Reactions

Use Reactions when a text lifecycle event should start a bounded response. Keep effect appearance, Typewriter progression, and event responses in their respective owners.

## When to use this skill

- Spawn a particle or pooled visual when a unit appears, disappears, or changes.
- Play a named audio cue, send a signal, or respond to a text marker.
- Move or release an outgoing glyph after text replacement.
- Share the same finite response across TextMeshPro, UI Toolkit, or an optional supported backend.

## Prerequisites

- Choose the text host and confirm that its renderer supports the intended output.
- Decide which event starts the response and whether it targets the full text, one event unit, or changed units.
- For audio, provide an Audio Set route; for scene or panel output, provide the required target binding.

## Quick start

1. Open the host's Reactions section. Choose a Built-In recipe for a ready-made response, Custom for a shared `TextStudioReactionAsset`, or Inline for a host-local response. Choose None when the host should have no reactions.
2. For a Custom recipe, create a Text Studio/Reactions/Reaction Set asset. Add a layer and choose its When event, Which target, and Action. A layer is an event-to-response rule, not a visual effect layer.
3. Start with one layer and a finite duration. Set overlap, cooldown, chance, and the recipe's active-response capacity only when the event rate requires them.
4. Test reveal, interruption, text replacement, hide, replay, and disable in Play Mode. Verify the response ends and releases its output.

## Workflows

### Workflow: Reuse an effect without duplicating its layers

1. Author the visual in a Code Effect or Composed Effect asset when it needs reusable effect controls.
2. Point a Reaction's Play Effect action at that shared effect and choose the lifecycle trigger and target.
3. Keep Typewriter reveal/hide effect references and Default Effects tag strings in their existing sections. They are not Reaction layers.

### Workflow: Connect gameplay and audio

1. Use Marker or Signal with a matching name for explicit gameplay events; call `PlayReaction(signal, intensity, unitIndex)` from C# for a named signal.
2. Use Send Signal for a finite callback or Play Audio Cue for a named route. Audio Set and Audio Player own clips and voice pooling.
3. Keep one automatic audio trigger route active. Public lifecycle observers can still listen without creating a Reaction.
4. Cancel or stop active responses when the owning gameplay flow requires it; normal host disable, detach, recipe replacement, and text replacement already have cleanup paths.

### Workflow: Share a response across hosts

1. Use a Custom Reaction asset for a reusable live definition. Use a Behavior Profile's Reactions domain when several hosts should receive the same recipe as part of a wider behavior setup.
2. Keep Default Effects and Typewriter domains independently selectable in the profile. A Typewriter preset owns timing and Reveal/Hide effect choices, not Reactions.
3. On UI Toolkit, assign the recipe to `TextStudioBinding`; a Label does not need a GameObject reaction driver. Use the optional UniText or 3D integration only for its supported renderer-specific capabilities.
4. Verify each selected host with its real output and resource limits. A recipe can be shared even when a particular output needs a different action asset or target binding.

## Verification

- The intended event fires once per semantic unit or text change, and the layer targets the correct generation of text.
- Active responses respect capacity and overlap policy, and replay, disable, detach, or recipe replacement leaves no pooled output behind.
- Optional audio is silent when no cue action is enabled; enabled cues use the intended Audio Set route without double playback.
- A no-Reactions host remains on the normal text path.
- Edit Mode does not dirty a scene or leave temporary GameObjects. Only supported changed-outgoing-unit motion has deterministic Edit Mode reaction preview; run other reactions in Play Mode.

## API quick reference

- `TextStudioText.Reactions` and `TextStudioBinding.Reactions` assign the same recipe model.
- `TextStudioReactionRecipe.None`, `.BuiltIn(id)`, and `.Custom(asset)` select common sources; Inline layers are host-local.
- `PlayReaction`, `StopReactions`, and `CancelReactions` control named and active responses.
- `OnReactionSignal` and `OnReactionAudioCue` expose finite outputs to application code.

## Common issues

- **Nothing appears:** Confirm the trigger, target, action resource, and host capability. An unset required target binding cannot spawn into a panel or scene.
- **The same sound plays twice:** Check whether the Audio Player still handles Typewriter events while a Reaction cue also starts for the same event.
- **Old output survives a text change:** Check the response's overlap and cancellation policy, then verify host disable and recipe replacement cleanup in Play Mode.

## Boundaries

- Do not build a second visual layer composer inside Reactions. Use Composed Effects for visual composition.
- Do not place scene-object references in a shared Reaction asset; bind scene or panel targets on the host.
- Do not promise Edit Mode playback for audio, particles, arbitrary custom actions, or physics. Use Play Mode for those outputs.
- Do not add a reaction just to apply an always-on `<wave>` tag; use Default Effects or text markup.
