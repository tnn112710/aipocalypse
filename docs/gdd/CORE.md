# Core Gameplay

High-level design of aipocalypse. Details live in topic files as they're written; unvetted ideas in [IDEAS.md](IDEAS.md).

## Pitch

A first-person extraction shooter with base defense. A game dev burned out by AI wakes up in the peaceful meadow he dreamed of — which turns out to be a broken sandbox made by an AI. By day, head out to loot, fish and fight, and bring it all home before dark: loot only counts once it's home. By night, light the fire and defend the base against whatever it attracts — or put it out and pretend nobody's home.

## Pillars

1. **Every bullet counts** — shooting is the star, but ammo is limited. Every bullet you take on a trip is one you won't have for tonight. Fall back on melee when it runs out.
2. **Bring it home** — every day trip is a raid: what you carry is at risk until you're home, and the sun is the timer. One more crate, or home before dark?
3. **Lights on or lights off** — every night you choose to defend the base or hide. You can rest, but you can't hide forever.
4. **From office worker to survivor** — the player starts weak and clumsy and *feels* every improvement. The longer you survive, the stronger you get.
5. **Everything is a game** — the world is AI-made, so classic games, references and déjà vu show up everywhere. Funny, never too serious.

**Core experiences:** gun shooting (melee when out of ammo), looting and extracting (search, carry, get it home), defending the base at night, and fishing and chopping (the safe, chill part near home). Each one feeds the others.

## Premise & tone

- The MC works in games, gets overwhelmed by AI, and quits to become a farmer. The night before leaving, he falls asleep at his desk, a flash from the computer takes him, and he wakes in a remote meadow.
- A cute guide **drone** welcomes him and explains the world — it's secretly the eyes of the **AI Lord** who built this sandbox.
- Tone: lots of jokes and cross-references; cozy at home, risky out in the world, a bit scary at night.

## Core loop

```text
MORNING (signals, loadout) → DAY (raid: loot, fish, fight) → DUSK (get home = extract) → NIGHT (fire on: defend / fire out: hide)
   ↑                                                                                             |
   └── extracted loot, XP, perk cards → better gear, stronger base, longer raids, bigger fires ──┘
Every 10th night: BIG FIGHT (the fire can't be put out)
```

| Scale | What the player does |
| --- | --- |
| Seconds | Shoot, melee, loot, fish, dodge, stay in the light |
| One day | Pick a loadout, raid, get home before dark; choose the fire for the night |
| One cycle (10 days) | Build up gear and base for the next big fight; stack perk cards before it |
| Long term | Survive as many days as possible and keep getting stronger; 30+ days to reach an ending; the game never ends — difficulty keeps scaling |

### Loop rules

1. **Home is the hub, biomes are the raids** — across the river, up to 3 portals open each day, out of up to 6 hand-made biomes. Pick one; the portal is one-way, so you find an exit inside to get home. What you carry can be lost until then; the stash at home is safe.
2. **Play safe or cross the river** — fishing and chopping around the house are safe; the portals across the river are where the danger and the loot are.
3. **The sun is the timer** — time passes in real time, at base too. Be home before dark.
4. **Everything feeds everything** — loot and fish pay for ammo; shooting protects your raids and drops loot; wood fuels the night. "Every bullet is a fish you caught."
5. **Ammo is limited** — every shot costs something, out on a raid or defending at night; melee is the fallback.
6. **The fire sets the night** — fire out: hide and skip; small fire: small night; bonfire: bigger wave, better loot.
7. **You can rest, but you can't hide forever** — skipping nights in a row invites raids on the base.
8. **Light keeps the Dark away, not the monsters** — the fire protects you from the Dark but attracts enemies.
9. **Big fights are fixed checkpoints** — every 10th night, harder each time; they can't be delayed or skipped.
10. **Death costs loot, never the run** — lost loot drops in a grave you get one chance to recover; XP, buildings and upgrades are kept.
11. **Upgrades make activities faster** — growth means doing more per day.
12. **Fights pay in perks** — every night fight ends with a perk card pick; skipped nights give none.

## Day

- **Time passes gradually in real time**, also at base. Dusk warns you; get home before dark. Activities and walking take real time; upgrades make activities faster.
- **Raids:** pick a loadout from the stash, pick a portal, loot what you can carry and find an exit home.
- **Portal biomes:** each biome is a hand-made map with its own lootables, enemies and easter eggs; what's in it rolls on every visit. The offer rerolls when you sleep, and the drone scans each portal first (biome, weather, threat, relic).
- **Activities:** exploring and looting (food, materials, gear), fishing, fighting out in the world, chopping trees (wood fuels the fire), building and repairing.
- **Minigames live inside activities**, never in arcade boxes, e.g. fish jump at hanging bait (shoot them).
- **Daytime dangers** are out in the world: snakes, predators, enemies far from base. The base is never attacked during the day.
- **Signals:** symbols on the ground in front of the house show what may come tonight if the fire is lit, the skip streak, and the countdown to the next big fight.

## Night

At dusk, the player chooses the fire:

| Fire | Night |
| --- | --- |
| **Out** | Go to bed in the tent/house and skip the night. Pretend nobody's home. |
| **Small fire** | A small wave comes to the base. Normal loot and XP, plus a perk card. |
| **Bonfire** | More wood, more torches lit. A bigger wave, better loot, plus a perk card. |

- **Lose a night defense** and the base takes damage: fences, defenses and buildings need materials to repair. The stash stays safe.
- **Skip streak:** the first skip is free; a second in a row brings thieves who take some stored food and items; from the third on, raiders damage fences and buildings. Fighting resets the streak. Fences, scarecrows, the dog and lamps reduce raids.
- **Stay out:** still in a biome at night? The base isn't attacked, but the risk is on you. It doesn't count as a skip, but it costs your sleep, so sanity doesn't recover.
- **Big fights** (every 10th night, visible countdown, harder each time): the AI Lord relights the fire — no hiding, no delaying. The biomes shut down at dusk and the drone sends anyone still inside home.
- **Night types** come from enemy themes (zombie, robot, dino, Sky Invaders…). Each has a small-night version and a big-fight boss version.
- **Light vs the Dark:** inside the light you're safe from the Dark (not from monsters). In the dark, sanity drains and warnings escalate; go too far and a hidden terror kills you instantly. Glowing loot in the dark tempts greedy players.

## Player

- **Survival meters:** HP, Hunger, Stamina (sprinting, fighting), Sanity.
- **Growth (XP → skill tree, permanent):** Fitness, Gunsmanship, Luck.
- **Perk cards:** after every night fight, pick 1 of 3 cards (max 1 reroll). Small/bonfire night cards are temporary and last 3–5 days; the big-fight card is permanent. XP is long-term growth; cards are short-term boosts that change each cycle.
- **Starts weak:** heavy recoil, frequent gun jams (clear them with a quick timed input), slow chopping. Growth makes all of it better.
- **Sanity:** low sanity makes the player "dizzy" — vignette, shorter vision, missing HUD. Drained by darkness; restored by sleep, food and pets.
- **Luck** affects loot, crits, fishing, the lottery and jam rate.

## Combat & enemies

- **Guns are the star; ammo is limited.** Save it, buy or craft more (see Resources); when it runs out, fight with melee. Tools double as melee weapons (axe).
- Enemies from many different packs, mixed on purpose — the mess hints that the world is AI-made.
- Every enemy = **behaviour archetype** (rusher, tank, ranged, flyer, swarm, sneaker) + **skin**. New enemies are data, not code.
- Defend the base with fences, barricades and your dog.

## World

- **Home map:** small and fixed — the house on a ridge above the sea, a meadow, and a river in front. Safe by day: fishing, chopping, building, and the night arena.
- **Beyond the river:** 3 fixed portal spots lead to the day's biomes (see Day).
- **Weather + day/night** change gameplay, e.g. rain helps fishing but puts out fires faster; fog shortens vision.
- Mostly alone; a few non-human NPCs later.
- Always first person.

## Companions

- **Drone:** guide, tips and in-world UI; comic tech-support voice; secretly the villain's eyes.
- **Dog:** fights a little; can be upgraded. Its backpack keeps items safe when you die.
- **Cat:** raises sanity and luck; hisses to warn of sneaky enemies.

## Resources

- **Coins**, **wood** (fuel for the night fire and torches, building, repairs; trees regrow), **food** (hunger), **materials**, **ammo**, **gear**.
- **Stash** at home: everything you've brought back, safe from death and lost fights.
- Crafting, selling and buying: TBD. Ammo will be buyable and craftable.

## Failure

- **Death never ends the run.** XP, buildings, upgrades and permanent perk cards are always kept.
- **Die anywhere** (out in the world or on any night): wake at base and lose 50–70% of your loot. The lost share drops in a grave where you died; you get one chance to recover it — die again first, or sleep before you reach a grave in a biome, and it's gone. The dog's backpack and equipped melee are always kept. Upgrades reduce the loss.
- **Lose a big fight:** the same loot loss, and the clock rewinds a few days (start with 3). You get time to prepare, but you still have to fight it.

## Endings

- **Escape:** find the relics hidden in the biomes to open the grand trial portal, then beat the trial (a rogue-lite run, designed later). Wake at your desk and discover the sandbox is the game you were making — AI took over. Delete it, fix it, leave it, or go back in.
- **Secret:** win the lottery at an extremely rare chance and instantly win.
- Otherwise the game continues without end.

## Not decided yet

Crafting and economy (shops, selling, buying ammo), what the 50–70% loss counts (only what you carried, or stored items too), whether skip-streak thieves can take stash items, perk card details (duration rules, duplicates, separate permanent pool, reroll per pick or per cycle, whether temporary cards survive death), grand trial details, a "stay forever" ending, intro setup.

## Inspirations

| Game | What we take | What we leave |
| --- | --- | --- |
| Escape from Duckov | The extraction loop: a home hub, separate raid maps, find an exit to get home; a grave to recover lost loot; the dog's backpack as a safe slot; limited ammo with melee fallback; a real-time clock that runs at base too; fishing; death costs loot, not the run | Top-down view |
| Darkwood | Home before dark; light draws enemies, hiding avoids them | Grim horror tone |
| Village 51 | Base defense + first-person shooting in one game | Farming as a core activity |
| Chop Chop Inc. | Chopping trees that regrow; "desk worker becomes strong" humor | Deep crafting and production lines |
| How to Fish | fishing with guns (gun fishing); stranded somewhere, working your way home | — |
| Megabonk | Grand trial: run-and-gun levels with card picks on level-up | Auto-attack (we keep manual aiming) |
| Cult of the Lamb | Temporary card buffs that make each stretch play differently | Cards lost all at once at the end of a run |
