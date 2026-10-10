# Text Studio Audio Typewriter

This sample shows three typewriter audio profiles using `TextStudioAudioSet` assets:

- `Classic Typewriter`: mechanical key sounds with the built-in Classic Typewriter timing.
- `Human Dialogue`: softer keyboard taps with Human Typist timing.
- `Sci-Fi Terminal`: short digital blips with Terminal Snap timing.

Open `../02_Effects/Character Voices.unity` and enter Play Mode. Choose a profile to swap the packaged `TextStudioAudioSet`, matching reveal cadence, sample text, text color, and named marker cue on one stable `TextStudioText` object. Use Replay to hear the current recipe again without replacing the authored hierarchy.

The shared `Typewriter Audio Reactions` asset chooses when a glyph or named marker requests sound. The lesson's Audio Player uses `Cue Trigger: Reactions`. The Audio Set owns the clip choices, character filter, cadence limit and voice pool. Switching Audio Sets changes the voice without copying the Reaction stack. For a quick audio setup without a Reaction asset, leave Cue Trigger on `Typewriter Events` to play glyph and marker sounds automatically.

Select any `TextStudioAudioSet` asset to audition it in the inspector. The Audio Set inspector can type sample text, play the first marker cue, and report missing or unusually quiet clips before you wire the asset into a scene.

The included clips are trimmed, normalized, and renamed from CC0 source packs. Source archives are not included.
