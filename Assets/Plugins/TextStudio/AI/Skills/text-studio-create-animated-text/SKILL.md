---
name: "text-studio-create-animated-text"
description: "Use this skill whenever the user asks to create animated TextMeshPro text or preserve an existing Unity UI Text component with Text Studio, then apply markup, placement, and a complete reveal-to-hide lifecycle."
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
  last-verified: "2026-10-02"
---

# Create Animated Text

Create one supported text host, establish its source and lifecycle, then add only the effects needed by the message.

## When to use this skill

- Creating new animated world-space text.
- Creating animated TextMeshProUGUI text on a Canvas.
- Adding Text Studio to an existing UnityEngine.UI.Text without converting it to TMP.
- Configuring markup, placement, reveal, active, and hide behavior together.

## Prerequisites

- The intended World Space or UI Canvas target is known.
- Text content, ownership, and desired lifecycle are defined.

## Quick start

1. Create a new TMP host from GameObject/Text Studio/Animated Text/World Space or UI Canvas, or use Add Text Studio on an existing Unity UI Text inspector.
2. Set representative text on TextStudioText.
3. Choose reveal mode and typewriter behavior.
4. Add supported effect markup or default effects.
5. Preview and test replacement, hide, disable, and replay.

## Workflows

### Workflow: Create world-space animated text

1. Create the World Space menu item under the intended parent.
2. Position and size the TextMeshPro object for its viewing distance.
3. Set markup and reveal behavior on TextStudioText.
4. Verify camera-facing, bounds, and material behavior in the target pipeline.

### Workflow: Create Canvas animated text

1. Create the UI Canvas menu item under the intended Canvas or let the command create a valid Canvas.
2. Configure RectTransform constraints before tuning animation.
3. Set representative localized text and markup.
4. Test masking, scaling, and layout at target resolutions.

### Workflow: Preserve existing Unity UI Text

1. Select the existing UnityEngine.UI.Text component and choose Add Text Studio in its normal inspector.
2. Keep its font, sizing, alignment, color, RectTransform, masks, CanvasGroup, Outline, Shadow, and prefab references intact.
3. Confirm the Text Studio backend is before downstream Canvas mesh modifiers; use Fix Effect Order when needed.
4. Use reveal, placement, transforms, alpha, color, and compatible vertex effects. Do not select TMP material effects, Text Studio 2D Appearance, or UV Offset for this backend.
5. Use only legacy native rich tags `b`, `i`, `color`, and `size`, and test the final Canvas hierarchy.

### Workflow: Place text on a custom curve

1. Choose Custom Curve in the existing Placement control. Arch, Valley, S-Curve, and Flat are analytic shapes; Custom exposes Unity's keyframe field and Edit Curve in Scene.
2. Keep the shape Inline for one host, or Save as Profile to share it. Make Local copies a linked profile before independent editing. Height uses the host's local layout units.
3. Use the shared Selection control for All, None, a half-open range such as `0..5`, a tag, or an advanced selector tree. Each Match curves repeated phrases separately; Entire Selection uses one domain across all selected units.
4. Check a straight word next to the curved phrase, wrapped text, and the target Canvas or UI Toolkit layout. A linked profile edit affects every subscriber.

### Workflow: Choose persistent 2D Appearance

1. On a supporting backend such as TMP, open Appearance and choose Inline for local layers or Style for a linked reusable asset. Empty Inline preserves native appearance, while named appearance tags remain available.
2. Use Browse Styles to preview and apply built-in or registered custom styles, filtering by origin, category, favorites, or recent choices. Browsing does not change the target; Apply Style links the asset.
3. Use Make Inline to copy a linked style's full inherited stack and rendering settings. The copy stops following the style assets, while referenced textures stay shared. Repair any reported inheritance errors before conversion.
4. Switching Source retains inactive settings. Save as Style creates a reusable asset; Save & Register additionally includes it in the Custom catalog and global style tags. Verify Undo and prefab overrides when changing an existing object.
5. Keep Typewriter and Writing ownership separate. Plain UniText components retain native appearance; Folio 3.11+ explicitly converted owned-output components support qualified Appearance through the integration. Legacy Unity UI Text does not render this stack. Read the integration diagnostics before promising parity.

### Workflow: Author Layered Appearance

1. Read [Layered Appearance](references/layered-appearance.md) when the request involves shared paint, selected matches, connected backgrounds, patterned lines, precise outlines, skip ink, inline images, or color adjustments.
2. Use a shared style for repeated consumers and a Paint asset for independently reusable color or texture settings. Use Inline or Make Local for deliberate independent copies.
3. Prepare only the fonts, fallback glyphs and static images needed by the output. Match detail and effect reach independently to the actual display size.
4. Review the resulting text at its target size, through wrapping and reveal, and in the intended backend. Resolve readiness diagnostics before claiming an effect is rendered.

### Workflow: Style Named Passages

1. Give a reusable `TextStudio2DStyleAsset` a stable ID and register it in Text Studio Project Settings' appearance catalog, or define a top-level Tagged Group in the selected Appearance. Keep an asset reference linked unless the user asks for a local copy.
2. Invoke it with `<$warning>Low health</$warning>` or `<style ref="warning">Low health</style>`. Native TMP `<style="Title">` remains a TMP style-sheet tag. Text Studio does not discover arbitrary unregistered style assets.
3. Use the default Replace mode to replace the enclosing Appearance within the span, or `mode=overlay` to add the complete style. Closing restores the enclosing result. Use `tint`, `opacity`, and explicitly exposed parameters for per-invocation changes; do not guess a target layer.
4. Use the typed Appearance Tag selector in Apply Where to narrow an already active style. Check repeated matches, nesting, wrapping, native gaps, text replacement, Undo, and a missing ID. Full 3D styles remain owned by the 3D host; these tags do not introduce scoped 3D style assets.

## Verification

- Exactly one TextStudioText owns the animated text lifecycle.
- The text remains readable at target size and contrast.
- Reveal, active, hide, and replay states complete without stale glyphs.
- Long, empty, and changed text update correctly.
- The hierarchy and scene dirty state are preserved by editor creation.

## API quick reference

- TextStudioText is the central runtime component.
- TextStudioApi.ShowText assigns text and optionally starts reveal.
- TextStudioApi.ConfigureReveal configures RevealMode, speed, looping, and delay.
- TextStudioApi.ConfigurePlacement changes supported placement mode.
- TextStudioText.ConfigureCurvePlacement and TextStudioBinding.ConfigureCurvePlacement accept inline or linked curves, height, selection, and mapping.
- GameObject/Text Studio/Animated Text provides World Space and UI Canvas creation.
- TextStudioUnityUITextBackend is added by the existing Text inspector workflow and is not the default renderer for newly created text.

## Common issues

- **Markup is shown literally:** Check tag spelling, closing tags, and whether the effect is registered.
- **Text clips during animation:** Increase host bounds or choose effects that fit the intended layout envelope.
- **New text inherits stale state:** Use the supported ShowText or SetText path so compilation and lifecycle reset correctly.
- **Outline or Shadow ignores animated geometry:** Use Fix Effect Order so the Text Studio backend executes first.

## Boundaries

- Do not add TextStudioText to an unsupported renderer and assume it will adapt.
- Do not animate layout-critical properties without checking surrounding UI.
- Do not create a second clock for the same text.
- Do not silently replace authored fonts, materials, or text content.
- Prefer TMP for newly created GameObject text. Preserve legacy Text when an existing hierarchy or integration benefits from keeping that component.
