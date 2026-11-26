# Forecast

The night forecast, the board in the yard that shows it, and how the player reveals it each day. Code in `_Core/ForecastSystem/` (namespace `MB13.Aipocalypse.Core.ForecastSystem`), data in `Data/Forecast/`, sounds in `Data/Audio/Forecast`, the board in `Content/Structures/ForecastBoard/`, script map in [CODE_STRUCTURE.md](CODE_STRUCTURE.md). Design and open questions: [docs/gdd/IDEAS.md](../gdd/IDEAS.md) → Night fight periods and sizes.

## Rules today

- A night has three **periods** (`NightPeriod`): Evening 20:00–22:00, Midnight 22:00–00:00, Late night 00:00–02:00. Each brings a **fight** (`FightSize`): none, small, medium or boss.
- **The same forecast every day**: small in all three periods (`ForecastSettings.Periods`). Rolling it per day and balancing come later.
- **Display only.** The forecast doesn't drive the raids yet: raids still follow [RAIDS.md](RAIDS.md) (`EveryNightSchedule`). The period hours are stored but nothing reads them yet.

## The reveal

- **Each morning** at `NewForecastHour` (06:00) the plaques hang as **'?'**. The day's forecast runs from then until the next 06:00, so it stays shown through the night.
- **The player pulls the rope** (`[E] Spin Forecast` on the handle by the right post): all three plaques spin like slot reels and land left to right; a boss always lands last.
- **Nobody pulled?** At `SelfRevealHour` (19:00) they spin by themselves, so the forecast is never hidden when the night starts.
- **Saved:** the day whose forecast was revealed (`ForecastSaveSection`). A load or a clock jump shows the state at once, without the spin.

**Changing the mechanic later:** the trigger is only `ForecastRope`. Anything else (a bell, a robot, the drone, shooting a target) calls `ForecastReveal.Reveal()`; remove or replace the rope and the board still spins. `ForecastSettings.HideUntilRevealed` off brings back the always-shown board (no '?', no rope prompt). Without a `WorldClock` in the scene the forecast always shows.

## The board

`ForecastBoard.prefab`: a frame of three HIGHLANDS logs (two posts 3.5 m apart, a crossbar), three Lakefell-wood plaques and a pull rope.

| Part | What it is |
| --- | --- |
| `Plaque_<Period>` | The hanger: two ropes from the crossbar hold the ends of an iron `Pin` through the plaque's middle |
| `Reel` | The plaque itself (two planks), turning on the pin; carries `Period` and `Mark` |
| `Period` (quad, top-left) | The period icon, fixed: a setting sun (Evening), a moon (Midnight), a star (Late night) |
| `Mark` (quad over the face) | Centred: '?' while hidden; then small crossed swords (Small), the same 1.7× larger (Medium), a horned skull (Boss), nothing for none |
| `PullRope` | `ForecastRope` + a trigger box to look at; a rope down to a wooden toggle `Handle` |

**The spin** (`ForecastBoard`, DOTween, tuning on the component):

1. The rope's handle is yanked down 0.22 m (lever sound) and springs back; the reveal fires as it bottoms out and the plaques jump 5 cm on their ropes.
2. Every reel rocks back 26°, holds a beat, then spins forward and slows down (OutQuart) while the frame rattles: the first lands after 5 turns in 1.6 s, each next one turns 2 more and lands 0.55 s later. A boss always lands last.
3. While a reel looks away during its last turn its face swaps from '?' to the result, so it lands showing it. Each edge-on pass ticks (one tick at most every 0.05 s for the whole board, so the ticking slows as the reels do).
4. **Near miss:** the last plaque stops 70° short of its face, teeters back and forth on the edge (0.55 s; a boss 1.1 s and one more swing), then tips over into place.
5. Landing: a wooden clack, the plaque overshoots and wobbles (16°), squashes and stretches, dips on its ropes, and wood chips puff from its bottom edge (`WoodHit`, the fences' effect). A boss: a deep thud, a bigger wobble (34°), two puffs, the whole board shakes and the other plaques jump.
6. A new morning turns the plaques back to '?' with one quick turn. A console override while shown spins to the new marks.

**Shots:** the planks have colliders, read as Wood: bullets leave the wood impact and bullet holes that ride along on the plaque. A hit spins its plaque round the pin by where it lands (`IHitReaction`, called by `DamageService.DealHit` for bullets, swings and arrows): within 6 cm of the pin it only wobbles, further out it turns 1–3 times, always landing face out (the forecast never changes). Ignored while the reveal spins.

**Wind:** the plaques rock on their pins with the weather (`WeatherService.Mix` strength and gusts, about ±1° when calm to ±7° in a gale), a little out of step with each other.

The paint is baked: `ForecastBoardSetup.Textures` composites the Layer Lab picto icons on the CPU, wears them with noise and writes one PNG and one alpha-clipped URP Lit material per mark, so the paint is lit like the wood. There is no text on the board.

**Sounds** (`Data/Audio/Forecast`, Gamemaster clips, 3D, 2–35 m): `Forecast_RopePull`, `Forecast_ReelTick`, `Forecast_Land`, `Forecast_LandBoss`. Swap clips in the Library Manager.

In WorldMap the board stands in front of the house on its right side (the `Forecast Board` scene root, house-local (10.5, 5), clear of the robots' `Spot_Garden` and the dog's `Visit_Garden`), its plaques turned to the path from the door to the gate.

## Testing

| Command | Does |
| --- | --- |
| `forecast.status` | Tonight's fight per period, shown or hidden |
| `forecast.reveal` | Reveal now, as the rope does (the plaques spin) |
| `forecast.hide` | Back to '?' (not after 19:00: past the self-reveal hour it stays shown) |
| `forecast.set LateNight Boss` | Override one period until `forecast.reset` (not saved); spins if shown |
| `forecast.reset` | Back to `ForecastSettings` |

`ForecastTests` cover the service (defaults, overrides, off), the reveal rules (the day before 06:00, hidden each morning, shown once pulled or after 19:00), `ForecastReveal` on a real clock (a live reveal, hidden the next morning, always shown without a clock or with hiding off), the save round trip, every `ForecastSettings` asset having every period, and the prefab (a plaque per period on a reel, a mark per fight, the '?', the rope with its collider).

## Wiring

`ForecastInstaller` (a `SceneInstaller` on the scene's `GameLifetimeScope`) registers `ForecastSettings`, `ForecastService`, `ForecastReveal` (entry point, ticked), `ForecastSaveSection` and, in cheat builds, `ForecastCommands`, and injects the scene's `ForecastBoard`s with their `ForecastRope` (collected on scene save / entering Play). It needs no other installer; `ForecastReveal` uses the `WorldClock` when there is one. Events (`_Common/Events/ForecastEvents.cs`): `ForecastChanged` (an override) and `ForecastRevealChanged` (`Revealed`, `Live`); the board spins on a live change and shows the state at once otherwise. Without the installer, or with `ForecastSettings.Enabled` off, the plaques stay blank.

Rebuild everything (sounds, textures, materials, prefab, settings, WorldMap placement): `unity command script run --file AgentScripts/Forecast/ForecastBoardSetup.cs --entry ForecastBoardSetup.All`, then the import presets (`AudioImportKit.Apply`, see [tools-unity-broaudio](../../.agents/skills/tools-unity-broaudio/SKILL.md)); each step (`Audio`, `Textures`, `Prefab`, `Data`, `World`) also runs alone. `World` reopens WorldMap from disk, so save the scene first.
