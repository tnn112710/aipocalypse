# Weather

- **Phase A (2026-11-08):** the weather is shown and heard (sky, rain, wind, fog, sound, lightning).
- **Phase B (2026-11-08):** it changes on its own by weighted rules on game time (`WeatherSchedule`, [Rules](#rules)), with separate tables for day, night, Blood Moon nights and eclipse days, and can be held or forced from the console or code.

Still to come, from the GDD and only after the user confirms each one: what the weather does to play (vision, fishing, sound masking), a forecast the player sees (the drone), real Blood Moons and eclipses (only marked by hand so far).

Design: `docs/superpowers/specs/2026-11-08-weather-design.md`. How COZY is used here: skill `tools-unity-cozy` and [DAYNIGHT.md](DAYNIGHT.md) → COZY.

## Model

Two layers:

- **Sky weather** (`SkyWeather`): what the sky does, exactly one at a time. Clear, Partly Cloudy, Cloudy, Overcast, Light Rain, Medium Rain, Heavy Rain (the storm), Fog. They are steps of one scale, so they never combine.
- **Wind level** (`WindLevel`): Calm, Breezy, Windy, Gale, on top of any sky weather.

Each sky weather has a default wind. An override replaces it until it is cleared ("auto"). Example: a windy sunny day = Clear + Windy. Breezy is the scene's tuned sway, so Partly Cloudy + Breezy (the start weather) looks exactly like the scene did before weather existed.

## What each part does

```text
WorldClock ──► WeatherSchedule (WeatherRules on Weather.asset; WeatherPlanner plans 24 h ahead)
weather.* console ──┘  └─► WeatherService.Set(sky, wind)
                     WeatherService (scene, EnvironmentInstaller; Enabled in Weather.asset)
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
- **Saves.** `WeatherService.State` (`WeatherState`: sky + wind override) and `Restore(state)`. Nothing saves yet; neither do the plan, the marked Blood Moon / eclipse days or forced weathers (reseeding `RandomService` replays the rolls).

## Rules

`WeatherSchedule` (scene, registered when `Weather.asset` → Rules is set and Enabled; needs the world clock) follows `WorldClock` and shows on `WeatherService` what its `WeatherPlanner` planned. The planner is plain C# (tests and the rules inspector run it directly) and rolls on `RandomService.Stream("weather")`.

- **Periods.** Night runs from `NightStart` (18:00) to `NightEnd` (06:00); the rest is day. A night marked Blood Moon (by the day it starts on) uses the Blood Moon column; a day marked eclipse uses the Eclipse column for its daytime. Only the console marks them so far (`weather.bloodmoon`, `weather.eclipse`); the future Blood Moon / eclipse system calls `WeatherSchedule.MarkBloodMoon` / `MarkEclipse` a few hours ahead.
- **How often.** When a weather ends, the next is picked by weight from the column of the period it starts in (never the same one again). Weight 0 = never in that period, not even on the way to another weather.
- **Walking.** Sky weathers are steps of one ladder: Clear, Partly Cloudy, Cloudy, Overcast, Light, Medium, Heavy Rain. Fog sits beside Clear, Partly Cloudy and Cloudy and reaches the rest through Cloudy. A far weather is reached through every step in between, each lasting `StepHours` (0.5–1 h), so clear never jumps to a storm.
- **How long.** Each weather picks a duration by weight from `DurationHours` (1, 2, 4, 6, 8 h), ± `DurationJitter` (0.5 h). The walk steps don't count.
- **Never where it isn't allowed.** A weather that would run into a period (or a forced weather) that doesn't allow it ends early, so the walk away finishes in time; one that couldn't last `ShortestHours` before that isn't picked. Marked late (a Blood Moon marked mid-storm at 17:00), the walk goes at the shortest step and may finish a little late.
- **Wind.** Each weather rolls its wind from its own wind weights when it starts; the wind then moves one level per step (0.5–1 h), also inside a weather, so calm to gale passes through breezy and windy.
- **Holding and forcing.** `Hold(sky, wind)` keeps a weather until `Release()` (blends to it now). `Force(sky, wind, hours, hour)` puts a fixed weather in the plan: from now it blends in at once; from a later hour the plan walks there so it's in place on time, and the rules carry on from it afterwards. A forced weather ignores the tables (Blood Moon + fog, eclipse + rain). If the rules never pick it in that period, the walk waits one step short until it starts. With the schedule on, change the weather through these, not `WeatherService.Set` (the next frame puts the plan back).
- **Clock jumps and sleep.** The plan is in game hours, so clock speed doesn't matter. A jump of more than half an hour, or the end of a time skip, cuts to the planned weather instead of blending; during a skip the shown weather waits.
- **Starting values** (`Data/Environment/Weather/WeatherRules.asset`, made by `WeatherSetup.Rules()`):

| Weather | Day | Night | Blood Moon | Eclipse | Durations 1 / 2 / 4 / 6 / 8 h | Wind Calm / Breezy / Windy / Gale |
| --- | --- | --- | --- | --- | --- | --- |
| Clear | 30 | 30 | 60 | 60 | 0 / 40 / 30 / 20 / 10 | 4 / 5 / 1 / 0 |
| Partly Cloudy | 30 | 30 | 30 | 30 | 0 / 40 / 30 / 20 / 10 | 2 / 7 / 1 / 0 |
| Cloudy | 15 | 15 | 10 | 10 | 0 / 50 / 30 / 15 / 5 | 1 / 6 / 3 / 0 |
| Overcast | 8 | 8 | 0 | 0 | 0 / 50 / 30 / 15 / 5 | 1 / 6 / 3 / 0 |
| Light Rain | 5 | 2.5 | 0 | 0 | 30 / 40 / 25 / 5 / 0 | 1 / 6 / 3 / 0 |
| Medium Rain | 3 | 1.5 | 0 | 0 | 40 / 40 / 20 / 0 / 0 | 0 / 3 / 6 / 1 |
| Heavy Rain | 1.5 | 0.75 | 0 | 0 | 60 / 40 / 0 / 0 / 0 | 0 / 0 / 4 / 6 |
| Fog | 4 | 4 | 0 | 0 | 30 / 50 / 20 / 0 / 0 | 9 / 1 / 0 / 0 |

Overcast and fog are 0 on Blood Moons and eclipses because they would hide the red moon and the eclipse.

The rules inspector (`WeatherRulesEditor`) shows the three tables as grids: each weight with its share, a share stripe per period and the average duration per weather. Its **Test** runs the planner over many days (seeded, a Blood Moon every Nth night, an eclipse every Nth day) and shows the share of time each weather really gets, which differs from the weights because of the walk steps and durations. With the starting values over 1000 days: rain 6.9% of the day and 5.9% of the night, none on Blood Moons and eclipses; windy or gale about 18%; about 10 sky changes a day counting steps; a rain spell every 2.2 days; no time in a weather its period doesn't allow.

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
| How often / how long / which wind each weather gets, per period; night hours, step length | `Data/Environment/Weather/WeatherRules.asset` (grids, with a Test) |
| Turn the rules off (console and code only) | `WeatherRules.asset` → Enabled, or clear `Weather.asset` → Rules |
| Turn weather off | `Weather.asset` → Enabled (the scene keeps COZY's current profile and the tuned wind) |

Console ([CONSOLE.md](CONSOLE.md)): `weather.show`, `weather.set <sky> [wind] [seconds]` and `weather.wind <Calm/Breezy/Windy/Gale/auto>` (both hold until `weather.auto`), `weather.auto`, `weather.forecast`, `weather.force <sky> <hours>` / `weather.force <sky> <wind> <hour> <hours>`, `weather.bloodmoon [day on]`, `weather.eclipse [day on]`, `weather.strike`. For an edit-mode look at COZY's sky alone: the user-scope `CozyKit.SetWeather`.

## Setup script

`AgentScripts/Weather/WeatherSetup.cs` (run with `unity command script run --file AgentScripts/Weather/WeatherSetup.cs --entry WeatherSetup.<Entry>`). Every step is safe to re-run and saves only what it wrote:

- `Profiles()`: the COZY profile copies.
- `Audio()`: the five sound entities.
- `Settings(reset = false)`: the settings asset and the flash profile. It keeps tuned values unless `reset`.
- `Rules(reset = false)`: `WeatherRules.asset` with the starting values above, set on `Weather.asset` → Rules. It keeps tuned rows unless `reset`.
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
