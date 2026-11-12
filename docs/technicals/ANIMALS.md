# Farm animals

Cows and sheep that live in a pen, graze on a pasture when released, come home at the bell, and earn Tokens the player collects with E. Code in `_Core/AnimalSystem/` (namespace `MB13.Aipocalypse.Core.AnimalSystem`), content in `Content/Animals/` and `Content/Structures/Pen/`, data in `Data/Animals/`, script map in [CODE_STRUCTURE.md](CODE_STRUCTURE.md). Design: [FARM_AND_ECONOMY.md](../gdd/FARM_AND_ECONOMY.md); what was built is the user's pick of 2026-11-12 below, not the whole GDD.

## How it plays

1. **Penned.** The herd (2 cows, 4 sheep in WorldMap) starts in the pen in the yard's west corner. It grazes there and earns Tokens at the penned rate.
2. **Release.** Look at the pen gate: **[E] Open Pen**. The herd walks out through the home gate to the pasture on the meadow and earns double there. With the home gate shut there is no way out: it keeps grazing in the pen.
3. **The bell.** **[E] Ring Bell** on the post by the pen gate (it only works there). Every animal walks home at its own pace, cows slowest. Look at one on its way: **[E] Hurry** (once its Tokens are collected) makes it, every animal within `HurryRadius` and the whole sheep flock run. Once all are in, the pen gate shuts itself.
4. **Collecting.** An animal holding at least `CollectAt` Tokens shows a coin over its back. Look at it within interact range: **[E] Collect Tokens**. Its Tokens burst out of the coin and arc to the player's feet, where the pickup magnet takes them. Walking up alone takes nothing. The pen's rail is about 1 m high: the player (jump 1.1 m) hops it, animals can't.
5. **Night.** Outside the waking hours (06:00–20:00) animals sleep where they stand and earn nothing. The bell still wakes them and brings them home.

Raiders ignore animals for now: nothing steals or hurts them.

## Rules

| Rule | Value | Where |
| --- | --- | --- |
| Tokens per waking day, penned / roaming | Cow 20 / 40, sheep 6 / 12 | `AnimalDefinition.TokensPerDayPenned`, `TokensPerDayRoaming` |
| Earned over | The waking hours of the world clock (a clock jump or a sleep earns for the waking hours it passes) | `AnimalSettings.Awake`, `AnimalEarnings.AwakeHours` |
| Roaming | The animal stands inside the pasture's disc; anywhere else pays the penned rate | `AnimalPasture.Contains` |
| Holds at most | `HoldDays` (2) roaming days, then it stops earning until collected | `AnimalDefinition.MaxHeld` |
| Released | The pen gate is open and the bell hasn't rung since | `HerdOrders.Released` |
| Called home | The bell rang with the gate open. Opening the gate again (after it shut) releases again | `HerdOrders.Call`, `OpenGate` |
| The gate | Won't close on an animal within `DoorwayRadius`; shut, it cuts the animals' navmesh. Not a barrier: raiders don't target it | `PenGate` |
| Can't get home | A called animal with no way into the pen (the pen gate or the home gate is shut) waits by the pen gate and looks again every 2 s | `WalkHome` |
| Flock | A kind with `FollowsLeader` (sheep) grazes within `FlockRadius` of the first of its kind once that one is on the pasture | `ChooseGrazeSpot` |
| Hurry | Lasts until the animal is home (or its walk ends) | `Animal.Hurry`, `Calm` |
| One key, two uses | E on an animal collects its Tokens when it shows the coin, else hurries it while called home | `Animal.Interact` |

The player's choices (2026-11-12): Tokens collected with E on the animal (changed the same day from walking up to it); released by opening the pen gate; the bell works only at home; roaming pays double; produce is counted per day; an animal holds two days; cows are few and slow, sheep a flock following its leader; one E hurries every animal nearby; animals sleep where they are at night. Animals earn Tokens for now; produce (milk, wool) comes later. Not built: thieves, names, a dusk readout, selling, feeding.

## Pieces

```text
Animal (prefab root)                     its definition, Tokens (AnimalEarnings), animator parameters, coin marker; IInteractable "Collect" / "Hurry"; no Update
AnimalDefinition (SO, Content/Animals)   speeds, grazing, flock, Tokens per day, hold, CollectAt, sounds
AnimalSettings (SO, Data/Animals)        Enabled, think interval, waking hours, the Token burst, hurry / wait / doorway radius, sounds
AnimalHerd (scene service)               the animals in play, HerdOrders, the pen and pasture, Asleep, Hurry(from)
HerdOrders (pure, AnimalTests)           GateOpen, Called, Released, Version
AnimalEarnings (pure, AnimalTests)       Earn over waking hours up to a cap, Collect whole Tokens, AwakeHours(from, to, window)
AnimalManager (scene entry point)        every frame: earnings, animation, marker, calls; a think every ThinkInterval; HandOver (from Animal's E); shuts the gate
AnimalTasks (Behavior Designer)          CalledHome, WalkHome, IsNight, SleepHere, ChooseGrazeSpot, WalkToGrazeSpot, GrazeHere
AnimalPen / AnimalPasture / PenGate / AnimalBell   scene components
AnimalSaveSection                        "animals": the orders, each animal by SaveId (position, yaw, Tokens held)
AnimalInstaller (SceneInstaller)         registers the above, injects the scene's animals, gates and bells (collected on save / Play)
```

- **Legs and tree:** an animal is the `WanderCapsule` contract without the enemy parts: `FollowerEntity` + `EnemyMover` (its `Initialize(navigation, speed, stopDistance)` overload) + a Manual `BehaviorTree`, on the `Animal` layer. The tree is `Content/Animals/_Shared/Animal.asset`, built from `AgentScripts/Animals/Animal.tree.txt`: come home (bell), else sleep (night), else graze. Only `AnimalManager` ticks it.
- **Model and animation:** polyperfect Low Poly Animated Animals ([ASSETS.md](ASSETS.md)). `Content/Animals/<Kind>/Model/` holds a first-party copy of the vendor prefab with its wander script, CharacterController, NavMeshAgent and AudioSource removed; the entity nests it as `Model`. Each kind has its own controller with three parameters: `Speed` (idle → walk → run, the clips retimed to the definition's speeds), `Eat`, `Sleep`.
- **Hand-over:** E on an animal with the coin calls `AnimalManager.HandOver`, which spawns up to `BurstPieces` Token pickups at the coin marker and throws each on an arc (`ArcVelocity`) that lands within `BurstScatter` of the player after `BurstFlightTime`. They pass through the player's body and the herd, so none rests on someone's back.

## In WorldMap

`Home Pen` is its own scene root (a map rebuild of `Landmarks` doesn't delete it): `Fence` (8 `PenRail`, the `PenGate`, posts), `Pen` (`AnimalPen` + `WaitPoint`), `PenBell`, `Pasture` (16 m disc, 32 m in front of the home gate, by the river), `Animals`. `AnimalInstaller` sits on the `GameLifetimeScope`. The pen's two other sides are the home fence.

The rails cut the navmesh for everyone, so raiders walk round the pen. Raiders that break one of the nine home-fence segments behind the pen end up inside it with no way into the yard and go on breaking fence.

## Tuning and adding

| To change | Edit |
| --- | --- |
| A kind's speeds, grazing, Tokens, sounds | `Content/Animals/<Kind>/<Kind>.asset` |
| Waking hours, the Token burst, hurry radius, bell and gate sounds | `Data/Animals/AnimalSettings.asset` |
| Turn the animals off | `AnimalSettings.Enabled`, or remove `AnimalInstaller` from the scene's scope |
| Pen size and place, pasture, herd | `AgentScripts/Animals/AnimalSetup.cs` (`SlotsAlongBack`, `SlotsAlongSide`, `GateSlot`, `PastureDistance`, `PastureRadius`, the herd list) and re-run `World` |

**Another kind:** a `Kind` entry in `AnimalSetup` (vendor prefab, clip file, sizes, its `Tune`), then `Content`. **Another animal in a scene:** drop its prefab under `Home Pen/Animals` and save the scene (the installer collects it; its `SaveId` is generated).

## Setup and checks

`AgentScripts/Animals/AnimalSetup.cs`: `Audio` (the Animals BroAudio library), `Content` (settings, definitions, controllers, model and entity prefabs), `Pen` (rail, post, gate, bell prefabs), `World` (the `Home Pen` root and the installer; animals already placed are kept). `World` saves the scene: check that the scene's diff only adds (a save can also write editor state such as sky values and light colours; revert those hunks). The tree: `TreeKit.Build AgentScripts/Animals/Animal.tree.txt`.

Console (`animal.`): `status`, `release`, `call`, `hurry`, `fill [tokens]`, `collect`.

`AnimalTests` cover the earning and order rules, the coin arc and the prefab wiring. `AgentScripts/Animals/AnimalPlayCheck.cs` plays the whole flow in WorldMap (enter Play first). Play check 2026-11-12 (editor, scripted): grazing in the pen, coins, walking up takes nothing, E hands over, release to the pasture, the roaming rate, the bell, hurry, the gate shutting itself, night, save section capture and restore, no errors.

## Known gaps

- **Not judged by hand:** walk and run pace against the clips (feet may slide), every Token number, the sounds, the bell's look (first-party primitives: no bell model in a declared pack).
- The cow's sleep clip lies it on its side; the coin marker stays at standing height over a sleeping animal.
- A called herd stops where it is when the home gate is shut; nothing tells the player why.
- The pen is a stand-in inside the yard until the new map has a paddock.
- The `Cow_White` vendor prefab placed by hand next to the pen is not part of the system.
