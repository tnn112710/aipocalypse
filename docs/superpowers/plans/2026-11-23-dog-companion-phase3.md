# Dog Companion, Phase 3 (the farm and home life) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Start only after Phase 2 has been played and accepted by the user.** Re-read the Phase 1 and 2 code first and adjust names here to match.

**Goal:** Loki becomes part of the farm:
- the **Herding** order brings the herd home;
- Loki and the robots greet each other;
- it playfully chases sheep;
- it howls on rare nights;
- it barks toward raiders at home, and fights them under the raid rule the user picks (open item below).

**Architecture:**
- **Herding:** new tree branches and `DogState` additions. Herding goes through the existing `HerdOrders.Call()`, `Animal.Hurry()` and `Animal.Startle()`.
- **Robots:** a small public entry point on `Robot` for the greeting. `DogManager` reaches the herd and robot rosters through optional `TryResolve`, so each system still works alone.

**Tech Stack:** as Phases 1–2, plus `AnimalHerd` / `HerdOrders` / `Animal` and `RobotRoster` / `Robot`.

**Spec:** `docs/superpowers/specs/2026-11-23-dog-companion-design.md`, Phase 3, behaviours 17–21.

## Global Constraints

- Everything in the Phase 1 and 2 constraints.
- **Numbers in `DogSettings`:**

  | Setting | Value |
  |---|---|
  | Robot greeting | within 4 m; once per 90 s per robot; both idle (the robot passes its `FreeToChat` rules) |
  | Sheep chase | at home, daytime, every 3–6 min; `Startle(dogPos, 4, run: true)` every 0.5 s for 5 s; sheep only; never animals going home; never during Herding |
  | Howl | at most once a night; a 20% roll between 22:00 and 02:00; at home; not during a raid |
  | Raid barking | bursts of 2–3 barks with 8–15 s gaps, within the voice budget |

- **Optional links:** `AnimalSystem` and `RobotSystem` are optional to `DogSystem`. With either installer missing, the matching behaviours simply never run, and the Herding slice is hidden without a herd.

## Open before starting (user)

- **Raids at home:** which raiders does an Active Loki fight? Under the Phase 2 rule (only enemies aware of the player within 15 m of the player), it would almost never fight raiders heading for the yard. Proposed: an Active Loki at home also engages raiders inside the yard or within 10 m of the fence. The retaliation, knock-down and self-rise rules stay as they are, so a raid never becomes "protect the dog". Waiting for the user's answer; Task 4 follows it.

## Review Focus

1. **Herding with animals stuck, asleep at night, or the herd already home.**
   - Expected: Herding is greyed when nothing is out, and Loki gives up on an animal that makes no progress for 20 s, moving to the next. Herding ends when all are home, or after 3 min.
   - Pinned by: Task 1 `Herding_GreyedWhenNothingOut`, `Herding_SkipsStuckAnimal`, `Herding_EndsWhenAllHome`.
2. **Sheep chase during the herd's going-home walk or at night.** It never happens.
   - Pinned by: Task 2 `SheepChase_NeverWhenGoingHomeOrNight`.
3. **A robot greeting interrupting a robot's job, call, raid shelter or sleep.** It only happens when both are free.
   - Pinned by: Task 3 probe `RobotGreet`, plus the `FreeToChat` reuse.
4. **The howl at most once a night, and never in a raid.**
   - Pinned by: Task 2 `Howl_OncePerNightNotInRaid`.
5. **Bark spam during long raids.** Bursts only, within the budget.
   - Pinned by: Task 4 `RaidBark_BurstsWithGaps`.

---

### Task 1: Herding

**Files:**
- Modify: `DogState`:
  - `bool CanHerd(int animalsOut)`;
  - `int PickHerdTarget(IReadOnlyList<HerdView> animals, Vector3 home)`: the farthest from home, ties broken by the slowest; it skips animals with no progress for 20 s;
  - `bool HerdDone(int home, int total, float startedAt, float now)`: all home, or 180 s.
  - `readonly struct HerdView { int Id; Vector3 Position; float Speed; bool Home; bool Sleeping; float NoProgressFor; }`.
- Modify: `DogTasks`, adding `DogIsHerding` and `DogHerd`. `DogHerd` gets behind the target relative to home, at 3 m. It barks within the budget and calls `Hurry()`, and moves to the next animal when the target is home.
- Modify: `DogManager`:
  - the Herding wheel slice (greyed when `CountHome == Animals.Count`, hidden without `AnimalHerd`);
  - on pick: `HerdOrders.Call()`, then `State.SetOrder(Herding)`;
  - when done: the order becomes Stay at home.
- Modify: `Dog.tree.txt`: Herding goes after the fight branches and before Follow. Rebuild.
- Modify: CSV row `wheel.dog_herding` = Herding.
- Test: `DogStateTests`:
  - `Herding_GreyedWhenNothingOut`
  - `Herding_PicksFarthestThenSlowest`
  - `Herding_SkipsStuckAnimal`
  - `Herding_EndsWhenAllHome`
  - `Herding_GivesUpAfterThreeMinutes`

- [ ] **Step 1:** Write the failing tests.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement and rebuild the tree.
- [ ] **Step 4:** Expected: PASS.

### Task 2: Sheep chase and the night howl

**Files:**
- Modify: `DogState`:
  - `bool TryStartSheepChase(float now, bool day, bool atHome, bool herding, float roll)`, with its 3–6 min timer;
  - `bool TryHowl(int day, int hour, bool atHome, bool raid, float roll)`, once per day index.
- Modify: `DogTasks`, adding `DogChaseSheep`: it picks the nearest sheep within 25 m that isn't `GoingHome` or `Sleeping`, runs at it, calls `Startle` every 0.5 s for 5 s, then trots back with Happy. Add `DogHowl`, which sits and plays the Howl act and sound.
- Test: `DogStateTests`:
  - `SheepChase_EveryThreeToSixMinutes`
  - `SheepChase_NeverWhenGoingHomeOrNight`
  - `Howl_OncePerNightNotInRaid`

- [ ] **Step 1:** Write the failing tests.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement and add the tree entries.
- [ ] **Step 4:** Expected: PASS.

### Task 3: Robots and Loki greet

**Files:**
- Modify: `_Core/RobotSystem/Robot.cs`, or `RobotManager`, adding `public bool TryGreetVisitor(Vector3 at, float now)`. It succeeds only when the robot passes the same checks `FreeToChat` uses, and then sets up a look-at, a wave and heart eyes through the existing emote paths. Its cooldown is 90 s, reusing the `RobotCooldown` pattern.
- Modify: `DogManager`: when Loki is free (home life or settled following) within 4 m of a robot, call `TryGreetVisitor`. On success Loki sniffs the robot (sniff stance, about 2 s) with a happy act.
- Test:
  - `RobotStateTests.GreetVisitor_OnlyWhenFreeAndOncePer90Seconds`, if the checks are in `RobotState`; otherwise a play probe only.

- [ ] **Step 1:** Write the failing test.
- [ ] **Step 2:** Implement.
- [ ] **Step 3:** Expected: PASS, with the robot tests still green.

### Task 4: Raids at home

**Files:**
- Modify: `DogManager`:
  - on `RaidStarted` while Loki is home or Staying in the yard, raid barking: it faces the nearest raider or broken barrier, in bursts of 2–3 with 8–15 s gaps;
  - engagement per the user's answer to "Open before starting". If the proposal is accepted, `DogCombat.PickTarget` gains a `homeDefence` input: raiders inside the yard or within 10 m of the fence count as engageable when Active.
- Test:
  - `DogStateTests.RaidBark_BurstsWithGaps`
  - `DogCombatTests.HomeDefence_EngagesRaidersNearTheYardOnlyWhenActive`, if accepted.

- [ ] **Step 1:** Write the failing tests.
- [ ] **Step 2:** Implement.
- [ ] **Step 3:** Expected: PASS.

### Task 5: Play checks and docs

**Files:**
- Modify: `AgentScripts/Dogs/DogPlayCheck.cs`, adding probes:

  | Probe | Passes when |
  |---|---|
  | `Herd` | the herd released, then the Herding slice → all home within 3 min; Loki ends Staying at home |
  | `HerdGreyed` | the herd home → the slice is disabled |
  | `RobotGreet` | Loki near a free Bolt → Bolt waves, Loki sniffs; not while Bolt is called |
  | `SheepChase` | `dog.chase` cheat → the sheep flee, Loki comes back happy |
  | `Howl` | `dog.howl` cheat path, plus a roll check at 23:00 |
  | `RaidBark` | a raid starts → barks in bursts |
  | `RaidDefence` | if accepted: a raider in the yard → an Active Loki engages |

  Add the `dog.chase` and `dog.howl` cheats to `DogCommands` and CONSOLE.md.
- Modify: `DOGS.md` (Phase 3), `ANIMALS.md` (the dog herds through `HerdOrders.Call` / `Hurry` / `Startle`), `ROBOTS.md` (`TryGreetVisitor`), `CODE_STRUCTURE.md`.

- [ ] **Step 1:** Run all the probes (Phases 1–3). Expected: all PASS.
- [ ] **Step 2:** Run the full EditMode suite. Expected: only the 4 known COZY failures. Recompile clean and the console has 0 errors.
- [ ] **Step 3:** Report to the user. Don't commit.
