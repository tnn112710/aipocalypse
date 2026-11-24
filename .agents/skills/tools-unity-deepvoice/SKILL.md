---
name: tools-unity-deepvoice
description: DeepVoice Pro (AiKodex) in aipocalypse — the editor-only cloud text-to-speech window that writes voice lines as WAVs, plus its trimmer / joiner / equalizer. Covers who presses Generate, the private invoice number, the monthly character quota, writing line sheets (voice, model, pace, instructions, file names), prompt tricks for pauses and emotion, the output location, and routing a clip into BroAudio. Use for any spoken line, voice-over, dialogue audio, talking drone / robot / NPC voice, or anything touching Assets/Plugins/DeepVoicePro.
---

# DeepVoice Pro (aipocalypse)

Installed at `Assets/Plugins/DeepVoicePro/` (vendor code, don't modify). Source: its PDF (`DeepVoice Pro documentation.pdf`) and `Editor/Scripts/DeepVoiceProEditor.cs`, v3.0.7. Only the Editor folder matters; nothing ships. `Demo/` is the vendor demo scene, never add it to Build Settings. Rules here come from PLUGINS.md and ASSETS.md; if they disagree with this file, the docs win.

## What it is

A **cloud** text-to-speech service wrapped in an editor window (`Window > DeepVoice Pro`). It posts the text to `dvpro.aikodex.com`, gets a base64 WAV back and writes it into the project. The same window has an Audio Trimmer, Audio Joiner and Audio Equalizer for clips. It needs the `com.unity.editorcoroutines` package (installed) and internet. Nothing is stored server-side, and generated lines may be used commercially if you own the rights to the content you generate.

## Status in this repo

- **Tag:** Undecided (PLUGINS.md → Audio). Ask the user before relying on it for anything beyond what they requested.
- **Install location is not the vendor default.** The window hard-codes `Assets/DeepVoicePro/...` for its logo, icons, voice previews and default output folder (`Assets/DeepVoicePro/Voices`, created when the window opens), but the pack sits in `Assets/Plugins/DeepVoicePro`. Expect missing icons and previews, and an output folder outside `Plugins/`. Not tested here because opening the window creates the folder. Fixing it (move the pack, or patch the paths with a documented local patch) is the user's decision; don't do either unasked.
- **No voiced characters are built.** The GDD floats a cheerful tech-support drone voice (IDEAS.md), which is a proposal: don't generate lines for it unless the user asks.

## Rules for agents

1. **The user presses Generate.** The terms forbid using the service "from outside Unity" and abusing it with request floods, so never call the endpoint with `curl`, Python or any script outside the editor. Each generation also spends the user's monthly character quota (the Status button shows usage; limits renew on the 30th / 31st).
   - A batch driven from inside the editor is possible (`SendMultiPayload` / `SendInstructPayload` are public on the open window, and the invoice comes from `PlayerPrefs`), but it is untested. Do it only on the user's explicit OK, after saying how many characters it costs.
2. **The invoice number is private.** It lives in editor `PlayerPrefs` key `DeepVoicePro_Invoice`. Never read it, print it, write it to a file or doc, commit it, or put it in any request.
3. **Your deliverable is a line sheet**, so the user can paste each row into the window. The plugin keeps no record of the text, only the WAV, so the sheet is also how a clip gets regenerated. One row per line:

   | Field | Notes |
   | --- | --- |
   | Text | ≤ 2500 characters (the counter turns red past it). Plain letters, periods, commas; no straight quotes `"` and no trailing line break, or the server answers 400 |
   | Model | `Multi` (pace / variability / clarity) or `Instruct` (free-text voice direction) |
   | Voice | From the lists below |
   | Settings | Multi: pace 0.7–1.2, variability 0–1, clarity 0–1 (window defaults 0.5 / 0.3 / 0.75). Instruct: the instruction block |
   | File | `<Voice>_<Body>_<take>.wav`: see the naming trap |

4. **Generated lines are AI-generated audio**: they go to `Assets/_Placeholder/Audio/Voice/` (ASSETS.md → `_Placeholder`, nothing ships from there) until the user says a line is final and where it lives. Add a line to `Assets/_Placeholder/Audio/SOURCES.txt` for each batch (model, voice, source text).
5. **Into the game** the same way as any clip (`tools-unity-broaudio`): a BroAudio entity in the right library (adding entities and `SoundID`s is Library Manager authoring, ask first), played through `AudioService`, never an `AudioClip` field in game code. Spoken text that also shows on screen is a localization row (`docs/technicals/LOCALIZATION.md`), not a string in the clip's name or a script.
6. **Import settings:** a voice line is usually 2–60 s, so it gets `AudioImport_Medium`. Run the BroAudio skill's `Apply` on the new files; a hand-set import is undone by the next `Apply`.

## Models and voices

- **Multi** — 51 voices: Jessie, Harry, Glinda, Clyde, Callum, Charlotte, Dave, Fin, Freya, Batman, Andrew, Hailey, Arthur, Anime_Girl, Valentina, Wayne, Jan, Noah, Lily, Lily_Narrator, Ethan, Sophia, Olivia, Ruby, Lucas, John, Werner, Mark, Alex, Brittney, Hope, Dakota, Amelia, Allison, Heather, Arabella, Nanchan, Jane, Ember, Anika, Lilian, Olufun, Bonnie, Taro, Eva, Nadya, Nik, Leoni, Pablo, Bjorn, James. Six `*_Old` variants (Char, Archer, Fin, Dave, Lucas, Clyde) are in the enum but not in the preview grid: skip them.
- **Instruct** — 11 voices: Archer, Breeze, Cadence, Dune, Flare, Glimmer, Halo, Iris, Jade, Karma, Lyric. The direction is a block of labelled lines, as the window's own examples write them: `Accent:`, `Tone:`, `Pacing:`, `Emotion:`, `Pronunciation:`, `Personality Affect:`.
- Per-voice samples are `Editor/Previews/<Voice>.wav` (Brittney and Nik have none). An agent can't listen, so a voice choice is the user's ear: offer two or three candidates, don't claim one "sounds right".
- Some voices come from third-party speech providers and the available set may change as models update. A name that stops working is a server-side change, not a project bug.

## Writing text that comes out right

The AI reads exactly what it is given.

- **Pauses:** a dash `-` or em-dash `--` is the most predictable. `...` also pauses but adds hesitation or nervousness. One or two line breaks inside the text also pause (but a trailing break causes the 400 error above).
- **Pace:** Multi only, the pace slider. High variability gives more expressive output that differs between takes, and can turn unstable. Very high clarity adds artifacts.
- **Emotion:** write like a book with a dialogue tag (`"Are you sure about that?" he said, confused.`). The tag is then spoken too, so trim it off with the Audio Trimmer. Without a tag the AI sometimes infers the emotion from the sentence, which is not reliable for sarcasm. On Instruct, put the emotion in the instruction block instead and no trimming is needed.
- **Long scripts:** split into sentence-sized lines and join them with the Audio Joiner. A very long generation can fail with a 408 timeout.
- **Languages:** the PDF says supported languages are listed as two-letter codes per model but doesn't list them. Only English is offered in the game for now (AGENTS.md → Text), so generate English.

## Output and file naming

- Writes `<folder>/<Voice>_<Body>_<take>.wav` and refreshes the AssetDatabase. The take number goes up by one after each success; the name tag shows `[Overwrite Name]` (red) when a file with that name exists and `[Available Name]` otherwise. An overwrite replaces the file silently.
- **Naming trap:** the first File Name field is also the **voice id sent to the server**. Editing it to a character name makes the request fail. Name a clip through the middle field (`Body`, e.g. `Drone_Greeting`) and leave the first one alone. A tidy per-character name comes from renaming the file in the Editor afterwards (with its `.meta`).
- The **Voices Folder** is locked to the default while Auto is ticked. Untick it to type another path; the `. . /` button opens a system folder picker and can return a path outside the project.
- Audio Trimmer / Joiner / Equalizer write new WAVs into the same folder (through a `Temp_data` subfolder). Audio Tools (`tools-unity-broaudio` → Clip editing) does the same jobs and is the project's choice for trimming and normalizing; use this window's tools only when a clip is already open there.

## Errors

| Message | Cause | Do |
| --- | --- | --- |
| `Invalid Invoice Number` / `Invalid Response` | Bad or unset invoice | User re-enters it, presses Verify, then saves |
| `Limit Reached` | Monthly characters spent | Wait for the renewal; don't retry in a loop |
| 400 | Quotes, line breaks or special formatting in the text | Re-paste as plain text, drop quotes and the trailing break |
| 408 | Model hit a generation error (huge file) | Fewer words, split into sentences |
| 500 / can't connect | Server down or no internet | Check the connection, retry hours later; contact `info@aikodex.com` if it lasts a day |

The window only logs these to the Unity console, so after a user-run generation `console status` / `console get` is how an agent sees why nothing appeared.

## Checks

- After the user generates: the WAV exists, is non-silent and plays (`AudioClip.length` > 0), and `git status` shows only the new clip and its `.meta`.
- After routing into the game: `Apply` reports `would change 0` on a re-run, and the entity plays through `AudioService` in play mode.
