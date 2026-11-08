# Building blocks for complex AI: proven patterns mapped to this codebase

Shared pieces the [behaviour catalogue](behaviour-catalogue.md) needs, each taken from a shipped game and mapped to our stack (BD Pro trees ticked by `EnemyManager`, `EnemyBrain` bridge, `EnemyMover` over A\* `FollowerEntity`, VContainer services, MessagePipe). **None of these is built.** The ranking is a recommendation (value / cost for a solo 6–9 month scope): propose a block to the user with the behaviour that needs it, build it when that behaviour is confirmed, then move its notes into SKILL.md and ARCHITECTURE.md → AI.

Research done 2026-10-31. Sources are at the end; claims marked (unverified) had no primary source.

## Rules every block follows

- **Cross-agent logic lives in a service, never in each tree** (Days Gone, Halo 2, Alien: Isolation all split this way). Services are plain C# in the owning system's installer, ticked centrally; trees ask through `EnemyBrain` (SKILL.md §4).
- **Trees decide, services allocate.** A tree never decides when to spawn, who may attack or which squad it joins.
- **World events arrive by MessagePipe** (raid start, midnight retreat, bell, day phase), one subscriber per system that forwards to the affected brains (`EventHandler.ExecuteEvent`), not polling conditionals.
- **Data over trees.** A new enemy, attack or boss move is data on a shared tree before it is a new tree.
- **Readability beats cleverness.** F.E.A.R.'s "flanking" was mostly cover choice plus barks; telegraphs, barks and visible state sell intelligence cheaper than planners.

What each agent should feel like to the player (telegraphs, fair senses, roster, bosses) is in [enemy-design.md](enemy-design.md); how agents combine into waves and a difficulty curve is in [encounters-and-waves.md](encounters-and-waves.md).

## Ranked blocks

### 1. Engagement slots and attack tokens (Kingdoms of Amalur, Doom 2016)

Who may attack at once. One `EngagementService` keeps a grid per defended target (player first; later the dog, fences, the paddock gate): a capacity, and per-enemy weights (goblin 4, tank 8). Each attack type has a token cost too. Enemies with a slot wait on an outer ring and step in only to strike; enemies without one stand in front of a free slot, which gives flanking for free. Release the slot after each attack so attackers rotate. Cooldowns per attack, per enemy kind and globally (no three AoE slams at once). Difficulty becomes two numbers: capacity and token budget.

- Tree: `HasEngagementSlot` conditional (abort Lower Priority) over the strike branch; fallback branch "hold at ring position" from the service.
- Cost: low (1–2 days). Value: high: readable melee raids, no swarming from behind off-screen.

### 2. Raid director (Risk of Rain 2, 7 Days to Die, Left 4 Dead)

When and what spawns. A `RaidDirector` (owned by a raid / spawn system, driven by the world clock and `DayPhaseChanged`) gets a credit budget for the night from the day and difficulty, and buys weighted enemy cards it can afford (thief, breaker, hunter, flyer; elite variants cost more). An alive cap holds the crowd under the perf gate, and killed enemies are replaced up to the cap until the wave's credits run out (7DTD). An intensity meter (damage taken, kills nearby) gives build-up → peak → lull (L4D); the next wave waits for the lull. Spawn out of view, by route (river, bridge, sky, inside the meadow), with a horn or howl telegraph before each wave. Midnight: stop buying, publish the retreat. Blood Moon / eclipse are director profiles, not new code.

- Tree: none. The director spawns through the resolver (SKILL.md §4) and assigns the role (thief / breaker / hunter) on the brain.
- Design side (warnings, intensity states with their timings, wave slots, formations, debuts, difficulty levers, the "too cheap" rule, a proposed data shape): [encounters-and-waves.md](encounters-and-waves.md).
- Cost: low–medium. Value: high: it *is* the raid experience, and it keeps enemy counts inside the budget.

### 3. Raiders know their targets; perception only where it matters (7 Days to Die, The Last of Us, Splinter Cell)

Blood-moon hordes in 7DTD always know where the player is: zero perception cost. Do the same for raiders: their role picks a target from a **target registry** (player, animals outside, drops, fence segments, exits), not from senses. Real perception is for daytime wanderers, wildlife and sneakers:

- `PerceptionService`: a fixed raycast budget per frame, round-robin over agents that need sight, 2–3 points on the target (Splinter Cell uses 8 bones; overkill here), batched with `RaycastCommand` (a suggestion, not sourced).
- `NoiseService.Emit(position, radius, kind)`: logical sound events (gunshot, sprint, bell), not real audio (The Last of Us). Radius multiplied per enemy kind; one occlusion ray per listener in range.
- Per agent: awareness (Unaware → Suspicious → Alert) and a last-known position (Halo 2's "beliefs"). Suspicious is the player's warning window (TLOU "agitated", Splinter Cell's detection timer): it plays a bark or sound and the agent turns to look, so the player knows they were noticed and why. A filling meter or icon is an option to ask the user about, since the HUD is theirs to decide.
- Sight is a cone plus range with a line-of-sight check; hearing is a radius. Tune both by playtest toward "fair" rather than realistic ([enemy-design.md](enemy-design.md) §1); day-chase give-up rules are in its §4 Pursuit limits.
- Tree: conditionals `CanSeeTarget`, `HeardNoise` under aborts; a Search subtree at the last-known position.
- Cost: medium (sight is the expensive part). Also covers the daytime leash: a chaser that loses its target for N seconds gives up and returns home.

### 4. Think-rate LOD and path spreading (Left 4 Dead, Warframe, LOD Trader, Project Highrise)

Scale before adding archetypes. `EnemyManager` ticks in tiers: near or on-screen at 15 Hz, far or off-screen at 2–5 Hz, outside an active bubble frozen or despawned (L4D keeps far wanderers only as a count per area). Avoid identical paths: give each attacker its own slot point from block 1, stagger repaths across ticks, and use the Movement Pack's `Pursue` look-ahead only where it reads well. A\* congestion costs are a later lever.

- Tree: none; it's `EnemyManager` and `EnemyMover`. Cost: low–medium. Gate: the 50-enemy budget (SKILL.md §6).

### 5. Companion follow (The Last of Us, Ellie)

Dog and drone. Every 0.25–0.5 s, sample ~8 candidate points in a ring around the player, reject ones behind walls or with an obstacle between companion and player, score by distance, staying on the same side, and **never in front of the player's view**. Move "close enough", with speed scaled by distance so it doesn't flip walk/run. Don't dodge pre-emptively; a last-moment sidestep plus a bark is enough. Ellie never teleports; in first person, teleporting when far and off-screen is acceptable (unverified). Companion combat lands mostly when the player can see it. Commands (stay, follow, herd) are brain state with aborts.

- Tree: `CompanionFollow` action (our task, or the Movement Pack's `Follow` once the A\* adapter exists). The drone needs a flying mover, not `FollowerEntity`.
- Cost: medium (position scoring is the work).

### 6. Boss = phase selector + data maneuvers (Psychonauts 2, Amalur, Left 4 Dead)

One boss tree reused per boss. Top: a phase selector with conditional aborts on HP thresholds. Each phase: a weighted-random (or utility) selector over **maneuvers**, each a data entry of telegraph → attack → recovery (the recovery is the punish window). Filter by range bucket, cooldown and "no more than N repeats"; shuffle like a deck (L4D's no-back-to-back rule). Enrage = a phase. A boss is unique: own `EntityScope`, ticked every frame. IDEAS.md's "boss = body + scale + weapon + archetype" fits: the maneuver set is the data. Psychonauts 2 built two bosses in about two months this way.

- Design the maneuver set from the player skills it tests (dodge, take cover, aim at the weak spot, move away from range): at least one maneuver per skill, styled to the boss's body and personality, plus a defensive option (block, retreat, armour phase) so the fight isn't only "shoot until dead". Fight structure, the pick rules (no more than two repeats, interleaved categories), giant bosses and elite-affix minibosses: [enemy-design.md](enemy-design.md) §5.

- Cost: medium per boss, mostly animation and tuning.

### 7. Animals and NPCs: routines and a few needs (Project Highrise, World of Zoo, Stardew Valley)

At scale, prefer routines over planners. Farm animals: 2–3 needs (hunger, sleep, roam urge) decaying in a central animal service at 1–2 Hz; a utility selector picks graze / wander-in-area / sleep / go home. World events override: the bell (go home, hurry on interact), night (sleep), carried (tree paused). Wildlife: herd anchor plus a single "flee threat" abort. Service robots and a collector robot: a schedule or job queue run open-loop (Highrise ran 1000 NPCs at 60 fps this way), re-deciding only when the queue empties or fails. Stardew keys schedules by weekday / season / weather in data.

- Tree: BD's utility selector (unity-behavior-designer-pro §4) for the "gray area" choice only (Merrill: convert only the selectors that need it).
- Cost: low. Where the animal system lives (own system vs a generalised mover shared with enemies) is undecided: ask before building.

### 8. Squads, only if raids need them (F.E.A.R., Days Gone)

A `SquadService` at ~2 Hz clusters nearby raiders, picks one squad behaviour, and writes a role into each member (Days Gone: a role is a subtree slotted into the individual tree). Members may override (fleeing beats advancing). Days Gone's *Confidence* (friendly vs enemy strength → retreat / hold / press) is a cheap later add. Skip frontlines. Barks before tactics.

- Cost: low for the coordinator, high for anything more. Build only when blocks 1–3 leave raids feeling flat.

## Architecture choice

Stay with **behaviour trees + a utility selector where choices are fuzzy + central services** (the hybrid Alien: Isolation, Days Gone and Halo 2 used). GOAP (F.E.A.R.) costs too much to author and debug at this scope, and Project Highrise abandoned a planner at scale. Pitfalls to avoid: per-agent state that should be shared, positions that jitter (use thresholds), parameter creep (shared definitions and variants), and cross-agent logic inside trees.

## Sources

Game AI Pro (gameaipro.com): Beyond the Kung-Fu Circle (Amalur engagement); Squad Coordination in Days Gone; Perception and Awareness in Splinter Cell Blacklist; Ellie: Buddy AI in The Last of Us; Infected AI in The Last of Us; 1000 NPCs at 60 FPS (Project Highrise); Building Utility Decisions into Your Existing Behavior Tree; Managing Pacing in Procedural Levels in Warframe; AI LOD Control with the LOD Trader; Advanced Techniques for Robust, Efficient Crowds; A Control-Based Architecture for Animal Behavior; Hierarchical AI for Multiplayer Bots in Killzone 3.
Other: Orkin, "Three States and a Plan: The AI of F.E.A.R." (GDC 2006); Booth, "The AI Systems of Left 4 Dead"; gamedeveloper.com: the AI of Doom (2016), the AI of Alien: Isolation, Handling Complexity in the Halo 2 AI, Psychonauts 2's modular boss maneuvers; riskofrain2.wiki.gg → Directors; 7daystodie.wiki.gg → Blood Moon Horde. Design sources (Unity blog, Stout, Keren, Left 4 Dead and Halo slides, wikis) are listed in [enemy-design.md](enemy-design.md) and [encounters-and-waves.md](encounters-and-waves.md). Unverified (no primary source found): Batman / God of War / Sifu attack rules, Souls / Monster Hunter boss selection, RDR2 / Far Cry animals, Valheim / ARK taming, Far Cry and Fallout dog companions.
