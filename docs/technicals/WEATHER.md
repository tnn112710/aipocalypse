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
                        ├─ CozyBridge.SetWeather(profile)    COZY: clouds, colour filter, its fog, VFX Graph rain
                        ├─ CozyBridge.SetRainWind(dir × s)   COZY's rain leans with the wind
                        ├─ WeatherWind.Set(str, speed, gust) trees, plants, grass sway
                        ├─ EnvironmentDirector.WeatherFogScale  Unity fog density (the default fog mode)
                        ├─ AmbienceService.NatureVolume      birds, meadow wind, owls get quieter in rain
                        ├─ WeatherAudio                      rain / wind beds (one kept player per sound)
                        ├─ LightningFlash + Thunder          storm: flash, then thunder 0.3–2.5 s later
                        └─ WeatherChanged (MessagePipe)      no subscriber yet
```

| Sky weather | COZY profile (ours, in `Data/Environment/Cozy/Weather/`) | Default wind | Rain | Bed | Fog density × | Nature × | Lightning |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Clear | `Weather_Clear` (COZY Clear: no clouds) | Calm | – | – | 1 | 1 | – |
| Partly Cloudy | `Weather_PartlyCloudy` | Breezy | – | – | 1 | 1 | – |
| Cloudy | `Weather_Cloudy` (COZY Mostly Cloudy) | Breezy | – | – | 1.1 | 1 | – |
| Overcast | `Weather_Overcast` | Breezy | – | – | 1.3 | 0.8 | – |
| Light Rain | `Weather_LightRain` (COZY Light Rain with its Drizzle rain) | Breezy | Drizzle | RainLight 0.7 | 1.5 | 0.5 | – |
| Medium Rain | `Weather_MediumRain` (COZY Light Rain) | Windy | Light Rain | RainMedium 0.8 | 2 | 0.3 | – |
| Heavy Rain | `Weather_HeavyRain` (COZY Thunder Storm) | Gale | Heavy Rain | RainHeavy 1 | 2.5 | 0.1 | yes |
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
    - Precipitation, Climate, Wind and Event FX: they do nothing without their COZY modules.
  - Kept: clouds, fog, colour filter and the VFX Graph rain and ground-fog FX, which stay vendor assets referenced by the copies.
  - `WeatherSettings` keeps each profile as a plain `ScriptableObject`, so only `CozyBridge` references COZY types.
- **Rain without FX blocking.**
  - COZY's VFX Graph rain finds the ground in the depth texture of its FX-blocking camera. That camera is off here, because it renders the scene a second time every frame.
  - COZY only sets the rain's range values (`CZY_RenderDistance`, `CZY_DepthTextureHeight`, the depth texture) while blocking is on. Without them no rain shows, and the compute shaders log "CZY_FXBlockDepthTex is not set".
  - So `CozyBridge.SetWeather` sets them itself, with a black depth texture (nothing blocks). Result: **rain also falls inside the cabin and under the porch.**
  - Fixing that means turning on FX blocking with a depth-only renderer (`FXBlockingRendererIndex`). Measure it first; it is the user's call.
- **Wind** (`WeatherWind` on `Environment/Wind/Weather Wind`, yaw 30) is the only writer of the foliage wind:
  - the Raygeas trees and grass values (`Raygeas*`) and the HIGHLANDS / shared Raygeas plant values (`RAYGlobal*`): the three Raygeas wind drivers' tuned values (copied into its fields), × strength (forces) and × speed (speeds), with the direction from its forward;
  - BK flutter: `BKFoliageGlobals.SetWindScale`;
  - Stylized Grass: its `WindController` on the same object reads the `WindZone` there (Main = strength, Turbulence = gust) and multiplies every grass material's own wind. At 1 / 1, facing yaw 30 like the materials, it computes the same sway as the materials alone.
  - The Raygeas drivers stay in the scene, switched off.
  - COZY's Wind module is not used: with it, COZY's Pure Nature integration (`COZY_BK`) would overwrite the BK values every frame.
- **Blend.**
  - A change blends every value (wind, fog, nature volume, bed volumes, lightning) from what is shown to the target over `TransitionSeconds` (20 game seconds), and COZY blends its profiles over the same time.
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
| A weather's wind, fog, birds, rain sound, lightning | `Data/Environment/Weather/Weather.asset` → Skies |
| Wind strength / speed / gusts / wind sound per level | `Weather.asset` → Winds |
| Blend time, rain lean, lightning interval / thunder delay | `Weather.asset` (top) |
| Clouds, rain particles, colour filter, COZY fog of a weather | the copy in `Data/Environment/Cozy/Weather/` (never COZY's own profiles) |
| Today's tuned sway (Breezy) | `WeatherWind` fields on `Environment/Wind/Weather Wind`, BK flutter on `BK Foliage Globals`, the grass materials' wind |
| Flash look | `Data/Environment/Weather/PP_Lightning.asset`, the curve on `Lightning Flash` |
| Turn weather off | `Weather.asset` → Enabled (the scene keeps COZY's current profile and the tuned wind) |

Console: `weather.show`, `weather.set <sky> [wind] [seconds]`, `weather.wind <Calm/Breezy/Windy/Gale/auto>`, `weather.strike` ([CONSOLE.md](CONSOLE.md)). For an edit-mode look at COZY's sky alone: the user-scope `CozyKit.SetWeather`.

## Setup script

`AgentScripts/Weather/WeatherSetup.cs` (run with `unity command script run --file AgentScripts/Weather/WeatherSetup.cs --entry WeatherSetup.<Entry>`). Every step is safe to re-run and saves only what it wrote:

- `Profiles()`: the COZY profile copies.
- `Audio()`: the five sound entities.
- `Settings(reset = false)`: the settings asset and the flash profile. It keeps tuned values unless `reset`.
- `Scene()`: WorldMap wiring. `Weather Wind` with the Raygeas values copied in and the drivers off, `Lightning Flash`, and the installer fields. Then it saves the scene.
- `All()`: everything above.

`Scene()` saves WorldMap, and that save also writes COZY's edit-mode churn: the `Sun Light` cookie cleared and its size set to 20000, the atmosphere module's parameter cache, and HUD `m_SizeDelta` values. Keep only your own hunks (`git diff`) and restore the rest from the committed scene.

## Cost

Editor, RTX 5070 Ti Laptop, 2026-11-08, `PerfSuite.Views` at 09:00 over the 9 recorded views, one pass each:

- Median frame time vs Partly Cloudy: Heavy Rain −0.3 ms, Fog −0.6 ms, i.e. no measurable cost.
- Draw calls: +0 to +6.
- **Not measured reliably.** The machine ran about 4 ms slower than the morning's COZY baseline in every weather. FrameTiming GPU time read 0 on most frames, so there is no trustworthy GPU number for the rain VFX or the ground fog. Re-measure with a clean editor before trusting the cost.

## Known limits

- Rain falls under roofs (above).
- Rain sound plays indoors at full volume (no indoor muffling).
- No wet surfaces, puddles or snow.
- Cloudy and Partly Cloudy look alike from the ground near the house.
- After a cut, the last weather's raindrops and ground fog fade out over a few seconds.
- No volume pass by ear on the new sounds.
