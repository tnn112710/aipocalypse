# Campfire

The yard campfire is a fire the player tends: wood goes in as fuel hours, a lit fire burns them, lights the yard and keeps the player sane within 30 m, and goes out when the fuel runs out. Design: [2026-11-28-campfire-design.md](../superpowers/specs/2026-11-28-campfire-design.md) (user, 2026-11-28). Code: `_Core/CampfireSystem/`; tuning: `Data/Campfire/CampfireSettings.asset`; content: `Content/Structures/Campfire/`, `Content/Items/Wood/`.

## How it plays

| You do | At | Result |
| --- | --- | --- |
| **E** / **hold E** | the wood pile | Take one log / all that fit (a pickup: feed line, sound). 8 logs, refilled each morning at Dawn: a stand-in for chopping |
| **E** | the fire, carrying wood | One log goes on (+2 h), when a whole log fits under the max. Adding wood doesn't light it |
| **E** | the fire, unlit, fuel > 0, no log fits or no wood | Light it (free) |
| **Hold E** | the fire, lit | Put it out; the fuel stays in the pit |
| Throw / drop wood (G, inventory Drop) | into the pit, or just short of it | Every whole log that fits burns; the rest lands as a pickup. A low, wide catch (3.4 × 0.8 m) takes logs landing within ~1.7 m of the pit: aimed at the pit, every throw from 2.5 and 3.5 m burned, and from 4.5 m all but aiming at the ground in front (2026-11-29, `CampfireProbe.ThrowG`). Without the installer the pit is a prop and the wood just lands |

- **Fuel** (`CampfireFuel`): game hours, 0 … `MaxFuel` (8 h = 4 logs of 2 h; the item's `MaterialDefinition.FuelHours`). A new game starts **full and unlit**. A lit fire burns 1 h per game hour: exactly the hours the clock moved, a sleep skip included (jumps and loads don't count). It never goes out by itself at dawn: it burns until the player puts it out or the fuel runs out (user).
- **Rain** burns faster: × 1.25 light, × 1.5 medium, × 2 heavy (`RainBurn`, keyed by `SkyWeather`; no weather service = × 1).
- **Strength** (0 … 1): full above `ShrinkBelow` (25 %) of the max, then the fuel share over it, 0 while unlit. The safe radius (`SafeRadius` 30 m × strength), the lights' intensity and range, and the flame size (`MinFlameScale` … 1) follow it, so a dying fire visibly shrinks first. The looks ease over `FadeSeconds` (1.2 s real) and hold while the game is paused; the safe radius follows the fuel at once.
- **A log going on flares the fire:** the lights × 1.6 (`FlareBoost`) and the flame's embers × 6 (`FlareEmbers`), settling over 0.8 s (`FlareSeconds`).
- **Logs in the pit:** four logs, one for every started quarter of the max (8 h: 4 logs, 4.5 h: 3, 1.2 h: 1, empty: none), lit or not, so a glance says how much is left.
- **Unlit = unsafe:** the pit's `SafeLight` is on only while lit. Sanity's two drains stack as before (NEEDS.md): lack of sleep always, darkness on top outside every lit fire.
- **The safe radius is flat distance:** walls don't block it, so inside the house (about 13 m from the fire) counts as in the light: home is safe while the fire burns (2026-11-29).
- **Leaving the light is cued:** stepping out of every safe light at night plays a wind gust and briefly darkens the screen edges (NEEDS.md → Sanity), also when the fire dies or shrinks past the player.
- **Prompts** carry the fuel: "Add Wood (3.0 / 8 h)", "Light Fire (6.0 / 8 h)", "Fire burning (7.0 / 8 h)" (nothing to do on a tap), "Fire out: needs wood", "Hold: Put Out Fire".

## Look and sound

- **Night** (user picked from captures): Night and Midnight looks' ambient × 0.3 and moon × 0.5, and their grade `PP_NightDark` (`PP_Night` − 0.5 EV); Blue hour half of it (ambient × 0.65, moon × 0.75, `PP_BlueHourDark` − 0.25 EV). Dusk unchanged. Baked into COZY with `CozySetup.Atmosphere` ([DAYNIGHT.md](DAYNIGHT.md)).
- **Flame:** Gabriel Aguiar `StylizedFire/vfx_Fire01_v1` (VFX Graph) under `Flame`; its colours are set on the instance (`FlamesColor` / `FlameStaticColor` (9, 2.9, 0.45), `AmbersColor` (10, 3.3, 0.5), HDR): the pack's own (63, 9.3, 3.3) washes out to pale peach under the night grade. It plays while lit and stops (particles die out) when out.
- **Light:** two shadowless point lights, warm (1, 0.62, 0.32): `Core Light` 1 m up (6, 8 m: the hot pool), `Fill Light` 6.5 m up (50, 42 m: from that height it lights the yard and the house front evenly, ~20 m). The core flickers (two noise octaves, down to 70 %, `FlickerAmount` / `FlickerSpeed`); the fill gets a quarter of that dip (`FillFlicker`), or the whole yard pulses like a strobe. The fill casts soft shadows (medium resolution tier): without them it lit the inside of the house through its walls. Only the home fire (the registry's first) keeps them, since each shadowed fill costs about 0.4 ms of GPU; any other fire's fill is unshadowed. The core stays unshadowed. Tuned from 22:00 captures: one light at ground level (the first try, 15 / 20 m) lit only ~5 m.
- **Sound** (library `Data/Audio/Campfire`, 3D, 2–28 m): `FireCrackle` loop while burning (volume × strength, `AudioService.StartAt`; paused while the game is), `FireIgnite`, `FireAddWood` (a log landing: PlaceHolder Inc `Wood 11-1…4`, picked by their waveform, not by ear), `FireSputter` (a dying fire: lit and below 25 %, every 4–9 s, `SputterEveryMin` / `Max`; PlaceHolder Inc `Sizzle` clips), `FirePutOut` (also when it burns out). Clips: Gamemaster Pro Sound Collection and PlaceHolder Inc (ASSETS.md).
- **HUD:** `HUD/SafeFrame/BottomLeft/FireBar` (`CampfireFuelHud`, a `StatBar` with `Data/UI/CampfireFuel.asset`: orange fill, fire icon, 262 px up above the dog's bar): the home fire's fuel / max in tenths of an hour (the style's `Decimals` 1, rounded up), shown when it's lit and under `WarnBelow` (35 %, a warning before the shrink; anywhere) or out while the player's darkness is over 0.5, they're within `FuelBarRange` (60 m) of it and they can do something (fuel in the pit, wood carried or in a pile); the warning colour below 25 % or while out ([UI.md](UI.md)).

## Prefabs and scene

- `Campfire.prefab`: root `Campfire`, `SafeLight`, `SaveId`, a solid capsule (0.85 m: nobody walks into the fire), a trigger sphere (1.05 m) and a low trigger box (3.4 × 0.8 m) that catch thrown wood, a circle `NavmeshCut` (1 m, so A* agents path around it; in play the nearest walkable node was 1.4 m from the centre). Children: `Pit` (HIGHLANDS `Camp/Bonfire`, ×1.3), `Stones` (9 Komorebi small stones), `Logs` (4 HIGHLANDS `Camp/Log_1` ×0.8 in a star, inner ends raised), `Flame`, `Core Light`, `Fill Light` (soft shadows).
- `WoodPile.prefab`: `WoodPile`, `SaveId`, a trigger box over `Model` (Lakefell `Environment/Log_2`, hidden while empty).
- WorldMap: `Environment/Landmarks/Home Base/Campfire` holds the campfire at yard-local (0, 6.5) (the middle of the yard in front of the door; it was at (3.5, 6.5), user 2026-11-28), three benches around it, two seat logs and the wood pile. `CampfireInstaller` on the `GameLifetimeScope` collects the fires and piles on save / Play. The map generator's Landmarks step places the same prefabs (ENVIRONMENT.md).

## Code

| Type | Does |
| --- | --- |
| `CampfireInstaller` (scene) | Collects, injects; registers `CampfireRegistry`, `CampfireTicker`, `CampfireSaveSection`, `CampfireCommands` (cheats). Without it the fire is a prop |
| `CampfireFuel` | The rules (plain C#): fuel, lit, strength, whole-unit adds, burning, going out |
| `Campfire` | Interaction, feeding, the looks, the sound, the safe light |
| `WoodPile` | The pile: one log on a tap, all on a hold, morning refill |
| `CampfireTicker` | Burns lit fires by the hours the clock moved since the last frame × rain; `TimeJumped` resets the baseline |
| `CampfireFuelHud` | The HUD fuel bar |
| `MaterialDefinition` (EquipmentSystem) | The wood item's type: `ItemCategory.Material`, carried, never held, `FuelHours` |

**Saved** (`campfires`, SAVE.md): each fire's fuel and lit, each pile's logs, by `SaveId`; restored silently (no ignite sound, the looks jump to the state).

**Console** ([CONSOLE.md](CONSOLE.md)): `campfire.status`, `campfire.fuel <hours>`, `campfire.light`, `campfire.out`, `campfire.pile <n>`; wood: `cheat.give wood 4`.

## Cost

Editor, RTX 5070 Ti laptop, 1080p Game view, 22:00 at the yard view, fire lit vs put out (alternating, 2 × 480 frames each, `CampfireProbe.Cost`):

| Fill light | GPU lit / out | Main thread lit / out |
| --- | --- | --- |
| No shadows (2026-11-28) | 4.49 / 4.26 ms (+0.22) | 11.47 / 10.69 ms (+0.78) |
| Soft shadows, medium tier (2026-11-29) | 4.86 / 4.25 ms (+0.61) | 11.86 / 11.59 ms (+0.27) |

The shadow costs about 0.4 ms of GPU here; each later fire with a shadowed fill pays it again. Editor CPU numbers are noisy and not split between the VFX update, the lights and the editor. Not measured: a build, an RTX 3060.

## Setup and checks

- `AgentScripts/Campfire/CampfireSetup.cs`: `Night` (looks and grades; then `CozySetup.Atmosphere`), `Wood` (item, strings, catalog), `All` (settings, sounds, prefabs), `Tune` (writes the light / flame numbers into the existing prefab and settings without rebuilding: a rebuild drops the scene instance's `SaveId`), `Pit` (adds the logs and the fill's shadows to the existing prefab, in place), `Cues` (the sounds, then the catch box and the sputter on the existing prefab), `Hud`, `Scene` (replaces Home Base/Campfire, adds the installer; refuses a dirty WorldMap).
- `AgentScripts/Campfire/CampfireProbe.cs` (Play in WorldMap): `Run` (console commands + state), `TakeAndTap`, `Hold`, `Throw`, `Navmesh`, `StandAt`, `View` (capture views), `Inside` (the player inside the house: does the fill light leak through the walls?), `Pile` (tap / hold), `Pause` / `Crackle`, `Flicker`, `FuelBar`, `Logs`, `ThrowG` (real G throws by distance and angle), `Flare`, `Sputter`, `Edge` (the leaving-the-light look), `Cost`.
- Tests: `CampfireTests` (wood item, fuel rules, the component, logs, pause, the installer guard, the flare, shadows only at home, the catch box, the ticker and jumps, the pile, save, console, the HUD bar and its tenths).

## Later (not built)

Wood chopping (the pile's refill goes), cooking on the fire, things thrown in for a buff or a colour or a summon (the same feed path: an entry per item), outer fire pits and player-built fires, a roof against rain, raiders putting the fire out, the fire as a beacon seen from the bridge, a `MaxFuel` upgrade.
