# Ideas — proposals and history

Current design lives in [CORE.md](CORE.md), [STORY.md](STORY.md), [RAIDS.md](RAIDS.md) and [FARM_AND_ECONOMY.md](FARM_AND_ECONOMY.md). Suggestions here are not permission to implement them. Confirm an idea with the user before moving it into the current design.

Tags: `(???)` = unsure; `[TBD]` = needs a decision. The historical notebook preserves earlier discussions and approvals in their original context; it is not a second source of current rules.

## Current review proposals — not confirmed

### GDD review (2026-11-07)

Decided by the user and moved into the GDD: grudge only from kills in the faction's region; staying out can't lower difficulty; 2–3 waves on ordinary raids, 4–5 on major events; bowl nights never raise Threat; one final boss, the ultimate AI boss, fought as a Megabonk-style run; dying in a raid costs X% of all Tokens except repaid ones; crafting stays core, the dog comes later; robots (one at home, one in the meadow, the rest outside); release in Early Access; no debt deadline; two ways to win (repay the debt or defeat the ultimate boss); a fast-travel portal to the regions later.

Still suggested:

- Where the boss route reveals the truth: during the final-boss run, the AI shows what reality has become before the last fight, so Endings 2–3 keep their meaning without Ending 1
- Tokens as one balance: a raid death already takes a share of all Tokens, so depositing Tokens only guards against outer-zone deaths. Suggested: no carrying or depositing Tokens, deposits are for items, and every death costs the same share. [TBD] what an outer-zone death costs in Tokens
- Fast-travel portal: one-way out to discovered spots, then walk home with the loot, so bringing loot back stays the risky part
  - Ref: Valheim — portals can't carry metal ore or bars, so hauling stays a trip by ship or cart
- First Early Access version: Pine Forest + Swamps (goblins + 2 factions, 2 region bosses) and Ending 1; Highlands, the final zone and Endings 2–3 in updates; 2 enemy types per faction, elites as tint/scale variants
  - Ref: Valheim — Early Access from February 2021; Mistlands added a biome and its boss in December 2022
  - Ref: Enshrouded — Melodies of the Mire (June 2024) added the Blackmire area during Early Access
- Sanity needs 1–2 effects before sleep is built (e.g. HUD glitches and fake enemy silhouettes, slower jam clearing), or it moves to later with the Dark
- One place for the difficulty: the drone's morning report ("Tonight: raid · Goblins + Swamp Folk (angry) · Threat 7"), with the dawn forecast proposal in RAIDS.md
- One raid job per faction, so grudge changes preparation: e.g. goblins steal, Pine Forest breaks fences, Swamps surface inside the meadow, Highlands fly
- Faction slots per raid set by Threat or bosses beaten, not by day
- Gunpowder as the scarce input, so crafted ammo doesn't erode "every bullet counts"

### Enemies are androids in disguise (user, 2026-11-24)

- Decided (user): every enemy is a robot or android made by the AI Lord, disguised as goblins, zombies and other creatures; some spark when killed as a hint. Now in [STORY.md](STORY.md#agreed-premise), [RAIDS.md](RAIDS.md#enemy-types) and [CORE.md](CORE.md#player-combat--progression)
- Still suggested:
  - Hints that grow over the run: early, a few sparks on death and a metallic ping on headshots; later, bodies "despawn" at dawn with a glitch and the drone says "Garbage collection complete"; on Blood Moons, skins flicker and damaged enemies show metal under the skin; the first region boss's skin peels off mid-fight to confirm it
  - Robot enemies are androids that didn't bother with a disguise
  - It explains the strange enemy mix (the AI's "skins") and gives kills a reason to drop scrap and circuit parts for crafting
  - Oil and sparks instead of blood
  - Later hooks, each to confirm: water or rain short-circuits them; an EMP weapon; rescued service robots are discarded enemy builds
  - Keep the farm animals and Loki real, so Ending 3's "I built this" keeps its weight (???)

### Screen modes: nightmare, glitch, retro, outlines (user, 2026-11-24)

- User: post effects that change the atmosphere (nightmare, glitch, retro, edge detection), in the game or in night fights
- Suggested framing: the world is the AI's render, so a screen mode is the AI changing its render settings. Each mode means something (a state, an event, a patch), never decoration only
- Two layers: one sustained mode at a time (an event or a night's patch), plus short bursts on top as feedback (an android dying, a jackpot, the drone's patch notes)
- Modes and where they could appear:
  - Nightmare (desaturated, dark red vignette, double vision pulses, grain, slight radial blur): Blood Moons, low sanity, the outer zone at night. Gives Sanity its first visible effects (still open in CORE). Ref: Amnesia: The Dark Descent — sanity loss blurs and warps the view; Eternal Darkness — sanity effects that mess with the player
  - Glitch (colour split jitter, scanlines, blocky mosaic bursts, hue shift): android deaths as a short spike, the intro where the HUD is taken, the drone's announcements, Threat rising, the eclipse. Ref: Doki Doki Literature Club, Inscryption — glitches as storytelling
  - Retro (pixelize, dithering, posterize, CRT curve and scanlines): a night's random AI patch ("Render mode: 1998"), a cursed box's punishment for a day, the screensaver sleep. Ref: Return of the Obra Dinn — dithered 1-bit look
  - Outlines / debug view (edge detection, comic or sketch): an AI "debug render" night where everything is outlined; may also make enemies easier to read at night. Ref: Lethal Company — low resolution and posterized post effects as its identity; Borderlands — outlines
- Rules suggested: enemies stay readable in every mode (checked at night per mode); no rapid full-screen flashing (photosensitivity); a "reduce screen effects" setting; each mode measured against the 60 fps budget (edge detection needs a depth / normals pass)
- Tools: SC Screen Effects (installed, Undecided in PLUGINS.md) has edge detection, scanlines, CRT tube distortion, colour split, pixelize, dithering, posterize, sketch, double vision, radial blur, mosaic and ready profiles (Dithered Retro, Comic Book, Sketch Book, Night Vision, Body Cam); URP volumes add grain, vignette, chromatic aberration and lens distortion. Blends with the time-of-day grades owned by `EnvironmentDirector` (DAYNIGHT.md)
- Open (???): which modes first; sustained modes only on events, or also random on ordinary nights; whether the player can ever choose a mode (an unlock, a "filter" upgrade)

### Gacha and random fun (user, 2026-11-24)

- User: more gacha and random fun, even if awkward and stupidly fun: chances on when fights happen, fishing, reward boxes, looting, crates
- Rule suggested: randomness the player sees before deciding (forecasts, box categories, tonight's modifier) makes plans and stories; randomness after committing (missed shots, random damage) feels unfair. Keep shooting predictable (jams are the deliberate exception), every random system gets bad-luck protection like the daily box, no real money, and the AI owns the randomness so it reads as a joke
- **Monster on the hook (user):** fishing sometimes hooks a monster or a boss instead of a fish, and the player must fight it. Extends [Fishing up enemies](#fishing-up-enemies). Decided (user, 2026-11-24): now in [FARM_AND_ECONOMY.md](FARM_AND_ECONOMY.md#crops-plants-and-fishing); the suggestions below are not confirmed
  - Ref: Terraria — Blood Moon fishing can hook enemies; Duke Fishron is summoned by fishing in the ocean with a Truffle Worm
  - Suggested: a tell before it surfaces (the bobber sparks, the line pulls hard, the drone: "That is not a fish"); bosses only with a rare bait, so the player chooses when; the androids come out of the water short-circuiting
  - Conflict: ordinary days in the meadow are safe ([CORE.md](CORE.md#loop-rules)). Suggested: monsters only on night fishing and in outer-zone waters, or daytime home-pond hooks stay small and silly (???)
  - Open (???): a short window to cut the line and lose the bait, or always a fight?
- **Bad box or jackpot (user):** a reward box can be a bad box or a jackpot
  - Ref: Call of Duty Zombies — the Mystery Box sometimes gives a joke weapon or the teddy bear that moves the box; Hades — Chaos gates give a curse first, then a strong boon
  - Suggested: a jackpot is a tiny chance in any box, with an oversized reveal; a cursed box flickers before the pick, so taking it is a choice: great or terrible inside. Bad results are mild and funny (a "box opening fee" invoice, a rubber chicken, a goblin popping out), never a big loss, and the bad-luck protection still applies
- **Betting with the robots (user liked, 2026-11-24):** the home robots offer bets on how the player will do; not confirmed, design below is a proposal
  - Fits what is built: the robots already watch raids from their shelter, dance on a win and cry on a loss ([ROBOTS.md](../technicals/ROBOTS.md)), so a bet gives those reactions stakes
  - Flow: talk to a robot during the day, it offers a bet with odds on its screen face; stake Tokens; bets lock at dusk like the tribute bowls; it reacts live from its shelter; settled at the raid's end
  - Raid bets, only on the player's success so nobody throws a raid: no fence breaks, no animal stolen, under N shots fired (fits "every bullet counts"), first wave melee only, N headshots
  - Day dares: catch 3 fish before noon; shoot the can off my head (a shooting showcase)
  - Robot race: Bolt and Pip race round the yard, the player bets on one; pure luck, pure silliness
  - Personalities: each robot bets its own way, e.g. Bolt is a smug gambler who bets against the player at long odds, Pip a cautious one with short odds. Odds come from "a very confident AI model" and are sometimes wildly wrong, which a skilled player can exploit
  - Payouts: Tokens, or the robot's own goods (more ammo stock tomorrow from the shop robot, a discount from the upgrade robot); losing: Tokens and the robot gloats (wears the player's hat, dances in their face)
  - A tally board on the shed with the player vs each robot; robots remember streaks and trash-talk; after a long winning streak a robot refuses to bet ("suspicious")
  - Limits: one bet per robot per day, a stake cap that grows with upgrades; never the best Token source. Blood Moon as the big betting night with higher stakes
  - Differs from the tribute bowls: the bowls change the raid, a bet changes how the player plays it
  - Ref: Cult of the Lamb — Knucklebones, wagering gold against NPCs with their own personalities; Hades — the Pact of Punishment, chosen constraints for more reward; Hitman — challenges as self-imposed goals; Final Fantasy VII — chocobo racing at the Gold Saucer
  - Open (???): raid bets only, or day dares and races too; Tokens only or robot perks; real losses or a free bet that only costs pride; one robot as the bookie or each robot its own
- Other places suggested (2026-11-23 review), not confirmed:
  - Tonight's raid as a chance in the morning ("Raid probability: 70%"), settled by the drone's wheel at dusk. Would change "known for sure in the morning" (user, 2026-10-31). Ref: Stardew Valley — the TV forecast plans the day
  - Raid modifiers rolled as the AI's patch for the night (balloons on goblins, low gravity, big heads); bad ones pay more. Ref: Deep Rock Galactic — mutators and warnings
  - A rare treasure goblin that sprints off with a loot bag. Ref: Diablo 3 — treasure goblins
  - Mimic crates. Ref: Enter the Gungeon — mimic chests
  - A broken gacha vending robot in the world: pay Tokens, maybe win, maybe it errors or falls on you. Ref: Risk of Rain 2 — Shrine of Chance
  - Looted guns with one "AI-generated" trait and a confidently wrong description. Ref: Borderlands — Tediore guns thrown on reload
  - Glitched fish worth 10×, treasure in the catch. Ref: Stardew Valley — fishing treasure chests; DREDGE — aberrant fish
  - Rare golden or glitched produce; a robot merchant on random days with random stock. Ref: Terraria — Traveling Merchant

### Night fight periods and sizes (user, 2026-11-26)

- User: three fight periods a night, **Evening 18–22**, **Midnight 22–02**, **Blue hour 02–06**. Evening is the most likely, a wave sometimes comes at Midnight, rarely in the Blue hour. Nights come in **small, medium and big**, and the player knows in advance. Unsure about the UI, flow and UX (???)
- Changes current rules: ordinary raids end at midnight ([CORE.md](CORE.md#night--special-events)), 2–3 waves a raid ([RAIDS.md](RAIDS.md#how-hard-wave-power-and-threat)), sleep after midnight on raid nights; the build starts raids at 20:00 ([technicals/RAIDS.md](../technicals/RAIDS.md))
- Real time: at 40–45 s per game hour a period is about 3 real minutes and the whole night 8–9, so a period holds one wave group, and waiting for a wave that never comes costs real minutes
- Suggested:
  - **Size sets the budget and the odds:** the morning forecast gives the size (Quiet / Small / Medium / Big; Blood Moon = all three periods). Size sets the wave power ([RAIDS.md](RAIDS.md#how-hard-wave-power-and-threat)) and the chance of later periods, e.g. Small: Midnight 15%, Blue hour 0 · Medium: 50% / 10% · Big: 80% / 35% (placeholders)
  - **Rolled at dawn, revealed in steps:** the whole night is rolled in the morning (saved, so reloading can't reroll it), but the player sees only the odds. When a period ends, the next one is confirmed or cleared at once ("All clear until dawn" or "More after 02:00"), so the player never waits blind and always knows if sleeping is safe
  - **Each period its own job, so it reads differently:** Evening = the main assault (fighters, breakers); Midnight = a second push; Blue hour = a small sneak raid of thieves after the animals while the player may be asleep
  - **Sleep between periods:** the bed offers sleep until a margin before the next confirmed wave, as `SleepRules` does today ("Sleep until 01:30, raiders expected at 02:00"). Big nights cost sleep and so sanity, a natural price of the size
  - **Evening keeps a dusk:** the window opens at 18:00 but the wave arrives at a random hour inside it (19:00 earliest), with a warning first, so ringing the bell and getting the animals home still fit
- UI suggested:
  - Morning: the drone's report / patch notes show the size and three period icons, certain ● likely ◐ unlikely ○
  - HUD: a three-segment night strip under the clock (18–22 · 22–02 · 02–06), each segment expected / possible / clear / active / done; it sits beside the 10-day progress bar ([Forecast and progress bar](#forecast-and-progress-bar-tbd))
  - Warnings before each wave, in game first: Loki growls toward the spawn side, Bolt's night light turns red, the far horn, then the existing RAID INCOMING banner
  - End of a period: the existing banner plus the verdict on the next one
  - Ref: Don't Starve — growls that grow louder before hound attacks, so the warning is a sound, not a menu
  - Ref: 7 Days to Die — the day counter turns red on horde nights, so everyone plans around it
- **Shorter periods (user, 2026-11-26):** shorten them so the player can still sleep, e.g. 20–22 / 22–00 / 00–02, or 18–21 / 21–00 / 00–03 (???)
  - Suggested: 20–22 / 22–00 / 00–02, used as **start windows**, not fight lengths: a wave starts inside its window (after a warning) and is fought until cleared; the next one starts only after the previous one is cleared plus a breather. Keeps 18–20 for dusk prep and leaves 02:00 → 07:00 for sleep on the worst night. Rename Blue hour (that's pre-dawn) to Late night
  - A wave fought at the normal clock pace eats the night: at 45 s per game hour, a 3-minute fight takes 4 game hours. Suggested: the night clock runs slower while raiders are alive (e.g. ×0.25, so 3 minutes ≈ 1 game hour) and at normal speed between waves. Measure how long a raid takes today first (`RaidSoak`)
  - The forecast can show start times instead of windows ("~20:00 · maybe ~22:30 · rarely ~01:00"): the randomness is *whether* a wave comes, not *when*
- **The forecast in the fiction (user question, 2026-11-26):** who tells the player, and why they know
  - Suggested: four layers, from words to world to HUD
    1. **The drone's morning briefing** gives the numbers, with the weather (already rolled per day by `WeatherPlanner`): "Today: sunny. Tonight: medium goblin activity. Return after midnight: 50% confidence." Its motive is logical before any reveal: the player is the collateral on the debt, and a dead debtor doesn't pay ("Protecting your repayment capacity is my top priority")
    2. **Signs in the world** make it physical: smoke columns over the goblin camp across the river during the day, one / two / three for Small / Medium / Big; drums at dusk. After a wave, survivors regroup with torches on the far bank when another wave is coming, and the torches go out on an all clear. The step-by-step reveal is then something the player sees, not a UI rule
    3. **A board at home** (a calendar in the house, or Bolt's face screen) to check again any time; it can hold the 10-day bar and moon phase too
    4. **The HUD night strip** as an early, cheap upgrade from [HUD earned back](#hud-taken-by-the-ai-lord-earned-back-user-2026-11-09): before it, the briefing, the signs and the board; after it, the strip under the clock. A good first upgrade that teaches upgrades
  - **The forecast as a story clue:** it is always right, which is suspicious: how does a drone know when goblins attack? Over the run the drone slips ("Tonight's raid is scheduled— predicted. Predicted."), and later it reads as what it is, a test plan: the AI scheduling stress tests on the player's behaviour (M.A.Y.B.E). Feeds STORY.md's "strange details gradually create suspicion" without revealing the sandbox early
  - Rules suggested: the numbers are never wrong (trust); the jokes live in the wording only. Plain labels on screen (Small / Medium / Big), flavour in the drone's lines
  - Ref: Stardew Valley — the TV weather forecast, a daily ritual that plans the day
  - Open (???): can the evening's result change the later odds (a crushed first wave is less likely to come back)? Rewarding and logical, but the morning odds would then move
- **The forecast as a slot machine (user, 2026-11-26):** shown on the drone's face or monitor, or a sign board: three reels, one per period, like a 777 slot; every day the player spins it to see the forecast
  - Suggested: the night is still rolled at dawn and saved; the spin only reveals it, so reloading or spinning again changes nothing
  - **Reel 1 stops in the morning** (the Evening's size). **Reels 2 and 3 keep spinning slowly** and each stops when the wave before it ends: the slot *is* the step-by-step reveal. A spinning reel's strip shows its odds, mostly blanks when unlikely, mostly goblin heads when likely, so players read the chance from the symbols, no numbers needed
  - Symbols: blank / zzz (no wave), one / two / three goblin heads (Small / Medium / Big). **Jackpot = three red moons = Blood Moon**: the jackpot is the worst night. The eclipse gets its own symbol
  - Where: a machine or board in the yard as the daily ritual (next to the daily box); the drone's face mirrors it when the player is away; the HUD strip upgrade is the same three mini reels, so one look everywhere
  - Never blocks information: if nobody spins by noon, the drone spins it ("I took the liberty")
  - Free to spin, no Tokens, no re-spins: the tribute bowls already change the night. A nudge upgrade (hold one reel) is a possible later extra (???)
  - Readable without the joke: a text line under the reels ("Tonight: Medium · after midnight likely"). Good trailer moment
  - **Forecast board concept (user, 2026-11-26):** a wooden slot-machine board in the yard: "TONIGHT" header, a lamp on top, a satellite dish, a side lever, three reels labelled Evening / Midnight / Late night. Suggested changes: reels 2–3 still spinning by day; the number of crossed swords shows the size; the lamp's colour shows the state from across the yard (amber waiting, red wave coming, green all clear); reels backlit at night; all words as TMP text from the string tables, not painted into the art; the dish as the quiet AI clue. Base props: Animpic POLY-SurvivalWorkshop (Should)
  - **Hanging plaques instead (user, 2026-11-26):** a slot machine needs a custom 3D model we don't have; instead **three wooden plaques hanging from a crossbar**, one per period. Replaces the reels; the rules above stay
    - Suggested: each plaque has a "?" back and an icon front. To reveal, the plaque spins on its rope several turns and slows to a stop (the gacha moment, cheap); the icon is swapped while it's edge-on, so one flat plaque shows any symbol
    - An unrevealed plaque shows its odds with a ribbon tied on (red = likely, white = unlikely) plus a short text line under it
    - Icons: Layer Lab Pictoicons (Should, the HUD's set): battle swords ×1–3 for size, check for all clear, moon (red) for Blood Moon, sleep for a quiet night. Blood Moon: all three flip red at once
    - Parts we own: Animpic `SM_Wooden_Plank_A–C` / `SM_Plywood_Board_A–C` as plaques, `SM_Rope`, `SM_Kerosene_Lamp` hanging in the middle as the state light (Should); the yard's Lakefell fence posts and `Beam_1` for the frame (Maybe, already used for the fence: ask before a new use)
    - Extras (???): the plaques sway with the wind level; shooting a plaque makes it spin (never changes the result)
    - **Mock-up rounds (2026-11-26):** gallery of in-engine captures (over the gate, low board, painted, no text). User direction so far: a low board beside the gate, brown wood, Saira if text is used, ideally **no text**. Suggested: the beam carries sun / moon / star for the three periods; plaques show only state (1–3 skulls or swords for size, a red "?" for likely and a faded "?" for unlikely, a leaf or check for calm); icons and labels painted into the plaque (one worn texture per plaque, lit like the wood), never floating sprites
    - **How the forecast is revealed (user question, 2026-11-26), suggested:**
      - Overnight the plaques reset: they hang turned away, so a fresh day is visible from across the yard
      - Morning: the player reveals it by pulling a rope or ringing a little bell on the board (E), and the evening plaque spins round and lands. If not done by about 09:00, the drone does it ("I took the liberty"), so information is never locked behind a chore. A HUD nudge ("Tonight's forecast is ready") until then
      - Midnight and Late night: they reveal by themselves when the wave before ends (or at the period start if nothing came), with the regroup torches and Loki's growl as the in-world signal; the plaque spins wherever the player is, and the HUD strip / drone line reports it when away
      - Rejected options: fully automatic at dawn (no gacha moment, easy to miss); fully manual every time (a chore, and it locks needed information)
      - Open (???): pull rope vs bell vs shooting the plaque as the reveal action; whether the morning reveal plays a short slot-like spin of all three plaques with only the first landing
    - **User picks so far (2026-11-26, rounds 6–9):**
      - Frame: F1, Highland logs (Raygeas HIGHLANDS `Log_1` as two posts and the crossbar, Should); nothing on the crossbar
      - Each plaque carries its own period icon, small, in its top-left corner: a setting sun (half sun under a horizon line) for evening, moon for midnight, star for late night
      - Plaque content: one crossed-swords mark for a normal fight, painted small for a small fight and larger for a medium one; a **boss fight** is one big horned monster skull (Pictoicon `boss`) instead of swords; empty plaque = no fight. The old "big" size is replaced by the boss night (???: does a non-boss big night still exist?)
      - Boss skull painted **red**, same red as the swords (user pick B1, 2026-11-26)
      - **Built 2026-11-27** (user asked: the board first, then a fixed forecast): the board in front of the house, right side; the forecast is small in all three periods every day, display only (raids unchanged), no reveal yet. See [FORECAST.md](../technicals/FORECAST.md)
      - **Next suggestions (2026-11-27, not decided):** raids follow the forecast (period schedule reading ForecastService); show the revealed icons on the HUD clock and the sleep screen; a first-day hint / map marker; plaques sway with the wind; dust puff on landing; boss extras (crows burst, Loki growls, robots flinch); "near miss" teeter on the last reel; a lantern on the post for night reading; a small creak at 06:00 when the plaques turn back to '?'; per-day weighted roll and boss cadence; odds on unrevealed later periods
      - **Reveal built 2026-11-27 (option A, user picks):** each morning (06:00) the plaques hang as '?'; "[E] Spin Forecast" on the pull rope spins all three like slot reels (reel flip on a pin), landing left to right, a boss last and heaviest; unpulled, they spin by themselves at 19:00. Sounds from Gamemaster. The trigger is swappable (anything may call `ForecastReveal.Reveal()`), and `HideUntilRevealed` off brings back the always-shown board. Polish 2026-11-27: swords 20% bigger and every mark centred; a wilder spin (yank jolt, bigger wind-up, more turns, frame rattle, squash and stretch, wood chips), a near-miss teeter on the last plaque, plaques rocking in the wind, and shots that spin a plaque round its pin (bullet holes stay on it)
      - Open (???): how likely / unlikely is shown on plaques not yet revealed
    - **Reveal options (suggested 2026-11-26, user to pick):**
      - A. Pull the rope: plaques spin like slot reels and land one by one (tick-tick-clack); unrevealed ones keep spinning and wobble to a stop showing "?"
      - B. Shoot the bell: a small bell or tin target on the crossbar; hit it and the plaques flip. Uses the gun, a little skill toy, works from across the yard
      - C. Loki does it: whistle, the dog runs to the board and tugs the rope; the plaques spin. Pays off the dog bond
      - D. Pull the cloth: each morning the plaques hang under a tied sack cloth; pull it off. A boss night's cloth is tied with a red string, a hint before the reveal
      - E. Scratch-off: plaques come covered in ash or mud; hold E to wipe, like a lottery scratch card
      - F. The drone paints it: at dawn the drone sprays the icons on with a short line; automatic but a scene. Also the fallback for A–E if the player hasn't revealed by ~09:00
      - G. Goblin calling card (2026-11-27): at dawn a goblin scout sneaks to the board, slams the plaques round, blows a short horn and runs off; the player may glimpse or shoot it (never changes the result). The raiders declare their own nights; a boss night's skull is pinned with a dagger
      - H. A home robot hangs them (2026-11-27): one robot walks to the board each morning with the plaques on its back, hangs them and waves; ties into the robots' routine
      - Later periods: the next plaque flips by itself when the wave before ends, with a sting and a torch lit on the post beside it
      - Boss reveal (any option): the board shakes, crows burst out of the trees, a low horn, Loki growls, the red skull lands last with a heavy clack
      - Suggested: A (or B) as the morning action, F as the fallback, later periods self-flip, the boss reveal as the big moment
  - Ref: Luck be a Landlord — a slot machine as the main interface, symbols readable at a glance; Call of Duty Zombies — the Mystery Box spin as a ritual players gather round
- Option not suggested: no confirmation between periods, and a Blue hour wave wakes the sleeping player with an alarm. More tension, but unclear when sleep is safe; maybe only on Big nights (???)
- Open (???): can a raid night skip Evening (only a Blue hour sneak)? Does Quiet stay its own night type or become "no periods"? Does the midnight retreat rule go away? Which units belong to each period?

### Survival stats: Hunger, Thirst, Sanity, weight (user, 2026-11-28)

- User: add Hunger, Thirst and Sanity beside Health and Stamina, plus a carried-weight gauge; all stats ready for upgrades and buffs. Full write-up, waiting for approval: [2026-11-28-survival-stats-design.md](../superpowers/specs/2026-11-28-survival-stats-design.md)
- Picked (user): meters weaken and never kill; Hunger = weak melee and shaky aim, Thirst = slower stamina regen and faster drain; two meals and three drinks a day; a well-fed bonus; Sanity drains with darkness (faster the farther from light), not sleeping and special enemy effects; the home campfire is the light and slowly restores it; low Sanity = screen and sound effects (SC Screen Effects) and a lying clock and minimap; nightmare enemies at zero; weight with Comfortable and Max limits, a crawl at Max
- Changes current rules: Thirst is new ([CORE.md](CORE.md#player-combat--progression) lists HP, Hunger, Stamina, Sanity)
- Later (user): fire pits lit across the river, Loki carrying a lantern, a torch then a belt lantern (torch + pistol), calming consumables, comfort food, resting on a bench
- Open (???): the nightmare enemies' design; flares and moon phases as extra night tools

### Boss rings (user, 2026-11-28)

- User: a region boss's first kill gives a **ring** (a Lord of the Rings nod) that is also its key toward the final boss: collect the rings to face the ultimate AI boss. Replaces the "key or trophy" in [RAIDS.md](RAIDS.md#region-bosses) once confirmed
- Decided (user): each ring gives the player a power; **only one ring worn at a time** (wearing several may come later); plain ring names for now, no AI-themed names. In the vertical slice, Phase 4 ([ROADMAP.md](../ROADMAP.md#phase-4-explore-about-115230-h-left))
  - Ref: Valheim — boss trophies hung on the Sacrificial Stones give Forsaken Powers, one active at a time, 5 min uptime, 20 min cooldown
  - Ref: Elden Ring — 2 Great Runes open the capital; one rune equipped at a time, active until death
- Suggested, not confirmed: passive or automatic powers so they need no new key (e.g. get back up once a day when knocked out; see enemies through walls for a few seconds; a short dash or float for cliffs); change rings at home only, so picking one is part of preparing; keep rings apart from learned skills (recoil, reload) so pillar 5 stays about learning
- Open (???): the powers per region; where and when rings can be swapped; whether the final zone needs every ring or N, with extra rings weakening the final boss (Zelda: Breath of the Wild, each freed Divine Beast weakens Ganon); whether a rematch upgrades a ring; pawning a ring to the AI Lord for a debt payment; Endings 2–3 as destroying vs keeping the boss's master ring; keep the LOTR nod a parody (no Tolkien names, script or "One Ring" wording)

### Valheim / Enshrouded + Escape from Duckov mix (user, 2026-11-07)

**Status (2026-11-07):** the confirmed decisions below, plus tribute levels, boss drops, weaker bosses only on Blood Moons and eclipses, the night-2 first raid and later gun rarity/attachments, now live in [CORE.md](CORE.md), [RAIDS.md](RAIDS.md) and [FARM_AND_ECONOMY.md](FARM_AND_ECONOMY.md). This section is the discussion history; unconfirmed suggestions are listed in RAIDS.md → Proposals.

- Idea (user): lean more on Valheim and Enshrouded, mixed (not copied) with Escape from Duckov's looting and the base-defense shooter
- Decided (user, 2026-11-07):
  - Regions across the river are all open; they differ in difficulty by distance: Pine Forest (nearest) → Swamps → Highlands. Each has a boss. Only a final zone stays locked until every region boss is beaten. Replaces the region-per-30-day-cycle proposal in Outer zone and progression
  - Rescue broken service robots in the outer zone; each one brought home opens a station (gunsmith, mechanic, cook, collector…). Answers the open upgrade/storage services question
    - Ref: Enshrouded — five craftspeople are rescued from the world and unlock crafting at the base
  - Crafting stays as simple as Escape from Duckov: a workbench with tiers, guns and defenses per tier, ammunition from gunpowder
    - Ref: Escape from Duckov — the workbench upgrade unlocks weapon mods and dismantling ammo into gunpowder
  - Lootable containers with a search time and rarity colors
  - More weight on melee is fine; it's the answer when the guns run dry
  - Defenses go on fixed spots, as now. No freeform building; few materials and recipes
  - Rejected: food buffs replacing the Hunger meter; Glitch zones (see Rejected)
- Still suggested: one signature material per region; looted guns with rarity and mods; limited backpack slots; comfort near the bed raises sanity restored per hour of sleep (Ref: Valheim — comfort sets how long the Rested buff lasts)
- Risk: Outpost: Infinity Siege (see Historical notebook) mixed the same genres and felt diluted. Borrow Valheim's and Enshrouded's structure, not their size
- [TBD] what the final zone is; its one boss is the ultimate AI boss, fought as a Megabonk-style run (user, 2026-11-07)

#### New enemy kinds in raids (user, 2026-11-07)

- User: likes more enemy kinds raiding, introduced gradually; unsure which rules make players love it
- Option A, boss-triggered: beating a region boss makes that region's creatures raid the farm. The drone warns right after the kill; the player already knows them from their region; when a new group joins, the oldest retires, so 2–3 kinds raid at a time. Risk: a player who avoids bosses gets the same raids forever
  - Ref: Valheim — "The forest is moving…" raids start after Eikthyr and stop after The Elder, when the swamp raid takes over
- Option B, calendar ("patch notes"): each 10-day block adds one kind. Teased on the progress bar ("v0.3 — coming soon: flying units"), debuts small in the block's first raid, mixes in, then stars in the Blood Moon that ends the block. No player agency
- Option C (suggested), hybrid: bosses decide which kinds can raid; the calendar decides how big raids get, so escalation never stalls. New kinds follow B's tease → debut → mix → Blood Moon arc
- Chosen (user, 2026-11-07), then replaced the same day by Grudge below: C + A — bosses add kinds with A's drone warning and retiring of the oldest group; the calendar grows raid size; new kinds follow the tease → debut → mix → Blood Moon arc
  - Suggested: goblin thieves stay in every raid and only region groups retire, since thieves are what make the animals worth protecting
- Rules for any option (suggested):
  - One debut per raid at most; about 3 kinds per raid
  - A debut is a few of the new kind plus familiar thieves; mixes come later (the first raid already works this way)
  - The morning forecast shows tonight's kinds; a new kind shows as a "?" silhouette
    - Ref: Plants vs. Zombies — the seed-selection screen shows the zombies coming in that level, so you pick plants for them
  - Each kind needs a different answer (thieves → guard animals, breakers → repair and turrets at fences, flyers → aim up, hunters → the player); a kind that changes no decision is just a reskin
  - The counter is reachable by the time a kind raids (e.g. the boss drop that sets off the swamp also unlocks its counter at the gunsmith robot)
  - The first kill of each kind pays: a bounty, a drone scan that shows its weakness, a unique crafting drop

#### When raids start (user question, 2026-11-07)

- User: should the early game have raids, or should they start once the player reaches the outer zone? Is that too late?
- Time check: a game day is ~20 min (12 day + 8 night), so the current first raid (after 4 quiet days) comes ~1.5 h into play. Steam refunds run to 2 h of play
- Suggested: the first river crossing starts the raids, A's cause and effect from day one ("they followed you home"). The first raid, a few goblin thieves, comes the night after the first crossing, night 2 at the earliest, so night 1 is always quiet. Fallback: no crossing by day 3 → the goblins come anyway on night 3. Needs a bridge repair cheap enough for day 1 (meadow wood)
  - Ref: Valheim — a weak boar-and-neck raid ("Eikthyr rallies the creatures of the forest") runs from world start until the first boss dies
  - Ref: 7 Days to Die — the first horde night is day 7, but zombies roam from the start, so fighting begins right away
- Would replace "Days 1–4 are quiet" in CORE.md

#### Grudge — raids as revenge (user, 2026-11-07)

- Idea (user): the game starts with a few minor goblin raids the player usually beats easily (goblins can be swapped later). Each outer-zone biome has its own enemies; only kinds the player kills hold a grudge and raid in revenge. A grudge meter per kind: more kills → more or stronger raiders; it fills fast with continuous killing, has a max, and falls over time when left alone. Replaces C + A's "bosses add kinds" (user, 2026-11-07)
  - Ref: Don't Starve — killing innocent creatures raises a hidden naughtiness count; past a threshold Krampus comes to steal; it falls with each day without such kills
  - Ref: The Forest / Sons of the Forest — cannibal aggression rises with kills and days and brings war parties to the base; in Sons, anger never decreases, so our decay gives the player a way back
- Suggested fixes:
  - Only kills in their territory count; a raid the player beats spends most of that grudge (they lost), a lost raid keeps it. Prevents a loop where defending raises the next raid
  - Grudge raids pay more as the grudge rises (more raiders, more ammunition, scrap and Tokens, but no progression materials; see Anti-grinding), so killing has a price, not only a penalty
    - Ref: Hades — the Pact of Punishment lets players raise Heat for extra rewards
  - Make it visible: 4 steps per meter (calm / annoyed / angry / furious), a small pop when a kill raises it, and the morning forecast names who is coming. Flavor (???): shown as the AI's 1–5 star "customer reviews" of the player
  - One meter per faction (goblins + one per region), not per kind: ~4 meters
  - The raid/quiet schedule still decides when; the grudge decides who and how many. The max caps raid size (also the crowd perf budget)
- Region bosses (user, 2026-11-07; replaces the earlier "Blood Moon or eclipse only" idea): each biome has its own enemies and a boss. Summon and defeat it for rewards and a trophy. It can be summoned again many times, harder each time. Killing it adds a weaker version of the boss to raid waves
  - Suggested: the first kill gives the trophy (and the final-zone key); repeat kills pay more materials and Tokens as difficulty rises
  - Suggested: the weaker boss leads its faction's raid when that grudge is at max, and joins Blood Moons. Cheap: same model and moves, scaled down
- Goblin raids also grow slowly with the days, so a player who rarely fights in the outer zone still sees harder nights over time (user, 2026-11-07)

##### Who raids: contact and chance (user, 2026-11-07)

- Idea (user): attacking or triggering an enemy kind in its biome (no kill needed) registers it as a possible raider. Even at grudge 0 it keeps a small chance to raid (e.g. 5–10%); kills raise the grudge, which raises both the chance and the amount. Balance it so too many kinds never raid at once
- Decided (user, 2026-11-07), later replaced the same day by per enemy type (see Enemy cast from installed packs): register and hold a grudge per faction (~4 meters); once registered, any of the faction's types can raid. Fallback if the user changes their mind: register per type, so a faction raid brings only the types already met. Registration happens when the player hits one of the faction's enemies or one starts chasing them (not when merely seen), and the drone says so ("New contact: Swamp Folk. They know your scent now")
- Suggested: building one raid night
  1. The schedule picks raid nights, as now; everything below is rolled at dawn and shown in the morning forecast (new faction = "?" silhouette)
  2. Goblins always come; their size grows slowly with the days
  3. Extra faction slots cap the mix: 1 early, 2 later (by day count). Blood Moons lift the cap: every registered faction comes, sized by its grudge, with difficulty rising gradually from one Blood Moon to the next, never all at once (user, 2026-11-07). [TBD] an enemy power score per wave to tune this later (user)
  4. Each registered faction rolls its chance (min 5–10% + grudge), angriest first, until the slots are full; at most one debut per raid
  5. A raid budget, growing with the days, is shared between the factions that come, weighted by grudge; it never stacks past the max. A second faction splits the budget, it doesn't double the raid
  6. Anti-streak: a faction that came in the last raid rolls at half chance, unless its grudge is at max
  - Ref: Risk of Rain 2 — the combat director earns credits over time and spends them on monsters, so a mix shares one budget
  - Ref: Escape from Duckov — storms with no warning drew "please add storm warning" threads; roll at dawn, not at the fence

##### Wave power score (user, 2026-11-07)

- Idea (user): each raid gets a wave power score; enemies are filled in until it's spent; grudge raises the score. Goal: progression and difficulty that can be balanced and monitored, between boring and too hard
- Suggested additions:
  - Each type has a power cost (elites and the weaker boss cost more); the budget buys from the factions coming tonight
  - Budget = base curve by day + grudge bonus per faction, capped. Shape it as a sawtooth, not a straight line: rise through each 10-day block, peak at the Blood Moon, drop after it, and start the next block a bit above the last one
    - Ref: Left 4 Dead — the AI Director builds tension, peaks, then relaxes before building again
  - Split a night's budget into 2–4 waves (small → big) with short pauses to reload and repair
  - (???) A small, bounded nudge from the last raid: easy win → +10%, lost raid → −10%. Scale with days and bosses beaten, never with the player's gear, or upgrades feel pointless
    - Ref: The Elder Scrolls IV: Oblivion — enemies leveled with the player, widely criticised for making leveling feel pointless
  - Monitor: a dev overlay and one log line per raid (budget, spent, types, waves, damage taken, animals lost, ammo used, result), plus a sheet of the budget curve for days 1–60 to check the whole run before playtests
- Accepted (user, 2026-11-07): type costs, sawtooth, waves, bounded nudge, monitoring
- Idea (user, 2026-11-07): difficulty follows the player, not the day. Dying in a raid resets the grudge and lowers the next raids, so a skilled player reaches a high difficulty by day 30 and a weaker one by day 60–80
- Suggested: a Threat level drives the budget instead of the day
  - Each won raid raises it (more for a clean win), a lost raid lowers it (e.g. −20%)
  - Floor: rises slowly with the days, so a struggling player still moves forward. Ceiling: set by bosses beaten, so a skilled player can't outrun the content
  - The sawtooth stays: the block shape (ramp → Blood Moon peak → quiet) sits on top of the Threat level
  - Two separate knobs: Threat = how hard (performance); grudge = who comes (player choices)
- Decided (user, 2026-11-07): dying in a raid costs carried Tokens, home damage and some animals, and lowers the grudge of the factions in that raid by a fixed 25% of the meter's max (not halving), down to 0. The 25% is upgradable. Later the same day the Token cost became X% of all Tokens except repaid ones (RAIDS.md)
  - Decided (user, 2026-11-07): two upgrades: more grudge reduction on death, and faster daily cool-down
  - Later (user, 2026-11-07): a tribute at the robots buys a grudge cut with Tokens — replaced the same day by the tribute bowls in RAIDS.md
- Suggested: show the Threat level openly, as the AI Lord's "difficulty patches" ("You were having too much fun. Difficulty +1"). Loot and the daily box get better as Threat rises, so players want to climb and losing on purpose doesn't pay
  - Ref: Resident Evil 4 — a hidden difficulty rank rises and falls with player performance
- (???) A starting difficulty choice that sets how fast Threat climbs
- [TBD] target win rates for ordinary raids vs Blood Moons; the cost of each type; how much a win or loss moves Threat

##### Anti-grinding (user concern, 2026-11-07; accepted by user)

- Concern (user): players farm weak enemies over and over instead of exploring
  - Ref: Soren Johnson — "Given the opportunity, players will optimize the fun out of a game" (Water Finds a Crack). Make exploring the fastest path, not forbid grinding
- Suggested: progress only comes from out there. Kills and raids pay ammunition, scrap and Tokens; next-tier materials, rescued robots, trophies and keys come only from exploring and bosses. Grinding forest enemies keeps the player at forest tier
- Suggested: cleared camps stay cleared for some days; wandering enemies per area are capped. Grinding one spot runs dry, so more kills mean walking deeper
  - Ref: Valheim — destroyed greydwarf nests don't come back
- Suggested: the grudge sets raid size, never raid frequency; the schedule still decides raid nights and a won raid spends the grudge, so raid loot has a weekly ceiling
- Grinding stays allowed as the safe, slow choice; exploring is the risky, fast one (pillar 2)

##### Valheim's raids compared (user, 2026-11-07)

- User: should raids follow Valheim's rules instead of ours?
  - Ref: Valheim — every 46 min a 20% roll starts a raid on a player within 40 m of 3+ base structures. Each boss kill enables the next biome's raid and retires its own (Eikthyr's raid ends when Eikthyr dies); some events (wolves, skeletons…) never retire. Raids last 80–150 s and the timer pauses while no player is near, so running away doesn't end them ([gamever.io](https://gamever.io/knowledge-base/valheim-raids-every-event-what-triggers-it-and-why-running-away-does-not-work))
- Suggested: keep ours (scheduled nights, grudge, Threat). Valheim's raids are an occasional side event; ours are the core of the night, so they need a warning (pillar 3) and the player's choice (grudge)
- Suggested borrowings:
  - A boss kill calms its faction: its grudge drops to 0 and it loses its minimum chance, so it comes back only if the player angers it again; the next region's faction takes notice (registered). Ordinary raids follow the player's frontier; Blood Moons still bring every faction and the weaker bosses
  - Leaving the farm during a raid counts as being knocked out (same costs); away all night, raiders also take some penned animals. Fleeing or skipping never beats defending
  - Each faction announces its arrival with its own line and sound, like "The forest is moving…"
- Decided (user, 2026-11-07): all three borrowings, now in RAIDS.md

###### Away on raid nights, Blood Moons and eclipses (user question, 2026-11-07)

- Suggested rule of thumb: ordinary raids come for the farm; major events come for the player. Away = across the river; the meadow counts as home. Quiet nights stay free
- Ordinary raid nights, away is a choice with a price:
  - Back before the raid ends: join the remaining waves; the raid counts as usual (won or lost by the result)
  - Not back by midnight: lost without a fight. Animals outside and some penned ones are taken, fences take more damage; Threat and grudge don't change
  - Suggested: the AI's "no-show fee", the same Token loss as being knocked out, so a rich player can't skip a hard raid to dodge it. Gentler fallback: property losses only
- Blood Moons and eclipses, attendance is mandatory. Options:
  - A (suggested): the AI pulls the player home. A warning before the event ("Sprint review in 1 hour. Attendance is mandatory."), then the drone teleports the player home with what they carry when it starts. The climax is always fought at the prepared farm; reuses the fast-travel portal's effect
  - B: the event comes to the player wherever they are; away means fighting without fences, in the dark. The eclipse giant would need arenas in every region
    - Ref: 7 Days to Die — guides say the Blood Moon horde spawns near the player and heads straight for them, hence dedicated horde bases
  - C: same as ordinary nights, scaled up. Lets the player skip the climax of each 10-day block
- Decided (user, 2026-11-07), instead of the above: every raid, Blood Moon and eclipse happens at the farm wherever the player is. Raiders target the farm and its resources, not the player, unless the player comes within their spotting range (different per enemy type). Away or late, the farm gets sabotaged and sacked; a late player can still fight the rest. The regions at night hold buffed enemies, night enemies and predators. Now in RAIDS.md → At the farm
- Suggested additions:
  - The Token loss comes with a lost raid, not only with dying: knocked out, or not home when the raid ends (framed as raiders cracking the robots' Token vault). Otherwise a rich player loses less by staying out than by fighting and losing. Replaces "leaving the farm counts as knocked out", which would punish chasing a thief across the river
  - Drone alerts while away (raid started, fence down, animals taken), so racing home is a decision, not a surprise in the morning
    - Ref: Dome Keeper — the store page pitches this choice: "Is there enough time to mine a little deeper and get back to defend before the monsters attack your dome?"
  - Notice rules: a hit always makes an enemy notice the player; a spotted enemy alerts nearby allies; a visible and audible cue when one turns; enemies go back to the farm when the player gets away. Per type: thieves flee with their loot, breakers keep breaking, fighters turn on the player
  - Night danger on a budget: outer-zone enemies use the existing elite markers at night; dedicated night predators later
  - Tech: while the player is far away, the raid can run as a cheap tally of losses and become live enemies as the player gets close
- Decided (user, 2026-11-07): the first four additions, now in RAIDS.md (At the farm, Losing a raid)

##### Enemy cast from installed packs (user, 2026-11-07)

- User: wants Synty robots and zombies in raids; enemies that match each biome would cost too many assets
- Suggested: cast each region's faction from packs already installed instead of matching the biome. The AI-made sandbox doesn't need ecologically sensible enemies (the early direction already said the mix itself hints at an AI-generated world); camp dressing tells each region's story
  - Goblins (home raiders from day 1): Sidekick Goblin Fighters, in use
  - Pine Forest → zombies: Sidekick Apocalypse Zombies (+ POLYGON City Zombies); boss from POLYGON Boss Zombies. An outbreak at an abandoned campsite, the AI's idea of a forest
  - Swamps → robots: Sidekick Sci-fi Robots (12 looks); boss = a mech from POLYGON Mech Pack (humanoid rig). The AI's dump of discarded robots rusting in the bog, also where broken service robots wait to be rescued
  - Highlands (later update) → open, e.g. Sidekick Fantasy Skeletons with a Fantasy Rivals boss (troll, golem)
  - Alternative: robots in the Highlands, next to the electronic final zone; the Swamps get skeletons, and robots arrive with the Highlands
- Why it's cheap: goblins, zombies, robots and skeletons are all Sidekick characters, built by SidekickKit like the goblins on the same humanoid clips; factions share behaviours (thief, breaker, fighter, ranged) and differ by look plus one signature unit; elites are tint/scale variants; weaker bosses are the same model scaled down
- Each faction a raid job, so the forecast changes preparation: goblins steal; zombies come in numbers and break fences; robots are few, armoured and ranged, with the longest spotting range, so they hunt the player
- Fallback: factions not tied to regions (zombies and robots roam everywhere in different mixes). Cheapest, but it blurs "a region = a faction + its boss" and the boss-calms-its-faction rule
- User (2026-11-07): enemies don't need to fit the biomes like Valheim's; regions can be just terrain zones, and one enemy type can be found in several regions
- Suggested: agree, with the cast above kept and only the exclusivity dropped. Regions introduce factions instead of owning them:
  - A region introduces a faction, and the region's boss belongs to it (Pine Forest: zombies; Swamps: robots). After that, the faction also turns up in other regions. Goblins are everywhere
  - Each region still differs by its mix (mostly robots in the Swamps, some zombies), terrain and hazards, materials, camps, the robot to rescue, and a danger tier: deeper regions field more and stronger elites
  - Later regions can add no new faction, only a harder mix and a boss, so content grows without new characters
  - Ref: 7 Days to Die — each biome has a gamestage and loot-stage bonus (Pine Forest +0/+0 up to Wasteland +50/+20) that raises enemy strength and loot beyond the player's own progression ([PinDrop, from biomes.xml](https://pindrop.gg/7-days-to-die/biomes))
- Rules this would change in RAIDS.md: a faction becomes a group of enemy types, not a biome's set; grudge rises from that faction's kills anywhere outside the home, not only in its region; a boss still calms its own faction and registers the faction the next region introduces
- Risk: if every faction shows up in the first region, later regions have nothing new to find. Introducing them region by region keeps the discovery
- Decided (user, 2026-11-07): raids are triggered per enemy type (goblins, zombies, robots…), not per region faction, because of limited assets. Types aren't tied to regions. Now in RAIDS.md → Enemy types. Carried over to fit: grudge rises from a type's kills anywhere in the outer zone; each region boss belongs to one type, and its kill calms that type and registers the type of the next region's boss. Still suggested, not written: which region introduces which type

### Debt to the AI Lord and endings (user, 2026-11-07)

**Status update (2026-11-07):** Tokens, the debt for alleged real-world token waste, and winning through cumulative repayments are now agreed in [STORY.md](STORY.md) and [FARM_AND_ECONOMY.md](FARM_AND_ECONOMY.md). The earlier world-generation invoice framing below is superseded. Remaining activity paths, invoice structures, prices, bosses and ending variants below are proposals, not approved rules.

**Later story decision (2026-11-07):** [STORY.md](STORY.md) now records the siren/PC-flash opening, the AI-occupation reveal after repayment, resistance or return, and the post-return ultimate-boss choice between 2018 and staying at the farm. Those are the current ending routes. The income-percentage ending selector and generic post-invoice stay choice below remain earlier proposals, not the adopted structure.

- Idea (user): the MC must stay in this world until he pays off his debt to the AI lords. Winning = reaching the target (e.g. 1B coins). Everything pays: animals, crops, fishing, herding, mining (???: not in CORE yet), looting, enemies, exploring, gambling, lottery, minigames
- Idea (user): Valheim-style biome bosses; each defeated boss gives rewards that speed up repayment. Bosses are not required: patient farming and fishing, or fighting and looting without summoning bosses, also reach the target. Different paths lead to different endings
- Suggested framing: the AI Lord bills the MC for generating his world: "1,000,000,000 tokens". Tokens are the currency, so huge prices read as AI satire (an egg sells for tens of thousands of tokens). The drone is the account manager
- Suggested scale rule: run length ≈ target ÷ average daily payment. 1B and 1M differ only by three zeros on every price. What matters is the growth: late-game daily income ~10–30× day one keeps an egg meaningful on day 50; an idle-game curve (×10,000) makes the farm path's products worthless
- Suggested invoices: the 1B comes as ~9 invoices of rising size (5M, 10M, 25M, 50M, 75M, 100M, 150M, 235M, 350M). Pay any amount at the trading robot, with no interest and no deadline. Each cleared invoice gives a reward (a service, an upgrade tier, access to the next region, a story beat), so paying early is progress, not lost money, and day one already moves a bar
  - Ref: Animal Crossing: New Horizons — Nook's home loans have no interest or deadline; paying one off unlocks the next house expansion
  - Ref: Recettear — rising installments over a month (820,000 pix in total); missing one is game over. Borrow the rising sizes, not the game over (death never ends a run here)
  - Ref: Lethal Company — the quota rises every 3-day cycle and failing it ends the run; same lesson
  - Open: if playtests show players ignore the debt, add a minimum payment at each eclipse (the "quarterly review") with a comic penalty, never a game over

Suggested paths, tracked as lifetime earnings by source (shown as a ledger on the robot's screen):

| Path | Earns from | Risk and pace |
| --- | --- | --- |
| Homestead | Animals, crops, fishing, cooking (mining ???) | Low risk, slowest; grows with farm upgrades |
| Adventure | Enemy bounties, loot, camps, exploration finds | Medium risk; ammunition eats part of the profit |
| Boss | Biome-boss rewards | High risk, big lumps, fastest |
| Luck | Lottery, scratch tickets, gambling minigames | Loses on average; a jackpot is the secret ending |

- Raids stay required on every path (no full-cozy mode), so Homestead still defends at night
- The ending is picked by the largest share when the last invoice clears: ≥ 60% (???) from one path gives its own ending, otherwise a mixed one
  - Ref: Dishonored — a hidden chaos tally from kills picks the light or dark ending. Keep ours visible so the path is a choice
  - Ref: Stardew Valley — Community Center (bundles) and Joja (gold) reach the same unlocks by different routes
- Suggested bosses: one per outer region, summoned at an altar with offerings found in that region, so the fight is opt-in and prepared. The boss drops a core: sell it to the Lord (a big payment) or install it at home for a lasting power. Its other drops open the next region (see Outer zone and progression)
  - Ref: Valheim — summon items go on a Forsaken Altar; the boss trophy on the Sacrificial Stones grants a Forsaken Power, one active at a time
  - Scope: with Blood Moon minibosses and the eclipse giant this makes 5+ bosses. Suggested: Blood Moon minibosses are elite regular enemies; on eclipse days the next unbeaten region boss comes to the farm. Surviving until the eclipse ends wins the day; killing it counts for the Boss path
- Suggested gambling: loses on average, daily limits ("machine restocks tomorrow"), parody art. An extra, not a core path
  - Ref: Balatro — rated PEGI 18 for depicting gambling, lowered to 12 on appeal
- Suggested after the last invoice: the ending plays (drone epilogue and stills, no cutscenes), then choose Log out (credits) or Stay (keep playing, ending recorded)
- [TBD] the number (1B tokens or 1M coins), one AI Lord or several (one per region, each holding a share?), deadlines, target run length per path, mining, number of endings

### HUD taken by the AI Lord, earned back (user, 2026-11-09)

**Status (2026-11-09):** the idea and the upgrade tracks below now live in [CORE.md](CORE.md#hud-earned-back). This section keeps the discussion and the unconfirmed suggestions.

- Idea (user): at the start the AI Lord destroys the player's assists (crosshair, minimap, HUD); the player is on their own and earns them back through upgrades. Fits pillar 5 (office worker → survivor) and the satire of being billed for basic features
  - Ref: Evoland — game features (colour, 3D, music) are the unlocks; the joke carries the progression
  - Ref: DLC Quest — basic features (moving left, animations, pause) are bought back; works as parody, keep it a joke, not a paywall feeling
- Upgrade tracks (user, 2026-11-09):

| Element | At start | Upgrades |
| --- | --- | --- |
| Crosshair | None for hip-fire | Tiny centre dot → reticle showing the gun's accuracy / spread → better reticles |
| Ammo counter | None | Ammo counter on the HUD |
| Minimap | None | Simple minimap → clearer → advanced; the compass comes with the minimap upgrades |
| Player stats (health, stamina, sanity) | Simple stats HUD | Better stats HUD |
| Time of day | No clock, only the time period | Clock |
| Damage numbers | None | Visible through an unlock |

- Later, nice to have: raid direction indicator, enemy health bars
- [TBD] how upgrades are unlocked: decided with the skill tree / upgrade system
- Suggested (not agreed yet):
  - Aiming down sights works from the start, so there is always one accurate way to shoot; the centre dot comes early (day 1–2), since no hip-fire crosshair plus scarce ammo is the harshest start
  - Each tier adds information, not just looks: spread reticle = when a shot lands; minimap tiers = discovered areas + your arrow → markers (home, animals, places of interest) → threats. "Better reticles" add a cue (in-range colour, range marker) or are cosmetic picks
  - Before an upgrade, give rough cues so it's tension, not guesswork: check-mag animation and a last-rounds sound (ammo), sun / light / sounds (time), a readable health signal from day one
  - Always on, never taken: interaction prompts, hit markers, damage direction
  - The removal is shown: full HUD for the first minute of the intro, then the AI Lord deletes it with a joke. Decided (user, 2026-11-09): the digital space intro in [STORY.md](STORY.md#opening-sequence--staging-draft), faceless attacker
  - 2–3 HUD unlocks in the first session (~2 h, the Steam refund window), e.g. dot, clock, first minimap
  - An assist setting that turns everything on (accessibility)
  - Minimap late, so the drone's paper map and landmark navigation (World & environment) keep their value; the minimap is also the costliest item (top-down render of the map, markers)
  - Open: the forecast progress bar sits "under the HUD clock"; decide whether it's visible before the clock upgrade

### Inventory and items (user, 2026-11-09)

**Status (2026-11-10):** built from the decisions below ([EQUIPMENT.md](../technicals/EQUIPMENT.md)). Not yet in CORE.md: the GDD still says "limited backpack slots" is a suggestion and keeps the 50–70% grave rule.

- Idea (user): an inventory like Escape from Duckov's, from basic to complex: items collected, used or spent, dropped, dragged around. Tokens are an item too (Duckov's Cash). First focus: Tokens and ammunition. Later: item weight (a heavy load slows and tires), backpacks with a set capacity, a pet that carries items
- Duckov, researched 2026-11-09 (escapefromduckov.net item database, Steam discussions):
  - Slots, not a grid, one stack per slot
  - Cash is an item (weight 0, stacks to 1,000,000) next to a banked balance; an ATM at the base moves money between them
  - Ammo items by calibre and tier (Rusty → AP): 0.01–0.02 kg a round, stacks of 240–320
  - Backpacks add 8–30 slots; four load states: Agile +15% speed, Heavy −20%, Overweight −40%, Unbearable
  - The dog: 1 → 6 safe slots that weigh nothing and survive death; one grave, the latest only, no timer
  - Player complaints: hidden weight thresholds, slots plus weight as a double limit, a small stash; modders added sort, loot-all and value tooltips
  - Ref: Escape from Tarkov — the grid creates tension, but "inventory Tetris" is the classic chore; Duckov, Valheim and Enshrouded use slots
  - Ref: ARC Raiders — coins are a balance never carried; fragmented ammo stacks are a top complaint
  - Ref: Abiotic Factor — two encumbrance tiers as yellow / red HUD markers; Palworld patched out freezing overweight players
- Decided (user, 2026-11-09 / 10):
  - Tokens: an item like Duckov's cash, weightless, no stack limit, auto-collected within 1 m (a magnet; the radius can be upgraded later). Placeholder coin model from Synty Polygon Prototype. No bank balance yet
  - Ammo: items that stack with a limit per stack (pistol / rifle 120, shells 40); calibres per gun type later
  - Loose items: aim and E to collect, with a HUD notice; a warning when full; a partial pickup leaves the rest
  - Lootables (crates, dead enemies): aim, E, and the Duckov-style screen opens at once; hidden items are revealed one per second with a ring on the slot (changed 2026-11-10 from "wait 1 s × items with a circle on the HUD, then the screen"). Paused at home; during a raid not paused, and a hit closes it (2026-11-10). A bag marker floats over crates and bodies that still hold loot within ~15 m, dim once everything in them has been seen (2026-11-10). Placeholder inventory sounds (2026-11-10). Bodies stay while they hold loot and sink 3 s after being emptied; at most ~20, all gone at dawn. Goblin drops (placeholder): nothing 30, Tokens 5–20 (40), pistol 6–12 (20), rifle 10–20 (15), shells 2–4 (10), medic box (5); 1–2 picks
  - Inventory screen: Tab, paused, drag and drop, sort, take all, drop by dragging out; G throws the held item forward
  - Active slots: 2 guns (keys 1, 2), melee (V), consumables and tools (3–9); more guns ride in the backpack; a third gun slot (1, 2, 3) maybe as an upgrade later
  - Equipment UI: Head, Body, Face slots shown, no items yet; no headphones or totems; a backpack item (+slots, +weight)
  - Weight: values now, slowdown later. A medic box collectible, held in hand (no use yet)
  - Item use (2026-11-10): select from slots 3–9 and hold the attack button (kit 3 s, bandage 1.5 s, shots 1 s, a progress ring); walk slowly, no sprint; let go, switch or a hit cancels. The medic box is a First Aid Kit with 5 uses of 20 HP; Bandage heals 15 HP over 10 s (a hit pauses it; bleeding later); Adrenaline (+25 % speed, 60 s) and Stamina Shot (regen ×2, sprint cost ½, 60 s)
  - Value and rarity (2026-11-10): every item has a value in Tokens and a rarity (grey, green, blue, purple, gold) shown as a slot border and in the tooltip; selling waits for the trading robot. Placeholder numbers
  - Icons (user, 2026-11-10): item icons from the UI icon packs in `Assets/_StoreAssets/UI`, one set and style preferred
  - Later: the dog carrying items
- Suggested (not agreed yet):
  - Bank the carried Tokens at the trading robot (Duckov's ATM without the chore): carried Tokens are at risk, banked ones only lose the lost raid's X%. The walk home is the risk
  - Weight tiers drawn on the bar, slower but never frozen; a heavy load makes the walk home slower, so greed competes with dusk. Generous slots, so weight is the limit that matters
  - Death (???): Duckov's one-grave rule (everything carried drops, latest grave only, no timer; equipped melee and the dog's slots kept) instead of the 50–70% roll in CORE.md
  - Ammo tiers later: crafted ammo cheaper but jams more
- [TBD] the bank, weight tiers and slowdown, the death rule, item use, armour items, the dog's slots

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

Superseded (user, 2026-11-07): all regions are open and ranked by difficulty, with summoned bosses ([CORE.md](CORE.md)). Kept for reference.

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
| Daily player biome portals provide the main expeditions | Player biome portals are removed; repairing the bridge unlocks the outer zone: open regions ranked by difficulty, each with its own enemy mix and a boss, and a locked final zone (user, 2026-11-07). A fast-travel portal to the regions comes later |
| Raids triggered per region faction (goblins + one faction per region) | Per enemy type (goblins, zombies, robots…), with types shared between regions, because of limited assets (user, 2026-11-07) |
| Possible fixed morning product collection and next-day grazing bonuses | Animals physically drop products near themselves, with faster production while roaming in the meadow |
| Selling and buying have no chosen home service | A cute robot trading station is chosen; more robots, one in the meadow and the rest rescued in the outer zone, open further stations (user, 2026-11-07) |
| Ordinary-night ending remains unresolved | At midnight, new attacks stop and surviving raiders retreat |
| Immediate versus next-morning sale payment remains unresolved | Sales pay immediately; purchase delivery timing remains open |
| Every ordinary night may require a defense | Quiet nights are part of the loop; the distribution of ordinary raids and quiet nights is undecided |
| Major-event dates remain undecided | A major event every 10 days: two Blood Moons, then an eclipse on every 30th day |
| Big fights every 10th night were rejected as forced | The Blood Moon is a required full-night event every 10 days |
| Night enemies arrive from the outer zone and portals | Goblins cross the river at night, some over the bridge; some enemies fly; some appear inside the meadow. The bridge is not a chokepoint |
| Enemies never cross into the meadow by day unless provoked | An enemy chasing the player can follow across the bridge |
| The broken bridge is the only way across | The bridge is the safe way; the river can't be swum until an upgrade, but a player who makes it across is not stopped |
| Ordinary raids versus quiet nights undistributed; forecasts undesigned | Tonight's type is known for sure in the morning; night 1 quiet and the first raid on night 2 (user, 2026-11-07), about 4 raids per 9-night block, never 3 in a row, quiet nights around major events, dealt from a shuffle bag |
| What happens at home while the player is away on a raid night | The raid resolves without the player: exposed animals are stolen and fences damaged; the morning shows the result |
| Stolen animals' fate undecided | They are held deep in the outer zone for 3 days, then lost forever; replacements can be bought |
| Pickup persistence undecided | Drops never despawn; each animal stops producing at a cap; a collector robot later |
| Animals managed only by the player at dusk | A bell calls them home; they walk slowly and hurry when the player interacts with them. At night they sleep and produce nothing |
| Death loses 50–70% of loot anywhere; the stash is safe | Deposited items are safe; a lost raid (knocked out, or not home when it ends) costs X% of all Tokens, deposited ones included but never repaid ones, and some animals (more on major events); a knock-out also costs part of what you carry (user, 2026-11-07) |
| Ammunition bought or crafted | Bought from the trading robot (every item has a daily stock limit), looted from enemies, or crafted from gunpowder (user, 2026-11-07) |
| XP skills, Luck, perk cards and the cat as core growth and companions | Upgrades and gear are the growth; skills, Luck, perk cards and the cat are extras for later (user, 2026-10-31) |
| Perk cards as the reward for night fights | Enemy loot and a better free daily box (pick one of three) |
| Sleep skips the night | Sleep restores sanity per hour slept: any time on quiet nights, after midnight on raid nights, never on Blood Moons |
| 30+ days to an ending through biome relics and the grand trial | Two ways to win: repay the debt (Ending 1) or defeat the ultimate AI boss in a Megabonk-style run, reached with every region key (Endings 2–3) (user, 2026-11-07). The trial's relics are replaced by the region keys; its "own game" reveal is dropped |

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
    - Update (user, 2026-11-09): a minimap returns as an upgrade, with the compass merged into it; see HUD taken by the AI Lord, earned back
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
- Update (user, 2026-11-07): the run-and-gun format is now the fight with the ultimate AI boss in the final zone (CORE.md)
- Earlier: not designed further, and its entry needed a new design once biome relics were gone; the region keys now open the final zone

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
- Food buffs replacing the Hunger meter (Valheim's food slots) — Hunger stays (user, 2026-11-07)
- Glitch zones, a Shroud-like timed danger zone (Enshrouded) — not wanted (user, 2026-11-07)
