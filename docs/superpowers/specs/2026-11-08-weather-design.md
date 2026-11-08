# Weather, phase A: sky weathers and wind you can switch on — design

Status: approved in conversation (user, 2026-11-08), written up for review. Phase A is looks and sound only: weather changes when the console asks. When it changes on its own (a daily roll, a drone forecast) and what it does to gameplay (vision, fishing, sound masking) are later phases. They come from `docs/gdd/` and need the user's confirmation first. Numbers here are starting values in a settings asset, not design canon.

## 1. Goal

WorldMap can show 8 sky weathers, each combined with any of 4 wind levels:

- the sky through COZY;
- the sway of trees and grass through our own wind driver;
- rain, wind and thunder sound through BroAudio.

Weather changes come only from the console in this phase, and they blend over a transition time. The service is built so a later schedule, the save system and gameplay rules can drive and read it without rewrites.

## 2. Decisions

| Topic | Decision |
| --- | --- |
| Model | Two layers. **Sky weather** (one at a time): Clear, Partly Cloudy, Cloudy, Overcast, Light Rain, Medium Rain, Heavy Rain (storm), Fog. **Wind level**: Calm, Breezy, Windy, Gale. Each sky weather has a default wind level; an override replaces it until it is cleared. "Windy" is a wind level, not a weather (the user's "medium windy" = Clear or Partly Cloudy + Windy) |
| Sky weathers never combine | They are steps on one scale (cloud cover → rain), plus Fog. "Sunny + partly cloudy" is Partly Cloudy. Named Clear, not Sunny, because it is also the clear night sky |
| Contradictions | Some combos contradict, such as Fog + Gale. The later rules phase may block them; in phase A the console accepts any combo |
| Who decides | Ours: `WeatherService`. COZY only draws (DAYNIGHT.md, skill `tools-unity-cozy`). Only `CozyBridge` touches COZY |
| What starts it | The console (`weather …`). The scene starts on Partly Cloudy + Breezy, which is today's look. No schedule yet |
| Breezy = today | Today's tuned sway (Raygeas drivers, BK flutter, Stylized Grass material wind) is the Breezy level, so the default scene doesn't change |
| Audio | Through `AudioService` only, never COZY's audio or thunder. Clips from ithappy Nature Sound FX (ASSETS.md: Should) |
| New SoundIDs | `RainLight`, `RainMedium`, `RainHeavy`, `Thunder`, `WindStrong` (approved with the design; `WindBreezy` dropped in the build, see §8) |
| Raygeas wind drivers | The three under `Environment/Wind` are switched off; our wind driver writes their shader values instead (approved with the design) |
| COZY Wind module | Not added: its Pure Nature integration (`COZY_BK`) would overwrite our BK foliage values every frame |
| Rain under roofs | COZY's FX blocking stays off: it renders an extra camera every frame through our main renderer. If rain shows inside the cabin or under the porch, the user decides between accepting it and a depth-only blocking renderer (measured first) |
| Wet surfaces, puddles, snow, glitch weather, Blood Moon, indoor muffling | Out of scope |

## 3. Weathers and wind levels

| Sky weather | COZY profile (our copy of) | Default wind | Rain particles (COZY VFX Graph FX) | Beds | Unity fog density × | Nature ambience × | Lightning |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Clear | Clear | Calm | – | – | 1 | 1 | – |
| Partly Cloudy | Partly Cloudy | Breezy | – | – | 1 | 1 | – |
| Cloudy | Mostly Cloudy | Breezy | – | – | 1.1 | 1 | – |
| Overcast | Overcast | Breezy | – | – | 1.3 | 0.8 | – |
| Light Rain | Light Rain, rain swapped to Stylized Drizzle | Breezy | Drizzle | RainLight | 1.5 | 0.5 | – |
| Medium Rain | Light Rain | Windy | Stylized Light Rain | RainMedium | 2 | 0.3 | – |
| Heavy Rain | Thunder Storm | Gale | Stylized Heavy Rain | RainHeavy | 2.5 | 0.1 | yes |
| Fog | Dense Fog | Calm | – | – | 4 | 0.7 | – |

| Wind level | Sway × (today = 1) | Gust × | Bed |
| --- | --- | --- | --- |
| Calm | 0.4 | 0.3 | – |
| Breezy | 1 | 1 | WindBreezy, quiet |
| Windy | 1.8 | 1.8 | WindStrong |
| Gale | 2.8 | 3 | WindStrong, loud |

**"Nature ambience ×"** scales the existing time-of-day ambience, so birds go quiet in rain. **"Unity fog density ×"** applies in the Unity fog mode. In the COZY fog mode, COZY's own fog in each profile does that job. The Kronnect fog modes keep their fixed comparison set-ups.

## 4. Architecture

```text
Console (WeatherCommands, cheat builds)
        │ Set(sky, wind?) / Wind(level?) / Show
        ▼
WeatherService (plain C#, scene: EnvironmentInstaller, Enabled flag in WeatherSettings)
  WeatherBlend  ── pure: from → to, progress over TransitionSeconds (scaled time), blended values
        │ each frame while blending, else on change
        ├─► CozyBridge.SetWeather(profile, seconds)          COZY draws clouds, rain, filter, its fog
        ├─► WeatherWind.Set(sway, gust)                      trees, grass, BK flutter, rain slant
        ├─► EnvironmentDirector.WeatherFogScale              Unity fog density
        ├─► AmbienceService.NatureVolume                     time-of-day beds and calls
        ├─► WeatherAudio (beds by SoundID, thunder + flash)  AudioService
        └─► publishes WeatherChanged (MessagePipe)            no subscriber yet; later rules and HUD
```

### 4.1 Data: `WeatherSettings` (`Data/Environment/Weather/Weather.asset`)

- `Enabled`, `StartSky`, `TransitionSeconds` (scaled seconds, 20), `BedFade`.
- `Dictionary<SkyWeather, SkyWeatherSettings>` and `Dictionary<WindLevel, WindLevelSettings>`, using Unity 6.6's serialized dictionaries.
- `SkyWeatherSettings`: the COZY `WeatherProfile`, `DefaultWind`, `FogDensityScale`, `NatureVolume`, beds (`SoundID` + volume), and lightning (on/off plus the real-seconds interval range).
- `WindLevelSettings`: `Sway`, `Gust`, bed (`SoundID` + volume).
- Enums `SkyWeather` and `WindLevel` in `_Core/EnvironmentSystem/Weather/`.

### 4.2 COZY profile copies (`Data/Environment/Cozy/Weather/`)

- One copy per sky weather, made from COZY's shipped profiles, which stay untouched.
- Removed from each copy: Audio FX and Thunder FX (COZY's thunder creates and destroys an object per strike and plays sound outside our mixer).
- Kept: the cloud, fog, filter and VFX Graph rain FX. Those FX profiles stay vendor assets, referenced but not edited. Precipitation, Climate, Wind and Event FX do nothing without their modules; they are removed so each copy shows what it actually does.
- Medium Rain gets its own copy of Light Rain. Light Rain points at COZY's Drizzle FX.
- Made by an `AgentScripts/Weather/WeatherSetup.cs` step, so they can be re-made after a COZY update.

### 4.3 `WeatherService`

- **API:** `Sky`, `Wind` (the effective level), `WindOverride` (nullable), `Set(SkyWeather, WindLevel? wind = null, float? seconds = null)`, `SetWind(WindLevel?)`. The state for saves is `WeatherState { Sky, WindOverride }`, read through `State`, with `Restore(WeatherState)` cutting to it without a transition.
- **Ticks** as an `ITickable`. It reads `TimeService` so a paused game holds the blend; COZY's own transition also runs on scaled time.
- **Blend:** `WeatherBlend` (plain class, unit-tested) holds the start and target values and the progress. It returns the sway, gust, fog scale, nature volume and per-SoundID bed volumes at the current progress. A change during a blend starts from the current blended values, so nothing jumps.
- **Without parts:** without COZY (bridge missing or not ready) the sky stays as it is, while wind, fog and sound still work. Without `WeatherSettings`, or with `Enabled` off, nothing is registered.

### 4.4 `CozyBridge.SetWeather(WeatherProfile, float seconds)`

- Sets `ChangeViaScripting` on the weather module (already the scene setting) and calls `SetProfile(profile, seconds)`.
- If COZY's VFX rain needs `CZY_WindDirection` to slant, the bridge writes it from the wind driver's direction and sway; to be checked in the shader before building.
- The bridge stays the only code that references `DistantLands.Cozy`. The director exposes its bridge (`EnvironmentDirector.Cozy`) for the service.

### 4.5 `WeatherWind` (scene component on `Environment/Wind`)

- The single writer of these shader values: the 7 Raygeas globals, with base values copied from today's three drivers and direction yaw 30, scaled by sway and gust.
- BK flutter goes through `BKFoliageGlobals`, which stays the only writer of BK values. It gains `SetWindScale(power, speed)` on top of its tuned values.
- Stylized Grass: a vendor `WindController` plus a `WindZone` on the same object. Our component sets `windMain` and `windTurbulence`, and the controller multiplies every grass material's own wind by them. At Breezy the grass must look like today; that gets checked against today in screenshots and motion before the Raygeas drivers are switched off.
- Leaf, dandelion and other particles, and water waves, are out of scope.

### 4.6 Audio

- **`WeatherAudio`** (owned by the service): one kept player per distinct bed `SoundID`, with volume = the blended sum, faded by `BedFade`. It starts and stops through `AudioService`.
- **Thunder:** while Heavy Rain's blend weight is ≥ 0.5, a strike comes every 8–25 real seconds (from the asset), skipped while paused or skipping time.
  - A strike first pulses a `Lightning Flash` URP Volume (post exposure, two quick flickers, about 0.25 s). The Volume is our own object and profile, separate from the director's grade volumes.
  - Then it plays `Thunder` after a 0.3–2.5 s delay.
- **Nature ambience:** `AmbienceService` gains `NatureVolume` (0–1) multiplying its beds and calls. It is set by the weather service and is 1 without weather.

### 4.7 Console (`WeatherCommands`, `[CommandGroup("weather")]`, cheat builds only)

- `weather.show`: the sky, the effective wind, whether it is an override, and the blend progress.
- `weather.set <sky> [wind]` and `weather.set <sky> <wind> <seconds>`.
- `weather.wind <level>` and `weather.wind auto` (clears the override).

The editor preview without Play is COZY-only, through the kit's `CozyKit.SetWeather`.

### 4.8 Event

- `WeatherChanged { SkyWeather Sky; WindLevel Wind; float Seconds }`, published on every `Set` or `SetWind` that changes something.
- Registered with the other events following `aipocalypse-architecture`.

## 5. Files

| Kind | Path |
| --- | --- |
| New code | `_Core/EnvironmentSystem/Weather/`: `SkyWeather.cs`, `WindLevel.cs`, `WeatherSettings.cs`, `WeatherService.cs`, `WeatherBlend.cs`, `WeatherAudio.cs`, `WeatherWind.cs`, `WeatherCommands.cs`, `WeatherChanged.cs` |
| Changed code | `EnvironmentInstaller` (settings, wind and flash fields, registrations), `CozyBridge` (`SetWeather`), `EnvironmentDirector` (`Cozy`, `WeatherFogScale`), `AmbienceService` (`NatureVolume`), `BKFoliageGlobals` (`SetWindScale`) |
| Data | `Data/Environment/Weather/Weather.asset`, `Data/Environment/Cozy/Weather/*.asset` (8), the new BroAudio entities in `Data/Audio/` |
| Scene (WorldMap) | `Environment/Wind`: Raygeas drivers off, `WeatherWind` + `WindZone` + Stylized Grass `WindController`; a `Lightning Flash` volume; installer fields |
| Setup | `AgentScripts/Weather/WeatherSetup.cs` (profile copies, settings asset, scene wiring; safe to re-run) |
| Tests | `Tests/EditMode/WeatherTests.cs` |
| Docs | new `docs/technicals/WEATHER.md` (+ the doc list in AGENTS.md), DAYNIGHT.md (weather paragraph → link), ENVIRONMENT.md (`Environment/Wind`), CODE_STRUCTURE.md, ASSETS.md (ithappy in use), PLUGINS.md (COZY weather use, Stylized Grass `WindController`), CONSOLE.md, skill `tools-unity-cozy` §3 |

## 6. Testing and checks

- **EditMode tests:**
  - the settings have an entry for every `SkyWeather` and `WindLevel`, each with a COZY profile;
  - the profile copies carry no Audio or Thunder FX;
  - the default wind applies, an override wins, and `auto` restores the default;
  - the blend reaches the target at `TransitionSeconds` and holds while paused;
  - a change mid-blend starts from the blended values;
  - `Restore` cuts;
  - the state round-trips.
- **Play mode probe:** each sky weather at 10:00 and 22:00, with screenshots of the same view. Check that the rain shows, the sky darkens, Unity fog thickens for Fog, and nothing appears in the console.
- **Wind:** Breezy matches today for grass and trees before the Raygeas drivers go off, then Calm and Gale are visibly calmer and stronger.
- **Audio:** beds fade in and out with the blend, birds go quiet in rain, thunder follows a flash, and nothing plays while paused.
- **Rain indoors:** check the cabin and porch, and report to the user.
- **Benchmark:** `PerfSuite.Views` at the usual views, Partly Cloudy vs Heavy Rain vs Fog, against today's numbers; recorded in WEATHER.md.
- **Finish:** recompile clean, EditMode tests pass, `git status` shows only our files.

## 7. Risks

- COZY's VFX rain may expect the FX blocking texture; with blocking off it might show nothing or fall everywhere. Check first, and settle it before building on it.
- In the global-wind path, Stylized Grass may move differently from its per-material path (time offset, direction from the `WindZone`). The Breezy check catches it, and `WindController.windAmbientMultiplier` / `windGustMultiplier` tune it.
- COZY's storm filter darkens COZY's sun, so `Daylight` drops and lamps may switch on in a daytime storm. That is probably right, but it gets checked.

## 8. Changes during the build (2026-11-08)

- **Risk 1 confirmed and solved.** With FX blocking off, COZY sets none of its rain's range values, and no rain showed. `CozyBridge.SetWeather` now sets them, with a black depth texture: nothing blocks, so the rain also falls under roofs.
- **Risk 2 checked from the shader source.** At Main 1 / Turbulence 1, with the zone at yaw 30 (all 20 grass materials point there), Stylized Grass computes the same sway as the materials alone.
- **`WindBreezy` dropped.** The time-of-day ambience already plays a meadow wind bed, so Breezy adds no sound; Windy and Gale use `WindStrong`.
- **`WeatherChanged` is empty.** Per the architecture skill's "something changed" rule, subscribers read `WeatherService`.
- **The COZY profile field is a plain `ScriptableObject`.** That keeps `DistantLands.Cozy` out of everything but the bridge.
- **The wind level has three multipliers:** strength (amplitude), speed and gust, instead of sway and gust.
- **Added `weather.strike`** (a flash and its thunder now) for testing.
- **Every COZY weather profile copy also loses its Precipitation, Climate, Wind and Event FX** (inert without their modules) and its forecast links.
- **The cost could not be measured reliably** in this editor session (WEATHER.md → Cost).
