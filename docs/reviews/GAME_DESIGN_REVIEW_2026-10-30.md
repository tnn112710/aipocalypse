# Game design review — 2026-10-30

Review of [CORE.md](../gdd/CORE.md), [FARM_AND_ECONOMY.md](../gdd/FARM_AND_ECONOMY.md), the current and historical sections of [IDEAS.md](../gdd/IDEAS.md), and the [roadmap](../ROADMAP.md). Clock and combat observations use the technical docs, not a new playtest or an inspection of live tuning.

**Status: review findings and proposals, not additional approved game rules.** No gameplay changes are authorized by this document alone. P0 means decide before specifying the complete loop; P1 means a significant experience risk to test; P2 means scope or document cleanup.

**Subsequent decisions, 2026-10-30:** Quiet nights are confirmed. The major-event calendar is fixed: Blood Moons on days 10, 20, 40, 50, and so on; eclipses replace them on days 30, 60, and so on. The review below predates those choices; its questions about whether quiet nights exist and major-event dates are resolved. Ordinary raid/rest distribution and event recovery periods remain open. See the current [calendar](../gdd/CORE.md#major-event-calendar).

## Overall assessment

The revised loop is coherent: build a livelihood, choose local work or exploration, prepare before dark, defend the home, and reinvest. The bridge, physical produce, cute service robots and enemies with theft/attack objectives give it a recognizable identity.

The primary risk is the combined workload and penalties. Produce collection, animal gathering, crops, cooking, inventory, travel and repairs could consume the day. Nightly defense then becomes an expense that sends the player back to the same maintenance tasks. Whether this happens depends on rules and tuning that are still open.

Keep the recent decisions intact: no player biome portals, outer-zone map design deferred, ordinary raids stop with a retreat at midnight, sales pay immediately, and animals drop physical products with faster production while freely grazing. The review below improves the interactions around those choices.

## P0 — rules needed for a complete playable loop

### 1. Failure, theft and player absence need one consistent contract

**Evidence:** [night rules](../gdd/CORE.md#night--special-events) leave attacks while away open. [Failure](../gdd/CORE.md#resources--failure) retains an older 50–70% grave-loss proposal, while animal loss and rescue remain undecided.

**Risk:** A player cannot assess an expedition without knowing what happens at home. Cancelling raids while away rewards leaving the farm; uncontrolled losses while away can make night exploration unacceptable. Death, lost animals, depleted ammunition, repair costs and another harder night could reinforce one another.

**Proposal:** Keep the safe stash and permanent progression. Define an ordinary-defense loss as a bounded setback with a feasible next day. State what happens to the current raid and clock after death, whether the player can re-enter the same defense, what home defenses accomplish without the player, and what thieves can actually take. Recoverable stolen animals are one candidate, not a settled rule.

Specify grave contents and expiry separately from animal rescue. The old portal-reroll expiry has no role in the new outer zone. Also check whether retained carried loot makes deliberate death a profitable shortcut home.

**Decision test:** After one bad defense and one failed recovery attempt, can the player identify a practical route back to stability?

### 2. The day needs a time budget before the outer zone is sized

**Evidence:** [Clock defaults](../technicals/DAYNIGHT.md#clock) document 30 real seconds per game hour: 06:00–18:00 is six minutes, 18:00–00:00 is three minutes, and the full cycle is twelve minutes. These are prototype defaults, not confirmed final pacing.

**Risk:** A large map, daily return deadline and growing farm can compete for the same small window. For illustration, an outing with two minutes of outward travel and two minutes of return travel would leave only two minutes for all exploration and farm work within that documented daylight window.

**Proposal:** Budget daylight across routine home work, selected activity, return travel and dusk preparation. Establish the first destination and its return margin before expanding the map. Let later shortcuts, better tools and automation buy usable time. Keep routine collection and gathering brief even as the farm improves.

Choose whether trading, inventory and planning menus pause the simulation. Real time at home need not automatically imply spending daylight while reading prices. Specify sleep eligibility, time advancement and production during skipped time together.

**Decision test:** Can a new player complete one interesting outing and prepare for defense without knowing the route perfectly or rushing every interaction?

### 3. The economy still lacks a reason to alternate activities

**Evidence:** [Economic roles](../gdd/FARM_AND_ECONOMY.md#balance-proposals--to-validate) are proposals. Stock, prices, upgrade requirements and rewards remain open. Immediate sale payment is confirmed; purchase delivery timing is not.

**Risk:** If coins buy everything, safe production may replace exploration. If defense consistently consumes all earnings, farming becomes compulsory replenishment. If advanced progression depends exclusively on combat drops, crops and fishing can become interchangeable busywork. Delayed essential deliveries could also defeat the reason for immediate payment.

**Proposal:** Give home production a dependable role in supplies, grazing a worthwhile surplus, fishing an active way to earn more, and exploration discoveries or components that matter for advancement. A successful ordinary cycle should leave useful progress after reasonable ammunition and repairs.

Recommend immediate availability of purchased essentials; reserve delivery delays for larger optional orders if desired. Start with understandable prices and worthwhile raw sales. Establish a basic food and ammunition recovery route before any major event.

The first bridge repair must be achievable before crossing the river. Repairing it should not unexpectedly add a second full raid on top of the existing portal attacks: crossing routes can share an overall threat budget.

**Decision test:** Can the player explain why they might choose farming, fishing or exploration today, and what improvement a successful day brings closer?

## P1 — significant gameplay risks

### 4. Physical produce needs collection rules that scale

**Evidence:** [Animal production](../gdd/FARM_AND_ECONOMY.md#farm-and-meadow) confirms drops near animals, faster production in the meadow and later dog-assisted gathering. Product persistence and uncollected production limits are open.

**Risk:** Faster grazing also means more scattered objects. A larger herd can increase search and pickup work faster than it increases enjoyment. The dog gathers animals, so it does not automatically solve the product-collection workload.

**Proposal:** Preserve physical drops. Test readable pickup shapes in grass, nearby same-product stacks, a convenient collection interaction, stable resting positions and explicit persistence rules. Avoid eggs rolling into inaccessible water or silently expiring because the player chose an expedition. Keep starting herd scale modest and make later collection help a distinct upgrade if needed.

Define what grants the grazing bonus: location, time outdoors or unsecured freedom. If a player fences off a piece of meadow, decide whether retaining the bonus is an intended earned improvement. Do not let the answer emerge accidentally from inconsistent zone rules.

**Decision test:** Does adding animals create a useful investment choice while keeping collection satisfying and manageable?

### 5. Portals must respect the purpose of fences and preparation

**Evidence:** [Night approaches](../gdd/CORE.md#night--special-events) include both the outer zone and attack portals, but their positions and warnings are open.

**Risk:** A portal appearing inside the defended yard can bypass the investment the game asks the player to make. Several unexpected approaches can make first-person defense feel like losing assets outside the field of view. Unwarned extra attackers after repairing the bridge can make progression feel like a punishment.

**Proposal:** Start with a readable front. Make ordinary portal approaches visible and outside the protected area, with enough warning for interception. Clearly announce any special event that changes those assumptions. A strong bridge position should work; later enemy objectives and signaled approaches can encourage movement.

Keep midnight retreat. Define what retreating thieves do with captured animals, how their escape remains readable, and when the farm is considered safe. Surviving through good positioning or good fences can be legitimate success; shooting should remain worthwhile through threat removal and rewards rather than arbitrary kill requirements.

**Decision test:** When an animal is lost or a gate breaks, can the player identify the warning and a plausible action that would have helped?

### 6. Combat currently has several penalties pulling in the same direction

**Evidence:** [Weapon rules](../technicals/WEAPONS.md#fatigue) document stamina spent on shots and swings, recovery while standing idle, and fatigue that raises recoil while reducing movement and melee strength. The lowest documented tier gives 2.5 times recoil and 40% melee damage/knockback, with additional winded effects. [CORE.md](../gdd/CORE.md#player-combat--progression) also retains weak starting handling and jams.

**Risk:** Moving, shooting, pursuing a thief and switching to melee can all worsen together. Melee is described as the ammunition fallback, yet the player may reach that fallback already exhausted from shooting. Severe sanity effects could add another layer of reduced information and control.

**Proposal:** Test stamina recovery while walking or reloading, retaining costs for sprinting and attacks. Consider fewer overlapping low-stamina penalties. Keep the starting gun satisfying, and make jams infrequent, understandable interruptions rather than the normal texture of combat. Sanity can create threats and atmosphere while preserving usable warning cues.

These are proposed changes to documented behavior, not conclusions from a new combat playtest. Existing animations and QTE infrastructure do not need to be removed to test different tuning and frequency.

**Decision test:** Does a struggling player retain at least one effective action: reposition, recover, use melee, protect an animal or retreat?

### 7. Mandatory defense can turn varied days into the same appointment

**Evidence:** The loop requires preparation and defense; quiet-night frequency, rewards and event schedules remain open.

**Risk:** If every evening repeats the same hold-position fight, daytime choices only change how well supplied the player is. Farm losses create repairs that consume the next day, further reducing variety. Staying away becomes either an exploit or an unacceptable risk unless the home rules support it.

**Proposal:** Vary a small set of readable objectives and enemy combinations. Sometimes the important decision is stopping a thief, sometimes holding a gate, sometimes choosing when to fall back. Keep repair work consolidated and quick. Decide whether quiet nights exist and whether forecasts let players plan around them.

For Blood Moons and eclipses, distinguish the full event window from continuous shooting. Surges and useful breaks can fill a whole night or day. Guarantee preparation and recovery opportunities instead of stacking major events unpredictably.

Valve's Left 4 Dead presentation describes buildup, peaks, fade and relaxation as a pacing structure. It supports testing breaks between pressure; it does not establish exact timings for this game.

### 8. Long-term purpose is still inherited from a different structure

**Evidence:** [Endings](../gdd/CORE.md#endings--retained-direction-to-revisit) retain relics, a separate grand trial, a lottery ending and a previous 30+ day target. Player biome portals are removed; Blood Moons and eclipses now provide major-event directions.

**Risk:** Improving the farm and surviving increasingly difficult raids may eventually become growth without a destination. A separate grand-trial mode adds another combat format and progression structure before the main one is established.

**Proposal:** Sketch a small progression chain: secure the home, repair the bridge, complete an outer-zone objective, earn an improvement, handle a major event, and unlock a new goal. Keep the outer map's full design deferred while defining the first destination and its purpose.

Consider making a later eclipse the campaign's final major encounter. Keep the existing story twist available; defer the separate trial, lottery ending and exact campaign length until they serve a clear purpose.

**Decision test:** After a successful day, does the player have a specific goal to anticipate beyond earning a larger number?

## P2 — redundant and obsolete material to simplify

| Material | Assessment | Recommendation |
| --- | --- | --- |
| Old player portal pool, one-way extraction exits and sleep-deleted biome graves | Explicitly superseded, preserved in historical notes | Keep only in a separate archive; do not revive their constraints in the current loop |
| Fire tiers, no-fire safety and the older ban on portal attacks | Already replaced by current decisions | Keep fire and light only for their newly chosen purposes |
| A completely peaceful full progression path | Replaced by scheduled defense | State clearly what safe local work means without promising defense-free progression |
| CORE's detailed history and repeated farm/trade rules | Current status is labelled, but mixed with the working specification | Keep CORE concise; use the economy topic as the detailed owner; place legacy reasoning in history |
| IDEAS.md's historical Promising and Rejected headings | Labelled historical, but still look authoritative when found by search | Move the snapshot to a dated archive and keep a curated ideas inbox |
| Repeated open-question tables across files | The same decision can drift across several descriptions | Maintain one prioritized decision queue and link to it from summaries |
| Fitness, Gunsmanship, Luck, gear, farm upgrades, dog training and temporary cards | Several overlapping progression layers | First prove gear/home improvements; define a distinct job for each additional layer before committing |
| Luck affecting loot, crits, fishing, jams and a lottery | Can become the broadly strongest investment and adds hidden variability | Defer it or give it narrower, visible effects |
| Hunger, stamina and sanity maintenance | Each needs a distinct gameplay purpose | Keep food useful; test whether hunger adds decisions beyond repeatedly replenishing a meter |
| Separate upgrade/storage services | Could duplicate gear progression or the existing safe stash | Make their added functions explicit; storage upgrades should improve the established storage model |
| Large minigame catalog, daily patch notes, lottery and many enemy themes | Adds scope without proving the daily loop | Keep as optional ideas outside the first playable |

No need to remove animal drops, the broken bridge, immediate trading or midnight retreat to address these issues.

## Recommended next design decisions

1. **Failure and absence:** what death does to a defense; what home defenses do while the player is away; what stolen animals cost and how recovery works.
2. **Time:** daylight budget, menu pauses, sleep eligibility and the first outing's return margin.
3. **Economy:** dependable supplies, purchase delivery timing, basic recovery and what exploration unlocks that coins alone cannot replace.
4. **Defense fairness:** portal boundaries, warnings, retreating thieves and whether quiet nights exist.
5. **Progression:** the first few milestones and one major-event reward before specifying all skill trees, card pools or campaign endings.

Rates, prices and exact event intervals should follow those decisions and a small playable test, rather than become a large untested balance table now.

## Smallest useful playtest

Use one animal type, physical produce, one trading service, one repairable gate, a short outing after the bridge, and one theft/attack encounter. Respect the roadmap's existing performance gates before expanding enemy archetypes. A normal cycle and a recovery cycle are more informative than adding several species and enemy skins.

Observe whether the player:

- Has enough time for an interesting chosen activity after routine preparation.
- Understands the grazing benefit and can collect products without repeated searching.
- Changes plans based on visible threats and current resources.
- Moves and makes priorities during defense instead of simply waiting for midnight.
- Understands losses and can recover without a long grind.
- Wants another day because of a concrete goal.

The roadmap currently emphasizes separate movement, shooting, equipment, enemies, fishing and crafting prototypes. It does not yet identify this integrated farm/trade/defense cycle as a named milestone. Add that milestone when implementation priorities are chosen; do not replace completed foundation work.

## Research used for review principles

- [Michael Booth, Valve — The AI Systems of Left 4 Dead](https://steamcdn-a.akamaihd.net/apps/valve/2009/ai_systems_of_l4d_mike_booth.pdf), particularly the pacing discussion on slides 77–80 and 91: vary pressure with opportunities for a natural break.
- [EXOR Studios — Design is All About Choices](https://www.exorstudios.com/blog/rb-design-choices): introduce mechanics with controlled pressure, and judge combinations through iteration. This supports a small integrated test; it does not prove that this proposed economy will be fun.
