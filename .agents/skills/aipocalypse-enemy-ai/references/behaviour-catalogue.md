# Behaviour catalogue: what the GDD asks of AI agents

Every agent behaviour the design mentions, read from `docs/gdd/` on 2026-10-31, with where it comes from and the building blocks it needs. **Nothing here is approved for implementation.** Each behaviour is proposed and confirmed with the user before it is built (AGENTS.md → Prompt first, GDD second), and the GDD moves: re-read the cited section before relying on a row.

Source tags: **Canon** = `CORE.md` / `FARM_AND_ECONOMY.md`; **Idea** = `IDEAS.md` (draft, never build from it unless asked); **Open** = the GDD names it as undecided. Building blocks are in [building-blocks.md](building-blocks.md); design guidance per family (thieves, breakers, raid end, pursuit limits, night, bosses) is in [enemy-design.md](enemy-design.md) and for raids, waves and difficulty in [encounters-and-waves.md](encounters-and-waves.md).

## Agent families

| Family | Who | Count at once (estimate) | Agent type (unity-behavior-designer-pro §1) |
| --- | --- | --- | --- |
| Raiders | goblins, robots, zombies, flyers; thieves vs attackers | raids: usually 20–30, at most 50 alive | Crowd: Manual trees ticked by `EnemyManager` |
| Outer-zone threats | wandering enemies by day, "extremely dangerous" ones and mystical entities after dark | tens | Crowd, same system |
| Minibosses / bosses | Blood Moon miniboss, eclipse giant boss | 1–3 | Unique: own `EntityScope`, ticked every frame or by their own system |
| Farm animals | chicken, cow, sheep, pig (roster open) | ~5–30 | Crowd-like (own manager, low think rate) |
| Wildlife | snakes, predators (Idea) | few | Crowd |
| Companions | dog, guide drone, cat (extra) | 1–3 | Unique |
| Service robots | trader, deposit, daily box; collector robot later | 1–4 | Unique; mostly stationary |

## Raiders (night)

| Behaviour | Source | Building blocks |
| --- | --- | --- |
| Raid nights only; the mix and size grow over time; first raid small and mostly thieves | Canon: CORE → Raid schedule | Raid director (spawn budget, mix table), spawning through the resolver, pooling |
| Arrive by three routes: cross the river freely (some use the bridge), fly in, appear inside the meadow (summoned / portals). The bridge is not a chokepoint | Canon: CORE → Raid and safety rules | Spawn points per route; A\* river tag walkable at night only (Idea: IDEAS → raids, Tech); a flying mover (no navmesh) |
| **Thieves** steal animals and produce left outside, then carry them off into the outer zone | Canon: CORE, FARM → Farm and meadow | Target registry (animals, drops), steal/carry action, flee-to-exit, animal "carried" state |
| A thief carrying an animal is slow, can't fight, drops it when shot | Idea: IDEAS → raids | Carry modifier on `EnemyStats`, drop on damage |
| **Attackers** go for the player; broken fences let threats into the yard | Canon: CORE → Raid and safety rules | Target choice (player vs structure), fence/barricade as damageable targets, path through breaches |
| Three jobs: thieves (goblins), breakers (robots → fences), hunters (zombies → player) | Idea: IDEAS → raids | Role = subtree + target filter; one tree per role, archetype as data |
| Archetypes: rusher, tank, ranged, flyer, swarm, sneaker (enemy = archetype + skin) | Idea: IDEAS → Combat & enemies | Shared subtrees per archetype; skins as prefab variants |
| At midnight new attacks stop and surviving raiders **retreat** (Blood Moon: fight until dawn) | Canon: CORE → Loop rules 8, Night table | Raid-end event routed to every raider → retreat subtree (exit point, despawn) |
| Raid resolves without the player when away: animals outside stolen, fences damaged by raid size | Canon: CORE → Raid and safety rules | Not AI: an abstract resolution step, no simulated agents |
| Drop loot and ammunition on death | Canon: CORE → Player, combat & progression | Death hook in `EnemyBody` → loot table |
| Sky Invaders raid: formation side to side, step down, speed up, landed invaders abduct animals | Idea: IDEAS → raids | Formation controller (one owner moves the group), not per-agent trees |
| Fishing up an enemy starts a fight on the bank | Idea: IDEAS → Fishing up enemies | Spawn an existing enemy at a point, aggro on the player |

## Outer zone and daytime

| Behaviour | Source | Building blocks |
| --- | --- | --- |
| Wandering enemies by day; they stay in the outer zone unless chasing the player | Canon: CORE → Loop rule 1, Daytime aggression | Home area per enemy (`EnemyMover.Home`), zone tag, perception |
| A chaser can follow across the bridge; pursuit limits and what it does in the meadow are **Open** | Canon: CORE → Daytime aggression | Leash / give-up rule, chase memory |
| After dark: extremely dangerous enemies, mystical entities, darkness drains sanity; light protects against the Dark | Canon: CORE → Night table; entity behaviour Open | Light-aware conditionals (avoid lit areas), night-only spawns |
| Stolen animals held deep in the outer zone for 3 days; camps designed later | Canon: CORE → Raid and safety rules; camps Open | Guard / camp behaviours, freed animal follows the player home |

## Minibosses and bosses

| Behaviour | Source | Building blocks |
| --- | --- | --- |
| Blood Moon: full-night miniboss event every 10th day | Canon: CORE → Major-event calendar; phases/warnings Open | Unique agent, phases, attack picker |
| Eclipse: full-day event, more enemies and one giant boss arriving around midday (Idea) | Canon: CORE; details Open | Boss + director wave together |
| Boss = body + scale + weapon + archetype from packs | Idea: IDEAS → AI-prompted bosses | Data-driven attack set on a shared boss tree |
| Mothership boss for Sky Invaders | Idea: IDEAS → raids | Formation controller + boss phases |

## Farm animals

| Behaviour | Source | Building blocks |
| --- | --- | --- |
| Stay in the paddock or roam the meadow; roaming produces faster | Canon: FARM → Farm and meadow | Wander inside an area (paddock / meadow polygon), area flag read by production |
| Produce physical drops near themselves; stop while uncollected drops are at a cap | Canon: FARM → Animal products | Not AI: a production timer per animal, ticked centrally |
| Sleep at night, produce nothing while asleep; on quiet nights left-out animals sleep where they are | Canon: FARM, CORE → Loop rule 3 | Day-phase event → sleep subtree |
| Bell: walk home slowly on their own; interacting makes one hurry | Canon: CORE → Loop rule 5 | Bell event → go-home subtree, speed modifier on interact |
| Can be stolen (carried) if outside during a raid; recoverable for 3 days | Canon: CORE | Carried state (tree paused, attached to the thief), rescued → follow player |
| Dog later helps bring them home | Canon: CORE, FARM | Herding: dog pushes, animals flee from the dog toward home |
| Flee or panic near enemies | Not in the GDD | Proposal only |

## Companions and service robots

| Behaviour | Source | Building blocks |
| --- | --- | --- |
| Dog: accompany exploration or stay home; later bring animals home; commands and training **Open** | Canon: CORE → Dog assignment, World & companions | Follow (not blocking the player, catch-up teleport), stay, herd |
| Dog: modest combat help, safe backpack | Canon mentions "previous draft, needs review" | Proposal only until reviewed |
| Guide drone: guide, tips, in-world information, comic tech support; reports the raid result in the morning | Canon: CORE → Premise, World & companions | Flying follow, look-at points of interest, barks; dialogue is not AI |
| Drone marks where a stolen animal was taken | Idea: IDEAS → raids | Not AI: a map marker |
| Cat: raises sanity, hisses to warn of sneakers | Idea: IDEAS → Extras | Perception of sneakers near home |
| Service robots: trading, deposits, daily box; reactions and expressions | Canon: CORE → Farm, food & services; FARM → Trading service | Stationary: an animation/state driver, not a tree |
| Collector robot gathers animal drops | Canon (later): FARM → Animal products | Worker loop: find drop → go → pick up → return → deposit |

## Read across the families

- Every family needs **targets that aren't the player**: animals, drops, fences, exits, home. Today `EnemyBrain` only knows `IPlayerTarget`.
- Several behaviours are **world events, not perception**: raid start, midnight retreat, the bell, day phase, Blood Moon. They arrive by MessagePipe through one subscriber per system (SKILL.md §4), not by polling.
- **Areas** matter as much as paths: paddock, meadow, outer zone, river at night, lit areas, breaches.
- Flying agents (raid flyers, drone, Sky Invaders) need a mover that isn't `FollowerEntity`.
