# Ideas — proposals and history

Current design lives in [CORE.md](CORE.md) and [FARM_AND_ECONOMY.md](FARM_AND_ECONOMY.md). Suggestions here are not permission to implement them. Confirm an idea with the user before moving it into the current design.

Tags: `(???)` = unsure; `[TBD]` = needs a decision. The historical notebook preserves earlier discussions and approvals in their original context; it is not a second source of current rules.

## Current review proposals — not confirmed

### Farm and economy

- Give animals, crops, fishing and exploration distinct economic roles: recurring production, planned harvests, active income and rare discoveries.
- Keep useful progress after reasonable ammunition, supply and repair costs; provide an economic recovery path after a bad night.
- Keep raw products worthwhile to sell; avoid making cooking every item compulsory for income.
- Consider stable base prices and optional product orders before adding fluctuating markets.
- Keep ingredients, products for sale and stored materials easy to distinguish.
- Make the first bridge repair achievable with resources obtainable before crossing.
- Give every product a clear job: it feeds you, heals you or pays for bullets. E.g. eggs and milk → meals, wool → bandages, pig truffles → coins; fish and crops → food or coins.
- Pig: it only produces while roaming, digging up truffles (or buried goods) in the meadow, which makes it the clearest example of "freedom increases production".
  - Ref: Stardew Valley — a pig only finds truffles when it has been fed and let outside, and leaves them on the ground
- Launch scope: 3 species (chicken, sheep, pig), 3–4 crops, about 6 recipes.
- Purchase delivery: instant for consumables (ammunition, food, healing), since sales pay immediately so the player can buy before a raid; a delay would add frustration without a decision. Larger items (animals, structures) could arrive the next morning.
- Daily box extras: a 4th box, or peeking inside one box, as further upgrades.
- Starting species, pig output, production rates, the drop cap, recipes, prices and stock sizes remain open in the economy topic file.

### Robot services and presentation

- Cute service robots are the chosen direction. A trader and mechanic could use different tools and expressions; the number of robots and exact models remain undecided.
- Goods could pass through an intake tray/scanner; purchases could arrive in a cargo compartment or locker.
- A shelter with farm crates and work surfaces could grow into a small service area as storage and upgrade functions are added.
- Brief reactions, occasional customer-service jokes and visible station improvements could give services personality.
- Trading-station names, the wooden-shelter concept and a particular robot silhouette are suggestions, not selected assets or approved pack imports.

### Raids, pacing and recovery

- Give major events advance warnings, distinct stages and recovery time. Avoid repeated major events with no chance to replenish supplies.
- Consider progression milestones for major difficulty increases rather than relying only on days passed.
- Make theft visible and interruptible: a thief carrying an animal is slow and can't fight; shoot it and it drops the animal.
- At dawn, the drone marks the rough area where a stolen animal was taken ("last seen near the Highlands"); the mark goes when the 3 days run out.
- For the enemy-system revision: three enemy jobs keep raids readable. Thieves (goblins) grab animals and produce and flee; breakers (robots) go for fences; hunters (zombies) go for the player. A raid type is then mostly a different mix.
- Review sanity effects so threats remain understandable. Exact effects, mystical entities and light protection still need design.
- Eclipse: the Dark comes to the meadow for the day, so campfires and torches matter at home too, while mystical entities roam; the giant boss arrives around midday. Decide with the enemy-system revision.
- (Tech) Goblins crossing the river only at night: an A* tag on the river that raiders may walk at night and not by day, so daytime enemies reach the meadow only over the bridge, while chasing the player.

### Forecast and progress bar (TBD)

- Set the moon cycle to 10 days (the clock uses 8 today) so the full moon always falls on a Blood Moon night; the sky becomes the countdown.
  - Ref: Kingdom Two Crowns — the moon's phases hint at the next Blood Moon
- Progress bar: a 10-segment bar under the HUD clock, one segment per day of the current block, ending in the Blood Moon or eclipse icon; tonight's segment shows raid or quiet once the morning forecast is known.
- Later, maybe: an uncertain forecast (e.g. "raid 70%" in the morning, confirmed at dusk) would make leaving animals out while exploring a real bet. Tonight's type is known for sure for now (user, 2026-10-31).
- Story reason: the AI Lord runs the world on sprints. The Blood Moon is the "sprint review", the eclipse the "quarterly release", and the morning forecast arrives as the AI Lord's patch notes (see Extras > Daily AI patch notes).

### Outer zone and progression (later)

- One 30-day cycle per outer region of the current map: Pine Forest (days 1–30) → Highlands (31–60) → Swamps (61–90). Each eclipse boss drops the gear that opens the next region (climbing gear, waders…); Blood Moon minibosses drop the materials for it. Difficulty then scales by cycle. Could also give the grand trial its entry (relics as eclipse-boss drops).
  - Ref: Valheim — each boss drops the item that unlocks the next biome's tools (Eikthyr's antlers → antler pickaxe → Black Forest ores)
  - Ref: Atomicrops — farm by day, defend the crops at night, and a boss on the farm at the end of each season
- A drowned player's grave washes up on the nearest bank.

### Scope

- Playtest slice before cycles 2–3: days 1–10 with 2 species, crops, the trading robot, the bridge repair, the Pine Forest, goblin-thief raids and the first Blood Moon.

## Decisions replaced by the current direction

| Earlier direction | Current direction |
| --- | --- |
| Farming is a small optional extra | Animals, crops/plants and fishing contribute to the core economy |
| Fire out / small fire / bonfire chooses whether and how much to fight | Night raids are part of the loop; Blood Moons and eclipses provide major-event directions |
| Night enemies never arrive through portals | Night threats can come from the outer zone and appear through portals |
| A completely peaceful progression path with no required defense; fights only when the player lights the fire | Fights are required on raid nights; no full-cozy mode. Ordinary days and quiet nights stay safe (user, 2026-10-31) |
| Daily player biome portals provide the main expeditions | Player biome portals are removed; repairing the bridge unlocks the outer-zone map, whose detailed design is deferred |
| Possible fixed morning product collection and next-day grazing bonuses | Animals physically drop products near themselves, with faster production while roaming in the meadow |
| Selling and buying have no chosen home service | A cute robot trading station is chosen; upgrade/storage services are possible later |
| Ordinary-night ending remains unresolved | At midnight, new attacks stop and surviving raiders retreat |
| Immediate versus next-morning sale payment remains unresolved | Sales pay immediately; purchase delivery timing remains open |
| Every ordinary night may require a defense | Quiet nights are part of the loop; the distribution of ordinary raids and quiet nights is undecided |
| Major-event dates remain undecided | A major event every 10 days: two Blood Moons, then an eclipse on every 30th day |
| Big fights every 10th night were rejected as forced | The Blood Moon is a required full-night event every 10 days |
| Night enemies arrive from the outer zone and portals | Goblins cross the river at night, some over the bridge; some enemies fly; some appear inside the meadow. The bridge is not a chokepoint |
| Enemies never cross into the meadow by day unless provoked | An enemy chasing the player can follow across the bridge |
| The broken bridge is the only way across | The bridge is the safe way; the river can't be swum until an upgrade, but a player who makes it across is not stopped |
| Ordinary raids versus quiet nights undistributed; forecasts undesigned | Tonight's type is known for sure in the morning; days 1–4 quiet, about 4 raids per 9-night block, never 3 in a row, quiet nights around major events, dealt from a shuffle bag |
| What happens at home while the player is away on a raid night | The raid resolves without the player: exposed animals are stolen and fences damaged; the morning shows the result |
| Stolen animals' fate undecided | They are held deep in the outer zone for 3 days, then lost forever; replacements can be bought |
| Pickup persistence undecided | Drops never despawn; each animal stops producing at a cap; a collector robot later |
| Animals managed only by the player at dusk | A bell calls them home; they walk slowly and hurry when the player interacts with them. At night they sleep and produce nothing |
| Death loses 50–70% of loot anywhere; the stash is safe | Deposits with the robots are safe; knocked out at home means waking the next morning, losing part of what you carry and some animals (more on major events) |
| Ammunition bought or crafted | Bought from the trading robot (every item has a daily stock limit) or looted from enemies; crafting open |
| XP skills, Luck, perk cards and the cat as core growth and companions | Upgrades and gear are the growth; skills, Luck, perk cards and the cat are extras for later (user, 2026-10-31) |
| Perk cards as the reward for night fights | Enemy loot and a better free daily box (pick one of three) |
| Sleep skips the night | Sleep restores sanity per hour slept: any time on quiet nights, after midnight on raid nights, never on Blood Moons |
| 30+ days to an ending through biome relics and the grand trial | Endless for now; the grand trial is on hold, not forgotten |

## Historical notebook — before 2026-10-30

The following is preserved context. Its earlier **Promising** and **Rejected** labels describe past decisions, not current approval. In particular, tiny-farm scope, extraction and player-portal rules, opt-in fights and bans on portal attacks must be read against the current documents above. The old fire choice and its leftovers were removed on 2026-10-31. Older reference claims and numerical suggestions have not been newly verified in this revision.

### Inbox

Raw and still-open ideas.

#### Foundation

- Engine: Unity, asset-store based
- Team: solo programmer; art/audio/content bought from asset stores; placeholders first for prototype
- Genre: extraction shooter FPS + base defense (user, 2026-10-10: "more like Escape from Duckov"); looting, fishing, survival, rogue-lite, simulation, semi open world; mid-core, low-budget indie
- Style: stylized low poly/toon
- Inspirations: Village 51, Chop Chop Inc., Escape from Duckov, Dome Keeper, Darkwood, How to Fish, Dave the Diver, Swamp Attack 1 & 2, Megabonk
- DNA: "How many things do you want to add?" — "Yes!" Fun, various activities, chilling, FPS
- Core experiences: gun shooting (melee when out of ammo), fishing (let the player have fun), item collecting (food, materials while exploring). An Escape from Duckov-style loop in first person, with Dome Keeper's go-out ↔ defend rhythm
- Always first-person view, including activities and minigames

#### Economy & crafting [TBD]

- Shipping, selling, buying, crafting — postponed. Where to sell crops and spend coins while mostly alone
- Ammo must be buyable and craftable, so the ammo loop depends on this
- The "no recipes" rule (see Promising > Wood & resources) gets revisited together with crafting
- Options raised so far: shipping bin, daily "hot item", vending terminal, drone delivery, wandering trader, arcade ticket counter, Lord's night market, the AI Lord's vending machine with surge pricing / "Ammo+ subscription" (AI satire)

#### Length & scaling

- Survive as many days as possible; surviving longer makes you stronger
- 30+ days for a normal run, but endless: scalable, loopable, incremental systems, no fixed end

#### Combat & enemies

- Combat: melee, guns
- Enemies: predators, monsters, robots/terminators/AI, dinosaurs, zombies, dragons, kaiju
- Use as many store asset packs as possible for variety; enemies don't need a consistent theme — the randomness itself is the hint that the world is an AI-generated sandbox
- Enemies may reference other games (cross-references, déjà vu)

#### World & environment (???)

- Update (user, 2026-10-10): the home map shrinks to the ridge, near meadow and river; beyond the river, portals lead to mission biomes (see Promising > Portal biomes beyond the river). The far-bank, woods, mine and deep-forest design below becomes biome #1
- Fixed map, not too big, with many places to explore; mostly alone, non-human NPCs in some places (low priority)
- A peaceful, cozy meadow with a big river, a pond, forest, mountains and caves in different directions
- Style: stylized/toon low poly. BK environment packs + MicroVerse presets for terrain and vegetation; Synty for props and characters; Cozy Stylized Weather 3; Stylized Water 3 (its River Modeler follows MicroVerse splines)
- Terrain size (???): 1000×1000 to 2000×2000
  - Suggested: ~1 km playable (2×2 tiles of 512 m) inside a ring of mountains you can see but can't walk on. Walking at ~5 m/s, center → edge takes ~100 s on 1 km but ~200 s on 2 km, so on 2 km one far trip eats most of a ~10 min day
  - Decided (user, 2026-10-19): 1000 × 1000 m terrain; the map itself is 700 × 700 m in the middle, with unwalkable boundary mountains in the outer ~150 m; southern mountains pushed north so the meadow is ~200 × 70 m (concept v13 in `docs/concepts/world-map.md`). Fenced base (cabin, yard, campfire, garden) ~30 × 30 m; base → broken bridge 80–100 m
- Base position (???): middle of the meadow (open 360°) or against a mountain (open 180°)
  - Suggested: near the center, backed by a cliff or a river bend. Every direction stays the same distance away, nights come from 1–2 readable sides (in first person you can't watch behind you), and there are fewer spawn lanes
  - Ref: Kingdom Two Crowns — Greed attack from both ends of the island; two readable fronts keep night defense manageable
- Layout suggestion: one direction = one zone + one landmark you can see from base (N mountains & caves, E big river, S meadow & pond, W forest). The farther from base, the higher the risk and the reward. The mountain ring is the map border, so no invisible walls. The river can block the far side until you build a bridge
  - Ref: Breath of the Wild — "triangle rule": big hills are landmarks, medium hills hide what's behind them; borrow it so players want to look past each hill
- User's layout (2026-10-04, "South Cliff"; options sketched in the Meadow Map Layouts artifact): base at the south end on a cliff (high ground: easier to overview and defend), inside a fenced garden with crops and animals. In front: a grass field/meadow with low hills and a few trees. Near the front and to the west, a river that starts from a waterfall with a pond; mountains in the west, forest in the east
  - Suggested: shape the map as a fan around the base, ~600–650 m deep and wider than tall (e.g. 1.2 × 0.65 km). With the base at the edge of a 1 × 1 km map, the far corners are ~900 m (3 min) away
  - Water (user, 2026-10-04): the waterfall is close to the house, the river runs along the west side, and a branch goes to a pond. Drawn as: waterfall from the mountains → pond at the west foot of the cliff (~200 m) → river flows north along the west → a branch crosses the meadow to a second pond (~360 m) → a creek runs east into the forest
  - Suggested: the main river is deep (a bridge is needed to reach the west mountains and caves); the branch and creek are shallow (wade or stepping stones). Night enemies cross the branch at fords, which gives readable lanes
  - Suggested: each water body has its own fish: the waterfall pond for common fish near home, the branch pond for calm-water fish, the river for river fish, the far north for rare fish
  - Suggested: hide each river junction under rocks or rapids. Stylized Water 3's River Modeler builds one mesh per spline, so branch seams need cover
  - Suggested: a low cliff (~5–8 m) with two ramps = two night lanes you watch from above
  - Open: what is north of the meadow? Where does the river leave the map (a gorge in the north mountains, or a lake)? Is the U-shaped line in the first sketch the valley rim (low hills) or the map border? Should the portal behind the waterfall stay so close to home (e.g. locked until later)?
- House on a ridge (user, 2026-10-04, side-view sketch): the ground rises gently from the meadow up to the house; behind the house the ridge drops into empty space. Replaces the "cliff in front + ramps" version
  - Suggested: the empty space is the edge of the AI-made world, a void with clouds far below. It fits the story, needs no mountain wall behind the house, and nothing can attack from behind
    - Ref: Valheim — water pours off the world's edge into a black void; a strong current drags you toward it and falling is instant death. Here, falling could be an "out of bounds" joke instead: the drone teleports you back ("Please stay inside the play area")
  - Suggested: let the pond near the house spill over the edge as a waterfall into the void. The river then flows south from the north mountains, along the west side, to the house
  - Suggested: distant floating chunks of other biomes (desert, snow, jungle) drift in the void behind the house, a hint at the portal biomes
  - Suggested: with no cliff in front, the slope is the defense: attackers walk uphill in the open, and a low stone wall with 2 gaps around the garden keeps 2 readable night lanes
  - Decided (user, 2026-10-04): the empty space is a sea far below; the house sits 5–10 m above the meadow; the waterfall drops off the edge into the sea
  - Wanted (user, 2026-10-04): a more complex map: rocks, hills, more forest past the river in front, denser the further out, more small river branches, a mine entrance (place open)
  - Suggested layout, in rings from the house:
    - Ridge (0–80 m): house, garden, cliff edge; the cliff behind drops ~60–100 m to the sea (user confirmed 2026-10-04)
    - Near meadow (80–220 m): open grass and flower fields, a few trees; the night arena. Readable sight lines
    - The main river crosses in front (~200 m), then bends south past the house into a pond at the edge and falls into the sea. 2 bridges + a few shallow fords = the night lanes (replaces the old cliff ramps)
    - Far bank (220–450 m): groves, rock outcrops, small hills; 2–3 shallow streams come down from the north and join the river
    - Woods (450–650 m): denser trees, stepped rock terraces, more streams, the mine
    - Deep forest & mountains (650 m+): densest trees, caves, the glitch, snow peaks
    - The farther, the denser, the darker and the richer: dense forest blocks sight lines (tension, ambushes) and also hides draw distance (cheaper rendering)
  - Mine entrance: option A chosen (user, 2026-10-04). Options were:
    - A (suggested): on the far bank, in a rocky ridge where the woods meet the mountains, ~450–550 m from home. Ore, metal and gunpowder → the ammo-crafting source. Always dark inside, so the light pillar applies by day too. A minecart track toward the river can later become a shortcut home
    - B: a sea cave in the cliff below the house, reached by a cliff path down to a small beach. Close to home and mysterious, but breaks "farther = riskier"
    - C: deep in the north mountains next to the caves; very far, end-game
    - Ref: Stardew Valley — the Mines are floors going down, with an elevator every 5 floors so progress isn't lost; a mine can grow downward instead of outward
  - Suggested: other biomes appear as islands on the sea horizon (desert, snow, volcano), a hint at the portal biomes (replaces the floating chunks)
  - Look references (user, 2026-10-04): Highlands, Grasslands, Toon Fantasy Nature, a painterly blue forest, rolling hills with a dirt path, a night forest with glowing flowers. Shared traits: painterly leaf-card foliage, dense grass with big color fields (lavender, yellow, white), chunky stylized rocks and stepped rock terraces, big soft clouds, blue atmospheric haze in the distance, bloom and light shafts, a blue-purple night with glowing flowers and fireflies. Much of this look is lighting and post-processing (color grade, fog, bloom), not just the models
- Art direction (suggested; 3D concept in the "South Cliff Meadow" artifact): one pack per job so the home map reads as one place. Plants & trees = Pure Nature 2: Meadows (BK); buildings, props & characters = Synty (POLYGON Meadow Forest: cabin, bridge, windmill, fences, warpgate for portals); cliffs & rocks = Toon Enchanted Meadow; water = Stylized Water 3; sky & weather = Cozy. One color grade over everything. Test a 100 × 100 m scene (Synty character + cabin, BK plants, one Toon cliff, day and night) before buying everything
  - Scale in the concept: cliff ~10 m, plateau ~300 m wide, fenced garden ~120 × 90 m (layout F's sketch drew the garden ~360 m wide at true scale, too big to defend at night)
- Map enhancement suggestions (not agreed yet):
  - Gates you open by getting stronger, not invisible walls: a giant fallen log blocks the forest path until the axe is upgraded; the deep river needs a bridge; a ladder or rope opens the mountain ledges; a flooded cave needs a later item. The map grows with the player (pillar 4)
    - Ref: Stardew Valley — a large log blocks the Secret Woods until you get the Steel Axe; a clear goal you can see from day one
    - Ref: Animal Crossing: New Horizons — the vaulting pole (rivers) and ladder (cliffs) open the rest of the island
    - Ref: Sons of the Forest — cave items (rebreather → rope gun → shovel) unlock the next caves; use 1–2 such items at most
  - Shortcuts home: ziplines from far lookouts down to the cliff top. These solve the long walk back from far zones before dark instead of shrinking the map
    - Ref: Dome Keeper — lifts/teleporters remove hauling later in a run
  - Tree types by zone: birch near home, oak in the east forest, pine in the mountains, glitched metal/electronic trees at glitch spots that drop special materials
    - Ref: Chop Chop Inc. — 8 tree types (incl. Metal, Electronic, Crystal) and new regions gated by tool upgrades; reviews say it runs out of things to do, so keep the map refreshing
  - Landmarks seen from the porch: the waterfall (W), a lookout tower in the north, one giant tree in the east forest, and one AI-glitch landmark (e.g. a half-rendered mountain or an oversized floating object). Every far zone has a silhouette you can point at
    - Ref: Grounded — the big oak is visible from the starting base and works as a compass
  - No minimap: the drone draws a paper map as you discover places, with comic notes ("here be snakes", "map not to scale"); a compass on the HUD
    - Ref: Firewatch — a map and compass, no waypoints; discovered paths get added to the map; every find feels earned
  - Something every 150–250 m (30–50 s of walking): a lookout, an old camp, a ruined cabin, a dock, beehives, berry bushes, a glitch patch, the MC's own office desk standing in the woods (easter egg)
  - Weather changes the map: rain floods the fords on the branch (the long way around), a dry spell lowers the pond and uncovers a sunken chest, fog hides landmarks
- The home map has one consistent look (one terrain/vegetation family, one color grade, Cozy lighting); mixed styles belong to the portal biomes and enemies
- Glitch spots: patches the AI "didn't finish" (Synty Prototype grid material, missing-texture magenta) that hide secrets (relics, easter eggs)

#### Grand trial portal (???)

- Updated (user, 2026-10-10): the portals beyond the river lead to daily mission biomes (see Promising > Portal biomes beyond the river). A separate grand trial portal unlocks once all the relics are found
- Ref: Megabonk — each stage has a ~10 min timer, a boss portal hidden somewhere on the map, and a final swarm at 0:00; beating the boss opens the portal to the next stage
- Open questions for the trial run:
  - Ammo vs hordes: suggested "trial ammo", plentiful inside and gone at the exit, so ammo stays scarce at home. Level-up cards that last only for the run reuse the perk card UI
  - Sun timer: suggested the run costs the rest of the day and you come back at dusk. Alternative: the run replaces the night fight
  - Death inside: lose only that run's rewards (???)

#### Activities

- Core activities (user, 2026-10-10): fishing and tree chopping. The tiny farm and minigames moved to Extras (the farm is core again since 2026-10-30)
- Build up base
- Wood, log chopping — agreed, see Promising > Wood & resources
- Fishing, gun fishing

#### Night types & night map (???)

- Quiet nights (peaceful, rainy, no threat): sleep, or go out at night for fishing and special things that only appear at night
- Signals show what will come; some are hidden "?" signals
- Think like a rogue-like progression map: each night is a node/room where you decide what to do; some are "?" secret rooms

#### Story & tone

- Comedy, fun, lots of funny lines and cross-references/déjà vu, cozy environment, not too serious; nights a bit scary
- MC works in the game industry, gets overwhelmed by AI, decides to resign and become a farmer somewhere peaceful
- The night before leaving, falls asleep at the desk, gets consumed by a flash of light from the computer, wakes in a remote meadow — the peace/nature he dreamt of
- The place isn't normal; the guide explains the world and how to escape
- Later twist: the world is a sandbox created by an AI boss/lord (???)

#### Objectives & ending

- Stay-forever ending (???): no ending defined yet

#### Review suggestions (not agreed yet)

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
- Fishing depth: species by spot × time × weather; a fish log that's never lost; glitched fish (AI-made world) bite more at dusk or at low sanity
  - Ref: Dredge — aberrant fish are far more common at high panic at night; tempts you to stay out too long
- Container search with a short reveal and rarity colors
- Travel upgrades: bike, zipline, drone hauling
  - Ref: Dome Keeper — the lift and teleporter remove hauling later in a run
- Real-time starting values (???): day ~10 min, dusk warning ~2 min, night fight ~4–5 min; sleeping skips the night
  - Ref: Darkwood — 9.5 min day / 4.5 min night, ~3 min of red dusk plus a siren; Stardew Valley — ~14 min day

### Promising

Direction recorded before this revision, with suggestions mixed in. Some decisions below have been replaced; use the current topic files for current rules.

#### Extraction loop — Duckov in first person, plus base defense

- Direction (user, 2026-10-10): more like Escape from Duckov — an extraction shooter FPS + base defense. Defend the base at night; explore and wander freely by day
- Home is the hub; raids happen in portal biomes beyond the river, and loot only counts once it's home (user, 2026-10-10; replaces "the home map is the raid"). Entry is one-way, so you extract through exits inside the biome (see Portal biomes beyond the river)
- Safe or risky is the player's choice (user): fish and chop around the house to play safe, or cross the river and take a portal
  - Suggested: home pays for food and wood (fish, chopping); guns, ammo, gear and materials come from the biomes, so playing safe never covers everything
- Loadout risk: you choose what to take from the stash; what you carry can be lost
- Death (user: Duckov's grave merged with the 50–70% rule): lose 50–70%; the lost share drops in a grave where you died, with one chance to recover it — gone if you die again first. The dog's backpack and equipped melee are always kept
  - (???) 50–70% of what you carried only, or stored items too? Suggested: carried only, so the stash is safe (matches Night stakes)
  - Ref: Escape from Duckov — on normal difficulty gear drops in a grave and is lost if you die again before reaching it; the dog's backpack survives death
- Night stakes (user): a lost night defense damages the base (fences, defenses, crops, stations), and repairs cost materials; the stash stays safe. Dying at night puts the grave at base, so the damage is the real cost
- Tension: every bullet you take on a trip is one you won't have for tonight's defense
- Ref: Road to Vostok — single-player survival FPS; shelters are the only save point and safe storage, and dying outside loses what you carried. The loop works in first person
- Ref: Outpost: Infinity Siege — FPS + tower defense + base building + extraction; OpenCritic 58, critics said the extraction gathering and rogue-like parts diluted it. Keep each layer simple
- Suggested (not agreed yet):
  - Economy [TBD] gets more urgent: extraction runs on loot value (selling, vendors, upgrades). Reopen it when you're ready
  - Gear becomes progression too (gun mods, armor tiers, bigger backpacks), next to XP and perk cards: three systems, one may need trimming
  - Ready-made from Review suggestions: backpack weight limit, container search with rarity colors, 3 ammo types, low-ammo safety net

#### Portal biomes beyond the river

- Idea (user, 2026-10-10): beyond the river, mission biomes change every day, each a different map with its own lootables, enemies and easter eggs
- Portals, not a seamless far side (user, 2026-10-10): you preview each biome from home, enter through a portal, then find a way out or search for relics. Like Duckov's other maps: you have to extract to get back to base
- Up to 3 portals open at a time, out of up to 6 biomes (user)
  - Shuffle-bag offers: every dawn 3 of the 6 open, and at least one still holds a relic you haven't found, so relic progress never stalls
  - Ref: Tetris — the 7-bag randomizer deals all 7 pieces before repeating, which prevents long droughts
- 3 fixed portal spots across the river, visible from the house
- Preview: the drone scans each portal (biome, weather, threat level, relic); the portal surface shows a pre-made 360° snapshot of its biome (a live camera into another scene costs much more)
  - Ref: Lethal Company — the terminal lists each moon with today's weather before you pick
  - Ref: Hades — each exit door shows its reward, so you choose a path by what you want
- One-way entry (user): the portal closes behind you; leave through one of 2–3 exits inside the biome
  - Suggested: some exits need a condition or cost (a zipline, a drone pickup beacon, a minecart out of the mine)
  - Ref: Escape from Duckov — exits (green smoke) include side tunnels and manholes; guides say learn two routes before you loot
- Hand-made pool (user): biomes repeat, so players learn each map; its contents roll on every visit
  - Biome #1 = the far-bank design in World & environment: woods, mine, deep forest (user)
  - Suggested: each biome has fixed spots for points of interest, containers, enemy spawns, exits and easter eggs, filled at random each visit, plus random weather. Start with 2 biomes and add more over time
  - Suggested: each biome is a theme the AI "generated": BK pack + MicroVerse preset + Cozy weather profile + enemy theme + loot table + easter eggs (dino jungle, zombie suburb, robot factory, 8-bit arcade…)
  - Suggested: rare biomes as jackpot days (the MC's office, a golden world, a half-rendered world)
  - Ref: Lethal Company — each moon has a fixed exterior and a random interior (Facility, Manor or Mineshaft), and the weather is random every day
  - Ref: Pacific Drive — the garage is the persistent base; junction roads stay the same, but landmarks, buildings, anomalies, loot and exits are placed again on each visit
  - Ref: Dave the Diver — the Blue Hole reshapes its layout and fish between dives
  - Ref: Animal Crossing: New Horizons — a Mystery Island ticket goes to a random island (~20 layouts, a few rare); once you leave, it's gone forever
  - Ref: Kate Compton's "10,000 bowls of oatmeal" — procedural output that's unique on paper but feels the same; why hand-made biomes beat generated ones here
- The home map shrinks to the ridge, near meadow and river (user): base, fishing, chopping, the night arena. Anything you build stays at home; gates and shortcuts inside a biome belong to it, and permanent upgrades (e.g. a better axe for the fallen log) open them on every visit
- Night enemies don't come out of the portals (user): night themes roll on their own
- The sun keeps running inside. Staying out at night (user): if you're still in a biome at night, the base isn't attacked, but the risk is on you
  - Suggested: the Dark still applies in there (carry a light); night enemies are tougher and loot is better, like Duckov's night raids. No base fight means no perk card
  - Portals only reroll when you sleep, so staying out could stretch one biome over several days; staying out costs your sleep, so sanity doesn't recover and you can't keep it up (user, 2026-10-10)
- Grand trial portal (user): unlocks once you find all the relics (user, 2026-10-10: relics, not missions)
  - Suggested: one relic per biome, found by searching (possibly over several visits). The locked portal sits behind the waterfall near home, a goal you can see from day one (answers the waterfall-portal question in World & environment)
- Suggested (tech): each biome is its own scene; the home map can unload while you're inside

#### Stats — two groups

- Survival meters (change daily): HP, Hunger, Stamina, Sanity
- Growth: XP skills moved to Extras (user, 2026-10-31); see Extras > Player skills
- Stamina is for sprinting and fighting
- Fighting tires you (user, 2026-10-17): each shot and swing costs stamina (per weapon, so spraying drains you); low stamina never blocks firing, it adds recoil and cuts melee damage in tiers (< 50%, < 25%, < 10%)
- Coins/currency; killing enemies gives coins and XP

#### Real-time day

- Time passes gradually in real time (like Escape from Duckov), also at base; get home before dark
- Dusk gives a warning before dark
- Activities and walking take real time; upgrades make activities faster (more done per day)
- Night ends when you sleep or finish fighting
- Ref: Escape from Duckov — time keeps passing in the bunker; day runs 06:00–22:00
- Ref: Dredge — the clock only moves while you sail or fish; resting at a dock skips to 06:00. Fallback if real time at base feels too stressful

#### Guns, ammo & jamming

- Office-worker start: more recoil, frequent jams
- Jams are temporary: player repairs the gun (active unjam); luck, sanity and gun skill speed up or make it instant
- Ammo is limited — no spraying anytime, anywhere. Save it, buy or craft more (Economy & crafting [TBD]); out of ammo → melee
- Ref: Escape from Duckov — vendors sell ammo, a workbench crafts gear, melee is the fallback

#### Light is safety

- Inside firelight/lamps/torches = safe from the Dark (not from monsters); in the dark, sanity drains fast
- Going a bit far into the dark for loot is allowed but risky (greed is the player's choice)
- Danger rings with escalating warnings:
  1. Edge of the light: sanity drains, whispers, eyes blinking in the dark
  2. Further out: drone warns ("uhh, boss?"), music cuts, screen distorts, torch flickers
  3. Too far: instant death, jump scare
- Loot glowing in the dark = bait for greedy players
- Torch/lantern burns fuel to push the safe radius out temporarily
- Ref: Dredge — panic builds at night and its effects hit your loot (crows steal fish, hallucinated monsters damage the boat); only sleep resets it. Sanity could cost loot, not just blur the screen

#### Daytime danger

- Some daytime threats exist out in the world: snakes, predators, enemies far from base
- The base is never attacked during the day: the challenge is out in the world, and the sunset is the timer
- Ref: Escape from Duckov — unannounced storms drew "please add storm warning" threads; threats the player can't react to feel unfair

#### Weather

- Weather has gameplay effects (fishing, vision, etc.), starting with ~3 types with 1–2 clear effects each
- Combines with day/night (e.g. a rainy night is scarier)

#### Enemies

- AI built per behaviour archetype (rusher, tank, ranged, flyer, swarm, sneaker); each enemy = archetype + skin from any pack
- Shared glitch spawn effect/outline to tie mixed packs together

#### Night fights & death

- Death never ends the run; XP, buildings and upgrades are always kept (like Escape from Duckov)
- Any death (in a biome or at night): wake at base and lose 50–70% of loot (???: carried only, or stored items too — see Extraction loop)
- The lost share drops in a grave where you died; one chance to recover it, gone if you die again first (user, 2026-10-10)
- Upgrades reduce the loss
- Dogs fight at a low rate; upgradable

#### Sleep nights — sounds from outside

- While sleeping, you hear what happens outside (sound only, with subtitles)

| Situation | Sounds |
| --- | --- |
| Quiet night | Crickets, wind, the cat purring, the dog snoring, the drone's quiet "zzz… standby mode" |
| Weather | Rain on the roof, thunder, wind shaking the shutters |
| Scary moments | Footsteps on the porch… scratching at the door… then it turns out to be the cat |
| Comedy | The MC talking in his sleep: "Per my last email…", "No, the deadline is Friday…" |

#### Companions

- Cat: moved to Extras (user, 2026-10-31)
- Guide: cute drone — cheerful tech-support voice, doubles as in-world UI (scans, tips); turns out to be the AI Lord's eyes

#### Wood & resources

- Chop trees for wood (inspired by Chop Chop Inc.); stumps regrow after some days
- Rule (???): resources are spent directly, like money — no recipes, no workbench, no intermediate items. Revisit with Economy & crafting [TBD]: ammo crafting needs at least a simple conversion
- Simple spend/repair system, extendable later as the game grows:
  - Fuel campfires and torches: costs wood
  - Build/repair fences and barricades: costs wood
  - Sell wood for money (Economy & crafting [TBD])
- Felling gives wood directly; no log-splitting step for now

#### Minigames — built into day actions

- Minigames happen inside normal actions in the world, not in arcade cabinets
- Fish shooting: hang bait over the water, fish jump out, shoot them
- Sky Invaders: a night raid type, with a mothership boss as a possible major-event version
  - Formation moves side to side, steps down, speeds up as fewer remain; barn roofs/fences act as shields; landed invaders abduct animals

#### Grand trial (escape) — on hold, not forgotten (user, 2026-10-31)

- Megabonk-inspired run-and-gun, 2–3 levels, rogue-like card selection to level up during the run
- Not designing further for now; the game is endless until it returns. Its entry needs a new design now that biome relics are gone (proposal: relics as eclipse-boss drops, see Outer zone and progression)

#### Endings

- Beat the grand trial: wake at your desk; the sandbox is the game you were making, but you used AI too much and it took control. Choose: delete it / fix it / leave it / go back into the game
- Secret: win the lottery at an extremely rare chance → instantly win the game

### Extras — nice to have

Liked, but not part of the core loop. They add fun, comedy and "wow" on top of it; not in CORE.md. Build after the core loop works.

#### Player skills (XP)

- Moved to Extras (user, 2026-10-31); upgrades and gear are the growth for now
- Growth from XP → level points: Fitness (HP, stamina, carry — replaces "strength"), Gunsmanship (recoil, jams, reload), Luck
- Fitness lowers the stamina cost of fighting; a Gunsmanship skill eases the low-stamina recoil penalty
  - Ref: Kingdom Come: Deliverance — leveling the bow skill cuts both sway and the stamina spent per shot; the same two levers as here
- Suggested: a farm skill ("Homestead": farm, fish, chop and repair speed) in place of Luck when skills come back

#### Perk cards

- Moved to Extras (user, 2026-10-31); enemy loot and the daily box are the fight rewards for now
- After a night fight, 3 cards appear; pick 1. Max 1 reroll
- Cards are temporary: each lasts 3–5 days, then expires
- Suggested (not agreed yet):
  - No permanent cards; gear and upgrades are the permanent growth
  - Duration by rarity (common 3 days, rare 5 days); cards expire at dawn; each card shows days left
  - Or cards only from major events, lasting until the next one (exactly 10 days)
  - Picking a card you already have refreshes its timer (or levels it up)
  - Cards survive death (death already costs loot)
  - Pool mixes combat, activity (fishing/chopping speed) and base/companion perks; Office-joke perks are the content
  - The grand trial reuses the same card system
  - Open (???): is the reroll per pick or per few days? Can Luck grant an extra reroll?
- Ref: Cult of the Lamb — tarot cards last only until the crusade ends; each run feels different without permanent power creep
- Ref: The Last Spell — after nights, level-ups offer random stat choices with rerolls; perk points capped at 10
- Ref: Thronefall — max 5 perks active at once; supports keeping the active card count small

#### Luck

- Moved to Extras with the skills (user, 2026-10-31)
- A growth stat; earlier also proposed as karma [TBD]: up for good actions (feeding animals, releasing rare fish, petting the cat), down for bad ones (breaking things)
- Affects loot, crits, fishing, lottery, jam rate

#### Cat

- Moved to Extras (user, 2026-10-31)
- Raises sanity (petting, sleeping nearby), boosts luck, hisses to warn of sneakers at night. No combat, no pest-catching

#### Fishing up enemies

- Sometimes the bite isn't a fish: a zombie in a snorkel, a robot, a shark with legs → instant fight on the riverbank
- More likely at dusk, at night, or in glitched water; reuses enemy prefabs
- Ref: Terraria — during a Blood Moon, 1 in 6 bites is an enemy (Zombie Merman, Wandering Eye Fish)

#### Message-in-a-bottle maps

- Fishing sometimes pulls up a bottle with a treasure map, badly drawn by the AI ("map not to scale")
- Leads far from base to dig up loot: fishing → exploring → collecting

#### Daily AI patch notes

- Each morning the AI Lord pushes a "patch" with 1–2 random rules for the day, shown by the drone / on the signals board
- e.g. "v0.13.2 — Gravity −30%. Chickens are now explosive. Fish prices +50%. Fixed a bug where the player was happy."
- Cheap variety: each rule toggles or tweaks an existing system
- Ref: Deep Rock Galactic — missions roll Warnings (harder) and Anomalies (Low Gravity, Gold Rush, Golden Bugs)

#### AI-prompted bosses

- Over the days before a Blood Moon or eclipse, the signals board shows the prompt the Lord is "typing", one piece at a time: "a chicken… but huge… with a minigun… photorealistic, 8k"
- Fight night: a giant low-poly chicken with a minigun ("photorealistic" never works)
- Boss = body + scale + weapon + archetype from the asset packs → endless boss variety
- Ref: Scribblenauts — type a noun + adjectives and it spawns

#### Screensaver sleep

- Skipping a night shows a retro screensaver (3D pipes, flying toasters, bouncing DVD logo) over the sleep sounds
- If the DVD logo hits the corner exactly: +luck the next day
- Ref: The Office (US) "Launch Party" cold open — the whole office cheers when the DVD logo hits the corner

#### Office-joke perks (???)

- Level-up perks named after office life, each with a real effect. List to polish later:
  - Ctrl+Z: once per night, undo the last hit taken
  - Reply All: shotgun fires 2× the pellets
  - Overtime: dusk lasts 1 minute longer
  - Copy-Paste: once a day, duplicate a caught fish
  - Per My Last Email: melee hits knock enemies far back

#### Minigame system (???)

- Moved out of the core loop (user, 2026-10-10), together with duck shooting, the lottery and other classic minigames

Many short "one more go" minigames, each a simpler take on a famous game. Extends Promising > Minigames.

- Every minigame = **template + skin + host + reward**, like enemy = archetype + skin. Code a template once; new minigames are mostly data and art
- Templates:
  1. Shooting gallery: targets move on paths; aim and shoot with the real gun
  2. Timing bar: stop or hold inside a zone
  3. Reveal grid: scratch, dig or flip hidden cells
  4. Cards: draw, pick, score combos (reuses the perk card UI)
  5. Drop & merge: physics drop; matching items merge
- Hosts (???: loosens "never in arcade boxes"):
  - Interruption: pops up during an activity (whack-a-mole while checking crops)
  - Activity: the activity itself is the minigame (fishing, cooking)
  - Device: in-world objects with a story reason — the MC's old work laptop, the AI Lord's vending machine, the drone's screen. Still no arcade cabinets
- When to play device games (???): by day at base (costs daylight) / quiet nights before sleep (costs sleep, so sanity) / quiet rainy nights
- Guardrails:
  - Sessions 30 s – 3 min; the sun keeps moving, so playing costs daylight
  - Rewards feed core resources (coins, food, luck, gunpowder, XP) but stay small next to going out; device games have daily limits ("machine restocks tomorrow")
  - Shooting minigames use real ammo and pay off only with good aim: trains the core skill, keeps "every bullet counts"
  - One hook per minigame: high score, collection log, upgrades, or a rare jackpot
  - No real playing cards or casino art (age rating); parody names and our own art
- Ref: Dave the Diver — many minigames, each feeding the dive ↔ sushi loop
- Ref: Stardew Valley — Journey of the Prairie King pulls players away from the farm for hours; keep minigame rewards small so they stay side dishes
- Ref: Balatro — rated PEGI 18 for "gambling imagery" (it teaches poker hands), later appealed down to 12

| Minigame | Based on | Template | Host | Hook / reward |
| --- | --- | --- | --- | --- |
| Ducks Must Be Paid | Bills Must Be Paid: smash bouncing piggy banks until stamina runs out; bills with deadlines; pay one → pick a perk | Shooting gallery | Pond | Coin-carrying ducks; the AI Lord's "bills" arrive with deadlines; pay one → pick a mini-perk for the session |
| Cast n Chill fishing | Cast n Chill: active or idle fishing, logbook | Timing bar | Fishing | Chill active fishing; idle rods set at the dock before leaving, collected at dusk; fish log |
| Scratch tickets | Scritchy Scratchy: drag to scratch, ticket tiers, Scratch Bot automation, prestige | Reveal grid | Vending machine | Ticket tiers; Luck shifts odds; the drone offers to "automate your hobby"; the secret-ending lottery is the rarest ticket |
| Quarterly Review | Balatro: poker hands × Joker multipliers vs a target score | Cards | Laptop | Office/fish cards; Coworker cards (Intern, Middle Manager) as multipliers; beat the KPI |
| Aim trainer | Aim Lab | Shooting gallery | The drone throws targets | Gunsmanship XP; costs ammo |
| Compost bin | Suika Game | Drop & merge | Farm | Matching scraps merge; top tier = fertilizer or a golden veg |
| Woodshed stacking | Tetris | Drop & merge | Chopping | Full rows = dry wood that burns longer |
| Treasure dig | Minesweeper | Reveal grid | Bottle maps | Detector numbers show nearby loot; a wrong dig springs a trap |
| Prove you're not a robot | CAPTCHA | Reveal grid | Vending machine | "Select all squares with fish"; harder as the Lord gets suspicious; a joke gate before buying |
| Campfire cooking | Cooking Mama | Timing bar | Base | A perfect cook = better food |
| Decode the signal | Wordle | Word puzzle (later) | Signals board | Daily word reveals tonight's hidden "?" signal |
| Solitaire | Windows Solitaire | Cards | Laptop | Pure office joke; the bouncing-cards win screen |
| Fish shooting, whack-a-mole, Sky Invaders | (already agreed) | Shooting gallery | Fishing / crops / night | — |

### Rejected

- Attacks on the base during ordinary days — no agency while away, rewards staying home (the eclipse is the scheduled exception)
- Tired (fight-streak penalty) — ammo cost already limits fighting every night
- Field campfire when caught outside at dusk — not wanted
- "Days without incident" streak sign with a growing bonus — not wanted
- Run-ending death (Dome Keeper / Don't Starve style) — death costs loot, never the run, like Escape from Duckov
- A far side that swaps daily (walk across the river into a different world each day) — every world would have to fit the same river edge, bridge ends, terrain heights and footprint. The current outer zone is one fixed map across the river
- A full-cozy mode or a raids-off setting — fights are required on raid nights (user, 2026-10-31)
- A barricade at the bridge as the night's front line — goblins cross the river anyway, so the bridge is not a chokepoint (user, 2026-10-31)
- A daytime leash at the bridge (chasers give up there) — an enemy chasing the player can follow across (user, 2026-10-31)
