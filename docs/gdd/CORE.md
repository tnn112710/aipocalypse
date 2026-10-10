# Core Gameplay

High-level design of aipocalypse. Details live in topic files as they're written; unvetted ideas in [IDEAS.md](IDEAS.md).

## Pitch

A first-person extraction shooter with base defense. A game dev burned out by AI wakes up in the peaceful meadow he dreamed of — which turns out to be a broken sandbox made by an AI. By day, head out to loot, fish and fight, and bring it all home before dark: loot only counts once it's home. By night, light the fire and defend the base against whatever it attracts — or put it out, rest, and pretend nobody's home. Fighting is never forced: the cozy side is a full way to play.

## Pillars

1. **Every bullet counts** — shooting is the star, but ammo is limited. Every bullet you take on a trip is one you won't have for tonight. Fall back on melee when it runs out.
2. **Bring it home** — every day trip is a raid: what you carry is at risk until you're home, and the sun is the timer. One more crate, or home before dark?
3. **Lights on or lights off** — every night you choose: light the fire to fight for loot and perks, or put it out and rest. The base is only attacked when you invite it.
4. **From office worker to survivor** — the player starts weak and clumsy and *feels* every improvement. The longer you survive, the stronger you get.
5. **Everything is a game** — the world is AI-made, so classic games, references and déjà vu show up everywhere. Funny, never too serious.

**Core experiences:** gun shooting (melee when out of ammo), looting and extracting (search, carry, get it home), defending the base at night (opt-in), and fishing and chopping (the safe, chill part near home). Each one feeds the others.

## Premise & tone

- The MC works in games, gets overwhelmed by AI, and quits to become a farmer. The night before leaving, he falls asleep at his desk, a flash from the computer takes him, and he wakes in a remote meadow.
- A cute guide **drone** welcomes him and explains the world — it's secretly the eyes of the **AI Lord** who built this sandbox.
- Tone: lots of jokes and cross-references; cozy at home, risky out in the world, a bit scary at night.

## Core loop

```text
MORNING (signals, loadout) → DAY (raid: loot, fish, fight) → DUSK (get home = extract) → NIGHT (fire on: defend / fire out: hide)
   ↑                                                                                             |
   └── extracted loot, XP, perk cards → better gear, stronger base, longer raids, bigger fires ──┘
```

| Scale | What the player does |
| --- | --- |
| Seconds | Shoot, melee, loot, fish, dodge, stay in the light |
| One day | Pick a loadout, raid, get home before dark; choose the fire for the night |
| Long term | Survive as many days as possible and keep getting stronger; 30+ days to reach an ending; the game never ends — difficulty keeps scaling |

### Loop rules

1. **Home is the hub, biomes are the raids** — across the river, up to 3 portals open each day, out of up to 6 hand-made biomes. Pick one; the portal is one-way, so you find an exit inside to get home. What you carry can be lost until then; the stash at home is safe.
2. **Play safe or cross the river** — fishing and chopping around the house are safe; the portals across the river are where the danger and the loot are.
3. **The sun is the timer** — time passes in real time, at base too. Be home before dark.
4. **Everything feeds everything** — loot and fish pay for ammo; shooting protects your raids and drops loot; wood fuels the night. "Every bullet is a fish you caught."
5. **Ammo is limited** — every shot costs something, out on a raid or defending at night; melee is the fallback.
6. **The fire sets the night** — fire out: hide and skip; small fire: small night; bonfire: bigger wave, better loot.
7. **Fighting is opt-in** — the base is only attacked when you light the fire. Rest, fish and chop for as long as you like.
8. **Light keeps the Dark away, not the monsters** — the fire protects you from the Dark but attracts enemies.
9. **Death costs loot, never the run** — lost loot drops in a grave you get one chance to recover; XP, buildings and upgrades are kept.
10. **Upgrades make activities faster** — growth means doing more per day.
11. **Fights pay in perks** — every night fight ends with a perk card pick; skipped nights give none.

## Day

- **Time passes gradually in real time**, also at base. Dusk warns you; get home before dark. Activities and walking take real time; upgrades make activities faster.
- **Raids:** pick a loadout from the stash, pick a portal, loot what you can carry and find an exit home.
- **Portal biomes:** each biome is a hand-made map with its own lootables, enemies and easter eggs; what's in it rolls on every visit. The offer rerolls when you sleep, and the drone scans each portal first (biome, weather, threat, relic).
- **Activities:** exploring and looting (food, materials, gear), fishing, fighting out in the world, chopping trees (wood fuels the fire), building and repairing.
- **Minigames live inside activities**, never in arcade boxes, e.g. fish jump at hanging bait (shoot them).
- **Daytime dangers** are out in the world: snakes, predators, enemies far from base. The base is never attacked during the day.
- **Signals:** symbols on the ground in front of the house show what may come tonight if the fire is lit.

## Night

At dusk, the player chooses the fire:

| Fire | Night |
| --- | --- |
| **Out** | Go to bed in the tent/house and skip the night. Pretend nobody's home. |
| **Small fire** | A small wave comes to the base. Normal loot and XP, plus a perk card. |
| **Bonfire** | More wood, more torches lit. A bigger wave, better loot, plus a perk card. |

- **Lose a night defense** and the base takes damage: fences, defenses and buildings need materials to repair. The stash stays safe.
- **Stay out:** still in a biome at night? The base isn't attacked, but the risk is on you. It costs your sleep, so sanity doesn't recover.
- **Night types** come from enemy themes (zombie, robot, dino, Sky Invaders…). Each has a small-fire and a bonfire version.
- **Light vs the Dark:** inside the light you're safe from the Dark (not from monsters). In the dark, sanity drains and warnings escalate; go too far and a hidden terror kills you instantly. Glowing loot in the dark tempts greedy players.

## Player

- **Survival meters:** HP, Hunger, Stamina (sprinting, fighting), Sanity.
- **Growth (XP → skill tree, permanent):** Fitness, Gunsmanship, Luck.
- **Perk cards:** after every night fight, pick 1 of 3 cards (max 1 reroll). Cards are temporary and last 3–5 days. XP is long-term growth; cards are short-term boosts that keep changing.
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

- **Death never ends the run.** XP, buildings and upgrades are always kept.
- **Die anywhere** (out in the world or on any night): wake at base and lose 50–70% of your loot. The lost share drops in a grave where you died; you get one chance to recover it — die again first, or sleep before you reach a grave in a biome, and it's gone. The dog's backpack and equipped melee are always kept. Upgrades reduce the loss.

## Endings

- **Escape:** find the relics hidden in the biomes to open the grand trial portal, then beat the trial (a rogue-lite run, designed later). Wake at your desk and discover the sandbox is the game you were making — AI took over. Delete it, fix it, leave it, or go back in.
- **Secret:** win the lottery at an extremely rare chance and instantly win.
- Otherwise the game continues without end.

## Not decided yet

Crafting and economy (shops, selling, buying ammo), what the 50–70% loss counts (only what you carried, or stored items too), where bosses live now that big fights are gone, perk card details (duration rules, duplicates, any permanent cards, reroll rules, whether cards survive death), grand trial details, a "stay forever" ending, intro setup.

## Inspirations

| Game | What we take | What we leave |
| --- | --- | --- |
| Escape from Duckov | The extraction loop: a home hub, separate raid maps, find an exit to get home; a grave to recover lost loot; the dog's backpack as a safe slot; limited ammo with melee fallback; a real-time clock that runs at base too; fishing; death costs loot, not the run | Top-down view |
| Village 51 | Base defense + first-person shooting in one game | Farming as a core activity |
| Chop Chop Inc. | Chopping trees that regrow; "desk worker becomes strong" humor | Deep crafting and production lines |
| Megabonk | Grand trial: run-and-gun levels with card picks on level-up | Auto-attack (we keep manual aiming) |
