# Dog Companion, Phase 2 (fighting) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Start only after Phase 1 has been played and accepted by the user.** Re-read the Phase 1 code first: names below follow the Phase 1 plan, so adjust them if Phase 1 changed any.

**Goal:** Loki fights the way the user designed it:
- an early-warning growl;
- bite and run (circle, dart in, bite with a stagger, get out);
- Q at an enemy sends it in; Active and Passive stances;
- a rare takedown that drags an enemy down (off until upgraded);
- enemies retaliate only when provoked and never chase;
- toughness, knock-down, help up and limp, with no death;
- a HUD toughness bar;
- every combat number upgradable through one stat sheet.

**Architecture:**
- **Dog side:** `DogStats` holds the base values plus upgrade sources. `DogCombat` (plain C#) picks targets, finds openings and rolls takedowns. New tree branches drive the skirmish.
- **Enemy side:** the EnemySystem gains `TargetKind.Dog` and a per-brain retaliation window checked in `EnemyBrain.ChooseTarget`. Loki is never registered in `TargetRegistry`.

**Tech Stack:** as Phase 1, plus `DamageService` / `DamageInfo`, `EnemyBrain` / `EnemyBody` / `EnemyAnimator`, `StaggerProfile`, `RandomService`, `TimeService.RequestHitStop`, the HUD `StatBar`.

**Spec:** `docs/superpowers/specs/2026-11-23-dog-companion-design.md`:
- Phase 2;
- "Enemies fight back only when provoked";
- "Stats (upgrade-ready)";
- behaviours 13, 14, 15 and 16.

## Global Constraints

- Everything in Phase 1's Global Constraints.
- **Start stats** (`Data/Dogs/DogStatBlock.asset`), every value verbatim from the spec's stat table:

  | Stat | Start value |
  |---|---|
  | BiteDamage | 12 |
  | BiteCooldown | 8 |
  | MaxBiteClass | 0 |
  | StaggerTime | 0.9 |
  | RetaliationWindow | 2.5 |
  | CircleRadius | 5 |
  | CircleSpeed | 3.5 |
  | FeintChance | 0.25 |
  | EscapeSpeed | 7 |
  | DodgeChance | 0.35 |
  | Toughness | 100 |
  | RegenPerSecond | 2 |
  | HelpUpToughness | 0.3 |
  | LimpTime | 30 |
  | RetreatAt | 0.25 |
  | EngageRange | 15 |
  | SentChaseRange | 30 |
  | SenseRange | 25 |
  | TakedownChanceBase | 0 |
  | TakedownChanceBonus | 0 |
  | MaxTakedownClass | 0 |
  | TakedownCooldown | 20 |
  | PinTime | 1.5 |

- **Weight-class order** in `DogSettings.Classes`: the `StaggerProfile` assets `Data/Enemies/Stagger/Light`, `Medium`, `Heavy`, `Boss`, in that order.
- **Fixed numbers** (not upgradable, in `DogSettings`):

  | Setting | Value |
  |---|---|
  | Regen delay | 10 s unhurt |
  | Self-rise | after 90 s down |
  | Retaliation radius | 3 m |
  | Melee reach slack | +0.5 m |
  | Opening | facing away > 60°, or circled ≥ 1.5 × cooldown |
  | Room to circle | at least 3 of 8 points reachable |
  | Sent: gives up after | 10 s with no bite |
  | Fight over | 5 s with no enemy aware of the player within 20 m |
  | Warning growl | once per enemy group per 30 s |
  | Takedown hit-stop | 0.06 s |
  | HUD | shown while Loki follows within 40 m or is fighting; warning below 30% |
  | Growl gap | 4 s |
  | Player hurt (behaviour 13) | below 30% health: whine (within its gap) and keep within the inner ring |

- **Enemy changes stay minimal:** append `TargetKind.Dog`; add the retaliation window; add ranged retaliation without repositioning. Enemies that aren't provoked behave exactly as before. The existing enemy tests must stay green.
- **SoundIDs** (approved 2026-11-23): `Dog_Growl`, `Dog_Bite`, added to `Data/Audio/Dogs.asset`.
- **Save:** add stance, toughness, the down state and limp time left; bump `SaveService.Version`.

## Review Focus

1. **Enemy behaviour when Loki isn't involved.** It must be unchanged: no enemy targets Loki unless bitten or near a bite.
   - Pinned by: Task 3 `Retaliate_WithoutWindowNeverTargetsDog`, plus a full EnemySystem test run.
2. **Retaliation that chases.** Once Loki is out of reach, the enemy goes back to the player on its next think.
   - Pinned by: Task 3 `Retaliate_DogOutOfReachDropsTheWindow`, Task 8 probe `NoChase`.
3. **The dog stuck down forever, or helped up mid-fight in a broken state.**
   - Pinned by: Task 2 `Toughness_SelfRisesAfter90Seconds`, `HelpUp_SetsThirtyPercentAndLimps`.
4. **Takedowns that are too frequent.** With the start stats there are none at all; with the review values the chance follows the formula and the cooldown applies.
   - Pinned by: Task 2 `Takedown_OffAtStartStats`, `Takedown_ChanceFollowsMissingHealth`, `Takedown_CooldownBlocks`.
5. **An Active Loki biting an unaware enemy** (it would spoil a sneak).
   - Pinned by: Task 4 `Combat_ActiveIgnoresUnawareEnemies`.

---

### Task 1: DogStats (upgrade-ready sheet)

**Files:**
- Create: `_Core/DogSystem/DogStats.cs`:
  - `enum DogStat`: the 23 stats above, in table order; append new ones at the end only;
  - `DogStatBlock : ScriptableObject` with `[SerializeField] Dictionary<DogStat, float> _base` and `float Base(DogStat)`;
  - `interface IDogUpgradeSource { float Modifier(DogStat stat); }`;
  - `sealed class DogStatSheet`: `DogStatSheet(DogStatBlock block)`, `void Add(IDogUpgradeSource source)`, `void Remove(IDogUpgradeSource source)`, `float Get(DogStat stat)` = base + the sum of modifiers, and `int GetClass(DogStat stat)` (rounded down, clamped to `Classes`).
- Create: `Data/Dogs/DogStatBlock.asset` (through `DogSetup.Data`), and `DogSettings.Classes`.
- Test: `Tests/EditMode/DogCombatTests.cs`:
  - `Stats_EveryStatHasABase`: loads the asset and checks every `DogStat` value.
  - `Stats_UpgradeSourcesAdd`
  - `Stats_ClassIsClampedToTheList`

- [ ] **Step 1:** Write the failing tests.
- [ ] **Step 2:** Run `--filter DogCombat`. Expected: FAIL.
- [ ] **Step 3:** Implement and create the asset.
- [ ] **Step 4:** Expected: PASS.

### Task 2: Toughness, down, help up, limp, takedown rolls (DogState)

**Files:**
- Modify: `_Core/DogSystem/DogState.cs`:
  - `float Toughness`, `bool Down`, `float LimpUntil`, `DogStance Stance`;
  - `void Hurt(float amount, float now)`, which marks the time for the regen delay and goes Down at 0;
  - `void Tick(float dt, float now, DogStatSheet stats)` for regen and self-rise;
  - `void HelpUp(float now, DogStatSheet stats)`;
  - `bool Retreating(DogStatSheet stats)` (at or below `RetreatAt`; never while sent);
  - `bool TryTakedown(float healthFraction, int enemyClass, float now, float roll, DogStatSheet stats)`, with the roll passed in;
  - `void SetStance(DogStance)`.
- Test: `DogCombatTests`:
  - `Toughness_HitsDownAtZero`
  - `Toughness_RegensOnlyAfterTenSecondsUnhurt`
  - `Toughness_SelfRisesAfter90Seconds`
  - `HelpUp_SetsThirtyPercentAndLimps`: toughness 30, limping until +30 s.
  - `Retreat_AtQuarterToughness`
  - `Takedown_OffAtStartStats`
  - `Takedown_ChanceFollowsMissingHealth`: base 0.05, bonus 0.25, health 0.2 → chance 0.25. A roll of 0.24 succeeds and 0.26 fails.
  - `Takedown_ClassAboveMaxNever`
  - `Takedown_CooldownBlocks`

- [ ] **Step 1:** Write the failing tests.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Expected: PASS.

### Task 3: Enemy retaliation window

**Files:**
- Modify: `_Core/_Common/Entities/ITargetable.cs`: append `Dog` to `TargetKind`.
- Modify: `Data/Enemies/EnemySystemSettings.asset`: `AttackersPerTarget` Dog = 2.
- Modify: `_Core/EnemySystem/EnemyBrain.cs`:
  - fields `ITargetable _retaliate; float _retaliateUntil;`;
  - `public void Retaliate(ITargetable target, float until)`, which sets them and `_nextRetarget = 0`;
  - at the top of `ChooseTarget()`, before the cached return: while `Time.time < _retaliateUntil && _retaliate.IsTargetable` and the flat distance ≤ the melee attack range + 0.5 → `return Target = _retaliate`. Otherwise clear both and fall through.
  - For testability, put the check in a static `bool KeepsRetaliating(Vector3 self, Vector3 target, float reach, float now, float until, bool targetable)`.
- Modify: `_Core/EnemySystem/Ranged/RangedTasks.cs`. While a ranged brain is retaliating (`brain.Retaliating`), `TakeRangedPosition` returns Success without moving and `RangedAttack` aims at the dog. The reach for ranged is `RangedSettings.MaxRange`.
- Test: `Tests/EditMode/EnemyRetaliationTests.cs`:
  - `Retaliate_WithoutWindowNeverTargetsDog`
  - `Retaliate_InReachKeepsTheDog`
  - `Retaliate_DogOutOfReachDropsTheWindow`
  - `Retaliate_WindowExpires`
  - `TargetKind_DogIsAppendedLast`: `(int)TargetKind.Dog == 3`.

- [ ] **Step 1:** Write the failing tests.
- [ ] **Step 2:** Run `--filter "Retaliat|TargetKind"` (single word filters if the pipe fails: run each).
- [ ] **Step 3:** Implement. Then run the whole `Enemy` filter. Expected: PASS, and the existing enemy tests are still green.

### Task 4: DogCombat (targets, openings, classes)

**Files:**
- Create: `_Core/DogSystem/DogCombat.cs`, plain C# over a list of `readonly struct FoeView { int Id; Vector3 Position; Vector3 Forward; bool Ranged; bool AwareOfPlayer; bool AttackingPlayer; bool Swinging; float HealthFraction; int Class; bool Alive; }`. `DogManager` fills these from `EnemyRegistry` (a linear scan within `SenseRange`).
  - `int PickTarget(IReadOnlyList<FoeView> foes, Vector3 player, DogStance stance, int sentId, DogStatSheet stats)`:
    - sent → `sentId` while alive and within `SentChaseRange`;
    - Passive → −1;
    - Active → among foes aware of the player within `EngageRange` of the player: ranged first, then attacking the player, then nearest to the player.
  - `bool CanBite(in FoeView foe, DogStatSheet stats)`: class ≤ `MaxBiteClass`.
  - `bool IsOpening(in FoeView foe, Vector3 dog, float circledFor, float cooldown)`: facing away > 60°, or swinging at someone else, or `circledFor ≥ 1.5 × cooldown`.
  - `int WarnTarget(IReadOnlyList<FoeView> foes, Vector3 dog, float range)`: the nearest foe that isn't aware of the player.
- Test: `DogCombatTests`:
  - `Combat_ActiveIgnoresUnawareEnemies`
  - `Combat_PrefersRangedThenAttackingPlayer`
  - `Combat_PassiveNeverPicks`
  - `Combat_SentOverridesAndRespectsChaseRange`
  - `Combat_AboveClassCannotBite`
  - `Combat_OpeningWhenFacingAwayOrWaitedLong`

- [ ] **Step 1:** Write the failing tests.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Expected: PASS.

### Task 5: Animator and prefab for combat

**Files:**
- Modify: `AgentScripts/Dogs/DogSetup.cs` `Controller()`, adding:
  - Stance 1 (combat) and 3 (injured) locomotion trees;
  - `Attack` + `AttackIndex`: Bite_Forward v2, Front_L, Front_R, Charge_Low (feint);
  - `Dodge` + `Side`;
  - `Down` / `Rise` + `Side`: Death1_L/R → a held lying pose; Death_Recovery_L/R;
  - growl idles (Combat_Idle_Growl / Bark);
  - a pin loop: Bite_Forward v2 looping copy.
  - Re-run the step. Re-measure the combat-trot ground speed into `DogSettings`.
- Modify: `Dog.cs`:
  - implements `ITargetable`: `Kind = Dog`, `IsTargetable = !Down`, `PointFor` = the root;
  - `TakeDamage`:
    - from the player: Phase 1 behaviour;
    - while Down: ignored;
    - while escaping: a `DodgeChance` roll on stream `Dog` → dodge and return default;
    - else `State.Hurt` plus a flinch by side or Down, returning an applied result. `IsDead` is always false.
- Test: `DogTests`:
  - `TakeDamage_FromEnemyReducesToughness`
  - `TakeDamage_WhileDownIgnored`
  - `Dog_IsNeverDead`

- [ ] **Step 1:** Write the failing tests.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement and run the setup step.
- [ ] **Step 4:** Expected: PASS.

### Task 6: Fight behaviour (tree branches and the bite)

**Files:**
- Modify: `_Core/DogSystem/DogTasks.cs`, adding:
  - Conditionals: `DogIsDown`, `DogHasFoe`, `DogIsRetreating`, `DogShouldWarn`, `DogPlayerHurt`.
  - Actions:
    - `DogLieDown`: whine within the voice budget.
    - `DogWarn`: face, Alert / growl, at most once per group per 30 s.
    - `DogSkirmish`, which runs circle, feint, opening, dart, bite, escape, and the no-room fallback:
      - **Bite:** `DamageService.Deal(body, new DamageInfo(stats.Get(BiteDamage), DamageType.Melee, point, push, dogGO))`, then `brain.Stagger(stats.Get(StaggerTime))` and `brain.Animation?.Hit(push, false)`. Then `Retaliate(dog, now + RetaliationWindow)` for the target and every live enemy within 3 m.
      - **Takedown:** if `State.TryTakedown(...)` succeeds, `Deal(body, Health.Current, force toward the dog)` for the dragged-down fall, a `PinTime` pin with retaliation windows to enemies within 3 m, `TimeService.RequestHitStop(0.06f, 0f)`, and the Bite and Growl sounds.
    - `DogBarkAtBig`: circle and bark only, for enemies above the class.
    - `DogBackOff`: heel while retreating.
    - `DogStayCloseHurtPlayer`: behaviour 13.
- Modify: `AgentScripts/Dogs/Dog.tree.txt`. The new priority order is:
  1. Down → lie
  2. Whistled
  3. Petting
  4. Catch up
  5. Retreating → back off
  6. Has foe → skirmish
  7. Should warn → warn
  8. Player hurt → stay close
  9. then Phase 1's Follow, Stay, Night and Home branches
  
  Rebuild with TreeKit.
- Modify: `DogManager`:
  - fills the `FoeView`s within `SenseRange` every think;
  - the fight-over timer;
  - sends from the whistle;
  - the hold-E prompt becomes "Help Loki up" while Down;
  - the Active / Passive wheel slice;
  - the HUD data.
- Modify: `PlayerWhistle` via `IWhistleFallback`, or a new `IEnemyWhistleHandler`, whichever is cleaner: the nearest aim hit's parent `EnemyBrain` → `DogManager.Sic(brain)`; the prompt "[Q] Sic Loki".
- Test: `DogTests.Bite_StaggersDamagesAndOpensRetaliationWindows`, using an EditMode enemy fixture if one exists, otherwise a play probe only (Task 8). Note which one in DOGS.md.

- [ ] **Step 1:** Write the failing tests where it's possible to.
- [ ] **Step 2:** Implement and rebuild the tree.
- [ ] **Step 3:** Recompile clean; `--filter Dog` and `--filter Enemy` PASS.

### Task 7: HUD bar, save, sounds, console

**Files:**
- Create: `_Core/UI/Hud/DogHud.cs`, a `PlayerHudView`-style presenter over a `StatBar` instance with the dog icon. It shows and hides by the rule in Global Constraints, with a warning colour below 30% and a down state. Built by `DogSetup.Hud` with a `StatBarStyle` asset `Data/UI/DogToughness.asset`.
- Modify: `DogSaveSection`: add `Stance`, `Toughness`, `Down`, `LimpLeft`; bump `SaveService.Version`.
- Modify: `DogSetup.Audio`: add `Dog_Growl` and `Dog_Bite`; append them to `DogSound`.
- Modify: `DogCommands`: add `stance`, `toughness`, `down`, `up`, `sic`, `takedown <base> <bonus>`.
- Test:
  - `DogStateTests.Save_RoundTripsCombatState`
  - `Settings_EveryDogSoundHasAnId` (still passes with the new sounds)

- [ ] **Step 1:** Write the failing tests.
- [ ] **Step 2:** Implement.
- [ ] **Step 3:** Expected: PASS; capture the HUD with Loki at 25%.

### Task 8: Play checks and docs

**Files:**
- Modify: `AgentScripts/Dogs/DogPlayCheck.cs`, adding probes. Spawn goblins with `enemy.spawn` or the console commands.

  | Probe | Passes when |
  |---|---|
  | `Warn` | an unaware goblin 20 m out → Loki growls and faces it; no bite |
  | `Skirmish` | an aware goblin → circle, then a bite within cooldown × 1.5 + 2 s, then escape to ≥ 4 m |
  | `NoChase` | after a bite, Loki 6 m out → the goblin targets the player again within 0.5 s |
  | `Retaliation` | the goblin swings at Loki only within the window and within reach |
  | `ArcherFlush` | an archer and a melee goblin → the archer is bitten first; only the bitten archer shoots at Loki |
  | `BigEnemy` | a Medium-class enemy → circle and bark only |
  | `Sic` | Passive, then Q at a goblin → Loki attacks, then returns to heel when it's dead |
  | `BackOff` | plain Q in a fight → heel until the fight ends |
  | `Down` | toughness 0 → down; nobody targets it; hold E → up at 30% and limping |
  | `SelfRise` | down for 90 s → up |
  | `Retreat` | 20% → backs off; Q at an enemy still sends it |
  | `Takedown` | `dog.takedown 1 0` → a bite drags the goblin down toward Loki, then a 1.5 s pin |
  | `Hud` | the bar shows, warns and hides |
  | `PlayerHurt` | player at 25% → whine, stays close |
  | `AfterLoadCombat` | stance and toughness survive a save and load |

- Modify: `DOGS.md` (Phase 2 sections), `CODE_STRUCTURE.md`, `UI.md` (the DogHud), `CONSOLE.md`, `SAVE.md`, `RAIDS.md` or the enemy docs (the retaliation rule) and `.agents/skills/aipocalypse-enemy-ai/SKILL.md` (a one-line note on `TargetKind.Dog` and `Retaliate`).

- [ ] **Step 1:** Run all the probes in WorldMap Play. Expected: all PASS, Phase 1 probes included.
- [ ] **Step 2:** Run the full EditMode suite. Expected: only the 4 known COZY failures. Recompile clean and the console has 0 errors.
- [ ] **Step 3:** Report and give the user the review values for takedowns (`dog.takedown 0.05 0.25`). Don't commit.
