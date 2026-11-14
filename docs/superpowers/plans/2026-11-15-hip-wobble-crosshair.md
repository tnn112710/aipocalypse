# Hip Wobble, Dynamic Crosshair and Far Cry Hip Framing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hip inaccuracy becomes a visible wobble of the gun (bullets follow the barrel), a large animated cross shows how far it may wander, and guns sit at the hip like Far Cry.

**Architecture:** `GunAccuracy` (plain C# rule) computes one envelope in degrees from the gun's hip cone, a movement scale, airborne spread, shot bloom, fatigue and steadiness. `HeldGun` exposes it as `HeldItem.Wobble` plus per-shot `WobbleKick`; `HeldItemSway` turns it into a smooth wander added to `AimOffset`, which the shots, the dot and the model already follow. `CrosshairHud` draws a cross at the envelope around the un-wobbled aim (`RestOffset`) with the dot wandering inside.

**Tech Stack:** Unity 6000.6, C#, uGUI, Unity Test Framework (EditMode), Unity CLI (`unity command`).

**Spec:** `docs/superpowers/specs/2026-11-15-hip-wobble-crosshair-design.md`

## Global Constraints

- Follow `AGENTS.md` code style: `_camelCase` private fields, `[SerializeField] T _field;` plus a read-only property, `sealed` leaf classes, compact `//` comments, no `[field: SerializeField]`.
- Pure data ScriptableObjects and `[Serializable]` settings classes may use public fields; runtime code never writes them.
- First-guess values (spec): walk ×1.5, crouch ×0.7, sprint ×2.5; bloom per shot AK 0.5°, MP5 0.4°, pistols 0.8°, shotguns 1.5°; `MaxBloom` 3°; `BloomRecovery` 4°/s.
- Block-out guns take their reference gun's values: Glock17 / DesertEagle ← Pistol; Uzi / P90 ← MP5; M1Carbine / M4 / FAL / RPD / AK74M ← AK47; HuntingRifle / AA12 ← PumpShotgun.
- Aiming down sights is unchanged (the envelope shrinks to `AimSpread`, 0 on every gun).
- Shotgun pellets keep their random `SpreadAngle` around the barrel.
- No sanity system; the hook is `StatId.HeldItemSwayScale` via `IGunUser.SteadinessScale`.
- Commit only when the user asks (AGENTS.md). Each task ends with a checkpoint: `git status` lists only that task's files.
- Editor work goes through the Unity CLI (`P="$(cygpath -w "$PWD")"`, `unity command --project-path "$P" ...`). After `script recompile`, poll `script recompile_status` until `completed`.
- Before `test run`, check that the active scene isn't dirty. `LocalizationTests.Csvs_MatchTheirTables` already fails (UI.csv double spaces) and is not this plan's concern; every other test must pass.
- Play-mode captures: `capture game_view --output-path <scratchpad>/<name>.png` (forward slashes in the path). Close `PauseMenu` before each capture (it opens on focus loss). If captures stay stale, ask the user to click the Game view once.
- Each task updates the docs its change touches (WEAPONS.md, UI.md, CODE_STRUCTURE.md for added scripts).

## Review Focus

- **A held item that isn't a gun (axe, consumable):** `Wobble` 0, no wander, the cross hidden as today. Test in Task 3: zero wobble gives a zero wander.
- **Switching guns mid-wobble or mid-bloom:** the new gun starts with no bloom and no leftover kick. Task 2: `OnHidden` resets the accuracy; Task 3: a hide resets the wander spring (`HeldItemSway.ResetWobble`).
- **Hit stop and pause (time scale 0):** the wander and bloom freeze rather than jump. Task 3 test: `Tick` with `deltaTime` 0 leaves the wander unchanged.
- **Aiming from a big hip wobble:** the dot must glide onto the sights as the aim weight rises (the envelope shrinks with `ConeAt`), with no snap. Task 2 test: envelope at aim weight 0.5 lies between the hip and aimed values.
- **The cross hidden by the dot or the hit marker at zero spread:** the cross keeps `MinGap` px from the centre. Task 4 checks it in play mode with the pistol standing still.

---

### Task 1: Envelope rule (`GunAccuracy`) and gun bloom settings

**Files:**

- Modify: `Assets/_Game/_Core/WeaponSystem/GunSettings.cs` (`AccuracySettings`, around line 193)
- Modify: `Assets/_Game/_Core/WeaponSystem/GunAccuracy.cs`
- Test: `Assets/_Game/Tests/EditMode/HeldMotionTests.cs` (`GunAccuracyTests`, lines 10–55)
- Data: the 16 gun assets `Assets/_Game/Content/Weapons/<Gun>/<Gun>.asset` (bloom values)
- Docs: `docs/technicals/WEAPONS.md` (top: references; rows Hip spread, Airborne inaccuracy)

**Interfaces:**

- Produces: `public struct AccuracyInput { public float Cone, MoveScale, AimWeight, AirScale, Fatigue, Steadiness; public bool Airborne; }` in `GunAccuracy.cs`.
- Produces: `GunAccuracy.Tick(in AccuracyInput input, float deltaTime)`; `float GunAccuracy.Spread` (the envelope, degrees); `float GunAccuracy.AddShot()` returns the kick in degrees; `GunAccuracy.Reset()`.
- Produces: `AccuracySettings.BloomPerShot` (0.5), `MaxBloom` (3), `BloomRecovery` (4), public fields with tooltips.

- [ ] **Step 1: Rewrite `GunAccuracyTests` for the envelope.** Settings: `AirborneSpread 2, AimMultiplier 0.25, SettleSpeed 6, BloomPerShot 0.5, MaxBloom 3, BloomRecovery 4`. Default input: `Cone 3, MoveScale 1, AimWeight 0, AirScale 1, Fatigue 1, Steadiness 1, Airborne false`.

```csharp
Standing_IsTheCone                        // one Tick(0.016) → Spread == 3 (±0.001)
Moving_ScalesTheCone                      // MoveScale 1.5 → 4.5
Airborne_AddsAirSpread_LessWhileAiming    // Cone 0, Airborne → 2; AimWeight 1 → 0.5
Shots_AddBloom_UpToTheCap_ThenRecover     // 10 × AddShot, Tick(0.016) → 6 (3 + cap 3); 120 × Tick(1/60) → 3 (±0.05)
Aiming_RemovesBloom                       // AddShot ×4, input Cone 0 + AimWeight 1 → Spread == 0
HalfAimed_IsBetweenHipAndAimed            // Cone = Mathf.Lerp(3, 0, 0.5), AimWeight 0.5, after 2 shots → 1.5 < Spread < 2.5
Fatigue_AndSteadiness_Multiply            // Fatigue 1.5, Steadiness 0.5 → 2.25
Spread_SettlesOverTime_AfterLanding       // Airborne then grounded Tick(0.1): Cone 0 → 0 < Spread < 2
AddShot_ReturnsTheKick                    // AddShot() == 0.5 at hip; with Fatigue 2 → 1.0; AimWeight 1 → 0
Reset_ClearsBloomAndSpread                // AddShot, Reset, Tick → 3
```

- [ ] **Step 2: Run them and see them fail.** `unity command --project-path "$P" test run --mode editor --filter "MB13.Aipocalypse.Tests.GunAccuracyTests"` → compile error (no `AccuracyInput`).

- [ ] **Step 3: Implement.** In `Tick`: `air = Airborne ? AirborneSpread × Lerp(1, AimMultiplier, AimWeight) × AirScale : 0`; bloom falls by `BloomRecovery × dt` (MoveTowards 0); `target = (Cone × MoveScale + air + bloom × (1 − AimWeight)) × Fatigue × Steadiness`; rises at once, otherwise settles with `Damping.ByRate(SettleSpeed, dt)` (as today). `AddShot()`: bloom += `BloomPerShot`, clamped to `MaxBloom`; returns `BloomPerShot × (1 − AimWeight) × Fatigue × Steadiness` using the last `Tick`'s input. Class comment: the hip envelope, which the gun's wobble fills (not a hidden cone).

- [ ] **Step 4: Recompile, run the filter again.** Expected: all 10 pass. `HeldGun` still calls the old `Tick`: change that one call to build an `AccuracyInput` from today's values (`Cone 0`, `MoveScale 1`, `Fatigue 1`, `Steadiness = _user.AirSpreadScale`, so behaviour is unchanged until Task 2) so this task compiles on its own.

- [ ] **Step 5: Set bloom on the gun assets.** A one-off `script run` file (scratchpad) sets `_accuracy.BloomPerShot` per the Global Constraints table (AK47 0.5, MP5 0.4, Pistol 0.8, PumpShotgun 1.5, DoubleBarrel 1.5, block-outs from their reference), `MaxBloom` 3, `BloomRecovery` 4, saving each with `AssetDatabase.SaveAssetIfDirty`. Check `git diff --stat` lists only the 16 gun assets.

- [ ] **Step 6: Docs.** WEAPONS.md: a line under the intro, "References for gun play and feel: Far Cry (gun position and view, hip and aimed framing, handling) and Bodycam (procedural hip wobble, bullets following the barrel)". Rewrite the Hip spread and Airborne inaccuracy rows to describe the envelope (filled in by Tasks 2–3).

- [ ] **Step 7: Checkpoint.** `git status`: GunSettings.cs, GunAccuracy.cs, HeldMotionTests.cs, HeldGun.cs (call site), 16 gun assets, WEAPONS.md.

### Task 2: The gun feeds the envelope and its shots follow the barrel

**Files:**

- Modify: `Assets/_Game/_Core/EquipmentSystem/HeldItemInput.cs` (add `public float MoveSpreadScale;`, comment: 1 = standing)
- Modify: `Assets/_Game/_Core/EquipmentSystem/HeldItem.cs` (virtuals)
- Modify: `Assets/_Game/_Core/WeaponSystem/IGunUser.cs`, `Assets/_Game/_Core/PlayerSystem/Player.cs` (`SteadinessScale`)
- Modify: `Assets/_Game/_Core/PlayerSystem/Tuning/PlayerHeldItemTuning.cs` (Guns header: three scales), `Assets/_Game/_Core/PlayerSystem/HeldItemDriver.cs` (line ~68: fill `MoveSpreadScale`)
- Modify: `Assets/_Game/_Core/WeaponSystem/HeldGun.cs` (accuracy tick, `Wobble`, `WobbleKick`, shot at line ~384, `OnHidden`)
- Create: `AgentScripts/Weapons/AimReview.cs` (play-mode review helper)
- Test: `Assets/_Game/Tests/EditMode/HeldMotionTests.cs` (new `MoveSpreadScaleTests`)
- Docs: WEAPONS.md (Hip spread row, Aim origin note)

**Interfaces:**

- Consumes: Task 1's `AccuracyInput`, `GunAccuracy.Tick/AddShot/Spread/Reset`.
- Produces: `HeldItem.Wobble` (`public virtual float Wobble => 0f;`, degrees: how far the aim may wander at the hip); `HeldItem.WobbleKick` (`public virtual Vector2 WobbleKick => default;`, degrees this frame, x right / y up, a shot's jolt, read after `Tick`, like `LookTurn`).
- Produces: `float IGunUser.SteadinessScale` (Player: `_stats.GetScale(StatId.HeldItemSwayScale)`).
- Produces: `PlayerHeldItemTuning.MoveSpreadScale(bool sprinting, bool moving, bool crouching) → float`, with fields `_walkSpreadScale` 1.5, `_crouchSpreadScale` 0.7, `_sprintSpreadScale` 2.5.
- Produces: `AimReview` static entries for later tasks: `Prepare()`, `Hold(string gun)`, `Fit(string part)` (`"none"` removes the optic), `Aim(bool on)`, `Stand(float x, float z, float yaw)`, `Info()` (aim weight, zoom, wobble), `Capture(string path)` (closes PauseMenu first, then captures the game view).

- [ ] **Step 1: Write `MoveSpreadScaleTests`.** On `ScriptableObject.CreateInstance<PlayerHeldItemTuning>()`:

```csharp
Standing_IsOne              // (false, false, false) → 1
Walking_IsWalkScale         // (false, true, false) → 1.5
Crouching_Reduces           // (false, false, true) → 0.7; crouch-walking (false, true, true) → 1.05
Sprinting_Overrides         // (true, true, false) → 2.5
```

- [ ] **Step 2: Run, see it fail** (no `MoveSpreadScale`).

- [ ] **Step 3: Implement the plumbing.** The tuning method: sprinting → sprint scale; else (moving ? walk : 1) × (crouching ? crouch : 1). The driver fills `MoveSpreadScale = tuning.MoveSpreadScale(_motor.IsSprinting, isMoving, _motor.IsCrouching)`. Add `SteadinessScale` to `IGunUser` and Player. In `HeldGun.Tick`, build the `AccuracyInput` from `stats.ConeAt(_aimWeight)`, `input.MoveSpreadScale`, `input.IsAirborne`, `_aimWeight`, `_user.AirSpreadScale`, `_user.FatigueRecoilScale`, `_user.SteadinessScale`. Clear `_wobbleKick` at the start of `Tick`; in `OnFired` add `RandomDirection × _accuracy.AddShot()`, with the direction from the same random source `BulletSpread` uses. `Wobble => _accuracy.Spread`; `Spread => Wobble + stats.SpreadAngle` (the cross radius). Remove `AirSpread`, `HipCone` and the `BulletSpread.Apply(direction, AirSpread + HipCone)` line from the shot; pellets keep `BulletSpread.Apply(direction, stats.SpreadAngle)`. `OnHidden` already calls `_accuracy.Reset()`; also zero `_wobbleKick`.

- [ ] **Step 4: Recompile, run all EditMode tests.** Expected: everything passes except the known localization test.

- [ ] **Step 5: `AimReview.cs`.** Port the scratchpad helper used during the optics work (OpticShots: equips via `Equipment.Switcher.EquipImmediately`, aims with a fresh virtual gamepad's left trigger after removing every gamepad, teleports with `Player.Teleport`), plus `Capture`. Dry-run: `script run --file AgentScripts/Weapons/AimReview.cs --dry-run` → no diagnostics.

- [ ] **Step 6: Play check: no hidden cone.** WeaponsTest, `Hold("AK47")`, `Stand(-1.25, 2, 2.9)`, standing still at the hip (only the small breathing sway moves the aim; the wander comes in Task 3), fire five single shots at the 25 m dummy. Expected: impacts land at the crosshair dot (the hit marker pops on the dummy each time), where before this task they scattered up to 3°. Then exit play mode.

- [ ] **Step 7: Docs and checkpoint.** WEAPONS.md Hip spread row: no random cone; the envelope and how movement, bloom, fatigue and steadiness change it, with the tuning locations. `git status`: this task's files only.

### Task 3: The wander (`HeldItemSway`)

**Files:**

- Modify: `Assets/_Game/_Core/PlayerSystem/HeldItemSway.cs` (`AimSwaySettings`, `HeldSwayInput`, `HeldItemSway`)
- Modify: `Assets/_Game/_Core/PlayerSystem/HeldItemDriver.cs` (`TickSway`: pass `held.Wobble`, call `KickWobble(held.WobbleKick)`; reset on a held item change)
- Test: `Assets/_Game/Tests/EditMode/HeldMotionTests.cs` (new `HipWobbleTests`)
- Docs: WEAPONS.md → Aim sway section (a Hip wobble row)

**Interfaces:**

- Consumes: `HeldItem.Wobble`, `HeldItem.WobbleKick` (Task 2).
- Produces: `HeldSwayInput.Wobble` (float, degrees); `HeldItemSway.KickWobble(Vector2 degrees)`; `HeldItemSway.ResetWobble()`; `HeldItemSway.RestOffset` (Vector2: the aim sway without the wander, clamped as today); `AimOffset` = `RestOffset` + wander (so `AimRotation`, shots and the model follow the wobble). Also `HeldItemSway.RestRotation` (as `AimRotation`, from `RestOffset`).
- Produces in `AimSwaySettings` (Header "Hip wobble"): `WobbleRate` 0.35 (cycles/s at rest), `TiredWobbleRate` 0.8 (gasping), `WobbleStiffness` 150, `WobbleDamping` 0.5, `MaxWobble` 10 (degrees, a safety cap on the envelope).

- [ ] **Step 1: Write `HipWobbleTests`** (new `HeldItemSway` with default settings and a stat provider returning 1, as the existing sway tests build it):

```csharp
ZeroWobble_GivesNoWander            // 600 ticks at 1/60 with Wobble 0 → AimOffset == RestOffset every tick
Wander_StaysInsideTheEnvelope       // Wobble 3, 3600 ticks at 1/60 → |AimOffset - RestOffset| <= 3 + 1e-4; mean magnitude > 0.5
Wander_IsSlow                       // Wobble 3: no tick moves the wander by more than 0.25° (at 1/60)
Kick_JoltsThenSettles               // Wobble 0, KickWobble((0, 1)), ticks → peak magnitude in [0.5, 1.5]; after 60 ticks < 0.1
Paused_FreezesTheWander             // Tick(dt 0) leaves AimOffset unchanged
ResetWobble_ClearsTheKick           // Wobble 0, KickWobble((0, 1)), 2 ticks, ResetWobble, 1 tick → AimOffset == RestOffset
```

- [ ] **Step 2: Run, see it fail.**

- [ ] **Step 3: Implement.** Wander = two `Mathf.PerlinNoise` channels (different seeds) sampled at a phase advanced by `Lerp(WobbleRate, TiredWobbleRate, Tiredness)`, each mapped to −1..1, the pair scaled ×1.6 and clamped to magnitude 1, × `Min(input.Wobble, MaxWobble)`. Plus a 2D spring (`WobbleStiffness`, `WobbleDamping`) that `KickWobble` pushes (an impulse sized so the peak ≈ the kick). The wander isn't multiplied by the steadiness scale again (the envelope already includes it). `deltaTime` ≤ 0 returns early, as `Tick` already does. The driver calls `ResetWobble()` when the held item changes.

- [ ] **Step 4: Run all EditMode tests.** All pass except the known localization test.

- [ ] **Step 5: Play check.** `Hold("AK47")`, standing still, 3 captures 0.5 s apart: the gun model visibly drifts and the dot drifts with it. Walk forward (virtual gamepad left stick up for 1 s, then capture): bigger drift. Hold the trigger for 10 rounds and capture mid-burst: the gun jolts. Exit play mode.

- [ ] **Step 6: Docs and checkpoint.** WEAPONS.md Aim sway: a Hip wobble row (wander inside the envelope, kicks, rates, where to tune). `git status`.

### Task 4: The large dynamic cross

**Files:**

- Modify: `Assets/_Game/_Core/PlayerSystem/PlayerAim.cs` (`Tick(Quaternion aimOffset, Quaternion restOffset, float spreadDegrees)`, `CrosshairCentre`)
- Modify: `Assets/_Game/_Core/PlayerSystem/HeldItemDriver.cs` (line ~120: pass `_sway.RestRotation`)
- Modify: `Assets/_Game/_Core/PlayerSystem/IPlayer.cs`, `Player.cs` (`Vector2 CrosshairCentre`)
- Modify: `Assets/_Game/_Core/UI/Widgets/CrosshairStyle.cs`, `Assets/_Game/_Core/UI/Hud/CrosshairHud.cs`
- Modify: `Assets/_Game/_Core/UI/Prefabs/HUD.prefab` (through a CLI script: tick sizes, hit marker under the dot)
- Modify: `Assets/_Game/_Core/_Debug/GunReadout.cs` (line 95: "Wobble" instead of "Cone")
- Data: `Assets/_Game/Data/UI/CrosshairStyle.asset`
- Test: `HeldMotionTests.cs` → `PlayerAimTests` (centre math)
- Docs: `docs/technicals/UI.md` (Crosshair section)

**Interfaces:**

- Consumes: `HeldItemSway.RestRotation` (Task 3); `HeldItem.Spread` (= wobble + pellet spread, Task 2).
- Produces: `IPlayer.CrosshairCentre` (half-screen-heights, like `CrosshairPosition`); `CrosshairStyle`: `MinGap` 10 (px, the cross never closes tighter), `OpenSpeed` 40, `CloseSpeed` 8 (rates), `FadeSpeed` kept; `MinSpread`, `SpreadFade` and `SpreadSpeed` removed (the cross no longer hides at small spread).

- [ ] **Step 1: Test the centre.** `PlayerAimTests.Centre_IgnoresTheWobble`: needs a testable split, so add `public static Vector2 PlayerAim.Centre(Quaternion restOffset, float tanHalfFov)` (ScreenPosition of the rest direction) and assert that a rest offset of 1° right maps to `tan(1°)/tan(30°)` across with `Tan30`, independent of the full aim offset.

- [ ] **Step 2: Run, see it fail; implement; run, see it pass.**

- [ ] **Step 3: HUD behaviour.** `CrosshairHud`: the crosshair node moves to `CrosshairCentre × pixels`; the dot's `anchoredPosition` = `(CrosshairPosition − CrosshairCentre) × pixels`; the four lines sit at radius `Max(MinGap, spread)`, eased toward the target with `OpenSpeed` when growing and `CloseSpeed` when shrinking (`Damping.ByRate`); the lines share the dot's aim / sprint fade and never fade on small spread. Keep the under-half-pixel skip.

- [ ] **Step 4: Prefab.** A CLI `script run` (scratchpad) opens HUD.prefab with `PrefabUtility.LoadPrefabContents`: `SafeFrame/Center/Crosshair` up / down lines 2 × 12 px, left / right 12 × 2 px (pivot at the end nearest the centre, so the radius is the gap); reparent `HitMarker` under the dot keeping its local position; save; `git diff` shows only those changes. Set the style asset's new values.

- [ ] **Step 5: Recompile, all EditMode tests pass** (except the known one).

- [ ] **Step 6: Play check.** Captures with the AK at the hip: standing (gap ≈ envelope 3° in px; the dot inside), walking (wider), mid-burst (wider still, then closing), crouching still (tighter, but not under `MinGap`), aimed (cross and dot faded). Pistol standing: the cross clear of the dot and the hit marker. Exit play mode.

- [ ] **Step 7: Docs and checkpoint.** UI.md Crosshair: cross at the rest aim, the dot wandering inside, the open/close rates, `MinGap`, where to tune. WEAPONS.md crosshair row. `git status`.

### Task 5: Far Cry hip framing

**Files:**

- Modify: `Poses/Hip` in `Assets/_Game/Content/Weapons/{AK47,MP5,Pistol,PumpShotgun,DoubleBarrel}/<Gun>.prefab`
- Modify: `Poses/Hip` (and `PosePivot`, which `GunBlockoutSetup` copies from Hip) in the 10 block-out prefabs with their own poses (Glock17, DesertEagle, Uzi, P90, M1Carbine, M4, FAL, HuntingRifle, AA12, RPD); AK74M inherits the AK47's
- Docs: WEAPONS.md (pose paragraph near line 116, with the Hip values)

**Interfaces:**

- Consumes: `AimReview` (Task 2) for captures.
- Produces: nothing new in code.

- [ ] **Step 1: Baseline captures.** Hip view of each base gun standing still at the range.

- [ ] **Step 2: Tune each base gun's `Poses/Hip`** (prefab edits through `PrefabUtility.LoadPrefabContents`, never YAML), keeping the barrel pointing at the screen centre 20 m out. Target at 16:9: the front sight about 56–62 % across and 55–60 % down; the gun in the lower right, upright (roll within ±3°); the stock leaving the bottom-right edge; the right hand visible on the grip. Iterate capture → adjust until all five match; record each final position and rotation.

- [ ] **Step 3: Block-outs.** Apply each base gun's hip change (position and rotation delta) to the block-outs that reference it, to both `Poses/Hip` and `PosePivot` (what `GunBlockoutSetup` would produce on a rebuild); confirm `GunBlockoutSetup` derives Hip from its reference (`Copy(refPoses.Find("Hip"), ...)`), so a rebuild reproduces it.

- [ ] **Step 4: Play check.** Captures of all 16 guns at the hip in a contact sheet; the cross and dot from Task 4 sit on the barrel's line. Fire a short burst with the AK: the tracers leave the muzzle toward the dot.

- [ ] **Step 5: Run all EditMode tests** (prefab wiring tests like `GunPartsTests` must still pass).

- [ ] **Step 6: Docs and checkpoint.** WEAPONS.md pose paragraph: the new Hip values and the Far Cry framing rule. `git status`.
