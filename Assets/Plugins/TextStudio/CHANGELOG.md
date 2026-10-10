# Changelog

## [1.2.0] - 2026-10-05

- Redesigned Runtime Preparation as a project overview: a status card with prepared counts, one Prepare action for open scenes and build content, the build-automation toggle and an inline font-outline license choice. Details explain each kind of prepared data with its benefit and runtime fallback and link every text to its prepared data; Prepared data and Settings tabs hold the asset inventory and expert controls. Characters shown at runtime adds digit, Latin, punctuation and other character sets plus phrases to 2D and 3D preparation for text that changes while playing.
- Added named Appearance styles in markup with nested Replace and Overlay composition, scoped selection and registration of custom styles.
- Added shared paints with gradients, textures, repeating patterns and procedural noise, practical mapping and spread controls, contextual gradient recipes and independent color and finished-image adjustments.
- Added layered fills, contour strokes, shadows, glows, faux depth, bevels and material response, plus editorial and animated title effects with more consistent opacity, lighting and texture sampling.
- Added underline, overline, strikethrough, backgrounds, borders, connected range surfaces and glyph-derived backplates, with reusable treatments, pattern continuity, skip-ink options and Typewriter-aware backing visibility.
- Added richer inline-image styling with original-color, tint and silhouette-fill choices, prepared silhouettes and predictable sizing, selection and reveal behavior.
- Added an Edit Selection shortcut beside Range mapping on paint, graphic and animated layers, making their selection easier to adjust while preserving existing selectors.
- Added finite Reactions for glyph, marker, signal and completion events, with deterministic variation, chance, cooldown, delay, named input gates, dependencies and bounded effect, audio and prefab responses. Prefab responses can optionally scale and offset with their text host, and optional physics responses can follow impact, break and return events.
- Added the `ReadsInfluenceTarget` effect capability, declared by Lens Magnifier, so the Shared Spatial Target inspector, its Scene label and Browse Target Effects recognize effects that read the target directly; the Text Magnifier sample no longer warns that no tag uses its Target.
- Added retained outgoing-letter Reactions across qualified TextMesh Pro, UI Toolkit and UniText outputs, preserving their presented appearance, clipping and pose while new text arrives. Unsupported capture routes report their limitations.
- Added reusable Typing Sparks, Completion Flash, Counter Outgoing and Outgoing Shards recipes and gameplay Behavior Profiles, with host-appropriate choices, warnings for incompatible saved recipes, preview controls, dependency editing and independent inline copies.
- Added Squiggle and Lens Magnifier effects, and improved Composed Effects, custom effects and renderer extensions with consistent timing, parameter handling, capability guidance, preview-only Solo and motion-accessibility behavior. Motion assignment is consolidated into source tags, profile-aware Default Effects and Typewriter Reveal/Hide controls.
- Added editable Custom Curve placement and shared curve assets, Scene knot editing and scoped selection, and expanded spatial wave, orientation, deformation and field controls.
- Adopted Unicode 17 grapheme and word segmentation, improved sprite and combining-character indexing, and fixed supplementary-character scrambling and substitute-glyph shape and padding.
- Simplified Advanced appearance conditions with expandable inline editors and clear add/remove controls, preserving nested selections, overlapping ranges and Undo.
- Fixed graphic and animated Appearance layers falling back to plain white with Word or Line mapping. Supported mappings now use consistent coordinates and preserve saved choices and Undo, while postprocessing shows only the mappings it supports.
- Rebuilt the Text Animator migration wizard as a guided, additive overview: a verdict with already-works, one-click and needs-you counts; step cards that explain what changes and run per step or per item (add Text Studio to Text Animator texts, sounds and events, typing speed, playbacks, style aliases); instruction cards for custom effects, scripts that call the Text Animator API, typed listeners and markup; live done states, a searchable findings list and a removal checklist. Project checks now skip unrelated prefabs, scenes and assemblies (about 23 s down to about 2 s in a large project) and leave Text Animator samples out by default, and repeated imports or listener copies update instead of duplicating.
- Corrected Appearance preset mapping so stencil graphics and face color ramps follow multiline and curved lettering, and depth ramps blend from front to back.
- Expanded the editable Appearance, Writing and complete Design library with layered title treatments, material finishes, animated surfaces and reusable recipes.
- Simplified Appearance authoring around Inline and Style, with separate Catalog and Custom choices, clear catalog style labels, a shared style browser, search, favorites, recent choices and reliable copying, saving and Undo. An empty inline stack preserves the text's native appearance.
- Unified Writing with Typewriter timing and shaped-letter progress, adding font-derived paths, pressure and stroke controls, reusable phrases, custom colors and shared 2D and 3D presentation settings. Writing distinguishes prepared handwriting, outline traces and automatic ink paths.
- Expanded Typewriter presets and reusable custom timing and Reveal/Hide treatments, with live inspector previews and mode-specific settings that preserve authored values.
- Improved text replacement and append continuity, whole-text dialogue defaults, changed-glyph and numeric transitions, reveal completion, action waits, pause ownership and playback recovery.
- Reduced idle evaluation, text-change work, selector cost, allocations and redundant uploads, improving long text, many-label scenes, animated Appearance and shader preparation. Layered styles now share storage and avoid extra composition when their effects can render directly.
- Expanded Appearance rendering on Metal, mobile Vulkan, OpenGL ES and WebGL through compatible texture-backed data, with clearer capability diagnostics and readable native fallback when styling is unavailable.
- Improved Canvas transforms and clipping, world-text visibility, material restoration, source mesh updates and layered output cleanup, fixing duplicated output, missing text, tint errors and edge artifacts.
- Added optional grouped world-text batching with sorting-aware eligibility and rendering statistics that explain the active route and its limitations.
- Added unified runtime preparation for fonts, images and geometry, with readiness guidance, saved-data inspection, project automation, configurable embedding and build checks. Improved prepared-font matching, fallback coverage and resource budgets. Install matching Text Studio and Text Studio 3D updates together when the add-on is used.
- Expanded optional UniText support for shaped text and compatible Folio Appearance output while preserving native layout, masks, sorting, interaction and document windows, with clearer version guidance and explicit fallback for unsupported glyph and decoration types. Added document-reader, interactive-journal and native spoiler-tag workflows.
- Expanded legacy Unity UI Text and UI Toolkit support, preserving editable input and password masking, authored UXML playback, presentation pauses and native output behavior.
- Improved optional-integration discovery, import prerequisites, local content-update verification and installation without customer-managed scripting defines.
- Improved native dialogue workflows and examples for Adventure Creator, Game Creator, Ink, Naninovel, Pixel Crushers, Yarn, PlayMaker and Visual Scripting, including choice, skip, reveal, cancellation and handoff behavior.
- Improved Timeline pause and seek ownership, Splines placement, Localization updates, LED Studio text fitting and capture, and optional Input System, UIEffect and PrimeTween workflows and examples.
- Improved bounded audio playback, glyph and named-marker cues, caption accessibility and motion preferences, with silent automatic Edit Mode previews and clearer runtime interaction boundaries. Marker routing can bind to nearby text automatically.
- Reorganized samples into directly accessible numbered tracks and native editable comparisons, with clearer controls, responsive layouts, readable stages and persisted recipe assets.
- Improved source editing, tag insertion and highlighting, literal and escaped markup parsing, contextual inspector controls, gallery navigation and previews, animated export timing, menu organization and lifecycle cleanup. New text created through the menu starts in Live preview, and live group-status updates preserve pointer interactions while editing inspector controls.
- Expanded runtime control with explicit append-and-reveal, per-glyph reveal age, presentation pauses, configurable spatial fields and reliable per-glyph visibility on plain text.
- Expanded the manual and shipped AI guidance for styling, Writing, Reactions, runtime control, integrations, preparation, Unicode and preview export.
- Removed hard Input System dependencies from base example assemblies; Whisperwood retains its keyboard controls through an automatically enabled optional backend.
- Fixed the UI Toolkit source example to resolve its built-in Typewriter preset without an asset URI import warning.

## [1.1.0] - 2026-08-26

- Added Unity 6.7 lifecycle ownership for runtime registries, authoring caches, editor services, integrations, and examples while retaining deterministic reset behavior on older supported Unity versions.
- Expanded effect authoring with progress-driven Surface Sweep and Shimmer highlights across ranges, lines, words, and other effect areas; new sweep angle, width, brightness, cadence, and reverse controls; deterministic Shake and Decrypt seeds; Chromatic Glitch seed and slice activity; and minimum Outline & Glow strength.
- Added renderer-independent scripting APIs and complete reference documentation for simulation extensions, including registration, glyph geometry and state, physical signals, and Edit Mode preview support.
- Added a production Text Magnifier example with polished lens and paper artwork, drag and auto-scan controls, live Edit Mode preview, strong magnification, and subtle glyph curvature over live TextMeshPro text.
- Redesigned Combat Feedback with balanced HUD cards, dedicated feedback lanes, camera-space compositing that keeps world damage text visible, and clearer controls and pool status.
- Made optional integrations activate when imported without customer-managed scripting defines. Game Creator 2 Dialogue is now a separate add-on alongside the core Game Creator 2 integration.
- Rebuilt the Integrations window with clearable status and category filters, alphabetical cards, accurate import availability, clearer separation of companions, adapters, and samples, and expanded setup and troubleshooting guidance for each integration.
- Updated the optional UniText integration for UniText 3.3 with current animation guidance, improved Unicode and decoration handling, and more efficient reuse of 3D font outlines. UniText `3.3.0-pre.7` or newer is required.
- Improved Preview Content editing with theme-aware tag highlighting, reliable synchronization, accurate wrapping and alignment, and automatic scrolling for longer text.
- Improved inline tag authoring with active-token completion, schema-backed parameter values, clearer markup delimiters, and edits that preserve Undo, focus, and existing content.
- Improved gallery onboarding so object-producing recipes start in Live preview and lead users from Drop-In Text into the numbered Learn path.
- Fixed Combat Feedback target reactions after entering Play Mode and prevented released emitters from flashing when their disappearance completes.
- Fixed `+ Tag` so range tags wrap and preview the complete selection.
- Fixed generated Gallery examples so animated, Wave, and Grid recipes start configured and visible, and fixed the Custom Effects lesson after a clean import.
- Fixed inline tag help so incomplete tags stop at the caret and the first parameter click remains active through focus changes.

## [1.0.2] - 2026-08-07

- Bugfixes

## [1.0.1] - 2026-08-05

### Initial release

- Animate TextMeshPro, UI Toolkit, and supported third-party text through one shared workflow.
- Create complete reveal, active, and hide sequences with reusable effects, typewriter presets, timings, playback, audio, and per-glyph motion.
- Combine effects through readable inline markup or reusable Composed Effects, with sequencing, spatial influence, color targeting, and renderer-aware 2D or 3D authoring.
- Build custom programmable Code Effects with typed defaults, aliases, ranges, and isolated runtime state, then author their lifecycle, playback, curves, timing, and combinations through the same Composed Effect workflow as built-ins.
- Edit every published Existing Effect parameter immediately without enable checkboxes, use sliders or draggable numeric labels, and keep compositions stable through explicit parameter snapshots and reset-to-current-default actions.
- Drive existing effects from Animator, Timeline, tween systems, gameplay, or editor scrubbing with one or more External Effect Drivers.
- Animate newly typed, pasted, inserted, or replaced player input without restarting text that was already present, using the included preset, connected input-field prefab, and guided lesson.
- Place text along lines, arcs, circles, waves, grids, spirals, rings, and Unity Splines while preserving multiline layout and offering flexible orientation controls.
- Build dialogue, subtitles, karaoke, long-form text, pages, scrolling, waits, input, markers, events, and external text-source experiences.
- Emit polished temporary and gameplay text with reusable presentation presets, pooling, number formatting, stacking, aggregation, target following, screen projection, and lifecycle events.
- Preview effects live from multiple views, compare renderers, scrub composed timing, and export presentation-ready MP4 or transparent GIF previews.
- Support accessible experiences with project-wide Full, Reduced, or No Motion policies and authoring guidance for flashing effects.
- Integrate with Timeline, PlayMaker, Game Creator, Yarn Spinner, Ink, Adventure Creator, PrimeTween, the Input System, and other optional production tools through guided setup.
- Explore a ready-to-open Timeline sequencing sample with reveal clips, blended effects, control markers, guided Edit Mode preview, and Game view transport controls.
- Start quickly with a Welcome journey, searchable gallery, recipes, guided lessons, production examples, project health checks, migration tools, and a complete demo.
- Extend the same foundation with optional Text Studio 3D, Mechanical Displays, Terminal FX, and custom renderer, effect, action, timing, placement, and text-source integrations.
