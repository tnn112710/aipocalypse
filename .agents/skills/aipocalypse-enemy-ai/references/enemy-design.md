# Designing enemies, threats and bosses

What each agent asks of the player: its job, how it reads, warns, fights, gives up and dies. Rules from shipped games and their developers, picked for this game (first person, scarce ammo, melee fallback, a farm to protect, cozy days and frightening nights). How agents combine into fights, waves and a difficulty curve is in [encounters-and-waves.md](encounters-and-waves.md); how to build the shared systems is in [building-blocks.md](building-blocks.md).

Research done 2026-10-31. **Nothing here is approved for implementation.** Lines marked *Proposal* map a rule onto a GDD behaviour and are proposed to the user first (AGENTS.md → Prompt first, GDD second). Numbers are the source game's, a starting point for tuning, never ours by default. (unverified) = no primary or reliable source found. Sources are at the end.

## 1. Readable and fair (every agent)

Every spec answers these (SKILL.md §0 step 3); check them in play, not only in the tree.

- **Every attack has a telegraph** longer than the player's reaction time. Stack channels: wind-up animation, particle charge-up, a bright flash just before it fires, a charge sound, a bark; a grounded game can stop at sound + animation + voice (Stout). A cue means one thing everywhere: if a red glow means "can't be blocked" on one enemy, it means that on all of them. Telegraphs are data on the attack, like damage and cooldown.
- **Damage matches how the attack looks.** A small, quick-looking swing never deals heavy damage.
- **Harder means faster or denser, never harder to read.** Raise difficulty by chaining attacks, tightening timing, shortening punish windows, raising speed or adding enemies; a telegraph never drops below reaction time and never disappears.
- **Soften what the player can't see.** Enemies strike less often while the player's back is turned (Dishonored 2), and attackers curve round to come from the front (Boomerang X). Attack tokens (building-blocks block 1) favour enemies on screen.
- **Senses are tuned to what the player believes the enemy can know**, not to realism: nothing notices the player from off-screen, through a wall or by a sound the player didn't hear themselves make. Use real perception and let enemies be fooled (Halo). Raiders that a service points at a target still show it: they look at it and head straight for it.
- **No hidden states.** Intent shows through posture, gesture, focus of attention and barks (Halo; F.E.A.R.'s enemies talking to each other was most of its perceived intelligence). Moving to suspicious, alert, fleeing or giving up gets a bark, sound, animation or icon. Avoid subtlety and anything that looks broken (Halo's own list).
- **Predictable, not repetitive.** Keep the numbers the player learns fixed per kind (detection range, reaction delay, speed, reach); vary only the presentation (animation, bark, small timing jitter). Simple AI is easier to track in a crowd (Ultrakill).
- **Tougher reads as smarter.** In Halo playtests weak enemies were rated "very intelligent" by 8% of players, tough ones by 43%. Spend scope on health, damage and clear reactions before clever logic.
- **Every agent can lose or give up** (§4 Pursuit). One that chases forever with no way to shake it reads as unfair.

## 2. The roster

- **One job per kind: it punishes one player habit.** Left 4 Dead's Hunter punishes stragglers, the Smoker tight groups, the Boomer "shoot everything". *Proposal:* thieves punish leaving animals and produce out, breakers punish neglecting the fence, hunters punish standing still or roaming alone; flyers and sneakers punish watching only the ground or only the front.
- **Put archetypes at the extremes** of health, damage and range so every group asks "who first, and with what weapon?" (Stout's Swarmer / Near / Far / Heavy: "an enemy that takes 10 hits to kill is MUCH different than one that takes 1, or 5"). Ours map as swarm = Swarmer, rusher = Near, ranged = Far, tank = Heavy; flyer and sneaker are ways of arriving, combined with one of those.
- **Scarce ammo is the question each mix asks.** Swarmers should die to melee, heavies are worth bullets, and weak points that remove an attack (Doom Eternal's Mancubus and Revenant cannons, Deep Rock Galactic's shoot-off armour) reward accuracy. *Proposal:* enemies as walking pickups (Doom 2016: glory kills always drop health, chainsaw kills drop ammo); a common round fodder drops, especially on melee kills, and a scarce premium tier saved for heavies (Metro 2033's military rounds double as money). RE4's drops weighted by what the player lacks (unverified).
- **Fodder is dangerous through a verb, not health** (Left 4 Dead: Infected that grab and slow make even one dangerous).
- **Counters come later.** An enemy built against a weapon or tactic (Doom Eternal's Carcass shield against rockets) only once the weapons it answers exist.
- **A personality and a breaking point per faction** are cheap flavour: Halo's Grunts flee easily, Elites take cover when hurt. *Proposal:* goblins scatter when their leader or a big one dies; robots never flee.
- **Silhouette and gait before colour.** At range players read silhouette and animation better than texture or colour (Valve, TF2), in the order side → class → weapon, with rim light to lift characters off the scene. At night that matters more: each archetype gets a distinct shape, size and walk.

## 3. Attacks, hits and feedback in first person

- **Show the danger where the player looks.** Ground markers or shadows for fast, area or overhead attacks (Keren), since in first person the attacker's body is often off-screen or behind.
- **Escalate pain reactions:** twitch → falter → stagger (Doom 2016). Stagger opens a punish or finisher window, and while the player is in it no other enemy starts an attack. Ties melee as a real fallback to `EnemyBody` hit reactions.
- **Stay findable.** Doom's demons prefer open spots near cover with a clear view of the player over hiding, so they keep attacking and the player always has a target.
- **Fair melee in a crowd:** attack tokens per attack type, released after each strike; the token pool is the cheapest difficulty knob (Doom 2016, analysed by Tommy Thompson). Breakers and thieves working on something else don't need a player token.

## 4. Survival threats: raiders, day wanderers, night

**Thieves** (Don't Starve's Krampus is the template)
- Take only what is loose (animals outside, drops on the ground); some storage is safe (Krampus can't open an ice box). Flee when the player comes near, never fight. Leave once nothing is in reach. Killing one returns everything it took; a warning sound comes before it appears.
- Being chased down is the fun: the fleeing carrier is a target the player wants to catch (Diablo's treasure goblin).
- A thief hit hard enough drops its load and flees, and may come back later (Raft's shark gives up after a few spear hits).
- Any grab on the player must be escapable alone: Left 4 Dead's pins need a teammate and don't transfer to a solo game.

**Breakers**
- Path to the target through what is cheapest to get through: 7 Days to Die's zombies break, climb or dig rather than walk round (cost comparison unverified). Doors and gates are the obvious first targets (Terraria goblins, Minecraft vindicators), which makes fence layout and upgrades a puzzle.
- A breach costs what it protected (Raft: what stands on a destroyed foundation is lost). *Proposal:* a broken fence segment opens a path into the paddock or yard until repaired.

**End of a raid** (*Proposal* for the midnight retreat)
- At the end time, survivors revert to their normal behaviour instead of vanishing (7 Days to Die at 04:00), or run for the nearest exit or darkness (Dying Light's Volatiles at dawn).
- Always back that with a hard timeout and a distance leash (Minecraft raids end after 40 min; raiders 112 blocks from the village leave). Despawn only out of sight.

**Pursuit limits** (*Proposal* for CORE → Daytime aggression, which is open)
- Chases escalate in levels, each more persistent (Dying Light pursuit 1–4, shown on the minimap). A pursuer gives up only after losing sight long enough, searches the last-known spot, then goes home; safe zones and dawn end it. Vision widens slightly right after a chase.
- A leashed enemy returns to its spawn and fully resets (World of Warcraft); territorial enemies drift back to camp (Zelda: Breath of the Wild, unverified).

**Night horror and light**
- Darkness kills on a timer with a warning: Don't Starve's Charlie strikes 5–10 s after full darkness (again every 5–11 s), after a spoken line and a whoosh; any light stops it.
- Light can forbid spawns outright (Minecraft: hostiles spawn only at block light 0), but protection is not invulnerability (Dying Light's Volatiles can still pounce into UV light; some Darkwood creatures are drawn to light).
- Mystery first, escalation later: The Forest's cannibals watch from a distance before they attack and grow bolder night by night.

**Tone**
- Keep the day truly safe so the night lands (Dredge: "We needed people to get relaxed"; the longer a threat stays unknown, the better). Daytime threats stay in the outer zone; dusk is the hand-off.

**Companions and livestock**
- Animals can distract attackers (Don't Starve's hounds go for nearby creatures and meat); some enemies fear pets (Minecraft skeletons flee wolves, unverified). Defenders fight once enemies cross the base boundary (Palworld). *Proposal:* thieves avoid the dog; loose animals pull raiders, so bringing them home at dusk is part of the defence.

## 5. Bosses and minibosses

**Structure**
- Teach, then test: Build-up → Reveal → Business as usual (basic attacks, any new mechanic taught safely) → Escalation → Midpoint → "It's ON!" (full moveset) → Kill → Victory (Stout).
- Each phase changes a rule or the arena, not only stats: Death's Gambit introduces a two-part boss's halves separately before combining them, and its harder versions add one twist instead of more health. *Proposal:* a later phase targets fences or animals, or calls adds.
- A breather before each spike; difficulty peaks mid-fight and eases into a predictable finish; don't pad length (Keren). Every boss has one standout feature (Death's Gambit).
- In a shooter, damage windows beat one long health soak: Destiny alternates damage phases with phases where other targets matter. That suits a night-long Blood Moon: the miniboss threatens in cycles and the waves fill the gaps.

**Picking attacks**
- Weighted random, the same move at most twice in a row, then excluded for a while (Bleed 2). Interleave categories (moving and stationary, close / mid / far), but "you can definitely go too far" with rules: it turns predictable.
- Fairness rules on the pick: at least one move threatens a player who stands still, some picks expose the weak point (Bleed 2). Each attack hints at the next; pure randomness feels helpless (Keren).

**Telegraph and punish**
- The unit is a maneuver: telegraph → attack → recovery, chained (Psychonauts 2). The recovery is the punish window; the telegraph can be one too (Doom Eternal's Marauder is staggerable while its eyes flash, fan wiki).
- Weak points are signposted: glow, flash, a counter of targets left, a phase indicator (Keren). One buff and one debuff active at a time, information shown in the world (Destiny).
- The boss can threaten the player anywhere; geometry never makes them safe (Destiny). Early bosses use simple attacks with easy tells, and the player always sees that hits land (Monster Hunter).

**Giant bosses in first person**
- Break a giant into armour targets on its limbs, then a second pass on the exposed body, with adds spawning and ammo, health and big-weapon pickups placed in the arena (Doom Eternal's Icon of Sin). Or cycle armour → exposed weak points → re-armour plus adds (Deep Rock Galactic's Dreadnoughts).
- Design the creature with its arena (Shadow of the Colossus). Here the arena is the farm: plan the cover, sight lines and what the boss can wreck.
- Plain upscaling looks cheap: id dropped giant-sized Quake monsters because they "looked terrible" (partly verified). "Body + scale" (IDEAS.md) needs a unique silhouette part or VFX per giant, not just a scale value.

**Minibosses and elites**
- Elite affixes make minibosses out of regular enemies (Risk of Rain 2: Blazing, Overloading, Glacial, Mending; Diablo champion packs share affixes). *Proposal:* Blood Moon minibosses as affixed regular kinds, an affix as data (a stat modifier set plus a visual and one behaviour hook) on `EnemyStats`.
- A pack of elites can stand in for the boss, under the same boss health bar (Risk of Rain 2's Horde of Many).

**Cheap production**
- Data-driven maneuvers reused across bosses and tuned per phase: Psychonauts 2 had two months to concept and prototype two bosses. Few shared skeletons (Monster Hunter). Expect several redesign passes and cut confusing mechanics rather than patch them (Death's Gambit, Destiny).

## Sources

Design: Mike Stout, gamedeveloper.com → "Trinity part 5: setups", "Boss battle design and structure", "Enemy attacks and telegraphing"; Itay Keren, "Boss Up" (GDC 2018; gcores.com/articles/103804); Unity blog, "How enemy AI works in video games".
Talks and papers: Valve, "Replayable Cooperative Game Design: Left 4 Dead" (GDC 2009 slides); Bungie, "The Illusion of Intelligence" (Halo, GDC 2002); Valve, "Illustrative Rendering in Team Fortress 2" (NPAR 2007); "Push-forward combat" Doom (GDC 2018); Rebecca Vessal, Psychonauts 2 modular maneuvers (gamedeveloper.com).
Articles and interviews: gamedeveloper.com → the AI of Doom 2016, Doom Eternal's aggressive resource management, Death's Gambit boss design, Dredge's unseen; pcgamesn.com Doom Eternal demons; 80.lv Ultrakill; automaton-media.com (Dishonored 2 / Boomerang X off-screen attacks); thesixthaxis.com Metro 2033; gameinformer.com Destiny raid design; mein-mmo.de Destiny 2 bosses; vice.com Monster Hunter monsters; glitterberri.com Shadow of the Colossus; bootdiskrevolution.com Bleed 2 boss AI; gamingbolt.com The Forest; devtrackers.gg Vermintide 2 audio cues.
Wikis (exact rules): dontstarve.wiki.gg (Krampus, Darkness, Periodic hound attacks); 7daystodie.wiki.gg (Blood Moon Horde, Heatmap); minecraft.wiki (Raid, Light); terraria.wiki.gg (Goblin Army); dyinglight.wiki.gg (Night); warcraft.wiki.gg (Leash); raft.fandom.com (Threats); riskofrain2.wiki.gg (Elites, Teleporter); doomwiki.org (Marauder, Final Sin); deeprockgalactic.wiki.gg (Update 12); palworld.wiki.gg (Base Raids).
