# Core Gameplay

High-level design of aipocalypse. Details live in topic files as they're written; unvetted ideas in [IDEAS.md](IDEAS.md).

## Pitch

A comedy first-person farm-survival shooter. A game dev burned out by AI wakes up in the peaceful meadow he dreamed of — which turns out to be a broken sandbox made by an AI. Farm, fish and explore by day; defend your base in the firelight by night.

## Pillars

1. **Cozy days, scary nights** — the contrast is the game. Day is safe-ish and relaxing; night is tense.
2. **From office worker to survivor** — the player starts weak and clumsy and *feels* every improvement.
3. **Everything is a game** — the world is AI-made, so classic games, references and déjà vu show up everywhere. Funny, never too serious.
4. **Your pace** — fight for fast progress, or chill and farm. Only big fights are (almost) forced.

## Premise & tone

- The MC works in games, gets overwhelmed by AI, and quits to become a farmer. The night before leaving, he falls asleep at his desk, a flash from the computer takes him, and he wakes in a remote meadow.
- A cute guide **drone** welcomes him and explains the world — it's secretly the eyes of the **AI Lord** who built this sandbox.
- Tone: comedy, lots of jokes and cross-references, cozy environment; nights a bit scary.

## Core loop

```
DAY (spend hours) → DUSK (decide) → NIGHT (fight or sleep) → MORNING (rewards, repeat)
        ↑                                                          |
        └───── stronger stats, more wood, better defenses ─────────┘
Every 5–10 nights: BIG FIGHT (prepare or pay the Lord's Wish List)
```

| Scale | What the player does |
|---|---|
| Seconds | Shoot, melee, chop, dodge, stay in the light |
| One day | Spend day hours on activities, prepare for the night, choose fight or sleep |
| One cycle (5–10 days) | Build up for the next big fight |
| Long term | 30+ days to reach an ending; the game never ends — difficulty keeps scaling |

## Day

- The day is a **clock of hours**. Activities cost hours; walking is free. When the hours run out, night comes. Upgrades make activities faster.
- **Activities:** light farming (plant, water, harvest; chickens, cows, ducks), fishing, chopping trees, exploring areas, building and repairing.
- **Minigames live inside activities**, never in arcade boxes. They're short surprise interruptions, e.g. moles pop up while checking crops (whack-a-mole), fish jump at hanging bait (shoot them).
- **Daytime dangers** exist but are light: snakes, predators far from base.
- **Omens** during the day hint at tonight's threat (e.g. a green-flickering sky means invaders).

## Night

- **Light the beacon** at the base = fight tonight's waves. Don't light it = sleep safely and recover.
- **Small nights** (optional): faster XP, coins and loot, but risky.
- **Big fights** (every 5–10 nights, harder each time): (almost) unavoidable. The AI Lord's **Wish List** — items gathered during the day — can delay the fight or, with luck, skip it for partial rewards. No skipping twice in a row.
- **Night types** come from enemy themes (zombie, robot, dino, Sky Invaders…). Each has a small-night version and a big-fight boss version.
- **Light is safety.** Inside firelight you're safe. In the dark, sanity drains and warnings escalate; go too far and a hidden terror kills you instantly. Glowing loot in the dark tempts greedy players.

## Player

- **Survival meters:** HP, Hunger, Stamina (sprinting, fighting), Sanity.
- **Growth (XP → points):** Fitness, Gunsmanship, Luck.
- **Starts weak:** heavy recoil, frequent gun jams (clear them with a quick timed input), slow chopping. Growth makes all of it better.
- **Sanity:** low sanity makes the player "dizzy" — vignette, shorter vision, missing HUD. Drained by darkness and night after night of fighting; restored by sleep, food and pets.
- **Luck works like karma:** good actions raise it, bad ones lower it. It affects loot, crits, fishing, the lottery and jam rate.

## Combat & enemies

- Melee and guns, first person. Tools double as melee weapons (axe).
- Enemies from many different packs, mixed on purpose — the mess hints that the world is AI-made.
- Every enemy = **behaviour archetype** (rusher, tank, ranged, flyer, swarm, sneaker) + **skin**. New enemies are data, not code.
- Defend the base with fences, barricades and your dog.

## World

- Fixed, compact map with the **base in the center**; river, forest, meadow, cave and more around it.
- **Weather + day/night** change gameplay, e.g. rain helps fishing but puts out fires faster; fog shortens vision.
- Mostly alone; a few non-human NPCs later.
- Always first person.

## Companions

- **Drone:** guide, tips and in-world UI; comic tech-support voice; secretly the villain's eyes.
- **Dog:** fights a little; can be upgraded.
- **Cat:** raises sanity and luck; hisses to warn of sneaky enemies.

## Resources

- **Rule: resources are spent directly, like money — no recipes.**
- **Coins**, **wood** (beacon/campfire fuel, building, repairs; trees regrow), **food** (hunger, Wish List).
- Where to buy and sell: TBD.

## Failure

- **Die on a small night:** wake at base, lose that night's loot and some coins, next day starts with fewer hours and lower sanity. XP, buildings and upgrades are kept.
- **Lose a big fight:** go back to the morning of the last big fight survived and prepare again.

## Endings

- **Escape:** beat the grand trial (a rogue-lite run, designed later). Wake at your desk and discover the sandbox is the game you were making — AI took over. Delete it, fix it, leave it, or go back in.
- **Secret:** win the lottery at an extremely rare chance and instantly win.
- Otherwise the game continues without end.

## Not decided yet

Economy (shops, selling), grand trial details, a "stay forever" ending, key fragments to unlock the trial, intro setup.

## Inspirations

Village 51, Chop Chop Inc., Escape from Duckov, How to Fish, Dave the Diver, Swamp Attack 1 & 2, Megabonk.
