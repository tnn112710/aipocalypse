# Ideas (draft, not canon)

Unvetted ideas. Nothing here is a commitment. When an idea is accepted, move it into `GDD.md` and delete it here; when rejected, move it to Rejected with a one-line reason.

Tags: MoSCoW priority — MH (must), SH (should), CH (could), WH (won't). `(???)` = unsure, needs adjusting/correcting/changing to fit the game. `[TBD]` = postponed, decide later. Untagged = not prioritized yet.

## Inbox

Raw and still-open ideas.

### Foundation

- Engine: Unity, asset-store based
- Team: solo programmer; art/audio/content bought from asset stores; placeholders first for prototype
- Genre: FPS, farm/base defense shooting, survival, rogue-lite, simulation, semi open world; mid-core, low-budget indie
- Style: stylized low poly
- Inspirations: Village 51, Chop Chop Inc., Escape from Duckov, How to Fish, Dave the Diver, Swamp Attack 1 & 2, Megabonk
- DNA: "How many things do you want to add?" — "Yes!" Fun, various activities, chilling, FPS
- Top 3 if only three could ship: FPS fighting; day/night/weather cycle; daytime activities -> night fight loop
- Always first-person view, including activities and minigames

### Economy [TBD]

- Shipping, selling, buying — postponed. Where to sell crops and spend coins while mostly alone
- Options raised so far: shipping bin, daily "hot item", vending terminal, drone delivery, wandering trader, arcade ticket counter, Lord's night market

### Length & scaling

- 30+ days for a normal run, but endless: scalable, loopable, incremental systems, no fixed end

### Combat & enemies

- Combat (MH): melee, guns
- Enemies (MH): predators, monsters, robots/terminators/AI, dinosaurs, zombies, dragons
- Use as many store asset packs as possible for variety; enemies don't need a consistent theme — the randomness itself is the hint that the world is an AI-generated sandbox
- Enemies may reference other games (cross-references, déjà vu)

### World

- Fixed map, not too big, with many places to explore
- Base in the center; surrounding areas: river, forest, meadow/grass fields, cave, etc.
- Mostly alone; non-human NPCs in some places (low priority)

### Activities / minigames / microgames (CH)

- Build up base/farm
- Wood, log chopping — agreed, see Promising > Wood & resources
- Light farming: plant, water, harvest, sell crops; animals (cows, chickens, ducks), collect eggs
- Fishing, gun fishing, duck shooting
- Lottery, classic minigames
- Funny minigames: whack-a-mole, duck shooting, space invaders, fish shooting, etc. — direction agreed, see Promising > Minigames

### Story & tone

- Comedy, fun, lots of funny lines and cross-references/déjà vu, cozy environment, not too serious; nights a bit scary
- MC works in the game industry, gets overwhelmed by AI, decides to resign and become a farmer somewhere peaceful
- The night before leaving, falls asleep at the desk, gets consumed by a flash of light from the computer, wakes in a remote meadow — the peace/nature he dreamt of
- The place isn't normal; the guide explains the world and how to escape
- Later twist: the world is a sandbox created by an AI boss/lord (???)

### Objectives & ending

- Multiple playstyles (???): stay and enjoy (simulation) / get strong and escape / hybrid
- Stay-forever path (???): no ending defined yet

## Promising

Agreed direction. The high-level summary is in [CORE.md](CORE.md); the details stay here until they move into their own topic files.

### Stats — two groups

- Survival meters (change daily): HP, Hunger, Stamina, Sanity
- Growth (from XP → level points): Fitness (HP, stamina, carry — replaces "strength"), Gunsmanship (recoil, jams, reload), Luck
- Stamina is for sprinting and fighting
- Coins/currency; killing enemies gives coins and XP

### Day clock

- Day shown as hours on a clock; activities cost hours; walking is free; when hours run out, night comes
- Upgrades make activities cheaper/faster (more done per day)
- Night ends when you sleep or finish fighting

### Guns & jamming

- Office-worker start: more recoil, frequent jams
- Jams are temporary: player repairs the gun (active unjam); luck, sanity and gun skill speed up or make it instant

### Light is safety

- Inside firelight/lamps/torches = safe; in the dark, sanity drains fast
- Going a bit far into the dark for loot is allowed but risky (greed is the player's choice)
- Danger rings with escalating warnings:
  1. Edge of the light: sanity drains, whispers, eyes blinking in the dark
  2. Further out: drone warns ("uhh, boss?"), music cuts, screen distorts, torch flickers
  3. Too far: instant death, jump scare
- Loot glowing in the dark = bait for greedy players
- Torch/lantern burns fuel to push the safe radius out temporarily

### Daytime danger

- Some daytime threats exist: snakes, predators

### Weather

- Weather has gameplay effects (fishing, vision, etc.), starting with ~3 types with 1–2 clear effects each
- Combines with day/night (e.g. a rainy night is scarier)

### Enemies

- AI built per behaviour archetype (rusher, tank, ranged, flyer, swarm, sneaker); each enemy = archetype + skin from any pack
- Shared glitch spawn effect/outline to tie mixed packs together

### Night fights & death

- Fights happen at base; small nights opt-in; big fight every 5–10 nights, (almost) unavoidable
- Starting a small night: light the beacon at the base = fight; don't light it = sleep
- Signals: symbols on the ground in front of the house hint at what's coming; each shows a % chance to happen
- Dying on a small night: wake at base, lose that night's loot + some coins, next day starts with fewer hours and lower sanity; XP, buildings, upgrades kept
- Losing a big fight: back to the morning of the last big fight survived (autosave); lose some resources; prepare again
- Dogs fight at a low rate; upgradable
- Big fights get harder and harder over time

### Rest vs fight balance

- The first night of either is free; only streaks count
- Consecutive sleep nights → raid chance on the farm grows; resets when you fight
- Consecutive fight nights → **Tired**: slower stamina regen, faster sanity drain (reuses existing stats, no new meter); resets when you sleep
- Tiredness has a limit; coffee/energy drinks offset it (office-worker joke)
- Raids can be prevented without combat: fences, scarecrows, dog on guard, lamps around fields (wood), so chill players defend by preparing
- Raid losses are capped: a share of crops/eggs/stored food; never buildings or animals
- Warnings before a raid: the signals show the raid chance; the drone may comment ("Something's been sniffing around the barn…")
- (CH) During a sleep-night raid, the dog barks and you may get up to defend, giving up that night's recovery
- References: This War of Mine (guard vs sleep, tiredness, shelter raids), Kingdom Two Crowns (base attacked at night)

### Big fight offering — the Lord's Wish List

- Before each big fight the AI Lord asks for specific items (e.g. a legendary catfish, 10 golden eggs, a perfect pumpkin) — ties daytime activities to nights
- Nothing offered: full fight happens
- Part of the list: fight delayed 2 nights and comes back harder
- Full list: luck roll. Win = fight skipped with partial rewards (50–70%) and Patience drops to 0; lose = counts as a delay
- Patience meter: can't skip again until it refills (no continuous skipping)
- Drone shows the odds before offering ("The Lord seems about 60% pleased")
- Fighting always pays best

### Luck as karma

- Up for good actions (feeding animals, releasing rare fish, petting the cat), down for bad ones (breaking things, skipping offerings)
- Affects loot, crits, fishing, lottery, jam rate

### Companions

- Cats: raise sanity (petting, sleeping nearby), boost luck, hiss to warn of sneakers at night. No combat, no pest-catching
- Guide: cute drone — cheerful tech-support voice, doubles as in-world UI (scans, tips); turns out to be the AI Lord's eyes

### Wood & resources

- Chop trees for wood (inspired by Chop Chop Inc.); stumps regrow after some days
- Rule: resources are spent directly, like money — no recipes, no workbench, no intermediate items
- Simple spend/repair system, extendable later as the game grows:
  - Light the beacon/campfire: costs wood
  - Build/repair fences and barricades: costs wood
  - Sell wood for money (where/how is part of Economy [TBD])
- Felling gives wood directly; no log-splitting step for now

### Minigames — built into day actions

- Minigames happen inside normal actions in the world, not in arcade cabinets
- Whack-a-mole: happens occasionally while checking crops
- Fish shooting: hang bait over the water, fish jump out, shoot them
- Sky Invaders: night fight mission — both a small-night mission and a big-fight version with a mothership boss
  - Formation moves side to side, steps down, speeds up as fewer remain; barn roofs/fences act as shields; landed invaders abduct animals

### Grand trial (escape) — deferred until after main gameplay

- Megabonk-inspired run-and-gun, 2–3 levels, rogue-like card selection to level up during the run
- Not designing further for now

### Endings

- Beat the grand trial: wake at your desk; the sandbox is the game you were making, but you used AI too much and it took control. Choose: delete it / fix it / leave it / go back into the game
- Secret: win the lottery at an extremely rare chance → instantly win the game

