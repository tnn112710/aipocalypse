# Ideas (draft, not canon)

Unvetted ideas. Nothing here is a commitment. When an idea is accepted, move it into `GDD.md` and delete it here; when rejected, move it to Rejected with a one-line reason.

Tags: `(???)` = unsure, needs adjusting/correcting/changing to fit the game. `[TBD]` = postponed, decide later.

## Inbox

Raw and still-open ideas.

### Foundation

- Engine: Unity, asset-store based
- Team: solo programmer; art/audio/content bought from asset stores; placeholders first for prototype
- Genre: FPS, base defense shooting, looting, fishing, survival, rogue-lite, simulation, semi open world; mid-core, low-budget indie
- Style: stylized low poly
- Inspirations: Village 51, Chop Chop Inc., Escape from Duckov, Dome Keeper, Darkwood, How to Fish, Dave the Diver, Swamp Attack 1 & 2, Megabonk
- DNA: "How many things do you want to add?" — "Yes!" Fun, various activities, chilling, FPS
- Core experiences: gun shooting (melee when out of ammo), fishing (let the player have fun), item collecting (food, materials while exploring). An Escape from Duckov-style loop in first person, with Dome Keeper's go-out ↔ defend rhythm
- Always first-person view, including activities and minigames

### Economy & crafting [TBD]

- Shipping, selling, buying, crafting — postponed. Where to sell crops and spend coins while mostly alone
- Ammo must be buyable and craftable, so the ammo loop depends on this
- The "no recipes" rule (see Promising > Wood & resources) gets revisited together with crafting
- Options raised so far: shipping bin, daily "hot item", vending terminal, drone delivery, wandering trader, arcade ticket counter, Lord's night market, the AI Lord's vending machine with surge pricing / "Ammo+ subscription" (AI satire)

### Length & scaling

- Survive as many days as possible; surviving longer makes you stronger
- 30+ days for a normal run, but endless: scalable, loopable, incremental systems, no fixed end

### Combat & enemies

- Combat: melee, guns
- Enemies: predators, monsters, robots/terminators/AI, dinosaurs, zombies, dragons
- Use as many store asset packs as possible for variety; enemies don't need a consistent theme — the randomness itself is the hint that the world is an AI-generated sandbox
- Enemies may reference other games (cross-references, déjà vu)

### World

- Fixed map, not too big, with many places to explore
- Base in the center; surrounding areas: river, forest, meadow/grass fields, cave, etc.
- Mostly alone; non-human NPCs in some places (low priority)

### Activities / minigames / microgames

- Build up base/farm
- Wood, log chopping — agreed, see Promising > Wood & resources
- Light farming, kept tiny (shooting, fishing and collecting come first): small garden, a few animals (chickens, ducks), collect eggs
- Fishing, gun fishing, duck shooting
- Lottery, classic minigames
- Funny minigames: whack-a-mole, duck shooting, space invaders, fish shooting, etc. — direction agreed, see Promising > Minigames

### Night types & night map (???)

- Fight or hide is now the fire choice (see Promising > The fire); revisit what's left of this section
- Quiet nights (peaceful, rainy, no threat): sleep, or go out at night for fishing and special things that only appear at night
- Signals show what will come; some are hidden "?" signals
- Think like a rogue-like progression map: each night is a node/room where you decide what to do; some are "?" secret rooms

### Story & tone

- Comedy, fun, lots of funny lines and cross-references/déjà vu, cozy environment, not too serious; nights a bit scary
- MC works in the game industry, gets overwhelmed by AI, decides to resign and become a farmer somewhere peaceful
- The night before leaving, falls asleep at the desk, gets consumed by a flash of light from the computer, wakes in a remote meadow — the peace/nature he dreamt of
- The place isn't normal; the guide explains the world and how to escape
- Later twist: the world is a sandbox created by an AI boss/lord (???)

### Objectives & ending

- Stay-forever ending (???): no ending defined yet

### Review suggestions (not agreed yet)

Raised while reworking the core loop; not reviewed yet.

- Distance = risk = reward: near base is safe with common loot; far out has rare loot and real enemies. Mirrors the light rings at night
- Backpack limit: a full backpack slows you (Fitness raises carry)
  - Ref: Dome Keeper — carrying ore slows you, so every trip home is a decision
- Melee kills drop extra scrap/ammo; kick enemies into the campfire
  - Ref: Doom Eternal — scarce ammo, and chainsaw kills drop ammo, pulling you into fights instead of away
- Low-ammo safety net: the less ammo you have, the more ammo/gunpowder drops
  - Ref: Resident Evil 4 — drops check your inventory; invisible to the player, prevents death spirals
- 3 ammo types max (pistol, shotgun, rifle) to keep scope down
- Crafted ammo is cheap but jams more; bought ammo is reliable (ties into Guns & jamming)
- Bonfire torches also widen the light around base: more room to fight, but more enemies coming
- Lights-out comedy: enemies knock and sniff at the window, the MC whispers "we're not interested"; plugs into the sleep-sounds table
- Fishing depth: species by spot × time × weather; a fish log that's never lost; glitched fish (AI-made world) bite more at dusk or at low sanity
  - Ref: Dredge — aberrant fish are far more common at high panic at night; tempts you to stay out too long
- Container search with a short reveal and rarity colors
- Travel upgrades: bike, zipline, drone hauling
  - Ref: Dome Keeper — the lift and teleporter remove hauling later in a run
- Real-time starting values (???): day ~10 min, dusk warning ~2 min, night fight ~4–5 min; sleeping skips the night
  - Ref: Darkwood — 9.5 min day / 4.5 min night, ~3 min of red dusk plus a siren; Stardew Valley — ~14 min day

## Promising

Agreed direction. The high-level summary is in [CORE.md](CORE.md); the details stay here until they move into their own topic files.

### Stats — two groups

- Survival meters (change daily): HP, Hunger, Stamina, Sanity
- Growth (from XP → level points): Fitness (HP, stamina, carry — replaces "strength"), Gunsmanship (recoil, jams, reload), Luck
- Stamina is for sprinting and fighting
- Coins/currency; killing enemies gives coins and XP

### Real-time day

- Time passes gradually in real time (like Escape from Duckov), also at base; get home before dark
- Dusk gives a warning before dark
- Activities and walking take real time; upgrades make activities faster (more done per day)
- Night ends when you sleep or finish fighting
- Ref: Escape from Duckov — time keeps passing in the bunker; day runs 06:00–22:00

### Guns, ammo & jamming

- Office-worker start: more recoil, frequent jams
- Jams are temporary: player repairs the gun (active unjam); luck, sanity and gun skill speed up or make it instant
- Ammo is limited — no spraying anytime, anywhere. Save it, buy or craft more (Economy & crafting [TBD]); out of ammo → melee
- Ref: Escape from Duckov — vendors sell ammo, a workbench crafts gear, melee is the fallback

### Light is safety

- Inside firelight/lamps/torches = safe from the Dark (not from monsters — fire attracts them); in the dark, sanity drains fast
- Going a bit far into the dark for loot is allowed but risky (greed is the player's choice)
- Danger rings with escalating warnings:
  1. Edge of the light: sanity drains, whispers, eyes blinking in the dark
  2. Further out: drone warns ("uhh, boss?"), music cuts, screen distorts, torch flickers
  3. Too far: instant death, jump scare
- Loot glowing in the dark = bait for greedy players
- Torch/lantern burns fuel to push the safe radius out temporarily

### Daytime danger

- Some daytime threats exist out in the world: snakes, predators, enemies far from base
- The base is never attacked during the day: the challenge is out in the world, and the sunset is the timer
- Ref: Escape from Duckov — unannounced storms drew "please add storm warning" threads; threats the player can't react to feel unfair

### Weather

- Weather has gameplay effects (fishing, vision, etc.), starting with ~3 types with 1–2 clear effects each
- Combines with day/night (e.g. a rainy night is scarier)

### Enemies

- AI built per behaviour archetype (rusher, tank, ranged, flyer, swarm, sneaker); each enemy = archetype + skin from any pack
- Shared glitch spawn effect/outline to tie mixed packs together

### The fire — lights on or lights off

- At dusk the player sets the campfire; it decides the night:
  - Out: put out the fire, go to bed in the tent/house, skip the night ("pretend nobody's home")
  - Small fire: a small wave comes to the base; normal loot and XP
  - Bonfire: more wood, more torches lit → bigger wave, better loot
- Wood is the night-difficulty knob
- Ref: Darkwood — the hideout's generator light attracts enemies; keeping the light from the windows can prevent attacks
- Ref: 7 Days to Die — campfires and forges create "heat" that attracts screamers, who call hordes

### Night fights & death

- Fights happen at base; the fire decides whether a night is a fight
- Big fight every 10th night on a fixed, visible countdown; harder each time; the AI Lord relights the fire, so no hiding; can't be delayed or skipped
- Signals: symbols on the ground in front of the house hint at what's coming if the fire is lit; each shows a % chance to happen
- Death never ends the run; XP, buildings and upgrades are always kept (like Escape from Duckov)
- Any death (out in the world, small night, big fight): wake at base and lose 50–70% of loot — a share of everything, stored items included
- Part of the lost loot is hidden somewhere on the map and can be partly recovered (???: how — e.g. a vague drone hint, one chance, gone if you die again first)
- Upgrades reduce the loss
- Losing a big fight also rewinds the clock X days (start: 3 (???)); the boss comes back when the countdown ends — you still have to fight it
- Dogs fight at a low rate; upgradable

### Rest vs fight balance

- You can rest, but you can't hide forever; only skip streaks count (fighting is limited by ammo, so there's no fight-streak penalty)
- Skip ladder: 1st skip free → 2nd in a row: thieves take some stored food/items → 3rd and later: raiders damage fences and buildings. Fighting resets the streak
- Raids can be reduced without combat: fences, scarecrows, dog on guard, lamps around fields (wood)
- Warnings: the signals show the skip streak and raid chance; the drone may comment ("Something's been sniffing around the barn…")
- During a raid, the dog barks and you may get up to defend, giving up that night's sleep
- Ref: Minecraft — phantoms spawn after 3 nights without a bed; ours is inverted: too much sleep invites trouble
- References: This War of Mine (guard vs sleep, shelter raids), Kingdom Two Crowns (base attacked at night)

### Sleep nights — sounds from outside

- While sleeping, you hear what happens outside (sound only, with subtitles)

| Situation | Sounds |
| --- | --- |
| Quiet night | Crickets, wind, the cat purring, the dog snoring, the drone's quiet "zzz… standby mode" |
| A farm raid | Chickens squawking, a fence cracking, the dog barking at something, chewing in the field |
| Tonight's signal threat nearby | Distant roars (dinos), groans (zombies), 8-bit beeps (invaders), all matching the morning's symbols |
| Weather | Rain on the roof, thunder, wind shaking the shutters |
| Scary moments | Footsteps on the porch… scratching at the door… then it turns out to be the cat |
| Comedy | The MC talking in his sleep: "Per my last email…", "No, the deadline is Friday…" |

### Luck as karma

- Up for good actions (feeding animals, releasing rare fish, petting the cat), down for bad ones (breaking things)
- Affects loot, crits, fishing, lottery, jam rate

### Companions

- Cats: raise sanity (petting, sleeping nearby), boost luck, hiss to warn of sneakers at night. No combat, no pest-catching
- Guide: cute drone — cheerful tech-support voice, doubles as in-world UI (scans, tips); turns out to be the AI Lord's eyes

### Wood & resources

- Chop trees for wood (inspired by Chop Chop Inc.); stumps regrow after some days
- Rule (???): resources are spent directly, like money — no recipes, no workbench, no intermediate items. Revisit with Economy & crafting [TBD]: ammo crafting needs at least a simple conversion
- Simple spend/repair system, extendable later as the game grows:
  - Feed the night fire and torches: costs wood (a bonfire costs more)
  - Build/repair fences and barricades: costs wood
  - Sell wood for money (Economy & crafting [TBD])
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

## Extras — nice to have

Liked, but not part of the core loop. They add fun, comedy and "wow" on top of it; not in CORE.md. Build after the core loop works.

### Fishing up enemies

- Sometimes the bite isn't a fish: a zombie in a snorkel, a robot, a shark with legs → instant fight on the riverbank
- More likely at dusk, at night, or in glitched water; reuses enemy prefabs
- Ref: Terraria — during a Blood Moon, 1 in 6 bites is an enemy (Zombie Merman, Wandering Eye Fish)

### Message-in-a-bottle maps

- Fishing sometimes pulls up a bottle with a treasure map, badly drawn by the AI ("map not to scale")
- Leads far from base to dig up loot: fishing → exploring → collecting

### Daily AI patch notes

- Each morning the AI Lord pushes a "patch" with 1–2 random rules for the day, shown by the drone / on the signals board
- e.g. "v0.13.2 — Gravity −30%. Chickens are now explosive. Fish prices +50%. Fixed a bug where the player was happy."
- Cheap variety: each rule toggles or tweaks an existing system
- Ref: Deep Rock Galactic — missions roll Warnings (harder) and Anomalies (Low Gravity, Gold Rush, Golden Bugs)

### AI-prompted bosses

- Over the 10 days before a big fight, the signals board shows the prompt the Lord is "typing", one piece at a time: "a chicken… but huge… with a minigun… photorealistic, 8k"
- Fight night: a giant low-poly chicken with a minigun ("photorealistic" never works)
- Boss = body + scale + weapon + archetype from the asset packs → endless boss variety
- Ref: Scribblenauts — type a noun + adjectives and it spawns

### Screensaver sleep

- Skipping a night shows a retro screensaver (3D pipes, flying toasters, bouncing DVD logo) over the sleep sounds
- If the DVD logo hits the corner exactly: +luck the next day
- Ref: The Office (US) "Launch Party" cold open — the whole office cheers when the DVD logo hits the corner

### Office-joke perks (???)

- Level-up perks named after office life, each with a real effect. List to polish later:
  - Ctrl+Z: once per night, undo the last hit taken
  - Reply All: shotgun fires 2× the pellets
  - Out of Office: one skip per cycle doesn't count toward the skip streak
  - Overtime: dusk lasts 1 minute longer
  - Copy-Paste: once a day, duplicate a caught fish
  - Per My Last Email: melee hits knock enemies far back

## Rejected

- Lord's Wish List (offer items to delay or skip big fights) — big fights are fixed and can't be delayed or skipped
- Attacks on the base during the day — no agency while away, rewards staying home, blurs the skip → raid rule
- Tired (fight-streak penalty) — ammo cost already limits fighting every night
- Fully peaceful playstyle ("stay and enjoy") — players must balance; you can rest but not hide forever
- Field campfire when caught outside at dusk — not wanted
- "Days without incident" streak sign with a growing bonus — not wanted
- Run-ending death (Dome Keeper / Don't Starve style) — death costs loot, never the run, like Escape from Duckov
