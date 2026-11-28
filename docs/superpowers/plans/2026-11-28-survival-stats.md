# Survival Stats Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Hunger, Thirst and Sanity meters, a carried-weight slowdown and their HUD, with every player stat (the five meters and the weight limits) changed only through the existing stat modifier layer, so upgrades, levels and buffs work the same way on all of them.

**Architecture:** One plain `Need` meter class is used three times, with its tiers as data. A tier is a row of stat changes. A per-player stat layer (`NeedConditions`, stacked over `PlayerEffects`) applies the active rows, so penalties reach movement, aim, stamina and melee through `IStatProvider` like every buff does today. Health, Stamina and the needs share one read/write interface (`IMeter`), reached by `PlayerMeterId`, so cheats, HUD and later scenarios treat all five alike. A scene installer ticks the needs on game time, so the mechanic is off in any scene without it. Sanity's screen look goes through a factory-made backend, so SC Screen Effects, URP's own effects or another tool can be swapped by one settings field.

**Tech Stack:** Unity 6000.6.7f1, URP 17.6, VContainer, MessagePipe, BroAudio (`AudioService`), uGUI with Layer Lab sprites, Unity Localization, SC Screen Effects 3.0.2 (URP volume components), NUnit EditMode tests, Unity CLI.

**Spec:** [docs/superpowers/specs/2026-11-28-survival-stats-design.md](../specs/2026-11-28-survival-stats-design.md)

## Global Constraints

- Follow AGENTS.md → Code style: `_camelCase` private fields, `[SerializeField] T _field;` never `[field: SerializeField]`, `sealed` leaf classes, no `private`, no `///` blocks, at most 2–3 `//` lines per class or method.
- **`StatId` is append-only.** Saved modifiers store it as an integer: new values go after `AimWhileFiringLevel` (14) and nothing is reordered.
- Every stat the spec lists as upgradeable is read through `IStatProvider.Get(StatId, baseValue)`. Nothing else changes a max, a drain or a recovery rate.
- Settings ScriptableObjects are read-only at runtime. Runtime switches (the freeze cheat) live on the runtime object, never on the asset.
- Game code reads input only through `InputService`; sound only through `AudioService` and `SoundID`s; cross-system events go through MessagePipe; no `X.Instance` singletons.
- Meters never kill: the health drain from an empty need stops at 25% of max health.
- Health, Stamina, ammo and the need gauges never show a wrong value. Only the clock and the minimap may lie.
- No player-facing text in code or assets: new strings go in `Assets/_Game/Data/Localization/Csv` with English only (read `docs/technicals/LOCALIZATION.md` first).
- Editor scripts save only what they wrote: `AssetDatabase.SaveAssetIfDirty(asset)`, never `AssetDatabase.SaveAssets()`. Check `git status` for unrelated files after each `script run`.
- Placeholder numbers below are the spec's starting values. They live in settings assets, never as constants.
- **Commits:** only when the user asks (AGENTS.md). Each task ends at a checkpoint where the work compiles, the tests pass and the result is reported.
- Editor commands, from the repo root in Git Bash:

  ```bash
  P="$(cygpath -w "$PWD")"
  unity command --project-path "$P" script recompile
  unity command --project-path "$P" script recompile_status    # poll until completed / up_to_date
  unity command --project-path "$P" console status
  unity command --project-path "$P" test run --mode editor     # check SceneManager.GetActiveScene().isDirty first
  ```

## Review Focus

1. **An old save without the new fields.** Loading a save from before this change must start the three meters at 100% and the hours awake at 0, never at 0% (JSON fills missing floats with 0). Test: Task 5, `PlayerSave_FromBeforeNeeds_StartsTheMetersFull`.
2. **An upgrade changing a max while the meter is partly full.** Raising `MaxHunger` keeps the same share, like `Health.SetMax`, and a load applies saved modifiers (order 90) before the player's meters (order 100), so a saved 130 of 150 isn't clamped to 100. Tests: Task 2, `SetMax_KeepsTheShare`; Task 5, `PlayerSave_RestoresAboveTheBaseMaxWhenAnUpgradeIsSaved`.
3. **Several sources stacking.** Hunger and Thirst both empty drain health together but still stop at the 25% floor; a buff and a tier penalty on the same stat multiply. Tests: Task 4, `EmptyHungerAndThirst_StopAtTheSameHealthFloor`; Task 3, `Get_StacksTierChangesOverTheLayerBelow`.
4. **Tier flapping.** A meter sitting on a threshold (50.0%) while effort drains and nothing refills must not raise `Changed` every frame. Test: Task 3, `Update_RaisesChangedOnlyWhenATierChanges`.
5. **A long time skip.** Sleeping eight hours advances needs in one large step: health drain and the darkness drain must clamp correctly and never go below the floor or under 0. Test: Task 4, `Advance_OneLargeStepMatchesManySmallSteps`.

---

## File map

| File | Responsibility |
| --- | --- |
| `_Core/_Common/Stats/IStatProvider.cs` (modify) | New `StatId` values (Task 1) |
| `_Core/_Common/Stats/StatChange.cs` (create) | One serialized stat change in a tier row |
| `_Core/_Common/Stats/IMeter.cs` (create) | Shared current / max / set interface for every meter |
| `_Core/_Common/Combat/Health.cs`, `_Core/PlayerSystem/Stamina.cs` (modify) | Implement `IMeter`; Stamina counts what was spent |
| `_Core/PlayerSystem/Needs/` (create) | `Need`, `NeedTier`, `NeedSettings`, `NeedConditions`, `PlayerNeeds`, `PlayerNeedsSettings`, `PlayerMeterId`, `NeedsInstaller`, `NeedsTicker`, `NeedsCommands`, later `SanityExposure`, `SafeLight`, `ComfortSources`, `SanityPresenter` |
| `_Core/PlayerSystem/CarryLoad.cs` (create) | Weight → speed, stamina cost and load state |
| `_Core/PlayerSystem/Player.cs`, `IPlayer.cs`, `PlayerMotor.cs`, `PlayerSaveSection.cs`, `Tuning/PlayerTuning.cs` (modify) | Wiring |
| `_Core/GraphicsSystem/ScreenLooks/` (create) | Screen-look service, backend interface, factory asset, volume backend, installer |
| `_Core/UI/Widgets/` (create `NeedRing`, `NeedRingStyle`, `BagGauge`, `BagGaugeStyle`) and `_Core/UI/Hud/` (create `PlayerNeedsHud`, `PlayerLoadHud`, `SanityGlitch`; modify `ClockHud`, `MinimapHud`, `PlayerVitalsHud`) | HUD |
| `_Core/DogSystem/DogManager.cs`, `DogSettings.cs` (modify) | Loki as a comfort source; petting comforts |
| `AgentScripts/Needs/NeedsSetup.cs`, `AgentScripts/Hud/NeedsHudSetup.cs`, `AgentScripts/Hud/LoadHudSetup.cs`, `AgentScripts/Graphics/ScreenLookSetup.cs` (create) | Assets, HUD nodes, sounds, scene wiring, re-runnable |
| `Assets/_Game/Data/Player/PlayerNeedsSettings.asset`, `Data/UI/*RingStyle.asset`, `Data/UI/BagGaugeStyle.asset`, `Data/Graphics/ScreenLooks/*` (create) | Data |
| `Assets/_Game/Tests/EditMode/` (create `NeedTests`, `NeedConditionsTests`, `PlayerNeedsTests`, `PlayerMeterTests`, `CarryLoadTests`, `SanityExposureTests`, `ScreenLookTests`, `SanityGlitchTests`, `NeedsHudPrefabTests`) | Tests |
| `docs/technicals/NEEDS.md` (create), `CODE_STRUCTURE.md`, `UI.md`, `EQUIPMENT.md`, `SAVE.md`, `CONSOLE.md`, `DOGS.md`, `PLUGINS.md`, `ASSETS.md`, `LOCALIZATION` CSVs, `AGENTS.md` doc list, `docs/ROADMAP.md` (modify) | Docs |

---

## Part 1: Hunger and Thirst

Ends with: both needs drain on game time, penalise and reward through stats, save, show as rings, and are set by cheats. Sanity exists as a third meter that only cheats change.

### Task 1: Stat groundwork

**Files:**
- Modify: `Assets/_Game/_Core/_Common/Stats/IStatProvider.cs`, `Assets/_Game/_Core/_Common/Combat/Health.cs`, `Assets/_Game/_Core/PlayerSystem/Stamina.cs`, `Assets/_Game/_Core/PlayerSystem/Player.cs` (melee line only)
- Create: `Assets/_Game/_Core/_Common/Stats/StatChange.cs`, `Assets/_Game/_Core/_Common/Stats/IMeter.cs`
- Test: `Assets/_Game/Tests/EditMode/PlayerMeterTests.cs`

**Interfaces:**
- Produces:
  - `StatId` appended, in this order: `MaxHunger, MaxThirst, MaxSanity, HungerDrain, ThirstDrain, SanityDrain, SanityRecovery, HungerPenaltyScale, ThirstPenaltyScale, HealthRegen, MeleePowerScale, ComfortableWeight, WeightMargin, DeathNeedFloor` (15–28). `…Drain`, `…Recovery`, `…PenaltyScale`, `MeleePowerScale` are scales (base 1); `HealthRegen` is HP per game hour (base 0); `ComfortableWeight` and `WeightMargin` are kg; `DeathNeedFloor` is a 0–1 share.
  - `[Serializable] public struct StatChange { public StatId Stat; public float Add; public float Multiply; }` with `Multiply` defaulting to 1 in a constructor `StatChange(StatId stat, float add = 0f, float multiply = 1f)`.
  - `public interface IMeter { float Current { get; } float Max { get; } float Normalized { get; } void Set(float current); }`
  - `Health : IMeter` (members exist). `Stamina : IMeter` with new `void Set(float current)` (clamped to 0..Max; leaving exhaustion when above the recover fraction) and `float TakeSpent()` (stamina spent since the last call, then resets to 0).
  - `Player`'s `IWeaponUser.MeleePower => Fatigue.MeleePower * _stats.GetScale(StatId.MeleePowerScale)`.

- [ ] **Step 1: Write the failing tests** in `PlayerMeterTests.cs`:

```csharp
[Test] public void StatId_KeepsTheValuesSavesAlreadyHold()
{
    Assert.AreEqual(14, (int)StatId.AimWhileFiringLevel);
    Assert.AreEqual(15, (int)StatId.MaxHunger);
    Assert.AreEqual(28, (int)StatId.DeathNeedFloor);
}
[Test] public void Stamina_SetClampsAndCountsAsAMeter()
{
    IMeter stamina = new Stamina(100f, 1f, 0.3f);
    stamina.Set(140f);  Assert.AreEqual(100f, stamina.Current);
    stamina.Set(-5f);   Assert.AreEqual(0f, stamina.Current);
}
[Test] public void Stamina_TakeSpentReturnsWhatWasSpentOnce()
{
    var stamina = new Stamina(100f, 1f, 0.3f);
    stamina.Spend(12f); stamina.Spend(3f);
    Assert.AreEqual(15f, stamina.TakeSpent(), 1e-4f);
    Assert.AreEqual(0f, stamina.TakeSpent());
}
```

- [ ] **Step 2: Recompile and run the tests; expect compile errors** (`StatId.MaxHunger`, `IMeter`, `TakeSpent` missing).
- [ ] **Step 3: Implement the interfaces above.** `TakeSpent` counts only what `Spend` actually removed (not more than the stamina left).
- [ ] **Step 4: Recompile, `console status` clean, run `test run --mode editor`: all pass** (existing `StatModifierTests` and `SaveTests` included).
- [ ] **Step 5: Checkpoint.** Add the new files to `CODE_STRUCTURE.md`.

### Task 2: The `Need` meter and its tiers

**Files:**
- Create: `Assets/_Game/_Core/PlayerSystem/Needs/Need.cs`, `NeedTier.cs`, `NeedSettings.cs`
- Test: `Assets/_Game/Tests/EditMode/NeedTests.cs`

**Interfaces:**
- Consumes: `StatChange`, `IMeter`, `StatId` (Task 1).
- Produces:
  - `public enum NeedSeverity { Good, Normal, Low, VeryLow, Empty }`
  - `[Serializable] public sealed class NeedTier { public NeedSeverity Severity; [Range(0,1)] public float From; public StatChange[] Changes; public float HealthPerHour; }`. `From` is the lowest share the tier covers. A tier with `Severity == Empty` applies only at exactly 0.
  - `[Serializable] public sealed class NeedSettings { public bool Enabled = true; public float BaseMax = 100f; public StatId MaxStat; public StatId DrainStat; public StatId PenaltyScaleStat; public float DrainPerHour; public float PerStaminaSpent; public NeedTier[] Tiers; }`
  - `public sealed class Need : IMeter` with `Need(NeedSettings settings, float max)`, `Current`, `Max`, `Normalized`, `Set(float)`, `Add(float delta)`, `SetMax(float max)` (keeps the share, same rule as `Health.SetMax`), `NeedTier Tier` (the active row, or null), `NeedSeverity Severity`, `event Action<NeedTier> TierChanged`. A new `Need` starts full.

- [ ] **Step 1: Write the failing tests** in `NeedTests.cs`, using a helper that builds the spec's Hunger tiers (`Good` from 0.75, `Normal` 0.5, `Low` 0.25, `VeryLow` 0.0001, `Empty` 0):

```csharp
[Test] public void New_StartsFull()                    { Assert.AreEqual(1f, Hunger().Normalized); }
[Test] public void Tier_PicksTheRowCoveringTheShare()
{
    var need = Hunger();
    need.Set(80f); Assert.AreEqual(NeedSeverity.Good, need.Severity);
    need.Set(50f); Assert.AreEqual(NeedSeverity.Normal, need.Severity);
    need.Set(49.9f); Assert.AreEqual(NeedSeverity.Low, need.Severity);
    need.Set(0.5f); Assert.AreEqual(NeedSeverity.VeryLow, need.Severity);
    need.Set(0f); Assert.AreEqual(NeedSeverity.Empty, need.Severity);
}
[Test] public void TierChanged_FiresOnlyOnACrossing()  // Set(70) then Set(65): 1 event (Good→Normal), not 2
[Test] public void SetMax_KeepsTheShare()               // Set(50) of 100, SetMax(150) → Current 75
[Test] public void Add_ClampsBetweenZeroAndMax()        // Add(-500) → 0; Add(500) → Max
```

- [ ] **Step 2: Run; expect compile errors.**
- [ ] **Step 3: Implement.** `Tier` is found by scanning the rows for the highest `From` at or below `Normalized`, with the `Empty` row reserved for 0; recomputed in `Set`/`Add`/`SetMax` only.
- [ ] **Step 4: Run tests: pass.**
- [ ] **Step 5: Checkpoint.**

### Task 3: `NeedConditions`, the tier stat layer

**Files:**
- Create: `Assets/_Game/_Core/PlayerSystem/Needs/NeedConditions.cs`
- Test: `Assets/_Game/Tests/EditMode/NeedConditionsTests.cs`

**Interfaces:**
- Consumes: `Need`, `NeedTier`, `StatChange` (Task 2), `IStatProvider`.
- Produces: `public sealed class NeedConditions : IStatProvider, IDisposable` with `NeedConditions(IStatProvider below, float easeTime)`, `void Track(Need need, StatId penaltyScaleStat)`, `void Tick(float deltaTime)`, `float Get(StatId, float)`, `event Action Changed`.
  - `Get` = the layer below's value, then for each tracked need's active tier, each `StatChange` on that stat: `(value + Add × s) × (1 + (Multiply − 1) × s)`, where `s` is the eased weight times `below.GetScale(penaltyScaleStat)`. A penalty scale of 0 switches that need's penalties off; it doesn't touch `Good`-tier bonuses (use `s = ease` for `Severity.Good`).
  - Eases between the old and new row over `easeTime` seconds (default 0.35, the fatigue value). `Changed` fires when a tier changes and when the layer below raises it, never from easing alone.

- [ ] **Step 1: Write the failing tests:**

```csharp
[Test] public void Get_AppliesTheActiveTiersChanges()        // Hunger Low: HeldItemSwayScale → 1.25 after easing (Tick 10 s)
[Test] public void Get_StacksTierChangesOverTheLayerBelow()  // below sets sway ×1.1; Low tier → 1.1 × 1.25
[Test] public void Get_PenaltyScaleZeroRemovesPenaltiesNotBonuses()
[Test] public void Get_EasesIntoANewTier()                   // right after Low: 1 < sway < 1.25; after 10 s: 1.25
[Test] public void Update_RaisesChangedOnlyWhenATierChanges()
{
    // need at 50.0 (Normal); 100 frames of Add(-0.0001f)/Add(+0.0001f) around the edge → Changed count ≤ number of crossings,
    // and 0 when nothing crosses; 100 Tick calls alone → 0
}
```

- [ ] **Step 2: Run; expect compile errors.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run tests: pass.**
- [ ] **Step 5: Checkpoint.**

### Task 4: `PlayerNeeds` rules and the settings asset

**Files:**
- Create: `Assets/_Game/_Core/PlayerSystem/Needs/PlayerNeeds.cs`, `PlayerNeedsSettings.cs`, `PlayerMeterId.cs`, `AgentScripts/Needs/NeedsSetup.cs`, `Assets/_Game/Data/Player/PlayerNeedsSettings.asset` (made by the setup script)
- Test: `Assets/_Game/Tests/EditMode/PlayerNeedsTests.cs`

**Interfaces:**
- Consumes: Tasks 1–3, `Health`, `Stamina`, `IStatProvider`.
- Produces:
  - `public enum PlayerMeterId { Health, Stamina, Hunger, Thirst, Sanity }`
  - `PlayerNeedsSettings : ScriptableObject` (`Create → Aipocalypse → Player → Needs Settings`): `NeedSettings Hunger, Thirst, Sanity`; `float AsleepDrainScale = 0.33f`; `float HealthFloor = 0.25f` (share of max health); `float OutOfCombatSeconds = 10f`; `float BaseDeathFloor = 0.5f`; `float EaseTime = 0.35f`. Read-only properties for each.
  - `public sealed class PlayerNeeds : IDisposable` with:
    - `PlayerNeeds(PlayerNeedsSettings settings, IStatProvider below, Health health, Stamina stamina)`; `IStatProvider Stats` (the `NeedConditions` layer to hand on); `Need Hunger, Thirst, Sanity`; `IMeter Meter(PlayerMeterId id)` (Health and Stamina included); `bool Frozen { get; set; }`; `float HoursAwake { get; set; }`
    - `void Advance(float gameHours, bool asleep)`: each enabled need loses `DrainPerHour × Get(DrainStat) × gameHours × (asleep ? AsleepDrainScale : 1)` plus `PerStaminaSpent × stamina.TakeSpent()`; then health: the sum of the active tiers' `HealthPerHour × gameHours` (negative never takes health below `HealthFloor × Max`), plus `Get(HealthRegen) × gameHours` once `OutOfCombatSeconds` have passed since `OnHurt`; `HoursAwake += gameHours` unless asleep. Nothing drains while `Frozen`.
    - `void Tick(float deltaTime)` (real time: easing and the out-of-combat timer)
    - `void OnHurt()`, `void ApplyDeathFloor()` (each need under `Get(DeathNeedFloor, BaseDeathFloor) × Max` is raised to it), `void Drain(PlayerMeterId id, float amount, string source)` (the hook for special enemy effects; `source` is for logs)
    - `void OnStatsChanged()` re-reads `MaxHunger / MaxThirst / MaxSanity` into `SetMax`.
  - `NeedsSetup.Settings()` writes the asset with the spec's values:

| | Hunger | Thirst | Sanity (pass 1) |
| --- | --- | --- | --- |
| Max / stats | 100, `MaxHunger`, `HungerDrain`, `HungerPenaltyScale` | 100, `MaxThirst`, `ThirstDrain`, `ThirstPenaltyScale` | 100, `MaxSanity`, `SanityDrain`; not tracked by `NeedConditions` (no stat changes) |
| `DrainPerHour` | 4.17 (24 h) | 6.25 (16 h) | 0 |
| `PerStaminaSpent` | 0.015 | 0.025 | 0 |
| Good ≥ 0.75 | `HealthRegen` +12 | `StaminaRegen` ×1.15 | — |
| Low ≥ 0.25 | `MeleePowerScale` ×0.85, `HeldItemSwayScale` ×1.25 | `StaminaRegen` ×0.8; `SprintStaminaCost`, `JumpStaminaCost`, `WalkStaminaCost`, `AttackStaminaCostScale` ×1.15 | Sanity tiers come in Task 11 |
| VeryLow > 0 | ×0.65, ×1.6 | ×0.6; costs ×1.35 | |
| Empty = 0 | ×0.5, ×2; `HealthPerHour` −10 | ×0.4; costs ×1.5; `HealthPerHour` −10 | |

- [ ] **Step 1: Write the failing tests** (with a `StatModifiers` below and a real `Health(100)` and `Stamina(100,…)`):

```csharp
[Test] public void Advance_AtRestEmptiesHungerInADayAndThirstInSixteenHours()
{   needs.Advance(24f, false); Assert.AreEqual(0f, needs.Hunger.Current, 0.5f);
    Assert.AreEqual(0f, needs.Thirst.Current); }                       // emptied at 16 h, clamped
[Test] public void Advance_EffortDrainsMore()                       // stamina.Spend(100) then Advance(0) → Hunger 98.5, Thirst 97.5
[Test] public void Advance_AsleepDrainsAThird()                     // Advance(3, asleep:true) → Hunger 100 − 4.17
[Test] public void Advance_DrainUpgradeSlowsIt()                    // stats.Set("u", HungerDrain, multiply:0.5) → Advance(24) leaves 50
[Test] public void EmptyHungerAndThirst_StopAtTheSameHealthFloor()  // both 0, Advance(100) → health 25, not lower
[Test] public void WellFed_RegeneratesHealthOnlyOutOfCombat()       // health 50; OnHurt; Tick(5)+Advance(1) → 50; Tick(6)+Advance(1) → 62
[Test] public void ApplyDeathFloor_RaisesOnlyMetersUnderHalf()      // Hunger 20 → 50, Thirst 80 stays 80
[Test] public void ApplyDeathFloor_UpgradeRaisesTheFloor()          // stats.Set("u", DeathNeedFloor, add:0.25) → Hunger 20 → 75
[Test] public void Frozen_StopsEveryDrain()
[Test] public void Advance_OneLargeStepMatchesManySmallSteps()      // Advance(8) vs 800 × Advance(0.01): equal within 0.5 on every meter and health
[Test] public void Meter_CoversEveryPlayerMeterId()                 // foreach enum value: Meter(id) != null
[Test] public void Settings_EveryNeedHasSortedTiersCoveringZeroToOne() // loads Data/Player/PlayerNeedsSettings.asset
```

- [ ] **Step 2: Run; expect compile errors.**
- [ ] **Step 3: Implement `PlayerNeeds`, `PlayerNeedsSettings`, `PlayerMeterId`; write `NeedsSetup.Settings()`** and run it: `unity command --project-path "$P" script run --file AgentScripts/Needs/NeedsSetup.cs --entry NeedsSetup.Settings`.
- [ ] **Step 4: Run tests: pass.** `git status` shows only the new asset and scripts.
- [ ] **Step 5: Checkpoint.**

### Task 5: Wire the needs into the player, the scene and the save

**Files:**
- Modify: `Assets/_Game/_Core/PlayerSystem/Player.cs`, `IPlayer.cs`, `Tuning/PlayerTuning.cs`, `PlayerSaveSection.cs`, `Assets/_Game/Data/Player/PlayerTuning.asset` (via script)
- Create: `Assets/_Game/_Core/PlayerSystem/Needs/NeedsInstaller.cs`, `NeedsTicker.cs`; extend `AgentScripts/Needs/NeedsSetup.cs` with `Wire()`
- Test: `Assets/_Game/Tests/EditMode/PlayerNeedsTests.cs` (save cases)

**Interfaces:**
- Consumes: `PlayerNeeds` (Task 4), `WorldClock.HoursPerSecond`, `SleepService.Stage`.
- Produces:
  - `PlayerTuning.Needs` (`PlayerNeedsSettings`, may be null = needs off).
  - `IPlayer.Needs` (`PlayerNeeds`, null when off) and `IPlayer.Meter(PlayerMeterId id)`.
  - In `Player.Construct`: `_effects = new PlayerEffects(stats, …)`, then `_needs = new PlayerNeeds(_tuning.Needs, _effects, …)` built after `_life` and `Stamina`; `stats = _needs.Stats`; everything after reads the layered stats. Because `Health` and `Stamina` are built from `stats`, build them on `_effects` first and point `_stats` at `_needs.Stats` once `_needs` exists. `OnHurt` → `_needs.OnHurt()`; `OnReviving` → `_needs.ApplyDeathFloor()`; `OnStatsChanged` → `_needs.OnStatsChanged()`; `Update` → `_needs.Tick(deltaTime)` after stamina; `OnDestroy` → dispose.
  - `NeedsInstaller : SceneInstaller` (`Problem` null; registers `NeedsTicker` as entry point). `NeedsTicker : ITickable` takes `PlayerRegistry` and resolves `WorldClock` and `SleepService` as optional (no clock = nothing drains). Each frame: `player.Needs?.Advance(clock.HoursPerSecond × Time.deltaTime, sleep?.Stage == SleepStage.Asleep)`; skipped while the player is dead.
  - `PlayerSave` gains `bool HasNeeds; float Hunger; float Thirst; float Sanity; float HoursAwake`. `Player.RestoreSaved` gains the needs (or a separate `Player.RestoreNeeds(...)` called from the section). `HasNeeds == false` keeps the fresh, full meters.
  - `NeedsSetup.Wire()`: assigns the settings to `PlayerTuning.asset` and adds `NeedsInstaller` to WorldMap's `GameLifetimeScope` object (the scene is saved by the script only if it changed nothing else; check `isDirty` first and ask the user if it already was).

- [ ] **Step 1: Write the failing save tests:**

```csharp
[Test] public void PlayerSave_FromBeforeNeeds_StartsTheMetersFull()   // JSON without the fields → HasNeeds false → restore leaves 100/100/100, HoursAwake 0
[Test] public void PlayerSave_RoundTripsTheMeters()                     // 40 / 60 / 70, HoursAwake 5 → same after capture and restore
[Test] public void PlayerSave_RestoresAboveTheBaseMaxWhenAnUpgradeIsSaved() // MaxHunger +50 applied before restore → 130 stays 130
```

  Test through the same JSON path `SaveTests` uses, not by building a `Player`.

- [ ] **Step 2: Run; expect failures.**
- [ ] **Step 3: Implement the wiring and run `NeedsSetup.Wire`.**
- [ ] **Step 4: Run all EditMode tests: pass.** Then a play probe in WorldMap: enter Play, `script eval` `Application.runInBackground = true`, read `Needs.Hunger.Current` now and after `clock.skip` of 6 hours: about 75. `WorldMapSmokeTests` still pass (PlayMode; if the CLI reports 0/0, rely on the probe and say so).
- [ ] **Step 5: Checkpoint.** Docs: create `docs/technicals/NEEDS.md` (rules, data, wiring, testing) and add it to AGENTS.md's doc list; `SAVE.md` (player fields); `CODE_STRUCTURE.md`.

### Task 6: Cheats

**Files:**
- Create: `Assets/_Game/_Core/PlayerSystem/Needs/NeedsCommands.cs` (registered by `NeedsInstaller` when `Cheats.Enabled`)
- Modify: `docs/technicals/CONSOLE.md`

**Interfaces:**
- Consumes: `IPlayer.Meter`, `PlayerNeeds.Frozen`, `HoursAwake`.
- Produces, in `[CommandGroup("cheat")]`, the same pattern as `PlayerCheatCommands`:
  - `cheat.meter <health|stamina|hunger|thirst|sanity|all> [percent=100]` → `Meter(id).Set(percent / 100 × Max)`; replies `"Hunger 40 / 100 (Low)"`. Health at 0 is refused (`cheat.kill` exists).
  - `cheat.needs` → one line per need: value, max, severity; plus hours awake and whether frozen.
  - `cheat.needsfreeze [on|off]` → `Frozen`.

- [ ] **Step 1: Implement.** The meter name parses case-insensitively from `PlayerMeterId`; an unknown name fails listing the valid ones.
- [ ] **Step 2: Recompile; in Play run each command through the console** (`unity command … script eval` calling the command registry, as `CONSOLE.md` shows) and read the replies: `cheat.meter thirst 10` → Thirst 10 (VeryLow); `cheat.meter all` → five meters full; `cheat.needsfreeze on` then a 2-hour skip leaves Hunger unchanged.
- [ ] **Step 3: Checkpoint.** `CONSOLE.md` rows for the three commands.

### Task 7: Need rings on the HUD, and the cough

**Files:**
- Create: `Assets/_Game/_Core/UI/Widgets/NeedRing.cs`, `NeedRingStyle.cs`; `Assets/_Game/_Core/UI/Hud/PlayerNeedsHud.cs`; `AgentScripts/Hud/NeedsHudSetup.cs`; `Data/UI/HungerRingStyle.asset`, `ThirstRingStyle.asset`, `SanityRingStyle.asset`
- Modify: `Assets/_Game/_Core/UI/Prefabs/HUD.prefab` (via script), BroAudio Player library (via script)
- Test: `Assets/_Game/Tests/EditMode/NeedsHudPrefabTests.cs`

**Interfaces:**
- Consumes: `IPlayer.Needs`, `Need.Normalized`, `Need.Severity`, `Need.TierChanged`.
- Produces:
  - `NeedRingStyle : ScriptableObject`: `Sprite Icon`, `Sprite RingSprite`, `Dictionary<NeedSeverity, Color> Colors` (white Normal, green Good, orange Low, red VeryLow and Empty), `float DimAlpha = 0.45f`, `float BrightTime = 2.5f`, `float PulseScale = 1.15f`, `float EmptyPulseRate = 1.2f`, `SoundID TierSound` (played on entering `Low` or worse), `float RepeatSoundEvery = 40f` (while `VeryLow` or `Empty`; 0 = never).
  - `NeedRing : MonoBehaviour` (passive view): `SetValue(float normalized)` (radial-360 fill, clockwise), `SetSeverity(NeedSeverity severity, bool pulse)`, `Brighten()`. Fades back to `DimAlpha` after `BrightTime`.
  - `PlayerNeedsHud : PlayerHudView` with `[SerializeField] NeedRing _hunger, _thirst, _sanity`: feeds values in `LateUpdate`, brightens on `TierChanged` and on any rise (a refill), plays `TierSound` through `AudioService`.
  - `NeedsHudSetup.Build()`: three style assets; icons picked from Layer Lab's Pictoicons (a food, a water drop and a brain or eye icon: record the chosen file names in `UI.md`); a `Needs` node under `SafeFrame/BottomLeft` above `Vitals` (nested Canvas, HorizontalLayoutGroup, three 64 px rings, 10 px gaps); `Effects` and `DogBar` moved up by the row's height. Sound: one BroAudio entity `Player_ThirstCough` in the Player library from Gamemaster `Voice/Human Male A/voice_male_coughing_*` (2D, random clip), with its import preset (`AudioImportKit.Apply`). Hunger's `TierSound` stays empty (no growl found); Sanity's sounds come in Task 13. Re-runnable.

- [ ] **Step 1: Write the failing prefab test:**

```csharp
[Test] public void Hud_HasARingPerNeedWithAStyleAndIcon()
{   // load HUD.prefab → PlayerNeedsHud found; its three NeedRing fields assigned; each ring's style has an Icon and a color for every NeedSeverity
}
```

- [ ] **Step 2: Run; expect failure.**
- [ ] **Step 3: Implement the widget, style and presenter; run `NeedsHudSetup.Build`.**
- [ ] **Step 4: Run tests: pass.** In Play: `cheat.meter hunger 30`, `cheat.meter thirst 80`, `cheat.meter sanity 10`, then `capture game_view --output-path <scratchpad>/rings.png`; check the rings' fill, colours and that nothing overlaps the bars, dog bar or effects row at 1920×1080 and at 21:9 (`ShrinkToFit`). Listen-check the cough by `cheat.meter thirst 20`.
- [ ] **Step 5: Checkpoint.** Docs: `UI.md` (HUD tree, ring widget, styles), `ASSETS.md` (Gamemaster cough in use), `CODE_STRUCTURE.md`, `NEEDS.md`. Report to the user with the capture; this ends Part 1.

---

## Part 2: Carried weight

Ends with: the player slows between Comfortable and Max, crawls at Max, and the bag gauge shows it.

### Task 8: Load rules and the motor

**Files:**
- Create: `Assets/_Game/_Core/PlayerSystem/CarryLoad.cs`
- Modify: `Assets/_Game/_Core/PlayerSystem/PlayerMotor.cs`, `Player.cs`, `Tuning/PlayerMovementTuning.cs`
- Test: `Assets/_Game/Tests/EditMode/CarryLoadTests.cs`

**Interfaces:**
- Consumes: `Inventory.Weight`, `Inventory.MaxWeight`, `Inventory.Changed`, `StatId.ComfortableWeight`, `StatId.WeightMargin`.
- Produces:
  - `public enum LoadState { Light, NearComfortable, Heavy, Overloaded }`
  - `[Serializable] public sealed class CarryLoadSettings { public bool Enabled = true; public float BaseMargin = 10f; public float NearShare = 0.8f; public float HeavySpeed = 0.6f; public float HeavyCost = 1.5f; public float OverloadedSpeed = 0.15f; }` held by `PlayerMovementTuning.Load`.
  - `public readonly struct CarryLoad { float Weight, Comfortable, Max, SpeedMultiplier, CostMultiplier; LoadState State; static CarryLoad Evaluate(float weight, float comfortable, float max, CarryLoadSettings s); }`. Speed 1 up to Comfortable, linear to `HeavySpeed` at Max, `OverloadedSpeed` at Max and above. Cost 1 → `HeavyCost` over the same range, `HeavyCost` above.
  - `PlayerMotor`: `float LoadSpeedMultiplier = 1`, `float LoadCostMultiplier = 1`, `bool Overloaded` (no sprint, no jump; crouch still works). Applied in the slowdown product and the sprint spend; the walk and jump spends take `LoadCostMultiplier` too.
  - `Player.Load` (`CarryLoad`, re-evaluated on `Inventory.Changed` and `_stats.Changed`, not per frame): Comfortable = `Get(ComfortableWeight, inventory.MaxWeight)`, Max = Comfortable + `Get(WeightMargin, BaseMargin)`.

- [ ] **Step 1: Write the failing tests:**

```csharp
[Test] public void Evaluate_IsLightUnderEightyPercentOfComfortable()   // 19 of 25/35 → Light, speed 1
[Test] public void Evaluate_IsNearFromEightyPercent()                  // 21 → NearComfortable, speed 1
[Test] public void Evaluate_SlowsLinearlyBetweenTheLimits()            // 30 → Heavy, speed 0.8, cost 1.25
[Test] public void Evaluate_CrawlsAtAndAboveMax()                      // 35 and 60 → Overloaded, speed 0.15
[Test] public void Evaluate_UpgradesMoveBothLimits()                   // via StatModifiers: ComfortableWeight +5 → 30 is NearComfortable
```

- [ ] **Step 2: Run; expect compile errors.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run tests: pass.** Play probe: `cheat.give` heavy items until Overloaded; `MovementReadout` (or `script eval` on `TargetSpeed`) shows the crawl, and sprint and jump do nothing.
- [ ] **Step 5: Checkpoint.** `EQUIPMENT.md` (weight now slows: rules, stats), `NEEDS.md` link.

### Task 9: The bag gauge

**Files:**
- Create: `Assets/_Game/_Core/UI/Widgets/BagGauge.cs`, `BagGaugeStyle.cs`; `Assets/_Game/_Core/UI/Hud/PlayerLoadHud.cs`; `AgentScripts/Hud/LoadHudSetup.cs`; `Data/UI/BagGaugeStyle.asset`
- Modify: `Assets/_Game/_Core/UI/Inventory/InventoryScreen.cs` (weight bar colours and notch), `Data/Localization/Csv` (HUD table), `HUD.prefab` and the inventory screen via script
- Test: `NeedsHudPrefabTests.cs` (one more case)

**Interfaces:**
- Consumes: `Player.Load` (Task 8).
- Produces:
  - `BagGaugeStyle`: `Sprite Icon` (the loot markers' bag sprite), `Dictionary<LoadState, Color> Colors` (white, yellow, orange, red), `float DimAlpha`, `float NotchWidth`.
  - `BagGauge` view: `SetLoad(float fill, float notch, LoadState state, string label)`; fill = Weight / Max (vertical, from the bottom); the notch sits at Comfortable / Max.
  - `PlayerLoadHud : PlayerHudView` builds the label from the localized format `hud.weight` = `"{0:0.0} / {1:0} kg"` (current / Max); dims while `Light`.
  - `LoadHudSetup.Build()`: the `Bag` node under `SafeFrame/BottomRight`, right-aligned, below the pickup feed (feed stays 150 px up; bag at 40 px up, 56 px icon, label under it), and the inventory screen's `WeightBar` recoloured by the same `LoadState` with a notch at Comfortable.

- [ ] **Step 1: Write the failing prefab test** `Hud_HasABagGaugeWithAColorPerLoadState`.
- [ ] **Step 2: Run; expect failure.**
- [ ] **Step 3: Implement, add the CSV row (English only), reimport the tables, run `LoadHudSetup.Build`.**
- [ ] **Step 4: Run tests: pass.** Captures at Light, Heavy and Overloaded (`capture game_view`), plus the inventory screen open; check the label doesn't collide with the pickup feed or the "Backpack full" warning.
- [ ] **Step 5: Checkpoint.** `UI.md`, `LOCALIZATION` row, `CODE_STRUCTURE.md`. Report to the user; this ends Part 2.

---

## Part 3: Sanity

Ends with: Sanity drains from darkness and lack of sleep, recovers by the campfire, eases near Loki, shows on screen and in sound, and makes the clock and minimap lie.

### Task 10: Sanity rules

**Files:**
- Create: `Assets/_Game/_Core/PlayerSystem/Needs/SanityExposure.cs`
- Modify: `PlayerNeedsSettings.cs` (a `SanitySettings` block), `NeedsSetup.Settings()` (Sanity tiers and values)
- Test: `Assets/_Game/Tests/EditMode/SanityExposureTests.cs`

**Interfaces:**
- Produces:
  - `[Serializable] public sealed class SanitySettings { public float AwakeGraceHours = 4f; public float AwakeEmptyHours = 18f; public float DarknessFullPerHour = 25f; public float FullDrainDistance = 150f; public AnimationCurve DarknessByDistance /* 0..1 over 0..FullDrainDistance, linear by default */; public float LightRecoveryPerHour = 3.3f; public float ComfortDrainScale = 0.6f; }`
  - `public readonly struct SanityInput { public readonly float HoursAwake; public readonly float Darkness /* 0 day .. 1 night */; public readonly float DistanceOutsideLight /* 0 inside a light */; public readonly bool InLight; public readonly bool NearComfort; }`
  - `public static class SanityExposure { public static float RatePerHour(in SanityInput input, SanitySettings s, float maxSanity); }`: negative = drain, positive = recovery.
    - Lack of sleep: after `AwakeGraceHours`, `maxSanity / (AwakeEmptyHours − AwakeGraceHours)` per hour (7.14 at max 100), day and night.
    - Darkness: `DarknessFullPerHour × curve(distance / FullDrainDistance) × Darkness`, only outside a light; × `ComfortDrainScale` near comfort.
    - In a light: `+LightRecoveryPerHour`, day or night, added to the lack-of-sleep drain (so late at night by the fire the net is still a loss).
  - `PlayerNeeds.Advance` gains a `SanityInput` parameter (pass 1 callers pass a default: no drain); sanity change = `RatePerHour × Get(SanityDrain or SanityRecovery) × gameHours`, the drain scale on negative parts and the recovery scale on positive ones.
  - Sanity tiers in the asset: `Good` ≥ 0.6, `Normal` ≥ 0.4 (uneasy), `Low` ≥ 0.2, `VeryLow` > 0, `Empty` = 0, all with no stat changes and no health drain.

- [ ] **Step 1: Write the failing tests:**

```csharp
[Test] public void Awake_NoDrainForFourHours()                     // HoursAwake 3.9, day, in no light → 0
[Test] public void Awake_EmptiesAtEighteenHours()                  // integrate from 4 to 18 h → −100 ± 0.5
[Test] public void Darkness_NothingByDay()                         // Darkness 0, 150 m out → only the awake part
[Test] public void Darkness_FasterFartherFromLight()               // 10 m < 75 m < 150 m drains; ≥150 m = −25/h at night
[Test] public void Darkness_FasterThanLackOfSleep()                // 150 m at night: |darkness part| > awake part
[Test] public void Light_RecoversDayAndNight()                     // InLight, HoursAwake 2 → +3.3
[Test] public void Light_OnlySlowsTheLossLateAtNight()             // InLight, HoursAwake 10 → 3.3 − 7.14 < 0
[Test] public void Comfort_SlowsOnlyTheDarknessDrain()             // near Loki at 150 m: darkness × 0.6, awake part unchanged
```

- [ ] **Step 2: Run; expect compile errors.**
- [ ] **Step 3: Implement; re-run `NeedsSetup.Settings`.**
- [ ] **Step 4: Run tests: pass.**
- [ ] **Step 5: Checkpoint.**

### Task 11: Lights, Loki and hours awake in the world

**Files:**
- Create: `Assets/_Game/_Core/PlayerSystem/Needs/SafeLight.cs`, `ComfortSources.cs`
- Modify: `NeedsInstaller.cs`, `NeedsTicker.cs`, `Player.cs` / `IPlayer.cs` (`Darkness`), `Assets/_Game/_Core/_Common/Events/PlayerEvents.cs`, `Assets/_Game/_Core/DogSystem/DogManager.cs`, `DogSettings.cs`, `AgentScripts/Maps/WorldMap/WorldMapLayout.cs` (campfire), `NeedsSetup.cs` (`Campfire()`)
- Test: `PlayerNeedsTests.cs`, `SanityExposureTests.cs` (nearest light)

**Interfaces:**
- Consumes: `SanityExposure` (Task 10), `TimeOfDayWeight`, `SleepService.Stage`.
- Produces:
  - `SafeLight : MonoBehaviour` with `[SerializeField] float _radius = 28f` (`Radius`), a gizmo disc; collected into `NeedsInstaller`'s `[SerializeField, ReadOnly] List<SafeLight> _lights` by `SceneCollection`, like `RaidInstaller`'s spawn zones. Its radius is the Sanity rule, separate from the light's range.
  - `public static float SafeLights.DistanceOutside(Vector3 position, IReadOnlyList<SafeLight> lights)` (0 inside any; else distance to the nearest edge) in `SafeLight.cs`.
  - `public interface IComfortSource { Vector3 Position { get; } float Radius { get; } bool IsActive { get; } }` and `ComfortSources` (scene service: `Add`, `Remove`, `bool Near(Vector3)`), registered by `NeedsInstaller`.
  - `public readonly struct PlayerComforted { public readonly PlayerMeterId Meter; public readonly float Amount; public readonly string Source; }` in `PlayerEvents.cs`; `NeedsTicker` subscribes and adds to that meter.
  - `IPlayer.Darkness` (0–1, the value `TickVision` already reads).
  - `void PlayerNeeds.OnWoke()` sets `HoursAwake` to 0. `NeedsTicker` builds the `SanityInput` each frame and calls `OnWoke()` once when the sleep stage reaches `Waking`.
  - Dogs (`DogManager`): registers the dog as an `IComfortSource` (radius `DogSettings.ComfortRadius = 8f`, active while following or at home and not knocked down) through an optional `ComfortSources`; `Pet` publishes `PlayerComforted(Sanity, DogSettings.PetSanity = 5f, "dog.pet")` at most once per `PetSanityCooldown = 180f` seconds.
  - Campfire: `NeedsSetup.Campfire()` adds a `SafeLight` to WorldMap's `Campfire` node, checks the HIGHLANDS `FX/Fire` child is active with its `Light` enabled (and turns them on if not), and reports both. `WorldMapLayout`'s campfire block adds the same `SafeLight`, so a map rebuild keeps it.

- [ ] **Step 1: Write the failing tests:** `DistanceOutside_IsZeroInsideAndMeasuresToTheNearestEdge` (two lights), `OnWoke_ResetsHoursAwake`, `Comforted_AddsToTheNamedMeter`.
- [ ] **Step 2: Run; expect failures.**
- [ ] **Step 3: Implement; run `NeedsSetup.Campfire`.** Check the WorldMap scene's dirty state first; save only that scene.
- [ ] **Step 4: Run tests: pass.** Play probe at night (`clock.set 22`): by the campfire Sanity rises slowly; at the bridge it drains; 150 m out it drains at about 25/h; with Loki following, slower. `capture scene_view` of the campfire with the `SafeLight` gizmo covering the yard and house.
- [ ] **Step 5: Checkpoint.** `DOGS.md` (comfort, pet), `NEEDS.md`, `ENVIRONMENT.md` (campfire light), `CODE_STRUCTURE.md`.

### Task 12: Screen looks behind a swappable backend

**Files:**
- Create: `Assets/_Game/_Core/GraphicsSystem/ScreenLooks/ScreenLookId.cs`, `IScreenLookBackend.cs`, `ScreenLookBackendAsset.cs`, `VolumeLookBackendAsset.cs`, `VolumeLookBackend.cs`, `ScreenLookService.cs`, `ScreenLookSettings.cs`, `ScreenLookInstaller.cs`; `Assets/_Game/_Core/PlayerSystem/Needs/SanityPresenter.cs`; `AgentScripts/Graphics/ScreenLookSetup.cs`; `Data/Graphics/ScreenLooks/` (settings, two backend assets, six volume profiles)
- Modify: `Assets/_Game/Settings/PC_Renderer.asset` (SC renderer features, via script), `docs/technicals/PLUGINS.md`
- Test: `Assets/_Game/Tests/EditMode/ScreenLookTests.cs`

**Interfaces:**
- Produces:
  - `public enum ScreenLookId { SanityUneasy, SanityLow, SanityCritical }` (later events add values).
  - `public interface IScreenLookBackend : IDisposable { void Apply(ScreenLookId look, float strength); }`
  - `public abstract class ScreenLookBackendAsset : ScriptableObject { public abstract IScreenLookBackend Create(Transform parent); }`: the factory. A new tool is a new subclass; game code never changes.
  - `VolumeLookBackendAsset : ScreenLookBackendAsset` with `Dictionary<ScreenLookId, VolumeLook> Looks` (`VolumeLook { VolumeProfile Profile; float PulseRate; float PulseDepth; }`) and `float Priority = 200` (above `EnvironmentDirector`'s grade volumes). Creates a `VolumeLookBackend`: one global `Volume` per look under `parent`, `weight = strength × pulse`. No code references SC: both backends are instances of this asset with different profiles.
  - `ScreenLookSettings : ScriptableObject` with `bool Enabled`, `ScreenLookBackendAsset Backend`.
  - `ScreenLookService : ITickable, IDisposable` (`ScreenLookInstaller : SceneInstaller`): `void Request(ScreenLookId look, float strength)` (kept until changed, 0 = off); null backend or disabled = no-op.
  - `SanityPresenter : ITickable` (registered by `NeedsInstaller`, resolves `ScreenLookService` optionally): maps the player's Sanity share to the three strengths: Uneasy 0 at 0.6 → 1 at 0.4; Low 0 at 0.4 → 1 at 0.2; Critical 0 at 0.2 → 1 at 0. Eased over 1 s.
  - `ScreenLookSetup.Build()`:
    - URP profiles: Uneasy = Vignette 0.3 (dark); Low = + ColorAdjustments saturation −40, FilmGrain 0.3; Critical = + ChromaticAberration 0.5, saturation −70, Vignette 0.45.
    - SC profiles: the same URP overrides plus SC `ColorSplit` on Low and SC `DoubleVision` + `ColorSplit` on Critical (pulse rate 0.5 Hz, depth 0.4).
    - Adds SC's `ColorSplitRenderer` and `DoubleVisionRenderer` features to `PC_Renderer` (saves it with `SaveAssetIfDirty`).
    - Writes `ScreenLookSettings` with `Backend` = the SC asset (user's pick).
    - Adds `ScreenLookInstaller` to WorldMap's scope.

- [ ] **Step 1: Write the failing tests** with a fake backend recording calls:

```csharp
[Test] public void Request_ReachesTheBackendOnTick()
[Test] public void NoBackend_DoesNothingAndDoesNotThrow()
[Test] public void Disabled_AppliesZeroStrength()
[Test] public void SanityPresenter_MapsTheShareToTheThreeLooks()   // 0.5 → Uneasy 0.5, Low 0, Critical 0; 0.1 → 1, 1, 0.5
[Test] public void BackendAssets_HaveAProfileForEveryLook()         // both Data assets: every ScreenLookId has a non-null profile
```

- [ ] **Step 2: Run; expect compile errors.**
- [ ] **Step 3: Implement; run `ScreenLookSetup.Build`.**
- [ ] **Step 4: Run tests: pass.** In Play at night: `cheat.meter sanity 50`, `30`, `10`, `0` with a `capture game_view` each, for both backends (swap `Backend` with `script eval`, not by editing the asset in Play). Check goblins stay readable at night in every capture. Measure the frame cost of the SC backend at Critical against URP-only in a development build or with editor overhead accounted for (`unity-optimization-pc`); report both numbers.
- [ ] **Step 5: Checkpoint.** `PLUGINS.md` (SC Screen Effects: Could, in use through `VolumeLookBackendAsset`, the two renderer features, measured cost), `DAYNIGHT.md` (looks sit above the grades), `NEEDS.md`, `CODE_STRUCTURE.md`.

### Task 13: Sanity sounds and the lying HUD

**Files:**
- Create: `Assets/_Game/_Core/UI/Hud/SanityGlitch.cs`
- Modify: `SanityPresenter.cs` (sounds), `ClockHud.cs`, `MinimapHud.cs` (and `MinimapView.cs` for fake blips), `PlayerVitalsHud.cs`, `PlayerNeedsHud.cs`, `NeedsHudSetup.cs` (Sanity sound entities)
- Test: `Assets/_Game/Tests/EditMode/SanityGlitchTests.cs`

**Interfaces:**
- Produces:
  - `public static class SanityGlitch` (pure, seeded by time, no `UnityEngine.Random`):
    - `bool ClockLies(float sanity, float time, out int hour, out int minute)`: below 0.4, a wrong time for 0.6 s at most every 8 s; never for more than one window in a row.
    - `int FakeBlips(float sanity, float time, Span<Vector2> blips)`: below 0.2, up to 3 blips at radar positions that move and vanish every 2–4 s; 0 above.
    - `bool CompassSwapped(float sanity, float time)`: below 0.2, letters swap for 1 s at most every 10 s.
    - `float Flicker(float sanity, float time)`: an alpha multiplier for the bars and rings (1 above 0.4, dipping to 0.35 below), never touching the values.
  - `ClockHud`, `MinimapHud`, `PlayerVitalsHud`, `PlayerNeedsHud` read the bound player's `Needs?.Sanity.Normalized` and apply the matching function; without needs, nothing changes.
  - Sounds (BroAudio, Player library): `Sanity_Whisper` (Gamemaster `Voice/Ghost/ghost_witch_voice_hiss_*`, only if it sounds right; otherwise left out and noted), `Sanity_Drone` (Gamemaster `Cinematic Sounds/cinematic_LowDrone1`, looping); the heartbeat reuses `LowHealthStyle.Heartbeat`. `SanityPresenter` keeps the drone and whisper players: drone volume follows Low strength, whispers from Uneasy, heartbeat every 1.1 s below 0.4.

- [ ] **Step 1: Write the failing tests:** `ClockLies_NeverAboveFortyPercent`, `ClockLies_LastsAtMostSixTenthsOfASecond` (sample a minute at 60 Hz), `FakeBlips_NoneAboveTwentyPercentAndAtMostThree`, `Flicker_IsOneAboveFortyPercentAndNeverBelowTheFloor`.
- [ ] **Step 2: Run; expect compile errors.**
- [ ] **Step 3: Implement; run `NeedsHudSetup.Build` again** for the sound entities and import presets.
- [ ] **Step 4: Run tests: pass.** In Play at `cheat.meter sanity 30` and `10`: captures of the clock and minimap mid-lie (several frames); listen-check drone, whispers and heartbeat. Health and ammo readouts unchanged in every capture.
- [ ] **Step 5: Checkpoint.** `UI.md` (lies), `ASSETS.md` (Gamemaster sounds in use), `NEEDS.md`, `CODE_STRUCTURE.md`.

### Task 14: Finish

- [ ] **Step 1:** Full `script recompile`, `recompile_status` clean, `console status` no new errors.
- [ ] **Step 2:** `test run --mode editor`: all pass. PlayMode smoke tests, or the play probe if the CLI reports 0/0.
- [ ] **Step 3:** A night soak: `RaidSoak` (`AgentScripts/Raids/RaidSoak.cs`) with the needs on, to check no errors and the frame times against the 60 fps target with the Critical look on screen.
- [ ] **Step 4:** Docs check: `NEEDS.md` complete; `ROADMAP.md` status line (survival meters built, nightmare enemies next); `CORE.md` is not changed (the GDD follows the user's promotion, not the build); the spec's status set to built.
- [ ] **Step 5:** Report what was verified and what wasn't (captures, numbers, listen checks). Commit only when the user asks.

---

## Not in this plan

- Nightmare enemies at zero Sanity: their own design and plan (spec → Build order, pass 4).
- Food and drink items, Sanity from sleep, comfort food, the bench, fire pits, Loki's lantern, the torch: later tasks; each plugs into `Need.Add`, `PlayerComforted`, `SafeLight` or a stat modifier source.
