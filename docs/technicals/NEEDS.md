# Needs

The player's survival meters beside health and stamina: **Hunger**, **Thirst** and **Sanity**. Code in `_Core/PlayerSystem/Needs/` (namespace `MB13.Aipocalypse.Core.PlayerSystem.Needs`), data in `Data/Player/PlayerNeedsSettings.asset`, script map in [CODE_STRUCTURE.md](CODE_STRUCTURE.md). Design: `docs/superpowers/specs/2026-11-28-survival-stats-design.md`; plan: `docs/superpowers/plans/2026-11-28-survival-stats.md`.

Nothing refills hunger and thirst yet except the cheats: food and drink come later. Sanity recovers by a lit campfire ([CAMPFIRE.md](CAMPFIRE.md)) and near Loki; sleep restoring it comes later.

## Rules

| | Hunger | Thirst | Sanity |
| --- | --- | --- | --- |
| Max | 100 (`StatId.MaxHunger`) | 100 (`MaxThirst`) | 100 (`MaxSanity`) |
| Drain at rest | 100 a game day (24 h) | 100 in 16 game hours | lack of sleep and darkness (Sanity below) |
| Effort | +0.015 per stamina point spent | +0.025 per stamina point | — |
| Asleep | a third of the time drain | a third | — |
| Low (under 60%) | weak melee, shaky aim | slower stamina regen, dearer stamina costs | screen and sound effects, a lying HUD (Sanity below) |
| Well fed (75% and up) | health regenerates out of combat (+12 a game hour) | stamina regen × 1.15 | — |
| Empty | −10 health a game hour, down to 25% of max health | the same, the two add up | — |

- **A new game** starts every need full.
- **Tiers** (`NeedTier`): Good from 75%, Normal from 60%, Low from 30%, VeryLow above 0, Empty at exactly 0 (Sanity: Good from 60%, Normal 30%, Low 10%), so a tier changes where its ring changes colour (user 2026-11-28; the ring's red under 10% is still VeryLow for hunger and thirst). Each tier is a row of stat changes (`StatChange`) and a health rate per game hour.

| Tier | Hunger | Thirst |
| --- | --- | --- |
| Low | melee × 0.85, held-item sway × 1.25 | stamina regen × 0.8; sprint, jump, walk and attack stamina costs × 1.15 |
| VeryLow | × 0.65, × 1.6 | × 0.6; costs × 1.35 |
| Empty | × 0.5, × 2; −10 health / h | × 0.4; costs × 1.5; −10 health / h |

- **Never kills.** An empty need drains health only down to `HealthFloor` (25%) of max health; a player already below it isn't touched.
- **Out of combat** means `OutOfCombatSeconds` (10 real seconds) since the last hit. Healing (well fed, `StatId.HealthRegen`) waits for it; the empty drain doesn't.
- **Tier changes ease in** over `EaseTime` (0.35 s), like low-stamina fatigue, so nothing snaps. `NeedConditions.Changed` fires on the crossing and once more when the ease ends, so stats read only on `Changed` (maxes, weight limits) pick up the new row.
- **After a death** each need under `StatId.DeathNeedFloor` (base 50%) is raised to it; higher ones are kept. Dying never refills.
- **Game time:** needs drain by the game hours the world clock passes (`WorldClock.HoursPerSecond`, a sleep skip included). A clock jump (`clock.set`, the time lost on death) doesn't drain them. A long step is cut into 0.02-hour steps, so a need emptying mid-skip starts its health drain there.

## One system for every stat

Every number an upgrade, a level or a buff may change is a `StatId`, read through the player's `IStatProvider`:

| Stat | Max | Drains slower | Recovers faster | Other |
| --- | --- | --- | --- | --- |
| Health | `MaxHealth` | — | `HealthRegen` (HP / game hour) | — |
| Stamina | `MaxStamina` | `SprintStaminaCost`, `WalkStaminaCost`, `JumpStaminaCost`, `AttackStaminaCostScale` | `StaminaRegen` | `FatiguePenaltyScale` |
| Hunger | `MaxHunger` | `HungerDrain` (scale) | — (food, later) | `HungerPenaltyScale` |
| Thirst | `MaxThirst` | `ThirstDrain` | — (drink, later) | `ThirstPenaltyScale` |
| Sanity | `MaxSanity` | `SanityDrain` | `SanityRecovery` | `SanityPenaltyScale` |
| Death | — | — | — | `DeathNeedFloor` |

- **To upgrade:** `StatModifiers.Set("upgrade.<name>", StatId.X, add, multiply)` (root, saved by `StatSaveSection`). Remove the source to undo it. `stat.set <stat> <multiply> [add]` does the same from the console.
- **A penalty scale of 0** switches that need's low-tier penalties off; its well-fed bonus stays.
- **Stat layers**, bottom to top: `StatModifiers` (upgrades, skills, gear) → `PlayerEffects` (consumable buffs) → `NeedConditions` (the needs' active tiers). Everything the player builds after the needs reads the top layer.
- **Every meter** (`Health`, `Stamina`, each `Need`) is an `IMeter` (`Current`, `Max`, `Normalized`, `Set`), reached by id with `IPlayer.Meter(PlayerMeterId)`. Cheats and the HUD go through it, so a new meter needs no new cheat.
- **Other drains** (an enemy's scream later): `PlayerNeeds.Drain(PlayerMeterId, amount, source)`, scaled by the need's drain stat.

## Sanity

Starts full and drains (`SanityExposure`, settings in `PlayerNeedsSettings.SanityRules`, rates per game hour; Sanity's own `NeedSettings.DrainPerHour` stays 0).

| Source | Rate | When |
| --- | --- | --- |
| Lack of sleep | 100 over 10 h (10 / h, from the base max): empty at 18 h awake | after `AwakeGraceHours` (8, user 2026-11-28) awake, day and night, anywhere |
| Darkness | up to 25 / h (`DarknessFullPerHour`), × `DarknessByDistance` (linear) of the flat distance outside the nearest `SafeLight` over `FullDrainDistance` (150 m), × the darkness (0 day → 1 night, the player's `TimeOfDayWeight`) | outside every light; × `ComfortDrainScale` (0.6) near Loki |
| In a light | +3.3 / h (`LightRecoveryPerHour`) | inside a `SafeLight`'s radius, day or night |
| Special enemies | `PlayerNeeds.Drain(PlayerMeterId.Sanity, amount, source)` | not used by any enemy yet |
| Petting Loki | +5 (`DogSettings.PetSanity`), once per 180 s | `PlayerComforted` |

- Drains are scaled by `StatId.SanityDrain`, recovery (light, comfort) by `StatId.SanityRecovery`. `Frozen` stops the drains, not the recovery.
- **Asleep:** sanity doesn't change (sleep restoring it comes later). A sleep's end (`SleepStage.Waking`) resets the hours awake (`PlayerNeeds.OnWoke`).
- Late at night by the fire the lack-of-sleep drain (10) outweighs the fire (3.3): staying up never replaces sleep.
- **Lights:** `SafeLight` (a component with a flat radius, gizmo when selected), collected into `NeedsInstaller` on scene save / Play. In WorldMap: the yard campfire's (22.5 m while lit, shrinking with the fuel below 25 %, off while unlit: an unlit fire keeps nobody safe; [CAMPFIRE.md](CAMPFIRE.md)). The radius is flat distance and walls don't block it: the house counts as in the fire's light. No light in a scene = dark everywhere at night.
- **Comfort sources:** `IComfortSource` in the scene's `ComfortSources`; the dog registers itself (DOGS.md).

### What low sanity shows

| Sanity | Screen look (`SanityEffectSettings` bands) | SC backend adds |
| --- | --- | --- |
| 60% and up | — | — |
| 60 → 30% | `SanityUneasy` fades in: vignette 0.3, saturation −15 | — |
| 30 → 10% | `SanityLow`: vignette 0.38, saturation −40, grain 0.3 | colour split (offset 0.4), slow pulse |
| 10 → 0% | `SanityCritical`: vignette 0.45, saturation −70, grain 0.45, chromatic aberration | colour split (0.9) and double vision, pulsing |

- **One seam, swappable tools (user):** game code asks `ScreenLookService.Request(ScreenLookId, strength)`; the backend `ScreenLookSettings.Backend` names draws it. `ScreenLookBackendAsset` is the factory (`Create(parent)` → `IScreenLookBackend`). `VolumeLookBackendAsset` serves every volume-based tool (one global volume per look, weight = strength × pulse, priority 200 + the look's number, off at 0): `ScLooks` (SC Screen Effects + URP, in use) and `UrpLooks` (URP only) are two assets of it. A tool that isn't volume-based is a new `ScreenLookBackendAsset` subclass. No backend or `Enabled` off: every look is 0.
- **Sounds** (`SanityEffectSettings`; the drone starts silent and its volume is sent only when it moved 0.02, `VolumeStep`, because BroAudio restarts a fade on every `SetVolume`; Player library, Gamemaster clips not yet heard by a person): `Sanity_Drone` (`cinematic_LowDrone1`, a loop at the Low look's strength × 0.5, paused with the game), `Sanity_Whisper` (12 `ghost_witch_voice_hiss` clips, every 6–14 s below 60%, louder lower), the low-health `Heartbeat` below 30% (every 1.1 s, faster towards empty; it can overlap the low-health heartbeat).
- **The HUD lies** (`SanityGlitch`, hash-seeded so every view agrees): below `LiesFrom` (30%) the clock shows a wrong time for at most 0.6 s in some 8 s slots and the health / stamina bars and need rings flicker (alpha ≥ 0.35, values stay true); below `BlipsFrom` (10%) the minimap adds up to 3 fake enemy dots that move every 2–4 s, and the compass letters swap (N↔S, E↔W) for at most a second in some 10 s slots. Health, stamina, ammo and the need values never show a wrong number.
- `SanityPresenter` (NeedsInstaller) eases the three strengths over `EaseTime` (1 s) from the player's sanity share; `ScreenLookInstaller` (WorldMap's scope) registers the service. Data: `Data/Graphics/ScreenLooks/` (settings, two backends, seven profiles: six sanity ones and `Look_LeftLight`), built by `AgentScripts/Graphics/ScreenLookSetup.Build` (also SC's two renderer features on `PC_Renderer`).
- **Leaving the light** (2026-11-29, user: you couldn't see where safety ends): stepping out of every `SafeLight` while darkness is over 0.5 plays `Sanity_LeftLight` (a wind gust: PlaceHolder Inc `Wind Gust 2-5`, `1-5`, `2-1`, 2D, not heard by a person yet) and the `LeftLight` screen look (`Look_LeftLight`: cold blue-black vignette 0.42, −0.4 EV, one profile in both backends), which rises over 0.4 s and fades over 3 s. Not on the first look (a load, a respawn), not again within 10 s (walking along the edge); it also fires when the light leaves the player (a fire burning out or shrinking). `LightEdgeCue` (plain C#, `LightEdgeCueTests`) decides; `SanityPresenter` plays it from `NeedsTicker.Exposure`; tuning `SanityEffectSettings.LeftLight*`. Setup: `NeedsHudSetup.LeftLightAudio`, `ScreenLookSetup.LeftLight`.
- Cost (2026-11-28, `PerfBuild` development player, 1080p windowed, WorldMap perf pose, RTX 5070 Ti laptop): sanity 0% vs 100% measured the same, median frame 5.27 vs 5.39 ms, GPU 4.67 vs 5.11 ms, no GC, no hitches, no sanity marker in the top lists. The display was off, so the 0% look wasn't seen on screen: re-check once with the screen on.

## Carried weight

Items already weigh something (EQUIPMENT.md); now the weight slows the player (`CarryLoad`, settings in `PlayerMovementTuning.Load`).

| Load | Kg (base) | Speed | Stamina costs | Gauge |
| --- | --- | --- | --- | --- |
| Light | under 80% of Comfortable | full | × 1 | white |
| NearComfortable | 80% of Comfortable up to it | full | × 1 | yellow |
| Heavy | Comfortable to Max | linear to × 0.6 at Max | linear to × 1.5 | orange |
| Overloaded | Max and above | × 0.15 (a crawl), no sprint, no jump | × 1.5 | red |

- **Comfortable** = `StatId.ComfortableWeight` over the inventory's limit (25 kg + the worn bag's `ExtraWeight`); **Max** = Comfortable + `StatId.WeightMargin` (base 10 kg). Upgrades or a better bag raise them later.
- The speed scale applies after the motor's slowdown floor (`MinSpeedShare`), so a heavy pack always slows; it scales sprint, walk and jump stamina costs. Never frozen, and a pickup is never refused for weight.
- The crawl (walking × `OverloadedSpeed`) is the slowest a loaded player moves: aiming, walking backward or a heavy landing can't stack under it (`PlayerMotor.LoadedSpeed`), but the footing (wading, mud, `GroundMultiplier`) slows the crawl itself; a gait already slower once loaded (a loaded crouch) keeps its own speed.
- **Entering Overloaded** shows "Too heavy" on the pickup feed's warning line (UI table `inventory.too_heavy`) and plays `InventoryStyle.OverloadedSound` (`Load_Overloaded`: 8 random Gamemaster heavy gear foley clips, Player library). Leaving it says nothing; a load or respawn already overloaded doesn't warn. While overloaded, each fresh sprint press or jump press (`PlayerMotor.LoadRefused`, counted in `IPlayer.LoadRefusals`) shows it again, with `InventoryStyle.RefusedSound` unless the line is still up.
- **HUD:** a bag bottom right under the pickup feed that fills its body (not the handle) in the load's colour, a notch at Comfortable and "kg / max kg" under it ([UI.md → Bag gauge](UI.md#bag-gauge)); the inventory screen's weight bar uses the same colours.
- `Player.UpdateLoad` re-evaluates on `Inventory.Changed` and on stat changes, then sets `PlayerMotor.LoadSpeedMultiplier`, `LoadCostMultiplier` and `Overloaded`. `IPlayer.Load` holds the result for the HUD; `cheat.carry [kg]` adds test weight.

## HUD

Three rings above the health and stamina bars (icon inside a ring that shows the share of the max, coloured by that share: green above 60%, yellow above 30%, orange above 10%, red below; dim while green, a pulse and a body sound on a worse tier): [UI.md → Need rings](UI.md#need-rings). Only thirst has a body sound so far (a cough); no imported pack has a stomach growl.

## Turning it off

- No `NeedsInstaller` on the scene's `GameLifetimeScope`: the needs never drain (the meters still exist).
- `PlayerTuning.Needs` empty: the player has no needs at all (`IPlayer.Needs` is null).
- `NeedSettings.Enabled` off for one need: it stays where it is and its tiers apply nothing.

## Saved

`PlayerSaveSection` saves the three needs and the hours awake (`PlayerSave.HasNeeds`). A save from before them loads with full needs. Stat modifiers load first (`SaveOrder.Stats`), so a value above the base max (an upgrade) isn't clamped.

## Wiring

- `Player` builds `PlayerNeeds` when `PlayerTuning.Needs` is set, over `PlayerEffects`, and hands `PlayerNeeds.Stats` (the `NeedConditions` layer) to everything built after it. It forwards hits (`OnHurt`), revives (`ApplyDeathFloor`), stat changes (maxes) and ticks the real-time part (`Tick`: easing, the out-of-combat timer).
- `NeedsInstaller` (a `SceneInstaller`, on WorldMap's `GameLifetimeScope`) registers `ComfortSources` and `NeedsTicker` (entry point, with the collected `SafeLight`s), which calls `PlayerNeeds.Advance(game hours, asleep, SanityInput)` each frame, resets the hours awake when a sleep ends and adds `PlayerComforted` sanity. `WorldClock` and `SleepService` are optional (`TryResolve`): without a clock nothing drains.
- Setup: `unity command script run --file AgentScripts/Needs/NeedsSetup.cs --entry NeedsSetup.Settings` (the asset; `ResetSettings` rewrites the starting values), then `NeedsSetup.Wire` (PlayerTuning, the installer on the open WorldMap); the campfire's `SafeLight` comes with its prefab (CAMPFIRE.md).

## Testing

| Test | Covers |
| --- | --- |
| `NeedTests` | tier picking (Empty at 0 only), `TierChanged` only on a crossing, max keeps the share, clamping |
| `NeedConditionsTests` | tier rows over the layer below, stacking with buffs, penalty scale 0, easing, `Changed` only on a tier change |
| `PlayerNeedsTests` | drain rates, effort, sleep, upgrades, health floor, out-of-combat healing, death floor, freeze, the drain hook, one long step = many short ones, every `PlayerMeterId`, the asset's tiers |
| `SanityExposureTests` | lack of sleep (grace, empty at 18 h), darkness by distance and only at night, faster than lack of sleep, light recovery, comfort |
| `ScreenLookTests` | the service reaches the backend, no backend / off is safe, the sanity bands, both backend assets have a profile per look |
| `SanityGlitchTests` | clock lies only below the threshold and ≤ 0.6 s, fake blips ≤ 3 inside the disc, compass swap ≤ 1 s, flicker never under 0.35 |
| `SanityWorldTests` | `SafeLight.DistanceOutside` (inside, nearest edge, flat, none), `ComfortSources.Near`, `Comfort` scaled by recovery |
| `PlayerNeedsSaveTests` | an old save keeps full needs, the round trip, an upgraded max isn't clamped |
| `PlayerMeterTests` | `StatId` numbers stay put, `Stamina` / `Health` as `IMeter`, setting stamina to 0 exhausts and winds |
| `CarryLoadTests` | load states and scales between the limits, the crawl, upgrades moving the limits, off |
| `PlayerMotorTests` (6 cases + `LoadedSpeed`) | overloaded: crawl below the slowdown floor, no sprint or jump, stacked slowdowns never under the crawl, the footing slows the crawl, each fresh sprint or jump press refused once (none when not overloaded); a heavy load slows the walk |
| `NeedsHudPrefabTests` | rings, bag and feed wiring in HUD.prefab, ring colour bands, rings dim only while green, tiers and sanity looks change on the colour edges (asset and code defaults), the bag's body range, the overloaded sound, no tier row × 0, Inspector-added rows × 1 |

Cheats ([CONSOLE.md](CONSOLE.md)): `cheat.meter <meter|all> [percent]`, `cheat.needs`, `cheat.needsfreeze [on/off]`, `cheat.awake [hours]`, `cheat.drain <need> <amount>`, `cheat.carry [kg]`.

Play probe: `AgentScripts/Needs/NeedsProbe.cs` (`NeedsProbe.Run "<commands>"` runs console commands and prints every meter).
