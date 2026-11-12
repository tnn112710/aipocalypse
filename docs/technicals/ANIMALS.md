# Farm animals

Cows, sheep, chickens and pigs that live in a pen, graze on a pasture when released, come home at the bell or when the player sends them, and earn Tokens the player collects with C. Code in `_Core/AnimalSystem/` (namespace `MB13.Aipocalypse.Core.AnimalSystem`), content in `Content/Animals/` and `Content/Structures/Pen/`, data in `Data/Animals/`, script map in [CODE_STRUCTURE.md](CODE_STRUCTURE.md). Design: [FARM_AND_ECONOMY.md](../gdd/FARM_AND_ECONOMY.md); what was built is the user's picks of 2026-11-12 below, not the whole GDD.

## How it plays

1. **Penned.** The herd (2 cows, 6 sheep, a rooster and 3 hens, 2 pigs in WorldMap) starts in the pen in the yard's west corner. It grazes there and earns Tokens at the penned rate.
2. **Release.** Look at the pen gate: **[E] Open Pen**. The herd walks out through the home gate to the pasture on the meadow and earns double there. The sheep keep round their leader, the hens round the rooster. With the home gate shut there is no way out: it keeps grazing in the pen.
3. **One animal.** Look at an animal: **[E] Send Home** sends that one into the pen, where it stays (penned rate) while the rest graze out; **[E] Let Out** on it again frees it. One at a time. Sending the lead sheep (or the rooster) home hands the flock to the next of its kind.
4. **The bell.** **[E] Ring Bell** on the post by the pen gate (it only works there). Every animal answers with its call, a moment apart, and walks home at its own pace, cows slowest. Look at one on its way: **[E] Hurry** makes that one trot. Once all are in, the pen gate shuts itself. The bell also cancels every Send Home.
5. **Collecting.** An animal holding at least `CollectAt` Tokens shows a coin over its back. Look at it: **[C] Collect Tokens**. Its Tokens burst out of the coin with a gold "+12 Tokens" and arc to the player's feet, where the pickup magnet takes them. One animal per press; walking up alone takes nothing. The pen's rail is about 1 m high: the player (jump 1.1 m) hops it, animals can't.
6. **Reactions.** A grazing animal looks up and turns to a player within `NoticeRadius`; one walked into steps aside. A gunshot sends every animal within `ShotRadius` running `FleeDistance` from it, with a scream, even out of its sleep (inside the pen it runs to the pen's far side). Animals walking home ignore both.
7. **Night.** Outside the waking hours (06:00–20:00) animals sleep where they stand, the coin lowered over them, and earn nothing. The bell still wakes them and brings them home.

Raiders ignore animals for now: nothing steals or hurts them.

## Rules

| Rule | Value | Where |
| --- | --- | --- |
| Tokens per waking day, penned / roaming | Cow 20 / 40, sheep 6 / 12, chicken 3 / 6, pig 12 / 24 (the WorldMap herd: 112 / 224) | `AnimalDefinition.TokensPerDayPenned`, `TokensPerDayRoaming` |
| Earned over | The waking hours of the world clock (a clock jump or a sleep earns for the waking hours it passes) | `AnimalSettings.Awake`, `AnimalEarnings.AwakeHours` |
| Roaming | The animal stands inside the pasture's disc; anywhere else pays the penned rate | `AnimalPasture.Contains` |
| Holds at most | `HoldDays` (2) roaming days, then it stops earning until collected | `AnimalDefinition.MaxHeld` |
| Released | The pen gate is open and the bell hasn't rung since | `HerdOrders.Released` |
| Called home | The bell rang with the gate open. Opening the gate again (after it shut) releases again | `HerdOrders.Call`, `OpenGate` |
| Sent home | One animal's own order: walks home and stays in the pen until let out, the bell rings, or a load says otherwise. Offered while the herd is out, or for an animal outside a shut pen | `Animal.SentHome`, `Interact` |
| Keys on an animal | E: Hurry after the bell (not yet home), else Let Out (sent home), else Send Home. C: Collect when it shows the coin | `Animal.TryGetPrompt`, `TryGetCollectPrompt` |
| The gate | Won't close on an animal within `DoorwayRadius`; shut, it cuts the animals' navmesh. Not a barrier: raiders don't target it | `PenGate` |
| Can't get home | An animal going home with no way into the pen (the pen gate or the home gate is shut) waits by the pen gate and looks again every 2 s | `WalkHome` |
| Flock | A kind with `FollowsLeader` (sheep, chicken) grazes within `FlockRadius` of where its leader is heading; a follower whose leader got 1.6 × `FlockRadius` away stops eating and moves after it. The leader is the first of its kind in the hierarchy not sent home (the rooster is placed before the hens) | `ChooseGrazeSpot`, `AnimalFlock`, `AnimalHerd.LeaderOf` |
| Speeds | Walk; Hurry (a trot, about 1.6 × walk); Flee (the run clip's speed) | `AnimalDefinition.WalkSpeed`, `HurrySpeed`, `FleeSpeed` |
| Hurry | One animal per press, after the bell; lasts until it is home (or its walk ends) | `Animal.Hurry`, `Calm` |
| Standing still | An animal halted, eating, asleep or turning to the player can't be pushed: its local avoidance is locked and the others steer round it (before, crowded animals nudged each other and shook). Animals turn at 180 deg/s (enemies 600), smoothed over 0.2 s | `EnemyMover.Initialize(..., holdGround: true)`, `AnimalSetup.TurnSpeed`, `TurnSmoothing` |
| Scares | A shot: run `FleeDistance` (7 m). Walked into (`BumpRadius` 1.3 m): step `StepAside` (2.5 m) at walking pace. A shot turns a step into a run | `AnimalManager.OnShot`, `Animal.Startle`, `MoveAway` |

The player's choices (2026-11-12): released by opening the pen gate; the bell works only at home; roaming pays double; produce is counted per day; an animal holds two days; cows are few and slow, sheep a flock following its leader; animals sleep where they are at night. Later the same day: Tokens collected with C on the animal, one at a time (bulk collecting to come with an upgrade or skill); E sends one animal home and lets it out again, and after the bell speeds up only that one (bulk herding maybe later); 2 more sheep, chickens (rooster + 3 hens, 3 / 6) and pigs (2, 12 / 24); one bigger pen; animals scatter from gunshots (a new `ShotFired` event); the flock fix, the lowered coin over a sleeper, the "+N Tokens" pop, every animal answering the bell, and the look-up / step-aside reactions. Animals earn Tokens for now; produce (milk, wool, eggs) comes later. Not built: thieves, names, a dusk readout, selling, feeding, bulk collecting or herding.

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
- **Model and animation:** polyperfect Low Poly Animated Animals ([ASSETS.md](ASSETS.md)). `Content/Animals/<Kind>/Model/` holds a first-party copy of the vendor prefab with its wander script, CharacterController, NavMeshAgent and AudioSource removed; the entity nests it as `Model`. Each kind has its own controller with three parameters: `Speed` (idle → walk → run, the clips retimed to the definition's walk and flee speeds), `Eat`, `Sleep`. A look whose clips live in another file gets its own controller (`Rooster.controller`).
- **The C key:** the `Collect` action of the Player map (C, gamepad d-pad down) → `InputService.CollectPressed` → `PlayerIntent.Collect` → `PlayerInteractor` uses the focus's `ICollectInteractable` (`_Common/Interaction`) on press; `InteractPromptHud` shows it on its own line under the E prompt (`CollectPrompt` in `HUD.prefab`).
- **Gunshots:** `HeldGun` publishes `ShotFired` (`_Common/Events/CombatEvents.cs`: position, shooter) once per shot; `AnimalManager` subscribes. Nothing else listens yet.
- **Hand-over:** C on an animal with the coin calls `AnimalManager.HandOver`, which spawns the gold `TokenNumber` (Damage Numbers Pro, its own pool) and up to `BurstPieces` Token pickups at the coin marker, throwing each on an arc (`ArcVelocity`) that lands within `BurstScatter` of the player after `BurstFlightTime`. They pass through the player's body and the herd, so none rests on someone's back.

## In WorldMap

`Home Pen` is its own scene root (a map rebuild of `Landmarks` doesn't delete it): `Fence` (4 slots along the home fence's side 0 with the `PenGate`, 8 along side 3, posts), `Pen` (`AnimalPen` + `WaitPoint`), `PenBell`, `Pasture` (16 m disc, 32 m in front of the home gate, by the river), `Animals` (14, the rooster before the hens). `AnimalInstaller` sits on the `GameLifetimeScope`. The pen's two other sides are the home fence; the house stands 4 m past its back run, the cart just past its far end.

The rails cut the navmesh for everyone, so raiders walk round the pen. Raiders that break one of the home-fence segments behind the pen end up inside it with no way into the yard and go on breaking fence.

## Tuning and adding

| To change | Edit |
| --- | --- |
| A kind's speeds, grazing, Tokens, sounds, marker drop | `Content/Animals/<Kind>/<Kind>.asset` (or its `Tune` in `AnimalSetup`, then `Content`) |
| Waking hours, the Token burst and number, answer delay, reactions, bell and gate sounds | `Data/Animals/AnimalSettings.asset` |
| Turn the animals off | `AnimalSettings.Enabled`, or remove `AnimalInstaller` from the scene's scope |
| Pen size and place, pasture, herd | `AgentScripts/Animals/AnimalSetup.cs` (`SlotsAlongBack`, `SlotsAlongSide`, `GateSlot`, `PastureDistance`, `PastureRadius`, the herd list) and re-run `World` |

**Another kind:** a `Kind` entry in `AnimalSetup` (vendor prefab, clip file, sizes, its `Tune`; `LookClips` for a look with its own clip file), add it to `Kinds`, then `Audio` (its sounds) and `Content`. **Another animal in a scene:** add it to the herd list in `World` (placed after the ones already there), or drop its prefab under `Home Pen/Animals` and save the scene (the installer collects it; its `SaveId` is generated). A flock's leader is the first of its kind under `Animals`.

## Setup and checks

`AgentScripts/Animals/AnimalSetup.cs`: `Audio` (the Animals BroAudio library), `Content` (settings, the token number, definitions, controllers, model and entity prefabs), `Pen` (rail, post, gate, bell prefabs), `World` (the `Home Pen` root and the installer; animals already placed are kept, missing ones from the list added; removes the stray vendor `Cow_White`). `World` saves the scene: open WorldMap fresh from disk first (Play leaves it dirty), and check that the scene's diff only touches the pen (a save can also write editor state such as sky values and light colours; revert those hunks). The tree: `TreeKit.Build AgentScripts/Animals/Animal.tree.txt`. The C prompt's line in `HUD.prefab` (a copy of `InteractPrompt` named `CollectPrompt`, 46 px below it) was added by a one-off script; `AnimalTests.Hud_ShowsTheCollectPrompt` guards it.

Console (`animal.`): `status`, `release`, `call`, `hurry` (every animal, the test helper), `fill [tokens]`, `collect`.

`AnimalTests` cover the earning and order rules, the coin arc, the prefab wiring (six animal prefabs, flee faster than hurry faster than walk) and the HUD's C line. `AgentScripts/Animals/AnimalPlayCheck.cs` plays the whole flow in WorldMap (enter Play first). `AgentScripts/Animals/AnimalJitterProbe.cs` packs the herd into a small circle in the pen and measures how much the animals standing still turn, slide and flip direction, and how the walkers weave (2026-11-12, crowded: still animals 5.6 direction flips/s and 4.6 cm/s sliding before the fix, about 0.02/s and 0 after; walkers keep 0.4–0.9 m/s and change direction 1–3 times a second going round the others). Play check 2026-11-12 (editor, scripted), all passed: every kind grazes in the pen; coins, walking up takes nothing, C hands over; look-up and step-aside; release to the pasture, the roaming rate, sheep within 3.8 m and chickens within 8.4 m of their leader (the rooster); Send Home, staying in, Let Out; the lead sheep sent home hands over the flock; a shot sends 10 running at flee speed; the bell, Hurry on one only; the gate shutting itself; night, the coin lowered; save section capture and restore with Send Home; no errors.

## Known gaps

- **Not judged by hand:** walk, trot and run pace against the clips (feet may slide; the chicken and pig authored speeds are guesses), every Token number, every sound (chicken and pig are Gamemaster impersonations), the bell's look (first-party primitives: no bell model in a declared pack), the "+N Tokens" number's size and colour, how the C prompt line sits under the E line on screen (checked in code only).
- The chickens spread up to about twice `FlockRadius` round the rooster.
- Two animals that stop overlapping stay overlapped (neither can push the other); a walker squeezing past several standing ones weaves a little. One play check read a 13.8 m/s top speed on a scared pig on the pasture that later probes didn't reproduce (sideways at most about 5 m/s per frame; probably a vertical snap on the slope).
- The cow's sleep clip lies it on its side; the coin drops 0.9 m over it.
- An animal going home stops where it is when the home gate is shut; nothing tells the player why.
- The pen is a stand-in inside the yard until the new map has a paddock.
