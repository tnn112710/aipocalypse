# Dog companion: Loki (design)

Status: agreed in conversation with the user on 2026-11-23, waiting for review. Nothing is built.

## Intent

A dog that feels like a dog, not a follower script. **Loki** (a placeholder name) comes exploring with the player, has a life of its own around them, and helps in fights. It stays a helper: it never does the player's fighting for them. It is also built so that levelling it up later only means changing numbers.

The user's own words set the bar: "smartly and lively follow the player, but make it like a dog, not just stupidly follow (player moves it moves, player stops it stops)".

## Goals

- **Following like a dog:** a comfort zone, scouting ahead, sniffing, settling down, checking in, swimming, and catching up when left behind. The full list is under Phase 1.
- **Orders on a command wheel** (tap E on Loki): **Follow**, **Stay**, **Herding**, an **Active / Passive** toggle and **Pet**. The wheel takes more slices later (Feed, Fetch).
- **A home of its own:** the SICS Toon Farm dog house in the yard, where Loki rests and sleeps.
- **Orders by whistle (Q):**
  - Q aimed at an enemy: "get it".
  - Plain Q: "come here", which also means "back off" during a fight.
- **Fighting by bite and run** (a skirmisher):
  - circles the enemy, darts in, bites (small damage plus a stagger), gets out;
  - a rare **takedown** drags an enemy down and kills it.
- **Enemies fight back only when provoked**, and never chase Loki.
- **Getting hurt:** Loki is knocked down, the player helps it up, and it limps for a while. It never dies.
- **Upgradable stats:** every number Loki fights with is read through one stat sheet. There is no levelling yet.

## The agreed behaviour list

The numbered list from the conversation and where each item is built:

| # | Behaviour | Phase |
|---|---|---|
| 1 | Comfort zone | 1 |
| 2 | Scouting ahead | 1 |
| 3 | Side, and keeping out of the aim | 1 |
| 4 | Sniffing, digging, marking | 1 |
| 5 | Settling: sit, lie, nap | 1 |
| 6 | Checking in | 1 |
| 7 | Swimming and shaking dry | 1 |
| 8 | Catching up after a gap | 1 |
| 9 | Look → head tilt; hold E → pet | 1 |
| 10 | Whistle comes, spam annoys | 1 |
| 11 | Sprinting alongside | 1 |
| 12 | Startle at gunfire | **Dropped** by the user |
| 13 | Whines and stays close when the player is hurt (below 30% health) | 2 |
| 14 | Early warning growl | 2 |
| 15 | Fighting (became bite and run) | 2 |
| 16 | Barks at enemies above its class | 2 |
| 17 | Barks toward raiders at home | 3 |
| 18 | Greets the robots | 3 |
| 19 | Chases sheep | 3 |
| 20 | Night: sleeps (now in its dog house), rare howl | 1 (sleeping), 3 (howl) |
| 21 | Orders: Follow / Stay / Herding | 1 (Follow, Stay), 3 (Herding) |

Added later in the conversation:
- the command wheel and pet slice;
- Q at an enemy;
- Active / Passive;
- the takedown;
- the retaliation rule;
- knock-down, help up and limp;
- friendly fire as a yelp and a flinch;
- the dog house;
- jumping.

## Non-goals (now)

- **Feeding**, which comes after the player's food system (user, 2026-11-23); then a Feed slice, a treat item and the Eat sound. Fetch (a slice later). A bond meter, naming the dog, sniffing out loot, carrying items or a backpack.
- A levelling UI or progression (only the stat hooks).
- More than one dog.
- Jump links placed across the wild map (only fence and house links, see Jumping).
- Herding the animals *out* to the pasture (Herding only brings them home).

## Anti-goals (a failure even if it works)

- Loki copies the player's moves exactly: starts when they start, stops when they stop.
- Loki wins fights for the player, or takedowns happen often enough to be expected.
- Barking is frequent enough to annoy (the user's explicit request).
- Loki gets in the player's line of fire, or bullets hurt it.
- Enemies hunt Loki, or a raid turns into "protect the dog".
- Loki can be lost for good: stuck, left behind, dead, or in a broken state after a load.

## Constraints

- **Model and clips only** from the Malbers Border Collie pack (`Animals Packs/04 Pets/Dog (Border Collie)`, PolyArt). Malbers' Animal Controller, AI, damage and sound components stay out, the same rule as ASSETS.md row 74, which this work updates.
- **The dog house:** `TFP_Dog_House_01B` from the SICS Toon Farm Pack. The pack is Maybe and only its farm animals were approved, so this use was approved by the user on 2026-11-23. Its row in ASSETS.md is updated in the same change.
  - Sizes: the house is 1.15 × 1.34 × 1.3 m (W × H × D) with 1 collider; the collie is 0.46 × 0.72 × 1.15 m, so it fits lying inside.
- **Later props:** the Synty Polygon Dog pack (Could) has a bone, bowls, a stick and balls for Feed and Fetch. None are used now.
- **Architecture as for the robots:**
  - a new `_Core/DogSystem/` with a scene installer and a central `DogManager` tick;
  - a Behavior Designer tree written as text (`AgentScripts/Dogs/Dog.tree.txt`, built by TreeKit, Manual mode);
  - A* `FollowerEntity` through `EnemyMover`;
  - plain C# rules with EditMode tests;
  - `DogSetup` and `DogPlayCheck` agent scripts.
- **The usual rules:**
  - audio only through BroAudio / `AudioService`; the user approves new SoundIDs before they are added;
  - every player-facing string in the localization CSVs;
  - input only through `InputService`;
  - time scale only through `TimeService`.
- **Placed in the scene** like the robots: WorldMap holds the Loki prefab at its dog house, and the installer collects it on save or Play. It isn't spawned.
- **Off switch:** the whole mechanic switches off with an `Enabled` flag in `DogSettings` (prototype rule).
- **Polish level:** not set in ROADMAP.md. ROADMAP suggests Medium, so build to Medium. The user can change it.

## Approaches considered

1. **Our own system modelled on RobotSystem (picked).** The tree, mover and animator pattern are proven here. Enemy, herd, whistle, interaction, save and HUD hooks already exist.
2. **Malbers Animal Controller with its AI.** The richest out of the box, but it is a heavy per-instance controller with its own NavMeshAgent AI, damage, sounds and input. It fights our A*, BroAudio and DI rules, and ASSETS.md says to avoid it.
3. **Extending EnemySystem with a "friendly enemy".** It reuses the hunt and melee code, but enemy trees assume one hunted target and crowd ticks. A companion's personality would fight that code constantly.

## Design

### Pieces

```
_Core/DogSystem/            namespace MB13.Aipocalypse.Core.DogSystem
  Dog                MonoBehaviour bridge: IInteractable (tap E → wheel), IHoldInteractable (hold E → pet / help up),
                     IWhistleTarget, INameTagged ("Loki"), ITargetable (Kind = Dog), IDamageable
  DogManager         ITickable + ILateTickable: reads the player, runs DogState and the senses, ticks the tree at ThinkInterval,
                     drives the animator, voice limits
  DogState           plain C#: order, stance, toughness, down / limp, annoyance, cooldowns, takedown rolls (tested)
  DogFollow          plain C#: picks where to be relative to the player (comfort zone, lead, side, aim clearance, gait) (tested)
  DogCombat          plain C#: target choice, opening test, weight-class checks, takedown chance (tested)
  DogStats           stat sheet: DogStat enum → base value (DogStatBlock asset) + upgrade sources (tested)
  DogVoice           bark and growl budget (tested)
  DogSettings        tuning ScriptableObject (Enabled, ranges, times, SoundIDs, the class order)
  DogTasks           Behavior Designer conditionals and actions
  DogLegs            movement over EnemyMover (traversable tags All + DogOnly)
  DogRoster / DogHouse the dog house (home spot, door point, lie-in pose), yard, night, raid (the scene service, like RobotRoster)
  DogSaveSection     order, stance, position, toughness, down / limp
  DogCommands        console (cheats)
  DogInstaller       SceneInstaller (needs EnemyInstaller, WorldTimeInstaller; AnimalInstaller and RobotInstaller optional)
  Links/DogJump      IOffMeshLinkHandler set on Loki's FollowerEntity (own asmdef, like RobotLinks)
_Core/UI/Wheel/      CommandWheel view + presenter: generic slices (icon, LocalizedString, enabled, selected)
_Core/UI/Hud/DogHud  icon + StatBar for toughness, down state
```

`DogStatBlock` (`Data/Dogs/`) holds the base values. `DogSettings` holds everything that isn't an upgradable stat.

### Model, animator, animation

- **Entity prefab** `Content/Dogs/Loki/Loki.prefab` (a variant of a `Dog.prefab` base), built by `DogSetup` the way `AnimalSetup.Entity` does it:
  - starts from the WanderCapsule template, with the enemy parts removed;
  - FollowerEntity radius about 0.3, height 0.7;
  - a nested model copy at `Content/Dogs/BorderCollie/Model/` (vendor scripts stripped, `applyRootMotion` off);
  - **colour:** pick at review (default Black and white).
- **Layer: Animal (20).** Bullets and projectiles already hit it.
  - **Whistle:** the heard mask gets Animal added, and `PlayerWhistle.Heard` must skip colliders that aren't an `IWhistleTarget`.
  - **Enemy sight:** `EnemySight` must not count Loki as a blocker, by an exemption or by its layer; that's the builder's call.
- **Generic controller built in code** (`DogSetup.Controller`). Parameters:

  | Parameter | Values |
  |---|---|
  | `Speed` | 1D blend: walk → trot → canter → run → sprint, timeScale matched to ground speed as in `AnimalSetup` |
  | `Turn` | turning on the spot |
  | `Stance` | 0 normal, 1 combat, 2 sniff, 3 injured; each its own locomotion blend tree |
  | `Pose` | 0 stand, 1 sit, 2 lie, 3 sleep, through the pack's transition clips |
  | `Act` + `ActIndex` | bark, happy, head tilt, shake water, dig, pee, eat, howl, yes, no, roll, alert, growl idle, whine (lie) |
  | `Attack` + `AttackIndex` | bite forward, front L, front R; charge low (feint) |
  | `Dodge` + `Side` | dodges |
  | `Hit` + `Side` | flinches |
  | `Down`, `Rise` + `Side` | the Death1 and Death_Recovery clips |
  | `Jump` + `JumpKind`, `Swim` | jumps; swimming |

- **Locomotion clips** keep horizontal root motion extracted (they play in place). Jump arcs are driven in code.

### Stats (upgrade-ready)

- `enum DogStat` lists every upgradable number. Each `DogStat` has a base value in `DogStatBlock` (`Dictionary<DogStat, float>`). A test checks that every enum value has an entry.
- `DogStatSheet.Get(stat)` returns base + the sum of all `IDogUpgradeSource` modifiers. The list of sources is empty now; a future levelling system registers one.
- Weight classes are an ordered list in `DogSettings` (`StaggerProfile` Light, Medium, Heavy, Boss), compared against `enemy.Definition.Reaction.Stagger`. The `MaxBiteClass` and `MaxTakedownClass` stats are indexes into it.

| Stat | Start value |
|---|---|
| BiteDamage | 12 |
| BiteCooldown | 8 s |
| MaxBiteClass | 0 (Light) |
| StaggerTime | 0.9 s |
| RetaliationWindow | 2.5 s |
| CircleRadius | 5 m |
| CircleSpeed | 3.5 m/s |
| FeintChance | 0.25 per circle lap |
| EscapeSpeed | 7 m/s |
| DodgeChance | 0.35 (an enemy swing at Loki misses while it's escaping) |
| Toughness | 100 |
| RegenPerSecond | 2, after 10 s unhurt |
| HelpUpToughness | 0.3 (fraction) |
| LimpTime | 30 s |
| RetreatAt | 0.25 (fraction) |
| EngageRange | 15 m from the player |
| SentChaseRange | 30 m |
| SenseRange | 25 m |
| TakedownChanceBase | 0 (off until unlocked) |
| TakedownChanceBonus | 0 (added × the fraction of health missing) |
| MaxTakedownClass | 0 (Light) |
| TakedownCooldown | 20 s |
| PinTime | 1.5 s |

**For review only:** to see takedowns, the user (or a cheat) can set TakedownChanceBase to 0.05 and TakedownChanceBonus to 0.25.

### Orders and stance

- **Order** is Follow, Stay or Herding; the **stance** is Active or Passive. Both are saved.
- **Defaults:** Follow and Active.
- After the player dies, or sleeps in bed with Loki following, Loki is at home and the order is Follow.

### Command wheel

- **Opening:** tap E on Loki opens the wheel; the prompt reads "[E] Orders" and "Hold: Pet". It opens only within interact reach and when no other UI is open.
- **While open:**
  - time runs at about 0.3 through `TimeService.Request(this, 0.3)`, released on every exit;
  - input switches to UI through `InputService.RequestUI`; the cursor is centred and its direction from the centre picks a slice, and on a gamepad the stick does.
- **Picking and closing:**
  - Click or Submit picks the slice under the cursor.
  - Esc, right click, B or E closes. E needs an E binding in the UI map; the builder adds it.
- **Slices:**

  | Slice | Phase | Notes |
  |---|---|---|
  | Follow | 1 | Highlighted when it's the current order |
  | Stay | 1 | Highlighted when it's the current order |
  | Pet | 1 | Same as hold E |
  | Active / Passive | 2 | Shows the current stance and toggles it |
  | Herding | 3 | Greyed when no animal is out |

- The wheel is generic (a slice list plus callbacks), so Feed and Fetch are one more slice each later.
- **Style:** Layer Lab sprites (`LayerLabSetup`); labels in the localization CSVs.

### Pet

- **Pet (hold E, or the wheel slice):**
  - Loki comes in front of the player and sits, then plays happy, a head tilt, or now and then a roll (1 in 4).
  - It ends annoyance, and it's cut short if a fight starts.
  - From Phase 2: it doesn't work while Loki is down; help it up first.

### Whistle (Q)

- **What answers:**
  - aimed at a robot: the robot answers, as today;
  - aimed at an **enemy** (the nearest aim hit's parent has an `EnemyBrain`), Phase 2: Loki is sent at that enemy;
  - otherwise, when Loki is within 60 m (`HearWhistleRange`): Loki comes.
- **Which one answers, in order:** a robot under the aim → that robot. Loki under the aim → Loki ("come"). An enemy under the aim → sic (Phase 2). A robot heard within 8° of the aim (through walls) → that robot. Otherwise → Loki, if within range. `PlayerWhistle` keeps its one-target contract and gains the dog fallback, through an optional `IWhistleFallback` that `DogManager` provides (builder's call on the exact shape).
- Robot whistles never count toward Loki's spam.
- **Plain whistle (Loki comes):**
  - Out of a fight: it comes running to heel and the order becomes Follow.
  - In a fight: it disengages and stays at heel until the fight ends. That's 5 s with no enemy aware of the player within 20 m.
- **Whistle spam:** 4 plain whistles in 5 s with no fight going on annoy Loki. It shakes its head ("no"), comes slowly, and ignores plain whistles for 6 s. A pet forgives it. Whistles in a fight never count.

### Phase 1: a living dog (following, home basics, wheel, pet / feed, saving)

**Following** (`DogFollow`, tuned in `DogSettings`):

1. **Comfort zone.**
   - Between 3 m (inner) and 8 m (outer) from the player, Loki is free.
   - Past the outer ring it trots to a point near the inner ring on its side. Past 15 m it sprints, overshoots a little, then eases off.
   - It reacts after 0.3–0.8 s (random), never in the same frame as the player, and uses hysteresis so it doesn't flip between states.
2. **Scouting ahead.** When the player has walked one way steadily for 2 s, Loki's goal moves 4–7 m ahead along the player's heading. It stops there, looks back, and waits.
3. **Side and aim clearance.**
   - It keeps a side, left or right, and swaps every 20–40 s.
   - While the player aims (`HeldItem.IsAiming`), it moves out of a 12° cone around the camera's forward line, plus 2 m.
4. **Sniffing.** While free, it picks a point within 6 m that stays inside the outer ring. It goes there with the sniff walk, then sniffs, digs, or (rarely, outdoors) pees. Any of these is dropped when the player leaves the ring.
5. **Settling.**
   - The player still for 3–6 s: it sits.
   - 15–25 s: it lies down.
   - 60 s: it yawns and naps.
   - It gets up through the transition clips when the player moves off, with the same reaction delay.
6. **Checking in.** Every 30–60 s it trots to 1.5 m in front of the player, looks up and plays happy, then goes back to what it was doing.
7. **Water.** The streams can be waded and are on the navmesh. Where the water is deeper than about 0.45 m at Loki (a Stylized Water height query), it swims, and on leaving the water it shakes itself dry. Lakes and the river aren't on the navmesh: if the player crosses one, Loki catches up on the other side (8).
8. **Catching up.**
   - Over 40 m away, or with no path for 3 s: it finds a navmesh point 10–15 m behind the player, out of camera view and line of sight, and appears there. Then it trots in.
   - After a load, sleep or respawn, it's placed by rule (home or near the player).
9. **Look.** When the player looks at Loki within 10 m for 1 s, it tilts its head (at most once every 20 s).
10. **Sprinting.** When the player sprints, it sprints alongside on its side, without scouting.
11. **Friendly fire.** A player bullet does no damage: Loki yelps (rate-limited) and flinches by side, nothing more.

**Stay:**
- **Out in the world:** Loki keeps within 4 m of the spot where it was told to stay, and sits, lies down, naps and sniffs there. At night, or when the player is over 150 m away, it walks home on its own.
- **In the yard:** it lives at home round its dog house: it wanders the yard and sniffs, naps lying in the doorway with its head out, and keeps returning to the house.

**The dog house (home):**
- `DogHouse` marks the house, its door point (on the navmesh, in front of the opening) and the lie-in pose.
- **Going in:** Loki walks to the door point, faces in, and the mover hands over to a short scripted move (FollowerEntity movement off) that walks it 0.6 m in. It turns to face out and lies down.
- **Coming out:** it reverses the move.
- **At night at home:** it sleeps inside with its head out (sleep pose).
- **Collider:** the house collider stays for the player. Loki's scripted move ignores it (Loki isn't a physics body). The navmesh is carved round the house as for any prop.

**Jumping** (the user asked for jumping):
- The fence is about 2 m high with a 2.4 m climb blocker, and with the gate shut the yard is its own navmesh island. So Loki **scrambles over the fence**: Ledge_2.0M (paws up), Climb Up, then a drop (Fall_from_Edge / Land).
- **Fence links:**
  - `DogSetup` places a two-way `NodeLink2` across every fence segment and the gate, tagged `DogOnly` (a new `NavTags` bit that walkers, robots and animals don't traverse).
  - A broken segment already lets every agent through, so its link stays harmless.
- **Indoors:** Loki follows the player into the house through the robots' door link (it hops the 1 m step) and settles and sniffs inside like anywhere else. Stay indoors works too.
- **Nav tags:**
  - The new `NavTags.DogOnly` bit is taken out of `Walkers`. Robots go from `All` to `All & ~DogOnly`. Animals and enemies use `Walkers` or need their masks checked.
  - Loki traverses everything.
  - The robot tests are updated with the new masks.
- **One handler for every link:** `FollowerEntity.onTraverseOffMeshLink` on Loki is set to `DogJump`, which overrides the link's own handler. It chooses the move from the height difference:
  - under 0.6 m: a trot jump along a code arc;
  - an obstacle with both ends at about the same height: the scramble described above;
  - up 0.6–2.2 m: the ledge clip for that height, then climb up;
  - down: a jump down or a fall from the edge.
- **Elsewhere:** catching up (8) covers places Loki can't reach.
- **If the scramble looks wrong in review**, the fallback is passing through the gate even when it's shut (a "dog flap").

**Home basics:**
- **The dog house** goes in the WorldMap yard, near the house and clear of the robots' spots, the herd's corner and the gate path. It's placed by `DogSetup.World`, with its navmesh tiles updated.
- **New game:** Loki starts at its dog house with the order Follow, so it joins the player straight away.

**Save:**
- **Phase 1 saves:** order, position, yaw, and inside or outside the yard.
- **Phase 2 adds:** stance, toughness, and the down and limp state with the time left. `SaveService.Version` is bumped and a round-trip test added for each change.

**Sounds:** each needs the user's OK before its SoundID is added. Barks are kept rare (see Voice).
- Bark, Growl, Whine, Yelp, Pant, Howl, Bite, Sniff (all 8 approved by the user, 2026-11-23, with a `Dogs` BroAudio library holding their entities; Phase 1 adds Bark, Whine, Yelp, Pant, Sniff, Howl; Phase 2 adds Growl, Bite);
- sources: the Gamemaster Pro dog clips and the collie's barks and howls.

### Phase 2: fighting

**Senses and early warning:**
- Loki senses enemies within 25 m of itself, without needing line of sight.
- If it senses one the player hasn't noticed (no enemy is aware of the player), it stops, faces it, takes the alert or combat stance and growls (spatial). At most once per enemy group every 30 s.

**Which enemies Loki fights:**
- **When Active**, it engages only enemies **aware of the player** within 15 m of the player.
  - It prefers ranged enemies, then the one attacking the player, then the nearest to the player.
  - Unaware enemies only get the warning growl, so Loki never spoils a sneak.
- **When Passive**, it never engages on its own. It stays at heel and growls.
- **Q at an enemy** (either stance) sends Loki at that enemy. It may chase up to 30 m from the player, and the enemy doesn't have to be aware. It goes back when the enemy dies, after 10 s with no bite landed, or (Passive) when that enemy is dead.
- **Above Loki's bite class**, it only circles and barks: no bite, and no retaliation window.

**The bite-and-run cycle** (Behavior Designer combat branch, numbers in `DogCombat`):

1. **Circle.** It orbits the target at CircleRadius with the combat trot, growling. It feints with FeintChance per lap (Charge_Low, then stops short).
2. **Opening.** It darts in when its cooldown is ready and either:
   - the target faces away from Loki (more than 60°);
   - the target is swinging at someone else;
   - or it has circled for 1.5 × the cooldown.
3. **Bite.** The clip is chosen by side (forward, front L, front R).
   - **Damage:** `DamageService.Deal(body, DamageInfo(BiteDamage, Melee, point, small push, source = Loki))`.
   - **Stagger:** `brain.Stagger(StaggerTime)` plus `brain.Animation.Hit(push, heavy: false)`.
   - **Retaliation:** the target and every live enemy within 3 m get a retaliation window (below).
4. **Escape.** It dodges (L or R, away from the target's facing), then runs at EscapeSpeed out to circle range on another bearing.
5. **No room to circle:** if fewer than 3 of 8 sampled points round the target are reachable, it bites, then goes back to the player's side for the cooldown.

**Takedown:**
- **When it can happen:** each bite rolls (`RandomService` stream `Dog`) when the target is within `MaxTakedownClass` and the cooldown is ready.
- **The chance:** `TakedownChanceBase + TakedownChanceBonus × (1 − health fraction)`.
- **On success:**
  - the kill is dealt as `Deal(body, Health.Current, force toward Loki)`, so the body falls **toward Loki's side**: dragged down;
  - Loki stays on it for PinTime (bite and growl loop), standing still;
  - enemies within 3 m get retaliation windows;
  - feedback: the Bite and Growl sounds, the body thud from the enemy's own death, and a tiny hit-stop (`TimeService.RequestHitStop`, 0.06 s).

**Enemies fight back only when provoked** (EnemySystem changes):
- **New target kind:** append `TargetKind.Dog`. `AttackersPerTarget` gets Dog = 2. Loki is **not** registered in `TargetRegistry`, so no enemy picks it by weight.
- **New brain call:** `EnemyBrain.Retaliate(ITargetable target, float until)` stores a window and sets `_nextRetarget = 0`.
- **In `ChooseTarget`,** before the cached target is returned:
  - while the window is open, Loki is targetable, and Loki is within the enemy's melee reach (+0.5 m), the target is Loki;
  - otherwise the window clears and the enemy goes back to its normal target. Since this runs on every think, nobody chases.
- **Ranged retaliation:** a bitten archer shoots at Loki from where it stands while the window is open, without repositioning. Other archers never aim at Loki.
- **Noticing:** `TakeDamage` → `Notice(true)` stays as it is. Active Loki only bites aware enemies, and a Q-sent bite on an unaware one is the player's choice.

**Toughness, knocked down, helped up:**
- Hits reduce toughness. Only damage from the player is ignored (a yelp and a flinch). Anything else that hurts it counts, such as explosions. Enemy arrows only hurt their own target kind, so a stray arrow can't hit Loki. A swing at Loki while it's escaping misses with DodgeChance (it plays a dodge).
- **At 0, Loki is down.**
  - It plays Death1 by side and lies whining (rate-limited). It isn't targetable and ignores damage.
  - The prompt reads "Hold: Help Loki up".
- **Helped up:**
  - it plays Death_Recovery by side;
  - toughness goes to HelpUpToughness;
  - it limps (the injured stance) for LimpTime;
  - it doesn't engage on its own while limping, though Q at an enemy still sends it.
- **Recovery:** it regenerates RegenPerSecond after 10 s unhurt. After 90 s down with nobody helping, it gets up by itself and limps to the player.
- **Backing off:** at or below RetreatAt it whimpers and backs off to heel. Q at an enemy overrides this.

**HUD** (`DogHud`):
- The dog icon and toughness `StatBar` show while Loki follows within 40 m or is fighting, and fade out otherwise.
- Warning colour below 30%, and a "down" icon state.

**Stance slice** on the wheel; console commands for the fight (below).

### Phase 3: the farm and home life

**Herding (wheel):**
- `HerdOrders.Call()` brings the herd home, the same as the bell. The slice is greyed when no animal is out.
- Loki then runs behind whichever animal is farthest from home or slowest and barks (within the bark budget). It calls `Hurry` on that animal, then moves to the next.
- When `CountHome == Animals.Count`, the order becomes Stay at home.
- Startle isn't used, because the herd ignores it while going home.

**Robots:**
- When Loki and a free robot (`FreeToChat` rules) are within 4 m and both are idle, Loki sniffs the robot and the robot waves at it, with heart eyes.
- Once per 90 s per robot. Robots get a small public entry point; `DogManager` reaches `RobotRoster` through `TryResolve`.

**Chasing sheep:** at home in the daytime, every 3–6 min, Loki runs at a nearby sheep.
- It calls `Startle(dogPos, 4, run: true)` every 0.5 s for 5 s, then trots back happy.
- Never animals that are going home, and never during Herding.

**Night:**
- **At home:** it sleeps in its dog house, as in Phase 1. When the player sleeps in bed, a following Loki goes home to its house. This replaces "near your bed" from behaviour 20, now that there's a dog house.
- **Howl:** rarely, at most once a night, a 20% roll between 22:00 and 02:00 while at home, not during a raid.

**Raids at home** (user, 2026-11-23):
- It barks toward the nearest raider or broken barrier, in bursts of 2–3 barks with 8–15 s gaps.
- **Active:** it attacks raiders that get **inside the yard**, whether they know about the player or not, using bite and run and the Phase 2 rules. Raiders outside the fence only get barked at.
- **Passive:** it only barks. It keeps away from raiders: it runs away from any raider within 6 m, to the side of the yard away from them, or into its dog house or the house. It never bites, so no raider ever has a reason to retaliate.

### Voice (bark budget)

- `DogVoice` allows at most one sound per 1.2 s, and per category:

  | Category | Most often |
  |---|---|
  | Bark | once per 6 s, 3 per minute |
  | Growl | once per 4 s |
  | Whine | once per 8 s |
  | Yelp | once per 1 s |

- Idle barks (at nothing) are off.
- These numbers are tuned in `DogSettings`. A test covers the budget.

### Console (cheats)

- `dog.come`
- `dog.order follow|stay|herd`
- `dog.stance active|passive`
- `dog.toughness <value>`
- `dog.down`
- `dog.up`
- `dog.sic` (the nearest enemy)
- `dog.takedown <base> <bonus>`
- `dog.where`

### Off switch and checks

- **Off switch:** `DogSettings.Enabled` false means Loki is never activated and the wheel and HUD never show. Enemies are unaffected, because Loki is never registered.
- **EditMode tests:** DogState, DogFollow, DogCombat, DogStats, DogVoice, the wheel slice logic, the save round trip, and the EnemyBrain retaliation window.
- **Play probes (DogPlayCheck, WorldMap):** at least one per numbered behaviour. Each phase lists its probes in its plan.

## Left to the builder

- Exact clip choices per action, the `Speed` thresholds per gait (measure ground speeds as `AnimalStrideProbe` does), and the colour variant (show the options).
- How `EnemySight` ignores Loki.
- How the ranged retaliation hooks into `RangedTasks`.
- The wheel's look within Layer Lab.
- Where the dog house goes in WorldMap, and its door point.
- The jump-arc maths.

## Open (user)

- Pick the colour variant at review.
- Decide whether the fence scramble or the gate "dog flap" stays, after seeing it.
