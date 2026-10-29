# Core Gameplay

Current design direction, revised 2026-10-30: a first-person shooter with farming, exploration and home defense. Farm and trading details live in [FARM_AND_ECONOMY.md](FARM_AND_ECONOMY.md). Open rules are marked explicitly; older proposals and superseded designs are in [IDEAS.md](IDEAS.md).

## Pitch

A game dev burned out by AI wakes up in the peaceful meadow he dreamed of — which turns out to be a broken sandbox made by an AI. Build a livelihood with animals, crops and fishing, repair the broken bridge to explore beyond the river, and bring resources home to prepare for threats. Animals produce faster when roaming the meadow, but must be gathered before goblins can steal them during raids. Defend the farm against thieves and attackers arriving from the outer zone and through portals, or enjoy fishing and other activities on quiet nights. Cute service robots handle trading and, potentially later, upgrades and storage.

## Pillars

1. **Build something worth protecting** — animals, crops and the home give exploration and defense a shared purpose.
2. **Choose how to spend the day** — work safely around the meadow or venture across the repaired bridge for resources and encounters.
3. **Prepare before dark** — gather animals, bring supplies home and ready the defenses. Daytime choices shape the night.
4. **Every bullet counts** — shooting is the star, but ammunition is limited and costs resources. Melee remains a fallback.
5. **From office worker to survivor** — improvements make the player and home more capable. The AI-made world supplies humor, strange combinations and familiar game references.

**Core experiences:** first-person shooting and melee, exploring and bringing loot home, tending a small farm, gathering physical animal products, fishing, trading and defending the home.

## Premise & tone

- The MC works in games, gets overwhelmed by AI, and quits to become a farmer. The night before leaving, he falls asleep at his desk, a flash from the computer takes him, and he wakes in a remote meadow.
- A cute guide **drone** welcomes him and explains the world — it is secretly the eyes of the **AI Lord** who built this sandbox.
- Cozy and funny around home; risky beyond the river; frightening after dark. Eclipse events interrupt the ordinary safe-day rhythm.
- Cute service robots provide a direction for home facilities. Their exact models, names and presentation are not selected.

## Core loop

~~~text
MORNING: collect, trade, prepare; choose animals' freedom and the dog's assignment
   -> DAY: farm / fish / gather locally, or explore beyond the repaired bridge
   -> DUSK: gather animals and ready defenses when an attack is expected
   -> NIGHT: defend against thieves and attackers, or enjoy a quiet night
   -> RECOVERY: secure the farm, use rewards and supplies, rest
   -> NEXT DAY: invest in the player, animals, home and future expeditions
~~~

| Scale | What the player does |
| --- | --- |
| Seconds | Shoot, swing, collect produce, harvest, fish, react to theft and threats |
| One day | Choose local work or exploration, manage roaming animals, and prepare for a raid or use a quiet night for activities such as fishing |
| Long term | Improve the home and equipment, train the dog, explore farther and face stronger events |

Animal products are available as physical drops during production, not only through a morning collection routine.

### Loop rules

1. **The meadow is safe during ordinary days** — animals can roam. Outer-zone enemies do not cross the bridge to actively attack the player by day unless provoked.
2. **Repair the bridge to explore** — the broken bridge initially prevents access across the river. Once repaired, it connects the meadow to the dangerous outer zone.
3. **Freedom increases production** — animals within the farm still produce; animals roaming freely in the meadow produce faster.
4. **Collect what animals drop** — products appear near animals as physical pickups for the player to collect.
5. **Bring animals home before night attacks** — exposed animals can be stolen by goblins during raids. The protection provided by fences and shelters, and animal routines on quiet nights, need detailed rules.
6. **Time passes in real time** — also at home. Local work, exploration, preparation and travel share the day's time.
7. **The farm supports the next outing and defense** — products, crops and fish can become food or money; sales pay immediately, so today's production can fund tonight's supplies.
8. **Some nights bring raids; others are quiet** — rest nights allow activities such as night fishing without a home raid. On raid nights, attackers can come from the outer zone and appear through portals. Ordinary raids stop at midnight: new attacks cease and surviving raiders retreat. Lighting a fire is no longer the condition that invites an attack.
9. **A major event occurs every 10 days** — Blood Moons are full-night miniboss events on days 10 and 20 of each 30-day cycle. An eclipse replaces the Blood Moon on every 30th day and is a full-day boss event. Encounter details and difficulty escalation remain open.
10. **Death does not end the run** — permanent growth and the safe home stash remain part of the design. Exact losses and recovery rules need review for this world structure.

## Day, dusk and exploration

- **At home:** collect products where animals drop them, tend crops and plants inside the fence, fish, chop, cook, trade, build and repair.
- **Animals:** choose whether to keep them within the farm for slower production or let them roam the safe meadow for faster production.
- **Outer zone:** after repairing the bridge, cross the river to explore for resources and loot while watching for wandering enemies and dangers.
- **Daytime aggression:** enemies stay in the outer zone rather than actively raiding across the bridge, unless provoked. Pursuit limits and what counts as provocation are open.
- **Dusk:** return with supplies, gather roaming animals and prepare when a night attack is expected. Quiet nights allow other activities, including night fishing.
- **Dog assignment:** take the dog exploring for help, or leave it in the inner zone. Later training can help automate animal gathering; exact abilities and commands are open.
- **Minigames:** the existing direction keeps them inside activities, such as shooting jumping fish at bait. Further minigame systems remain optional ideas.
- **Player biome portals:** removed. Exploration beyond the repaired bridge leads into the outer-zone map; its structure, extent and detailed design are deferred. Enemy attack portals remain a separate feature.

## Night & special events

| Situation | Current direction | Still open |
| --- | --- | --- |
| Quiet night | No home raid; rest or pursue activities such as night fishing | Distribution of quiet nights, sleep, animal routines and lighting needs |
| Ordinary raid night | Defend until midnight (12 a.m.); new attacks stop and surviving raiders retreat | Raid frequency, sleep rules and how retreating thieves handle stolen animals |
| Blood Moon | Full-night miniboss event on every 10th day except eclipse days | Warnings, phases, rewards and how the night resolves |
| Eclipse | Full-day boss event on every 30th day, replacing that day's Blood Moon | Warnings, stages, rewards and recovery afterward |
| Outer zone after dark | Extremely dangerous enemies, darkness draining sanity and attacks by mystical entities | Light protection, entity behavior, dog benefits and escape options |

### Major-event calendar

| Day | Event | Duration |
| --- | --- | --- |
| 10 | Blood Moon | Night of day 10 through the following dawn |
| 20 | Blood Moon | Night of day 20 through the following dawn |
| 30 | Eclipse | Daytime boss event |
| 40 | Blood Moon | Night of day 40 through the following dawn |
| 50 | Blood Moon | Night of day 50 through the following dawn |
| 60 | Eclipse | Daytime boss event |

The pattern repeats every 30 days. An eclipse replaces the Blood Moon in that major-event slot; the two events do not stack on the same date. The other dates contain a mix of ordinary raids and quiet nights; their exact distribution remains undecided. Whether an eclipse day is followed by a quiet night, and the preparation/recovery periods around major events, are also open.

### Raid and safety rules

- Enemies include goblins, robots and zombies. Some steal; others attack the player, with broken fences allowing threats into the defended area.
- Night threats can approach from the outer zone and through portals. Portal locations, warnings and the relationship between those routes are not fixed.
- The midnight retreat ends an ordinary farm raid. Outer-zone night dangers remain; Blood Moons retain their full-night duration.
- Fences and barricades are defenses. Failure can leave the home damaged and require repairs; the stash remains safe.
- Difficulty increases gradually. Whether it scales with days, progression or major victories is undecided.
- Staying outside at night is risky rather than an automatic extraction failure. What happens to a raid at home while the player is away still needs a rule.
- Light protects against the Dark, not ordinary enemies. Specific sanity effects and mystical attack thresholds need review.
- The old fire-out / small-fire / bonfire selection no longer defines the night. Campfires and torches can still provide light; fuel and any other effects are not settled.
- Warnings about night threats were part of the previous draft. How the player forecasts ordinary raids, Blood Moons and eclipses remains to be designed.

## Farm, food & services

- Crops and plants grow inside the fence. Animal products, harvests and fish support cooking, eating where applicable, or selling.
- Animals drop produce where they are; freely roaming animals produce faster. Feeding, rates, persistence of uncollected goods and species-specific outputs need detail.
- A **trading station with a cute robot** provides the home service for selling and purchasing.
- An **upgrade service** and **storage station**, inspired by Escape from Duckov's home facilities, are possible later additions. Their functions and unlocks are not fixed.
- Sales pay immediately through the trading robot. Purchase delivery timing, stock, prices, recipes and economic balance are open.

See [FARM_AND_ECONOMY.md](FARM_AND_ECONOMY.md) for confirmed decisions, presentation proposals and remaining economy questions.

## Player, combat & progression

The previous draft's player and combat direction is retained for further design:

- **Survival meters:** HP, Hunger, Stamina and Sanity. Stamina supports sprinting and fighting.
- **Permanent growth:** XP into Fitness, Gunsmanship and Luck. Improvements make activities faster and the player more capable.
- **Starts weak:** more recoil, jams and slower work. A timed input helps clear jams. Exact starting severity needs balancing.
- **Limited ammunition:** buy or craft more; melee and tools such as an axe are fallbacks.
- **Enemies:** behavior archetypes with different appearances; their objectives can include stealing and attacking.
- **Perk cards:** the earlier draft offered a pick of 1 of 3 temporary cards, lasting 3–5 days, with at most 1 reroll. How cards are awarded under scheduled raids and boss events is now open.
- **Sanity:** darkness drains it; sleep, food and pets can restore it. Detailed effects, including the older HUD-removal and instant-death ideas, require review.
- **Luck:** the earlier draft connected it to loot, crits, fishing, the lottery and jam rate; exact rules are not designed.

## World & companions

- **Home:** a house on a ridge above the sea, fenced farm, meadow and river. Farming, fishing, chopping, services and defense share the inner zone.
- **Bridge:** initially broken; repairing it unlocks access to the outer zone. Repair costs and the introductory sequence are open.
- **Outer zone:** exploration, resources and wandering threats; substantially more dangerous at night. Its map design is deferred; the former player biome-portal system is removed.
- **Weather:** intended to affect activities and visibility. Effects on fire and fuel must be reviewed now that fire no longer selects raid difficulty.
- **Guide drone:** guide, tips and in-world information; comic tech-support personality; secretly connected to the AI Lord.
- **Dog:** can accompany exploration or remain in the inner zone; later gathering assistance is planned. The previous draft also included modest combat help and a safe backpack; their interaction with new duties needs review.
- **Cat:** the earlier direction provides sanity and luck benefits and warnings about sneaky enemies; details remain open.
- Always first person; mostly alone, with service robots and possible non-human NPCs.

## Resources & failure

- **Resources:** coins, wood, food, farm products, materials, ammunition and equipment. Trees regrow; their timing is not fixed.
- **Home stash:** safe from player death and lost defenses.
- **Death:** respawn at home and retain XP, buildings and upgrades. Damage to structures can require repair; retained ownership does not imply that structures remain undamaged.
- **Loot loss:** the previous draft proposed losing 50–70% of loot into a recoverable grave, with a second death losing the grave; equipped melee and the dog's backpack were protected. Exact carried-item selection, percentage and grave lifetime need review. Stored stash items are safe.
- **Animal theft:** goblins can steal exposed animals. Rescue, replacement, permanent loss and what happens when an animal is secured inside remain undecided.
- The old rule that sleeping deletes a grave in a rerolled biome is not established for the new outer zone.

## Endings — retained direction to revisit

- **Escape:** the earlier concept used relics to unlock a grand trial, then revealed that the sandbox was the game the MC was making. Wake at the desk and choose to delete it, fix it, leave it or return.
- **Secret:** the earlier concept included an extremely rare lottery win.
- The previous 30+ day target, relic locations, grand-trial entry and structure, boss progression and endless continuation need review together. The former biome-portal progression is removed. Blood Moons and eclipses now provide a direction for major encounters.

## Open decisions

| Area | Needed decision |
| --- | --- |
| World | Outer-zone map structure, extent and exploration progression; player biome portals are removed |
| Ordinary raids and quiet nights | Their distribution, sleeping, quiet-night animal routines, attacks while away and retreating thieves carrying stolen animals |
| Major events | Warnings, stages, rewards, difficulty escalation and preparation/recovery around the fixed 10-day/30-day calendar |
| Animal safety | Fence/shelter protection, theft, rescue and loss |
| Farm and trade | Production, pickup persistence, recipes, purchase delivery timing, prices and stock; sale payment is immediate |
| Services | Upgrade/storage functions, unlocks, robot presentation and station layout |
| Progression | Difficulty scaling, skill/gear balance, perk rewards, relics and endings |
| Failure | Carried-item loss, graves, structure damage and economic recovery |

## References

References inform specific aspects of the design; they do not commit the game to all of a reference's systems.

| Game | Reference purpose |
| --- | --- |
| Escape from Duckov | First-person adaptation of preparing at home, risking carried loot and returning; death without losing the whole game; possible home upgrade/storage facilities |
| Thronefall / Kingdom Two Crowns | Daytime investment, protecting income and a readable defense rhythm |
| Slime Rancher | Physical produce, home trading and later help with routine work |
| DREDGE | Tempting excursions, preparation and danger after dark |
| Terraria | Blood Moon and eclipse as changes to the normal day/night rhythm |
| Village 51 | Combining first-person shooting and base defense |
| Chop Chop Inc. | Regrowing trees and office-worker-to-survivor humor |
| Megabonk | The deferred grand-trial concept with manual aiming retained |
