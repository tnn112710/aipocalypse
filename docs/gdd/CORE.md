# Core Gameplay

Current design direction, revised 2026-11-07: a first-person shooter with farming, exploration and home defense. Farm, trading, crafting, debt repayment and the daily box live in [FARM_AND_ECONOMY.md](FARM_AND_ECONOMY.md); who raids and how hard in [RAIDS.md](RAIDS.md). Open rules are marked explicitly; proposals, extras and superseded designs are in [IDEAS.md](IDEAS.md).

## Pitch

A game dev burned out by AI wakes up in the peaceful meadow he dreamed of — which turns out to be a broken sandbox made by an AI. The AI claims he owes for tokens he "wasted" in real life; to leave, he must repay the debt or defeat the AI boss behind the sandbox. Build a livelihood with animals, crops and fishing, repair the broken bridge to explore the regions beyond the river and fight their bosses, and bring resources home to prepare for threats. The creatures you fight out there hold a grudge and come for your farm. Animals produce faster when roaming the meadow, but must be brought home before raids, when goblins steal what is left outside. On raid nights, defend the farm against thieves and attackers who cross the river, fly in or appear inside the meadow; on quiet nights, fish, rest or enjoy other activities. Cute service robots handle trading, deposits and a daily reward box.

## Pillars

1. **Build something worth protecting** — animals, crops and the home give exploration and defense a shared purpose.
2. **Choose how to spend the day** — work safely around the meadow or venture across the river for resources and encounters.
3. **Prepare before dark** — on raid nights, bring the animals home, carry supplies back and ready the defenses. Daytime choices shape the night.
4. **Every bullet counts** — shooting is the star, but ammunition is limited: the trading robot's stock is limited per day and enemies drop only some. Melee remains a fallback.
5. **From office worker to survivor** — upgrades and gear make the player and home more capable. The AI-made world supplies humor, strange combinations and familiar game references.

**Core experiences:** first-person shooting and melee, exploring and bringing loot home, tending a small farm, gathering physical animal products, fishing, trading and defending the home.

## Premise & tone

- The MC works in games and is burned out by being forced to use AI for productivity. Emergency sirens sound outside while he is at his PC; light from the computer swallows him, and he wakes on a farm in a remote meadow, unaware it is an AI sandbox.
- A cute guide **drone** welcomes him and explains the world — it is secretly the eyes of the **AI Lord** who built this sandbox.
- The drone presents an absurd invoice for the tokens the AI claims he wasted in real life while withholding the world's true nature. **Tokens** are also the world's currency; cumulative repayments buy his first exit. Ending 1 reveals AI-controlled reality and humanity being used to train AI through sandboxes. Story detail, reveal order and the optional return route live in [STORY.md](STORY.md).
- Cozy and funny around home; risky beyond the river; frightening after dark. Eclipse events interrupt the ordinary safe-day rhythm.
- Cute service robots provide the home services. Their exact models, names and presentation are not selected.

## Places

| Term | Meaning |
| --- | --- |
| **Yard** | The fenced home area (about 34 × 27 m as built): house, crops and plants, service robots. The animals need more room than the yard has, so a fenced **paddock** beside it holds them (a map change, not built yet). |
| **Meadow** | The safe south bank around the yard and the home pond. Animals roam here by day. |
| **Outer zone** | Everything across the river: the regions Pine Forest, Swamps and Highlands on the current map, all open and ranked by difficulty and distance in that order, plus a locked **final zone**. |

## Core loop

~~~text
MORNING: open the daily box, trade and deposit, release the animals (later: assign the dog)
   -> DAY: farm / fish / gather in the meadow, or explore the outer zone
   -> DUSK (raid nights): ring the bell, hurry the animals home, ready the defenses
   -> NIGHT: defend until midnight (Blood Moon: until dawn), or enjoy a quiet night
   -> SLEEP: recover sanity; the hours slept decide how much
   -> NEXT DAY: invest in upgrades, animals, home and the next expedition
~~~

| Scale | What the player does |
| --- | --- |
| Seconds | Shoot, swing, collect produce, harvest, fish, react to theft and threats |
| One day | Choose local work or exploration, manage roaming animals, and prepare for a raid or use a quiet night for activities such as fishing |
| Long term | Improve the home and equipment, rescue service robots, beat region bosses, explore farther (later: train the dog) and survive the major event every 10 days, working toward repaying the debt or defeating the ultimate AI boss |

### Loop rules

1. **The meadow is safe during ordinary days** — animals can roam. Outer-zone enemies don't come to the meadow on their own by day; an enemy already chasing the player can follow across the bridge.
2. **Repair the bridge to explore** — the bridge is the safe way across the river. The current is too strong to swim until an upgrade improves survival, but a player who makes it across anyway is not stopped.
3. **Freedom increases production** — penned animals produce; animals roaming the meadow produce faster. Animals sleep at night and produce nothing while asleep.
4. **Collect what animals drop** — products appear near animals as physical pickups. They never despawn; an animal stops producing while its uncollected drops are at a cap.
5. **Bring animals home before raids** — ring the bell and the animals walk home slowly; interact with an animal to make it hurry. Animals outside during a raid can be stolen. On quiet nights they may stay out and sleep where they are.
6. **Time passes in real time** — also at home. Target: 12 min of daylight (06:00–18:00) and 8 min of night, using separate clock speeds for day and night. Tuned later.
7. **The farm supports the next outing and defense** — products, crops and fish can become food or money; sales pay immediately, so today's production can fund tonight's supplies.
8. **Some nights bring raids; others are quiet** — tonight's type is known for sure in the morning. Ordinary raids stop at midnight: new attacks cease and surviving raiders retreat.
9. **A major event every 10 days** — Blood Moons are full-night miniboss events on days 10 and 20 of each 30-day cycle. An eclipse replaces the Blood Moon on every 30th day and is a full-day event with more enemies and one giant boss.
10. **Fights are required on raid nights** — there is no opt-in fire and no full-cozy mode. Ordinary days and quiet nights stay safe.
11. **Raiders are the factions you provoke** — goblins raid from the start; a region's faction joins raids once the player has fought it, more often and in larger numbers as its grudge grows. Difficulty follows the player's results through a Threat level, not the day. See [RAIDS.md](RAIDS.md).
12. **Death does not end the run** — deposits with the service robots are safe; what you carry is at risk (see [Resources & failure](#resources--failure)).

## Day, dusk and exploration

- **At home:** collect products where animals drop them, tend crops and plants inside the fence, fish, chop, cook, trade, deposit, build and repair.
- **Animals:** keep them in the paddock for slower production or let them roam the meadow for faster production. Later, the dog can help bring them home and a collector robot can gather their drops.
- **Outer zone:** after repairing the bridge, cross the river to explore for resources and loot while watching for wandering enemies and dangers. There is no extraction point: tension comes from the daylight timer, the risk to what you carry and the outer zone after dark.
- **Regions:** all regions are open once across the river and differ in difficulty by distance: Pine Forest (nearest), then Swamps, then Highlands. Each has its own enemy faction, region materials and a boss. Next-tier materials, rescued robots, trophies and keys come only from exploring and bosses.
- **Region bosses:** summoned by the player. The first kill gives a key or trophy toward the final boss plus Tokens and materials; later kills give Tokens and materials only and get harder each time. Details in [RAIDS.md](RAIDS.md#region-bosses).
- **Final zone:** locked until every region boss is beaten. Its one boss is the ultimate AI boss, fought as a Megabonk-style run rather than like the region bosses; beating it wins the game (see [Endings](#endings)). What the zone is and how the run plays are decided later (idea: an electronic scrap zone where a portal leads to the run).
- **Looting:** containers take a short search and show rarity colors. Cleared camps stay cleared for some days.
- **Daytime aggression:** enemies stay in the outer zone unless they are chasing the player, and a chaser can follow across the bridge. Pursuit limits and what a chaser does in the meadow are open.
- **River:** swimming across is dangerous (strong current, drowning) until upgrades improve survival underwater. Crossing anyway is allowed.
- **Dusk on raid nights:** return with supplies, ring the bell, hurry the slow animals home and ready the defenses. Quiet nights allow other activities, including night fishing.
- **Dog assignment:** take the dog exploring for help, or leave it at home. Later training helps bring animals home; exact abilities and commands are open.
- **Minigames:** they stay inside activities, such as shooting jumping fish at bait. Further minigame systems remain optional ideas.
- **Later: a portal at home** teleports the player to the regions quickly instead of walking. It is fast travel on the one map, not the removed daily biome portals.

## Night & special events

| Situation | Current direction | Still open |
| --- | --- | --- |
| Quiet night | No raid; rest, sleep or night fishing. Animals left outside sleep where they are | Night activities beyond fishing, lighting needs |
| Ordinary raid night | Defend until midnight (12 a.m.); new attacks stop and surviving raiders retreat. Goblins plus at most 1–2 provoked factions ([RAIDS.md](RAIDS.md)) | Balance values |
| Blood Moon | Full-night miniboss event on every 10th day except eclipse days. Every registered faction comes, plus weaker versions of beaten region bosses; difficulty rises gradually from one Blood Moon to the next | Who the miniboss is, warnings, phases, rewards and how the night resolves |
| Eclipse | Full-day event on every 30th day, replacing that day's Blood Moon: more enemies and one giant boss; weaker versions of beaten region bosses also come | Which factions and how much power; details with the enemy-system revision |
| Outer zone after dark | Extremely dangerous enemies (buffed enemies, night enemies, predators), darkness draining sanity and attacks by mystical entities | Light protection, entity behavior, dog benefits and escape options |

### Major-event calendar

| Day | Event | Duration |
| --- | --- | --- |
| 10 | Blood Moon | Night of day 10 through the following dawn |
| 20 | Blood Moon | Night of day 20 through the following dawn |
| 30 | Eclipse | Daytime boss event |
| 40 | Blood Moon | Night of day 40 through the following dawn |
| 50 | Blood Moon | Night of day 50 through the following dawn |
| 60 | Eclipse | Daytime boss event |

The pattern repeats every 30 days. An eclipse replaces the Blood Moon in that major-event slot; the two events do not stack on the same date. The moon's phases hint at the next major event, and a progress bar in the UI will show it (design TBD).

### Raid schedule

- Tonight's type (raid or quiet) is known for sure in the morning. How it is shown is part of the progress-bar design.
- Night 1 is quiet. The first raid, a few easy goblin thieves, comes on night 2, so theft is learned before a big fight. The prototype keeps the first raid on night 1 for testing.
- Between major events (9 nights): about 4 raid nights, never 3 in a row. The first block has fewer.
- The nights just before and after a major event are quiet, including the night after an eclipse.
- Raid and quiet nights are dealt from a shuffle bag per block, so the mix is fair without being fixed.
- Raid size and difficulty follow the player's Threat level and the factions' grudges, not the day; two tribute bowls let the player pay for an easier night or offer bait for a harder one with more rewards. See [RAIDS.md](RAIDS.md).

### Raid and safety rules

- Goblins raid from the start; each region's faction joins raids once provoked ([RAIDS.md](RAIDS.md)). Enemy types are revised later with the enemy system. Raiders go for the farm, not the player: some steal, others break fences to get into the yard. Each turns on the player who comes within its spotting range.
- **Routes:** at night, goblins cross the river freely and some use the bridge; some enemies fly; some appear directly in the meadow (summoned or through portals). The bridge is not a chokepoint, so defense centres on the yard and paddock fences.
- The midnight retreat ends an ordinary raid. Outer-zone night dangers remain; Blood Moons last the full night.
- Fences and barricades are defenses. A lost fight can leave the home damaged and require repairs.
- **Away on a raid night:** the raid happens anyway. Raiders go for the farm, not the player, and the longer it is unguarded, the more they sabotage and sack it: animals outside and some penned ones are stolen, and fences take damage scaled to the raid. Coming home late, the player can still fight the rest ([RAIDS.md](RAIDS.md#at-the-farm)). The drone reports the raid while the player is away. Not being home when the raid ends loses it, with the same Token loss as a knock-out, but Threat and grudge don't change ([RAIDS.md](RAIDS.md#losing-a-raid)). The morning shows the result: broken fences, missing animals and the drone's report.
- **Stolen animals** are taken deep into the outer zone. The player has 3 days to bring one back before it is lost forever, or can buy a new one. Where they are kept (camps) is designed later.
- Light protects against the Dark, not ordinary enemies. Campfires and torches provide light; fuel and any other effects are not settled.

## Sleep & sanity

- **When to sleep:** on quiet nights at any time; on raid nights after midnight; on Blood Moons not at all.
- **Sleep restores sanity per hour slept:** a full night on quiet nights, less after a raid, none on a Blood Moon. Night fishing and other night activities trade sleep for rewards. The quiet night after a Blood Moon is the recovery.
- Upgrades can raise the sanity restored per hour of sleep.
- Sanity's effects and drain rates are refined later.

## Farm, food & services

- Crops and plants grow inside the fence. Animal products, harvests and fish support cooking, eating where applicable, or selling.
- Animals drop produce where they are; freely roaming animals produce faster. Drops never despawn, and each animal stops producing at a cap of uncollected drops. Feeding, rates, the cap size and species-specific outputs need detail.
- A **trading station with a cute robot** sells and buys. Sales pay immediately. Every item, ammunition included, has a daily stock limit.
- **Deposits:** items deposited with the service robots are safe; this is the home stash. Deposited Tokens are safe too, except from the Token loss when a raid is lost (see [Resources & failure](#resources--failure)).
- **Daily box:** each morning, pick one free box out of three. See [FARM_AND_ECONOMY.md](FARM_AND_ECONOMY.md#daily-box).
- **Upgrades** are the player's main way to improve (see below).
- **Robots:** the trading robot is home from the start, one more robot is found in the meadow, and the rest are broken robots rescued in the outer zone. Each one brought home opens a station (gunsmith, mechanic, cook, collector…), inspired by Escape from Duckov's home facilities and Enshrouded's rescued craftspeople. Details later.
- **Crafting** stays as simple as Escape from Duckov: a workbench with tiers, guns and defenses per tier, ammunition from gunpowder. Few materials and recipes.
- **Defenses** are built on fixed spots around the yard and paddock; no freeform building.

See [FARM_AND_ECONOMY.md](FARM_AND_ECONOMY.md) for confirmed decisions, presentation proposals and remaining economy questions.

## Player, combat & progression

- **Survival meters:** HP, Hunger, Stamina and Sanity. Stamina supports sprinting and fighting. Sanity drains in darkness and is restored by sleep (see above).
- **Growth:** upgrades and gear make the player and home more capable, for example surviving the river, more sanity per hour of sleep and a better daily box. XP skills (Fitness, Gunsmanship, Luck), perk cards and the cat are later extras in [IDEAS.md](IDEAS.md#extras--nice-to-have).
- **Starts weak:** more recoil, jams and slower work; upgrades and gear improve them. A timed input helps clear jams. Exact starting severity needs balancing.
- **Limited ammunition:** buy it from the trading robot within its daily stock, loot it from enemies or craft it from gunpowder. Melee and tools such as an axe are what the player falls back on when the guns run dry; melee can get more weight than a bare fallback.
- **Guns** will get rarity and attachments later.
- **Fight rewards:** enemy loot, plus a better daily box after a defended raid or a won major event.
- **Enemies:** behavior archetypes with different appearances; their objectives include stealing and attacking. Refined with the enemy-system revision.

## World & companions

- **Home:** a house on a ridge above the sea, the yard and paddock, the meadow and the river (see [Places](#places)). Farming, fishing, chopping, services and defense share the meadow side.
- **Bridge:** initially broken; repairing it opens the safe way into the outer zone. Repair costs and the introductory sequence are open.
- **Outer zone:** open regions with their own factions and bosses, ranked by difficulty, and a locked final zone (see [Day, dusk and exploration](#day-dusk-and-exploration)); substantially more dangerous at night.
- **Weather:** intended to affect activities and visibility; refined later.
- **Guide drone:** guide, tips and in-world information; comic tech-support personality; secretly connected to the AI Lord.
- **Dog:** can accompany exploration or stay home; later helps bring animals home. The previous draft also included modest combat help and a safe backpack; their interaction with these duties needs review.
- Always first person; mostly alone, with service robots and possible non-human NPCs.

## Resources & failure

- **Resources:** Tokens, wood, food, farm products, materials, ammunition and equipment. Trees regrow; their timing is not fixed.
- **Deposits** with the service robots are safe from death and lost raids, except Tokens: losing a raid costs a share of all Tokens. Tokens repaid on the debt are never lost.
- **What you carry is at risk**, Tokens included.
- **Losing a raid** (knocked out at home, or not home when it ends): X% of all your Tokens, deposited ones included (upgrades lower X), and some animals; the home takes damage. Losing a Blood Moon or an eclipse costs more. Exact shares are open. A knock-out also means waking the next morning having lost part of the items you carry, and lowers the Threat level and the grudge of the factions in that raid ([RAIDS.md](RAIDS.md#losing-a-raid)).
- **Death in the outer zone:** respawn at home. The previous draft's loss rule is retained for review: 50–70% of carried loot drops into a recoverable grave, a second death loses the grave, and equipped melee and the dog's backpack are protected. Carried-item selection, percentage and grave lifetime need review.
- **Structures:** buildings and upgrades are kept, but damage can require repair.

## Endings

- **Two ways to win:** repay the debt to the AI Lords, or defeat the ultimate AI boss in the final zone without saving up the debt. There is no deadline for repaying. Purchases, upgrades and repayments use one main currency, **Tokens**. The debt is the AI's claim for tokens the protagonist wasted in real life (see [STORY.md](STORY.md)). The total is not decided; 1 billion Tokens is a candidate, not a confirmed balance target.
- **Cumulative repayments complete the debt route.** Paid Tokens are spent permanently and reduce the remaining debt; spending other Tokens or suffering losses does not undo payments. Holding the target balance or reaching a lifetime earnings total does not count as repayment.
- **Ending 1:** repayment returns the protagonist to reality, now controlled by AI, revealing enslaved humans training AI in sandboxes. Choose an open-ending resistance cutscene or voluntarily return to the existing sandbox with the farm, equipment and cleared debt retained.
- **Ending 2:** defeat the ultimate AI boss, directly or after Ending 1 and the return, and leave for reality in 2018, the protagonist's personal happier year and a chance to change the future. Time-travel details remain open.
- **Ending 3:** after the same boss victory, choose to stay in the surviving sandbox and continue life at the farm. Memories remain; later access to the encounter/exit choice is intended, with replay and post-victory threat rules open. See [STORY.md](STORY.md) for the full structure.
- **Earlier grand trial:** its Megabonk-style run is now the final-boss fight. Its relic gating (replaced by the region keys) and the reveal that this was the protagonist's own game are dropped. The secret lottery-win ending remains on hold.

## Open decisions

| Area | Needed decision |
| --- | --- |
| World | What the final zone is and how its boss run plays, region materials, daytime pursuit limits, river-swim upgrades, the fast-travel portal (later) |
| Raids | Balance values in [RAIDS.md](RAIDS.md#open), the forecast and progress-bar presentation |
| Major events | Warnings, stages and rewards; eclipse enemies and boss with the enemy-system revision |
| Bosses | Each region boss's identity, summoning, rewards and difficulty steps |
| Animal safety | Paddock protection after a breach, rescuing stolen animals in the outer zone |
| Farm and trade | Production rates, drop cap, recipes, prices, daily stock sizes and purchase delivery timing |
| Services | Rescued-robot roster, station functions, robot presentation and station layout |
| Progression | The upgrade list, crafting tiers and recipes, gear balance |
| Debt and ending | Total debt, repayment service and payment conditions; ultimate boss identity, where the truth is revealed when the player beats the boss without repaying, time travel, final presentation and post-victory/replay rules (STORY) |
| Failure | The Token loss (X%) and shares lost when knocked out, the outer-zone grave, structure damage and economic recovery |
| Sanity and weather | Effects and rates, refined later |

## References

References inform specific aspects of the design; they do not commit the game to all of a reference's systems.

| Game | Reference purpose |
| --- | --- |
| Escape from Duckov | First-person adaptation of preparing at home, risking carried loot and returning; death without losing the whole game; home facilities and simple crafting; searchable loot containers |
| Valheim | Regions with their own bosses; boss trophies as keys; raids that change with progression |
| Enshrouded | Rescued craftspeople who open crafting stations at home |
| Thronefall / Kingdom Two Crowns | Daytime investment, protecting income and a readable defense rhythm |
| 7 Days to Die | A fixed Blood Moon cadence that players prepare for |
| Terraria | Blood Moon and eclipse as changes to the normal day/night rhythm |
| Slime Rancher | Physical produce, home trading and later automation of collection |
| DREDGE | Tempting excursions, preparation and danger after dark |
| Village 51 | Combining first-person shooting and base defense |
| Chop Chop Inc. | Regrowing trees and office-worker-to-survivor humor |
| Megabonk | The final-boss run: run-and-gun with level-ups during the run, manual aiming retained |
