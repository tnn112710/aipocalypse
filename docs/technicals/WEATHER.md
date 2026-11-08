# Weather

Phase A (2026-11-08): the weather can be shown and heard, but nothing decides it yet. It changes only from the console.

Still to come, from the GDD and only after the user confirms each one:

- when the weather changes (a daily roll, a forecast);
- what it does to play (vision, fishing, sound masking).

Design: `docs/superpowers/specs/2026-11-08-weather-design.md`. How COZY is used here: skill `tools-unity-cozy` and [DAYNIGHT.md](DAYNIGHT.md) → COZY.

## Model

Two layers:

- **Sky weather** (`SkyWeather`): what the sky does, exactly one at a time. Clear, Partly Cloudy, Cloudy, Overcast, Light Rain, Medium Rain, Heavy Rain (the storm), Fog. They are steps of one scale, so they never combine.
- **Wind level** (`WindLevel`): Calm, Breezy, Windy, Gale, on top of any sky weather.

Each sky weather has a default wind. An override replaces it until it is cleared ("auto"). Example: a windy sunny day = Clear + Windy. Breezy is the scene's tuned sway, so Partly Cloudy + Breezy (the start weather) looks exactly like the scene did before weather existed.

## What each part does

```text
weather.* console ──► WeatherService (scene, EnvironmentInstaller; Enabled in Weather.asset)
                        WeatherBlend: shown values → target over TransitionSeconds (game time: pausing holds it)
                        ├─ CozyBridge.SetWeather(profile)    COZY: clouds, colour filter, its fog, ground mist
                        ├─ WeatherRain.Set(amount, drift)    the rain drops (Unluck's rain), leaning with the wind
                        ├─ WeatherWind.Set(str, speed, gust) trees, plants, grass sway
                        ├─ EnvironmentDirector.WeatherFogScale  Unity fog density (the default fog mode)
                        ├─ AmbienceService.NatureVolume      birds, meadow wind, owls get quieter in rain
                        ├─ WeatherAudio                      rain / wind beds (one kept player per sound)
                        ├─ LightningFlash + Thunder          storm: flash, then thunder 0.3–2.5 s later
                        └─ WeatherChanged (MessagePipe)      no subscriber yet
```

| Sky weather | COZY profile (ours, in `Data/Environment/Cozy/Weather/`) | Default wind | Rain amount | Bed | Fog density × | Nature × | Lightning |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Clear | `Weather_Clear` (COZY Clear: no clouds) | Calm | – | – | 1 | 1 | – |
| Partly Cloudy | `Weather_PartlyCloudy` | Breezy | – | – | 1 | 1 | – |
| Cloudy | `Weather_Cloudy` (COZY Mostly Cloudy) | Breezy | – | – | 1.1 | 1 | – |
| Overcast | `Weather_Overcast` | Breezy | – | – | 1.3 | 0.8 | – |
| Light Rain | `Weather_LightRain` (COZY Light Rain) | Breezy | 0.2 | RainLight 0.7 | 1.5 | 0.5 | – |
| Medium Rain | `Weather_MediumRain` (COZY Light Rain) | Windy | 0.5 | RainMedium 0.8 | 2 | 0.3 | – |
| Heavy Rain | `Weather_HeavyRain` (COZY Thunder Storm) | Gale | 1 | RainHeavy 1 | 2.5 | 0.1 | yes |
| Fog | `Weather_Fog` (COZY Dense Fog) | Calm | – | – | 4 | 0.7 | – |

| Wind level | Strength × | Speed × | Gust × | Bed |
| --- | --- | --- | --- | --- |
| Calm | 0.4 | 0.8 | 0.3 | – |
| Breezy | 1 | 1 | 1 | – (the ambience's meadow wind) |
| Windy | 1.8 | 1.3 | 1.8 | WindStrong 0.45 |
| Gale | 2.8 | 1.6 | 3 | WindStrong 0.9 |

All of these are starting values in `Data/Environment/Weather/Weather.asset` (`WeatherSettings`). Tune them there.

### Details

- **COZY profiles.**
  - Copies of COZY's shipped profiles, made by `WeatherSetup.Profiles()`.
  - Removed from each copy:
    - COZY's Audio FX and Thunder FX: sound is ours, and COZY's thunder creates and destroys an object per strike;
    - Precipitation, Climate, Wind and Event FX: they do nothing without their COZY modules;
    - **its VFX Graph rain** (Stylized Drizzle / Light Rain / Heavy Rain).
  - Kept: clouds, fog, colour filter and the ground mist (Stylized Fog VFX), which stay vendor assets referenced by the copies.
  - `WeatherSettings` keeps each profile as a plain `ScriptableObject`, so only `CozyBridge` references COZY types.
- **Why COZY's rain was dropped.**
  - It ran about 18,600 GPU particles in heavy rain, about +1.6–2.2 ms a frame in the editor.
  - It looked like ovals.
  - It needs COZY's FX-blocking camera (off here: it renders the scene a second time every frame). Without that camera COZY never sets the rain's range values, and no rain shows at all.
- **Rain** (`Environment/Weather/Rain`): an instance of `Environment/Weather/WeatherRain.prefab` (made by `WeatherSetup.Rain()` from Unluck Stylized Weather particles), with `WeatherRain` on its root. The prefab's root follows the camera every frame. Its layers (`WeatherRain` → Layers):

  | Layer | From | Full rate | Heavy rain | What it is |
  | --- | --- | --- | --- | --- |
  | Rain Multi | 0 | 130/s | ~325 particles | Unluck's multi-streak sheets (4–5 m), spawned 6 m above the camera, light grey: the main rain |
  | Rain Warpy | rain amount 0.5 | 80/s | ~80 | Unluck's falling, widening streak sheets: heavy rain only |
  | Splashes | 0 | 120/s | ~45 | Unluck's splash sprite (from `Rain -C`), emitted on a 10 m disc |

  - **Rain Multi changes from the pack:**
    - It spawns 6 m above the camera instead of 17.9 m. Its sheets sink only about 11 m in their 2.5 s, so from 17.9 m they never came below about 6.5 m over the camera, and light and medium rain showed only when looking up. At 6 m the sheets span about 6 m above to 5 m below eye level.
    - Its colour is light grey instead of the pack's dark teal. The material is additive, so the dark teal added almost nothing over trees and grass.
  - **Amount:** a layer's rate = full rate × (rain amount − From) / (1 − From).
  - **No collision.** No particle collides with anything (the user's call, for cost). The splashes are fake: their disc sits on the ground under the camera, found by one downward raycast per frame against Default and Water. The raycast skips `CharacterController`s, so the player's capsule doesn't count as ground.
  - **Wind:** the falling layers drift sideways with the wind: `RainSlant` (0.6 m/s) × wind strength, along `WeatherWind`'s direction.
  - **Night:** the particles are unlit, so each layer's authored colour is multiplied toward a dim blue-grey as the director's `Daylight` drops.
  - **Off:** when no rain is set, every layer turns off once the last particles are gone.
  - **Not used from the pack:** its wrapper script and weather controller.
  - **History:** the first replacement for COZY's rain was Unluck's `Rain -C` (stretched drops with world collision). The user found it looked bad and picked Multi / Warpy instead.
- **Wind** (`WeatherWind` on `Environment/Wind/Weather Wind`, yaw 30) is the only writer of the foliage wind:
  - the Raygeas trees and grass values (`Raygeas*`) and the HIGHLANDS / shared Raygeas plant values (`RAYGlobal*`): the three Raygeas wind drivers' tuned values (copied into its fields), × strength (forces) and × speed (speeds), with the direction from its forward;
  - BK flutter: `BKFoliageGlobals.SetWindScale`;
  - Stylized Grass: its `WindController` on the same object reads the `WindZone` there (Main = strength, Turbulence = gust) and multiplies every grass material's own wind. At 1 / 1, facing yaw 30 like the materials, it computes the same sway as the materials alone.
  - The Raygeas drivers stay in the scene, switched off.
  - COZY's Wind module is not used: with it, COZY's Pure Nature integration (`COZY_BK`) would overwrite the BK values every frame.
- **Blend.**
  - A change blends every value (wind, fog, rain amount, nature volume, bed volumes, lightning) from what is shown to the target over `TransitionSeconds` (20 game seconds), and COZY blends its profiles over the same time.
  - A change in the middle of a blend starts from the blended values.
  - `Restore(state)` and `seconds = 0` cut.
- **Lightning.**
  - While the storm's blend weight is at least 0.5, a strike comes every 8–25 game seconds. None while the clock skips; a paused game holds the timers.
  - A strike pulses `Environment/Weather/Lightning Flash`: its own global Volume (`PP_Lightning`: post exposure +1.6, priority 100), off between strikes.
  - Then `Thunder` plays after 0.3–2.5 s.
- **Sounds** live in the Ambience library: `RainLight` (Drizzle_Loop_v1), `RainMedium` (Light_Rain_Loop_v1), `RainHeavy` (Heavy_Rain_Loop_v1), `WindStrong` (Strong_Wind_Loop_v1) and `Thunder` (Close_Thunder_v1/v2, random, ±10% pitch). All are ithappy Nature Sound FX ([ASSETS.md](ASSETS.md)).
  - Beds keep playing while paused, like the time-of-day ambience.
  - No volume tuning by ear yet.
- **Saves.** `WeatherService.State` (`WeatherState`: sky + wind override) and `Restore(state)`. Nothing saves yet.

## Changing things

| To change | Where |
| --- | --- |
| A weather's wind, fog, birds, rain amount and sound, lightning | `Data/Environment/Weather/Weather.asset` → Skies |
| Rain: per-layer full rate and start amount, night tint | `WeatherRain` on the root of `Environment/Weather/WeatherRain.prefab`; each layer's look (size, speed, colour, splash disc radius) on its child particle system |
| Wind strength / speed / gusts / wind sound per level | `Weather.asset` → Winds |
| Blend time, rain lean, lightning interval / thunder delay | `Weather.asset` (top) |
| Clouds, colour filter, COZY fog and ground mist of a weather | the copy in `Data/Environment/Cozy/Weather/` (never COZY's own profiles) |
| Today's tuned sway (Breezy) | `WeatherWind` fields on `Environment/Wind/Weather Wind`, BK flutter on `BK Foliage Globals`, the grass materials' wind |
| Flash look | `Data/Environment/Weather/PP_Lightning.asset`, the curve on `Lightning Flash` |
| Turn weather off | `Weather.asset` → Enabled (the scene keeps COZY's current profile and the tuned wind) |

Console: `weather.show`, `weather.set <sky> [wind] [seconds]`, `weather.wind <Calm/Breezy/Windy/Gale/auto>`, `weather.strike` ([CONSOLE.md](CONSOLE.md)). For an edit-mode look at COZY's sky alone: the user-scope `CozyKit.SetWeather`.

## Setup script

`AgentScripts/Weather/WeatherSetup.cs` (run with `unity command script run --file AgentScripts/Weather/WeatherSetup.cs --entry WeatherSetup.<Entry>`). Every step is safe to re-run and saves only what it wrote:

- `Profiles()`: the COZY profile copies.
- `Audio()`: the five sound entities.
- `Settings(reset = false)`: the settings asset and the flash profile. It keeps tuned values unless `reset`.
- `Rain()`: rebuilds `WeatherRain.prefab` from Unluck's `Rain Multi`, `Rain Warpy` and `Rain -C`'s splash sprite, overwriting hand tuning of the prefab (the scene instance stays linked).
- `Scene()`: WorldMap wiring. `Weather Wind` with the Raygeas values copied in and the drivers off, `Rain` with the rain prefab, `Lightning Flash`, and the installer fields. Then it saves the scene.
- `All()`: everything above.

`Scene()` saves WorldMap, and that save also writes COZY's edit-mode churn: the `Sun Light` cookie cleared and its size set to 20000, the atmosphere module's parameter cache, and HUD `m_SizeDelta` values. Keep only your own hunks (`git diff`) and restore the rest from the committed scene.

## Cost

Editor, RTX 5070 Ti Laptop, 2026-11-08, spawn view at 11:00, median frame time over 240 frames.

| Rain | Partly Cloudy | Light | Medium | Heavy |
| --- | --- | --- | --- | --- |
| COZY's VFX Graph rain (before) | 12.45 ms | – | – | 14.04 ms (18.6k drops) |
| Unluck `Rain -C` with collision (replaced) | 11.72 / 11.58 ms | 12.21 / 11.77 (660) | 12.04 / 11.92 (1650) | 12.10 / 11.87 (3300) |
| Unluck Multi + Warpy + fake splashes, no collision (now) | 12.91 / 12.66 ms | 13.11 / 12.47 (65) | 12.67 / 12.02 (163) | 13.19 / 11.85 (405) |

- Heavy rain now costs nothing measurable: two rounds came out −0.8 and +0.3 ms around Partly Cloudy, which is noise.
- With COZY's rain it cost about +1.6 to +2.2 ms.
- No GPU time: FrameTiming read 0 on most frames in this session.
- An earlier `PerfSuite.Views` pass (9 views, COZY rain) ran about 4 ms slower than the morning baseline in every weather, so it isn't usable.

## Known limits

- Rain falls through roofs (no collision); splashes follow the ground under the camera, so indoors they appear on the floor.
- Rain sound plays indoors at full volume (no indoor muffling).
- No wet surfaces, puddles or snow.
- Cloudy and Partly Cloudy look alike from the ground near the house.
- After a cut, the last weather's raindrops and ground fog fade out over a few seconds.
- No volume pass by ear on the new sounds.
