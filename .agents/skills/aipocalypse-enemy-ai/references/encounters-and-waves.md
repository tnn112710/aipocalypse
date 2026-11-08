# Encounters, waves and difficulty

How enemies combine: a fight's mix, a raid night's shape, where and how enemies spawn, how new kinds are introduced and how difficulty grows over days. Single-enemy design (jobs, telegraphs, thieves, bosses) is in [enemy-design.md](enemy-design.md); the raid director that runs all this is [building-blocks.md](building-blocks.md) block 2.

Research done 2026-10-31. **Nothing here is approved for implementation**; raid rules come from CORE.md and are proposed before they're built (AGENTS.md → Prompt first, GDD second). *Proposal* marks a mapping onto our game; numbers are the source game's starting points. Level Up! rules come from the `game-design` skill's notes on the book; (unverified) = from summaries or memory. Sources are at the end.

## 1. A spec sheet per enemy kind (Level Up!)

Rogers' general rules (enemy attributes, behaviour vocabulary, encounters, bosses) are in the user-scope `game-design` skill: `references/enemies.md`, `encounters.md` and `bosses.md`, notes from the book itself with printed page numbers. This section only maps them onto aipocalypse and stands on its own where that skill isn't installed.

- *Proposal:* a kind's design sheet is the tree spec's header answers (SKILL.md §0 step 3) plus his attributes: size, behaviour, speed relative to the player, movement style, attack. The numbers live in its `EnemyDefinition`.
- His behaviour types (patroller, chaser, shooter, guard, bomber, burrower…) map onto ours: thieves are reverse guards (take a thing, then flee), breakers are chasers whose target is a structure, flyers are bombers.
- Pin the player with one enemy and punish from another axis: ground rushers plus a flyer, or hunters on the player while thieves work the paddock.
- Narrow places carry the danger, wide ones are for exploring (also his Disneyland talk): the yard gate and fence gaps are ours.

## 2. Composing a fight

- **Mix jobs so the player must triage.** Each kind punishes one habit (Left 4 Dead's specials); put objective attackers (thieves, breakers) and player hunters in the same wave so defending one means leaving the other.
- **Mix extremes** (Stout's Swarmer / Near / Far / Heavy): fights are words made of these letters, e.g. rushers on open ground while ranged ones hold a ridge, or swarmers screening a heavy across the river. Every group should ask "who first, and with what?"
- **Fake scale.** "You're always threatened, but rarely seriously so"; the Two Towers battle feels huge but is a few enemies at a time (Rogers). With attack tokens (building-blocks block 1) a big crowd shows at the edges while a few engage, which also keeps us under the 50-alive cap (usually 20–30).
- **Layer the population** (Left 4 Dead): wanderers that ignore the player until alerted, mobs of 20–30, periodic specials, bosses. *Proposal:* day wanderers, raid fodder, raid specials (thieves, flyers, sneakers), Blood Moon / Eclipse bosses.
- **Territory and fallback, not a fight to the last enemy** (Halo): a group holds ground, falls back to a second line, makes a last stand and then breaks; Halo 2 triggered the retreats at under 75% and under 25% alive. *Proposal:* a raid group that loses most of its members routs back to its route's exit, which gives the midnight retreat an earlier beat too.
- **Targets with capacities** (Halo 3 Objectives: prioritised tasks such as "guard door" over "guard hallway", each with a capacity, that squads fill in order). *Proposal:* the raid director lists targets (paddock gate, loose animals, fence segments, the player) with a priority and capacity, and assigns spawned enemies to them; trees only execute.
- **Design owns the three-minute layer, AI the thirty-second one** (Halo): where a fight happens, battle lines and retreat conditions are data the designer sets; reactions are the tree's.
- **Structured unpredictability:** set up many possible moments knowing only some will fire; "low probability + high drama = memorable" (Left 4 Dead).

## 3. A raid night

- **Warn in stages that build through the day.** 7 Days to Die: day number turns red at 08:00, thunder and a reddening sky at 18:00, fully red at 21:00, a scream starts the horde at 22:00, clear at 04:00. Don't Starve: a growl that grows louder, with warnings shortening from 120 s (days 1–10) to 30 s (day 51+). Minecraft: a raid bar while the bell rings, a horn for each wave. Terraria: "approaching from the west", then "has arrived", with a progress bar. They Are Billions: announced hours ahead with only a general direction. *Proposal:* the morning tells the night's kind (CORE already says so); dusk changes sky and sound; each wave opens with a horn or call from its route's side; a big wave is flagged ahead (Plants vs Zombies puts flags on the progress bar, in the middle and at the end).
- **Pace by intensity** (Left 4 Dead, from Valve's slides): intensity rises with damage taken and nearby kills and decays only while no enemy is engaging. States: Build Up → Sustain Peak (full threat for 3–5 s after intensity peaks) → Peak Fade (minimal population, waits for a natural break) → Relax (30–45 s: no new mobs or specials, existing ones may still attack). Bosses ignore the pacing. A Relax is when the player reloads, repairs a fence and herds an animal.
- **A low trickle between bursts** keeps the night alive (Deep Rock Galactic: small pressure waves every 10–15 s between announced swarms).
- **Waves as slots** (7 Days to Die `gamestages.xml`): each wave has a total count, an alive cap per player, a duration and a spawn interval; a last wave with count 999 keeps spawning until the night ends. *Proposal:* ordinary raids end with a finite budget and the midnight cut; Blood Moon and Eclipse end on the open tail.
- **Finite and capped.** A total for the night and a maximum alive (7 Days to Die: about 8 per player by default); when the last wave is cleared nothing else spawns. The end of a raid and the retreat: [enemy-design.md](enemy-design.md) §4.
- **Whole-night events have a fixed window, an omen and no skipping** (Terraria's Blood Moon 19:30–04:30, red sky and water, can't sleep through it). Minibosses join by wave and are capped (Pumpkin Moon: Mourning Wood from wave 6, at most 2; Pumpkin King from wave 10). A day event grows its roster with progress (Solar Eclipse). A little schedule jitter is an option (7 Days to Die's range setting).

## 4. Spawning and formations

- **Spawn where the player can't see**, at the edge of the play space or ahead along the route, never in view or close behind (attributed to Left 4 Dead's director, unverified). Despawn only out of sight too.
- **Spawn by route, announced but not exact** (They Are Billions gives the direction; terrain can split a wave so it hits from two sides). *Proposal:* each route (river crossings, sky, inside the meadow) has spawn points, a weight per wave and its own audio cue.
- **Formations** (*Proposal*: the sources name the effect, not a system): trickle (a few at a time on one route), burst (a group at once, telegraphed), pincer (two routes at once, the second a few seconds later so the player turns and hears it), surround (only for in-meadow appearances, after a visible and audible warm-up, never at melee range). One formation per wave is plenty.
- **Spawns inside the meadow are telegraphed** like attacks (enemy-design §1): a portal or summoning effect that builds before anything steps out.

## 5. Introducing new enemies

- **Introduce → develop → twist → conclude** (Koichi Hayashida, Nintendo): learn it safely, a slightly harder case, something unexpected, then show mastery. *Proposal:* a new kind debuts alone in a small raid, appears mixed with known kinds in the next, gets a twist later (a new route, a pairing, an elite), then becomes a staple.
- **One new thing at a time, and never in the first attacks**: Don't Starve's special hound variants have a 0% chance before day 17. Each new kind also gives a 6–9 month scope a content cadence.
- **Foreshadow** ("expectation enhances surprise", Rogers): the morning announcement or the drone can name a newcomer before it appears.

## 6. Difficulty over the days

- **Shape: rising peaks with rests** (Schell's interest curve, secondary summary). *Proposal:* the calendar already gives that: raids grow, quiet nights rest, a Blood Moon every 10th day and the Eclipse on the 30th are the peaks.
- **Which levers, in order** (*Proposal*, from the sources below): budget and alive count; mix (more costly cards, new kinds); aggression (more attack tokens, shorter cooldowns); routes open at once; elites; per-enemy health and damage last. With scarce ammo, health scaling multiplies the ammo every kill costs, so composition and aggression come first (Doom scales tokens with difficulty; Vermintide grows hordes from team performance). Tougher still reads as smarter (enemy-design §1), so a heavier mix beats bigger health bars.
- **Credits buy composition** (Risk of Rain 2): the director earns credits over time and buys weighted cards it can afford; a "too cheap" rule refuses a cheap card once credits exceed six times an elite's cost (unless it's the dearest option), so late raids buy heavies and elites instead of hundreds of fodder.
- **Use difficulty to discourage boring tactics** (Halo's "negative reinforcement"): if camping one spot wins, the next raid sends what punishes it (a flyer, a breaker on the far side).
- **Exact formulas from shipped games** (reference, not ours):
  - Risk of Rain 2: `coeff = (1 + 0.3·(players−1) + minutes·0.0506·diff·players^0.2) · 1.15^stagesCompleted`; credits per second `= mult·(1 + 0.4·coeff)·(players+1)/2`; boss director gets `600·√coeff` up front; elite cost ×6 (tier 1) or ×36; map cap 40 monsters (bosses bypass); enemy level `= 1 + (coeff − playerFactor)/0.33`, each level +30% health and +20% damage.
  - 7 Days to Die: `gamestage = (playerLevel + daysAlive)·1.2`, days alive reduced by 2 per death and capped at the player level; the horde uses the highest ladder rung at or below it, fixed for the night.
  - Brotato endless: `w = max(0, wave−20)`, `mult = 2 + max(0, (wave−35)·0.2)`, `factor = w(w+1)/200·mult`; health ×(1 + 2.25·factor), damage ×(1 + factor); one more elite per 10 waves.
  - Vampire Survivors: one wave per minute with a minimum alive quota refilled at an interval; above 300 alive only bosses and events spawn.
- **Adaptive difficulty** stays inside a night (Left 4 Dead's intensity), not across days, unless the user asks for it (RE4's hidden adjustment exists; its rules are unverified).

## 7. Raid data, a proposed shape

Fields for when the raid director is built (data in `Data/Enemies/` or per map, read-only at runtime like every definition asset):

- **Raid night:** kind (normal / Blood Moon / Eclipse), budget from a curve over days, credit rate, alive cap (≤ the perf gate), attack tokens per kind (player melee, player ranged, structures), routes open, end rule (midnight cut or open tail), rout thresholds, phases, morning announcement.
- **Phase:** start, duration or end condition, share of the budget, pacing state, route weights, role weights (thief / breaker / hunter), required cards (debuts, minibosses), formation, telegraph cue.
- **Enemy card:** enemy definition, cost, weight, role, archetype, allowed routes, first day, debut day, group size range, max alive, elite tiers and their cost multiplier, ammo drop.
- **Director state** (runtime, in the service): credits, intensity, pacing state, alive count, spent, losses.

## Sources

Books and talks: Scott Rogers, *Level Up! The Guide to Great Video Game Design* (Wiley 2010; notes in the user-scope `game-design` skill) and "Everything I Learned About Level Design I Learned from Disneyland" (GDC 2009; notes at craphound.com/disneylandleveldesign.txt); Jesse Schell, *The Art of Game Design* (interest curves); Valve, "Replayable Cooperative Game Design: Left 4 Dead" (GDC 2009 slides); Bungie, "The Illusion of Intelligence" (Halo, GDC 2002) and Halo 3 Objectives (web.cs.wpi.edu/~rich/courses/imgd4000-d09/lectures/halo3.pdf); Mike Stout, "Trinity part 5: setups" (gamedeveloper.com).
Articles: gamedeveloper.com → "The secret to Mario level design" (Hayashida), the AI of Doom 2016; pcgamesn.com Vermintide 2 enemy guide.
Wikis (exact rules): 7daystodie.wiki.gg (Blood Moon Horde, Gamestage), 7d2dmodding.wiki.gg (Gamestages.xml), dontstarve.wiki.gg (Periodic hound attacks), minecraft.wiki (Raid), terraria.wiki.gg (Blood Moon, Pumpkin Moon, Solar Eclipse, Goblin Army), riskofrain2.wiki.gg (Difficulty, Directors, Elites), deeprockgalactic.wiki.gg (Swarm), brotato.wiki.spellsandguns.com (Endless Mode), they-are-billions.fandom.com; Plants vs. Zombies (Wikipedia).
