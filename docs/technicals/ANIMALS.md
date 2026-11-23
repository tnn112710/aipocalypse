# Farm animals

Cows, sheep, chickens and pigs that live in a pen, graze on a pasture when released, come home at the bell or when the player sends them, and earn Tokens the player collects with C. Code in `_Core/AnimalSystem/` (namespace `MB13.Aipocalypse.Core.AnimalSystem`), content in `Content/Animals/` and `Content/Structures/Pen/`, data in `Data/Animals/`, script map in [CODE_STRUCTURE.md](CODE_STRUCTURE.md). Design: [FARM_AND_ECONOMY.md](../gdd/FARM_AND_ECONOMY.md); what was built is the user's picks of 2026-11-12 below, not the whole GDD.

## How it plays

**No pen fence in WorldMap (user, 2026-11-22):** the rails and pen gate are gone; the herd lives loose in its home corner of the yard (the `AnimalPen` box, still "the pen" in code) and the bell hangs by the home gate. Where a scene still has a `PenGate`, the old way below (gate releases, bell only calls) still works.

1. **Home.** The herd (2 cows, 6 sheep, a rooster and 3 hens, 2 pigs in WorldMap) starts in its home corner, the yard's west corner. It grazes there, picking its spots inside that box, and earns Tokens at the penned rate.
2. **Release.** Look at the bell by the home gate: **[E] Let Out Herd**. The herd walks out to the pasture on the meadow and earns double there. The sheep keep round their leader, the hens round the rooster. (With a pen gate: **[E] Open Pen** on it.)
3. **One animal.** Look at an animal: **[E] Send Home** sends that one home, where it stays (penned rate) while the rest graze out; **[E] Let Out** on it again frees it. One at a time. Sending the lead sheep (or the rooster) home hands the flock to the next of its kind.
4. **The bell.** While the herd is out the bell offers **[E] Ring Bell** (it only works where it hangs). Every animal answers with its call, a moment apart, and walks home at its own pace, cows slowest. Look at one on its way: **[E] Hurry** makes that one trot. Once all are in, the orders close by themselves and the bell offers Let Out Herd again (with a pen gate: the gate shuts itself). Let Out on the way home releases them again. The bell also cancels every Send Home.
5. **Collecting.** An animal holding at least `CollectAt` Tokens shows a coin over its back. Look at it: **[C] Collect Tokens**. Its Tokens burst out of the coin with a gold "+12 Tokens" and arc to the player's feet, where the pickup magnet takes them. One animal per press; walking up alone takes nothing. The pen's rail is about 1 m high: the player (jump 1.1 m) hops it, animals can't.
6. **Reactions.** A grazing animal looks up and turns to a player within `NoticeRadius`; one walked into steps aside. A gunshot sends every animal within `ShotRadius` running `FleeDistance` from it, with a scream, even out of its sleep (inside the pen it runs to the pen's far side). Animals walking home ignore both.
7. **Daily life.** Grazing is mostly a short step on, head down, then eating again, with now and then a longer walk; sometimes an animal just stands about with one of its spare idle clips and looks round. Every animal rolls its own pace (±10%) and patience (meals, idles and calls ±25%) when it wakes, so no two move in step. Chickens peck in short bursts and sometimes dart. In the last 1.5 hours of the waking day released animals drift to the side of the pasture nearest home. Big turns while walking are arcs, and turning on the spot steps the legs.
8. **Night.** Outside the waking hours (06:00–20:00) animals sleep where they stand, the coin lowered over them, and earn nothing. Pigs lie down and get up again with their own clips, sheep lie and chickens sit to sleep, cows doze standing (the pack has no cow sleep clip). The bell still wakes them and brings them home.

Raiders ignore animals for now: nothing steals or hurts them.

## Rules

| Rule | Value | Where |
| --- | --- | --- |
| Tokens per waking day, penned / roaming | Cow 20 / 40, sheep 6 / 12, chicken 3 / 6, pig 12 / 24 (the WorldMap herd: 112 / 224) | `AnimalDefinition.TokensPerDayPenned`, `TokensPerDayRoaming` |
| Earned over | The waking hours of the world clock (a clock jump or a sleep earns for the waking hours it passes) | `AnimalSettings.Awake`, `AnimalEarnings.AwakeHours` |
| Roaming | The animal stands inside the pasture's disc; anywhere else pays the penned rate | `AnimalPasture.Contains` |
| Holds at most | `HoldDays` (2) roaming days, then it stops earning until collected | `AnimalDefinition.MaxHeld` |
| Released | Let out (by the bell, or the pen gate open) and the bell hasn't rung since | `HerdOrders.Released` |
| Called home | The bell rang while released. Letting out again releases again | `HerdOrders.Call`, `OpenGate`; the gate bell: `HerdOrders.Bell` |
| Sent home | One animal's own order: walks home and stays in the pen until let out, the bell rings, or a load says otherwise. Offered while the herd is out, or for an animal outside a shut pen | `Animal.SentHome`, `Interact` |
| Keys on an animal | E: Hurry after the bell (not yet home), else Let Out (sent home), else Send Home. C: Collect when it shows the coin | `Animal.TryGetPrompt`, `TryGetCollectPrompt` |
| The pen gate (optional; none in WorldMap) | Won't close on an animal within `DoorwayRadius`; shut, it cuts the animals' navmesh. Not a barrier: raiders don't target it | `PenGate` |
| Can't get home | An animal going home with no way into its home (the yard shut, or a pen gate shut) waits at the pen's `WaitPoint` (WorldMap: just outside the home gate) and looks again every 2 s | `WalkHome` |
| Flock | A kind with `FollowsLeader` (sheep, chicken) grazes within `FlockRadius` of where its leader is heading; a follower whose leader got 1.6 × `FlockRadius` away stops eating and moves after it. The leader is the first of its kind in the hierarchy not sent home (the rooster is placed before the hens) | `ChooseGrazeSpot`, `AnimalFlock`, `AnimalHerd.LeaderOf` |
| Speeds | Walk (near the walk clip's own pace, so the feet don't slide); Hurry (a trot, about 1.6 × walk; also a chicken's dart and a straggler catching up with its flock); Flee (the run). Cow 0.85 / 1.4 / 3, sheep 0.52 / 0.9 / 3, chicken 0.35 / 1.1 / 1.6, pig 0.75 / 1.25 / 2.6 m/s, each animal × its own Pace | `AnimalDefinition.WalkSpeed`, `HurrySpeed`, `FleeSpeed`; `Animal.Pace` |
| Clip speeds | The walk and run clips play at speed / (authored × ModelScale), the authored ground speed measured from the clips' planted feet at model scale 1: cow 1.23 / 5.36 (its run is a long trot), sheep 0.48 / 1.27 (a trot: no run clip, so fleeing at 3 m/s plays it about 2.8× fast), hen and rooster 0.25 / 1.0, pig 0.81 / 1.57 (a trot) m/s; model scale cow 0.7, sheep 0.85, pig 0.85, chickens 1 (the polyperfect herd's in-game sizes). Each animal's Animator runs at Pace / its scale, and Speed is fed divided by Pace, so a quicker or smaller animal steps quicker | `AnimalSetup.Kind.AuthoredWalk`, `AuthoredRun`, `ModelScale`; `AgentScripts/Animals/AnimalStrideProbe.cs` |
| Grazing moves | `DriftChance` (cow 0.6, sheep 0.7, chicken 0.8, pig 0.6): a step of `DriftDistance` mostly ahead (any way when a fence is in front), kept where it belongs (pen, pasture, near its leader, the home side at dusk); otherwise a walk to a new spot, in the pen near itself | `ChooseGrazeSpot` |
| Standing about | `IdleChance` (0.2–0.3) of a stop is `IdleTime` with a random spare idle clip (`Idle` parameter 1..`Fidgets`, or the plain idle) and one look round | `GrazeHere` |
| Dusk | The last `DuskHours` (1.5) of the waking day: released non-followers graze within 35% of the pasture's radius of its point nearest the pen; dusk starting ends what they were doing | `AnimalHerd.Dusk`, `ChooseGrazeSpot.HomeSide` |
| Hurry | One animal per press, after the bell; lasts until it is home (or its walk ends) | `Animal.Hurry`, `Calm` |
| Standing still | An animal halted, eating, asleep or turning to the player can't be pushed: its local avoidance is locked and the others steer round it (before, crowded animals nudged each other and shook). Animals turn at 180 deg/s (enemies 600), smoothed over 0.2 s | `EnemyMover.Initialize(..., holdGround: true)`, `AnimalSetup.TurnSpeed`, `TurnSmoothing` |
| Scares | A shot: run `FleeDistance` (7 m). Walked into (`BumpRadius` 1.3 m): step `StepAside` (2.5 m) at its trot. A shot turns a step into a run. Both radii scale by the kind's `ScareScale` (cow 0.8, chicken 1.5) | `AnimalManager.OnShot`, `Animal.Startle`, `MoveAway` |

The player's choices (2026-11-12; the pen fence was removed on 2026-11-22, see above): released by opening the pen gate; the bell works only at home; roaming pays double; produce is counted per day; an animal holds two days; cows are few and slow, sheep a flock following its leader; animals sleep where they are at night. Later the same day: Tokens collected with C on the animal, one at a time (bulk collecting to come with an upgrade or skill); E sends one animal home and lets it out again, and after the bell speeds up only that one (bulk herding maybe later); 2 more sheep, chickens (rooster + 3 hens, 3 / 6) and pigs (2, 12 / 24); one bigger pen; animals scatter from gunshots (a new `ShotFired` event); the flock fix, the lowered coin over a sleeper, the "+N Tokens" pop, every animal answering the bell, and the look-up / step-aside reactions. The natural-behaviour pass (same day, the user's picks): spare idles, grazing in short steps, each animal its own pace and patience, chickens peck and dart, drifting home at dusk, start / stop clips, walk speeds matched to the clips, arcs instead of spinning on the spot (not picked: midday rest, sleeping together, the rooster's dawn crow, calls answering each other, single file, personalities, head look, chewing, bump reactions). Animals earn Tokens for now; produce (milk, wool, eggs) comes later. Not built: thieves, names, a dusk readout, selling, feeding, bulk collecting or herding.

## Pieces

```text
Animal (prefab root)                     its definition, Tokens (AnimalEarnings), SentHome, Startled, animator parameters, coin marker;
                                         IInteractable (E: Send Home / Let Out / Hurry) + ICollectInteractable (C: Collect); no Update
AnimalDefinition (SO, Content/Animals)   speeds (walk, hurry, flee), grazing, flock, Tokens per day, hold, CollectAt, sounds (call, hurried, startled), marker drop asleep
AnimalSettings (SO, Data/Animals)        Enabled, think interval, waking hours, the Token burst and number, answer delay, wait / doorway radius, reactions, sounds
AnimalHerd (scene service)               the animals in play (hierarchy order), HerdOrders, the pen and pasture, Asleep, the player's position, LeaderOf
HerdOrders (pure, AnimalTests)           GateOpen, Called, Released, Version
AnimalEarnings (pure, AnimalTests)       Earn over waking hours up to a cap, Collect whole Tokens, AwakeHours(from, to, window)
AnimalManager (scene entry point)        every frame: earnings, animation, marker, calls, the player's position, step-asides; a think every ThinkInterval;
                                         ShotFired → scares; HandOver (from Animal's C); shuts the gate
AnimalTasks (Behavior Designer)          CalledHome, WalkHome, IsStartled, MoveAway, IsNight, SleepHere, ChooseGrazeSpot, WalkToGrazeSpot, GrazeHere
AnimalPen / AnimalPasture / PenGate / AnimalBell   scene components
AnimalSaveSection                        "animals": the orders, each animal by SaveId (position, yaw, Tokens held, sent home)
AnimalInstaller (SceneInstaller)         registers the above, injects the scene's animals, gates and bells (collected on save / Play)
```

- **Legs and tree:** an animal is the `WanderCapsule` contract without the enemy parts: `FollowerEntity` (calmer turning than an enemy's) + `EnemyMover` (its `Initialize(navigation, speed, stopDistance, holdGround)` overload) + a Manual `BehaviorTree`, on the `Animal` layer. The tree is `Content/Animals/_Shared/Animal.asset`, built from `AgentScripts/Animals/Animal.tree.txt`: come home (bell or sent), else move away (scared), else sleep (night), else graze. Only `AnimalManager` ticks it.
- **Model and animation:** the SICS Toon Farm Pack (since 2026-11-22; polyperfect before, now Avoid, [ASSETS.md](ASSETS.md)). `Content/Animals/<Kind>/Model/` holds a first-party copy of one look of the vendor prefab (the pack keeps every look's mesh in each prefab, switched off: the copy drops them) on the pack's own toon material `TFP_Atlas_1A` (Amplify `Toon/CustomToon`, a stepped light ramp; the user's pick over URP Lit after a comparison); the entity nests it as `Model`. Looks: cow `TFP_Cow_01E` (black and white) and `01A` (brown), sheep `TFP_Sheep_01A` (its `Wool Size` blend shape at 100, the pack's 35: `AnimalSetup.Kind.BlendShapes`), hen `TFP_Chicken_01A`, rooster `TFP_Rooster_01A`, pig `TFP_Pig_01A`; the pack has many more (`AgentScripts/Animals/AnimalLookSheet.cs` renders them). Each kind has its own controller with four parameters: `Speed`, `Eat`, `Sleep` and `Idle` (int). `Stand` (the idle clip) is home: `Eat` and the spare idles (`Idle 1..n`: cow `Idle_02`–`06`, sheep `Idle_02`–`05`, pig `Idle_02`, `03`, chickens `Idle_02`, `WingFlap`) branch from it. `Locomotion` blends idle → walk → run on `Speed`. `Sleep` from any awake state (pig through `LieDown`), then back to `Stand` (pig through `WakeUp`, the others a cross-fade); the cow's `Sleep` is its idle at 0.4 speed. The sheep and rooster clips import as one-shots, so `AnimalSetup` makes looping copies in `<Kind>/Animations/`. The Animators cull completely off screen (they don't update until seen). A look whose clips live in another file gets its own controller (`Rooster.controller`). The FollowerEntity turns at 180 deg/s, 120 on the spot, smoothed, and doesn't rotate on the spot while walking (`allowRotatingOnSpot` off).
- **The C key:** the `Collect` action of the Player map (C, gamepad d-pad down) → `InputService.CollectPressed` → `PlayerIntent.Collect` → `PlayerInteractor` uses the focus's `ICollectInteractable` (`_Common/Interaction`) on press; `InteractPromptHud` shows it on its own line under the E prompt (`CollectPrompt` in `HUD.prefab`).
- **Gunshots:** `HeldGun` publishes `ShotFired` (`_Common/Events/CombatEvents.cs`: position, shooter) once per shot; `AnimalManager` subscribes. Nothing else listens yet.
- **Hand-over:** C on an animal with the coin calls `AnimalManager.HandOver`, which spawns the gold `TokenNumber` (Damage Numbers Pro, its own pool) and up to `BurstPieces` Token pickups at the coin marker, throwing each on an arc (`ArcVelocity`) that lands within `BurstScatter` of the player after `BurstFlightTime`. They pass through the player's body, the herd and the pen's fence (`AnimalHerd.FenceColliders`, from `AnimalPen.Fence`), so none rests on someone's back or drops back into the pen off a rail when collecting across it.

## In WorldMap

`Home Pen` is its own scene root (a map rebuild of `Landmarks` doesn't delete it): `Pen` (`AnimalPen`: the 8.4 × 17.4 m home box in the yard's west corner, 4 slots along the home fence's side 0 and 8 along side 3, no gate; `WaitPoint` 2.5 m outside the home gate), `PenBell` (inside the yard beside the home gate), `Pasture` (16 m disc, 32 m in front of the home gate, by the river), `Animals` (14, the rooster before the hens). `AnimalInstaller` sits on the `GameLifetimeScope`. The home box's two outer sides are the home fence.

No rails since 2026-11-22: nothing but the home fence stands between the herd and raiders. Two of side 3's start-down slots (`Fence 3-04`, `3-05`) border the home box ([DEFENSE.md](DEFENSE.md#in-worldmap)). `PenRail`, `PenPost` and `PenGate` stay in `Content/Structures/Pen/` for other scenes.

## Tuning and adding

| To change | Edit |
| --- | --- |
| A kind's speeds, grazing, Tokens, sounds, marker drop | `Content/Animals/<Kind>/<Kind>.asset` (or its `Tune` in `AnimalSetup`, then `Content`) |
| Waking hours, the Token burst and number, answer delay, reactions, bell and gate sounds | `Data/Animals/AnimalSettings.asset` |
| Turn the animals off | `AnimalSettings.Enabled`, or remove `AnimalInstaller` from the scene's scope |
| Pen size and place, pasture, herd | `AgentScripts/Animals/AnimalSetup.cs` (`SlotsAlongBack`, `SlotsAlongSide`, `GateSlot`, `PastureDistance`, `PastureRadius`, the herd list) and re-run `World` |

**Another kind:** a `Kind` entry in `AnimalSetup` (vendor prefab, clip file, sizes, its `Tune`; `LookClips` for a look with its own clip file), add it to `Kinds`, then `Audio` (its sounds) and `Content`. **Another animal in a scene:** add it to the herd list in `World` (placed after the ones already there), or drop its prefab under `Home Pen/Animals` and save the scene (the installer collects it; its `SaveId` is generated). A flock's leader is the first of its kind under `Animals`.

## Setup and checks

`AgentScripts/Animals/AnimalSetup.cs`: `Audio` (the Animals BroAudio library), `Content` (settings, the token number, definitions, controllers and looping clip copies, model and entity prefabs; a model whose vendor source changed is nested afresh in its entity), `Pen` (rail, post, gate, bell prefabs), `World` (the `Home Pen` root and the installer; animals already placed are kept, missing ones from the list added; removes the stray vendor `Cow_White`). `World` saves the scene: open WorldMap fresh from disk first (Play leaves it dirty), and check that the scene's diff only touches the pen (a save can also write editor state such as sky values and light colours; revert those hunks). The tree: `TreeKit.Build AgentScripts/Animals/Animal.tree.txt`. The C prompt's line in `HUD.prefab` (a copy of `InteractPrompt` named `CollectPrompt`, 46 px below it) was added by a one-off script; `AnimalTests.Hud_ShowsTheCollectPrompt` guards it.

Console (`animal.`): `status`, `release`, `call`, `hurry` (every animal, the test helper), `fill [tokens]`, `collect`.

`AnimalTests` cover the earning and order rules, the coin arc, the prefab wiring (six animal prefabs, flee faster than hurry faster than walk) and the HUD's C line. `AgentScripts/Animals/AnimalPlayCheck.cs` plays the whole flow in WorldMap (enter Play first). `AgentScripts/Animals/AnimalStrideProbe.cs` measures the clips' ground speeds (editor, no Play). `AgentScripts/Animals/AnimalLookSheet.cs` renders offscreen PNG sheets: `Compare` (the pack's toon material against another material, day and evening light), `Looks` (every look in a pack folder) and `Poses` (a model posed by clips). After the Toon Farm switch (2026-11-22) the play check passed in full; sleep and wake states were probed with the animators set to always animate (an unfocused editor renders nothing, so culled animators don't update during scripted checks). `AgentScripts/Animals/AnimalJitterProbe.cs` packs the herd into a small circle in the pen and measures how much the animals standing still turn, slide and flip direction, and how the walkers weave (2026-11-12, crowded: still animals 5.6 direction flips/s and 4.6 cm/s sliding before the fix, about 0.02/s and 0 after; walkers keep 0.4–0.9 m/s and change direction 1–3 times a second going round the others). Play check 2026-11-12 (editor, scripted), all passed twice in a row after the natural-behaviour pass: every kind grazes in the pen; spare idles, chicken darts, each animal its own pace; coins, walking up takes nothing, C hands over; look-up, turning to face the player, and step-aside; release to the pasture, the roaming rate, the flocks gathering round their leaders (the rooster for the hens; stragglers trot back within about 30 s); cows and pigs drifting to the home side at dusk; Send Home, staying in, Let Out; the lead sheep sent home hands over the flock; a shot sends 10 running at flee speed; the bell, Hurry on one only; the gate shutting itself; night, the coin lowered; save section capture and restore with Send Home; no errors.

## The dog (DOGS.md)

- **Herding:** the companion dog brings the herd home: `HerdOrders.Call`, then `Animal.Hurry` on the stragglers.
- **Sheep:** at home it now and then chases them (`Animal.Startle` with run).

## Known gaps

- **Not judged by hand:** whether the feet still slide (clip speeds are measured, not watched; trot speeds blend walk and run clips and may slide a little), how the sheep's sped-up trot reads when it flees, the Toon Farm models' toon shading against the world at every hour and weather, how often they move (in the pen about 30–40% of the time, chickens about 60%), every Token number, every sound (chicken and pig are Gamemaster impersonations), the bell's look (first-party primitives: no bell model in a declared pack), the "+N Tokens" number's size and colour, how the C prompt line sits under the E line on screen (checked in code only).
- The chickens spread up to about twice `FlockRadius` round the rooster. Animals at the slower, clip-matched walk take longer to get anywhere: sheep walk 0.52 m/s (was 1.1), chickens 0.35 (was 0.6).
- Dusk lasts about 50 real seconds at the clock's pace; a herd far out may not reach the home side before it sleeps.
- BroAudio can't set one playing sound's pitch, so each animal varies its call timing only (the entities already randomise pitch).
- Two animals that stop overlapping stay overlapped (neither can push the other); a walker squeezing past several standing ones weaves a little. One play check read a 13.8 m/s top speed on a scared pig on the pasture that later probes didn't reproduce (sideways at most about 5 m/s per frame; probably a vertical snap on the slope).
- Cows sleep standing (no cow sleep clip in the pack); the coin drops 0.4 m over them.
- An animal going home stops where it is when the home gate is shut; nothing tells the player why.
- The pen is a stand-in inside the yard until the new map has a paddock.
