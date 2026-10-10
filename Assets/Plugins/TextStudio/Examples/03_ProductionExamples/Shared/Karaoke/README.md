# Karaoke

Open `../../../02_Effects/Karaoke Styles.unity`. Three authored treatments play together with one audio owner. Play, pause, restart and seek remain contextual controls; the six complete styles and their coverage settings remain inspectable assets and controller methods. Native word highlights follow the absolute audio-sample clock.

`Assets/TextStudioKaraokeCueSheet.asset` stores the audio clip plus editable line and word timing. Cue validation requires one word cue per displayed whitespace-delimited word. The player resolves active and completed words separately, and cue gaps never invent an active highlight. Played Color switches between keeping the completed-word color and restoring the authored lyric color so only the current word remains highlighted. The active recipe eases away over the selected preset's Release Duration; it fades back to either the completed color or the authored color, depending on Played Color.

The `Assets/Styles` folder contains six copyable presets and their inspectable Composed Effects:

1. **Current Word** - mint/white tint with a subtle range pulse.
2. **Classic Progressive** - teal completed words with restrained active emphasis.
3. **Paint-On Sweep** - a soft left-to-right Color Sweep with a progress-driven shimmer band.
4. **Beat Pop** - gold/magenta lift with an approximately 1.18× word pop.
5. **Neon Wave** - cyan/magenta gradient, shimmer, wave, and tiny pulse.
6. **Bounce Along** - per-glyph bounce with coral/gold emphasis.

Every preset records its default coverage, description, recipe, release duration, active/completed Composed Effects, and reduced-motion alternatives. Selecting a preset restores that inspectable default; Played Color can then switch completion behavior independently. Layers use blending-friendly Additive, Multiply, and Lerp modes. Inspect the preset asset for its active effect, release duration and completion behavior. Bounce Along uses the longest tail so its per-glyph motion visibly settles after a word completes.

The persisted lyric view submits those recipes as external layers over the compiled word range. Existing text color, motion, reveal, scale, and placement continue to blend underneath. The sample has no Timeline dependency; Timeline support remains an optional guarded adapter. The included lyric and audio are original sample material and may be replaced with project-owned content.
