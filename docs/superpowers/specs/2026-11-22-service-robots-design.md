# Service robots: first two, at home (design)

2026-11-22. The user's picks are marked *(user)*; everything else is my call, open to correction.

## Intent

The home gets its first **service robots**: friendly, cute helpers who will later open the Shop, Storage, Upgrade and other menus. This step builds the robots themselves, not the menus. They should feel like friends or companions, lively and expressive *(user)*, so the home feels lived in before any service exists. (Design context, draft: `docs/gdd/FARM_AND_ECONOMY.md` → Rescued robots, `CORE.md` → Robots.)

## Goals

- **Two robots** from `Assets/_StoreAssets/Characters/GrigoriyArx/DrollRobots/Prefabs/Robots/Rob15.prefab`, as two variants *(user)*, **about 1.3 m tall** *(user)* (the model is 1.76 m: scale ≈ 0.74), **tinted differently** so they can be told apart *(user)*.
- **Each has its own spot** in the garden or inside the house *(user)*. Mostly it stands there and looks around; now and then it walks a little nearby and comes back *(user: "no need to always walk, most of the time stay, look around, then walk occasionally")*.
- **E on a robot** (the normal interact key) is where its menu will open. For now: it stops, turns to the player and waves or talks *(user)*. Looking at it does the same *(user)*.
- **Q whistles** *(user)*: aim at a robot up to **50 m** away *(user)* and press Q; the player whistles (`Assets/_StoreAssets/Characters/Malbers Animations/Common/Audio/Whistle/Whistle.wav`) *(user)* and the robot comes to the player, with emotions *(user)*.
- **Raiders ignore the robots** *(user)*.
- **Robots never leave the yard** *(user)*: whistled from outside, a robot comes to the inside of the fence nearest the player, waves and waits, then goes back.
- **Easy to change later** *(user)*: the robot's model, and which shop it is assigned.
- **Nice, cute emotions** *(user)*: animation plus the pack's face expressions.
- **Extras** *(user picked 1–7 of my suggestions)*: head tracking; beeps and chirps; welcome home; whistle spam gets a reaction; the two robots greet each other; raid reactions; a service icon over each robot. Details in Design → Extras.

- **Polish:** above the roadmap's Medium for "the robots' look and personality", approved *(user)*. Built in **two passes** *(user)*: **pass 1** = robots, spots, idle and strolls, E greeting, whistle, faces, head tracking, beeps, and fixes 1–7 below; **pass 2** = welcome home, whistle spam, robots greeting each other, raid reactions, service icon. The user plays pass 1 before pass 2 starts.
- **Fixes and improvements** *(user picked all 7)*: (1) no blocking: the doorway stays out of the house robot's stroll area, and a robot walked into steps aside like the animals; (2) beep limits: a minimum gap per robot and a shared rate across both; (3) console commands `robot.status`, `robot.call`, `robot.raid`, `robot.welcome` (dev builds, like `AnimalCommands`); (4) a test camera point inside the house for play-check screenshots; (5) the face screen glows softly at night (emission on the eyes / mouth materials, by the time-of-day); (6) aiming a gun at a robot within a few metres makes it nervous: worried face, a small "no" shake, cooldown; (7) each robot rolls its own personality: its own stroll, wave and dance timings and chances (± share, like an animal's Pace / Patience), so the two never move in step.

## Non-goals

- No menus, no shop, storage or upgrade logic *(user: "no need to open the menu right now")*.
- No rescue, unlock or "found in the meadow" flow: both robots are simply at home.
- No saving of robot state: on load each robot starts at its spot.
- No HUD prompt for the whistle (my call: keep the step small; the E prompt is there when close).
- No dialogue lines, voiced speech or speech bubbles (quips were suggestion 8, not picked). Beeps and chirps are in.
- Robots outside the yard, following the player, or fighting.

## Anti-goals

- Robots that read as adult NPCs or enemies (too tall, too stiff, too quiet).
- Robots you have to hunt for: they stay near their spots.
- Shop, model or emotion choices baked into code, so changing one means code edits.
- Any of the vendor's demo scripts (`Rob15Ctrl`, `RobotColorManager`, `EmotionChanger`, `PushObjects`, … legacy `Input`) or its `CharacterController` on our prefabs.

## Constraints

- AGENTS.md: a **behaviour tree for every agent**, ticked centrally (no per-robot `Update`); VContainer installer per system; input only through `InputService`; MessagePipe across systems; every player-facing string in the localization CSVs (English only); BroAudio for sound (a new `SoundID` needs the user's OK: the whistle is that OK); prototype mechanics stay modular (an `Enabled` flag, own installer).
- Characters skill: **model prefab vs entity prefab**; no vendor MonoBehaviours on the model; `Rob15` is a **Generic** rig with its own clips (walk 0.84 m/s, run 1.75 m/s at full size, root-motion clips, turn clips, emotes, hits, falls, punches), URP Lit, a `LODGroup`. Its face is two renderers (eyes, mouth) whose `_BaseMap` offset picks 1 of 10 expressions each (offset x = index / 10).
- Navigation: robots walk the WorldMap A* recast graph like the animals. **Unverified: whether the house interior is on the cached navmesh** (the bed's surroundings didn't resolve in edit mode because the graph isn't loaded outside Play). If it isn't, the house robot needs a re-bake (ENVIRONMENT.md → `Navigation.Bake`) or its spot moves; check first in Play.
- Keys: E Interact, C Collect, **F is GunLight**, G Drop; **Q is free** and becomes Whistle (gamepad binding: builder's choice of a free button).

## Approaches

1. **A new self-contained Robot system, modelled on the farm animals (picked).** Own folder, installer, settings, manager ticking each robot's Manual BD tree; legs from `FollowerEntity` + `EnemyMover`; its own animator controller; a face component. Keeps robots independent of the herd and easy to switch off or grow into services.
2. Robots as another animal kind. Quicker, but drags in herd, Token and pen rules that don't fit, and ties shops, whistling and emotions into animal code. Dropped.
3. A coded state machine instead of a tree. Simpler, but breaks the "behaviour tree for every agent" rule. Dropped.

The user handed me the *how*; I picked 1.

## Design

### Pieces

```text
_Core/RobotSystem/   (namespace MB13.Aipocalypse.Core.RobotSystem)
  RobotInstaller        SceneInstaller: RobotSettings, RobotRoster (scene service), RobotManager (entry point);
                        collects and injects the scene's robots (like AnimalInstaller); needs EnemyInstaller (navmesh)
                        and WorldTimeInstaller (night)
  RobotSettings (SO)    Enabled, ThinkInterval, awake hours (night standby), idle / stroll timings and chances,
                        stroll radius, notice radius (glance / wave), greet cooldown, call: arrive distance,
                        wait time, leave-when-player-is-farther-than; emotion timings; sounds
  RobotManager          every frame: animation and face (blinks, expression timers), player position, who the
                        player is looking at (IPlayer.Interactor.Focus); a think every ThinkInterval
  Robot (entity root)   IInteractable (E) + IWhistleTarget (Q) + IDamageable (hit reaction, never hurt);
                        its RobotService, its Home (spot), state the tree reads (Called, Greeting, LookedAt,
                        Sleeping); no Update
  RobotFace             sets eyes / mouth expression by index on its own material instances (no vendor script);
                        named expressions come from RobotFaceSet below
  RobotFaceSet (SO)     per model: renderer names for eyes / mouth, expression count, which index is
                        Neutral / Happy / Blink (eyes closed) / Sleepy / Surprised / Sad / Angry / Talk
  RobotService (SO)     what a robot offers: DisplayName and prompt verb (localized), Icon (sprite), later its menu.
                        Placeholders: Shop, Upgrades (my call; reassign freely)
  RobotSpot             scene component: where a robot lives, stroll radius gizmo
  RobotTasks            BD tasks for the tree below
_Common/Interaction/IWhistleTarget.cs     TryWhistle(actor): something the player can call from afar
PlayerSystem: PlayerWhistle              Q → whistle sound (always, with a short cooldown) + a long look cast
                                         (≤ RobotSettings-independent WhistleRange 50 m in player tuning) → IWhistleTarget
InputSystem: Player map action Whistle (Q) → InputService.WhistlePressed → PlayerIntent.Whistle
Content/Robots/<Name>/                   Model/ (first-party model prefab), <Name>.controller, the entity prefab
                                         and its variants, face set, tints
Data/Robots/                             RobotSettings, RobotService assets
```

Shop is assigned on each robot (its `RobotService` field), so reassigning is one drag in the inspector. The model is the entity's `Model` child: a different look is a prefab variant that swaps `Model` (characters skill §4, extra looks) plus its controller and face set; nothing in code names `Rob15`.

### The two robots in WorldMap

- `Robot_A` (Shop placeholder), tint 1, spot in the garden near the house.
- `Robot_B` (Upgrades placeholder), tint 2, spot inside the house.
- WorldMap has an uncommitted change removing a raw vendor `Rob11` placed near the house: **intended, it goes in with this work** *(user)*. Any other unrelated scene hunks are reverted before finishing.
- Both under a new scene root `Home Robots` (own root so a `Landmarks` rebuild doesn't delete them, like `Home Pen`), with the installer on the `GameLifetimeScope`. Exact spots chosen by the builder and shown to the user in a screenshot.
- Tints: material copies of the pack's body material in `Content/Robots/`, two clearly different colours that sit well in the cozy palette; the user picks from a screenshot.

### Behaviour tree (TreeKit spec, priority order)

1. **Night** (outside awake hours): walk home to the spot, **standby** (idle, sleepy eyes, slow blinks). E or a whistle wakes it for that interaction, then back to standby.
2. **Called** (whistled): a happy reaction (thumbs up or a hop, happy face), then **trot** to the player; stop at arrive distance, face the player, wait. Leaves when the wait ends or the player walks farther than the leave distance: a little shrug ("don't know"), then walks home. A new whistle restarts the wait.
3. **Greeting** (E pressed or being looked at): stop, turn to face the player, wave (first time after a while) or talk (talk clip + talk mouth). Holds while the player keeps looking or stays close, then resumes.
4. **Home life:** mostly **stand and look around** (turn in place a little, glances, blinks, small face changes); now and then (chance) a **short stroll** within the spot's radius and back; rarely an idle flourish (a little dance or a laugh). Away from the spot (after a call or a stroll), it walks back first.

### Emotions (cute, lively)

| Moment | Body | Face |
| --- | --- | --- |
| Idle | stand, small turns, look around | neutral, blinks every few seconds, now and then a happy or curious look |
| Player passes close (notice radius), not greeted recently | turns its head / body to the player, sometimes waves | happy |
| E or looked at | stops, faces the player, wave or talk | happy, talk mouth while talking |
| Whistled | thumbs up or hop, then trots over | surprised → happy |
| Waiting by the player too long / player leaves | shrug ("don't know") | sad or neutral, then neutral walking home |
| Rare idle flourish | short dance or laugh | happy |
| Shot by the player (any hit) | hit clip (front / left / right), no damage | surprised → angry or sad for a moment |
| Night | standby | sleepy, eyes closed most of the time |

Which atlas index is which expression is read from the textures and recorded in `RobotFaceSet`.

### Animation

Our own controller from the pack's clips (Generic, `applyRootMotion` off, the mover moves the body): idle, a Speed blend of walk and run played at speed / (authored × scale), turn-in-place clips for facing, emote states by index (hello, talk, thumbs up, laugh, don't know, no, win, dance), hit states, a standby state. Built by an editor script in `AgentScripts/Robots/` (reusing `CharacterKit` where it fits). Animators cull off screen; the manager advances face timers only for robots on screen or near.

### Whistle

- Q always plays the whistle (a new `SoundID`, the Malbers clip as a placeholder, ASSETS.md row updated), with a short cooldown.
- A thin sphere cast from the camera up to **50 m** *(user)* against the robots' layer, blocked by walls; the first `IWhistleTarget` hit is called. Nothing hit: just the whistle. The house robot hears only when seen (inside, or through the open door): accepted.
- The player system knows only `IWhistleTarget`, not robots (later a dog or animal could answer too).
- **Where the robot goes** *(user: stays in the yard)*: the player's position if it's inside the yard; otherwise the closest point inside the yard to the player (the `YardFence` / home area bounds, pulled in from the fence by a margin), then the nearest navmesh point reachable from the robot's spot. It never paths through the home gate. There it faces the player, waves, and waits as usual; the "player walked away" rule measures from that point.

### Being hit

My call: robots take **no damage** and show **no hit marker or damage number**. A hit from the player (bullet or melee) plays a hit clip by side plus a surprised → angry or sad face and a squeak. Raiders never attack robots (they aren't targets), so only the player's hits reach them. The builder picks the hook: `IDamageable` returning a zero-damage result with `IShowsOwnHits` (if that suppresses the generic hit feedback), or a small "pokeable" check in the hit path; whichever keeps normal hit feedback off a friendly.

### Extras (user picked 1–7)

1. **Head tracking.** Within the notice radius the head turns toward the player's head, clamped (yaw ±70°, pitch ±30°), smoothed, faded in and out; also toward the other robot while greeting it and toward the player while called. A head-bone rotation applied after the Animator (two robots: negligible cost), by the manager, not a per-robot `Update`.
2. **Beeps and chirps.** Short robot sounds on: greet, wave, called (rising), happy, shrug (sad boop), hit (squeak), welcome home, spam annoyance, raid fear, dawn relief, standby wake. Source: Gamemaster Audio Pro `Retro_Classic/Robot`, `Retro_Classic/Menu_UI_Beeps`, `Alarms_Beeps_Siren/beep_*`, `Sci-Fi` (already a declared pack), placeholders until final audio. New `SoundID`s in a Robots BroAudio library: **adding them is approved by the user's pick**; the builder lists the chosen clips per moment for the user to hear.
3. **Welcome home.** When the player comes into the yard after being away at least `WelcomeAwayMinutes` (game time), the nearest awake robot that can see the player turns, waves, happy face, chirp. Not at night, not during a raid.
4. **Whistle spam.** Three or more whistles at the same robot within a few seconds: it stops, shakes its head ("no" clip), annoyed face, grumpy beep, then comes anyway. Cooldown so it happens once per burst.
5. **Robots greet each other.** When the two pass within a few metres, both free (not called, not greeting the player, awake), they stop, look at each other, wave (one may laugh), chirp; a long cooldown.
6. **Raid reactions.** On `RaidStarted`: each robot hurries to its spot (the house robot goes inside), scared face, stays put in a fearful idle (no cower clip: idle or "cry" emote with the scared face, builder's pick), awake through the raid instead of standing by. On `RaidEnded` with `Cleared` or `Dawn` (not `Defeat`): at the next awake moment both do a short relieved dance with a happy chirp. `Defeat`: a sad face and the "cry" emote once.
7. **Service icon.** A small world-space icon over each robot's head (its `RobotService.Icon`: cart for Shop, wrench for Upgrades, placeholders), shown while the player is within a few metres or looking at it, faded, always facing the camera. Icon sprites from a declared UI pack (builder checks ASSETS.md; ask if only Undecided packs have them).

### Off switch and checks

- `RobotSettings.Enabled` or removing `RobotInstaller`: robots just stand.
- Raiders ignore robots: they aren't registered as targets (and not on a layer enemies target).
- EditMode tests: the rules in plain C# (when to stroll, call wait / leave, greet cooldown, night window), the prefab wiring (model, controller, face set, service, collider, layer) like `AnimalTests`, the input action exists with Q.
- Play check script (like `AnimalPlayCheck`): idle and strolls, E greeting, whistle from 30 m and 60 m (no answer past 50 m), night standby, a shot, raiders ignoring them; screenshots of both robots at their spots for the user.
- Docs: a new `docs/technicals/ROBOTS.md`, CODE_STRUCTURE.md, ASSETS.md (GrigoriyArx row: now used), input keys where documented.

## Left to the builder

- Exact numbers (stroll chance and radius, notice radius, wait time, blink intervals), in `RobotSettings`.
- The robots' spots and the two tints (shown to the user before settling).
- Gamepad binding for Whistle.
- How the controller is generated and which clips fill each emote slot.
