# Text Studio Whisperwood Letters Demo

Whisperwood Letters is a compact fantasy dialogue sample built in two scene variants:

- `TextStudioWhisperwood_Canvas.unity`
- `TextStudioWhisperwood_UIToolkit.unity`

Both scenes run the same simple flow. The Canvas version uses `TextStudioDialogueSource` with `TextStudioText` and TextMeshPro UGUI. The UI Toolkit version uses normal `Label` elements with persisted `TextStudioBinding` objects on their `text` properties.

## What To Inspect

- In the Canvas scene, expand `Whisperwood Canvas` to inspect the world map, persistent player/NPC markers, menu, dialogue, credits, and button prefab instances.
- In the UI Toolkit scene, select `Whisperwood UI Document`, then open `UIToolkit/Whisperwood.uxml`. Layout and styling are persisted in UXML/USS, as they would be in a normal UI Toolkit project.
- Open `Data/WhisperwoodStory.asset` to edit the three villagers, positions, colors, rewards, and dialogue without changing presentation code.
- Open `Prefabs/WhisperwoodButton.prefab` and `WhisperwoodNpcMarker.prefab` to see the reusable Canvas view pieces.

The runtime controller only drives state, movement, dialogue, and view updates. It does not create the scene hierarchy or visual tree.

## Controls

- Start begins the vignette.
- WASD or arrow keys move the player marker through the village.
- E greets the nearest villager or inspects the Moon Gate. Choose a question to continue, or leave and return later.
- Space, Return, a dialogue-area mouse click, or Continue completes the current reveal/wait or advances to the next dialogue line.
- Skip Conversation closes the current dialogue and applies its reward.
- Journal or J opens your collected words. Back, J or Escape returns to the same conversation position.
- Sound on/off toggles the dialogue voice and stays as chosen when you restart.
- Escape opens the pause menu.

These controls are read through Unity's Input System. The Canvas UI also uses the Input System UI module, so the sample does not require the legacy input handler.

## Covered Concepts

- Title, start menu, pause menu, and credits effects.
- A small top-down exploration loop with contextual prompts.
- Production-style dialogue reveal through `AdvanceOrSkip()`.
- Skippable waits, wait-for-input beats, and marker events. Each authored `<event=word,ember>` (or `dew` / `star`) lights its villager’s lantern and updates the quest when reached. Explicitly skipping the whole conversation also grants that word, once.
- Dialogue and its reveal pause while the journal or pause menu is open.
- Restrained built-in effects such as `typewriter`, `em`, `shimmer`, `gradientFlow`, `colorPulse`, `pop`, `wave`, `shake`, `fade`, and `pulse`.
- Canvas and UI Toolkit parity without optional Text Studio add-ons.

## Dialogue voice

The saved Dialogue Voice object uses `TextStudioAudioPlayer` with three packaged human voice clips, a three-source pool and a short cooldown. The shared `Whisperwood Dialogue Voice` Reaction asset requests a glyph cue; the controller passes Canvas or UI Toolkit Reaction cues to the same Audio Player. Speaker pitch ranges live in `SetSpeakerVoice`; replace the clips or adjust the player to change the voice. Reactions and audio remain silent during Edit Mode preview.

## Preview

Text and visual effects preview in Edit Mode. Movement, conversation choices and quest progression belong to Play Mode; ordinary preview does not execute gameplay markers.

## Art

The demo uses two text-free bitmap assets in `Art`: a fantasy village map and a parchment/wood panel texture. All readable copy is driven by Text Studio markup from the shared story data and view controller.

## Moon Gate finale

The collected words become an inscription before you step through the gate. Canvas uses the packaged Kalam font, native Clean Ink Reveal Writing preset and layered Appearance heading. UI Toolkit presents the same story moment with its native letter reveal; it does not simulate Writing or prepared Appearance support. The reward is one saved text object, reused on restart. Skip inscription finishes the lettering; Step through opens the credits. The journal preserves inscription progress.

## Luma's three notes

Choose Listen to the three notes in Luma's conversation for an optional rhythmic reminder. The packaged bell phrase is synthesized audio; its cue sheet marks EMBER, DEW and STAR. The controller reads `AudioSource.timeSamples` and submits a native color effect to each cue's word range through `ITextStudioExternalEffectSource`. Canvas and UI Toolkit use the same cue sheet and effect. Skip clue or Space returns to the conversation without collecting a word. Pause and Journal pause the audio; Sound off mutes it while preserving timing. No audio starts in Edit Mode.
