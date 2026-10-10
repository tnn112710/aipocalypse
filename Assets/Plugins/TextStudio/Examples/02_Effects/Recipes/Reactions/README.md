# Reactions

Open `../../Reactions.unity` and press Play. Click the text itself, or use the arrow keys and Enter to move between the four examples. The first two lines also reveal once when Play begins. Nothing plays audio.

- **Finish with a sparkle:** word reveal belongs to Typewriter. A linked Reaction pulses the completed line, then its dependent spark runs after that pulse finishes.
- **A spark at the word:** `<event=here>` triggers a small native UI burst and a response on the current word. This object stores its recipe inline, so copying the object carries the setup with it.
- **Tap for a shake:** the built-in Damage Jolt recipe responds to the `damage` signal. The sample's input method only calls `PlayReaction("damage")`.
- **Changing score:** the real text changes from 98 to 99 to 100 and onwards. A linked Reaction captures only outgoing digits and drifts them away. Retained TMP geometry uses a static font and the native renderer; no duplicate text objects are constructed by the sample.

The additional recipes are ready to inspect or assign: **Old digits tumble** uses seeded ballistic motion without a physics dependency; **Occasional letter sparks** combines chance and cooldown; **Finish before reacting again** ignores overlapping signals; **Tell the host when hidden** sends a named notification after hiding finishes. These assets keep less common options available without another menu in the scene.

For audible responses, open [Events and Sound](../../Events%20and%20Sound.unity) and turn Sound on explicitly. Its audio routes complement this quiet visual lesson.

The saved scene is readable in Edit Mode. Scene reactions run in Play Mode. To preview an outgoing recipe without entering Play, select **Old digits drift away** or **Old digits tumble** and use its Inspector preview. The isolated preview supports deterministic outgoing motion; markers, signal responses and UI sparks require Play Mode. It does not start audio or gameplay.

Reuse a text object and its referenced recipe/action assets. The small `TextStudioReactionsLesson` component supplies clicks and score changes only. Change trigger, target, delay, duration, intensity, chance, cooldown and overlap in the native Reactions inspector. Typewriter remains the owner of reveal and hide visibility.
