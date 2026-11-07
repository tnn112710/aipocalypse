# Raids

Who raids the farm, how hard, and how that grows. Agreed 2026-11-07. [CORE.md](CORE.md) owns the night schedule (raid or quiet night, Blood Moon, eclipse) and the failure rules; this file owns what a raid contains. Numbers are placeholders to tune unless stated otherwise. Unconfirmed suggestions are listed under [Proposals](#proposals--not-confirmed).

## Summary

- Raids hit the farm wherever the player is. Raiders go for the farm and its resources, not the player, unless the player comes close.
- Goblins raid from the start, easy at first, and grow slowly with the days.
- Other enemy types (zombies, robots…) raid only after the player provokes them: **grudge**, held per enemy type, decides *who* comes.
- A **Threat level** that follows the player's results decides *how hard* raids are, not the day.
- Beating a region boss calms its enemy type and wakes the type of the next region's boss; once killed, bosses return as weaker versions on Blood Moons and eclipses.
- Two **tribute bowls**: pay for an easier night, or offer bait for a harder night with more rewards.

## First raid

- Night 1 is quiet. The first raid, a few easy goblins, comes on night 2. The player usually wins it.
- **Prototype:** the first raid stays on night 1 for testing.

## Enemy types

- Raids are triggered **per enemy type**, a kind of enemy such as goblins, zombies or robots, not per region faction, so the asset budget sets how many there are. Goblins may be swapped for another enemy later.
- **Units:** each type has a few units (e.g. thieves, fence-breakers, fighters, shooters) and elite variants.
- **Not tied to a region:** one type turns up in several regions, in different mixes ([CORE.md](CORE.md#day-dusk-and-exploration)).
- Goblins are in every raid; their share grows slowly with the days, so nights get harder even for a player who rarely fights in the outer zone.
- **Registering:** hitting an enemy, or being chased by one, registers its type as a possible raider (no kill needed). From then on any of its units can raid.
- **Arrival:** each type announces itself with its own line and sound when it reaches the farm, like Valheim's "The forest is moving…"; the drone adds a joke ("The robots have joined the meeting").

## Grudge

- One grudge meter per registered enemy type other than goblins, from 0 to a max.
- Killing a type's enemies anywhere in the outer zone raises its grudge, whichever region; continuous killing fills it fast. Kills while defending the home don't count, so defending can't feed the next raid.
- The grudge cools down over time while the player leaves the type alone.
- A higher grudge raises both the type's chance to join a raid and how many of it come.
- Grudge changes raid size and mix, never how often raids happen; the schedule in CORE decides raid nights.

## Who comes on a raid night

- Goblins always come.
- Each registered type has a chance to join: a minimum of about 5–10% even at grudge 0, rising with its grudge. A type calmed by its boss's defeat has no minimum (see [Region bosses](#region-bosses)).
- **Ordinary raids:** at most 1 extra type early in the game and 2 later, so too many kinds never raid at once.
- **Blood Moons:** the limit is lifted and every registered type comes. Difficulty rises gradually from one Blood Moon to the next, never all at once.
- **Weaker bosses:** once a region boss has been killed, a weaker version of it appears on Blood Moons and eclipse days, never on ordinary nights.

## At the farm

- Every raid, Blood Moon and eclipse happens at the farm, wherever the player is.
- **Raiders target the farm, not the player:** animals, produce, crops, fences and defenses. Deposited items stay safe.
- **Noticing the player:** an enemy turns on the player only within its spotting range, which differs per unit.
  - A hit always makes an enemy notice the player, and a spotted enemy alerts the ones near it.
  - A clear cue, sound and visual, shows when an enemy turns on the player.
  - Enemies go back to the farm when the player gets away.
  - Each unit reacts its own way: thieves flee with their loot, breakers keep breaking, fighters turn on the player.
- **Away or late:** the longer the farm is unguarded, the more the raiders sabotage and sack it. A player who gets home late can still fight the rest of the raid.
- **Alerts:** while the player is away, the drone reports the raid as it goes ("Raid started", "Fence down", "They're in the paddock"), so racing home is a choice, not a surprise in the morning.
- **Out at night:** the regions are more dangerous after dark, which also pushes the player home before a raid. At night, outer-zone enemies use the elite markers (see [Enemy toughness](#enemy-toughness)); dedicated night enemies and predators come later.

## How hard: wave power and Threat

- Every raid gets a **wave power** budget, filled with enemies. Each unit has a power cost; elites and weaker bosses cost more.
- The budget comes from the **Threat level** plus the grudges of the enemy types coming.
- **Threat** rises after each won raid and falls after a lost one; how far one result moves it is capped. A skilled player reaches high Threat around day 30, a struggling one around day 60–80.
  - **Floor:** rises slowly with the days, so a struggling player still moves forward.
  - **Ceiling:** set by the region bosses beaten, so a skilled player can't outrun the content.
  - Never scaled by the player's gear, so upgrades always feel like upgrades.
  - **Won or lost:** a raid is won when the player is home and still standing at its end, and lost when they're knocked out or not home. Only a knock-out lowers Threat and grudge; being away changes neither, so staying out can't be used to lower difficulty.
  - **Running away doesn't work:** every lost raid carries the Token loss (see [Losing a raid](#losing-a-raid)), so leaving or staying away never costs less than fighting. Chasing a thief across the river is fine as long as the player is home when the raid ends.
- Threat is shown openly, framed as the AI Lord's "difficulty patches" ("You were having too much fun. Threat +1"). Higher Threat gives better loot and a better daily box.
- **Rhythm:** a sawtooth on top of Threat. Raids build up through each 10-day block, peak at the Blood Moon, then drop; the next block starts a little higher than the last.
- A raid night is split into waves, small to big, with short pauses to reload and repair: 2–3 on ordinary raids, which end at midnight, and 4–5 on Blood Moons and eclipses.

### Enemy toughness

- **HP is fixed per unit** and never grows with time, so "every bullet counts" stays readable and gun tiers feel like real upgrades.
- Raids get harder through more enemies, harder mixes and **elite variants**: visibly marked (aura, armor or color), with more HP and damage and a higher power cost. Higher Threat and Blood Moons make elites more common.
- **Special occasions** use the same visible markers, e.g. enraged enemies on Blood Moons or armored units on eclipses.
- **Bosses are the exception:** repeat summons scale their HP.

## Tribute bowls

Two bowls side by side on one altar in the yard: calm the night or provoke it.

- **One bowl per night:** filling one shuts the other; taking the offering back reopens both. The bowls lock at dusk, setting the night.
- **The level shows on the bowl:** 1–3 glowing marks, readable from across the yard.
- **Calm bowl:** sacrifice Tokens and resources to lower that night's wave power: level 1 −10%, level 2 −20%, level 3 −30%. Blood Moons and eclipse days need a larger tribute. Upgrades can raise the reductions later.
- **Provoke bowl:** offer bait (common enemy parts or trophies, not Tokens) to raise that night's wave power, e.g. +10% / +20% / +30%, for more rewards.
  - Pays consumables only: more ammunition, scrap and Tokens, and a better daily box; never progression materials (see [Anti-grinding](#anti-grinding)).
  - Closed on Blood Moons and eclipse days, which are already the peak nights.
  - Built after the calm bowl.
- **Bowl nights never raise Threat**, whichever bowl is used; losing one still lowers it. Paying for an easier night never makes the next one harder, and provoking can't push Threat up.
- Flavor: the drone has no idea why the AI added a ritual altar either.

## Losing a raid

- **Lost when** the player is knocked out, or not home when the raid ends.
- **Token loss:** the raiders crack the robots' Token vault and take X% of all the player's Tokens, deposited ones included. Tokens already repaid on the debt are never lost.
- Also damage to the home and some animals, penned ones included ([CORE.md](CORE.md#resources--failure)). A knocked-out player also loses part of the carried items.
- **Knocked out:** also lowers Threat, and the grudge of every enemy type in that raid by a fixed 25% of the meter's max, down to 0. Being away lowers neither.
- **Upgrades:** a smaller Token loss; more grudge reduction on death; faster grudge cool-down.

## Region bosses

- Each region has a boss, and each boss belongs to one enemy type (e.g. a zombie boss, a robot mech). The player summons the boss to fight it.
- **First kill:** a key or trophy (like Valheim's boss trophies) toward the final boss, plus Tokens and materials.
- **Later kills:** Tokens and materials only; the boss gets harder each time.
- **Every kill calms its type:** its grudge drops to 0 and it loses its minimum chance, so it joins ordinary raids again only if the player angers it again. The type of the next region's boss takes notice and is registered, so ordinary raids follow the player's progress; Blood Moons still bring every registered type. Like Valheim, where each boss's own raid ends when the boss dies.
- After the first kill, its weaker version raids on Blood Moons and eclipse days (see above).

## Anti-grinding

- Progress only comes from exploring and bosses. Kills and raids pay ammunition, scrap and Tokens; next-tier materials, rescued robots, trophies and keys don't drop from raids.
- Cleared camps stay cleared for some days, and wandering enemies per area are capped, so grinding one spot runs dry.
- Grinding stays allowed as the safe, slow choice; exploring is the risky, fast one.

## Balancing and monitoring

- A dev overlay and one log line per raid: budget, points spent, enemy types and units, waves, damage taken, animals lost, ammunition used, result.
- A sheet of the budget curve (Threat floor and ceiling, sawtooth) for days 1–60, to check a whole run before playtests.

## Proposals — not confirmed

- A raid the player beats spends most of the grudge of the enemy types in it; a lost raid keeps it.
- Grudge shown in 4 steps (calm, annoyed, angry, furious), with a small pop when a kill raises it; the drone announces a new registration ("New contact: robots. They know your scent now"). Flavor: enemy types rate the player like 1–5 star customer reviews.
- The raid is rolled at dawn and the morning forecast names the enemy types coming; a type's first raid shows as a "?" silhouette.
- Types roll angriest first until the slots are full; at most one type's first raid per night; a type that came last raid rolls at half chance unless its grudge is at max.
- The types coming share one budget, weighted by grudge, so a second type adds variety rather than doubling the raid.
- Each type has its own raid job, so the forecast changes preparation: goblins steal; zombies come in numbers and break fences; robots are few, armored and ranged, with the longest spotting range.
- A starting difficulty choice (Relaxed / Normal / Hard) that sets how fast Threat climbs.

## Open

| Area | Needed decision |
| --- | --- |
| Balance | Target win rates for ordinary raids and Blood Moons; each unit's power cost; Threat steps; grudge gain, cap and cool-down rates; the Token loss (X%); a shots-to-kill table (each unit and elite × each gun tier) |
| Enemy types | Which types beyond goblins (zombies and robots wanted), their units, and which regions each turns up in |
| Blood Moon miniboss | Who leads Blood Moons before any region boss is beaten; decided later |
| Eclipse | Which enemy types and how much power, alongside its giant boss |
| Bosses | Each boss's identity and enemy type, summoning, rewards and difficulty steps |
| Final zone | Its one boss is the ultimate AI boss, fought as a Megabonk-style run ([CORE.md](CORE.md#day-dusk-and-exploration)). What the zone is and how the run plays: revisit after the rest |

## References

| Game | Reference purpose |
| --- | --- |
| Valheim | Raid types unlocked by progression, each boss kill ending its own raid; raids announced by a line; running away doesn't end a raid; boss trophies as keys; visible star levels for tougher creatures |
| Don't Starve | A kill-driven meter (naughtiness) that summons a thief and cools down over days |
| The Forest / Sons of the Forest | Enemy aggression rising with kills and bringing raids to the base; ours can cool down |
| Risk of Rain 2 | A director that spends a growing budget on monsters |
| Left 4 Dead | Tension that builds, peaks and relaxes |
| Resident Evil 4 | Difficulty that follows player performance |
| Hades | Opt-in difficulty paid back with rewards (Heat costs nothing) |
| Deep Rock Galactic | Picking a harder hazard level before a mission for more pay |
| Dome Keeper | Digging a little deeper or getting back before the attack on the base |
