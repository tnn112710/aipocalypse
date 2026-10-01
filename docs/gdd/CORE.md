# Core Gameplay

High-level design of aipocalypse. Details live in topic files as they're written; unvetted ideas in [IDEAS.md](IDEAS.md).

## Pitch

A comedy first-person survival shooter. A game dev burned out by AI wakes up in the peaceful meadow he dreamed of — which turns out to be a broken sandbox made by an AI. By day, go out to scavenge, fish and fight, and get home before dark. By night, light the fire and fight whatever it attracts — or put it out and pretend nobody's home.

## Pillars

1. **Every bullet counts** — shooting is the star, but ammo is limited. Save it, earn it, and fall back on melee when it runs out.
2. **Go out, get home** — collecting and fishing happen out in the world, and the sun is the timer. One more fish, or home before dark?
3. **Lights on or lights off** — every night you choose to fight or hide. You can rest, but you can't hide forever.
4. **From office worker to survivor** — the player starts weak and clumsy and *feels* every improvement. The longer you survive, the stronger you get.
5. **Everything is a game** — the world is AI-made, so classic games, references and déjà vu show up everywhere. Funny, never too serious.

**Core experiences:** gun shooting (melee when out of ammo), fishing (the fun, chill part), item collecting (food and materials found while exploring). Each one feeds the others.

## Premise & tone

- The MC works in games, gets overwhelmed by AI, and quits to become a farmer. The night before leaving, he falls asleep at his desk, a flash from the computer takes him, and he wakes in a remote meadow.
- A cute guide **drone** welcomes him and explains the world — it's secretly the eyes of the **AI Lord** who built this sandbox.
- Tone: comedy, lots of jokes and cross-references; cozy at home, risky out in the world, a bit scary at night.

## Core loop

```
MORNING (signals, plan) → DAY (go out: collect, fish, fight) → DUSK (get home) → NIGHT (fire on: fight / fire out: hide)
   ↑                                                                                            |
   └────────────── loot, XP, upgrades → longer trips, bigger fires, stronger player ─────────────┘
Every 10th night: BIG FIGHT (the fire can't be put out)
```

| Scale | What the player does |
| --- | --- |
| Seconds | Shoot, melee, loot, fish, dodge, stay in the light |
| One day | Go out and get back before dark; choose the fire for the night |
| One cycle (10 days) | Build up for the next big fight |
| Long term | Survive as many days as possible and keep getting stronger; 30+ days to reach an ending; the game never ends — difficulty keeps scaling |

### Loop rules

1. **The sun is the timer** — time passes in real time, at base too. Be home before dark.
2. **The three activities feed each other** — fishing and collecting pay for ammo; shooting protects your trips and drops loot. "Every bullet is a fish you caught."
3. **Ammo is limited** — every shot costs something; melee is the fallback.
4. **The fire sets the night** — fire out: hide and skip; small fire: small night; bonfire: bigger wave, better loot.
5. **You can rest, but you can't hide forever** — skipping nights in a row invites raids on the base.
6. **Light keeps the Dark away, not the monsters** — the fire protects you from the Dark but attracts enemies.
7. **Big fights are fixed checkpoints** — every 10th night, harder each time; they can't be delayed or skipped.
8. **Death costs loot, never the run** — XP, buildings and upgrades are kept.
9. **Upgrades make activities faster** — growth means doing more per day.

## Day

- **Time passes gradually in real time**, also at base. Dusk warns you; get home before dark. Activities and walking take real time; upgrades make activities faster.
- **Activities:** exploring and collecting (food, materials, loot), fishing, fighting out in the world, chopping trees (wood fuels the fire), building and repairing, a tiny farm (small garden, a few animals).
- **Minigames live inside activities**, never in arcade boxes. They're short surprise interruptions, e.g. moles pop up while checking crops (whack-a-mole), fish jump at hanging bait (shoot them).
- **Daytime dangers** are out in the world: snakes, predators, enemies far from base. The base is never attacked during the day.
- **Signals:** symbols on the ground in front of the house show what may come tonight if the fire is lit, the skip streak, and the countdown to the next big fight.

## Night

At dusk, the player chooses the fire:

| Fire | Night |
| --- | --- |
| **Out** | Go to bed in the tent/house and skip the night. Pretend nobody's home. |
| **Small fire** | A small wave comes to the base. Normal loot and XP. |
| **Bonfire** | More wood, more torches lit. A bigger wave, better loot. |

- **Skip streak:** the first skip is free; a second in a row brings thieves who take some stored food and items; from the third on, raiders damage fences and buildings. Fighting resets the streak. Fences, scarecrows, the dog and lamps reduce raids.
- **Big fights** (every 10th night, visible countdown, harder each time): the AI Lord relights the fire — no hiding, no delaying.
- **Night types** come from enemy themes (zombie, robot, dino, Sky Invaders…). Each has a small-night version and a big-fight boss version.
- **Light vs the Dark:** inside the light you're safe from the Dark (not from monsters). In the dark, sanity drains and warnings escalate; go too far and a hidden terror kills you instantly. Glowing loot in the dark tempts greedy players.

## Player

- **Survival meters:** HP, Hunger, Stamina (sprinting, fighting), Sanity.
- **Growth (XP → points):** Fitness, Gunsmanship, Luck.
- **Starts weak:** heavy recoil, frequent gun jams (clear them with a quick timed input), slow chopping. Growth makes all of it better.
- **Sanity:** low sanity makes the player "dizzy" — vignette, shorter vision, missing HUD. Drained by darkness; restored by sleep, food and pets.
- **Luck works like karma:** good actions raise it, bad ones lower it. It affects loot, crits, fishing, the lottery and jam rate.

## Combat & enemies

- **Guns are the star; ammo is limited.** Save it, buy or craft more (see Resources); when it runs out, fight with melee. Tools double as melee weapons (axe).
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

- **Coins**, **wood** (fuel for the night fire and torches, building, repairs; trees regrow), **food** (hunger), **materials**, **ammo**.
- Crafting, selling and buying: TBD. Ammo will be buyable and craftable.

## Failure

- **Death never ends the run.** XP, buildings and upgrades are always kept.
- **Die anywhere** (out in the world or on any night): wake at base and lose 50–70% of your loot — a share of everything, stored items included. Part of it is hidden somewhere on the map and can be partly recovered. Upgrades reduce the loss.
- **Lose a big fight:** the same loot loss, and the clock rewinds a few days (start with 3). You get time to prepare, but you still have to fight it.

## Endings

- **Escape:** beat the grand trial (a rogue-lite run, designed later). Wake at your desk and discover the sandbox is the game you were making — AI took over. Delete it, fix it, leave it, or go back in.
- **Secret:** win the lottery at an extremely rare chance and instantly win.
- Otherwise the game continues without end.

## Not decided yet

Crafting and economy (shops, selling, buying ammo), how lost loot is recovered, grand trial details, a "stay forever" ending, key fragments to unlock the trial, intro setup.

## Inspirations

| Game | What we take | What we leave |
| --- | --- | --- |
| Escape from Duckov | Go out, loot, get home; limited ammo with melee fallback; a real-time clock that runs at base too; fishing; death costs loot, not the run | Top-down view; separate raid maps |
| Dome Keeper | Go-out ↔ defend rhythm with a timer; systems that feed each other; upgrades you can feel | Mining; 2D |
| Darkwood | Home before dark; light draws enemies, hiding avoids them | Grim horror tone |
| Village 51 | Farming + base defense + first-person shooting in one game | — |
| Chop Chop Inc. | Chopping trees that regrow; "desk worker becomes strong" humor | Deep crafting and production lines |
| How to Fish | Comedy fishing with guns (gun fishing); stranded somewhere, working your way home | — |
| Megabonk | Grand trial: run-and-gun levels with card picks on level-up | Auto-attack (we keep manual aiming) |
