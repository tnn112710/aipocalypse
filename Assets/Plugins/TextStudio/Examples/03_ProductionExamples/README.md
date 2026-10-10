# Text Studio production recipes

The focused gameplay scenes live in `../02_Effects`:

- `Combat Feedback.unity`: bounded world and projected emitters, semantic presets, rapid-hit aggregation, target tracking and reduced motion.
- `Responsive HUD.unity`: live input, numeric changes, combo tiers and queued, merged or priority notifications. Prepared icons remain attached through text replacements.
- `Accessible Captions.unity`: speaker and sound captions, text size, safe-area width, background and reduced motion. The reusable `Shared/Captions/Assets/RelayRidgeSubtitleCues.asset` includes six English, French and German cues with inline emphasis.
- `Karaoke Styles.unity`: three simultaneous treatments with one audio owner, six retained style recipes, seek, pause and replay.

`Text Magnifier/Text Magnifier.unity` remains a distinct production scene: drag the lens over live text to inspect native radial resampling.

Reusable view prefabs remain in `Shared/Prefabs`, gameplay behavior profiles and emission presets in `Shared/Gameplay Feedback`, subtitle cues in `Shared/Captions/Assets`, and karaoke audio, cues and styles in `Shared/Karaoke/Assets`. `../02_Effects/Recipes` contains the native HUD signal specimens and a restartable round countdown. Instantiate the countdown under a Canvas; its small controller sequences values while Text Studio owns the text presentation.

All presentation is persisted and inspectable. Builders live in Text Studio Debug. Optional integrations and add-ons keep their own samples outside this base package.

## Combat feedback

The focused encounter demonstrates world and projected emitters, bounded pools, target tracking, merging and overflow policies. Damage, critical, healing, status, combo and loot remain independently callable on its controller. Inspect the emitters and controller for density, following, stacking and overflow. Disabling the encounter releases emissions; enabling it rebuilds bounded pools.

## Live HUD

The focused HUD supports callsign input, supplies, combos, waypoints, prompt replacement and priority warnings. Its bounded notification queue merges pickups and resumes ordinary notices after a warning. The HUD signal inputs recipe demonstrates normalized values, audio amplitude and tempo; audio starts silent. Disabling the round countdown stops its sequence.
