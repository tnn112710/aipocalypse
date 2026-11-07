# Raids

Who raids the farm, how hard, and how that grows. Agreed 2026-11-07. [CORE.md](CORE.md) owns the night schedule (raid or quiet night, Blood Moon, eclipse) and the failure rules; this file owns what a raid contains. Numbers are placeholders to tune unless stated otherwise. Unconfirmed suggestions are listed under [Proposals](#proposals--not-confirmed).

## Summary

- Goblins raid from the start, easy at first, and grow slowly with the days.
- Region factions raid only after the player provokes them: **grudge** decides *who* comes.
- A **Threat level** that follows the player's results decides *how hard* raids are, not the day.
- Region bosses, once killed, return as weaker versions on Blood Moons and eclipses.
- Two **tribute bowls**: pay for an easier night, or offer bait for a harder night with more rewards.

## First raid

- Night 1 is quiet. The first raid, a few easy goblins, comes on night 2. The player usually wins it.
- **Prototype:** the first raid stays on night 1 for testing.

## Factions

- A **faction** is the set of enemy types from one biome: goblins, plus one faction per region (Pine Forest, Swamps, Highlands). Goblins may be swapped for another enemy later.
- Goblins are in every raid; their share grows slowly with the days, so nights get harder even for a player who rarely fights in the outer zone.
- **Registering:** hitting one of a faction's enemies, or being chased by one, registers that faction as a possible raider (no kill needed). From then on any of its types can raid.
- Fallback if this proves too broad: register per type, so a faction's raid brings only the types the player has met.

## Grudge

- One grudge meter per registered region faction, from 0 to a max.
- Killing a faction's enemies in its own region raises its grudge; continuous killing fills it fast. Kills while defending the home don't count, so defending can't feed the next raid.
- The grudge cools down over time while the player leaves the faction alone.
- A higher grudge raises both the faction's chance to join a raid and how many of it come.
- Grudge changes raid size and mix, never how often raids happen; the schedule in CORE decides raid nights.

## Who comes on a raid night

- Goblins always come.
- Each registered faction has a chance to join: a minimum of about 5–10% even at grudge 0, rising with its grudge.
- **Ordinary raids:** at most 1 extra faction early in the game and 2 later, so too many kinds never raid at once.
- **Blood Moons:** the limit is lifted and every registered faction comes. Difficulty rises gradually from one Blood Moon to the next, never all at once.
- **Weaker bosses:** once a region boss has been killed, a weaker version of it appears on Blood Moons and eclipse days, never on ordinary nights.

## How hard: wave power and Threat

- Every raid gets a **wave power** budget, filled with enemies. Each enemy type has a power cost; elites and weaker bosses cost more.
- The budget comes from the **Threat level** plus the grudges of the factions coming.
- **Threat** rises after each won raid and falls after a lost one; how far one result moves it is capped. A skilled player reaches high Threat around day 30, a struggling one around day 60–80.
  - **Floor:** rises slowly with the days, so a struggling player still moves forward.
  - **Ceiling:** set by the region bosses beaten, so a skilled player can't outrun the content.
  - Never scaled by the player's gear, so upgrades always feel like upgrades.
  - **Won or lost:** a raid is lost when the player is knocked out at home and won when they're still standing at its end. Being away on a raid night changes neither Threat nor grudge, so staying out can't be used to lower difficulty.
- Threat is shown openly, framed as the AI Lord's "difficulty patches" ("You were having too much fun. Threat +1"). Higher Threat gives better loot and a better daily box.
- **Rhythm:** a sawtooth on top of Threat. Raids build up through each 10-day block, peak at the Blood Moon, then drop; the next block starts a little higher than the last.
- A raid night is split into waves, small to big, with short pauses to reload and repair: 2–3 on ordinary raids, which end at midnight, and 4–5 on Blood Moons and eclipses.

### Enemy toughness

- **HP is fixed per enemy type** and never grows with time, so "every bullet counts" stays readable and gun tiers feel like real upgrades.
- Raids get harder through more enemies, harder mixes and **elite variants**: visibly marked (aura, armor or color), with more HP and damage and a higher power cost. Higher Threat and Blood Moons make elites more common.
- **Special occasions** use the same visible markers, e.g. enraged enemies on Blood Moons or armored types on eclipses.
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

## Dying in a raid

- Costs X% of all the player's Tokens, deposited ones included; Tokens already repaid on the debt are never lost. Also part of the carried items, damage to the home and some animals ([CORE.md](CORE.md#resources--failure)).
- Lowers Threat, and the grudge of every faction in that raid by a fixed 25% of the meter's max, down to 0.
- **Upgrades:** a smaller Token loss; more grudge reduction on death; faster grudge cool-down.

## Region bosses

- Each region has its own faction and a boss. The player summons the boss to fight it.
- **First kill:** a key or trophy (like Valheim's boss trophies) toward the final boss, plus Tokens and materials.
- **Later kills:** Tokens and materials only; the boss gets harder each time.
- After the first kill, its weaker version raids on Blood Moons and eclipse days (see above).

## Anti-grinding

- Progress only comes from exploring and bosses. Kills and raids pay ammunition, scrap and Tokens; next-tier materials, rescued robots, trophies and keys don't drop from raids.
- Cleared camps stay cleared for some days, and wandering enemies per area are capped, so grinding one spot runs dry.
- Grinding stays allowed as the safe, slow choice; exploring is the risky, fast one.

## Balancing and monitoring

- A dev overlay and one log line per raid: budget, points spent, factions and types, waves, damage taken, animals lost, ammunition used, result.
- A sheet of the budget curve (Threat floor and ceiling, sawtooth) for days 1–60, to check a whole run before playtests.

## Proposals — not confirmed

- A raid the player beats spends most of the grudge of the factions in it; a lost raid keeps it.
- Grudge shown in 4 steps (calm, annoyed, angry, furious), with a small pop when a kill raises it; the drone announces a new registration ("New contact: Swamp Folk. They know your scent now"). Flavor: factions rate the player like 1–5 star customer reviews.
- The raid is rolled at dawn and the morning forecast names the factions coming; a faction's first raid shows as a "?" silhouette.
- Factions roll angriest first until the slots are full; at most one faction's first raid per night; a faction that came last raid rolls at half chance unless its grudge is at max.
- The factions coming share one budget, weighted by grudge, so a second faction adds variety rather than doubling the raid.
- A starting difficulty choice (Relaxed / Normal / Hard) that sets how fast Threat climbs.

## Open

| Area | Needed decision |
| --- | --- |
| Balance | Target win rates for ordinary raids and Blood Moons; each type's power cost; Threat steps; grudge gain, cap and cool-down rates; the Token loss on death (X%); a shots-to-kill table (each type and elite × each gun tier) |
| Blood Moon miniboss | Who leads Blood Moons before any region boss is beaten; decided later |
| Eclipse | Which factions and how much power, alongside its giant boss |
| Bosses | Each boss's identity, summoning, rewards and difficulty steps |
| Final zone | Its one boss is the ultimate AI boss, fought as a Megabonk-style run ([CORE.md](CORE.md#day-dusk-and-exploration)). What the zone is and how the run plays: revisit after the rest |

## References

| Game | Reference purpose |
| --- | --- |
| Valheim | Raid types unlocked by progression; boss trophies as keys; visible star levels for tougher creatures |
| Don't Starve | A kill-driven meter (naughtiness) that summons a thief and cools down over days |
| The Forest / Sons of the Forest | Enemy aggression rising with kills and bringing raids to the base; ours can cool down |
| Risk of Rain 2 | A director that spends a growing budget on monsters |
| Left 4 Dead | Tension that builds, peaks and relaxes |
| Resident Evil 4 | Difficulty that follows player performance |
| Hades | Opt-in difficulty paid back with rewards (Heat costs nothing) |
| Deep Rock Galactic | Picking a harder hazard level before a mission for more pay |
